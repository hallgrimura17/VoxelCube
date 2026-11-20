using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "ChunkData", menuName = "ChunkData", order = 1)]
public class ChunkData : ScriptableObject
{
    public string Name;
    [SerializeField]
    [HideInInspector]
    public int[] Density;
    [SerializeField]
    [HideInInspector]
    public int[] Material;
    public string Info;
    public int SolidVoxels;

#if UNITY_EDITOR
    public bool forceSave;

    public void OnValidate()
    {
        if(forceSave)
        {
            forceSave = false;
            AssetDatabase.SaveAssets();
        }
    }
#endif

    internal void Set(string name, int[] density, int[] material)
    {
        Name = name;
        Density = density;
        Material = material;
        SolidVoxels = Density.Count(x => x >= TerrainChunkManager.IsoLevel) - 4224; //Supposed all gray voxels from empty building
        Info = $"Density: {Density.Length}, Material: {material.Length}";
    }
}
