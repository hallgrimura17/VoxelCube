using Assets.Extensions;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
public class ChunkMeshToVoxel
{
    public static ChunkData CollisionsToChunkData(GameObject meshParent, Vector3Int startingPosition, float scanSize, LayerMask scanMask, string dataPath = "ChunkData/")
    {
        var scene = meshParent.scene;
        Debug.Log(meshParent.name + " is from the Scene: " + scene.name);
        var physicsScene = scene.GetPhysicsScene();
        var fieldSize = TerrainChunkManager.FieldSize;
        var density = new int[TerrainChunkManager.NativeArraySize];
        var material = new int[TerrainChunkManager.NativeArraySize];
        var cubeOffset = (GameController.CubeOffset / (float)TerrainChunkManager.CubeSize);
        var maxDistance = cubeOffset.sqrMagnitude;
        for (int z = 0; z < fieldSize; z++)
            for (int y = 0; y < fieldSize; y++)
                for (int x = 0; x < fieldSize; x++)
                {
                    var index = TerrainChunkManager.GetIndex(x, y, z);
                    var currentPosition = startingPosition + new Vector3Int(x, y, z);
                    var hits = new Collider[10];
                    physicsScene.OverlapSphere(currentPosition, scanSize, hits, scanMask, QueryTriggerInteraction.Ignore);
                    hits = hits.Where(x => x != null).ToArray();
                    if (hits.Any())
                    {
                        var colliderMaterial = hits.Min(x => x.layerOverridePriority);
                        if(colliderMaterial >= 0)
                        {
                            var depthDensityMultiplier = (maxDistance - (currentPosition - cubeOffset).sqrMagnitude) / maxDistance;
                            var voxelDensity = Mathf.RoundToInt(TerrainChunkManager.IsoLevel * depthDensityMultiplier * 10f);
                            density[index] = voxelDensity;
                            material[index] = colliderMaterial;
                            Debug.DrawLine(currentPosition, currentPosition + voxelDensity / TerrainChunkManager.IsoLevel * Vector3.one, ((VoxelMaterial)material[index]).ToColor(), 5f);
                        }
                    }
                    else
                        Debug.DrawLine(currentPosition, currentPosition + Vector3.one, Color.black, 2f);
                }

        var name = $"{meshParent.name}";
        var chunkDataInstance = SOHelpers.CreateSOInstance<ChunkData>("Assets/" + dataPath, name);
        chunkDataInstance.Set(name, density, material);
        return chunkDataInstance;
    }
}
#endif
