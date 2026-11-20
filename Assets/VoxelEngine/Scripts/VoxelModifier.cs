using Assets.Resources;
using System;
using System.Collections.Generic;
using UnityEngine;

public static class VoxelModifier
{

    public static void MineChunksAtPoint(Vector3 worldPosition, Vector3 normal, float range, float amount, float resourceMultiplier = 1f, bool emitParticles = true)
    {
        var isoFlips = new List<VoxelModification>();
        var particleRotation = Quaternion.LookRotation(normal);
        //worldPosition += 0.5f * TerrainChunkManager.CubeSize * Vector3.one;
        if (range > 10)
        { 
            range = 10;
            Debug.LogError($"Voxel mine range of {range}, reducing to 10"); 
        }
        var samplingRange = range < 1f ? range + TerrainChunkManager.CubeSize * 0.5f : range;
        ForEachVoxelCheap(worldPosition, samplingRange, (worldPoint, fallOff) =>
        {
            var distanceAdjustedAmount = (int)(fallOff * amount);
            var result = TerrainChunkManager.instance.MineChunksAtPosition(worldPoint, distanceAdjustedAmount, normal);
            if (emitParticles && result.IsoFlip)
            {
                //Debug.DrawRay(worldPosition, worldPoint - worldPosition, Color.green, 5f);
                //ParticleManager.instance.EmitParticleEffect("VoxelDestruction", worldPoint, particleRotation, 1);
            }
            //else
            //    Debug.DrawRay(worldPosition, worldPoint - worldPosition, Color.red, 5f);

            if (result.IsoFlip)
            {
                isoFlips.Add(result);
            }
        });
        var terrainChunkManager = TerrainChunkManager.instance;
        if (terrainChunkManager.IsCollapsing) terrainChunkManager.QueueCollapseVoxels(isoFlips);
        else GameController.instance.SpawnResources(isoFlips, worldPosition, normal);
    }

    public static void OverrideMaterial(Vector3 worldPosition, float range, VoxelMaterial material)
    {
        worldPosition += 0.5f * TerrainChunkManager.CubeSize * Vector3.one;
        ForEachVoxel(worldPosition, range, (worldPoint, fallOff) =>
        {
            if(fallOff > 0)
                TerrainChunkManager.instance.VoxelMaterialModification(worldPoint, material);
        });

    }

    public static void ForEachVoxel(Vector3 worldPosition, float range, Action<Vector3, float> action)
    {
        var cubeSize = TerrainChunkManager.CubeSize;
        var loopLimit = Mathf.FloorToInt(range / cubeSize);
        for (float x = -loopLimit; x <= loopLimit; x++)
            for (float y = -loopLimit; y <= loopLimit; y++)
                for (float z = -loopLimit; z <= loopLimit; z++)
                {
                    var position = worldPosition + cubeSize * new Vector3(x, y, z);
                    var distance = Vector3.Distance(position, worldPosition);
                    float fallOff = Math.Max(Mathf.Clamp01(1f - range), 1f - distance / range);
                    action(position, fallOff);
                }
    }

    public static void ForEachVoxelCheap(Vector3 worldPosition, float range, Action<Vector3, float> action)
    {
        var cubeSize = TerrainChunkManager.CubeSize;
        var loopLimit = Mathf.FloorToInt(range / cubeSize);
        var sqrRange = range * range;
        for (float x = -loopLimit; x <= loopLimit; x++)
            for (float y = -loopLimit; y <= loopLimit; y++)
                for (float z = -loopLimit; z <= loopLimit; z++)
                {
                    var position = worldPosition + cubeSize * new Vector3(x, y, z);
                    var sqrDistance = (position - worldPosition).sqrMagnitude;
                    if (sqrDistance > sqrRange) continue;
                    float fallOff = 1f - Mathf.Sqrt(sqrDistance) / range;
                    action(position, fallOff);
                }
    }

}