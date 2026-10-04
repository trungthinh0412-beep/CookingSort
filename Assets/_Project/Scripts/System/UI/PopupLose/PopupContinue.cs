using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using CustomTween;
using Lean.Pool;

public class PopupContinue : Popup
{
    [SerializeField] private GameObject btnCoin;
    [SerializeField] private GameObject btnAds;

    [Header("Target Preview")]
    [SerializeField] private Transform targetGroup;
    [SerializeField] private IngameTargetItem targetItemPrefab;
    [SerializeField, Range(0.5f, 3f)] private float targetItemScale = 1.15f;
    [SerializeField] private Sprite completedTargetIcon;
    [SerializeField] private Sprite incompleteTargetIcon;

    [Header("Intro Animation")]
    [SerializeField, Range(0.5f, 1f)]
    private float introShrinkScale = .85f;
    [SerializeField, Range(1f, 1.25f)]
    private float introOvershootScale = 1.05f;
    [SerializeField, Min(0f)] private float introStartDelay = .1f;
    [SerializeField, Min(.01f)] private float introOvershootDuration = .12f;
    [SerializeField, Min(.01f)] private float introSettleDuration = .1f;

    private CardSlotHolderGold[] _coinTrays;
    private CardSlotHolderBonus[] _rewardAdsTrays;
    private readonly List<IngameTargetItem> _targetItems =
        new List<IngameTargetItem>();
    private Sequence _introSequence;
    private Vector3 _containerBaseScale = Vector3.one;
    private bool _introScaleCached;

    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        CacheIntroScale();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        CacheIntroScale();
        StopIntroAnimation();
        RefreshOptions();
        RefreshTargetPreview();
    }

    protected override void AfterShown()
    {
        base.AfterShown();
        PlayIntroAnimation();
    }

    protected override void BeforeHide()
    {
        StopIntroAnimation();
        base.BeforeHide();
    }

    protected override void OnDisable()
    {
        StopIntroAnimation();
        base.OnDisable();
    }

    private void CacheIntroScale()
    {
        if (_introScaleCached || container == null)
            return;

        _containerBaseScale = container.localScale;
        _introScaleCached = true;
    }

    private void PlayIntroAnimation()
    {
        StopIntroAnimation();
        if (container == null)
            return;

        Vector3 shrinkScale = _containerBaseScale * introShrinkScale;
        Vector3 overshootScale = _containerBaseScale * introOvershootScale;
        container.localScale = shrinkScale;

        _introSequence = Sequence.Create(useUnscaledTime: true)
            .ChainDelay(introStartDelay);
        float startTime = _introSequence.duration;
        _introSequence.Insert(
            startTime,
            Tween.Scale(
                container,
                shrinkScale,
                overshootScale,
                introOvershootDuration,
                Ease.OutQuad
            )
        );
        _introSequence.Insert(
            startTime + introOvershootDuration,
            Tween.Scale(
                container,
                overshootScale,
                _containerBaseScale,
                introSettleDuration,
                Ease.OutQuad
            )
        );
        _introSequence.OnComplete(() =>
        {
            if (container != null)
                container.localScale = _containerBaseScale;
        });
    }

    private void StopIntroAnimation()
    {
        if (_introSequence.isAlive)
            _introSequence.Stop();

        if (container != null && _introScaleCached)
            container.localScale = _containerBaseScale;
    }

    private void RefreshOptions()
    {
        Level level = GetCurrentLevel();

        _coinTrays = level != null
            ? level.GetComponentsInChildren<CardSlotHolderGold>(true)
            : new CardSlotHolderGold[0];
        _rewardAdsTrays = level != null
            ? level.GetComponentsInChildren<CardSlotHolderBonus>(true)
            : new CardSlotHolderBonus[0];

        EvaluateOptions(
            _coinTrays,
            _rewardAdsTrays,
            out bool showCoin,
            out bool showAds
        );

        if (btnCoin != null)
            btnCoin.SetActive(showCoin);

        if (btnAds != null)
            btnAds.SetActive(showAds);
    }

    private void RefreshTargetPreview()
    {
        for (int i = 0; i < _targetItems.Count; i++)
        {
            IngameTargetItem item = _targetItems[i];
            if (item != null)
                LeanPool.Despawn(item.gameObject);
        }

        _targetItems.Clear();

        Level level = GetCurrentLevel();
        if (level == null || level.CardTargets == null ||
            targetGroup == null || targetItemPrefab == null)
        {
            return;
        }

        for (int i = 0; i < level.CardTargets.Count; i++)
        {
            CardTarget target = level.CardTargets[i];
            if (target == null)
                continue;

            IngameTargetItem item = LeanPool.Spawn(
                targetItemPrefab,
                targetGroup
            );
            if (item == null)
                continue;

            Sprite targetSprite = level.TargetConfig != null
                ? level.TargetConfig.GetTargetSprite(target.cardType)
                : null;
            item.SetDisplayScale(targetItemScale);
            item.Setup(target, targetSprite);
            item.ShowStatusIcon(
                target.IsCompleted
                    ? completedTargetIcon
                    : incompleteTargetIcon
            );
            _targetItems.Add(item);
        }
    }

    public static bool HasAvailableOption(Level level)
    {
        if (level == null)
            return false;

        CardSlotHolderGold[] coinTrays =
            level.GetComponentsInChildren<CardSlotHolderGold>(true);
        CardSlotHolderBonus[] rewardAdsTrays =
            level.GetComponentsInChildren<CardSlotHolderBonus>(true);

        EvaluateOptions(
            coinTrays,
            rewardAdsTrays,
            out bool showCoin,
            out bool showAds
        );

        return showCoin || showAds;
    }

    private static void EvaluateOptions(
        CardSlotHolderGold[] coinTrays,
        CardSlotHolderBonus[] rewardAdsTrays,
        out bool showCoin,
        out bool showAds)
    {
        coinTrays = coinTrays ?? new CardSlotHolderGold[0];
        rewardAdsTrays =
            rewardAdsTrays ?? new CardSlotHolderBonus[0];

        bool usedCoin = coinTrays.Any(tray =>
            tray != null && tray.IsPurchased);
        bool usedRewardAds = rewardAdsTrays.Any(tray =>
            tray != null && tray.IsUnlocked);

        bool hasLockedCoinTray = coinTrays.Any(tray =>
            tray != null && tray.IsLocked);
        bool hasLockedRewardAdsTray = rewardAdsTrays.Any(tray =>
            tray != null && !tray.IsUnlocked);

        if (usedCoin && !usedRewardAds)
        {
            showCoin = false;
            showAds = true;
        }
        else if (!usedCoin && usedRewardAds)
        {
            showCoin = true;
            showAds = false;
        }
        else
        {
            // Chua dung tray nao hoac da dung ca hai loai:
            // hien ca hai lua chon dung nhu bang logic Continue.
            showCoin = true;
            showAds = true;
        }

        showCoin &= hasLockedCoinTray;
        showAds &= hasLockedRewardAdsTray;
    }

    private static Level GetCurrentLevel()
    {
        return GameManager.Instance != null &&
               GameManager.Instance.levelController != null
            ? GameManager.Instance.levelController.currentLevel
            : null;
    }

    public void OnClickCoin()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);

        RefreshOptions();
        CardSlotHolderGold coinTray = _coinTrays.FirstOrDefault(
            tray => tray != null && tray.IsLocked
        );

        if (coinTray == null)
        {
            Observer.Notify?.Invoke(
                "No Coin Tray is available.",
                Vector3.zero
            );
            return;
        }

        if (!coinTray.TryPurchase())
            return;

        ResumeGameplay();
    }

    public void OnClickAds()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);

        RefreshOptions();
        CardSlotHolderBonus rewardAdsTray =
            _rewardAdsTrays.FirstOrDefault(
                tray => tray != null && !tray.IsUnlocked
            );

        if (rewardAdsTray == null)
        {
            Observer.Notify?.Invoke(
                "No Reward Ads Tray is available.",
                Vector3.zero
            );
            return;
        }

        AdsController.Instance.ShowRewardAds(() =>
        {
            if (rewardAdsTray != null &&
                rewardAdsTray.UnlockByRewardAds())
            {
                ResumeGameplay();
            }
        }, placement: "PopupContinue_OnClickAds");
    }

    private void ResumeGameplay()
    {
        Hide();

        if (GameManager.Instance != null)
        {
            Level level = GetCurrentLevel();
            if (level != null)
                level.RestoreMovesForContinue();

            GameManager.Instance.CallResume();
        }
    }

    public void OnClickX()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        Hide();

        if (GameManager.Instance != null)
            GameManager.Instance.ConfirmLoseAfterWarning();
        else
            PopupController.Instance.Show<PopupLose>();
    }
}
