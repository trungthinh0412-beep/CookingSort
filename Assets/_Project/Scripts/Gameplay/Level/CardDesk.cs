using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CustomTween;
using UnityEngine;
using Random = UnityEngine.Random;

public class CardDesk : MonoBehaviour
{
    private const float DominantValueFillThreshold = 0.5f;

    [SerializeField] private CardDeskConfig config;
    [SerializeField] private Transform spawnPoint;

    [Header("Custom Button Simulation")]
    [SerializeField, Range(0.5f, 1f)] private float pressScale = 0.9f;
    [SerializeField, Min(0.01f)] private float pressDuration = 0.2f;
    [SerializeField] private Ease pressEase = Ease.Default;
    [SerializeField, Min(0f)] private float dealDelay = 0.05f;

    [Header("Deal Animation")]
    [SerializeField, Min(0f)] private float deckRestoreLeadTime = 0.1f;

    [Header("Deal Shadow")]
    [SerializeField] private Sprite dealShadowSprite;
    [SerializeField, Range(0f, 1f)] private float dealShadowMaxAlpha = 0.72f;
    [SerializeField, Min(0f)] private float maxShadowOffset = 0.9f;
    [SerializeField, Range(0f, 0.15f)] private float shadowRotationDelay = 0.035f;

    private Level _level;
    private SpriteRenderer[] _deckCardRenderers;
    private bool[] _deckCardRendererStates;
    private readonly List<SpriteRenderer> _activeDealShadows = new List<SpriteRenderer>();
    private int _emptyBoardFallbackValue;

    public bool IsReceivingCards { get; private set; }
    public Sprite FlightShadowSprite => dealShadowSprite;
    public float FlightShadowMaxAlpha => dealShadowMaxAlpha;
    public float FlightShadowMaxOffset => maxShadowOffset;
    public float FlightShadowRotationDelay => shadowRotationDelay;

    private class CardAnimInfo
    {
        public Card card;
        public CardSlot targetSlot;
        public CardData data;
        public int sortingOffset;
    }

        private Vector3 _originalScale;
        private Tween? _pressTween;

    private void Awake()
    {
        _originalScale = transform.localScale;
        _level = GetComponentInParent<Level>();

        _deckCardRenderers = GetComponentsInChildren<SpriteRenderer>(true)
            .Where(renderer => renderer.transform.parent == transform)
            .ToArray();
        _deckCardRendererStates = _deckCardRenderers
            .Select(renderer => renderer.enabled)
            .ToArray();
    }

    private void OnDisable()
    {
        _ = SetDeckCardsVisibleAsync(true);
        CleanupDealShadows();
        _emptyBoardFallbackValue = 0;
        IsReceivingCards = false;
    }

                public void OnPointerDownDesk()
    {
        if (IsReceivingCards || config == null || _level == null)
            return;

        if (_pressTween.HasValue)
        {
            _pressTween.Value.Stop();
            _pressTween = null;
        }

        // 0.9 scale
        _pressTween = Tween.Scale(transform, transform.localScale, _originalScale * pressScale, pressDuration, pressEase);
        
        VibrationController.Instance?.HapticLight();
    }

    public void OnPointerUpDesk()
    {
        if (IsReceivingCards || config == null || _level == null)
        {
            BounceBack();
            return;
        }

        IsReceivingCards = true;
        BounceBack();
        
        // Wait briefly so the scale-up animation feels perfectly smooth before a potentially heavy spawn operation
        StartCoroutine(DelayedDistributeCards());
    }

    public bool TryAutoDealOnce(CardType lastBurnedCardType)
    {
        if (IsReceivingCards || config == null || _level == null ||
            !_level.HasAvailableDealSlots())
        {
            return false;
        }

        _emptyBoardFallbackValue = Card.IsNumberCardType(lastBurnedCardType)
            ? (int)lastBurnedCardType + 1
            : 0;
        IsReceivingCards = true;
        StartCoroutine(DelayedDistributeCards());
        return true;
    }

    private IEnumerator DelayedDistributeCards()
    {
                if (dealDelay > 0f) yield return new WaitForSeconds(dealDelay); // for visual smoothness
        GenerateAndDistributeCards();
    }

    public void CancelPress()
    {
        BounceBack();
    }

    private void BounceBack()
    {
        if (_pressTween.HasValue)
        {
            _pressTween.Value.Stop();
            _pressTween = null;
        }
        _pressTween = Tween.Scale(transform, transform.localScale, _originalScale, pressDuration, pressEase);
    }

    private void GenerateAndDistributeCards()
    {
        var capacityHolders = _level.CardSlotHolders
            .Where(IsCapacityHolder)
            .ToList();
        var holders = capacityHolders
            .Where(IsAvailableDealHolder)
            .ToList();
        int totalEmptySlots = holders.Sum(h => h.GetEmptySlotCount());

        if (totalEmptySlots == 0) {
            _emptyBoardFallbackValue = 0;
            IsReceivingCards = false;
            _level.OnBoardChanged?.Invoke();
            return;
        }

        if (!_level.ConsumeDeal()) {
            _emptyBoardFallbackValue = 0;
            IsReceivingCards = false;
            return;
        }

        int totalCapacity = capacityHolders.Sum(h => h.CardSlots.Count);
        int totalCapacityRemaining = capacityHolders.Sum(h => h.GetEmptySlotCount());
        int numCards = GetCapacityBasedDealCount(
            totalCapacityRemaining,
            totalCapacity
        );

        // A regular deal always leaves one free board slot. When only one
        // slot remains, the last deal is allowed to fill it as specified.
        int maxDealableCards = totalEmptySlots == 1
            ? 1
            : totalEmptySlots - 1;
        numCards = Mathf.Clamp(numCards, 1, maxDealableCards);

        CardSlotHolder intentionallySkippedHolder = null;
        if (holders.Count > 1 &&
            Random.value < config.leaveOneTrayEmptyChance)
        {
            intentionallySkippedHolder = holders[Random.Range(0, holders.Count)];
        }

        int maxDealValue = GetCurrentMaxBoardValue();
        int minDealValue = GetMinimumDealValue(maxDealValue);
        _emptyBoardFallbackValue = 0;
        bool hasConfiguredMinimum =
            _level != null &&
            _level.MinDealCardValue > 0 &&
            _level.MinDealCardValue == minDealValue;
        bool mustAddConfiguredMinimum =
            hasConfiguredMinimum &&
            !HasCurrentBoardValue(minDealValue);
        int regularDealMinValue = mustAddConfiguredMinimum
            ? GetRegularDealMinimum(minDealValue, maxDealValue)
            : minDealValue;
        bool protectMinimumFrequency =
            mustAddConfiguredMinimum;
        CardType? rangeExtensionType = GetRangeExtensionDealType(minDealValue);
        List<CardType> generatedCards =
            new List<CardType>(numCards);

        CardType configuredMinimumType =
            (CardType)(minDealValue - 1);
        if (mustAddConfiguredMinimum)
            generatedCards.Add(configuredMinimumType);

        if (rangeExtensionType.HasValue &&
            generatedCards.Count < numCards &&
            (!mustAddConfiguredMinimum ||
             rangeExtensionType.Value != configuredMinimumType))
        {
            generatedCards.Add(rangeExtensionType.Value);
        }

        for (int i = generatedCards.Count; i < numCards; i++) {
            generatedCards.Add(
                RollDealCardType(
                    regularDealMinValue,
                    maxDealValue
                )
            );
        }

        var groupedCards = generatedCards
                           .GroupBy(c => c)
                           .OrderByDescending(g =>
                               mustAddConfiguredMinimum &&
                               g.Key == configuredMinimumType)
                           .ThenByDescending(g =>
                               rangeExtensionType.HasValue &&
                               g.Key == rangeExtensionType.Value)
                           .ThenByDescending(g => g.Count())
                           .ToList();

        Dictionary<CardSlotHolder, int> holderDealtCount = new Dictionary<CardSlotHolder, int>();
        Dictionary<CardSlotHolder, int> holderDealLimits =
            BuildRandomTrayDealLimits(
                holders,
                numCards,
                intentionallySkippedHolder
            );
        Dictionary<CardSlotHolder, int> holderValueLimits = new Dictionary<CardSlotHolder, int>();
        HashSet<CardSlotHolder> affectedHolders = new HashSet<CardSlotHolder>();

        foreach (CardSlotHolder holder in holders)
        {
            if (holder != null)
            {
                holderValueLimits[holder] =
                    Random.value < config.allowThreeValuesPerTrayChance
                        ? 3
                        : 2;
            }
        }

        List<CardAnimInfo> spawnedCardsBatch = new List<CardAnimInfo>();
        foreach (var group in groupedCards) {
            CardType requestedType = group.Key;
            bool isRangeExtension =
                rangeExtensionType.HasValue &&
                requestedType == rangeExtensionType.Value;
            int remaining = group.Count();

            while (remaining > 0) {
                CardSlotHolder bestHolder = SelectDealHolder(
                    holders,
                    requestedType,
                    holderDealtCount,
                    holderDealLimits,
                    holderValueLimits,
                    minDealValue,
                    maxDealValue,
                    protectMinimumFrequency,
                    isRangeExtension,
                    out CardType typeToSpawn
                );

                if (bestHolder == null &&
                    !isRangeExtension)
                {
                    if (!TrySelectExistingValueFallback(
                            holders,
                            holderDealtCount,
                            holderDealLimits,
                            holderValueLimits,
                            minDealValue,
                            maxDealValue,
                            protectMinimumFrequency,
                            out bestHolder,
                            out typeToSpawn
                        ))
                    {
                        bestHolder = null;
                    }
                }

                if (bestHolder == null &&
                    !TrySelectGuaranteedDealFallback(
                        holders,
                        requestedType,
                        minDealValue,
                        maxDealValue,
                        out bestHolder,
                        out typeToSpawn
                    ))
                {
                    break;
                }

                // Deal one card per selection. Selection keeps filling a
                // high-capacity tray before moving to another tray.
                const int spawnAmount = 1;

                if (!holderDealtCount.ContainsKey(bestHolder))
                    holderDealtCount[bestHolder] = 0;
                holderDealtCount[bestHolder] += spawnAmount;
                affectedHolders.Add(bestHolder);

                List<CardSlot> targetSlots = bestHolder.GetEmptySlots(spawnAmount);
                Vector3 trayStartPos = spawnPoint != null
                    ? spawnPoint.position
                    : transform.position;

                for (int i = 0; i < spawnAmount; i++) {
                    CardSlot targetSlot = targetSlots[i];

                    Card spawnPrefab = _level.GetCardPrefab(typeToSpawn);
                    if (spawnPrefab == null)
                    {
                        Debug.LogError($"CardDesk: Missing prefab for {typeToSpawn}.");
                        continue;
                    }

                    Card newCard = Instantiate(spawnPrefab, trayStartPos, Quaternion.identity, _level.transform);
                    newCard.SetEnabledRenderer(true);
                    newCard.CardType = typeToSpawn;
                    var data = _level.CardConfig.GetCardData(typeToSpawn);

                    Card holderTopCard = bestHolder.GetTopCard();
                    if (holderTopCard != null && holderTopCard.IsUnassignedWild)
                    {
                        holderTopCard.BindWild(
                            typeToSpawn,
                            data != null ? data.icon : null
                        );
                    }

                    if (data != null)
                        newCard.SetIcon(data.icon);

                    newCard.PlaySpawnEffect();

                    targetSlot.Card = newCard;
                    bestHolder.NotifyCardsChanged();

                    int dealSequenceIndex = spawnedCardsBatch.Count;
                    int sortingOffset = 100 + dealSequenceIndex;
                    newCard.SetSortingOrder(sortingOffset);

                    spawnedCardsBatch.Add(new CardAnimInfo {
                        card = newCard,
                        targetSlot = targetSlot,
                        data = data,
                        sortingOffset = sortingOffset
                    });
                }

                remaining -= spawnAmount;
            }
        }

        if (spawnedCardsBatch.Count == 0) {
            IsReceivingCards = false;
            return;
        }

        SoundController.Instance?.PlayFX(SoundName.DealCard);

        InitialBoardDealAnimator dealAnimator = _level.InitialBoardDealAnimator;

                        if (dealAnimator == null) {
            FinishDeal(spawnedCardsBatch, affectedHolders);
            return;
        }

        spawnedCardsBatch.Sort(
            (left, right) => left.sortingOffset.CompareTo(right.sortingOffset)
        );

        foreach (CardSlotHolder holder in affectedHolders)
            holder.NormalizeSlotLayout();

        List<CardSlot> dealSlots = spawnedCardsBatch
            .Select(info => info.targetSlot)
            .ToList();

        _ = SetDeckCardsVisibleAsync(false, 0.1f);

        Tween.Delay(dealAnimator.GetFlightDuration(dealSlots.Count))
             .OnComplete(() =>
             {
                 _ = SetDeckCardsVisibleAsync(true, deckRestoreLeadTime);
             });

                        dealAnimator.PlayCards(
            dealSlots,
            () => FinishDeal(spawnedCardsBatch, affectedHolders)
        );
    }

        private void FinishDeal(
        List<CardAnimInfo> spawnedCardsBatch,
        HashSet<CardSlotHolder> affectedHolders)
    {
        IsReceivingCards = false;

        foreach (CardAnimInfo info in spawnedCardsBatch) {
            if (info.card && info.card.DynamicShadow) {
                info.card.DynamicShadow.SetShadowEnabled(false);
            }
        }

        bool startedAnyMerge = false;

        foreach (CardSlotHolder holder in affectedHolders) {
            holder.NormalizeSlotLayout();
            startedAnyMerge |= _level.TryMergeCards(holder);
        }

        if (!startedAnyMerge) {
            _level.OnBoardChanged?.Invoke();
        }
    }

    private void CleanupDealShadows()
    {
        foreach (SpriteRenderer shadowRenderer in _activeDealShadows)
        {
            if (shadowRenderer != null)
            {
                CardFlightShadow.Release(shadowRenderer);
            }
        }

        _activeDealShadows.Clear();
    }

    private async Task SetDeckCardsVisibleAsync(bool visible, float duration = 0f, CancellationToken cancellationToken = default)
    {
        if (_deckCardRenderers == null || _deckCardRendererStates == null || _deckCardRenderers.Length == 0)
            return;

        int cardCount = _deckCardRenderers.Length;
        int delayMs = duration > 0f ? Mathf.RoundToInt((duration / cardCount) * 1000f) : 0;

        for (int i = 0; i < cardCount; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            // Determine card index dynamically
            int cardIndex = visible ? i : (cardCount - 1 - i);

            SpriteRenderer renderer = _deckCardRenderers[cardIndex];
            if (renderer != null)
            {
                renderer.enabled = visible && _deckCardRendererStates[cardIndex];
            }

            if (delayMs > 0)
            {
                try
                {
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private static bool IsAvailableDealHolder(CardSlotHolder holder)
    {
        if (holder == null || !holder.gameObject.activeInHierarchy || holder.IsLocked ||
            holder.GetEmptySlotCount() <= 0)
        {
            return false;
        }

        Card topCard = holder.GetTopCard();
        return topCard == null || (!topCard.IsObstacleCard && !topCard.IsFrozen);
    }

    private static CardSlotHolder SelectDealHolder(
        List<CardSlotHolder> holders,
        CardType cardType,
        Dictionary<CardSlotHolder, int> holderDealtCount,
        Dictionary<CardSlotHolder, int> holderDealLimits,
        Dictionary<CardSlotHolder, int> holderValueLimits,
        int minimumDealValue,
        int highestBoardValue,
        bool protectMinimumFrequency,
        bool preserveRequestedType,
        out CardType selectedType)
    {
        CardSlotHolder bestHolder = null;
        CardType bestType = cardType;
        int bestDealLimit = int.MinValue;
        int bestDealtCount = int.MinValue;
        int equalBestCount = 0;

        foreach (CardSlotHolder holder in holders)
        {
            CardType alignedType = preserveRequestedType
                ? cardType
                : GetTrayAlignedDealType(
                    holder,
                    cardType,
                    minimumDealValue,
                    highestBoardValue,
                    protectMinimumFrequency
                );

            // Do not let tray alignment turn nine identical cards into a
            // completed stack. Keep the originally rolled value when it is
            // different; otherwise this holder is not eligible for the card.
            if (WouldDealTenthMatchingCard(holder, alignedType))
            {
                alignedType = cardType;

                if (WouldDealTenthMatchingCard(holder, alignedType))
                    continue;
            }

            if (!CanReceiveDealCard(holder, alignedType))
                continue;

            int valueLimit = holderValueLimits.TryGetValue(holder, out int maxValues)
                ? maxValues
                : 2;
            if (!preserveRequestedType &&
                !CanReceiveDealValue(holder, alignedType, valueLimit))
                continue;

            int dealtCount = holderDealtCount.TryGetValue(holder, out int count)
                ? count
                : 0;
            int dealLimit = holderDealLimits.TryGetValue(holder, out int limit)
                ? limit
                : 0;
            int emptySlotCount = holder.GetEmptySlotCount();

            if (emptySlotCount <= 0 || dealtCount >= dealLimit)
                continue;

            bool isBetter = dealLimit > bestDealLimit ||
                            (dealLimit == bestDealLimit &&
                             dealtCount > bestDealtCount);

            if (isBetter)
            {
                bestHolder = holder;
                bestType = alignedType;
                bestDealLimit = dealLimit;
                bestDealtCount = dealtCount;
                equalBestCount = 1;
            }
            else if (dealLimit == bestDealLimit &&
                     dealtCount == bestDealtCount)
            {
                equalBestCount++;

                if (Random.Range(0, equalBestCount) == 0)
                {
                    bestHolder = holder;
                    bestType = alignedType;
                }
            }
        }

        selectedType = bestType;
        return bestHolder;
    }

    private static bool WouldDealTenthMatchingCard(
        CardSlotHolder holder,
        CardType cardType)
    {
        if (holder == null || holder.CardSlots == null ||
            holder.GetEmptySlotCount() <= 0)
        {
            return false;
        }

        int matchingCardCount = 0;

        foreach (CardSlot slot in holder.CardSlots)
        {
            Card card = slot != null ? slot.Card : null;
            if (card != null && card.CardType == cardType)
                matchingCardCount++;
        }

        return matchingCardCount >= CardSlotHolder.MAX_SLOTS - 1;
    }

    private static bool TrySelectExistingValueFallback(
        List<CardSlotHolder> holders,
        Dictionary<CardSlotHolder, int> holderDealtCount,
        Dictionary<CardSlotHolder, int> holderDealLimits,
        Dictionary<CardSlotHolder, int> holderValueLimits,
        int minimumDealValue,
        int highestBoardValue,
        bool protectMinimumFrequency,
        out CardSlotHolder selectedHolder,
        out CardType selectedType)
    {
        List<CardType> existingTypes = new List<CardType>();

        foreach (CardSlotHolder holder in holders)
        {
            if (holder == null || holder.CardSlots == null)
                continue;

            foreach (CardSlot slot in holder.CardSlots)
            {
                Card card = slot != null ? slot.Card : null;
                if (card == null || !card.IsNumberCard ||
                    !IsWithinDealValueRange(
                        card.CardType,
                        minimumDealValue,
                        highestBoardValue
                    ) ||
                    (protectMinimumFrequency &&
                     (int)card.CardType + 1 == minimumDealValue) ||
                    existingTypes.Contains(card.CardType))
                {
                    continue;
                }

                existingTypes.Add(card.CardType);
            }
        }

        // Start at a random value so fallback does not systematically favor
        // the lowest or the first value found in the hierarchy.
        int startIndex = existingTypes.Count > 0
            ? Random.Range(0, existingTypes.Count)
            : 0;

        for (int offset = 0; offset < existingTypes.Count; offset++)
        {
            CardType candidateType = existingTypes[
                (startIndex + offset) % existingTypes.Count
            ];
            CardSlotHolder candidateHolder = SelectDealHolder(
                holders,
                candidateType,
                holderDealtCount,
                holderDealLimits,
                holderValueLimits,
                minimumDealValue,
                highestBoardValue,
                protectMinimumFrequency,
                false,
                out CardType alignedType
            );

            if (candidateHolder == null)
                continue;

            selectedHolder = candidateHolder;
            selectedType = alignedType;
            return true;
        }

        selectedHolder = null;
        selectedType = default;
        return false;
    }

    private static bool TrySelectGuaranteedDealFallback(
        List<CardSlotHolder> holders,
        CardType requestedType,
        int minimumDealValue,
        int highestBoardValue,
        out CardSlotHolder selectedHolder,
        out CardType selectedType)
    {
        List<CardType> preferredTypes = new List<CardType>();
        AddUniqueDealType(preferredTypes, requestedType);

        int highestCardValue = (int)CardType.Card20 + 1;
        int safeMinimum = Mathf.Clamp(
            minimumDealValue,
            1,
            highestCardValue
        );
        int safeMaximum = Mathf.Clamp(
            highestBoardValue,
            safeMinimum,
            highestCardValue
        );

        if (safeMinimum == safeMaximum)
        {
            AddUniqueDealType(
                preferredTypes,
                (CardType)(safeMinimum - 1)
            );
        }
        else
        {
            for (int value = safeMaximum - 1;
                 value >= safeMinimum;
                 value--)
            {
                AddUniqueDealType(
                    preferredTypes,
                    (CardType)(value - 1)
                );
            }
        }

        foreach (CardSlotHolder holder in holders)
        {
            if (holder == null || holder.CardSlots == null)
                continue;

            foreach (CardSlot slot in holder.CardSlots)
            {
                Card card = slot != null ? slot.Card : null;
                if (card != null && card.IsNumberCard)
                    AddUniqueDealType(preferredTypes, card.CardType);
            }
        }

        if (TrySelectFallbackHolder(
                holders,
                preferredTypes,
                false,
                out selectedHolder,
                out selectedType) ||
            TrySelectFallbackHolder(
                holders,
                preferredTypes,
                true,
                out selectedHolder,
                out selectedType))
        {
            return true;
        }

        // A special tray can reject every value in the normal deal range
        // (for example a high-value tray after all regular trays are full).
        // Search all numeric cards only as a final fallback so an unlocked,
        // empty tray never makes the deck appear unresponsive.
        List<CardType> allNumberTypes = new List<CardType>();
        for (int value = 1; value <= highestCardValue; value++)
            allNumberTypes.Add((CardType)(value - 1));

        return TrySelectFallbackHolder(
                   holders,
                   allNumberTypes,
                   false,
                   out selectedHolder,
                   out selectedType) ||
               TrySelectFallbackHolder(
                   holders,
                   allNumberTypes,
                   true,
                   out selectedHolder,
                   out selectedType);
    }

    private static bool TrySelectFallbackHolder(
        List<CardSlotHolder> holders,
        List<CardType> candidateTypes,
        bool allowTenthMatchingCard,
        out CardSlotHolder selectedHolder,
        out CardType selectedType)
    {
        selectedHolder = null;
        selectedType = default;

        if (holders == null || holders.Count == 0 ||
            candidateTypes == null || candidateTypes.Count == 0)
        {
            return false;
        }

        int holderStart = Random.Range(0, holders.Count);

        for (int holderOffset = 0;
             holderOffset < holders.Count;
             holderOffset++)
        {
            CardSlotHolder holder = holders[
                (holderStart + holderOffset) % holders.Count
            ];

            for (int typeOffset = 0;
                 typeOffset < candidateTypes.Count;
                 typeOffset++)
            {
                CardType candidateType = candidateTypes[typeOffset];

                if (!CanReceiveDealCard(holder, candidateType) ||
                    (!allowTenthMatchingCard &&
                     WouldDealTenthMatchingCard(holder, candidateType)))
                {
                    continue;
                }

                selectedHolder = holder;
                selectedType = candidateType;
                return true;
            }
        }

        return false;
    }

    private static void AddUniqueDealType(
        List<CardType> types,
        CardType type)
    {
        if (IsNumericCardType(type) && !types.Contains(type))
            types.Add(type);
    }

    private static CardType GetTrayAlignedDealType(
        CardSlotHolder holder,
        CardType requestedType,
        int minimumDealValue,
        int highestBoardValue,
        bool protectMinimumFrequency)
    {
        if (holder == null || holder.CardSlots == null ||
            holder.CardSlots.Count == 0)
        {
            return requestedType;
        }

        int occupiedSlotCount = 0;
        Dictionary<CardType, int> valueCounts =
            new Dictionary<CardType, int>();

        foreach (CardSlot slot in holder.CardSlots)
        {
            Card card = slot != null ? slot.Card : null;
            if (card == null)
                continue;

            occupiedSlotCount++;

            if (!IsNumericCardType(card.CardType))
                continue;

            valueCounts.TryGetValue(card.CardType, out int count);
            valueCounts[card.CardType] = count + 1;
        }

        if (occupiedSlotCount <=
            holder.CardSlots.Count * DominantValueFillThreshold ||
            valueCounts.Count == 0)
        {
            return requestedType;
        }

        int dominantCount = valueCounts.Values.Max();
        Card topCard = holder.GetTopCard();

        // Alignment may never bypass the board value rule. If the tray is
        // dominated by the current highest value, keep the already rolled
        // value, which is in [deal minimum, board maximum - 1].
        bool HasDominantLegalValue(CardType type)
        {
            bool isProtectedMinimum =
                protectMinimumFrequency &&
                (int)type + 1 == minimumDealValue &&
                type != requestedType;

            return valueCounts.TryGetValue(type, out int count) &&
                   count == dominantCount &&
                   !isProtectedMinimum &&
                   IsWithinDealValueRange(
                       type,
                       minimumDealValue,
                       highestBoardValue
                   );
        }

        // If multiple values have the same share, continue the exposed group
        // first. This avoids introducing another alternating run in the tray.
        if (topCard != null && HasDominantLegalValue(topCard.CardType))
        {
            return topCard.CardType;
        }

        if (HasDominantLegalValue(requestedType))
        {
            return requestedType;
        }

        foreach (KeyValuePair<CardType, int> valueCount in valueCounts)
        {
            bool isProtectedMinimum =
                protectMinimumFrequency &&
                (int)valueCount.Key + 1 == minimumDealValue &&
                valueCount.Key != requestedType;

            if (valueCount.Value == dominantCount &&
                !isProtectedMinimum &&
                IsWithinDealValueRange(
                    valueCount.Key,
                    minimumDealValue,
                    highestBoardValue
                ))
            {
                return valueCount.Key;
            }
        }

        return requestedType;
    }

    private static bool IsWithinDealValueRange(
        CardType cardType,
        int minimumDealValue,
        int highestBoardValue)
    {
        int cardValue = (int)cardType + 1;

        // Card1 is the only possible value when the whole board is at 1.
        return cardValue >= minimumDealValue &&
               (highestBoardValue <= 1 || cardValue < highestBoardValue);
    }

    private static bool CanReceiveDealValue(
        CardSlotHolder holder,
        CardType cardType,
        int valueLimit)
    {
        if (holder == null || !IsNumericCardType(cardType))
            return false;

        HashSet<CardType> distinctValues = new HashSet<CardType>();

        foreach (CardSlot slot in holder.CardSlots)
        {
            Card card = slot != null ? slot.Card : null;
            if (card != null && card.IsNumberCard)
                distinctValues.Add(card.CardType);
        }

        return distinctValues.Contains(cardType) ||
               distinctValues.Count < Mathf.Max(1, valueLimit);
    }

    private static bool IsNumericCardType(CardType cardType)
    {
        return cardType >= CardType.Card1 && cardType <= CardType.Card20;
    }

    private static int GetCapacityBasedDealCount(
        int remainingCapacity,
        int totalCapacity)
    {
        if (remainingCapacity <= 0 || totalCapacity <= 0)
            return 0;

        float remainingRatio = remainingCapacity / (float)totalCapacity;

        if (remainingRatio <= 0.2f)
            return Random.Range(6, 9);
        if (remainingRatio <= 0.4f)
            return Random.Range(8, 11);
        if (remainingRatio <= 0.6f)
            return Random.Range(10, 13);
        if (remainingRatio <= 0.8f)
            return Random.Range(12, 15);
        if (remainingRatio <= 0.9f)
            return Random.Range(14, 17);

        return Random.Range(16, 19);
    }

    private static int GetDynamicTrayDealLimit(int emptySlotCount)
    {
        if (emptySlotCount <= 0)
            return 0;
        if (emptySlotCount <= 2)
            return 1;
        if (emptySlotCount <= 4)
            return 2;
        if (emptySlotCount <= 8)
            return 3;

        return 4;
    }

    private static Dictionary<CardSlotHolder, int>
        BuildRandomTrayDealLimits(
            List<CardSlotHolder> holders,
            int cardCount,
            CardSlotHolder intentionallySkippedHolder)
    {
        Dictionary<CardSlotHolder, int> limits =
            new Dictionary<CardSlotHolder, int>();
        List<CardSlotHolder> shuffledHolders =
            new List<CardSlotHolder>();

        foreach (CardSlotHolder holder in holders)
        {
            if (holder == null)
                continue;

            limits[holder] = 0;

            if (holder != intentionallySkippedHolder &&
                GetDynamicTrayDealLimit(holder.GetEmptySlotCount()) > 0)
            {
                shuffledHolders.Add(holder);
            }
        }

        for (int i = shuffledHolders.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            CardSlotHolder temp = shuffledHolders[i];
            shuffledHolders[i] = shuffledHolders[swapIndex];
            shuffledHolders[swapIndex] = temp;
        }

        int remainingCapacity = shuffledHolders.Sum(holder =>
            GetDynamicTrayDealLimit(holder.GetEmptySlotCount())
        );
        int remainingCards = Mathf.Min(
            Mathf.Max(0, cardCount),
            remainingCapacity
        );

        foreach (CardSlotHolder holder in shuffledHolders)
        {
            int holderCapacity =
                GetDynamicTrayDealLimit(holder.GetEmptySlotCount());
            remainingCapacity -= holderCapacity;

            int minimum = Mathf.Max(
                0,
                remainingCards - remainingCapacity
            );
            int maximum = Mathf.Min(
                holderCapacity,
                remainingCards
            );
            int limit = minimum < maximum
                ? Random.Range(minimum, maximum + 1)
                : minimum;

            limits[holder] = limit;
            remainingCards -= limit;
        }

        return limits;
    }

    private static bool IsCapacityHolder(CardSlotHolder holder)
    {
        if (holder == null || !holder.gameObject.activeInHierarchy || holder.IsLocked)
            return false;

        Card topCard = holder.GetTopCard();
        return topCard == null || (!topCard.IsObstacleCard && !topCard.IsFrozen);
    }

    private static bool CanReceiveDealCard(
        CardSlotHolder holder,
        CardType cardType)
    {
        return IsAvailableDealHolder(holder) &&
               holder.CanReceiveCardsIgnoringTopType(cardType);
    }

    private int GetCurrentMaxBoardValue()
    {
        return GetCurrentBoardValueRange().y;
    }

    private bool HasCurrentBoardValue(int value)
    {
        foreach (CardSlotHolder holder in _level.CardSlotHolders)
        {
            if (holder == null || holder.CardSlots == null)
                continue;

            foreach (CardSlot slot in holder.CardSlots)
            {
                Card card = slot != null ? slot.Card : null;
                if (card != null && card.IsNumberCard &&
                    !card.IsUnassignedWild &&
                    (int)card.CardType + 1 == value)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private CardType? GetRangeExtensionDealType(int minimumDealValue)
    {
        HashSet<CardType> boardValues = new HashSet<CardType>();

        foreach (CardSlotHolder holder in _level.CardSlotHolders)
        {
            if (holder == null || holder.CardSlots == null)
                continue;

            foreach (CardSlot slot in holder.CardSlots)
            {
                Card card = slot != null ? slot.Card : null;
                if (card == null || card.IsUnassignedWild ||
                    !card.IsNumberCard)
                {
                    continue;
                }

                boardValues.Add(card.CardType);
            }
        }

        if (boardValues.Count != 2)
            return null;

        CardType lowestType = boardValues.Min();
        if (lowestType <= CardType.Card1)
            return null;

        CardType extensionType = (CardType)((int)lowestType - 1);
        return (int)extensionType + 1 >= minimumDealValue
            ? extensionType
            : null;
    }

    private Vector2Int GetCurrentBoardValueRange()
    {
        int minValue = int.MaxValue;
        int maxValue = 0;

        foreach (CardSlotHolder holder in _level.CardSlotHolders)
        {
            if (holder == null || holder.CardSlots == null)
                continue;

            // Inspect every slot, including cards below the top card.
            foreach (CardSlot slot in holder.CardSlots)
            {
                if (slot == null || slot.Card == null ||
                    slot.Card.IsUnassignedWild || !slot.Card.IsNumberCard)
                    continue;

                int value = (int)slot.Card.CardType + 1;
                minValue = Mathf.Min(minValue, value);
                maxValue = Mathf.Max(maxValue, value);
            }
        }

        int fallbackValue = Mathf.Clamp(
            _emptyBoardFallbackValue,
            1,
            (int)CardType.Card20 + 1
        );
        return maxValue == 0
            ? new Vector2Int(fallbackValue, fallbackValue)
            : new Vector2Int(minValue, maxValue);
    }

    private int GetMinimumDealValue(int maxValue)
    {
        int highestCardValue = (int)CardType.Card20 + 1;
        maxValue = Mathf.Clamp(maxValue, 1, highestCardValue);
        int maximumLegalMinimum = maxValue > 1 ? maxValue - 1 : 1;
        int configuredMinimum = _level != null
            ? _level.MinDealCardValue
            : 0;

        if (configuredMinimum > 0)
        {
            return Mathf.Clamp(
                configuredMinimum,
                1,
                maximumLegalMinimum
            );
        }

        return Mathf.Clamp(
            GetCurrentBoardValueRange().x,
            1,
            maxValue
        );
    }

    private int GetRegularDealMinimum(
        int minimumDealValue,
        int maximumDealValue)
    {
        int nextBoardValue = int.MaxValue;

        foreach (CardSlotHolder holder in _level.CardSlotHolders)
        {
            if (holder == null || holder.CardSlots == null)
                continue;

            foreach (CardSlot slot in holder.CardSlots)
            {
                Card card = slot != null ? slot.Card : null;
                if (card == null || card.IsUnassignedWild ||
                    !card.IsNumberCard)
                {
                    continue;
                }

                int value = (int)card.CardType + 1;
                if (value > minimumDealValue && value < nextBoardValue)
                    nextBoardValue = value;
            }
        }

        if (nextBoardValue != int.MaxValue)
        {
            return Mathf.Clamp(
                nextBoardValue,
                minimumDealValue,
                maximumDealValue
            );
        }

        return minimumDealValue;
    }

    private CardType RollDealCardType(
        int minValue,
        int maxValue)
    {
        int highestCardValue = (int)CardType.Card20 + 1;
        maxValue = Mathf.Clamp(maxValue, 1, highestCardValue);
        minValue = Mathf.Clamp(minValue, 1, maxValue);

        // A uniform board has no value in [min, max - 1].
        // Deal its existing value so merging can continue without regressing.
        if (minValue == maxValue)
            return (CardType)(minValue - 1);

        int lowerValueCount = maxValue - minValue;
        DealWeightProfile profile = config.GetDealWeightProfile(lowerValueCount);

        // Wider ranges and missing profiles still use the entire legal range.
        if (profile == null || profile.weights == null ||
            profile.weights.Count < lowerValueCount)
        {
            return (CardType)Random.Range(minValue - 1, maxValue - 1);
        }

        float totalWeight = 0f;
        for (int i = 0; i < lowerValueCount; i++)
            totalWeight += Mathf.Max(0f, profile.weights[i]);

        if (totalWeight <= 0f)
            return (CardType)Random.Range(minValue - 1, maxValue - 1);

        float randomWeight = Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;
        int lastPositiveValue = maxValue - 1;

        for (int i = 0; i < lowerValueCount; i++)
        {
            float weight = Mathf.Max(0f, profile.weights[i]);
            if (weight <= 0f)
                continue;

            int cardValue = maxValue - (i + 1);
            lastPositiveValue = cardValue;
            cumulativeWeight += weight;
            if (randomWeight < cumulativeWeight)
                return (CardType)(cardValue - 1);
        }

        return (CardType)(lastPositiveValue - 1);
    }
}
