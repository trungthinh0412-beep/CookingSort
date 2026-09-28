using CustomTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ButtonStarChest : ResourceHandler
{
    [Header("Star Chest Setting")]
    [SerializeField] private Image progressFill;
    [SerializeField] private StarChestConfig starChestConfig;
    [SerializeField] private GameObject shufferBoosterPrefab;
    [SerializeField] private GameObject bombBoosterPrefab;
    [SerializeField] private GameObject playButton;
    [SerializeField] private float offsetXPosFlyBooster = 1;
    [SerializeField] private float bezierCurveBooster = 1.2f;

    [Header("Star Settings")]
    [SerializeField] private Resource starPrefab;
    [SerializeField] private GameObject starTarget;
    [SerializeField] private TextMeshProUGUI starText;
    [SerializeField] private GameObject starBar;

    protected override Resource Prefab => starPrefab;
    protected override GameObject Target => starTarget;
    protected override TextMeshProUGUI AmountText => starText;
    protected override GameObject Bar => starBar;

    protected override int Cache { get; set; }

    protected override int CurrentValue => Data.PlayerData.CurrentStar;
    private int targetOpen => starChestConfig.targetStar;
    private GameObject _bomb;
    private GameObject _suffer;



    protected override void OnEnable()
    {
        base.OnEnable();
        Setup();
    }
    public override void ResetCache()
    {
        Cache = CurrentValue;
        AmountText.text = $"{Cache}/{targetOpen}";
        progressFill.fillAmount = (float)Data.PlayerData.CurrentStar / targetOpen;
    }

    private void Setup()
    {
        starText.text = $"{CurrentValue}/{targetOpen}";
    }

    protected override void SubscribeEvents()
    {
        Observer.StarChanged += OnStarChanged;
    }

    protected override void UnsubscribeEvents()
    {
        Observer.StarChanged -= OnStarChanged;
    }

    private void OnStarChanged(int amount)
    {
        if (amount > 0) Increase(amount);
        else Decrease(-amount);
    }

    protected override void OnCollectedEffect(GameObject target)
    {
        SoundController.Instance.PlayFX(SoundName.GoldCollect);
        VFXController.Instance.SpawnEffect(
            EffectName.SparkleGold,
            Vector3.zero,
            target.transform,
            0.5f
        );
        // update progress fill here to avoid change logic resource handle
        starText.text = $"{CurrentValue}/{targetOpen}";
        progressFill.fillAmount = (float)Data.PlayerData.CurrentStar / targetOpen;
        if (Data.PlayerData.CurrentStar >= targetOpen)
        {
            Data.PlayerData.CurrentStar -= targetOpen;
            var popupClaimStarChest = PopupController.Instance.Get<PopupClaimStarChest>();
            popupClaimStarChest.AfterHiddenAction = SpawnFlyBooster;
            PopupController.Instance.Show<PopupClaimStarChest>();
            Data.PlayerData.CurrentShuffle++;
            Data.PlayerData.CurrentBomb++;
        }
    }
    void SpawnFlyBooster()
    {
        if (_bomb == null) _bomb = Instantiate(bombBoosterPrefab, PopupController.Instance.CanvasTransform);
        FlyBooster(_bomb.transform, false, bezierCurveBooster);
        if (_suffer == null) _suffer = Instantiate(shufferBoosterPrefab, PopupController.Instance.CanvasTransform);
        FlyBooster(_suffer.transform, true, -bezierCurveBooster);
    }
    private Tween MoveBezierToTarget(GameObject obj, GameObject target, float bezierCurve, float startDelay = 0f)
    {
        Vector3 p0 = obj.transform.position;
        Vector3 p2 = target.transform.position;

        // Control point: midpoint + small perpendicular offset
        Vector3 mid = (p0 + p2) * 0.5f;
        Vector3 dir = (p2 - p0).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        // Alternate curve direction per resource for variety
        float side = 1f;
        Vector3 p1 = mid + perp * (bezierCurve * side);

        return Tween.Custom<Transform>(obj.transform, 0f, 1f, durationTarget,
            (t, v) =>
            {
                float u = 1f - v;
                t.position = u * u * p0 + 2f * u * v * p1 + v * v * p2;
            }, easeTarget, startDelay: startDelay);
    }
    public void FlyBooster(Transform target, bool isOffsetRight, float bezierCurve)
    {
        target.gameObject.SetActive(true);
        target.GetComponent<RectTransform>().anchoredPosition = Vector3.zero + new Vector3(isOffsetRight ? offsetXPosFlyBooster : -offsetXPosFlyBooster, 0, 0);
        Tween.Scale(target.transform, 2f, 1f).OnComplete(() =>
        {
            Tween.Scale(target.transform, 1f, 1f).OnComplete(() =>
            {
                MoveBezierToTarget(target.gameObject, playButton, bezierCurve).OnComplete(() =>
                {
                    target.gameObject.SetActive(false);
                    SoundController.Instance.PlayFX(SoundName.GoldCollect);
                });
            });
        });
    }
}
