using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Repeats a Soul Core button action while held, with an accelerating repeat rate.</summary>
public class SoulCoreHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private const float InitialRepeatDelay = 0.45f;
    private const float InitialRepeatInterval = 0.32f;
    private const float RepeatRateAcceleration = 4f;
    private Button button;
    private Action action;
    private bool pointerHeld;
    private bool suppressNextClick;
    private bool repeatedDuringHold;
    private float repeatDelayRemaining;
    private float repeatHoldDuration;
    private float repeatProgress;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    /// <summary>Connects the action invoked on click and while the pointer is held.</summary>
    public void Initialize(Action buttonAction)
    {
        action = buttonAction;
        button.onClick.AddListener(HandleClick);
    }

    void Update()
    {
        if (!pointerHeld) return;
        var deltaTime = Time.unscaledDeltaTime;
        if (repeatDelayRemaining > 0f)
        {
            var delayDelta = Mathf.Min(repeatDelayRemaining, deltaTime);
            repeatDelayRemaining -= delayDelta;
            deltaTime -= delayDelta;
            if (deltaTime <= 0f) return;
        }
        if (!button.interactable)
        {
            repeatProgress = 0f;
            return;
        }
        repeatHoldDuration += deltaTime;
        repeatProgress += deltaTime * (1f + repeatHoldDuration * RepeatRateAcceleration) / InitialRepeatInterval;
        while (repeatProgress >= 1f && button.interactable)
        {
            repeatProgress -= 1f;
            action.Invoke();
            repeatedDuringHold = true;
        }
        if (!button.interactable) repeatProgress = 0f;
    }

    /// <summary>Starts hold repetition when the pointer presses the button.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.interactable) return;
        pointerHeld = true;
        suppressNextClick = false;
        repeatedDuringHold = false;
        repeatDelayRemaining = InitialRepeatDelay;
        repeatHoldDuration = 0f;
        repeatProgress = 0f;
    }

    /// <summary>Stops repetition and prevents the release click from duplicating a repeated action.</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        pointerHeld = false;
        suppressNextClick = repeatedDuringHold && eventData.pointerPress == gameObject;
    }

    void HandleClick()
    {
        if (suppressNextClick)
        {
            suppressNextClick = false;
            return;
        }
        if (button.interactable) action.Invoke();
    }
}
