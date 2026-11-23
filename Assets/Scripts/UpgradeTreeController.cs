using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns upgrade definitions, procedural branch layout, and purchase flow for the upgrade tree.</summary>
public class UpgradeTreeController : MonoBehaviour
{
    public static UpgradeTreeController Instance { get; private set; }
    private const float BaseBranchSpacing = 36f;
    private const float PriceSpacing = 3f;
    private const float SiblingLaneSpacing = 132f;
    private const float NodeCollisionPadding = 24f;
    private const float CollisionPushOutStep = 64f;
    private const float ParentLinkThickness = 5f;
    private static readonly Color ParentLinkColor = new (0.28f, 0.34f, 0.4f, 0.85f);
    [SerializeField] private UpgradeTreeDefinition treeDefinition;
    [SerializeField] private GameObject nodePrefab;
    private readonly Dictionary<string, UpgradeNodeDefinition> definitionsById = new ();
    private readonly Dictionary<string, UpgradeNodeController> viewsById = new ();
    private readonly Dictionary<string, Vector2> positionsById = new ();
    private readonly Dictionary<string, string> placementParentById = new ();
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
        foreach (var definition in treeDefinition.nodes)
        {
            var nodeObject = Instantiate(nodePrefab, mapContent, false);
            nodeObject.name = $"Node_{definition.nodeId}";
            var view = nodeObject.GetComponent<UpgradeNodeController>();
            view.Initialize(definition, UpgradeController.Instance.GetLevel(definition.nodeId));
            viewsById.Add(definition.nodeId, view);
        }
        LayoutNodes();
        CreateParentLinks();
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

    void CreateParentLinks()
    {
        var linksObject = new GameObject("UpgradeLinks", typeof(RectTransform));
        var linksTransform = linksObject.GetComponent<RectTransform>();
        linksTransform.SetParent(mapContent, false);
        linksTransform.anchorMin = Vector2.zero;
        linksTransform.anchorMax = Vector2.one;
        linksTransform.pivot = Vector2.one * 0.5f;
        linksTransform.offsetMin = Vector2.zero;
        linksTransform.offsetMax = Vector2.zero;
        linksTransform.SetAsFirstSibling();
        foreach (var definition in treeDefinition.nodes)
        {
            if (definition.prerequisiteNodeIds.Length == 0) continue;
            var parentId = placementParentById[definition.nodeId];
            var parentPosition = positionsById[parentId];
            var childPosition = positionsById[definition.nodeId];
            var direction = (childPosition - parentPosition).normalized;
            var parentHalfSize = viewsById[parentId].GetComponent<RectTransform>().rect.width * 0.5f;
            var childHalfSize = viewsById[definition.nodeId].GetComponent<RectTransform>().rect.width * 0.5f;
            var start = parentPosition + direction * (parentHalfSize / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y)));
            var end = childPosition - direction * (childHalfSize / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y)));
            var linkObject = new GameObject($"Link_{parentId}_{definition.nodeId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var linkTransform = linkObject.GetComponent<RectTransform>();
            linkTransform.SetParent(linksTransform, false);
            linkTransform.anchorMin = Vector2.one * 0.5f;
            linkTransform.anchorMax = Vector2.one * 0.5f;
            linkTransform.pivot = Vector2.one * 0.5f;
            linkTransform.anchoredPosition = (start + end) * 0.5f;
            linkTransform.sizeDelta = new Vector2(Vector2.Distance(start, end), ParentLinkThickness);
            linkTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            var linkImage = linkObject.GetComponent<Image>();
            linkImage.color = ParentLinkColor;
            linkImage.raycastTarget = false;
        }
    }

    void LayoutNodes()
    {
        positionsById.Clear();
        placementParentById.Clear();
        var pending = new List<UpgradeNodeDefinition>();
        foreach (var definition in treeDefinition.nodes)
        {
            if (definition.prerequisiteNodeIds.Length == 0) positionsById.Add(definition.nodeId, Vector2.zero);
            else
            {
                placementParentById.Add(definition.nodeId, FindPlacementParent(definition).nodeId);
                pending.Add(definition);
            }
        }

        while (pending.Count > 0)
        {
            var madeProgress = false;
            for (var index = pending.Count - 1; index >= 0; index--)
            {
                var definition = pending[index];
                var parentId = placementParentById[definition.nodeId];
                if (!positionsById.TryGetValue(parentId, out var parentPosition)) continue;
                positionsById.Add(definition.nodeId, GetNodePosition(definition, parentId, parentPosition));
                viewsById[definition.nodeId].GetComponent<RectTransform>().anchoredPosition = positionsById[definition.nodeId];
                pending.RemoveAt(index);
                madeProgress = true;
            }
            if (!madeProgress) throw new System.InvalidOperationException("Upgrade tree prerequisites must form a connected acyclic graph rooted at a node without prerequisites.");
        }

        foreach (var definition in treeDefinition.nodes)
            if (definition.prerequisiteNodeIds.Length == 0) viewsById[definition.nodeId].GetComponent<RectTransform>().anchoredPosition = positionsById[definition.nodeId];
    }

    UpgradeNodeDefinition FindPlacementParent(UpgradeNodeDefinition definition)
    {
        UpgradeNodeDefinition bestParent = null;
        foreach (var prerequisiteId in definition.prerequisiteNodeIds)
        {
            var candidate = definitionsById[prerequisiteId];
            if (bestParent == null || IsBetterPlacementParent(definition, candidate, bestParent)) bestParent = candidate;
        }
        return bestParent;
    }

    bool IsBetterPlacementParent(UpgradeNodeDefinition child, UpgradeNodeDefinition candidate, UpgradeNodeDefinition currentBest)
    {
        var candidateDirectionMatches = candidate.direction == child.direction;
        var bestDirectionMatches = currentBest.direction == child.direction;
        if (candidateDirectionMatches != bestDirectionMatches) return candidateDirectionMatches;
        var candidatePriceDifference = Mathf.Abs(GetStartingPrice(candidate) - GetStartingPrice(child));
        var bestPriceDifference = Mathf.Abs(GetStartingPrice(currentBest) - GetStartingPrice(child));
        if (!Mathf.Approximately(candidatePriceDifference, bestPriceDifference)) return candidatePriceDifference < bestPriceDifference;
        return string.CompareOrdinal(candidate.nodeId, currentBest.nodeId) < 0;
    }

    Vector2 GetNodePosition(UpgradeNodeDefinition definition, string parentId, Vector2 parentPosition)
    {
        GetDirectionVectors(definition.direction, out var direction, out var perpendicular);
        var siblingIndex = 0;
        var siblingCount = 0;
        var startingPrice = GetStartingPrice(definition);
        foreach (var sibling in treeDefinition.nodes)
        {
            if (!placementParentById.TryGetValue(sibling.nodeId, out var siblingParent) || siblingParent != parentId || sibling.direction != definition.direction) continue;
            var siblingPrice = GetStartingPrice(sibling);
            if (siblingPrice < startingPrice || Mathf.Approximately(siblingPrice, startingPrice) && string.CompareOrdinal(sibling.nodeId, definition.nodeId) < 0) siblingIndex++;
            siblingCount++;
        }
        var laneOffset = (siblingIndex - (siblingCount - 1) * 0.5f) * SiblingLaneSpacing;
        var parentRadius = viewsById[parentId].GetLayoutRadius();
        var nodeRadius = viewsById[definition.nodeId].GetLayoutRadius();
        var branchDistance = parentRadius + nodeRadius + BaseBranchSpacing + startingPrice * PriceSpacing;
        var position = parentPosition + direction * branchDistance + perpendicular * laneOffset;
        while (HasNodeCollision(position, definition.nodeId))
        {
            branchDistance += CollisionPushOutStep;
            position = parentPosition + direction * branchDistance + perpendicular * laneOffset;
        }
        return position;
    }

    bool HasNodeCollision(Vector2 candidate, string nodeId)
    {
        var candidateRadius = viewsById[nodeId].GetLayoutRadius();
        foreach (var placedNode in positionsById)
        {
            var placedRadius = viewsById[placedNode.Key].GetLayoutRadius();
            if (Vector2.Distance(candidate, placedNode.Value) < candidateRadius + placedRadius + NodeCollisionPadding) return true;
        }
        return false;
    }

    static int GetStartingPrice(UpgradeNodeDefinition definition)
    {
        return definition.levelCosts[0];
    }

    static void GetDirectionVectors(UpgradeDirection upgradeDirection, out Vector2 direction, out Vector2 perpendicular)
    {
        direction = upgradeDirection switch
        {
            UpgradeDirection.Damage => Vector2.up,
            UpgradeDirection.Utility => Vector2.right,
            UpgradeDirection.Collection => Vector2.down,
            _ => Vector2.left
        };
        perpendicular = new Vector2(-direction.y, direction.x);
    }
}
