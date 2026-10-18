using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class UISpriteAnimator : MonoBehaviour
{
    [Header("Reference")]
    public Image targetImage;

    [Tooltip("Kéo texture đã Sprite Mode = Multiple và Slice sẵn vào đây")]
    public Texture2D spriteSheet;

    [SerializeField] private Sprite[] frames;

    public float AnimationDuration => frames == null || frames.Length == 0
        ? 0f
        : frames.Length / Mathf.Max(fps, .01f);

    [Header("Animation")]
    public float fps = 12f;
    public bool loop = true;
    public bool pingPong = false;

    [Header("Delay")]
    public bool useStartDelay = false;
    public float startDelay = 1f;

    public bool useLoopDelay = false;
    public float loopDelay = 1f;

    [Header("Frame FX")]
    [SerializeField] private bool useFrameFx = false;
    [SerializeField] private GameObject frameFx;
    [Tooltip("Frame được tính từ 1. Ví dụ nhập 9 để phát FX khi hiển thị frame thứ 9.")]
    [SerializeField, Min(1)] private int playFxAtFrame = 1;

    private int currentFrame = 0;
    private float timer;
    private int direction = 1;

    private bool isPlaying = false;
    private bool isWaitingLoop = false;

    private void Start()
    {
        RestartAnimation();
    }

    /// <summary>
    /// Resets every piece of playback state and starts again at the first
    /// sprite. Call this when a reusable popup is shown again.
    /// </summary>
    public void RestartAnimation()
    {
        CancelInvoke(nameof(StartAnimation));
        CancelInvoke(nameof(ResumeAnimation));

        currentFrame = 0;
        timer = 0f;
        direction = 1;
        isPlaying = false;
        isWaitingLoop = false;

        if (targetImage != null && frames != null && frames.Length > 0)
            targetImage.sprite = frames[0];

        if (useFrameFx && frameFx != null)
            frameFx.SetActive(false);

        if (useStartDelay && startDelay > 0f)
            Invoke(nameof(StartAnimation), startDelay);
        else
            StartAnimation();
    }

    private void StartAnimation()
    {
        isPlaying = true;
        PlayFrameFxIfNeeded();
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        if (frames == null || frames.Length == 0 || targetImage == null)
            return;

        timer += Time.deltaTime;

        float frameTime = 1f / Mathf.Max(fps, 0.01f);

        // Catch up after a slow frame so low-end devices preserve the
        // configured animation timing instead of visibly slowing it down.
        while (timer >= frameTime && isPlaying)
        {
            timer -= frameTime;

            currentFrame += direction;

            if (pingPong)
            {
                if (currentFrame >= frames.Length)
                {
                    currentFrame = Mathf.Max(frames.Length - 2, 0);
                    direction = -1;

                    HandleLoopDelay();
                }
                else if (currentFrame < 0)
                {
                    currentFrame = Mathf.Min(1, frames.Length - 1);
                    direction = 1;

                    HandleLoopDelay();
                }
            }
            else
            {
                if (currentFrame >= frames.Length)
                {
                    if (loop)
                    {
                        currentFrame = 0;

                        HandleLoopDelay();
                    }
                    else
                    {
                        currentFrame = frames.Length - 1;
                        isPlaying = false;
                    }
                }
            }

            targetImage.sprite = frames[currentFrame];
            PlayFrameFxIfNeeded();
        }
    }

    private void PlayFrameFxIfNeeded()
    {
        if (!useFrameFx || frameFx == null || currentFrame + 1 != playFxAtFrame)
            return;

        frameFx.SetActive(false);
        frameFx.SetActive(true);

        ParticleSystem[] particleSystems =
            frameFx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
            particleSystems[i].Play(true);
        }

        Animator[] animators = frameFx.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].enabled = true;
            animators[i].Play(0, 0, 0f);
        }

        Animation[] animations = frameFx.GetComponentsInChildren<Animation>(true);
        for (int i = 0; i < animations.Length; i++)
        {
            animations[i].Stop();
            animations[i].Play();
        }
    }

    private void HandleLoopDelay()
    {
        if (useLoopDelay && !isWaitingLoop)
        {
            isPlaying = false;
            isWaitingLoop = true;

            Invoke(nameof(ResumeAnimation), loopDelay);
        }
    }

    private void ResumeAnimation()
    {
        isPlaying = true;
        isWaitingLoop = false;
    }

#if UNITY_EDITOR

    public int EditorPreviewFrameCount => frames?.Length ?? 0;

    public void EditorPreviewSetFrame(int frameIndex)
    {
        if (targetImage == null || frames == null || frames.Length == 0)
            return;

        int safeFrameIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
        targetImage.sprite = frames[safeFrameIndex];
    }

    private void OnValidate()
    {
        fps = Mathf.Max(fps, .01f);
        playFxAtFrame = Mathf.Max(1, playFxAtFrame);

        if (spriteSheet == null)
        {
            frames = new Sprite[0];
            return;
        }

        LoadSpritesFromSheet();
    }

    private void LoadSpritesFromSheet()
    {
        string path = AssetDatabase.GetAssetPath(spriteSheet);

        if (string.IsNullOrEmpty(path))
            return;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

        List<Sprite> spriteList = new List<Sprite>();

        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                spriteList.Add(sprite);
            }
        }

        // Sort theo tên để frame chạy đúng thứ tự
        spriteList.Sort((a, b) =>
            EditorUtility.NaturalCompare(
                a.name,
                b.name
            )
        );

        frames = spriteList.ToArray();

        if (frames.Length > 0 && targetImage != null)
        {
            targetImage.sprite = frames[0];
        }

        EditorUtility.SetDirty(this);
    }

#endif
}
