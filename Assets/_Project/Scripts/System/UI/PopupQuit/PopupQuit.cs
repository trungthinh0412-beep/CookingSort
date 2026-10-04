using CustomTween;
using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PopupQuit : Popup
{
    [Header("Heart State")]
    [SerializeField] private RectTransform heartFrame;
    [SerializeField] private Image normalHeartIcon;
    [SerializeField] private RectTransform brokenHeartLeft;
    [SerializeField] private RectTransform brokenHeartRight;
    [Header("Quit State")]
    [SerializeField] private RectTransform kingQuitRoot;
    [SerializeField] private RectTransform kingsChar;
    [SerializeField] private RectTransform noticeQuit;
    [SerializeField] private TextMeshProUGUI quitButtonText;
    [Header("Timing")]
    [SerializeField] private float heartBreakStartDelay = 0.67f;
    [SerializeField] private float heartFadeDuration = 0.22f;
    [SerializeField] private float heartBreakFadeDuration = 0.18f;
    [SerializeField] private float heartBreakMoveDuration = 0.16f;
    [SerializeField] private float heartBreakLeftAngle = 12f;
    [SerializeField] private float heartBreakRightAngle = -12f;
    [SerializeField] private float heartBreakOvershootAngle = 2f;
    [SerializeField] private float heartBreakSettleDuration = 0.05f;
    [SerializeField] private float heartBreakDropY = 6f;
    [SerializeField] private float quitContentScaleDuration = 0.18f;
    [SerializeField] private float quitContentStaggerDelay = 0.05f;

    private InGamePauseMenu _pauseMenu;
    private CanvasGroup _normalHeartCanvasGroup;
    private CanvasGroup _brokenHeartLeftCanvasGroup;
    private CanvasGroup _brokenHeartRightCanvasGroup;
    private CanvasGroup _kingsCharCanvasGroup;
    private CanvasGroup _noticeQuitCanvasGroup;
    private Vector2 _brokenHeartLeftBasePosition;
    private Vector2 _brokenHeartRightBasePosition;
    private Vector2 _normalHeartBasePosition;
    private Quaternion _brokenHeartLeftBaseRotation = Quaternion.identity;
    private Quaternion _brokenHeartRightBaseRotation = Quaternion.identity;
    private bool _hasEnteredQuitState;
    private bool _cachedViewState;
    private int _quitClickCount;
    private Tween _normalHeartFadeTween;
    private Tween _brokenHeartLeftFadeTween;
    private Tween _brokenHeartRightFadeTween;
    private Tween _kingCharScaleTween;
    private Tween _noticeScaleTween;
    private Tween _heartBreakDelayTween;
    private Coroutine _brokenHeartRoutine;

    public void SetPauseMenu(InGamePauseMenu pauseMenu)
    {
        _pauseMenu = pauseMenu;
    }

    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        CacheViewState();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        PlayMenuBarSound();
        CacheViewState();
        ResetViewState();
    }

    protected override void AfterShown()
    {
        base.AfterShown();
        PlayHeartBreakAnimation();
    }

    protected override void BeforeHide()
    {
        PlayMenuBarSound();
        StopStateTweens();
        base.BeforeHide();
    }

    protected override void OnDisable()
    {
        StopStateTweens();
        base.OnDisable();
    }

    public void OnClickResume()
    {
        Hide();

        if (_pauseMenu != null &&
            _pauseMenu.IsOpen)
        {
            _pauseMenu.Close();
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    public void OnClickQuit()
    {
        _quitClickCount++;

        if (_quitClickCount == 1)
        {
            SoundController.Instance?.PlayFX(
                SoundName.ClickButton
            );
            ShowQuitState();
            return;
        }

        if (_quitClickCount < 2)
            return;

        SoundController.Instance?.PauseBackground();
        SoundController.Instance?.PlayFX(SoundName.LoseLevel);

        Hide(PopupAnimation.None);

        if (_pauseMenu != null &&
            _pauseMenu.IsOpen)
        {
            _pauseMenu.CloseImmediately();
        }

        GameManager.Instance.ReplayGamePausedWithPopupBoosterQuit();
    }

    private void CacheViewState()
    {
        if (_cachedViewState)
            return;

        _normalHeartCanvasGroup = EnsureCanvasGroup(
            normalHeartIcon != null
                ? normalHeartIcon.gameObject
                : null);
        _brokenHeartLeftCanvasGroup = EnsureCanvasGroup(
            brokenHeartLeft != null
                ? brokenHeartLeft.gameObject
                : null);
        _brokenHeartRightCanvasGroup = EnsureCanvasGroup(
            brokenHeartRight != null
                ? brokenHeartRight.gameObject
                : null);
        _kingsCharCanvasGroup = EnsureCanvasGroup(
            kingsChar != null
                ? kingsChar.gameObject
                : null);
        _noticeQuitCanvasGroup = EnsureCanvasGroup(
            noticeQuit != null
                ? noticeQuit.gameObject
                : null);

        if (brokenHeartLeft != null)
        {
            _brokenHeartLeftBasePosition = brokenHeartLeft.anchoredPosition;
            _brokenHeartLeftBaseRotation = brokenHeartLeft.localRotation;
        }

        if (brokenHeartRight != null)
        {
            _brokenHeartRightBasePosition = brokenHeartRight.anchoredPosition;
            _brokenHeartRightBaseRotation = brokenHeartRight.localRotation;
        }

        if (normalHeartIcon != null)
            _normalHeartBasePosition = normalHeartIcon.rectTransform.anchoredPosition;

        _cachedViewState = true;
    }

    private void ResetViewState()
    {
        StopStateTweens();
        _hasEnteredQuitState = false;
        _quitClickCount = 0;

        if (heartFrame != null)
            heartFrame.gameObject.SetActive(true);

        if (_normalHeartCanvasGroup != null)
            _normalHeartCanvasGroup.alpha = 1f;

        if (_brokenHeartLeftCanvasGroup != null)
            _brokenHeartLeftCanvasGroup.alpha = 0f;

        if (_brokenHeartRightCanvasGroup != null)
            _brokenHeartRightCanvasGroup.alpha = 0f;

        if (normalHeartIcon != null)
            normalHeartIcon.rectTransform.anchoredPosition = _normalHeartBasePosition;

        if (brokenHeartLeft != null)
        {
            brokenHeartLeft.gameObject.SetActive(false);
            brokenHeartLeft.anchoredPosition = _brokenHeartLeftBasePosition;
            brokenHeartLeft.localRotation = _brokenHeartLeftBaseRotation;
        }

        if (brokenHeartRight != null)
        {
            brokenHeartRight.gameObject.SetActive(false);
            brokenHeartRight.anchoredPosition = _brokenHeartRightBasePosition;
            brokenHeartRight.localRotation = _brokenHeartRightBaseRotation;
        }

        if (kingQuitRoot != null)
            kingQuitRoot.gameObject.SetActive(false);

        if (_kingsCharCanvasGroup != null)
            _kingsCharCanvasGroup.alpha = 0f;

        if (_noticeQuitCanvasGroup != null)
            _noticeQuitCanvasGroup.alpha = 0f;

        if (quitButtonText != null)
            quitButtonText.text = "Quit";
    }

    private void PlayHeartBreakAnimation()
    {
        if (heartFrame == null || normalHeartIcon == null ||
            brokenHeartLeft == null || brokenHeartRight == null)
            return;

        _heartBreakDelayTween = Tween.Delay(
            heartBreakStartDelay,
            useUnscaledTime: true).OnComplete(() =>
        {
            if (heartFrame == null || !heartFrame.gameObject.activeInHierarchy)
                return;

            brokenHeartLeft.gameObject.SetActive(true);
            brokenHeartRight.gameObject.SetActive(true);

            brokenHeartLeft.anchoredPosition = _brokenHeartLeftBasePosition;
            brokenHeartRight.anchoredPosition = _brokenHeartRightBasePosition;

            if (brokenHeartLeft != null)
                brokenHeartLeft.localRotation = _brokenHeartLeftBaseRotation;

            if (brokenHeartRight != null)
                brokenHeartRight.localRotation = _brokenHeartRightBaseRotation;

            if (_normalHeartCanvasGroup != null)
            {
                _normalHeartFadeTween = Tween.Alpha(
                    _normalHeartCanvasGroup,
                    1f,
                    0f,
                    heartFadeDuration,
                    useUnscaledTime: true);
            }

            if (_brokenHeartLeftCanvasGroup != null)
            {
                _brokenHeartLeftFadeTween = Tween.Alpha(
                    _brokenHeartLeftCanvasGroup,
                    0f,
                    1f,
                    heartBreakFadeDuration,
                    useUnscaledTime: true);
            }

            if (_brokenHeartRightCanvasGroup != null)
            {
                _brokenHeartRightFadeTween = Tween.Alpha(
                    _brokenHeartRightCanvasGroup,
                    0f,
                    1f,
                    heartBreakFadeDuration,
                    useUnscaledTime: true);
            }

            if (_brokenHeartRoutine != null)
                StopCoroutine(_brokenHeartRoutine);

            _brokenHeartRoutine = StartCoroutine(AnimateBrokenHeartOpen());
        });
    }

    private void ShowQuitState()
    {
        if (_hasEnteredQuitState)
            return;

        _hasEnteredQuitState = true;
        StopStateTweens();

        if (heartFrame != null)
            heartFrame.gameObject.SetActive(false);

        if (kingQuitRoot != null)
            kingQuitRoot.gameObject.SetActive(true);

        if (_kingsCharCanvasGroup != null)
            _kingsCharCanvasGroup.alpha = 0f;

        if (_noticeQuitCanvasGroup != null)
            _noticeQuitCanvasGroup.alpha = 0f;

        if (quitButtonText != null)
            quitButtonText.text = "Quit";

        if (_kingsCharCanvasGroup != null)
        {
            _kingCharScaleTween = Tween.Alpha(
                _kingsCharCanvasGroup,
                0f,
                1f,
                quitContentScaleDuration,
                useUnscaledTime: true);
        }

        if (_noticeQuitCanvasGroup != null)
        {
            _noticeScaleTween = Tween.Alpha(
                _noticeQuitCanvasGroup,
                0f,
                1f,
                quitContentScaleDuration,
                startDelay: quitContentStaggerDelay,
                useUnscaledTime: true);
        }
    }

    private void StopStateTweens()
    {
        _heartBreakDelayTween.Stop();
        _normalHeartFadeTween.Stop();
        _brokenHeartLeftFadeTween.Stop();
        _brokenHeartRightFadeTween.Stop();
        _kingCharScaleTween.Stop();
        _noticeScaleTween.Stop();

        if (_brokenHeartRoutine != null)
        {
            StopCoroutine(_brokenHeartRoutine);
            _brokenHeartRoutine = null;
        }
    }

    private IEnumerator AnimateBrokenHeartOpen()
    {
        if (brokenHeartLeft == null || brokenHeartRight == null)
            yield break;

        brokenHeartLeft.localRotation = _brokenHeartLeftBaseRotation;
        brokenHeartRight.localRotation = _brokenHeartRightBaseRotation;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, heartBreakMoveDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            float dropOffset = Mathf.Lerp(0f, heartBreakDropY, t);
            float leftAngle = Mathf.LerpAngle(
                _brokenHeartLeftBaseRotation.eulerAngles.z,
                _brokenHeartLeftBaseRotation.eulerAngles.z + heartBreakLeftAngle,
                t);
            float rightAngle = Mathf.LerpAngle(
                _brokenHeartRightBaseRotation.eulerAngles.z,
                _brokenHeartRightBaseRotation.eulerAngles.z + heartBreakRightAngle,
                t);

            brokenHeartLeft.anchoredPosition =
                _brokenHeartLeftBasePosition + Vector2.down * dropOffset;
            brokenHeartRight.anchoredPosition =
                _brokenHeartRightBasePosition + Vector2.down * dropOffset;
            brokenHeartLeft.localRotation =
                Quaternion.Euler(0f, 0f, leftAngle);
            brokenHeartRight.localRotation =
                Quaternion.Euler(0f, 0f, rightAngle);

            yield return null;
        }

        float leftTargetAngle =
            _brokenHeartLeftBaseRotation.eulerAngles.z + heartBreakLeftAngle;
        float rightTargetAngle =
            _brokenHeartRightBaseRotation.eulerAngles.z + heartBreakRightAngle;
        float leftOvershootAngle = leftTargetAngle +
                                   Mathf.Sign(heartBreakLeftAngle) * heartBreakOvershootAngle;
        float rightOvershootAngle = rightTargetAngle +
                                    Mathf.Sign(heartBreakRightAngle) * heartBreakOvershootAngle;

        brokenHeartLeft.localRotation =
            Quaternion.Euler(0f, 0f, leftOvershootAngle);
        brokenHeartRight.localRotation =
            Quaternion.Euler(0f, 0f, rightOvershootAngle);

        elapsed = 0f;
        float settleDuration = Mathf.Max(0.01f, heartBreakSettleDuration);

        while (elapsed < settleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            t = t * t * (3f - 2f * t);
            float dropOffset = Mathf.Lerp(heartBreakDropY, 0f, t);

            float leftAngle = Mathf.LerpAngle(
                leftOvershootAngle,
                leftTargetAngle,
                t);
            float rightAngle = Mathf.LerpAngle(
                rightOvershootAngle,
                rightTargetAngle,
                t);

            brokenHeartLeft.anchoredPosition =
                _brokenHeartLeftBasePosition + Vector2.down * dropOffset;
            brokenHeartRight.anchoredPosition =
                _brokenHeartRightBasePosition + Vector2.down * dropOffset;
            brokenHeartLeft.localRotation =
                Quaternion.Euler(0f, 0f, leftAngle);
            brokenHeartRight.localRotation =
                Quaternion.Euler(0f, 0f, rightAngle);

            yield return null;
        }

        brokenHeartLeft.anchoredPosition = _brokenHeartLeftBasePosition;
        brokenHeartRight.anchoredPosition = _brokenHeartRightBasePosition;
        brokenHeartLeft.localRotation =
            Quaternion.Euler(0f, 0f, leftTargetAngle);
        brokenHeartRight.localRotation =
            Quaternion.Euler(0f, 0f, rightTargetAngle);
        _brokenHeartRoutine = null;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        if (target == null)
            return null;

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    private static void PlayMenuBarSound()
    {
        SoundController.Instance?.PlayFX(SoundName.MenuBar);
    }
}
