using Assets.Extensions;
using Assets.Resources;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BlasterTool : PlayerTool
{
    public List<RayProjectile> Projectiles = new ();
    public bool SpawnFromCamera = true;
    public override string DisplayName => "Blaster";
    public override int HotbarOrder => 0;
    public override bool IsUnlocked => true;


    [SerializeField] private float orbitSensitivity = 0.2f;
    [SerializeField] private float baseFireInterval = 0.35f;
    private const float MinAimDirectionSqrMagnitude = 0.0001f;
    private const float MinOrbitPitch = -80f;
    private const float MaxOrbitPitch = 80f;
    private const float CameraSurfacePadding = 5f;
    private const float MinimumCameraDistance = 0.05f;
    [SerializeField] private float zoomSensitivity = 0.01f;
    private const int GroundLayerIndex = 3;
    private const int VoxelLayerIndex = 7;
    [SerializeField] private LayerMask cameraCollisionLayers = (1 << GroundLayerIndex) | (1 << VoxelLayerIndex);
    private Camera mainCamera;
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
        cameraPivot = transform.parent;
        var rotation = cameraPivot.eulerAngles;
        orbitYaw = rotation.y;
        orbitPitch = rotation.x > 180f ? rotation.x - 360f : rotation.x;
        desiredCameraDistance = Mathf.Max(MinimumCameraDistance, -transform.localPosition.z);
        defaultCameraDistance = desiredCameraDistance;
    }

    void Update()
    {
        if(mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        var mouse = Mouse.current;
        if (UIController.Instance.IsGameplayInputBlocked)
        {
            UpdateCameraZoom(mouse, false);
            return;
        }
        if (mouse.rightButton.isPressed)
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
        UpdateCameraZoom(mouse, true);
    }

    void UpdateCameraZoom(Mouse mouse, bool readScrollWheel)
    {
        if (readScrollWheel) desiredCameraDistance = Mathf.Max(MinimumCameraDistance, desiredCameraDistance - mouse.scroll.ReadValue().y * zoomSensitivity);
        var cameraDirection = cameraPivot.TransformDirection(Vector3.back);
        var cameraDistance = desiredCameraDistance;
        var rayOrigin = cameraPivot.position + cameraDirection * defaultCameraDistance;
        if (Physics.Raycast(rayOrigin, -cameraDirection, out var hit, defaultCameraDistance, cameraCollisionLayers, QueryTriggerInteraction.Ignore)) cameraDistance = Mathf.Max(cameraDistance, defaultCameraDistance - hit.distance + CameraSurfacePadding);
        cameraDistance = Mathf.Max(MinimumCameraDistance, cameraDistance);
        var localPosition = transform.localPosition;
        localPosition.z = -cameraDistance;
        transform.localPosition = localPosition;
    }

    /// <summary>Toggles projectile spawning between the camera and the player weapon position.</summary>
    public void ToggleSpawnFromCamera()
    {
        SpawnFromCamera = !SpawnFromCamera;
        UIController.Instance.SetSpawnMode(SpawnFromCamera);
    }


    public void ShootProjectile(int index)
    {
        var ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        var position = ray.origin;
        var direction = ray.direction;
        if (!SpawnFromCamera)
        {
            position += direction * -transform.localPosition.z;
            if (Physics.Raycast(ray, out RaycastHit hit, -transform.localPosition.z)) position = hit.point;
            direction = transform.root.position - position;
            if (direction.sqrMagnitude < MinAimDirectionSqrMagnitude)
            {
                position = ray.origin;
                direction = ray.direction;
            }
            else
            {
                position += direction.normalized * transform.localPosition.z;
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
