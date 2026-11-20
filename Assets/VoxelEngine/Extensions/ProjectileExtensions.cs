using Assets;
using UnityEngine;

namespace Assets.Extensions
{
    public static class ProjectileExtensions
    {
        public static RayProjectile CreateProjectile(this RayProjectile projectile, Vector3 position, Quaternion rotation)
        {
            var instance = Object.Instantiate(projectile, position, rotation);

            return instance;
        }

        public static void SpawnCopies(this RayProjectile projectile, int copies, float distanceBetweenProjectiles = 0.4f)
        {
            var projectileCount = copies;
            if (projectileCount % 2 == 0)
            {
                Object.Destroy(projectile.gameObject, 0.000001f);
            }
            else
            {
                projectileCount--;
            }

            var ringLimit = 4;
            var baseDegree = 360f / Mathf.Min(projectileCount, ringLimit);
            var baseOffset = new Vector3(distanceBetweenProjectiles * 0.1f, 0, 1f);
            for (int i = 0; i < projectileCount; i++)
            {
                var projectileOffset = Quaternion.Euler(0, 0, baseDegree * i) * baseOffset;
                projectileOffset.z = 0;
                var worldProjectileOffset = projectile.transform.TransformDirection(projectileOffset);
                var projectileInstance = Object.Instantiate(projectile, projectile.transform.position - 10f * worldProjectileOffset, projectile.transform.rotation);

                if (i % ringLimit == ringLimit - 1)
                {
                    projectileCount -= i;
                    i = 0;
                    ringLimit *= 2;
                    baseDegree = 360f / Mathf.Min(projectileCount, ringLimit);
                    baseOffset += new Vector3(distanceBetweenProjectiles * 0.1f, 0);
                }
            }
        }

        public static void SplitProjectile(this RayProjectile projectile, int splits, float angle = 45f)
        {
            var position = projectile.transform.position;
            var baseRotation = projectile.transform.rotation;
            var spreadAxis = projectile.transform.up;
            for (int i = 1; i <= splits; i++)
            {
                var offset = i * angle;
                Object.Instantiate(projectile, position, Quaternion.AngleAxis(-offset, spreadAxis) * baseRotation);
                Object.Instantiate(projectile, position, Quaternion.AngleAxis(offset, spreadAxis) * baseRotation);
            }
        }
    }
}