using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Sends this upgrade's current information to the shared hover tooltip.</summary>
public class UpgradeButtonTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string upgradeTitle = "UPGRADE";
    [SerializeField] private string upgradeDescription = "Describe this upgrade.";
    [SerializeField] private int price = 100;
    [SerializeField] private int currentLevel;
    [SerializeField] private int maxLevel = 1;
    private Vector2 pointerPosition;
    private bool isHovered;

    /// <summary>Updates this node's title, effect, cost, currency, and level range.</summary>
    public void SetUpgradeDetails(string title, string description, int cost, int level, int levelCap)
    {
        upgradeTitle = title;
        upgradeDescription = description;
        price = cost;
        currentLevel = level;
        maxLevel = levelCap;
        if (isHovered) ShowTooltip();
    }

    /// <summary>Shows this upgrade's details in the shared tooltip.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerPosition = eventData.position;
        isHovered = true;
        ShowTooltip();
    }

    void ShowTooltip()
    {
        UpgradeTooltip.Instance.Show(upgradeTitle, $"{upgradeDescription}\nLEVEL {currentLevel}/{maxLevel}", price, pointerPosition);
    }

    /// <summary>Hides the shared tooltip when the pointer leaves this upgrade.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        UpgradeTooltip.Instance.Hide();
    }
}
