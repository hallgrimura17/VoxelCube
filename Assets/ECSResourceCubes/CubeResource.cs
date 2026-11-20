using Unity.Entities;
using Unity.Mathematics;

public struct CubeResource : IComponentData
{
    public int Tier;
}

public struct CubeResourceAttractionTarget : IComponentData
{
    public float3 Position;
    public float RangeSquared;
    public float PullSpeed;
}
