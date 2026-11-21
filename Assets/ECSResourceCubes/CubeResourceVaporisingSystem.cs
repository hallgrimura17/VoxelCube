using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public partial class CubeResourceVaporisingSystem : SystemBase
{
    private NativeQueue<VaporisedResource> vaporisedResources;
    private EntityQuery heatedResources;
    public float HottestHeatProgress { get; private set; }
    public float AverageHeatProgress { get; private set; }

    protected override void OnCreate()
    {
        vaporisedResources = new NativeQueue<VaporisedResource>(Allocator.Persistent);
        heatedResources = GetEntityQuery(ComponentType.ReadOnly<CubeResourceVaporisingHeatActive>(), ComponentType.ReadOnly<CubeResourceVaporisingHeat>());
        RequireForUpdate<CubeVaporisingBeamTarget>();
    }

    protected override void OnUpdate()
    {
        var target = SystemAPI.GetSingleton<CubeVaporisingBeamTarget>();
        if (target.Active == 0 && heatedResources.IsEmpty)
        {
            HottestHeatProgress = 0f;
            AverageHeatProgress = 0f;
            return;
        }
        vaporisedResources.Clear();
        var gameController = GameController.instance;
        if (target.Active != 0)
        {
            Dependency = new CubeResourceVaporisingJob
            {
                Target = target,
                DeltaTime = SystemAPI.Time.DeltaTime,
                BaseReward = gameController.resourceValue * gameController.resourceValueMultiplier,
                VaporisedResources = vaporisedResources.AsParallelWriter()
            }.ScheduleParallel(Dependency);
        }
        else
        {
            Dependency = new CubeResourceHeatDecayJob { DeltaTime = SystemAPI.Time.DeltaTime, Target = target }.ScheduleParallel(Dependency);
        }
        Dependency.Complete();
        var heatProgress = heatedResources.ToComponentDataArray<CubeResourceVaporisingHeat>(Allocator.Temp);
        HottestHeatProgress = 0f;
        var totalHeatProgress = 0f;
        for (var index = 0; index < heatProgress.Length; index++)
        {
            var progress = heatProgress[index].Progress;
            HottestHeatProgress = math.max(HottestHeatProgress, progress);
            totalHeatProgress += progress;
        }
        AverageHeatProgress = heatProgress.Length > 0 ? totalHeatProgress / heatProgress.Length : 0f;
        heatProgress.Dispose();

        var vaporisedCount = 0;
        var averagePosition = float3.zero;
        var averageColor = float4.zero;
        var averageSize = 0f;
        var averageTier = 0f;
        var rewardPopup = CurrencyRewardPopup.Instance;
        while (vaporisedResources.TryDequeue(out var vaporisedResource))
        {
            var rewardPosition = new Vector3(vaporisedResource.Position.x, vaporisedResource.Position.y, vaporisedResource.Position.z);
            rewardPopup.Show(vaporisedResource.Reward, rewardPosition);
            EntityManager.DestroyEntity(vaporisedResource.Entity);
            vaporisedCount++;
            averagePosition += vaporisedResource.Position;
            averageColor += vaporisedResource.Color;
            averageSize += vaporisedResource.Size;
            averageTier += vaporisedResource.Tier;
        }

        vaporisedResources.Clear();
        if (vaporisedCount == 0) return;
        averagePosition /= vaporisedCount;
        averageColor /= vaporisedCount;
        averageSize /= vaporisedCount;
        averageTier /= vaporisedCount;
        var popupPosition = new Vector3(averagePosition.x, averagePosition.y, averagePosition.z);
        Assets.Resources.ParticleManager.instance.EmitParticleEffectWithSizeMultiplierAndColor("Merge", popupPosition, Quaternion.identity, vaporisedCount * 6, averageSize, new Color(averageColor.x, averageColor.y, averageColor.z, 1f));
        Assets.Resources.SoundManager.instance.PlaySoundClipAtLocation(popupPosition, "CubeDissolve", 2f / (1f + averageTier * 0.08f));
    }

    protected override void OnDestroy()
    {
        if (vaporisedResources.IsCreated) vaporisedResources.Dispose();
    }
}

[BurstCompile]
[WithNone(typeof(Prefab))]
[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct CubeResourceVaporisingJob : IJobEntity
{
    public CubeVaporisingBeamTarget Target;
    public float DeltaTime;
    public float BaseReward;
    public NativeQueue<VaporisedResource>.ParallelWriter VaporisedResources;

    public void Execute(Entity entity, in CubeResource resource, in LocalTransform transform, in URPMaterialPropertyBaseColor baseColor, ref CubeResourceVaporisingHeat heat, EnabledRefRW<CubeResourceVaporisingHeatActive> heatActive, ref URPMaterialPropertyEmissionColor emission)
    {
        if (Target.Active == 0)
        {
            CoolResource(Target, DeltaTime, baseColor.Value, ref heat, heatActive, ref emission);
            return;
        }

        var resourceRadius = Target.Radius + transform.Scale * 0.8660254f;
        if (math.distancesq(transform.Position, Target.Position) > resourceRadius * resourceRadius)
        {
            CoolResource(Target, DeltaTime, baseColor.Value, ref heat, heatActive, ref emission);
            return;
        }

        heatActive.ValueRW = true;
        var heatDuration = Target.BaseHeatDuration + resource.Tier * Target.AdditionalHeatDurationPerTier;
        heat.Progress = math.min(1f, heat.Progress + DeltaTime / heatDuration);
        emission.Value = new float4(baseColor.Value.x, baseColor.Value.y, baseColor.Value.z, 1f) * (heat.Progress * Target.EmissionIntensity);
        emission.Value.w = 1f;
        if (heat.Progress < 1f) return;
        var reward = (int)math.round(BaseReward * math.pow(2f, resource.Tier));
        VaporisedResources.Enqueue(new VaporisedResource { Entity = entity, Reward = reward, Position = transform.Position, Color = baseColor.Value, Size = transform.Scale / CubeResourceEntityFactory.ResourceScale, Tier = resource.Tier });
    }

    internal static void CoolResource(CubeVaporisingBeamTarget target, float deltaTime, float4 baseColor, ref CubeResourceVaporisingHeat heat, EnabledRefRW<CubeResourceVaporisingHeatActive> heatActive, ref URPMaterialPropertyEmissionColor emission)
    {
        if (!heatActive.ValueRO) return;
        heat.Progress = math.max(0f, heat.Progress - deltaTime / target.HeatDecayDuration);
        if (heat.Progress <= 0f)
        {
            emission.Value = float4.zero;
            heatActive.ValueRW = false;
            return;
        }
        emission.Value = new float4(baseColor.x, baseColor.y, baseColor.z, 1f) * (heat.Progress * target.EmissionIntensity);
        emission.Value.w = 1f;
    }
}

[BurstCompile]
[WithNone(typeof(Prefab))]
public partial struct CubeResourceHeatDecayJob : IJobEntity
{
    public float DeltaTime;
    public CubeVaporisingBeamTarget Target;

    public void Execute(in URPMaterialPropertyBaseColor baseColor, ref CubeResourceVaporisingHeat heat, EnabledRefRW<CubeResourceVaporisingHeatActive> heatActive, ref URPMaterialPropertyEmissionColor emission)
    {
        CubeResourceVaporisingJob.CoolResource(Target, DeltaTime, baseColor.Value, ref heat, heatActive, ref emission);
    }
}

public struct VaporisedResource
{
    public Entity Entity;
    public int Reward;
    public float3 Position;
    public float4 Color;
    public float Size;
    public int Tier;
}
