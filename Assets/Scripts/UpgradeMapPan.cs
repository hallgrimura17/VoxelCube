using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Pans and zooms the upgrade map.</summary>
public class UpgradeMapPan : MonoBehaviour, IDragHandler, IScrollHandler
{
    private const float MinimumZoom = 0.5f;
    private const float MaximumZoom = 2.5f;
    private const float ZoomStep = 1.15f;
    [SerializeField] private RectTransform mapContent;
    [SerializeField] private Canvas parentCanvas;

    public RectTransform MapContent => mapContent;

    /// <summary>Moves the map content with the pointer drag.</summary>
    public void OnDrag(PointerEventData eventData)
    {
        mapContent.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
    }

    /// <summary>Zooms the map toward or away from the pointer's position.</summary>
    public void OnScroll(PointerEventData eventData)
    {
        ZoomAt(eventData.scrollDelta.y > 0f ? ZoomStep : 1f / ZoomStep, eventData.position);
    }

    /// <summary>Zooms in around the center of the map viewport.</summary>
    public void ZoomIn()
    {
        ZoomAt(ZoomStep, GetViewportCenterScreenPosition());
    }

    /// <summary>Zooms out around the center of the map viewport.</summary>
    public void ZoomOut()
    {
        ZoomAt(1f / ZoomStep, GetViewportCenterScreenPosition());
    }

    void ZoomAt(float multiplier, Vector2 screenPosition)
    {
        var viewportTransform = (RectTransform)transform;
        var eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportTransform, screenPosition, eventCamera, out var pointerInViewport);
        var pointInMap = mapContent.InverseTransformPoint(viewportTransform.TransformPoint(pointerInViewport));
        var currentZoom = mapContent.localScale.x;
        var nextZoom = Mathf.Clamp(currentZoom * multiplier, MinimumZoom, MaximumZoom);
        mapContent.localScale = Vector3.one * nextZoom;
        var pointerAfterZoom = viewportTransform.InverseTransformPoint(mapContent.TransformPoint(pointInMap));
        mapContent.anchoredPosition += pointerInViewport - new Vector2(pointerAfterZoom.x, pointerAfterZoom.y);
    }

    Vector2 GetViewportCenterScreenPosition()
    {
        var viewportTransform = (RectTransform)transform;
        var eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(eventCamera, viewportTransform.TransformPoint(viewportTransform.rect.center));
    }
}
