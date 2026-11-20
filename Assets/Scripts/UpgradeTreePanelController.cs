using UnityEngine;

/// <summary>Opens and closes the upgrade tree overlay.</summary>
public class UpgradeTreePanelController : MonoBehaviour
{
    [SerializeField] private GameObject upgradePanel;

    /// <summary>Toggles the upgrade tree overlay and updates gameplay input blocking.</summary>
    public void TogglePanel()
    {
        SetPanelOpen(!upgradePanel.activeSelf);
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
