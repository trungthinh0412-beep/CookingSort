using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using CustomTween;

public sealed class PopupGoodJob : Popup, IPointerClickHandler
{
    private const string NextButtonName = "Next_Btn";
    private const string BonusButtonName = "Bonus_btn";

    [Header("Reward Appearance")]
    [SerializeField] private RectTransform coinRewards;
    [SerializeField] private RectTransform gemsRewards;
    [SerializeField, Min(1f)] private float rewardOvershootScale = 1.1f;
    [SerializeField, Min(0.01f)] private float rewardExpandDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float rewardSettleDuration = 0.1f;

    private CustomButton _nextButton;
    private CustomButton _bonusButton;
    private bool _isClosing;
    private Vector3 _coinRewardsBaseScale;
    private Vector3 _gemsRewardsBaseScale;
    private bool _rewardBaseScalesCached;
    private Tween _coinRewardTween;
    private Tween _gemsRewardTween;

    protected override void OnInstantiate()
    {
        base.OnInstantiate();

        CacheRewardBaseScales();

        _nextButton = BindButton(NextButtonName, OnClickNext);
        _bonusButton = BindButton(BonusButtonName, OnClickGetX2BuyAds);
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();

        _isClosing = false;
        SetButtonsInteractable(true);
        ResetRewardScales();

        // GoodJob is shown over the Home background while its side tabs and
        // persistent bottom bar are still outside the screen.
        if (PopupController.Instance != null)
            PopupController.Instance.SetBottomBarVisible(false);
    }

    protected override void AfterShown()
    {
        base.AfterShown();
        SoundController.Instance?.PlayFX(SoundName.WinLevel);
        PlayRewardPopAnimation();
    }

    protected override void OnDisable()
    {
        StopRewardTweens();
        RestoreRewardScales();
        base.OnDisable();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Keep the existing tap-to-continue behaviour for the popup
        // background. Next_Btn and Bonus_btn have their own handlers.
        OnClickNext();
    }

    public void OnClickNext()
    {
        CloseToHome();
    }

    public void OnClickGetX2BuyAds()
    {
        if (_isClosing)
            return;

        SetButtonsInteractable(false);

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        CloseToHome();
    }

    private void CloseToHome()
    {
        if (_isClosing)
            return;

        _isClosing = true;
        SetButtonsInteractable(false);
        Hide(PopupAnimation.None);
        PopupController.Instance?.SetBottomBarVisible(true);
    }

    private CustomButton BindButton(string buttonName, UnityAction clickAction)
    {
        Transform buttonTransform = FindDescendant(buttonName);
        if (buttonTransform == null)
        {
            Debug.LogWarning(
                $"[PopupGoodJob] Could not find {buttonName} in the popup."
            );
            return null;
        }

        CustomButton button = buttonTransform.GetComponent<CustomButton>();
        if (button == null)
            button = buttonTransform.gameObject.AddComponent<CustomButton>();

        button.Click.AddListener(clickAction);
        return button;
    }

    private Transform FindDescendant(string targetName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == targetName)
                return child;
        }

        return null;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (_nextButton != null)
            _nextButton.Interactable = interactable;

        if (_bonusButton != null)
            _bonusButton.Interactable = interactable;
    }

    private void CacheRewardBaseScales()
    {
        if (_rewardBaseScalesCached)
            return;

        if (coinRewards != null)
            _coinRewardsBaseScale = coinRewards.localScale;

        if (gemsRewards != null)
            _gemsRewardsBaseScale = gemsRewards.localScale;

        _rewardBaseScalesCached = true;
    }

    private void ResetRewardScales()
    {
        CacheRewardBaseScales();
        StopRewardTweens();

        if (coinRewards != null)
            coinRewards.localScale = Vector3.zero;

        if (gemsRewards != null)
            gemsRewards.localScale = Vector3.zero;
    }

    private void RestoreRewardScales()
    {
        if (!_rewardBaseScalesCached)
            return;

        if (coinRewards != null)
            coinRewards.localScale = _coinRewardsBaseScale;

        if (gemsRewards != null)
            gemsRewards.localScale = _gemsRewardsBaseScale;
    }

    private void PlayRewardPopAnimation()
    {
        PlayCoinRewardPop();
        PlayGemsRewardPop();
    }

    private void PlayCoinRewardPop()
    {
        if (coinRewards == null)
            return;

        Vector3 overshootScale = _coinRewardsBaseScale * rewardOvershootScale;
        _coinRewardTween = Tween.Scale(
            coinRewards,
            Vector3.zero,
            overshootScale,
            rewardExpandDuration,
            Ease.OutCubic,
            useUnscaledTime: true).OnComplete(() =>
        {
            _coinRewardTween = Tween.Scale(
                coinRewards,
                overshootScale,
                _coinRewardsBaseScale,
                rewardSettleDuration,
                Ease.InOutQuad,
                useUnscaledTime: true);
        });
    }

    private void PlayGemsRewardPop()
    {
        if (gemsRewards == null)
            return;

        Vector3 overshootScale = _gemsRewardsBaseScale * rewardOvershootScale;
        _gemsRewardTween = Tween.Scale(
            gemsRewards,
            Vector3.zero,
            overshootScale,
            rewardExpandDuration,
            Ease.OutCubic,
            useUnscaledTime: true).OnComplete(() =>
        {
            _gemsRewardTween = Tween.Scale(
                gemsRewards,
                overshootScale,
                _gemsRewardsBaseScale,
                rewardSettleDuration,
                Ease.InOutQuad,
                useUnscaledTime: true);
        });
    }

    private void StopRewardTweens()
    {
        _coinRewardTween.Stop();
        _gemsRewardTween.Stop();
    }
}
