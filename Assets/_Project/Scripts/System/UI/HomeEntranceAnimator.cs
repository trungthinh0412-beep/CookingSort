using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable UI entrance animation for a screen composed of RectTransforms.
/// Each target is moved from outside the screen back to its cached hierarchy
/// position. The animation uses unscaled time so it also works while paused.
/// It also owns the reversible Home-to-Kingdom-Build transition.
/// </summary>
public sealed class HomeEntranceAnimator : MonoBehaviour
{
    private enum SlideDirection
    {
        FromTop,
        FromBottom,
        FromLeft,
        FromRight
    }

    [Header("Targets")]
    [SerializeField] private RectTransform topBar;
    [SerializeField] private RectTransform bottomBar;
    [SerializeField] private RectTransform groupBtnRight;
    [SerializeField] private RectTransform groupBtnLeft;
    [SerializeField] private RectTransform playButton;
    [SerializeField] private RectTransform taskButton;
    [SerializeField] private RectTransform tableAndKingRoot;
    [SerializeField] private RectTransform homeBackground;

    [Header("Automatic references")]
    [SerializeField] private bool findTargetsByName = true;
    [SerializeField] private bool includePersistentBottomBar = true;

    [Header("Timing")]
    [SerializeField] private float duration = 0.45f;
    [SerializeField] private float stagger = 0.04f;
    [SerializeField] private float extraHiddenOffset = 24f;

    [Header("Kingdom build transition")]
    [SerializeField] private float kingdomTransitionDuration = 0.30f;
    [SerializeField] private Vector3 kingdomBackgroundScale = Vector3.one;
    [SerializeField] private bool fadeTableAndKing = true;

    private readonly Dictionary<RectTransform, Vector2> shownPositions =
        new Dictionary<RectTransform, Vector2>();

    private Coroutine animationRoutine;
    private CanvasGroup tableAndKingCanvasGroup;
    private CanvasGroup homeCanvasGroup;
    private CanvasGroup bottomBarCanvasGroup;
    private HomeBackgroundScaleBlocker backgroundScaleBlocker;
    private Vector3 originalBackgroundScale = Vector3.one;
    private Vector3 homeBackgroundScale = Vector3.one;
    private float originalTableAndKingAlpha = 1f;
    private bool originalTableAndKingInteractable = true;
    private bool originalTableAndKingBlocksRaycasts = true;
    private bool originalHomeInteractable = true;
    private bool originalHomeBlocksRaycasts = true;
    private bool originalBottomBarInteractable = true;
    private bool originalBottomBarBlocksRaycasts = true;
    private bool bottomBarInputCached;
    private bool homeStateCached;
    private bool homeBackgroundScaleCached;

    public bool IsTransitioning => animationRoutine != null;

    private void Awake()
    {
        ResolveTargets();
        CacheHomeState();
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    /// <summary>
    /// Plays the entrance animation. Calling it again safely restarts it.
    /// </summary>
    public void Play()
    {
        ResolveTargets();
        Canvas.ForceUpdateCanvases();

        List<SlideTarget> targets = BuildTargets();
        if (targets.Count == 0)
            return;

        CacheShownPositions(targets);

        if (animationRoutine != null)
            StopAnimation();

        animationRoutine = StartCoroutine(PlayRoutine(targets));
    }

    /// <summary>
    /// Moves the Home UI away so the Kingdom Build view can take over.
    /// All Home positions are restored by PlayReturnFromKingdom().
    /// </summary>
    public void PlayExitToKingdom(Action onComplete = null)
    {
        ResolveTargets();
        Canvas.ForceUpdateCanvases();
        CacheHomeState();

        List<SlideTarget> targets = BuildTargets();
        CacheShownPositions(targets);

        if (homeBackground != null)
        {
            if (!homeBackgroundScaleCached)
            {
                homeBackgroundScale = homeBackground.localScale;
                homeBackgroundScaleCached = true;
            }

            backgroundScaleBlocker?.BeginTransitionOverride();
        }

        SetHomeInput(false);
        SetBottomBarInput(false);
        SetTableAndKingInput(false);

        StartTransition(
            targets,
            GetKingdomTargetPositions(targets),
            GetKingdomTargetBackgroundScale(),
            0f,
            onComplete
        );
    }

    /// <summary>
    /// Reverses PlayExitToKingdom without snapping any Home element.
    /// </summary>
    public void PlayReturnFromKingdom(Action onComplete = null)
    {
        ResolveTargets();
        Canvas.ForceUpdateCanvases();
        CacheHomeState();

        List<SlideTarget> targets = BuildTargets();
        CacheShownPositions(targets);

        SetHomeInput(false);
        SetBottomBarInput(false);
        SetTableAndKingInput(false);

        StartTransition(
            targets,
            GetHomeTargetPositions(targets),
            homeBackgroundScaleCached
                ? homeBackgroundScale
                : originalBackgroundScale,
            originalTableAndKingAlpha,
            onComplete
        );
    }

    /// <summary>
    /// Restores Home immediately when another system disables PopupHome or a
    /// transition is cancelled.
    /// </summary>
    public void RestoreHomeState()
    {
        StopAnimation();
        ResolveTargets();
        Canvas.ForceUpdateCanvases();

        List<SlideTarget> targets = BuildTargets();
        CacheShownPositions(targets);

        for (int index = 0; index < targets.Count; index++)
        {
            SlideTarget target = targets[index];
            target.Transform.anchoredPosition = shownPositions[target.Transform];
        }

        if (homeBackground != null)
        {
            homeBackground.localScale = homeBackgroundScaleCached
                ? homeBackgroundScale
                : originalBackgroundScale;
        }

        if (tableAndKingCanvasGroup != null)
        {
            tableAndKingCanvasGroup.alpha = originalTableAndKingAlpha;
            SetTableAndKingInput(true);
        }

        SetHomeInput(true);
        SetBottomBarInput(true);
        backgroundScaleBlocker?.EndTransitionOverride(homeBackground != null
            ? homeBackground.localScale
            : originalBackgroundScale);
    }

    /// <summary>
    /// Clears cached positions. Use this after changing the hierarchy at runtime.
    /// </summary>
    public void RefreshPositions()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        shownPositions.Clear();
        ResolveTargets();
        CacheHomeState();
    }

    private void ResolveTargets()
    {
        if (findTargetsByName)
        {
            if (topBar == null)
                topBar = FindChild("Topbar");

            if (groupBtnRight == null)
                groupBtnRight = FindChild("Group_btn_Right");

            if (groupBtnLeft == null)
                groupBtnLeft = FindChild("Group_btn_Left");

            if (playButton == null)
                playButton = FindChild("BtnPlay");

            if (taskButton == null)
                taskButton = FindChild("Btn_Task");

            if (tableAndKingRoot == null)
                tableAndKingRoot = FindChild("Tbale_decor");

            if (homeBackground == null)
                homeBackground = FindChild("Background");
        }

        if (includePersistentBottomBar && bottomBar == null &&
            PopupController.Instance != null &&
            PopupController.Instance.BottomBarInstance != null)
        {
            bottomBar = PopupController.Instance.BottomBarInstance.gameObject
                .GetComponent<RectTransform>();
        }
    }

    private List<SlideTarget> BuildTargets()
    {
        var targets = new List<SlideTarget>(6);

        AddTarget(targets, topBar, SlideDirection.FromTop);
        AddTarget(targets, bottomBar, SlideDirection.FromBottom);
        AddTarget(targets, groupBtnRight, SlideDirection.FromRight);
        AddTarget(targets, groupBtnLeft, SlideDirection.FromLeft);
        AddTarget(targets, playButton, SlideDirection.FromBottom);
        AddTarget(targets, taskButton, SlideDirection.FromBottom);

        return targets;
    }

    private static void AddTarget(
        List<SlideTarget> targets,
        RectTransform target,
        SlideDirection direction)
    {
        if (target == null || !target.gameObject.activeSelf)
            return;

        for (int index = 0; index < targets.Count; index++)
        {
            if (targets[index].Transform == target)
                return;
        }

        targets.Add(new SlideTarget(target, direction));
    }

    private void CacheShownPositions(List<SlideTarget> targets)
    {
        for (int index = 0; index < targets.Count; index++)
        {
            RectTransform target = targets[index].Transform;

            if (!shownPositions.ContainsKey(target))
                shownPositions.Add(target, target.anchoredPosition);
        }
    }

    private IEnumerator PlayRoutine(List<SlideTarget> targets)
    {
        for (int index = 0; index < targets.Count; index++)
        {
            SlideTarget target = targets[index];
            Vector2 shownPosition = shownPositions[target.Transform];
            target.Transform.anchoredPosition = GetHiddenPosition(
                target.Transform,
                shownPosition,
                target.Direction);
        }

        float totalDuration = Mathf.Max(duration, 0.001f);
        float elapsed = 0f;
        float finalTime = totalDuration +
            Mathf.Max(0f, stagger) * Mathf.Max(0, targets.Count - 1);

        while (elapsed < finalTime)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int index = 0; index < targets.Count; index++)
            {
                SlideTarget target = targets[index];
                float startTime = Mathf.Max(0f, stagger) * index;
                float progress = Mathf.Clamp01(
                    (elapsed - startTime) / totalDuration);

                if (progress <= 0f)
                    continue;

                progress = EaseOutCubic(progress);

                Vector2 hiddenPosition = GetHiddenPosition(
                    target.Transform,
                    shownPositions[target.Transform],
                    target.Direction);

                target.Transform.anchoredPosition = Vector2.LerpUnclamped(
                    hiddenPosition,
                    shownPositions[target.Transform],
                    progress);
            }

            yield return null;
        }

        for (int index = 0; index < targets.Count; index++)
        {
            SlideTarget target = targets[index];
            target.Transform.anchoredPosition = shownPositions[target.Transform];
        }

        animationRoutine = null;
    }

    private void StartTransition(
        List<SlideTarget> targets,
        Dictionary<RectTransform, Vector2> targetPositions,
        Vector3 targetBackgroundScale,
        float targetTableAndKingAlpha,
        Action onComplete)
    {
        StopAnimation();

        if (targets.Count == 0 && homeBackground == null &&
            (!fadeTableAndKing || tableAndKingCanvasGroup == null))
        {
            CompleteTransition(targetTableAndKingAlpha, targetBackgroundScale);
            onComplete?.Invoke();
            return;
        }

        animationRoutine = StartCoroutine(TransitionRoutine(
            targets,
            targetPositions,
            targetBackgroundScale,
            targetTableAndKingAlpha,
            onComplete));
    }

    private IEnumerator TransitionRoutine(
        List<SlideTarget> targets,
        Dictionary<RectTransform, Vector2> targetPositions,
        Vector3 targetBackgroundScale,
        float targetTableAndKingAlpha,
        Action onComplete)
    {
        var startPositions = new Dictionary<RectTransform, Vector2>();

        for (int index = 0; index < targets.Count; index++)
        {
            RectTransform target = targets[index].Transform;
            startPositions[target] = target.anchoredPosition;
        }

        Vector3 startBackgroundScale = homeBackground != null
            ? homeBackground.localScale
            : targetBackgroundScale;
        float startTableAndKingAlpha = tableAndKingCanvasGroup != null
            ? tableAndKingCanvasGroup.alpha
            : targetTableAndKingAlpha;

        float safeDuration = Mathf.Max(0.001f, kingdomTransitionDuration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            progress = EaseInOutCubic(progress);

            for (int index = 0; index < targets.Count; index++)
            {
                RectTransform target = targets[index].Transform;
                target.anchoredPosition = Vector2.LerpUnclamped(
                    startPositions[target],
                    targetPositions[target],
                    progress);
            }

            if (homeBackground != null)
            {
                homeBackground.localScale = Vector3.LerpUnclamped(
                    startBackgroundScale,
                    targetBackgroundScale,
                    progress);
            }

            if (tableAndKingCanvasGroup != null && fadeTableAndKing)
            {
                tableAndKingCanvasGroup.alpha = Mathf.LerpUnclamped(
                    startTableAndKingAlpha,
                    targetTableAndKingAlpha,
                    progress);
            }

            yield return null;
        }

        for (int index = 0; index < targets.Count; index++)
        {
            RectTransform target = targets[index].Transform;
            target.anchoredPosition = targetPositions[target];
        }

        CompleteTransition(targetTableAndKingAlpha, targetBackgroundScale);
        animationRoutine = null;
        onComplete?.Invoke();
    }

    private void CompleteTransition(
        float targetTableAndKingAlpha,
        Vector3 targetBackgroundScale)
    {
        if (homeBackground != null)
            homeBackground.localScale = targetBackgroundScale;

        if (tableAndKingCanvasGroup != null && fadeTableAndKing)
        {
            tableAndKingCanvasGroup.alpha = targetTableAndKingAlpha;

            if (Mathf.Approximately(targetTableAndKingAlpha, 0f))
                SetTableAndKingInput(false);
            else
                SetTableAndKingInput(true);
        }

        if (Mathf.Approximately(targetTableAndKingAlpha, 0f))
        {
            SetHomeInput(false);
            SetBottomBarInput(false);
        }
        else
        {
            SetHomeInput(true);
            SetBottomBarInput(true);

            if (homeBackground != null)
            {
                backgroundScaleBlocker?.EndTransitionOverride(
                    homeBackground.localScale);
            }
        }
    }

    private Dictionary<RectTransform, Vector2> GetKingdomTargetPositions(
        List<SlideTarget> targets)
    {
        var result = new Dictionary<RectTransform, Vector2>();

        for (int index = 0; index < targets.Count; index++)
        {
            SlideTarget target = targets[index];
            result[target.Transform] = GetHiddenPosition(
                target.Transform,
                shownPositions[target.Transform],
                target.Direction);
        }

        return result;
    }

    private Dictionary<RectTransform, Vector2> GetHomeTargetPositions(
        List<SlideTarget> targets)
    {
        var result = new Dictionary<RectTransform, Vector2>();

        for (int index = 0; index < targets.Count; index++)
        {
            SlideTarget target = targets[index];
            result[target.Transform] = shownPositions[target.Transform];
        }

        return result;
    }

    private Vector3 GetKingdomTargetBackgroundScale()
    {
        return kingdomBackgroundScale;
    }

    private void CacheHomeState()
    {
        if (homeStateCached)
            return;

        homeCanvasGroup = GetComponent<CanvasGroup>();
        backgroundScaleBlocker = GetComponent<HomeBackgroundScaleBlocker>();

        if (homeBackground != null)
            originalBackgroundScale = homeBackground.localScale;

        if (tableAndKingRoot != null)
        {
            tableAndKingCanvasGroup =
                tableAndKingRoot.GetComponent<CanvasGroup>();

            if (tableAndKingCanvasGroup == null)
            {
                tableAndKingCanvasGroup =
                    tableAndKingRoot.gameObject.AddComponent<CanvasGroup>();
            }

            originalTableAndKingAlpha = tableAndKingCanvasGroup.alpha;
            originalTableAndKingInteractable =
                tableAndKingCanvasGroup.interactable;
            originalTableAndKingBlocksRaycasts =
                tableAndKingCanvasGroup.blocksRaycasts;
        }

        if (homeCanvasGroup != null)
        {
            originalHomeInteractable = homeCanvasGroup.interactable;
            originalHomeBlocksRaycasts = homeCanvasGroup.blocksRaycasts;
        }

        homeStateCached = true;
    }

    private void SetHomeInput(bool enabled)
    {
        if (homeCanvasGroup == null)
            return;

        homeCanvasGroup.interactable = enabled
            ? originalHomeInteractable
            : false;
        homeCanvasGroup.blocksRaycasts = enabled
            ? originalHomeBlocksRaycasts
            : false;
    }

    private void SetBottomBarInput(bool enabled)
    {
        if (bottomBar == null)
            return;

        if (bottomBarCanvasGroup == null)
        {
            bottomBarCanvasGroup =
                bottomBar.GetComponent<CanvasGroup>();

            if (bottomBarCanvasGroup == null)
                bottomBarCanvasGroup = bottomBar.gameObject.AddComponent<CanvasGroup>();
        }

        if (!bottomBarInputCached)
        {
            originalBottomBarInteractable =
                bottomBarCanvasGroup.interactable;
            originalBottomBarBlocksRaycasts =
                bottomBarCanvasGroup.blocksRaycasts;
            bottomBarInputCached = true;
        }

        bottomBarCanvasGroup.interactable = enabled
            ? originalBottomBarInteractable
            : false;
        bottomBarCanvasGroup.blocksRaycasts = enabled
            ? originalBottomBarBlocksRaycasts
            : false;
    }

    private void SetTableAndKingInput(bool enabled)
    {
        if (tableAndKingCanvasGroup == null)
            return;

        tableAndKingCanvasGroup.interactable = enabled
            ? originalTableAndKingInteractable
            : false;
        tableAndKingCanvasGroup.blocksRaycasts = enabled
            ? originalTableAndKingBlocksRaycasts
            : false;
    }

    private void StopAnimation()
    {
        if (animationRoutine == null)
            return;

        StopCoroutine(animationRoutine);
        animationRoutine = null;
    }

    private Vector2 GetHiddenPosition(
        RectTransform target,
        Vector2 shownPosition,
        SlideDirection direction)
    {
        RectTransform parent = target.parent as RectTransform;
        float parentSize = 0f;
        float targetSize = 0f;

        if (direction == SlideDirection.FromLeft ||
            direction == SlideDirection.FromRight)
        {
            parentSize = parent != null ? parent.rect.width : 0f;
            targetSize = target.rect.width;
        }
        else
        {
            parentSize = parent != null ? parent.rect.height : 0f;
            targetSize = target.rect.height;
        }

        float offset = parentSize * 0.5f + targetSize * 0.5f +
            Mathf.Max(0f, extraHiddenOffset);

        Vector2 hiddenPosition = shownPosition;

        switch (direction)
        {
            case SlideDirection.FromTop:
                hiddenPosition.y += offset;
                break;
            case SlideDirection.FromBottom:
                hiddenPosition.y -= offset;
                break;
            case SlideDirection.FromLeft:
                hiddenPosition.x -= offset;
                break;
            case SlideDirection.FromRight:
                hiddenPosition.x += offset;
                break;
        }

        return hiddenPosition;
    }

    private RectTransform FindChild(string childName)
    {
        RectTransform[] children =
            GetComponentsInChildren<RectTransform>(true);

        for (int index = 0; index < children.Length; index++)
        {
            RectTransform child = children[index];

            if (child != transform && child.name == childName)
                return child;
        }

        return null;
    }

    private static float EaseOutCubic(float value)
    {
        value = Mathf.Clamp01(value);
        return 1f - Mathf.Pow(1f - value, 3f);
    }

    private static float EaseInOutCubic(float value)
    {
        value = Mathf.Clamp01(value);

        return value < 0.5f
            ? 4f * value * value * value
            : 1f - Mathf.Pow(-2f * value + 2f, 3f) / 2f;
    }

    private readonly struct SlideTarget
    {
        public readonly RectTransform Transform;
        public readonly SlideDirection Direction;

        public SlideTarget(RectTransform transform, SlideDirection direction)
        {
            Transform = transform;
            Direction = direction;
        }
    }
}
