using UnityEngine;

/// <summary>Defines the summon, unsummon, and ability actions shared by soul-core creatures.</summary>
public interface ISoulCoreCreature
{
    string SoulCoreCreatureId { get; }
    bool IsSoulCoreSummoned { get; }
    bool CanActivateSoulCoreAbility { get; }

    /// <summary>Initializes a newly spawned creature at its requested position and target.</summary>
    void Summon(Vector3 spawnPosition, Transform target);

    /// <summary>Removes this summoned creature from play.</summary>
    void Unsummon();

    /// <summary>Activates this creature's ability.</summary>
    void ActivateSoulCoreAbility();
}
