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
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotLabels;
    [SerializeField] private Image[] slotBackgrounds;
    private readonly List<PlayerTool> unlockedTools = new();
    private PlayerTool selectedTool;

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
        RefreshTools();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
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
        for (var index = 0; index < tools.Length && unlockedTools.Count < MaxToolSlots; index++)
        {
            if (tools[index].IsUnlocked) unlockedTools.Add(tools[index]);
        }
        for (var index = 0; index < slotButtons.Length; index++)
        {
            var isVisible = index < unlockedTools.Count;
            slotButtons[index].gameObject.SetActive(isVisible);
            if (isVisible) slotLabels[index].text = $"{index + 1}   {unlockedTools[index].DisplayName}";
        }
        var selectedIndex = unlockedTools.IndexOf(previousSelection);
        SelectTool(selectedIndex >= 0 ? selectedIndex : unlockedTools.Count > 0 ? 0 : -1);
    }

    /// <summary>Selects an unlocked tool slot from a number key or UI button.</summary>
    public void SelectTool(int index)
    {
        if (index < 0 || index >= unlockedTools.Count) return;
        selectedTool = unlockedTools[index];
        for (var toolIndex = 0; toolIndex < unlockedTools.Count; toolIndex++) unlockedTools[toolIndex].SetSelected(toolIndex == index);
        for (var slotIndex = 0; slotIndex < unlockedTools.Count; slotIndex++) slotBackgrounds[slotIndex].color = slotIndex == index ? SelectedColor : UnselectedColor;
    }
}
