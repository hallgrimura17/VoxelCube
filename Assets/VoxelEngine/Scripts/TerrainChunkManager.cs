using Assets.Extensions;
using Assets.Resources;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class TerrainChunkManager : MonoBehaviour
{
    public static TerrainChunkManager instance;
    public ComputeVoxelMeshGenerator ComputeVoxelMeshGenerator;
    private static int fieldSize;
    private static int voxelDataRevision;
    public static int VoxelDataRevision => voxelDataRevision;
    internal bool IsCollapsing => isCollapsing;
    internal bool IsCubeCollapsed => isCubeCollapsed;
    internal void QueueCollapseVoxels(IReadOnlyList<VoxelModification> voxels)
    {
        if (isCollapsing) pendingCollapseVoxels.AddRange(voxels);
    }
    internal static void MarkVoxelDataChanged() => voxelDataRevision++;
    public static int MaxDepth;
    public Vector3Int HoleSize = new Vector3Int(2, 8, 2);
    public static int FieldSize => ChunkDataSize + 1;
    public TerrainChunk ChunkTemplate;
    public TerrainChunk Chunk;
    private const float DisconnectedVoxelCheckInterval = 1f;
    private const float CubeCollapseDuration = 3f;
    private const float CubeCollapseEndScale = 0.1f;
    private const float CollapsedVoxelImpulseMultiplier = 32f;
    private const int CollapseDropBatchSize = 32;
    private const float CollapseDropSpawnAreaSize = 6f;
    [SerializeField] private AnimationCurve collapseShrinkCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    private bool prestigeGrantedForCubeClear;
    private bool masteryPointGrantedForCubeClear;
    private bool isCollapsing;
    private bool isCubeCollapsed;
    private Coroutine collapseRoutine;
    private Vector3 chunkBasePosition;
    private Vector3 chunkBaseScale;
    private readonly List<VoxelModification> pendingCollapseVoxels = new();
    private float nextDisconnectedVoxelCheckTime;
    private int lastDisconnectedVoxelCheckRevision = -1;

    public static readonly float CubeSize = 0.5f;
    public static int ChunkWorldSize => Mathf.FloorToInt(CubeSize * ChunkDataSize);
    public static readonly int ChunkDataSize = 32;
    public static readonly int IsoLevel = 128;

    public static int NativeArraySize => (int)Mathf.Pow(FieldSize, 3) + 3;
    public static Vector3Int ChunkCenterOffset => ChunkOffset / 2;
    public static Vector3Int ChunkOffset => (int)ChunkWorldSize * Vector3Int.one;

    public void OnValidate()
    {
        fieldSize = ChunkDataSize + 1;
    }

    public void Awake()
    {
        instance = this;
        voxelDataRevision = 0;
        fieldSize = ChunkDataSize + 1;
        ChunkTemplate.Size = ChunkDataSize;
        chunkBasePosition = Chunk != null ? Chunk.transform.localPosition : ChunkTemplate.transform.localPosition;
        chunkBaseScale = Chunk != null ? Chunk.transform.localScale : ChunkTemplate.transform.localScale;
        MaxDepth = HoleSize.y * ChunkWorldSize;
    }

    private void Update()
    {
        if (isCollapsing || Time.time < nextDisconnectedVoxelCheckTime) return;
        nextDisconnectedVoxelCheckTime = Time.time + DisconnectedVoxelCheckInterval;
        if (lastDisconnectedVoxelCheckRevision == VoxelDataRevision) return;
        if (Chunk != null && Chunk.IsInitialized && !IsCenterVoxelSolid())
        {
            BeginCubeCollapse();
            return;
        }
        var disconnectedVoxels = DestroyDisconnectedVoxels();
        lastDisconnectedVoxelCheckRevision = VoxelDataRevision;
        if (disconnectedVoxels.Count >= 10)
        {
            var collapsePosition = Vector3.zero;
            foreach (var voxel in disconnectedVoxels) collapsePosition += voxel.Position;
            collapsePosition /= disconnectedVoxels.Count;
            SoundManager.instance.PlaySoundClipAtLocation(collapsePosition, "VoxelCollapse");
        }
        if (disconnectedVoxels.Count > 0) GameController.instance.SpawnDisconnectedResources(disconnectedVoxels);
        GrantCubeClearRewards();
    }

    #region Helpers
    public TerrainChunk GetChunkAtPosition(Vector3 worldPosition, bool CreateIfNotFound = false)
    {
        return GetChunkAtPosition(WorldPositionToChunkPosition(worldPosition), CreateIfNotFound);
    }

    public void GenerateMeshForChunk(TerrainChunk chunk, bool instant = false)
    {
        if (instant)
            ComputeVoxelMeshGenerator.GenerateChunkUpdate(chunk);
        else
            ComputeVoxelMeshGenerator.RequestMeshForChunk(chunk);
    }

    public void UpdateMeshForChunk(TerrainChunk chunk)
    {
        ComputeVoxelMeshGenerator.GenerateChunkUpdate(chunk);
    }

    public void RequestMeshForChunk(TerrainChunk chunk)
    {
       ComputeVoxelMeshGenerator.RequestMeshForChunk(chunk);
    }

    public TerrainChunk GetChunkAtPosition(Vector3Int chunkPosition, bool CreateIfNotFound = false)
    {
        if (Chunk != null) return Chunk.Offset == chunkPosition ? Chunk : null;
        return CreateIfNotFound ? CreateChunk(chunkPosition) : null;
    }

    public TerrainChunk CreateChunk(Vector3Int chunkPosition)
    {
        if (Chunk != null) return Chunk;
        ChunkTemplate.Offset = chunkPosition;
        Chunk = Instantiate(ChunkTemplate, ChunkWorldSize * (Vector3)chunkPosition, Quaternion.identity, transform);
        Chunk.name = Chunk.GetObjectName;
        chunkBasePosition = Chunk.transform.localPosition;
        chunkBaseScale = Chunk.transform.localScale;
        MarkVoxelDataChanged();
        return Chunk;
    }

    public static Vector3Int WorldPositionToChunkPosition(Vector3 position)
    {
        position /= ChunkWorldSize;
        return position.FloorToInt();
    }

    public Vector3Int WorldPositionToChunkPosition(Vector3Int position)
    {
        return new Vector3Int(Mathf.FloorToInt((float)position.x / ChunkWorldSize), Mathf.FloorToInt((float)position.y / ChunkWorldSize), Mathf.FloorToInt((float)position.z / ChunkWorldSize));
    }

    public static Vector3Int WorldPositionToLocalVoxelPosition(Vector3 position)
    {
        return (position / CubeSize - ChunkDataSize * WorldPositionToChunkPosition(position)).RoundToInt();
    }

    internal List<VoxelModification> DestroyDisconnectedVoxels()
    {
        if (Chunk == null || !Chunk.IsInitialized) return new List<VoxelModification>();
        var solidVoxels = new HashSet<Vector3Int>();
        var chunk = Chunk;
        for (var z = 0; z <= ChunkDataSize; z++)
            for (var y = 0; y <= ChunkDataSize; y++)
                for (var x = 0; x <= ChunkDataSize; x++)
                {
                    if (chunk.Density[GetIndex(x, y, z)] < IsoLevel) continue;
                    solidVoxels.Add(chunk.Offset * ChunkDataSize + new Vector3Int(x, y, z));
                }

        if (solidVoxels.Count == 0) return new List<VoxelModification>();
        var center = new Vector3Int(Mathf.RoundToInt(GameController.CubeOffset.x / CubeSize), Mathf.RoundToInt(GameController.CubeOffset.y / CubeSize), Mathf.RoundToInt(GameController.CubeOffset.z / CubeSize));
        var connectedVoxels = new HashSet<Vector3Int>();
        var pendingVoxels = new Queue<Vector3Int>();
        if (solidVoxels.Contains(center))
        {
            connectedVoxels.Add(center);
            pendingVoxels.Enqueue(center);
        }
        while (pendingVoxels.Count > 0)
        {
            var voxel = pendingVoxels.Dequeue();
            AddConnectedVoxel(voxel + Vector3Int.right, solidVoxels, connectedVoxels, pendingVoxels);
            AddConnectedVoxel(voxel + Vector3Int.left, solidVoxels, connectedVoxels, pendingVoxels);
            AddConnectedVoxel(voxel + Vector3Int.up, solidVoxels, connectedVoxels, pendingVoxels);
            AddConnectedVoxel(voxel + Vector3Int.down, solidVoxels, connectedVoxels, pendingVoxels);
            AddConnectedVoxel(voxel + new Vector3Int(0, 0, 1), solidVoxels, connectedVoxels, pendingVoxels);
            AddConnectedVoxel(voxel + new Vector3Int(0, 0, -1), solidVoxels, connectedVoxels, pendingVoxels);
        }

        if (connectedVoxels.Count == solidVoxels.Count) return new List<VoxelModification>();
        var disconnectedVoxels = new HashSet<Vector3Int>(solidVoxels);
        disconnectedVoxels.ExceptWith(connectedVoxels);
        var removedResources = new List<VoxelModification>(disconnectedVoxels.Count);
        var changed = false;
        for (var z = 0; z <= ChunkDataSize; z++)
            for (var y = 0; y <= ChunkDataSize; y++)
                for (var x = 0; x <= ChunkDataSize; x++)
                {
                    var index = GetIndex(x, y, z);
                    var voxel = chunk.Offset * ChunkDataSize + new Vector3Int(x, y, z);
                    var density = chunk.Density[index];
                    if (density < IsoLevel || !disconnectedVoxels.Contains(voxel)) continue;
                    removedResources.Add(new VoxelModification { IsoFlip = true, Density = 0, Material = chunk.Material[index], Position = (Vector3)voxel * CubeSize });
                    chunk.Density[index] = 0;
                    changed = true;
                }
        if (changed)
        {
            MarkVoxelDataChanged();
            chunk.MakeDirty();
        }
        return removedResources;
    }

    private static void AddConnectedVoxel(Vector3Int voxel, HashSet<Vector3Int> solidVoxels, HashSet<Vector3Int> connectedVoxels, Queue<Vector3Int> pendingVoxels)
    {
        if (solidVoxels.Contains(voxel) && connectedVoxels.Add(voxel)) pendingVoxels.Enqueue(voxel);
    }



    private static Vector3Int CenterVoxelPosition => new Vector3Int(Mathf.RoundToInt(GameController.CubeOffset.x / CubeSize), Mathf.RoundToInt(GameController.CubeOffset.y / CubeSize), Mathf.RoundToInt(GameController.CubeOffset.z / CubeSize));

    private bool IsCenterVoxelSolid()
    {
        var localCenter = CenterVoxelPosition - Chunk.Offset * ChunkDataSize;
        if (localCenter.x < 0 || localCenter.x > ChunkDataSize || localCenter.y < 0 || localCenter.y > ChunkDataSize || localCenter.z < 0 || localCenter.z > ChunkDataSize) return false;
        return Chunk.Density[GetIndex(localCenter.x, localCenter.y, localCenter.z)] >= IsoLevel;
    }

    private void BeginCubeCollapse()
    {
        if (isCollapsing) return;
        isCollapsing = true;
        collapseRoutine = StartCoroutine(CollapseCube());
    }

    private IEnumerator CollapseCube()
    {
        SoundManager.instance.PlaySoundClipAtLocation(Chunk.transform.position, "CubeCollapse");
        var startingPosition = Chunk.transform.localPosition;
        var startingScale = Chunk.transform.localScale;
        var originalCollapseCenter = Chunk.transform.TransformPoint((Vector3)ChunkCenterOffset);
        var centerOffset = Vector3.Scale((Vector3)ChunkCenterOffset, startingScale);
        var elapsed = 0f;
        while (elapsed < CubeCollapseDuration)
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / CubeCollapseDuration);
            var curveProgress = collapseShrinkCurve.Evaluate(progress);
            var scaleMultiplier = Mathf.Lerp(1f, CubeCollapseEndScale, curveProgress);
            Chunk.transform.localScale = startingScale * scaleMultiplier;
            Chunk.transform.localPosition = startingPosition + Chunk.transform.localRotation * centerOffset * (1f - scaleMultiplier);
            yield return null;
        }
        Chunk.transform.localScale = startingScale * CubeCollapseEndScale;
        Chunk.transform.localPosition = startingPosition + Chunk.transform.localRotation * centerOffset * (1f - CubeCollapseEndScale);
        var releasedVoxels = new List<VoxelModification>(pendingCollapseVoxels);
        pendingCollapseVoxels.Clear();
        var collapsedCenter = Chunk.transform.TransformPoint((Vector3)ChunkCenterOffset);
        for (var index = 0; index < releasedVoxels.Count; index++) releasedVoxels[index] = ScaleCollapseVoxelPosition(releasedVoxels[index], originalCollapseCenter, collapsedCenter);
        var changed = false;
        for (var z = 0; z <= ChunkDataSize; z++)
            for (var y = 0; y <= ChunkDataSize; y++)
                for (var x = 0; x <= ChunkDataSize; x++)
                {
                    var index = GetIndex(x, y, z);
                    var density = Chunk.Density[index];
                    var voxel = Chunk.Offset * ChunkDataSize + new Vector3Int(x, y, z);
                    if (density >= IsoLevel) releasedVoxels.Add(new VoxelModification { IsoFlip = true, Density = 0, Material = Chunk.Material[index], Position = Chunk.transform.TransformPoint((Vector3)(voxel - Chunk.Offset * ChunkDataSize) * CubeSize) });
                    if (density == 0) continue;
                    Chunk.Density[index] = 0;
                    changed = true;
                }
        if (changed)
        {
            MarkVoxelDataChanged();
            Chunk.MakeDirty();
        }
        isCubeCollapsed = true;
        if (releasedVoxels.Count >= 10)
        {
            var collapsePosition = Vector3.zero;
            foreach (var voxel in releasedVoxels) collapsePosition += voxel.Position;
            SoundManager.instance.PlaySoundClipAtLocation(collapsePosition / releasedVoxels.Count, "VoxelCollapse");
        }
        if (releasedVoxels.Count > 0) yield return SpawnCollapseDropsInBatches(releasedVoxels, collapsedCenter);
        lastDisconnectedVoxelCheckRevision = VoxelDataRevision;
        GrantCubeClearRewards();
        collapseRoutine = null;
        isCollapsing = false;
    }

    private static VoxelModification ScaleCollapseVoxelPosition(VoxelModification voxel, Vector3 originalCenter, Vector3 collapsedCenter)
    {
        voxel.Position = collapsedCenter + (voxel.Position - originalCenter) * CubeCollapseEndScale;
        return voxel;
    }

    private IEnumerator SpawnCollapseDropsInBatches(List<VoxelModification> releasedVoxels, Vector3 spawnAreaCenter)
    {
        var halfAreaSize = CollapseDropSpawnAreaSize * 0.5f;
        var sourceIndex = 0;
        while (sourceIndex < releasedVoxels.Count)
        {
            var spawnedCount = 0;
            while (sourceIndex < releasedVoxels.Count && spawnedCount < CollapseDropBatchSize)
            {
                var remainingDrops = releasedVoxels.Count - sourceIndex;
                var maximumTier = 0;
                while (maximumTier < 5 && (1 << (maximumTier + 1)) <= remainingDrops) maximumTier++;
                var tier = UnityEngine.Random.Range(0, maximumTier + 1);
                var consumedDrops = 1 << tier;
                var voxel = releasedVoxels[sourceIndex];
                voxel.Position = spawnAreaCenter + new Vector3(UnityEngine.Random.Range(-halfAreaSize, halfAreaSize), UnityEngine.Random.Range(-halfAreaSize, halfAreaSize), UnityEngine.Random.Range(-halfAreaSize, halfAreaSize));
                GameController.instance.SpawnCollapseResource(voxel, UnityEngine.Random.onUnitSphere, CollapsedVoxelImpulseMultiplier, tier);
                sourceIndex += consumedDrops;
                spawnedCount++;
            }
            if (sourceIndex < releasedVoxels.Count) yield return null;
        }
    }

    /// <summary>Restores the single terrain chunk from its original data.</summary>
    public void ResetCube()
    {
        if (collapseRoutine != null) StopCoroutine(collapseRoutine);
        collapseRoutine = null;
        isCollapsing = false;
        isCubeCollapsed = false;
        pendingCollapseVoxels.Clear();
        prestigeGrantedForCubeClear = false;
        masteryPointGrantedForCubeClear = false;
        if (Chunk != null)
        {
            Chunk.transform.localPosition = chunkBasePosition;
            Chunk.transform.localScale = chunkBaseScale;
            Chunk.LoadChunkData(Chunk.ChunkData.Density, Chunk.ChunkData.Material);
        }
        lastDisconnectedVoxelCheckRevision = VoxelDataRevision;
        nextDisconnectedVoxelCheckTime = Time.time + DisconnectedVoxelCheckInterval;
    }

    bool IsCubeCleared()
    {
        if (Chunk == null || !Chunk.IsInitialized) return false;
        for (var index = 0; index < Chunk.Density.Length; index++)
            if (Chunk.Density[index] >= IsoLevel) return false;
        return true;
    }

    private void GrantCubeClearRewards()
    {
        if (!IsCubeCleared()) return;
        if (!prestigeGrantedForCubeClear)
        {
            prestigeGrantedForCubeClear = true;
            GameController.instance.TryUnlockNextPrestige();
        }
        if (!masteryPointGrantedForCubeClear)
        {
            masteryPointGrantedForCubeClear = true;
            MasterySystem.Instance.AddSkillPoints(1);
        }
    }

    public VoxelModification MineChunksAtPosition(Vector3 worldPoint, int amount, Vector3 normal)
    {
        return VoxelModification(worldPoint, amount, normal);
    }

    private VoxelModification VoxelModification(Vector3 worldPoint, int amount, Vector3 normal)
    {
        if (isCollapsing) return new VoxelModification();
        var chunkPosition = WorldPositionToChunkPosition(worldPoint);
        var chunk = GetChunkAtPosition(chunkPosition);
        if (chunk == null) return new VoxelModification();
        var voxelLocalPosition = (worldPoint / CubeSize - ChunkDataSize * chunkPosition).FloorToInt();
        var voxelMaterial = (VoxelMaterial)chunk.GetMaterial(voxelLocalPosition);
        var prestigeAdjustedAmount = amount / GameController.instance.CubeHardnessMultiplier;
        var materialAdjustedDensity = (int)(prestigeAdjustedAmount / voxelMaterial.GetMaterialHardness());
        var modification = chunk.AdjustDensity(voxelLocalPosition, -materialAdjustedDensity);
        var voxelPosition = chunk.Offset * ChunkDataSize + voxelLocalPosition;
        modification.Position = (Vector3)voxelPosition * CubeSize;
        chunk.MakeDirty();
        if (modification.IsoFlip && modification.Density < IsoLevel && voxelPosition == CenterVoxelPosition)
        {
            pendingCollapseVoxels.Add(new VoxelModification { IsoFlip = true, Density = 0, Material = modification.Material, Position = modification.Position });
            modification.IsoFlip = false;
            BeginCubeCollapse();
        }
        return modification;
    }

    public void VoxelMaterialModification(Vector3 worldPoint, VoxelMaterial material)
    {
        if (isCollapsing) return;
        var chunkPosition = WorldPositionToChunkPosition(worldPoint);
        var chunk = GetChunkAtPosition(chunkPosition);
        if (chunk == null) return;
        var voxelLocalPosition = (worldPoint / CubeSize - ChunkDataSize * chunkPosition).FloorToInt();
        chunk.SetMaterial(voxelLocalPosition, (int)material);
        chunk.MakeDirty();
    }

    #endregion

    public static int GetIndex(int x, int y, int z)
    {
        return x + y * fieldSize + z * fieldSize * fieldSize;
    }

    internal float GetVoxelDensityAtPosition(Vector3 worldPosition)
    {
        var chunk = GetChunkAtPosition(worldPosition);
        var localVoxelPosition = WorldPositionToLocalVoxelPosition(worldPosition);
        var density = chunk != null ? chunk.GetDensity(localVoxelPosition) : -1;
        return density;
    }

    internal float GetVoxelMaterialAtPosition(Vector3 worldPosition)
    {
        var chunk = GetChunkAtPosition(worldPosition);
        var localVoxelPosition = WorldPositionToLocalVoxelPosition(worldPosition);
        var material = chunk != null ? chunk.GetMaterial(localVoxelPosition) : 0;
        return material;
    }

    public bool VoxelIsDense(Vector3 worldPosition)
    {
        return GetVoxelDensityAtPosition(worldPosition) >= IsoLevel;
    }

    public bool IsEntityOutOfbounds(Vector3 worldPosition)
    {
        return IsUnderGround(worldPosition) && worldPosition.y > -MaxDepth;
    }

    public bool IsUnderGround(Vector3 worldPosition)
    {
        if (IsInsideHoleArea(worldPosition))
            return VoxelIsDense(worldPosition);
        return 0 > worldPosition.y;
    }

    public bool IsInsideHoleArea(Vector3 worldPosition) => 0 <= worldPosition.x && worldPosition.x <= ChunkWorldSize * 2f && 0 <= worldPosition.z && worldPosition.z <= ChunkWorldSize * 2f;
    
}