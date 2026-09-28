using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    public static event Action OnTransitionFinished;

    [Header("Components")]
    [SerializeField] private GameObject transitionRoot;
    [SerializeField] private CanvasGroup transitionCanvasGroup;
    [SerializeField] private Image revealBackground;

    [Header("Logo")]
    [SerializeField] private RectTransform transitionLogo;
    [SerializeField] private CanvasGroup logoCanvasGroup;

    [Header("Loading Text")]
    [SerializeField] private RectTransform loadingText;
    [SerializeField] private CanvasGroup loadingTextCanvasGroup;

    [Header("Circle Reveal")]
    [SerializeField] private float startRadius = 0.01f;
    [SerializeField] private float fullRadius = 1.25f;

    [Header("Circle Timing")]
    [SerializeField] private float coverDuration = 0.55f;
    [SerializeField] private float holdAfterSceneLoaded = 0.10f;
    [SerializeField] private float revealDuration = 0.50f;

    [Header("Logo Cartoon Bounce")]
    [SerializeField] private float logoNormalScale = 1f;
    [SerializeField] private float logoBounceDelay = 0.02f;
    [SerializeField] private float logoBounceDuration = 0.58f;

    [SerializeField] private float bounceScale1 = 1.12f;
    [SerializeField] private float bounceScale2 = 0.94f;
    [SerializeField] private float bounceScale3 = 1.06f;
    [SerializeField] private float bounceScale4 = 0.98f;

    [Header("Loading Text")]
    [SerializeField] private float loadingTextNormalScale = 1f;

    [Header("Close Animation")]
    [Range(0f, 1f)]
    [SerializeField] private float contentCloseStartProgress = 0.30f;

    [Range(0f, 1f)]
    [SerializeField] private float contentFadeStartProgress = 0.48f;

    [SerializeField] private float contentMinScale = 0.02f;

    [Header("Close Squash And Stretch")]
    [SerializeField] private float closeStretchAmount = 0.035f;
    [SerializeField] private float closeStretchFrequency = 1.5f;

    private static readonly int RadiusId =
        Shader.PropertyToID("_Radius");

    private Material _runtimeMaterial;
    private Coroutine _currentCoroutine;
    private float _lastRadius = float.NaN;

    private Action _onCovered;
    private bool _waitingForNewScene;
    private bool _waitForSceneLoadAfterCover;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateRuntimeMaterial();
        PrepareCanvasGroups();
        ResetImmediately();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void CreateRuntimeMaterial()
    {
        if (revealBackground == null)
        {
            Debug.LogError(
                "TransitionManager: Chưa kéo RevealBackground."
            );

            return;
        }

        if (revealBackground.material == null)
        {
            Debug.LogError(
                "TransitionManager: RevealBackground chưa có material."
            );

            return;
        }

        _runtimeMaterial =
            new Material(revealBackground.material);

        revealBackground.material =
            _runtimeMaterial;
    }

    private void PrepareCanvasGroups()
    {
        if (transitionLogo != null)
        {
            if (logoCanvasGroup == null)
            {
                logoCanvasGroup =
                    transitionLogo.GetComponent<CanvasGroup>();
            }

            if (logoCanvasGroup == null)
            {
                logoCanvasGroup =
                    transitionLogo.gameObject
                        .AddComponent<CanvasGroup>();
            }
        }

        if (loadingText != null)
        {
            if (loadingTextCanvasGroup == null)
            {
                loadingTextCanvasGroup =
                    loadingText.GetComponent<CanvasGroup>();
            }

            if (loadingTextCanvasGroup == null)
            {
                loadingTextCanvasGroup =
                    loadingText.gameObject
                        .AddComponent<CanvasGroup>();
            }
        }
    }

    public void PlayCover(Action onCovered)
    {
        TryStartTransition(onCovered, true);
    }

    public bool PlayTransition(Action onCovered)
    {
        return TryStartTransition(onCovered, false);
    }

    private bool TryStartTransition(
        Action onCovered,
        bool waitForSceneLoad
    )
    {
        if (IsPlaying)
            return false;

        StopCurrentCoroutine();

        IsPlaying = true;

        _onCovered = onCovered;
        _waitingForNewScene = false;
        _waitForSceneLoadAfterCover =
            waitForSceneLoad;

        _currentCoroutine =
            StartCoroutine(
                CoverCoroutine()
            );

        return true;
    }

    private IEnumerator CoverCoroutine()
    {
        if (transitionRoot == null)
        {
            Debug.LogError(
                "TransitionManager: Chưa kéo TransitionRoot."
            );

            IsPlaying = false;

            _onCovered?.Invoke();
            _onCovered = null;

            yield break;
        }

        transitionRoot.SetActive(true);

        if (transitionCanvasGroup != null)
        {
            transitionCanvasGroup.alpha = 1f;
            transitionCanvasGroup.interactable = true;
            transitionCanvasGroup.blocksRaycasts = true;
        }

        SetRadius(startRadius);
        PrepareContentForCover();

        float elapsed = 0f;

        while (elapsed < coverDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    Mathf.Max(
                        coverDuration,
                        0.001f
                    )
                );

            float radiusProgress =
                EaseOutCubic(progress);

            float radius =
                Mathf.Lerp(
                    startRadius,
                    fullRadius,
                    radiusProgress
                );

            SetRadius(radius);

            UpdateLogoCartoonBounce(
                elapsed
            );

            yield return null;
        }

        SetRadius(fullRadius);

        if (transitionLogo != null)
        {
            transitionLogo.localScale =
                Vector3.one *
                logoNormalScale;
        }

        if (loadingText != null)
        {
            loadingText.localScale =
                Vector3.one *
                loadingTextNormalScale;
        }

        _currentCoroutine = null;
        _waitingForNewScene =
            _waitForSceneLoadAfterCover;

        _onCovered?.Invoke();
        _onCovered = null;

        if (!_waitForSceneLoadAfterCover &&
            IsPlaying)
        {
            _currentCoroutine =
                StartCoroutine(
                    RevealCoroutine()
                );
        }
    }

    private void PrepareContentForCover()
    {
        if (transitionLogo != null)
        {
            transitionLogo.gameObject
                .SetActive(true);

            transitionLogo.localScale =
                Vector3.one *
                logoNormalScale;
        }

        if (loadingText != null)
        {
            loadingText.gameObject
                .SetActive(true);

            loadingText.localScale =
                Vector3.one *
                loadingTextNormalScale;
        }

        if (logoCanvasGroup != null)
        {
            logoCanvasGroup.alpha = 1f;
        }

        if (loadingTextCanvasGroup != null)
        {
            loadingTextCanvasGroup.alpha = 1f;
        }
    }

    private void UpdateLogoCartoonBounce(
        float elapsed
    )
    {
        if (transitionLogo == null)
            return;

        float bounceElapsed =
            elapsed -
            logoBounceDelay;

        if (bounceElapsed <= 0f)
        {
            transitionLogo.localScale =
                Vector3.one *
                logoNormalScale;

            return;
        }

        float progress =
            Mathf.Clamp01(
                bounceElapsed /
                Mathf.Max(
                    logoBounceDuration,
                    0.001f
                )
            );

        float scale;

        if (progress < 0.28f)
        {
            float phase =
                progress /
                0.28f;

            scale =
                Mathf.Lerp(
                    logoNormalScale,
                    bounceScale1,
                    EaseOutQuart(phase)
                );
        }
        else if (progress < 0.48f)
        {
            float phase =
                (progress - 0.28f) /
                0.20f;

            scale =
                Mathf.Lerp(
                    bounceScale1,
                    bounceScale2,
                    EaseInOutCubic(phase)
                );
        }
        else if (progress < 0.68f)
        {
            float phase =
                (progress - 0.48f) /
                0.20f;

            scale =
                Mathf.Lerp(
                    bounceScale2,
                    bounceScale3,
                    EaseOutCubic(phase)
                );
        }
        else if (progress < 0.84f)
        {
            float phase =
                (progress - 0.68f) /
                0.16f;

            scale =
                Mathf.Lerp(
                    bounceScale3,
                    bounceScale4,
                    EaseInOutCubic(phase)
                );
        }
        else
        {
            float phase =
                (progress - 0.84f) /
                0.16f;

            scale =
                Mathf.Lerp(
                    bounceScale4,
                    logoNormalScale,
                    EaseOutCubic(phase)
                );
        }

        float squashWave =
            Mathf.Sin(
                progress *
                Mathf.PI *
                4f
            );

        float squash =
            squashWave *
            0.018f *
            (1f - progress);

        float scaleX =
            scale + squash;

        float scaleY =
            scale - squash;

        transitionLogo.localScale =
            new Vector3(
                scaleX,
                scaleY,
                1f
            );
    }

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode loadSceneMode
    )
    {
        if (!_waitingForNewScene)
            return;

        _waitingForNewScene = false;

        StopCurrentCoroutine();

        _currentCoroutine =
            StartCoroutine(
                RevealCoroutine()
            );
    }

    private IEnumerator RevealCoroutine()
    {
        yield return null;

        if (holdAfterSceneLoaded > 0f)
        {
            yield return
                new WaitForSecondsRealtime(
                    holdAfterSceneLoaded
                );
        }

        float elapsed = 0f;

        while (elapsed < revealDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    Mathf.Max(
                        revealDuration,
                        0.001f
                    )
                );

            float radiusProgress =
                EaseInCubic(progress);

            float radius =
                Mathf.Lerp(
                    fullRadius,
                    startRadius,
                    radiusProgress
                );

            SetRadius(radius);

            UpdateContentClose(
                progress
            );

            yield return null;
        }

        SetRadius(startRadius);

        if (transitionLogo != null)
        {
            transitionLogo.localScale =
                Vector3.one *
                contentMinScale;
        }

        if (loadingText != null)
        {
            loadingText.localScale =
                Vector3.one *
                contentMinScale;
        }

        if (logoCanvasGroup != null)
        {
            logoCanvasGroup.alpha = 0f;
        }

        if (loadingTextCanvasGroup != null)
        {
            loadingTextCanvasGroup.alpha = 0f;
        }

        FinishTransitionVisual();

        IsPlaying = false;
        _currentCoroutine = null;

        OnTransitionFinished?.Invoke();
    }

    private void UpdateContentClose(
        float progress
    )
    {
        float scaleProgress =
            Mathf.InverseLerp(
                contentCloseStartProgress,
                1f,
                progress
            );

        scaleProgress =
            Mathf.Clamp01(
                scaleProgress
            );

        float easedScale =
            EaseInCubic(
                scaleProgress
            );

        float scale =
            Mathf.Lerp(
                1f,
                contentMinScale,
                easedScale
            );

        float fadeProgress =
            Mathf.InverseLerp(
                contentFadeStartProgress,
                1f,
                progress
            );

        fadeProgress =
            Mathf.Clamp01(
                fadeProgress
            );

        float alpha =
            1f -
            EaseInCubic(
                fadeProgress
            );

        float stretchWave =
            Mathf.Sin(
                scaleProgress *
                Mathf.PI *
                closeStretchFrequency
            );

        float stretch =
            closeStretchAmount *
            (1f - scaleProgress) *
            stretchWave;

        float scaleX =
            Mathf.Max(
                contentMinScale,
                scale + stretch
            );

        float scaleY =
            Mathf.Max(
                contentMinScale,
                scale - stretch
            );

        if (transitionLogo != null)
        {
            transitionLogo.localScale =
                new Vector3(
                    scaleX,
                    scaleY,
                    1f
                );
        }

        if (loadingText != null)
        {
            loadingText.localScale =
                Vector3.one *
                scale;
        }

        if (logoCanvasGroup != null)
        {
            logoCanvasGroup.alpha =
                alpha;
        }

        if (loadingTextCanvasGroup != null)
        {
            loadingTextCanvasGroup.alpha =
                alpha;
        }
    }

    private void FinishTransitionVisual()
    {
        SetRadius(startRadius);

        if (transitionLogo != null)
        {
            transitionLogo.localScale =
                Vector3.one *
                logoNormalScale;

            transitionLogo.gameObject
                .SetActive(false);
        }

        if (loadingText != null)
        {
            loadingText.localScale =
                Vector3.one *
                loadingTextNormalScale;

            loadingText.gameObject
                .SetActive(false);
        }

        if (logoCanvasGroup != null)
        {
            logoCanvasGroup.alpha = 1f;
        }

        if (loadingTextCanvasGroup != null)
        {
            loadingTextCanvasGroup.alpha = 1f;
        }

        if (transitionCanvasGroup != null)
        {
            transitionCanvasGroup.alpha = 0f;
            transitionCanvasGroup.interactable = false;
            transitionCanvasGroup.blocksRaycasts = false;
        }

        if (transitionRoot != null)
        {
            transitionRoot.SetActive(false);
        }

        _waitingForNewScene = false;
        _waitForSceneLoadAfterCover = false;
        _onCovered = null;
    }

    public void ResetImmediately()
    {
        StopCurrentCoroutine();

        FinishTransitionVisual();

        IsPlaying = false;
    }

    private void SetRadius(
        float radius
    )
    {
        if (_runtimeMaterial == null ||
            Mathf.Approximately(_lastRadius, radius))
            return;

        _runtimeMaterial.SetFloat(
            RadiusId,
            radius
        );

        _lastRadius = radius;
    }

    private void StopCurrentCoroutine()
    {
        if (_currentCoroutine == null)
            return;

        StopCoroutine(
            _currentCoroutine
        );

        _currentCoroutine = null;
    }

    private static float EaseOutQuart(
        float value
    )
    {
        value =
            Mathf.Clamp01(value);

        return
            1f -
            Mathf.Pow(
                1f - value,
                4f
            );
    }

    private static float EaseOutCubic(
        float value
    )
    {
        value =
            Mathf.Clamp01(value);

        return
            1f -
            Mathf.Pow(
                1f - value,
                3f
            );
    }

    private static float EaseInCubic(
        float value
    )
    {
        value =
            Mathf.Clamp01(value);

        return
            value *
            value *
            value;
    }

    private static float EaseInOutCubic(
        float value
    )
    {
        value =
            Mathf.Clamp01(value);

        if (value < 0.5f)
        {
            return
                4f *
                value *
                value *
                value;
        }

        return
            1f -
            Mathf.Pow(
                -2f * value + 2f,
                3f
            ) / 2f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (_runtimeMaterial != null)
        {
            Destroy(
                _runtimeMaterial
            );
        }
    }
}
