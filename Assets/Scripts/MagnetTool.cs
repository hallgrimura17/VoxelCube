using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Pulls nearby resource cubes toward the cursor while selected and left-click is held.</summary>
public sealed class MagnetTool : PlayerTool
{
    private const float MaxRaycastDistance = 500f;
    private const float PullTargetHeightOffset = 1f;
    private const float StartingPullSpeed = 8f;
    private const int GroundLayerIndex = 3;
    private const int VoxelLayerIndex = 7;
    [SerializeField] private LayerMask cameraCollisionLayers = (1 << GroundLayerIndex) | (1 << VoxelLayerIndex);
    private float magnetRange;
    private float magnetPullSpeed = StartingPullSpeed;
    private Camera mainCamera;

    public override string DisplayName => "Magnet";
    public override int HotbarOrder => 1;
    public override bool IsUnlocked => UpgradeController.Instance.IsInitialized && UpgradeController.Instance.GetLevel("magnet_field") > 0;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (!IsSelected || !Mouse.current.leftButton.isPressed || !IsUnlocked || UIController.Instance.IsGameplayInputBlocked)
        {
            GameController.instance.ClearResourceAttraction();
            return;
        }

        var ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit, MaxRaycastDistance, cameraCollisionLayers, QueryTriggerInteraction.Ignore))
        {
            GameController.instance.ClearResourceAttraction();
            return;
        }

        GameController.instance.SetResourceAttraction(hit.point + Vector3.up * PullTargetHeightOffset, magnetRange, magnetPullSpeed);
    }



    /// <summary>Restores the Magnet's locked defaults before upgrade effects are recalculated.</summary>
    public void ResetUpgradeEffects()
    {
        magnetRange = 0f;
        magnetPullSpeed = StartingPullSpeed;
        GameController.instance.ClearResourceAttraction();
    }

    /// <summary>Applies the initial radius when the Magnet is unlocked.</summary>
    public void ApplyMagnetUnlock(float amount)
    {
        magnetRange += amount;
    }

    /// <summary>Increases the Magnet's radius by the purchased upgrade amount.</summary>
    public void ApplyMagnetRangeUpgrade(float amount)
    {
        magnetRange += amount;
    }

    /// <summary>Increases the Magnet's pull speed by the purchased upgrade amount.</summary>
    public void ApplyMagnetPullSpeedUpgrade(float amount)
    {
        magnetPullSpeed += amount;
    }
}
