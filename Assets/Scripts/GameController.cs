using Assets.Resources;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class GameController : MonoBehaviour
{
    private const string HighestUnlockedPrestigeSaveKey = "VoxelCube.HighestUnlockedPrestige.v1";
    private const string SelectedPrestigeSaveKey = "VoxelCube.SelectedPrestige.v1";
    public static GameController instance;


    public int currency = 0, Prestige = 0;
    public int HighestUnlockedPrestige { get; private set; }
    public int resourceValue => 1 << Prestige;
    public float CubeHardnessMultiplier => Mathf.Pow(2f, Prestige);
    public float miningDamageMultiplier = 1f;
    public float resourceValueMultiplier = 1f;
    public float resourceSpawnImpulse = 3f;
    public float resourceCubeCenterPushImpulse = 3f;
    public float disconnectedResourcePushMultiplier = 0.25f;
    [SerializeField, Min(1f)] private float resourceCubeMergeSizeMultiplier = 2f;
    [SerializeField] private Material resourceCubeMaterial;
    internal float ResourceCubeMergeSizeMultiplier => resourceCubeMergeSizeMultiplier;

    public static Vector3 CubeOffset => 8 * Vector3.one;

    void Awake()
    {
        instance = this;
        HighestUnlockedPrestige = Mathf.Max(Prestige, PlayerPrefs.GetInt(HighestUnlockedPrestigeSaveKey, Prestige));
        Prestige = Mathf.Clamp(PlayerPrefs.GetInt(SelectedPrestigeSaveKey, HighestUnlockedPrestige), 0, HighestUnlockedPrestige);
        CubeResourceEntityFactory.Initialize(resourceCubeMaterial);
    }

    private void OnDestroy()
    {
        CubeResourceEntityFactory.Shutdown();
    }

    void Start()
    {
        UpdateCurrencyDisplay();
    }
    public AudioSource DropAudios;
    internal void SpawnResources(IReadOnlyList<VoxelModification> modifications, Vector3 spawnOrigin, Vector3 fallbackDirection)
    {
        CubeResourceEntityFactory.SpawnResources(modifications, spawnOrigin, fallbackDirection, CubeOffset, resourceSpawnImpulse, resourceCubeCenterPushImpulse);
        SoundManager.instance.PlaySoundClip("VoxelDrop", DropAudios);
    }

    internal void SpawnDisconnectedResources(IReadOnlyList<VoxelModification> modifications, float impulseMultiplier = 1f)
    {
        var pushMultiplier = disconnectedResourcePushMultiplier * impulseMultiplier;
        CubeResourceEntityFactory.SpawnResources(modifications, CubeOffset, Vector3.zero, CubeOffset, resourceSpawnImpulse * pushMultiplier, resourceCubeCenterPushImpulse * pushMultiplier);
    }

    internal void SpawnCollapseResource(VoxelModification modification, Vector3 launchDirection, float impulseMultiplier, int tier)
    {
        var pushMultiplier = disconnectedResourcePushMultiplier * impulseMultiplier;
        CubeResourceEntityFactory.SpawnCollapseResource(modification, launchDirection, resourceSpawnImpulse * pushMultiplier, tier);
    }

    internal void SetResourceAttraction(Entity targetEntity, Vector3 destination, float range, float pullSpeed)
    {
        CubeResourceEntityFactory.SetResourceAttraction(targetEntity, destination, range, pullSpeed);
    }

    internal void ClearResourceAttraction()
    {
        CubeResourceEntityFactory.SetResourceAttraction(Entity.Null, Vector3.zero, 0f, 0f);
    }

    /// <summary>Unlocks and enters the next prestige after completing the current highest unlocked level.</summary>
    public bool TryUnlockNextPrestige()
    {
        if (Prestige < HighestUnlockedPrestige) return false;
        Prestige++;
        HighestUnlockedPrestige = Prestige;
        PlayerPrefs.SetInt(HighestUnlockedPrestigeSaveKey, HighestUnlockedPrestige);
        SaveSelectedPrestige();
        UIController.Instance.RefreshPrestigeControls();
        return true;
    }

    /// <summary>Moves to the next prestige already unlocked by the player.</summary>
    public void RaisePrestige()
    {
        if (Prestige >= HighestUnlockedPrestige) return;
        Prestige++;
        SaveSelectedPrestige();
        UIController.Instance.RefreshPrestigeControls();
    }

    /// <summary>Moves to the previous unlocked prestige.</summary>
    public void LowerPrestige()
    {
        if (Prestige <= 0) return;
        Prestige--;
        SaveSelectedPrestige();
        UIController.Instance.RefreshPrestigeControls();
    }

    /// <summary>Restores all loaded terrain chunks from their source data at the selected prestige.</summary>
    public void ResetCube()
    {
        TerrainChunkManager.instance.ResetCube();
    }

    public void AddCurrency(int amount)
    {
        currency += amount;
        UpdateCurrencyDisplay();
    }


    public bool TrySpendCurrency(int amount)
    { 
        if (currency < amount) return false;
        currency -= amount;
        UpdateCurrencyDisplay();
        return true;
    }

    public int GetCurrency()
    {
        return currency;
    }

    public void ApplyUpgradeEffect(UpgradeEffectType effect, float amount)
    {
        switch (effect)
        {
            case UpgradeEffectType.MiningDamage: miningDamageMultiplier += amount; break;
            case UpgradeEffectType.ResourceValue: resourceValueMultiplier += amount; break;
        }
    }

    internal void ResetUpgradeEffects()
    {
        miningDamageMultiplier = 1f;
        resourceValueMultiplier = 1f;
    }

    private void SaveSelectedPrestige()
    {
        PlayerPrefs.SetInt(SelectedPrestigeSaveKey, Prestige);
        PlayerPrefs.Save();
    }

    void UpdateCurrencyDisplay()
    {
        UIController.Instance.RefreshCurrency(currency);
    }
}
