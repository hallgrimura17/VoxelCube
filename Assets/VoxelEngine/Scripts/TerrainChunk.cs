using Assets.Extensions;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

[Serializable]
public struct Voxel
{
    public int Density;
    public int Material;
}

[Serializable]
public struct VoxelModification
{
    public bool IsoFlip;
    public int Density;
    public int Material;
    public Vector3 Position;
}

public enum VoxelMaterial
{
    Base = 0,
    Gold = 1,
}

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TerrainChunk : MonoBehaviour
{
    public bool ShowDebug;
    public bool IsLoaded, IsDirty, IsGenerated;
    public Vector3Int Offset = Vector3Int.zero;
    public int Size = 32;
    internal int FieldSize = 33;
    public float CubeSize = 0.5f, WorldSize;
    public int GetFieldLength => (int)Mathf.Pow(FieldSize, 3) + 3;
    public NativeArray<int> Density;
    public NativeArray<int> Material;

    public int GetDensityArrayLength => Density.Length;
    private MeshFilter meshFilter;
    public Mesh GetMesh => meshFilter.sharedMesh;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    public string GetFileName => $"{ChunkData?.Name}";
    public string GetObjectName => $"Chunk_{Offset}";

    public bool IsInitialized => Density != null && Density.IsCreated;

    public ChunkData ChunkData;

    public void Awake()
    {
        FieldSize = Size + 1;
        WorldSize = Size * CubeSize;
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
    }
    public void Start()
    {
        LoadChunkData(ChunkData.Density, ChunkData.Material);
    }

    public void LoadChunkData(int[] densityData, int[] materialData)
    {
        var density = new NativeArray<int>(densityData, Allocator.Persistent);
        var material = new NativeArray<int>(materialData, Allocator.Persistent);
        AssignField(density, material);
        TerrainChunkManager.instance.GenerateMeshForChunk(this, true);
    }

#if UNITY_EDITOR
    public bool SaveMesh, SaveVoxels, LoadMesh;
    public const string MeshPath = "Assets/VoxelEngine/Mesh/";
    public void OnValidate()
    {
        if(SaveMesh)
        {
            SaveMesh = false;
            this.SaveMeshAsset($"{MeshPath}{GetFileName}.asset");
        }

        if (SaveVoxels)
        {
            SaveVoxels = false;
            ChunkVoxelData.SaveChunk(this);
        }

        if(LoadMesh)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshPath}{GetFileName}.asset");
            if (asset != null)
            {
                meshFilter = GetComponent<MeshFilter>();
                meshFilter.mesh = asset;
            }
            LoadMesh = false;
        }
    }
    #endif
    public void OnDestroy()
    {
        Density.Dispose();
        Material.Dispose();
    }

    public int GetDensity(Vector3Int localPosition) => Density[GetIndex(localPosition)];

    public int GetDensity(Vector3 worldPosition) => Density[GetIndex(WorldPositionToLocalVoxelPosition(worldPosition))];

    public int GetMaterial(Vector3Int localPosition) => Material[GetIndex(localPosition)];
    public int GetMaterial(Vector3 worldPosition) => Material[GetIndex(WorldPositionToLocalVoxelPosition(worldPosition))];


    public Voxel GetVoxel(Vector3Int localPosition)
    {
        return GetVoxel(localPosition.x, localPosition.y, localPosition.z);
    }

    public Voxel GetVoxel(int x, int y, int z)
    {
        var index = TerrainChunkManager.GetIndex(x, y, z);
        return new Voxel()
        {
            Density = Density[index],
            Material = Material[index]
        };
    }

    public VoxelModification AdjustDensity(Vector3Int localPosition, int value)
    {
        var index = GetIndex(localPosition);
        return AdjustDensity(index, value);
    }

    public VoxelModification AdjustDensity(int index, int value)
    {
        var valueBefore = Density[index];
        Density[index] += value;
        if (Density[index] < 0)
            Density[index] = 0;
        var valueNow = Density[index];
        if (valueBefore != valueNow) TerrainChunkManager.MarkVoxelDataChanged();

        var voxelModification = new VoxelModification()
        {
            IsoFlip = (valueBefore < TerrainChunkManager.IsoLevel) != (valueNow < TerrainChunkManager.IsoLevel),
            Density = valueNow,
            Material = Material[index],
        };

        return voxelModification;
    }

    public int GetIndex(Vector3Int localPosition)
    {
        return TerrainChunkManager.GetIndex(localPosition.x, localPosition.y, localPosition.z);
    }

    public Vector3Int WorldPositionToLocalVoxelPosition(Vector3 worldPosition)
    {
        return (worldPosition - TerrainChunkManager.ChunkWorldSize * Offset).FloorToInt();
    }

    public void SetMaterial(Vector3Int localPosition, int material)
    {
        Material[GetIndex(localPosition)] = material;
    }

    public void AssignMesh(Mesh mesh)
    {
        IsDirty = false;
        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;
    }

    void OnDrawGizmosSelected()
    {
        if (Density == null || Material == null || Density.Length == 0 || !ShowDebug) return;
        for (int x = 0; x <= Size; x++)
            for (int y = 0; y <= Size; y++)
                for (int z = 0; z <= Size; z++)
                {
                    var voxel = GetVoxel(x, y, z);
                    Gizmos.color = ((VoxelMaterial)voxel.Material).ToColor();

                    Vector3 worldPos = transform.position + new Vector3(x, y, z) * CubeSize;
                    if (voxel.Density >= 128)
                    {
                        Gizmos.DrawWireSphere(worldPos, CubeSize * 0.1f * voxel.Density / 128f);
                    }
                    else
                    {
                        Gizmos.DrawWireCube(worldPos, Vector3.one * CubeSize * 0.1f + new Vector3(voxel.Density / 1000f, voxel.Density / 256f, voxel.Density / 2000f));
                    }
                }
    }

    void OnDrawGizmos()
    {
        var color = new Color(0.2f, 0.2f, 0.2f);
        if(IsDirty)
            color += Color.red;
        if(IsGenerated)
            color += Color.green;
        if (IsLoaded)
            color += Color.blue;
        Gizmos.color = color;   
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(TerrainChunkManager.ChunkCenterOffset, TerrainChunkManager.ChunkOffset);
        Gizmos.matrix = Matrix4x4.identity;
    }

    internal void Unload()
    {
        IsLoaded = false;
        meshRenderer.enabled = false;
        //meshCollider.enabled = false;
    }

    internal void Load()
    {
        IsLoaded = true;
        meshRenderer.enabled = true;
        //meshCollider.enabled = true;
    }

    public void MakeDirty()
    {
        if(!IsDirty)
            TerrainChunkManager.instance.UpdateMeshForChunk(this);
        IsDirty = true;
    }

    internal void AssignField(NativeArray<int> density, NativeArray<int> material)
    {
        IsGenerated = true;
        IsLoaded = true;
        if (Density.IsCreated) Density.Dispose();
        Density = density;
        if (Material.IsCreated) Material.Dispose();
        Material = material;
        TerrainChunkManager.MarkVoxelDataChanged();
    }
    
    internal void UpdateDensity(int[] density)
    {
        if (Density.IsCreated) Density.Dispose();
        Density = new NativeArray<int>(density, Allocator.Persistent);
        TerrainChunkManager.MarkVoxelDataChanged();
        MakeDirty();
    }

    internal void UpdateMaterial(int[] material)
    {
        if (Material.IsCreated) Material.Dispose();
        Material = new NativeArray<int>(material, Allocator.Persistent);
        MakeDirty();
    }

    internal bool LocalPositionIsOutOfBounds(Vector3Int localVoxelPosition)
    {
        var index = GetIndex(localVoxelPosition);
        return GetDensityArrayLength < index || index < 0;
    }
}

public static class TerrainChunkExtensions
{
    public static Vector3 ChunkPositionToWorldPosition(this TerrainChunk chunk)
    {
        return new Vector3((chunk.Offset.x + 0.5f) * chunk.Size, (chunk.Offset.y + 0.5f) * chunk.Size, (chunk.Offset.z + 0.5f) * chunk.Size);
    }

    public static NativeArray<byte> ShrinkToByte(this NativeArray<int> array)
    {
        var byteArray = new NativeArray<byte>(array.Length, Allocator.Persistent);
        for (int i = 0; i < array.Length; i++)
        {
            byteArray[i] = (byte)array[i];
        }
        array.Dispose();
        return byteArray;
    }

    #if UNITY_EDITOR
    public static void SaveMeshAsset(this TerrainChunk chunk, string path = "Assets/VoxelEngine/Mesh/")
    {
        // Ensure the path is valid and includes "Assets/"
        if (!path.StartsWith("Assets/"))
        {
            Debug.LogError("Mesh save path must start with 'Assets/'.");
            return;
        }

        // Create the asset
        AssetDatabase.CreateAsset(chunk.GetMesh, path);

        // Save all pending asset changes to disk
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(); // Refresh the Project window to show the new asset
        Debug.Log($"Mesh saved to: {path}");
    }
    #endif
}