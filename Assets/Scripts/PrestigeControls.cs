using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Connects the prestige controls and displays the selected and unlocked prestige range.</summary>
public class PrestigeControls : MonoBehaviour
{
    [SerializeField] private Button lowerPrestigeButton;
    [SerializeField] private Button resetCubeButton;
    [SerializeField] private Button raisePrestigeButton;
    [SerializeField] private TMP_Text resetCubeLabel;

    public static PrestigeControls Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        lowerPrestigeButton.onClick.AddListener(GameController.instance.LowerPrestige);
        resetCubeButton.onClick.AddListener(GameController.instance.ResetCube);
        raisePrestigeButton.onClick.AddListener(GameController.instance.RaisePrestige);
        Refresh();
    }

    /// <summary>Refreshes the button states and selected prestige label.</summary>
    public void Refresh()
    {
        var controller = GameController.instance;
        lowerPrestigeButton.gameObject.SetActive(controller.Prestige > 0);
        raisePrestigeButton.gameObject.SetActive(controller.Prestige < controller.HighestUnlockedPrestige);
        resetCubeLabel.text = $"RESET CUBE\nPRESTIGE {controller.Prestige}";
    }
}
