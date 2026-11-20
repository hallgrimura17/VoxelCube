using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
public partial struct CubeResourceAttractionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CubeResourceAttractionTarget>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var target = SystemAPI.GetSingleton<CubeResourceAttractionTarget>();
        if (target.RangeSquared <= 0f || target.PullSpeed <= 0f) return;
        state.Dependency = new CubeResourceAttractionJob { Target = target }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithNone(typeof(Prefab))]
public partial struct CubeResourceAttractionJob : IJobEntity
{
    public CubeResourceAttractionTarget Target;

    public void Execute(in CubeResource resource, in LocalTransform transform, ref PhysicsVelocity velocity)
    {
        var offset = Target.Position - transform.Position;
        if (Unity.Mathematics.math.lengthsq(offset) > Target.RangeSquared) return;
        velocity.Linear = Unity.Mathematics.math.normalizesafe(offset) * Target.PullSpeed;
    }
}


public struct CubeResourceMergeCandidate
{
    public Entity EntityA;
    public Entity EntityB;
}

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
public partial class CubeResourceMergeSystem : SystemBase
{
    private const float MergePitchPerTier = 0.08f;
    private readonly List<CubeResourceMergeCandidate> candidates = new();
    private readonly HashSet<Entity> handledEntities = new();

    protected override void OnCreate()
    {
        RequireForUpdate<SimulationSingleton>();
    }

    protected override void OnUpdate()
    {
        var events = new NativeQueue<CubeResourceMergeCandidate>(Allocator.TempJob);
        var simulation = SystemAPI.GetSingleton<SimulationSingleton>();
        Dependency = new CubeResourceMergeCollisionJob
        {
            Resources = SystemAPI.GetComponentLookup<CubeResource>(true),
            Candidates = events.AsParallelWriter()
        }.ScheduleParallel(1, simulation, Dependency);
        Dependency.Complete();

        candidates.Clear();
        while (events.TryDequeue(out var candidate)) candidates.Add(candidate);
        events.Dispose();
        if (candidates.Count == 0) return;

        candidates.Sort(CompareCandidates);
        handledEntities.Clear();
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        for (var index = 0; index < candidates.Count; index++) Merge(candidates[index], commandBuffer);
        commandBuffer.Playback(EntityManager);
        commandBuffer.Dispose();
    }

    private void Merge(CubeResourceMergeCandidate candidate, EntityCommandBuffer commandBuffer)
    {
        var entityA = candidate.EntityA;
        var entityB = candidate.EntityB;
        if (handledEntities.Contains(entityA) || handledEntities.Contains(entityB) || !EntityManager.Exists(entityA) || !EntityManager.Exists(entityB)) return;
        if (!EntityManager.HasComponent<CubeResource>(entityA) || !EntityManager.HasComponent<CubeResource>(entityB)) return;

        var resourceA = EntityManager.GetComponentData<CubeResource>(entityA);
        var resourceB = EntityManager.GetComponentData<CubeResource>(entityB);
        if (resourceA.Tier != resourceB.Tier) return;

        var transformA = EntityManager.GetComponentData<LocalTransform>(entityA);
        var transformB = EntityManager.GetComponentData<LocalTransform>(entityB);
        var velocityA = EntityManager.GetComponentData<PhysicsVelocity>(entityA);
        var velocityB = EntityManager.GetComponentData<PhysicsVelocity>(entityB);
        var survivor = entityA.Index < entityB.Index ? entityA : entityB;
        var consumed = survivor == entityA ? entityB : entityA;
        var resource = survivor == entityA ? resourceA : resourceB;
        resource.Tier++;
        var scale = CubeResourceEntityFactory.GetResourceScale(resource.Tier, GameController.instance.ResourceCubeMergeSizeMultiplier);
        var mergePosition = Unity.Mathematics.math.lerp(transformA.Position, transformB.Position, 0.5f);
        var transform = EntityManager.GetComponentData<LocalTransform>(survivor);
        transform.Position = mergePosition;
        transform.Rotation = Unity.Mathematics.math.slerp(transformA.Rotation, transformB.Rotation, 0.5f);
        transform.Scale = scale;
        EntityManager.SetComponentData(survivor, transform);
        EntityManager.SetComponentData(survivor, resource);
        var tierColor = CubeResourceEntityFactory.GetTierColor(resource.Tier);
        EntityManager.SetComponentData(survivor, new URPMaterialPropertyBaseColor { Value = tierColor });
        EntityManager.SetComponentData(survivor, new PhysicsVelocity
        {
            Linear = (velocityA.Linear + velocityB.Linear) * 0.5f,
            Angular = (velocityA.Angular + velocityB.Angular) * 0.5f
        });
        EntityManager.SetComponentData(survivor, CubeResourceEntityFactory.CreateResourceMass(resource.Tier));
        var mergeWorldPosition = new UnityEngine.Vector3(mergePosition.x, mergePosition.y, mergePosition.z);
        Assets.Resources.SoundManager.instance.PlaySoundClipAtLocation(mergeWorldPosition, "CubeMerge", 2f / (1f + resource.Tier * MergePitchPerTier));
        Assets.Resources.ParticleManager.instance.EmitParticleEffectWithSizeMultiplierAndColor("Merge", mergeWorldPosition, UnityEngine.Quaternion.identity, 1, scale / CubeResourceEntityFactory.ResourceScale, new UnityEngine.Color(tierColor.x, tierColor.y, tierColor.z, tierColor.w));
        handledEntities.Add(survivor);
        handledEntities.Add(consumed);
        commandBuffer.DestroyEntity(consumed);
    }

    private static int CompareCandidates(CubeResourceMergeCandidate left, CubeResourceMergeCandidate right)
    {
        var comparison = left.EntityA.Index.CompareTo(right.EntityA.Index);
        if (comparison != 0) return comparison;
        comparison = left.EntityA.Version.CompareTo(right.EntityA.Version);
        if (comparison != 0) return comparison;
        comparison = left.EntityB.Index.CompareTo(right.EntityB.Index);
        return comparison != 0 ? comparison : left.EntityB.Version.CompareTo(right.EntityB.Version);
    }
}

[BurstCompile]
public struct CubeResourceMergeCollisionJob : ICollisionEventsJob
{
    [ReadOnly] public ComponentLookup<CubeResource> Resources;
    public NativeQueue<CubeResourceMergeCandidate>.ParallelWriter Candidates;

    public void Execute(CollisionEvent collisionEvent)
    {
        var entityA = collisionEvent.EntityA;
        var entityB = collisionEvent.EntityB;
        if (!Resources.HasComponent(entityA) || !Resources.HasComponent(entityB) || Resources[entityA].Tier != Resources[entityB].Tier) return;
        if (entityA.Index > entityB.Index || entityA.Index == entityB.Index && entityA.Version > entityB.Version)
        {
            var temporary = entityA;
            entityA = entityB;
            entityB = temporary;
        }
        Candidates.Enqueue(new CubeResourceMergeCandidate { EntityA = entityA, EntityB = entityB });
    }
}
