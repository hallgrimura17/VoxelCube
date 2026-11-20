using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Pans the upgrade map by dragging while keeping its zoom fixed.</summary>
public class UpgradeMapPan : MonoBehaviour, IDragHandler
{
    [SerializeField] private RectTransform mapContent;
    [SerializeField] private Canvas parentCanvas;

    public RectTransform MapContent => mapContent;

    /// <summary>Moves the map content with the pointer drag.</summary>
    public void OnDrag(PointerEventData eventData)
    {
        mapContent.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
    }
}
