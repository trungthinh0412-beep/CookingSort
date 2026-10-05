using System.Collections;
using System.Collections.Generic;
using CustomTween;
using Lean.Pool;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class PopupBooster : Popup
{
    [System.Serializable]
    private sealed class BoosterCardView
    {
        [SerializeField] private CustomButton statusButton;
        [SerializeField] private CustomButton selectionButton;
        [SerializeField] private GameObject selectionCheckmark;
        [SerializeField] private GameObject lockLevel;
        [SerializeField] private GameObject quantityBadge;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private CustomButton plusButton;

        public bool IsSelected { get; set; }

        public void ApplyPreLevelCard(
            Sprite icon,
            int amount,
            bool isUnlocked,
            string timedAmount = null)
        {
            if (!isUnlocked)
            {
                IsSelected = false;

                if (lockLevel != null)
                    lockLevel.SetActive(true);
                if (statusButton != null)
                {
                    statusButton.gameObject.SetActive(true);
                    SetIconVisibility(statusButton, false);
                    SetQuantityBadgeVisibility(statusButton, false);
                }
                if (selectionButton != null)
                {
                    selectionButton.gameObject.SetActive(false);
                    SetIconVisibility(selectionButton, false);
                    SetQuantityBadgeVisibility(selectionButton, false);
                }
                if (selectionCheckmark != null)
                    selectionCheckmark.SetActive(false);
                if (plusButton != null)
                    plusButton.gameObject.SetActive(false);
                if (quantityBadge != null)
                    quantityBadge.SetActive(false);
                if (quantityText != null)
                    quantityText.gameObject.SetActive(false);

                return;
            }

            if (lockLevel != null)
                lockLevel.SetActive(false);
            if (statusButton != null)
            {
                statusButton.gameObject.SetActive(!IsSelected);
                SetIconVisibility(statusButton, !IsSelected);
                SetQuantityBadgeVisibility(statusButton, true);
            }
            if (selectionButton != null)
            {
                selectionButton.gameObject.SetActive(IsSelected);
                SetIconVisibility(selectionButton, IsSelected);
                SetQuantityBadgeVisibility(selectionButton, true);
            }
            if (selectionCheckmark != null)
                selectionCheckmark.SetActive(IsSelected);
            if (plusButton != null)
                plusButton.gameObject.SetActive(false);
            if (quantityBadge != null)
                quantityBadge.SetActive(true);
            if (quantityText != null)
            {
                quantityText.text = string.IsNullOrEmpty(timedAmount)
                    ? Mathf.Max(0, amount).ToString()
                    : timedAmount;
                quantityText.gameObject.SetActive(true);
            }

            if (icon == null)
                return;

            ApplyIcon(statusButton, icon);
            ApplyIcon(selectionButton, icon);
        }

        private static void ApplyIcon(CustomButton button, Sprite icon)
        {
            if (button == null)
                return;

            foreach (Image image in button.GetComponentsInChildren<Image>(true))
            {
                if (image.name != "BoosterIcon")
                    continue;

                image.sprite = icon;
                image.enabled = true;
                break;
            }
        }

        private static void SetIconVisibility(CustomButton button, bool isVisible)
        {
            if (button == null)
                return;

            foreach (Image image in button.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "BoosterIcon")
                    image.enabled = isVisible;
            }
        }

        private static void SetQuantityBadgeVisibility(
            CustomButton button,
            bool isVisible)
        {
            if (button == null)
                return;

            foreach (Transform child in button.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "QuantityBadge")
                    child.gameObject.SetActive(isVisible);
            }
        }
    }

    [SerializeField] private CustomButton closeButton;
    [SerializeField] private CustomButton playButton;
    [SerializeField] private TextMeshProUGUI levelText;
    [Header("Pre-Level Card Slots")]
    [SerializeField]
    private bool giveOnePreLevelCardForTesting;
    [FormerlySerializedAs("wildCard")]
    [FormerlySerializedAs("moreDealCard")]
    [SerializeField] private BoosterCardView preLevelCardSlot1;
    [FormerlySerializedAs("extraTrayCard")]
    [FormerlySerializedAs("magicSwapCard")]
    [SerializeField] private BoosterCardView preLevelCardSlot2;
    [FormerlySerializedAs("freeMovesCard")]
    [FormerlySerializedAs("magnetCard")]
    [SerializeField] private BoosterCardView preLevelCardSlot3;
    [Header("Intro Animation")]
    [SerializeField] private Transform playAnimationTarget;
    [SerializeField] private Transform playButton1AnimationTarget;
    [SerializeField] private Transform playButton2AnimationTarget;
    [SerializeField] private Transform playButton3AnimationTarget;
    [SerializeField] private Transform[] boosterAnimationTargets;
    [Header("Mini Game Boart")]
    [SerializeField] private GameObject miniGameBoart;
    [Header("Target UI")]
    [SerializeField] private Transform targetGroup;
    [SerializeField] private IngameTargetItem targetItemPrefab;
    [SerializeField, Range(0.5f, 3f)] private float targetItemScale = 1.15f;

        [Header("Play Button Animation")]
    [SerializeField] private float playShrinkScale = .85f;
    [SerializeField] private float playOvershootScale = 1.05f;
    [SerializeField] private float playStartDelay = .1f;
    [SerializeField] private float playOvershootDuration = .12f;
    [SerializeField] private float playSettleDuration = .1f;
    [SerializeField] private float playButton3LoopMinScale = .9f;
    [SerializeField] private float playButton3LoopMaxScale = 1.1f;
    [SerializeField] private float playButton3LoopHalfDuration = .65f;

    [Header("Booster Slots Animation")]
    [SerializeField] private float boosterOvershootScale = 1.2f;
    [SerializeField] private float boosterShrinkScale = .85f;
    [SerializeField] private float boosterOvershootDuration = .08f;
    [SerializeField] private float boosterSettleDuration = .08f;
    private const int PreLevelBoosterUnlockLevel = 8;
    private const int MultiplePlayButtonsUnlockLevel = 12;

    private readonly List<IngameTargetItem> _targetItems =
        new List<IngameTargetItem>();
    private readonly List<PreLevelCardData> _visiblePreLevelCards =
        new List<PreLevelCardData>();
    private readonly HashSet<CardType> _selectedTimedPreLevelCards =
        new HashSet<CardType>();
    private int _targetSetupVersion;
    private Sequence _introSequence;
    private Vector3 _playBaseScale = Vector3.one;
    private Vector3[] _playButtonBaseScales;
    private Vector3[] _boosterBaseScales;
    private Coroutine _playButton3LoopCoroutine;
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

        int currentLevel = Data.PlayerData != null
            ? Data.PlayerData.CurrentLevelIndex
            : 1;
        RefreshPlayButtonVisibility(currentLevel);
        UpdateLevelText(currentLevel);

        RefundSelectedPreLevelCards();
        HidePreLevelCardSlots();
        SetupTargets();
        UpdateMiniGameBoartVisibility();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Observer.PreLevelCardChanged += OnPreLevelCardAmountChanged;
        Observer.LevelChanged += OnLevelChanged;
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
        Observer.PreLevelCardChanged -= OnPreLevelCardAmountChanged;
        Observer.LevelChanged -= OnLevelChanged;
        StopIntroAnimation();
        base.OnDisable();
    }

    public void OnClickPreLevelCard() => OnClickPreLevelCardSlot(0);

    // Kept for existing button bindings in older PopupBooster prefabs.
    public void OnClickWildCard() => OnClickPreLevelCardSlot(0);
    public void OnClickExtraTray() => OnClickPreLevelCardSlot(1);
    public void OnClickFreeMoves() => OnClickPreLevelCardSlot(2);

    protected static void PlayClickSound()
    {
        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);
    }

    protected static void PlayMenuBarSound()
    {
        SoundController.Instance?.PlayFX(SoundName.MenuBar);
    }

    private void OnClickPreLevelCardSlot(int slotIndex)
    {
        if (!ArePreLevelBoostersUnlocked())
            return;

        if (slotIndex < 0 || slotIndex >= _visiblePreLevelCards.Count)
            return;

        PreLevelCardData card = _visiblePreLevelCards[slotIndex];
        BoosterCardView view = GetPreLevelCardView(slotIndex);
        if (view == null)
            return;

        int amount = GetPreLevelCardAmount(card.cardType);
        bool timedActive = IsTimedPreLevelCardActive(card.cardType);
        if (amount <= 0 && !timedActive && !view.IsSelected)
        {
            PlayClickSound();
            OpenShopTab();
            return;
        }

        view.IsSelected = !view.IsSelected;
        if (view.IsSelected)
        {
            if (timedActive)
                _selectedTimedPreLevelCards.Add(card.cardType);
            else
                SetPreLevelCardAmount(card.cardType, amount - 1);
        }
        else if (!_selectedTimedPreLevelCards.Remove(card.cardType))
        {
            SetPreLevelCardAmount(card.cardType, amount + 1);
        }
        RefreshPreLevelCardViews();
        Data.SaveData();
        PlayClickSound();
    }

    private BoosterCardView GetPreLevelCardView(int slotIndex)
    {
        return slotIndex switch
        {
            0 => preLevelCardSlot1,
            1 => preLevelCardSlot2,
            2 => preLevelCardSlot3,
            _ => null
        };
    }

    private static int GetPreLevelCardAmount(CardType cardType)
    {
        return Data.PlayerData != null
            ? Data.PlayerData.GetPreLevelCardAmount(cardType)
            : 0;
    }

    private static void SetPreLevelCardAmount(CardType cardType, int amount)
    {
        Data.PlayerData?.SetPreLevelCardAmount(cardType, amount);
    }

    private static bool IsTimedPreLevelCardActive(CardType cardType)
    {
        return Data.PlayerData != null &&
               Data.PlayerData.IsTimedPreLevelCardActive(cardType);
    }

    private static string GetTimedPreLevelCardText(CardType cardType)
    {
        if (!IsTimedPreLevelCardActive(cardType))
            return null;

        System.TimeSpan remaining =
            Data.PlayerData.GetTimedPreLevelCardRemaining(cardType);
        if (remaining.TotalHours >= 1d)
            return $"{Mathf.CeilToInt((float)remaining.TotalHours)}h";

        return $"{Mathf.Max(1, Mathf.CeilToInt((float)remaining.TotalMinutes))}m";
    }

    private void OnPreLevelCardAmountChanged(CardType changedCardType)
    {
        for (int index = 0; index < _visiblePreLevelCards.Count; index++)
        {
            if (_visiblePreLevelCards[index].cardType == changedCardType)
            {
                RefreshPreLevelCardViews();
                return;
            }
        }
    }

    protected void RefundSelectedPreLevelCards()
    {
        bool refunded = false;

        for (int index = 0; index < _visiblePreLevelCards.Count; index++)
        {
            BoosterCardView view = GetPreLevelCardView(index);
            if (view == null || !view.IsSelected)
                continue;

            CardType cardType = _visiblePreLevelCards[index].cardType;
            view.IsSelected = false;
            if (!_selectedTimedPreLevelCards.Remove(cardType))
            {
                SetPreLevelCardAmount(
                    cardType,
                    GetPreLevelCardAmount(cardType) + 1
                );
            }
            refunded = true;
        }

        if (refunded)
            Data.SaveData();
    }

    protected void QueueSelectedPreLevelCards()
    {
        List<CardType> selectedCards = new List<CardType>();

        for (int index = 0; index < _visiblePreLevelCards.Count; index++)
        {
            BoosterCardView view = GetPreLevelCardView(index);
            if (view == null || !view.IsSelected)
                continue;

            selectedCards.Add(_visiblePreLevelCards[index].cardType);
            _selectedTimedPreLevelCards.Remove(
                _visiblePreLevelCards[index].cardType);
            view.IsSelected = false;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.QueuePreLevelCards(selectedCards);
    }

    private void OpenShopTab()
    {
        PopupController popupController = PopupController.Instance;
        if (popupController == null)
            return;

        RefundSelectedPreLevelCards();
        Observer.Notify?.Invoke("Shop is unavailable.", Vector3.zero);
    }

    private void HidePreLevelCardSlots()
    {
        _visiblePreLevelCards.Clear();
        _selectedTimedPreLevelCards.Clear();
        RefreshPreLevelCardViews();
    }

    private void DisplayPreLevelCards(Level level)
    {
        BoosterCardView[] cardViews =
        {
            preLevelCardSlot1,
            preLevelCardSlot2,
            preLevelCardSlot3
        };

        _visiblePreLevelCards.Clear();

        if (level != null && level.CardConfig != null &&
            level.CardConfig.preLevelCards != null)
        {
            foreach (PreLevelCardData card in level.CardConfig.preLevelCards)
            {
                // Wind Card is configured but will be enabled in a later pass.
                // Reserve the three available slots for the other pre-level cards.
                if (card == null || card.cardType == CardType.WildCard)
                    continue;

                _visiblePreLevelCards.Add(card);
                if (_visiblePreLevelCards.Count == cardViews.Length)
                    break;
            }
        }

        if (giveOnePreLevelCardForTesting && Data.PlayerData != null)
        {
            for (int index = 0; index < _visiblePreLevelCards.Count; index++)
            {
                CardType cardType = _visiblePreLevelCards[index].cardType;
                if (GetPreLevelCardAmount(cardType) <= 0)
                {
                    SetPreLevelCardAmount(cardType, 1);
                }
            }
        }

        RefreshPreLevelCardViews(cardViews);
    }

    private void RefreshPreLevelCardViews()
    {
        RefreshPreLevelCardViews(new[]
        {
            preLevelCardSlot1,
            preLevelCardSlot2,
            preLevelCardSlot3
        });
    }

    private void RefreshPreLevelCardViews(BoosterCardView[] cardViews)
    {
        bool isUnlocked = ArePreLevelBoostersUnlocked();

        for (int index = 0; index < cardViews.Length; index++)
        {
            BoosterCardView view = cardViews[index];
            if (view == null)
                continue;

            if (index < _visiblePreLevelCards.Count)
            {
                PreLevelCardData card = _visiblePreLevelCards[index];
                view.ApplyPreLevelCard(
                    card.preLevelIcon,
                    GetPreLevelCardAmount(card.cardType),
                    isUnlocked,
                    GetTimedPreLevelCardText(card.cardType)
                );
            }
            else
            {
                view.IsSelected = false;
                view.ApplyPreLevelCard(null, 0, isUnlocked);
            }
        }
    }

    private void UpdateMiniGameBoartVisibility()
    {
        if (miniGameBoart == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Mini_Game_Boart")
                {
                    miniGameBoart = child.gameObject;
                    break;
                }
            }
        }

        if (miniGameBoart != null)
        {
            PopupHome popupHome = PopupController.Instance != null
                ? PopupController.Instance.Get<PopupHome>() as PopupHome
                : null;

            bool isBoartRaceActive = popupHome != null && popupHome.IsBoartRaceActive();
            miniGameBoart.SetActive(isBoartRaceActive);
        }
    }

    private void OnLevelChanged(int level)
    {
        RefreshPlayButtonVisibility(level);
        RefreshPreLevelCardViews();
    }

    private void RefreshPlayButtonVisibility(int level)
    {
        bool showMultiplePlayButtons =
            level >= MultiplePlayButtonsUnlockLevel;

        SetPlayButtonActive(playButton1AnimationTarget,
            !showMultiplePlayButtons);
        SetPlayButtonActive(playButton2AnimationTarget,
            showMultiplePlayButtons);
        SetPlayButtonActive(playButton3AnimationTarget,
            showMultiplePlayButtons);
    }

    private static void SetPlayButtonActive(Transform target, bool isActive)
    {
        if (target != null && target.gameObject.activeSelf != isActive)
            target.gameObject.SetActive(isActive);
    }

    private static bool ArePreLevelBoostersUnlocked()
    {
        return Data.PlayerData == null ||
               Data.PlayerData.CurrentLevelIndex >= PreLevelBoosterUnlockLevel;
    }


    private void CacheIntroAnimationScales()
    {
        if (_introScalesCached)
            return;

        if (playAnimationTarget != null)
            _playBaseScale = playAnimationTarget.localScale;

        Transform[] playButtons = GetPlayButtonAnimationTargets();
        _playButtonBaseScales = new Vector3[playButtons.Length];

        for (int index = 0; index < playButtons.Length; index++)
        {
            Transform playButtonTarget = playButtons[index];
            _playButtonBaseScales[index] = playButtonTarget != null
                ? playButtonTarget.localScale
                : Vector3.one;
        }

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

        Transform[] playButtons = GetPlayButtonAnimationTargets();
        bool hasActivePlayButton = false;

        for (int index = 0; index < playButtons.Length; index++)
        {
            Transform playButtonTarget = playButtons[index];
            if (playButtonTarget == null ||
                !playButtonTarget.gameObject.activeInHierarchy)
                continue;

            hasActivePlayButton = true;
            playButtonTarget.localScale =
                _playButtonBaseScales[index] * playShrinkScale;
        }

        if (!hasActivePlayButton)
            return;

        _introSequence = Sequence.Create(useUnscaledTime: true)
            .ChainDelay(playStartDelay);

        float animationStartTime = _introSequence.duration;

        for (int index = 0; index < playButtons.Length; index++)
        {
            Transform playButtonTarget = playButtons[index];
            if (playButtonTarget == null ||
                !playButtonTarget.gameObject.activeInHierarchy)
                continue;

            Vector3 baseScale = _playButtonBaseScales[index];
            _introSequence.Insert(
                animationStartTime,
                Tween.Scale(
                    playButtonTarget,
                    baseScale * playShrinkScale,
                    baseScale * playOvershootScale,
                    playOvershootDuration,
                    Ease.OutQuad));
            _introSequence.Insert(
                animationStartTime + playOvershootDuration,
                Tween.Scale(
                    playButtonTarget,
                    baseScale * playOvershootScale,
                    baseScale,
                    playSettleDuration,
                    Ease.OutQuad));
        }

        int boosterCount = boosterAnimationTargets != null
            ? boosterAnimationTargets.Length
            : 0;

        for (int index = 0; index < boosterCount; index++)
        {
            Transform booster = boosterAnimationTargets[index];
            if (booster == null)
                continue;

            Vector3 baseScale = _boosterBaseScales[index];
            booster.localScale = baseScale * boosterShrinkScale;

            _introSequence.Insert(
                animationStartTime,
                Tween.Scale(
                    booster,
                    baseScale * boosterShrinkScale,
                    baseScale * boosterOvershootScale,
                    boosterOvershootDuration,
                    Ease.OutQuad));
            _introSequence.Insert(
                animationStartTime + boosterOvershootDuration,
                Tween.Scale(
                    booster,
                    baseScale * boosterOvershootScale,
                    baseScale,
                    boosterSettleDuration,
                    Ease.OutQuad));
        }

        _introSequence.OnComplete(() =>
        {
            ResetIntroAnimationScales();
            StartPlayButton3Loop();
        });
    }

    private void StopIntroAnimation()
    {
        if (_introSequence.isAlive)
            _introSequence.Stop();

        if (_playButton3LoopCoroutine != null)
        {
            StopCoroutine(_playButton3LoopCoroutine);
            _playButton3LoopCoroutine = null;
        }

        ResetIntroAnimationScales();
    }

    private void StartPlayButton3Loop()
    {
        if (playButton3AnimationTarget == null ||
            !playButton3AnimationTarget.gameObject.activeInHierarchy)
            return;

        _playButton3LoopCoroutine =
            StartCoroutine(PlayButton3LoopRoutine());
    }

    private IEnumerator PlayButton3LoopRoutine()
    {
        Vector3 baseScale = GetPlayButtonBaseScale(2);
        float elapsed = 0f;
        float halfDuration = Mathf.Max(.01f, playButton3LoopHalfDuration);
        float middleScale =
            (playButton3LoopMinScale + playButton3LoopMaxScale) * .5f;
        float scaleAmplitude =
            (playButton3LoopMaxScale - playButton3LoopMinScale) * .5f;

        while (playButton3AnimationTarget != null &&
               playButton3AnimationTarget.gameObject.activeInHierarchy)
        {
            elapsed += Time.unscaledDeltaTime;
            float scale = middleScale + scaleAmplitude *
                Mathf.Sin(elapsed * Mathf.PI / halfDuration);
            playButton3AnimationTarget.localScale = baseScale * scale;
            yield return null;
        }

        _playButton3LoopCoroutine = null;
    }

    private void ResetIntroAnimationScales()
    {
        if (playAnimationTarget != null)
            playAnimationTarget.localScale = _playBaseScale;

        Transform[] playButtons = GetPlayButtonAnimationTargets();
        if (_playButtonBaseScales != null)
        {
            int playButtonCount = Mathf.Min(
                playButtons.Length,
                _playButtonBaseScales.Length);

            for (int index = 0; index < playButtonCount; index++)
            {
                if (playButtons[index] != null)
                    playButtons[index].localScale =
                        _playButtonBaseScales[index];
            }
        }

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

    private Transform[] GetPlayButtonAnimationTargets()
    {
        if (playButton1AnimationTarget != null ||
            playButton2AnimationTarget != null ||
            playButton3AnimationTarget != null)
        {
            return new[]
            {
                playButton1AnimationTarget,
                playButton2AnimationTarget,
                playButton3AnimationTarget
            };
        }

        return new[] { playAnimationTarget };
    }

    private Vector3 GetPlayButtonBaseScale(int index)
    {
        return _playButtonBaseScales != null &&
               index >= 0 && index < _playButtonBaseScales.Length
            ? _playButtonBaseScales[index]
            : Vector3.one;
    }

    public virtual void OnClickClose()
    {
        PlayMenuBarSound();
        RefundSelectedPreLevelCards();
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
        PlayMenuBarSound();
        QueueSelectedPreLevelCards();
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
            targetGroup.LeanPoolClear();

        _targetItems.Clear();

        if (targetGroup == null || targetItemPrefab == null)
            return;

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

        if (setupVersion != _targetSetupVersion ||
            level == null ||
            level.CardTargets == null)
        {
            return;
        }

        foreach (CardTarget target in level.CardTargets)
        {
            IngameTargetItem item =
                LeanPool.Spawn(targetItemPrefab, targetGroup);

            if (item == null)
                continue;

            item.SetDisplayScale(targetItemScale);

            Sprite targetSprite =
                level.TargetConfig != null
                    ? level.TargetConfig.GetTargetSprite(target.cardType)
                    : null;

            if (targetSprite == null)
            {
                Debug.LogWarning(
                    $"[PopupBooster] Khong tim thay target sprite cho " +
                    $"{target.cardType} trong TargetConfig cua {level.name}.");
            }

            item.Setup(target, targetSprite);
            _targetItems.Add(item);
        }

        Dictionary<CardType, int> currentCounts =
            level.GetCurrentCardCounts();

        foreach (IngameTargetItem item in _targetItems)
        {
            if (item != null)
                item.UpdateProgress(currentCounts);
        }

        DisplayPreLevelCards(level);
    }

}
