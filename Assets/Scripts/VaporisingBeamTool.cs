using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Heats resource cubes around the cursor until they vaporise.</summary>
public sealed class VaporisingBeamTool : PlayerTool
{
    private const float MaxRaycastDistance = 500f;
    private const float EmissionIntensity = 5f;
    private const float BeamWidth = 0.12f;
    private const float CameraOriginVerticalOffset = 0.5f;
    private const float BeamActiveVolume = 1f;
    private const float BeamVolumeTransitionSpeed = 8f;
    private const float MinimumVaporisingEffectScale = 0.25f;
    [SerializeField] private float minimumBeamPitch = 1f;
    [SerializeField] private float maximumBeamPitch = 1.75f;
    [SerializeField] private bool useAverageHeatForBeamPitch;
    private const int GroundLayerIndex = 3;
    private const int VoxelLayerIndex = 7;
    [SerializeField] private float vaporisingRadius;
    [SerializeField] private float baseHeatDuration = 2f;
    [SerializeField] private float additionalHeatDurationPerTier = 1f;
    [SerializeField] private float heatDecayDuration = 8f;
    [SerializeField] private LayerMask cameraCollisionLayers = (1 << GroundLayerIndex) | (1 << VoxelLayerIndex);
    private Camera mainCamera;
    private EntityManager entityManager;
    private EntityQuery physicsWorldQuery;
    private bool physicsWorldQueryCreated;
    private CubeResourceVaporisingSystem vaporisingSystem;
    private LineRenderer beamLine;
    private AudioSource beamAudioSource;
    private ParticleSystem vaporisingEffect;
    [SerializeField] private UnityEngine.Material beamMaterial;

    public override string DisplayName => "Vaporising Beam";
    public override int HotbarOrder => 1;
    public override bool IsUnlocked => true;

    private void Start()
    {
        mainCamera = Camera.main;
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        physicsWorldQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
        physicsWorldQueryCreated = true;
        vaporisingSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CubeResourceVaporisingSystem>();
        beamAudioSource = GetComponent<AudioSource>();
        beamAudioSource.volume = 0f;
        beamAudioSource.pitch = minimumBeamPitch;
        beamAudioSource.Play();
        beamLine = GetComponent<LineRenderer>();
        vaporisingEffect = Assets.Resources.ParticleManager.instance.GetParticleSystem("VaporisingBlast");
        var effectMain = vaporisingEffect.main;
        effectMain.loop = true;
        effectMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
        ConfigureBeamVisual();
    }

    private void Update()
    {
        var beamActive = IsSelected && Mouse.current.leftButton.isPressed && !UIController.Instance.IsGameplayInputBlocked;
        beamAudioSource.volume = Mathf.MoveTowards(beamAudioSource.volume, beamActive ? BeamActiveVolume : 0f, BeamVolumeTransitionSpeed * Time.deltaTime);
        var heatProgressForPitch = useAverageHeatForBeamPitch ? vaporisingSystem.AverageHeatProgress : vaporisingSystem.HottestHeatProgress;
        beamAudioSource.pitch = Mathf.Lerp(minimumBeamPitch, maximumBeamPitch, heatProgressForPitch);
        if (!beamActive)
        {
            ClearTarget();
            return;
        }

        var ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        var resourceHit = TryGetResourceHit(ray, out var hitPoint);
        if (!resourceHit) hitPoint = UnityEngine.Physics.Raycast(ray, out var hit, MaxRaycastDistance, cameraCollisionLayers, QueryTriggerInteraction.Ignore) ? hit.point : ray.GetPoint(MaxRaycastDistance);
        CubeResourceEntityFactory.SetVaporisingTarget(true, hitPoint, vaporisingRadius, baseHeatDuration, additionalHeatDurationPerTier, EmissionIntensity, heatDecayDuration);

        beamLine.SetPosition(0, mainCamera.transform.position - mainCamera.transform.up * CameraOriginVerticalOffset);
        beamLine.SetPosition(1, hitPoint);
        beamLine.enabled = true;
        UpdateVaporisingEffect(hitPoint);
    }

    private bool TryGetResourceHit(UnityEngine.Ray ray, out Vector3 hitPoint)
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
            hitPoint = new Vector3(collector.ClosestHit.Position.x, collector.ClosestHit.Position.y, collector.ClosestHit.Position.z);
            return true;
        }
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

    private void UpdateVaporisingEffect(Vector3 position)
    {
        Assets.Resources.ParticleManager.instance.MoveParticleSystem(vaporisingEffect, position, Quaternion.identity);
        var effectScale = Mathf.Max(vaporisingRadius * 2f, MinimumVaporisingEffectScale);
        vaporisingEffect.transform.localScale = Vector3.one * effectScale;
        if (!vaporisingEffect.isPlaying) vaporisingEffect.Play(true);
    }

    private void ClearTarget()
    {
        CubeResourceEntityFactory.SetVaporisingTarget(false, Vector3.zero, vaporisingRadius, baseHeatDuration, additionalHeatDurationPerTier, EmissionIntensity, heatDecayDuration);
        beamLine.enabled = false;
        if (vaporisingEffect.isPlaying) vaporisingEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ConfigureBeamVisual()
    {
        beamLine.useWorldSpace = true;
        beamLine.positionCount = 2;
        beamLine.widthMultiplier = BeamWidth;
        beamLine.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        beamLine.startColor = new Color(1f, 0.55f, 0.12f, 0.95f);
        beamLine.endColor = new Color(1f, 0.9f, 0.3f, 0.95f);
        beamLine.sharedMaterial = beamMaterial;
        beamLine.enabled = false;
    }

    private void OnDestroy()
    {
        if (physicsWorldQueryCreated) physicsWorldQuery.Dispose();
    }
}
