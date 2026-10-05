using System;
using System.Collections;
using System.Collections.Generic;
using CustomTween;
using Lean.Pool;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class PopupInGame : Popup
{
    [Header("Base Game")]
    [SerializeField] private bool stripLegacyContent = true;

    private const int UseBoosterSortingOrder = 32000;
    private const float TrayFullProgressDangerThreshold = 0.3f;
    private const int FreeMoveAdRewardAmount = 5;

    [Header("Level UI")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("Gold UI")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("Merge Gold Reward")]
    [SerializeField] private RectTransform mergeCoinTarget;
    [SerializeField] private RectTransform mergeCoinFlyRoot;
    [SerializeField] private CoinFlyFXTemplate mergeCoinFxTemplate;
    [SerializeField] private List<Sprite> mergeCoinAnimationFrames =
        new List<Sprite>();
    [SerializeField, Min(0f)] private float mergeCoinAnticipationDuration = 0.06f;
    [SerializeField, Range(2, 8)] private int mergeCoinMinimumCount = 2;
    [SerializeField, Range(2, 8)] private int mergeCoinMaximumCount = 8;
    [SerializeField, Min(0f)] private float mergeCoinSpawnRadiusX = 45f;
    [SerializeField, Min(0f)] private float mergeCoinSpawnRadiusY = 30f;
    [SerializeField, Min(0f)] private float mergeCoinBurstDistanceMin = 55f;
    [SerializeField, Min(0f)] private float mergeCoinBurstDistanceMax = 105f;
    [SerializeField, Min(0.01f)] private float mergeCoinBurstDurationMin = 0.14f;
    [SerializeField, Min(0.01f)] private float mergeCoinBurstDurationMax = 0.22f;
    [SerializeField, Min(0f)] private float mergeCoinHangDurationMin = 0.05f;
    [SerializeField, Min(0f)] private float mergeCoinHangDurationMax = 0.12f;
    [SerializeField, Min(0.05f)] private float mergeCoinFlightDurationMin = 0.30f;
    [SerializeField, Min(0.05f)] private float mergeCoinFlightDurationMax = 0.42f;
    [SerializeField, Min(0f)] private float mergeCoinStaggerMin = 0.03f;
    [SerializeField, Min(0f)] private float mergeCoinStaggerMax = 0.045f;
    [SerializeField, Min(0f)] private float mergeCoinCurveStrengthMin = 40f;
    [SerializeField, Min(0f)] private float mergeCoinCurveStrengthMax = 110f;
    [SerializeField, Range(0.1f, 2f)] private float mergeCoinSpawnScale = 0.35f;
    [SerializeField, Range(0.1f, 2f)] private float mergeCoinOvershootScale = 1.18f;
    [SerializeField, Range(0.05f, 1f)] private float mergeCoinFlightEndScale = 0.28f;
    [SerializeField, Range(0.5f, 1.5f)] private float mergeCoinDepthScaleMin = 0.9f;
    [SerializeField, Range(0.5f, 1.5f)] private float mergeCoinDepthScaleMax = 1.3f;
    [SerializeField, Range(0f, 1f)] private float mergeCoinPullStretchChance = 0.45f;
    [SerializeField, Range(0f, 0.4f)] private float mergeCoinPullStretchStrength = 0.18f;
    [SerializeField, Min(1f)] private float mergeCoinFrameRate = 24f;
    [SerializeField, Range(0f, 30f)] private float mergeCoinStartRotation = 15f;
    [SerializeField, Range(0f, 30f)] private float mergeCoinRotationDrift = 8f;
    [SerializeField, Min(1f)] private float mergeCoinSize = 82f;
    [SerializeField, Range(0, 5)] private int mergeCoinTrailCount = 0;
    [SerializeField, Min(1f)] private float mergeCoinTrailFollowSpeed = 18f;
    [SerializeField, Min(1f)] private float mergeCoinTrailHeadWidth = 64f;
    [SerializeField, Min(1f)] private float mergeCoinTrailLength = 180f;
    [SerializeField, Range(0f, 1f)] private float mergeCoinTrailAlpha = 0.6f;
    [SerializeField, Range(1f, 1.5f)] private float mergeCoinTargetPulseScale = 1.13f;
    [SerializeField, Range(0.8f, 1f)] private float mergeCoinTargetPulseUndershoot = 0.97f;
    [SerializeField, Min(0.05f)] private float mergeCoinTargetPulseDuration = 0.12f;
    [SerializeField, Range(1f, 1.3f)] private float mergeCoinFinalImpactMultiplier = 1.12f;
    [SerializeField, Range(1f, 5f)] private float mergeCoinTargetSparkleMultiplier = 3f;

    [Header("Star UI")]
    [SerializeField] private TextMeshProUGUI starText;
    [SerializeField] private GameObject starTarget;
    [SerializeField] private GameObject starPrefab;

    [Header("Pause Menu")]
    [SerializeField] private InGamePauseMenu pauseMenu;
    
    [Header("Target UI")]
    [SerializeField] private Transform targetGroup;
    [SerializeField] private IngameTargetItem targetItemPrefab;
    [SerializeField, Range(0.5f, 2f)] private float targetItemScale = 0.9f;

    [Header("Use Booster UI")]
    [SerializeField] private GameObject useBooster;
    [SerializeField] private Image useBoosterIcon;
    [SerializeField] private Image boosterTitleImage;
    [SerializeField] private Sprite magicMoveTitleSprite;
    [SerializeField] private Sprite magnetTitleSprite;
    [SerializeField] private Sprite lighterTitleSprite;
    [SerializeField] private TextMeshProUGUI boosterInstructionText;
    [SerializeField] private ParticleSystem useBoosterGoldDustParticles;

    [Header("Booster Focus Transition")]
    [SerializeField] private RectTransform boosterFocusTopbar;
    [SerializeField] private RectTransform boosterFocusCoinTray;
    [SerializeField, Min(0f)] private float boosterTopbarUpOffset = 280f;
    [SerializeField, Min(0.05f)] private float boosterFocusMoveDuration = 0.1f;

    [Header("Use Booster Content Animation")]
    [SerializeField, Range(1f, 1.4f)] private float useBoosterOvershootScale = 1.2f;
    [SerializeField, Range(0.5f, 1f)] private float useBoosterUndershootScale = 0.9f;
    [SerializeField, Min(0.05f)] private float useBoosterPopUpDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float useBoosterPopUndershootDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float useBoosterPopSettleDuration = 0.09f;
    [SerializeField, Min(0f)] private float useBoosterTitlePopDelay = 0.2f;
    [SerializeField, Min(0f)] private float useBoosterInstructionStartDelay = 0.14f;
    [SerializeField, Min(0.005f)] private float boosterTypingInterval = 0.014f;
    [SerializeField] private AudioClip boosterTypingSound;
    [SerializeField, Range(0f, 1f)] private float boosterTypingVolume = 0.35f;
    [SerializeField, Min(1)] private int boosterTypingSoundEveryCharacters = 2;

    [Header("Move UI")]
    [FormerlySerializedAs("dealText")]
    [SerializeField] private TextMeshProUGUI moveText;
    [SerializeField] private Color normalMoveColor = Color.white;
    [SerializeField] private Color freeMoveColor =
        new Color32(66, 214, 86, 255);

    [Header("Move Odometer Animation")]
    [SerializeField, Min(0.05f)] private float moveRollDuration = 0.24f;
    [SerializeField, Min(0f)] private float moveRollStagger = 0.025f;
    [SerializeField, Range(0.5f, 2f)]
    private float moveRollDistanceMultiplier = 1f;

    [Header("Move Bonus Fly Effect")]
    [SerializeField] private RectTransform moveBonusFlyPrefab;
    [SerializeField] private RectTransform moveBonusFlyRoot;
    [SerializeField] private RectTransform moveBonusFlyStartPoint;
    [SerializeField, Min(0f)] private float moveBonusFlyDelay = 0.2f;
    [SerializeField, Min(0.05f)] private float moveBonusFlyDuration = 0.65f;
    [SerializeField, Min(0f)] private float moveBonusFlyArcHeight = 140f;
    [SerializeField, Range(0.1f, 2f)] private float moveBonusStartScale = 0.75f;
    [SerializeField, Range(0.1f, 2f)] private float moveBonusPeakScale = 1.15f;
    [SerializeField, Range(0.05f, 1f)] private float moveBonusEndScale = 0.35f;
    [SerializeField, Min(0f)] private float moveBonusShakeDuration = 0.28f;
    [SerializeField, Min(0f)] private float moveBonusShakeAmplitude = 9f;
    [SerializeField, Min(0f)] private float moveBonusShakeAngle = 7f;
    [SerializeField, Min(0.1f)] private float moveBonusShakeFrequency = 18f;

    [Header("Bottom Bar Entrance")]
    [SerializeField, Min(0f)] private float bottomEntranceDelay = 0.05f;
    [SerializeField, Min(0.1f)] private float bottomEntranceDuration = 0.34f;
    [SerializeField, Min(0f)] private float bottomWorldStartOffset = 3.8f;
    [SerializeField, Min(0f)] private float boosterUiStartOffset = 420f;
    [SerializeField] private int bottomTabMinimumSortingOrder = 1;

    [Header("Deal Bar Entrance")]
    [SerializeField, Min(0f)] private float dealBarEntranceStartOffset = 340f;
    [SerializeField, Min(0.1f)] private float dealBarEntranceDuration = 0.34f;
    [SerializeField, Min(0f)] private float freeMoveEntranceStartOffset = 210f;
    [SerializeField, Min(0.1f)] private float freeMoveEntranceDuration = 0.48f;

    [Header("Settings Button Entrance")]
    [SerializeField] private RectTransform settingsButton;
    [SerializeField, Min(0.01f)] private float settingsButtonScaleUpDuration = 0.16f;
    [SerializeField, Min(0.01f)] private float settingsButtonSettleDuration = 0.1f;
    [SerializeField, Min(1f)] private float settingsButtonOvershootScale = 1.2f;

    [Header("Tray Full Soft Lose")]
    [SerializeField] private RectTransform trayFullButton;
    [SerializeField] private TextMeshProUGUI trayFullCountdownText;
    [SerializeField] private GameObject trayFullWarning;
    [SerializeField] private RectTransform trayFullTargetBar;
    [SerializeField] private RectTransform trayFullProgressWarning;
    [SerializeField] private Image trayFullProgressFill;
    [SerializeField] private RectTransform trayFullHeart;
    [SerializeField] private Color trayFullProgressSafeColor =
        new Color(111f / 255f, 1f, 0f, 1f);
    [SerializeField] private Color trayFullProgressDangerColor =
        new Color(1f, 52f / 255f, 0f, 1f);
    [SerializeField, Min(0.1f)] private float trayFullCountdownDuration = 10f;
    [SerializeField, Min(0.1f)] private float trayFullWarningStartCycleDuration = 2.4f;
    [SerializeField, Min(0.1f)] private float trayFullWarningEndCycleDuration = 0.65f;
    [SerializeField, Min(0.05f)] private float trayFullSlideDuration = 0.35f;
    [SerializeField, Min(0.05f)] private float trayFullTopbarTransitionDuration = 0.35f;
    [SerializeField, Min(0f)] private float trayFullSafeLeftMargin = 20f;
    [SerializeField, Min(0f)] private float trayFullHiddenPadding = 40f;

    [Header("Out Of Move Transition")]
    [SerializeField] private GameObject outOfMoveDarkBackground;
    [SerializeField] private CanvasGroup outOfMoveDarkCanvasGroup;
    [SerializeField] private RectTransform outOfMoveTab;
    [SerializeField, Range(0f, 1f)] private float outOfMoveDarkAlpha = 0.6f;
    [SerializeField, Min(0.01f)] private float outOfMoveDarkFadeDuration = 0.12f;
    [SerializeField, Min(0f)] private float outOfMoveOffscreenPadding = 80f;
    [SerializeField, Min(0f)] private float outOfMoveLandingOvershoot = 125f;
    [SerializeField, Min(0.01f)] private float outOfMoveDropDuration = 0.24f;
    [SerializeField, Min(0.01f)] private float outOfMoveSettleDuration = 0.1f;
    [SerializeField, Min(0f)] private float outOfMoveHoldDuration = 0.55f;
    [SerializeField, Min(0f)] private float outOfMoveExitLift = 90f;
    [SerializeField, Min(0.01f)] private float outOfMoveExitLiftDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float outOfMoveExitDuration = 0.22f;

    private List<IngameTargetItem> _targetItems = new List<IngameTargetItem>();

    private int _currentStar;
    private Level _currentLevel;
    private int _activeLevelDisplayIndex;
    private Coroutine _moveBonusFlyRoutine;
    private RectTransform _activeMoveBonusVisual;
    private Vector3 _preparedMoveBonusWorldPosition;
    private bool _hasPreparedMoveBonusVisual;
    private bool _activeMoveBonusUsesPreparedVisual;
    private bool _deferMoveCountUntilBonusArrival;
    private bool _suppressNextMoveBonusFly;
    private readonly List<RectTransform> _bottomEntranceBoosters =
        new List<RectTransform>();
    private readonly List<Vector2> _bottomEntranceBoosterFinalPositions =
        new List<Vector2>();
    private Coroutine _bottomEntranceRoutine;
    private Coroutine _dealBarEntranceRoutine;
    private Coroutine _settingsButtonEntranceRoutine;
    private InitialBoardDealAnimator _initialBoardDealAnimator;
    private Transform _bottomEntranceTab;
    private Transform _bottomEntranceDeck;
    private Vector3 _bottomEntranceTabFinalPosition;
    private Vector3 _bottomEntranceDeckFinalPosition;
    private Vector3 _settingsButtonFinalScale;
    private bool _settingsButtonScaleCached;
    private RectTransform _dealBar;
    private CustomButton _freeMoveButton;
    private Vector2 _dealBarFinalPosition;
    private Vector2 _freeMoveButtonFinalPosition;
    private bool _dealBarPositionsCached;
    private bool _freeMoveAdRequestInProgress;

    private RectTransform FreeMoveButtonTransform => _freeMoveButton != null
        ? _freeMoveButton.transform as RectTransform
        : null;
    private readonly List<TextMeshProUGUI> _moveDigits =
        new List<TextMeshProUGUI>();
    private readonly List<Vector2> _moveDigitHomePositions =
        new List<Vector2>();
    private readonly List<TextMeshProUGUI> _moveRollOldDigits =
        new List<TextMeshProUGUI>();
    private readonly List<int> _moveRollDigitIndices =
        new List<int>();
    private Coroutine _moveRollRoutine;
    private int _displayedMoveCount;
    private int _moveRollTargetCount;
    private bool _hasDisplayedMoveCount;
    private Coroutine _trayFullWarningRoutine;
    private CustomButton _trayFullButtonComponent;
    private Graphic _trayFullWarningGraphic;
    private float _trayFullWarningBaseAlpha = 1f;
    private Vector3 _trayFullHeartBaseScale = Vector3.one;
    private bool _trayFullHeartScaleCached;
    private Vector2 _trayFullShownPosition;
    private Vector2 _trayFullHiddenPosition;
    private Vector2 _trayFullTargetBarHomePosition;
    private Vector2 _trayFullTargetBarWarningPosition;
    private Vector2 _trayFullProgressHiddenPosition;
    private Vector2 _trayFullProgressShownPosition;
    private float _trayFullTopbarTransitionProgress;
    private bool _trayFullTopbarPositionsCached;
    private bool _cancelTrayFullWarning;
    private Coroutine _outOfMoveRoutine;
    private Vector2 _outOfMoveCenterPosition;
    private Vector3 _mergeCoinTargetBaseScale;
    private Coroutine _mergeCoinTargetPulseRoutine;
    private Coroutine _boosterFocusTransitionRoutine;
    private Coroutine _boosterContentRoutine;
    private Vector2 _boosterTopbarHomePosition;
    private Vector2 _boosterCoinTrayHomePosition;
    private Vector3 _boosterCoinTrayHomeScale = Vector3.one;
    private bool _boosterFocusPositionsCached;
    private bool _boosterFocusVisible;
    private string _boosterInstruction = string.Empty;
    private Vector3 _useBoosterIconBaseScale = Vector3.one;
    private Vector3 _boosterTitleBaseScale = Vector3.one;
    private float _mergeCoinTargetImpactStrength;
    private int _mergeCoinPendingDisplayGold;
    private readonly Vector3[] _mergeCoinSourceCorners = new Vector3[4];
    private readonly Vector3[] _mergeCoinTargetCorners = new Vector3[4];
    private readonly Stack<MergeCoinFlightVisual> _mergeCoinFlightVisualPool =
        new Stack<MergeCoinFlightVisual>();
    private readonly Stack<MergeCoinSequenceState> _mergeCoinSequencePool =
        new Stack<MergeCoinSequenceState>();
    private readonly List<MergeCoinSequenceState> _activeMergeCoinSequences =
        new List<MergeCoinSequenceState>();

    private const int MaxMergeCoinVisuals = 16;

    public event Action<int> MergeCoinArrived;
    public event Action MergeCoinSequenceFinished;

    public CoinFlyFXTemplate SharedCoinFlyFXTemplate => mergeCoinFxTemplate;
    public RectTransform SharedCoinFlyTarget => mergeCoinTarget;
    public CoinFlyFX.Options SharedCoinFlyFXOptions => new CoinFlyFX.Options
    {
        anticipationDuration = mergeCoinAnticipationDuration,
        minimumCount = mergeCoinMinimumCount,
        maximumCount = mergeCoinMaximumCount,
        spawnRadiusX = mergeCoinSpawnRadiusX,
        spawnRadiusY = mergeCoinSpawnRadiusY,
        burstDistanceMin = mergeCoinBurstDistanceMin,
        burstDistanceMax = mergeCoinBurstDistanceMax,
        burstDurationMin = mergeCoinBurstDurationMin,
        burstDurationMax = mergeCoinBurstDurationMax,
        hangDurationMin = mergeCoinHangDurationMin,
        hangDurationMax = mergeCoinHangDurationMax,
        flightDurationMin = mergeCoinFlightDurationMin,
        flightDurationMax = mergeCoinFlightDurationMax,
        staggerMin = mergeCoinStaggerMin,
        staggerMax = mergeCoinStaggerMax,
        curveStrengthMin = mergeCoinCurveStrengthMin,
        curveStrengthMax = mergeCoinCurveStrengthMax,
        spawnScale = mergeCoinSpawnScale,
        overshootScale = mergeCoinOvershootScale,
        flightEndScale = mergeCoinFlightEndScale,
        depthScaleMin = mergeCoinDepthScaleMin,
        depthScaleMax = mergeCoinDepthScaleMax,
        pullStretchChance = mergeCoinPullStretchChance,
        pullStretchStrength = mergeCoinPullStretchStrength,
        startRotation = mergeCoinStartRotation,
        rotationDrift = mergeCoinRotationDrift,
        coinSize = mergeCoinSize,
        trailCount = mergeCoinTrailCount,
        trailFollowSpeed = mergeCoinTrailFollowSpeed,
        trailHeadWidth = mergeCoinTrailHeadWidth,
        trailLength = mergeCoinTrailLength,
        trailAlpha = mergeCoinTrailAlpha,
        targetPulseScale = mergeCoinTargetPulseScale,
        targetPulseUndershoot = mergeCoinTargetPulseUndershoot,
        targetPulseDuration = mergeCoinTargetPulseDuration,
        finalImpactMultiplier = mergeCoinFinalImpactMultiplier
    };

    public Transform UseBoosterRoot =>
        useBooster != null ? useBooster.transform : null;

    private sealed class MergeCoinTrailVisual
    {
        public GameObject GameObject;
        public RectTransform Transform;
        public MergeCoinTrailGraphic Graphic;
        public Vector2 Position;
    }

    private sealed class MergeCoinFlightVisual
    {
        public GameObject CoinObject;
        public RectTransform CoinTransform;
        public Image CoinImage;
        public Animator CoinAnimator;
        public readonly List<MergeCoinTrailVisual> Trails =
            new List<MergeCoinTrailVisual>();
    }

    private sealed class MergeCoinSequenceState
    {
        public readonly MergeCoinFlightVisual[] Visuals =
            new MergeCoinFlightVisual[MaxMergeCoinVisuals];
        public readonly Vector2[] SpawnPositions =
            new Vector2[MaxMergeCoinVisuals];
        public readonly Vector2[] BurstPositions =
            new Vector2[MaxMergeCoinVisuals];
        public readonly Vector2[] HangPositions =
            new Vector2[MaxMergeCoinVisuals];
        public readonly Vector2[] ControlPoints =
            new Vector2[MaxMergeCoinVisuals];
        public readonly float[] SpawnDelays = new float[MaxMergeCoinVisuals];
        public readonly float[] BurstDurations = new float[MaxMergeCoinVisuals];
        public readonly float[] HangDurations = new float[MaxMergeCoinVisuals];
        public readonly float[] FlightDurations = new float[MaxMergeCoinVisuals];
        public readonly float[] StartRotations = new float[MaxMergeCoinVisuals];
        public readonly float[] RotationDrifts = new float[MaxMergeCoinVisuals];
        public readonly float[] DepthScales = new float[MaxMergeCoinVisuals];
        public readonly float[] PullStretchStrengths = new float[MaxMergeCoinVisuals];
        public readonly int[] StartFrames = new int[MaxMergeCoinVisuals];
        public readonly int[] GoldAmounts = new int[MaxMergeCoinVisuals];
        public readonly bool[] Collected = new bool[MaxMergeCoinVisuals];
        public readonly bool[] TrailStarted = new bool[MaxMergeCoinVisuals];
        public RectTransform FlyRoot;
        public Coroutine Coroutine;
        public int Count;
        public int CompletedCount;
    }

    private IReadOnlyList<Sprite> ActiveMergeCoinAnimationFrames
    {
        get
        {
            CoinFlyFXSettings settings = mergeCoinFxTemplate != null
                ? mergeCoinFxTemplate.Settings
                : null;
            if (settings != null && settings.HasExpectedCoinSpinFrames)
            {
                return settings.CoinSpinFrames;
            }

            return mergeCoinAnimationFrames;
        }
    }

    private float ActiveMergeCoinFrameRate
    {
        get
        {
            CoinFlyFXSettings settings = mergeCoinFxTemplate != null
                ? mergeCoinFxTemplate.Settings
                : null;
            return settings != null
                ? Mathf.Max(1f, settings.SpinFrameRate)
                : Mathf.Max(1f, mergeCoinFrameRate);
        }
    }

    private RectTransform ActiveMergeCoinViewPrefab =>
        mergeCoinFxTemplate != null
            ? mergeCoinFxTemplate.CoinViewPrefab
            : null;

    public int CurrentStar
    {
        get => _currentStar;
        set
        {
            _currentStar = value;

            if (starText != null)
            {
                starText.text = _currentStar.ToString();
            }
        }
    }

    private void Awake()
    {
        if (stripLegacyContent)
        {
            ClearLegacyContent();
            return;
        }

        ConfigureUseBoosterSorting();
        CacheBoosterFocusLayout();

        if (useBoosterIcon != null)
        {
            useBoosterIcon.raycastTarget = false;
            _useBoosterIconBaseScale = useBoosterIcon.rectTransform.localScale;
        }
        if (boosterTitleImage != null)
        {
            boosterTitleImage.raycastTarget = false;
            boosterTitleImage.preserveAspect = true;
            _boosterTitleBaseScale = boosterTitleImage.rectTransform.localScale;
        }
        if (boosterInstructionText != null)
            boosterInstructionText.raycastTarget = false;

        if (mergeCoinTarget != null)
            _mergeCoinTargetBaseScale = mergeCoinTarget.localScale;

        InitializeMoveOdometer();
        ResolveFreeMoveButton();
        InitializeTrayFullWarning();
        InitializeOutOfMoveTransition();
        RegisterTrayFullButton();
        Observer.StartLevel += StartLevel;
        Observer.GoldChangedDone += UpdateGoldText;
    }

    private void ClearLegacyContent()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void ConfigureUseBoosterSorting()
    {
        if (useBooster == null)
            return;

        Canvas useBoosterCanvas = useBooster.GetComponent<Canvas>();
        if (useBoosterCanvas == null)
            useBoosterCanvas = useBooster.AddComponent<Canvas>();

        Canvas parentCanvas = useBooster.transform.parent != null
            ? useBooster.transform.parent.GetComponentInParent<Canvas>()
            : null;
        Canvas rootCanvas = parentCanvas != null &&
                            parentCanvas.rootCanvas != null
            ? parentCanvas.rootCanvas
            : parentCanvas;

        useBoosterCanvas.overrideSorting = true;
        useBoosterCanvas.sortingLayerID = rootCanvas != null
            ? rootCanvas.sortingLayerID
            : 0;
        useBoosterCanvas.sortingOrder = UseBoosterSortingOrder;

        EnsureUseBoosterGoldDust();
    }

    private void EnsureUseBoosterGoldDust()
    {
        if (useBooster == null)
            return;

        if (useBoosterGoldDustParticles == null)
            useBoosterGoldDustParticles =
                useBooster.GetComponentInChildren<ParticleSystem>(true);

        if (useBoosterGoldDustParticles != null)
            useBoosterGoldDustParticles.transform.SetAsFirstSibling();
    }

    private void PlayUseBoosterGoldDust()
    {
        EnsureUseBoosterGoldDust();
        if (useBoosterGoldDustParticles == null)
            return;

        useBoosterGoldDustParticles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
        useBoosterGoldDustParticles.Play(true);
    }

    private void OnDestroy()
    {
        Observer.StartLevel -= StartLevel;
        Observer.GoldChangedDone -= UpdateGoldText;

        if (_trayFullButtonComponent != null)
            _trayFullButtonComponent.Click.RemoveListener(OnClickTrayFullExit);

        if (_freeMoveButton != null)
            _freeMoveButton.Click.RemoveListener(OnClickFreeMove);

        StopMoveBonusFly(false);
        StopMoveRoll(false);
        StopBottomEntrance(true);
        ResetTrayFullWarning();
        CancelMergeCoinSequences();
        StopMergeCoinTargetPulse();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();

        Setup();
    }

    private void Setup()
    {
        if (levelText != null)
        {
            int levelIndex = GameManager.Instance != null &&
                             GameManager.Instance.gameState == GameState.WinGame &&
                             _activeLevelDisplayIndex > 0
                ? _activeLevelDisplayIndex
                : Data.PlayerData.CurrentLevelIndex;
            levelText.text =
                $"Level {levelIndex}";
        }

        UpdateGoldText();

        if (_currentLevel != null)
            SetMoveCountImmediate(_currentLevel.CurrentMoveCount);
    }

    private void StartLevel(Level level)
    {
        StopBottomEntrance(true);

        if (_currentLevel != null)
        {
            _currentLevel.OnBoardChanged -= UpdateTargetsUI;
            _currentLevel.OnBoardChanged -= EvaluateTrayFullWarning;
            _currentLevel.OnMoveCountChanged -= UpdateMoveCount;
            _currentLevel.OnMoveBonusGranted -= PlayMoveBonusFly;
        }

        ResetTrayFullWarning();
        StopOutOfMoveTransition();
        _currentLevel = level;
        _activeLevelDisplayIndex = Data.PlayerData != null
            ? Data.PlayerData.CurrentLevelIndex
            : 0;
        StopMoveRoll(false);
        _hasDisplayedMoveCount = false;

        if (_currentLevel != null)
        {
            _currentLevel.OnBoardChanged += UpdateTargetsUI;
            _currentLevel.OnBoardChanged += EvaluateTrayFullWarning;
            _currentLevel.OnMoveCountChanged += UpdateMoveCount;
            _currentLevel.OnMoveBonusGranted += PlayMoveBonusFly;
        }

        CurrentStar = 0;
        
        SetupTargets();
        
        if (_currentLevel != null)
        {
            PlayStartMoveBonusIfNeeded();
            StartBottomEntrance(_currentLevel);
        }

        UpdateUseBooster(_currentLevel != null
            ? _currentLevel.ActiveBooster
            : null);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        Observer.FoodBoxCompleted += FoodBoxCompleted;
        Observer.ActiveBoosterChanged += UpdateUseBooster;
        UpdateGoldText();
        UpdateUseBooster(_currentLevel != null
            ? _currentLevel.ActiveBooster
            : null);

        if (_currentLevel != null)
        {
            _currentLevel.OnBoardChanged -= UpdateTargetsUI;
            _currentLevel.OnBoardChanged -= EvaluateTrayFullWarning;
            _currentLevel.OnMoveCountChanged -= UpdateMoveCount;
            _currentLevel.OnMoveBonusGranted -= PlayMoveBonusFly;
            _currentLevel.OnBoardChanged += UpdateTargetsUI;
            _currentLevel.OnBoardChanged += EvaluateTrayFullWarning;
            _currentLevel.OnMoveCountChanged += UpdateMoveCount;
            _currentLevel.OnMoveBonusGranted += PlayMoveBonusFly;
            EvaluateTrayFullWarning();
        }
    }

    private void UpdateGoldText()
    {
        if (goldText == null || Data.PlayerData == null)
            return;

        int displayedGold = Mathf.Max(
            0,
            Data.PlayerData.CurrentGold - _mergeCoinPendingDisplayGold
        );
        goldText.text = displayedGold.ToString();
    }

    public void PlayMergeGoldReward(
        Vector3 sourceWorldPosition,
        Camera gameplayCamera,
        int goldAmount,
        int visualCoinCount
    )
    {
        if (goldAmount <= 0)
            return;

        CommitSharedCoinReward(goldAmount);
        CoinFlyFX.PlayFromWorld(
            sourceWorldPosition,
            gameplayCamera,
            mergeCoinTarget,
            goldAmount,
            visualCoinCount,
            mergeCoinFlyRoot != null
                ? mergeCoinFlyRoot
                : transform as RectTransform,
            OnSharedCoinArrived,
            OnSharedCoinSequenceFinished,
            addToWallet: false,
            template: mergeCoinFxTemplate,
            options: SharedCoinFlyFXOptions
        );
    }

    public void PlayWinCleanupGoldReward(
        Vector3 sourceWorldPosition,
        Camera gameplayCamera,
        int goldAmount,
        int visualCoinCount,
        Action onCompleted)
    {
        if (goldAmount <= 0)
        {
            onCompleted?.Invoke();
            return;
        }

        CommitSharedCoinReward(goldAmount);

        CoinFlyFX.Options options = SharedCoinFlyFXOptions;
        int coinCount = Mathf.Clamp(visualCoinCount, 1, 16);
        options.minimumCount = coinCount;
        options.maximumCount = coinCount;

        CoinFlyFX.PlayFromWorld(
            sourceWorldPosition,
            gameplayCamera,
            mergeCoinTarget,
            goldAmount,
            coinCount,
            mergeCoinFlyRoot != null
                ? mergeCoinFlyRoot
                : transform as RectTransform,
            OnSharedCoinArrived,
            () =>
            {
                OnSharedCoinSequenceFinished();
                onCompleted?.Invoke();
            },
            addToWallet: false,
            template: mergeCoinFxTemplate,
            options: options
        );
    }

    public void PlayFromWorld(
        Vector3 sourceWorldPosition,
        Camera sourceCamera,
        int rewardAmount,
        int visualCoinCount = 12)
    {
        PlayMergeGoldReward(
            sourceWorldPosition,
            sourceCamera,
            rewardAmount,
            visualCoinCount
        );
    }

    public void PlayFromUI(
        RectTransform source,
        int rewardAmount,
        int visualCoinCount = 12)
    {
        if (rewardAmount <= 0)
            return;

        CommitSharedCoinReward(rewardAmount);
        CoinFlyFX.PlayFromUI(
            source,
            mergeCoinTarget,
            rewardAmount,
            visualCoinCount,
            mergeCoinFlyRoot != null
                ? mergeCoinFlyRoot
                : transform as RectTransform,
            OnSharedCoinArrived,
            OnSharedCoinSequenceFinished,
            addToWallet: false,
            template: mergeCoinFxTemplate,
            options: SharedCoinFlyFXOptions
        );
    }

    private void CommitSharedCoinReward(int rewardAmount)
    {
        if (rewardAmount <= 0 || Data.PlayerData == null)
            return;

        _mergeCoinPendingDisplayGold += rewardAmount;
        GoldHandler.AddWithoutResourceAnimation(rewardAmount);
    }

    private void OnSharedCoinArrived(int rewardAmount)
    {
        RevealMergeCoinGold(rewardAmount);
        PlayMergeCoinTargetSparkle();
        MergeCoinArrived?.Invoke(rewardAmount);
    }

    private void PlayMergeCoinTargetSparkle()
    {
        if (mergeCoinTarget == null || VFXController.Instance == null)
            return;

        // Reuse the exact gold sparkle used by GoldHandler on the Home HUD.
        VFXController.Instance.SpawnEffect(
            EffectName.SparkleGold,
            Vector3.zero,
            mergeCoinTarget,
            0.5f,
            mergeCoinTargetSparkleMultiplier
        );
    }

    private void OnSharedCoinSequenceFinished()
    {
        MergeCoinSequenceFinished?.Invoke();
    }

    private void StartMergeCoinSequence(
        RectTransform flyRoot,
        Vector2 sourcePosition,
        Vector2 targetPosition,
        int goldAmount,
        int visualCoinCount,
        bool commitReward)
    {
        int minimumCoinCount = Mathf.Clamp(
            mergeCoinMinimumCount,
            1,
            MaxMergeCoinVisuals
        );
        int maximumCoinCount = Mathf.Clamp(
            mergeCoinMaximumCount,
            minimumCoinCount,
            MaxMergeCoinVisuals
        );
        int coinCount = Mathf.Clamp(
            visualCoinCount,
            minimumCoinCount,
            maximumCoinCount
        );

        // Currency is committed before the visual sequence begins. The HUD
        // holds the uncollected portion back and reveals it coin by coin.
        if (commitReward && goldAmount > 0 && Data.PlayerData != null)
        {
            _mergeCoinPendingDisplayGold += goldAmount;
            Data.PlayerData.CurrentGold += goldAmount;
        }

        MergeCoinSequenceState state = _mergeCoinSequencePool.Count > 0
            ? _mergeCoinSequencePool.Pop()
            : new MergeCoinSequenceState();
        PrepareMergeCoinSequence(
            state,
            flyRoot,
            sourcePosition,
            targetPosition,
            goldAmount,
            coinCount
        );
        _activeMergeCoinSequences.Add(state);
        state.Coroutine = StartCoroutine(PlayMergeCoinSequence(state));
    }

    private void GrantMergeGoldImmediately(int goldAmount)
    {
        if (goldAmount <= 0 || Data.PlayerData == null)
            return;

        Data.PlayerData.CurrentGold += goldAmount;
        UpdateGoldText();
    }

    private void RevealMergeCoinGold(int goldAmount)
    {
        if (goldAmount <= 0)
            return;

        _mergeCoinPendingDisplayGold = Mathf.Max(
            0,
            _mergeCoinPendingDisplayGold - goldAmount
        );
        UpdateGoldText();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Preview Merge Coin Collect FX")]
    private void PreviewMergeCoinCollectFx()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[PopupInGame] Enter Play Mode before previewing Merge Coin Collect FX."
            );
            return;
        }

        RectTransform flyRoot = mergeCoinFlyRoot != null
            ? mergeCoinFlyRoot
            : transform as RectTransform;
        if (flyRoot == null || mergeCoinTarget == null ||
            ActiveMergeCoinAnimationFrames == null ||
            ActiveMergeCoinAnimationFrames.Count == 0 ||
            !TryGetMergeCoinTargetPosition(flyRoot, out Vector2 targetPosition))
        {
            Debug.LogWarning(
                "[PopupInGame] Merge Coin FX needs a fly root, target, and animation frames."
            );
            return;
        }

        CoinFlyFXSettings settings = mergeCoinFxTemplate != null
            ? mergeCoinFxTemplate.Settings
            : null;
        int previewCoinCount = settings != null
            ? settings.PreviewCoinCount
            : 12;
        Vector2 previewSource = flyRoot.rect.center +
                                Vector2.down * flyRoot.rect.height * 0.18f;

        StartMergeCoinSequence(
            flyRoot,
            previewSource,
            targetPosition,
            0,
            previewCoinCount,
            commitReward: false
        );
    }
#endif

    private static Camera GetUiCamera(RectTransform rectTransform)
    {
        Canvas parentCanvas = rectTransform != null
            ? rectTransform.GetComponentInParent<Canvas>()
            : null;

        return parentCanvas != null &&
               parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;
    }

    private bool TryGetMergeCoinTargetPosition(
        RectTransform flyRoot,
        out Vector2 targetPosition)
    {
        targetPosition = Vector2.zero;
        if (flyRoot == null || mergeCoinTarget == null)
            return false;

        Camera uiCamera = GetUiCamera(flyRoot);
        mergeCoinTarget.GetWorldCorners(_mergeCoinTargetCorners);
        Vector3 targetCenter = (_mergeCoinTargetCorners[0] +
                                _mergeCoinTargetCorners[2]) * 0.5f;
        Vector2 targetScreenPosition = RectTransformUtility.WorldToScreenPoint(
            uiCamera,
            targetCenter
        );
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            flyRoot,
            targetScreenPosition,
            uiCamera,
            out targetPosition
        );
    }

    private void PrepareMergeCoinSequence(
        MergeCoinSequenceState state,
        RectTransform flyRoot,
        Vector2 sourcePosition,
        Vector2 targetPosition,
        int goldAmount,
        int coinCount)
    {
        state.FlyRoot = flyRoot;
        state.Count = coinCount;
        state.CompletedCount = 0;

        float staggerMin = Mathf.Max(0f, mergeCoinStaggerMin);
        float staggerMax = Mathf.Max(staggerMin, mergeCoinStaggerMax);
        float accumulatedDelay = 0f;
        for (int i = 0; i < coinCount; i++)
        {
            if (i > 0)
                accumulatedDelay += Random.Range(staggerMin, staggerMax);
            state.SpawnDelays[i] = accumulatedDelay;
        }

        int distributedGold = 0;
        int frameCount = ActiveMergeCoinAnimationFrames.Count;
        float previousArrivalTime = 0f;
        for (int i = 0; i < coinCount; i++)
        {
            MergeCoinFlightVisual visual = GetMergeCoinFlightVisual(
                flyRoot,
                sourcePosition
            );
            state.Visuals[i] = visual;
            state.Collected[i] = false;
            state.TrailStarted[i] = false;

            Vector2 spawnOffset = new Vector2(
                Random.Range(-mergeCoinSpawnRadiusX, mergeCoinSpawnRadiusX),
                Random.Range(-mergeCoinSpawnRadiusY, mergeCoinSpawnRadiusY)
            );
            Vector2 outward = spawnOffset.sqrMagnitude > 1f
                ? spawnOffset.normalized
                : Random.insideUnitCircle.normalized;
            outward.y = Mathf.Abs(outward.y) * 0.75f + 0.25f;
            outward.Normalize();

            Vector2 spawnPosition = sourcePosition + spawnOffset;
            Vector2 burstPosition = spawnPosition + outward * Random.Range(
                Mathf.Min(mergeCoinBurstDistanceMin, mergeCoinBurstDistanceMax),
                Mathf.Max(mergeCoinBurstDistanceMin, mergeCoinBurstDistanceMax)
            );
            Vector2 hangPosition = burstPosition + outward * Random.Range(6f, 16f) +
                                   Vector2.up * Random.Range(-3f, 5f);

            Vector2 targetDirection = targetPosition - hangPosition;
            Vector2 perpendicular = targetDirection.sqrMagnitude > 0.001f
                ? new Vector2(-targetDirection.y, targetDirection.x).normalized
                : Vector2.right;
            float curveDistanceFactor = Mathf.Clamp(
                targetDirection.magnitude / 500f,
                0.65f,
                1.25f
            );
            float curveStrength = Random.Range(
                Mathf.Min(mergeCoinCurveStrengthMin, mergeCoinCurveStrengthMax),
                Mathf.Max(mergeCoinCurveStrengthMin, mergeCoinCurveStrengthMax)
            ) * curveDistanceFactor * (Random.value < 0.5f ? -1f : 1f);

            state.SpawnPositions[i] = spawnPosition;
            state.BurstPositions[i] = burstPosition;
            state.HangPositions[i] = hangPosition;
            state.ControlPoints[i] = hangPosition +
                targetDirection * Random.Range(0.3f, 0.55f) +
                perpendicular * curveStrength;
            state.BurstDurations[i] = Random.Range(
                Mathf.Min(mergeCoinBurstDurationMin, mergeCoinBurstDurationMax),
                Mathf.Max(mergeCoinBurstDurationMin, mergeCoinBurstDurationMax)
            );
            state.HangDurations[i] = Random.Range(
                Mathf.Min(mergeCoinHangDurationMin, mergeCoinHangDurationMax),
                Mathf.Max(mergeCoinHangDurationMin, mergeCoinHangDurationMax)
            );
            state.FlightDurations[i] = Random.Range(
                Mathf.Min(mergeCoinFlightDurationMin, mergeCoinFlightDurationMax),
                Mathf.Max(mergeCoinFlightDurationMin, mergeCoinFlightDurationMax)
            );
            if (i == coinCount - 1)
                state.FlightDurations[i] *= 0.9f;

            float arrivalTime = state.SpawnDelays[i] +
                                state.BurstDurations[i] +
                                state.HangDurations[i] +
                                state.FlightDurations[i];
            if (i > 0)
            {
                float minimumArrivalTime = previousArrivalTime + staggerMin;
                if (arrivalTime < minimumArrivalTime)
                {
                    state.FlightDurations[i] +=
                        minimumArrivalTime - arrivalTime;
                    arrivalTime = minimumArrivalTime;
                }
            }
            previousArrivalTime = arrivalTime;

            state.StartRotations[i] = Random.Range(
                -mergeCoinStartRotation,
                mergeCoinStartRotation
            );
            state.RotationDrifts[i] = Random.Range(
                -mergeCoinRotationDrift,
                mergeCoinRotationDrift
            );
            state.DepthScales[i] = Random.Range(
                Mathf.Min(mergeCoinDepthScaleMin, mergeCoinDepthScaleMax),
                Mathf.Max(mergeCoinDepthScaleMin, mergeCoinDepthScaleMax)
            );
            state.PullStretchStrengths[i] =
                Random.value < mergeCoinPullStretchChance
                    ? mergeCoinPullStretchStrength * Random.Range(0.8f, 1.2f)
                    : 0f;
            state.StartFrames[i] = Random.Range(0, frameCount);

            int cumulativeGold = (int)(
                (long)goldAmount * (i + 1) / coinCount
            );
            state.GoldAmounts[i] = cumulativeGold - distributedGold;
            distributedGold = cumulativeGold;

            visual.CoinTransform.anchoredPosition = spawnPosition;
            visual.CoinTransform.localScale = Vector3.zero;
            visual.CoinTransform.localEulerAngles = new Vector3(
                0f,
                0f,
                state.StartRotations[i]
            );
            SetMergeCoinTrailsVisible(visual, false, spawnPosition);
            SetMergeCoinSprite(visual, state.StartFrames[i]);
        }

    }

    private IEnumerator PlayMergeCoinSequence(MergeCoinSequenceState state)
    {
        float anticipationElapsed = 0f;
        float anticipationDuration = Mathf.Max(
            0f,
            mergeCoinAnticipationDuration
        );
        while (anticipationElapsed < anticipationDuration)
        {
            if (!CanContinueMergeCoinSequence(state))
            {
                FinishMergeCoinSequence(state);
                yield break;
            }
            anticipationElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        float elapsed = 0f;
        while (state.CompletedCount < state.Count)
        {
            if (!CanContinueMergeCoinSequence(state) ||
                !TryGetMergeCoinTargetPosition(state.FlyRoot, out Vector2 targetPosition))
            {
                FinishMergeCoinSequence(state);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < state.Count; i++)
            {
                if (state.Collected[i] || state.Visuals[i] == null)
                    continue;

                float localElapsed = elapsed - state.SpawnDelays[i];
                if (localElapsed < 0f)
                    continue;

                MergeCoinFlightVisual visual = state.Visuals[i];
                RectTransform coinTransform = visual.CoinTransform;
                UpdateMergeCoinFrame(
                    visual,
                    localElapsed,
                    state.StartFrames[i]
                );

                float burstDuration = Mathf.Max(0.01f, state.BurstDurations[i]);
                float hangDuration = Mathf.Max(0f, state.HangDurations[i]);
                if (localElapsed < burstDuration)
                {
                    float burstProgress = Mathf.Clamp01(
                        localElapsed / burstDuration
                    );
                    float moveEase = 1f - Mathf.Pow(1f - burstProgress, 3f);
                    coinTransform.anchoredPosition = Vector2.LerpUnclamped(
                        state.SpawnPositions[i],
                        state.BurstPositions[i],
                        moveEase
                    );
                    coinTransform.localScale = Vector3.one *
                        (EvaluateMergeCoinSpawnScale(burstProgress) *
                         state.DepthScales[i]);
                    coinTransform.localEulerAngles = new Vector3(
                        0f,
                        0f,
                        state.StartRotations[i] +
                        state.RotationDrifts[i] * burstProgress * 0.35f
                    );
                    continue;
                }

                float afterBurst = localElapsed - burstDuration;
                if (afterBurst < hangDuration)
                {
                    float hangProgress = hangDuration > 0f
                        ? Mathf.Clamp01(afterBurst / hangDuration)
                        : 1f;
                    float settle = 1f - (1f - hangProgress) *
                                   (1f - hangProgress);
                    Vector2 hangPosition = Vector2.Lerp(
                        state.BurstPositions[i],
                        state.HangPositions[i],
                        settle
                    );
                    hangPosition.y += Mathf.Sin(hangProgress * Mathf.PI) * 3f;
                    coinTransform.anchoredPosition = hangPosition;
                    coinTransform.localScale = Vector3.one * state.DepthScales[i];
                    continue;
                }

                if (!state.TrailStarted[i])
                {
                    state.TrailStarted[i] = true;
                    SetMergeCoinTrailsVisible(visual, true, state.HangPositions[i]);
                }

                float flightElapsed = afterBurst - hangDuration;
                float flightDuration = Mathf.Max(0.05f, state.FlightDurations[i]);
                float progress = Mathf.Clamp01(flightElapsed / flightDuration);
                float eased = progress * progress * progress;
                float inverse = 1f - eased;
                coinTransform.anchoredPosition =
                    inverse * inverse * state.HangPositions[i] +
                    2f * inverse * eased * state.ControlPoints[i] +
                    eased * eased * targetPosition;

                float shrinkProgress = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.78f, 1f, eased));
                float baseScale = state.DepthScales[i] * Mathf.Lerp(
                    1f,
                    mergeCoinFlightEndScale,
                    shrinkProgress
                );
                float pullEnvelope = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.12f, 0.72f, eased)) *
                    (1f - Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(0.82f, 1f, eased)));
                float stretch = state.PullStretchStrengths[i] * pullEnvelope;
                coinTransform.localScale = new Vector3(
                    baseScale * (1f - stretch),
                    baseScale * (1f + stretch),
                    1f
                );
                coinTransform.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    state.StartRotations[i] + state.RotationDrifts[i] * progress
                );

                Color coinColor = visual.CoinImage.color;
                coinColor.a = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.94f, 1f, eased)
                );
                visual.CoinImage.color = coinColor;
                UpdateMergeCoinTrail(
                    visual.Trails,
                    coinTransform.anchoredPosition,
                    coinColor.a
                );

                if (progress >= 1f)
                {
                    CompleteMergeCoinCollection(
                        state,
                        i,
                        i == state.Count - 1
                    );
                }
            }

            yield return null;
        }

        FinishMergeCoinSequence(state);
    }

    private float EvaluateMergeCoinSpawnScale(float progress)
    {
        const float overshootPoint = 0.62f;
        if (progress < overshootPoint)
        {
            float rise = Mathf.Clamp01(progress / overshootPoint);
            float eased = 1f - Mathf.Pow(1f - rise, 3f);
            return Mathf.Lerp(
                mergeCoinSpawnScale,
                mergeCoinOvershootScale,
                eased
            );
        }

        float settle = Mathf.InverseLerp(overshootPoint, 1f, progress);
        settle = settle * settle * (3f - 2f * settle);
        return Mathf.Lerp(mergeCoinOvershootScale, 1f, settle);
    }

    private void UpdateMergeCoinFrame(
        MergeCoinFlightVisual visual,
        float elapsed,
        int startFrame)
    {
        IReadOnlyList<Sprite> animationFrames = ActiveMergeCoinAnimationFrames;
        if (visual == null || visual.CoinImage == null ||
            animationFrames == null ||
            animationFrames.Count == 0)
        {
            return;
        }

        if (visual.CoinAnimator != null &&
            visual.CoinAnimator.runtimeAnimatorController != null)
        {
            return;
        }

        int frameIndex = Mathf.FloorToInt(
            elapsed * ActiveMergeCoinFrameRate
        ) % animationFrames.Count;
        frameIndex = (frameIndex + startFrame) % animationFrames.Count;
        SetMergeCoinSprite(visual, frameIndex);
    }

    private void SetMergeCoinSprite(
        MergeCoinFlightVisual visual,
        int frameIndex)
    {
        IReadOnlyList<Sprite> animationFrames = ActiveMergeCoinAnimationFrames;
        if (visual == null || visual.CoinImage == null ||
            animationFrames == null || animationFrames.Count == 0)
        {
            return;
        }

        int safeFrameIndex = frameIndex % animationFrames.Count;
        if (safeFrameIndex < 0)
            safeFrameIndex += animationFrames.Count;

        visual.CoinImage.sprite = animationFrames[safeFrameIndex];
    }

    private void CompleteMergeCoinCollection(
        MergeCoinSequenceState state,
        int coinIndex,
        bool isFinalCoin)
    {
        if (state == null || state.Collected[coinIndex])
            return;

        state.Collected[coinIndex] = true;
        state.CompletedCount++;
        int goldAmount = state.GoldAmounts[coinIndex];
        RevealMergeCoinGold(goldAmount);
        PlayMergeCoinTargetSparkle();
        MergeCoinArrived?.Invoke(goldAmount);

        if (mergeCoinTarget != null)
            QueueMergeCoinTargetPulse(isFinalCoin);

        ReleaseMergeCoinFlightVisual(state.Visuals[coinIndex]);
        state.Visuals[coinIndex] = null;
    }

    private bool CanContinueMergeCoinSequence(MergeCoinSequenceState state)
    {
        return state != null && isActiveAndEnabled &&
               state.FlyRoot != null && mergeCoinTarget != null;
    }

    private void FinishMergeCoinSequence(MergeCoinSequenceState state)
    {
        if (state == null)
            return;

        int unrevealedGold = 0;
        for (int i = 0; i < state.Count; i++)
        {
            if (!state.Collected[i])
                unrevealedGold += state.GoldAmounts[i];

            if (state.Visuals[i] != null)
            {
                ReleaseMergeCoinFlightVisual(state.Visuals[i]);
                state.Visuals[i] = null;
            }
            state.Collected[i] = true;
            state.TrailStarted[i] = false;
        }

        _activeMergeCoinSequences.Remove(state);
        state.Coroutine = null;
        state.FlyRoot = null;
        state.Count = 0;
        state.CompletedCount = 0;
        _mergeCoinSequencePool.Push(state);

        RevealMergeCoinGold(unrevealedGold);
        MergeCoinSequenceFinished?.Invoke();
    }

    private void CancelMergeCoinSequences()
    {
        int unrevealedGold = 0;
        for (int sequenceIndex = _activeMergeCoinSequences.Count - 1;
             sequenceIndex >= 0;
             sequenceIndex--)
        {
            MergeCoinSequenceState state =
                _activeMergeCoinSequences[sequenceIndex];
            if (state.Coroutine != null)
                StopCoroutine(state.Coroutine);

            for (int i = 0; i < state.Count; i++)
            {
                if (!state.Collected[i])
                    unrevealedGold += state.GoldAmounts[i];
                if (state.Visuals[i] != null)
                {
                    ReleaseMergeCoinFlightVisual(state.Visuals[i]);
                    state.Visuals[i] = null;
                }
                state.Collected[i] = true;
                state.TrailStarted[i] = false;
            }

            state.Coroutine = null;
            state.FlyRoot = null;
            state.Count = 0;
            state.CompletedCount = 0;
            _mergeCoinSequencePool.Push(state);
        }
        _activeMergeCoinSequences.Clear();

        RevealMergeCoinGold(unrevealedGold);
    }

    private MergeCoinFlightVisual GetMergeCoinFlightVisual(
        RectTransform flyRoot,
        Vector2 sourcePosition
    )
    {
        MergeCoinFlightVisual visual = _mergeCoinFlightVisualPool.Count > 0
            ? _mergeCoinFlightVisualPool.Pop()
            : CreateMergeCoinFlightVisual();

        visual.CoinObject.SetActive(true);
        visual.CoinTransform.SetParent(flyRoot, false);
        visual.CoinTransform.SetAsLastSibling();
        visual.CoinTransform.anchoredPosition = Vector2.zero;
        visual.CoinTransform.localScale = Vector3.one;
        visual.CoinTransform.localEulerAngles = Vector3.zero;
        visual.CoinTransform.sizeDelta = Vector2.one * mergeCoinSize;
        visual.CoinImage.material = null;
        visual.CoinImage.type = Image.Type.Simple;
        visual.CoinImage.preserveAspect = true;
        visual.CoinImage.color = Color.white;

        if (visual.CoinAnimator != null &&
            visual.CoinAnimator.runtimeAnimatorController != null)
        {
            visual.CoinAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            visual.CoinAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            visual.CoinAnimator.enabled = true;
            visual.CoinAnimator.speed = Random.Range(0.9f, 1.1f);
            visual.CoinAnimator.Rebind();
            visual.CoinAnimator.Play("Base Layer.CoinSpin", 0, Random.value);
            visual.CoinAnimator.Update(0f);
        }

        int trailCount = Mathf.Clamp(mergeCoinTrailCount, 0, 5);
        while (visual.Trails.Count < trailCount)
            visual.Trails.Add(CreateMergeCoinTrailVisual());

        for (int i = 0; i < trailCount; i++)
        {
            MergeCoinTrailVisual trail = visual.Trails[i];
            trail.GameObject.SetActive(true);
            trail.Transform.SetParent(flyRoot, false);
            trail.Transform.SetSiblingIndex(visual.CoinTransform.GetSiblingIndex());
            trail.Transform.anchoredPosition = sourcePosition;
            trail.Transform.localScale = Vector3.one;
            trail.Transform.localEulerAngles = Vector3.zero;
            trail.Position = sourcePosition;

            float trailProgress = (i + 1f) / (trailCount + 1f);
            trail.Transform.sizeDelta = new Vector2(
                Mathf.Lerp(
                    mergeCoinTrailHeadWidth,
                    mergeCoinTrailHeadWidth * 0.45f,
                    trailProgress
                ),
                Mathf.Lerp(
                    mergeCoinTrailLength,
                    mergeCoinTrailLength * 0.45f,
                    trailProgress
                )
            );

            trail.Graphic.color = new Color(
                1f,
                0.91f,
                0.76f,
                mergeCoinTrailAlpha
            );
        }

        for (int i = trailCount; i < visual.Trails.Count; i++)
            visual.Trails[i].GameObject.SetActive(false);

        return visual;
    }

    private void UpdateMergeCoinTrail(
        List<MergeCoinTrailVisual> trails,
        Vector2 coinPosition,
        float opacity
    )
    {
        float follow = 1f - Mathf.Exp(
            -Mathf.Max(1f, mergeCoinTrailFollowSpeed) * Time.unscaledDeltaTime
        );
        Vector2 previousPosition = coinPosition;

        for (int i = 0; i < trails.Count; i++)
        {
            MergeCoinTrailVisual trail = trails[i];
            if (!trail.GameObject.activeSelf)
                continue;

            trail.Position = Vector2.Lerp(
                trail.Position,
                previousPosition,
                follow
            );
            RectTransform trailTransform = trail.Transform;
            trailTransform.anchoredPosition = trail.Position;
            trailTransform.localScale = Vector3.one;

            Vector2 trailDirection = previousPosition - trail.Position;
            if (trailDirection.sqrMagnitude > 0.01f)
            {
                trailTransform.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    Mathf.Atan2(trailDirection.y, trailDirection.x) *
                    Mathf.Rad2Deg - 90f
                );

                float halfTrailLength = trailTransform.rect.height *
                    trailTransform.localScale.y * 0.5f;
                trailTransform.anchoredPosition = previousPosition -
                    trailDirection.normalized * halfTrailLength;
            }

            Color color = trail.Graphic.color;
            color.a = mergeCoinTrailAlpha * Mathf.Clamp01(opacity);
            trail.Graphic.color = color;

            previousPosition = trail.Position;
        }
    }

    private void SetMergeCoinTrailsVisible(
        MergeCoinFlightVisual visual,
        bool visible,
        Vector2 startPosition)
    {
        if (visual == null)
            return;

        int trailCount = Mathf.Clamp(mergeCoinTrailCount, 0, 5);
        while (visual.Trails.Count < trailCount)
            visual.Trails.Add(CreateMergeCoinTrailVisual());

        for (int i = 0; i < visual.Trails.Count; i++)
        {
            MergeCoinTrailVisual trail = visual.Trails[i];
            bool shouldShow = visible && i < trailCount;
            trail.GameObject.SetActive(shouldShow);
            if (!shouldShow)
                continue;

            trail.Transform.SetParent(
                visual.CoinTransform.parent,
                false
            );
            trail.Transform.SetSiblingIndex(
                visual.CoinTransform.GetSiblingIndex()
            );
            trail.Transform.anchoredPosition = startPosition;
            trail.Transform.localScale = Vector3.one;
            trail.Transform.localEulerAngles = Vector3.zero;
            trail.Position = startPosition;

            float trailProgress = (i + 1f) / (trailCount + 1f);
            trail.Transform.sizeDelta = new Vector2(
                Mathf.Lerp(
                    mergeCoinTrailHeadWidth * 0.7f,
                    mergeCoinTrailHeadWidth * 0.38f,
                    trailProgress
                ),
                Mathf.Lerp(
                    mergeCoinTrailLength * 0.55f,
                    mergeCoinTrailLength * 0.3f,
                    trailProgress
                )
            );
            trail.Graphic.color = new Color(
                1f,
                0.91f,
                0.76f,
                mergeCoinTrailAlpha
            );
        }
    }

    private MergeCoinFlightVisual CreateMergeCoinFlightVisual()
    {
        RectTransform viewPrefab = ActiveMergeCoinViewPrefab;
        GameObject coinObject = viewPrefab != null
            ? Instantiate(viewPrefab.gameObject)
            : new GameObject(
                "MergeCoin",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
        coinObject.name = "MergeCoin";
        coinObject.layer = gameObject.layer;

        RectTransform coinTransform = coinObject.GetComponent<RectTransform>();
        coinTransform.anchorMin = new Vector2(0.5f, 0.5f);
        coinTransform.anchorMax = new Vector2(0.5f, 0.5f);
        coinTransform.pivot = new Vector2(0.5f, 0.5f);

        Image coinImage = coinObject.GetComponent<Image>();
        if (coinImage == null)
            coinImage = coinObject.AddComponent<Image>();
        coinImage.material = null;
        coinImage.type = Image.Type.Simple;
        coinImage.raycastTarget = false;
        coinImage.preserveAspect = true;

        return new MergeCoinFlightVisual
        {
            CoinObject = coinObject,
            CoinTransform = coinTransform,
            CoinImage = coinImage,
            CoinAnimator = coinObject.GetComponent<Animator>()
        };
    }

    private MergeCoinTrailVisual CreateMergeCoinTrailVisual()
    {
        GameObject trailObject = new GameObject(
            "MergeCoinTrail",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(MergeCoinTrailGraphic)
        );
        trailObject.layer = gameObject.layer;

        RectTransform trailTransform = trailObject.GetComponent<RectTransform>();
        trailTransform.anchorMin = new Vector2(0.5f, 0.5f);
        trailTransform.anchorMax = new Vector2(0.5f, 0.5f);
        trailTransform.pivot = new Vector2(0.5f, 0.5f);

        MergeCoinTrailGraphic trailGraphic =
            trailObject.GetComponent<MergeCoinTrailGraphic>();
        trailGraphic.raycastTarget = false;

        return new MergeCoinTrailVisual
        {
            GameObject = trailObject,
            Transform = trailTransform,
            Graphic = trailGraphic
        };
    }

    private void ReleaseMergeCoinFlightVisual(MergeCoinFlightVisual visual)
    {
        if (visual == null || visual.CoinObject == null ||
            visual.CoinTransform == null || visual.CoinImage == null)
        {
            return;
        }

        visual.CoinTransform.anchoredPosition = Vector2.zero;
        visual.CoinTransform.localScale = Vector3.one;
        visual.CoinTransform.localEulerAngles = Vector3.zero;
        if (visual.CoinAnimator != null)
        {
            visual.CoinAnimator.enabled = false;
            visual.CoinAnimator.speed = 1f;
            visual.CoinAnimator.Rebind();
        }
        visual.CoinImage.color = Color.white;
        IReadOnlyList<Sprite> animationFrames = ActiveMergeCoinAnimationFrames;
        if (animationFrames != null && animationFrames.Count > 0)
        {
            visual.CoinImage.sprite = animationFrames[0];
        }
        visual.CoinObject.SetActive(false);
        for (int i = 0; i < visual.Trails.Count; i++)
        {
            visual.Trails[i].GameObject.SetActive(false);
            visual.Trails[i].Position = Vector2.zero;
            visual.Trails[i].Transform.anchoredPosition = Vector2.zero;
            visual.Trails[i].Transform.localScale = Vector3.one;
            visual.Trails[i].Transform.localEulerAngles = Vector3.zero;
        }

        _mergeCoinFlightVisualPool.Push(visual);
    }

    private void QueueMergeCoinTargetPulse(bool isFinalCoin)
    {
        float impactStrength = isFinalCoin
            ? Mathf.Max(1f, mergeCoinFinalImpactMultiplier)
            : 1f;
        _mergeCoinTargetImpactStrength = Mathf.Max(
            _mergeCoinTargetImpactStrength,
            impactStrength
        );

        if (_mergeCoinTargetPulseRoutine != null)
            StopCoroutine(_mergeCoinTargetPulseRoutine);

        _mergeCoinTargetPulseRoutine = StartCoroutine(
            PlayMergeCoinTargetPulse()
        );
    }

    private IEnumerator PlayMergeCoinTargetPulse()
    {
        if (mergeCoinTarget == null)
        {
            _mergeCoinTargetPulseRoutine = null;
            yield break;
        }

        Vector3 baseScale = _mergeCoinTargetBaseScale == Vector3.zero
            ? mergeCoinTarget.localScale
            : _mergeCoinTargetBaseScale;
        Vector3 startScale = mergeCoinTarget.localScale;
        float strength = Mathf.Max(1f, _mergeCoinTargetImpactStrength);
        _mergeCoinTargetImpactStrength = 0f;
        float peakMultiplier = 1f +
            (Mathf.Max(1f, mergeCoinTargetPulseScale) - 1f) * strength;
        Vector3 peakScale = baseScale * peakMultiplier;
        Vector3 undershootScale = baseScale * mergeCoinTargetPulseUndershoot;
        float duration = Mathf.Max(0.05f, mergeCoinTargetPulseDuration);
        float elapsed = 0f;

        while (elapsed < duration && mergeCoinTarget != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (progress < 0.38f)
            {
                float phase = progress / 0.38f;
                phase = 1f - Mathf.Pow(1f - phase, 3f);
                mergeCoinTarget.localScale = Vector3.LerpUnclamped(
                    startScale,
                    peakScale,
                    phase
                );
            }
            else if (progress < 0.72f)
            {
                float phase = Mathf.InverseLerp(0.38f, 0.72f, progress);
                phase = phase * phase * (3f - 2f * phase);
                mergeCoinTarget.localScale = Vector3.LerpUnclamped(
                    peakScale,
                    undershootScale,
                    phase
                );
            }
            else
            {
                float phase = Mathf.InverseLerp(0.72f, 1f, progress);
                phase = 1f - Mathf.Pow(1f - phase, 3f);
                mergeCoinTarget.localScale = Vector3.LerpUnclamped(
                    undershootScale,
                    baseScale,
                    phase
                );
            }
            yield return null;
        }

        if (mergeCoinTarget != null)
            mergeCoinTarget.localScale = baseScale;

        _mergeCoinTargetPulseRoutine = null;
    }

    private void StopMergeCoinTargetPulse()
    {
        if (_mergeCoinTargetPulseRoutine != null)
            StopCoroutine(_mergeCoinTargetPulseRoutine);

        _mergeCoinTargetPulseRoutine = null;
        _mergeCoinTargetImpactStrength = 0f;

        if (mergeCoinTarget != null && _mergeCoinTargetBaseScale != Vector3.zero)
            mergeCoinTarget.localScale = _mergeCoinTargetBaseScale;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        Observer.FoodBoxCompleted -= FoodBoxCompleted;
        Observer.ActiveBoosterChanged -= UpdateUseBooster;

        StopBoosterFocusAnimation(true);

        if (useBooster != null)
            useBooster.SetActive(false);

        if (_currentLevel != null)
        {
            _currentLevel.OnBoardChanged -= UpdateTargetsUI;
            _currentLevel.OnBoardChanged -= EvaluateTrayFullWarning;
            _currentLevel.OnMoveCountChanged -= UpdateMoveCount;
            _currentLevel.OnMoveBonusGranted -= PlayMoveBonusFly;
        }

        StopMoveBonusFly(false);
        StopMoveRoll(true);
        StopBottomEntrance(true);
        ResetTrayFullWarning();
        StopOutOfMoveTransition();
        CancelMergeCoinSequences();
        StopMergeCoinTargetPulse();
    }

    private void UpdateUseBooster(BoosterType? activeBooster)
    {
        if (useBooster == null)
            return;

        bool shouldShow = activeBooster == BoosterType.MagicMove ||
                          activeBooster == BoosterType.Magnet ||
                          activeBooster == BoosterType.Lighter;

        if (!shouldShow)
        {
            StartBoosterFocusExit();
            return;
        }

        BoosterType type = activeBooster.Value;
        InGameBoosterItem[] boosterItems =
            GetComponentsInChildren<InGameBoosterItem>(true);

        for (int i = 0; i < boosterItems.Length; i++)
        {
            InGameBoosterItem item = boosterItems[i];
            if (item != null && item.BoosterType == type)
            {
                if (useBoosterIcon != null)
                    useBoosterIcon.sprite = item.GetDisplaySprite();

                break;
            }
        }

        switch (type)
        {
            case BoosterType.MagicMove:
                SetUseBoosterContent(
                    magicMoveTitleSprite,
                    "Move matching cards to another tray!"
                );
                break;
            case BoosterType.Magnet:
                SetUseBoosterContent(
                    magnetTitleSprite,
                    "Tap a tray to collect matching cards!"
                );
                break;
            case BoosterType.Lighter:
                SetUseBoosterContent(
                    lighterTitleSprite,
                    "Tap a stack to burn matching cards!"
                );
                break;
        }

        StartBoosterFocusEnter(type);
    }

    private void SetUseBoosterContent(Sprite titleSprite, string instruction)
    {
        if (boosterTitleImage != null)
            boosterTitleImage.sprite = titleSprite;

        if (boosterInstructionText != null)
            boosterInstructionText.text = string.Empty;

        _boosterInstruction = instruction ?? string.Empty;
    }

    private void CacheBoosterFocusLayout()
    {
        if (_boosterFocusPositionsCached)
            return;

        if (boosterFocusTopbar == null)
            boosterFocusTopbar = FindChildRecursive(transform, "Topbar") as RectTransform;
        if (boosterFocusCoinTray == null)
            boosterFocusCoinTray = FindChildRecursive(transform, "coin_tray") as RectTransform;

        if (boosterFocusTopbar == null || boosterFocusCoinTray == null)
            return;

        _boosterTopbarHomePosition = boosterFocusTopbar.anchoredPosition;
        _boosterCoinTrayHomePosition = boosterFocusCoinTray.anchoredPosition;
        _boosterCoinTrayHomeScale = boosterFocusCoinTray.localScale;
        _boosterFocusPositionsCached = true;
    }

    private void StartBoosterFocusEnter(BoosterType activeBooster)
    {
        CacheBoosterFocusLayout();
        StopBoosterFocusAnimation(false);

        if (useBooster != null)
            useBooster.SetActive(false);

        if (_currentLevel != null)
            _currentLevel.SetBoosterFocusDimVisible(false);

        _boosterFocusTransitionRoutine = StartCoroutine(
            PlayBoosterFocusEnter(activeBooster)
        );
    }

    private IEnumerator PlayBoosterFocusEnter(BoosterType activeBooster)
    {
        yield return MoveBoosterFocusLayout(true);
        _boosterFocusTransitionRoutine = null;

        if (_currentLevel == null ||
            _currentLevel.ActiveBooster != activeBooster)
            yield break;

        _currentLevel.SetBoosterFocusDimVisible(true);
        _boosterFocusVisible = true;

        if (activeBooster == BoosterType.Lighter)
            SoundController.Instance?.PlayFX(SoundName.GaslighterOpen);

        useBooster.transform.SetAsLastSibling();
        useBooster.SetActive(true);
        ConfigureUseBoosterSorting();
        PlayUseBoosterGoldDust();
        _boosterContentRoutine = StartCoroutine(PlayUseBoosterContent());
    }

    private void StartBoosterFocusExit()
    {
        CacheBoosterFocusLayout();

        if (_boosterFocusTransitionRoutine == null &&
            !_boosterFocusVisible &&
            (useBooster == null || !useBooster.activeSelf))
        {
            ResetBoosterFocusLayout();
            return;
        }

        StopBoosterFocusAnimation(false);
        HideUseBoosterVisuals();

        if (_currentLevel != null)
            _currentLevel.SetBoosterFocusDimVisible(false);

        _boosterFocusVisible = false;
        _boosterFocusTransitionRoutine = StartCoroutine(
            PlayBoosterFocusExit()
        );
    }

    private IEnumerator PlayBoosterFocusExit()
    {
        yield return MoveBoosterFocusLayout(false);
        _boosterFocusTransitionRoutine = null;
    }

    private IEnumerator MoveBoosterFocusLayout(bool focused)
    {
        if (!_boosterFocusPositionsCached)
            yield break;

        Vector2 topbarStart = boosterFocusTopbar.anchoredPosition;
        Vector3 coinScaleStart = boosterFocusCoinTray.localScale;
        Vector2 topbarTarget = _boosterTopbarHomePosition +
                               Vector2.up * boosterTopbarUpOffset;
        Vector3 coinScaleTarget = Vector3.zero;

        if (!focused)
        {
            topbarTarget = _boosterTopbarHomePosition;
            coinScaleTarget = _boosterCoinTrayHomeScale;
        }

        boosterFocusCoinTray.anchoredPosition = _boosterCoinTrayHomePosition;

        float duration = Mathf.Max(0.01f, boosterFocusMoveDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            boosterFocusTopbar.anchoredPosition =
                Vector2.LerpUnclamped(topbarStart, topbarTarget, eased);
            boosterFocusCoinTray.localScale =
                Vector3.LerpUnclamped(coinScaleStart, coinScaleTarget, eased);
            yield return null;
        }

        boosterFocusTopbar.anchoredPosition = topbarTarget;
        boosterFocusCoinTray.localScale = coinScaleTarget;
    }

    private IEnumerator PlayUseBoosterContent()
    {
        RectTransform icon = useBoosterIcon != null
            ? useBoosterIcon.rectTransform
            : null;
        RectTransform title = boosterTitleImage != null
            ? boosterTitleImage.rectTransform
            : null;

        if (icon != null)
            icon.localScale = Vector3.zero;
        if (title != null)
            title.localScale = Vector3.zero;
        if (boosterInstructionText != null)
            boosterInstructionText.text = string.Empty;

        float upDuration = Mathf.Max(0.01f, useBoosterPopUpDuration);
        float undershootDuration = Mathf.Max(
            0.01f,
            useBoosterPopUndershootDuration
        );
        float settleDuration = Mathf.Max(0.01f, useBoosterPopSettleDuration);
        float titleDelay = Mathf.Max(0f, useBoosterTitlePopDelay);
        float popDuration = upDuration + undershootDuration + settleDuration;
        float elapsed = 0f;
        while (elapsed < popDuration + titleDelay)
        {
            elapsed += Time.unscaledDeltaTime;

            if (icon != null)
            {
                icon.localScale = _useBoosterIconBaseScale *
                                  EvaluateBoosterPopScale(
                                      elapsed,
                                      upDuration,
                                      undershootDuration,
                                      settleDuration
                                  );
            }

            if (title != null)
            {
                title.localScale = _boosterTitleBaseScale *
                                   EvaluateBoosterPopScale(
                                       elapsed - titleDelay,
                                       upDuration,
                                       undershootDuration,
                                       settleDuration
                                   );
            }

            yield return null;
        }

        if (icon != null)
            icon.localScale = _useBoosterIconBaseScale;
        if (title != null)
            title.localScale = _boosterTitleBaseScale;

        if (boosterInstructionText != null)
        {
            float instructionDelay = 0f;
            while (instructionDelay < useBoosterInstructionStartDelay)
            {
                instructionDelay += Time.unscaledDeltaTime;
                yield return null;
            }

            for (int index = 0; index < _boosterInstruction.Length; index++)
            {
                boosterInstructionText.text =
                    _boosterInstruction.Substring(0, index + 1);

                if (boosterTypingSound != null &&
                    index % Mathf.Max(1, boosterTypingSoundEveryCharacters) == 0 &&
                    SoundController.Instance != null &&
                    SoundController.Instance.fxAudio != null)
                {
                    SoundController.Instance.fxAudio.PlayOneShot(
                        boosterTypingSound,
                        boosterTypingVolume
                    );
                }

                float wait = 0f;
                while (wait < boosterTypingInterval)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        _boosterContentRoutine = null;
    }

    private float EvaluateBoosterPopScale(
        float time,
        float upDuration,
        float undershootDuration,
        float settleDuration)
    {
        if (time <= 0f)
            return 0f;

        if (time < upDuration)
        {
            float progress = Mathf.Clamp01(time / upDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            return useBoosterOvershootScale * eased;
        }

        time -= upDuration;
        if (time < undershootDuration)
        {
            float progress = Mathf.Clamp01(time / undershootDuration);
            float eased = progress * progress * (3f - 2f * progress);
            return Mathf.Lerp(
                useBoosterOvershootScale,
                useBoosterUndershootScale,
                eased
            );
        }

        time -= undershootDuration;
        if (time < settleDuration)
        {
            float progress = Mathf.Clamp01(time / settleDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            return Mathf.Lerp(useBoosterUndershootScale, 1f, eased);
        }

        return 1f;
    }

    private void StopBoosterFocusAnimation(bool resetLayout)
    {
        if (_boosterFocusTransitionRoutine != null)
            StopCoroutine(_boosterFocusTransitionRoutine);
        if (_boosterContentRoutine != null)
            StopCoroutine(_boosterContentRoutine);

        _boosterFocusTransitionRoutine = null;
        _boosterContentRoutine = null;

        if (resetLayout)
        {
            HideUseBoosterVisuals();
            ResetBoosterFocusLayout();
            _boosterFocusVisible = false;
        }
    }

    private void HideUseBoosterVisuals()
    {
        if (useBoosterGoldDustParticles != null)
        {
            useBoosterGoldDustParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        if (useBooster != null)
            useBooster.SetActive(false);
        if (useBoosterIcon != null)
            useBoosterIcon.rectTransform.localScale = _useBoosterIconBaseScale;
        if (boosterTitleImage != null)
            boosterTitleImage.rectTransform.localScale = _boosterTitleBaseScale;
        if (boosterInstructionText != null)
            boosterInstructionText.text = string.Empty;
    }

    private void ResetBoosterFocusLayout()
    {
        if (!_boosterFocusPositionsCached)
            return;

        boosterFocusTopbar.anchoredPosition = _boosterTopbarHomePosition;
        boosterFocusCoinTray.anchoredPosition = _boosterCoinTrayHomePosition;
        boosterFocusCoinTray.localScale = _boosterCoinTrayHomeScale;
    }

    private void InitializeTrayFullWarning()
    {
        ResolveTrayFullTopbarReferences();

        if (trayFullButton == null || trayFullWarning == null)
            return;

        trayFullButton.anchorMin = new Vector2(0f, 0.5f);
        trayFullButton.anchorMax = new Vector2(0f, 0.5f);

        float width = Mathf.Max(
            trayFullButton.rect.width,
            trayFullButton.sizeDelta.x
        );

        _trayFullShownPosition = trayFullButton.anchoredPosition;
        if (!TryGetTrayFullBoosterMidpointX(out _trayFullShownPosition.x))
        {
            _trayFullShownPosition.x = trayFullSafeLeftMargin +
                                       width * trayFullButton.pivot.x;
        }

        _trayFullHiddenPosition = _trayFullShownPosition;
        _trayFullHiddenPosition.x = -trayFullHiddenPadding -
                                    width * (1f - trayFullButton.pivot.x);

        trayFullButton.anchoredPosition = _trayFullHiddenPosition;
        trayFullButton.gameObject.SetActive(false);
        _trayFullWarningGraphic = trayFullWarning.GetComponent<Graphic>();
        if (_trayFullWarningGraphic != null)
            _trayFullWarningBaseAlpha = _trayFullWarningGraphic.color.a;
        SetTrayFullWarningAlpha(0f);
        trayFullWarning.SetActive(false);

        if (trayFullHeart != null && !_trayFullHeartScaleCached)
        {
            _trayFullHeartBaseScale = trayFullHeart.localScale;
            _trayFullHeartScaleCached = true;
        }

        CacheTrayFullTopbarPositions();
        ResetTrayFullProgress();
    }

    private void ResolveTrayFullTopbarReferences()
    {
        if (trayFullTargetBar == null)
        {
            trayFullTargetBar = FindChildRecursive(transform, "Target_bar")
                as RectTransform;
        }

        if (trayFullProgressWarning == null)
        {
            trayFullProgressWarning =
                FindChildRecursive(transform, "Progress_warning")
                    as RectTransform;
        }

        Transform progressRoot = trayFullProgressWarning != null
            ? trayFullProgressWarning
            : transform;

        if (trayFullCountdownText == null)
        {
            Transform text = FindChildRecursive(progressRoot, "Text_progress");
            trayFullCountdownText = text != null
                ? text.GetComponent<TextMeshProUGUI>()
                : null;
        }

        if (trayFullProgressFill == null)
        {
            Transform fill = FindChildRecursive(progressRoot, "Progressfill");
            trayFullProgressFill = fill != null
                ? fill.GetComponent<Image>()
                : null;
        }

        if (trayFullHeart == null)
        {
            trayFullHeart = FindChildRecursive(progressRoot, "Image_heart")
                as RectTransform;
        }
    }

    private bool TryGetTrayFullBoosterMidpointX(out float anchoredPositionX)
    {
        anchoredPositionX = 0f;
        RectTransform magicMove = null;
        RectTransform lighter = null;
        InGameBoosterItem[] boosterItems =
            GetComponentsInChildren<InGameBoosterItem>(true);

        for (int i = 0; i < boosterItems.Length; i++)
        {
            InGameBoosterItem item = boosterItems[i];
            RectTransform button = item != null
                ? item.transform.parent as RectTransform
                : null;
            if (button == null)
                continue;

            if (item.BoosterType == BoosterType.MagicMove)
                magicMove = button;
            else if (item.BoosterType == BoosterType.Lighter)
                lighter = button;
        }

        RectTransform parent = trayFullButton.parent as RectTransform;
        if (magicMove == null || lighter == null || parent == null)
            return false;

        float magicMoveX = parent.InverseTransformPoint(magicMove.position).x;
        float lighterX = parent.InverseTransformPoint(lighter.position).x;
        float midpointX = (magicMoveX + lighterX) * 0.5f;
        float anchorX = Mathf.Lerp(
            parent.rect.xMin,
            parent.rect.xMax,
            trayFullButton.anchorMin.x
        );
        anchoredPositionX = midpointX - anchorX;
        return true;
    }

    private void RegisterTrayFullButton()
    {
        if (trayFullButton == null)
            return;

        _trayFullButtonComponent =
            trayFullButton.GetComponent<CustomButton>();

        if (_trayFullButtonComponent == null)
            return;

        _trayFullButtonComponent.Click.RemoveListener(OnClickTrayFullExit);
        _trayFullButtonComponent.Click.AddListener(OnClickTrayFullExit);
    }

    public void OnClickTrayFullExit()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null ||
            gameManager.gameState != GameState.PlayingGame)
        {
            return;
        }

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        ResetTrayFullWarning();
        gameManager.OnLoseGame(
            0f,
            LoseReason.BoardFull,
            allowContinue: false
        );
    }

    private void EvaluateTrayFullWarning()
    {
        if (!ShouldShowTrayFullWarning())
        {
            _cancelTrayFullWarning = true;
            if (_trayFullWarningRoutine == null)
                HideTrayFullVisuals();
            return;
        }

        if (_trayFullWarningRoutine != null ||
            trayFullButton == null ||
            trayFullWarning == null)
        {
            _cancelTrayFullWarning = false;
            return;
        }

        _cancelTrayFullWarning = false;
        _trayFullWarningRoutine = StartCoroutine(TrayFullWarningRoutine());
    }

    private bool ShouldShowTrayFullWarning()
    {
        return _currentLevel != null &&
               GameManager.Instance != null &&
               GameManager.Instance.gameState == GameState.PlayingGame &&
               !_currentLevel.HasAvailableDealSlots();
    }

    private IEnumerator TrayFullWarningRoutine()
    {
        trayFullButton.gameObject.SetActive(true);
        trayFullButton.SetAsLastSibling();
        trayFullButton.anchoredPosition = _trayFullHiddenPosition;
        trayFullWarning.SetActive(true);
        if (trayFullProgressWarning != null)
        {
            trayFullProgressWarning.gameObject.SetActive(true);
            SetTrayFullTopbarPositions(0f);
        }
        SetTrayFullWarningAlpha(0f);

        float elapsed = 0f;
        float warningPhase = 0f;
        UpdateTrayFullCountdown(trayFullCountdownDuration);

        while (elapsed < trayFullCountdownDuration)
        {
            if (_cancelTrayFullWarning ||
                _currentLevel == null ||
                GameManager.Instance == null ||
                GameManager.Instance.gameState != GameState.PlayingGame)
            {
                trayFullWarning.SetActive(false);
                yield return SlideTrayFullButtonOut();
                HideTrayFullVisuals();
                _trayFullWarningRoutine = null;
                EvaluateTrayFullWarning();
                yield break;
            }

            float deltaTime = Time.unscaledDeltaTime;
            elapsed += deltaTime;

            float slideProgress = trayFullSlideDuration > 0f
                ? Mathf.Clamp01(elapsed / trayFullSlideDuration)
                : 1f;
            trayFullButton.anchoredPosition = Vector2.LerpUnclamped(
                _trayFullHiddenPosition,
                _trayFullShownPosition,
                EaseOutBack(slideProgress)
            );
            float topbarProgress = trayFullTopbarTransitionDuration > 0f
                ? Mathf.Clamp01(elapsed / trayFullTopbarTransitionDuration)
                : 1f;
            SetTrayFullTopbarPositions(EaseOutQuad(topbarProgress));

            float countdownProgress = trayFullCountdownDuration > 0f
                ? Mathf.Clamp01(elapsed / trayFullCountdownDuration)
                : 1f;
            float startFrequency = 1f / Mathf.Max(
                0.1f,
                trayFullWarningStartCycleDuration
            );
            float endFrequency = 1f / Mathf.Max(
                0.1f,
                trayFullWarningEndCycleDuration
            );
            warningPhase = Mathf.Repeat(
                warningPhase + deltaTime * Mathf.Lerp(
                    startFrequency,
                    endFrequency,
                    countdownProgress
                ),
                1f
            );
            float warningAlpha = 0.5f -
                                 0.5f * Mathf.Cos(warningPhase * Mathf.PI * 2f);
            SetTrayFullWarningAlpha(warningAlpha);
            UpdateTrayFullProgress(countdownProgress, warningAlpha);

            UpdateTrayFullCountdown(
                Mathf.Max(0f, trayFullCountdownDuration - elapsed)
            );
            yield return null;
        }

        bool shouldLose = ShouldShowTrayFullWarning();
        _trayFullWarningRoutine = null;

        if (shouldLose && GameManager.Instance != null)
        {
            // Keep the warning topbar in place while the lose popup is shown.
            // It only returns to Target_bar after the tray warning is resolved.
            SetTrayFullTopbarPositions(1f);
            GameManager.Instance.OnLoseGame(
                0f,
                LoseReason.BoardFull,
                allowContinue: false
            );
            yield break;
        }

        HideTrayFullVisuals();
    }

    private IEnumerator SlideTrayFullButtonOut()
    {
        Vector2 startPosition = trayFullButton.anchoredPosition;
        float topbarStartProgress = _trayFullTopbarTransitionProgress;
        float duration = Mathf.Max(0.05f, trayFullSlideDuration * 0.7f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            trayFullButton.anchoredPosition = Vector2.Lerp(
                startPosition,
                _trayFullHiddenPosition,
                progress * progress
            );
            SetTrayFullTopbarPositions(Mathf.Lerp(
                topbarStartProgress,
                0f,
                progress * progress
            ));
            yield return null;
        }
    }

    private void UpdateTrayFullCountdown(float remainingTime)
    {
        if (trayFullCountdownText != null)
        {
            trayFullCountdownText.text = Mathf.CeilToInt(remainingTime)
                .ToString();
        }
    }

    private void ResetTrayFullWarning()
    {
        if (_trayFullWarningRoutine != null)
        {
            StopCoroutine(_trayFullWarningRoutine);
            _trayFullWarningRoutine = null;
        }

        _cancelTrayFullWarning = false;
        HideTrayFullVisuals();
    }

    private void HideTrayFullVisuals()
    {
        if (trayFullWarning != null)
        {
            SetTrayFullWarningAlpha(0f);
            trayFullWarning.SetActive(false);
        }

        if (trayFullButton != null)
        {
            trayFullButton.anchoredPosition = _trayFullHiddenPosition;
            trayFullButton.gameObject.SetActive(false);
        }

        SetTrayFullTopbarPositions(0f);
        if (trayFullProgressWarning != null)
            trayFullProgressWarning.gameObject.SetActive(false);

        ResetTrayFullProgress();
    }

    private void UpdateTrayFullProgress(float elapsedProgress, float warningPulse)
    {
        float remainingProgress = 1f - Mathf.Clamp01(elapsedProgress);

        if (trayFullProgressFill != null)
        {
            trayFullProgressFill.fillAmount = remainingProgress;
            trayFullProgressFill.color = remainingProgress <
                TrayFullProgressDangerThreshold
                ? trayFullProgressDangerColor
                : trayFullProgressSafeColor;
        }

        if (trayFullHeart != null && _trayFullHeartScaleCached)
        {
            float scale = Mathf.Lerp(0.9f, 1.1f, Mathf.Clamp01(warningPulse));
            trayFullHeart.localScale = _trayFullHeartBaseScale * scale;
        }
    }

    private void ResetTrayFullProgress()
    {
        if (trayFullProgressFill != null)
        {
            trayFullProgressFill.fillAmount = 1f;
            trayFullProgressFill.color = trayFullProgressSafeColor;
        }

        if (trayFullHeart != null && _trayFullHeartScaleCached)
            trayFullHeart.localScale = _trayFullHeartBaseScale;
    }

    private void CacheTrayFullTopbarPositions()
    {
        if (trayFullTargetBar != null)
            _trayFullTargetBarHomePosition = trayFullTargetBar.anchoredPosition;

        if (trayFullProgressWarning != null)
        {
            _trayFullProgressHiddenPosition =
                trayFullProgressWarning.anchoredPosition;
            _trayFullProgressShownPosition = trayFullTargetBar != null
                ? _trayFullTargetBarHomePosition
                : _trayFullProgressHiddenPosition;
        }

        if (trayFullTargetBar != null && trayFullProgressWarning != null)
        {
            float moveDistance = Mathf.Abs(
                _trayFullProgressHiddenPosition.y -
                _trayFullProgressShownPosition.y
            );
            _trayFullTargetBarWarningPosition =
                _trayFullTargetBarHomePosition + Vector2.up * moveDistance;
        }
        else
        {
            _trayFullTargetBarWarningPosition =
                _trayFullTargetBarHomePosition;
        }

        _trayFullTopbarPositionsCached = trayFullTargetBar != null ||
                                          trayFullProgressWarning != null;
        SetTrayFullTopbarPositions(0f);

        if (trayFullProgressWarning != null)
            trayFullProgressWarning.gameObject.SetActive(false);
    }

    private void SetTrayFullTopbarPositions(float progress)
    {
        if (!_trayFullTopbarPositionsCached)
            return;

        _trayFullTopbarTransitionProgress = Mathf.Clamp01(progress);

        if (trayFullTargetBar != null)
        {
            trayFullTargetBar.anchoredPosition = Vector2.LerpUnclamped(
                _trayFullTargetBarHomePosition,
                _trayFullTargetBarWarningPosition,
                _trayFullTopbarTransitionProgress
            );
        }

        if (trayFullProgressWarning != null)
        {
            trayFullProgressWarning.anchoredPosition = Vector2.LerpUnclamped(
                _trayFullProgressHiddenPosition,
                _trayFullProgressShownPosition,
                _trayFullTopbarTransitionProgress
            );
        }
    }

    private static float EaseOutQuad(float progress)
    {
        float inverse = 1f - Mathf.Clamp01(progress);
        return 1f - inverse * inverse;
    }

    private void SetTrayFullWarningAlpha(float alpha)
    {
        if (_trayFullWarningGraphic == null)
            return;

        Color color = _trayFullWarningGraphic.color;
        color.a = _trayFullWarningBaseAlpha * Mathf.Clamp01(alpha);
        _trayFullWarningGraphic.color = color;
    }

    private void InitializeOutOfMoveTransition()
    {
        if (outOfMoveTab != null)
            _outOfMoveCenterPosition = outOfMoveTab.anchoredPosition;

        if (outOfMoveDarkCanvasGroup == null &&
            outOfMoveDarkBackground != null)
        {
            outOfMoveDarkCanvasGroup =
                outOfMoveDarkBackground.GetComponent<CanvasGroup>();
        }

        ResetOutOfMoveVisuals();
    }

    public bool PlayOutOfMoveTransition(Action onComplete)
    {
        if (!isActiveAndEnabled ||
            outOfMoveDarkBackground == null ||
            outOfMoveTab == null)
        {
            return false;
        }

        if (_outOfMoveRoutine != null)
            return true;

        _outOfMoveRoutine = StartCoroutine(
            OutOfMoveTransitionRoutine(onComplete)
        );
        return true;
    }

    private IEnumerator OutOfMoveTransitionRoutine(Action onComplete)
    {
        Canvas.ForceUpdateCanvases();

        RectTransform parent = outOfMoveTab.parent as RectTransform;
        if (parent == null)
        {
            _outOfMoveRoutine = null;
            onComplete?.Invoke();
            yield break;
        }

        float anchorY = Mathf.Lerp(
            parent.rect.yMin,
            parent.rect.yMax,
            outOfMoveTab.anchorMin.y
        );
        float tabHeight = outOfMoveTab.rect.height;
        float topY = parent.rect.yMax + outOfMoveOffscreenPadding +
                     tabHeight * outOfMoveTab.pivot.y - anchorY;
        float bottomY = parent.rect.yMin - outOfMoveOffscreenPadding -
                        tabHeight * (1f - outOfMoveTab.pivot.y) - anchorY;

        Vector2 topPosition = new Vector2(
            _outOfMoveCenterPosition.x,
            topY
        );
        Vector2 bottomPosition = new Vector2(
            _outOfMoveCenterPosition.x,
            bottomY
        );

        outOfMoveDarkBackground.SetActive(true);
        outOfMoveDarkBackground.transform.SetAsLastSibling();
        if (outOfMoveDarkCanvasGroup != null)
        {
            outOfMoveDarkCanvasGroup.alpha = 0f;
            outOfMoveDarkCanvasGroup.interactable = false;
            outOfMoveDarkCanvasGroup.blocksRaycasts = true;
        }

        outOfMoveTab.gameObject.SetActive(true);
        outOfMoveTab.SetAsLastSibling();
        outOfMoveTab.anchoredPosition = topPosition;

        yield return AnimateOutOfMoveTab(
            topPosition,
            _outOfMoveCenterPosition,
            outOfMoveDropDuration + outOfMoveSettleDuration,
            progress => EaseOutBack(
                progress,
                outOfMoveLandingOvershoot /
                Mathf.Max(1f, Mathf.Abs(topPosition.y -
                                        _outOfMoveCenterPosition.y))
            ),
            true
        );

        float holdElapsed = 0f;
        while (holdElapsed < outOfMoveHoldDuration)
        {
            holdElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return AnimateOutOfMoveTab(
            _outOfMoveCenterPosition,
            bottomPosition,
            outOfMoveExitLiftDuration + outOfMoveExitDuration,
            progress => EaseInBack(
                progress,
                outOfMoveExitLift /
                Mathf.Max(1f, Mathf.Abs(bottomPosition.y -
                                        _outOfMoveCenterPosition.y))
            )
        );

        ResetOutOfMoveVisuals();
        _outOfMoveRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator AnimateOutOfMoveTab(
        Vector2 from,
        Vector2 to,
        float duration,
        Func<float, float> ease,
        bool fadeDarkBackground = false)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = ease != null ? ease(progress) : progress;
            outOfMoveTab.anchoredPosition = Vector2.LerpUnclamped(
                from,
                to,
                easedProgress
            );

            if (fadeDarkBackground && outOfMoveDarkCanvasGroup != null)
            {
                float fadeProgress = Mathf.Clamp01(
                    elapsed / Mathf.Max(0.01f, outOfMoveDarkFadeDuration)
                );
                outOfMoveDarkCanvasGroup.alpha = Mathf.Lerp(
                    0f,
                    outOfMoveDarkAlpha,
                    Mathf.SmoothStep(0f, 1f, fadeProgress)
                );
            }

            yield return null;
        }

        outOfMoveTab.anchoredPosition = to;
        if (fadeDarkBackground && outOfMoveDarkCanvasGroup != null)
            outOfMoveDarkCanvasGroup.alpha = outOfMoveDarkAlpha;
    }

    private void StopOutOfMoveTransition()
    {
        if (_outOfMoveRoutine != null)
        {
            StopCoroutine(_outOfMoveRoutine);
            _outOfMoveRoutine = null;
        }

        ResetOutOfMoveVisuals();
    }

    private void ResetOutOfMoveVisuals()
    {
        if (outOfMoveTab != null)
        {
            outOfMoveTab.anchoredPosition = _outOfMoveCenterPosition;
            outOfMoveTab.gameObject.SetActive(false);
        }

        if (outOfMoveDarkCanvasGroup != null)
        {
            outOfMoveDarkCanvasGroup.alpha = 0f;
            outOfMoveDarkCanvasGroup.interactable = false;
            outOfMoveDarkCanvasGroup.blocksRaycasts = false;
        }

        if (outOfMoveDarkBackground != null)
            outOfMoveDarkBackground.SetActive(false);
    }

    private static float EaseOutBack(float progress, float overshootRatio)
    {
        progress = Mathf.Clamp01(progress);
        float overshoot = Mathf.Lerp(
            1.70158f,
            3.4f,
            Mathf.Clamp01(overshootRatio / .18f)
        );
        float shifted = progress - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted +
               overshoot * shifted * shifted;
    }

    private static float EaseInBack(float progress, float overshootRatio)
    {
        progress = Mathf.Clamp01(progress);
        float overshoot = Mathf.Lerp(
            1.70158f,
            3.4f,
            Mathf.Clamp01(overshootRatio / .18f)
        );
        return (overshoot + 1f) * progress * progress * progress -
               overshoot * progress * progress;
    }

    private static float EaseOutBack(float progress)
    {
        const float overshoot = 1.70158f;
        float shifted = progress - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted +
               overshoot * shifted * shifted;
    }

    private void UpdateMoveCount(int count)
    {
        if (moveText == null)
            return;

        InitializeMoveOdometer();

        if (_deferMoveCountUntilBonusArrival)
        {
            _moveRollTargetCount = Mathf.Max(0, count);
            return;
        }

        if (!isActiveAndEnabled || !_hasDisplayedMoveCount)
        {
            SetMoveCountImmediate(count);
            return;
        }

        if (count == _displayedMoveCount)
        {
            SetMoveDigitColors(GetCurrentMoveColor());
            return;
        }

        StartMoveRoll(count);
    }

    private void InitializeMoveOdometer()
    {
        if (moveText == null || _moveDigits.Count > 0)
            return;

        RectTransform digitRoot = moveText.transform.parent as RectTransform;
        TextMeshProUGUI[] digitTexts = digitRoot != null
            ? digitRoot.GetComponentsInChildren<TextMeshProUGUI>(true)
            : new[] { moveText };

        System.Array.Sort(
            digitTexts,
            (left, right) => left.rectTransform.anchoredPosition.x
                .CompareTo(right.rectTransform.anchoredPosition.x)
        );

        for (int i = 0; i < digitTexts.Length; i++)
        {
            TextMeshProUGUI digit = digitTexts[i];
            if (digit == null || digit.transform.parent != moveText.transform.parent)
                continue;

            _moveDigits.Add(digit);
            _moveDigitHomePositions.Add(digit.rectTransform.anchoredPosition);
        }

        if (_moveDigits.Count == 0)
        {
            _moveDigits.Add(moveText);
            _moveDigitHomePositions.Add(moveText.rectTransform.anchoredPosition);
        }

        if (_moveDigits.Count > 1 && digitRoot != null &&
            digitRoot.GetComponent<RectMask2D>() == null)
        {
            digitRoot.gameObject.AddComponent<RectMask2D>();
        }
    }

    private void SetMoveCountImmediate(int count)
    {
        InitializeMoveOdometer();
        StopMoveRoll(false);

        _displayedMoveCount = Mathf.Max(0, count);
        _moveRollTargetCount = _displayedMoveCount;
        _hasDisplayedMoveCount = true;

        SetMoveDigitValues(_displayedMoveCount);
        SetMoveDigitColors(GetCurrentMoveColor());
        ResetMoveDigitPositions();
    }

    private void StartMoveRoll(int targetCount)
    {
        StopMoveRoll(true);

        targetCount = Mathf.Max(0, targetCount);
        if (targetCount == _displayedMoveCount)
        {
            SetMoveDigitColors(GetCurrentMoveColor());
            return;
        }

        _moveRollTargetCount = targetCount;
        _moveRollRoutine = StartCoroutine(AnimateMoveRoll(targetCount));
    }

    public void PlayWinMoveCountdown(Action onCompleted = null)
    {
        InitializeMoveOdometer();
        StartMoveRoll(0);
        StartCoroutine(WaitForWinMoveCountdown(onCompleted));
    }

    private IEnumerator WaitForWinMoveCountdown(Action onCompleted)
    {
        while (_moveRollRoutine != null)
            yield return null;

        onCompleted?.Invoke();
    }

    private IEnumerator AnimateMoveRoll(int targetCount)
    {
        string oldValue = FormatMoveCount(_displayedMoveCount);
        string newValue = FormatMoveCount(targetCount);
        float direction = targetCount < _displayedMoveCount ? -1f : 1f;
        Color targetColor = GetCurrentMoveColor();

        _moveRollOldDigits.Clear();
        _moveRollDigitIndices.Clear();

        for (int i = 0; i < _moveDigits.Count; i++)
        {
            TextMeshProUGUI digit = _moveDigits[i];
            if (digit == null)
                continue;

            string oldDigit = _moveDigits.Count <= 1
                ? oldValue
                : GetMoveDigit(oldValue, i).ToString();
            string newDigit = _moveDigits.Count <= 1
                ? newValue
                : GetMoveDigit(newValue, i).ToString();
            digit.text = newDigit;

            if (oldDigit == newDigit)
            {
                digit.color = targetColor;
                continue;
            }

            TextMeshProUGUI oldVisual = Instantiate(digit, digit.transform.parent);
            oldVisual.name = $"{digit.name}_RollOut";
            oldVisual.text = oldDigit;
            oldVisual.color = digit.color;
            oldVisual.raycastTarget = false;
            oldVisual.rectTransform.anchoredPosition = _moveDigitHomePositions[i];

            digit.color = targetColor;
            _moveRollOldDigits.Add(oldVisual);
            _moveRollDigitIndices.Add(i);
        }

        float duration = Mathf.Max(0.05f, moveRollDuration);
        float maximumDelay = _moveDigits.Count > 1
            ? (_moveDigits.Count - 1) * moveRollStagger
            : 0f;
        float elapsed = 0f;

        while (elapsed < duration + maximumDelay)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < _moveRollOldDigits.Count; i++)
            {
                TextMeshProUGUI oldVisual = _moveRollOldDigits[i];
                int digitIndex = _moveRollDigitIndices[i];
                TextMeshProUGUI newVisual = _moveDigits[digitIndex];
                if (oldVisual == null || newVisual == null)
                    continue;

                float delay = (_moveDigits.Count - 1 - digitIndex) *
                              moveRollStagger;
                float progress = Mathf.Clamp01((elapsed - delay) / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                Vector2 home = _moveDigitHomePositions[digitIndex];
                float distance = Mathf.Max(
                    1f,
                    Mathf.Abs(newVisual.rectTransform.rect.height)
                ) * moveRollDistanceMultiplier;
                Vector2 travel = Vector2.up * distance * direction;

                oldVisual.rectTransform.anchoredPosition =
                    home + travel * eased;
                newVisual.rectTransform.anchoredPosition =
                    home - travel * (1f - eased);
            }

            yield return null;
        }

        FinishMoveRoll(targetCount);
    }

    private void FinishMoveRoll(int targetCount)
    {
        CleanupMoveRollOldDigits();
        _moveRollRoutine = null;
        _displayedMoveCount = Mathf.Max(0, targetCount);
        _moveRollTargetCount = _displayedMoveCount;
        _hasDisplayedMoveCount = true;
        SetMoveDigitValues(_displayedMoveCount);
        SetMoveDigitColors(GetCurrentMoveColor());
        ResetMoveDigitPositions();
    }

    private void StopMoveRoll(bool completeCurrentRoll)
    {
        if (_moveRollRoutine != null)
        {
            StopCoroutine(_moveRollRoutine);
            _moveRollRoutine = null;
        }

        CleanupMoveRollOldDigits();
        ResetMoveDigitPositions();

        if (!completeCurrentRoll || !_hasDisplayedMoveCount)
            return;

        _displayedMoveCount = Mathf.Max(0, _moveRollTargetCount);
        SetMoveDigitValues(_displayedMoveCount);
        SetMoveDigitColors(GetCurrentMoveColor());
    }

    private void CleanupMoveRollOldDigits()
    {
        for (int i = 0; i < _moveRollOldDigits.Count; i++)
        {
            TextMeshProUGUI oldDigit = _moveRollOldDigits[i];
            if (oldDigit == null)
                continue;

            oldDigit.gameObject.SetActive(false);
            Destroy(oldDigit.gameObject);
        }

        _moveRollOldDigits.Clear();
        _moveRollDigitIndices.Clear();
    }

    private void SetMoveDigitValues(int count)
    {
        string value = FormatMoveCount(count);

        for (int i = 0; i < _moveDigits.Count; i++)
        {
            if (_moveDigits[i] != null)
            {
                _moveDigits[i].text = _moveDigits.Count <= 1
                    ? value
                    : GetMoveDigit(value, i).ToString();
            }
        }
    }

    private string FormatMoveCount(int count)
    {
        count = Mathf.Max(0, count);
        if (_moveDigits.Count <= 1)
            return count.ToString();

        string value = count.ToString();
        if (value.Length > _moveDigits.Count)
            value = value.Substring(value.Length - _moveDigits.Count);

        return value.PadLeft(_moveDigits.Count, '0');
    }

    private char GetMoveDigit(string value, int index)
    {
        return index >= 0 && index < value.Length ? value[index] : '0';
    }

    private Color GetCurrentMoveColor()
    {
        return _currentLevel != null &&
               _currentLevel.RemainingFreeMoveCount > 0
            ? freeMoveColor
            : normalMoveColor;
    }

    private void SetMoveDigitColors(Color color)
    {
        for (int i = 0; i < _moveDigits.Count; i++)
        {
            if (_moveDigits[i] != null)
                _moveDigits[i].color = color;
        }
    }

    private void ResetMoveDigitPositions()
    {
        int count = Mathf.Min(
            _moveDigits.Count,
            _moveDigitHomePositions.Count
        );

        for (int i = 0; i < count; i++)
        {
            if (_moveDigits[i] != null)
            {
                _moveDigits[i].rectTransform.anchoredPosition =
                    _moveDigitHomePositions[i];
            }
        }
    }

    private void PlayStartMoveBonusIfNeeded()
    {
        int bonusAmount = _currentLevel != null
            ? _currentLevel.RemainingFreeMoveCount
            : 0;

        if (bonusAmount <= 0 || moveBonusFlyPrefab == null)
        {
            UpdateMoveCount(
                _currentLevel != null
                    ? _currentLevel.CurrentMoveCount
                    : 0
            );
            return;
        }

        StopMoveBonusFly(false);

        if (moveText != null)
        {
            SetMoveCountImmediate(Mathf.Max(
                0,
                _currentLevel.CurrentMoveCount - bonusAmount
            ));
            SetMoveDigitColors(normalMoveColor);
        }

        StartMoveBonusFly(bonusAmount, true);
    }

    private void PlayMoveBonusFly(int bonusAmount)
    {
        if (_suppressNextMoveBonusFly)
        {
            _suppressNextMoveBonusFly = false;
            return;
        }

        if (bonusAmount <= 0 ||
            (moveBonusFlyPrefab == null && !_hasPreparedMoveBonusVisual))
        {
            _deferMoveCountUntilBonusArrival = false;
            return;
        }

        if (!_hasPreparedMoveBonusVisual)
            StopMoveBonusFly(true);
        StartMoveBonusFly(bonusAmount, false);
    }

    public bool PrepareMoveBonusFlyFrom(RectTransform source)
    {
        if (source == null)
            return false;

        RectTransform parent = moveBonusFlyRoot != null
            ? moveBonusFlyRoot
            : transform as RectTransform;

        if (parent == null)
            return false;

        StopMoveBonusFly(false);

        _activeMoveBonusVisual = Instantiate(source, parent);
        _activeMoveBonusVisual.name = "Icon_Move_plus_Flying";
        _activeMoveBonusVisual.position = source.position;
        _activeMoveBonusVisual.rotation = source.rotation;
        _activeMoveBonusVisual.localScale = source.lossyScale;
        _activeMoveBonusVisual.gameObject.SetActive(true);

        _preparedMoveBonusWorldPosition = source.position;
        _hasPreparedMoveBonusVisual = true;
        _deferMoveCountUntilBonusArrival = true;
        return true;
    }

    private void StartMoveBonusFly(
        int bonusAmount,
        bool revealMoveCountOnArrival)
    {
        RectTransform parent = moveBonusFlyRoot != null
            ? moveBonusFlyRoot
            : transform as RectTransform;

        if (parent == null || moveText == null)
        {
            if (revealMoveCountOnArrival && _currentLevel != null)
                UpdateMoveCount(_currentLevel.CurrentMoveCount);
            return;
        }

        bool hasPreparedVisual = _hasPreparedMoveBonusVisual &&
                                 _activeMoveBonusVisual != null;
        _activeMoveBonusUsesPreparedVisual = hasPreparedVisual;

        if (!hasPreparedVisual)
        {
            _activeMoveBonusVisual = Instantiate(
                moveBonusFlyPrefab,
                parent
            );
        }

        _hasPreparedMoveBonusVisual = false;
        _activeMoveBonusVisual.gameObject.SetActive(true);

        TextMeshProUGUI bonusText =
            _activeMoveBonusVisual.GetComponentInChildren<TextMeshProUGUI>(true);
        if (bonusText != null)
            bonusText.text = $"+{bonusAmount}";

        _moveBonusFlyRoutine = StartCoroutine(
            AnimateMoveBonusFly(
                revealMoveCountOnArrival
            )
        );
    }

    private IEnumerator AnimateMoveBonusFly(
        bool revealMoveCountOnArrival)
    {
        if (moveBonusFlyDelay > 0f)
            yield return new WaitForSecondsRealtime(moveBonusFlyDelay);

        if (_activeMoveBonusVisual == null || moveText == null)
            yield break;

        RectTransform visual = _activeMoveBonusVisual;
        RectTransform parent = visual.parent as RectTransform;
        Vector3 targetPosition = parent != null
            ? parent.InverseTransformPoint(moveText.rectTransform.position)
            : moveText.rectTransform.position;
        Vector3 startPosition = _activeMoveBonusUsesPreparedVisual
            ? (parent != null
                ? parent.InverseTransformPoint(_preparedMoveBonusWorldPosition)
                : _preparedMoveBonusWorldPosition)
            : moveBonusFlyStartPoint != null
                ? (parent != null
                    ? parent.InverseTransformPoint(moveBonusFlyStartPoint.position)
                    : moveBonusFlyStartPoint.position)
                : targetPosition + Vector3.down * (moveBonusFlyArcHeight * 1.35f);
        Vector3 controlPoint =
            (startPosition + targetPosition) * 0.5f +
            Vector3.up * moveBonusFlyArcHeight;
        Vector3 baseScale = visual.localScale;
        CanvasGroup canvasGroup = visual.GetComponent<CanvasGroup>();

        visual.localPosition = startPosition;
        visual.localScale = baseScale * moveBonusStartScale;

        float shakeDuration = Mathf.Max(0f, moveBonusShakeDuration);
        if (shakeDuration > 0f)
        {
            float shakeElapsed = 0f;
            while (shakeElapsed < shakeDuration)
            {
                shakeElapsed += Time.unscaledDeltaTime;
                float shakeProgress = Mathf.Clamp01(shakeElapsed / shakeDuration);
                float fade = 1f - shakeProgress;
                float wave = Mathf.Sin(shakeElapsed *
                                       Mathf.Max(0.1f, moveBonusShakeFrequency) *
                                       Mathf.PI * 2f);

                visual.localPosition = startPosition +
                    new Vector3(
                        wave * moveBonusShakeAmplitude * fade,
                        Mathf.Abs(wave) * moveBonusShakeAmplitude * 0.35f * fade,
                        0f
                    );
                visual.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    wave * moveBonusShakeAngle * fade
                );

                yield return null;
            }

            visual.localPosition = startPosition;
            visual.localRotation = Quaternion.identity;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, moveBonusFlyDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float inverse = 1f - eased;

            visual.localPosition =
                inverse * inverse * startPosition +
                2f * inverse * eased * controlPoint +
                eased * eased * targetPosition;

            float scale = progress < 0.42f
                ? Mathf.Lerp(
                    moveBonusStartScale,
                    moveBonusPeakScale,
                    progress / 0.42f)
                : Mathf.Lerp(
                    moveBonusPeakScale,
                    moveBonusEndScale,
                    (progress - 0.42f) / 0.58f);
            visual.localScale = baseScale * scale;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = progress < 0.72f
                    ? 1f
                    : Mathf.InverseLerp(1f, 0.72f, progress);
            }

            yield return null;
        }

        if (_deferMoveCountUntilBonusArrival)
        {
            _deferMoveCountUntilBonusArrival = false;
        }

        if ((revealMoveCountOnArrival || !_hasDisplayedMoveCount) &&
            _currentLevel != null)
            UpdateMoveCount(_currentLevel.CurrentMoveCount);
        else if (_currentLevel != null)
            UpdateMoveCount(_currentLevel.CurrentMoveCount);

        Tween.PunchScale(
            moveText.transform,
            Vector3.one * 0.18f,
            0.22f,
            1,
            useUnscaledTime: true
        );

        Destroy(visual.gameObject);
        _activeMoveBonusVisual = null;
        _moveBonusFlyRoutine = null;
    }

    private void StopMoveBonusFly(bool refreshMoveText)
    {
        if (_moveBonusFlyRoutine != null)
        {
            StopCoroutine(_moveBonusFlyRoutine);
            _moveBonusFlyRoutine = null;
        }

        if (_activeMoveBonusVisual != null)
        {
            Destroy(_activeMoveBonusVisual.gameObject);
            _activeMoveBonusVisual = null;
        }

        _hasPreparedMoveBonusVisual = false;
        _activeMoveBonusUsesPreparedVisual = false;
        _deferMoveCountUntilBonusArrival = false;

        if (refreshMoveText && _currentLevel != null)
            UpdateMoveCount(_currentLevel.CurrentMoveCount);
    }

    private void ResolveFreeMoveButton()
    {
        if (_dealBar == null)
            _dealBar = FindChildRecursive(transform, "Deal_bar") as RectTransform;

        if (_freeMoveButton == null)
        {
            Transform button = FindChildRecursive(
                _dealBar != null ? _dealBar : transform,
                "Button_free_move"
            );
            _freeMoveButton = button != null
                ? button.GetComponent<CustomButton>()
                : null;
        }

        if (_freeMoveButton == null)
            return;

        _freeMoveButton.Click.RemoveListener(OnClickFreeMove);
        _freeMoveButton.Click.AddListener(OnClickFreeMove);
    }

    private void StartDealBarEntrance()
    {
        ResolveFreeMoveButton();
        StopDealBarEntrance(true);

        if (_dealBar == null)
            return;

        if (!_dealBarPositionsCached)
        {
            _dealBarFinalPosition = _dealBar.anchoredPosition;
            _freeMoveButtonFinalPosition = FreeMoveButtonTransform != null
                ? FreeMoveButtonTransform.anchoredPosition
                : Vector2.zero;
            _dealBarPositionsCached = true;
        }

        _dealBar.anchoredPosition = _dealBarFinalPosition +
            Vector2.up * dealBarEntranceStartOffset;

        if (_freeMoveButton != null && FreeMoveButtonTransform != null)
        {
            FreeMoveButtonTransform.anchoredPosition =
                _freeMoveButtonFinalPosition +
                Vector2.up * freeMoveEntranceStartOffset;
            _freeMoveButton.Interactable = false;
        }

        _dealBarEntranceRoutine = StartCoroutine(AnimateDealBarEntrance());
    }

    private IEnumerator AnimateDealBarEntrance()
    {
        float elapsed = 0f;
        float dealDuration = Mathf.Max(0.1f, dealBarEntranceDuration);
        Vector2 dealStart = _dealBarFinalPosition +
            Vector2.up * dealBarEntranceStartOffset;

        while (elapsed < dealDuration && _dealBar != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / dealDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            _dealBar.anchoredPosition = Vector2.LerpUnclamped(
                dealStart,
                _dealBarFinalPosition,
                eased
            );
            yield return null;
        }

        if (_dealBar != null)
            _dealBar.anchoredPosition = _dealBarFinalPosition;

        if (_freeMoveButton != null && FreeMoveButtonTransform != null)
        {
            elapsed = 0f;
            float freeMoveDuration = Mathf.Max(0.1f, freeMoveEntranceDuration);
            Vector2 freeMoveStart = _freeMoveButtonFinalPosition +
                Vector2.up * freeMoveEntranceStartOffset;

            while (elapsed < freeMoveDuration &&
                   FreeMoveButtonTransform != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / freeMoveDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                FreeMoveButtonTransform.anchoredPosition =
                    Vector2.LerpUnclamped(
                        freeMoveStart,
                        _freeMoveButtonFinalPosition,
                        eased
                    );
                yield return null;
            }

            if (_freeMoveButton != null && FreeMoveButtonTransform != null)
            {
                FreeMoveButtonTransform.anchoredPosition =
                    _freeMoveButtonFinalPosition;
                _freeMoveButton.Interactable = !_freeMoveAdRequestInProgress;
            }
        }

        _dealBarEntranceRoutine = null;
    }

    private void StopDealBarEntrance(bool restoreFinalPositions)
    {
        if (_dealBarEntranceRoutine != null)
        {
            StopCoroutine(_dealBarEntranceRoutine);
            _dealBarEntranceRoutine = null;
        }

        if (!restoreFinalPositions || !_dealBarPositionsCached)
            return;

        if (_dealBar != null)
            _dealBar.anchoredPosition = _dealBarFinalPosition;

        if (_freeMoveButton != null && FreeMoveButtonTransform != null)
        {
            FreeMoveButtonTransform.anchoredPosition =
                _freeMoveButtonFinalPosition;
            _freeMoveButton.Interactable = !_freeMoveAdRequestInProgress;
        }
    }

    private void OnClickFreeMove()
    {
        if (_freeMoveAdRequestInProgress || _currentLevel == null ||
            GameManager.Instance == null ||
            GameManager.Instance.gameState != GameState.PlayingGame)
        {
            return;
        }

        _freeMoveAdRequestInProgress = true;
        if (_freeMoveButton != null)
            _freeMoveButton.Interactable = false;

        SoundController.Instance?.PlayFX(SoundName.ClickButton);

        Level requestedLevel = _currentLevel;
        if (AdsController.Instance == null)
        {
            CompleteFreeMoveAdReward(requestedLevel);
            return;
        }

        AdsController.Instance.ShowRewardAds(
            () => CompleteFreeMoveAdReward(requestedLevel),
            failedCallback: CancelFreeMoveAdRequest,
            placement: "PopupInGame_FreeMove",
            completeAfterClose: true
        );
    }

    private void CompleteFreeMoveAdReward(Level requestedLevel)
    {
        if (!_freeMoveAdRequestInProgress)
            return;

        _freeMoveAdRequestInProgress = false;
        if (requestedLevel == _currentLevel && requestedLevel != null &&
            GameManager.Instance != null &&
            GameManager.Instance.gameState == GameState.PlayingGame)
        {
            // Keep the reward focused on the DealText odometer. The normal
            // move-bonus fly visual is reserved for pre-game rewards.
            _suppressNextMoveBonusFly = true;
            requestedLevel.GrantMoveBonus(FreeMoveAdRewardAmount);
        }

        if (_freeMoveButton != null)
            _freeMoveButton.Interactable = true;
    }

    private void CancelFreeMoveAdRequest()
    {
        _freeMoveAdRequestInProgress = false;
        if (_freeMoveButton != null)
            _freeMoveButton.Interactable = true;
    }

    private void StartBottomEntrance(Level level)
    {
        StopBottomEntrance(true);

        if (level == null)
            return;

        PrepareSettingsButtonEntrance();
        StartDealBarEntrance();

        _bottomEntranceTab = FindChildRecursive(
            level.transform,
            "bottom_tab_ingame"
        );

        CardDesk cardDesk = level.GetComponentInChildren<CardDesk>(true);
        _initialBoardDealAnimator = level.InitialBoardDealAnimator;
        if (_initialBoardDealAnimator != null)
        {
            _initialBoardDealAnimator.DealCompleted -=
                HandleInitialBoardDealCompleted;
            _initialBoardDealAnimator.DealCompleted +=
                HandleInitialBoardDealCompleted;
        }
        _bottomEntranceDeck = cardDesk != null ? cardDesk.transform : null;
        _initialBoardDealAnimator?.Prepare();

        CacheBottomEntranceBoosters();

        if (_bottomEntranceTab == null &&
            _bottomEntranceDeck == null &&
            _bottomEntranceBoosters.Count == 0)
        {
            PlaySettingsButtonEntrance();
            _bottomEntranceRoutine = StartCoroutine(
                PlayInitialDealWhenTableReady()
            );
            return;
        }

        ConfigureBottomTabSorting();

        if (_bottomEntranceTab != null)
        {
            _bottomEntranceTabFinalPosition =
                _bottomEntranceTab.localPosition;
            _bottomEntranceTab.localPosition =
                _bottomEntranceTabFinalPosition +
                Vector3.down * bottomWorldStartOffset;
        }

        if (_bottomEntranceDeck != null)
        {
            _bottomEntranceDeckFinalPosition =
                _bottomEntranceDeck.localPosition;
            _bottomEntranceDeck.localPosition =
                _bottomEntranceDeckFinalPosition +
                Vector3.down * bottomWorldStartOffset;
        }

        for (int i = 0; i < _bottomEntranceBoosters.Count; i++)
        {
            RectTransform booster = _bottomEntranceBoosters[i];
            if (booster == null)
                continue;

            Vector2 finalPosition = booster.anchoredPosition;
            _bottomEntranceBoosterFinalPositions.Add(finalPosition);
            booster.anchoredPosition =
                finalPosition + Vector2.down * boosterUiStartOffset;
        }

        _bottomEntranceRoutine = StartCoroutine(AnimateBottomEntrance());
    }

    private void CacheBottomEntranceBoosters()
    {
        _bottomEntranceBoosters.Clear();
        _bottomEntranceBoosterFinalPositions.Clear();

        InGameBoosterItem[] boosterItems =
            GetComponentsInChildren<InGameBoosterItem>(true);

        for (int i = 0; i < boosterItems.Length; i++)
        {
            InGameBoosterItem item = boosterItems[i];
            RectTransform target = item != null
                ? item.transform.parent as RectTransform
                : null;

            if (target != null && !_bottomEntranceBoosters.Contains(target))
                _bottomEntranceBoosters.Add(target);
        }

        _bottomEntranceBoosters.Sort(
            (left, right) => left.anchoredPosition.x
                .CompareTo(right.anchoredPosition.x)
        );
    }

    private IEnumerator AnimateBottomEntrance()
    {
        if (bottomEntranceDelay > 0f)
            yield return new WaitForSecondsRealtime(bottomEntranceDelay);

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, bottomEntranceDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float inverse = 1f - progress;
            float eased = 1f - inverse * inverse * inverse;

            if (_bottomEntranceTab != null)
            {
                _bottomEntranceTab.localPosition = Vector3.LerpUnclamped(
                    _bottomEntranceTabFinalPosition +
                    Vector3.down * bottomWorldStartOffset,
                    _bottomEntranceTabFinalPosition,
                    eased
                );
            }

            if (_bottomEntranceDeck != null)
            {
                _bottomEntranceDeck.localPosition = Vector3.LerpUnclamped(
                    _bottomEntranceDeckFinalPosition +
                    Vector3.down * bottomWorldStartOffset,
                    _bottomEntranceDeckFinalPosition,
                    eased
                );
            }

            int boosterCount = Mathf.Min(
                _bottomEntranceBoosters.Count,
                _bottomEntranceBoosterFinalPositions.Count
            );

            for (int i = 0; i < boosterCount; i++)
            {
                RectTransform booster = _bottomEntranceBoosters[i];
                if (booster == null)
                    continue;

                Vector2 finalPosition =
                    _bottomEntranceBoosterFinalPositions[i];
                booster.anchoredPosition = Vector2.LerpUnclamped(
                    finalPosition + Vector2.down * boosterUiStartOffset,
                    finalPosition,
                    eased
                );
            }

            yield return null;
        }

        RestoreBottomEntranceFinalPositions();
        PlaySettingsButtonEntrance();

        while (_currentLevel != null &&
               _currentLevel.IsTableEntrancePlaying)
        {
            yield return null;
        }

        _bottomEntranceRoutine = null;
        _initialBoardDealAnimator?.Play();
    }

    private IEnumerator PlayInitialDealWhenTableReady()
    {
        while (_currentLevel != null &&
               _currentLevel.IsTableEntrancePlaying)
        {
            yield return null;
        }

        _bottomEntranceRoutine = null;
        _initialBoardDealAnimator?.Play();
    }

    private void StopBottomEntrance(bool restoreFinalPositions)
    {
        if (_bottomEntranceRoutine != null)
        {
            StopCoroutine(_bottomEntranceRoutine);
            _bottomEntranceRoutine = null;
        }

        if (restoreFinalPositions)
        {
            RestoreBottomEntranceFinalPositions();
            StopDealBarEntrance(true);
            StopSettingsButtonEntrance(true);
            _initialBoardDealAnimator?.StopAndRestore();
        }

        if (_initialBoardDealAnimator != null)
        {
            _initialBoardDealAnimator.DealCompleted -=
                HandleInitialBoardDealCompleted;
        }

        _bottomEntranceTab = null;
        _bottomEntranceDeck = null;
        _initialBoardDealAnimator = null;
        _bottomEntranceBoosters.Clear();
        _bottomEntranceBoosterFinalPositions.Clear();
    }

    private void PrepareSettingsButtonEntrance()
    {
        if (settingsButton == null)
        {
            Transform button = FindChildRecursive(transform, "Btn_setting");
            settingsButton = button as RectTransform;
        }

        if (settingsButton == null)
            return;

        if (!_settingsButtonScaleCached)
        {
            _settingsButtonFinalScale = settingsButton.localScale;
            _settingsButtonScaleCached = true;
        }

        settingsButton.localScale = Vector3.zero;
    }

    private void PlaySettingsButtonEntrance()
    {
        if (settingsButton == null)
            return;

        StopSettingsButtonEntrance(false);
        _settingsButtonEntranceRoutine = StartCoroutine(
            AnimateSettingsButtonEntrance()
        );
    }

    private IEnumerator AnimateSettingsButtonEntrance()
    {
        Vector3 overshootScale = _settingsButtonFinalScale *
            Mathf.Max(1f, settingsButtonOvershootScale);
        float elapsed = 0f;
        float scaleUpDuration = Mathf.Max(0.01f, settingsButtonScaleUpDuration);

        while (elapsed < scaleUpDuration && settingsButton != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / scaleUpDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            settingsButton.localScale = Vector3.LerpUnclamped(
                Vector3.zero,
                overshootScale,
                eased
            );
            yield return null;
        }

        elapsed = 0f;
        float settleDuration = Mathf.Max(0.01f, settingsButtonSettleDuration);
        while (elapsed < settleDuration && settingsButton != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / settleDuration);
            float eased = 1f - (1f - progress) * (1f - progress);
            settingsButton.localScale = Vector3.LerpUnclamped(
                overshootScale,
                _settingsButtonFinalScale,
                eased
            );
            yield return null;
        }

        if (settingsButton != null)
            settingsButton.localScale = _settingsButtonFinalScale;

        _settingsButtonEntranceRoutine = null;
    }

    private void StopSettingsButtonEntrance(bool restoreFinalScale)
    {
        if (_settingsButtonEntranceRoutine != null)
        {
            StopCoroutine(_settingsButtonEntranceRoutine);
            _settingsButtonEntranceRoutine = null;
        }

        if (restoreFinalScale && settingsButton != null &&
            _settingsButtonScaleCached)
        {
            settingsButton.localScale = _settingsButtonFinalScale;
        }
    }

    public void HideSettingsButtonForWin()
    {
        if (settingsButton == null)
        {
            Transform button = FindChildRecursive(transform, "Btn_setting");
            settingsButton = button as RectTransform;
        }

        if (settingsButton == null)
            return;

        StopSettingsButtonEntrance(false);
        settingsButton.localScale = Vector3.zero;
    }

    private void HandleInitialBoardDealCompleted()
    {
        if (_initialBoardDealAnimator != null)
        {
            _initialBoardDealAnimator.DealCompleted -=
                HandleInitialBoardDealCompleted;
        }

        if (!isActiveAndEnabled ||
            PopupController.Instance == null ||
            GameManager.Instance == null ||
            GameManager.Instance.gameState != GameState.PlayingGame)
        {
            return;
        }

        EvaluateTrayFullWarning();
    }

    private void RestoreBottomEntranceFinalPositions()
    {
        if (_bottomEntranceTab != null)
        {
            _bottomEntranceTab.localPosition =
                _bottomEntranceTabFinalPosition;
        }

        if (_bottomEntranceDeck != null)
        {
            _bottomEntranceDeck.localPosition =
                _bottomEntranceDeckFinalPosition;
        }

        int boosterCount = Mathf.Min(
            _bottomEntranceBoosters.Count,
            _bottomEntranceBoosterFinalPositions.Count
        );

        for (int i = 0; i < boosterCount; i++)
        {
            if (_bottomEntranceBoosters[i] != null)
            {
                _bottomEntranceBoosters[i].anchoredPosition =
                    _bottomEntranceBoosterFinalPositions[i];
            }
        }
    }

    private void ConfigureBottomTabSorting()
    {
        if (_bottomEntranceTab == null)
            return;

        SpriteRenderer bottomRenderer =
            _bottomEntranceTab.GetComponent<SpriteRenderer>();

        if (bottomRenderer == null)
            return;

        int targetOrder = bottomTabMinimumSortingOrder;

        if (_bottomEntranceDeck == null)
        {
            bottomRenderer.sortingOrder = targetOrder;
            return;
        }

        SpriteRenderer[] deckRenderers =
            _bottomEntranceDeck.GetComponentsInChildren<SpriteRenderer>(true);

        if (deckRenderers.Length == 0)
        {
            bottomRenderer.sortingOrder = targetOrder;
            return;
        }

        SpriteRenderer deckRenderer =
            _bottomEntranceDeck.GetComponent<SpriteRenderer>();

        if (deckRenderer == null)
            deckRenderer = deckRenderers[0];

        bottomRenderer.sortingLayerID = deckRenderer.sortingLayerID;
        bottomRenderer.sortingOrder = targetOrder;

        int minimumDeckOrder = int.MaxValue;
        for (int i = 0; i < deckRenderers.Length; i++)
        {
            SpriteRenderer renderer = deckRenderers[i];
            if (renderer == null ||
                renderer.sortingLayerID != bottomRenderer.sortingLayerID)
                continue;

            minimumDeckOrder = Mathf.Min(
                minimumDeckOrder,
                renderer.sortingOrder
            );
        }

        if (minimumDeckOrder > targetOrder)
            return;

        int deckSortingOffset = targetOrder + 1 - minimumDeckOrder;
        for (int i = 0; i < deckRenderers.Length; i++)
        {
            SpriteRenderer renderer = deckRenderers[i];
            if (renderer != null &&
                renderer.sortingLayerID == bottomRenderer.sortingLayerID)
            {
                renderer.sortingOrder += deckSortingOffset;
            }
        }
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(
                root.GetChild(i),
                childName
            );

            if (result != null)
                return result;
        }

        return null;
    }

    private void SetupTargets()
    {
        // LeanPoolClear() da tra het target cu ve pool.
        // Khong duoc Destroy() chung nua, neu khong pool se giu clone da bi huy
        // va lan Spawn sau se warning "pool contained a null despawned clone".
        if (targetGroup != null) targetGroup.LeanPoolClear();

        _targetItems.Clear();

        if (_currentLevel == null || _currentLevel.CardTargets == null || targetGroup == null || targetItemPrefab == null)
            return;

        foreach (var target in _currentLevel.CardTargets)
        {
            var item = LeanPool.Spawn(targetItemPrefab, targetGroup);
            if (item == null) continue;

            item.SetDisplayScale(targetItemScale);

            Sprite targetSprite = _currentLevel.TargetConfig != null
                ? _currentLevel.TargetConfig.GetTargetSprite(target.cardType)
                : null;

            if (targetSprite == null)
            {
                Debug.LogWarning(
                    $"[PopupInGame] Khong tim thay target sprite cho {target.cardType} " +
                    $"trong TargetConfig cua {_currentLevel.name}.");
            }

            item.Setup(target, targetSprite);
            _targetItems.Add(item);
        }

        UpdateTargetsUI();
    }

    private void UpdateTargetsUI()
    {
        if (_currentLevel == null) return;
        
        var currentCounts = _currentLevel.GetCurrentCardCounts();
        foreach (var item in _targetItems)
        {
            if (item != null)
            {
                item.UpdateProgress(currentCounts);
            }
        }
    }

    private void FoodBoxCompleted(Vector3 position)
    {
        StartCoroutine(
            SpawnStarsWithDelay(position)
        );
    }

    private IEnumerator SpawnStarsWithDelay(
        Vector3 position)
    {
        for (int i = 0; i < 2; i++)
        {
            PopupController.Instance.StartCoroutine(
                SpawnAndFlyStar(position)
            );

            yield return new WaitForSeconds(0.05f);
        }
    }

    private IEnumerator SpawnAndFlyStar(
        Vector3 position)
    {
        if (starPrefab == null ||
            starTarget == null)
        {
            yield break;
        }

        if (PopupController.Instance == null)
        {
            yield break;
        }

        Canvas canvas =
            PopupController.Instance
                .CanvasTransform
                .GetComponent<Canvas>();

        if (canvas == null)
        {
            yield break;
        }

        Camera cam =
            canvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

        if (cam == null)
        {
            cam = Camera.main;
        }

        Camera worldCamera = Camera.main;

        if (worldCamera == null)
        {
            yield break;
        }

        Vector3 screenPos =
            worldCamera.WorldToScreenPoint(position);

        RectTransform canvasRect =
            canvas.GetComponent<RectTransform>();

        Vector3 spawnWorldPos = position;

        if (RectTransformUtility
            .ScreenPointToWorldPointInRectangle(
                canvasRect,
                screenPos,
                cam,
                out Vector3 worldPoint))
        {
            spawnWorldPos = worldPoint;
        }

        Transform canvasRoot =
            PopupController.Instance.CanvasTransform;

        GameObject star =
            LeanPool.Spawn(
                starPrefab,
                canvasRoot
            );

        star.transform.position =
            spawnWorldPos;

        Vector3 startPos =
            star.transform.position;

        Vector3 targetPos =
            starTarget.transform.position;

        Vector3 midPoint =
            (startPos + targetPos) / 2f;

        Vector3 direction =
            (targetPos - startPos).normalized;

        Vector3 perpendicular =
            new Vector3(
                -direction.y,
                direction.x,
                0f
            );

        float distance =
            Vector3.Distance(
                startPos,
                targetPos
            );

        float offsetLen =
            Random.Range(
                distance * 0.05f,
                distance * 0.15f
            );

        Vector3 controlPoint =
            midPoint +
            perpendicular * -offsetLen;

        float duration =
            Random.Range(
                0.5f,
                0.7f
            );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );

            Vector3 m1 =
                Vector3.Lerp(
                    startPos,
                    controlPoint,
                    t
                );

            Vector3 m2 =
                Vector3.Lerp(
                    controlPoint,
                    targetPos,
                    t
                );

            star.transform.position =
                Vector3.Lerp(
                    m1,
                    m2,
                    t
                );

            yield return null;
        }

        star.transform.position =
            targetPos;

        Tween.PunchScale(
            starTarget.transform,
            Vector3.one,
            0.2f,
            1
        );

        CurrentStar++;

        LeanPool.Despawn(star);
    }

    // =========================================================
    // PAUSE MENU
    // =========================================================

    public void OnClickSetting()
    {
        if (_initialBoardDealAnimator != null && _initialBoardDealAnimator.IsBusy)
            return;

        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        if (pauseMenu != null)
        {
            pauseMenu.Toggle();
        }
        else
        {
            Debug.LogWarning(
                "InGamePauseMenu chưa được gán vào PopupInGame!"
            );
        }
    }

    // =========================================================
    // DEBUG / GAME CONTROL
    // =========================================================

    public void OnClickReplay()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        // Đảm bảo không bị giữ TimeScale = 0
        Time.timeScale = 1f;

        GameManager.Instance.ReplayGame();
    }

    public void OnClickPrevious()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        Time.timeScale = 1f;

        GameManager.Instance.BackLevel();
    }

    public void OnClickSkip()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        Time.timeScale = 1f;

        GameManager.Instance.NextLevel();
    }

    public void OnClickLose()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        Time.timeScale = 1f;

        GameManager.Instance.ShowContinueWarning();
    }

    public void OnClickWin()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        Time.timeScale = 1f;

        GameManager.Instance.OnWinGame(1f);
    }
}

public class MergeCoinTrailGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        float halfWidth = rect.width * 0.5f;

        UIVertex headLeft = UIVertex.simpleVert;
        headLeft.color = color;
        headLeft.position = new Vector3(-halfWidth, rect.yMax, 0f);

        UIVertex headRight = UIVertex.simpleVert;
        headRight.color = color;
        headRight.position = new Vector3(halfWidth, rect.yMax, 0f);

        UIVertex tail = UIVertex.simpleVert;
        tail.color = color;
        tail.position = new Vector3(0f, rect.yMin, 0f);

        vertexHelper.AddVert(headLeft);
        vertexHelper.AddVert(headRight);
        vertexHelper.AddVert(tail);
        vertexHelper.AddTriangle(0, 1, 2);
    }
}
