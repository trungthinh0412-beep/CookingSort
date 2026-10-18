using System;
using System.Collections;
using System.Collections.Generic;
using CustomTween;
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
    [SerializeField] private float minimumVisibleDuration = 2.4f;
    [SerializeField] private float destinationSwitchBeforeEnd = 0.1f;
    [Header("Cover Visual Animation")]
    [Tooltip("Each root contains one complete transition visual, such as Lighter or Magnet.")]
    [SerializeField] private RectTransform[] visualPacks;
    [SerializeField] private RectTransform title;
    [SerializeField] private RectTransform imageLight;
    [SerializeField] private RectTransform imageBooster;
    [SerializeField] private RectTransform imageArrow;
    [SerializeField] private RectTransform dominoImage1;
    [SerializeField] private RectTransform dominoImage2;
    [SerializeField] private RectTransform dominoImage3;
    [SerializeField] private float titleStartTime = 0f;
    [SerializeField] private float titleScaleDuration = 0.28f;
    [SerializeField] private float lightStartTime = 0.18f;
    [SerializeField] private float lightScaleDuration = 0.68f;
    [SerializeField] private float boosterStartTime = 0.28f;
    [SerializeField] private float boosterScaleDuration = 0.26f;
    [SerializeField] private float arrowStartTime = 0.39f;
    [SerializeField] private float arrowScaleDuration = 0.24f;
    [SerializeField] private float domino1ScaleUpStartTime = 0.52f;
    [SerializeField] private float domino2ScaleUpStartTime = 0.61f;
    [SerializeField] private float domino3ScaleUpStartTime = 0.7f;
    [SerializeField] private float dominoScaleUpDuration = 0.23f;
    [SerializeField] private float domino1ScaleDownStartTime = 1.44f;
    [SerializeField] private float domino2ScaleDownStartTime = 1.51f;
    [SerializeField] private float domino3ScaleDownStartTime = 1.58f;
    [SerializeField] private float dominoScaleDownDuration = 0.2f;
    [SerializeField] private float supportScaleDownStartTime = 1.8f;
    [SerializeField] private float supportScaleDownDuration = 0.22f;
    [SerializeField] private float animationEndTime = 2.2f;
    [SerializeField] private float softBackOvershoot = 1.05f;

    private Coroutine _fadeRoutine;
    private Sequence _visualSequence;
    private float _coverStartedAt;
    private bool _coverFinished;
    private bool _revealRequested;
    private bool _nearFinishInvoked;
    private Action _pendingRevealFinishedCallback;
    private Action _pendingRevealNearFinishCallback;
    private readonly Dictionary<RectTransform, Vector3> _targetScales =
        new Dictionary<RectTransform, Vector3>();
    private RectTransform _activeVisualPack;
    private bool _hasExplicitVisualPack;
    private Vector3 _titleScale;
    private Vector3 _imageLightScale;
    private Vector3 _imageBoosterScale;
    private Vector3 _imageArrowScale;
    private Vector3 _dominoImage1Scale;
    private Vector3 _dominoImage2Scale;
    private Vector3 _dominoImage3Scale;

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
        PlayCoverVisualAnimation();

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
        StopVisualAnimation();
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

    private void PlayCoverVisualAnimation()
    {
        if (!_hasExplicitVisualPack)
            SelectRandomVisualPack();

        _hasExplicitVisualPack = false;
        ResolveVisuals();
        StopVisualAnimation();
        SetTransitionVisualsHidden();

        _visualSequence = Sequence.Create(useUnscaledTime: true);

        // Title_ owns the opening beat. Do not let any supporting element
        // begin scaling until that beat has completed.
        float contentStartTime = titleStartTime + titleScaleDuration;

        InsertScale(
            _visualSequence,
            title,
            titleStartTime,
            Vector3.zero,
            _titleScale,
            titleScaleDuration,
            Easing.Overshoot(softBackOvershoot)
        );
        InsertScale(
            _visualSequence,
            imageLight,
            Mathf.Max(lightStartTime, contentStartTime),
            Vector3.zero,
            _imageLightScale,
            lightScaleDuration,
            Ease.OutCubic
        );
        InsertScale(
            _visualSequence,
            imageBooster,
            Mathf.Max(boosterStartTime, contentStartTime),
            Vector3.zero,
            _imageBoosterScale,
            boosterScaleDuration,
            Easing.Overshoot(softBackOvershoot)
        );
        InsertScale(
            _visualSequence,
            imageArrow,
            Mathf.Max(arrowStartTime, contentStartTime),
            Vector3.zero,
            _imageArrowScale,
            arrowScaleDuration,
            Easing.Overshoot(softBackOvershoot)
        );
        InsertScale(
            _visualSequence,
            dominoImage1,
            Mathf.Max(domino1ScaleUpStartTime, contentStartTime),
            Vector3.zero,
            _dominoImage1Scale,
            dominoScaleUpDuration,
            Easing.Overshoot(softBackOvershoot)
        );
        InsertScale(
            _visualSequence,
            dominoImage2,
            Mathf.Max(domino2ScaleUpStartTime, contentStartTime),
            Vector3.zero,
            _dominoImage2Scale,
            dominoScaleUpDuration,
            Easing.Overshoot(softBackOvershoot)
        );
        InsertScale(
            _visualSequence,
            dominoImage3,
            Mathf.Max(domino3ScaleUpStartTime, contentStartTime),
            Vector3.zero,
            _dominoImage3Scale,
            dominoScaleUpDuration,
            Easing.Overshoot(softBackOvershoot)
        );
        InsertScale(
            _visualSequence,
            dominoImage1,
            domino1ScaleDownStartTime,
            _dominoImage1Scale,
            Vector3.zero,
            dominoScaleDownDuration,
            Ease.InBack
        );
        InsertScale(
            _visualSequence,
            dominoImage2,
            domino2ScaleDownStartTime,
            _dominoImage2Scale,
            Vector3.zero,
            dominoScaleDownDuration,
            Ease.InBack
        );
        InsertScale(
            _visualSequence,
            dominoImage3,
            domino3ScaleDownStartTime,
            _dominoImage3Scale,
            Vector3.zero,
            dominoScaleDownDuration,
            Ease.InBack
        );
        InsertScale(
            _visualSequence,
            imageArrow,
            supportScaleDownStartTime,
            _imageArrowScale,
            Vector3.zero,
            supportScaleDownDuration,
            Ease.InBack
        );
        InsertScale(
            _visualSequence,
            imageBooster,
            supportScaleDownStartTime + 0.06f,
            _imageBoosterScale,
            Vector3.zero,
            supportScaleDownDuration,
            Ease.InBack
        );
        InsertScale(
            _visualSequence,
            imageLight,
            supportScaleDownStartTime + 0.12f,
            _imageLightScale,
            Vector3.zero,
            supportScaleDownDuration,
            Ease.InBack
        );
        if (animationEndTime > 0f)
            _visualSequence.Insert(animationEndTime, Tween.Delay(0.001f));
    }

    private void StopVisualAnimation()
    {
        if (!_visualSequence.isAlive)
        {
            _visualSequence = default;
            return;
        }

        _visualSequence.Stop();
        _visualSequence = default;
    }

    private void InsertScale(
        Sequence sequence,
        RectTransform target,
        float startTime,
        Vector3 from,
        Vector3 to,
        float duration,
        Ease ease
    )
    {
        if (target == null)
            return;

        sequence.InsertCallback(
            startTime,
            target,
            tweenTarget => tweenTarget.gameObject.SetActive(true)
        );
        sequence.Insert(
            startTime,
            Tween.Custom(
                new TweenSettings<Vector3>(
                    from,
                    to,
                    Mathf.Max(0.001f, duration),
                    ease
                ),
                value => target.localScale = value
            )
        );
    }

    private void InsertScale(
        Sequence sequence,
        RectTransform target,
        float startTime,
        Vector3 from,
        Vector3 to,
        float duration,
        Easing easing
    )
    {
        if (target == null)
            return;

        sequence.InsertCallback(
            startTime,
            target,
            tweenTarget => tweenTarget.gameObject.SetActive(true)
        );
        sequence.Insert(
            startTime,
            Tween.Custom(
                new TweenSettings<Vector3>(
                    from,
                    to,
                    Mathf.Max(0.001f, duration),
                    easing
                ),
                value => target.localScale = value
            )
        );
    }

    /// <summary>
    /// Chooses one of the transition packs assigned in <see cref="visualPacks"/>.
    /// Add another root later and select it through
    /// <see cref="SelectVisualPack"/> when its transition should be shown.
    /// </summary>
    public void SelectRandomVisualPack()
    {
        List<RectTransform> candidates = new List<RectTransform>();

        if (visualPacks != null)
        {
            foreach (RectTransform pack in visualPacks)
            {
                if (pack != null)
                    candidates.Add(pack);
            }
        }

        if (candidates.Count > 0)
            ApplyVisualPack(
                candidates[UnityEngine.Random.Range(0, candidates.Count)]
            );
    }

    /// <summary>
    /// Sets the visual pack used by the next cover animation. This lets future
    /// packs (for example Magic Move) reuse the same animation timeline.
    /// </summary>
    public void SelectVisualPack(RectTransform pack)
    {
        if (pack == null)
            return;

        _hasExplicitVisualPack = true;
        ApplyVisualPack(pack);
    }

    private void ApplyVisualPack(RectTransform pack)
    {
        if (visualPacks != null)
        {
            foreach (RectTransform visualPack in visualPacks)
            {
                if (visualPack != null)
                    visualPack.gameObject.SetActive(visualPack == pack);
            }
        }

        _activeVisualPack = pack;
        title = null;
        imageLight = null;
        imageBooster = null;
        imageArrow = null;
        dominoImage1 = null;
        dominoImage2 = null;
        dominoImage3 = null;
    }

    private void ResolveVisuals()
    {
        if (_activeVisualPack == null)
            return;

        title = FindDeepChild(_activeVisualPack, "Title_");
        imageLight = FindDeepChild(_activeVisualPack, "Image_light");
        imageBooster = FindDeepChild(_activeVisualPack, "Image_booster");
        imageArrow = FindDeepChild(_activeVisualPack, "Image_arrow");
        dominoImage1 = FindDeepChild(_activeVisualPack, "Image (1)");
        dominoImage2 = FindDeepChild(_activeVisualPack, "Image (2)");
        dominoImage3 = FindDeepChild(_activeVisualPack, "Image (3)");

        _titleScale = GetTargetScale(title);
        _imageLightScale = GetTargetScale(imageLight);
        _imageBoosterScale = GetTargetScale(imageBooster);
        _imageArrowScale = GetTargetScale(imageArrow);
        _dominoImage1Scale = GetTargetScale(dominoImage1);
        _dominoImage2Scale = GetTargetScale(dominoImage2);
        _dominoImage3Scale = GetTargetScale(dominoImage3);

    }

    private void SetTransitionVisualsHidden()
    {
        SetScale(title, Vector3.zero);
        SetScale(imageLight, Vector3.zero);
        SetScale(imageBooster, Vector3.zero);
        SetScale(imageArrow, Vector3.zero);
        SetScale(dominoImage1, Vector3.zero);
        SetScale(dominoImage2, Vector3.zero);
        SetScale(dominoImage3, Vector3.zero);
    }

    private static RectTransform FindDeepChild(
        RectTransform root,
        string targetName
    )
    {
        if (root == null)
            return null;

        RectTransform[] children = root.GetComponentsInChildren<RectTransform>(true);

        foreach (RectTransform child in children)
        {
            if (child.name.Trim() == targetName)
                return child;
        }

        return null;
    }

    private Vector3 GetTargetScale(RectTransform target)
    {
        if (target == null)
            return Vector3.one;

        if (!_targetScales.TryGetValue(target, out Vector3 targetScale))
        {
            targetScale = target.localScale;
            _targetScales.Add(target, targetScale);
        }

        return targetScale;
    }

    private static void SetScale(RectTransform target, Vector3 scale)
    {
        if (target == null)
            return;

        target.gameObject.SetActive(true);
        target.localScale = scale;
    }

    private static float EaseInOutCubic(float value)
    {
        value = Mathf.Clamp01(value);

        if (value < 0.5f)
            return 4f * value * value * value;

        return 1f - Mathf.Pow(-2f * value + 2f, 3f) / 2f;
    }
}
