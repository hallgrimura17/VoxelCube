using Assets.Resources;
using UnityEngine;

/// <summary>Orbits the cube, fires mining shots, and can add a periodic high-damage beam attack.</summary>
public class UFOController : MonoBehaviour, ISoulCoreCreature
{
    public const string CreatureId = "UFO";
    private const float BeamRange = 200f;
    private const float BeamImpactRadius = 2.5f;
    [SerializeField] private Transform orbitTarget;
    [SerializeField] private Vector3 orbitCenterOffset = new (8f, 8f, 8f);
    [SerializeField] private RayProjectile projectilePrefab;
    [SerializeField] private float orbitRadius = 36f;
    [SerializeField] private float orbitSpeed = 90f;
    [SerializeField] private Vector2 directionChangeInterval = new (2f, 5f);
    [SerializeField] private float discSpinSpeed = 180f;
    [SerializeField] private float bobAmplitude = 0.5f;
    [SerializeField] private float bobFrequency = 2f;
    [SerializeField] private float fireInterval = 2.5f;
    [SerializeField] private float projectilePower = 0.4f;
    [SerializeField] private float projectileLaunchOffset = 0.55f;
    [SerializeField] private float beamInterval = 6f;
    [SerializeField] private float beamDuration = 0.45f;
    private Vector3 orbitDirection;
    private Vector3 orbitTangent;
    private Vector3 targetOrbitTangent;
    private LineRenderer beamLine;
    private Material beamMaterial;
    private float nextDirectionChange;
    private float nextShotTime;
    private float nextBeamTime;
    private float beamTimeRemaining;
    private float beamDamage;
    private bool beamUpgradePurchased;
    private bool soulCoreSummoned;
    private bool CanFireAtCube => !TerrainChunkManager.instance.IsCollapsing && !TerrainChunkManager.instance.IsCubeCollapsed;

    public string SoulCoreCreatureId => CreatureId;
    public bool IsSoulCoreSummoned => soulCoreSummoned;
    public bool CanActivateSoulCoreAbility => beamUpgradePurchased && CanFireAtCube;

    /// <summary>Initializes this UFO as a summoned creature with its scene orbit target.</summary>
    public void Summon(Vector3 spawnPosition, Transform target)
    {
        transform.position = spawnPosition;
        orbitTarget = target;
        soulCoreSummoned = true;
    }

    /// <summary>Destroys this summoned UFO.</summary>
    public void Unsummon()
    {
        soulCoreSummoned = false;
        Destroy(gameObject);
    }

    /// <summary>Activates the UFO's purchased beam ability.</summary>
    public void ActivateSoulCoreAbility()
    {
        ActivateBeam();
    }

    void Start()
    {
        var orbitCenter = orbitTarget.position + orbitCenterOffset;
        orbitDirection = (transform.position - orbitCenter).normalized;
        orbitTangent = Vector3.ProjectOnPlane(Random.onUnitSphere, orbitDirection).normalized;
        targetOrbitTangent = orbitTangent;
        transform.position = orbitCenter + orbitDirection * orbitRadius;
        nextDirectionChange = Time.time + Random.Range(directionChangeInterval.x, directionChangeInterval.y);
        nextShotTime = Time.time + fireInterval;
        nextBeamTime = Time.time + beamInterval;
        CreateBeamVisual();
        SoundManager.instance.PlaySoundClipAtLocation(transform.position, "UFO");
    }

    void Update()
    {
        var orbitCenter = orbitTarget.position + orbitCenterOffset;
        var deltaTime = Time.deltaTime;
        if (Time.time >= nextDirectionChange)
        {
            targetOrbitTangent = Vector3.ProjectOnPlane(Random.onUnitSphere, orbitDirection).normalized;
            nextDirectionChange = Time.time + Random.Range(directionChangeInterval.x, directionChangeInterval.y);
        }

        orbitTangent = Vector3.RotateTowards(orbitTangent, targetOrbitTangent, orbitSpeed * Mathf.Deg2Rad * deltaTime, 0f).normalized;
        var rotationAxis = Vector3.Cross(orbitDirection, orbitTangent).normalized;
        var orbitRotation = Quaternion.AngleAxis(orbitSpeed * deltaTime, rotationAxis);
        orbitDirection = orbitRotation * orbitDirection;
        orbitTangent = orbitRotation * orbitTangent;
        targetOrbitTangent = orbitRotation * targetOrbitTangent;
        transform.position = orbitCenter + orbitDirection * orbitRadius + Vector3.up * (Mathf.Sin(Time.time * bobFrequency) * bobAmplitude);
        var shotDirection = (orbitCenter - transform.position).normalized;
        var undersideRotation = Quaternion.FromToRotation(Vector3.down, shotDirection);
        var discSpinRotation = Quaternion.AngleAxis(Time.time * discSpinSpeed, Vector3.down);
        transform.rotation = undersideRotation * discSpinRotation;

        if (CanFireAtCube && Time.time >= nextShotTime)
        {
            var projectilePosition = transform.position + shotDirection * projectileLaunchOffset;
            var projectile = Instantiate(projectilePrefab, projectilePosition, Quaternion.LookRotation(shotDirection));
            projectile.Initialize(projectilePower);
            nextShotTime = Time.time + fireInterval;
        }

        if (beamLine.enabled)
        {
            beamTimeRemaining -= deltaTime;
            if (beamTimeRemaining <= 0f) beamLine.enabled = false;
        }
        if (CanFireAtCube && beamUpgradePurchased && Time.time >= nextBeamTime) FireBeam(orbitCenter);
    }

    /// <summary>Enables the UFO beam and increases its mining damage by the purchased upgrade amount.</summary>
    public void ApplyBeamUpgrade(float amount)
    {
        beamUpgradePurchased = true;
        beamDamage += amount;
    }

    /// <summary>Restores the beam upgrade to its unpurchased state.</summary>
    public void ResetUpgradeEffects()
    {
        beamUpgradePurchased = false;
        beamDamage = 0f;
        beamTimeRemaining = 0f;
        if (beamLine != null) beamLine.enabled = false;
    }

    /// <summary>Immediately fires the purchased beam ability toward the cube.</summary>
    public void ActivateBeam()
    {
        if (!beamUpgradePurchased) return;
        FireBeam(orbitTarget.position + orbitCenterOffset);
    }

    void CreateBeamVisual()
    {
        var beamObject = new GameObject("UFOBeam");
        beamObject.transform.SetParent(transform, false);
        beamLine = beamObject.AddComponent<LineRenderer>();
        beamLine.useWorldSpace = true;
        beamLine.positionCount = 2;
        beamLine.widthMultiplier = 0.45f;
        beamLine.numCapVertices = 4;
        beamLine.startColor = new Color(0.28f, 0.86f, 1f, 0.95f);
        beamLine.endColor = new Color(0.62f, 0.97f, 1f, 0.8f);
        beamMaterial = new Material(Shader.Find("Sprites/Default"));
        beamLine.sharedMaterial = beamMaterial;
        beamLine.enabled = false;
    }

    void FireBeam(Vector3 targetPosition)
    {
        if (!CanFireAtCube) return;
        var direction = (targetPosition - transform.position).normalized;
        var hits = Physics.RaycastAll(transform.position, direction, BeamRange, ~0, QueryTriggerInteraction.Ignore);
        var closestDistance = float.PositiveInfinity;
        var foundHit = false;
        var targetHit = default(RaycastHit);
        foreach (var hit in hits)
        {
            if (hit.transform.root == transform.root || hit.distance >= closestDistance) continue;
            closestDistance = hit.distance;
            targetHit = hit;
            foundHit = true;
        }
        if (foundHit)
        {
            beamLine.SetPosition(0, transform.position);
            beamLine.SetPosition(1, targetHit.point);
            beamLine.enabled = true;
            beamTimeRemaining = beamDuration;
            VoxelModifier.MineChunksAtPoint(targetHit.point, targetHit.normal, BeamImpactRadius, beamDamage);
        }
        nextBeamTime = Time.time + beamInterval;
    }

    void OnDestroy()
    {
        Destroy(beamMaterial);
    }
}
