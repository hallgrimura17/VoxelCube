using Unity.Entities;
using Unity.Mathematics;

public struct CubeResource : IComponentData
{
    public int Tier;
}

public struct CubeResourceAttractionTarget : IComponentData
{
    public Entity TargetEntity;
    public float3 Position;
    public float RangeSquared;
    public float PullSpeed;
}

public struct CubeVaporisingBeamTarget : IComponentData
{
    public float3 Position;
    public float Radius;
    public float BaseHeatDuration;
    public float AdditionalHeatDurationPerTier;
    public float EmissionIntensity;
    public float HeatDecayDuration;
    public int Active;
}

public struct CubeResourceVaporisingHeat : IComponentData
{
    public float Progress;
}

public struct CubeResourceVaporisingHeatActive : IComponentData, IEnableableComponent
{
}
