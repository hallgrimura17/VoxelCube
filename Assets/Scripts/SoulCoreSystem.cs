using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Manages soul-core creature balances, saved summons, and shared creature actions.</summary>
public class SoulCoreSystem : MonoBehaviour
{
    private const int StartingSoulCores = 0;

    private const float DefaultAbilityCooldown = 600f;
    private const string SoulCoreSaveKey = "VoxelCube.SoulCores.v2";
    private const string PreviousSoulCoreSaveKey = "VoxelCube.SoulCores";
    private const string UfoCountSaveKey = "VoxelCube.UFOs.v2";
    private const string PreviousUfoCountSaveKey = "VoxelCube.UFOs";
    private const string CreatureCountSaveKeyPrefix = "VoxelCube.SoulCoreCreatureCount.";

    [Serializable]
    private sealed class CreatureDefinition
    {
        public string creatureId;
        public string displayName;
        public int soulCoreCost = 1;
        public GameObject prefab;
        public Transform target;
        public string abilityUpgradeId;
        public string abilityName;
        public float abilityCooldown = DefaultAbilityCooldown;
    }

    [SerializeField] private CreatureDefinition[] creatures;
    private readonly Dictionary<string, int> creatureCounts = new();
    private readonly Dictionary<string, float> abilityCooldowns = new();
    [SerializeField] private int soulCores = StartingSoulCores;

    public static SoulCoreSystem Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        soulCores = PlayerPrefs.HasKey(SoulCoreSaveKey) ? PlayerPrefs.GetInt(SoulCoreSaveKey) : Mathf.Max(0, PlayerPrefs.GetInt(PreviousSoulCoreSaveKey, 1) - 1);
    }

    /// <summary>Restores creature summons and connects each configured creature card.</summary>
    private void Start()
    {
        for (var index = 0; index < creatures.Length; index++)
        {
            var definition = creatures[index];
            creatureCounts.Add(definition.creatureId, CountSummonedCreatures(definition.creatureId));
            abilityCooldowns.Add(definition.creatureId, 0f);
            UIController.Instance.BindSoulCoreCreature(definition.creatureId, () => SummonCreature(definition), () => UnsummonCreature(definition), () => ActivateCreatureAbility(definition));
            RestoreCreatureCount(definition);
        }
        SaveSoulCores();
        RefreshDisplay();
    }

    private void Update()
    {
        for (var index = 0; index < creatures.Length; index++)
        {
            var definition = creatures[index];
            var previousCooldown = abilityCooldowns[definition.creatureId];
            if (previousCooldown <= 0f) continue;
            var cooldown = Mathf.Max(0f, previousCooldown - Time.deltaTime);
            abilityCooldowns[definition.creatureId] = cooldown;
            if (Mathf.CeilToInt(previousCooldown) != Mathf.CeilToInt(cooldown)) RefreshCreatureDisplay(definition);
        }
    }

    /// <summary>Refreshes creature ability controls after upgrade levels change.</summary>
    public void RefreshAbilityUnlocks()
    {
        RefreshDisplay();
    }

    /// <summary>Adds soul-core tokens to the player's balance.</summary>
    public void AddSoulCores(int amount)
    {
        soulCores += amount;
        SaveSoulCores();
        RefreshDisplay();
    }

    private void SummonCreature(CreatureDefinition definition)
    {
        if (soulCores < definition.soulCoreCost) return;
        SpawnCreature(definition);
        creatureCounts[definition.creatureId]++;
        soulCores -= definition.soulCoreCost;
        SaveSoulCores();
        RefreshDisplay();
    }

    private void UnsummonCreature(CreatureDefinition definition)
    {
        var summonedCreatures = FindSummonedCreatures(definition.creatureId);
        if (summonedCreatures.Count == 0) return;
        summonedCreatures[0].Unsummon();
        creatureCounts[definition.creatureId]--;
        soulCores += definition.soulCoreCost;
        SaveSoulCores();
        RefreshDisplay();
    }

    private void ActivateCreatureAbility(CreatureDefinition definition)
    {
        if (!IsAbilityUnlocked(definition) || abilityCooldowns[definition.creatureId] > 0f) return;
        var summonedCreatures = FindSummonedCreatures(definition.creatureId);
        var activated = false;
        foreach (var creature in summonedCreatures)
        {
            if (!creature.CanActivateSoulCoreAbility) continue;
            creature.ActivateSoulCoreAbility();
            activated = true;
        }
        if (activated) abilityCooldowns[definition.creatureId] = definition.abilityCooldown;
        RefreshCreatureDisplay(definition);
    }

    private void RestoreCreatureCount(CreatureDefinition definition)
    {
        var savedCount = GetSavedCreatureCount(definition);
        while (creatureCounts[definition.creatureId] < savedCount)
        {
            SpawnCreature(definition);
            creatureCounts[definition.creatureId]++;
        }
    }

    private void SpawnCreature(CreatureDefinition definition)
    {
        var spawnPosition = definition.prefab.transform.position + definition.prefab.transform.right * (creatureCounts[definition.creatureId] * 4f);
        var spawnedObject = Instantiate(definition.prefab, spawnPosition, definition.prefab.transform.rotation);
        spawnedObject.name = $"{definition.displayName}_{creatureCounts[definition.creatureId] + 1}";
        var creature = GetSoulCoreCreature(spawnedObject);
        creature.Summon(spawnPosition, definition.target);
        if (creature is UFOController ufo && UpgradeController.Instance.IsInitialized) UpgradeController.Instance.ApplyCurrentUpgrades(ufo);
    }

    private int GetSavedCreatureCount(CreatureDefinition definition)
    {
        var key = CreatureCountSaveKeyPrefix + definition.creatureId;
        if (PlayerPrefs.HasKey(key)) return PlayerPrefs.GetInt(key);
        if (definition.creatureId != UFOController.CreatureId) return 0;
        if (PlayerPrefs.HasKey(UfoCountSaveKey)) return PlayerPrefs.GetInt(UfoCountSaveKey);
        return Mathf.Max(0, PlayerPrefs.GetInt(PreviousUfoCountSaveKey, 0) - 1);
    }

    private int CountSummonedCreatures(string creatureId)
    {
        return FindSummonedCreatures(creatureId).Count;
    }

    private List<ISoulCoreCreature> FindSummonedCreatures(string creatureId)
    {
        var summonedCreatures = new List<ISoulCoreCreature>();
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>())
            if (behaviour is ISoulCoreCreature creature && creature.SoulCoreCreatureId == creatureId && creature.IsSoulCoreSummoned) summonedCreatures.Add(creature);
        return summonedCreatures;
    }

    private ISoulCoreCreature GetSoulCoreCreature(GameObject creatureObject)
    {
        foreach (var behaviour in creatureObject.GetComponents<MonoBehaviour>())
            if (behaviour is ISoulCoreCreature creature) return creature;
        return null;
    }

    private bool IsAbilityUnlocked(CreatureDefinition definition)
    {
        return !string.IsNullOrEmpty(definition.abilityUpgradeId) && UpgradeController.Instance.IsInitialized && UpgradeController.Instance.GetLevel(definition.abilityUpgradeId) > 0;
    }

    private void SaveSoulCores()
    {
        PlayerPrefs.SetInt(SoulCoreSaveKey, soulCores);
        for (var index = 0; index < creatures.Length; index++)
        {
            var definition = creatures[index];
            PlayerPrefs.SetInt(CreatureCountSaveKeyPrefix + definition.creatureId, creatureCounts[definition.creatureId]);
        }
        PlayerPrefs.Save();
    }

    private void RefreshDisplay()
    {
        UIController.Instance.RefreshSoulCoreCount(soulCores);
        for (var index = 0; index < creatures.Length; index++) RefreshCreatureDisplay(creatures[index]);
    }

    private void RefreshCreatureDisplay(CreatureDefinition definition)
    {
        var cooldown = abilityCooldowns[definition.creatureId];
        var abilityUnlocked = IsAbilityUnlocked(definition);
        var abilityAvailable = abilityUnlocked && cooldown <= 0f && FindSummonedCreatures(definition.creatureId).Exists(creature => creature.CanActivateSoulCoreAbility);
        var cooldownText = cooldown > 0f ? FormatCooldown(cooldown) : string.Empty;
        UIController.Instance.RefreshSoulCoreCreature(definition.creatureId, definition.displayName, creatureCounts[definition.creatureId], definition.soulCoreCost, soulCores, definition.abilityName, cooldownText, abilityUnlocked, abilityAvailable);
    }

    private static string FormatCooldown(float remaining)
    {
        var totalSeconds = Mathf.CeilToInt(remaining);
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}
