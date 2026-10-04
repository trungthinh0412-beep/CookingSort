using System.Collections;
using UnityEngine;

public sealed class PopupPreInGame : Popup
{
    private const int EXTRA_MOVE_AMOUNT = 5;

    [Header("Frame Animation")]
    [SerializeField] private RectTransform frame;
    [SerializeField] private CustomButton extraMoveButton;
    [SerializeField] private RectTransform moveIcon;
    [SerializeField, Min(0f)] private float hiddenOffset = 1200f;
    [SerializeField, Min(0.01f)] private float enterDuration = 0.22f;
    [SerializeField, Min(0f)] private float enterOvershoot = 45f;
    [SerializeField, Min(0.01f)] private float enterSettleDuration = 0.12f;
    [SerializeField, Min(0f)] private float displayDuration = 2.5f;
    [SerializeField, Min(0f)] private float exitAnticipation = 35f;
    [SerializeField, Min(0.01f)] private float exitAnticipationDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float exitDuration = 0.2f;

    private Vector2 _shownPosition;
    private Coroutine _sequenceRoutine;
    private bool _extraMoveRequestInProgress;
    private bool _extraMoveGranted;

    private enum FrameEase
    {
        EaseOutCubic,
        EaseInCubic,
        SmoothStep
    }

    private void Awake()
    {
        if (frame == null)
            frame = container;

        if (frame != null)
            _shownPosition = frame.anchoredPosition;

        if (extraMoveButton != null)
            extraMoveButton.Click.AddListener(OnClickExtraMove);
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();

        StopSequence();
        _extraMoveRequestInProgress = false;
        _extraMoveGranted = false;

        if (extraMoveButton != null)
            extraMoveButton.Interactable = true;

        if (frame != null)
        {
            frame.anchoredPosition =
                _shownPosition + Vector2.up * hiddenOffset;
        }
    }

    protected override void AfterShown()
    {
        base.AfterShown();

        if (frame == null)
        {
            Hide(PopupAnimation.None);
            return;
        }

        _sequenceRoutine = StartCoroutine(PlaySequence());
    }

    protected override void OnDisable()
    {
        StopSequence();

        if (frame != null)
            frame.anchoredPosition = _shownPosition;

        base.OnDisable();
    }

    private void OnDestroy()
    {
        if (extraMoveButton != null)
            extraMoveButton.Click.RemoveListener(OnClickExtraMove);
    }

    public void OnClickExtraMove()
    {
        if (_extraMoveRequestInProgress || _extraMoveGranted)
            return;

        _extraMoveRequestInProgress = true;
        StopSequence();

        if (extraMoveButton != null)
            extraMoveButton.Interactable = false;

        SoundController.Instance?.PlayFX(SoundName.ClickButton);

        if (AdsController.Instance == null)
        {
            CompleteExtraMoveRequest();
            return;
        }

        AdsController.Instance.ShowInterstitialImmediate(
            CompleteExtraMoveRequest,
            placement: "PopupPreInGame_OnClickExtraMove"
        );
    }

    private void CompleteExtraMoveRequest()
    {
        if (_extraMoveGranted)
            return;

        _extraMoveGranted = true;
        _extraMoveRequestInProgress = false;

        PopupInGame popupInGame = PopupController.Instance != null
            ? PopupController.Instance.Get<PopupInGame>() as PopupInGame
            : null;
        popupInGame?.PrepareMoveBonusFlyFrom(moveIcon);

        Level level = GameManager.Instance != null &&
                      GameManager.Instance.levelController != null
            ? GameManager.Instance.levelController.currentLevel
            : null;
        level?.GrantMoveBonus(EXTRA_MOVE_AMOUNT);

        Hide(PopupAnimation.None);
    }

    private IEnumerator PlaySequence()
    {
        Vector2 hiddenPosition =
            _shownPosition + Vector2.up * hiddenOffset;
        Vector2 enterOvershootPosition =
            _shownPosition + Vector2.down * enterOvershoot;
        Vector2 exitAnticipationPosition =
            _shownPosition + Vector2.down * exitAnticipation;

        yield return AnimateFrame(
            hiddenPosition,
            enterOvershootPosition,
            enterDuration,
            FrameEase.EaseOutCubic
        );

        yield return AnimateFrame(
            enterOvershootPosition,
            _shownPosition,
            enterSettleDuration,
            FrameEase.SmoothStep
        );

        if (displayDuration > 0f)
            yield return new WaitForSecondsRealtime(displayDuration);

        yield return AnimateFrame(
            _shownPosition,
            exitAnticipationPosition,
            exitAnticipationDuration,
            FrameEase.SmoothStep
        );

        yield return AnimateFrame(
            exitAnticipationPosition,
            hiddenPosition,
            exitDuration,
            FrameEase.EaseInCubic
        );

        _sequenceRoutine = null;
        Hide(PopupAnimation.None);
    }

    private IEnumerator AnimateFrame(
        Vector2 from,
        Vector2 to,
        float duration,
        FrameEase ease)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        frame.anchoredPosition = from;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            float easedProgress;

            switch (ease)
            {
                case FrameEase.EaseInCubic:
                    easedProgress = progress * progress * progress;
                    break;

                case FrameEase.SmoothStep:
                    easedProgress =
                        progress * progress * (3f - 2f * progress);
                    break;

                default:
                    easedProgress =
                        1f - Mathf.Pow(1f - progress, 3f);
                    break;
            }

            frame.anchoredPosition = Vector2.LerpUnclamped(
                from,
                to,
                easedProgress
            );

            yield return null;
        }

        frame.anchoredPosition = to;
    }

    private void StopSequence()
    {
        if (_sequenceRoutine == null)
            return;

        StopCoroutine(_sequenceRoutine);
        _sequenceRoutine = null;
    }
}
