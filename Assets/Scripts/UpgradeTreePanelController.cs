using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Opens and closes the upgrade tree overlay.</summary>
public class UpgradeTreePanelController : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject upgradePanel;
    private RectTransform mapViewport;

    void Awake()
    {
        mapViewport = upgradePanel.transform.Find("Frame/MapViewport").GetComponent<RectTransform>();
    }

    /// <summary>Toggles the upgrade tree overlay and updates gameplay input blocking.</summary>
    public void TogglePanel()
    {
        SetPanelOpen(!upgradePanel.activeSelf);
    }

    /// <summary>Closes the upgrade tree when a click lands outside its map viewport.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (upgradePanel.activeSelf && !RectTransformUtility.RectangleContainsScreenPoint(mapViewport, eventData.position, eventData.pressEventCamera)) ClosePanel();
    }

    /// <summary>Closes the upgrade tree overlay and restores gameplay input.</summary>
    public void ClosePanel()
    {
        SetPanelOpen(false);
    }

    void SetPanelOpen(bool isOpen)
    {
        upgradePanel.SetActive(isOpen);
        UIController.Instance.SetPanelOpen(upgradePanel, isOpen);
        if (isOpen) UpgradeTreeController.Instance.RefreshNodes();
    }
}
