using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Connects the mastery panel's creature paths to mastery purchases.</summary>
public sealed class MasteryTreePanelController : MonoBehaviour, IPointerClickHandler
{
    public static MasteryTreePanelController Instance { get; private set; }
    private GameObject panel;
    private RectTransform frameTransform;
    private TMP_Text pointsText;
    private CreaturePath[] creaturePaths;

    private sealed class CreaturePath
    {
        public string creatureId;
        public string cardName;
        public Button[] levelButtons;
        public TMP_Text levelText;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        panel = transform.Find("MasteryTreePanel").gameObject;
        var frame = panel.transform.Find("Frame");
        frameTransform = frame.GetComponent<RectTransform>();
        panel.GetComponent<Image>().raycastTarget = true;
        pointsText = frame.Find("PointsText").GetComponent<TMP_Text>();
        transform.Find("MasteryToggleButton").GetComponent<Button>().onClick.AddListener(TogglePanel);
        frame.Find("CloseButton").GetComponent<Button>().onClick.AddListener(ClosePanel);
        creaturePaths = new[] { CreatePath("Cart", "CartCard"), CreatePath("UFO", "UFOCard") };
        for (var pathIndex = 0; pathIndex < creaturePaths.Length; pathIndex++)
            for (var level = 1; level <= 4; level++)
            {
                var creatureId = creaturePaths[pathIndex].creatureId;
                var targetLevel = level;
                creaturePaths[pathIndex].levelButtons[level - 1].onClick.AddListener(() => Purchase(creatureId, targetLevel));
            }
        Refresh();
    }

    /// <summary>Toggles the mastery panel and blocks gameplay input while it is open.</summary>
    public void TogglePanel() => SetPanelOpen(!panel.activeSelf);

    /// <summary>Closes the mastery tree when a click lands outside its content frame.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (panel.activeSelf && !RectTransformUtility.RectangleContainsScreenPoint(frameTransform, eventData.position, eventData.pressEventCamera)) ClosePanel();
    }

    /// <summary>Closes the mastery panel and restores gameplay input.</summary>
    public void ClosePanel() => SetPanelOpen(false);

    /// <summary>Refreshes available points and each creature's four-level path.</summary>
    public void Refresh()
    {
        if (pointsText == null) return;
        var points = MasterySystem.Instance.SkillPoints;
        pointsText.text = $"MASTERY POINTS: {points}";
        for (var pathIndex = 0; pathIndex < creaturePaths.Length; pathIndex++)
        {
            var path = creaturePaths[pathIndex];
            var currentLevel = MasterySystem.Instance.GetLevel(path.creatureId);
            path.levelText.text = $"MASTERY {currentLevel} / 4";
            for (var levelIndex = 0; levelIndex < path.levelButtons.Length; levelIndex++)
            {
                var targetLevel = levelIndex + 1;
                var button = path.levelButtons[levelIndex];
                var background = button.transform.Find("Background").GetComponent<Image>();
                var label = button.GetComponentInChildren<TMP_Text>(true);
                button.interactable = targetLevel == currentLevel + 1 && points > 0;
                background.color = targetLevel <= currentLevel ? new Color(0.16f, 0.64f, 0.58f, 1f) : targetLevel == currentLevel + 1 ? new Color(0.22f, 0.48f, 0.62f, 1f) : new Color(0.13f, 0.16f, 0.19f, 1f);
                label.text = targetLevel <= currentLevel ? $"RANK {targetLevel}  •  OWNED" : targetLevel == currentLevel + 1 ? $"{GetRankTitle(path.creatureId, targetLevel)}  •  1 POINT" : $"RANK {targetLevel}  •  LOCKED";
            }
        }
    }

    private CreaturePath CreatePath(string creatureId, string cardName)
    {
        var card = panel.transform.Find($"Frame/Cards/{cardName}");
        var buttons = new Button[4];
        for (var level = 1; level <= buttons.Length; level++) buttons[level - 1] = card.Find($"Level_{level}").GetComponent<Button>();
        return new CreaturePath { creatureId = creatureId, cardName = cardName, levelButtons = buttons, levelText = card.Find("LevelText").GetComponent<TMP_Text>() };
    }

    private void Purchase(string creatureId, int targetLevel)
    {
        MasterySystem.Instance.TryPurchaseLevel(creatureId, targetLevel);
        Refresh();
    }

    private void SetPanelOpen(bool isOpen)
    {
        panel.SetActive(isOpen);
        UIController.Instance.SetPanelOpen(panel, isOpen);
        if (isOpen) Refresh();
    }

    private static string GetRankTitle(string creatureId, int level)
    {
        if (level == 1) return $"UNLOCK {creatureId.ToUpperInvariant()}";
        if (creatureId == ResourceCartChaser.CreatureId) return level switch { 2 => "RUSH ABILITY", 3 => "RUSH DURATION +100%", _ => "RUSH POWER +50%" };
        return level switch { 2 => "BEAM ABILITY", 3 => "BEAM DURATION +100%", _ => "BEAM DAMAGE +50%" };
    }
}
