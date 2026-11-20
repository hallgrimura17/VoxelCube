using UnityEngine;

/// <summary>Base class for tools that can be selected from the player hotbar.</summary>
public abstract class PlayerTool : MonoBehaviour
{
    public abstract string DisplayName { get; }
    public abstract int HotbarOrder { get; }
    public abstract bool IsUnlocked { get; }
    public bool IsSelected { get; private set; }

    /// <summary>Sets whether this tool is the currently selected player tool.</summary>
    public void SetSelected(bool selected)
    {
        IsSelected = selected;
    }
}
