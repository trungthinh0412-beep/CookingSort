using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum SpriteAnimatorTargetType
{
    UI,
    World
}

public enum SpriteAnimatorFrameSource
{
    SpriteSheet,
    FrameByFrame
}

/// <summary>
/// A sprite-sheet animator that can render either to a Canvas Image or a
/// world-space SpriteRenderer. Choose the target in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpriteAnimator : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private SpriteAnimatorTargetType targetType;
    [SerializeField] private Image targetImage;
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Frames")]
    [SerializeField] private SpriteAnimatorFrameSource frameSource;
    [Tooltip("Optional source texture for UI grid frames.")]
    [SerializeField] private Texture2D spriteSheet;
    [SerializeField] private Sprite[] frames = new Sprite[0];

    [Header("UI Grid Frames")]
    [SerializeField] private bool generateGridFrames;
    [SerializeField, Min(1)] private int gridColumns = 1;
    [SerializeField, Min(1)] private int gridRows = 1;
    [SerializeField] private float gridPixelsPerUnit = 100f;

    [Header("UI Frame FX")]
    [SerializeField] private bool useFrameFx;
    [SerializeField] private GameObject frameFx;
    [SerializeField, Min(1)] private int playFxAtFrame = 1;

    [Header("World Animation Library")]
    [SerializeField] private List<WorldSpriteAnimation> animations =
        new List<WorldSpriteAnimation>();
    [SerializeField] private string playOnEnableAnimation = "Default";
    [SerializeField] private bool enableIdleVariation;
    [SerializeField] private string primaryIdleAnimation = "Idle_1";
    [SerializeField] private string alternateIdleAnimation = "Idle_2";
    [SerializeField, Range(0f, 1f)] private float idleVariationChance = 0.2f;

    [Header("World Transition")]
    [Tooltip("Finish the current clip's full cycle before switching to another clip.")]
    [SerializeField] private bool finishCurrentAnimationBeforeSwitching = true;

    [Header("Linked Idle Chain")]
    [SerializeField] private bool useLinkedIdleChain;
    [SerializeField] private string linkedIdle1 = "Idle_1";
    [SerializeField] private string linkedIdle2 = "Idle_2";
    [SerializeField] private string linkedIdle3 = "Idle_3";
    [SerializeField, Range(0f, 1f)] private float idle1Chance = .75f;
    [SerializeField, Range(0f, 1f)] private float idle2Chance = .2f;
    [SerializeField, Range(0f, 1f)] private float idle3Chance = .05f;

    [Header("Playback")]
    [SerializeField, Min(0.01f)] private float fps = 12f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool pingPong;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool ignoreTimeScale;

    [Header("Delays")]
    [SerializeField] private bool useStartDelay;
    [SerializeField, Min(0f)] private float startDelay;
    [SerializeField] private bool useLoopDelay;
    [SerializeField, Min(0f)] private float loopDelay;

    private int currentFrame;
    private int direction = 1;
    private float elapsed;
    private float delayRemaining;
    private bool isPlaying;
    private int currentWorldAnimationIndex = -1;
    private Sprite[] runtimeGridFrames;
    private string nextWorldAnimationName;
    private string pendingWorldAnimationName;
    private float pendingTransitionFrameDuration = -1f;
    private float pendingTransitionFps = -1f;
    private bool playingPendingExitRange;
    private bool pendingSwitchAtForwardBoundary;
    private string queuedForwardBoundaryAnimationName;
    private bool primaryIdleOnly;
    private int linkedIdleDestination;

    public string CurrentAnimationName => CurrentWorldAnimation?.Name ?? string.Empty;
    public bool IsPlaying => isPlaying;

    private WorldSpriteAnimation CurrentWorldAnimation
    {
        get
        {
            if (animations == null || currentWorldAnimationIndex < 0 ||
                currentWorldAnimationIndex >= animations.Count)
                return null;

            return animations[currentWorldAnimationIndex];
        }
    }

    private Sprite[] ActiveFrames
    {
        get
        {
            if (targetType == SpriteAnimatorTargetType.World &&
                animations != null && animations.Count > 0 &&
                CurrentWorldAnimation != null && CurrentWorldAnimation.HasFrames)
                return CurrentWorldAnimation.Frames;
            if (targetType != SpriteAnimatorTargetType.UI ||
                frameSource == SpriteAnimatorFrameSource.FrameByFrame)
                return frames;

            return generateGridFrames ? GetOrCreateGridFrames() : frames;
        }
    }

    private float ActiveFps => targetType == SpriteAnimatorTargetType.World &&
        CurrentWorldAnimation != null ? CurrentWorldAnimation.Fps : fps;
    private bool ActiveLoop => targetType == SpriteAnimatorTargetType.World &&
        CurrentWorldAnimation != null ? CurrentWorldAnimation.Loop : loop;
    private bool ActivePingPong => targetType == SpriteAnimatorTargetType.World &&
        CurrentWorldAnimation != null ? CurrentWorldAnimation.PingPong : pingPong;
    private float ActiveLoopDelay => targetType == SpriteAnimatorTargetType.World &&
        CurrentWorldAnimation != null ? CurrentWorldAnimation.LoopDelay :
        (useLoopDelay ? loopDelay : 0f);

    private void Reset()
    {
        targetImage = GetComponent<Image>();
        targetRenderer = GetComponent<SpriteRenderer>();
        targetType = targetImage != null
            ? SpriteAnimatorTargetType.UI
            : SpriteAnimatorTargetType.World;
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Restart();
    }

    private void Update()
    {
        Sprite[] activeFrames = ActiveFrames;
        if (!isPlaying || activeFrames == null || activeFrames.Length == 0)
            return;

        float deltaTime = ignoreTimeScale
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        if (delayRemaining > 0f)
        {
            delayRemaining -= deltaTime;
            return;
        }

        elapsed += deltaTime;
        float frameDuration = GetCurrentFrameDuration();
        while (elapsed >= frameDuration && isPlaying)
        {
            elapsed -= frameDuration;
            AdvanceFrame();
            frameDuration = GetCurrentFrameDuration();
        }
    }

    public void Restart()
    {
        nextWorldAnimationName = null;
        pendingWorldAnimationName = null;
        pendingTransitionFrameDuration = -1f;
        pendingTransitionFps = -1f;
        pendingSwitchAtForwardBoundary = false;
        queuedForwardBoundaryAnimationName = null;
        playingPendingExitRange = false;
        SelectInitialWorldAnimation();
        currentFrame = GetInitialFrame();
        direction = 1;
        elapsed = 0f;
        delayRemaining = useStartDelay ? startDelay : 0f;
        isPlaying = ActiveFrames != null && ActiveFrames.Length > 0;
        ShowFrame();
    }

    public void Play() => isPlaying = ActiveFrames != null && ActiveFrames.Length > 0;

    public void Pause() => isPlaying = false;

    public void SetPrimaryIdleOnly(bool value) => primaryIdleOnly = value;

    // Kept so existing UI animation calls continue to work after migration.
    public void RestartAnimation() => Restart();

    public void PlayAnimation(string animationName)
    {
        nextWorldAnimationName = null;
        queuedForwardBoundaryAnimationName = null;
        RequestWorldAnimation(animationName);
    }

    public void PlayAnimation(string animationName, float maxFinishDuration)
    {
        nextWorldAnimationName = null;
        queuedForwardBoundaryAnimationName = null;
        RequestWorldAnimation(animationName, maxFinishDuration);
    }

    /// <summary>
    /// Queues a world animation and uses the supplied FPS while the current
    /// animation finishes. This preserves frame order while making a specific
    /// transition more responsive.
    /// </summary>
    public void PlayAnimation(string animationName, float maxFinishDuration,
        float transitionFps)
    {
        nextWorldAnimationName = null;
        queuedForwardBoundaryAnimationName = null;
        RequestWorldAnimation(animationName, maxFinishDuration, transitionFps);
    }

    public void PlayAnimationAtForwardBoundary(
        string animationName,
        float transitionFps = -1f)
    {
        nextWorldAnimationName = null;
        queuedForwardBoundaryAnimationName = null;
        RequestWorldAnimation(
            animationName,
            -1f,
            transitionFps,
            true
        );
    }

    public void PlayAnimationThenAtForwardBoundary(
        string animationName,
        string nextAnimationName,
        float transitionFps = -1f)
    {
        nextWorldAnimationName = nextAnimationName;
        queuedForwardBoundaryAnimationName = animationName;
        RequestWorldAnimation(
            animationName,
            -1f,
            transitionFps,
            true
        );
    }

    /// <summary>
    /// Plays a non-looping world animation, then automatically starts the
    /// supplied animation when the first one finishes.
    /// </summary>
    public void PlayAnimationThen(string animationName, string nextAnimationName)
    {
        nextWorldAnimationName = nextAnimationName;
        queuedForwardBoundaryAnimationName = null;
        RequestWorldAnimation(animationName);
    }

    private void RequestWorldAnimation(string animationName)
    {
        RequestWorldAnimation(animationName, -1f);
    }

    private void RequestWorldAnimation(string animationName, float maxFinishDuration)
    {
        RequestWorldAnimation(animationName, maxFinishDuration, -1f);
    }

    private void RequestWorldAnimation(string animationName, float maxFinishDuration,
        float transitionFps)
    {
        RequestWorldAnimation(
            animationName,
            maxFinishDuration,
            transitionFps,
            false
        );
    }

    private void RequestWorldAnimation(
        string animationName,
        float maxFinishDuration,
        float transitionFps,
        bool switchAtForwardBoundary)
    {
        if (targetType != SpriteAnimatorTargetType.World)
            return;

        if (isPlaying && CurrentWorldAnimation != null &&
            CurrentAnimationName == animationName)
        {
            pendingWorldAnimationName = null;
            pendingTransitionFrameDuration = -1f;
            pendingTransitionFps = -1f;
            pendingSwitchAtForwardBoundary = false;
            return;
        }

        if (!finishCurrentAnimationBeforeSwitching || !isPlaying ||
            CurrentWorldAnimation == null)
        {
            pendingWorldAnimationName = null;
            pendingTransitionFrameDuration = -1f;
            pendingTransitionFps = -1f;
            pendingSwitchAtForwardBoundary = false;
            PlayWorldAnimation(animationName);
            return;
        }

        pendingWorldAnimationName = animationName;
        delayRemaining = 0f;
        pendingTransitionFrameDuration = maxFinishDuration > 0f
            ? CalculateTransitionFrameDuration(maxFinishDuration)
            : -1f;
        pendingTransitionFps = transitionFps > 0f ? transitionFps : -1f;
        pendingSwitchAtForwardBoundary = switchAtForwardBoundary;
        if (pendingSwitchAtForwardBoundary && ActivePingPong && direction < 0)
            direction = 1;
    }

    private void PlayWorldAnimation(string animationName)
    {
        if (targetType != SpriteAnimatorTargetType.World)
            return;

        int animationIndex = FindWorldAnimationIndex(animationName);
        if (animationIndex < 0)
        {
            Debug.LogWarning($"[SpriteAnimator] Animation '{animationName}' was not found on '{name}'.", this);
            return;
        }

        currentWorldAnimationIndex = animationIndex;
        currentFrame = GetInitialFrame();
        direction = 1;
        playingPendingExitRange = false;
        elapsed = 0f;
        delayRemaining = 0f;
        isPlaying = ActiveFrames != null && ActiveFrames.Length > 0;
        ShowFrame();
    }

    private void AdvanceFrame()
    {
        if (TryAdvanceLinkedIdle())
            return;

        Sprite[] activeFrames = ActiveFrames;
        if (activeFrames.Length == 1)
        {
            if (TryPlayPendingWorldAnimation())
                return;

            if (!ActiveLoop) isPlaying = false;
            if (!isPlaying)
                PlayQueuedWorldAnimation();
            return;
        }

        if (TryAdvancePendingExitRange(activeFrames))
            return;

        int nextFrame = currentFrame + direction;
        bool usesLoopFrameRange = CurrentWorldAnimation != null &&
                                  CurrentWorldAnimation.UseLoopFrameRange;
        int loopEndFrame = usesLoopFrameRange
            ? Mathf.Clamp(CurrentWorldAnimation.LoopEndFrame >= 0
                ? CurrentWorldAnimation.LoopEndFrame
                : activeFrames.Length - 1, 0, activeFrames.Length - 1)
            : activeFrames.Length - 1;
        int loopStartFrame = usesLoopFrameRange
            ? Mathf.Clamp(CurrentWorldAnimation.LoopStartFrame, 0,
                loopEndFrame)
            : 0;
        // After Scare_Time's first pass reaches its final frame, never let its
        // reverse pass go below the configured loop-start frame.
        bool isRangeLowerBoundary = usesLoopFrameRange && direction < 0 &&
                                    nextFrame < loopStartFrame;
        if (nextFrame > loopEndFrame || nextFrame < 0 ||
            isRangeLowerBoundary)
        {
            if (direction > 0 && currentFrame >= loopEndFrame &&
                pendingSwitchAtForwardBoundary &&
                TryPlayPendingWorldAnimation())
            {
                return;
            }

            if (CurrentWorldAnimation != null &&
                CurrentWorldAnimation.UseLoopFrameRange &&
                direction > 0 && currentFrame >= loopEndFrame &&
                TryPlayPendingWorldAnimation())
            {
                return;
            }

            if (direction > 0 && currentFrame >= loopEndFrame &&
                !string.IsNullOrEmpty(nextWorldAnimationName) &&
                CurrentAnimationName == queuedForwardBoundaryAnimationName)
            {
                queuedForwardBoundaryAnimationName = null;
                PlayQueuedWorldAnimation();
                return;
            }

            bool completedCycle = (!ActivePingPong || direction < 0) &&
                                  !isRangeLowerBoundary;
            if (completedCycle && TryPlayPendingWorldAnimation())
                return;

            if (ActivePingPong)
            {
                if (isRangeLowerBoundary)
                {
                    direction = 1;
                    nextFrame = Mathf.Min(loopStartFrame + 1,
                        activeFrames.Length - 1);
                }
                else
                {
                    direction = -direction;
                    nextFrame = Mathf.Clamp(
                        currentFrame + direction,
                        0,
                        loopEndFrame);
                }
            }
            else if (ActiveLoop)
            {
                nextFrame = 0;
            }
            else
            {
                currentFrame = activeFrames.Length - 1;
                isPlaying = false;
                ShowFrame();
                PlayQueuedWorldAnimation();
                return;
            }

            delayRemaining = ActiveLoopDelay;
            if (completedCycle && TryPlayIdleVariation())
            {
                ShowFrame();
                return;
            }
        }

        currentFrame = nextFrame;
        ShowFrame();
    }

    private void ShowFrame()
    {
        Sprite[] activeFrames = ActiveFrames;
        if (activeFrames == null || activeFrames.Length == 0)
            return;

        Sprite sprite = activeFrames[Mathf.Clamp(currentFrame, 0, activeFrames.Length - 1)];
        if (targetType == SpriteAnimatorTargetType.UI)
        {
            if (targetImage != null) targetImage.sprite = sprite;
        }
        else if (targetRenderer != null)
        {
            targetRenderer.sprite = sprite;
        }

        PlayFrameFxIfNeeded();
    }

    private void SelectInitialWorldAnimation()
    {
        if (targetType != SpriteAnimatorTargetType.World)
            return;

        int index = FindWorldAnimationIndex(playOnEnableAnimation);
        currentWorldAnimationIndex = index >= 0 ? index : 0;
    }

    private void PlayQueuedWorldAnimation()
    {
        if (string.IsNullOrEmpty(nextWorldAnimationName))
            return;

        string nextAnimation = nextWorldAnimationName;
        nextWorldAnimationName = null;
        queuedForwardBoundaryAnimationName = null;
        PlayWorldAnimation(nextAnimation);
    }

    private bool TryPlayPendingWorldAnimation()
    {
        if (string.IsNullOrEmpty(pendingWorldAnimationName))
            return false;

        string pendingAnimation = pendingWorldAnimationName;
        pendingWorldAnimationName = null;
        pendingTransitionFrameDuration = -1f;
        pendingTransitionFps = -1f;
        pendingSwitchAtForwardBoundary = false;
        PlayWorldAnimation(pendingAnimation);
        return true;
    }

    private bool TryAdvancePendingExitRange(Sprite[] activeFrames)
    {
        if (CurrentWorldAnimation == null ||
            !CurrentWorldAnimation.UsePendingExitRange ||
            string.IsNullOrEmpty(pendingWorldAnimationName))
        {
            return false;
        }

        int exitStart = Mathf.Clamp(CurrentWorldAnimation.PendingExitStartFrame,
            0, activeFrames.Length - 1);
        int exitEnd = Mathf.Clamp(CurrentWorldAnimation.PendingExitEndFrame >= 0
            ? CurrentWorldAnimation.PendingExitEndFrame
            : activeFrames.Length - 1, exitStart, activeFrames.Length - 1);

        if (playingPendingExitRange)
        {
            if (currentFrame >= exitEnd)
                return TryPlayPendingWorldAnimation();

            direction = 1;
            currentFrame++;
            ShowFrame();
            return true;
        }

        if (currentFrame > exitStart)
        {
            direction = -1;
            currentFrame--;
            ShowFrame();
            return true;
        }

        if (currentFrame == exitStart)
        {
            playingPendingExitRange = true;
            direction = 1;
            currentFrame = Mathf.Min(exitStart + 1, exitEnd);
            ShowFrame();
            return true;
        }

        return false;
    }

    private float GetCurrentFrameDuration()
    {
        float normalFrameDuration = 1f / Mathf.Max(0.01f, ActiveFps);
        float transitionFrameDuration = pendingTransitionFrameDuration > 0f
            ? Mathf.Min(normalFrameDuration, pendingTransitionFrameDuration)
            : normalFrameDuration;

        // A requested transition FPS takes precedence for this one transition.
        // It is never allowed to slow the clip below its configured FPS.
        if (pendingTransitionFps > 0f)
            return 1f / Mathf.Max(ActiveFps, pendingTransitionFps);

        return transitionFrameDuration;
    }

    private float CalculateTransitionFrameDuration(float maxFinishDuration)
    {
        Sprite[] activeFrames = ActiveFrames;
        if (activeFrames == null || activeFrames.Length == 0)
            return -1f;

        int remainingSteps;
        if (activeFrames.Length == 1)
        {
            remainingSteps = 1;
        }
        else if (!ActivePingPong)
        {
            remainingSteps = activeFrames.Length - currentFrame;
        }
        else if (direction > 0)
        {
            remainingSteps = activeFrames.Length - currentFrame +
                             activeFrames.Length - 1;
        }
        else
        {
            remainingSteps = currentFrame + 1;
        }

        return Mathf.Max(0.01f, maxFinishDuration) /
               Mathf.Max(1, remainingSteps);
    }

    private bool TryAdvanceLinkedIdle()
    {
        if (!useLinkedIdleChain)
            return false;

        int idleIndex = GetLinkedIdleIndex(CurrentAnimationName);
        Sprite[] activeFrames = ActiveFrames;
        if (idleIndex < 0 || activeFrames == null || activeFrames.Length < 2)
            return false;

        if (direction > 0 && currentFrame >= activeFrames.Length - 1)
        {
            if (idleIndex == 0)
            {
                if (primaryIdleOnly || !string.IsNullOrEmpty(pendingWorldAnimationName))
                {
                    direction = -1;
                    currentFrame--;
                }
                else if (linkedIdleDestination > 0)
                {
                    SwitchLinkedIdle(1, 1, 1);
                }
                else
                {
                    direction = -1;
                    currentFrame--;
                }
            }
            else if (idleIndex == 1 && linkedIdleDestination == 2 && !primaryIdleOnly)
            {
                SwitchLinkedIdle(2, 0, 1);
            }
            else
            {
                direction = -1;
                currentFrame--;
            }
        }
        else if (direction < 0 && currentFrame <= 0)
        {
            if (idleIndex == 2)
            {
                SwitchLinkedIdle(1, ActiveFrames.Length - 1, -1);
            }
            else if (idleIndex == 1)
            {
                int idle1Index = FindWorldAnimationIndex(linkedIdle1);
                WorldSpriteAnimation idle1 = idle1Index >= 0 ? animations[idle1Index] : null;
                if (idle1 == null || !idle1.HasFrames)
                    return false;
                currentWorldAnimationIndex = idle1Index;
                currentFrame = Mathf.Max(0, idle1.Frames.Length - 2);
                direction = -1;
            }
            else if (TryPlayPendingWorldAnimation())
            {
                return true;
            }
            else
            {
                linkedIdleDestination = SelectLinkedIdleDestination();
                direction = 1;
                currentFrame = 1;
            }
        }
        else
        {
            currentFrame += direction;
        }

        ShowFrame();
        return true;
    }

    private void SwitchLinkedIdle(int linkedIndex, int frame, int newDirection)
    {
        int animationIndex = FindWorldAnimationIndex(GetLinkedIdleName(linkedIndex));
        if (animationIndex < 0 || animations[animationIndex] == null ||
            !animations[animationIndex].HasFrames)
            return;

        currentWorldAnimationIndex = animationIndex;
        currentFrame = Mathf.Clamp(frame, 0, animations[animationIndex].Frames.Length - 1);
        direction = newDirection;
    }

    private int GetLinkedIdleIndex(string animationName)
    {
        if (animationName == linkedIdle1) return 0;
        if (animationName == linkedIdle2) return 1;
        if (animationName == linkedIdle3) return 2;
        return -1;
    }

    private string GetLinkedIdleName(int idleIndex)
    {
        return idleIndex == 0 ? linkedIdle1 : idleIndex == 1 ? linkedIdle2 : linkedIdle3;
    }

    private int SelectLinkedIdleDestination()
    {
        if (primaryIdleOnly)
            return 0;

        float total = Mathf.Max(.001f, idle1Chance + idle2Chance + idle3Chance);
        float roll = Random.value * total;
        if (roll < idle1Chance) return 0;
        if (roll < idle1Chance + idle2Chance) return 1;
        return 2;
    }

    private int FindWorldAnimationIndex(string animationName)
    {
        if (animations == null)
            return -1;

        for (int i = 0; i < animations.Count; i++)
        {
            if (animations[i] != null && animations[i].Name == animationName)
                return i;
        }

        return -1;
    }

    private int GetInitialFrame()
    {
        if (targetType != SpriteAnimatorTargetType.World ||
            CurrentWorldAnimation == null || !CurrentWorldAnimation.HasFrames)
            return 0;

        if (CurrentWorldAnimation.UseLoopFrameRange)
            return 0;

        if (!CurrentWorldAnimation.SkipBoundaryFrames)
            return CurrentWorldAnimation.PingPong &&
                   CurrentWorldAnimation.Frames.Length > 1 ? 1 : 0;

        return CurrentWorldAnimation.Loop &&
               CurrentWorldAnimation.Frames.Length > 2 ? 1 : 0;
    }

    private bool TryPlayIdleVariation()
    {
        if (targetType != SpriteAnimatorTargetType.World || !enableIdleVariation)
            return false;

        if (!string.IsNullOrEmpty(pendingWorldAnimationName))
            return false;

        bool returnToPrimary = CurrentAnimationName == alternateIdleAnimation;
        if (!returnToPrimary && (CurrentAnimationName != primaryIdleAnimation ||
                                 Random.value > idleVariationChance))
            return false;

        int nextIndex = FindWorldAnimationIndex(returnToPrimary
            ? primaryIdleAnimation
            : alternateIdleAnimation);
        if (nextIndex < 0)
            return false;

        currentWorldAnimationIndex = nextIndex;
        currentFrame = 0;
        direction = 1;
        elapsed = 0f;
        return true;
    }

    private Sprite[] GetOrCreateGridFrames()
    {
        if (runtimeGridFrames != null && runtimeGridFrames.Length > 0)
            return runtimeGridFrames;

        if (spriteSheet == null || gridColumns < 1 || gridRows < 1)
            return System.Array.Empty<Sprite>();

        int frameWidth = spriteSheet.width / gridColumns;
        int frameHeight = spriteSheet.height / gridRows;
        if (frameWidth < 1 || frameHeight < 1)
            return System.Array.Empty<Sprite>();

        runtimeGridFrames = new Sprite[gridColumns * gridRows];
        int index = 0;
        for (int row = gridRows - 1; row >= 0; row--)
        {
            for (int column = 0; column < gridColumns; column++)
            {
                runtimeGridFrames[index++] = Sprite.Create(
                    spriteSheet,
                    new Rect(column * frameWidth, row * frameHeight, frameWidth, frameHeight),
                    new Vector2(.5f, .5f),
                    Mathf.Max(0.01f, gridPixelsPerUnit));
            }
        }

        return runtimeGridFrames;
    }

    private void PlayFrameFxIfNeeded()
    {
        if (targetType != SpriteAnimatorTargetType.UI || !useFrameFx ||
            frameFx == null || currentFrame + 1 != playFxAtFrame)
            return;

        frameFx.SetActive(false);
        frameFx.SetActive(true);

        foreach (ParticleSystem particle in frameFx.GetComponentsInChildren<ParticleSystem>(true))
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
        }
    }

#if UNITY_EDITOR
    public int EditorPreviewAnimationCount =>
        targetType == SpriteAnimatorTargetType.World
            ? animations?.Count ?? 0
            : 1;

    public string EditorPreviewAnimationName(int animationIndex)
    {
        if (targetType != SpriteAnimatorTargetType.World)
            return "UI Animation";

        if (animations == null || animationIndex < 0 ||
            animationIndex >= animations.Count ||
            animations[animationIndex] == null)
        {
            return $"Animation {animationIndex + 1}";
        }

        string animationName = animations[animationIndex].Name;
        return string.IsNullOrWhiteSpace(animationName)
            ? $"Animation {animationIndex + 1}"
            : animationName;
    }

    public int EditorPreviewFrameCount(int animationIndex)
    {
        return GetEditorPreviewFrames(animationIndex).Length;
    }

    public float EditorPreviewFps(int animationIndex)
    {
        WorldSpriteAnimation animation = GetEditorPreviewAnimation(animationIndex);
        return Mathf.Max(
            0.01f,
            animation != null ? animation.Fps : fps
        );
    }

    public bool EditorPreviewLoops(int animationIndex)
    {
        WorldSpriteAnimation animation = GetEditorPreviewAnimation(animationIndex);
        return animation != null ? animation.Loop : loop;
    }

    public bool EditorPreviewPingPongs(int animationIndex)
    {
        WorldSpriteAnimation animation = GetEditorPreviewAnimation(animationIndex);
        return animation != null ? animation.PingPong : pingPong;
    }

    public float EditorPreviewLoopDelay(int animationIndex)
    {
        WorldSpriteAnimation animation = GetEditorPreviewAnimation(animationIndex);
        return animation != null
            ? Mathf.Max(0f, animation.LoopDelay)
            : useLoopDelay ? Mathf.Max(0f, loopDelay) : 0f;
    }

    public bool EditorPreviewUsesLoopRange(int animationIndex)
    {
        WorldSpriteAnimation animation = GetEditorPreviewAnimation(animationIndex);
        return animation != null && animation.UseLoopFrameRange;
    }

    public int EditorPreviewLoopStart(int animationIndex)
    {
        WorldSpriteAnimation animation = GetEditorPreviewAnimation(animationIndex);
        int frameCount = EditorPreviewFrameCount(animationIndex);
        return animation != null && frameCount > 0
            ? Mathf.Clamp(animation.LoopStartFrame, 0, frameCount - 1)
            : 0;
    }

    public int EditorPreviewLoopEnd(int animationIndex)
    {
        WorldSpriteAnimation animation = GetEditorPreviewAnimation(animationIndex);
        int frameCount = EditorPreviewFrameCount(animationIndex);
        if (frameCount <= 0)
            return 0;

        if (animation == null || animation.LoopEndFrame < 0)
            return frameCount - 1;

        return Mathf.Clamp(
            animation.LoopEndFrame,
            EditorPreviewLoopStart(animationIndex),
            frameCount - 1
        );
    }

    public Sprite EditorPreviewDisplayedSprite
    {
        get
        {
            return targetType == SpriteAnimatorTargetType.UI
                ? targetImage != null ? targetImage.sprite : null
                : targetRenderer != null ? targetRenderer.sprite : null;
        }
    }

    public void EditorPreviewSetFrame(int animationIndex, int frameIndex)
    {
        Sprite[] previewFrames = GetEditorPreviewFrames(animationIndex);
        if (previewFrames.Length == 0)
            return;

        EditorPreviewSetDisplayedSprite(
            previewFrames[Mathf.Clamp(frameIndex, 0, previewFrames.Length - 1)]
        );
    }

    public void EditorPreviewSetDisplayedSprite(Sprite sprite)
    {
        if (targetType == SpriteAnimatorTargetType.UI)
        {
            if (targetImage != null)
                targetImage.sprite = sprite;
        }
        else if (targetRenderer != null)
        {
            targetRenderer.sprite = sprite;
        }
    }

    private WorldSpriteAnimation GetEditorPreviewAnimation(int animationIndex)
    {
        if (targetType != SpriteAnimatorTargetType.World || animations == null ||
            animationIndex < 0 || animationIndex >= animations.Count)
        {
            return null;
        }

        return animations[animationIndex];
    }

    private Sprite[] GetEditorPreviewFrames(int animationIndex)
    {
        WorldSpriteAnimation animation = GetEditorPreviewAnimation(animationIndex);
        if (animation != null)
            return animation.Frames ?? System.Array.Empty<Sprite>();

        if (targetType == SpriteAnimatorTargetType.UI &&
            frameSource == SpriteAnimatorFrameSource.SpriteSheet &&
            generateGridFrames)
        {
            return GetOrCreateGridFrames();
        }

        return frames ?? System.Array.Empty<Sprite>();
    }

    private void OnValidate()
    {
        fps = Mathf.Max(0.01f, fps);
        startDelay = Mathf.Max(0f, startDelay);
        loopDelay = Mathf.Max(0f, loopDelay);
        gridColumns = Mathf.Max(1, gridColumns);
        gridRows = Mathf.Max(1, gridRows);
        gridPixelsPerUnit = Mathf.Max(0.01f, gridPixelsPerUnit);
        runtimeGridFrames = null;
        LoadWorldSpritesFromSheets();
        if (targetType == SpriteAnimatorTargetType.UI &&
            frameSource == SpriteAnimatorFrameSource.SpriteSheet)
        {
            if (generateGridFrames)
            {
                frames = System.Array.Empty<Sprite>();
            }
            else
            {
                LoadSpritesFromSheet();
            }
        }

        if (!Application.isPlaying)
        {
            SelectInitialWorldAnimation();
            ShowFrame();
        }
    }

    private void LoadSpritesFromSheet()
    {
        if (spriteSheet == null)
        {
            frames = System.Array.Empty<Sprite>();
            return;
        }

        string path = AssetDatabase.GetAssetPath(spriteSheet);
        if (string.IsNullOrEmpty(path))
            return;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        List<Sprite> sprites = new List<Sprite>();
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite)
                sprites.Add(sprite);
        }

        sprites.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
        frames = sprites.ToArray();
    }

    private void LoadWorldSpritesFromSheets()
    {
        if (animations == null)
            return;

        foreach (WorldSpriteAnimation animation in animations)
        {
            if (animation == null || animation.SpriteSheet == null)
                continue;

            string path = AssetDatabase.GetAssetPath(animation.SpriteSheet);
            if (string.IsNullOrEmpty(path))
                continue;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            List<Sprite> sprites = new List<Sprite>();
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is Sprite sprite)
                    sprites.Add(sprite);
            }

            sprites.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            int start = Mathf.Clamp(animation.SpriteSheetStartFrame, 0, sprites.Count);
            int end = animation.SpriteSheetEndFrame < 0
                ? sprites.Count - 1
                : Mathf.Clamp(animation.SpriteSheetEndFrame, start - 1, sprites.Count - 1);
            int count = Mathf.Max(0, end - start + 1);
            animation.SetFrames(sprites.GetRange(start, count).ToArray());
        }
    }
#endif
}

