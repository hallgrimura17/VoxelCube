using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Connects a visual map node to its authored definition and animated interaction feedback.</summary>
public class UpgradeNodeController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private string nodeId;
    [SerializeField] private Image nodeImage;
    [SerializeField, Tooltip("Controls the icon's hover and press effects. The keycap body's press movement remains enabled.")] private bool clickEffectEnabled = true;
    [SerializeField, Range(0f, 3f), Tooltip("Scales the keycap side offset based on this node's position in the upgrade tree; 0 disables the position effect.")] private float treePositionSideScale = 1f;
    private static readonly Color SmallTierColor = new (0.18f, 0.82f, 0.94f, 1f);
    private static readonly Color MediumTierColor = new (0.24f, 0.55f, 1f, 1f);
    private static readonly Color LargeTierColor = new (1f, 0.68f, 0.24f, 1f);
    private static readonly Color LockedUiColor = new (0.42f, 0.44f, 0.47f, 1f);
    private static readonly Color MaxedGlowColor = new (1f, 0.78f, 0.16f, 0.85f);
    private const float HoverScale = 1.1f;
    private const float PressedScale = 0.96f;
    private const float PressedDepth = 6f;
    private const float HoverLift = 3f;
    private const float HoverTilt = -7f;
    private const float PressedTilt = 0f;
    private const float InteractionSmoothing = 18f;
    private const float BaseNodeSize = 64f;
    private const float StartingPriceSizeWeight = 4f;
    private const float CurrentLevelSizeWeight = 2f;
    private const float PurchasedTint = 0.28f;
    private const int SmallTierMaxLevel = 1;
    private const int MediumTierMaxLevel = 5;
    private const string CubeIconName = "Icon";
    private const string SideName = "Side";
    private const string PriceLabelName = "PriceLabel";
    private const string HighlightName = "Highlight";
    private const string ProgressFillPath = "ProgressFill";
    private Button purchaseButton;
    private RectTransform nodeTransform;
    private RectTransform cubeTransform;
    private RectTransform sideTransform;
    private RectTransform panelTransform;
    private TMP_Text priceLabel;
    private Image levelProgressFill;
    private Image buttonHighlightImage;
    private Color basePriceColor;
    private Color baseHighlightColor;
    private ColorBlock baseButtonColors;
    private Vector3 baseCubeScale;
    private Vector3 restingCubeScale;
    private Vector2 baseSidePosition;
    private RectTransform[] visualTransforms;
    private Vector2[] baseVisualPositions;
    private UpgradeNodeDefinition definition;
    private int currentLevel;
    private Vector2 pointerPosition;
    private bool referencesCached;
    private bool isHovered;
    private bool isPressed;

    public string NodeId => nodeId;

    void CacheReferences()
    {
        if (referencesCached) return;
        purchaseButton = GetComponent<Button>();
        purchaseButton.onClick.AddListener(Purchase);
        baseButtonColors = purchaseButton.colors;
        buttonHighlightImage = transform.Find(HighlightName).GetComponent<Image>();
        baseHighlightColor = buttonHighlightImage.color;
        nodeTransform = GetComponent<RectTransform>();
        cubeTransform = transform.Find(CubeIconName).GetComponent<RectTransform>();
        sideTransform = transform.Find(SideName).GetComponent<RectTransform>();
        panelTransform = (RectTransform)transform.parent;
        priceLabel = transform.Find(PriceLabelName).GetComponent<TMP_Text>();
        basePriceColor = priceLabel.color;
        levelProgressFill = transform.Find(ProgressFillPath).GetComponent<Image>();
        nodeImage = cubeTransform.GetComponent<Image>();
        baseCubeScale = cubeTransform.localScale;
        baseSidePosition = sideTransform.anchoredPosition;
        visualTransforms = new RectTransform[transform.childCount];
        baseVisualPositions = new Vector2[visualTransforms.Length];
        for (var index = 0; index < visualTransforms.Length; index++)
        {
            visualTransforms[index] = transform.GetChild(index).GetComponent<RectTransform>();
            baseVisualPositions[index] = visualTransforms[index].anchoredPosition;
        }
        restingCubeScale = baseCubeScale;
        referencesCached = true;
    }

    void Update()
    {
        var blend = 1f - Mathf.Exp(-InteractionSmoothing * Time.unscaledDeltaTime);
        var hoverEffect = clickEffectEnabled && isHovered && !isPressed;
        var iconClickEffect = clickEffectEnabled && isPressed;
        var scaleFactor = iconClickEffect ? PressedScale : hoverEffect ? HoverScale : 1f;
        var tilt = iconClickEffect ? PressedTilt : hoverEffect ? HoverTilt : 0f;
        var panelPosition = panelTransform.InverseTransformPoint(nodeTransform.position);
        var panelDirection = new Vector2(panelPosition.x, panelPosition.y).normalized;
        var sideDirection = -panelDirection;
        var localSideDirection = baseSidePosition.normalized;
        if (sideDirection.sqrMagnitude > 0f)
        {
            var localDirection = nodeTransform.InverseTransformVector(panelTransform.TransformVector(sideDirection));
            localSideDirection = new Vector2(localDirection.x, localDirection.y).normalized;
        }
        var visualOffset = isPressed ? localSideDirection * PressedDepth : Vector2.zero;
        var hoverOffset = hoverEffect ? Vector2.up * HoverLift : Vector2.zero;
        for (var index = 0; index < visualTransforms.Length; index++)
        {
            var targetPosition = baseVisualPositions[index] + visualOffset;
            if (visualTransforms[index] == cubeTransform) targetPosition += hoverOffset;
            if (visualTransforms[index] == sideTransform) targetPosition = localSideDirection * baseSidePosition.magnitude * treePositionSideScale + visualOffset;
            visualTransforms[index].anchoredPosition = Vector2.Lerp(visualTransforms[index].anchoredPosition, targetPosition, blend);
        }
        cubeTransform.localScale = Vector3.Lerp(cubeTransform.localScale, restingCubeScale * scaleFactor, blend);
        cubeTransform.localRotation = Quaternion.Slerp(cubeTransform.localRotation, Quaternion.Euler(0f, 0f, tilt), blend);
    }

    /// <summary>Returns the conservative radius of this node's maximum-size backing.</summary>
    public float GetLayoutRadius()
    {
        return nodeTransform.rect.size.magnitude * 0.5f;
    }

    /// <summary>Assigns upgrade data, sets its maximum-size backing, and initializes its current-level face.</summary>
    public void Initialize(UpgradeNodeDefinition nodeDefinition, int level)
    {
        CacheReferences();
        definition = nodeDefinition;
        currentLevel = level;
        nodeId = definition.nodeId;
        nodeTransform.sizeDelta = Vector2.one * GetMaximumSize(definition);
        UpdateCurrentSize();
        RefreshVisual();
    }

    /// <summary>Refreshes the current level, cost label, level progress, node color, and tooltip data.</summary>
    public void RefreshVisual()
    {
        currentLevel = UpgradeController.Instance.GetLevel(nodeId);
        UpdateCurrentSize();
        var canPurchase = UpgradeTreeController.Instance.CanPurchase(nodeId);
        var isMaxed = currentLevel >= definition.maxLevel;
        var tierColor = GetTierColor(GetSizeTier(definition.maxLevel));
        nodeImage.color = isMaxed ? Color.Lerp(tierColor, Color.white, PurchasedTint) : canPurchase ? tierColor : LockedUiColor;
        priceLabel.text = isMaxed ? "MAX" : $"${definition.levelCosts[currentLevel]:N0}";
        priceLabel.color = isMaxed ? Color.yellow : canPurchase ? basePriceColor : LockedUiColor;
        levelProgressFill.color = tierColor;
        levelProgressFill.fillAmount = currentLevel / (float)definition.maxLevel;
        var buttonColors = baseButtonColors;
        if (isMaxed)
        {
            buttonColors.normalColor = MaxedGlowColor;
            buttonColors.highlightedColor = Color.yellow;
            buttonColors.pressedColor = MaxedGlowColor;
            buttonColors.selectedColor = Color.yellow;
            buttonHighlightImage.color = Color.yellow;
        }
        else if (!canPurchase)
        {
            buttonColors.normalColor = LockedUiColor;
            buttonColors.highlightedColor = LockedUiColor;
            buttonColors.pressedColor = LockedUiColor;
            buttonColors.selectedColor = LockedUiColor;
            buttonHighlightImage.color = LockedUiColor;
        }
        else buttonHighlightImage.color = baseHighlightColor;
        purchaseButton.colors = buttonColors;
        purchaseButton.interactable = true;
        if (isHovered) ShowTooltip();
    }

    void ShowTooltip()
    {
        var price = currentLevel < definition.maxLevel ? definition.levelCosts[currentLevel] : 0;
        UpgradeTooltip.Instance.Show(definition.title, $"{definition.description}\nLEVEL {currentLevel}/{definition.maxLevel}", price, pointerPosition);
    }

    void UpdateCurrentSize()
    {
        var maximumSize = GetMaximumSize(definition);
        var currentSize = BaseNodeSize + GetStartingPrice(definition) * StartingPriceSizeWeight + currentLevel * CurrentLevelSizeWeight;
        restingCubeScale = baseCubeScale * (currentSize / maximumSize);
        cubeTransform.localScale = restingCubeScale;
    }

    static float GetMaximumSize(UpgradeNodeDefinition nodeDefinition)
    {
        return BaseNodeSize + GetStartingPrice(nodeDefinition) * StartingPriceSizeWeight + nodeDefinition.maxLevel * CurrentLevelSizeWeight;
    }

    static int GetStartingPrice(UpgradeNodeDefinition nodeDefinition)
    {
        return nodeDefinition.levelCosts[0];
    }

    /// <summary>Raises the cube while the pointer is over the node.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        pointerPosition = eventData.position;
        ShowTooltip();
    }

    /// <summary>Returns the cube to its resting pose and hides its tooltip when the pointer leaves.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        UpgradeTooltip.Instance.Hide();
    }

    /// <summary>Moves and scales the complete keycap visual while the pointer is held down.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
    }

    /// <summary>Releases the cube after the pointer is lifted.</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
    }

    /// <summary>Forwards a click to the tree controller for validation and purchase.</summary>
    public void Purchase()
    {
        UpgradeTreeController.Instance.TryPurchase(nodeId);
    }

    void OnDisable()
    {
        if (isHovered) UpgradeTooltip.Instance.Hide();
        isHovered = false;
        isPressed = false;
        for (var index = 0; index < visualTransforms.Length; index++) visualTransforms[index].anchoredPosition = baseVisualPositions[index];
        cubeTransform.localScale = restingCubeScale;
        cubeTransform.localRotation = Quaternion.identity;
    }

    static NodeTier GetSizeTier(int maximumLevel)
    {
        return maximumLevel <= SmallTierMaxLevel ? NodeTier.Small : maximumLevel <= MediumTierMaxLevel ? NodeTier.Medium : NodeTier.Large;
    }

    static Color GetTierColor(NodeTier tier)
    {
        return tier switch { NodeTier.Small => SmallTierColor, NodeTier.Medium => MediumTierColor, _ => LargeTierColor };
    }

    enum NodeTier
    {
        Small,
        Medium,
        Large
    }
}
