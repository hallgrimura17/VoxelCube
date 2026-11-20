using System;
using System.Collections.Generic;
using UnityEngine;

public enum UpgradeCategory
{
    Projectiles,
    Carts,
    Currency,
    UFO,
    Magnets
}

public enum UpgradeEffectType
{
    MiningDamage,
    ProjectileCount,
    FireRate,
    ResourceValue,
    CartCollection,
    CartZoomies,
    UfoBeam,
    Magnet,
    CartCannon,
    MagnetRange,
    MagnetPullSpeed
}

[Serializable]
public class UpgradeNodeDefinition
{
    public string nodeId;
    public UpgradeCategory category;
    public string[] prerequisiteNodeIds;
    public Vector2 mapPosition;
    public string title;
    [TextArea] public string description;
    public UpgradeEffectType effectType;
    public float effectAmountPerLevel;
    public int startingLevel;
    public int maxLevel = 1;
    public int[] levelCosts;
}

[CreateAssetMenu(fileName = "UpgradeTree", menuName = "Game/Upgrade Tree")]
public class UpgradeTreeDefinition : ScriptableObject
{
    public List<UpgradeNodeDefinition> nodes = new ();
}
