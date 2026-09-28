using CustomTween;
using TMPro;
using UnityEngine;

public class PopupBooster : Popup
{
    [System.Serializable]
    private sealed class BoosterCardView
    {
        [SerializeField] private CustomButton statusButton;
        [SerializeField] private CustomButton selectionButton;
        [SerializeField] private GameObject selectionCheckmark;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private CustomButton plusButton;

        public bool IsSelected { get; set; }

        public void Apply(int amount)
        {
            bool hasBooster = amount > 0;
            if (!hasBooster)
                IsSelected = false;

            if (statusButton != null)
                statusButton.gameObject.SetActive(!IsSelected);

            if (selectionButton != null)
                selectionButton.gameObject.SetActive(IsSelected);

            if (selectionCheckmark != null)
                selectionCheckmark.SetActive(IsSelected);

            if (quantityText != null)
            {
                quantityText.text = Mathf.Max(0, amount).ToString();
                quantityText.gameObject.SetActive(hasBooster);
            }

            if (plusButton != null)
                plusButton.gameObject.SetActive(!hasBooster);
        }

        public void ConfigurePressAnimation(float duration)
        {
            if (statusButton != null)
                statusButton.ScaleDuration = duration;

            if (selectionButton != null)
                selectionButton.ScaleDuration = duration;
        }
    }

    [SerializeField] private CustomButton closeButton;
    [SerializeField] private CustomButton playButton;
    [SerializeField] private TextMeshProUGUI levelText;
    [Header("Booster Selection")]
    [SerializeField] private BoosterCardView moreDealCard;
    [SerializeField] private BoosterCardView magicSwapCard;
    [SerializeField] private BoosterCardView magnetCard;
    [Header("Intro Animation")]
    [SerializeField] private Transform playAnimationTarget;
    [SerializeField] private Transform[] boosterAnimationTargets;
    [Header("Target UI")]
    [SerializeField] private Transform targetGroup;

    private const float PlayShrinkScale = .9f;
    private const float PlayOvershootScale = 1.1f;
    private const float PlayStartDelay = .2f;
    private const float PlayShrinkDuration = .15f;
    private const float PlayOvershootDuration = .2f;
    private const float PlaySettleDuration = .15f;
    // Thoi gian cho sau animation cua nut Play truoc khi 3 booster bat dau.
    private const float BoosterStartDelay = .2f;
    private const float BoosterOvershootScale = 1.2f;
    private const float BoosterShrinkScale = .9f;
    private const float BoosterOvershootDuration = .12f;
    private const float BoosterShrinkDuration = .12f;
    private const float BoosterSettleDuration = .12f;
    // Khoang cach giua tung booster trong hieu ung domino.
    private const float BoosterStaggerDelay = .07f;
    // Toc do hieu ung nhan cua Status/Selection button.
    private const float BoosterPressDuration = .08f;

    private int _targetSetupVersion;
    private Sequence _introSequence;
    private Vector3 _playBaseScale = Vector3.one;
    private Vector3[] _boosterBaseScales;
    private bool _introScalesCached;

    protected override void OnInstantiate()
    {
        base.OnInstantiate();

        CacheIntroAnimationScales();

    }

    protected override void BeforeShow()
    {
        base.BeforeShow();

        CacheIntroAnimationScales();
        StopIntroAnimation();

        UpdateLevelText(Data.PlayerData != null
            ? Data.PlayerData.CurrentLevelIndex
            : 1);

        RefreshBoosterCards(true);
        SetupTargets();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Observer.UseBooster += OnBoosterAmountChanged;
    }

    protected override void AfterShown()
    {
        base.AfterShown();
        PlayIntroAnimation();
    }

    protected override void BeforeHide()
    {
        StopIntroAnimation();

        // Huy ket qua Addressables dang cho neu popup da dong.
        _targetSetupVersion++;
        base.BeforeHide();
    }

    protected override void OnDisable()
    {
        Observer.UseBooster -= OnBoosterAmountChanged;
        StopIntroAnimation();
        base.OnDisable();
    }

    public void OnClickMoreDeal()
    {
        OnClickBooster(BoosterType.MoreDeal);
    }

    public void OnClickMagicSwap()
    {
        OnClickBooster(BoosterType.MagicSwap);
    }

    public void OnClickMagnet()
    {
        OnClickBooster(BoosterType.Magnet);
    }

    private void OnClickBooster(BoosterType boosterType)
    {
        BoosterCardView card = GetBoosterCard(boosterType);
        if (card == null)
        {
            Debug.LogWarning(
                $"[PopupBooster] Chua gan card cho {boosterType} trong prefab.");
            return;
        }

        int amount = GetBoosterAmount(boosterType);
        if (amount <= 0)
        {
            card.Apply(amount);
            PlayClickSound();
            ShowBuyBoosterPopup(boosterType);
            return;
        }

        card.IsSelected = !card.IsSelected;
        card.Apply(amount);
        PlayClickSound();
    }

    private BoosterCardView GetBoosterCard(BoosterType boosterType)
    {
        return boosterType switch
        {
            BoosterType.MoreDeal => moreDealCard,
            BoosterType.MagicSwap => magicSwapCard,
            BoosterType.Magnet => magnetCard,
            _ => null
        };
    }

    private void RefreshBoosterCards(bool resetSelection)
    {
        RefreshBoosterCard(
            moreDealCard,
            BoosterType.MoreDeal,
            resetSelection);
        RefreshBoosterCard(
            magicSwapCard,
            BoosterType.MagicSwap,
            resetSelection);
        RefreshBoosterCard(
            magnetCard,
            BoosterType.Magnet,
            resetSelection);
    }

    private void OnBoosterAmountChanged(BoosterType changedType)
    {
        BoosterCardView card = GetBoosterCard(changedType);
        if (card == null)
            return;

        card.Apply(GetBoosterAmount(changedType));
    }

    private static void RefreshBoosterCard(
        BoosterCardView card,
        BoosterType boosterType,
        bool resetSelection)
    {
        if (card == null)
            return;

        if (resetSelection)
            card.IsSelected = false;

        card.ConfigurePressAnimation(BoosterPressDuration);
        card.Apply(GetBoosterAmount(boosterType));
    }

    protected static void PlayClickSound()
    {
        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);
    }

    protected static int GetBoosterAmount(BoosterType boosterType)
    {
        if (Data.PlayerData == null)
            return 0;

        return boosterType switch
        {
            BoosterType.Shuffle => Data.PlayerData.CurrentShuffle,
            BoosterType.Bomb => Data.PlayerData.CurrentBomb,
            BoosterType.MoreDeal => Data.PlayerData.CurrentMoreDeal,
            BoosterType.MagicSwap => Data.PlayerData.CurrentMagicSwap,
            BoosterType.Magnet => Data.PlayerData.CurrentMagnet,
            _ => 0
        };
    }

    protected static void ShowBuyBoosterPopup(BoosterType boosterType)
    {
        if (PopupController.Instance == null)
            return;

        PopupBuyBooster popup =
            PopupController.Instance.Get<PopupBuyBooster>()
            as PopupBuyBooster;

        if (popup == null)
            return;

        popup.Init(boosterType);
        PopupController.Instance.Show<PopupBuyBooster>(
            PopupAnimation.ScaleFade);
    }

    private void CacheIntroAnimationScales()
    {
        if (_introScalesCached)
            return;

        if (playAnimationTarget != null)
            _playBaseScale = playAnimationTarget.localScale;

        int boosterCount = boosterAnimationTargets != null
            ? boosterAnimationTargets.Length
            : 0;
        _boosterBaseScales = new Vector3[boosterCount];

        for (int index = 0; index < boosterCount; index++)
        {
            Transform booster = boosterAnimationTargets[index];
            _boosterBaseScales[index] = booster != null
                ? booster.localScale
                : Vector3.one;
        }

        _introScalesCached = true;
    }

    private void PlayIntroAnimation()
    {
        StopIntroAnimation();

        if (playAnimationTarget == null)
            return;

        _introSequence = Sequence.Create(useUnscaledTime: true)
            .ChainDelay(PlayStartDelay)
            .Chain(Tween.Scale(
                playAnimationTarget,
                _playBaseScale,
                _playBaseScale * PlayShrinkScale,
                PlayShrinkDuration,
                Ease.InQuad))
            .Chain(Tween.Scale(
                playAnimationTarget,
                _playBaseScale * PlayShrinkScale,
                _playBaseScale * PlayOvershootScale,
                PlayOvershootDuration,
                Ease.OutQuad))
            .Chain(Tween.Scale(
                playAnimationTarget,
                _playBaseScale * PlayOvershootScale,
                _playBaseScale,
                PlaySettleDuration,
                Ease.OutQuad))
            .ChainDelay(BoosterStartDelay);

        float dominoStartTime = _introSequence.duration;
        int boosterCount = boosterAnimationTargets != null
            ? boosterAnimationTargets.Length
            : 0;

        for (int index = 0; index < boosterCount; index++)
        {
            Transform booster = boosterAnimationTargets[index];
            if (booster == null)
                continue;

            Vector3 baseScale = _boosterBaseScales[index];
            float startTime = dominoStartTime + index * BoosterStaggerDelay;

            _introSequence.Insert(
                startTime,
                Tween.Scale(
                    booster,
                    baseScale,
                    baseScale * BoosterOvershootScale,
                    BoosterOvershootDuration,
                    Ease.OutQuad));
            _introSequence.Insert(
                startTime + BoosterOvershootDuration,
                Tween.Scale(
                    booster,
                    baseScale * BoosterOvershootScale,
                    baseScale * BoosterShrinkScale,
                    BoosterShrinkDuration,
                    Ease.InOutQuad));
            _introSequence.Insert(
                startTime + BoosterOvershootDuration + BoosterShrinkDuration,
                Tween.Scale(
                    booster,
                    baseScale * BoosterShrinkScale,
                    baseScale,
                    BoosterSettleDuration,
                    Ease.OutQuad));
        }

        _introSequence.OnComplete(ResetIntroAnimationScales);
    }

    private void StopIntroAnimation()
    {
        if (_introSequence.isAlive)
            _introSequence.Stop();

        ResetIntroAnimationScales();
    }

    private void ResetIntroAnimationScales()
    {
        if (playAnimationTarget != null)
            playAnimationTarget.localScale = _playBaseScale;

        if (boosterAnimationTargets == null || _boosterBaseScales == null)
            return;

        int boosterCount = Mathf.Min(
            boosterAnimationTargets.Length,
            _boosterBaseScales.Length);

        for (int index = 0; index < boosterCount; index++)
        {
            Transform booster = boosterAnimationTargets[index];
            if (booster != null)
                booster.localScale = _boosterBaseScales[index];
        }
    }

    public virtual void OnClickClose()
    {

        AfterHiddenAction = RestoreHomeAfterClose;
        Hide(PopupAnimation.None);
    }

    protected virtual void RestoreHomeAfterClose()
    {
        if (PopupController.Instance != null)
        {
            PopupController.Instance.Show<PopupHome>(
                PopupAnimation.None
            );
        }
    }

    public virtual void OnClickPlay()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        GameManager.Instance.PlayCurrentLevel(
            usePopupTransition: true
        );
    }

    private void UpdateLevelText(int level)
    {
        if (levelText != null)
        {
            levelText.text =
                $"Level {Mathf.Max(1, level)}";
        }
    }

    private async void SetupTargets()
    {
        int setupVersion = ++_targetSetupVersion;

        if (targetGroup != null)
            targetGroup.gameObject.SetActive(false);

        SetBoosterAreaVisible(false);

        LevelController levelController =
            GameManager.Instance != null
                ? GameManager.Instance.levelController
                : null;

        if (levelController == null)
        {
            Debug.LogWarning(
                "[PopupBooster] LevelController chua duoc gan trong GameManager.");
            return;
        }

        Level level;

        try
        {
            level = await levelController.PrepareLevelAsync(false);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"[PopupBooster] Khong the setup target: {exception.Message}");
            return;
        }

        if (setupVersion != _targetSetupVersion || level == null)
            return;
    }

    private void SetBoosterAreaVisible(bool visible)
    {
        if (boosterAnimationTargets == null)
            return;

        for (int i = 0; i < boosterAnimationTargets.Length; i++)
        {
            if (boosterAnimationTargets[i] != null)
                boosterAnimationTargets[i].gameObject.SetActive(visible);
        }
    }

}
