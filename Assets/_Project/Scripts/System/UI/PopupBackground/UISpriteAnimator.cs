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

    [Header("Animation")]
    public float fps = 12f;
    public bool loop = true;
    public bool pingPong = false;

    [Header("Delay")]
    public bool useStartDelay = false;
    public float startDelay = 1f;

    public bool useLoopDelay = false;
    public float loopDelay = 1f;

    private int currentFrame = 0;
    private float timer;
    private int direction = 1;

    private bool isPlaying = false;
    private bool isWaitingLoop = false;

    private void Start()
    {
        if (targetImage != null && frames != null && frames.Length > 0)
        {
            targetImage.sprite = frames[0];
        }

        if (useStartDelay)
        {
            Invoke(nameof(StartAnimation), startDelay);
        }
        else
        {
            StartAnimation();
        }
    }

    private void StartAnimation()
    {
        isPlaying = true;
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        if (frames == null || frames.Length == 0 || targetImage == null)
            return;

        timer += Time.deltaTime;

        float frameTime = 1f / Mathf.Max(fps, 0.01f);

        if (timer >= frameTime)
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

    private void OnValidate()
    {
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