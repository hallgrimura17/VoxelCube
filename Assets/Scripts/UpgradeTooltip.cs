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
    [SerializeField] private RectTransform treeViewport;
    [SerializeField] private Vector2 pointerOffset = new (24f, 0f);
    private RectTransform tooltipShadowTransform;
    private Vector2 shadowOffset;
    private int displayedPrice;
    private bool isVisible;
    private const float CanvasEdgePadding = 16f;
    private const float NodeTooltipGap = 18f;
    private const float DirectionDeadZoneSquared = 0.01f;
    private static readonly Color CurrencyColor = new(1f, 0.82f, 0.2f, 1f);

    void Awake()
    {
        Instance = this;
        tooltipShadowTransform = tooltipShadow.GetComponent<RectTransform>();
        shadowOffset = tooltipShadowTransform.anchoredPosition;
        tooltipPanel.gameObject.SetActive(false);
        tooltipShadow.SetActive(false);
    }

    /// <summary>Shows upgrade details and positions the shared tooltip beside the pointer.</summary>
    public void Show(string title, string description, int price, Vector2 screenPosition)
    {
        titleText.text = title;
        descriptionText.text = description;
        displayedPrice = price;
        RefreshPriceText();
        priceText.color = CurrencyColor;
        tooltipPanel.gameObject.SetActive(true);
        tooltipShadow.SetActive(true);
        isVisible = true;
        transform.SetAsLastSibling();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out var localPosition);
        var treeCenterScreen = RectTransformUtility.WorldToScreenPoint(null, treeViewport.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, treeCenterScreen, null, out var treeCenter);
        var direction = localPosition - treeCenter;
        if (direction.sqrMagnitude < DirectionDeadZoneSquared) direction = Vector2.up;
        direction.Normalize();
        var halfSize = tooltipPanel.rect.size * 0.5f;
        var offset = Mathf.Abs(direction.x) * halfSize.x + Mathf.Abs(direction.y) * halfSize.y + pointerOffset.magnitude + NodeTooltipGap;
        var desiredPosition = localPosition + direction * offset;
        var x = Mathf.Clamp(desiredPosition.x, -canvasRect.rect.width * 0.5f + halfSize.x + CanvasEdgePadding, canvasRect.rect.width * 0.5f - halfSize.x - CanvasEdgePadding);
        var y = Mathf.Clamp(desiredPosition.y, -canvasRect.rect.height * 0.5f + halfSize.y + CanvasEdgePadding, canvasRect.rect.height * 0.5f - halfSize.y - CanvasEdgePadding);
        var tooltipPosition = new Vector2(x, y);
        tooltipPanel.anchoredPosition = tooltipPosition;
        tooltipShadowTransform.anchoredPosition = tooltipPosition + shadowOffset;
    }

    /// <summary>Refreshes the displayed owned-currency amount while the tooltip is open.</summary>
    public void RefreshCurrency()
    {
        if (isVisible) RefreshPriceText();
    }

    void RefreshPriceText()
    {
        priceText.text = $"COST  ${displayedPrice:N0}  /  OWNED ${GameController.instance.currency:N0}";
    }

    /// <summary>Hides the shared upgrade tooltip.</summary>
    public void Hide()
    {
        isVisible = false;
        tooltipPanel.gameObject.SetActive(false);
        tooltipShadow.SetActive(false);
    }
}
