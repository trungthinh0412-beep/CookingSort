using TMPro;
using UnityEngine;

public class GoldHandler : ResourceHandler
{
    private static int _suppressIncreaseAnimationDepth;
    private int _activeCoinFxSequences;
    private float _nextCollectSoundTime;

    [Header("Gold Settings")]
    [SerializeField] private Resource goldPrefab;
    [SerializeField] private GameObject goldTarget;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private GameObject goldBar;

    [Header("Coin Collect FX")]
    [SerializeField] private CoinFlyFXTemplate coinFlyFxTemplate;
    [SerializeField, Range(1, 8)] private int minimumVisualCoinCount = 2;
    [SerializeField, Range(1, 8)] private int maximumVisualCoinCount = 8;
    [SerializeField, Min(0.01f)] private float collectSoundInterval = 0.07f;

    protected override Resource Prefab => goldPrefab;
    protected override GameObject Target => goldTarget;
    protected override TextMeshProUGUI AmountText => goldText;
    protected override GameObject Bar => goldBar;

    protected override int Cache { get; set; }

    protected override int CurrentValue => Data.PlayerData.CurrentGold;

    public RectTransform CoinFlyTarget => goldTarget != null
        ? goldTarget.transform as RectTransform
        : null;

    protected override void OnEnable()
    {
        base.OnEnable();
        Setup();
    }

    private void Setup()
    {
        // A popup can be disabled while another screen changes the balance.
        // Reset both the visible text and the animation cache when it returns.
        ResetCache();
    }

    protected override void SubscribeEvents()
    {
        Observer.GoldChanged += OnGoldChanged;
    }

    protected override void UnsubscribeEvents()
    {
        Observer.GoldChanged -= OnGoldChanged;
    }

    private void OnGoldChanged(int amount)
    {
        if (amount > 0 && _suppressIncreaseAnimationDepth > 0)
        {
            ResetCache();
            return;
        }

        if (amount > 0) PlayCoinIncrease(amount);
        else Decrease(-amount);
    }

    private void PlayCoinIncrease(int amount)
    {
        PlayCoinIncrease(amount, null, null, false);
    }

    private void PlayCoinIncrease(
        int amount,
        Vector3? sourceOverride,
        Camera sourceCameraOverride,
        bool rewindDisplayedAmount)
    {
        RectTransform target = CoinFlyTarget;
        RectTransform canvasRect = canvas != null
            ? canvas.transform as RectTransform
            : null;
        if (amount <= 0 || target == null || canvasRect == null ||
            coinFlyFxTemplate == null)
        {
            ResetCache();
            return;
        }

        int minimumCount = Mathf.Clamp(minimumVisualCoinCount, 1, 8);
        int maximumCount = Mathf.Clamp(
            maximumVisualCoinCount,
            minimumCount,
            8
        );
        int visualCoinCount = Mathf.Clamp(amount, minimumCount, maximumCount);

        if (rewindDisplayedAmount)
        {
            Cache = Mathf.Max(0, CurrentValue - amount);
            if (AmountText != null)
                AmountText.text = Cache.ToString();
        }

        Vector3 sourceWorldPosition = sourceOverride ?? from ??
            canvasRect.TransformPoint(canvasRect.rect.center);
        Camera sourceCamera = sourceOverride.HasValue
            ? sourceCameraOverride
            : canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        CoinFlyFX.Options options = CoinFlyFX.Options.Default;
        options.minimumCount = minimumCount;
        options.maximumCount = maximumCount;

        _activeCoinFxSequences++;
        CoinFlyFX.PlayFromWorld(
            sourceWorldPosition,
            sourceCamera,
            target,
            amount,
            visualCoinCount,
            canvasRect,
            OnCoinFxArrived,
            OnCoinFxCompleted,
            addToWallet: false,
            template: coinFlyFxTemplate,
            options: options
        );
    }

    public static bool PlayCommittedRewardFromWorld(
        Vector3 sourceWorldPosition,
        Camera sourceCamera,
        int rewardAmount)
    {
        if (rewardAmount <= 0)
            return false;

        GoldHandler[] handlers = FindObjectsByType<GoldHandler>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
        for (int i = 0; i < handlers.Length; i++)
        {
            GoldHandler handler = handlers[i];
            if (handler == null || !handler.isActiveAndEnabled ||
                handler.CoinFlyTarget == null ||
                !handler.CoinFlyTarget.gameObject.activeInHierarchy)
            {
                continue;
            }

            handler.PlayCoinIncrease(
                rewardAmount,
                sourceWorldPosition,
                sourceCamera,
                true
            );
            return true;
        }

        return false;
    }

    public static bool PlayCommittedRewardFromUI(
        RectTransform source,
        int rewardAmount)
    {
        if (source == null || rewardAmount <= 0)
            return false;

        GoldHandler[] handlers = FindObjectsByType<GoldHandler>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        GoldHandler selectedHandler = null;
        int selectedScore = int.MinValue;

        for (int i = 0; i < handlers.Length; i++)
        {
            GoldHandler handler = handlers[i];
            if (handler == null || !handler.isActiveAndEnabled ||
                handler.CoinFlyTarget == null ||
                !handler.CoinFlyTarget.gameObject.activeInHierarchy)
            {
                continue;
            }

            Canvas targetCanvas = handler.CoinFlyTarget
                .GetComponentInParent<Canvas>();
            int score = targetCanvas != null
                ? targetCanvas.sortingOrder
                : 0;
            if (sourceCanvas != null && targetCanvas == sourceCanvas)
                score += 10000;

            if (selectedHandler == null || score > selectedScore)
            {
                selectedHandler = handler;
                selectedScore = score;
            }
        }

        if (selectedHandler == null)
            return false;

        selectedHandler.PlayCoinIncreaseFromUI(source, rewardAmount);
        return true;
    }

    private void PlayCoinIncreaseFromUI(
        RectTransform source,
        int rewardAmount)
    {
        RectTransform target = CoinFlyTarget;
        RectTransform canvasRect = CoinFlyFX.GetOrCreateOverlayRoot(source);
        if (source == null || rewardAmount <= 0 || target == null ||
            canvasRect == null || coinFlyFxTemplate == null)
        {
            ResetCache();
            return;
        }

        int minimumCount = Mathf.Clamp(minimumVisualCoinCount, 1, 8);
        int maximumCount = Mathf.Clamp(
            maximumVisualCoinCount,
            minimumCount,
            8
        );
        int visualCoinCount = Mathf.Clamp(
            rewardAmount,
            minimumCount,
            maximumCount
        );

        Cache = Mathf.Max(0, CurrentValue - rewardAmount);
        if (AmountText != null)
            AmountText.text = Cache.ToString();

        CoinFlyFX.Options options = CoinFlyFX.Options.Default;
        options.minimumCount = minimumCount;
        options.maximumCount = maximumCount;

        _activeCoinFxSequences++;
        CoinFlyFX.PlayFromUI(
            source,
            target,
            rewardAmount,
            visualCoinCount,
            canvasRect,
            OnCoinFxArrived,
            OnCoinFxCompleted,
            addToWallet: false,
            template: coinFlyFxTemplate,
            options: options
        );
    }

    private void OnCoinFxArrived(int amount)
    {
        Cache += Mathf.Max(0, amount);
        if (AmountText != null)
            AmountText.text = Cache.ToString();

        OnCollectedEffect(Target);
    }

    private void OnCoinFxCompleted()
    {
        _activeCoinFxSequences = Mathf.Max(0, _activeCoinFxSequences - 1);
        if (_activeCoinFxSequences == 0)
            from = null;
    }

    public static void AddWithoutResourceAnimation(int amount)
    {
        if (amount <= 0 || Data.PlayerData == null)
            return;

        _suppressIncreaseAnimationDepth++;
        try
        {
            Data.PlayerData.CurrentGold += amount;
        }
        finally
        {
            _suppressIncreaseAnimationDepth = Mathf.Max(
                0,
                _suppressIncreaseAnimationDepth - 1
            );
        }
    }

    public void OnClickShop()
    {
        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        PopupController popupController = PopupController.Instance;
        if (popupController == null)
            return;

        // Gold can be tapped while another main page is still active. Keep
        // that page from covering Shop, then restore Shop's visual state.
        popupController.HideAllExcept<PopupShop>();
        popupController.Show<PopupShop>(PopupAnimation.None);

        PopupShop popupShop = popupController.Get<PopupShop>() as PopupShop;
        if (popupShop == null)
            return;

        popupShop.Show(PopupAnimation.None);
        popupShop.ShowGoldPackages();
    }
    
    protected override void OnCollectedEffect(GameObject target)
    {
        if (target == null)
            return;

        if (SoundController.Instance != null &&
            Time.unscaledTime >= _nextCollectSoundTime)
        {
            _nextCollectSoundTime = Time.unscaledTime + collectSoundInterval;
            SoundController.Instance.PlayFX(SoundName.GoldCollect);
        }

        if (VFXController.Instance != null)
        {
            VFXController.Instance.SpawnEffect(
                EffectName.SparkleGold,
                Vector3.zero,
                target.transform,
                0.5f
            );
        }
    }
}
