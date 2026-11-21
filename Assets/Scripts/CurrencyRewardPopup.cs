using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Displays pooled screen-space currency reward popups near vaporised resources.</summary>
public sealed class CurrencyRewardPopup : MonoBehaviour
{
    private const float RiseDuration = 0.2f;
    private const float FlightDuration = 0.55f;
    private const float RiseDistance = 65f;
    [SerializeField] private float popupLingerDuration = 0.4f;
    private static readonly Color RewardColor = new(1f, 0.82f, 0.2f, 1f);
    private readonly List<Popup> popups = new();
    private Canvas popupCanvas;
    private RectTransform canvasRect;
    [SerializeField] private RectTransform popupParent;
    [SerializeField] private TextMeshProUGUI popupTemplate;
    private Camera mainCamera;

    private sealed class Popup
    {
        public TextMeshProUGUI Text;
        public Vector2 StartPosition;
        public float Elapsed;
        public int Amount;
    }

    public static CurrencyRewardPopup Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        popupCanvas = GetComponent<Canvas>();
        canvasRect = (RectTransform)popupCanvas.transform;
        mainCamera = Camera.main;
        popupTemplate.gameObject.SetActive(false);
        popups.Add(new Popup { Text = popupTemplate });
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        for (var index = 0; index < popups.Count; index++)
        {
            var popup = popups[index];
            if (!popup.Text.gameObject.activeSelf) continue;
            popup.Elapsed += Time.deltaTime;
            if (popup.Elapsed < RiseDuration)
            {
                var riseProgress = popup.Elapsed / RiseDuration;
                popup.Text.rectTransform.anchoredPosition = popup.StartPosition + Vector2.up * (RiseDistance * riseProgress);
                popup.Text.color = RewardColor;
                continue;
            }

            var flightStartPosition = popup.StartPosition + Vector2.up * RiseDistance;
            if (popup.Elapsed < RiseDuration + popupLingerDuration)
            {
                popup.Text.rectTransform.anchoredPosition = flightStartPosition;
                popup.Text.color = RewardColor;
                continue;
            }

            var flightElapsed = popup.Elapsed - RiseDuration - popupLingerDuration;
            var flightProgress = Mathf.Clamp01(flightElapsed / FlightDuration);
            var eventCamera = popupCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : popupCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, UIController.Instance.GetCurrencyCounterScreenPosition(), eventCamera, out var counterPosition);
            popup.Text.rectTransform.anchoredPosition = Vector2.Lerp(flightStartPosition, counterPosition, Mathf.SmoothStep(0f, 1f, flightProgress));
            popup.Text.color = RewardColor;
            if (flightProgress >= 1f)
            {
                GameController.instance.AddCurrency(popup.Amount);
                UIController.Instance.PulseCurrencyCounter();
                popup.Text.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>Shows the currency gained at the supplied resource world position.</summary>
    public void Show(int amount, Vector3 worldPosition)
    {
        var screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
        var eventCamera = popupCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : popupCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out var canvasPosition);
        Popup popup = null;
        for (var index = 0; index < popups.Count; index++)
        {
            if (popups[index].Text.gameObject.activeSelf) continue;
            popup = popups[index];
            break;
        }
        if (popup == null) popup = CreatePopup();
        popup.Text.text = $"+${amount:N0}";
        popup.Text.color = RewardColor;
        popup.Amount = amount;
        popup.StartPosition = canvasPosition;
        popup.Elapsed = 0f;
        popup.Text.rectTransform.anchoredPosition = canvasPosition;
        popup.Text.gameObject.SetActive(true);
    }

    private Popup CreatePopup()
    {
        var text = Instantiate(popupTemplate, popupParent, false);
        text.name = "CurrencyRewardPopupText";
        var popup = new Popup { Text = text };
        popups.Add(popup);
        return popup;
    }
}
