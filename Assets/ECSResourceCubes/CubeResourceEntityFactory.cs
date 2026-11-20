using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

public struct CubeResourceSpawnRequest
{
    public float3 Position;
    public quaternion Rotation;
    public float3 InitialVelocity;
}

public static class CubeResourceEntityFactory
{
    private const uint ResourceCollisionCategory = 1u << 0;
    private const uint EnvironmentCollisionCategory = 1u << 1;
    private const int SpawnJobBatchSize = 128;
    private const int TierColorCount = 8;
    internal const float ResourceScale = 0.5f;
    internal const float ResourceMass = 1f;
    private const float ResourceMassMultiplierPerTier = 2f;
    private const string GroundObjectName = "Ground";
    private const string ResourceCeilingObjectName = "ResourceCeiling";
    private const string ResourceWallNamePrefix = "ResourceWall_";
    private static readonly List<Entity> environmentEntities = new();
    private static readonly List<BlobAssetReference<Unity.Physics.Collider>> environmentColliders = new();
    private static BlobAssetReference<Unity.Physics.Collider> resourceCollider;
    private static Entity resourcePrototype;
    private static Entity attractionTarget;
    private static UnityEngine.Material resourceMaterial;
    private static World world;

    internal static void Initialize(UnityEngine.Material cubeMaterial)
    {
        world = World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;
        resourceMaterial = cubeMaterial;
        var physicsMaterial = Unity.Physics.Material.Default;
        physicsMaterial.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
        var resourceFilter = new CollisionFilter { BelongsTo = ResourceCollisionCategory, CollidesWith = ResourceCollisionCategory | EnvironmentCollisionCategory, GroupIndex = 0 };
        resourceCollider = Unity.Physics.BoxCollider.Create(new BoxGeometry { Center = float3.zero, Orientation = quaternion.identity, Size = new float3(1f), BevelRadius = 0f }, resourceFilter, physicsMaterial);
        resourcePrototype = CreatePrototype(entityManager);
        attractionTarget = entityManager.CreateEntity(typeof(CubeResourceAttractionTarget));
        CreateEnvironmentColliders(entityManager);
    }

    internal static void SpawnResources(IReadOnlyList<VoxelModification> modifications, Vector3 spawnOrigin, Vector3 fallbackDirection, Vector3 cubeCenter, float impulseMagnitude, float cubeCenterImpulseMagnitude)
    {
        if (modifications.Count == 0) return;
        var requests = new NativeArray<CubeResourceSpawnRequest>(modifications.Count, Allocator.TempJob);
        for (var index = 0; index < modifications.Count; index++)
        {
            var modification = modifications[index];
            var rotation = UnityEngine.Random.rotation;
            var initialVelocityDirection = (modification.Position - spawnOrigin).normalized;
            if (initialVelocityDirection == Vector3.zero) initialVelocityDirection = fallbackDirection.normalized;
            var cubeCenterDirection = (modification.Position - cubeCenter).normalized;
            var initialVelocity = (initialVelocityDirection * impulseMagnitude + cubeCenterDirection * cubeCenterImpulseMagnitude) / ResourceMass;
            initialVelocity.y = Mathf.Max(0f, initialVelocity.y);
            requests[index] = new CubeResourceSpawnRequest
            {
                Position = ToFloat3(modification.Position),
                Rotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                InitialVelocity = ToFloat3(initialVelocity)
            };
        }

        var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        new CubeResourceSpawnJob
        {
            Prototype = resourcePrototype,
            Requests = requests,
            CommandBuffer = commandBuffer.AsParallelWriter()
        }.Schedule(requests.Length, SpawnJobBatchSize).Complete();
        commandBuffer.Playback(world.EntityManager);
        commandBuffer.Dispose();
        requests.Dispose();
    }

    internal static void SetResourceAttraction(Vector3 destination, float range, float pullSpeed)
    {
        world.EntityManager.SetComponentData(attractionTarget, new CubeResourceAttractionTarget { Position = ToFloat3(destination), RangeSquared = range * range, PullSpeed = pullSpeed });
    }

    internal static float4 GetTierColor(int tier)
    {
        var hue = (tier % TierColorCount) / (float)TierColorCount;
        var color = Color.HSVToRGB(hue, 0.85f, 1f).linear;
        return new float4(color.r, color.g, color.b, 1f);
    }

    internal static float GetResourceScale(int tier, float tierScaleMultiplier) => ResourceScale * math.pow(tierScaleMultiplier, tier);

    internal static PhysicsMass CreateResourceMass(int tier)
    {
        var mass = ResourceMass * math.pow(ResourceMassMultiplierPerTier, tier);
        return PhysicsMass.CreateDynamic(resourceCollider.Value.MassProperties, mass);
    }

    internal static void Shutdown()
    {
        if (world != null && world.IsCreated)
        {
            var entityManager = world.EntityManager;
            entityManager.CompleteAllTrackedJobs();
            var resources = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CubeResource>());
            entityManager.DestroyEntity(resources);
            resources.Dispose();
            if (entityManager.Exists(attractionTarget)) entityManager.DestroyEntity(attractionTarget);
            for (var index = 0; index < environmentEntities.Count; index++)
                if (entityManager.Exists(environmentEntities[index])) entityManager.DestroyEntity(environmentEntities[index]);
            entityManager.CompleteAllTrackedJobs();
        }
        for (var index = 0; index < environmentColliders.Count; index++)
            if (environmentColliders[index].IsCreated) environmentColliders[index].Dispose();
        if (resourceCollider.IsCreated) resourceCollider.Dispose();
        environmentEntities.Clear();
        environmentColliders.Clear();
        resourcePrototype = Entity.Null;
        attractionTarget = Entity.Null;
        resourceCollider = default;
        resourceMaterial = null;
        world = null;
    }

    private static Entity CreatePrototype(EntityManager entityManager)
    {
        var mesh = Resources.GetBuiltinResource<UnityEngine.Mesh>("Cube.fbx");
        var entity = entityManager.CreateEntity();
        entityManager.AddSharedComponent(entity, PhysicsWorldIndex.Default);
        var renderArray = new RenderMeshArray(new[] { resourceMaterial }, new[] { mesh });
        RenderMeshUtility.AddComponents(entity, entityManager, new RenderMeshDescription(ShadowCastingMode.Off, false), renderArray, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, ResourceScale));
        entityManager.AddComponentData(entity, new LocalToWorld { Value = float4x4.identity });
        entityManager.AddComponentData(entity, new URPMaterialPropertyBaseColor { Value = GetTierColor(0) });
        entityManager.AddComponentData(entity, new CubeResource());
        entityManager.AddComponentData(entity, new PhysicsCollider { Value = resourceCollider });
        entityManager.AddComponentData(entity, CreateResourceMass(0));
        entityManager.AddComponentData(entity, PhysicsVelocity.Zero);
        entityManager.AddComponentData(entity, new PhysicsGravityFactor { Value = 1f });
        entityManager.AddComponent<Prefab>(entity);
        return entity;
    }

    private static void CreateEnvironmentColliders(EntityManager entityManager)
    {
        foreach (var boxCollider in Object.FindObjectsByType<UnityEngine.BoxCollider>())
        {
            var objectName = boxCollider.gameObject.name;
            if (objectName == GroundObjectName || objectName == ResourceCeilingObjectName || objectName.StartsWith(ResourceWallNamePrefix)) CreateEnvironmentCollider(entityManager, boxCollider);
        }
    }

    private static void CreateEnvironmentCollider(EntityManager entityManager, UnityEngine.BoxCollider boxCollider)
    {
        var scale = ToFloat3(boxCollider.transform.lossyScale);
        var filter = new CollisionFilter { BelongsTo = EnvironmentCollisionCategory, CollidesWith = ResourceCollisionCategory, GroupIndex = 0 };
        var collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
        {
            Center = ToFloat3(boxCollider.center) * scale,
            Orientation = quaternion.identity,
            Size = ToFloat3(boxCollider.size) * scale,
            BevelRadius = 0f
        }, filter);
        var entity = entityManager.CreateEntity();
        entityManager.AddSharedComponent(entity, PhysicsWorldIndex.Default);
        var rotation = boxCollider.transform.rotation;
        entityManager.AddComponentData(entity, new PhysicsCollider { Value = collider });
        entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(ToFloat3(boxCollider.transform.position), new quaternion(rotation.x, rotation.y, rotation.z, rotation.w), 1f));
        environmentEntities.Add(entity);
        environmentColliders.Add(collider);
    }

    private static float3 ToFloat3(Vector3 value) => new (value.x, value.y, value.z);
}

[BurstCompile]
public struct CubeResourceSpawnJob : IJobParallelFor
{
    public Entity Prototype;
    [ReadOnly] public NativeArray<CubeResourceSpawnRequest> Requests;
    public EntityCommandBuffer.ParallelWriter CommandBuffer;

    /// <summary>Instantiates and positions one resource entity.</summary>
    public void Execute(int index)
    {
        var request = Requests[index];
        var entity = CommandBuffer.Instantiate(index, Prototype);
        CommandBuffer.SetComponent(index, entity, LocalTransform.FromPositionRotationScale(request.Position, request.Rotation, CubeResourceEntityFactory.ResourceScale));
        CommandBuffer.SetComponent(index, entity, new PhysicsVelocity { Linear = request.InitialVelocity, Angular = float3.zero });
    }
}
