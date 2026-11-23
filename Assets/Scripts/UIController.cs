using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Owns shared UI references and exposes UI operations to gameplay systems.</summary>
public class UIController : MonoBehaviour
{
    private const float CurrencyPulseDuration = 0.2f;
    private const float CurrencyPulseScale = 1.2f;
    private static readonly Color CurrencyColor = new(1f, 0.82f, 0.2f, 1f);
    public static UIController Instance { get; private set; }
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private TMP_Text soulCoreCountText;
    [SerializeField] private SoulCoreCreatureUIReferences[] soulCoreCreatures;
    [SerializeField] private Image spawnModeIcon;
    private readonly HashSet<GameObject> openPanels = new();
    private Vector3 currencyCounterBaseScale;
    private float currencyPulseTime;

    public bool IsGameplayInputBlocked => openPanels.Count > 0 || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    public bool SpawnFromCamera { get; private set; } = true;

    private void Awake()
    {
        Instance = this;
        currencyCounterBaseScale = currencyText.rectTransform.localScale;
        currencyText.color = CurrencyColor;
        foreach (var creatureUI in soulCoreCreatures)
        {
            creatureUI.summonButton.interactable = false;
            creatureUI.unsummonButton.interactable = false;
            creatureUI.abilityButton.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (currencyPulseTime <= 0f) return;
        currencyPulseTime = Mathf.Max(0f, currencyPulseTime - Time.deltaTime);
        var progress = 1f - currencyPulseTime / CurrencyPulseDuration;
        currencyText.rectTransform.localScale = currencyCounterBaseScale * Mathf.Lerp(CurrencyPulseScale, 1f, progress);
    }

    /// <summary>Returns the currency counter's screen position for reward animations.</summary>
    public Vector2 GetCurrencyCounterScreenPosition()
    {
        return RectTransformUtility.WorldToScreenPoint(null, currencyText.rectTransform.position);
    }

    /// <summary>Triggers a brief scale pulse when a reward reaches the currency counter.</summary>
    public void PulseCurrencyCounter()
    {
        currencyPulseTime = CurrencyPulseDuration;
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
        currencyText.text = $"${currency:N0}";
        currencyText.color = CurrencyColor;
        UpgradeTreeController.Instance.RefreshNodes();
        UpgradeTooltip.Instance.RefreshCurrency();
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
    public void RefreshSoulCoreCreature(string creatureId, string displayName, int count, int cost, int soulCores, string abilityName, string cooldownText, bool creatureUnlocked, bool abilityUnlocked, bool abilityInteractable)
    {
        var creatureUI = FindSoulCoreCreatureUI(creatureId);
        creatureUI.countText.text = creatureUnlocked ? $"{displayName} [{count}]" : $"{displayName} [LOCKED]";
        creatureUI.costText.text = creatureUnlocked ? $"{cost}" : "LOCKED";
        creatureUI.summonButton.interactable = creatureUnlocked && soulCores >= cost;
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
    }

    private SoulCoreCreatureUIReferences FindSoulCoreCreatureUI(string creatureId)
    {
        for (var index = 0; index < soulCoreCreatures.Length; index++)
            if (soulCoreCreatures[index].creatureId == creatureId) return soulCoreCreatures[index];
        return null;
    }
}
