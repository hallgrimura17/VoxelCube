using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Owns shared UI references and exposes UI operations to gameplay systems.</summary>
public class UIController : MonoBehaviour
{
    private const string CameraModeText = "FIRING MODE: From Camera";
    private const string CenterModeText = "FIRING MODE: Through Center";
    public static UIController Instance { get; private set; }
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private TMP_Text soulCoreCountText;
    [SerializeField] private SoulCoreCreatureUIReferences[] soulCoreCreatures;
    [SerializeField] private Image spawnModeIcon;
    [SerializeField] private SpawnModeTooltip spawnModeTooltip;
    private readonly HashSet<GameObject> openPanels = new();

    public bool IsGameplayInputBlocked => openPanels.Count > 0 || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    public bool SpawnFromCamera { get; private set; } = true;

    private void Awake()
    {
        Instance = this;
        foreach (var creatureUI in soulCoreCreatures)
        {
            creatureUI.summonButton.interactable = false;
            creatureUI.unsummonButton.interactable = false;
            creatureUI.abilityButton.gameObject.SetActive(false);
        }
    }

    /// <summary>Registers or unregisters a panel as blocking gameplay input.</summary>
    public void SetPanelOpen(GameObject panel, bool isOpen)
    {
        if (isOpen) openPanels.Add(panel);
        else openPanels.Remove(panel);
    }

    /// <summary>Displays the current currency and refreshes currency-dependent UI.</summary>
    public void RefreshCurrency(int currency)
    {
        currencyText.text = $"{currency:N0}";
        UpgradeTreeController.Instance.RefreshNodes();
    }

    /// <summary>Refreshes the prestige controls after the selected prestige changes.</summary>
    public void RefreshPrestigeControls()
    {
        PrestigeControls.Instance.Refresh();
    }

    /// <summary>Refreshes the hotbar after gameplay tools unlock or change.</summary>
    public void RefreshToolHotbar()
    {
        ToolHotbar.Instance.RefreshTools();
    }

    /// <summary>Connects a creature card's summon, unsummon, and ability actions.</summary>
    public void BindSoulCoreCreature(string creatureId, Action summon, Action unsummon, Action activateAbility)
    {
        var creatureUI = FindSoulCoreCreatureUI(creatureId);
        creatureUI.summonHoldButton.Initialize(summon);
        creatureUI.unsummonHoldButton.Initialize(unsummon);
        creatureUI.abilityButton.onClick.AddListener(() => activateAbility());
    }

    /// <summary>Refreshes the shared soul-core balance label.</summary>
    public void RefreshSoulCoreCount(int soulCores)
    {
        soulCoreCountText.text = $"{soulCores}";
    }

    /// <summary>Refreshes one creature's count, cost, and summon/unsummon/ability controls.</summary>
    public void RefreshSoulCoreCreature(string creatureId, string displayName, int count, int cost, int soulCores, string abilityName, string cooldownText, bool abilityUnlocked, bool abilityInteractable)
    {
        var creatureUI = FindSoulCoreCreatureUI(creatureId);
        creatureUI.countText.text = $"{displayName} [{count}]";
        creatureUI.costText.text = $"{cost}";
        creatureUI.summonButton.interactable = soulCores >= cost;
        creatureUI.unsummonButton.interactable = count > 0;
        creatureUI.abilityButton.gameObject.SetActive(abilityUnlocked);
        creatureUI.abilityButton.interactable = abilityInteractable;
        creatureUI.abilityText.text = string.IsNullOrEmpty(cooldownText) ? abilityName : $"{abilityName}\n{cooldownText}";
    }

    /// <summary>Updates the firing-mode icon and tooltip state.</summary>
    public void SetSpawnMode(bool spawnFromCamera)
    {
        SpawnFromCamera = spawnFromCamera;
        spawnModeIcon.color = spawnFromCamera ? Color.white : Color.gray;
        spawnModeTooltip.RefreshModeText(spawnFromCamera);
    }

    /// <summary>Shows the firing-mode tooltip with the current mode.</summary>
    public void ShowSpawnModeTooltip()
    {
        spawnModeTooltip.Show(SpawnFromCamera);
    }

    /// <summary>Hides the firing-mode tooltip.</summary>
    public void HideSpawnModeTooltip()
    {
        spawnModeTooltip.Hide();
    }

    private SoulCoreCreatureUIReferences FindSoulCoreCreatureUI(string creatureId)
    {
        for (var index = 0; index < soulCoreCreatures.Length; index++)
            if (soulCoreCreatures[index].creatureId == creatureId) return soulCoreCreatures[index];
        return null;
    }
}
