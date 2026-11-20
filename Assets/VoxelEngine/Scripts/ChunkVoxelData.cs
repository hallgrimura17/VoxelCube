using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "ChunkVoxelData", menuName = "Level/ChunkVoxelData", order = 1)]
public class ChunkVoxelData : ScriptableObject
{
    public Vector3Int Offset;
    public NativeArray<int> Density;
    public NativeArray<int> Material;
    public int HelperVoxelArrayLength;

    #if UNITY_EDITOR
    public static void SaveChunk(TerrainChunk terrainChunk)
    {
        if (!terrainChunk.IsInitialized) return;
        var path = $"Assets/Database/ChunkData/{terrainChunk.GetFileName}.asset";
        ChunkData thisInstance;
        if (!AssetDatabase.AssetPathExists(path))
        {
            thisInstance = CreateInstance<ChunkData>();
            AssetDatabase.CreateAsset(thisInstance, path);
        }
        else
        {
            thisInstance = AssetDatabase.LoadAssetAtPath<ChunkData>(path);
        }

        thisInstance.Density = terrainChunk.Density.ToArray();
        thisInstance.Material = terrainChunk.Material.ToArray();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"VoxelData saved to: {path}");
    }
    #endif
}
[CreateAssetMenu(fileName = "SavedChunkVoxelDatabase", menuName = "Level/ChunkVoxelDatabase", order = 1)]
public class SavedChunkVoxelDatabase : ScriptableObject
{
    public List<ChunkVoxelData> ChunkVoxelData;
    public Dictionary<Vector3Int, ChunkVoxelData> ChunkLookup;

    public ChunkVoxelData GetChunkVoxelData(Vector3Int position)
    {
        position.z = 0;

        if(ChunkLookup == null)
        {
            FillChunkLookup();
        }
        if(ChunkLookup.TryGetValue(position, out var voxelData))
        {
            voxelData = new ChunkVoxelData();
        }
        return voxelData; //I dont think I should use this since its just a hassle to fill in chunk this way, might even lag
    }

    private void FillChunkLookup()
    {
        if(ChunkLookup == null)
            ChunkLookup = new Dictionary<Vector3Int, ChunkVoxelData>();
        else
            ChunkLookup.Clear();
        foreach(var VoxelData in ChunkVoxelData)
        {
            ChunkLookup.Add(VoxelData.Offset, VoxelData);
        }
    }
}