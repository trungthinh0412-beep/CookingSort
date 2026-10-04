using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// Shared coin collect presentation. Call PlayFromUI or PlayFromWorld from
/// any popup; the service resolves the project's existing CoinFly template
/// and runs independently from the popup that requested it.
/// </summary>
public sealed class CoinFlyFX : MonoBehaviour
{
    [Serializable]
    public struct Options
    {
        public float anticipationDuration;
        public int minimumCount;
        public int maximumCount;
        public float spawnRadiusX;
        public float spawnRadiusY;
        public float burstDistanceMin;
        public float burstDistanceMax;
        public float burstDurationMin;
        public float burstDurationMax;
        public float hangDurationMin;
        public float hangDurationMax;
        public float flightDurationMin;
        public float flightDurationMax;
        public float staggerMin;
        public float staggerMax;
        public float curveStrengthMin;
        public float curveStrengthMax;
        public float spawnScale;
        public float overshootScale;
        public float flightEndScale;
        public float depthScaleMin;
        public float depthScaleMax;
        public float pullStretchChance;
        public float pullStretchStrength;
        public float startRotation;
        public float rotationDrift;
        public float coinSize;
        public int trailCount;
        public float trailFollowSpeed;
        public float trailHeadWidth;
        public float trailLength;
        public float trailAlpha;
        public float targetPulseScale;
        public float targetPulseUndershoot;
        public float targetPulseDuration;
        public float finalImpactMultiplier;

        public static Options Default => new Options
        {
            anticipationDuration = 0.06f,
            minimumCount = 2,
            maximumCount = 8,
            spawnRadiusX = 45f,
            spawnRadiusY = 30f,
            burstDistanceMin = 55f,
            burstDistanceMax = 105f,
            burstDurationMin = 0.14f,
            burstDurationMax = 0.22f,
            hangDurationMin = 0.05f,
            hangDurationMax = 0.12f,
            flightDurationMin = 0.30f,
            flightDurationMax = 0.42f,
            staggerMin = 0.03f,
            staggerMax = 0.045f,
            curveStrengthMin = 40f,
            curveStrengthMax = 110f,
            spawnScale = 0.35f,
            overshootScale = 1.18f,
            flightEndScale = 0.28f,
            depthScaleMin = 0.9f,
            depthScaleMax = 1.3f,
            pullStretchChance = 0.45f,
            pullStretchStrength = 0.18f,
            startRotation = 15f,
            rotationDrift = 8f,
            coinSize = 82f,
            trailCount = 0,
            trailFollowSpeed = 18f,
            trailHeadWidth = 64f,
            trailLength = 180f,
            trailAlpha = 0.6f,
            targetPulseScale = 1.13f,
            targetPulseUndershoot = 0.97f,
            targetPulseDuration = 0.12f,
            finalImpactMultiplier = 1.12f
        };
    }

    private sealed class TrailVisual
    {
        public GameObject gameObject;
        public RectTransform transform;
        public MergeCoinTrailGraphic graphic;
        public Vector2 position;
    }

    private sealed class CoinVisual
    {
        public GameObject gameObject;
        public RectTransform transform;
        public Image image;
        public Animator animator;
        public int poolKey;
        public readonly List<TrailVisual> trails = new List<TrailVisual>();
    }

    private sealed class Sequence
    {
        public int remaining;
        public Action complete;
    }

    private static CoinFlyFX _instance;
    private static RectTransform _overlayRoot;
    private readonly Dictionary<int, Stack<CoinVisual>> _visualPools =
        new Dictionary<int, Stack<CoinVisual>>();
    private readonly Dictionary<RectTransform, Coroutine> _targetPulses =
        new Dictionary<RectTransform, Coroutine>();
    private readonly Dictionary<RectTransform, Vector3> _targetBaseScales =
        new Dictionary<RectTransform, Vector3>();
    private readonly Vector3[] _corners = new Vector3[4];

    public static bool PlayFromUI(
        RectTransform source,
        int rewardAmount,
        int visualCoinCount = 12)
    {
        return PlayFromUI(
            source,
            FindActiveGoldTarget(),
            rewardAmount,
            visualCoinCount
        );
    }

    public static RectTransform GetOrCreateOverlayRoot(
        RectTransform reference,
        int sortingOrder = 950)
    {
        if (_overlayRoot != null)
        {
            Canvas cachedCanvas = _overlayRoot.GetComponent<Canvas>();
            if (cachedCanvas != null)
                cachedCanvas.sortingOrder = sortingOrder;
            _overlayRoot.SetAsLastSibling();
            return _overlayRoot;
        }

        Canvas referenceCanvas = reference != null
            ? reference.GetComponentInParent<Canvas>()
            : null;
        Canvas rootCanvas = referenceCanvas != null
            ? referenceCanvas.rootCanvas
            : null;
        if (rootCanvas == null)
            return null;

        GameObject overlay = new GameObject(
            "[CoinFlyFX Overlay]",
            typeof(RectTransform),
            typeof(Canvas)
        );
        overlay.layer = rootCanvas.gameObject.layer;

        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.SetParent(rootCanvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();

        Canvas overlayCanvas = overlay.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingLayerID = rootCanvas.sortingLayerID;
        overlayCanvas.sortingOrder = sortingOrder;

        _overlayRoot = rect;
        return _overlayRoot;
    }

    public static bool PlayFromWorld(
        Vector3 sourceWorldPosition,
        Camera sourceCamera,
        int rewardAmount,
        int visualCoinCount = 12)
    {
        return PlayFromWorld(
            sourceWorldPosition,
            sourceCamera,
            FindActiveGoldTarget(),
            rewardAmount,
            visualCoinCount
        );
    }

    public static bool PlayFromUI(
        RectTransform source,
        RectTransform target,
        int rewardAmount,
        int visualCoinCount = 12,
        RectTransform flyRoot = null,
        Action<int> onCoinArrived = null,
        Action onComplete = null,
        bool addToWallet = true,
        CoinFlyFXTemplate template = null,
        Options? options = null)
    {
        if (source == null || target == null)
        {
            CompleteWithoutVisual(
                rewardAmount,
                addToWallet,
                onCoinArrived,
                onComplete
            );
            return false;
        }

        RectTransform safeRoot = flyRoot != null
            ? flyRoot
            : FindFlyRoot(target);
        if (safeRoot == null ||
            !TryConvertUiCenter(source, safeRoot, out Vector2 startPosition))
        {
            CompleteWithoutVisual(
                rewardAmount,
                addToWallet,
                onCoinArrived,
                onComplete
            );
            return false;
        }

        return PlayInternal(
            safeRoot,
            target,
            startPosition,
            rewardAmount,
            visualCoinCount,
            onCoinArrived,
            onComplete,
            addToWallet,
            template,
            options
        );
    }

    public static bool PlayFromWorld(
        Vector3 sourceWorldPosition,
        Camera sourceCamera,
        RectTransform target,
        int rewardAmount,
        int visualCoinCount = 12,
        RectTransform flyRoot = null,
        Action<int> onCoinArrived = null,
        Action onComplete = null,
        bool addToWallet = true,
        CoinFlyFXTemplate template = null,
        Options? options = null)
    {
        if (target == null)
        {
            CompleteWithoutVisual(
                rewardAmount,
                addToWallet,
                onCoinArrived,
                onComplete
            );
            return false;
        }

        RectTransform safeRoot = flyRoot != null
            ? flyRoot
            : FindFlyRoot(target);
        sourceCamera ??= Camera.main;
        Vector3 screenPosition = sourceCamera != null
            ? sourceCamera.WorldToScreenPoint(sourceWorldPosition)
            : sourceWorldPosition;
        if (safeRoot == null ||
            (sourceCamera != null && screenPosition.z < 0f) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                safeRoot,
                screenPosition,
                GetUiCamera(safeRoot),
                out Vector2 startPosition))
        {
            CompleteWithoutVisual(
                rewardAmount,
                addToWallet,
                onCoinArrived,
                onComplete
            );
            return false;
        }

        return PlayInternal(
            safeRoot,
            target,
            startPosition,
            rewardAmount,
            visualCoinCount,
            onCoinArrived,
            onComplete,
            addToWallet,
            template,
            options
        );
    }

    private static bool PlayInternal(
        RectTransform flyRoot,
        RectTransform target,
        Vector2 startPosition,
        int rewardAmount,
        int visualCoinCount,
        Action<int> onCoinArrived,
        Action onComplete,
        bool addToWallet,
        CoinFlyFXTemplate template,
        Options? requestedOptions)
    {
        CoinFlyFX player = EnsureInstance();
        PopupInGame popupInGame = GetPopupInGame();
        template ??= popupInGame != null
            ? popupInGame.SharedCoinFlyFXTemplate
            : null;
        Options options = requestedOptions ??
            (popupInGame != null
                ? popupInGame.SharedCoinFlyFXOptions
                : Options.Default);

        if (player == null || template == null ||
            template.CoinViewPrefab == null || template.Settings == null ||
            !target.gameObject.activeInHierarchy)
        {
            CompleteWithoutVisual(
                rewardAmount,
                addToWallet,
                onCoinArrived,
                onComplete
            );
            return false;
        }

        if (addToWallet && rewardAmount > 0)
            GoldHandler.AddWithoutResourceAnimation(rewardAmount);

        int minimumCount = Mathf.Clamp(options.minimumCount, 1, 16);
        int maximumCount = Mathf.Clamp(options.maximumCount, minimumCount, 16);
        int coinCount = Mathf.Clamp(
            visualCoinCount,
            minimumCount,
            maximumCount
        );
        Sequence sequence = new Sequence
        {
            remaining = coinCount,
            complete = onComplete
        };

        int distributedReward = 0;
        float delay = Mathf.Max(0f, options.anticipationDuration);
        // Keep a visible launch gap so coins always form a stream instead of
        // leaving the source on the same frame.
        float staggerMin = Mathf.Max(0.015f, options.staggerMin);
        float staggerMax = Mathf.Max(staggerMin, options.staggerMax);
        for (int i = 0; i < coinCount; i++)
        {
            if (i > 0)
                delay += Random.Range(staggerMin, staggerMax);

            int cumulativeReward = (int)(
                (long)Mathf.Max(0, rewardAmount) * (i + 1) / coinCount
            );
            int coinReward = cumulativeReward - distributedReward;
            distributedReward = cumulativeReward;

            player.StartCoroutine(player.PlayCoin(
                sequence,
                flyRoot,
                target,
                startPosition,
                coinReward,
                delay,
                i == coinCount - 1,
                onCoinArrived,
                template,
                options
            ));
        }

        return true;
    }

    private IEnumerator PlayCoin(
        Sequence sequence,
        RectTransform flyRoot,
        RectTransform target,
        Vector2 sourcePosition,
        int coinReward,
        float delay,
        bool isFinalCoin,
        Action<int> onCoinArrived,
        CoinFlyFXTemplate template,
        Options options)
    {
        float waited = 0f;
        while (waited < delay)
        {
            if (!CanContinue(flyRoot, target))
            {
                FinishCoin(sequence, coinReward, onCoinArrived);
                yield break;
            }
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!TryGetTargetPosition(target, flyRoot, out Vector2 targetPosition))
        {
            FinishCoin(sequence, coinReward, onCoinArrived);
            yield break;
        }

        CoinVisual visual = GetVisual(template, flyRoot, sourcePosition, options);
        if (visual == null)
        {
            FinishCoin(sequence, coinReward, onCoinArrived);
            yield break;
        }

        Vector2 spawnOffset = new Vector2(
            Random.Range(-options.spawnRadiusX, options.spawnRadiusX),
            Random.Range(-options.spawnRadiusY, options.spawnRadiusY)
        );
        Vector2 outward = spawnOffset.sqrMagnitude > 1f
            ? spawnOffset.normalized
            : Random.insideUnitCircle.normalized;
        if (outward.sqrMagnitude < 0.001f)
            outward = Vector2.up;
        outward.y = Mathf.Abs(outward.y) * 0.75f + 0.25f;
        outward.Normalize();

        Vector2 spawnPosition = sourcePosition + spawnOffset;
        Vector2 burstPosition = spawnPosition + outward * Random.Range(
            Mathf.Min(options.burstDistanceMin, options.burstDistanceMax),
            Mathf.Max(options.burstDistanceMin, options.burstDistanceMax)
        );
        Vector2 hangPosition = burstPosition + outward * Random.Range(6f, 16f) +
                               Vector2.up * Random.Range(-3f, 5f);
        Vector2 targetDirection = targetPosition - hangPosition;
        float distanceFactor = Mathf.Clamp(
            targetDirection.magnitude / 500f,
            0.65f,
            1.25f
        );
        float bendStrength = Random.Range(
            Mathf.Min(options.curveStrengthMin, options.curveStrengthMax),
            Mathf.Max(options.curveStrengthMin, options.curveStrengthMax)
        ) * distanceFactor;

        // All coins bend from the same side of the target. Two controls make
        // a hook/J-shaped lane: rise first, then turn firmly into the wallet.
        float approachSide = Mathf.Abs(hangPosition.x - targetPosition.x) > 0.01f
            ? Mathf.Sign(hangPosition.x - targetPosition.x)
            : 1f;
        float verticalSign = Mathf.Abs(targetDirection.y) > 0.01f
            ? Mathf.Sign(targetDirection.y)
            : 1f;
        Vector2 verticalTowardTarget = Vector2.up * verticalSign;
        float verticalDistance = Mathf.Abs(targetDirection.y);
        float firstRise = Mathf.Clamp(verticalDistance * 0.38f, 70f, 240f);
        float finalApproach = Mathf.Clamp(verticalDistance * 0.2f, 45f, 160f);
        float laneVariation = Random.Range(-0.08f, 0.08f) * bendStrength;
        float signedBend = approachSide * (bendStrength + laneVariation);
        Vector2 firstControlPoint = hangPosition +
            verticalTowardTarget * firstRise +
            Vector2.right * (signedBend * 0.22f);
        Vector2 secondControlPoint = targetPosition -
            verticalTowardTarget * finalApproach +
            Vector2.right * signedBend;

        // The reference uses a loose stream: each coin gets one fixed random
        // timing at spawn so the group feels organic without frame jitter.
        float burstDuration = Random.Range(
            Mathf.Min(options.burstDurationMin, options.burstDurationMax),
            Mathf.Max(options.burstDurationMin, options.burstDurationMax)
        );
        float hangDuration = Random.Range(
            Mathf.Min(options.hangDurationMin, options.hangDurationMax),
            Mathf.Max(options.hangDurationMin, options.hangDurationMax)
        );
        float flightDuration = Random.Range(
            Mathf.Min(options.flightDurationMin, options.flightDurationMax),
            Mathf.Max(options.flightDurationMin, options.flightDurationMax)
        );
        burstDuration = Mathf.Max(0.01f, burstDuration);
        hangDuration = Mathf.Max(0f, hangDuration);
        flightDuration = Mathf.Max(0.05f, flightDuration);
        float startRotation = Random.Range(
            -options.startRotation,
            options.startRotation
        );
        float rotationDrift = Random.Range(
            -options.rotationDrift,
            options.rotationDrift
        );
        float depthScale = Random.Range(
            Mathf.Min(options.depthScaleMin, options.depthScaleMax),
            Mathf.Max(options.depthScaleMin, options.depthScaleMax)
        );
        depthScale = Mathf.Max(0.1f, depthScale);
        float pullStretch = Random.value < Mathf.Clamp01(options.pullStretchChance)
            ? Mathf.Max(0f, options.pullStretchStrength) * Random.Range(0.8f, 1.2f)
            : 0f;

        visual.transform.anchoredPosition = spawnPosition;
        visual.transform.localScale = Vector3.zero;
        visual.transform.localEulerAngles = Vector3.forward * startRotation;
        SetTrailsVisible(visual, false, spawnPosition, options);

        float elapsed = 0f;
        while (elapsed < burstDuration)
        {
            if (!CanContinue(flyRoot, target))
            {
                ReleaseVisual(visual, template);
                FinishCoin(sequence, coinReward, onCoinArrived);
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, burstDuration));
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            visual.transform.anchoredPosition = Vector2.LerpUnclamped(
                spawnPosition,
                burstPosition,
                eased
            );
            visual.transform.localScale = Vector3.one *
                (EvaluateSpawnScale(progress, options) * depthScale);
            visual.transform.localEulerAngles = Vector3.forward *
                (startRotation + rotationDrift * progress * 0.35f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < hangDuration)
        {
            if (!CanContinue(flyRoot, target))
            {
                ReleaseVisual(visual, template);
                FinishCoin(sequence, coinReward, onCoinArrived);
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, hangDuration));
            float settle = 1f - (1f - progress) * (1f - progress);
            Vector2 position = Vector2.Lerp(burstPosition, hangPosition, settle);
            position.y += Mathf.Sin(progress * Mathf.PI) * 3f;
            visual.transform.anchoredPosition = position;
            visual.transform.localScale = Vector3.one * depthScale;
            yield return null;
        }

        SetTrailsVisible(visual, true, hangPosition, options);
        elapsed = 0f;
        while (elapsed < flightDuration)
        {
            if (!CanContinue(flyRoot, target) ||
                !TryGetTargetPosition(target, flyRoot, out targetPosition))
            {
                ReleaseVisual(visual, template);
                FinishCoin(sequence, coinReward, onCoinArrived);
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, flightDuration));
            // A short cubic pull keeps the hover readable, then rapidly
            // accelerates the coin into the HUD target like a strong magnet.
            float eased = progress * progress * progress;
            float inverse = 1f - eased;
            visual.transform.anchoredPosition =
                inverse * inverse * inverse * hangPosition +
                3f * inverse * inverse * eased * firstControlPoint +
                3f * inverse * eased * eased * secondControlPoint +
                eased * eased * eased * targetPosition;
            float shrink = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.78f, 1f, eased)
            );
            float baseScale = depthScale * Mathf.Lerp(
                1f,
                options.flightEndScale,
                shrink
            );
            float pullEnvelope = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.12f, 0.72f, eased)
            ) * (1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.82f, 1f, eased)
            ));
            float stretch = pullStretch * pullEnvelope;
            visual.transform.localScale = new Vector3(
                baseScale * (1f - stretch),
                baseScale * (1f + stretch),
                1f
            );
            visual.transform.localEulerAngles = Vector3.forward *
                (startRotation + rotationDrift * progress);
            Color color = visual.image.color;
            color.a = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.94f, 1f, eased)
            );
            visual.image.color = color;
            UpdateTrails(visual.trails, visual.transform.anchoredPosition,
                color.a, options);
            yield return null;
        }

        QueueTargetPulse(target, isFinalCoin, options);
        ReleaseVisual(visual, template);
        FinishCoin(sequence, coinReward, onCoinArrived);
    }

    private static void FinishCoin(
        Sequence sequence,
        int coinReward,
        Action<int> onCoinArrived)
    {
        onCoinArrived?.Invoke(coinReward);
        if (sequence == null)
            return;

        sequence.remaining--;
        if (sequence.remaining <= 0)
            sequence.complete?.Invoke();
    }

    private CoinVisual GetVisual(
        CoinFlyFXTemplate template,
        RectTransform flyRoot,
        Vector2 sourcePosition,
        Options options)
    {
        RectTransform prefab = template.CoinViewPrefab;
        int key = prefab.GetInstanceID();
        if (!_visualPools.TryGetValue(key, out Stack<CoinVisual> pool))
        {
            pool = new Stack<CoinVisual>();
            _visualPools.Add(key, pool);
        }

        CoinVisual visual = pool.Count > 0 ? pool.Pop() : CreateVisual(prefab, key);
        if (visual == null)
            return null;

        visual.gameObject.SetActive(true);
        visual.gameObject.layer = flyRoot.gameObject.layer;
        visual.transform.SetParent(flyRoot, false);
        visual.transform.SetAsLastSibling();
        visual.transform.anchoredPosition = sourcePosition;
        visual.transform.localScale = Vector3.one;
        visual.transform.localEulerAngles = Vector3.zero;
        visual.transform.sizeDelta = Vector2.one * Mathf.Max(1f, options.coinSize);
        visual.image.color = Color.white;
        visual.image.raycastTarget = false;
        visual.image.preserveAspect = true;

        if (visual.animator != null &&
            visual.animator.runtimeAnimatorController != null)
        {
            visual.animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            visual.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            visual.animator.enabled = true;
            visual.animator.speed = Random.Range(0.9f, 1.1f);
            visual.animator.Rebind();
            visual.animator.Play("Base Layer.CoinSpin", 0, Random.value);
            visual.animator.Update(0f);
        }

        int trailCount = Mathf.Clamp(options.trailCount, 0, 5);
        while (visual.trails.Count < trailCount)
            visual.trails.Add(CreateTrail(flyRoot.gameObject.layer));
        for (int i = trailCount; i < visual.trails.Count; i++)
            visual.trails[i].gameObject.SetActive(false);
        return visual;
    }

    private static CoinVisual CreateVisual(RectTransform prefab, int poolKey)
    {
        GameObject instance = Instantiate(prefab.gameObject);
        instance.name = "CoinFlyFX_Coin";
        RectTransform rect = instance.GetComponent<RectTransform>();
        Image image = instance.GetComponent<Image>();
        if (rect == null || image == null)
        {
            Destroy(instance);
            return null;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return new CoinVisual
        {
            gameObject = instance,
            transform = rect,
            image = image,
            animator = instance.GetComponent<Animator>(),
            poolKey = poolKey
        };
    }

    private static TrailVisual CreateTrail(int layer)
    {
        GameObject trailObject = new GameObject(
            "CoinFlyFX_Trail",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(MergeCoinTrailGraphic)
        );
        trailObject.layer = layer;
        RectTransform rect = trailObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        MergeCoinTrailGraphic graphic =
            trailObject.GetComponent<MergeCoinTrailGraphic>();
        graphic.raycastTarget = false;
        return new TrailVisual
        {
            gameObject = trailObject,
            transform = rect,
            graphic = graphic
        };
    }

    private void ReleaseVisual(CoinVisual visual, CoinFlyFXTemplate template)
    {
        if (visual == null)
            return;

        if (visual.animator != null)
        {
            visual.animator.enabled = false;
            visual.animator.speed = 1f;
        }
        visual.image.color = Color.white;
        visual.gameObject.SetActive(false);
        for (int i = 0; i < visual.trails.Count; i++)
            visual.trails[i].gameObject.SetActive(false);

        if (!_visualPools.TryGetValue(visual.poolKey, out Stack<CoinVisual> pool))
        {
            pool = new Stack<CoinVisual>();
            _visualPools.Add(visual.poolKey, pool);
        }
        pool.Push(visual);
    }

    private static void SetTrailsVisible(
        CoinVisual visual,
        bool visible,
        Vector2 startPosition,
        Options options)
    {
        int count = Mathf.Clamp(options.trailCount, 0, 5);
        for (int i = 0; i < visual.trails.Count; i++)
        {
            TrailVisual trail = visual.trails[i];
            bool show = visible && i < count;
            trail.gameObject.SetActive(show);
            if (!show)
                continue;

            trail.transform.SetParent(visual.transform.parent, false);
            trail.transform.SetSiblingIndex(visual.transform.GetSiblingIndex());
            trail.transform.anchoredPosition = startPosition;
            trail.transform.localScale = Vector3.one;
            trail.transform.localEulerAngles = Vector3.zero;
            trail.position = startPosition;
            float ratio = (i + 1f) / (count + 1f);
            trail.transform.sizeDelta = new Vector2(
                Mathf.Lerp(options.trailHeadWidth * 0.7f,
                    options.trailHeadWidth * 0.38f, ratio),
                Mathf.Lerp(options.trailLength * 0.55f,
                    options.trailLength * 0.3f, ratio)
            );
            trail.graphic.color = new Color(
                1f,
                0.91f,
                0.76f,
                options.trailAlpha
            );
        }
    }

    private static void UpdateTrails(
        List<TrailVisual> trails,
        Vector2 coinPosition,
        float opacity,
        Options options)
    {
        float follow = 1f - Mathf.Exp(
            -Mathf.Max(1f, options.trailFollowSpeed) * Time.unscaledDeltaTime
        );
        Vector2 previous = coinPosition;
        for (int i = 0; i < trails.Count; i++)
        {
            TrailVisual trail = trails[i];
            if (!trail.gameObject.activeSelf)
                continue;

            trail.position = Vector2.Lerp(trail.position, previous, follow);
            Vector2 direction = previous - trail.position;
            trail.transform.anchoredPosition = trail.position;
            if (direction.sqrMagnitude > 0.01f)
            {
                trail.transform.localEulerAngles = Vector3.forward *
                    (Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
                float halfLength = trail.transform.rect.height * 0.5f;
                trail.transform.anchoredPosition = previous -
                    direction.normalized * halfLength;
            }
            Color color = trail.graphic.color;
            color.a = options.trailAlpha * Mathf.Clamp01(opacity);
            trail.graphic.color = color;
            previous = trail.position;
        }
    }

    private void QueueTargetPulse(
        RectTransform target,
        bool isFinalCoin,
        Options options)
    {
        if (target == null)
            return;

        if (!_targetBaseScales.ContainsKey(target))
            _targetBaseScales[target] = target.localScale;
        if (_targetPulses.TryGetValue(target, out Coroutine active) &&
            active != null)
        {
            StopCoroutine(active);
        }
        _targetPulses[target] = StartCoroutine(PlayTargetPulse(
            target,
            isFinalCoin,
            options
        ));
    }

    private IEnumerator PlayTargetPulse(
        RectTransform target,
        bool isFinalCoin,
        Options options)
    {
        if (target == null || !_targetBaseScales.TryGetValue(
                target,
                out Vector3 baseScale))
            yield break;

        Vector3 startScale = target.localScale;
        float strength = isFinalCoin
            ? Mathf.Max(1f, options.finalImpactMultiplier)
            : 1f;
        Vector3 peak = baseScale * (1f +
            (Mathf.Max(1f, options.targetPulseScale) - 1f) * strength);
        Vector3 undershoot = baseScale * options.targetPulseUndershoot;
        float duration = Mathf.Max(0.05f, options.targetPulseDuration);
        float elapsed = 0f;
        while (elapsed < duration && target != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (progress < 0.38f)
            {
                float phase = 1f - Mathf.Pow(1f - progress / 0.38f, 3f);
                target.localScale = Vector3.LerpUnclamped(startScale, peak, phase);
            }
            else if (progress < 0.72f)
            {
                float phase = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.38f, 0.72f, progress));
                target.localScale = Vector3.LerpUnclamped(peak, undershoot, phase);
            }
            else
            {
                float phase = Mathf.InverseLerp(0.72f, 1f, progress);
                phase = 1f - Mathf.Pow(1f - phase, 3f);
                target.localScale = Vector3.LerpUnclamped(undershoot, baseScale, phase);
            }
            yield return null;
        }

        if (target != null)
            target.localScale = baseScale;
        _targetPulses.Remove(target);
        _targetBaseScales.Remove(target);
    }

    private static float EvaluateSpawnScale(float progress, Options options)
    {
        const float overshootPoint = 0.62f;
        if (progress < overshootPoint)
        {
            float rise = Mathf.Clamp01(progress / overshootPoint);
            float eased = 1f - Mathf.Pow(1f - rise, 3f);
            return Mathf.Lerp(options.spawnScale, options.overshootScale, eased);
        }

        float settle = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(overshootPoint, 1f, progress)
        );
        return Mathf.Lerp(options.overshootScale, 1f, settle);
    }

    private static bool TryConvertUiCenter(
        RectTransform source,
        RectTransform flyRoot,
        out Vector2 position)
    {
        Vector3[] corners = new Vector3[4];
        source.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(
            GetUiCamera(source),
            center
        );
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            flyRoot,
            screen,
            GetUiCamera(flyRoot),
            out position
        );
    }

    private bool TryGetTargetPosition(
        RectTransform target,
        RectTransform flyRoot,
        out Vector2 position)
    {
        position = Vector2.zero;
        if (target == null || flyRoot == null)
            return false;

        target.GetWorldCorners(_corners);
        Vector3 center = (_corners[0] + _corners[2]) * 0.5f;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(
            GetUiCamera(target),
            center
        );
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            flyRoot,
            screen,
            GetUiCamera(flyRoot),
            out position
        );
    }

    private static RectTransform FindFlyRoot(RectTransform target)
    {
        Canvas canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            return null;
        Canvas rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        return rootCanvas.transform as RectTransform;
    }

    private static Camera GetUiCamera(RectTransform rectTransform)
    {
        Canvas canvas = rectTransform != null
            ? rectTransform.GetComponentInParent<Canvas>()
            : null;
        Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
        return rootCanvas != null &&
               rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;
    }

    private static bool CanContinue(RectTransform root, RectTransform target)
    {
        return root != null && target != null &&
               root.gameObject.activeInHierarchy &&
               target.gameObject.activeInHierarchy;
    }

    private static CoinFlyFX EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        GameObject owner = PopupController.Instance != null
            ? PopupController.Instance.gameObject
            : null;
        if (owner == null)
        {
            owner = new GameObject("[CoinFlyFX]");
            DontDestroyOnLoad(owner);
        }
        _instance = owner.GetComponent<CoinFlyFX>();
        if (_instance == null)
            _instance = owner.AddComponent<CoinFlyFX>();
        return _instance;
    }

    private static PopupInGame GetPopupInGame()
    {
        return PopupController.Instance != null
            ? PopupController.Instance.Get<PopupInGame>() as PopupInGame
            : null;
    }

    private static RectTransform FindActiveGoldTarget()
    {
        GoldHandler[] handlers = FindObjectsByType<GoldHandler>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
        for (int i = 0; i < handlers.Length; i++)
        {
            RectTransform target = handlers[i] != null
                ? handlers[i].CoinFlyTarget
                : null;
            if (target != null && target.gameObject.activeInHierarchy)
                return target;
        }

        PopupInGame popupInGame = GetPopupInGame();
        return popupInGame != null && popupInGame.isActiveAndEnabled
            ? popupInGame.SharedCoinFlyTarget
            : null;
    }

    private static void CompleteWithoutVisual(
        int rewardAmount,
        bool addToWallet,
        Action<int> onCoinArrived,
        Action onComplete)
    {
        if (addToWallet && rewardAmount > 0)
            GoldHandler.AddWithoutResourceAnimation(rewardAmount);
        onCoinArrived?.Invoke(Mathf.Max(0, rewardAmount));
        onComplete?.Invoke();
    }
}
