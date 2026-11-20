using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Offsets a button's visual background while its pointer is pressed.</summary>
public class UIButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform pressTarget;
    [SerializeField] private float pressedOffset = 8f;
    private Vector2 restingPosition;

    void Awake()
    {
        restingPosition = pressTarget.anchoredPosition;
    }

    /// <summary>Moves the button background down while pressed.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        pressTarget.anchoredPosition = restingPosition + Vector2.down * pressedOffset;
    }

    /// <summary>Restores the button background after release.</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        pressTarget.anchoredPosition = restingPosition;
    }

    void OnDisable()
    {
        pressTarget.anchoredPosition = restingPosition;
    }
}
