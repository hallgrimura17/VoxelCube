using TMPro;
using UnityEngine;

/// <summary>Displays upgrade details in one shared HUD tooltip.</summary>
public class UpgradeTooltip : MonoBehaviour
{
    public static UpgradeTooltip Instance { get; private set; }
    [SerializeField] private RectTransform tooltipPanel;
    [SerializeField] private GameObject tooltipShadow;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Vector2 pointerOffset = new (24f, 0f);
    private const float CanvasEdgePadding = 16f;
    private static readonly Color CurrencyColor = new(1f, 0.82f, 0.2f, 1f);

    void Awake()
    {
        Instance = this;
        tooltipPanel.gameObject.SetActive(false);
        tooltipShadow.SetActive(false);
    }

    /// <summary>Shows upgrade details and positions the shared tooltip beside the pointer.</summary>
    public void Show(string title, string description, int price, Vector2 screenPosition)
    {
        titleText.text = title;
        descriptionText.text = description;
        priceText.text = $"COST  ${price:N0}  /  OWNED ${GameController.instance.currency:N0}";
        priceText.color = CurrencyColor;
        tooltipPanel.gameObject.SetActive(true);
        tooltipShadow.SetActive(true);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out var localPosition);
        var halfSize = tooltipPanel.rect.size * 0.5f;
        var x = localPosition.x + pointerOffset.x;
        if (x + halfSize.x > canvasRect.rect.width * 0.5f - CanvasEdgePadding) x = localPosition.x - pointerOffset.x - halfSize.x * 2f;
        var y = Mathf.Clamp(localPosition.y + pointerOffset.y, -canvasRect.rect.height * 0.5f + halfSize.y + CanvasEdgePadding, canvasRect.rect.height * 0.5f - halfSize.y - CanvasEdgePadding);
        tooltipPanel.anchoredPosition = new Vector2(Mathf.Clamp(x, -canvasRect.rect.width * 0.5f + halfSize.x + CanvasEdgePadding, canvasRect.rect.width * 0.5f - halfSize.x - CanvasEdgePadding), y);
    }

    /// <summary>Hides the shared upgrade tooltip.</summary>
    public void Hide()
    {
        tooltipPanel.gameObject.SetActive(false);
        tooltipShadow.SetActive(false);
    }
}
