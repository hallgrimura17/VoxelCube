using UnityEngine;

namespace Assets.Extensions
{
    public static class VoxelMaterialExtensions
    {
        public static float GetMaterialHardness(this VoxelMaterial material) => material switch
        {
            VoxelMaterial.Base => 0.1f,
            VoxelMaterial.Gold => 0.5f,
            _ => 0.1f,
        };
        public static Color ToColor(this VoxelMaterial material)
        {
            switch (material)
            {
                case VoxelMaterial.Gold:
                    return new Color(0.8f, 0.8f, 0.2f);
            }
            return Color.white;
        }
    }
}
