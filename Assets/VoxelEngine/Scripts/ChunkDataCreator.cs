using Assets.Extensions;
using UnityEngine;

#if UNITY_EDITOR
public class ChunkDataCreator : MonoBehaviour
{
    public ChunkData ChunkData;
    public Vector3Int ScanPosition => transform.position.FloorToInt();
    public float ScanSize = 1f;
    public LayerMask ScanMask;
    public string ExtraDataPath = "ChunkData/";

    public bool Run = false;

    public void OnValidate()
    {
        if (Run)
        {
            Run = false;
            ChunkData = ChunkMeshToVoxel.CollisionsToChunkData(gameObject, ScanPosition, ScanSize, ScanMask, ExtraDataPath);
        }
    }
    public void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(ScanPosition + TerrainChunkManager.ChunkCenterOffset * 2, TerrainChunkManager.ChunkOffset * 2);
    }
}

#endif
