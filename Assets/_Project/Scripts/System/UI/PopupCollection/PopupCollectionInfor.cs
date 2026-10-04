using System.Collections;
using System.Collections.Generic;
using CustomTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

[System.Serializable]
public sealed class CollectionFrameOverride
{
    [SerializeField] private string collectionId;
    [SerializeField] private string collectionName;
    [SerializeField] private Sprite frameSprite;

    public string CollectionId => collectionId;
    public Sprite FrameSprite => frameSprite;

    public void Sync(CollectionData collection)
    {
        collectionId = collection == null ? string.Empty : collection.Id;
        collectionName = collection == null ? string.Empty : collection.DisplayName;
    }
}

/// <summary>One data-driven Collection details popup; presentation never grants or claims progress.</summary>
public sealed class PopupCollectionInfor : Popup, IPointerDownHandler, IPointerUpHandler
{
    private const float SwipeThreshold = 120f;

    [Header("Collection")]
    [SerializeField] private CollectionConfig collectionConfig;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image coverImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text pageText;
    [SerializeField] private CustomButton previousButton;
    [SerializeField] private CustomButton nextButton;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private GameObject lockedStateRoot;
    [SerializeField] private TMP_Text lockedStateText;
    [SerializeField] private List<CollectionFrameOverride> collectionFrames =
        new List<CollectionFrameOverride>();

    [Header("Navigation")]
    [Min(0.01f)] [SerializeField] private float pageSlideDuration = 0.22f;
    [Min(0f)] [SerializeField] private float pageSlideGap = 24f;
    [SerializeField] private AnimationCurve pageSlideCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Cards")]
    [SerializeField] private RectTransform cardContent;
    [SerializeField] private CollectionCardItem cardItemPrefab;
    [SerializeField] private ScrollRect cardScrollRect;
    [SerializeField] private CollectionCardRewardPresenter receivePresenter;

    [Header("Reward")]
    [SerializeField] private GameObject rewardRoot;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text rewardStateText;
    [SerializeField] private CustomButton claimButton;
    [SerializeField] private TMP_Text claimButtonText;
    [SerializeField] private GameObject completeRoot;
    [SerializeField] private GameObject rewardClaimedRoot;

    [Header("Presentation")]
    [Min(0)] [SerializeField] private float completeDelay = 0.2f;
    [Min(0)] [SerializeField] private float completePulseDuration = 0.22f;
    [Min(0)] [SerializeField] private float newCardViewSeconds = 0.6f;
    [SerializeField] private ParticleSystem completeVfx;
    [SerializeField] private UnityEvent onCollectionCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onRewardClaimed = new UnityEvent();

    private readonly List<CollectionCardItem> _cardItems = new List<CollectionCardItem>();
    private readonly Dictionary<string, CollectionCardItem> _slots = new Dictionary<string, CollectionCardItem>();
    private readonly Queue<CardAddResult> _receives = new Queue<CardAddResult>();
    private readonly HashSet<string> _viewedNewCards = new HashSet<string>();
    private readonly Dictionary<string, float> _viewTimes = new Dictionary<string, float>();
    private readonly Dictionary<RectTransform, Vector2> _stationaryNavigationPositions =
        new Dictionary<RectTransform, Vector2>();
    private CollectionData _selectedCollection;
    private PlayerData _renderedPlayer;
    private Coroutine _presentation;
    private Tween _rewardTween;
    private int _displayedOwned;
    private bool _completeVisible;
    private bool _acknowledging;
    private string _revealingCardId;
    private int _revealingQuantity;
    private int _selectedCollectionIndex = -1;
    private Vector2 _pointerDownPosition;
    private bool _hasPointerDownPosition;
    private Sprite _defaultFrameSprite;
    private Coroutine _pageSlide;
    private GameObject _pageSlideClone;
    private Vector2 _slideRootBasePosition;
    private bool _hasSlideRootBasePosition;

    public CollectionData SelectedCollection => _selectedCollection;
    public bool IsPresenting { get; private set; }
    public bool IsPageSliding => _pageSlide != null;
    private bool CanView => _selectedCollection != null && CollectionManager.IsCollectionUnlocked(_selectedCollection.Id);

    public void Setup(CollectionData data)
    {
        SelectCollection(data, true);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureManager();
        BindClaim();
        BindNavigation();
        Observer.CollectionChanged += OnProgressChanged;
        Observer.CollectionCardAdded += OnCardAdded;
        Observer.LevelChanged += OnLevelChanged;
        Refresh();
        ResetScroll();
    }

    protected override void OnDisable()
    {
        Observer.CollectionChanged -= OnProgressChanged;
        Observer.CollectionCardAdded -= OnCardAdded;
        Observer.LevelChanged -= OnLevelChanged;
        if (claimButton != null)
            claimButton.Click.RemoveListener(OnClaimReward);
        UnbindNavigation();
        StopPageSlide();
        AcknowledgeViewedCards();
        CancelPresentation();
        base.OnDisable();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        EnsureManager();
        BindClaim();
        BindNavigation();
        Refresh();
    }

    protected override void BeforeHide()
    {
        AcknowledgeViewedCards();
        CancelPresentation();
        base.BeforeHide();
    }

    private void EnsureManager()
    {
        if (collectionConfig != null && CollectionManager.Config != collectionConfig)
            CollectionManager.Initialize(collectionConfig);
    }

    private void BindClaim()
    {
        if (claimButton == null)
            return;
        claimButton.Click.RemoveListener(OnClaimReward);
        claimButton.Click.AddListener(OnClaimReward);
    }

    private void BindNavigation()
    {
        ResolveNavigationReferences();

        if (previousButton != null)
        {
            previousButton.Click.RemoveListener(OnPreviousCollection);
            previousButton.Click.AddListener(OnPreviousCollection);
        }

        if (nextButton != null)
        {
            nextButton.Click.RemoveListener(OnNextCollection);
            nextButton.Click.AddListener(OnNextCollection);
        }
    }

    private void UnbindNavigation()
    {
        if (previousButton != null)
            previousButton.Click.RemoveListener(OnPreviousCollection);

        if (nextButton != null)
            nextButton.Click.RemoveListener(OnNextCollection);
    }

    private void ResolveNavigationReferences()
    {
        if (pageText != null && previousButton != null && nextButton != null)
            return;

        CustomButton[] buttons = GetComponentsInChildren<CustomButton>(true);
        foreach (CustomButton button in buttons)
        {
            if (button == null || button == claimButton)
                continue;

            RectTransform rect = button.transform as RectTransform;
            if (rect == null || rect.anchoredPosition.y > -500f)
                continue;

            if (rect.anchoredPosition.x < 0f && previousButton == null)
                previousButton = button;
            else if (rect.anchoredPosition.x > 0f && nextButton == null)
                nextButton = button;
        }

        if (pageText != null)
            return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text == null || text == progressText || text == titleText ||
                text == lockedStateText || text == rewardText ||
                text == rewardStateText || text == claimButtonText)
            {
                continue;
            }

            RectTransform rect = text.transform as RectTransform;
            if (rect != null && rect.anchoredPosition.y < -500f)
            {
                pageText = text;
                return;
            }
        }
    }

    public void Refresh()
    {
        if (IsPresenting || IsPageSliding)
            return;

        RefreshView();
    }

    private void RefreshView()
    {
        _renderedPlayer = Data.PlayerData;
        RefreshHeader();
        RefreshCards();
        _displayedOwned = _selectedCollection == null ? 0 :
            CollectionManager.GetOwnedUniqueCardCount(_selectedCollection.Id);
        _completeVisible = CanView && CollectionManager.IsCollectionComplete(_selectedCollection.Id);
        RefreshProgress();
        RefreshPage();
        RefreshReward();
    }

    private void RefreshHeader()
    {
        bool accessible = CanView;
        if (titleText != null)
            titleText.text = _selectedCollection == null ? string.Empty : _selectedCollection.DisplayName;
        if (frameImage != null)
        {
            if (_defaultFrameSprite == null)
                _defaultFrameSprite = frameImage.sprite;

            Sprite frameSprite = GetFrameSprite(_selectedCollection);
            frameImage.sprite = frameSprite != null
                ? frameSprite
                : _defaultFrameSprite;
        }
        if (coverImage != null)
        {
            coverImage.sprite = _selectedCollection == null ? null : _selectedCollection.CoverSprite;
            coverImage.enabled = accessible && coverImage.sprite != null;
        }
        if (lockedStateRoot != null)
            lockedStateRoot.SetActive(_selectedCollection != null && !accessible);
        if (lockedStateText != null)
        {
            int level = collectionConfig == null ? 39 : collectionConfig.UnlockLevel;
            if (_selectedCollection != null)
                level = Mathf.Max(level, _selectedCollection.UnlockLevel);
            lockedStateText.text = $"Reach Level {level} to unlock this Collection!";
        }
        if (cardContent != null)
            cardContent.gameObject.SetActive(accessible);
        if (emptyState != null)
            emptyState.SetActive(_selectedCollection == null || (accessible && _selectedCollection.TotalCards == 0));
    }

    private void RefreshCards()
    {
        _slots.Clear();
        int count = 0;
        if (CanView && _selectedCollection.Cards != null && cardContent != null && cardItemPrefab != null)
        {
            foreach (CollectionCardData card in _selectedCollection.Cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.Id) || _slots.ContainsKey(card.Id))
                    continue;
                if (count == _cardItems.Count)
                    _cardItems.Add(Instantiate(cardItemPrefab, cardContent));
                CollectionCardItem item = _cardItems[count++];
                item.Setup(card, CollectionManager.GetCardProgress(card.Id));
                item.gameObject.SetActive(true);
                _slots.Add(card.Id, item);
            }
        }
        for (int i = count; i < _cardItems.Count; i++)
            _cardItems[i].Clear();
    }

    private void RefreshProgress()
    {
        int total = _selectedCollection == null ? 0 : _selectedCollection.TotalCards;
        if (progressText != null)
            progressText.text = _selectedCollection != null && !CanView ? string.Empty : $"{_displayedOwned}/{total}";
        if (progressFill != null)
            progressFill.fillAmount = !CanView || total == 0 ? 0f : (float)_displayedOwned / total;
    }

    private void RefreshPage()
    {
        int total = GetCollectionCount();
        if (pageText != null)
        {
            int page = total == 0 ? 0 : Mathf.Clamp(_selectedCollectionIndex + 1, 1, total);
            pageText.text = $"{page}/{total}";
        }

        bool canNavigate = total > 1;
        if (previousButton != null)
            previousButton.Interactable = canNavigate && !IsPageSliding;
        if (nextButton != null)
            nextButton.Interactable = canNavigate && !IsPageSliding;
    }

    private void RefreshReward()
    {
        bool accessible = CanView;
        bool hasReward = accessible && _selectedCollection.HasReward;
        RewardData reward = _selectedCollection == null ? null : _selectedCollection.Reward;
        CollectionRewardState state = _selectedCollection == null ? CollectionRewardState.Locked :
            CollectionManager.GetRewardState(_selectedCollection.Id);
        bool claimed = state == CollectionRewardState.Claimed;
        if (rewardRoot != null)
            rewardRoot.SetActive(hasReward);
        if (rewardText != null)
            rewardText.text = !hasReward ? string.Empty :
                reward.totalStar == 0 ? $"{reward.totalGold} Coins" :
                reward.totalGold == 0 ? $"{reward.totalStar} Stars" :
                $"{reward.totalGold} Coins  +  {reward.totalStar} Stars";
        if (rewardStateText != null)
            rewardStateText.text = claimed ? "Reward claimed" :
                state == CollectionRewardState.ReadyToClaim && !IsPresenting ? "Ready to claim" : "Collect all cards";
        if (completeRoot != null)
            completeRoot.SetActive(accessible && _completeVisible);
        if (rewardClaimedRoot != null)
            rewardClaimedRoot.SetActive(accessible && claimed);
        if (claimButton != null)
            claimButton.Interactable = hasReward && state == CollectionRewardState.ReadyToClaim && !IsPresenting;
        if (claimButtonText != null)
            claimButtonText.text = claimed ? "Claimed" : "Claim";
    }

    private void OnProgressChanged()
    {
        if (_acknowledging)
            return;
        if (!ReferenceEquals(_renderedPlayer, Data.PlayerData))
        {
            _viewedNewCards.Clear();
            _viewTimes.Clear();
            CancelPresentation();
        }
        if (IsPresenting)
        {
            bool invalidated = !CanView || (_revealingCardId != null &&
                CollectionManager.GetCardProgress(_revealingCardId).Quantity < _revealingQuantity);
            foreach (CardAddResult pending in _receives)
                invalidated |= CollectionManager.GetCardProgress(pending.CardId).Quantity < pending.NewQuantity;
            if (invalidated)
                CancelPresentation();
        }
        if (!IsPresenting)
            Refresh();
    }

    private void OnLevelChanged(int level)
    {
        CancelPresentation();
        Refresh();
    }

    private void OnCardAdded(CardAddResult result)
    {
        if (!result.Success || !isActiveAndEnabled || !CanView ||
            _selectedCollection.Id != result.CollectionId)
            return;
        if (!Application.isPlaying)
        {
            Refresh();
            return;
        }
        _receives.Enqueue(result);
        if (IsPresenting)
            return;
        IsPresenting = true;
        RefreshReward();
        _presentation = StartCoroutine(PresentReceives());
    }

    private IEnumerator PresentReceives()
    {
        while (_receives.Count > 0)
        {
            CardAddResult result = _receives.Dequeue();
            _revealingCardId = result.CardId;
            _revealingQuantity = result.NewQuantity;
            _slots.TryGetValue(result.CardId, out CollectionCardItem item);
            if (receivePresenter != null)
            {
                RectTransform target = TryGetCardTarget(result.CardId, out RectTransform visible) ? visible : null;
                yield return receivePresenter.Present(result, target);
            }
            if (item != null)
                yield return item.PlayReceive(result);
            _revealingCardId = null;
            _displayedOwned = result.OwnedUniqueCardCount;
            RefreshProgress();

            if (result.CollectionCompletedNow)
            {
                if (completeDelay > 0)
                    yield return new WaitForSecondsRealtime(completeDelay);
                _completeVisible = true;
                RefreshReward();
                if (completeVfx != null)
                    completeVfx.Play();
                onCollectionCompleted?.Invoke();
                if (rewardRoot != null && rewardRoot.activeInHierarchy && Application.isPlaying)
                {
                    _rewardTween = Tween.Scale(rewardRoot.transform, 1.06f, completePulseDuration,
                        Ease.OutBack, useUnscaledTime: true);
                    yield return _rewardTween.ToYieldInstruction();
                    _rewardTween = Tween.Scale(rewardRoot.transform, 1f, completePulseDuration, useUnscaledTime: true);
                    yield return _rewardTween.ToYieldInstruction();
                }
            }
        }
        _presentation = null;
        IsPresenting = false;
        Refresh(); // Reconcile live quantities/NEW flags after any queued results.
    }

    private void CancelPresentation()
    {
        if (_presentation != null)
            StopCoroutine(_presentation);
        _presentation = null;
        _receives.Clear();
        _revealingCardId = null;
        IsPresenting = false;
        if (_rewardTween.isAlive)
            _rewardTween.Stop();
        if (rewardRoot != null)
            rewardRoot.transform.localScale = Vector3.one;
        if (receivePresenter != null)
            receivePresenter.Cancel();
        foreach (CollectionCardItem item in _cardItems)
            if (item != null)
                item.CancelPresentation();
        if (completeVfx != null)
            completeVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void LateUpdate()
    {
        if (!CanView || IsHiding || (CanvasGroup != null && CanvasGroup.alpha < 0.95f))
            return;
        foreach (KeyValuePair<string, CollectionCardItem> pair in _slots)
        {
            if (!pair.Value.Progress.IsNew || _viewedNewCards.Contains(pair.Key))
                continue;
            bool pending = pair.Key == _revealingCardId;
            foreach (CardAddResult receive in _receives)
                pending |= receive.CardId == pair.Key;
            if (pending || !IsVisible(pair.Value.RectTransform))
            {
                _viewTimes[pair.Key] = 0;
                continue;
            }
            _viewTimes.TryGetValue(pair.Key, out float time);
            time += Time.unscaledDeltaTime;
            _viewTimes[pair.Key] = time;
            if (time >= newCardViewSeconds)
                _viewedNewCards.Add(pair.Key);
        }
    }

    private bool IsVisible(RectTransform item)
    {
        if (!isActiveAndEnabled || !item.gameObject.activeInHierarchy)
            return false;
        RectTransform viewport = cardScrollRect == null ? cardContent : cardScrollRect.viewport;
        if (viewport == null)
            return true;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, item);
        var cardRect = new Rect((Vector2)bounds.min, (Vector2)bounds.size);
        return viewport.rect.Overlaps(cardRect);
    }

    public bool TryGetCardTarget(string cardId, out RectTransform target)
    {
        target = null;
        if (CanView && _slots.TryGetValue(cardId, out CollectionCardItem item) && IsVisible(item.RectTransform))
            target = item.RectTransform;
        return target != null;
    }

    private void AcknowledgeViewedCards()
    {
        var ids = new List<string>(_viewedNewCards);
        _viewedNewCards.Clear();
        _viewTimes.Clear();
        if (!ReferenceEquals(_renderedPlayer, Data.PlayerData) || ids.Count == 0)
            return;
        _acknowledging = true;
        try { CollectionManager.MarkCardsSeen(ids); }
        finally { _acknowledging = false; }
    }

    private void ResetScroll()
    {
        if (cardContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardContent);
        if (cardScrollRect != null)
        {
            cardScrollRect.StopMovement();
            cardScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void OnPreviousCollection()
    {
        SelectCollectionByOffset(-1);
    }

    public void OnNextCollection()
    {
        SelectCollectionByOffset(1);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        _pointerDownPosition = eventData.position;
        _hasPointerDownPosition = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_hasPointerDownPosition || eventData == null)
            return;

        _hasPointerDownPosition = false;
        Vector2 delta = eventData.position - _pointerDownPosition;

        if (Mathf.Abs(delta.x) < SwipeThreshold ||
            Mathf.Abs(delta.x) < Mathf.Abs(delta.y))
        {
            return;
        }

        if (delta.x < 0f)
            OnNextCollection();
        else
            OnPreviousCollection();
    }

    private void SelectCollectionByOffset(int offset)
    {
        if (IsPageSliding)
            return;

        int count = GetCollectionCount();
        if (count <= 1)
            return;

        int currentIndex = Mathf.Clamp(_selectedCollectionIndex, 0, count - 1);
        int nextIndex = (currentIndex + offset + count) % count;

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        CollectionData nextCollection = GetCollectionAt(nextIndex);
        if (isActiveAndEnabled && Application.isPlaying)
            _pageSlide = StartCoroutine(SlideToCollection(nextCollection, offset));
        else
            SelectCollection(nextCollection, true);
    }

    private IEnumerator SlideToCollection(CollectionData collection, int offset)
    {
        RectTransform slideRoot = GetPageSlideRoot();
        if (slideRoot == null)
        {
            SelectCollection(collection, true);
            _pageSlide = null;
            yield break;
        }

        CaptureSlideRootBasePosition(slideRoot);
        CaptureStationaryNavigation(slideRoot);
        RefreshPage();

        float slideDistance = GetPageSlideDistance(slideRoot);
        float direction = offset >= 0 ? 1f : -1f;
        Vector2 exitPosition = _slideRootBasePosition + Vector2.left * direction * slideDistance;
        Vector2 enterPosition = _slideRootBasePosition + Vector2.right * direction * slideDistance;

        RectTransform outgoingSlideRoot = CreateOutgoingSlideRoot(slideRoot);

        SelectCollection(collection, true);
        slideRoot.anchoredPosition = enterPosition;
        UpdateStationaryNavigation(slideRoot);

        yield return AnimateSlideRoots(
            outgoingSlideRoot,
            _slideRootBasePosition,
            exitPosition,
            slideRoot,
            enterPosition,
            _slideRootBasePosition);

        slideRoot.anchoredPosition = _slideRootBasePosition;
        UpdateStationaryNavigation(slideRoot);
        ClearPageSlideClone();
        _stationaryNavigationPositions.Clear();
        _pageSlide = null;
        RefreshView();
    }

    private IEnumerator AnimateSlideRoots(
        RectTransform outgoingTarget,
        Vector2 outgoingFrom,
        Vector2 outgoingTo,
        RectTransform incomingTarget,
        Vector2 incomingFrom,
        Vector2 incomingTo)
    {
        float duration = Mathf.Max(0.01f, pageSlideDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = pageSlideCurve == null ? t : pageSlideCurve.Evaluate(t);
            if (outgoingTarget != null)
                outgoingTarget.anchoredPosition =
                    Vector2.LerpUnclamped(outgoingFrom, outgoingTo, eased);
            incomingTarget.anchoredPosition =
                Vector2.LerpUnclamped(incomingFrom, incomingTo, eased);
            UpdateStationaryNavigation(incomingTarget);
            yield return null;
        }

        if (outgoingTarget != null)
            outgoingTarget.anchoredPosition = outgoingTo;
        incomingTarget.anchoredPosition = incomingTo;
        UpdateStationaryNavigation(incomingTarget);
    }

    private void StopPageSlide()
    {
        if (_pageSlide != null)
        {
            StopCoroutine(_pageSlide);
            _pageSlide = null;
        }

        ClearPageSlideClone();

        RectTransform slideRoot = GetPageSlideRoot();
        if (slideRoot != null && _hasSlideRootBasePosition)
        {
            slideRoot.anchoredPosition = _slideRootBasePosition;
            UpdateStationaryNavigation(slideRoot);
        }

        _stationaryNavigationPositions.Clear();
    }

    private void CaptureSlideRootBasePosition(RectTransform slideRoot)
    {
        if (_hasSlideRootBasePosition || slideRoot == null)
            return;

        _slideRootBasePosition = slideRoot.anchoredPosition;
        _hasSlideRootBasePosition = true;
    }

    private RectTransform GetPageSlideRoot()
    {
        return frameImage != null
            ? frameImage.rectTransform
            : container;
    }

    private float GetPageSlideDistance(RectTransform slideRoot)
    {
        return Mathf.Max(1f, slideRoot.rect.width) + pageSlideGap;
    }

    private RectTransform CreateOutgoingSlideRoot(RectTransform slideRoot)
    {
        ClearPageSlideClone();

        if (slideRoot == null || slideRoot.parent == null)
            return null;

        _pageSlideClone = Instantiate(slideRoot.gameObject, slideRoot.parent);
        _pageSlideClone.name = $"{slideRoot.gameObject.name}_SlideClone";

        RectTransform cloneRect = _pageSlideClone.transform as RectTransform;
        if (cloneRect == null)
        {
            ClearPageSlideClone();
            return null;
        }

        cloneRect.SetSiblingIndex(slideRoot.GetSiblingIndex());
        cloneRect.anchoredPosition = _slideRootBasePosition;
        HideCloneNavigation(cloneRect, slideRoot);
        return cloneRect;
    }

    private void ClearPageSlideClone()
    {
        if (_pageSlideClone == null)
            return;

        Destroy(_pageSlideClone);
        _pageSlideClone = null;
    }

    private void HideCloneNavigation(
        RectTransform cloneRoot,
        RectTransform originalRoot)
    {
        HideCloneTarget(
            cloneRoot,
            originalRoot,
            previousButton == null ? null : previousButton.transform);
        HideCloneTarget(
            cloneRoot,
            originalRoot,
            nextButton == null ? null : nextButton.transform);
        HideCloneTarget(cloneRoot, originalRoot, GetPageTextRoot(originalRoot));
    }

    private void HideCloneTarget(
        RectTransform cloneRoot,
        RectTransform originalRoot,
        Transform originalTarget)
    {
        if (cloneRoot == null || originalRoot == null ||
            originalTarget == null || !originalTarget.IsChildOf(originalRoot))
        {
            return;
        }

        string path = GetRelativePath(originalRoot, originalTarget);
        if (string.IsNullOrEmpty(path))
            return;

        Transform cloneTarget = cloneRoot.Find(path);
        if (cloneTarget != null)
            cloneTarget.gameObject.SetActive(false);
    }

    private static string GetRelativePath(
        Transform root,
        Transform target)
    {
        if (root == null || target == null || target == root)
            return string.Empty;

        var names = new Stack<string>();
        Transform current = target;

        while (current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return current == root ? string.Join("/", names.ToArray()) : string.Empty;
    }

    private void UpdateStationaryNavigation(RectTransform slideRoot)
    {
        if (slideRoot == null || !_hasSlideRootBasePosition)
            return;

        Vector2 offset = slideRoot.anchoredPosition - _slideRootBasePosition;
        CounterMoveIfChild(previousButton == null ? null : previousButton.transform as RectTransform, slideRoot, offset);
        CounterMoveIfChild(nextButton == null ? null : nextButton.transform as RectTransform, slideRoot, offset);
        CounterMoveIfChild(GetPageTextRoot(slideRoot), slideRoot, offset);
    }

    private RectTransform GetPageTextRoot(RectTransform slideRoot)
    {
        if (pageText == null)
            return null;

        RectTransform textRect = pageText.transform as RectTransform;
        RectTransform parentRect = pageText.transform.parent as RectTransform;

        return parentRect != null && parentRect != slideRoot
            ? parentRect
            : textRect;
    }

    private void CaptureStationaryNavigation(RectTransform slideRoot)
    {
        _stationaryNavigationPositions.Clear();
        CaptureStationaryNavigationRoot(
            previousButton == null ? null : previousButton.transform as RectTransform,
            slideRoot);
        CaptureStationaryNavigationRoot(
            nextButton == null ? null : nextButton.transform as RectTransform,
            slideRoot);
        CaptureStationaryNavigationRoot(GetPageTextRoot(slideRoot), slideRoot);
    }

    private void CaptureStationaryNavigationRoot(
        RectTransform target,
        RectTransform slideRoot)
    {
        if (target == null || slideRoot == null || target == slideRoot ||
            !target.IsChildOf(slideRoot) ||
            _stationaryNavigationPositions.ContainsKey(target))
        {
            return;
        }

        _stationaryNavigationPositions.Add(target, target.anchoredPosition);
    }

    private void CounterMoveIfChild(
        RectTransform target,
        RectTransform slideRoot,
        Vector2 offset)
    {
        if (target == null || slideRoot == null || target == slideRoot ||
            !target.IsChildOf(slideRoot))
        {
            return;
        }

        if (_stationaryNavigationPositions.TryGetValue(target, out Vector2 basePosition))
            target.anchoredPosition = basePosition - offset;
    }

    private void SelectCollection(CollectionData data, bool resetScroll)
    {
        AcknowledgeViewedCards();
        CancelPresentation();
        EnsureManager();

        _selectedCollection = ResolveCollection(data);
        _selectedCollectionIndex = GetCollectionIndex(_selectedCollection);

        RefreshView();

        if (resetScroll)
            ResetScroll();
    }

    private CollectionData ResolveCollection(CollectionData data)
    {
        if (collectionConfig == null)
            return null;

        CollectionData collection =
            data == null ? null : collectionConfig.GetCollection(data.Id);

        if (collection != null)
            return collection;

        return GetCollectionAt(0);
    }

    private int GetCollectionCount()
    {
        return collectionConfig == null || collectionConfig.Collections == null
            ? 0
            : collectionConfig.Collections.Count;
    }

    private CollectionData GetCollectionAt(int index)
    {
        if (collectionConfig == null || collectionConfig.Collections == null ||
            index < 0 || index >= collectionConfig.Collections.Count)
        {
            return null;
        }

        return collectionConfig.Collections[index];
    }

    private Sprite GetFrameSprite(CollectionData collection)
    {
        if (collection == null || collectionFrames == null)
            return null;

        foreach (CollectionFrameOverride frameOverride in collectionFrames)
        {
            if (frameOverride != null &&
                string.Equals(
                    frameOverride.CollectionId,
                    collection.Id,
                    System.StringComparison.Ordinal))
            {
                return frameOverride.FrameSprite;
            }
        }

        return null;
    }

    private int GetCollectionIndex(CollectionData collection)
    {
        if (collection == null || collectionConfig == null ||
            collectionConfig.Collections == null)
        {
            return -1;
        }

        for (int i = 0; i < collectionConfig.Collections.Count; i++)
        {
            CollectionData candidate = collectionConfig.Collections[i];
            if (candidate != null &&
                string.Equals(candidate.Id, collection.Id, System.StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void OnValidate()
    {
        SyncCollectionFrames();
    }

    [ContextMenu("Sync Collection Frames")]
    private void SyncCollectionFrames()
    {
        if (collectionFrames == null)
            collectionFrames = new List<CollectionFrameOverride>();

        if (collectionConfig == null || collectionConfig.Collections == null)
            return;

        var syncedFrames = new List<CollectionFrameOverride>();

        foreach (CollectionData collection in collectionConfig.Collections)
        {
            if (collection == null)
                continue;

            CollectionFrameOverride frameOverride =
                FindFrameOverride(collection.Id) ?? new CollectionFrameOverride();

            frameOverride.Sync(collection);
            syncedFrames.Add(frameOverride);
        }

        collectionFrames = syncedFrames;
    }

    private CollectionFrameOverride FindFrameOverride(string collectionId)
    {
        if (string.IsNullOrWhiteSpace(collectionId) || collectionFrames == null)
            return null;

        foreach (CollectionFrameOverride frameOverride in collectionFrames)
        {
            if (frameOverride != null &&
                string.Equals(
                    frameOverride.CollectionId,
                    collectionId,
                    System.StringComparison.Ordinal))
            {
                return frameOverride;
            }
        }

        return null;
    }

    public void OnClaimReward()
    {
        if (IsPresenting || !CanView ||
            CollectionManager.GetRewardState(_selectedCollection.Id) != CollectionRewardState.ReadyToClaim)
            return;
        // Existing resource handlers use this position for their currency flight effect.
        if (rewardRoot != null)
            Observer.SpawnResourcesChanged?.Invoke(rewardRoot.transform.position);
        if (!CollectionManager.TryClaimReward(_selectedCollection.Id))
            return;
        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);
        RefreshReward();
        onRewardClaimed?.Invoke();
    }

    public void OnClose()
    {
        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);
        AfterHiddenAction = RestoreCollectionAfterClose;
        Hide(PopupAnimation.None);
    }

    private void RestoreCollectionAfterClose()
    {
        PopupController controller = PopupController.Instance;
        if (controller == null)
            return;
        if (controller.Get<PopupCollection>() is PopupCollection)
            controller.Show<PopupCollection>(PopupAnimation.None);
        else
            controller.SetBottomBarVisible(true);
    }
}
