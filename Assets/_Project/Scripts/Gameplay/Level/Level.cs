using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using CustomTween;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
using CustomInspector;
#endif

[System.Serializable]
public class CardTarget
{
    public CardType cardType;
    public int count;

    private bool _isCompleted;

    public bool IsCompleted => _isCompleted;

    public void MarkCompleted()
    {
        _isCompleted = true;
    }

    public void ResetProgress()
    {
        _isCompleted = false;
    }
}

public class Level : MonoBehaviour
{
    private const int FreeMovesBoosterAmount = 5;

    [Header("Win Condition")]
    [SerializeField]
    private List<CardTarget> cardTargets =
        new List<CardTarget>();

    [Header("Deal Settings")]
    [SerializeField] private int maxDealCount = 5;
    [Tooltip("Lowest number card that can be dealt. Set to 0 to use the current board minimum. The deal still stays below the highest card on the board.")]
    [SerializeField, Min(0)] private int minDealCardValue;

    [Header("Move Settings")]
    [SerializeField, Min(1)] private int maxMoveCount = 30;

    [Header("Layer")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private LayerMask highLightLayer;

    [Header("Card")]
    [SerializeField] private Card cardPrefab;
    [SerializeField] private CardConfig cardConfig;
    [SerializeField] private MergeGoldRewardConfig mergeGoldRewardConfig;
    [SerializeField] private TargetConfig targetConfig;
    [SerializeField] private GameObject preLevelExtraTrayPrefab;

    [Header("Obstacle - Downgrade Card")]
    [SerializeField, Min(0.1f)]
    private float downgradeEffectDuration = 0.7f;
    [SerializeField, Min(0.1f)]
    private float downgradeBlinkDuration = 1f;
    [SerializeField, Range(1, 8)]
    private int downgradeBlinkCount = 2;
    [SerializeField]
    private Color downgradeBlinkColor = new Color(1f, 0.12f, 0.08f, 1f);

    [Header("Magic Swap Selection")]
    [SerializeField, Min(0f)]
    private float magicSelectionTouchPadding = 24f;

    [SerializeField]
    private bool magicSelectionHaptic = true;

    [Header("Booster Focus Dim")]
    [SerializeField, Range(0f, 1f)]
    private float boosterDimAlpha = 0.8f;

    [SerializeField, Min(0.01f)]
    private float boosterDimFadeDuration = 0.07f;

    [SerializeField]
    private int boosterDimSortingOrder = 5000;

    [SerializeField, Min(1)]
    private int boosterCardSortingOffset = 6000;

    [Header("Lighter Burn Effect")]
    [SerializeField] private GameObject lighterBoosterPrefab;
    [SerializeField] private Vector3 lighterBoosterStartOffset =
        Vector3.zero;
    [SerializeField] private Vector3 lighterBoosterEndOffset =
        new Vector3(0f, 0.12f, 0f);
    [SerializeField, Min(0f)]
    private float lighterBoosterIgnitionDuration = 0.3f;
    [SerializeField, Min(0.01f)]
    private float lighterBoosterLiftDuration = 0.68f;
    [SerializeField, Min(1)]
    private int lighterBoosterSortingOffset = 5;

    [Header("Booster Tray Pulse")]
    [SerializeField, Range(1f, 1.25f)]
    private float boosterTrayPulseUpScale = 1.1f;

    [SerializeField, Range(0.8f, 1f)]
    private float boosterTrayPulseDownScale = 0.95f;

    [SerializeField, Min(0.01f)]
    private float boosterTrayPulseUpDuration = 0.1f;

    [SerializeField, Min(0.01f)]
    private float boosterTrayPulseDownDuration = 0.08f;

    [SerializeField, Min(0.01f)]
    private float boosterTrayPulseSettleDuration = 0.1f;

    [Header("Card Slot Holders")]
    [SerializeField]
    private List<CardSlotHolder> cardSlotHolders =
        new List<CardSlotHolder>();

    [Header("Win Card Cleanup")]
    [SerializeField, Min(0.05f)] private float winCleanupFlipDuration = 0.2f;
    [SerializeField, Min(0f)] private float winCleanupFlipStagger = 0.035f;
    [SerializeField, Min(0.05f)] private float winCleanupDisappearDuration = 0.16f;
    [SerializeField, Min(0f)] private float winCleanupDisappearStagger = 0.055f;
    [SerializeField, Range(1, 4)] private int winCleanupVisualCoinsPerCard = 1;

    [Header("Table Entrance")]
    [SerializeField] private Transform tableIngame;
    [SerializeField] private Transform tableEntranceTrayRoot;
    [SerializeField, Min(0f)] private float tableEntranceOffset = 12f;
    [SerializeField, Min(0f)] private float tableEntranceDelay = 0f;
    [SerializeField, Min(0.01f)] private float tableEntranceDuration = 0.26f;
    [SerializeField, Min(0f)] private float tableEntranceOvershoot = 0.15f;

    [Header("Move Card")]
    [SerializeField] private CardMoveSettings cardMove = new CardMoveSettings();

    [Header("Merge - Flip To Back")]
    [SerializeField, Min(0.05f)] private float mergeFlipDuration = 0.2f;
    [SerializeField, Min(0f)] private float mergeFlipScaleUp = 1.1f;
    [SerializeField, Min(0f)] private float mergeFlipStagger = 0.025f;
    [SerializeField, Min(0f)] private float mergeFlipLiftDuration = 0.05f;
    [SerializeField, Min(0f)] private float mergeFlipSettleDuration = 0.09f;
    [SerializeField] private float cardFlipRotateZ = 15f;
    [SerializeField, Range(0f, 160f)] private float mergeFlipCurveAngle = 120f;
    [SerializeField, Range(0.3f, 1f)] private float mergeFlipCurveShade = 0.72f;
    [SerializeField, Range(0f, 0.4f)] private float mergeFlipBendStretch = 0.05f;
    [SerializeField, Range(0f, 0.3f)] private float mergeFlipSettleSquash = 0.06f;

    [SerializeField] private AnimationCurve mergeFlipCurve =
        CardDealSettings.BuildCurve(
            new[] { 0f, 0.15f, 0.3f, 0.5f, 0.7f, 0.85f, 1f },
            new[] { 0f, 0.06f, 0.22f, 0.5f, 0.78f, 0.94f, 1f }
        );

    [Header("Merge - Compress & Impact")]
    [SerializeField, Min(0.05f)] private float mergeCompressDuration = 0.2f;
    [SerializeField, Min(0f)] private float mergeCompressCardDelay = 0.05f;
    [SerializeField] private Vector3 mergeCompressCenterOffset = new Vector3(0f, -0.2f, 0f);
    [SerializeField] private Vector3 mergeCompressShadowScaleUp = new Vector3(1f, 1f, 1f);
    [SerializeField, Range(1f, 4f)] private float mergeCompressFallPower = 2f;
    [SerializeField, Min(0f)] private float mergeStackSpacing = 0.012f;
    [SerializeField] private Vector3 mergeCompressScale = Vector3.one;
    [SerializeField, Min(0f)] private float mergeCompressPopScaleDuration = 0.15f;
    [SerializeField] private Vector3 mergeCompressPopScale = new Vector3(1.2f, 1.2f, 1.2f);

    [Header("Merge - Result")]
    [SerializeField] private float mergeResultStackSpacing = 0.012f;
    [SerializeField] private float mergeResultPreFlightDuration = 0.15f; 
    [SerializeField] private Vector3 mergeResultPreFlightScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField, Min(0.05f)] private float mergeResultFlightDuration = 0.35f;
    [SerializeField, Min(0f)] private float mergeResultFlightStagger = 0.05f;
    [SerializeField, Min(0f)] private float mergeResultFlightHeight = 0.45f;
    [SerializeField] private float mergeResultFlightRotateY = 180f;
    [SerializeField, Min(0f)] private float mergeResultSideOffset = 0.22f;
    [SerializeField, Range(0f, 0.5f)] private float mergeResultFlightScale = 0.22f;
    [SerializeField, Range(0.25f, 2f)] private float mergeResultRotationRounds = 1f;
    [SerializeField, Min(0.05f)] private float mergeResultLandingDuration = 0.12f;
    [SerializeField] private Vector2 mergeResultLandingScale = new Vector2(1.12f, 0.88f);
    [SerializeField, Min(1)] private int mergeSortingOrderOffset = 40;

    [Header("Merge Flight Curves - Result")]
    [SerializeField] private AnimationCurve mergeFlightCurveFirst = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve mergeFlightCurveLast = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve mergeResultRotationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Merge - Target Celebration")]
    [SerializeField] private bool enableMergeTargetCelebration = true;
    [Tooltip("Screen position used by the target card. (0.5, 0.5) is the center.")]
    [SerializeField] private Vector2 mergeTargetCenterViewport =
        new Vector2(0.5f, 0.5f);
    [SerializeField, Min(0.01f)] private float mergeTargetFlyOutDuration = 0.38f;
    [Tooltip("So vong la bai xoay theo truc BD khi dung o giua man hinh.")]
    [SerializeField, Min(0f)] private float mergeTargetSpinTurns = 1f;
    [Tooltip("Toc do xoay tinh bang so vong moi giay. Vi du 4 = 4 vong/giay.")]
    [SerializeField, Min(0.01f)] private float mergeTargetSpinSpeed = 2.4f;
    [SerializeField, Min(0.01f)] private float mergeTargetReturnDuration = 0.34f;
    [SerializeField, Min(1f)] private float mergeTargetPeakScale = 2.1f;
    [SerializeField, Min(0f)] private float mergeTargetArcHeight = 0.75f;
    [SerializeField] private float mergeTargetArcSideOffset = 0.2f;
    [SerializeField, Min(1)] private int mergeTargetSortingOrder = 10000;
    [Tooltip("Keo object FX da nam trong Hierarchy vao day va de inactive. Object se duoc bat khi la target dat scale lon nhat.")]
    [SerializeField] private GameObject mergeTargetPeakFx;
    [SerializeField] private Vector3 mergeTargetPeakFxOffset = Vector3.zero;
    [SerializeField, Min(0.05f)] private float mergeTargetPeakFxLifetime = 1.5f;
    [Tooltip("Khoang cach sorting cua FX nam duoi la bai. Gia tri luon duoc tinh la so duong.")]
    [SerializeField] private int mergeTargetPeakFxSortingOffset = 5;

    public Card CardPrefab => cardPrefab;
    public CardConfig CardConfig => cardConfig;
    public TargetConfig TargetConfig => targetConfig;
    public List<CardSlotHolder> CardSlotHolders => cardSlotHolders;
    public List<CardTarget> CardTargets => cardTargets;
    public int MinDealCardValue => Mathf.Clamp(minDealCardValue, 0, 20);
    public InitialBoardDealAnimator InitialBoardDealAnimator =>
        _initialBoardDealAnimator;
    public bool IsTableEntrancePlaying =>
        _tableEntranceWaitingForTransition ||
        _tableEntranceRoutine != null;

    public Card GetCardPrefab(CardType cardType)
    {
        Card configuredPrefab = cardConfig != null
            ? cardConfig.GetCardPrefab(cardType)
            : null;
        return configuredPrefab != null ? configuredPrefab : cardPrefab;
    }

    public int CurrentDealCount { get; private set; }
    public int CurrentMoveCount { get; private set; }
    public int RemainingFreeMoveCount { get; private set; }

    public Action OnBoardChanged;
    public Action<int> OnDealCountChanged;
    public Action<int> OnMoveCountChanged;
    public Action<int> OnMoveBonusGranted;
    public Action<CardSlotHolder, int> OnStackCompleted;

    private LayerMask _defaultTargetLayer;
    private Camera _mainCamera;
    private CardDesk _cardDesk;
    private InitialBoardDealAnimator _initialBoardDealAnimator;
    private Vector3 _tableIngameShownPosition;
    private Vector3 _tableEntranceTrayRootShownPosition;
    private Coroutine _tableEntranceRoutine;
    private bool _tableEntranceWaitingForTransition;
    private bool _tableEntranceHasPlayed;
    private Coroutine _winCleanupRoutine;
    private Action _winCleanupCompleted;
    private int _winCleanupPendingCoinFx;

    private CardSlotHolder _selectedHolder;
    private List<Card> _selectedCards = new List<Card>();
    private BoosterType? _activeBooster;
    private Lean.Touch.LeanFinger _magicSelectionFinger;
    private CardDesk _pressedDesk;
    private CardSlotHolder _magicSelectionDragHolder;
    private Card _magicSelectedSourceCard;
    private SpriteRenderer _boosterDimRenderer;
    private Sprite _boosterDimSprite;
    private Coroutine _boosterDimFadeRoutine;
    private Coroutine _boosterTrayPulseRoutine;
    private readonly Dictionary<SpriteRenderer, int>
        _boosterCardSortingOrders =
            new Dictionary<SpriteRenderer, int>();
    private readonly Dictionary<Transform, Vector3>
        _boosterTrayOriginalScales =
            new Dictionary<Transform, Vector3>();
    private Vector2 _magicSelectionAlongAxis = Vector2.up;
    private Vector2 _magicSelectionAcrossAxis = Vector2.right;
    private readonly List<MagicCardHitArea> _magicCardHitAreas =
        new List<MagicCardHitArea>(CardSlotHolder.MAX_SLOTS);

    private readonly List<Card> _cardsToMoveBuffer = new List<Card>(CardSlotHolder.MAX_SLOTS);
    private readonly List<CardSlot> _targetSlotsBuffer = new List<CardSlot>(CardSlotHolder.MAX_SLOTS);
    private readonly List<SpriteRenderer> _activeFlightShadows =
        new List<SpriteRenderer>();

    private bool _isMovingCardCluster;
    private int _activeMergeTargetCelebrations;
    private Coroutine _mergeTargetPeakFxDisableRoutine;
    private readonly Dictionary<Renderer, int>
        _mergeTargetPeakFxAuthoredSortingOrders =
            new Dictionary<Renderer, int>();
    private readonly HashSet<CardSlotHolder> _movingHolders =
        new HashSet<CardSlotHolder>();
    private readonly HashSet<CardSlotHolder> _mergingHolders =
        new HashSet<CardSlotHolder>();

    private sealed class MagnetCandidate
    {
        public Card Card;
        public CardSlotHolder Holder;
        public float SqrDistance;
    }

    private sealed class MagicCardHitArea
    {
        public Card Card;
        public float MinAlong;
        public float MaxAlong;
        public float MinAcross;
        public float MaxAcross;
    }

    private sealed class WinCleanupCardState
    {
        public Card Card;
        public CardSlot Slot;
        public CardSlotHolder Holder;
        public Vector3 BaseScale;
        public bool Removed;
    }

    private enum DowngradeInteraction
    {
        None,
        MoveOntoDowngrade,
        MoveDowngradeOntoCards
    }

    private sealed class DowngradeEffectContext
    {
        public Card ObstacleCard;
        public CardSlotHolder TargetHolder;
        public List<Card> AffectedCards;
    }

    private sealed class DowngradeCardVisualState
    {
        public Card Card;
        public Vector3 StartPosition;
        public Vector3 GatherPosition;
        public Vector3 FinalPosition;
        public Quaternion StartRotation;
        public Quaternion FinalRotation;
        public bool IsObstacle;
    }

    private sealed class WildBindContext
    {
        public Card Card;
        public CardType ResolvedType;
        public Sprite ResolvedSprite;
    }

    public CardSlotHolder SelectedHolder => _selectedHolder;
    public List<Card> SelectedCards => _selectedCards;
    public BoosterType? ActiveBooster => _activeBooster;
    public bool IsAnimatingCards =>
        _isMovingCardCluster ||
        _activeMergeTargetCelebrations > 0 ||
        _movingHolders.Count > 0 ||
        _mergingHolders.Count > 0 ||
        (_initialBoardDealAnimator != null &&
         _initialBoardDealAnimator.IsBusy) ||
        (_cardDesk != null && _cardDesk.IsReceivingCards);

    private bool IsCardInputGloballyBlocked =>
        _activeMergeTargetCelebrations > 0 ||
        (_initialBoardDealAnimator != null &&
         _initialBoardDealAnimator.IsBusy) ||
        (_cardDesk != null && _cardDesk.IsReceivingCards);

    private bool IsHolderAnimating(CardSlotHolder holder)
    {
        return holder != null &&
               (_movingHolders.Contains(holder) ||
                _mergingHolders.Contains(holder));
    }

    private void Awake()
    {
        Card.SetThemeConfig(cardConfig);

        Transform environment = transform.Find("Environment");
        if (environment != null)
            environment.gameObject.SetActive(true);
        foreach (Camera levelCamera in GetComponentsInChildren<Camera>(true))
        {
            if (levelCamera.CompareTag("MainCamera"))
            {
                _mainCamera = levelCamera;
                break;
            }
        }
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        CacheTableEntranceTarget();
        _cardDesk = GetComponentInChildren<CardDesk>(true);
        _defaultTargetLayer = targetLayer;
        CurrentDealCount = maxDealCount;
        CurrentMoveCount = Mathf.Max(1, maxMoveCount);
        ResetTargetProgress();

        RefreshCardSlotHolders();
        InitializeKingCardCountdowns();
        _initialBoardDealAnimator =
            GetComponent<InitialBoardDealAnimator>();
        if (_initialBoardDealAnimator == null)
        {
            _initialBoardDealAnimator =
                gameObject.AddComponent<InitialBoardDealAnimator>();
        }
        _initialBoardDealAnimator.Initialize(this);
        ApplyCardSlotLayoutByName();
        AttachBonusComponents();
        _initialBoardDealAnimator.Prepare();
        OnBoardChanged += HandleBoardChanged;
    }

    public Dictionary<CardType, int> GetPreLevelCardCounts()
    {
        Dictionary<CardType, int> counts =
            new Dictionary<CardType, int>();

        foreach (CardSlotHolder holder in cardSlotHolders)
        {
            if (holder == null || holder.CardSlots == null)
                continue;

            foreach (CardSlot slot in holder.CardSlots)
            {
                if (slot == null || slot.Card == null ||
                    cardConfig == null ||
                    !cardConfig.IsPreLevelCard(slot.Card.CardType))
                {
                    continue;
                }

                counts.TryGetValue(slot.Card.CardType, out int count);
                counts[slot.Card.CardType] = count + 1;
            }
        }

        return counts;
    }

    public void ApplySelectedPreLevelCards(IEnumerable<CardType> cardTypes)
    {
        if (cardTypes == null)
            return;

        foreach (CardType cardType in cardTypes)
        {
            if (cardType == CardType.WildCard && !SpawnSelectedWildCard())
            {
                RefundSelectedPreLevelCard(
                    cardType,
                    "No empty tray slot is available for Wild Card."
                );
            }
        }
    }

    private bool SpawnSelectedWildCard()
    {
        List<CardSlotHolder> availableHolders =
            new List<CardSlotHolder>();

        for (int i = 0; i < cardSlotHolders.Count; i++)
        {
            CardSlotHolder holder = cardSlotHolders[i];
            if (holder != null && !holder.IsLocked &&
                !holder.IsBlockedByIron &&
                holder.GetEmptySlotCount() > 0 &&
                !HasSpecialHolderRule(holder))
            {
                availableHolders.Add(holder);
            }
        }

        Card wildCardPrefab = GetCardPrefab(CardType.WildCard);
        if (availableHolders.Count == 0 || wildCardPrefab == null)
            return false;

        CardSlotHolder targetHolder = availableHolders[
            UnityEngine.Random.Range(0, availableHolders.Count)
        ];
        List<CardSlot> emptySlots = targetHolder.GetEmptySlots(1);
        if (emptySlots.Count == 0 || emptySlots[0] == null)
            return false;

        CardData data = cardConfig != null
            ? cardConfig.GetPreLevelCardData(CardType.WildCard)
            : null;
        Card wildCard = Instantiate(wildCardPrefab, emptySlots[0].transform);
        wildCard.transform.localPosition = Vector3.zero;
        wildCard.transform.localRotation = Quaternion.identity;
        wildCard.transform.localScale = Vector3.zero;
        wildCard.SetAsWild(data != null ? data.icon : null);
        emptySlots[0].SetCard(wildCard);
        targetHolder.NotifyCardsChanged();
        targetHolder.PlayActiveSoftEffect();
        wildCard.PlaySpawnEffect();

        Tween.Scale(
            wildCard.transform,
            Vector3.zero,
            Vector3.one,
            0.35f,
            Ease.OutBack
        );

        OnBoardChanged?.Invoke();
        return true;
    }

    private static bool HasSpecialHolderRule(CardSlotHolder holder)
    {
        if (holder == null)
            return false;

        foreach (MonoBehaviour behaviour in holder.GetComponents<MonoBehaviour>())
        {
            if (behaviour is ICardSlotHolderRule)
                return true;
        }

        return false;
    }

    private static void RefundSelectedPreLevelCard(
        CardType cardType,
        string message)
    {
        if (Data.PlayerData != null)
        {
            Data.PlayerData.SetPreLevelCardAmount(
                cardType,
                Data.PlayerData.GetPreLevelCardAmount(cardType) + 1
            );
            Data.SaveData();
        }

        Observer.Notify?.Invoke(message, Vector3.zero);
    }

    private bool TryAddExtraTray()
    {
        if (preLevelExtraTrayPrefab != null)
        {
            Transform trayParent = transform.Find("SlotsRoot");
            if (trayParent == null)
                trayParent = transform;

            GameObject trayObject = Instantiate(
                preLevelExtraTrayPrefab,
                trayParent
            );
            trayObject.name = "CardSlotHolderExtraTray";

            CardSlotHolder holder =
                trayObject.GetComponentInChildren<CardSlotHolder>(true);
            CardSlotHolderGold goldHolder =
                trayObject.GetComponentInChildren<CardSlotHolderGold>(true);

            if (holder != null && goldHolder != null)
            {
                if (!cardSlotHolders.Contains(holder))
                    cardSlotHolders.Add(holder);

                TrayLayoutGroup layout =
                    trayParent.GetComponent<TrayLayoutGroup>();
                if (layout != null)
                    layout.RebuildLayout();

                return goldHolder.UnlockByInGameBooster();
            }

            Destroy(trayObject);
        }

        for (int i = 0; i < cardSlotHolders.Count; i++)
        {
            CardSlotHolder holder = cardSlotHolders[i];
            if (holder == null || !holder.IsLocked)
                continue;

            CardSlotHolderGold goldHolder =
                holder.GetComponent<CardSlotHolderGold>();

            if (goldHolder != null &&
                goldHolder.UnlockByInGameBooster())
            {
                return true;
            }
        }

        return false;
    }

    private void HandleBoardChanged()
    {
        CheckWinCondition();

        if (GameManager.Instance == null ||
            GameManager.Instance.gameState != GameState.PlayingGame)
        {
            return;
        }
        
        if (CurrentMoveCount <= 0)
        {
            GameManager.Instance.ShowContinueWarning();
        }
    }

    private void ResetTargetProgress()
    {
        if (cardTargets == null)
            return;

        for (int i = 0; i < cardTargets.Count; i++)
        {
            if (cardTargets[i] != null)
                cardTargets[i].ResetProgress();
        }
    }

    private void RefreshCardSlotHolders()
    {
        cardSlotHolders = new List<CardSlotHolder>(
            GetComponentsInChildren<CardSlotHolder>(true)
        );

        cardSlotHolders.RemoveAll(holder => holder == null);

    }

    private void ApplyCardSlotLayoutByName()
    {
        if (cardSlotHolders == null ||
            cardSlotHolders.Count == 0)
        {
            return;
        }

        Dictionary<string, Transform> layoutTemplates =
            new Dictionary<string, Transform>();

        for (int i = 0; i < cardSlotHolders.Count; i++)
        {
            CardSlotHolder holder = cardSlotHolders[i];
            string layoutKey =
                holder != null
                    ? GetCardSlotLayoutKey(holder.name)
                    : string.Empty;

            if (holder == null ||
                string.IsNullOrEmpty(layoutKey))
            {
                continue;
            }

            if (!layoutTemplates.ContainsKey(layoutKey))
            {
                layoutTemplates.Add(
                    layoutKey,
                    holder.transform
                );
            }
        }

        for (int i = 0; i < cardSlotHolders.Count; i++)
        {
            CardSlotHolder holder = cardSlotHolders[i];
            string layoutKey =
                holder != null
                    ? GetCardSlotLayoutKey(holder.name)
                    : string.Empty;

            if (holder == null ||
                string.IsNullOrEmpty(layoutKey) ||
                !layoutTemplates.TryGetValue(
                    layoutKey,
                    out Transform template
                ) ||
                template == holder.transform)
            {
                continue;
            }

            holder.transform.localPosition =
                template.localPosition;
            holder.transform.localRotation =
                template.localRotation;
            holder.transform.localScale =
                template.localScale;
        }
    }

    private static string GetCardSlotLayoutKey(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) ||
            !objectName.StartsWith("CardSlotHolder_"))
        {
            return string.Empty;
        }

        int suffixStart = objectName.LastIndexOf(" (");

        if (suffixStart > 0 &&
            objectName.EndsWith(")"))
        {
            string suffix = objectName.Substring(
                suffixStart + 2,
                objectName.Length - suffixStart - 3
            );

            int suffixNumber;
            if (int.TryParse(suffix, out suffixNumber))
            {
                return objectName.Substring(0, suffixStart);
            }
        }

        return objectName;
    }

    private void AttachBonusComponents()
    {
        if (cardSlotHolders == null)
        {
            return;
        }

        for (int i = 0; i < cardSlotHolders.Count; i++)
        {
            CardSlotHolder holder = cardSlotHolders[i];

            if (holder == null ||
                holder.name != "CardSlotHolderBonus" ||
                holder.GetComponent<CardSlotHolderBonus>() != null)
            {
                continue;
            }

            holder.gameObject.AddComponent<CardSlotHolderBonus>();
        }
    }

    public bool ActivateBooster(BoosterType boosterType)
    {
        if (IsAnimatingCards || GetBoosterAmount(boosterType) <= 0)
            return false;

        if (boosterType == BoosterType.FreeMoves)
        {
            CancelActiveBooster();
            RemainingFreeMoveCount += FreeMovesBoosterAmount;
            CurrentMoveCount += FreeMovesBoosterAmount;
            OnMoveCountChanged?.Invoke(CurrentMoveCount);
            OnMoveBonusGranted?.Invoke(FreeMovesBoosterAmount);
            ConsumeBooster(boosterType);
            return true;
        }

        if (boosterType == BoosterType.ExtraTray)
        {
            CancelActiveBooster();
            if (!TryAddExtraTray())
            {
                Observer.Notify?.Invoke(
                    "No locked Gold Tray is available for Extra Tray.",
                    Vector3.zero
                );
                return false;
            }

            ConsumeBooster(boosterType);
            OnBoardChanged?.Invoke();
            return true;
        }

        if (boosterType != BoosterType.MagicMove &&
            boosterType != BoosterType.Magnet &&
            boosterType != BoosterType.Lighter)
        {
            return false;
        }

        if (_activeBooster == boosterType)
        {
            CancelActiveBooster();
            return false;
        }

        Deselect();
        _activeBooster = boosterType;

        Observer.ActiveBoosterChanged?.Invoke(_activeBooster);

        return true;
    }

    public void GrantMoveBonus(int amount)
    {
        RemainingFreeMoveCount += amount;
        CurrentMoveCount += amount;
        OnMoveCountChanged?.Invoke(CurrentMoveCount);
        OnMoveBonusGranted?.Invoke(amount);
    }

    public void CancelActiveBooster()
    {
        Deselect();
        SetBoosterDimVisible(false);
        _activeBooster = null;
        Observer.ActiveBoosterChanged?.Invoke(null);
    }

    public void SetBoosterFocusDimVisible(bool visible)
    {
        SetBoosterDimVisible(visible);
    }

    private void EnsureBoosterDim()
    {
        if (_boosterDimRenderer != null)
            return;

        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_mainCamera == null)
            return;

        GameObject dimObject = new GameObject(
            "BoosterFocusDim"
        );
        dimObject.transform.SetParent(
            _mainCamera.transform,
            false
        );
        dimObject.transform.localPosition =
            new Vector3(0f, 0f, 1f);

        _boosterDimSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );

        _boosterDimRenderer =
            dimObject.AddComponent<SpriteRenderer>();
        _boosterDimRenderer.sprite =
            _boosterDimSprite;
        _boosterDimRenderer.sortingOrder =
            boosterDimSortingOrder;
        _boosterDimRenderer.color =
            new Color(0f, 0f, 0f, 0f);
        _boosterDimRenderer.enabled = false;
        ResizeBoosterDim();
    }

    private void ResizeBoosterDim()
    {
        if (_boosterDimRenderer == null ||
            _mainCamera == null)
        {
            return;
        }

        float height;

        if (_mainCamera.orthographic)
        {
            height = _mainCamera.orthographicSize * 2f;
        }
        else
        {
            float distance = Mathf.Abs(
                _boosterDimRenderer.transform.localPosition.z
            );
            height = 2f * distance * Mathf.Tan(
                _mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad
            );
        }

        float width = height * _mainCamera.aspect;
        _boosterDimRenderer.transform.localScale =
            new Vector3(width, height, 1f);
    }

    private void SetBoosterCardsHighlighted(bool highlighted)
    {
        if (highlighted)
        {
            if (_boosterCardSortingOrders.Count > 0)
                return;

            CardSlotHolder[] holders =
                GetComponentsInChildren<CardSlotHolder>(true);

            for (int i = 0; i < holders.Length; i++)
            {
                CardSlotHolder holder = holders[i];
                if (holder == null ||
                    !holder.gameObject.activeInHierarchy ||
                    holder.IsLocked)
                {
                    continue;
                }

                AddBoosterHighlightedRenderers(holder.transform);
            }

            return;
        }

        foreach (KeyValuePair<SpriteRenderer, int> pair in
                 _boosterCardSortingOrders)
        {
            if (pair.Key != null)
                pair.Key.sortingOrder = pair.Value;
        }

        _boosterCardSortingOrders.Clear();
    }

    private void AddBoosterHighlightedRenderers(Transform root)
    {
        if (root == null)
            return;

        SpriteRenderer[] renderers =
            root.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null ||
                _boosterCardSortingOrders.ContainsKey(renderer))
            {
                continue;
            }

            _boosterCardSortingOrders.Add(renderer, renderer.sortingOrder);
            renderer.sortingOrder += boosterCardSortingOffset;
        }
    }

    private void PlayBoosterTrayPulse()
    {
        StopBoosterTrayPulse();

        CardSlotHolder[] holders =
            GetComponentsInChildren<CardSlotHolder>(true);

        for (int i = 0; i < holders.Length; i++)
        {
            CardSlotHolder holder = holders[i];
            if (holder == null ||
                !holder.gameObject.activeInHierarchy ||
                holder.IsLocked)
            {
                continue;
            }

            Transform holderTransform = holder.transform;
            if (!_boosterTrayOriginalScales.ContainsKey(holderTransform))
            {
                _boosterTrayOriginalScales.Add(
                    holderTransform,
                    holderTransform.localScale
                );
            }
        }

        if (_boosterTrayOriginalScales.Count > 0)
        {
            _boosterTrayPulseRoutine = StartCoroutine(
                AnimateBoosterTrayPulse()
            );
        }
    }

    private IEnumerator AnimateBoosterTrayPulse()
    {
        yield return AnimateBoosterTrayScale(
            1f,
            boosterTrayPulseUpScale,
            boosterTrayPulseUpDuration
        );
        yield return AnimateBoosterTrayScale(
            boosterTrayPulseUpScale,
            boosterTrayPulseDownScale,
            boosterTrayPulseDownDuration
        );
        yield return AnimateBoosterTrayScale(
            boosterTrayPulseDownScale,
            1f,
            boosterTrayPulseSettleDuration
        );

        RestoreBoosterTrayScales();
        _boosterTrayPulseRoutine = null;
    }

    private IEnumerator AnimateBoosterTrayScale(
        float from,
        float to,
        float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            float eased = progress * progress * (3f - 2f * progress);
            ApplyBoosterTrayScale(Mathf.LerpUnclamped(from, to, eased));
            yield return null;
        }

        ApplyBoosterTrayScale(to);
    }

    private void ApplyBoosterTrayScale(float multiplier)
    {
        foreach (KeyValuePair<Transform, Vector3> pair in
                 _boosterTrayOriginalScales)
        {
            if (pair.Key != null)
                pair.Key.localScale = pair.Value * multiplier;
        }
    }

    private void StopBoosterTrayPulse()
    {
        if (_boosterTrayPulseRoutine != null)
        {
            StopCoroutine(_boosterTrayPulseRoutine);
            _boosterTrayPulseRoutine = null;
        }

        RestoreBoosterTrayScales();
    }

    private void RestoreBoosterTrayScales()
    {
        foreach (KeyValuePair<Transform, Vector3> pair in
                 _boosterTrayOriginalScales)
        {
            if (pair.Key != null)
                pair.Key.localScale = pair.Value;
        }

        _boosterTrayOriginalScales.Clear();
    }

    private void SetBoosterDimVisible(
        bool visible,
        bool immediate = false)
    {
        InGameBoosterItem.SetBoosterFocusDimVisible(
            visible,
            boosterDimAlpha,
            immediate ? 0f : boosterDimFadeDuration
        );

        if (visible)
        {
            EnsureBoosterDim();
            ResizeBoosterDim();
            SetBoosterCardsHighlighted(true);
            PlayBoosterTrayPulse();
        }
        else
        {
            StopBoosterTrayPulse();
        }

        if (_boosterDimRenderer == null)
        {
            if (!visible)
                SetBoosterCardsHighlighted(false);

            return;
        }

        if (_boosterDimFadeRoutine != null)
        {
            StopCoroutine(_boosterDimFadeRoutine);
            _boosterDimFadeRoutine = null;
        }

        float targetAlpha =
            visible ? boosterDimAlpha : 0f;

        if (immediate || boosterDimFadeDuration <= 0f)
        {
            Color color = _boosterDimRenderer.color;
            color.a = targetAlpha;
            _boosterDimRenderer.color = color;
            _boosterDimRenderer.enabled = visible;

            if (!visible)
                SetBoosterCardsHighlighted(false);

            return;
        }

        _boosterDimRenderer.enabled = true;
        _boosterDimFadeRoutine = StartCoroutine(
            FadeBoosterDim(targetAlpha, visible)
        );
    }

    private IEnumerator FadeBoosterDim(
        float targetAlpha,
        bool keepVisible)
    {
        float startAlpha = _boosterDimRenderer.color.a;
        float elapsed = 0f;

        while (elapsed < boosterDimFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float percent = Mathf.Clamp01(
                elapsed / boosterDimFadeDuration
            );
            Color color = _boosterDimRenderer.color;
            color.a = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                percent
            );
            _boosterDimRenderer.color = color;
            yield return null;
        }

        Color finalColor = _boosterDimRenderer.color;
        finalColor.a = targetAlpha;
        _boosterDimRenderer.color = finalColor;
        _boosterDimRenderer.enabled = keepVisible;

        if (!keepVisible)
            SetBoosterCardsHighlighted(false);

        _boosterDimFadeRoutine = null;
    }

    public void OnClickCardSlotHolder(
        CardSlotHolder holder,
        Vector2? screenPosition = null)
    {
        if (IsCardInputGloballyBlocked)
            return;

        if (holder == null || IsHolderAnimating(holder))
            return;

        if (_selectedHolder != null &&
            IsHolderAnimating(_selectedHolder))
        {
            Deselect();
            return;
        }

        if (_activeBooster == BoosterType.Lighter)
        {
            TryUseLighter(holder);
            return;
        }

        if (holder.IsLocked)
        {

            return;
        }

        if (_activeBooster == BoosterType.Magnet)
        {
            TryUseMagnet(holder);
            return;
        }

        if (_activeBooster == BoosterType.MagicMove)
        {
            if (_selectedHolder == null)
            {
                Card selectedCard = screenPosition.HasValue
                    ? FindMagicCardAtScreenPosition(
                        holder,
                        screenPosition.Value
                    )
                    : null;

                TrySelectMagicCard(holder, selectedCard);
                return;
            }

            if (_selectedHolder == holder)
            {
                if (screenPosition.HasValue)
                {
                    Card selectedCard = FindMagicCardAtScreenPosition(
                        holder,
                        screenPosition.Value
                    );
                    TrySelectMagicCard(
                        holder,
                        selectedCard,
                        playHaptic: true
                    );
                }
                else
                {
                    Deselect();
                }

                return;
            }

            TryMagicMove(targetHolder: holder);
            return;
        }

        if (_selectedHolder == null)
        {
            TrySelect(holder);
            return;
        }

        if (_selectedHolder == holder)
        {
            Deselect();
            return;
        }

        TryMoveCards(holder);
    }

    private Card FindMagicCardAtScreenPosition(
        CardSlotHolder holder,
        Vector2 screenPosition)
    {
        if (holder == null || _mainCamera == null)
            return null;

        BuildMagicCardHitAreas(holder);
        return FindMagicCardInCachedHitAreas(screenPosition);
    }

    private void BuildMagicCardHitAreas(CardSlotHolder holder)
    {
        _magicCardHitAreas.Clear();

        if (holder == null || _mainCamera == null)
            return;

        List<Card> cards = new List<Card>(CardSlotHolder.MAX_SLOTS);
        List<Vector2> centers = new List<Vector2>(CardSlotHolder.MAX_SLOTS);

        for (int i = 0; i < holder.CardSlots.Count; i++)
        {
            CardSlot slot = holder.CardSlots[i];
            Card card = slot != null ? slot.Card : null;

            if (card == null)
                continue;

            cards.Add(card);
            centers.Add(
                _mainCamera.WorldToScreenPoint(card.transform.position)
            );
        }

        if (cards.Count == 0)
            return;

        Vector2 alongAxis;
        if (cards.Count > 1)
        {
            alongAxis = centers[centers.Count - 1] - centers[0];
        }
        else
        {
            Vector2 holderScreenPosition =
                _mainCamera.WorldToScreenPoint(holder.transform.position);
            Vector2 holderUpScreenPosition =
                _mainCamera.WorldToScreenPoint(
                    holder.transform.position + holder.transform.up
                );
            alongAxis = holderUpScreenPosition - holderScreenPosition;
        }

        if (alongAxis.sqrMagnitude < 0.001f)
            alongAxis = Vector2.up;

        alongAxis.Normalize();
        Vector2 acrossAxis = new Vector2(-alongAxis.y, alongAxis.x);
        _magicSelectionAlongAxis = alongAxis;
        _magicSelectionAcrossAxis = acrossAxis;

        List<MagicCardHitArea> rawAreas =
            new List<MagicCardHitArea>(cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            SpriteRenderer renderer = card.GetComponent<SpriteRenderer>();

            if (renderer == null)
                continue;

            Bounds localBounds = renderer.sprite != null
                ? renderer.sprite.bounds
                : new Bounds(Vector3.zero, Vector3.one);
            Transform cardParent = card.transform.parent;
            Vector3 worldCenter;
            Vector3 worldRight;
            Vector3 worldUp;

            if (cardParent != null)
            {
                worldCenter = cardParent.TransformPoint(
                    card.transform.localPosition + localBounds.center
                );
                worldRight = cardParent.TransformVector(
                    Vector3.right * localBounds.extents.x
                );
                worldUp = cardParent.TransformVector(
                    Vector3.up * localBounds.extents.y
                );
            }
            else
            {
                worldCenter = card.transform.position;
                worldRight = Vector3.right * localBounds.extents.x;
                worldUp = Vector3.up * localBounds.extents.y;
            }

            Vector2 corner0 = _mainCamera.WorldToScreenPoint(
                worldCenter - worldRight - worldUp
            );
            Vector2 corner1 = _mainCamera.WorldToScreenPoint(
                worldCenter - worldRight + worldUp
            );
            Vector2 corner2 = _mainCamera.WorldToScreenPoint(
                worldCenter + worldRight - worldUp
            );
            Vector2 corner3 = _mainCamera.WorldToScreenPoint(
                worldCenter + worldRight + worldUp
            );

            float along0 = Vector2.Dot(corner0, alongAxis);
            float along1 = Vector2.Dot(corner1, alongAxis);
            float along2 = Vector2.Dot(corner2, alongAxis);
            float along3 = Vector2.Dot(corner3, alongAxis);
            float across0 = Vector2.Dot(corner0, acrossAxis);
            float across1 = Vector2.Dot(corner1, acrossAxis);
            float across2 = Vector2.Dot(corner2, acrossAxis);
            float across3 = Vector2.Dot(corner3, acrossAxis);

            rawAreas.Add(
                new MagicCardHitArea
                {
                    Card = card,
                    MinAlong = Mathf.Min(along0, along1, along2, along3),
                    MaxAlong = Mathf.Max(along0, along1, along2, along3),
                    MinAcross = Mathf.Min(across0, across1, across2, across3),
                    MaxAcross = Mathf.Max(across0, across1, across2, across3)
                }
            );
        }

        rawAreas.Sort(
            (a, b) =>
            {
                float centerA = (a.MinAlong + a.MaxAlong) * 0.5f;
                float centerB = (b.MinAlong + b.MaxAlong) * 0.5f;
                return centerA.CompareTo(centerB);
            }
        );

        for (int i = 0; i < rawAreas.Count; i++)
        {
            MagicCardHitArea area = rawAreas[i];

            if (i < rawAreas.Count - 1)
            {
                area.MaxAlong = Mathf.Clamp(
                    rawAreas[i + 1].MinAlong,
                    area.MinAlong,
                    area.MaxAlong
                );
            }

            _magicCardHitAreas.Add(area);
        }
    }

    private Card FindMagicCardInCachedHitAreas(Vector2 screenPosition)
    {
        if (_magicCardHitAreas.Count == 0)
            return null;

        float pointerAlong = Vector2.Dot(
            screenPosition,
            _magicSelectionAlongAxis
        );
        float pointerAcross = Vector2.Dot(
            screenPosition,
            _magicSelectionAcrossAxis
        );

        for (int i = _magicCardHitAreas.Count - 1; i >= 0; i--)
        {
            MagicCardHitArea area = _magicCardHitAreas[i];
            if (pointerAlong >= area.MinAlong &&
                pointerAlong <= area.MaxAlong &&
                pointerAcross >= area.MinAcross &&
                pointerAcross <= area.MaxAcross)
            {
                return area.Card;
            }
        }

        Card closestCard = null;
        float closestSqrDistance = float.PositiveInfinity;
        float paddingSqr =
            magicSelectionTouchPadding * magicSelectionTouchPadding;

        for (int i = 0; i < _magicCardHitAreas.Count; i++)
        {
            MagicCardHitArea area = _magicCardHitAreas[i];
            float closestAlong = Mathf.Clamp(
                pointerAlong,
                area.MinAlong,
                area.MaxAlong
            );
            float closestAcross = Mathf.Clamp(
                pointerAcross,
                area.MinAcross,
                area.MaxAcross
            );
            float sqrDistance =
                (pointerAlong - closestAlong) *
                (pointerAlong - closestAlong) +
                (pointerAcross - closestAcross) *
                (pointerAcross - closestAcross);

            if (sqrDistance >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistance;
            closestCard = area.Card;
        }

        return closestSqrDistance <= paddingSqr
            ? closestCard
            : null;
    }

    private void BeginMagicCardSelection(
        CardSlotHolder holder,
        Lean.Touch.LeanFinger finger)
    {
        if (holder == null || holder.IsLocked || finger == null)
            return;

        BuildMagicCardHitAreas(holder);
        Card selectedCard = FindMagicCardInCachedHitAreas(
            finger.ScreenPosition
        );
        TrySelectMagicCard(holder, selectedCard, playHaptic: true);

        if (_selectedHolder != holder || selectedCard == null)
        {
            EndMagicCardSelectionGesture();
            return;
        }

        _magicSelectionFinger = finger;
        _magicSelectionDragHolder = holder;
    }

    private void UpdateMagicCardSelectionGesture(
        Lean.Touch.LeanFinger finger)
    {
        if (finger == null ||
            finger != _magicSelectionFinger ||
            !finger.Set)
        {
            return;
        }

        if (_activeBooster != BoosterType.MagicMove ||
            _selectedHolder == null ||
            _selectedHolder != _magicSelectionDragHolder)
        {
            EndMagicCardSelectionGesture();
            return;
        }

        Card selectedCard = FindMagicCardInCachedHitAreas(
            finger.ScreenPosition
        );

        if (selectedCard != null &&
            selectedCard != _magicSelectedSourceCard)
        {
            TrySelectMagicCard(
                _magicSelectionDragHolder,
                selectedCard,
                playHaptic: true
            );
        }
    }

    private void EndMagicCardSelectionGesture()
    {
        _magicSelectionFinger = null;
        _magicSelectionDragHolder = null;
        _magicCardHitAreas.Clear();
    }

    private void TrySelectMagicCard(
        CardSlotHolder holder,
        Card selectedCard,
        bool playHaptic = false)
    {
        if (holder == null || selectedCard == null || selectedCard.IsFrozen)
            return;

        if (_selectedHolder == holder &&
            _magicSelectedSourceCard == selectedCard)
        {
            return;
        }

        int selectedIndex = -1;

        for (int i = 0; i < holder.CardSlots.Count; i++)
        {
            if (holder.CardSlots[i] != null &&
                holder.CardSlots[i].Card == selectedCard)
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0)
            return;

        for (int i = 0; i < _selectedCards.Count; i++)
        {
            Card oldCard = _selectedCards[i];
            if (oldCard == null)
                continue;

            oldCard.SetSelected(false);
        }

        _selectedCards.Clear();

        CardType selectedType = selectedCard.CardType;
        int consecutiveStart = selectedIndex;
        int consecutiveEnd = selectedIndex;

        for (int i = selectedIndex - 1; i >= 0; i--)
        {
            CardSlot slot = holder.CardSlots[i];
            Card card = slot != null ? slot.Card : null;

            if (card == null || card.IsFrozen || card.CardType != selectedType)
                break;

            consecutiveStart = i;
        }

        for (int i = selectedIndex + 1;
             i < holder.CardSlots.Count;
             i++)
        {
            CardSlot slot = holder.CardSlots[i];
            Card card = slot != null ? slot.Card : null;

            if (card == null || card.IsFrozen || card.CardType != selectedType)
                break;

            consecutiveEnd = i;
        }

        List<Card> selectedStack = new List<Card>(
            consecutiveEnd - consecutiveStart + 1
        );

        for (int i = consecutiveStart; i <= consecutiveEnd; i++)
        {
            Card card = holder.CardSlots[i].Card;
            selectedStack.Add(card);
        }

        selectedStack.Reverse();
        _selectedHolder = holder;
        _selectedCards = selectedStack;
        _magicSelectedSourceCard = selectedCard;

        for (int i = 0; i < _selectedCards.Count; i++)
        {
            Card card = _selectedCards[i];
            if (card == null)
                continue;

            card.SetSelected(true, delay: i * SelectStaggerPerCard);
        }

        if (playHaptic && magicSelectionHaptic)
            VibrationController.Instance?.HapticLight();
    }

    private const float SelectStaggerPerCard = 0.025f;

    private void TrySelect(CardSlotHolder holder)
    {
        if (holder == null || IsHolderAnimating(holder))
            return;

        if (holder.IsEmpty())
            return;

        List<Card> topCards =
            holder.GetTopMatchingCards();

        if (topCards == null ||
            topCards.Count == 0)
        {
            return;
        }

        _selectedHolder = holder;
        _selectedCards = topCards;

        for (int i = 0;
             i < _selectedCards.Count;
             i++)
        {
            Card card =
                _selectedCards[i];

            if (card != null)
            {
                card.SetSelected(true, delay: i * SelectStaggerPerCard);
            }
        }
    }

    public void Deselect()
    {
        for (int i = 0;
             i < _selectedCards.Count;
             i++)
        {
            Card card =
                _selectedCards[i];

            if (card != null)
            {
                card.SetSelected(
                    false,
                    delay: (_selectedCards.Count - i - 1) * SelectStaggerPerCard
                );
            }
        }

        _selectedHolder = null;
        _selectedCards.Clear();
        _magicSelectedSourceCard = null;
        EndMagicCardSelectionGesture();
    }

    private void TryMoveCards(CardSlotHolder targetHolder)
    {
        if (targetHolder == null || IsHolderAnimating(targetHolder))
            return;

        if (_selectedCards.Count == 0 ||
            _selectedHolder == null ||
            IsHolderAnimating(_selectedHolder))
        {
            return;
        }

        if (_selectedCards[0] == null)
        {
            Deselect();
            return;
        }

        Card sourceTopCard = _selectedCards[0];
        Card targetTopCard = targetHolder.GetTopCard();
        bool sourceIsIron =
            sourceTopCard.CardType == CardType.IronCard;
        bool targetIsIron =
            targetTopCard != null &&
            targetTopCard.CardType == CardType.IronCard;

        // An exposed Iron Card blocks every incoming card or stack.
        // The Iron Card itself can still move alone to another tray,
        // ignoring the destination's top card type.
        if (targetIsIron)
        {
            Deselect();
            return;
        }

        if (sourceIsIron)
        {
            TryMoveIronCard(targetHolder);
            return;
        }

        bool sourceIsDarkKing =
            sourceTopCard.CardType == CardType.DarkingCard;

        if (sourceIsDarkKing)
        {
            TryMoveDarkKingCard(targetHolder, targetTopCard);
            return;
        }

        bool sourceIsDowngrade =
            sourceTopCard.CardType == CardType.DowngradeCard;
        bool targetIsDowngrade =
            targetTopCard != null &&
            targetTopCard.CardType == CardType.DowngradeCard;

        if (sourceIsDowngrade || targetIsDowngrade)
        {
            TryMoveDowngradeCard(
                targetHolder,
                sourceTopCard,
                targetTopCard,
                sourceIsDowngrade,
                targetIsDowngrade
            );
            return;
        }

        bool selectionContainsChain =
            SelectionContainsCardType(CardType.ChainCard);
        bool targetIsChain =
            targetTopCard != null &&
            targetTopCard.CardType == CardType.ChainCard;

        if (selectionContainsChain || targetIsChain)
        {
            TryMoveChainCards(
                targetHolder,
                selectionContainsChain
            );
            return;
        }

        int emptySlots =
            targetHolder.GetEmptySlotCount();

        if (emptySlots <= 0)
        {
            Deselect();
            return;
        }

        if (!PrepareWildCardMove(
                sourceTopCard,
                targetHolder,
                ignoreTopType: false,
                out WildBindContext wildBindContext))
        {
            Deselect();
            TrySelect(targetHolder);
            return;
        }

        int moveCount =
            Mathf.Min(
                _selectedCards.Count,
                emptySlots
            );

        moveCount =
            Mathf.Min(
                moveCount,
                CardSlotHolder.MAX_SLOTS
            );

        if (moveCount <= 0)
        {
            Deselect();
            return;
        }

        MoveSelectedCards(
            targetHolder,
            moveCount,
            wildBindContext: wildBindContext
        );
    }

    private void MoveSelectedCards(
        CardSlotHolder targetHolder,
        int moveCount,
        bool compactSource = false,
        DowngradeInteraction downgradeInteraction =
            DowngradeInteraction.None,
        Card targetDowngradeCard = null,
        WildBindContext wildBindContext = null,
        bool consumeMove = true)
    {
        if (targetHolder == null ||
            _selectedHolder == null ||
            moveCount <= 0)
        {
            return;
        }

        CardSlotHolder sourceHolder = _selectedHolder;
        List<Card> cardsToMove = new List<Card>(moveCount);
        for (int i = 0; i < moveCount; i++)
        {
            Card card = _selectedCards[i];
            if (card == null)
                return;

            cardsToMove.Add(card);
        }

        cardsToMove.Reverse();

        DowngradeEffectContext downgradeContext =
            CreateDowngradeEffectContext(
                downgradeInteraction,
                targetHolder,
                cardsToMove,
                targetDowngradeCard
            );

        List<CardSlot> targetSlots =
            targetHolder.GetEmptySlots(moveCount);

        if (targetSlots.Count != cardsToMove.Count)
            return;

        HashSet<CardSlotHolder> movementHolders =
            new HashSet<CardSlotHolder>
            {
                sourceHolder,
                targetHolder
            };

        if (!TryReserveMovingHolders(movementHolders))
            return;

        foreach (Card card in cardsToMove)
        {
            if (card != null)
            {
                card.PlayMoveEffect();
                card.SetSelected(false, animate: false);
            }
        }

        sourceHolder.RemoveCards(
            cardsToMove
        );

        if (compactSource)
            CompactHolderCards(sourceHolder);

        _selectedCards.RemoveRange(
            0,
            moveCount
        );

        if (_selectedCards.Count == 0)
        {
            _selectedHolder = null;
        }
        else
        {
            Deselect();
        }

        for (int i = 0;
             i < cardsToMove.Count &&
             i < targetSlots.Count;
             i++)
        {
            if (targetSlots[i] != null)
            {
                targetSlots[i].Card =
                    cardsToMove[i];
            }
        }

        targetHolder.NotifyCardsChanged();

        if (consumeMove)
            ConsumeMove();

        StartCoroutine(
            AnimateCardCluster(
                cardsToMove,
                targetSlots,
                targetHolder,
                movementHolders,
                downgradeContext,
                wildBindContext
            )
        );
    }

    private void TryMoveDowngradeCard(
        CardSlotHolder targetHolder,
        Card sourceTopCard,
        Card targetTopCard,
        bool sourceIsDowngrade,
        bool targetIsDowngrade)
    {
        // Two obstacle cards do not trigger each other.
        if (sourceIsDowngrade && targetIsDowngrade)
        {
            Deselect();
            return;
        }

        if (targetHolder.GetEmptySlotCount() <= 0)
        {
            Deselect();
            return;
        }

        if (sourceIsDowngrade)
        {
            if (!HasNumberCard(targetHolder) ||
                !targetHolder.CanReceiveCardsIgnoringTopType(
                    CardType.DowngradeCard))
            {
                Deselect();
                return;
            }

            MoveSelectedCards(
                targetHolder,
                1,
                downgradeInteraction:
                    DowngradeInteraction.MoveDowngradeOntoCards
            );
            return;
        }

        if (!Card.IsNumberCardType(sourceTopCard.CardType) ||
            targetTopCard == null ||
            !targetHolder.CanReceiveCardsIgnoringTopType(
                sourceTopCard.CardType))
        {
            Deselect();
            return;
        }

        bool chainGroup =
            SelectionContainsCardType(CardType.ChainCard);
        int emptySlotCount = targetHolder.GetEmptySlotCount();

        if (chainGroup && emptySlotCount < _selectedCards.Count)
        {
            Deselect();
            return;
        }

        int moveCount = chainGroup
            ? _selectedCards.Count
            : Mathf.Min(_selectedCards.Count, emptySlotCount);
        moveCount = Mathf.Min(moveCount, CardSlotHolder.MAX_SLOTS);

        if (moveCount <= 0)
        {
            Deselect();
            return;
        }

        MoveSelectedCards(
            targetHolder,
            moveCount,
            compactSource: chainGroup,
            downgradeInteraction:
                DowngradeInteraction.MoveOntoDowngrade,
            targetDowngradeCard: targetTopCard
        );
    }

    private void TryMoveChainCards(
        CardSlotHolder targetHolder,
        bool requiresWholeGroup)
    {
        if (targetHolder == null || _selectedCards.Count == 0)
            return;

        int emptySlotCount = targetHolder.GetEmptySlotCount();
        if (emptySlotCount <= 0 ||
            (requiresWholeGroup &&
             emptySlotCount < _selectedCards.Count))
        {
            Deselect();
            return;
        }

        int moveCount = requiresWholeGroup
            ? _selectedCards.Count
            : Mathf.Min(_selectedCards.Count, emptySlotCount);
        moveCount = Mathf.Min(moveCount, CardSlotHolder.MAX_SLOTS);

        for (int i = 0; i < moveCount; i++)
        {
            Card card = _selectedCards[i];
            if (card == null ||
                !targetHolder.CanReceiveCardsIgnoringTopType(
                    card.CardType))
            {
                Deselect();
                return;
            }
        }

        MoveSelectedCards(
            targetHolder,
            moveCount,
            compactSource: requiresWholeGroup
        );
    }

    private void TryMoveIronCard(CardSlotHolder targetHolder)
    {
        if (targetHolder == null ||
            targetHolder.GetEmptySlotCount() <= 0 ||
            !targetHolder.CanReceiveCardsIgnoringTopType(
                CardType.IronCard))
        {
            Deselect();
            return;
        }

        MoveSelectedCards(targetHolder, 1);
    }

    private void TryMoveDarkKingCard(
        CardSlotHolder targetHolder,
        Card targetTopCard)
    {
        // Dark King always moves alone. It may ignore the value of a normal
        // number card, while keeping its previous ability to move onto an
        // empty tray or another Dark King.
        bool hasValidTopCard =
            targetTopCard == null ||
            targetTopCard.IsNumberCard ||
            targetTopCard.CardType == CardType.DarkingCard;

        if (targetHolder == null ||
            !hasValidTopCard ||
            targetHolder.GetEmptySlotCount() <= 0 ||
            !targetHolder.CanReceiveCardsIgnoringTopType(
                CardType.DarkingCard))
        {
            Deselect();
            return;
        }

        MoveSelectedCards(targetHolder, 1);
    }

    private bool SelectionContainsCardType(CardType type)
    {
        for (int i = 0; i < _selectedCards.Count; i++)
        {
            Card card = _selectedCards[i];
            if (card != null && card.CardType == type)
                return true;
        }

        return false;
    }

    private static bool HasNumberCard(CardSlotHolder holder)
    {
        if (holder == null || holder.CardSlots == null)
            return false;

        for (int i = 0; i < holder.CardSlots.Count; i++)
        {
            CardSlot slot = holder.CardSlots[i];
            if (slot != null && slot.Card != null &&
                slot.Card.IsNumberCard)
            {
                return true;
            }
        }

        return false;
    }

    private DowngradeEffectContext CreateDowngradeEffectContext(
        DowngradeInteraction interaction,
        CardSlotHolder targetHolder,
        List<Card> cardsToMove,
        Card targetDowngradeCard)
    {
        if (interaction == DowngradeInteraction.None ||
            targetHolder == null)
        {
            return null;
        }

        DowngradeEffectContext context = new DowngradeEffectContext
        {
            TargetHolder = targetHolder,
            AffectedCards = new List<Card>()
        };

        if (interaction == DowngradeInteraction.MoveOntoDowngrade)
        {
            context.ObstacleCard = targetDowngradeCard;
            for (int i = 0; i < cardsToMove.Count; i++)
            {
                Card card = cardsToMove[i];
                if (card != null && card.IsNumberCard)
                    context.AffectedCards.Add(card);
            }
        }
        else
        {
            context.ObstacleCard =
                cardsToMove.Count > 0 ? cardsToMove[0] : null;

            for (int i = 0; i < targetHolder.CardSlots.Count; i++)
            {
                CardSlot slot = targetHolder.CardSlots[i];
                Card card = slot != null ? slot.Card : null;
                if (card != null && card.IsNumberCard)
                    context.AffectedCards.Add(card);
            }
        }

        return context.ObstacleCard != null ? context : null;
    }

    private void TryMagicMove(CardSlotHolder targetHolder)
    {
        if (targetHolder == null ||
            _selectedHolder == null ||
            _selectedCards.Count == 0 ||
            _selectedCards[0] == null)
        {
            return;
        }

        if (!PrepareWildCardMove(
            _selectedCards[0],
            targetHolder,
            ignoreTopType: true,
            out WildBindContext wildBindContext))
        {
            return;
        }

        CardType cardType = wildBindContext != null
            ? wildBindContext.ResolvedType
            : _selectedCards[0].CardType;

        if (!targetHolder.CanReceiveCardsIgnoringTopType(cardType))
        {
            return;
        }

        int moveCount = Mathf.Min(
            _selectedCards.Count,
            CardSlotHolder.MAX_SLOTS
        );

        if (targetHolder.GetEmptySlotCount() < moveCount)
        {
            return;
        }

        _activeBooster = null;
        SetBoosterDimVisible(false, true);
        Observer.ActiveBoosterChanged?.Invoke(null);
        _magicSelectedSourceCard = null;
        EndMagicCardSelectionGesture();
        ConsumeBooster(BoosterType.MagicMove);
        MoveSelectedCards(
            targetHolder,
            moveCount,
            compactSource: true,
            wildBindContext: wildBindContext,
            consumeMove: false
        );
    }

    private void TryUseLighter(CardSlotHolder holder)
    {
        List<Card> cardsToBurn = GetAllLighterCards(holder);
        if (cardsToBurn.Count == 0)
        {
            return;
        }

        HashSet<CardSlotHolder> movementHolders =
            new HashSet<CardSlotHolder> { holder };
        if (!TryReserveMovingHolders(movementHolders))
            return;

        _activeBooster = null;
        Observer.ActiveBoosterChanged?.Invoke(null);
        ConsumeBooster(BoosterType.Lighter);
        SoundController.Instance?.PlayFX(SoundName.GaslighterFireBurn);

        StartLighterBurn(holder, cardsToBurn, movementHolders);
    }

    private static List<Card> GetAllLighterCards(CardSlotHolder holder)
    {
        List<Card> result = new List<Card>();
        if (holder == null || holder.CardSlots == null)
            return result;

        for (int i = 0; i < holder.CardSlots.Count; i++)
        {
            CardSlot slot = holder.CardSlots[i];
            Card card = slot != null ? slot.Card : null;
            if (card != null)
                result.Add(card);
        }

        return result;
    }

    private void StartLighterBurn(
        CardSlotHolder holder,
        List<Card> cardsToBurn,
        HashSet<CardSlotHolder> movementHolders)
    {
        if (holder == null || cardsToBurn == null ||
            cardsToBurn.Count == 0)
        {
            ReleaseMovingHolders(movementHolders);
            SetBoosterDimVisible(false);
            return;
        }

        CardType lastBurnedCardType = cardsToBurn[0] != null
            ? cardsToBurn[0].CardType
            : CardType.Card1;

        GameObject lighterBooster = SpawnLighterBoosterEffect(
            holder,
            cardsToBurn,
            out Vector3 lighterBoosterEndPosition
        );
        StartCoroutine(PlayLighterBurnSequence(
            holder,
            cardsToBurn,
            movementHolders,
            lastBurnedCardType,
            lighterBooster,
            lighterBoosterEndPosition
        ));
    }

    private IEnumerator PlayLighterBurnSequence(
        CardSlotHolder holder,
        List<Card> cardsToBurn,
        HashSet<CardSlotHolder> movementHolders,
        CardType lastBurnedCardType,
        GameObject lighterBooster,
        Vector3 lighterBoosterEndPosition
    )
    {
        // Let the flame establish itself at the bottom of the stack before
        // cards dissolve. The holder remains reserved during this beat.
        if (lighterBooster != null && lighterBoosterIgnitionDuration > 0f)
        {
            yield return new WaitForSeconds(lighterBoosterIgnitionDuration);
        }

        if (lighterBooster != null)
        {
            StartCoroutine(MoveLighterBoosterEffect(
                lighterBooster,
                lighterBooster.transform.position,
                lighterBoosterEndPosition
            ));
        }

        holder.RemoveCards(cardsToBurn);
        bool shouldAutoDeal = IsBoardEmptyAfterLighter();

        CompactHolderCards(holder);
        holder.NormalizeSlotLayout();

        SetBoosterDimVisible(false, true);
        ReleaseMovingHolders(movementHolders);
        OnBoardChanged?.Invoke();

        int pendingBurns = cardsToBurn.Count;
        Action onBurnComplete = () =>
        {
            pendingBurns--;
            if (pendingBurns <= 0)
                FinishLighterBurn(shouldAutoDeal, lastBurnedCardType);
        };

        for (int i = 0; i < cardsToBurn.Count; i++)
        {
            Card card = cardsToBurn[i];
            if (card == null)
            {
                onBurnComplete();
                continue;
            }

            card.PlayBurn(() =>
            {
                if (card != null)
                    Destroy(card.gameObject);
                onBurnComplete();
            });
        }
    }

    private GameObject SpawnLighterBoosterEffect(
        CardSlotHolder holder,
        List<Card> cardsToBurn,
        out Vector3 endPosition
    )
    {
        endPosition = Vector3.zero;

        if (lighterBoosterPrefab == null || holder == null ||
            cardsToBurn == null || cardsToBurn.Count == 0)
        {
            return null;
        }

        Card lowestCard = null;
        Card highestCard = null;

        for (int i = 0; i < cardsToBurn.Count; i++)
        {
            Card card = cardsToBurn[i];
            if (card == null)
                continue;

            if (lowestCard == null ||
                card.transform.position.y < lowestCard.transform.position.y)
            {
                lowestCard = card;
            }

            if (highestCard == null ||
                card.transform.position.y > highestCard.transform.position.y)
            {
                highestCard = card;
            }
        }

        if (lowestCard == null || highestCard == null)
            return null;

        GameObject lighterBooster = Instantiate(
            lighterBoosterPrefab,
            holder.transform
        );
        lighterBooster.transform.position =
            lowestCard.transform.position + lighterBoosterStartOffset;

        ParticleSystem[] particleSystems =
            lighterBooster.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }

        ConfigureLighterBoosterRendering(lighterBooster, cardsToBurn);
        endPosition = highestCard.transform.position + lighterBoosterEndOffset;
        return lighterBooster;
    }

    private void ConfigureLighterBoosterRendering(
        GameObject lighterBooster,
        List<Card> cardsToBurn
    )
    {
        int sortingLayerId = 0;
        int highestCardSortingOrder = 0;
        int cardLayer = lighterBooster.layer;

        for (int i = 0; i < cardsToBurn.Count; i++)
        {
            Card card = cardsToBurn[i];
            if (card == null)
                continue;

            cardLayer = card.gameObject.layer;
            SpriteRenderer[] cardRenderers =
                card.GetComponentsInChildren<SpriteRenderer>(true);
            for (int rendererIndex = 0;
                 rendererIndex < cardRenderers.Length;
                 rendererIndex++)
            {
                SpriteRenderer cardRenderer = cardRenderers[rendererIndex];
                if (cardRenderer == null ||
                    cardRenderer.sortingOrder < highestCardSortingOrder)
                {
                    continue;
                }

                highestCardSortingOrder = cardRenderer.sortingOrder;
                sortingLayerId = cardRenderer.sortingLayerID;
            }
        }

        SetLayerRecursively(lighterBooster.transform, cardLayer);

        ParticleSystemRenderer[] particleRenderers =
            lighterBooster.GetComponentsInChildren<ParticleSystemRenderer>(true);
        int effectSortingOrder = highestCardSortingOrder +
            Mathf.Max(1, lighterBoosterSortingOffset);
        for (int i = 0; i < particleRenderers.Length; i++)
        {
            ParticleSystemRenderer particleRenderer = particleRenderers[i];
            particleRenderer.sortingLayerID = sortingLayerId;
            particleRenderer.sortingOrder = effectSortingOrder;
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    private IEnumerator MoveLighterBoosterEffect(
        GameObject lighterBooster,
        Vector3 startPosition,
        Vector3 endPosition
    )
    {
        float duration = Mathf.Max(0.01f, lighterBoosterLiftDuration);
        float elapsed = 0f;

        while (elapsed < duration && lighterBooster != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            progress = progress * progress * (3f - 2f * progress);
            lighterBooster.transform.position = Vector3.LerpUnclamped(
                startPosition,
                endPosition,
                progress
            );
            yield return null;
        }

        if (lighterBooster != null)
            Destroy(lighterBooster);
    }

    private void FinishLighterBurn(
        bool shouldAutoDeal,
        CardType lastBurnedCardType)
    {
        if (!shouldAutoDeal ||
            (GameManager.Instance != null &&
             GameManager.Instance.gameState != GameState.PlayingGame))
        {
            return;
        }

        if (_cardDesk == null)
            _cardDesk = GetComponentInChildren<CardDesk>(true);

        _cardDesk?.TryAutoDealOnce(lastBurnedCardType);
    }

    private bool IsBoardEmptyAfterLighter()
    {
        if (cardSlotHolders == null)
            return true;

        for (int holderIndex = 0;
             holderIndex < cardSlotHolders.Count;
             holderIndex++)
        {
            CardSlotHolder holder = cardSlotHolders[holderIndex];
            if (holder == null || !holder.gameObject.activeInHierarchy ||
                holder.CardSlots == null)
                continue;

            for (int slotIndex = 0;
                 slotIndex < holder.CardSlots.Count;
                 slotIndex++)
            {
                CardSlot slot = holder.CardSlots[slotIndex];
                if (slot != null && slot.Card != null)
                    return false;
            }
        }

        return true;
    }

    private void TryUseMagnet(CardSlotHolder targetHolder)
    {
        if (targetHolder == null ||
            IsHolderAnimating(targetHolder) ||
            targetHolder.IsEmpty())
        {
            return;
        }

        Card targetTopCard = targetHolder.GetTopCard();
        if (targetTopCard != null && targetTopCard.IsUnassignedWild)
        {
            return;
        }

        CardType? selectedType = targetHolder.GetTopCardType();
        if (!selectedType.HasValue)
            return;

        if (!targetHolder.CanReceiveCardsIgnoringTopType(
                selectedType.Value))
        {
            return;
        }

        int currentCardCount =
            targetHolder.CardSlots.Count -
            targetHolder.GetEmptySlotCount();

        int availableSpace = Mathf.Min(
            targetHolder.GetEmptySlotCount(),
            Mathf.Max(
                0,
                CardSlotHolder.MAX_SLOTS - currentCardCount
            )
        );

        if (availableSpace <= 0)
        {
            return;
        }

        Vector3 targetPosition = targetHolder.transform.position;
        int targetTopIndex = targetHolder.GetTopCardIndex();

        if (targetTopIndex >= 0 &&
            targetTopIndex < targetHolder.CardSlots.Count &&
            targetHolder.CardSlots[targetTopIndex].Card != null)
        {
            targetPosition = targetHolder
                .CardSlots[targetTopIndex]
                .Card.transform.position;
        }

        List<MagnetCandidate> candidates =
            new List<MagnetCandidate>();

        for (int holderIndex = 0;
             holderIndex < cardSlotHolders.Count;
             holderIndex++)
        {
            CardSlotHolder sourceHolder =
                cardSlotHolders[holderIndex];

            if (sourceHolder == null ||
                sourceHolder == targetHolder ||
                sourceHolder.IsLocked ||
                IsHolderAnimating(sourceHolder))
            {
                continue;
            }

            for (int slotIndex = 0;
                 slotIndex < sourceHolder.CardSlots.Count;
                 slotIndex++)
            {
                CardSlot slot = sourceHolder.CardSlots[slotIndex];
                Card card = slot != null ? slot.Card : null;

                if (card == null ||
                    card.IsFrozen ||
                    card.IsUnassignedWild ||
                    card.CardType != selectedType.Value)
                    continue;

                candidates.Add(
                    new MagnetCandidate
                    {
                        Card = card,
                        Holder = sourceHolder,
                        SqrDistance =
                            (card.transform.position - targetPosition)
                            .sqrMagnitude
                    }
                );
            }
        }

        candidates.Sort(
            (a, b) => a.SqrDistance.CompareTo(b.SqrDistance)
        );

        int pullCount = Mathf.Min(availableSpace, candidates.Count);
        if (pullCount <= 0)
        {
            return;
        }

        List<Card> cardsToMove = new List<Card>(pullCount);
        Dictionary<CardSlotHolder, List<Card>> cardsBySource =
            new Dictionary<CardSlotHolder, List<Card>>();

        for (int i = 0; i < pullCount; i++)
        {
            MagnetCandidate candidate = candidates[i];
            cardsToMove.Add(candidate.Card);

            if (!cardsBySource.TryGetValue(
                    candidate.Holder,
                    out List<Card> sourceCards))
            {
                sourceCards = new List<Card>();
                cardsBySource.Add(candidate.Holder, sourceCards);
            }

            sourceCards.Add(candidate.Card);
        }

        HashSet<CardSlotHolder> movementHolders =
            new HashSet<CardSlotHolder> { targetHolder };

        foreach (CardSlotHolder sourceHolder in cardsBySource.Keys)
            movementHolders.Add(sourceHolder);

        if (!TryReserveMovingHolders(movementHolders))
            return;

        foreach (KeyValuePair<CardSlotHolder, List<Card>> pair
                 in cardsBySource)
        {
            pair.Key.RemoveCards(pair.Value);
            CompactHolderCards(pair.Key);
        }

        List<CardSlot> targetSlots =
            targetHolder.GetEmptySlots(pullCount);

        for (int i = 0;
             i < cardsToMove.Count && i < targetSlots.Count;
             i++)
        {
            targetSlots[i].Card = cardsToMove[i];
            cardsToMove[i].SetSelected(false, animate: false);
            cardsToMove[i].transform.localScale = Vector3.one;
        }

        targetHolder.NotifyCardsChanged();
        _activeBooster = null;
        SetBoosterDimVisible(false, true);
        Observer.ActiveBoosterChanged?.Invoke(null);
        ConsumeBooster(BoosterType.Magnet);

        StartCoroutine(
            AnimateCardCluster(
                cardsToMove,
                targetSlots,
                targetHolder,
                movementHolders
            )
        );
    }

    private bool PrepareWildCardMove(
        Card sourceTopCard,
        CardSlotHolder targetHolder,
        bool ignoreTopType,
        out WildBindContext bindContext)
    {
        bindContext = null;

        if (sourceTopCard == null || targetHolder == null)
            return false;

        Card targetTopCard = targetHolder.GetTopCard();
        bool sourceIsWild = sourceTopCard.IsUnassignedWild;
        bool targetIsWild =
            targetTopCard != null && targetTopCard.IsUnassignedWild;

        if (sourceIsWild && targetIsWild)
            return false;

        CardType resolvedType = sourceTopCard.CardType;
        if (sourceIsWild && targetTopCard != null)
            resolvedType = targetTopCard.CardType;

        if (!targetHolder.CanReceiveCardsIgnoringTopType(resolvedType))
            return false;

        if (!ignoreTopType &&
            targetTopCard != null &&
            !targetIsWild &&
            targetTopCard.CardType != resolvedType)
        {
            return false;
        }

        Sprite resolvedSprite = null;
        if (cardConfig != null)
        {
            CardData data = cardConfig.GetCardData(resolvedType);
            resolvedSprite = data != null ? data.icon : null;
        }

        if (sourceIsWild && targetTopCard != null)
        {
            bindContext = new WildBindContext
            {
                Card = sourceTopCard,
                ResolvedType = resolvedType,
                ResolvedSprite = resolvedSprite
            };
        }
        else if (targetIsWild && !sourceIsWild)
        {
            bindContext = new WildBindContext
            {
                Card = targetTopCard,
                ResolvedType = resolvedType,
                ResolvedSprite = resolvedSprite
            };
        }

        return true;
    }

    private static void CompactHolderCards(CardSlotHolder holder)
    {
        if (holder == null || holder.CardSlots == null)
            return;

        List<Card> remainingCards = new List<Card>();

        for (int i = 0; i < holder.CardSlots.Count; i++)
        {
            CardSlot slot = holder.CardSlots[i];
            if (slot != null && slot.Card != null)
                remainingCards.Add(slot.Card);
        }

        for (int i = 0; i < holder.CardSlots.Count; i++)
        {
            CardSlot slot = holder.CardSlots[i];
            if (slot != null)
                slot.RemoveCard();
        }

        int count = Mathf.Min(
            remainingCards.Count,
            holder.CardSlots.Count
        );

        for (int i = 0; i < count; i++)
            holder.CardSlots[i].SetCard(remainingCards[i]);

        holder.NotifyCardsChanged();
    }

    private static int GetBoosterAmount(BoosterType boosterType)
    {
        if (Data.PlayerData == null)
            return 0;

        return boosterType switch
        {
            BoosterType.MoreDeal => Data.PlayerData.CurrentMoreDeal,
            BoosterType.MagicMove => Data.PlayerData.CurrentMagicSwap,
            BoosterType.Magnet => Data.PlayerData.CurrentMagnet,
            BoosterType.Lighter => Data.PlayerData.CurrentLighter,
            BoosterType.ExtraTray => Data.PlayerData.CurrentExtraTray,
            BoosterType.FreeMoves => Data.PlayerData.CurrentFreeMoves,
            _ => 0
        };
    }

    private static void ConsumeBooster(BoosterType boosterType)
    {
        if (Data.PlayerData == null)
            return;

        switch (boosterType)
        {
            case BoosterType.MoreDeal:
                Data.PlayerData.CurrentMoreDeal--;
                break;
            case BoosterType.MagicMove:
                Data.PlayerData.CurrentMagicSwap--;
                break;
            case BoosterType.Magnet:
                Data.PlayerData.CurrentMagnet--;
                break;
            case BoosterType.Lighter:
                Data.PlayerData.CurrentLighter--;
                break;
            case BoosterType.ExtraTray:
                Data.PlayerData.CurrentExtraTray--;
                break;
            case BoosterType.FreeMoves:
                Data.PlayerData.CurrentFreeMoves--;
                break;
        }

        Data.SaveData();
    }

    private bool TryReserveMovingHolders(
        HashSet<CardSlotHolder> holders)
    {
        if (holders == null || holders.Count == 0)
            return false;

        foreach (CardSlotHolder holder in holders)
        {
            if (holder == null || IsHolderAnimating(holder))
                return false;
        }

        foreach (CardSlotHolder holder in holders)
            _movingHolders.Add(holder);

        return true;
    }

    private void ReleaseMovingHolders(
        HashSet<CardSlotHolder> holders)
    {
        if (holders == null)
            return;

        foreach (CardSlotHolder holder in holders)
        {
            if (holder != null)
                _movingHolders.Remove(holder);
        }
    }

    private void NotifyBoardChangedIfIdle()
    {
        if (_movingHolders.Count > 0 ||
            _mergingHolders.Count > 0 ||
            IsCardInputGloballyBlocked)
        {
            return;
        }

        OnBoardChanged?.Invoke();
    }

    private SpriteRenderer CreateLevelFlightShadow(Card card)
    {
        if (card == null)
            return null;

        if (_cardDesk == null)
            _cardDesk = GetComponentInChildren<CardDesk>(true);

        SpriteRenderer shadowRenderer = CardFlightShadow.Create(
            card,
            transform,
            _cardDesk != null ? _cardDesk.FlightShadowSprite : null,
            _cardDesk != null ? _cardDesk.FlightShadowMaxAlpha : 0.72f
        );

        if (shadowRenderer != null)
            _activeFlightShadows.Add(shadowRenderer);

        return shadowRenderer;
    }

    private void UpdateLevelFlightShadow(
        SpriteRenderer shadowRenderer,
        Card card,
        Vector3 groundPosition,
        float heightStrength,
        float cardRotationZ)
    {
        if (shadowRenderer == null || card == null)
            return;

        CardFlightShadow.Update(
            shadowRenderer,
            card.transform.position,
            groundPosition,
            heightStrength,
            cardRotationZ,
            card.transform.localScale,
            _cardDesk != null
                ? _cardDesk.FlightShadowMaxOffset
                : 0.9f,
            _cardDesk != null
                ? _cardDesk.FlightShadowRotationDelay
                : 0.035f
        );
    }

    private void ReleaseLevelFlightShadow(
        SpriteRenderer shadowRenderer)
    {
        if (shadowRenderer == null)
            return;

        _activeFlightShadows.Remove(shadowRenderer);
        CardFlightShadow.Release(shadowRenderer);
    }

    private void CleanupLevelFlightShadows()
    {
        for (int i = _activeFlightShadows.Count - 1; i >= 0; i--)
        {
            CardFlightShadow.Release(_activeFlightShadows[i]);
        }

        _activeFlightShadows.Clear();
    }

    private IEnumerator AnimateCardCluster(
        List<Card> cards,
        List<CardSlot> targetSlots,
        CardSlotHolder targetHolder,
        HashSet<CardSlotHolder> movementHolders,
        DowngradeEffectContext downgradeContext = null,
        WildBindContext wildBindContext = null
    ) {
        _isMovingCardCluster = true;

        CardMoveSettings move = cardMove;

        int count =
            Mathf.Min(
                cards.Count,
                targetSlots.Count
            );

        if (count <= 0) {
            _isMovingCardCluster = false;
            _cardsToMoveBuffer.Clear();
            _targetSlotsBuffer.Clear();
            ReleaseMovingHolders(movementHolders);
            yield break;
        }

        Vector3[] startPositions = new Vector3[count];
        Vector3[] endPositions = new Vector3[count];
        float[] startScales = new float[count];
        Transform[] wrappers = new Transform[count];
        bool[] hasLanded = new bool[count];

        Vector3 averageStart = Vector3.zero;
        Vector3 averageEnd = Vector3.zero;
        int validCardCount = 0;

        for (int i = 0; i < count; i++) {
            Card card = cards[i];
            CardSlot targetSlot = targetSlots[i];

            if (card == null || targetSlot == null) {
                hasLanded[i] = true;
                continue;
            }            startPositions[i] = card.transform.position;
            endPositions[i] = targetSlot.transform.position;
            startScales[i] = card.transform.localScale.x;

            GameObject wrapper = new GameObject("CardFlight");
            wrapper.transform.SetParent(transform, false);
            wrapper.transform.position = startPositions[i];
            wrapper.transform.localScale = new Vector3(
                startScales[i],
                startScales[i],
                1f
            );
            wrappers[i] = wrapper.transform;

            card.transform.SetParent(wrapper.transform, false);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one;

            averageStart += startPositions[i];
            averageEnd += endPositions[i];
            validCardCount++;

            card.SetSortingOrder(300 + i);
            card.SetShadowEnabled(true);
        }

        if (validCardCount <= 0) {
            _isMovingCardCluster = false;
            _cardsToMoveBuffer.Clear();
            _targetSlotsBuffer.Clear();
            ReleaseMovingHolders(movementHolders);
            yield break;
        }

        if (validCardCount == 1)
            SoundController.Instance?.PlayFX(SoundName.MoveOneCard);
        else if (validCardCount == 2)
            SoundController.Instance?.PlayFX(SoundName.MoveTwoCards);
        else
            SoundController.Instance?.PlayFX(SoundName.DealManyCards);

        averageStart /= validCardCount;
        averageEnd /= validCardCount;

        float directionSign =
            Mathf.Abs(averageEnd.x - averageStart.x) > 0.01f
                ? Mathf.Sign(averageEnd.x - averageStart.x)
                : 1f;

        float settleTau = move.SettleTau;
        float cardDuration =
            Mathf.Max(move.flightDuration, 0.001f) * settleTau;
        float totalDuration =
            move.staggerPerCard * (count - 1) + cardDuration;

        float elapsed = 0f;

        while (elapsed < totalDuration) {
            elapsed += Time.deltaTime;

            for (int i = 0; i < count; i++) {
                Card card = cards[i];

                if (hasLanded[i] || card == null || targetSlots[i] == null) {
                    continue;
                }

                float localTime = elapsed - i * move.staggerPerCard;

                if (localTime < 0f) {
                    if (card.DynamicShadow != null) {
                        card.DynamicShadow.UpdateFlight(
                            startPositions[i],
                            0f,
                            0f,
                            0f,
                            0f
                        );
                    }

                    continue;
                }

                float tau = localTime / Mathf.Max(move.flightDuration, 0.001f);

                if (tau >= settleTau) {
                    hasLanded[i] = true;
                    tau = settleTau;
                }

                UpdateCardMotion(
                    card,
                    wrappers[i],
                    tau,
                    startPositions[i],
                    endPositions[i],
                    startScales[i],
                    directionSign
                );
            }

            yield return null;
        }

        for (int i = 0; i < count; i++) {
            if (cards[i] != null && targetSlots[i] != null && !hasLanded[i]) {
                UpdateCardMotion(
                    cards[i],
                    wrappers[i],
                    settleTau,
                    startPositions[i],
                    endPositions[i],
                    startScales[i],
                    directionSign
                );
            }
        }

        for (int i = 0;
             i < count;
             i++) {
            Card card =
                cards[i];

            CardSlot targetSlot =
                targetSlots[i];

            if (card == null ||
                targetSlot == null) {
                continue;
            }

            card.SetShadowEnabled(true);

            card.transform.SetParent(
                targetSlot.transform
            );

            card.transform.localPosition =
                Vector3.zero;

            card.transform.localRotation =
                Quaternion.identity;

            card.transform.localScale =
                Vector3.one;

        }

        // Resolve the Wild Card only after every moving card has landed and
        // has been parented to its destination slot.
        if (wildBindContext != null &&
            wildBindContext.Card != null &&
            wildBindContext.Card.BindWild(
                wildBindContext.ResolvedType,
                wildBindContext.ResolvedSprite))
        {
            PlayWildCardTransformEffect(wildBindContext.Card);
        }

        for (int i = 0; i < count; i++) {
            if (wrappers[i] != null) {
                Destroy(wrappers[i].gameObject);
            }
        }

        if (downgradeContext != null)
        {
            yield return PlayDowngradeEffect(downgradeContext);
        }

        _isMovingCardCluster = false;

        ReleaseMovingHolders(movementHolders);

        targetHolder.NormalizeSlotLayout();
        bool startedMerge =
            TryMergeCards(
                targetHolder
            );

        if (!startedMerge) {
            OnBoardChanged?.Invoke();
        }

        _cardsToMoveBuffer.Clear();
        _targetSlotsBuffer.Clear();
    }

    private static void PlayWildCardTransformEffect(Card card)
    {
        if (card == null || VFXController.Instance == null)
            return;

        VFXController.Instance.SpawnEffect(
            EffectName.SparkleGold,
            Vector3.zero,
            card.transform,
            Vector3.one * 0.75f,
            1.2f
        );
    }

    private IEnumerator PlayDowngradeEffect(
        DowngradeEffectContext context)
    {
        if (context == null ||
            context.TargetHolder == null ||
            context.ObstacleCard == null)
        {
            yield break;
        }

        context.ObstacleCard.PlayActivateEffect();

        List<Card> affectedCards = context.AffectedCards ?? new List<Card>();
        List<DowngradeCardVisualState> visualStates =
            CaptureDowngradeTrayVisuals(context);
        if (visualStates.Count == 0)
            yield break;

        DowngradeCardVisualState downgradeVisual = null;
        for (int i = 0; i < visualStates.Count; i++)
        {
            if (visualStates[i].IsObstacle)
            {
                downgradeVisual = visualStates[i];
                break;
            }
        }
        Vector3 gatherPosition = downgradeVisual != null
            ? downgradeVisual.StartPosition
            : visualStates[0].StartPosition;

        float duration = Mathf.Max(0.1f, downgradeEffectDuration);
        float hideDuration = duration * 0.18f;
        float gatherDuration = Mathf.Max(0.01f, mergeCompressDuration) +
            Mathf.Max(0, visualStates.Count - 1) * mergeCompressCardDelay;
        float spreadDuration = duration * 0.3f +
            Mathf.Max(0, visualStates.Count - 1) * mergeResultFlightStagger;
        float revealDuration = duration * 0.2f;

        // Hide every face, including the downgrade card.
        yield return AnimateDowngradePhase(hideDuration, progress =>
        {
            SetDowngradeFlip(visualStates,
                Mathf.Lerp(0f, 90f, EaseInOut(progress)));
        });

        SetDowngradeBackFace(visualStates);
        SetDowngradeFlip(visualStates, -90f);

        // Show the backs while collapsing the whole tray onto its highest card.
        yield return AnimateDowngradePhase(gatherDuration, progress =>
        {
            SetDowngradeFlip(visualStates,
                Mathf.Lerp(-90f, 0f, EaseInOut(progress)));
            GatherDowngradeCards(visualStates, gatherPosition, progress,
                gatherDuration);
        });

        // Resolve the tray while every card is hidden in one stack.
        ApplyDowngradeValues(affectedCards);
        SetDowngradeBackFace(visualStates);
        RemoveCardFromHolder(context.TargetHolder, context.ObstacleCard);
        context.ObstacleCard.PlayDestroyEffect();
        context.ObstacleCard.gameObject.SetActive(false);
        Destroy(context.ObstacleCard.gameObject);
        CompactHolderCards(context.TargetHolder);
        context.TargetHolder.NormalizeSlotLayout();
        CaptureDowngradeFinalLayout(visualStates);

        // Deal the remaining card backs into their compacted slots.
        yield return AnimateDowngradePhase(spreadDuration, progress =>
        {
            SpreadDowngradeCards(visualStates, progress, spreadDuration);
        });

        // Flip up only after the downgrade card has gone and slots are stable.
        yield return AnimateDowngradePhase(revealDuration * 0.5f, progress =>
        {
            SetDowngradeFinalFlip(visualStates,
                Mathf.Lerp(0f, 90f, EaseInOut(progress)));
        });
        SetDowngradeFrontFace(visualStates);
        SetDowngradeFinalFlip(visualStates, -90f);
        yield return AnimateDowngradePhase(revealDuration * 0.5f, progress =>
        {
            SetDowngradeFinalFlip(visualStates,
                Mathf.Lerp(-90f, 0f, EaseInOut(progress)));
        });

        RestoreDowngradeVisuals(visualStates);
        yield return PlayDowngradedCardsBlink(affectedCards);
    }

    private static IEnumerator AnimateDowngradePhase(
        float duration,
        System.Action<float> update)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            update?.Invoke(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        update?.Invoke(1f);
    }

    private static float EaseInOut(float progress)
    {
        return progress * progress * (3f - 2f * progress);
    }

    private static List<DowngradeCardVisualState> CaptureDowngradeTrayVisuals(
        DowngradeEffectContext context)
    {
        List<DowngradeCardVisualState> states =
            new List<DowngradeCardVisualState>();
        List<CardSlot> slots = context.TargetHolder.CardSlots;
        if (slots == null)
            return states;

        for (int i = 0; i < slots.Count; i++)
        {
            Card card = slots[i] != null ? slots[i].Card : null;
            if (card == null ||
                (card != context.ObstacleCard &&
                 !context.AffectedCards.Contains(card)))
                continue;

            states.Add(new DowngradeCardVisualState
            {
                Card = card,
                StartPosition = card.transform.position,
                StartRotation = card.transform.rotation,
                IsObstacle = card == context.ObstacleCard
            });
        }

        return states;
    }

    private static void SetDowngradeFlip(
        List<DowngradeCardVisualState> states,
        float yAngle)
    {
        for (int i = 0; i < states.Count; i++)
        {
            DowngradeCardVisualState state = states[i];
            if (state.Card == null)
                continue;

            state.Card.transform.rotation = state.StartRotation *
                Quaternion.Euler(0f, yAngle, 0f);
        }
    }

    private static void SetDowngradeBackFace(
        List<DowngradeCardVisualState> states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Card != null)
                states[i].Card.SetOffSprite();
        }
    }

    private void GatherDowngradeCards(
        List<DowngradeCardVisualState> states,
        Vector3 gatherPosition,
        float progress,
        float duration)
    {
        float longestTravel = 0.0001f;
        for (int i = 0; i < states.Count; i++)
        {
            states[i].GatherPosition = gatherPosition;
            longestTravel = Mathf.Max(longestTravel, Vector3.Distance(
                states[i].StartPosition, states[i].GatherPosition));
        }

        for (int i = 0; i < states.Count; i++)
        {
            DowngradeCardVisualState state = states[i];
            if (state.Card == null)
                continue;

            float delay = (states.Count - 1 - i) * mergeCompressCardDelay;
            float elapsed = progress * duration;
            float travelRatio = Vector3.Distance(
                state.StartPosition,
                state.GatherPosition) / longestTravel;
            float cardDuration = Mathf.Max(0.01f,
                mergeCompressDuration * travelRatio);
            float localProgress = Mathf.Clamp01(
                (elapsed - delay) / cardDuration);
            float moveProgress = Mathf.Pow(
                localProgress,
                mergeCompressFallPower);
            state.Card.transform.position = Vector3.Lerp(
                state.StartPosition,
                state.GatherPosition,
                moveProgress);
        }
    }

    private static void CaptureDowngradeFinalLayout(
        List<DowngradeCardVisualState> states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            DowngradeCardVisualState state = states[i];
            if (state.Card == null || state.IsObstacle)
                continue;

            state.FinalPosition = state.Card.transform.position;
            state.FinalRotation = state.Card.transform.rotation;
            state.Card.transform.position = state.GatherPosition;
            state.Card.transform.rotation = state.FinalRotation;
        }
    }

    private void SpreadDowngradeCards(
        List<DowngradeCardVisualState> states,
        float progress,
        float duration)
    {
        for (int i = 0; i < states.Count; i++)
        {
            DowngradeCardVisualState state = states[i];
            if (state.Card == null || state.IsObstacle)
                continue;

            float delay = i * mergeResultFlightStagger;
            float elapsed = progress * duration;
            float cardDuration = Mathf.Max(0.01f,
                duration - Mathf.Max(0, states.Count - 1) *
                mergeResultFlightStagger);
            float localProgress = Mathf.Clamp01(
                (elapsed - delay) / cardDuration);
            float moveProgress = EaseInOut(localProgress);
            state.Card.transform.position = Vector3.Lerp(
                state.GatherPosition,
                state.FinalPosition,
                moveProgress);
        }
    }

    private static void SetDowngradeFinalFlip(
        List<DowngradeCardVisualState> states,
        float yAngle)
    {
        for (int i = 0; i < states.Count; i++)
        {
            DowngradeCardVisualState state = states[i];
            if (state.Card == null || state.IsObstacle)
                continue;

            state.Card.transform.rotation = state.FinalRotation *
                Quaternion.Euler(0f, yAngle, 0f);
        }
    }

    private void SetDowngradeFrontFace(
        List<DowngradeCardVisualState> states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            Card card = states[i].Card;
            if (card == null || states[i].IsObstacle)
                continue;

            CardData data = cardConfig != null
                ? cardConfig.GetCardData(card.CardType)
                : null;
            card.SetIcon(data != null ? data.icon : null);
        }
    }

    private static void RestoreDowngradeVisuals(
        List<DowngradeCardVisualState> states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            DowngradeCardVisualState state = states[i];
            if (state.Card == null || state.IsObstacle)
                continue;

            state.Card.transform.position = state.FinalPosition;
            state.Card.transform.rotation = state.FinalRotation;
        }
    }

    private IEnumerator PlayDowngradedCardsBlink(List<Card> affectedCards)
    {
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        List<Color> originalColors = new List<Color>();
        for (int i = 0; i < affectedCards.Count; i++)
        {
            SpriteRenderer renderer = affectedCards[i] != null
                ? affectedCards[i].IconRenderer
                : null;
            if (renderer == null)
                continue;

            renderers.Add(renderer);
            originalColors.Add(renderer.color);
        }

        float duration = Mathf.Max(0.1f, downgradeBlinkDuration);
        int blinkCount = Mathf.Max(1, downgradeBlinkCount);
        yield return AnimateDowngradePhase(duration, progress =>
        {
            float intensity = Mathf.Abs(
                Mathf.Sin(progress * Mathf.PI * blinkCount));
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color tint = downgradeBlinkColor;
                tint.a = originalColors[i].a;
                renderers[i].color = Color.Lerp(
                    originalColors[i], tint, intensity);
            }
        });

        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = originalColors[i];
        }
    }

    private void ApplyDowngradeValues(List<Card> cards)
    {
        if (cards == null)
            return;

        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card == null || !card.IsNumberCard)
                continue;

            int currentValue = (int)card.CardType;
            CardType downgradedType = (CardType)Mathf.Max(
                (int)CardType.Card1,
                currentValue - 1
            );
            card.CardType = downgradedType;

            CardData data = cardConfig != null
                ? cardConfig.GetCardData(downgradedType)
                : null;
            card.SetIcon(data != null ? data.icon : null);
        }
    }

    private static void RemoveCardFromHolder(
        CardSlotHolder holder,
        Card card)
    {
        if (holder == null || card == null || holder.CardSlots == null)
            return;

        for (int i = 0; i < holder.CardSlots.Count; i++)
        {
            CardSlot slot = holder.CardSlots[i];
            if (slot != null && slot.Card == card)
            {
                slot.RemoveCard();
                return;
            }
        }
    }
    
    private void UpdateCardMotion(
        Card card,
        Transform wrapper,
        float tau,
        Vector3 startPos,
        Vector3 endPos,
        float startScale,
        float directionSign
    ) {
        if (wrapper == null)
            return;

        CardMoveSettings move = cardMove;
        float flightTau = Mathf.Clamp01(tau);

        bool isVerticalMove = Mathf.Abs(endPos.x - startPos.x) < 0.5f;

        float pathT = Mathf.Clamp01(move.positionCurve.Evaluate(flightTau));
        Vector3 ground = Vector3.Lerp(startPos, endPos, pathT);

        float peak =
            move.arcHeight +
            Vector3.Distance(startPos, endPos) * move.arcHeightPerDistance;

        if (isVerticalMove)
            peak *= move.verticalMoveArcScale;

        float height = tau >= 1f
            ? 0f
            : peak * Mathf.Max(0f, move.heightCurve.Evaluate(flightTau));

        wrapper.position = ground + Vector3.up * height;

        float rotationZ = 0f;

        if (move.spin && !isVerticalMove) {
            rotationZ =
                360f *
                move.spinRounds *
                Mathf.Clamp01(move.spinCurve.Evaluate(flightTau)) *
                -directionSign;
        }

        card.transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);        float airborneFactor =
            startScale / Mathf.Max(move.referenceAirborneScale, 0.01f);

        float release = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(move.airborneReleaseTau, 1f, tau)
        );

        float scaleFactor = Mathf.Lerp(airborneFactor, 1f, release);
        float scaleX = move.scaleXCurve.Evaluate(tau) * scaleFactor;
        float scaleY = move.scaleYCurve.Evaluate(tau) * scaleFactor;

        wrapper.localScale = new Vector3(scaleX, scaleY, 1f);

        if (card.DynamicShadow != null) {
            float shadowHeight = height;

            if (tau >= 1f) {
                float settleT = Mathf.InverseLerp(1f, move.SettleTau, tau);
                shadowHeight = move.landShadowHeight * (1f - settleT);
            }

            card.DynamicShadow.UpdateFlight(
                ground,
                shadowHeight,
                move.shadowDropPerHeight,
                move.shadowSidePerHeight,
                move.shadowScalePerHeight
            );
        }
    }

    private static float EaseOutElasticCustom(
        float t
    )
    {
        if (t == 0f)
            return 0f;

        if (t == 1f)
            return 1f;

        float c4 =
            (2f * Mathf.PI) /
            3f;

        return
            Mathf.Pow(
                2f,
                -10f * t
            ) *
            Mathf.Sin(
                (
                    t * 10f -
                    0.75f
                ) *
                c4
            ) +
            1f;
    }

    private static float EaseLinear(
        float t
    )
    {
        return Mathf.Clamp01(t);
    }

    private static float EaseIn(
        float t
    )
    {
        t = Mathf.Clamp01(t);

        return 
            t * 
            t * 
            t;
    }

    private static float EaseOut(
        float t
    )
    {
        t = Mathf.Clamp01(t);

        return 
            1f - 
            Mathf.Pow(
                1f - t, 
                3f
            );
    }
    
    private static float EaseInOutSine(
        float t
    )
    {
        t = Mathf.Clamp01(t);

        return
            -(Mathf.Cos(Mathf.PI * t) - 1f) /
            2f;
    }
    
    private static float EaseOutCubic(
        float t
    )
    {
        t = Mathf.Clamp01(t);

        return
            1f -
            Mathf.Pow(
                1f - t,
                3f
            );
    }

    private static float EaseInOutCubic(
        float t
    )
    {
        t = Mathf.Clamp01(t);

        if (t < 0.5f)
        {
            return
                4f *
                t *
                t *
                t;
        }

        return
            1f -
            Mathf.Pow(
                -2f * t + 2f,
                3f
            ) /
            2f;
    }

    public bool TryMergeCards(
        CardSlotHolder holder
    )
    {
        if (holder == null ||
            _movingHolders.Contains(holder) ||
            _mergingHolders.Contains(holder))
            return false;

        if (holder.CardSlots == null ||
            holder.CardSlots.Count < 2)
            return false;

        if (holder.GetEmptySlotCount() > 0)
            return false;

        CardType? firstType =
            null;

        bool allSame =
            true;

        List<Card> mergeCards =
            new List<Card>();

        foreach (CardSlot slot in
                 holder.CardSlots)
        {
            if (slot == null ||
                slot.IsEmpty ||
                slot.Card == null ||
                slot.Card.IsFrozen)
            {
                allSame = false;
                break;
            }

            mergeCards.Add(
                slot.Card
            );

            if (firstType == null)
            {
                firstType =
                    slot.Card.CardType;
            }
            else if (
                slot.Card.CardType !=
                firstType.Value
            )
            {
                allSame = false;
                break;
            }
        }

        if (!allSame ||
            firstType == null ||
            mergeCards.Count < 2)
        {
            return false;
        }

        CardType currentType =
            firstType.Value;

        if (currentType >=
            CardType.Card20)
        {
            return false;
        }

        CardType nextType =
            currentType + 1;

        CardData nextCardData =
            cardConfig != null
                ? cardConfig.GetCardData(nextType)
                : null;
        MergeGoldRewardEntry goldReward = mergeGoldRewardConfig != null
            ? mergeGoldRewardConfig.GetReward(nextType)
            : default;

        _mergingHolders.Add(holder);

        StartCoroutine(
            AnimateMergeCards(
                holder,
                mergeCards,
                nextType,
                nextCardData != null
                    ? nextCardData.icon
                    : null,
                goldReward
            )
        );

        return true;
    }

    private IEnumerator AnimateMergeCards(
        CardSlotHolder holder,
        List<Card> cards,
        CardType nextType,
        Sprite nextFaceSprite,
        MergeGoldRewardEntry goldReward
    )
    {
        List<int> sortingOffsets =
            new List<int>(cards.Count);

        for (int i = 0; i < holder.CardSlots.Count; i++)
        {
            CardSlot slot = holder.CardSlots[i];

            if (slot == null)
                continue;

            slot.RemoveCard();
        }

        holder.NotifyCardsChanged();

        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];

            if (card == null)
            {
                sortingOffsets.Add(0);
                continue;
            }

            card.SetSelected(false, animate: false);
            card.transform.SetParent(
                transform,
                true
            );
            card.transform.rotation =
                Quaternion.identity;
            card.transform.localScale =
                Vector3.one;

            if (card.DynamicShadow) card.DynamicShadow.SetShadowEnabled(true);
            
            int sortingOffset =
                mergeSortingOrderOffset + i * mergeSortingOrderOffset;

            sortingOffsets.Add(
                sortingOffset
            );
            card.SetSortingOrder(
                sortingOffset
            );
        }

        yield return StartCoroutine(
            AnimateCardFlip(
                cards,
                false,
                null,
                mergeFlipDuration,
                mergeFlipStagger
            )
        );

        GetBottomAndTopResultSlots(
            holder,
            out CardSlot firstResultSlot,
            out CardSlot secondResultSlot
        );

        if (firstResultSlot == null ||
            secondResultSlot == null)
        {
            _mergingHolders.Remove(holder);
            yield break;
        }

        Vector3 mergeCenter =
            firstResultSlot.transform.position + mergeCompressCenterOffset;

        bool mergeGoldRewardStarted = false;
        Action playMergeGoldReward = null;
        if (goldReward.goldAmount > 0)
        {
            playMergeGoldReward = () =>
            {
                if (mergeGoldRewardStarted)
                    return;

                mergeGoldRewardStarted = true;
                PopupInGame popupInGame = PopupController.Instance != null
                    ? PopupController.Instance.Get<PopupInGame>() as PopupInGame
                    : null;

                if (popupInGame != null)
                {
                    popupInGame.PlayMergeGoldReward(
                        mergeCenter,
                        _mainCamera,
                        goldReward.goldAmount,
                        goldReward.visualCoinCount
                    );
                }
                else if (Data.PlayerData != null)
                {
                    Data.PlayerData.CurrentGold += goldReward.goldAmount;
                }
            };
        }

        yield return StartCoroutine(
            CompressMergeStack(
                cards,
                mergeCenter,
                playMergeGoldReward
            )
        );

        holder.PlayActiveSoftEffect();

        const int resultCount = 2;
        List<Card> resultCards =
            new List<Card>(resultCount);
        List<int> resultSortingOffsets =
            new List<int>(resultCount);

        int firstResultIndex =
            Mathf.Max(
                0,
                cards.Count - resultCount
            );

        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];

            if (card == null)
                continue;

            if (i < firstResultIndex)
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
                continue;
            }

            resultCards.Add(card);
            resultSortingOffsets.Add(
                sortingOffsets[i]
            );

            card.transform.position =
                mergeCenter +
                Vector3.up *
                ((resultCards.Count - 1) *
                 mergeResultStackSpacing);
            card.transform.rotation =
                Quaternion.identity;
            card.transform.localScale =
                Vector3.one;

            card.CardType = nextType;

            if (nextFaceSprite != null)
            {
                card.SetIcon(nextFaceSprite);
            }

        }

        List<CardSlot> resultSlots =
            new List<CardSlot>
            {
                holder.CardSlots[0],
                holder.CardSlots[1]
            };

        yield return StartCoroutine(
            FlyMergeResultsToSlots(
                resultCards,
                resultSlots
            )
        );

        for (int i = 0; i < resultCards.Count; i++)
        {
            Card card = resultCards[i];
            CardSlot slot = resultSlots[i];

            if (card == null || slot == null)
                continue;

            slot.SetCard(card);
            card.transform.localPosition =
                Vector3.zero;
            card.transform.localRotation =
                Quaternion.identity;
            card.transform.localScale =
                Vector3.one;
        }

        holder.NormalizeSlotLayout();
        holder.NotifyCardsChanged();

        if (cards.Count == CardSlotHolder.MAX_SLOTS)
            SoundController.Instance?.PlayFX(SoundName.StackCardComplete);

        if (IsPendingTarget(nextType) && resultCards.Count > 0)
        {
            Card targetCard = resultCards[resultCards.Count - 1];
            if (targetCard != null)
            {
                yield return StartCoroutine(
                    PlayMergeTargetCelebration(targetCard)
                );
                holder.NormalizeSlotLayout();
                holder.NotifyCardsChanged();
            }
        }

        OnStackCompleted?.Invoke(holder, cards.Count);

        _mergingHolders.Remove(holder);

        if (_mergingHolders.Count == 0)
        {
            OnBoardChanged?.Invoke();
        }
    }

    private bool IsPendingTarget(CardType cardType)
    {
        if (!enableMergeTargetCelebration || cardTargets == null)
            return false;

        for (int i = 0; i < cardTargets.Count; i++)
        {
            CardTarget target = cardTargets[i];
            if (target != null && !target.IsCompleted &&
                target.cardType == cardType)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator PlayMergeTargetCelebration(Card card)
    {
        if (card == null)
            yield break;

        _activeMergeTargetCelebrations++;

        Transform cardTransform = card.transform;
        Transform originalParent = cardTransform.parent;
        Vector3 originalLocalPosition = cardTransform.localPosition;
        Quaternion originalLocalRotation = cardTransform.localRotation;
        Vector3 originalLocalScale = cardTransform.localScale;
        Vector3 startWorldPosition = cardTransform.position;
        Quaternion startWorldRotation = cardTransform.rotation;
        int originalSortingOrder = card.SortIndex;

        DynamicShadow shadow = card.DynamicShadow;
        bool originalShadowEnabled = shadow != null && shadow.isShadowEnabled;
        bool originalPreventShadowDisable =
            shadow != null && shadow.preventShadowDisable;
        bool originalFollowSourceRotation =
            shadow != null && shadow.followSourceRotation;
        bool originalFollowSourceYRotation =
            shadow != null && shadow.followSourceYRotation;

        Vector3 targetWorldPosition = GetMergeTargetCenter(startWorldPosition);
        float sideDirection = startWorldPosition.x <= targetWorldPosition.x
            ? -1f
            : 1f;
        Vector3 controlPoint =
            (startWorldPosition + targetWorldPosition) * 0.5f +
            Vector3.up * Mathf.Max(0f, mergeTargetArcHeight) +
            Vector3.right * (sideDirection * mergeTargetArcSideOffset);

        Vector3 spinAxis = GetMergeTargetBdSpinAxis(card);
        float flyOutDuration = Mathf.Max(0.01f, mergeTargetFlyOutDuration);
        float spinTurns = Mathf.Max(0f, mergeTargetSpinTurns);
        float spinSpeed = Mathf.Max(0.01f, mergeTargetSpinSpeed);
        float spinDuration = spinTurns / spinSpeed;
        float totalSpinAngle = 360f * spinTurns;
        float returnDuration = Mathf.Max(0.01f, mergeTargetReturnDuration);
        Vector3 peakScale = originalLocalScale *
                            Mathf.Max(1f, mergeTargetPeakScale);

        card.SetSelected(false, animate: false);
        card.SetSortingOrder(Mathf.Max(
            originalSortingOrder + 1,
            mergeTargetSortingOrder
        ));

        if (shadow != null)
        {
            shadow.SwitchShadow(true);
            shadow.preventShadowDisable = true;
            shadow.followSourceRotation = true;
            shadow.followSourceYRotation = true;
            shadow.SetShadowEnabled(true);
        }

        float elapsed = 0f;
        while (elapsed < flyOutDuration && card != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / flyOutDuration);
            float eased = EaseOutCubic(progress);

            cardTransform.position = EvaluateQuadraticBezier(
                startWorldPosition,
                controlPoint,
                targetWorldPosition,
                eased
            );
            cardTransform.localScale = Vector3.LerpUnclamped(
                originalLocalScale,
                peakScale,
                eased
            );
            cardTransform.rotation = startWorldRotation;

            shadow?.UpdateMergeFlightCurveMovement(
                progress,
                startWorldPosition,
                targetWorldPosition
            );
            yield return null;
        }

        if (card != null)
        {
            cardTransform.position = targetWorldPosition;
            cardTransform.localScale = peakScale;
            cardTransform.rotation = startWorldRotation;
            PlayMergeTargetPeakFx(card, targetWorldPosition);
        }

        elapsed = 0f;
        while (elapsed < spinDuration && card != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / spinDuration);
            float eased = EaseInOutCubic(progress);

            cardTransform.position = targetWorldPosition;
            cardTransform.localScale = peakScale;
            ApplyMergeTargetSpin(
                cardTransform,
                startWorldRotation,
                spinAxis,
                totalSpinAngle * eased
            );
            yield return null;
        }

        if (card != null)
        {
            cardTransform.position = targetWorldPosition;
            cardTransform.localScale = peakScale;
            cardTransform.rotation = startWorldRotation;
        }

        elapsed = 0f;
        while (elapsed < returnDuration && card != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / returnDuration);
            float eased = EaseInOutCubic(progress);

            cardTransform.position = EvaluateQuadraticBezier(
                targetWorldPosition,
                controlPoint,
                startWorldPosition,
                eased
            );
            cardTransform.localScale = Vector3.LerpUnclamped(
                peakScale,
                originalLocalScale,
                eased
            );
            cardTransform.rotation = startWorldRotation;

            shadow?.UpdateMergeFlightCurveMovement(
                progress,
                targetWorldPosition,
                startWorldPosition
            );
            yield return null;
        }

        if (card != null)
        {
            cardTransform.SetParent(originalParent, false);
            cardTransform.localPosition = originalLocalPosition;
            cardTransform.localRotation = originalLocalRotation;
            cardTransform.localScale = originalLocalScale;
            card.SetSortingOrder(originalSortingOrder);

            if (shadow != null)
            {
                shadow.SwitchShadow(false);
                shadow.preventShadowDisable = originalPreventShadowDisable;
                shadow.followSourceRotation = originalFollowSourceRotation;
                shadow.followSourceYRotation = originalFollowSourceYRotation;
                shadow.SetShadowEnabled(originalShadowEnabled);
            }
        }

        _activeMergeTargetCelebrations = Mathf.Max(
            0,
            _activeMergeTargetCelebrations - 1
        );
    }

    private void PlayMergeTargetPeakFx(Card card, Vector3 targetWorldPosition)
    {
        if (mergeTargetPeakFx == null)
            return;

        if (_mergeTargetPeakFxDisableRoutine != null)
        {
            StopCoroutine(_mergeTargetPeakFxDisableRoutine);
            _mergeTargetPeakFxDisableRoutine = null;
        }

        mergeTargetPeakFx.SetActive(false);
        mergeTargetPeakFx.transform.position =
            targetWorldPosition + mergeTargetPeakFxOffset;

        int sortingLayerId = 0;
        if (card != null)
        {
            SpriteRenderer cardRenderer = card.IconRenderer != null
                ? card.IconRenderer
                : card.GetComponentInChildren<SpriteRenderer>(true);
            if (cardRenderer != null)
                sortingLayerId = cardRenderer.sortingLayerID;

            SetLayerRecursively(
                mergeTargetPeakFx.transform,
                card.gameObject.layer
            );
        }

        Renderer[] renderers =
            mergeTargetPeakFx.GetComponentsInChildren<Renderer>(true);
        int highestAuthoredOrder = int.MinValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!_mergeTargetPeakFxAuthoredSortingOrders.TryGetValue(
                    renderer,
                    out int authoredOrder))
            {
                authoredOrder = renderer.sortingOrder;
                _mergeTargetPeakFxAuthoredSortingOrders[renderer] =
                    authoredOrder;
            }

            highestAuthoredOrder = Mathf.Max(
                highestAuthoredOrder,
                authoredOrder
            );
        }

        if (highestAuthoredOrder == int.MinValue)
            highestAuthoredOrder = 0;

        int highestFxSortingOrder = mergeTargetSortingOrder -
            Mathf.Max(1, Mathf.Abs(mergeTargetPeakFxSortingOffset));

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null ||
                !_mergeTargetPeakFxAuthoredSortingOrders.TryGetValue(
                    renderer,
                    out int authoredOrder))
            {
                continue;
            }

            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = highestFxSortingOrder +
                                    authoredOrder -
                                    highestAuthoredOrder;
        }

        mergeTargetPeakFx.SetActive(true);

        ParticleSystem[] particleSystems =
            mergeTargetPeakFx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
            particleSystems[i].Play(true);
        }

        Animator[] animators =
            mergeTargetPeakFx.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].enabled = true;
            animators[i].Play(0, 0, 0f);
        }

        Animation[] animations =
            mergeTargetPeakFx.GetComponentsInChildren<Animation>(true);
        for (int i = 0; i < animations.Length; i++)
        {
            animations[i].Stop();
            animations[i].Play();
        }

        _mergeTargetPeakFxDisableRoutine = StartCoroutine(
            DisableMergeTargetPeakFxAfterDelay()
        );
    }

    private IEnumerator DisableMergeTargetPeakFxAfterDelay()
    {
        yield return new WaitForSeconds(
            Mathf.Max(0.05f, mergeTargetPeakFxLifetime)
        );

        if (mergeTargetPeakFx != null)
            mergeTargetPeakFx.SetActive(false);

        _mergeTargetPeakFxDisableRoutine = null;
    }

    private Vector3 GetMergeTargetCenter(Vector3 sourceWorldPosition)
    {
        if (_mainCamera == null)
            return sourceWorldPosition;

        Vector3 sourceScreenPosition =
            _mainCamera.WorldToScreenPoint(sourceWorldPosition);
        Vector2 viewport = new Vector2(
            Mathf.Clamp01(mergeTargetCenterViewport.x),
            Mathf.Clamp01(mergeTargetCenterViewport.y)
        );
        Vector3 targetScreenPosition = new Vector3(
            Screen.width * viewport.x,
            Screen.height * viewport.y,
            sourceScreenPosition.z
        );
        Vector3 targetWorldPosition =
            _mainCamera.ScreenToWorldPoint(targetScreenPosition);
        targetWorldPosition.z = sourceWorldPosition.z;
        return targetWorldPosition;
    }

    private static Vector3 EvaluateQuadraticBezier(
        Vector3 start,
        Vector3 control,
        Vector3 end,
        float progress)
    {
        float t = Mathf.Clamp01(progress);
        float inverse = 1f - t;
        return inverse * inverse * start +
               2f * inverse * t * control +
               t * t * end;
    }

    private static void ApplyMergeTargetSpin(
        Transform target,
        Quaternion baseRotation,
        Vector3 spinAxis,
        float angle)
    {
        if (target == null)
            return;

        target.rotation = baseRotation * Quaternion.AngleAxis(angle, spinAxis);
    }

    private static Vector3 GetMergeTargetBdSpinAxis(Card card)
    {
        // Card corners are ordered clockwise:
        // A (top-left), B (top-right), C (bottom-right), D (bottom-left).
        // Therefore BD runs from the top-right to the bottom-left.
        if (card == null)
            return new Vector3(-0.65f, -1f, 0f).normalized;

        SpriteRenderer renderer = card.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = card.IconRenderer;

        if (renderer == null || renderer.sprite == null)
            return new Vector3(-0.65f, -1f, 0f).normalized;

        Vector3 spriteSize = renderer.sprite.bounds.size;
        Vector3 rendererBdAxis = new Vector3(
            -Mathf.Max(0.0001f, spriteSize.x),
            -Mathf.Max(0.0001f, spriteSize.y),
            0f
        );
        Vector3 worldBdAxis = renderer.transform.TransformVector(rendererBdAxis);
        Vector3 cardLocalBdAxis =
            card.transform.InverseTransformVector(worldBdAxis);

        return cardLocalBdAxis.sqrMagnitude > 0.0001f
            ? cardLocalBdAxis.normalized
            : new Vector3(-0.65f, -1f, 0f).normalized;
    }

    private void GetBottomAndTopResultSlots(
        CardSlotHolder holder,
        out CardSlot bottomSlot,
        out CardSlot topSlot
    )
    {
        bottomSlot = null;
        topSlot = null;

        float lowestScreenY =
            float.PositiveInfinity;
        float highestScreenY =
            float.NegativeInfinity;

        for (int i = 0; i < holder.CardSlots.Count; i++)
        {
            CardSlot slot = holder.CardSlots[i];

            if (slot == null)
                continue;

            float screenY =
                _mainCamera != null
                    ? _mainCamera.WorldToScreenPoint(
                        slot.transform.position
                    ).y
                    : slot.transform.position.y;

            if (screenY < lowestScreenY)
            {
                lowestScreenY =
                    screenY;
                bottomSlot =
                    slot;
            }

            if (screenY > highestScreenY)
            {
                highestScreenY =
                    screenY;
                topSlot =
                    slot;
            }
        }
    }

    private sealed class MergeFlipCard
    {
        public Card Card;
        public SpriteRenderer Icon;
        public CardBendSurface Bend;
        public Sprite From;
        public Sprite To;
        public Vector3 BaseScale;
        public Quaternion BaseRotation;
    }

    private IEnumerator AnimateCardFlip(
        List<Card> cards,
        bool showFront,
        Sprite frontSprite,
        float duration,
        float delayBetweenEachCard
    ) {
        List<MergeFlipCard> flipCards =
            new List<MergeFlipCard>(cards.Count);

        for (int i = 0; i < cards.Count; i++) {
            Card card = cards[i];

            if (card == null || card.IconRenderer == null) {
                flipCards.Add(null);
                continue;
            }

            Sprite from = card.IconRenderer.sprite;
            Sprite to = showFront ? frontSprite : card.OffSprite;

            flipCards.Add(
                new MergeFlipCard {
                    Card = card,
                    Icon = card.IconRenderer,
                    From = from,
                    To = to != null ? to : from,
                    BaseScale = card.transform.localScale,
                    BaseRotation = card.transform.localRotation
                }
            );
        }

        float lift = Mathf.Max(1f, mergeFlipScaleUp);
        float liftDuration = Mathf.Max(0f, mergeFlipLiftDuration);
        float safeDuration = Mathf.Max(0.01f, duration);
        float settleDuration = Mathf.Max(0f, mergeFlipSettleDuration);
        float stagger = Mathf.Max(0f, delayBetweenEachCard);

        float elapsed = 0f;

        while (elapsed < liftDuration) {
            float rise =
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / liftDuration));

            ApplyMergeFlipPose(flipCards, Mathf.Lerp(1f, lift, rise), 0f, 1f, 1f);
            yield return null;
            elapsed += Time.deltaTime;
        }

        BeginMergeFlipBend(flipCards);

        float totalDuration =
            safeDuration +
            Mathf.Max(0, cards.Count - 1) * stagger;

        elapsed = 0f;

        while (elapsed < totalDuration) {
            ApplyMergeFlipTurn(flipCards, lift, elapsed, safeDuration, stagger);
            yield return null;
            elapsed += Time.deltaTime;
        }

        ApplyMergeFlipTurn(flipCards, lift, totalDuration, safeDuration, stagger);
        EndMergeFlipBend(flipCards);

        elapsed = 0f;

        while (elapsed < settleDuration) {
            float k = Mathf.Clamp01(elapsed / settleDuration);
            float drop = Mathf.SmoothStep(0f, 1f, k);
            float squash = mergeFlipSettleSquash * Mathf.Sin(k * Mathf.PI);

            ApplyMergeFlipPose(
                flipCards,
                Mathf.Lerp(lift, 1f, drop),
                0f,
                1f + squash,
                1f - squash
            );

            yield return null;
            elapsed += Time.deltaTime;
        }

        ApplyMergeFlipPose(flipCards, 1f, 0f, 1f, 1f);
    }

    private void ApplyMergeFlipPose(
        List<MergeFlipCard> flipCards,
        float uniform,
        float lean,
        float stretchX,
        float stretchY
    ) {
        for (int i = 0; i < flipCards.Count; i++) {
            MergeFlipCard state = flipCards[i];

            if (state == null || state.Card == null)
                continue;

            Transform cardTransform = state.Card.transform;

            cardTransform.localRotation =
                state.BaseRotation * Quaternion.Euler(0f, 0f, lean);

            cardTransform.localScale = new Vector3(
                state.BaseScale.x * uniform * stretchX,
                state.BaseScale.y * uniform * stretchY,
                state.BaseScale.z
            );
        }
    }

    private void ApplyMergeFlipTurn(
        List<MergeFlipCard> flipCards,
        float lift,
        float elapsed,
        float duration,
        float stagger
    ) {
        for (int i = 0; i < flipCards.Count; i++) {
            MergeFlipCard state = flipCards[i];

            if (state == null || state.Card == null)
                continue;

            float localTime = elapsed - i * stagger;

            float turn = localTime <= 0f
                ? 0f
                : Mathf.Clamp01(
                    mergeFlipCurve.Evaluate(Mathf.Clamp01(localTime / duration))
                );

            float bend = Mathf.Sin(turn * Mathf.PI);
            float yaw = 180f * turn;

            Transform cardTransform = state.Card.transform;

            cardTransform.localRotation =
                state.BaseRotation *
                Quaternion.Euler(0f, 0f, cardFlipRotateZ * bend);

            cardTransform.localScale = new Vector3(
                state.BaseScale.x * lift,
                state.BaseScale.y * lift * (1f + mergeFlipBendStretch * bend),
                state.BaseScale.z
            );

            if (state.Bend == null)
                continue;

            state.Bend.SetSprite(yaw > 90f ? state.To : state.From);
            state.Bend.SetPose(yaw, mergeFlipCurveAngle * bend, mergeFlipCurveShade);
        }
    }

    private void BeginMergeFlipBend(List<MergeFlipCard> flipCards) {
        for (int i = 0; i < flipCards.Count; i++) {
            MergeFlipCard state = flipCards[i];

            if (state == null || state.Card == null || state.Icon == null)
                continue;

            state.Bend = CardBendSurface.For(state.Card);

            if (state.Bend == null)
                continue;

            state.Bend.Begin(state.Icon, state.From);
            state.Icon.enabled = false;
        }
    }

    private void EndMergeFlipBend(List<MergeFlipCard> flipCards) {
        for (int i = 0; i < flipCards.Count; i++) {
            MergeFlipCard state = flipCards[i];

            if (state == null || state.Card == null)
                continue;

            if (state.Bend != null) {
                state.Bend.End();
                state.Bend = null;
            }

            if (state.To != null)
                state.Card.SetIcon(state.To);

            if (state.Icon != null)
                state.Icon.enabled = true;
        }
    }

    private IEnumerator CompressMergeStack(
        List<Card> cards,
        Vector3 mergeCenter,
        Action onMergeReached = null
    ) {
        if (cards == null || cards.Count == 0) yield break;

        Vector3[] startPositions = new Vector3[cards.Count];
        Vector3[] targetPositions = new Vector3[cards.Count];
        float[] travelRatios = new float[cards.Count];
        bool[] hasFinishedMoving = new bool[cards.Count];

        Card baseCard = null;
        bool baseCardFinishedMove = false;

        float longestTravel = 0.0001f;

        for (int i = 0; i < cards.Count; i++) {
            if (cards[i] == null)
                continue;

            startPositions[i] = cards[i].transform.position;
            targetPositions[i] = mergeCenter + Vector3.up * (i * mergeStackSpacing);

            longestTravel = Mathf.Max(
                longestTravel,
                Vector3.Distance(startPositions[i], targetPositions[i])
            );
        }

        for (int i = 0; i < cards.Count; i++) {
            if (cards[i] == null)
                continue;

            travelRatios[i] =
                Vector3.Distance(startPositions[i], targetPositions[i]) /
                longestTravel;
        }

        for (int i = cards.Count - 1; i >= 0; i--) {
            if (cards[i] != null) {
                baseCard = cards[i];
                break;
            }
        }

        float compressDuration = Mathf.Max(0.01f, mergeCompressDuration);
        float popDuration = Mathf.Max(0.01f, mergeCompressPopScaleDuration);

        float totalRoutineDuration = 0f;

        for (int i = 0; i < cards.Count; i++) {
            if (cards[i] == null)
                continue;

            totalRoutineDuration = Mathf.Max(
                totalRoutineDuration,
                ((cards.Count - 1 - i) * mergeCompressCardDelay) +
                Mathf.Max(0.01f, compressDuration * travelRatios[i])
            );
        }
        float elapsed = 0f;

        bool isBaseCardPopping = false;
        float currentPopTimer = 0f;
        bool mergeReachedNotified = false;

        foreach (var card in cards) {
            if (card) {
                card.DynamicShadow.baseShadowScale = mergeCompressShadowScaleUp;
                card.DynamicShadow.enableScaleMultiplier = false;
                card.DynamicShadow.enableDistanceMultiplier = false;
            }
        }

        while (elapsed < totalRoutineDuration) {
            elapsed += Time.deltaTime;

            for (int i = 0; i < cards.Count; i++) {
                Card card = cards[i];
                if (card == null) continue;

                float cardStartTime = (cards.Count - 1 - i) * mergeCompressCardDelay;
                float cardTime = elapsed - cardStartTime;

                if (cardTime < 0f) continue;

                Vector3 targetPosition = targetPositions[i];

                float cardDuration =
                    Mathf.Max(0.01f, compressDuration * travelRatios[i]);

                if (cardTime <= cardDuration) {
                    float progress = Mathf.Clamp01(cardTime / cardDuration);

                    float scaleProgress = Mathf.Clamp01(progress / 0.3f);
                    float scaleEased = Mathf.SmoothStep(0f, 1f, scaleProgress);

                    float moveEased = Mathf.Pow(progress, mergeCompressFallPower);

                    card.transform.localScale = Vector3.Lerp(Vector3.one, mergeCompressScale, scaleEased);
                    card.transform.position = Vector3.Lerp(startPositions[i], targetPosition, moveEased);
                }

                else {
                    card.transform.position = targetPosition;

                    if (card != baseCard) {
                        card.transform.localScale = Vector3.one;
                    }

                    if (!hasFinishedMoving[i]) {
                        hasFinishedMoving[i] = true;

                        if (card == baseCard) {
                            baseCardFinishedMove = true;

                            if (!mergeReachedNotified)
                            {
                                mergeReachedNotified = true;
                                onMergeReached?.Invoke();
                            }
                        }

                        if (baseCardFinishedMove && !isBaseCardPopping) {
                            isBaseCardPopping = true;
                            currentPopTimer = 0f;
                        }
                    }
                }
            }

            if (isBaseCardPopping && baseCard != null) {
                currentPopTimer += Time.deltaTime;
                float popProgress = Mathf.Clamp01(currentPopTimer / popDuration);

                if (popProgress <= 0.5f) {
                    float t = popProgress * 2f;
                    float easedT = Mathf.SmoothStep(0f, 1f, t);
                    baseCard.transform.localScale = Vector3.Lerp(mergeCompressScale, mergeCompressPopScale, easedT);
                }

                else {
                    float t = (popProgress - 0.5f) * 2f;
                    float easedT = Mathf.SmoothStep(0f, 1f, t);
                    baseCard.transform.localScale = Vector3.Lerp(mergeCompressPopScale, mergeCompressScale, easedT);
                }

                if (currentPopTimer >= popDuration) {
                    isBaseCardPopping = false;
                    baseCard.transform.localScale = mergeCompressScale;
                }
            }
            else if (baseCard != null && baseCardFinishedMove && !isBaseCardPopping) {
                baseCard.transform.localScale = Vector3.one;
            }

            yield return null;
        }

        for (int i = 0; i < cards.Count; i++) {
            Card card = cards[i];
            if (card != null) {
                card.transform.position = targetPositions[i];
                card.transform.localScale = mergeCompressScale;
                card.DynamicShadow.baseShadowScale = Vector3.one;
                card.DynamicShadow.enableScaleMultiplier = true;
                card.DynamicShadow.enableDistanceMultiplier = true;
            }
        }

        if (!mergeReachedNotified)
            onMergeReached?.Invoke();
    }

    private IEnumerator FlyMergeResultsToSlots(
        List<Card> cards,
        List<CardSlot> targetSlots
    ) {
        int count = Mathf.Min(cards.Count, targetSlots.Count);
        Vector3[] startPositions = new Vector3[count];

        for (int i = 0; i < count; i++) {
            if (cards[i] != null) {
                startPositions[i] = cards[i].transform.position;
                cards[i].DynamicShadow.SwitchShadow(true);
            }
        }

        float elapsed = 0f;
        float preFlightDur = Mathf.Max(0f, mergeResultPreFlightDuration);
        float flightDur = Mathf.Max(0.01f, mergeResultFlightDuration);
        float landingDur = Mathf.Max(0.01f, mergeResultLandingDuration);
        float stagger = Mathf.Max(0f, mergeResultFlightStagger);

        float startInterval = stagger;
        float totalDuration = flightDur + landingDur + Mathf.Max(0, count - 1) * startInterval;

        int halfCount = count / 2;
        List<bool> hasReversedSortingOrder = new List<bool>(halfCount);
        for (int i = 0; i < halfCount; i++) {
            hasReversedSortingOrder.Add(false);
        }

        bool[] shadowDisabled = new bool[count];
        const float apexProgress = 0.58f;

        float rotationTurns = Mathf.Max(1f, Mathf.Round(mergeResultRotationRounds));
        float rotationCurveEnd = mergeResultRotationCurve.Evaluate(1f);

        if (Mathf.Abs(rotationCurveEnd) < 0.0001f)
            rotationCurveEnd = 1f;

        yield return null;
        yield return null;

        while (elapsed < preFlightDur) {
            float preEased =
                Mathf.Sin(Mathf.Clamp01(elapsed / preFlightDur) * Mathf.PI);

            for (int i = 0; i < count; i++) {
                Card card = cards[i];

                if (card == null)
                    continue;

                card.transform.position = startPositions[i];
                card.transform.rotation = Quaternion.identity;
                card.transform.localScale =
                    Vector3.Lerp(Vector3.one, mergeResultPreFlightScale, preEased);
            }

            yield return null;
            elapsed += Time.deltaTime;
        }

        for (int i = 0; i < count; i++) {
            if (cards[i] != null)
                cards[i].transform.localScale = Vector3.one;
        }

        elapsed = 0f;

        while (elapsed < totalDuration) {
            elapsed += Time.deltaTime;

            for (int i = 0; i < count; i++) {
                Card card = cards[i];
                CardSlot targetSlot = targetSlots[i];

                if (card == null || targetSlot == null)
                    continue;

                float localElapsed = elapsed - i * startInterval;

                if (localElapsed < 0f)
                    continue;

                if (localElapsed <= flightDur) {
                    float progress = Mathf.Clamp01(localElapsed / flightDur);

                    float curveBlend = count > 1 ? (float)i / (count - 1) : 0f;
                    float curve1 = mergeFlightCurveFirst.Evaluate(progress);
                    float curve2 = mergeFlightCurveLast.Evaluate(progress);

                    float moveEased = Mathf.Lerp(curve1, curve2, curveBlend);

                    float rotationEased =
                        mergeResultRotationCurve.Evaluate(progress) / rotationCurveEnd;

                    float arc = moveEased <= apexProgress
                        ? Mathf.SmoothStep(0f, 1f, moveEased / apexProgress)
                        : Mathf.SmoothStep(1f, 0f, (moveEased - apexProgress) / (1f - apexProgress));

                    float sideDirection = i % 2 == 0 ? -1f : 1f;
                    Vector3 targetPosition = targetSlot.transform.position;

                    card.DynamicShadow.UpdateMergeFlightCurveMovement(progress, startPositions[i], targetPosition);

                    float verticalDistance = Mathf.Abs(targetPosition.y - startPositions[i].y);
                    float arcHeight = mergeResultFlightHeight + verticalDistance * 0.45f;

                    Vector3 flightPosition = startPositions[i];
                    flightPosition.x = Mathf.Lerp(startPositions[i].x, targetPosition.x, moveEased);
                    flightPosition.z = Mathf.Lerp(startPositions[i].z, targetPosition.z, moveEased);

                    float apexY = Mathf.Max(startPositions[i].y, targetPosition.y) + arcHeight;

                    if (moveEased <= apexProgress) {
                        float riseProgress = Mathf.SmoothStep(0f, 1f, moveEased / apexProgress);
                        flightPosition.y = Mathf.Lerp(startPositions[i].y, apexY, riseProgress);
                    }
                    else {
                        float fallProgress = Mathf.SmoothStep(0f, 1f, (moveEased - apexProgress) / (1f - apexProgress));
                        flightPosition.y = Mathf.Lerp(apexY, targetPosition.y, fallProgress);
                    }

                    float sideArc = Mathf.Sin(rotationEased * Mathf.PI);
                    card.transform.position =
                        flightPosition + Vector3.right * (sideDirection * mergeResultSideOffset * sideArc);

                    float currentRotY = sideArc * mergeResultFlightRotateY;
                    float currentRotZ = -360f * rotationTurns * rotationEased;

                    card.transform.rotation = Quaternion.Euler(0f, currentRotY, currentRotZ);

                    float eased = EaseOut(progress);
                    Vector3 baseFlightScale = Vector3.Lerp(Vector3.one, mergeResultLandingScale, eased);
                    card.transform.localScale = baseFlightScale + (Vector3.one * (mergeResultFlightScale * arc));

                    if (progress >= 0.5f && i < halfCount && !hasReversedSortingOrder[i]) {
                        hasReversedSortingOrder[i] = true;

                    }
                }

                else if (localElapsed <= flightDur + landingDur) {
                    if (!shadowDisabled[i]) {
                        shadowDisabled[i] = true;
                        card.DynamicShadow.SwitchShadow(false);
                        card.DynamicShadow.SetShadowEnabled(false);
                        card.transform.rotation = Quaternion.identity;
                    }

                    float landingTime = localElapsed - flightDur;
                    float progress = Mathf.Clamp01(landingTime / landingDur);

                    float scaleProgress = Mathf.SmoothStep(0f, 1f, progress);
                    card.transform.localScale = Vector3.Lerp(mergeResultLandingScale, Vector3.one, scaleProgress);
                    card.transform.position = targetSlot.transform.position;
                    
                }
            }

            yield return null;
        }

        for (int i = 0; i < count; i++) {
            if (cards[i] == null || targetSlots[i] == null) {
                continue;
            }

            cards[i].transform.position = targetSlots[i].transform.position;
            cards[i].transform.rotation = Quaternion.identity;
            cards[i].transform.localScale = Vector3.one;
            cards[i].DynamicShadow.SwitchShadow(false);
        }
    }    private void OnEnable()
    {
        _initialBoardDealAnimator?.Prepare();

        if (GameManager.Instance == null ||
            GameManager.Instance.gameState == GameState.PlayingGame)
        {
            PrepareTableEntrance();
        }

        Lean.Touch.LeanTouch.OnFingerDown +=
            HandleFingerDown;
        Lean.Touch.LeanTouch.OnFingerUpdate +=
            HandleFingerUpdate;
        Lean.Touch.LeanTouch.OnFingerUp +=
            HandleFingerUp;
    }

    private void OnDisable()
    {
        TransitionManager.OnTransitionFinished -=
            HandleTableEntranceTransitionFinished;

        if (_tableEntranceRoutine != null)
        {
            StopCoroutine(_tableEntranceRoutine);
            _tableEntranceRoutine = null;
        }

        RestoreTableEntranceShownPosition();

        _tableEntranceWaitingForTransition = false;
        _tableEntranceHasPlayed = false;

        Lean.Touch.LeanTouch.OnFingerDown -=
            HandleFingerDown;
        Lean.Touch.LeanTouch.OnFingerUpdate -=
            HandleFingerUpdate;
        Lean.Touch.LeanTouch.OnFingerUp -=
            HandleFingerUp;
        EndMagicCardSelectionGesture();
        CleanupLevelFlightShadows();
        _movingHolders.Clear();
        _mergingHolders.Clear();
        _activeMergeTargetCelebrations = 0;
        if (_mergeTargetPeakFxDisableRoutine != null)
        {
            StopCoroutine(_mergeTargetPeakFxDisableRoutine);
            _mergeTargetPeakFxDisableRoutine = null;
        }
        if (mergeTargetPeakFx != null)
            mergeTargetPeakFx.SetActive(false);
        _activeBooster = null;
        Observer.ActiveBoosterChanged?.Invoke(null);
        SetBoosterDimVisible(false, true);
    }

    private void CacheTableEntranceTarget()
    {
        Transform[] children = null;

        if (tableIngame == null || tableEntranceTrayRoot == null)
            children = GetComponentsInChildren<Transform>(true);

        if (tableIngame == null && children != null)
        {
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == "table_ingame")
                {
                    tableIngame = children[i];
                    break;
                }
            }
        }

        if (tableEntranceTrayRoot == null && children != null)
        {
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == "SlotsRoot")
                {
                    tableEntranceTrayRoot = children[i];
                    break;
                }
            }
        }

        if (tableIngame != null)
            _tableIngameShownPosition = tableIngame.localPosition;

        if (tableEntranceTrayRoot != null)
        {
            _tableEntranceTrayRootShownPosition =
                tableEntranceTrayRoot.localPosition;
        }
    }

    private void PrepareTableEntrance()
    {
        if (tableIngame == null || tableEntranceTrayRoot == null)
            CacheTableEntranceTarget();

        // The table and tray are one entrance visual. Moving only one of them
        // leaves malformed level prefabs (for example a missing table_ingame)
        // with the tray offset from its authored position.
        if (tableIngame == null || tableEntranceTrayRoot == null)
            return;

        SetTableEntranceHiddenPosition();

        TransitionManager.OnTransitionFinished -=
            HandleTableEntranceTransitionFinished;
        TransitionManager.OnTransitionFinished +=
            HandleTableEntranceTransitionFinished;

        if (TransitionManager.Instance != null &&
            TransitionManager.Instance.IsPlaying)
        {
            _tableEntranceWaitingForTransition = true;
            return;
        }

        _tableEntranceWaitingForTransition = false;
        PlayTableEntrance();
    }

    private void HandleTableEntranceTransitionFinished()
    {
        if (!isActiveAndEnabled ||
            !_tableEntranceWaitingForTransition)
        {
            return;
        }

        _tableEntranceWaitingForTransition = false;
        PlayTableEntrance();
    }

    private void SetTableEntranceHiddenPosition()
    {
        Vector3 hiddenOffset =
            Vector3.right * tableEntranceOffset;

        if (tableIngame != null)
        {
            tableIngame.localPosition =
                _tableIngameShownPosition + hiddenOffset;
        }

        if (tableEntranceTrayRoot != null)
        {
            tableEntranceTrayRoot.localPosition =
                _tableEntranceTrayRootShownPosition + hiddenOffset;
        }
    }

    private void RestoreTableEntranceShownPosition()
    {
        if (tableIngame != null)
            tableIngame.localPosition = _tableIngameShownPosition;

        if (tableEntranceTrayRoot != null)
        {
            tableEntranceTrayRoot.localPosition =
                _tableEntranceTrayRootShownPosition;
        }
    }

    private void PlayTableEntrance()
    {
        if (!isActiveAndEnabled ||
            (tableIngame == null && tableEntranceTrayRoot == null) ||
            _tableEntranceHasPlayed)
        {
            return;
        }

        _tableEntranceHasPlayed = true;

        if (_tableEntranceRoutine != null)
            StopCoroutine(_tableEntranceRoutine);

        _tableEntranceRoutine =
            StartCoroutine(PlayTableEntranceRoutine());
    }

    private IEnumerator PlayTableEntranceRoutine()
    {
        SetTableEntranceHiddenPosition();

        if (tableEntranceDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                tableEntranceDelay
            );
        }

        Vector3 tableHiddenPosition =
            _tableIngameShownPosition +
            Vector3.right * tableEntranceOffset;
        Vector3 tableOvershootPosition =
            _tableIngameShownPosition -
            Vector3.right * tableEntranceOvershoot;
        Vector3 trayRootHiddenPosition =
            _tableEntranceTrayRootShownPosition +
            Vector3.right * tableEntranceOffset;
        Vector3 trayRootOvershootPosition =
            _tableEntranceTrayRootShownPosition -
            Vector3.right * tableEntranceOvershoot;
        float elapsed = 0f;

        while (elapsed < tableEntranceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(
                elapsed / Mathf.Max(tableEntranceDuration, 0.001f)
            );
            float easedProgress =
                1f - Mathf.Pow(1f - progress, 3f);

            if (tableIngame != null)
            {
                tableIngame.localPosition = Vector3.Lerp(
                    tableHiddenPosition,
                    tableOvershootPosition,
                    easedProgress
                );
            }

            if (tableEntranceTrayRoot != null)
            {
                tableEntranceTrayRoot.localPosition = Vector3.Lerp(
                    trayRootHiddenPosition,
                    trayRootOvershootPosition,
                    easedProgress
                );
            }

            yield return null;
        }

        const float settleDuration = 0.1f;
        elapsed = 0f;

        while (elapsed < settleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(
                elapsed / settleDuration
            );
            float easedProgress =
                progress * progress * (3f - 2f * progress);

            if (tableIngame != null)
            {
                tableIngame.localPosition = Vector3.Lerp(
                    tableOvershootPosition,
                    _tableIngameShownPosition,
                    easedProgress
                );
            }

            if (tableEntranceTrayRoot != null)
            {
                tableEntranceTrayRoot.localPosition = Vector3.Lerp(
                    trayRootOvershootPosition,
                    _tableEntranceTrayRootShownPosition,
                    easedProgress
                );
            }

            yield return null;
        }

        if (tableIngame != null)
            tableIngame.localPosition = _tableIngameShownPosition;

        if (tableEntranceTrayRoot != null)
        {
            tableEntranceTrayRoot.localPosition =
                _tableEntranceTrayRootShownPosition;
        }

        _tableEntranceRoutine = null;
    }

    private void OnDestroy()
    {
        OnBoardChanged -= HandleBoardChanged;
        StopBoosterTrayPulse();
        SetBoosterCardsHighlighted(false);

        if (_boosterDimRenderer != null)
            Destroy(_boosterDimRenderer.gameObject);

        if (_boosterDimSprite != null)
            Destroy(_boosterDimSprite);
    }

    private void HandleFingerDown(
        Lean.Touch.LeanFinger finger
    )
    {
        if (finger.IsOverGui)
        {
            if (_activeBooster.HasValue &&
                !IsPointerOverActiveBoosterButton(finger.ScreenPosition))
            {
                CancelActiveBooster();
            }

            return;
        }

        if (_mainCamera == null)
            return;

        if (IsCardInputGloballyBlocked)
            return;

        Ray ray =
            _mainCamera.ScreenPointToRay(
                finger.ScreenPosition
            );

        RaycastHit2D hit =
            Physics2D.GetRayIntersection(
                ray,
                Mathf.Infinity,
                targetLayer
            );

        if (hit.collider == null)
        {
            CancelBoosterFromDimTap();
            return;
        }

        CardSlotHolder holder =
            hit.collider
                .GetComponentInParent<CardSlotHolder>();

        if (holder != null)
        {
            if (_activeBooster == BoosterType.MagicMove &&
                (_selectedHolder == null || _selectedHolder == holder))
            {
                BeginMagicCardSelection(holder, finger);
                return;
            }

            OnClickCardSlotHolder(
                holder,
                finger.ScreenPosition
            );

            return;
        }

                        CardDesk desk =
            hit.collider
                .GetComponentInParent<CardDesk>();

        if (desk != null)
        {
            if (IsAnimatingCards)
                return;

            Deselect();

            _pressedDesk = desk;
            desk.OnPointerDownDesk();
            return;
        }

        CancelBoosterFromDimTap();
    }

    private void CancelBoosterFromDimTap()
    {
        if (_activeBooster.HasValue)
            CancelActiveBooster();
    }

    private bool IsPointerOverActiveBoosterButton(Vector2 screenPosition)
    {
        if (!_activeBooster.HasValue || EventSystem.current == null)
            return false;

        var pointer = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);

        for (int index = 0; index < results.Count; index++)
        {
            InGameBoosterItem item = results[index].gameObject
                .GetComponentInParent<InGameBoosterItem>();
            if (item != null && item.BoosterType == _activeBooster.Value)
                return true;
        }

        return false;
    }

    private void HandleFingerUpdate(
        Lean.Touch.LeanFinger finger)
    {
        UpdateMagicCardSelectionGesture(finger);
    }

        private void HandleFingerUp(
        Lean.Touch.LeanFinger finger)
    {
        if (finger == _magicSelectionFinger)
            EndMagicCardSelectionGesture();

        if (_pressedDesk != null)
        {
            _pressedDesk.OnPointerUpDesk();
            _pressedDesk = null;
        }
    }

    public void ChangeTargetLayerToHighlight()
    {
        targetLayer =
            highLightLayer;
    }

    public void ResetLayerTarget()
    {
        targetLayer =
            _defaultTargetLayer;
    }
    
    public void ReverseListSortingOrder(List<Card> cards)
    {
        if (cards == null || cards.Count <= 1)
            return;

        int count = cards.Count;
    
        for (int i = 0; i < count / 2; i++)
        {
            Card cardA = cards[i];
            Card cardB = cards[count - 1 - i];

            if (cardA != null && cardB != null)
            {
                cardA.SwapSortingOrder(cardB);
            }
        }
    }
    
    public void ReverseListSortingOrder(List<Card> cards, int targetIndex)
    {
        if (cards == null || cards.Count <= 1)
            return;

        int count = cards.Count;

        int a = targetIndex;
        int b = count - 1 - targetIndex;
        if (a < 0 || b < 0 || a >= count || b >= count) return;
        
        Card cardA = cards[a];
        Card cardB = cards[b];
        
        if (cardA != null && cardB != null)
        {
            cardA.SwapSortingOrder(cardB);
        }
    }

    public Dictionary<CardType, int>
        GetCurrentCardCounts()
    {
        Dictionary<CardType, int> counts =
            new Dictionary<CardType, int>();

        if (cardSlotHolders == null)
        {
            return counts;
        }

        foreach (CardSlotHolder holder in
                 cardSlotHolders)
        {
            if (holder == null)
                continue;

            if (holder.IsLocked)
                continue;

            if (holder.CardSlots == null)
                continue;

            foreach (CardSlot slot in
                     holder.CardSlots)
            {
                if (slot == null)
                    continue;

                if (slot.IsEmpty)
                    continue;

                if (slot.Card == null)
                    continue;

                if (slot.Card.IsUnassignedWild)
                    continue;

                CardType type =
                    slot.Card.CardType;

                int count;
                counts.TryGetValue(type, out count);
                counts[type] = count + 1;
            }
        }

        return counts;
    }

    public void PlayWinCleanup(Action onCompleted)
    {
        _winCleanupCompleted += onCompleted;

        if (_winCleanupRoutine != null)
            return;

        _winCleanupRoutine = StartCoroutine(PlayWinCleanupRoutine());
    }

    private IEnumerator PlayWinCleanupRoutine()
    {
        List<WinCleanupCardState> states = GatherWinCleanupCards();
        if (states.Count == 0)
        {
            CompleteWinCleanup();
            yield break;
        }

        PopupInGame popupInGame = PopupController.Instance != null
            ? PopupController.Instance.Get<PopupInGame>() as PopupInGame
            : null;
        bool moveCountdownCompleted = popupInGame == null;
        popupInGame?.PlayWinMoveCountdown(
            () => moveCountdownCompleted = true
        );
        while (!moveCountdownCompleted)
            yield return null;

        List<Card> cards = new List<Card>(states.Count);
        Dictionary<Card, WinCleanupCardState> statesByCard =
            new Dictionary<Card, WinCleanupCardState>();
        int totalGold = 0;
        for (int i = 0; i < states.Count; i++)
        {
            Card card = states[i].Card;
            if (card == null)
                continue;

            card.SetSelected(false, animate: false);
            cards.Add(card);
            statesByCard[card] = states[i];

            MergeGoldRewardEntry reward = mergeGoldRewardConfig != null
                ? mergeGoldRewardConfig.GetReward(card.CardType)
                : default;
            totalGold += Mathf.Max(1, reward.goldAmount);
        }

        yield return StartCoroutine(AnimateCardFlip(
            cards,
            false,
            null,
            Mathf.Max(0.18f, winCleanupFlipDuration * 0.5f),
            Mathf.Max(0.03f, winCleanupFlipStagger * 0.5f)
        ));

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
                cards[i].SetOffSprite();
        }

        yield return new WaitForSecondsRealtime(0.12f);

        bool cardsReturned = false;
        Action<Card> removeReturnedCard = card =>
        {
            if (card != null && statesByCard.TryGetValue(card, out WinCleanupCardState state))
                RemoveWinCleanupCardWithoutReward(state);
        };

        if (_initialBoardDealAnimator != null)
        {
            _initialBoardDealAnimator.PlayCardsToDesk(
                cards,
                removeReturnedCard,
                () => cardsReturned = true
            );
        }
        else
        {
            for (int i = 0; i < cards.Count; i++)
                removeReturnedCard(cards[i]);
            cardsReturned = true;
        }

        while (!cardsReturned)
            yield return null;

        for (int i = 0; i < cardSlotHolders.Count; i++)
        {
            if (cardSlotHolders[i] != null)
                cardSlotHolders[i].NotifyCardsChanged();
        }

        if (totalGold > 0 && popupInGame != null)
        {
            _winCleanupPendingCoinFx++;
            int visualCoinCount = UnityEngine.Random.Range(8, 12);
            popupInGame.PlayWinCleanupGoldReward(
                _cardDesk != null ? _cardDesk.transform.position : transform.position,
                _mainCamera,
                totalGold,
                visualCoinCount,
                OnWinCleanupCoinFxCompleted
            );
        }
        else if (totalGold > 0 && Data.PlayerData != null)
        {
            GoldHandler.AddWithoutResourceAnimation(totalGold);
        }

        while (_winCleanupPendingCoinFx > 0)
            yield return null;

        CompleteWinCleanup();
    }

    private List<WinCleanupCardState> GatherWinCleanupCards()
    {
        List<WinCleanupCardState> states = new List<WinCleanupCardState>();
        if (cardSlotHolders == null)
            return states;

        for (int holderIndex = 0;
             holderIndex < cardSlotHolders.Count;
             holderIndex++)
        {
            CardSlotHolder holder = cardSlotHolders[holderIndex];
            if (holder == null || !holder.gameObject.activeInHierarchy ||
                holder.CardSlots == null)
            {
                continue;
            }

            for (int slotIndex = 0;
                 slotIndex < holder.CardSlots.Count;
                 slotIndex++)
            {
                CardSlot slot = holder.CardSlots[slotIndex];
                Card card = slot != null ? slot.Card : null;
                if (card == null || !card.gameObject.activeInHierarchy)
                    continue;

                states.Add(new WinCleanupCardState
                {
                    Card = card,
                    Slot = slot,
                    Holder = holder,
                    BaseScale = card.transform.localScale
                });
            }
        }

        return states;
    }

    private IEnumerator DisappearWinCleanupCards(
        List<WinCleanupCardState> states)
    {
        float duration = Mathf.Max(0.05f, winCleanupDisappearDuration);
        float stagger = Mathf.Max(0f, winCleanupDisappearStagger);
        float totalDuration = duration +
                              Mathf.Max(0, states.Count - 1) * stagger;
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            for (int i = 0; i < states.Count; i++)
            {
                WinCleanupCardState state = states[i];
                if (state.Removed || state.Card == null)
                    continue;

                float localTime = elapsed - i * stagger;
                if (localTime < 0f)
                    continue;

                float progress = Mathf.Clamp01(localTime / duration);
                float eased = progress * progress;
                state.Card.transform.localScale = Vector3.LerpUnclamped(
                    state.BaseScale,
                    Vector3.zero,
                    eased
                );

                if (progress >= 1f)
                    RemoveWinCleanupCard(state);
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        for (int i = 0; i < states.Count; i++)
            RemoveWinCleanupCard(states[i]);

        for (int i = 0; i < cardSlotHolders.Count; i++)
        {
            if (cardSlotHolders[i] != null)
                cardSlotHolders[i].NotifyCardsChanged();
        }
    }

    private void RemoveWinCleanupCard(WinCleanupCardState state)
    {
        if (state == null || state.Removed)
            return;

        state.Removed = true;
        Card card = state.Card;
        if (card == null)
            return;

        Vector3 sourcePosition = card.transform.position;
        CardType cardType = card.CardType;

        if (state.Slot != null && state.Slot.Card == card)
            state.Slot.RemoveCard();

        card.gameObject.SetActive(false);
        Destroy(card.gameObject);

        MergeGoldRewardEntry reward = mergeGoldRewardConfig != null
            ? mergeGoldRewardConfig.GetReward(cardType)
            : default;
        int goldAmount = Mathf.Max(1, reward.goldAmount);

        PopupInGame popupInGame = PopupController.Instance != null
            ? PopupController.Instance.Get<PopupInGame>() as PopupInGame
            : null;
        if (popupInGame == null)
        {
            if (Data.PlayerData != null)
                GoldHandler.AddWithoutResourceAnimation(goldAmount);
            return;
        }

        _winCleanupPendingCoinFx++;
        popupInGame.PlayWinCleanupGoldReward(
            sourcePosition,
            _mainCamera,
            goldAmount,
            winCleanupVisualCoinsPerCard,
            OnWinCleanupCoinFxCompleted
        );
    }

    private static void RemoveWinCleanupCardWithoutReward(WinCleanupCardState state)
    {
        if (state == null || state.Removed)
            return;

        state.Removed = true;
        Card card = state.Card;
        if (card == null)
            return;

        if (state.Slot != null && state.Slot.Card == card)
            state.Slot.RemoveCard();

        card.gameObject.SetActive(false);
        Destroy(card.gameObject);
    }

    private void OnWinCleanupCoinFxCompleted()
    {
        _winCleanupPendingCoinFx = Mathf.Max(
            0,
            _winCleanupPendingCoinFx - 1
        );
    }

    private void CompleteWinCleanup()
    {
        _winCleanupRoutine = null;
        _winCleanupPendingCoinFx = 0;

        Action completed = _winCleanupCompleted;
        _winCleanupCompleted = null;
        completed?.Invoke();
    }

    public void CheckWinCondition()
    {
        if (cardTargets == null ||
            cardTargets.Count == 0)
        {
            return;
        }

        Dictionary<CardType, int>
            currentCounts =
                GetCurrentCardCounts();

        bool allMet = true;

        foreach (CardTarget target in
                 cardTargets)
        {
            if (target == null)
                continue;

            int current;
            currentCounts.TryGetValue(target.cardType, out current);

            if (!target.IsCompleted &&
                current >= target.count)
            {
                target.MarkCompleted();
            }

            if (!target.IsCompleted)
                allMet = false;
        }

        if (allMet)
        {
            GameManager.Instance
                .OnWinGame();
        }
    }

    // =========================================================
    // DEAL
    // =========================================================

    public bool HasAvailableDealSlots()
    {
        if (cardSlotHolders == null)
            return false;

        for (int i = 0; i < cardSlotHolders.Count; i++)
        {
            CardSlotHolder holder = cardSlotHolders[i];
            if (holder == null || !holder.gameObject.activeInHierarchy ||
                holder.IsLocked || holder.IsBlockedByIron ||
                holder.GetEmptySlotCount() <= 0)
            {
                continue;
            }

            Card topCard = holder.GetTopCard();
            if (topCard != null &&
                (topCard.IsObstacleCard || topCard.IsFrozen))
            {
                continue;
            }

            for (CardType type = CardType.Card1;
                 type <= CardType.Card20;
                 type++)
            {
                if (holder.CanReceiveCardsIgnoringTopType(type))
                    return true;
            }
        }

        return false;
    }

    public bool ConsumeDeal()
    {
        if (!HasAvailableDealSlots())
            return false;
        
        OnDealCountChanged?.Invoke(
            CurrentDealCount
        );

        return true;
    }

    private void ConsumeMove()
    {
        if (CurrentMoveCount <= 0)
            return;

        CurrentMoveCount--;
        TickDarkKingCardCountdowns();
        TickFrozenCardCountdowns();

        if (RemainingFreeMoveCount > 0)
            RemainingFreeMoveCount--;

        OnMoveCountChanged?.Invoke(CurrentMoveCount);
    }

    private void InitializeKingCardCountdowns()
    {
        int moveCount = cardConfig != null
            ? cardConfig.DefaultDarkKingMovesBeforeTrayLock
            : 3;
        int frozenMoveCount = cardConfig != null
            ? cardConfig.DefaultFrozenMovesBeforeOpen
            : 3;
        Sprite frozenSprite = cardConfig != null
            ? cardConfig.GetEditorCardData(CardType.FrozenCard)?.icon
            : null;

        for (int holderIndex = 0;
             holderIndex < cardSlotHolders.Count;
             holderIndex++)
        {
            CardSlotHolder holder = cardSlotHolders[holderIndex];
            if (holder == null || holder.CardSlots == null)
                continue;

            for (int slotIndex = 0;
                 slotIndex < holder.CardSlots.Count;
                 slotIndex++)
            {
                CardSlot slot = holder.CardSlots[slotIndex];
                slot?.Card?.InitializeDarkKingCountdown(moveCount);
                Card card = slot != null ? slot.Card : null;
                if (card != null)
                {
                    Sprite numberSprite = cardConfig != null
                        ? cardConfig.GetCardData(card.CardType)?.icon
                        : null;
                    card.InitializeFrozenCountdown(frozenMoveCount, frozenSprite,
                        numberSprite);
                }
            }
        }
    }

    private void TickFrozenCardCountdowns()
    {
        bool openedAny = false;
        for (int holderIndex = 0; holderIndex < cardSlotHolders.Count; holderIndex++)
        {
            CardSlotHolder holder = cardSlotHolders[holderIndex];
            if (holder == null || holder.CardSlots == null)
                continue;

            for (int slotIndex = 0; slotIndex < holder.CardSlots.Count; slotIndex++)
            {
                CardSlot slot = holder.CardSlots[slotIndex];
                if (slot?.Card != null && slot.Card.ConsumeFrozenMove())
                    openedAny = true;
            }
        }

        if (openedAny)
            OnBoardChanged?.Invoke();
    }

    private void TickDarkKingCardCountdowns()
    {
        int moveCount = cardConfig != null
            ? cardConfig.DefaultDarkKingMovesBeforeTrayLock
            : 3;
        List<CardSlotHolder> holdersToExplode = null;

        for (int holderIndex = 0;
             holderIndex < cardSlotHolders.Count;
             holderIndex++)
        {
            CardSlotHolder holder = cardSlotHolders[holderIndex];
            if (holder == null || holder.IsLocked ||
                holder.CardSlots == null)
            {
                continue;
            }

            bool shouldExplode = false;

            for (int slotIndex = 0;
                 slotIndex < holder.CardSlots.Count;
                 slotIndex++)
            {
                CardSlot slot = holder.CardSlots[slotIndex];
                Card card = slot != null ? slot.Card : null;

                if (card != null &&
                    card.ConsumeDarkKingMove(moveCount))
                {
                    shouldExplode = true;
                }
            }

            if (shouldExplode)
            {
                // Mark it unavailable immediately so another move cannot
                // schedule a second explosion during the anticipation frame.
                holder.SetLocked(true);
                holdersToExplode ??= new List<CardSlotHolder>();
                holdersToExplode.Add(holder);
            }
        }

        if (holdersToExplode == null)
            return;

        for (int i = 0; i < holdersToExplode.Count; i++)
        {
            CardSlotHolder holder = holdersToExplode[i];
            if (holder != null)
                StartCoroutine(ExplodeDarkKingHolder(holder));
        }
    }

    private IEnumerator ExplodeDarkKingHolder(CardSlotHolder holder)
    {
        if (holder.CardSlots != null)
        {
            for (int i = 0; i < holder.CardSlots.Count; i++)
            {
                Card card = holder.CardSlots[i]?.Card;
                if (card != null && card.CardType == CardType.DarkingCard)
                    card.PlayDestroyEffect();
            }
        }

        Transform holderTransform = holder.transform;
        Transform effectParent = holderTransform.parent != null
            ? holderTransform.parent
            : transform;
        Vector3 localPosition = effectParent.InverseTransformPoint(
            holderTransform.position
        );

        DarkKingExplosionEffect.CreateScorchMark(
            effectParent,
            localPosition,
            holderTransform.localScale
        );

        if (VFXController.Instance != null)
        {
            VFXController.Instance.SpawnEffect(
                EffectName.BombExplosion,
                localPosition,
                effectParent,
                Vector3.one * 2.2f,
                2.8f
            );
        }

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.BombExplosion);

        // Remove the tray in the same frame as the explosion begins.
        cardSlotHolders.Remove(holder);
        holder.gameObject.SetActive(false);
        OnBoardChanged?.Invoke();
        yield break;
    }

    public void RestoreMovesForContinue()
    {
        RemainingFreeMoveCount = 0;
        CurrentMoveCount = Mathf.Max(1, maxMoveCount);
        OnMoveCountChanged?.Invoke(CurrentMoveCount);
    }

#if UNITY_EDITOR

    public const string EditorPlayLevelKey = "SolitaireSort.PlayThisLevel.Index";
    private const string PreviousStartSceneKey = "SolitaireSort.PlayThisLevel.PreviousScene";

    [InitializeOnLoadMethod]
    private static void RegisterEditorPlayCleanup()
    {
        EditorApplication.playModeStateChanged -= RestoreEditorStartScene;
        EditorApplication.playModeStateChanged += RestoreEditorStartScene;
    }

    private static void RestoreEditorStartScene(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode &&
            state != PlayModeStateChange.EnteredEditMode)
            return;

        string path = SessionState.GetString(PreviousStartSceneKey, "__none__");
        if (path != "__none__")
        {
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene =
                string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.EraseString(PreviousStartSceneKey);
        }
        if (state == PlayModeStateChange.EnteredEditMode)
            SessionState.EraseInt(EditorPlayLevelKey);
    }

    [Button("Play This Level")]
    public void PlayThisLevel()
    {
        int levelIndex = Utility.GetNumberInAString(gameObject.name, "Level");
        if (levelIndex < 1)
        {
            Debug.LogError("Play This Level requires a name such as 'Level 3'.", this);
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying)
            {
                if (Data.PlayerData == null || GameManager.Instance == null)
                {
                    Debug.LogError("Play This Level requires GameplayScene to be running.");
                    return;
                }
                Data.PlayerData.CurrentLevelIndex = levelIndex;
                Time.timeScale = 1f;
                GameManager.Instance.PrepareLevel();
                GameManager.Instance.StartGame();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                "Assets/_Project/Scenes/GameplayScene.unity");
            if (scene == null)
            {
                Debug.LogError("Play This Level could not find GameplayScene.");
                return;
            }

            var previousScene = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
            SessionState.SetString(PreviousStartSceneKey,
                previousScene == null ? "" : AssetDatabase.GetAssetPath(previousScene));
            SessionState.SetInt(EditorPlayLevelKey, levelIndex);
            SceneAsset loadingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                "Assets/_Project/Scenes/LoadingScene.unity");
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene =
                loadingScene != null ? loadingScene : scene;
            EditorApplication.isPlaying = true;
        };
    }

#endif
}
