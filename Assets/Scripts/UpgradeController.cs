using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
internal sealed class UpgradeProgressSave
{
    public List<UpgradeProgressEntry> levels = new();
}

[Serializable]
internal sealed class UpgradeProgressEntry
{
    public string nodeId;
    public int level;
}

/// <summary>Tracks upgrade levels, persists purchases, and applies their gameplay effects.</summary>
public class UpgradeController : MonoBehaviour
{
    public static UpgradeController Instance { get; private set; }
    private const string UpgradeSaveFileName = "upgrades.json";
    private readonly Dictionary<string, UpgradeNodeDefinition> definitionsById = new ();
    private readonly Dictionary<string, int> levelsById = new ();
    private bool initialized;

    public bool IsInitialized => initialized;

    void Awake()
    {
        Instance = this;
    }

#if UNITY_EDITOR
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.f5Key.wasPressedThisFrame || !(keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)) return;
        UpgradeTreeController.Instance.ResetPurchasedUpgradesAndRefund();
    }
#endif

    /// <summary>Initializes upgrade levels from authored defaults or the saved progress file.</summary>
    public void Initialize(UpgradeTreeDefinition treeDefinition)
    {
        var savedProgress = LoadProgress();
        foreach (var definition in treeDefinition.nodes)
        {
            var level = definition.startingLevel;
            foreach (var entry in savedProgress.levels)
            {
                if (entry.nodeId != definition.nodeId) continue;
                level = Mathf.Clamp(entry.level, definition.startingLevel, definition.maxLevel);
                break;
            }
            definitionsById.Add(definition.nodeId, definition);
            levelsById.Add(definition.nodeId, level);
            for (var currentLevel = 0; currentLevel < level; currentLevel++) ApplyEffect(definition.effectType, definition.effectAmountPerLevel);
        }
        initialized = true;
        UIController.Instance.RefreshToolHotbar();
    }

    /// <summary>Returns the current purchased level for an upgrade node.</summary>
    public int GetLevel(string nodeId)
    {
        return levelsById[nodeId];
    }

    /// <summary>Records one purchased level and applies its configured gameplay effect.</summary>
    public void ApplyPurchasedLevel(string nodeId)
    {
        var definition = definitionsById[nodeId];
        levelsById[nodeId]++;
        ApplyEffect(definition.effectType, definition.effectAmountPerLevel);
        SaveProgress();
        UIController.Instance.RefreshToolHotbar();
        SoulCoreSystem.Instance.RefreshAbilityUnlocks();
    }

    /// <summary>Resets purchased levels to authored starting levels and returns the total currency refund.</summary>
    public int ResetPurchasedUpgrades()
    {
        var refund = 0;
        foreach (var definition in definitionsById.Values)
        {
            var currentLevel = levelsById[definition.nodeId];
            for (var level = definition.startingLevel; level < currentLevel; level++) refund += definition.levelCosts[level];
            levelsById[definition.nodeId] = definition.startingLevel;
        }

        GameController.instance.ResetUpgradeEffects();
        foreach (var blaster in FindObjectsByType<BlasterTool>()) blaster.ResetUpgradeEffects();
        foreach (var magnet in FindObjectsByType<MagnetTool>()) magnet.ResetUpgradeEffects();
        foreach (var ufo in FindObjectsByType<UFOController>()) ufo.ResetUpgradeEffects();
        foreach (var definition in definitionsById.Values)
            for (var level = 0; level < definition.startingLevel; level++) ApplyEffect(definition.effectType, definition.effectAmountPerLevel);

        SaveProgress();
        UIController.Instance.RefreshToolHotbar();
        SoulCoreSystem.Instance.RefreshAbilityUnlocks();
        return refund;
    }

    /// <summary>Applies purchased ability upgrades to a newly spawned UFO.</summary>
    public void ApplyCurrentUpgrades(UFOController ufo)
    {
        if (levelsById["ufo_beam"] > 0) ufo.ApplyBeamUpgrade(definitionsById["ufo_beam"].effectAmountPerLevel);
    }

    private static string SavePath => Path.Combine(Application.persistentDataPath, UpgradeSaveFileName);

    private static UpgradeProgressSave LoadProgress()
    {
        return File.Exists(SavePath) ? JsonUtility.FromJson<UpgradeProgressSave>(File.ReadAllText(SavePath)) : new UpgradeProgressSave();
    }

    private void SaveProgress()
    {
        var save = new UpgradeProgressSave();
        foreach (var level in levelsById) save.levels.Add(new UpgradeProgressEntry { nodeId = level.Key, level = level.Value });
        File.WriteAllText(SavePath, JsonUtility.ToJson(save, true));
    }

    void ApplyEffect(UpgradeEffectType effect, float amount)
    {
        GameController.instance.ApplyUpgradeEffect(effect, amount);
        switch (effect)
        {
            case UpgradeEffectType.ProjectileCount:
                foreach (var spawner in FindObjectsByType<BlasterTool>()) spawner.AddProjectileCount(Mathf.RoundToInt(amount));
                break;
            case UpgradeEffectType.FireRate:
                foreach (var spawner in FindObjectsByType<BlasterTool>()) spawner.ApplyFireRateUpgrade(amount);
                break;

            case UpgradeEffectType.UfoBeam:
                foreach (var ufo in FindObjectsByType<UFOController>()) ufo.ApplyBeamUpgrade(amount);
                break;
            case UpgradeEffectType.Magnet:
                foreach (var magnet in FindObjectsByType<MagnetTool>()) magnet.ApplyMagnetUnlock(amount);
                break;
            case UpgradeEffectType.MagnetRange:
                foreach (var magnet in FindObjectsByType<MagnetTool>()) magnet.ApplyMagnetRangeUpgrade(amount);
                break;
            case UpgradeEffectType.MagnetPullSpeed:
                foreach (var magnet in FindObjectsByType<MagnetTool>()) magnet.ApplyMagnetPullSpeedUpgrade(amount);
                break;
            case UpgradeEffectType.Placeholder:
                break;
        }
    }
}
