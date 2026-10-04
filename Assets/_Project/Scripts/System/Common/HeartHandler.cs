using TMPro;
using UnityEngine;

public class HeartHandler : ResourceHandler
{
    [Header("Heart Settings")]
    [SerializeField] private Resource heartPrefab;
    [SerializeField] private GameObject heartTarget;
    [SerializeField] private TextMeshProUGUI heartText;
    [SerializeField] private GameObject heartBar;
    [SerializeField] private TextMeshProUGUI timeRemainText;

    private int _lastRemainingSeconds = int.MinValue;
    private int _lastHeartAmount = int.MinValue;
    private bool _lastInfiniteHeart;

    protected override Resource Prefab => heartPrefab;
    protected override GameObject Target => heartTarget;
    protected override TextMeshProUGUI AmountText => heartText;
    protected override GameObject Bar => heartBar;

    protected override int Cache { get; set; }

    protected override int CurrentValue => Data.PlayerData.CurrentHeart;

    protected override void Awake()
    {
        base.Awake();
        RefreshHeartDisplay();
    }

    protected override void SubscribeEvents()
    {
        Observer.HeartChanged += OnHeartChanged;
    }

    protected override void UnsubscribeEvents()
    {
        Observer.HeartChanged -= OnHeartChanged;
    }

    private void OnHeartChanged(int amount)
    {
        if (amount > 0) Increase(amount);
        else Decrease(-amount);
    }

    protected override void OnCollectedEffect(GameObject target)
    {
        VFXController.Instance.SpawnEffect(
            EffectName.SparkleHeart,
            Vector3.zero,
            target.transform,
            0.5f
        );
    }

    private void Update()
    {
        RefreshHeartDisplay();
    }

    private void RefreshHeartDisplay()
    {
        HeartController controller = HeartController.Instance;
        if (timeRemainText != null && controller != null)
        {
            int remainingSeconds = controller.GetRemainingDisplaySeconds();
            if (remainingSeconds != _lastRemainingSeconds)
            {
                timeRemainText.text = controller.GetRemainingTime();
                _lastRemainingSeconds = remainingSeconds;
            }
        }

        if (heartText == null || Data.PlayerData == null)
            return;

        int currentHeart = Data.PlayerData.CurrentHeart;
        bool infiniteHeart = Data.PlayerData.IsInfiniteHeart();
        if (currentHeart == _lastHeartAmount &&
            infiniteHeart == _lastInfiniteHeart)
        {
            return;
        }

        if (infiniteHeart)
        {
            heartText.text = "∞";
        }
        else
        {
            heartText.text = currentHeart.ToString();
        }

        _lastHeartAmount = currentHeart;
        _lastInfiniteHeart = infiniteHeart;
    }
    public void OnClickShop()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        PopupController.Instance.Show<PopupMoreLife>(PopupAnimation.None);
    }
}
