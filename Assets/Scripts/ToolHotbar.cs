using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Maps the player's unlocked tools to scene-authored hotbar slots.</summary>
public class ToolHotbar : MonoBehaviour
{
    private const int MaxToolSlots = 9;
    private static readonly Color SelectedColor = new(0.28f, 0.55f, 0.78f, 1f);
    private static readonly Color UnselectedColor = new(0.3569f, 0.3725f, 0.3882f, 1f);
    private static readonly Color SelectedModeColor = new(0.95f, 0.55f, 0.18f, 1f);
    private static readonly Color UnselectedModeColor = new(0.12f, 0.16f, 0.2f, 1f);
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotLabels;
    [SerializeField] private Image[] slotBackgrounds;
    [SerializeField] private Button[] blasterModeButtons;
    [SerializeField] private TMP_Text[] blasterModeLabels;
    [SerializeField] private Button[] magnetModeButtons;
    [SerializeField] private TMP_Text[] magnetModeLabels;
    [SerializeField] private Button[] perspectiveButtons;
    [SerializeField] private GameObject perspectiveCrosshair;

    [SerializeField] private TMP_Text[] perspectiveLabels;
    private readonly List<PlayerTool> unlockedTools = new();
    private PlayerTool selectedTool;
    private BlasterTool blasterTool;
    private MagnetTool magnetTool;
    private PlayerPerspectiveController perspectiveController;

    public static ToolHotbar Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        for (var index = 0; index < slotButtons.Length; index++)
        {
            var toolIndex = index;
            slotButtons[index].onClick.AddListener(() => SelectTool(toolIndex));
        }
        blasterModeButtons[0].onClick.AddListener(() => blasterTool.SetSpawnFromCamera(true));
        blasterModeButtons[1].onClick.AddListener(() => blasterTool.SetSpawnFromCamera(false));
        magnetModeButtons[0].onClick.AddListener(() => magnetTool.SetSelectMode());
        magnetModeButtons[1].onClick.AddListener(() => magnetTool.SetPullMode());
        RefreshTools();
        perspectiveController = blasterTool.transform.parent.gameObject.AddComponent<PlayerPerspectiveController>();
        perspectiveController.Initialize(blasterTool);
        perspectiveCrosshair.SetActive(false);
        for (var index = 0; index < perspectiveButtons.Length; index++)
        {
            var mode = (PlayerPerspectiveController.PerspectiveMode)index;
            perspectiveButtons[index].onClick.AddListener(() => perspectiveController.SetMode(mode));
        }
        RefreshPerspectiveControls();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard.fKey.wasPressedThisFrame)
        {
            perspectiveController.CycleMode();
            return;
        }
        if (keyboard.eKey.wasPressedThisFrame)
        {
            ToggleSelectedToolMode();
            return;
        }
        if (keyboard.digit1Key.wasPressedThisFrame) SelectTool(0);
        else if (keyboard.digit2Key.wasPressedThisFrame) SelectTool(1);
        else if (keyboard.digit3Key.wasPressedThisFrame) SelectTool(2);
        else if (keyboard.digit4Key.wasPressedThisFrame) SelectTool(3);
        else if (keyboard.digit5Key.wasPressedThisFrame) SelectTool(4);
        else if (keyboard.digit6Key.wasPressedThisFrame) SelectTool(5);
        else if (keyboard.digit7Key.wasPressedThisFrame) SelectTool(6);
        else if (keyboard.digit8Key.wasPressedThisFrame) SelectTool(7);
        else if (keyboard.digit9Key.wasPressedThisFrame) SelectTool(8);
    }

    /// <summary>Refreshes visible hotbar slots when a tool becomes unlocked.</summary>
    public void RefreshTools()
    {
        var previousSelection = selectedTool;
        var tools = FindObjectsByType<PlayerTool>();
        System.Array.Sort(tools, (first, second) =>
        {
            var orderComparison = first.HotbarOrder.CompareTo(second.HotbarOrder);
            return orderComparison != 0 ? orderComparison : string.CompareOrdinal(first.DisplayName, second.DisplayName);
        });
        unlockedTools.Clear();
        blasterTool = null;
        magnetTool = null;
        for (var index = 0; index < tools.Length && unlockedTools.Count < MaxToolSlots; index++)
        {
            if (tools[index] is BlasterTool blaster) blasterTool = blaster;
            if (tools[index] is MagnetTool magnet) magnetTool = magnet;
            if (tools[index].IsUnlocked) unlockedTools.Add(tools[index]);
        }
        for (var index = 0; index < slotButtons.Length; index++)
        {
            var isVisible = index < unlockedTools.Count;
            slotButtons[index].gameObject.SetActive(isVisible);
            if (isVisible) slotLabels[index].text = $"{index + 1}   {unlockedTools[index].DisplayName}";
        }
        var selectedIndex = unlockedTools.IndexOf(previousSelection);
        SelectTool(selectedIndex >= 0 ? selectedIndex : unlockedTools.Count > 0 ? 0 : -1, false);
        RefreshModeControls();
    }

    /// <summary>Refreshes perspective selector highlighting to match the active movement mode.</summary>
    public void RefreshPerspectiveControls()
    {
        if (perspectiveButtons == null) return;
        for (var index = 0; index < perspectiveButtons.Length; index++)
        {
            var isSelected = (int)perspectiveController.CurrentMode == index;
            perspectiveButtons[index].targetGraphic.color = isSelected ? SelectedModeColor : UnselectedModeColor;
        }
    }

    /// <summary>Shows the center-screen reticle only in Free Cam and FPS perspectives.</summary>
    public void SetPerspectiveCrosshairVisible(bool isVisible)
    {
        perspectiveCrosshair.SetActive(isVisible);
    }



    /// <summary>Refreshes mode button visibility, labels, and active-mode highlighting.</summary>
    public void RefreshModeControls()
    {
        SetModeButton(blasterModeButtons[0], blasterModeLabels[0], "CAMERA", blasterTool != null && blasterTool.IsUnlocked && selectedTool == blasterTool, blasterTool != null && blasterTool.SpawnFromCamera);
        SetModeButton(blasterModeButtons[1], blasterModeLabels[1], "CENTER", blasterTool != null && blasterTool.IsUnlocked && selectedTool == blasterTool, blasterTool != null && !blasterTool.SpawnFromCamera);
        SetModeButton(magnetModeButtons[0], magnetModeLabels[0], "SELECT", magnetTool != null && magnetTool.IsUnlocked && selectedTool == magnetTool, magnetTool != null && !magnetTool.IsPullMode);
        SetModeButton(magnetModeButtons[1], magnetModeLabels[1], "PULL", magnetTool != null && magnetTool.IsUnlocked && selectedTool == magnetTool, magnetTool != null && magnetTool.IsPullMode);
    }

    private static void SetModeButton(Button button, TMP_Text label, string text, bool visible, bool isSelected)
    {
        button.gameObject.SetActive(visible);
        label.text = text;
        button.transition = Selectable.Transition.None;
        button.targetGraphic.color = isSelected ? SelectedModeColor : UnselectedModeColor;
    }

    /// <summary>Selects an unlocked hotbar slot, or toggles its mode if it is already selected.</summary>
    public void SelectTool(int index)
    {
        SelectTool(index, true);
    }

    private void SelectTool(int index, bool toggleModeIfSelected)
    {
        if (index < 0 || index >= unlockedTools.Count) return;
        if (toggleModeIfSelected && selectedTool == unlockedTools[index])
        {
            ToggleSelectedToolMode();
            return;
        }
        selectedTool = unlockedTools[index];
        for (var toolIndex = 0; toolIndex < unlockedTools.Count; toolIndex++) unlockedTools[toolIndex].SetSelected(toolIndex == index);
        for (var slotIndex = 0; slotIndex < unlockedTools.Count; slotIndex++) slotBackgrounds[slotIndex].color = slotIndex == index ? SelectedColor : UnselectedColor;
        RefreshModeControls();
    }

    private void ToggleSelectedToolMode()
    {
        if (selectedTool == null) return;
        selectedTool.ToggleMode();
        for (var index = 0; index < unlockedTools.Count; index++) slotLabels[index].text = $"{index + 1}   {unlockedTools[index].DisplayName}";
    }
}
