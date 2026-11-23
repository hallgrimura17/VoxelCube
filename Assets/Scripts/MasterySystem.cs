using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
internal sealed class MasteryProgressSave
{
    public int skillPoints;
    public List<MasteryProgressEntry> levels = new();
}

[Serializable]
internal sealed class MasteryProgressEntry
{
    public string creatureId;
    public int level;
}

/// <summary>Persists mastery points and creature levels independently of currency upgrades.</summary>
public sealed class MasterySystem : MonoBehaviour
{
    private const string SaveKey = "VoxelCube.MasteryProgress.v1";
    private const int MasteryCost = 1;
    private readonly Dictionary<string, int> levelsByCreature = new();
    public static MasterySystem Instance { get; private set; }
    public int SkillPoints { get; private set; }

    private void Awake()
    {
        Instance = this;
        var save = PlayerPrefs.HasKey(SaveKey) ? JsonUtility.FromJson<MasteryProgressSave>(PlayerPrefs.GetString(SaveKey)) : new MasteryProgressSave();
        SkillPoints = save.skillPoints;
        foreach (var entry in save.levels) levelsByCreature[entry.creatureId] = Mathf.Clamp(entry.level, 0, GetMaximumLevel(entry.creatureId));
    }

    /// <summary>Returns a creature's current mastery level.</summary>
    public int GetLevel(string creatureId) => levelsByCreature.TryGetValue(creatureId, out var level) ? level : 0;

    /// <summary>Returns the configured mastery cap for a supported creature.</summary>
    public int GetMaximumLevel(string creatureId) => creatureId == ResourceCartChaser.CreatureId || creatureId == UFOController.CreatureId ? 4 : 0;

    /// <summary>Returns whether the player has unlocked the creature's first mastery level.</summary>
    public bool IsCreatureUnlocked(string creatureId) => GetLevel(creatureId) > 0;

    /// <summary>Awards mastery points for clearing a cube.</summary>
    public void AddSkillPoints(int amount)
    {
        SkillPoints += amount;
        SaveProgress();
        if (MasteryTreePanelController.Instance != null) MasteryTreePanelController.Instance.Refresh();
    }

    /// <summary>Purchases exactly the next mastery level for one skill point.</summary>
    public bool TryPurchaseLevel(string creatureId, int targetLevel)
    {
        var currentLevel = GetLevel(creatureId);
        if (targetLevel != currentLevel + 1 || targetLevel > GetMaximumLevel(creatureId) || SkillPoints < MasteryCost) return false;
        SkillPoints -= MasteryCost;
        levelsByCreature[creatureId] = targetLevel;
        SaveProgress();
        if (MasteryTreePanelController.Instance != null) MasteryTreePanelController.Instance.Refresh();
        if (SoulCoreSystem.Instance != null) SoulCoreSystem.Instance.RefreshAbilityUnlocks();
        return true;
    }

    private void SaveProgress()
    {
        var save = new MasteryProgressSave { skillPoints = SkillPoints };
        foreach (var entry in levelsByCreature) save.levels.Add(new MasteryProgressEntry { creatureId = entry.Key, level = entry.Value });
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
        PlayerPrefs.Save();
    }
}
