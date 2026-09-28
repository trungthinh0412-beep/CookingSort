using System;
using System.Collections.Generic;
using CustomTween;
using Lean.Pool;
using TMPro;
using UnityEngine;

/// <summary>
/// Handles the Build resource shown on Home. Build currently uses the
/// player's Star balance and receives its own UI/resource references.
/// </summary>
public class BuildHandler : ResourceHandler
{
    [Header("Build Settings")]
    [SerializeField] private Resource buildPrefab;
    [SerializeField] private GameObject buildTarget;
    [SerializeField] private TextMeshProUGUI buildText;
    [SerializeField] private GameObject buildBar;

    private readonly List<Resource> activeBuildResources = new List<Resource>();

    public Transform BuildTargetTransform => buildTarget != null ? buildTarget.transform : null;

    protected override Resource Prefab => buildPrefab;
    protected override GameObject Target => buildTarget;
    protected override TextMeshProUGUI AmountText => buildText;
    protected override GameObject Bar => buildBar;

    protected override int Cache { get; set; }
    protected override int CurrentValue => Data.PlayerData.CurrentStar;

    protected override void Awake()
    {
        base.Awake();

        // The Home instance can work immediately after replacing a copied
        // GoldHandler. Explicit Inspector assignments still take priority.
        if (buildTarget == null)
            buildTarget = transform.Find("Icon")?.gameObject;
        if (buildText == null)
            buildText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (buildBar == null)
            buildBar = gameObject;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ResetCache();
    }

    public Tween PlayBuildResourceTo(
        Transform target,
        float duration,
        Ease ease,
        float arc,
        Action onComplete
    )
    {
        if (target == null || buildTarget == null || buildPrefab == null ||
            PopupController.Instance == null)
        {
            onComplete?.Invoke();
            return default;
        }

        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Transform resourceParent = targetCanvas != null
            ? targetCanvas.transform
            : PopupController.Instance.CanvasTransform;

        if (resourceParent == null)
        {
            onComplete?.Invoke();
            return default;
        }

        Resource resource = LeanPool.Spawn(
            buildPrefab,
            resourceParent
        );

        activeBuildResources.Add(resource);

        Vector3 startPosition = buildTarget.transform.position;
        Vector3 endPosition = target.position;
        float canvasPlaneDistance = targetCanvas != null
            ? targetCanvas.planeDistance
            : canvas == null ? endPosition.z : canvas.planeDistance;
        startPosition.z = canvasPlaneDistance;
        endPosition.z = canvasPlaneDistance;

        resource.transform.localScale = Vector3.one * scale;
        resource.transform.position = startPosition;

        Animator animator = resource.GetComponent<Animator>();
        if (animator != null)
            animator.enabled = true;

        Vector3 midpoint = (startPosition + endPosition) * .5f;
        Vector3 direction = (endPosition - startPosition).normalized;
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f);
        Vector3 controlPoint = midpoint + perpendicular * arc;

        float safeDuration = Mathf.Max(.01f, duration);

        return Tween.Custom<Transform>(resource.transform, 0f, 1f, safeDuration,
                (transform, value) =>
                {
                    float inverse = 1f - value;
                    transform.position =
                        inverse * inverse * startPosition +
                        2f * inverse * value * controlPoint +
                        value * value * endPosition;
                },
                ease,
                useUnscaledTime: true
            )
            .OnComplete(() =>
            {
                activeBuildResources.Remove(resource);
                LeanPool.Despawn(resource);
                onComplete?.Invoke();
            });
    }

    public void StopBuildResourceAnimations()
    {
        for (int i = activeBuildResources.Count - 1; i >= 0; i--)
        {
            Resource resource = activeBuildResources[i];

            if (resource != null)
                LeanPool.Despawn(resource);
        }

        activeBuildResources.Clear();
    }

    protected override void SubscribeEvents()
    {
        Observer.StarChanged += OnBuildChanged;
    }

    protected override void UnsubscribeEvents()
    {
        Observer.StarChanged -= OnBuildChanged;
    }

    protected override void OnDisable()
    {
        StopBuildResourceAnimations();
        base.OnDisable();
    }

    private void OnBuildChanged(int amount)
    {
        // The build UI is usable before a flying resource prefab is assigned.
        // Assign Build Prefab and Build Target in the Inspector to enable the
        // same flying-resource animation as Gold and Heart.
        if (buildPrefab == null || buildTarget == null)
        {
            ResetCache();
            return;
        }

        if (amount > 0)
            Increase(amount);
        else if (amount < 0)
            Decrease(-amount);
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
    }
}
