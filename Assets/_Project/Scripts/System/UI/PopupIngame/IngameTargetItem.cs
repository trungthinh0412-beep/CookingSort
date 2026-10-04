using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class IngameTargetItem : MonoBehaviour
{
    [SerializeField] private Image cardIcon;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Image tickTarget;

    [Header("Target Count Animation")]
    [SerializeField, Range(1f, 2f)] private float countPopScale = 1.5f;
    [SerializeField, Range(0.5f, 1f)] private float countUndershootScale = 0.9f;
    [SerializeField, Min(0.01f)] private float countPopDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float countUndershootDuration = 0.07f;
    [SerializeField, Min(0.01f)] private float countSettleDuration = 0.07f;
    [SerializeField, Range(1f, 1.6f)] private float tickPeakScale = 1.3f;
    [SerializeField, Range(0.2f, 0.8f)] private float tickPeakTimeRatio = 0.55f;

    [Header("Target Card Flip")]
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField, Range(0.25f, 1f)] private float flipToBackPopRatio = 0.667f;
    [SerializeField, Min(0f)] private float cardBackHoldDuration = 0.06f;
    [SerializeField, Min(0.05f)] private float flipBackToFrontDuration = 0.07f;
    [SerializeField, Range(0f, 0.2f)] private float cardFlipBendStretch = 0.06f;
    [SerializeField, Range(0f, 12f)] private float cardFlipLeanAngle = 4f;
    [SerializeField] private AnimationCurve numberCardFlipCurve =
        CardDealSettings.BuildCurve(
            new[] { 0f, 0.15f, 0.3f, 0.5f, 0.7f, 0.85f, 1f },
            new[] { 0f, 0.06f, 0.22f, 0.5f, 0.78f, 0.94f, 1f }
        );

    private CardTarget _target;
    private RectTransform _itemTransform;
    private Vector3 _itemBaseScale = Vector3.one;
    private RectTransform _countTransform;
    private Vector3 _countBaseScale = Vector3.one;
    private RectTransform _tickTransform;
    private Vector3 _tickBaseScale = Vector3.one;
    private RectTransform _flipVisualTransform;
    private Image _flipVisualImage;
    private Coroutine _countAnimation;
    private int _displayedRemaining;
    private bool _hasDisplayedRemaining;
    private bool _applyFirstProgressImmediately;
    private Sprite _frontSprite;
    private bool _cardFlipActive;
    private bool _cardIconWasEnabled;
    private bool _revealTickAtUndershoot;
    private bool _tickRevealed;
    private Sprite _defaultTickSprite;

    private void Awake()
    {
        CacheItemTransform();
        CacheCountTransform();
        CacheTickTransform();
        EnsureCardFlipVisual();

        if (tickTarget != null)
            _defaultTickSprite = tickTarget.sprite;
    }

    private void OnDisable()
    {
        StopCountAnimation();
        ResetCountScale();
    }

    public void Setup(CardTarget target, Sprite targetSprite)
    {
        StopCountAnimation();
        CacheItemTransform();
        CacheCountTransform();
        CacheTickTransform();
        ResetCountScale();

        _target = target;

        // Item duoc pool lai, nen luon gan ca null de khong giu icon
        // cua target truoc neu config hien tai chua co sprite.
        if (cardIcon != null)
        {
            cardIcon.sprite = targetSprite;
            cardIcon.preserveAspect = true;
            _frontSprite = targetSprite;
        }

        if (tickTarget != null)
        {
            tickTarget.raycastTarget = false;
            tickTarget.sprite = _defaultTickSprite;
        }

        int initialRemaining = target != null ? Mathf.Max(0, target.count) : 0;
        SetRemainingImmediate(initialRemaining);
        _applyFirstProgressImmediately = true;
    }

    /// <summary>
    /// Uses the target item's icon area as a static success/failure marker.
    /// PopupContinue uses this to show the final target state without a count.
    /// </summary>
    public void ShowStatusIcon(Sprite statusIcon)
    {
        StopCountAnimation();
        ResetCountScale();

        if (countText != null)
            countText.gameObject.SetActive(false);

        if (tickTarget == null)
            return;

        tickTarget.sprite = statusIcon != null
            ? statusIcon
            : _defaultTickSprite;
        tickTarget.preserveAspect = true;
        tickTarget.gameObject.SetActive(true);
    }

    public void SetDisplayScale(float scale)
    {
        CacheItemTransform();
        _itemBaseScale = Vector3.one * Mathf.Max(0.01f, scale);
        if (_itemTransform != null)
            _itemTransform.localScale = _itemBaseScale;
    }

    public void UpdateProgress(Dictionary<CardType, int> currentCounts)
    {
        if (_target == null || countText == null)
            return;

        int current = !_target.IsCompleted && currentCounts != null &&
                      currentCounts.TryGetValue(_target.cardType, out int count)
            ? count
            : _target.IsCompleted
                ? _target.count
                : 0;

        int remaining = Mathf.Max(
            0,
            _target.count - current
        );

        if (_applyFirstProgressImmediately)
        {
            _applyFirstProgressImmediately = false;
            SetRemainingImmediate(remaining);
            return;
        }

        if (!_hasDisplayedRemaining)
        {
            SetRemainingImmediate(remaining);
            return;
        }

        if (remaining == _displayedRemaining)
            return;

        bool gainedTargetProgress = remaining < _displayedRemaining;
        _displayedRemaining = remaining;
        _revealTickAtUndershoot = remaining == 0;
        _tickRevealed = false;

        if (!_revealTickAtUndershoot)
        {
            ShowCountText();
            countText.text = remaining.ToString();
        }

        if (!gainedTargetProgress || !isActiveAndEnabled)
        {
            StopCountAnimation();
            ResetCountScale();
            return;
        }

        StopCountAnimation();
        ResetCountScale();
        _countAnimation = StartCoroutine(PlayCountAnimation());
    }

    private IEnumerator PlayCountAnimation()
    {
        CacheCountTransform();
        EnsureCardFlipVisual();
        if (_itemTransform == null)
            yield break;

        PrepareCardFlip();

        float totalDuration = GetTotalAnimationDuration();
        float tickRevealTime = GetTickRevealTime();
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float itemScale = EvaluateCountScale(elapsed);
            _itemTransform.localScale = _itemBaseScale * itemScale;
            ApplyCardFlipPose(elapsed);

            if (_revealTickAtUndershoot && !_tickRevealed &&
                elapsed >= tickRevealTime)
            {
                ShowTickTarget();
            }

            if (_tickRevealed)
                ApplyTickScale(elapsed, tickRevealTime, itemScale);

            yield return null;
        }

        if (_revealTickAtUndershoot && !_tickRevealed)
            ShowTickTarget();

        ResetCountScale();
        _countAnimation = null;
    }

    private float EvaluateCountScale(float elapsed)
    {
        float pop = Mathf.Max(0.01f, countPopDuration);
        float peakHold = Mathf.Max(0f, cardBackHoldDuration) +
                         Mathf.Max(0.05f, flipBackToFrontDuration);
        float undershoot = Mathf.Max(0.01f, countUndershootDuration);
        float settle = Mathf.Max(0.01f, countSettleDuration);

        if (elapsed < pop)
        {
            float progress = Mathf.Clamp01(elapsed / pop);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            return Mathf.LerpUnclamped(1f, countPopScale, eased);
        }

        elapsed -= pop;
        if (elapsed < peakHold)
            return countPopScale;

        elapsed -= peakHold;
        if (elapsed < undershoot)
        {
            float progress = Mathf.Clamp01(elapsed / undershoot);
            return Mathf.LerpUnclamped(
                countPopScale,
                countUndershootScale,
                progress * progress
            );
        }

        elapsed -= undershoot;
        if (elapsed < settle)
        {
            float progress = Mathf.Clamp01(elapsed / settle);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            return Mathf.LerpUnclamped(countUndershootScale, 1f, eased);
        }

        return 1f;
    }

    private void PrepareCardFlip()
    {
        if (cardIcon == null || _flipVisualImage == null)
            return;

        if (cardIcon.sprite != null && cardIcon.sprite != cardBackSprite)
            _frontSprite = cardIcon.sprite;

        _cardIconWasEnabled = cardIcon.enabled;
        _cardFlipActive = true;
        CopyCardIconAppearance();
        _flipVisualImage.sprite = _frontSprite;
        _flipVisualTransform.localScale = Vector3.one;
        _flipVisualTransform.localRotation = Quaternion.identity;
        _flipVisualImage.gameObject.SetActive(true);
        cardIcon.enabled = false;
    }

    private void ApplyCardFlipPose(float elapsed)
    {
        if (!_cardFlipActive || _flipVisualTransform == null ||
            _flipVisualImage == null)
        {
            return;
        }

        float pop = Mathf.Max(0.01f, countPopDuration);
        float flipToBackDuration = Mathf.Max(
            0.05f,
            pop * Mathf.Clamp(flipToBackPopRatio, 0.25f, 1f)
        );
        float holdEnd = pop + Mathf.Max(0f, cardBackHoldDuration);
        float flipToFrontDuration = Mathf.Max(
            0.05f,
            flipBackToFrontDuration
        );

        float yaw;
        if (elapsed < flipToBackDuration)
        {
            float turn = EvaluateNumberCardFlip(elapsed / flipToBackDuration);
            yaw = 180f * turn;
        }
        else if (elapsed < holdEnd)
        {
            yaw = 180f;
        }
        else if (elapsed < holdEnd + flipToFrontDuration)
        {
            float turn = EvaluateNumberCardFlip(
                (elapsed - holdEnd) / flipToFrontDuration
            );
            yaw = Mathf.LerpUnclamped(180f, 360f, turn);
        }
        else
        {
            yaw = 360f;
        }

        float radians = yaw * Mathf.Deg2Rad;
        float edge = Mathf.Abs(Mathf.Cos(radians));
        float bend = Mathf.Abs(Mathf.Sin(radians));

        _flipVisualTransform.localScale = new Vector3(
            Mathf.Max(0.015f, edge),
            1f + cardFlipBendStretch * bend,
            1f
        );
        _flipVisualTransform.localRotation = Quaternion.Euler(
            0f,
            0f,
            cardFlipLeanAngle * Mathf.Sin(radians)
        );

        bool showBack = yaw >= 90f && yaw < 270f;
        _flipVisualImage.sprite = showBack && cardBackSprite != null
            ? cardBackSprite
            : _frontSprite;
    }

    private float EvaluateNumberCardFlip(float progress)
    {
        progress = Mathf.Clamp01(progress);
        return numberCardFlipCurve != null && numberCardFlipCurve.length > 0
            ? Mathf.Clamp01(numberCardFlipCurve.Evaluate(progress))
            : Mathf.SmoothStep(0f, 1f, progress);
    }

    private float GetTotalAnimationDuration()
    {
        return Mathf.Max(0.01f, countPopDuration) +
               Mathf.Max(0f, cardBackHoldDuration) +
               Mathf.Max(0.05f, flipBackToFrontDuration) +
               Mathf.Max(0.01f, countUndershootDuration) +
               Mathf.Max(0.01f, countSettleDuration);
    }

    private float GetTickRevealTime()
    {
        return Mathf.Max(0.01f, countPopDuration) +
               Mathf.Max(0f, cardBackHoldDuration) +
               Mathf.Max(0.05f, flipBackToFrontDuration) +
               Mathf.Max(0.01f, countUndershootDuration);
    }

    private void SetRemainingImmediate(int remaining)
    {
        _displayedRemaining = Mathf.Max(0, remaining);
        _hasDisplayedRemaining = true;
        _revealTickAtUndershoot = false;
        _tickRevealed = _displayedRemaining == 0;

        if (_displayedRemaining == 0)
        {
            ShowTickTarget();
        }
        else if (countText != null)
        {
            ShowCountText();
            countText.text = _displayedRemaining.ToString();
        }

        ResetCountScale();
    }

    private void ShowCountText()
    {
        if (countText != null)
            countText.gameObject.SetActive(true);

        if (tickTarget != null)
            tickTarget.gameObject.SetActive(false);
    }

    private void ShowTickTarget()
    {
        _tickRevealed = true;

        if (countText != null)
            countText.gameObject.SetActive(false);

        if (tickTarget != null)
            tickTarget.gameObject.SetActive(true);
    }

    private void ApplyTickScale(
        float elapsed,
        float revealTime,
        float itemScale)
    {
        if (_tickTransform == null)
            return;

        float duration = Mathf.Max(0.01f, countSettleDuration);
        float progress = Mathf.Clamp01((elapsed - revealTime) / duration);
        float peakTime = Mathf.Clamp(tickPeakTimeRatio, 0.2f, 0.8f);
        float visibleScale;

        if (progress < peakTime)
        {
            float phase = Mathf.Clamp01(progress / peakTime);
            float eased = 1f - Mathf.Pow(1f - phase, 3f);
            visibleScale = Mathf.LerpUnclamped(
                countUndershootScale,
                tickPeakScale,
                eased
            );
        }
        else
        {
            float phase = Mathf.Clamp01(
                (progress - peakTime) / Mathf.Max(0.01f, 1f - peakTime)
            );
            float eased = phase * phase * (3f - 2f * phase);
            visibleScale = Mathf.LerpUnclamped(tickPeakScale, 1f, eased);
        }

        float localCompensation = visibleScale / Mathf.Max(0.01f, itemScale);
        _tickTransform.localScale = _tickBaseScale * localCompensation;
    }

    private void CacheCountTransform()
    {
        if (countText == null || _countTransform != null)
            return;

        _countTransform = countText.rectTransform;
        _countBaseScale = _countTransform.localScale;
    }

    private void CacheItemTransform()
    {
        if (_itemTransform != null)
            return;

        _itemTransform = transform as RectTransform;
        if (_itemTransform != null)
            _itemBaseScale = _itemTransform.localScale;
    }

    private void CacheTickTransform()
    {
        if (tickTarget == null || _tickTransform != null)
            return;

        _tickTransform = tickTarget.rectTransform;
        _tickBaseScale = _tickTransform.localScale;

        if (_tickBaseScale.sqrMagnitude < 0.0001f)
        {
            _tickBaseScale = Vector3.one;
            _tickTransform.localScale = _tickBaseScale;
        }
    }

    private void EnsureCardFlipVisual()
    {
        if (_flipVisualImage != null || cardIcon == null ||
            _itemTransform == null)
        {
            return;
        }

        GameObject flipObject = new GameObject(
            "CardFlipVisual",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        flipObject.layer = gameObject.layer;

        _flipVisualTransform = flipObject.GetComponent<RectTransform>();
        _flipVisualTransform.SetParent(_itemTransform, false);
        _flipVisualTransform.SetAsFirstSibling();
        _flipVisualTransform.anchorMin = Vector2.zero;
        _flipVisualTransform.anchorMax = Vector2.one;
        _flipVisualTransform.offsetMin = Vector2.zero;
        _flipVisualTransform.offsetMax = Vector2.zero;
        _flipVisualTransform.pivot = new Vector2(0.5f, 0.5f);

        _flipVisualImage = flipObject.GetComponent<Image>();
        _flipVisualImage.raycastTarget = false;
        _flipVisualImage.preserveAspect = true;

        Shadow[] sourceEffects = cardIcon.GetComponents<Shadow>();
        foreach (Shadow source in sourceEffects)
        {
            Shadow copy = source is Outline
                ? flipObject.AddComponent<Outline>()
                : flipObject.AddComponent<Shadow>();
            copy.effectColor = source.effectColor;
            copy.effectDistance = source.effectDistance;
            copy.useGraphicAlpha = source.useGraphicAlpha;
        }

        CopyCardIconAppearance();
        flipObject.SetActive(false);
    }

    private void CopyCardIconAppearance()
    {
        if (cardIcon == null || _flipVisualImage == null)
            return;

        _flipVisualImage.color = cardIcon.color;
        _flipVisualImage.material = cardIcon.material;
        _flipVisualImage.type = cardIcon.type;
        _flipVisualImage.fillCenter = cardIcon.fillCenter;
        _flipVisualImage.pixelsPerUnitMultiplier =
            cardIcon.pixelsPerUnitMultiplier;
    }

    private void StopCountAnimation()
    {
        if (_countAnimation == null)
            return;

        StopCoroutine(_countAnimation);
        _countAnimation = null;
    }

    private void ResetCountScale()
    {
        if (_itemTransform != null)
            _itemTransform.localScale = _itemBaseScale;

        if (_countTransform != null)
            _countTransform.localScale = _countBaseScale;

        if (_tickTransform != null)
            _tickTransform.localScale = _tickBaseScale;

        if (_flipVisualTransform != null)
        {
            _flipVisualTransform.localScale = Vector3.one;
            _flipVisualTransform.localRotation = Quaternion.identity;
        }

        if (_flipVisualImage != null)
            _flipVisualImage.gameObject.SetActive(false);

        if (_cardFlipActive && cardIcon != null)
            cardIcon.enabled = _cardIconWasEnabled;

        _cardFlipActive = false;

        if (cardIcon != null && _frontSprite != null)
            cardIcon.sprite = _frontSprite;
    }
}
