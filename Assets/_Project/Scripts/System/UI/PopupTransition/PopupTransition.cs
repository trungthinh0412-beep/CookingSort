using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Lightweight UI-only transition. It is independent from TransitionManager,
/// which remains responsible for the loading-scene transition.
/// </summary>
public sealed class PopupTransition : Popup
{
    [SerializeField] private CanvasGroup transitionGroup;
    [SerializeField] private float coverDuration = 0.18f;
    [SerializeField] private float revealDuration = 0.22f;
    [SerializeField] private float minimumVisibleDuration = 1.5f;
    [SerializeField] private float destinationSwitchBeforeEnd = 0.1f;

    private Coroutine _fadeRoutine;
    private float _coverStartedAt;
    private bool _coverFinished;
    private bool _revealRequested;
    private bool _nearFinishInvoked;
    private Action _pendingRevealFinishedCallback;
    private Action _pendingRevealNearFinishCallback;

    public void ShowAndCover(Action onCovered = null)
    {
        if (PopupController.Instance != null)
        {
            PopupController.Instance.Show<PopupTransition>(
                PopupAnimation.None
            );
        }
        else if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        PlayCover(onCovered);
    }

    public void PlayCover(Action onCovered = null)
    {
        StopFade();
        _coverStartedAt = Time.unscaledTime;
        _coverFinished = false;
        _revealRequested = false;
        _nearFinishInvoked = false;
        _pendingRevealFinishedCallback = null;
        _pendingRevealNearFinishCallback = null;

        BringToFront();
        SetRaycastState(true);
        SetAlpha(0f);

        _fadeRoutine = StartCoroutine(
            FadeRoutine(
                0f,
                1f,
                coverDuration,
                () =>
                {
                    _coverFinished = true;
                    onCovered?.Invoke();

                    if (_revealRequested)
                    {
                        Action finishedCallback =
                            _pendingRevealFinishedCallback;
                        Action nearFinishCallback =
                            _pendingRevealNearFinishCallback;
                        _revealRequested = false;
                        _pendingRevealFinishedCallback = null;
                        _pendingRevealNearFinishCallback = null;
                        PlayReveal(
                            finishedCallback,
                            nearFinishCallback
                        );
                    }
                }
            )
        );
    }

    public void PlayReveal(
        Action onFinished = null,
        Action onNearFinish = null
    )
    {
        if (!isActiveAndEnabled)
        {
            onNearFinish?.Invoke();
            onFinished?.Invoke();
            return;
        }

        if (!_coverFinished)
        {
            _revealRequested = true;
            _pendingRevealFinishedCallback = onFinished;
            _pendingRevealNearFinishCallback = onNearFinish;
            return;
        }

        StopFade();
        BringToFront();
        SetRaycastState(true);

        _nearFinishInvoked = false;
        _fadeRoutine = StartCoroutine(
            RevealRoutine(onFinished, onNearFinish)
        );
    }

    protected override void OnDisable()
    {
        StopFade();
        _coverFinished = false;
        _revealRequested = false;
        _nearFinishInvoked = false;
        _pendingRevealFinishedCallback = null;
        _pendingRevealNearFinishCallback = null;

        base.OnDisable();
    }

    private IEnumerator RevealRoutine(
        Action onFinished,
        Action onNearFinish
    )
    {
        float elapsedSinceCover = Time.unscaledTime - _coverStartedAt;
        float safeRevealDuration = Mathf.Max(0.001f, revealDuration);
        float delay = Mathf.Max(
            0f,
            minimumVisibleDuration - elapsedSinceCover - safeRevealDuration
        );

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        float elapsed = 0f;
        float switchThreshold = Mathf.Clamp(
            destinationSwitchBeforeEnd,
            0f,
            safeRevealDuration
        );
        float destinationSwitchTime =
            safeRevealDuration - switchThreshold;
        float from = transitionGroup != null
            ? transitionGroup.alpha
            : CanvasGroup != null
                ? CanvasGroup.alpha
                : 1f;

        while (elapsed < safeRevealDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (!_nearFinishInvoked &&
                elapsed >= destinationSwitchTime)
            {
                // Let the destination popup replace the old one during the
                // last part of the fade, not after this object is disabled.
                // This keeps the hand-off free of a one-frame flash.
                _nearFinishInvoked = true;
                onNearFinish?.Invoke();
            }

            // Keep the old popup completely covered until the hand-off. Only
            // the final destinationSwitchBeforeEnd seconds reveal the new
            // popup, so the previous popup cannot show through the fade.
            float fadeProgress = switchThreshold > 0f
                ? Mathf.Clamp01(
                    (elapsed - destinationSwitchTime) /
                    switchThreshold
                )
                : 0f;
            fadeProgress = EaseInOutCubic(fadeProgress);
            SetAlpha(Mathf.LerpUnclamped(from, 0f, fadeProgress));

            yield return null;
        }

        // Keep this object alive until the final callback has completed.
        // The callback normally has already switched the destination popup
        // at the 0.1-second hand-off point above.
        SetAlpha(0f);
        _fadeRoutine = null;
        try
        {
            onFinished?.Invoke();
        }
        finally
        {
            SetRaycastState(false);
            gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeRoutine(
        float from,
        float to,
        float duration,
        Action onFinished
    )
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.001f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / safeDuration);
            progress = EaseInOutCubic(progress);
            SetAlpha(Mathf.LerpUnclamped(from, to, progress));

            yield return null;
        }

        SetAlpha(to);
        _fadeRoutine = null;
        onFinished?.Invoke();
    }

    private void BringToFront()
    {
        transform.SetAsLastSibling();

        Canvas canvas = Canvas;
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10000;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (transitionGroup != null)
            transitionGroup.alpha = alpha;

        CanvasGroup rootGroup = CanvasGroup;
        if (rootGroup != null && rootGroup != transitionGroup)
            rootGroup.alpha = alpha;
    }

    private void SetRaycastState(bool enabled)
    {
        if (transitionGroup != null)
        {
            transitionGroup.interactable = enabled;
            transitionGroup.blocksRaycasts = enabled;
        }

        CanvasGroup rootGroup = CanvasGroup;
        if (rootGroup != null && rootGroup != transitionGroup)
        {
            rootGroup.interactable = enabled;
            rootGroup.blocksRaycasts = enabled;
        }
    }

    private void StopFade()
    {
        if (_fadeRoutine == null)
            return;

        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
    }

    private static float EaseInOutCubic(float value)
    {
        value = Mathf.Clamp01(value);

        if (value < 0.5f)
            return 4f * value * value * value;

        return 1f - Mathf.Pow(-2f * value + 2f, 3f) / 2f;
    }
}
