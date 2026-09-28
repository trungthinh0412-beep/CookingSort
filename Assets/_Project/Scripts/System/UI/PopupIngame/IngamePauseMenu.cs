
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InGamePauseMenu : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private GameObject darkBackground;
    [SerializeField] private CanvasGroup darkBackgroundCanvasGroup;
    [SerializeField] private RectTransform settingButton;

    [Header("Pause Buttons")]
    [SerializeField] private List<RectTransform> pauseButtons;

    [SerializeField] private RectTransform cancelButton;

    [Header("Setting Icons")]
    [SerializeField] private Image musicIcon;
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;

    [SerializeField] private Image soundIcon;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;

    [SerializeField] private Image vibrationIcon;
    [SerializeField] private Sprite vibrationOnSprite;
    [SerializeField] private Sprite vibrationOffSprite;

    [Header("Game Speed")]
    [SerializeField] private Image timeScaleIcon;
    [SerializeField] private Sprite normalTimeScaleSprite;
    [SerializeField] private Sprite fastTimeScaleSprite;
    [SerializeField] private float normalGameTimeScale = 1f;
    [SerializeField] private float fastGameTimeScale = 1.5f;

    [Header("Animation")]
    [SerializeField] private float moveDuration = 0.13f;
    [SerializeField] private float dominoDelay = 0.03f;
    [SerializeField] private float revealDuration = 0.05f;

    [Header("Close Animation")]
    [SerializeField] private float closeMoveDuration = 0.12f;
    [SerializeField] private float closeDominoDelay = 0.018f;
    [SerializeField] private float closeRotationAngle = 10f;

    [Range(0f, 1.5f)]
    [SerializeField] private float closeJoltStrength = 0.65f;

    [Range(0f, 0.9f)]
    [SerializeField] private float closeFadeStart = 0.35f;

    [Header("Button Overshoot")]
    [SerializeField] private float overshootDistance = 55f;
    [SerializeField] private float bounceBackDuration = 0.18f;
    [SerializeField] private float startScale = 0.35f;
    [SerializeField] private float overshootScale = 1.2f;
    [SerializeField] private float rotationBounceAngle = 20f;
    [SerializeField] private float positiveRotationAngle = 16f;

    [Range(0.3f, 0.7f)]
    [SerializeField] private float positiveRotationPeakTime = 0.52f;

    [Header("Cartoon Jelly")]
    [Range(0f, 1f)]
    [SerializeField] private float buttonStickiness = 0.5f;

    [Range(0f, 0.4f)]
    [SerializeField] private float pullStretch = 0.16f;

    [Range(0f, 0.4f)]
    [SerializeField] private float jellyAmount = 0.14f;

    [Min(0.1f)]
    [SerializeField] private float jellyOscillations = 1f;

    [Min(0f)]
    [SerializeField] private float jellyDamping = 3f;

    [Header("Dark Background Fade")]
    [SerializeField] private float darkFadeDuration = 0.18f;

    [Range(0f, 1f)]
    [SerializeField] private float darkMaxAlpha = 0.6f;

    private readonly List<Vector2> _targetPositions =
        new List<Vector2>();

    private readonly Dictionary<RectTransform, CanvasGroup>
        _buttonCanvasGroups =
            new Dictionary<RectTransform, CanvasGroup>();

    private Vector2 _collapsedPosition;
    private Quaternion _cancelRotation;
    private bool _cancelPositionCached;
    private Coroutine _menuCoroutine;
    private Coroutine _darkFadeCoroutine;

    private bool _isOpen;
    private bool _buttonsAnimating;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        CacheTargetPositions();

        if (cancelButton != null)
        {
            _cancelRotation = cancelButton.localRotation;
        }

        HideInstant();
    }

    private void OnEnable()
    {
        RefreshSettingIcons();
        RefreshTimeScaleVisual();

        if (!_isOpen)
        {
            ApplyGameTimeScale();
        }
    }

    private void LateUpdate()
    {
        if (!_isOpen)
            return;

        if (cancelButton != null &&
            _cancelPositionCached)
        {
            cancelButton.anchoredPosition = _collapsedPosition;
            cancelButton.localScale = Vector3.one;
            cancelButton.localRotation = _cancelRotation;
        }

        if (_buttonsAnimating)
            return;

        for (int i = 0; i < pauseButtons.Count; i++)
        {
            RectTransform button = pauseButtons[i];

            if (button == null ||
                button == cancelButton)
                continue;

            button.anchoredPosition = _targetPositions[i];
            button.localScale = Vector3.one;
            button.localRotation = Quaternion.identity;
        }
    }

    private void CacheTargetPositions()
    {
        _targetPositions.Clear();

        for (int i = 0; i < pauseButtons.Count; i++)
        {
            if (pauseButtons[i] != null &&
                pauseButtons[i] != cancelButton)
            {
                _targetPositions.Add(
                    pauseButtons[i].anchoredPosition
                );
            }
            else
            {
                _targetPositions.Add(
                    Vector2.zero
                );
            }
        }
    }

    public void Toggle()
    {
        if (_isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (_isOpen)
            return;

        _isOpen = true;

        Time.timeScale = 0f;

        ShowDarkBackground();

        RefreshUIOrder();
        ShowCancelButton();

        StopCurrentAnimation();

        _menuCoroutine =
            StartCoroutine(
                OpenAnimation()
            );
    }

    public void Close()
    {
        if (!_isOpen)
            return;

        _isOpen = false;
        if (darkBackgroundCanvasGroup != null)
        {
            darkBackgroundCanvasGroup.interactable =
                false;
        }

        HideDarkBackgroundSmooth();

        StopCurrentAnimation();

        _menuCoroutine =
            StartCoroutine(
                CloseAnimation()
            );
    }

    public void CloseImmediately()
    {
        _isOpen = false;
        StopCurrentAnimation();

        if (_darkFadeCoroutine != null)
        {
            StopCoroutine(
                _darkFadeCoroutine
            );

            _darkFadeCoroutine =
                null;
        }

        Time.timeScale = 1f;
        HideInstant();
    }

    public void OnClickCancel()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        Close();
    }

    private void RefreshUIOrder()
    {
        for (int i = pauseButtons.Count - 1;
             i >= 0;
             i--)
        {
            RectTransform button =
                pauseButtons[i];

            if (button == null ||
                button == cancelButton)
                continue;

            button.SetAsLastSibling();
        }

        if (cancelButton != null)
        {
            cancelButton.SetAsLastSibling();
        }
    }

    private IEnumerator OpenAnimation()
    {
        List<int> animationOrder =
            GetAnimationOrder(false);

        Vector2 collapsedCenter =
            GetCollapsedCenter();

        for (int orderIndex = 0;
             orderIndex < animationOrder.Count;
             orderIndex++)
        {
            int i = animationOrder[orderIndex];

            RectTransform button =
                pauseButtons[i];

            if (button == null ||
                button == cancelButton)
                continue;

            DisableScaleEffect(button);
            button.gameObject.SetActive(true);
            SetButtonCanvasState(
                button,
                0f,
                false
            );
            SetButtonAnimationState(
                button,
                collapsedCenter,
                startScale,
                0f
            );
        }

        RefreshUIOrder();

        _buttonsAnimating = true;

        float moveTime =
            Mathf.Max(0.01f, moveDuration);

        float bounceTime =
            Mathf.Max(0.01f, bounceBackDuration);

        float delay =
            Mathf.Max(0f, dominoDelay);

        float totalDuration =
            moveTime +
            bounceTime +
            delay * Mathf.Max(
                0,
                animationOrder.Count - 1
            );

        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int orderIndex = 0;
                 orderIndex < animationOrder.Count;
                 orderIndex++)
            {
                float localTime =
                    elapsed -
                    orderIndex * delay;

                if (localTime < 0f)
                    continue;

                int i = animationOrder[orderIndex];

                RectTransform button =
                    pauseButtons[i];

                if (button == null)
                    continue;

                float revealT =
                    Mathf.Clamp01(
                        localTime /
                        Mathf.Max(
                            0.001f,
                            revealDuration
                        )
                    );

                revealT =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        revealT
                    );

                SetButtonCanvasState(
                    button,
                    revealT,
                    false
                );

                ApplyOpenAnimationFrame(
                    button,
                    collapsedCenter,
                    _targetPositions[i],
                    localTime,
                    moveTime,
                    bounceTime
                );
            }

            yield return null;
        }

        for (int orderIndex = 0;
             orderIndex < animationOrder.Count;
             orderIndex++)
        {
            int i = animationOrder[orderIndex];

            SetButtonOpenState(
                pauseButtons[i],
                _targetPositions[i]
            );
        }

        _buttonsAnimating = false;
        _menuCoroutine = null;
    }

    private void ApplyOpenAnimationFrame(
        RectTransform button,
        Vector2 startCenter,
        Vector2 targetPosition,
        float elapsed,
        float moveTime,
        float bounceTime
    )
    {
        if (button == null)
            return;

        Vector2 targetCenter =
            targetPosition +
            button.rect.center;

        Vector2 overshootCenter =
            targetCenter +
            Vector2.up * overshootDistance;

        if (elapsed < moveTime)
        {
            float rawT =
                Mathf.Clamp01(
                    elapsed / moveTime
                );

            float easeOutT =
                1f -
                Mathf.Pow(1f - rawT, 3f);

            // A slower release keeps consecutive buttons visually attached
            // near the cancel button before the upward pull accelerates.
            float stickyT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    rawT
                );

            float moveT =
                Mathf.Lerp(
                    easeOutT,
                    stickyT,
                    buttonStickiness
                );

            float baseScale =
                Mathf.Lerp(
                    startScale,
                    overshootScale,
                    moveT
                );

            float stretchWave =
                Mathf.Sin(rawT * Mathf.PI);

            float stretch =
                stretchWave *
                stretchWave *
                pullStretch;

            Vector2 scale =
                new Vector2(
                    baseScale * (1f - stretch),
                    baseScale * (1f + stretch)
                );

            float rotation =
                Mathf.Lerp(
                    0f,
                    -rotationBounceAngle,
                    moveT
                );

            SetButtonAnimationState(
                button,
                Vector2.Lerp(
                    startCenter,
                    overshootCenter,
                    moveT
                ),
                scale,
                rotation
            );

            return;
        }

        float bounceT =
            Mathf.Clamp01(
                (elapsed - moveTime) /
                bounceTime
            );

        float settleT =
            Mathf.SmoothStep(
                0f,
                1f,
                bounceT
            );

        // SmoothStep has zero velocity at both ends. Building the damping
        // from it prevents the sharp velocity change that looked like a
        // short pause when the pull phase entered the bounce phase.
        float dampingPower =
            Mathf.Max(
                0.01f,
                jellyDamping * 0.5f
            );

        float smoothDamping =
            Mathf.Pow(
                1f - settleT,
                dampingPower
            );

        float oscillationPhase =
            bounceT *
            Mathf.PI *
            2f *
            jellyOscillations;

        float jellyWave =
            Mathf.Sin(oscillationPhase) *
            Mathf.Sin(bounceT * Mathf.PI) *
            smoothDamping;

        float positionWave =
            Mathf.Cos(oscillationPhase) *
            smoothDamping;

        float baseBounceScale =
            Mathf.Lerp(
                overshootScale,
                1f,
                settleT
            );

        Vector2 bounceScale =
            new Vector2(
                baseBounceScale *
                (1f + jellyWave * jellyAmount),
                baseBounceScale *
                (1f - jellyWave * jellyAmount)
            );

        SetButtonAnimationState(
            button,
            targetCenter +
            Vector2.up *
            overshootDistance *
            positionWave,
            bounceScale,
            GetBounceRotation(bounceT)
        );

        if (bounceT >= 1f)
        {
            SetButtonOpenState(
                button,
                targetPosition
            );
        }
    }

    private void SetButtonOpenState(
        RectTransform button,
        Vector2 targetPosition
    )
    {
        if (button == null)
            return;

        button.anchoredPosition = targetPosition;
        button.localScale = Vector3.one;
        button.localRotation = Quaternion.identity;

        SetButtonCanvasState(
            button,
            1f,
            true
        );
    }

    private Vector2 GetCollapsedCenter()
    {
        if (cancelButton == null)
            return GetCollapsedPosition();

        Vector3 centerOffset =
            _cancelRotation *
            (Vector3)cancelButton.rect.center;

        return _collapsedPosition +
               new Vector2(
                   centerOffset.x,
                   centerOffset.y
               );
    }

    private Vector2 GetButtonCenter(
        RectTransform button
    )
    {
        Vector3 scaledCenter =
            Vector3.Scale(
                button.rect.center,
                button.localScale
            );

        Vector3 centerOffset =
            button.localRotation *
            scaledCenter;

        return button.anchoredPosition +
               new Vector2(
                   centerOffset.x,
                   centerOffset.y
               );
    }

    private void SetButtonAnimationState(
        RectTransform button,
        Vector2 centerPosition,
        float scale,
        float rotation
    )
    {
        SetButtonAnimationState(
            button,
            centerPosition,
            Vector2.one * scale,
            rotation
        );
    }

    private void SetButtonAnimationState(
        RectTransform button,
        Vector2 centerPosition,
        Vector2 scale,
        float rotation
    )
    {
        Quaternion localRotation =
            Quaternion.Euler(
                0f,
                0f,
                rotation
            );

        Vector3 scaledCenter =
            Vector3.Scale(
                button.rect.center,
                new Vector3(
                    scale.x,
                    scale.y,
                    1f
                )
            );

        Vector3 centerOffset =
            localRotation *
            scaledCenter;

        button.localScale =
            new Vector3(
                scale.x,
                scale.y,
                1f
            );
        button.localRotation =
            localRotation;
        button.anchoredPosition =
            centerPosition -
            new Vector2(
                centerOffset.x,
                centerOffset.y
            );
    }

    private IEnumerator CloseAnimation()
    {
        List<int> animationOrder =
            GetAnimationOrder(true);

        List<RectTransform> buttons =
            new List<RectTransform>();

        List<Vector2> startCenters =
            new List<Vector2>();

        List<Vector2> startScales =
            new List<Vector2>();

        List<float> startRotations =
            new List<float>();

        List<float> startAlphas =
            new List<float>();

        float closeDuration =
            Mathf.Max(0.01f, closeMoveDuration);

        float closeDelay =
            Mathf.Max(0f, closeDominoDelay);

        Vector2 targetCenter =
            GetCollapsedCenter();

        for (int orderIndex = 0;
             orderIndex < animationOrder.Count;
             orderIndex++)
        {
            int i = animationOrder[orderIndex];

            RectTransform button =
                pauseButtons[i];

            if (button == null ||
                button == cancelButton)
                continue;

            CanvasGroup canvasGroup =
                GetButtonCanvasGroup(button);

            buttons.Add(button);
            startCenters.Add(
                GetButtonCenter(button)
            );
            startScales.Add(
                new Vector2(
                    button.localScale.x,
                    button.localScale.y
                )
            );
            startRotations.Add(
                Mathf.DeltaAngle(
                    0f,
                    button.localEulerAngles.z
                )
            );
            startAlphas.Add(
                canvasGroup != null
                    ? canvasGroup.alpha
                    : 1f
            );
        }

        float totalDuration =
            closeDuration +
            closeDelay * Mathf.Max(
                0,
                buttons.Count - 1
            );

        CanvasGroup cancelCanvasGroup =
            GetButtonCanvasGroup(cancelButton);

        float cancelStartAlpha =
            cancelCanvasGroup != null
                ? cancelCanvasGroup.alpha
                : 1f;

        _buttonsAnimating = true;

        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0;
                 i < buttons.Count;
                 i++)
            {
                float localTime =
                    elapsed - i * closeDelay;

                if (localTime < 0f)
                    continue;

                float t =
                    Mathf.Clamp01(
                        localTime /
                        closeDuration
                    );

                float moveT =
                    EaseInOutBackSoft(t);

                float rotationWave =
                    Mathf.Sin(t * Mathf.PI);

                rotationWave *=
                    rotationWave;

                float fadeT =
                    Mathf.InverseLerp(
                        closeFadeStart,
                        1f,
                        t
                    );

                fadeT =
                    EaseInOutCubic(fadeT);

                SetButtonAnimationState(
                    buttons[i],
                    Vector2.LerpUnclamped(
                        startCenters[i],
                        targetCenter,
                        moveT
                    ),
                    Vector2.LerpUnclamped(
                        startScales[i],
                        Vector2.one * startScale,
                        moveT
                    ),
                    Mathf.LerpUnclamped(
                        startRotations[i],
                        0f,
                        moveT
                    ) +
                    closeRotationAngle *
                    rotationWave
                );

                SetButtonCanvasState(
                    buttons[i],
                    Mathf.Lerp(
                        startAlphas[i],
                        0f,
                        fadeT
                    ),
                    false
                );
            }

            float cancelT =
                EaseInOutCubic(
                    Mathf.Clamp01(
                        elapsed /
                        totalDuration
                    )
                );

            SetButtonCanvasState(
                cancelButton,
                Mathf.Lerp(
                    cancelStartAlpha,
                    0f,
                    cancelT
                ),
                false
            );

            yield return null;
        }

        for (int i = 0;
             i < buttons.Count;
             i++)
        {
            SetButtonAnimationState(
                buttons[i],
                targetCenter,
                startScale,
                0f
            );

            SetButtonCanvasState(
                buttons[i],
                0f,
                false
            );
        }

        SetButtonCanvasState(
            cancelButton,
            0f,
            false
        );

        ApplyGameTimeScale();

        _buttonsAnimating = false;
        _menuCoroutine = null;
    }

    private Vector2 GetCollapsedPosition()
    {
        return cancelButton != null
            ? _collapsedPosition
            : Vector2.zero;
    }

    private List<int> GetAnimationOrder(
        bool reverse
    )
    {
        List<int> order = new List<int>();

        for (int i = 0; i < pauseButtons.Count; i++)
        {
            if (pauseButtons[i] == null ||
                pauseButtons[i] == cancelButton)
                continue;

            order.Add(i);
        }

        if (reverse)
        {
            order.Reverse();
        }

        return order;
    }

    private float GetBounceRotation(float t)
    {
        t = Mathf.Clamp01(t);

        float peakTime =
            Mathf.Clamp(
                positiveRotationPeakTime,
                0.01f,
                0.99f
            );

        if (t < peakTime)
        {
            float swingT =
                Mathf.Clamp01(
                    t / peakTime
                );

            swingT =
                EaseInOutSine(swingT);

            return Mathf.Lerp(
                -rotationBounceAngle,
                positiveRotationAngle,
                swingT
            );
        }

        float balanceT =
            Mathf.Clamp01(
                (t - peakTime) /
                (1f - peakTime)
            );

        balanceT =
            EaseInOutSine(balanceT);

        return Mathf.Lerp(
            positiveRotationAngle,
            0f,
            balanceT
        );
    }

    private float EaseInOutSine(float t)
    {
        return -(
            Mathf.Cos(Mathf.PI * t) - 1f
        ) / 2f;
    }

    private void ShowCancelButton()
    {
        if (cancelButton == null)
            return;

        DisableScaleEffect(cancelButton);

        if (!_cancelPositionCached)
        {
            Canvas.ForceUpdateCanvases();

            if (settingButton != null)
            {
                Vector3 settingCenter =
                    settingButton.TransformPoint(
                        settingButton.rect.center
                    );

                Vector3 cancelCenter =
                    cancelButton.TransformPoint(
                        cancelButton.rect.center
                    );

                cancelButton.position +=
                    settingCenter - cancelCenter;
            }

            _collapsedPosition = cancelButton.anchoredPosition;
            _cancelPositionCached = true;
        }
        else
        {
            cancelButton.anchoredPosition = _collapsedPosition;
        }

        cancelButton.gameObject.SetActive(true);
        cancelButton.localScale = Vector3.one;
        cancelButton.localRotation = _cancelRotation;

        SetButtonCanvasState(
            cancelButton,
            1f,
            true
        );

        cancelButton.SetAsLastSibling();
    }

    private void DisableScaleEffect(
        RectTransform button
    )
    {
        if (button == null)
            return;

        UIEffect scaleEffect =
            button.GetComponent<UIEffect>();

        if (scaleEffect != null)
        {
            scaleEffect.enabled = false;
        }
    }

    private CanvasGroup GetButtonCanvasGroup(
        RectTransform button
    )
    {
        if (button == null)
            return null;

        if (_buttonCanvasGroups.TryGetValue(
                button,
                out CanvasGroup cachedGroup
            ))
        {
            return cachedGroup;
        }

        CanvasGroup canvasGroup =
            button.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup =
                button.gameObject
                    .AddComponent<CanvasGroup>();
        }

        _buttonCanvasGroups[button] =
            canvasGroup;

        return canvasGroup;
    }

    private void SetButtonCanvasState(
        RectTransform button,
        float alpha,
        bool interactable
    )
    {
        CanvasGroup canvasGroup =
            GetButtonCanvasGroup(button);

        if (canvasGroup == null)
            return;

        canvasGroup.alpha =
            Mathf.Clamp01(alpha);
        canvasGroup.interactable =
            interactable;
        canvasGroup.blocksRaycasts =
            interactable;
    }

    private float EaseInOutBackSoft(float t)
    {
        t = Mathf.Clamp01(t);

        float overshoot =
            Mathf.Max(
                0f,
                closeJoltStrength
            ) *
            1.525f;

        if (t < 0.5f)
        {
            float doubledT =
                2f * t;

            return doubledT *
                   doubledT *
                   (
                       (overshoot + 1f) *
                       doubledT -
                       overshoot
                   ) /
                   2f;
        }

        float shiftedT =
            2f * t - 2f;

        return (
            shiftedT *
            shiftedT *
            (
                (overshoot + 1f) *
                shiftedT +
                overshoot
            ) +
            2f
        ) / 2f;
    }

    private float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.5f)
        {
            return 4f * t * t * t;
        }

        return 1f -
               Mathf.Pow(
                   -2f * t + 2f,
                   3f
               ) /
               2f;
    }
    private void ShowDarkBackground()
    {
        if (darkBackground == null)
            return;

        darkBackground.SetActive(true);

        if (darkBackgroundCanvasGroup == null)
            return;

        darkBackgroundCanvasGroup.interactable =
            true;

        darkBackgroundCanvasGroup.blocksRaycasts =
            true;

        if (_darkFadeCoroutine != null)
        {
            StopCoroutine(
                _darkFadeCoroutine
            );
        }

        _darkFadeCoroutine =
            StartCoroutine(
                FadeDarkBackground(
                    darkBackgroundCanvasGroup.alpha,
                    darkMaxAlpha
                )
            );
    }

    private void HideDarkBackgroundSmooth()
    {
        if (darkBackground == null)
            return;

        if (darkBackgroundCanvasGroup == null)
        {
            darkBackground.SetActive(false);

            return;
        }

        darkBackgroundCanvasGroup.interactable =
            false;

        if (_darkFadeCoroutine != null)
        {
            StopCoroutine(
                _darkFadeCoroutine
            );
        }

        _darkFadeCoroutine =
            StartCoroutine(
                FadeOutDarkBackground()
            );
    }

    private IEnumerator FadeOutDarkBackground()
    {
        yield return
            FadeDarkBackground(
                darkBackgroundCanvasGroup.alpha,
                0f
            );

        if (darkBackgroundCanvasGroup != null)
        {
            darkBackgroundCanvasGroup.interactable =
                false;

            darkBackgroundCanvasGroup.blocksRaycasts =
                false;
        }

        if (darkBackground != null)
        {
            darkBackground.SetActive(false);
        }

        _darkFadeCoroutine =
            null;
    }

    private IEnumerator FadeDarkBackground(
        float from,
        float to
    )
    {
        if (darkBackgroundCanvasGroup == null)
            yield break;

        float elapsed = 0f;

        while (elapsed < darkFadeDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    Mathf.Max(
                        darkFadeDuration,
                        0.001f
                    )
                );

            t =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            darkBackgroundCanvasGroup.alpha =
                Mathf.Lerp(
                    from,
                    to,
                    t
                );

            yield return null;
        }

        darkBackgroundCanvasGroup.alpha =
            to;
    }

    private void HideInstant()
    {
        _isOpen = false;

        if (darkBackgroundCanvasGroup != null)
        {
            darkBackgroundCanvasGroup.alpha =
                0f;

            darkBackgroundCanvasGroup.interactable =
                false;

            darkBackgroundCanvasGroup.blocksRaycasts =
                false;
        }

        if (darkBackground != null)
        {
            darkBackground.SetActive(false);
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(false);
        }

        for (int i = 0; i < pauseButtons.Count; i++)
        {
            RectTransform button =
                pauseButtons[i];

            if (button == null ||
                button == cancelButton)
                continue;

            button.localScale =
                Vector3.one;

            button.localRotation =
                Quaternion.identity;

            button.anchoredPosition =
                GetCollapsedPosition();

            button.gameObject
                .SetActive(true);

            SetButtonCanvasState(
                button,
                0f,
                false
            );
        }
    }

    private void StopCurrentAnimation()
    {
        if (_menuCoroutine != null)
        {
            StopCoroutine(
                _menuCoroutine
            );

            _menuCoroutine =
                null;
        }

        _buttonsAnimating = false;
    }

    public void OnClickMusic()
    {
        if (Data.PlayerData == null)
            return;

        Data.PlayerData.MusicState =
            !Data.PlayerData.MusicState;

        Data.SaveData();
        RefreshSettingIcons();
    }

    public void OnClickTimeScale()
    {
        if (Data.PlayerData == null)
            return;

        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        Data.PlayerData.FastGameSpeed =
            !Data.PlayerData.FastGameSpeed;

        Data.SaveData();
        RefreshTimeScaleVisual();

        if (!_isOpen)
        {
            ApplyGameTimeScale();
        }
    }

    public void OnClickSound()
    {
        if (Data.PlayerData == null)
            return;

        Data.PlayerData.SoundState =
            !Data.PlayerData.SoundState;

        Data.SaveData();
        RefreshSettingIcons();
    }

    public void OnClickVibration()
    {
        if (Data.PlayerData == null)
            return;

        Data.PlayerData.VibrationState =
            !Data.PlayerData.VibrationState;

        Data.SaveData();
        RefreshSettingIcons();
    }

    public void OnClickExit()
    {
        if (GameManager.Instance.IsPictureReplay)
        {
            CloseImmediately();
            GameManager.Instance.ExitPictureReplay();
            return;
        }
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        var popupQuit =
            PopupController.Instance
                .Get<PopupQuit>() as PopupQuit;

        if (popupQuit == null)
        {
            Debug.LogError(
                "PopupQuit không tìm thấy!"
            );

            return;
        }

        popupQuit.SetPauseMenu(this);

        PopupController.Instance
            .Show<PopupQuit>(
                PopupAnimation.None
            );
    }

    private void ApplyGameTimeScale()
    {
        bool useFastSpeed =
            Data.PlayerData != null &&
            Data.PlayerData.FastGameSpeed;

        float selectedTimeScale =
            useFastSpeed
                ? fastGameTimeScale
                : normalGameTimeScale;

        Time.timeScale =
            Mathf.Max(
                0.01f,
                selectedTimeScale
            );
    }

    private void RefreshTimeScaleVisual()
    {
        bool useFastSpeed =
            Data.PlayerData != null &&
            Data.PlayerData.FastGameSpeed;

        SetIconSprite(
            timeScaleIcon,
            useFastSpeed,
            fastTimeScaleSprite,
            normalTimeScaleSprite
        );
    }

    private void RefreshSettingIcons()
    {
        if (Data.PlayerData == null)
            return;

        SetIconSprite(
            musicIcon,
            Data.PlayerData.MusicState,
            musicOnSprite,
            musicOffSprite
        );

        SetIconSprite(
            soundIcon,
            Data.PlayerData.SoundState,
            soundOnSprite,
            soundOffSprite
        );

        SetIconSprite(
            vibrationIcon,
            Data.PlayerData.VibrationState,
            vibrationOnSprite,
            vibrationOffSprite
        );
    }

    private void SetIconSprite(
        Image icon,
        bool isOn,
        Sprite onSprite,
        Sprite offSprite
    )
    {
        if (icon == null)
            return;

        Sprite targetSprite =
            isOn ? onSprite : offSprite;

        if (targetSprite == null)
            return;

        icon.sprite = targetSprite;
        icon.SetVerticesDirty();
    }

    private void OnDisable()
    {
        StopCurrentAnimation();

        if (_darkFadeCoroutine != null)
        {
            StopCoroutine(
                _darkFadeCoroutine
            );

            _darkFadeCoroutine =
                null;
        }

        Time.timeScale =
            1f;

        HideInstant();
    }
}
