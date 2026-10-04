using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using CustomTween;

public sealed class PopupGoodJob : Popup, IPointerClickHandler
{
    private const int CoinReward = 50;
    private const int GemReward = 10;
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
    private bool _claimInProgress;
    private bool _rewardClaimed;
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

        _claimInProgress = false;
        _rewardClaimed = false;
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
        SoundController.Instance?.PauseBackground();
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
        ClaimAndReturnHome(1);
    }

    public void OnClickGetX2BuyAds()
    {
        if (_claimInProgress)
            return;

        _claimInProgress = true;
        SetButtonsInteractable(false);

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        if (AdsController.Instance != null)
        {
            AdsController.Instance.ShowInterstitial(
                () => ClaimAndReturnHome(2),
                placement: "PopupGoodJob_OnClickGetX2BuyAds"
            );
            return;
        }

        ClaimAndReturnHome(2);
    }

    private void ClaimAndReturnHome(int rewardMultiplier)
    {
        if (_rewardClaimed)
            return;

        _claimInProgress = true;
        _rewardClaimed = true;
        SetButtonsInteractable(false);

        int goldReward = CoinReward * rewardMultiplier;
        Vector3 coinSourceWorldPosition = coinRewards != null
            ? coinRewards.position
            : transform.position;
        Camera coinSourceCamera = GetUiCamera(coinRewards);
        bool goldCommitted = false;

        if (Data.PlayerData != null)
        {
            // Commit the wallet immediately, then explicitly play one visual
            // sequence after Home is visible so the hidden GoodJob source can
            // fly into the active Home GoldHandler without a duplicate batch.
            GoldHandler.AddWithoutResourceAnimation(goldReward);
            goldCommitted = true;
            Data.PlayerData.CurrentStar += GemReward * rewardMultiplier;
            Data.SaveData();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnHome();
            if (goldCommitted)
            {
                GoldHandler.PlayCommittedRewardFromWorld(
                    coinSourceWorldPosition,
                    coinSourceCamera,
                    goldReward
                );
            }
            return;
        }

        Hide(PopupAnimation.None);
        if (goldCommitted)
        {
            GoldHandler.PlayCommittedRewardFromWorld(
                coinSourceWorldPosition,
                coinSourceCamera,
                goldReward
            );
        }
    }

    private static Camera GetUiCamera(RectTransform source)
    {
        Canvas sourceCanvas = source != null
            ? source.GetComponentInParent<Canvas>()
            : null;
        return sourceCanvas != null &&
               sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? sourceCanvas.worldCamera
            : null;
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
