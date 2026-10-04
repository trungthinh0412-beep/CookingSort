using System.Collections;
using System.Collections.Generic;
using CustomInspector;
using TMPro;
using UnityEngine;
using Action = System.Action;

public class Card : MonoBehaviour
{
    private static CardConfig _themeConfig;

    [SerializeField] private CardType cardType;
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private DynamicShadow dynamicShadow;
    [SerializeField] private SimpleShadow deckFlipShadow;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private GameObject highlightOutline;

    [Header("Select Animation")]
    [SerializeField] private float selectedScale = 1.08f;
    [SerializeField] private float selectLift = 0f;

    [SerializeField, Min(0.01f)] private float selectDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float deselectDuration = 0.15f;

    [Header("Select Glow")]
    [SerializeField, Min(0.01f)] private float glowFadeInDuration = 0.3f;
    [SerializeField, Range(0f, 1f)] private float glowSustain = 0.28f;
    [SerializeField, Min(0.01f)] private float glowDecayTime = 0.35f;
    [SerializeField, Range(0f, 1f)] private float glowPeakAlpha = 0.95f;
    [SerializeField, Min(0.01f)] private float glowFadeOutDuration = 0.06f;

    [Header("Dark King Card")]
    [SerializeField, Min(0)]
    [ShowIf(nameof(cardType), CardType.DarkingCard)]
    [Tooltip("So luot rieng cua la nay. Dat 0 de dung default tu CardConfig.")]
    private int darkKingMovesBeforeTrayLock;

    [Header("Frozen Number Card")]
    [SerializeField]
    [Tooltip("Dong bang la so nay cho den khi du so move.")]
    private bool isFrozenCard;
    [SerializeField, Min(0), Tooltip("So move rieng cua la nay. Dat 0 de dung default tu CardConfig.")]
    private int frozenMovesBeforeOpen;
    
    private Coroutine _glowRoutine;
    private Coroutine _selectRoutine;
    private bool _isSelected;
    private SpriteRenderer _outlineRenderer;
    private bool _isUnassignedWild;
    private bool _isShowingOffSprite;
    private bool _darkKingCountdownInitialized;
    private int _darkKingMovesRemaining;
    private Canvas _darkKingCountdownCanvas;
    private TMP_Text _darkKingCountdownText;
    private bool _frozenCountdownInitialized;
    private int _frozenMovesRemaining;
    private Sprite _frozenNumberSprite;
    private CardEffectController _effectController;
    private CardBurnEffect _burnEffect;

    public bool IsUnassignedWild => _isUnassignedWild;
    public bool IsNumberCard => IsNumberCardType(cardType) && !IsFrozen;
    public bool IsFrozen => isFrozenCard && IsNumberCardType(cardType) &&
                            _frozenCountdownInitialized && _frozenMovesRemaining > 0;
    public bool IsConfiguredFrozen => isFrozenCard && IsNumberCardType(cardType);
    public int FrozenMovesBeforeOpen => frozenMovesBeforeOpen;
    public bool IsObstacleCard => IsObstacleCardType(cardType);
    public int DarkKingMovesRemaining => cardType == CardType.DarkingCard
        ? _darkKingMovesRemaining
        : 0;
    public int DarkKingMovesBeforeTrayLock =>
        darkKingMovesBeforeTrayLock;

    private bool _isTemporarySortIndex;
    private int _temporarySortIndex;

    public SpriteRenderer IconRenderer => iconRenderer;
    public DynamicShadow DynamicShadow => dynamicShadow;
    public SimpleShadow DeckFlipShadow => deckFlipShadow;
    public int SortIndex => iconRenderer.sortingOrder;
    public CardEffectController EffectController
    {
        get
        {
            if (_effectController == null)
                _effectController = GetComponent<CardEffectController>();
            return _effectController;
        }
    }

    public CardType CardType
    {
        get => cardType;
        set
        {
            cardType = value;
            RefreshDarkKingCountdownVisual();
        }
    }

    public static bool IsNumberCardType(CardType type)
    {
        return type >= CardType.Card1 && type <= CardType.Card20;
    }

    public static void SetThemeConfig(CardConfig config)
    {
        _themeConfig = config;
    }

    public static bool IsObstacleCardType(CardType type)
    {
        return type == CardType.DowngradeCard ||
               type == CardType.ChainCard ||
               type == CardType.FrozenCard ||
               type == CardType.IronCard ||
               type == CardType.DarkingCard;
    }

    public Sprite IconSprite => iconRenderer != null ? iconRenderer.sprite : null;

    public Sprite OffSprite => offSprite;

    private void Awake()
    {
        _effectController = GetComponent<CardEffectController>();
        _burnEffect = GetComponent<CardBurnEffect>();
        if (_burnEffect == null)
            _burnEffect = gameObject.AddComponent<CardBurnEffect>();
        _isUnassignedWild = cardType == CardType.WildCard;

        if (highlightOutline != null)
        {
            _outlineRenderer = highlightOutline.GetComponent<SpriteRenderer>();
        }

        RefreshDarkKingCountdownVisual();
    }

    public void PlayBurn(Action onComplete = null)
    {
        PlayBurn(-1f, -1, -1f, onComplete);
    }

    public void PlayBurn(
        float duration,
        int sparkCount,
        float sparkLifetime,
        Action onComplete = null)
    {
        if (_burnEffect == null)
            _burnEffect = GetComponent<CardBurnEffect>();
        if (_burnEffect == null)
            _burnEffect = gameObject.AddComponent<CardBurnEffect>();

        SpriteRenderer[] allRenderers =
            GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> auxiliaryRenderers =
            new List<SpriteRenderer>(allRenderers.Length);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null && allRenderers[i] != iconRenderer)
                auxiliaryRenderers.Add(allRenderers[i]);
        }

        _burnEffect.SetTargetRenderers(iconRenderer);
        _burnEffect.SetHiddenRenderers(auxiliaryRenderers.ToArray());
        _burnEffect.PlayBurn(
            duration,
            sparkCount,
            sparkLifetime,
            onComplete
        );
    }

    public void InitializeDarkKingCountdown(int moveCount)
    {
        if (cardType != CardType.DarkingCard ||
            _darkKingCountdownInitialized)
            return;

        _darkKingMovesRemaining = GetDarkKingMoveCount(moveCount);
        _darkKingCountdownInitialized = true;
        RefreshDarkKingCountdownVisual();
    }

    public bool ConsumeDarkKingMove(int configuredMoveCount)
    {
        if (cardType != CardType.DarkingCard)
            return false;

        InitializeDarkKingCountdown(configuredMoveCount);
        _darkKingMovesRemaining = Mathf.Max(0, _darkKingMovesRemaining - 1);
        RefreshDarkKingCountdownVisual();
        EffectController?.PlayCountdown(_darkKingMovesRemaining);
        if (_darkKingMovesRemaining == 0)
            EffectController?.PlayActivate();
        return _darkKingMovesRemaining == 0;
    }

    public void SetDarkKingMovesBeforeTrayLock(int moveCount)
    {
        darkKingMovesBeforeTrayLock = Mathf.Max(1, moveCount);
        _darkKingCountdownInitialized = false;
        RefreshDarkKingCountdownVisual();
    }

    public void InitializeFrozenCountdown(int defaultMoveCount, Sprite frozenSprite,
        Sprite numberSprite)
    {
        if (!IsConfiguredFrozen || _frozenCountdownInitialized)
            return;

        _frozenMovesRemaining = frozenMovesBeforeOpen > 0
            ? frozenMovesBeforeOpen
            : Mathf.Max(1, defaultMoveCount);
        _frozenNumberSprite = numberSprite != null
            ? numberSprite
            : iconRenderer != null ? iconRenderer.sprite : null;
        _frozenCountdownInitialized = true;
        if (frozenSprite != null && iconRenderer != null)
            iconRenderer.sprite = frozenSprite;
        RefreshDarkKingCountdownVisual();
    }

    public void SetFrozenMovesBeforeOpen(int moveCount)
    {
        if (!IsNumberCardType(cardType))
            return;

        isFrozenCard = true;
        frozenMovesBeforeOpen = Mathf.Max(1, moveCount);
        _frozenCountdownInitialized = false;
    }

    public bool ConsumeFrozenMove()
    {
        if (!IsFrozen)
            return false;

        _frozenMovesRemaining--;
        if (_frozenMovesRemaining == 0 && iconRenderer != null)
        {
            EffectController?.PlayActivate();
            FrozenCardBreakEffect.Play(iconRenderer);
            iconRenderer.sprite = _frozenNumberSprite;
        }
        RefreshDarkKingCountdownVisual();
        return _frozenMovesRemaining == 0;
    }

    private int GetDarkKingMoveCount(int defaultMoveCount)
    {
        return darkKingMovesBeforeTrayLock > 0
            ? darkKingMovesBeforeTrayLock
            : Mathf.Max(1, defaultMoveCount);
    }

    public void SetEnabledRenderer(bool isEnabled) {
        if (iconRenderer) iconRenderer.enabled = isEnabled;
        SetShadowEnabled(isEnabled);
        RefreshDarkKingCountdownVisual();
    }

    public void SetShadowEnabled(bool isEnabled) {
        if (dynamicShadow) dynamicShadow.SetShadowEnabled(isEnabled);
    }

    public void SetIcon(Sprite sprite)
    {
        if (iconRenderer != null)
        {
            iconRenderer.sprite = sprite;
        }

        _isShowingOffSprite = false;
        RefreshDarkKingCountdownVisual();
    }

    public void SetOffSprite()
    {
        if (iconRenderer != null && offSprite != null)
        {
            iconRenderer.sprite = offSprite;
        }

        _isShowingOffSprite = true;
        RefreshDarkKingCountdownVisual();
    }

    private void RefreshDarkKingCountdownVisual()
    {
        bool shouldShow =
            ((cardType == CardType.DarkingCard && _darkKingCountdownInitialized) || IsFrozen) &&
            !_isShowingOffSprite &&
            iconRenderer != null &&
            iconRenderer.enabled;

        if (cardType == CardType.DarkingCard || IsFrozen)
            EnsureDarkKingCountdownVisual();

        if (_darkKingCountdownCanvas == null)
            return;

        _darkKingCountdownCanvas.gameObject.SetActive(shouldShow);
        if (!shouldShow || _darkKingCountdownText == null)
            return;

        _darkKingCountdownText.text = IsFrozen
            ? _frozenMovesRemaining.ToString()
            : _darkKingMovesRemaining.ToString();
        UpdateDarkKingCountdownSorting();
    }

    private void EnsureDarkKingCountdownVisual()
    {
        if (_darkKingCountdownCanvas != null)
            return;

        GameObject canvasObject = new GameObject(
            "DarkKingCountdownCanvas",
            typeof(RectTransform),
            typeof(Canvas)
        );
        canvasObject.layer = gameObject.layer;
        RectTransform canvasTransform =
            canvasObject.GetComponent<RectTransform>();
        canvasTransform.SetParent(transform, false);
        canvasTransform.localPosition = new Vector3(0.14f, -0.4f, 0f);
        canvasTransform.localRotation = Quaternion.identity;
        canvasTransform.localScale = Vector3.one * 0.01f;
        canvasTransform.sizeDelta = new Vector2(42f, 20f);

        _darkKingCountdownCanvas = canvasObject.GetComponent<Canvas>();
        _darkKingCountdownCanvas.renderMode = RenderMode.WorldSpace;
        _darkKingCountdownCanvas.overrideSorting = true;

        GameObject textObject = new GameObject(
            "CountdownText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.layer = gameObject.layer;
        RectTransform textTransform =
            textObject.GetComponent<RectTransform>();
        textTransform.SetParent(canvasTransform, false);
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;

        _darkKingCountdownText =
            textObject.GetComponent<TextMeshProUGUI>();
        _darkKingCountdownText.raycastTarget = false;
        _darkKingCountdownText.textWrappingMode =
            TextWrappingModes.NoWrap;
        _darkKingCountdownText.alignment = TextAlignmentOptions.Center;
        _darkKingCountdownText.fontSize = 16f;
        _darkKingCountdownText.fontStyle = FontStyles.Bold;
        _darkKingCountdownText.color = new Color32(54, 45, 72, 255);
        _darkKingCountdownText.outlineColor =
            new Color32(235, 224, 246, 255);
        _darkKingCountdownText.outlineWidth = 0.12f;

        UpdateDarkKingCountdownSorting();
    }

    private void UpdateDarkKingCountdownSorting()
    {
        if (_darkKingCountdownCanvas == null || iconRenderer == null)
            return;

        _darkKingCountdownCanvas.sortingLayerID =
            iconRenderer.sortingLayerID;
        _darkKingCountdownCanvas.sortingOrder =
            iconRenderer.sortingOrder + 1;
    }

    public void SetAsWild(Sprite wildSprite)
    {
        cardType = CardType.WildCard;
        _isUnassignedWild = true;
        SetIcon(wildSprite);
    }

    public bool BindWild(CardType type, Sprite cardSprite)
    {
        if (!_isUnassignedWild)
            return false;

        _isUnassignedWild = false;
        cardType = type;
        SetIcon(cardSprite);
        EffectController?.PlayActivate();
        return true;
    }

    public void PlaySpawnEffect() => EffectController?.PlaySpawn();

    public void PlayMoveEffect() => EffectController?.PlayMove();

    public void PlayActivateEffect() => EffectController?.PlayActivate();

    public void PlayDestroyEffect() => EffectController?.PlayDestroy();

    public void SetSelected(bool selected, float delay = 0f, bool animate = true)
    {
        StopSelectAnimation();

        bool wasSelected = _isSelected;
        _isSelected = selected;

        if (selected != wasSelected)
            EffectController?.PlaySelected(selected);

        if (highlightOutline != null)
        {
            if (selected)
            {
                highlightOutline.SetActive(true);

                if (iconRenderer != null && _outlineRenderer != null)
                {
                    _outlineRenderer.sortingOrder = iconRenderer.sortingOrder - 1;
                }

                if (_outlineRenderer != null)
                {
                    // CardConfig is the single source of truth for the
                    // selection glow. Do not brighten or mix it with white,
                    // otherwise the configured card colour is washed out.
                    Color glowColor = GetThemeColor(cardType);

                    StopGlow();
                    _glowRoutine = StartCoroutine(
                        GlowRoutine(
                            _outlineRenderer,
                            glowColor,
                            animate ? delay + selectDuration : 0f
                        )
                    );
                }
            }
            else if (animate && _outlineRenderer != null &&
                     highlightOutline.activeSelf && isActiveAndEnabled)
            {
                StopGlow();
                _glowRoutine = StartCoroutine(
                    GlowFadeOutRoutine(_outlineRenderer)
                );
            }
            else
            {
                StopGlow();
                highlightOutline.SetActive(false);
            }
        }

        if (DynamicShadow != null) {
            DynamicShadow.SetShadowEnabled(selected);
        }

        if (!animate || (!selected && !wasSelected))
            return;

        Vector3 targetScale = Vector3.one * (selected ? selectedScale : 1f);
        Vector3 targetPosition = new Vector3(
            0f,
            selected ? selectLift : 0f,
            0f
        );

        if (!isActiveAndEnabled)
        {
            transform.localScale = targetScale;
            transform.localPosition = targetPosition;
            return;
        }

        _selectRoutine = StartCoroutine(
            SelectRoutine(selected, targetScale, targetPosition, delay)
        );
    }

    public void StopSelectAnimation()
    {
        if (_selectRoutine == null)
            return;

        StopCoroutine(_selectRoutine);
        _selectRoutine = null;
    }

    private void StopGlow()
    {
        if (_glowRoutine == null)
            return;

        StopCoroutine(_glowRoutine);
        _glowRoutine = null;
    }

    private IEnumerator SelectRoutine(
        bool selected,
        Vector3 targetScale,
        Vector3 targetPosition,
        float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Transform t = transform;
        Vector3 fromScale = t.localScale;
        Vector3 fromPosition = t.localPosition;
        targetPosition.z = fromPosition.z;

        float duration = selected ? selectDuration : deselectDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);

            float eased = EaseOutQuad(p);

            t.localScale = Vector3.LerpUnclamped(fromScale, targetScale, eased);
            t.localPosition = Vector3.LerpUnclamped(
                fromPosition,
                targetPosition,
                eased
            );

            yield return null;
        }

        t.localScale = targetScale;
        t.localPosition = targetPosition;
        _selectRoutine = null;
    }

    private static float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    public void SetTemporarySortIndex(int index) {
        if (!_isTemporarySortIndex) _temporarySortIndex = iconRenderer.sortingOrder;
        
        iconRenderer.sortingOrder = index;
        UpdateDarkKingCountdownSorting();
        _isTemporarySortIndex = true;
    }

    public void RestoreSortIndex() {
        if (_isTemporarySortIndex) {
            iconRenderer.sortingOrder = _temporarySortIndex;
            UpdateDarkKingCountdownSorting();
            _isTemporarySortIndex = false;
        }
    }

    private IEnumerator GlowRoutine(
        SpriteRenderer outlineRenderer,
        Color baseColor,
        float delay)
    {
        Color hidden = baseColor;
        hidden.a = 0f;
        outlineRenderer.color = hidden;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.deltaTime;

            float strength;
            if (elapsed < glowFadeInDuration)
            {
                strength = EaseOutQuad(elapsed / glowFadeInDuration);
            }
            else
            {
                // Keep the selected outline alive by fading smoothly between
                // its sustain and peak strengths for as long as it is active.
                // Starting at one avoids a visible jump after the fade-in.
                float pulse = Mathf.PingPong(
                    (elapsed - glowFadeInDuration) /
                    Mathf.Max(0.01f, glowDecayTime) + 1f,
                    1f
                );
                strength = Mathf.Lerp(glowSustain, 1f, pulse);
            }

            Color c = baseColor;
            c.a = glowPeakAlpha * strength;
            outlineRenderer.color = c;

            yield return null;
        }
    }

    private IEnumerator GlowFadeOutRoutine(SpriteRenderer outlineRenderer)
    {
        Color from = outlineRenderer.color;
        float elapsed = 0f;

        while (elapsed < glowFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            Color c = from;
            c.a = Mathf.Lerp(
                from.a,
                0f,
                Mathf.Clamp01(elapsed / glowFadeOutDuration)
            );
            outlineRenderer.color = c;
            yield return null;
        }

        highlightOutline.SetActive(false);
        _glowRoutine = null;
    }

    public static Color GetThemeColor(CardType type)
    {
        if (_themeConfig != null &&
            _themeConfig.TryGetGlowColor(type, out Color configuredColor))
        {
            return configuredColor;
        }

        Color baseColor = type switch
        {
            CardType.Card1 => new Color32(38, 148, 235, 255),
            CardType.Card2 => new Color32(235, 41, 122, 255),
            CardType.Card3 => new Color32(50, 186, 38, 255),
            CardType.Card4 => new Color32(115, 41, 186, 255),
            CardType.Card5 => new Color32(20, 168, 145, 255),
            CardType.Card6 => new Color32(168, 51, 38, 255),
            CardType.Card7 => new Color32(51, 51, 186, 255),
            CardType.Card8 => new Color32(235, 115, 20, 255),
            CardType.Card9 => new Color32(92, 92, 77, 255),
            CardType.Card10 => new Color32(235, 20, 20, 255),
            CardType.Card11 => new Color32(92, 102, 122, 255),
            CardType.Card12 => new Color32(186, 77, 215, 255),
            CardType.Card13 => new Color32(20, 102, 51, 255),
            CardType.Card14 => new Color32(235, 148, 38, 255),
            CardType.Card15 => new Color32(148, 168, 38, 255),
            CardType.Card16 => new Color32(235, 102, 122, 255),
            CardType.Card17 => new Color32(115, 133, 168, 255),
            CardType.Card18 => new Color32(168, 122, 38, 255),
            CardType.Card19 => new Color32(51, 133, 77, 255),
            CardType.Card20 => new Color32(168, 61, 102, 255),

            _ => Color.white
        };

 
        return baseColor;
    }

    public void SetSortingOrder(int order) {
        if (iconRenderer == null) return;
        iconRenderer.sortingOrder = order;
        if (_outlineRenderer != null) _outlineRenderer.sortingOrder = order - 1;
        if (dynamicShadow != null) dynamicShadow.SetSortingOrder(order - 2);
        UpdateDarkKingCountdownSorting();
    }

    public void AddSortingOrderOffset(int offset) {
        if (iconRenderer == null) return;
        
        iconRenderer.sortingOrder += offset;

        if (_outlineRenderer != null)
        {
            _outlineRenderer.sortingOrder = iconRenderer.sortingOrder - 1;
        }

        if (dynamicShadow != null) {
            dynamicShadow.SetSortingOrder(iconRenderer.sortingOrder - 2);
        }

        UpdateDarkKingCountdownSorting();
    }
    
    public void SwapSortingOrder(Card otherCard)
    {
        if (otherCard == null || otherCard.IconRenderer == null || iconRenderer == null)
            return;

        (iconRenderer.sortingOrder, otherCard.IconRenderer.sortingOrder) = (otherCard.IconRenderer.sortingOrder, iconRenderer.sortingOrder);

        if (_outlineRenderer != null)
        {
            _outlineRenderer.sortingOrder = iconRenderer.sortingOrder - 1;
        }
        
        if (dynamicShadow != null) {
            dynamicShadow.SetSortingOrder(iconRenderer.sortingOrder - 2);
        }

        UpdateDarkKingCountdownSorting();

        if (otherCard._outlineRenderer != null)
        {
            otherCard._outlineRenderer.sortingOrder = otherCard.IconRenderer.sortingOrder - 1;
        }
        
        if (otherCard.dynamicShadow != null) {
            otherCard.dynamicShadow.SetSortingOrder(otherCard.IconRenderer.sortingOrder - 2);
        }

        otherCard.UpdateDarkKingCountdownSorting();
    }
}

public static class CardFlightShadow
{
    public static SpriteRenderer Create(
        Card card,
        Transform parent,
        Sprite shadowSprite,
        float alpha,
        string objectName = "CardFlightShadow")
    {
        if (card == null)
            return null;

        SpriteRenderer sourceRenderer = card.GetComponent<SpriteRenderer>();
        if (sourceRenderer == null)
            return null;

        GameObject shadowObject = new GameObject(objectName);
        shadowObject.layer = card.gameObject.layer;
        shadowObject.transform.SetParent(parent, true);
        shadowObject.transform.position = card.transform.position;
        shadowObject.transform.rotation = card.transform.rotation;
        shadowObject.transform.localScale = card.transform.localScale;

        SpriteRenderer shadowRenderer =
            shadowObject.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = shadowSprite != null
            ? shadowSprite
            : sourceRenderer.sprite;
        shadowRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        shadowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        shadowRenderer.color = new Color(
            1f,
            1f,
            1f,
            Mathf.Clamp01(alpha)
        );

        return shadowRenderer;
    }

    public static void Update(
        SpriteRenderer shadowRenderer,
        Vector3 cardPosition,
        Vector3 groundPosition,
        float heightStrength,
        float cardRotationZ,
        Vector3 cardScale,
        float maxOffset,
        float rotationDelay = 0.035f,
        float scaleBoost = 0.08f)
    {
        if (shadowRenderer == null)
            return;

        float strength = Mathf.Clamp01(heightStrength);
        shadowRenderer.transform.position = Vector3.Lerp(
            groundPosition,
            cardPosition,
            0.2f
        ) + Vector3.down * (strength * Mathf.Max(0f, maxOffset));
        float rotationFollow = rotationDelay <= 0f
            ? 1f
            : 1f - Mathf.Exp(
                -Time.deltaTime / Mathf.Max(0.001f, rotationDelay)
            );
        float shadowRotation = Mathf.LerpAngle(
            shadowRenderer.transform.eulerAngles.z,
            cardRotationZ,
            rotationFollow
        );
        shadowRenderer.transform.rotation = Quaternion.Euler(
            0f,
            0f,
            shadowRotation
        );

        Vector3 shadowScale = cardScale;
        shadowScale.x += strength * scaleBoost;
        shadowScale.y += strength * scaleBoost;
        shadowScale.z = 1f;
        shadowRenderer.transform.localScale = shadowScale;
    }

    public static void Release(SpriteRenderer shadowRenderer)
    {
        if (shadowRenderer == null)
            return;

        shadowRenderer.enabled = false;
        Object.Destroy(shadowRenderer.gameObject);
    }
}
