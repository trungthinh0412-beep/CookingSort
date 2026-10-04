using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class WorldSpriteAnimation
{
    [SerializeField] private string animationName = "New Animation";

    [Tooltip("Keo cac Sprite vao theo dung thu tu frame 0, 1, 2, ...")]
    [SerializeField] private Sprite[] frames = new Sprite[0];

    [SerializeField, Min(0.01f)] private float fps = 12f;
    [SerializeField] private bool loop = true;

    [Tooltip("Bat de chay tu dau den cuoi roi chay nguoc ve dau.")]
    [SerializeField] private bool pingPong;

    [Tooltip("Only enable for clips authored with redundant first and last frames.")]
    [SerializeField] private bool skipBoundaryFrames = true;

    [Tooltip("Thoi gian doi sau khi hoan thanh mot vong truoc khi chay lai.")]
    [SerializeField, Min(0f)] private float loopDelay;

    public string Name => animationName;
    public Sprite[] Frames => frames;
    public float Fps => fps;
    public bool Loop => loop;
    public bool PingPong => pingPong;
    public bool SkipBoundaryFrames => skipBoundaryFrames;
    public float LoopDelay => loopDelay;
    public bool HasFrames => frames != null && frames.Length > 0;

    public WorldSpriteAnimation(string name)
    {
        animationName = name;
    }

    public WorldSpriteAnimation(
        string name,
        Sprite[] animationFrames,
        float animationFps,
        bool shouldLoop,
        bool shouldPingPong
    )
    {
        animationName = name;
        frames = animationFrames ?? new Sprite[0];
        fps = animationFps;
        loop = shouldLoop;
        pingPong = shouldPingPong;
    }

    public void Validate(int index)
    {
        if (string.IsNullOrWhiteSpace(animationName))
        {
            animationName = $"Animation {index + 1}";
        }

        fps = Mathf.Max(0.01f, fps);
        loopDelay = Mathf.Max(0f, loopDelay);

        if (frames == null)
        {
            frames = new Sprite[0];
        }
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class WorldSpriteAnimator : MonoBehaviour
{
    private const int CurrentDataVersion = 1;

    [Header("Reference")]
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Animation Library")]
    [SerializeField]
    private List<WorldSpriteAnimation> animations =
        new List<WorldSpriteAnimation>();

    [Header("Playback")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private string playOnEnableAnimation = "Default";

    [Tooltip("Bat neu animation van phai chay khi Time.timeScale = 0.")]
    [SerializeField] private bool ignoreTimeScale;

    [Header("Idle Variation")]
    [SerializeField] private bool enableIdleVariation;
    [SerializeField] private string primaryIdleAnimation = "Idle_1";
    [SerializeField] private string alternateIdleAnimation = "Idle_2";
    [Tooltip("Chance to play the alternate idle after a complete primary idle cycle.")]
    [SerializeField, Range(0f, 1f)] private float idleVariationChance = 0.2f;

    // Du lieu cua phien ban cu. Tu dong chuyen vao Animation Library mot lan.
    [FormerlySerializedAs("frames")]
    [SerializeField, HideInInspector] private Sprite[] legacyFrames = new Sprite[0];

    [FormerlySerializedAs("fps")]
    [SerializeField, HideInInspector] private float legacyFps = 12f;

    [FormerlySerializedAs("loop")]
    [SerializeField, HideInInspector] private bool legacyLoop = true;

    [FormerlySerializedAs("pingPong")]
    [SerializeField, HideInInspector] private bool legacyPingPong;

    [SerializeField, HideInInspector] private int dataVersion;

    private int _currentAnimationIndex = -1;
    private int _currentFrame;
    private int _direction = 1;
    private float _elapsedTime;
    private bool _isPlaying;
    private bool _isWaitingForLoop;
    private float _loopDelayRemaining;

    public string CurrentAnimationName => CurrentAnimation?.Name ?? string.Empty;
    public int CurrentFrame => _currentFrame;
    public bool IsPlaying => _isPlaying;
    public bool IsWaitingForLoop => _isWaitingForLoop;
    public int AnimationCount => animations?.Count ?? 0;

    private WorldSpriteAnimation CurrentAnimation
    {
        get
        {
            if (animations == null ||
                _currentAnimationIndex < 0 ||
                _currentAnimationIndex >= animations.Count)
            {
                return null;
            }

            return animations[_currentAnimationIndex];
        }
    }

    private void Reset()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
        animations = new List<WorldSpriteAnimation>
        {
            new WorldSpriteAnimation("Default")
        };
        playOnEnableAnimation = "Default";
        dataVersion = CurrentDataVersion;
    }

    private void Awake()
    {
        MigrateLegacyData();
        EnsureAnimationLibrary();
        EnsureRenderer();
        SelectInitialAnimation();
        ShowCurrentFrame();
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            PlayAnimation(playOnEnableAnimation);
        }
    }

    private void OnDisable()
    {
        _isPlaying = false;
    }

    private void Update()
    {
        WorldSpriteAnimation animation = CurrentAnimation;

        if (!_isPlaying || targetRenderer == null ||
            animation == null || !animation.HasFrames)
        {
            return;
        }

        float deltaTime = ignoreTimeScale
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        if (_isWaitingForLoop)
        {
            _loopDelayRemaining -= deltaTime;

            if (_loopDelayRemaining <= 0f)
            {
                CompleteLoopDelay();
            }

            return;
        }

        _elapsedTime += deltaTime;

        float frameDuration = 1f / Mathf.Max(0.01f, animation.Fps);

        // Dung while de animation khong bi cham khi game tut FPS.
        while (_elapsedTime >= frameDuration &&
               _isPlaying && !_isWaitingForLoop)
        {
            _elapsedTime -= frameDuration;
            AdvanceFrame(animation);
            animation = CurrentAnimation;
            if (animation == null || !animation.HasFrames)
                break;
            frameDuration = 1f / Mathf.Max(0.01f, animation.Fps);
        }
    }

    /// <summary>
    /// Play an animation from its first frame by name.
    /// Example: animator.PlayAnimation("Idle");
    /// </summary>
    public void PlayAnimation(string animationName)
    {
        int animationIndex = FindAnimationIndex(animationName);

        if (animationIndex < 0)
        {
            Debug.LogWarning(
                $"[WorldSpriteAnimator] Animation '{animationName}' was not found on '{name}'.",
                this
            );
            return;
        }

        PlayAnimation(animationIndex);
    }

    /// <summary>
    /// Play an animation from its first frame by list index.
    /// </summary>
    public void PlayAnimation(int animationIndex)
    {
        if (animations == null ||
            animationIndex < 0 ||
            animationIndex >= animations.Count)
        {
            Debug.LogWarning(
                $"[WorldSpriteAnimator] Animation index {animationIndex} is invalid on '{name}'.",
                this
            );
            return;
        }

        _currentAnimationIndex = animationIndex;
        _currentFrame = GetLoopStartFrame(animations[animationIndex]);
        if (animations[animationIndex] != null && !animations[animationIndex].SkipBoundaryFrames)
            _currentFrame = 0;
        _direction = 1;
        _elapsedTime = 0f;
        _isWaitingForLoop = false;
        _loopDelayRemaining = 0f;
        _isPlaying = animations[animationIndex] != null &&
                     animations[animationIndex].HasFrames;
        ShowCurrentFrame();
    }

    /// <summary>
    /// Resume the currently selected animation.
    /// </summary>
    public void Play()
    {
        if (CurrentAnimation == null)
        {
            SelectInitialAnimation();
        }

        if (targetRenderer == null)
        {
            EnsureRenderer();
        }

        if (targetRenderer == null ||
            CurrentAnimation == null ||
            !CurrentAnimation.HasFrames)
        {
            return;
        }

        _isPlaying = true;
        ShowCurrentFrame();
    }

    public void Pause()
    {
        _isPlaying = false;
    }

    public void Restart()
    {
        if (CurrentAnimation == null)
        {
            SelectInitialAnimation();
        }

        _currentFrame = GetLoopStartFrame(CurrentAnimation);
        _direction = 1;
        _elapsedTime = 0f;
        _isWaitingForLoop = false;
        _loopDelayRemaining = 0f;
        ShowCurrentFrame();
        Play();
    }

    public void Stop()
    {
        _isPlaying = false;
        _currentFrame = 0;
        _direction = 1;
        _elapsedTime = 0f;
        _isWaitingForLoop = false;
        _loopDelayRemaining = 0f;
        ShowCurrentFrame();
    }

    public void SetFrame(int frameIndex)
    {
        WorldSpriteAnimation animation = CurrentAnimation;

        if (animation == null || !animation.HasFrames)
        {
            return;
        }

        _currentFrame = Mathf.Clamp(
            frameIndex,
            0,
            animation.Frames.Length - 1
        );
        _elapsedTime = 0f;
        _isWaitingForLoop = false;
        _loopDelayRemaining = 0f;
        ShowCurrentFrame();
    }

    public bool HasAnimation(string animationName)
    {
        return FindAnimationIndex(animationName) >= 0;
    }

    public string GetAnimationName(int animationIndex)
    {
        if (animations == null ||
            animationIndex < 0 ||
            animationIndex >= animations.Count ||
            animations[animationIndex] == null)
        {
            return string.Empty;
        }

        return animations[animationIndex].Name;
    }

    private int FindAnimationIndex(string animationName)
    {
        if (animations == null || string.IsNullOrEmpty(animationName))
        {
            return -1;
        }

        for (int i = 0; i < animations.Count; i++)
        {
            WorldSpriteAnimation animation = animations[i];

            if (animation != null &&
                string.Equals(
                    animation.Name,
                    animationName,
                    StringComparison.Ordinal
                ))
            {
                return i;
            }
        }

        return -1;
    }

    private void SelectInitialAnimation()
    {
        int animationIndex = FindAnimationIndex(playOnEnableAnimation);

        if (animationIndex < 0 && animations != null && animations.Count > 0)
        {
            animationIndex = 0;
        }

        _currentAnimationIndex = animationIndex;
        _currentFrame = 0;
        _direction = 1;
        _elapsedTime = 0f;
        _isWaitingForLoop = false;
        _loopDelayRemaining = 0f;
    }

    private void AdvanceFrame(WorldSpriteAnimation animation)
    {
        Sprite[] animationFrames = animation.Frames;

        if (animationFrames.Length == 1)
        {
            if (TryPlayIdleVariation(animation))
                return;

            if (!animation.Loop)
            {
                _isPlaying = false;
            }

            return;
        }

        int nextFrame = _currentFrame + _direction;

        // Loop animations commonly contain duplicated boundary frames.
        // Show frame 0 on the initial play, but skip frame 0 and the final
        // frame at loop boundaries so the animation does not visibly pause or
        // jump when it wraps around.
        bool skipLoopBoundaryFrames =
            animation.Loop && animation.SkipBoundaryFrames && animationFrames.Length > 2;

        if (skipLoopBoundaryFrames && !animation.PingPong &&
            nextFrame == animationFrames.Length - 1)
        {
            BeginLoopRestart(animation);
            return;
        }

        if (skipLoopBoundaryFrames && animation.PingPong &&
            _direction > 0 && nextFrame == animationFrames.Length - 1)
        {
            _direction = -1;
            _currentFrame = animationFrames.Length - 2;
            ShowCurrentFrame();
            return;
        }

        if (skipLoopBoundaryFrames && animation.PingPong &&
            _direction < 0 && nextFrame <= 0)
        {
            BeginLoopRestart(animation);
            return;
        }

        if (nextFrame >= 0 && nextFrame < animationFrames.Length)
        {
            _currentFrame = nextFrame;
            ShowCurrentFrame();
            return;
        }

        if (!animation.PingPong)
        {
            if (animation.Loop)
            {
                BeginLoopRestart(animation);
            }
            else
            {
                if (TryPlayIdleVariation(animation))
                    return;

                _currentFrame = animationFrames.Length - 1;
                _isPlaying = false;
                ShowCurrentFrame();
            }

            return;
        }

        if (_direction > 0)
        {
            _direction = -1;
            _currentFrame = animationFrames.Length - 2;
            ShowCurrentFrame();
            return;
        }

        if (animation.Loop)
        {
            BeginLoopRestart(animation);
            return;
        }
        else
        {
            if (TryPlayIdleVariation(animation))
                return;

            _currentFrame = 0;
            _isPlaying = false;
        }

        ShowCurrentFrame();
    }

    private void BeginLoopRestart(WorldSpriteAnimation animation)
    {
        _direction = 1;

        if (animation.LoopDelay > 0f)
        {
            _elapsedTime = 0f;
            _isWaitingForLoop = true;
            _loopDelayRemaining = animation.LoopDelay;
            return;
        }

        // Ping Pong da hien frame 0 khi quay nguoc, nen tiep tuc tu frame 1
        // de khong lap frame dau hai lan khi Loop Delay = 0.
        if (TryPlayIdleVariation(animation))
            return;

        _currentFrame = GetLoopStartFrame(animation);
        ShowCurrentFrame();
    }

    private void CompleteLoopDelay()
    {
        WorldSpriteAnimation animation = CurrentAnimation;
        _isWaitingForLoop = false;
        _loopDelayRemaining = 0f;
        _elapsedTime = 0f;
        _direction = 1;
        if (TryPlayIdleVariation(animation))
            return;

        _currentFrame = GetLoopStartFrame(animation);
        ShowCurrentFrame();
    }

    private bool TryPlayIdleVariation(WorldSpriteAnimation completedAnimation)
    {
        if (!enableIdleVariation || completedAnimation == null ||
            primaryIdleAnimation == alternateIdleAnimation)
            return false;

        bool returningToPrimary = completedAnimation.Name == alternateIdleAnimation;
        if (!returningToPrimary && completedAnimation.Name != primaryIdleAnimation)
            return false;

        int nextIndex = FindAnimationIndex(returningToPrimary ? primaryIdleAnimation : alternateIdleAnimation);
        if (nextIndex < 0 || !animations[nextIndex].HasFrames)
            return false;

        if (!returningToPrimary && UnityEngine.Random.value >= idleVariationChance)
            return false;

        float remainingTime = _elapsedTime;
        PlayAnimation(nextIndex);
        _elapsedTime = remainingTime;
        // A different clip must start at its first frame, even when marked Loop.
        _currentFrame = 0;
        ShowCurrentFrame();
        return true;
    }

    private static int GetLoopStartFrame(WorldSpriteAnimation animation)
    {
        if (animation != null && !animation.SkipBoundaryFrames)
            return animation.PingPong && animation.Frames != null && animation.Frames.Length > 1 ? 1 : 0;

        return animation != null && animation.Loop &&
               animation.Frames != null && animation.Frames.Length > 2
            ? 1
            : 0;
    }

    private void EnsureRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void EnsureAnimationLibrary()
    {
        if (animations == null)
        {
            animations = new List<WorldSpriteAnimation>();
        }

        if (animations.Count == 0)
        {
            animations.Add(new WorldSpriteAnimation("Default"));
        }

        for (int i = 0; i < animations.Count; i++)
        {
            if (animations[i] == null)
            {
                animations[i] = new WorldSpriteAnimation($"Animation {i + 1}");
            }

            animations[i].Validate(i);
        }

        if (FindAnimationIndex(playOnEnableAnimation) < 0)
        {
            playOnEnableAnimation = animations[0].Name;
        }
    }

    private void MigrateLegacyData()
    {
        if (dataVersion >= CurrentDataVersion)
        {
            return;
        }

        if (animations == null)
        {
            animations = new List<WorldSpriteAnimation>();
        }

        if (legacyFrames != null && legacyFrames.Length > 0)
        {
            animations.Insert(
                0,
                new WorldSpriteAnimation(
                    "Default",
                    legacyFrames,
                    legacyFps,
                    legacyLoop,
                    legacyPingPong
                )
            );
            playOnEnableAnimation = "Default";
        }

        legacyFrames = new Sprite[0];
        dataVersion = CurrentDataVersion;
    }

    private void ShowCurrentFrame()
    {
        WorldSpriteAnimation animation = CurrentAnimation;

        if (targetRenderer == null || animation == null || !animation.HasFrames)
        {
            return;
        }

        _currentFrame = Mathf.Clamp(
            _currentFrame,
            0,
            animation.Frames.Length - 1
        );
        targetRenderer.sprite = animation.Frames[_currentFrame];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MigrateLegacyData();
        EnsureAnimationLibrary();
        EnsureRenderer();

        if (!Application.isPlaying)
        {
            SelectInitialAnimation();
        }
    }
#endif
}
