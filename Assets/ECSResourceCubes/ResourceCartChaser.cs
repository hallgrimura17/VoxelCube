using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>Moves the summoned cart toward nearby resource cubes without collecting them.</summary>
public sealed class ResourceCartChaser : MonoBehaviour, ISoulCoreCreature
{
    private const float ChaseRange = 30f;
    private const float MovementSpeed = 5f;
    public const string CreatureId = "Cart";
    private EntityQuery resourceQuery;
    private bool soulCoreSummoned;

    public string SoulCoreCreatureId => CreatureId;
    public bool IsSoulCoreSummoned => soulCoreSummoned;
    public bool CanActivateSoulCoreAbility => false;

    /// <summary>Initializes the summoned cart at its requested position.</summary>
    public void Summon(Vector3 spawnPosition, Transform target)
    {
        transform.position = spawnPosition;
        soulCoreSummoned = true;
    }

    /// <summary>Removes the summoned cart from play.</summary>
    public void Unsummon()
    {
        soulCoreSummoned = false;
        Destroy(gameObject);
    }

    /// <summary>Activates the cart's optional ability.</summary>
    public void ActivateSoulCoreAbility()
    {
    }

    private void Start()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        resourceQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CubeResource>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<Prefab>());
    }

    private void Update()
    {
        var resourceTransforms = resourceQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var nearestPosition = transform.position;
        var nearestDistanceSquared = ChaseRange * ChaseRange;
        var hasTarget = false;
        for (var index = 0; index < resourceTransforms.Length; index++)
        {
            var resourcePosition = (Vector3)resourceTransforms[index].Position;
            var distanceSquared = (resourcePosition - transform.position).sqrMagnitude;
            if (distanceSquared >= nearestDistanceSquared) continue;
            nearestDistanceSquared = distanceSquared;
            nearestPosition = resourcePosition;
            hasTarget = true;
        }
        resourceTransforms.Dispose();
        if (hasTarget) transform.position = Vector3.MoveTowards(transform.position, nearestPosition, MovementSpeed * Time.deltaTime);
    }
}
