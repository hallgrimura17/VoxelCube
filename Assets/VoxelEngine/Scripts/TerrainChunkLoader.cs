using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunkLoader : MonoBehaviour
{
    public TerrainChunkManager TerrainChunkManager;
    public ChunkData DefaultChunkData;

    private void Awake()
    {
        TerrainChunkManager.GetChunkAtPosition(transform.position, true).ChunkData = DefaultChunkData;
        //var holeSize = TerrainChunkManager.HoleSize;
        //for(int x = 0; x < holeSize.x; x++)
        //    for(int y = 0; y < holeSize.y; y++)
        //        for(int z = 0; z < holeSize.z; z++)
        //            TerrainChunkManager.GetChunkAtPosition(transform.position + new Vector3(x - 1, y - 1, z - 1) * TerrainChunkManager.ChunkWorldSize, true).ChunkData = DefaultChunkData;
    }

}
