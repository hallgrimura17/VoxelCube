using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns upgrade definitions and validates purchase flow for the upgrade tree.</summary>
public class UpgradeTreeController : MonoBehaviour
{
    public static UpgradeTreeController Instance { get; private set; }
    [SerializeField] private UpgradeTreeDefinition treeDefinition;
    private readonly Dictionary<string, UpgradeNodeDefinition> definitionsById = new ();
    private readonly Dictionary<string, UpgradeNodeController> viewsById = new ();
    private RectTransform mapContent;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UpgradeController.Instance.Initialize(treeDefinition);
        foreach (var definition in treeDefinition.nodes) definitionsById.Add(definition.nodeId, definition);
        mapContent = GetComponentInChildren<UpgradeMapPan>(true).MapContent;
        var existingViews = GetComponentsInChildren<UpgradeNodeController>(true);
        var nodeTemplate = existingViews[0];
        foreach (var view in existingViews) viewsById.Add(view.NodeId, view);
        foreach (var definition in treeDefinition.nodes)
        {
            if (viewsById.ContainsKey(definition.nodeId)) continue;
            var nodeObject = Instantiate(nodeTemplate.gameObject, mapContent, false);
            nodeObject.name = $"Node_{definition.nodeId}";
            viewsById.Add(definition.nodeId, nodeObject.GetComponent<UpgradeNodeController>());
        }
        foreach (var definition in treeDefinition.nodes) viewsById[definition.nodeId].Initialize(definition, UpgradeController.Instance.GetLevel(definition.nodeId));
        LayoutNodes();
        CreatePrerequisiteLinks();
        RefreshNodes();
    }

    /// <summary>Returns whether this node is currently affordable and all parents are complete.</summary>
    public bool CanPurchase(string nodeId)
    {
        var definition = definitionsById[nodeId];
        var level = UpgradeController.Instance.GetLevel(nodeId);
        if (level >= definition.maxLevel) return false;
        foreach (var prerequisiteId in definition.prerequisiteNodeIds)
        {
            var prerequisite = definitionsById[prerequisiteId];
            if (UpgradeController.Instance.GetLevel(prerequisiteId) < prerequisite.maxLevel) return false;
        }
        return GameController.instance.currency >= definition.levelCosts[level];
    }

    /// <summary>Attempts one level purchase, deducting its defined resource and applying its configured effect.</summary>
    public void TryPurchase(string nodeId)
    {
        if (!CanPurchase(nodeId)) return;
        var definition = definitionsById[nodeId];
        var level = UpgradeController.Instance.GetLevel(nodeId);
        if (!GameController.instance.TrySpendCurrency(definition.levelCosts[level])) return;
        UpgradeController.Instance.ApplyPurchasedLevel(nodeId);
        RefreshNodes();
    }

    /// <summary>Resets purchased upgrades and refunds their recorded currency costs.</summary>
    public void ResetPurchasedUpgradesAndRefund()
    {
        var refund = UpgradeController.Instance.ResetPurchasedUpgrades();
        GameController.instance.AddCurrency(refund);
        RefreshNodes();
    }

    /// <summary>Refreshes node colors and tooltip data from current session progress.</summary>
    public void RefreshNodes()
    {
        foreach (var view in viewsById.Values) view.RefreshVisual();
    }

    void LayoutNodes()
    {
        foreach (var definition in treeDefinition.nodes) viewsById[definition.nodeId].GetComponent<RectTransform>().anchoredPosition = definition.mapPosition;
    }

    void CreatePrerequisiteLinks()
    {
        for (var childIndex = 0; childIndex < mapContent.childCount; childIndex++)
        {
            var child = mapContent.GetChild(childIndex);
            if (child.name.StartsWith("LinkH") || child.name.StartsWith("LinkV")) child.gameObject.SetActive(false);
        }

        foreach (var definition in treeDefinition.nodes)
        {
            foreach (var prerequisiteId in definition.prerequisiteNodeIds)
            {
                var start = definitionsById[prerequisiteId].mapPosition;
                var end = definition.mapPosition;
                var direction = end - start;
                var linkObject = new GameObject($"UpgradeLink_{prerequisiteId}_{definition.nodeId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var linkRect = linkObject.GetComponent<RectTransform>();
                var linkImage = linkObject.GetComponent<Image>();
                linkRect.SetParent(mapContent, false);
                linkRect.anchoredPosition = (start + end) * 0.5f;
                linkRect.sizeDelta = new Vector2(direction.magnitude, 6f);
                linkRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                linkImage.color = new Color(0.2f, 0.58f, 0.72f, 0.85f);
                linkImage.raycastTarget = false;
                linkRect.SetAsFirstSibling();
            }
        }
    }
}
