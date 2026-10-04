using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(CardSlotHolder))]
public class CardSlotHolderGold : MonoBehaviour
{
    [Header("Purchase Config")]
    [SerializeField, Min(0)] private int goldPrice = 100;

    [Header("Purchase Visual")]
    [SerializeField] private GameObject purchaseVisual;
    [SerializeField] private TMP_Text priceText;

    [Header("Unlock Effect")]
    [SerializeField] private GameObject unlockFx;
    [SerializeField] private Transform unlockIcon;
    [Tooltip("Fade lock visuals before Purchase Visual is hidden.")]
    [SerializeField] private LockEffect lockEffect;
    [SerializeField, Min(0.01f)] private float unlockFxDuration = 0.8f;
    [SerializeField, Min(0f)] private float unlockIconRotationTurns = 1f;
    [SerializeField, Min(0.01f)] private float unlockIconRotationDuration = 0.55f;
    [SerializeField, Min(1f)] private float unlockIconMaxScale = 1.35f;
    [SerializeField, Min(0.01f)] private float unlockIconScaleUpDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float unlockIconScaleDownDuration = 0.3f;

    [Header("Events")]
    [SerializeField] private UnityEvent onPurchased;
    [SerializeField] private UnityEvent onNotEnoughGold;

    [Header("Runtime")]
    [SerializeField] private bool isPurchased;
    [SerializeField] private bool isUnlockedByPreLevelBooster;

    [Header("Pre-Level Extra Tray Effect")]
    [Tooltip("Optional swipe/glow object. Dat inactive san va keo vao day.")]
    [SerializeField] private GameObject preLevelUnlockFx;
    [SerializeField, Min(0.05f)] private float preLevelFxDuration = 0.55f;
    [SerializeField, Min(0f)] private float preLevelFxMoveUp = 0.45f;

    private CardSlotHolder _holder;
    private Level _level;
    private Coroutine _unlockEffectRoutine;
    private Vector3 _unlockIconAuthoredScale = Vector3.one;
    private Quaternion _unlockIconAuthoredRotation = Quaternion.identity;
    private bool _hasCachedUnlockIconTransform;
    private bool _isPlayingUnlockEffect;

    public int GoldPrice => goldPrice;
    public bool IsPurchased => isPurchased;
    public bool IsLocked =>
        _holder != null
            ? _holder.IsLocked
            : !isPurchased && !isUnlockedByPreLevelBooster;

    private void Awake()
    {
        _holder = GetComponent<CardSlotHolder>();
        _level = GetComponentInParent<Level>();
        CacheUnlockIconTransform();
        ApplyState();
    }

    private void Start()
    {
        ApplyState();
    }

    public void OnClickPurchase()
    {
        TryPurchase();
    }

    public bool TryPurchase()
    {
        if (isPurchased)
            return true;

        if (Data.PlayerData == null)
        {
            Debug.LogWarning(
                "CardSlotHolderGold: PlayerData is not ready."
            );
            return false;
        }

        int safePrice = Mathf.Max(0, goldPrice);

        if (Data.PlayerData.CurrentGold < safePrice)
        {
            Observer.Notify?.Invoke(
                "Not enough gold!",
                Vector2.zero
            );
            onNotEnoughGold?.Invoke();
            return false;
        }

        Data.PlayerData.CurrentGold -= safePrice;
        Data.SaveData();

        isPurchased = true;
        ApplyUnlockedStateWithEffect();
        onPurchased?.Invoke();

        if (_level != null)
            _level.OnBoardChanged?.Invoke();

        return true;
    }

    public bool UnlockByPreLevelBooster()
    {
        if (_holder == null)
            _holder = GetComponent<CardSlotHolder>();

        if (_holder == null || !_holder.IsLocked)
            return false;

        isUnlockedByPreLevelBooster = true;
        ApplyUnlockedStateWithEffect();
        _holder.PlayActiveSoftEffect();

        if (preLevelUnlockFx != null && preLevelUnlockFx != unlockFx)
            StartCoroutine(PlayPreLevelUnlockFx());
        else if (unlockFx == null)
            StartCoroutine(PlayGeneratedSwipeFx());

        if (_level == null)
            _level = GetComponentInParent<Level>();

        if (_level != null)
            _level.OnBoardChanged?.Invoke();

        return true;
    }

    public bool UnlockByInGameBooster()
    {
        return UnlockByPreLevelBooster();
    }

    private void ApplyState()
    {
        bool locked = !isPurchased && !isUnlockedByPreLevelBooster;

        if (_holder == null)
            _holder = GetComponent<CardSlotHolder>();

        if (_holder != null)
        {
            _holder.SetLocked(locked);
            _holder.NotifyCardsChanged();
        }

        if (purchaseVisual != null)
            purchaseVisual.SetActive(locked || _isPlayingUnlockEffect);

        if (locked && !_isPlayingUnlockEffect && lockEffect != null)
            lockEffect.RestoreAlpha();

        if (priceText != null)
        {
            priceText.text = Mathf.Max(0, goldPrice).ToString();
            priceText.gameObject.SetActive(locked);
        }

        if (locked && !_isPlayingUnlockEffect)
            ResetUnlockIconTransform();
    }

    private void ApplyUnlockedStateWithEffect()
    {
        if (unlockFx == null && unlockIcon == null && lockEffect == null)
        {
            ApplyState();
            return;
        }

        if (_unlockEffectRoutine != null)
            StopCoroutine(_unlockEffectRoutine);

        CacheUnlockIconTransform();
        _isPlayingUnlockEffect = true;
        ApplyState();
        if (lockEffect != null)
            lockEffect.RestoreAlpha();
        _unlockEffectRoutine = StartCoroutine(PlayUnlockEffect());
    }

    private IEnumerator PlayUnlockEffect()
    {
        if (unlockFx != null)
        {
            unlockFx.SetActive(false);
            unlockFx.SetActive(true);

            ParticleSystem[] particleSystems =
                unlockFx.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystems[i].Play(true);
            }

            Animator[] animators = unlockFx.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = true;
                animators[i].Play(0, 0, 0f);
            }

            Animation[] animations = unlockFx.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < animations.Length; i++)
            {
                animations[i].Stop();
                animations[i].Play();
            }
        }

        if (unlockIcon != null)
        {
            unlockIcon.gameObject.SetActive(true);
            unlockIcon.localScale = _unlockIconAuthoredScale;
            unlockIcon.localRotation = _unlockIconAuthoredRotation;
        }

        float rotateDuration = Mathf.Max(0.01f, unlockIconRotationDuration);
        float scaleUpDuration = Mathf.Max(0.01f, unlockIconScaleUpDuration);
        float scaleDownDuration = Mathf.Max(0.01f, unlockIconScaleDownDuration);
        float scaleDuration = scaleUpDuration + scaleDownDuration;
        float lockFadeDuration = lockEffect != null
            ? lockEffect.FadeDuration
            : 0f;
        float duration = Mathf.Max(
            unlockFx != null ? Mathf.Max(0.01f, unlockFxDuration) : 0f,
            unlockIcon != null ? Mathf.Max(rotateDuration, scaleDuration) : 0f,
            lockFadeDuration
        );
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (unlockIcon != null)
            {
                float rotateProgress = Mathf.Clamp01(elapsed / rotateDuration);
                float angle = 360f * unlockIconRotationTurns * rotateProgress;
                unlockIcon.localRotation = _unlockIconAuthoredRotation *
                    Quaternion.Euler(0f, 0f, angle);

                float scaleMultiplier;
                if (elapsed <= scaleUpDuration)
                {
                    float progress = Mathf.Clamp01(elapsed / scaleUpDuration);
                    scaleMultiplier = Mathf.Lerp(
                        1f,
                        unlockIconMaxScale,
                        Mathf.SmoothStep(0f, 1f, progress)
                    );
                }
                else
                {
                    float progress = Mathf.Clamp01(
                        (elapsed - scaleUpDuration) / scaleDownDuration
                    );
                    scaleMultiplier = Mathf.Lerp(
                        unlockIconMaxScale,
                        0f,
                        Mathf.SmoothStep(0f, 1f, progress)
                    );
                }

                unlockIcon.localScale =
                    _unlockIconAuthoredScale * scaleMultiplier;
            }

            if (lockEffect != null)
            {
                float fadeProgress = Mathf.Clamp01(
                    elapsed / Mathf.Max(0.01f, lockFadeDuration)
                );
                lockEffect.SetFadeProgress(fadeProgress);
            }

            yield return null;
        }

        if (lockEffect != null)
            lockEffect.SetFadeProgress(1f);

        if (unlockIcon != null)
        {
            unlockIcon.localScale = Vector3.zero;
            unlockIcon.localRotation = _unlockIconAuthoredRotation;
        }

        if (unlockFx != null)
            unlockFx.SetActive(false);

        _unlockEffectRoutine = null;
        _isPlayingUnlockEffect = false;
        ApplyState();
    }

    private void CacheUnlockIconTransform()
    {
        if (unlockIcon == null || _hasCachedUnlockIconTransform)
            return;

        _unlockIconAuthoredScale = unlockIcon.localScale;
        _unlockIconAuthoredRotation = unlockIcon.localRotation;
        _hasCachedUnlockIconTransform = true;
    }

    private void ResetUnlockIconTransform()
    {
        if (unlockIcon == null)
            return;

        CacheUnlockIconTransform();
        unlockIcon.localScale = _unlockIconAuthoredScale;
        unlockIcon.localRotation = _unlockIconAuthoredRotation;
    }

    private IEnumerator PlayPreLevelUnlockFx()
    {
        Transform fxTransform = preLevelUnlockFx.transform;
        Vector3 startPosition = fxTransform.localPosition;
        SpriteRenderer[] renderers =
            preLevelUnlockFx.GetComponentsInChildren<SpriteRenderer>(true);
        Color[] startColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            startColors[i] = renderers[i].color;

        preLevelUnlockFx.SetActive(true);
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, preLevelFxDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            fxTransform.localPosition = startPosition +
                Vector3.up * (preLevelFxMoveUp * eased);

            float alpha = Mathf.Sin(progress * Mathf.PI);
            for (int i = 0; i < renderers.Length; i++)
            {
                Color color = startColors[i];
                color.a *= alpha;
                renderers[i].color = color;
            }

            yield return null;
        }

        fxTransform.localPosition = startPosition;
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].color = startColors[i];
        preLevelUnlockFx.SetActive(false);
    }

    private IEnumerator PlayGeneratedSwipeFx()
    {
        SpriteRenderer[] holderRenderers =
            GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer referenceRenderer = null;
        float largestArea = 0f;

        for (int i = 0; i < holderRenderers.Length; i++)
        {
            SpriteRenderer candidate = holderRenderers[i];
            if (candidate == null || candidate.sprite == null)
                continue;

            float area = candidate.bounds.size.x * candidate.bounds.size.y;
            if (area > largestArea)
            {
                largestArea = area;
                referenceRenderer = candidate;
            }
        }

        if (referenceRenderer == null)
            yield break;

        GameObject swipeObject = new GameObject("ExtraTraySwipeFx");
        swipeObject.transform.SetParent(transform, false);

        Sprite swipeSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
        SpriteRenderer swipeRenderer =
            swipeObject.AddComponent<SpriteRenderer>();
        swipeRenderer.sprite = swipeSprite;
        swipeRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
        swipeRenderer.sortingOrder = referenceRenderer.sortingOrder + 50;

        Vector3 localSize = transform.InverseTransformVector(
            referenceRenderer.bounds.size
        );
        float width = Mathf.Max(0.25f, Mathf.Abs(localSize.x));
        float height = Mathf.Max(0.5f, Mathf.Abs(localSize.y));
        swipeObject.transform.localScale =
            new Vector3(width * 0.9f, height * 0.08f, 1f);

        Vector3 center = transform.InverseTransformPoint(
            referenceRenderer.bounds.center
        );
        Vector3 start = center + Vector3.down * (height * 0.5f);
        Vector3 end = center + Vector3.up * (height * 0.5f);
        float duration = Mathf.Max(0.05f, preLevelFxDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            swipeObject.transform.localPosition = Vector3.Lerp(
                start,
                end,
                Mathf.SmoothStep(0f, 1f, progress)
            );
            swipeRenderer.color = new Color(
                0.65f,
                1f,
                0.85f,
                Mathf.Sin(progress * Mathf.PI) * 0.9f
            );
            yield return null;
        }

        Destroy(swipeSprite);
        Destroy(swipeObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        goldPrice = Mathf.Max(0, goldPrice);
        preLevelFxDuration = Mathf.Max(0.05f, preLevelFxDuration);
        preLevelFxMoveUp = Mathf.Max(0f, preLevelFxMoveUp);
        unlockFxDuration = Mathf.Max(0.01f, unlockFxDuration);
        unlockIconRotationTurns = Mathf.Max(0f, unlockIconRotationTurns);
        unlockIconRotationDuration = Mathf.Max(0.01f, unlockIconRotationDuration);
        unlockIconMaxScale = Mathf.Max(1f, unlockIconMaxScale);
        unlockIconScaleUpDuration = Mathf.Max(0.01f, unlockIconScaleUpDuration);
        unlockIconScaleDownDuration = Mathf.Max(0.01f, unlockIconScaleDownDuration);

        if (!Application.isPlaying && priceText != null)
            priceText.text = goldPrice.ToString();
    }
#endif
}
