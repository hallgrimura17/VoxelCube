using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Connects a visual map node to its authored ScriptableObject definition.</summary>
public class UpgradeNodeController : MonoBehaviour
{
    [SerializeField] private string nodeId;
    [SerializeField] private Image nodeImage;
    [SerializeField] private UpgradeButtonTooltip tooltip;
    private Button purchaseButton;
    private UpgradeNodeDefinition definition;
    private int currentLevel;

    public string NodeId => nodeId;

    void Awake()
    {
        purchaseButton = GetComponent<Button>();
        purchaseButton.onClick.AddListener(Purchase);
    }

    /// <summary>Assigns immutable upgrade data and the current session level from the tree controller.</summary>
    public void Initialize(UpgradeNodeDefinition nodeDefinition, int level)
    {
        definition = nodeDefinition;
        currentLevel = level;
        nodeId = definition.nodeId;
        nodeImage = GetComponent<Image>();
        tooltip = GetComponent<UpgradeButtonTooltip>();
        GetComponentInChildren<TMP_Text>(true).text = definition.title.Substring(0, 1);
        RefreshVisual();
    }

    /// <summary>Refreshes the node tint and shared tooltip data from its definition.</summary>
    public void RefreshVisual()
    {
        currentLevel = UpgradeController.Instance.GetLevel(nodeId);
        var tree = UpgradeTreeController.Instance;
        var canPurchase = tree.CanPurchase(nodeId);
        nodeImage.color = currentLevel >= definition.maxLevel ? Color.blue : canPurchase ? new Color(0.78f, 0.79f, 0.78f, 1f) : new Color(0.24f, 0.25f, 0.26f, 1f);
        var nextLevel = Mathf.Min(currentLevel, definition.maxLevel - 1);
        tooltip.SetUpgradeDetails(definition.title, definition.description, currentLevel < definition.maxLevel ? definition.levelCosts[nextLevel] : 0, currentLevel, definition.maxLevel);
    }

    /// <summary>Forwards a click to the tree controller for validation and purchase.</summary>
    public void Purchase()
    {
        UpgradeTreeController.Instance.TryPurchase(nodeId);
    }
}
