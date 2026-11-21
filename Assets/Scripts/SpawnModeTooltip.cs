using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Displays the firing mode when its UI control is hovered.</summary>
public class SpawnModeTooltip : MonoBehaviour
{
    private const string CameraModeText = "FIRING MODE: From Camera";
    private const string CenterModeText = "FIRING MODE: Through Center";
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text tooltipText;

    /// <summary>Updates the firing-mode label and shows the tooltip.</summary>
    public void Show(bool spawnFromCamera)
    {
        RefreshModeText(spawnFromCamera);
        tooltipPanel.SetActive(true);
    }

    /// <summary>Refreshes the displayed firing mode after it changes.</summary>
    public void RefreshModeText(bool spawnFromCamera)
    {
        tooltipText.text = spawnFromCamera ? CameraModeText : CenterModeText;
    }

    /// <summary>Hides the firing-mode tooltip when the pointer leaves the button.</summary>
    public void Hide()
    {
        tooltipPanel.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
    }

    public void OnPointerExit(PointerEventData eventData)
    {
    }
}
