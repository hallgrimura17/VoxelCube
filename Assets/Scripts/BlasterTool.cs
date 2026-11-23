using Assets.Extensions;
using Assets.Resources;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BlasterTool : PlayerTool
{
    public List<RayProjectile> Projectiles = new ();
    public bool SpawnFromCamera = true;
    public float OrbitDistance => desiredCameraDistance;
    public override string DisplayName => "Blaster";
    public override int HotbarOrder => 0;
    public override bool IsUnlocked => true;


    [SerializeField] private float orbitSensitivity = 0.2f;
    [SerializeField] private float baseFireInterval = 0.35f;
    private const float MinAimDirectionSqrMagnitude = 0.0001f;
    private const float MinOrbitPitch = -80f;
    private const float MaxOrbitPitch = 80f;
    private const float CameraSurfacePadding = 5f;
    private const float CameraOriginVerticalOffset = 0.5f;
    private const float MaximumAimDistance = 1000f;
    private const float MinimumCameraDistance = 0.05f;
    [SerializeField] private float zoomSensitivity = 0.01f;
    private const int GroundLayerIndex = 3;
    private const int VoxelLayerIndex = 7;
    [SerializeField] private LayerMask cameraCollisionLayers = (1 << GroundLayerIndex) | (1 << VoxelLayerIndex);
    private Camera mainCamera;
    private Transform playerCameraTransform;
    private Transform cameraPivot;
    private float orbitYaw;
    private float orbitPitch;
    private float desiredCameraDistance;
    private float defaultCameraDistance;
    private float nextFireTime;
    private int projectileCount = 1;
    private float fireRateMultiplier = 1f;
    private AudioSource shootAudios;

    void Start()
    {
        shootAudios = GetComponent<AudioSource>();
        UIController.Instance.SetSpawnMode(SpawnFromCamera);
        playerCameraTransform = transform.parent;
        cameraPivot = playerCameraTransform.parent;
        var rotation = cameraPivot.eulerAngles;
        orbitYaw = rotation.y;
        orbitPitch = rotation.x > 180f ? rotation.x - 360f : rotation.x;
        desiredCameraDistance = Mathf.Max(MinimumCameraDistance, -playerCameraTransform.localPosition.z);
        defaultCameraDistance = desiredCameraDistance;
    }

    void Update()
    {
        if(mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        var mouse = Mouse.current;
        var isOrbitMode = PlayerPerspectiveController.Instance == null || PlayerPerspectiveController.Instance.CurrentMode == PlayerPerspectiveController.PerspectiveMode.Orbit;
        if (UIController.Instance.IsGameplayInputBlocked)
        {
            if (isOrbitMode) UpdateCameraZoom(mouse, false);
            return;
        }
        if (isOrbitMode && mouse.rightButton.isPressed)
        {
            var mouseDelta = mouse.delta.ReadValue();
            orbitYaw += mouseDelta.x * orbitSensitivity;
            orbitPitch = Mathf.Clamp(orbitPitch - mouseDelta.y * orbitSensitivity, MinOrbitPitch, MaxOrbitPitch);
            cameraPivot.rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        }
        if (IsSelected && mouse.leftButton.isPressed && Time.time >= nextFireTime)
        {
            ShootProjectile(0);
            nextFireTime = Time.time + baseFireInterval / fireRateMultiplier;
        }
        if (isOrbitMode) UpdateCameraZoom(mouse, true);
    }

    void UpdateCameraZoom(Mouse mouse, bool readScrollWheel)
    {
        if (readScrollWheel) desiredCameraDistance = Mathf.Max(MinimumCameraDistance, desiredCameraDistance - mouse.scroll.ReadValue().y * zoomSensitivity);
        var cameraDirection = cameraPivot.TransformDirection(Vector3.back);
        var cameraDistance = desiredCameraDistance;
        var rayOrigin = cameraPivot.position + cameraDirection * defaultCameraDistance;
        if (Physics.Raycast(rayOrigin, -cameraDirection, out var hit, defaultCameraDistance, cameraCollisionLayers, QueryTriggerInteraction.Ignore)) cameraDistance = Mathf.Max(cameraDistance, defaultCameraDistance - hit.distance + CameraSurfacePadding);
        cameraDistance = Mathf.Max(MinimumCameraDistance, cameraDistance);
        var localPosition = playerCameraTransform.localPosition;
        localPosition.z = -cameraDistance;
        playerCameraTransform.localPosition = localPosition;
    }

    /// <summary>Synchronizes orbit angles and camera distance after returning from a free-movement mode.</summary>
    public void SetOrbitState(float yaw, float pitch, float distance)
    {
        orbitYaw = yaw;
        orbitPitch = pitch;
        desiredCameraDistance = distance;
        cameraPivot.rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
    }

    /// <summary>Toggles projectile spawning between the camera and the player weapon position.</summary>
    public override void ToggleMode()
    {
        SetSpawnFromCamera(!SpawnFromCamera);
    }

    /// <summary>Sets projectile spawning to the camera or player weapon position.</summary>
    public void SetSpawnFromCamera(bool spawnFromCamera)
    {
        SpawnFromCamera = spawnFromCamera;
        UIController.Instance.SetSpawnMode(SpawnFromCamera);
        ToolHotbar.Instance.RefreshModeControls();
    }

    /// <summary>Toggles projectile spawning between the camera and the player weapon position.</summary>
    public void ToggleSpawnFromCamera()
    {
        SetSpawnFromCamera(!SpawnFromCamera);
    }


    public void ShootProjectile(int index)
    {
        var aimPosition = Cursor.lockState == CursorLockMode.Locked ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : Mouse.current.position.ReadValue();
        var aimRay = mainCamera.ScreenPointToRay(aimPosition);
        var position = aimRay.origin - mainCamera.transform.up * CameraOriginVerticalOffset;
        var direction = aimRay.direction;
        if (!SpawnFromCamera)
        {
            var targetCenter = GameController.CubeOffset;
            var targetCollider = TerrainChunkManager.instance.Chunk.GetComponent<MeshCollider>();
            var aimPoint = targetCollider.Raycast(aimRay, out var hit, MaximumAimDistance) ? hit.point : aimRay.GetPoint(Mathf.Max(0f, Vector3.Dot(targetCenter - aimRay.origin, aimRay.direction)));
            var lineDirection = (targetCenter - aimPoint).normalized;
            position = transform.position + transform.up * CameraOriginVerticalOffset;
            if (lineDirection.sqrMagnitude < MinAimDirectionSqrMagnitude) direction = targetCenter - position;
            else
            {
                var distanceBehindAimPoint = Vector3.Dot(position - aimPoint, lineDirection);
                position = aimPoint + lineDirection * Mathf.Min(0f, distanceBehindAimPoint);
                direction = lineDirection;
            }
        }

        for (var shot = 0; shot < projectileCount; shot++)
        {
            var spread = (shot - (projectileCount - 1) * 0.5f) * 3f;
            SpawnProjectile(position, Quaternion.AngleAxis(spread, Vector3.up) * direction, index);
        }
    }

    public void AddProjectileCount(int amount)
    {
        projectileCount += amount;
    }

    public void ApplyFireRateUpgrade(float amount)
    {
        fireRateMultiplier += amount;
    }

    /// <summary>Restores projectile count and fire rate to their base values.</summary>
    public void ResetUpgradeEffects()
    {
        projectileCount = 1;
        fireRateMultiplier = 1f;
    }

    public void SpawnProjectile(Vector3 position, Vector3 direction, int index)
    {
        if (index < 0 || index >= Projectiles.Count) return;
        var projectile = Instantiate(Projectiles[index], position, Quaternion.LookRotation(direction));
        shootAudios.PlaySound("ShootLaser", true);
        projectile.OnSpawn?.Invoke();
    }
}
