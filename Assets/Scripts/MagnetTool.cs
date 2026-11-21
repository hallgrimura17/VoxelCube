using Assets.Resources;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Transforms;

/// <summary>Pulls the clicked resource cube toward the cursor, with radius upgrades for nearby cubes.</summary>
public sealed class MagnetTool : PlayerTool
{
    private enum MagnetMode { Select, Pull }
    private const float MaxRaycastDistance = 500f;
    private const float PullTargetHeightOffset = 1f;
    private const float StartingPullSpeed = 8f;
    private const float SelectModeSurfaceClearance = 0.1f;
    private const float SelectModeEffectScale = 0.5f;
    private const float MagnetEffectSurfaceOffset = 0.1f;
    [SerializeField] private float startingMagnetRange;
    private const float SelectModePullSpeedMultiplier = 3f;
    private const int GroundLayerIndex = 3;
    private const int WallLayerIndex = 7;
    [SerializeField] private LayerMask pullCollisionLayers = (1 << GroundLayerIndex) | (1 << WallLayerIndex);
    private MagnetMode mode;
    private float magnetRange;
    private float magnetPullSpeed = StartingPullSpeed;
    private Camera mainCamera;
    private EntityManager entityManager;
    private EntityQuery physicsWorldQuery;
    private ParticleSystem magnetEffect;
    private Entity grabbedResource = Entity.Null;

    public override string DisplayName => "Magnet";
    /// <summary>Returns whether the Magnet is in Pull mode.</summary>
    public bool IsPullMode => mode == MagnetMode.Pull;
    public override int HotbarOrder => 2;
    public override bool IsUnlocked => UpgradeController.Instance.IsInitialized && UpgradeController.Instance.GetLevel("magnet_field") > 0;

    private void Awake()
    {
        magnetRange = startingMagnetRange;
    }

    private void Start()
    {
        mainCamera = Camera.main;
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        physicsWorldQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
        magnetEffect = ParticleManager.instance.GetParticleSystem("Magnet");
        var effectMain = magnetEffect.main;
        effectMain.loop = true;
        effectMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
    }

    /// <summary>Toggles between Select and Pull mode.</summary>
    public override void ToggleMode()
    {
        SetMode(mode == MagnetMode.Select ? MagnetMode.Pull : MagnetMode.Select);
    }

    /// <summary>Switches the Magnet to Select mode.</summary>
    public void SetSelectMode()
    {
        SetMode(MagnetMode.Select);
    }

    /// <summary>Switches the Magnet to Pull mode.</summary>
    public void SetPullMode()
    {
        SetMode(MagnetMode.Pull);
    }

    private void SetMode(MagnetMode newMode)
    {
        mode = newMode;
        grabbedResource = Entity.Null;
        GameController.instance.ClearResourceAttraction();
        StopMagnetEffect();
        ToolHotbar.Instance.RefreshModeControls();
    }

    private void Update()
    {
        if (!IsSelected || !Mouse.current.leftButton.isPressed || !IsUnlocked || UIController.Instance.IsGameplayInputBlocked)
        {
            grabbedResource = Entity.Null;
            GameController.instance.ClearResourceAttraction();
            StopMagnetEffect();
            return;
        }

        var ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (mode == MagnetMode.Select)
        {
            UpdateSelectMode(ray);
            return;
        }

        grabbedResource = Entity.Null;
        if (!Physics.Raycast(ray, out var hit, MaxRaycastDistance, pullCollisionLayers, QueryTriggerInteraction.Ignore))
        {
            GameController.instance.ClearResourceAttraction();
            StopMagnetEffect();
            return;
        }
        GameController.instance.SetResourceAttraction(Entity.Null, hit.point + mainCamera.transform.up * PullTargetHeightOffset, magnetRange, magnetPullSpeed);
        UpdateMagnetEffect(hit.point + hit.normal * MagnetEffectSurfaceOffset, magnetRange, hit.normal);
    }

    private void UpdateSelectMode(UnityEngine.Ray ray)
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!TryGetResourceHit(ray, out grabbedResource, out _))
            {
                GameController.instance.ClearResourceAttraction();
                StopMagnetEffect();
                return;
            }
        }
        if (grabbedResource == Entity.Null)
        {
            GameController.instance.ClearResourceAttraction();
            StopMagnetEffect();
            return;
        }

        if (!Physics.Raycast(ray, out var hit, MaxRaycastDistance, pullCollisionLayers, QueryTriggerInteraction.Ignore))
        {
            GameController.instance.ClearResourceAttraction();
            StopMagnetEffect();
            return;
        }
        var cubeHalfExtent = entityManager.GetComponentData<LocalTransform>(grabbedResource).Scale * 0.5f + SelectModeSurfaceClearance;
        var targetPosition = hit.point + hit.normal * cubeHalfExtent;
        GameController.instance.SetResourceAttraction(grabbedResource, targetPosition, magnetRange, magnetPullSpeed * SelectModePullSpeedMultiplier);
        UpdateMagnetEffect(targetPosition + hit.normal * MagnetEffectSurfaceOffset, SelectModeEffectScale, hit.normal);
    }

    private void UpdateMagnetEffect(Vector3 position, float scale, Vector3 surfaceNormal)
    {
        ParticleManager.instance.MoveParticleSystem(magnetEffect, position, Quaternion.LookRotation(surfaceNormal));
        magnetEffect.transform.localScale = Vector3.one * scale;
        if (!magnetEffect.isPlaying) magnetEffect.Play(true);
    }

    private void StopMagnetEffect()
    {
        if (magnetEffect.isPlaying) magnetEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private bool TryGetResourceHit(UnityEngine.Ray ray, out Entity targetEntity, out Vector3 hitPoint)
    {
        var input = new RaycastInput
        {
            Start = new float3(ray.origin.x, ray.origin.y, ray.origin.z),
            End = new float3(ray.GetPoint(MaxRaycastDistance).x, ray.GetPoint(MaxRaycastDistance).y, ray.GetPoint(MaxRaycastDistance).z),
            Filter = new CollisionFilter { BelongsTo = uint.MaxValue, CollidesWith = uint.MaxValue, GroupIndex = 0 }
        };
        var collisionWorld = physicsWorldQuery.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        var collector = new ResourceRaycastCollector(entityManager);
        if (collisionWorld.CastRay(input, ref collector) && collector.NumHits > 0)
        {
            targetEntity = collector.ClosestHit.Entity;
            hitPoint = new Vector3(collector.ClosestHit.Position.x, collector.ClosestHit.Position.y, collector.ClosestHit.Position.z);
            return true;
        }
        targetEntity = Entity.Null;
        hitPoint = default;
        return false;
    }

    private struct ResourceRaycastCollector : ICollector<Unity.Physics.RaycastHit>
    {
        private readonly EntityManager entityManager;
        public bool EarlyOutOnFirstHit => false;
        public float MaxFraction { get; private set; }
        public int NumHits { get; private set; }
        public Unity.Physics.RaycastHit ClosestHit { get; private set; }

        public ResourceRaycastCollector(EntityManager entityManager)
        {
            this.entityManager = entityManager;
            MaxFraction = 1f;
            NumHits = 0;
            ClosestHit = default;
        }

        public bool AddHit(Unity.Physics.RaycastHit hit)
        {
            if (!entityManager.HasComponent<CubeResource>(hit.Entity) || hit.Fraction > MaxFraction) return false;
            MaxFraction = hit.Fraction;
            NumHits = 1;
            ClosestHit = hit;
            return true;
        }
    }


    private void OnDestroy()
    {
        physicsWorldQuery.Dispose();
    }

    /// <summary>Restores the Magnet's locked defaults before upgrade effects are recalculated.</summary>
    public void ResetUpgradeEffects()
    {
        magnetRange = startingMagnetRange;
        magnetPullSpeed = StartingPullSpeed;
        GameController.instance.ClearResourceAttraction();
    }

    /// <summary>Applies the configured starting radius when the Magnet is unlocked.</summary>
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
