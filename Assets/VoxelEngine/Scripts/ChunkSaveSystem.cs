using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public class WorldSaveFile
{
    public WorldStateSaveData WorldStateSaveData = new WorldStateSaveData();
    public TerrainChunkSaveData[] Chunks/* = new TerrainChunkSaveData[0]*/;
    public ResourceSaveData[] Resources/* = new ResourceSaveData[0]*/;
}

[Serializable]
public class WorldStateSaveData
{
    public WorldStateSaveData() { }
}

[Serializable]
public class ResourceSaveData
{
    //public Resource type;
    public float3 Position;
    public float4 Rotation;
}

[Serializable]
public class TerrainChunkSaveData
{
    public int3 Position;
    public int[] Density;
    public int[] Material;
}
