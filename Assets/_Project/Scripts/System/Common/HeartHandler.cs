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

    protected override Resource Prefab => heartPrefab;
    protected override GameObject Target => heartTarget;
    protected override TextMeshProUGUI AmountText => heartText;
    protected override GameObject Bar => heartBar;

    protected override int Cache { get; set; }

    protected override int CurrentValue => Data.PlayerData.CurrentHeart;

    protected override void Awake()
    {
        base.Awake();
        timeRemainText.text = HeartController.Instance.GetRemainingTime();
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
        timeRemainText.text = HeartController.Instance.GetRemainingTime();
        if (Data.PlayerData.IsInfiniteHeart())
        {
            heartText.text = "∞";
        }
        else
        {
            heartText.text = Data.PlayerData.CurrentHeart.ToString();
        }
    }
    public void OnClickShop()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        PopupController.Instance.Show<PopupMoreLife>(PopupAnimation.None);
    }
}
