using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InitialBoardDealAnimator : MonoBehaviour
{
    public event Action DealCompleted;

    [Header("Initial Deal Animation")]
    [SerializeField] private bool playOnLevelStart = true;
    [SerializeField] private CardDealSettings deal = new CardDealSettings();
    [SerializeField] private Transform dealOrigin;
    [SerializeField, Min(1)] private int inFlightSortingOffset = 1000;
    [SerializeField, Min(1)] private int inFlightSortingStep = 10;

    [Header("Return To Desk Animation")]
    [SerializeField, Range(0.5f, 2f)] private float returnFlightDurationMultiplier = 0.675f;
    [SerializeField, Range(0.5f, 2f)] private float returnIntervalMultiplier = 0.675f;
    [SerializeField, Range(0.75f, 1f)] private float returnDeckDropScale = 0.88f;
    [SerializeField, Min(0.05f)] private float returnDeckDropDuration = 0.16f;
    [SerializeField, Min(0.05f)] private float returnDeckRestoreDuration = 0.28f;

    private sealed class TrayDealState
    {
        public readonly List<CardSlot> Slots;
        public int NextCardIndex;

        public TrayDealState(List<CardSlot> slots)
        {
            Slots = slots;
        }

        public bool HasNext => NextCardIndex < Slots.Count;
    }

    private sealed class CardInfo
    {
        public Card Card;
        public CardSlot TargetSlot;
        public SpriteRenderer[] Renderers;
        public bool[] RendererStates;
        public int[] FinalSortingOrders;
        public Vector3 FinalLocalPosition;
        public Quaternion FinalLocalRotation;
        public Vector3 FinalLocalScale;
        public int FinalSiblingIndex;
        public Vector3 StartWorldPosition;
        public Vector3 FinalWorldPosition;
        public Quaternion FinalWorldRotation;
        public Vector3 FinalWorldScale;
        public Sprite FaceSprite;
        public Sprite BackSprite;
        public CardBendSurface Bend;
        public Vector3 ChordDirection;
        public float ChordLength;
        public float SpinSign;
        public float Delay;
        public int SequenceIndex;
        public bool Launched;
        public bool FaceRevealed;
        public bool Landed;
        public bool Settled;
        public bool Completed;
    }

    private sealed class ReturnCardInfo
    {
        public Card Card;
        public Vector3 StartPosition;
        public Quaternion StartRotation;
        public Vector3 StartScale;
        public float Delay;
        public float SpinSign;
    }

    private readonly List<CardInfo> _cards = new List<CardInfo>();
    private Level _level;
    private Transform _resolvedDealOrigin;
    private Transform _flightLayer;
    private Transform _deckReactionTarget;
    private Vector3 _deckBaseLocalScale;
    private float _deckReactionImpulse;
    private Coroutine _routine;
    private bool _prepared;
    private bool _hasPlayed;
    private bool _hasDeckBaseScale;
    private bool _dealCompletedRaised;
    private Action _externalDealCompleted;

    public bool IsBusy => _prepared || _routine != null;

    public float GetFlightDuration(int cardCount)
    {
        return deal.startDelay +
               Mathf.Max(0, cardCount - 1) *
               Mathf.Max(0.005f, deal.dealInterval) +
               deal.flightDuration;
    }

    public void Initialize(Level level)
    {
        if (_level != level)
        {
            StopAndRestore();
            _hasPlayed = false;
            _dealCompletedRaised = false;
        }

        _level = level;
        CacheDealOrigin();
        EnsureFlightLayer();
        CacheDeckReactionTarget();
    }

    private void OnDisable()
    {
        StopAndRestore();
    }

    public void Prepare()
    {
        StopAndRestore();

        if (_hasPlayed || !playOnLevelStart || _level == null ||
            _level.CardSlotHolders == null)
        {
            return;
        }

        CacheDealOrigin();
        EnsureFlightLayer();
        CacheDeckReactionTarget();

        List<CardSlotHolder> holders = _level.CardSlotHolders
            .Where(holder => holder != null)
            .ToList();
        holders.Sort(CompareTrayDealOrder);

        List<TrayDealState> trays = new List<TrayDealState>(holders.Count);
        for (int holderIndex = 0; holderIndex < holders.Count; holderIndex++)
        {
            CardSlotHolder holder = holders[holderIndex];
            // Level.Awake can prepare the deal before a child holder's Awake
            // has run. Normalize here as well so the captured landing
            // transforms and sorting orders can never come from stale prefab
            // or previous animation state.
            holder.NormalizeSlotLayout();

            List<CardSlot> occupiedSlots = holder.CardSlots != null
                ? holder.CardSlots
                    .Where(slot => slot != null && slot.Card != null)
                    .ToList()
                : new List<CardSlot>();

            for (int slotIndex = 0;
                 slotIndex < occupiedSlots.Count;
                 slotIndex++)
            {
                SnapCardToSlot(
                    occupiedSlots[slotIndex].Card,
                    occupiedSlots[slotIndex]
                );
            }
            trays.Add(new TrayDealState(occupiedSlots));
        }

        BuildPingPongDealOrder(trays);
        _prepared = _cards.Count > 0;
    }

    public void Play()
    {
        if (_hasPlayed)
            return;

        if (!_prepared)
            Prepare();

        if (_routine != null)
            return;

        if (!_prepared || _cards.Count == 0)
        {
            _hasPlayed = true;
            RaiseDealCompleted();
            return;
        }

        CacheDealOrigin();
        EnsureFlightLayer();
        CacheDeckReactionTarget();

        // Pre-level booster visuals may finish changing scale while the
        // bottom bar enters. Capture the exact authored gameplay result now.
        for (int i = 0; i < _cards.Count; i++)
        {
            CaptureFinalTransform(_cards[i]);
            CaptureFinalSorting(_cards[i]);
        }

        RefreshMotionMetrics();
        _hasPlayed = true;
        _routine = StartCoroutine(Animate(playInitialDealSound: true));
    }

    /// <summary>
    /// Plays the same deal animation used for the authored starting board
    /// with cards created by the desk during gameplay.
    /// </summary>
    public void PlayCards(IList<CardSlot> targetSlots, Action onCompleted = null)
    {
        if (_level == null || targetSlots == null || targetSlots.Count == 0)
        {
            onCompleted?.Invoke();
            return;
        }

        StopAndRestore();

        CacheDealOrigin();
        EnsureFlightLayer();
        CacheDeckReactionTarget();

        int sequenceIndex = 0;
        for (int i = 0; i < targetSlots.Count; i++)
        {
            CardSlot slot = targetSlots[i];
            if (slot == null || slot.Card == null)
                continue;

            Card card = slot.Card;
            // The desk assigns the face before handing the batch to this
            // animator. Keep the card in its slot so its authored final
            // transform is captured exactly, then the animator moves it to
            // the flight layer when that card launches.
            SnapCardToSlot(card, slot);
            CardInfo info = CreateCardInfo(card, slot, sequenceIndex++);
            _cards.Add(info);
            HideRenderers(info);
        }

        _externalDealCompleted = onCompleted;
        _prepared = _cards.Count > 0;

        if (!_prepared)
        {
            Action completion = _externalDealCompleted;
            _externalDealCompleted = null;
            completion?.Invoke();
            return;
        }

        for (int i = 0; i < _cards.Count; i++)
        {
            CaptureFinalTransform(_cards[i]);
            CaptureFinalSorting(_cards[i]);
        }

        RefreshMotionMetrics();
        _routine = StartCoroutine(Animate(playInitialDealSound: false));
    }

    /// <summary>
    /// Returns cards from trays to the deck by replaying the normal deal path
    /// in reverse. This is used by the win cleanup after PopupWin closes.
    /// </summary>
    public void PlayCardsToDesk(
        IList<Card> cards,
        Action<Card> onCardArrived,
        Action onCompleted = null)
    {
        if (cards == null || cards.Count == 0)
        {
            onCompleted?.Invoke();
            return;
        }

        StopAndRestore();
        CacheDealOrigin();
        EnsureFlightLayer();
        CacheDeckReactionTarget();

        Vector3 origin = _resolvedDealOrigin != null
            ? _resolvedDealOrigin.position
            : transform.position;
        List<ReturnCardInfo> returnCards = new List<ReturnCardInfo>();
        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card == null || !card.gameObject.activeInHierarchy)
                continue;

            card.SetSortingOrder(inFlightSortingOffset +
                                 returnCards.Count * inFlightSortingStep);
            returnCards.Add(new ReturnCardInfo
            {
                Card = card,
                StartPosition = card.transform.position,
                StartRotation = card.transform.rotation,
                StartScale = card.transform.localScale,
                Delay = returnCards.Count * Mathf.Max(0.005f, deal.dealInterval) *
                        returnIntervalMultiplier,
                SpinSign = returnCards.Count % 2 == 0 ? 1f : -1f
            });
        }

        if (returnCards.Count == 0)
        {
            onCompleted?.Invoke();
            return;
        }

        _routine = StartCoroutine(AnimateCardsToDesk(
            returnCards,
            origin,
            onCardArrived,
            onCompleted
        ));
    }

    private IEnumerator AnimateCardsToDesk(
        List<ReturnCardInfo> returnCards,
        Vector3 origin,
        Action<Card> onCardArrived,
        Action onCompleted)
    {
        float duration = Mathf.Max(0.05f, deal.flightDuration) *
                         returnFlightDurationMultiplier;
        float lastCardEnd = deal.startDelay +
                            (returnCards.Count - 1) *
                            Mathf.Max(0.005f, deal.dealInterval) *
                            returnIntervalMultiplier + duration;
        bool[] arrived = new bool[returnCards.Count];
        float elapsed = 0f;

        SoundController.Instance?.PlayFX(SoundName.DealCardStartLevel);
        while (elapsed < lastCardEnd)
        {
            elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < returnCards.Count; i++)
            {
                ReturnCardInfo info = returnCards[i];
                if (arrived[i] || info.Card == null)
                    continue;

                float time = elapsed - deal.startDelay - info.Delay;
                if (time < 0f)
                    continue;

                float progress = Mathf.Clamp01(time / duration);
                float reverseTime = 1f - progress;
                float travel = deal.travelCurve.Evaluate(reverseTime);
                Vector3 ground = Vector3.LerpUnclamped(
                    origin,
                    info.StartPosition,
                    travel
                );
                float chordLength = Vector3.Distance(origin, info.StartPosition);
                float arc = deal.arcHeight +
                            chordLength * deal.arcHeightPerDistance;
                float height = arc * deal.arcCurve.Evaluate(reverseTime);
                Transform cardTransform = info.Card.transform;
                cardTransform.position = ground + Vector3.up * height;

                float spin = 360f * deal.turns * info.SpinSign *
                             (1f - deal.turnCurve.Evaluate(reverseTime));
                cardTransform.rotation = info.StartRotation *
                                         Quaternion.Euler(0f, 0f, spin);

                float airborne = Mathf.LerpUnclamped(
                    1f,
                    Mathf.Max(1f, deal.flightScale),
                    Mathf.Max(0f, deal.flightScaleCurve.Evaluate(reverseTime))
                );
                cardTransform.localScale = info.StartScale * airborne;

                if (progress < 1f)
                    continue;

                arrived[i] = true;
                _deckReactionImpulse = Mathf.Min(
                    1f,
                    _deckReactionImpulse + 0.34f
                );
                onCardArrived?.Invoke(info.Card);
            }

            UpdateDeckReaction(Time.unscaledDeltaTime);
            yield return null;
        }

        for (int i = 0; i < returnCards.Count; i++)
        {
            if (!arrived[i] && returnCards[i].Card != null)
                onCardArrived?.Invoke(returnCards[i].Card);
        }

        yield return StartCoroutine(PlayReturnDeckDrop());
        RestoreDeckReaction();
        _routine = null;
        onCompleted?.Invoke();
    }

    private IEnumerator PlayReturnDeckDrop()
    {
        if (_deckReactionTarget == null || !_hasDeckBaseScale)
            yield break;

        Vector3 startScale = _deckReactionTarget.localScale;
        Vector3 dropScale = new Vector3(
            _deckBaseLocalScale.x * 1.03f,
            _deckBaseLocalScale.y * returnDeckDropScale,
            _deckBaseLocalScale.z
        );
        float elapsed = 0f;
        float dropDuration = Mathf.Max(0.05f, returnDeckDropDuration);
        while (elapsed < dropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / dropDuration));
            _deckReactionTarget.localScale = Vector3.LerpUnclamped(
                startScale,
                dropScale,
                progress
            );
            yield return null;
        }

        elapsed = 0f;
        float restoreDuration = Mathf.Max(0.05f, returnDeckRestoreDuration);
        while (elapsed < restoreDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / restoreDuration));
            _deckReactionTarget.localScale = Vector3.LerpUnclamped(
                dropScale,
                _deckBaseLocalScale,
                progress
            );
            yield return null;
        }

        _deckReactionTarget.localScale = _deckBaseLocalScale;
    }

    private void BuildPingPongDealOrder(List<TrayDealState> trays)
    {
        if (trays == null || trays.Count == 0)
            return;

        int remainingCardCount = 0;
        for (int i = 0; i < trays.Count; i++)
            remainingCardCount += trays[i].Slots.Count;

        int sequenceIndex = 0;
        int trayIndex = 0;
        int direction = trays.Count > 1 ? 1 : 0;

        // Real-dealer wave: A B C D C B A B C D...
        // Do not repeat either endpoint when the direction reverses. Empty or
        // exhausted trays are simply skipped without consuming a time slot.
        while (remainingCardCount > 0)
        {
            TrayDealState tray = trays[trayIndex];
            if (tray.HasNext)
            {
                CardSlot slot = tray.Slots[tray.NextCardIndex++];
                CardInfo info = CreateCardInfo(
                    slot.Card,
                    slot,
                    sequenceIndex
                );
                _cards.Add(info);
                HideRenderers(info);
                sequenceIndex++;
                remainingCardCount--;
            }

            if (trays.Count <= 1)
                continue;

            trayIndex += direction;
            if (trayIndex >= trays.Count)
            {
                direction = -1;
                trayIndex = trays.Count - 2;
            }
            else if (trayIndex < 0)
            {
                direction = 1;
                trayIndex = 1;
            }
        }
    }

    private IEnumerator Animate(bool playInitialDealSound)
    {
        float lastCardEnd = 0f;
        bool initialDealSoundPlayed = !playInitialDealSound;

        for (int i = 0; i < _cards.Count; i++)
        {
            CardInfo info = _cards[i];
            if (info == null)
                continue;

            lastCardEnd = Mathf.Max(
                lastCardEnd,
                deal.startDelay + info.Delay +
                deal.flightDuration * deal.SettleTau
            );
        }

        float elapsed = 0f;

        while (elapsed < lastCardEnd)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < _cards.Count; i++)
            {
                CardInfo info = _cards[i];
                if (info == null || info.Card == null || info.Settled)
                    continue;

                float cardTime = elapsed - deal.startDelay - info.Delay;
                if (cardTime < 0f)
                    continue;

                if (!info.Launched)
                {
                    if (!initialDealSoundPlayed)
                    {
                        initialDealSoundPlayed = true;
                        SoundController.Instance?.PlayFX(
                            SoundName.DealCardStartLevel
                        );
                    }

                    Launch(info);
                }

                UpdateCardMotion(info, cardTime);
            }

            UpdateDeckReaction(Time.unscaledDeltaTime);
            yield return null;
        }

        for (int i = 0; i < _cards.Count; i++)
        {
            CardInfo info = _cards[i];
            if (info == null || info.Card == null)
                continue;

            if (!info.Launched)
                Launch(info);

            if (!info.Settled)
                Settle(info);
        }

        RestoreDeckReaction();

        yield return StartCoroutine(FlipAllFaceUp());

        for (int i = 0; i < _cards.Count; i++)
        {
            CardInfo info = _cards[i];
            if (info == null || info.Card == null)
                continue;

            RestoreFace(info);
            info.Card.transform.localRotation = info.FinalLocalRotation;
            info.Card.transform.localScale = info.FinalLocalScale;
        }

        _cards.Clear();
        _prepared = false;
        _routine = null;
        RaiseDealCompleted();

        Action externalCompletion = _externalDealCompleted;
        _externalDealCompleted = null;
        externalCompletion?.Invoke();
    }

    private void UpdateCardMotion(CardInfo info, float cardTime)
    {
        float duration = Mathf.Max(0.05f, deal.flightDuration);
        float tau = cardTime / duration;

        if (tau >= deal.SettleTau)
        {
            Settle(info);
            return;
        }

        Transform cardTransform = info.Card.transform;

        if (tau < 1f)
        {
            float travel = deal.travelCurve.Evaluate(tau);
            float arc = deal.arcHeight +
                        info.ChordLength * deal.arcHeightPerDistance;

            Vector3 ground =
                info.StartWorldPosition +
                info.ChordDirection * (info.ChordLength * travel);
            float height = arc * deal.arcCurve.Evaluate(tau);

            cardTransform.position = ground + Vector3.up * height;

            float angle = -360f * deal.turns * info.SpinSign *
                          deal.turnCurve.Evaluate(tau);

            cardTransform.rotation =
                info.FinalWorldRotation * Quaternion.Euler(0f, 0f, angle);

            UpdateFlightShadow(info, ground, height);
        }
        else
        {
            TouchDown(info);
        }

        ApplyDealScale(info, tau);
    }

    private void ApplyDealScale(CardInfo info, float tau)
    {
        float airborne = Mathf.LerpUnclamped(
            1f,
            Mathf.Max(1f, deal.flightScale),
            Mathf.Max(0f, deal.flightScaleCurve.Evaluate(Mathf.Clamp01(tau)))
        );

        float stretchX = deal.stretchXCurve.Evaluate(tau);
        float stretchY = deal.stretchYCurve.Evaluate(tau);

        SetWorldScale(
            info.Card.transform,
            new Vector3(
                info.FinalWorldScale.x * airborne * stretchX,
                info.FinalWorldScale.y * airborne * stretchY,
                info.FinalWorldScale.z
            )
        );
    }

    private void UpdateFlightShadow(CardInfo info, Vector3 ground, float height)
    {
        DynamicShadow shadow = info.Card != null
            ? info.Card.DynamicShadow
            : null;
        if (shadow == null)
            return;

        shadow.UpdateFlight(
            ground,
            height,
            deal.shadowDropPerHeight,
            deal.shadowSidePerHeight,
            deal.shadowScalePerHeight
        );
    }


    private void TouchDown(CardInfo info)
    {
        if (info.Landed)
            return;

        info.Landed = true;

        Transform cardTransform = info.Card.transform;
        cardTransform.position = info.FinalWorldPosition;
        cardTransform.rotation = info.FinalWorldRotation;
        UpdateFlightShadow(info, info.FinalWorldPosition, 0f);

        info.Completed = true;
        RestoreFinalHierarchy(info);
        RestoreSorting(info);
        RestoreRenderers(info);
    }

    private void Settle(CardInfo info)
    {
        if (!info.Landed)
            TouchDown(info);

        if (info.Settled)
            return;

        info.Settled = true;

        Transform cardTransform = info.Card.transform;
        cardTransform.position = info.FinalWorldPosition;
        cardTransform.rotation = info.FinalWorldRotation;
        SetWorldScale(cardTransform, info.FinalWorldScale);
    }

    private IEnumerator FlipAllFaceUp()
    {
        float lift = Mathf.Max(1f, deal.flipLiftScale);
        float liftIn = Mathf.Max(0f, Mathf.Min(deal.flipLiftDuration, deal.flipDelay));
        float hold = Mathf.Max(0f, deal.flipDelay);
        float flip = Mathf.Max(0.02f, deal.flipDuration);
        float settle = Mathf.Max(0f, deal.flipSettleDuration);

        float elapsed = 0f;

        while (elapsed < hold)
        {
            float rise = liftIn > 0f
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / liftIn))
                : 1f;

            ApplyFlipPose(Mathf.Lerp(1f, lift, rise), 0f, 0f, 1f, 1f);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        BeginBend();
        elapsed = 0f;

        while (elapsed < flip)
        {
            float turn = Mathf.Clamp01(
                deal.flipCurve.Evaluate(Mathf.Clamp01(elapsed / flip))
            );

            float bend = Mathf.Sin(turn * Mathf.PI);

            ApplyFlipPose(lift, 180f * turn, bend, 1f, 1f);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        EndBend();
        RevealAllFaces();
        elapsed = 0f;

        while (elapsed < settle)
        {
            float k = Mathf.Clamp01(elapsed / settle);

            float tau = Mathf.Lerp(1f, deal.SettleTau, k);
            float drop = Mathf.SmoothStep(0f, 1f, k);

            ApplyFlipPose(
                Mathf.Lerp(lift, 1f, drop),
                0f,
                0f,
                deal.stretchXCurve.Evaluate(tau),
                deal.stretchYCurve.Evaluate(tau)
            );

            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        float punch = Mathf.Max(0f, deal.flipPunchDuration);
        elapsed = 0f;

        while (elapsed < punch)
        {
            float k = Mathf.Clamp01(elapsed / punch);
            float ring = deal.flipPunchAmount *
                         Mathf.Cos(k * Mathf.PI * 2f * deal.flipPunchOscillations) *
                         (1f - k);

            ApplyFlipPose(1f, 0f, 0f, 1f + ring, 1f - ring);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        ApplyFlipPose(1f, 0f, 0f, 1f, 1f);
    }

    private void BeginBend()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            CardInfo info = _cards[i];
            if (info == null || info.Card == null)
                continue;

            SpriteRenderer icon = info.Card.IconRenderer;
            if (icon == null)
                continue;

            info.BackSprite = icon.sprite;
            info.Bend = CardBendSurface.For(info.Card);

            if (info.Bend == null)
                continue;

            info.Bend.Begin(icon, info.BackSprite);
            icon.enabled = false;
        }
    }

    private void EndBend()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            CardInfo info = _cards[i];
            if (info == null || info.Card == null)
                continue;

            if (info.Bend != null)
            {
                info.Bend.End();
                info.Bend = null;
            }

            SpriteRenderer icon = info.Card.IconRenderer;
            if (icon != null)
                icon.enabled = true;
        }
    }

    private void ApplyFlipPose(
        float uniform,
        float yaw,
        float bend,
        float stretchX,
        float stretchY)
    {
        float lean = deal.flipLeanAngle * bend;
        float stretch = stretchY * (1f + deal.flipBendStretch * bend);
        float curve = deal.flipCurveAngle * bend;
        bool showFace = yaw > 90f;

        for (int i = 0; i < _cards.Count; i++)
        {
            CardInfo info = _cards[i];
            if (info == null || info.Card == null)
                continue;

            Transform cardTransform = info.Card.transform;

            cardTransform.localRotation =
                info.FinalLocalRotation * Quaternion.Euler(0f, 0f, lean);

            cardTransform.localScale = new Vector3(
                info.FinalLocalScale.x * uniform * stretchX,
                info.FinalLocalScale.y * uniform * stretch,
                info.FinalLocalScale.z
            );

            if (info.Bend == null)
                continue;

            Sprite face = showFace && info.FaceSprite != null
                ? info.FaceSprite
                : info.BackSprite;

            info.Bend.SetSprite(face);
            info.Bend.SetPose(yaw, curve, deal.flipCurveShade);
        }
    }

    private void RevealAllFaces()
    {
        for (int i = 0; i < _cards.Count; i++)
            RevealFace(_cards[i]);
    }

    private void RaiseDealCompleted()
    {
        if (_dealCompletedRaised)
            return;

        _dealCompletedRaised = true;
        DealCompleted?.Invoke();
    }

    private CardInfo CreateCardInfo(
        Card card,
        CardSlot targetSlot,
        int sequenceIndex)
    {
        SpriteRenderer[] renderers =
            card.GetComponentsInChildren<SpriteRenderer>(true);
        bool[] rendererStates = new bool[renderers.Length];
        int[] sortingOrders = new int[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            rendererStates[i] = renderer.enabled;
            sortingOrders[i] = renderer.sortingOrder;
        }

        CardInfo info = new CardInfo
        {
            Card = card,
            TargetSlot = targetSlot,
            FaceSprite = card.IconSprite,
            Renderers = renderers,
            RendererStates = rendererStates,
            FinalSortingOrders = sortingOrders,
            Delay = sequenceIndex * Mathf.Max(0.005f, deal.dealInterval),
            SequenceIndex = sequenceIndex
        };

        CaptureFinalTransform(info);
        return info;
    }

    private static void CaptureFinalTransform(CardInfo info)
    {
        if (info == null || info.Card == null || info.TargetSlot == null)
            return;

        Transform cardTransform = info.Card.transform;
        Transform slotTransform = info.TargetSlot.transform;

        if (cardTransform.parent == slotTransform)
        {
            info.FinalLocalPosition = cardTransform.localPosition;
            info.FinalLocalRotation = cardTransform.localRotation;
            info.FinalLocalScale = cardTransform.localScale;
            info.FinalSiblingIndex = cardTransform.GetSiblingIndex();
        }

        info.FinalWorldPosition = slotTransform.TransformPoint(
            info.FinalLocalPosition
        );
        info.FinalWorldRotation =
            slotTransform.rotation * info.FinalLocalRotation;
        info.FinalWorldScale = Vector3.Scale(
            slotTransform.lossyScale,
            info.FinalLocalScale
        );
    }

    private static void CaptureFinalSorting(CardInfo info)
    {
        if (info == null || info.Renderers == null ||
            info.FinalSortingOrders == null)
        {
            return;
        }

        int count = Mathf.Min(
            info.Renderers.Length,
            info.FinalSortingOrders.Length
        );
        for (int i = 0; i < count; i++)
        {
            if (info.Renderers[i] != null)
            {
                info.FinalSortingOrders[i] =
                    info.Renderers[i].sortingOrder;
            }
        }
    }

    private void RefreshMotionMetrics()
    {
        Vector3 origin = _resolvedDealOrigin != null
            ? _resolvedDealOrigin.position
            : transform.position;

        for (int i = 0; i < _cards.Count; i++)
        {
            CardInfo info = _cards[i];
            if (info == null || info.Card == null)
                continue;

            info.StartWorldPosition = origin;

            Vector3 chord = info.FinalWorldPosition - origin;
            info.ChordLength = chord.magnitude;
            info.ChordDirection = info.ChordLength > 0.0001f
                ? chord / info.ChordLength
                : Vector3.up;

            info.SpinSign = Mathf.Abs(chord.x) > 0.01f
                ? Mathf.Sign(chord.x)
                : 1f;
        }
    }

    private void Launch(CardInfo info)
    {
        if (info == null || info.Card == null || info.TargetSlot == null)
            return;

        info.Launched = true;
        Transform cardTransform = info.Card.transform;
        cardTransform.SetParent(
            _flightLayer != null ? _flightLayer : transform,
            true
        );
        cardTransform.SetAsLastSibling();
        cardTransform.position = info.StartWorldPosition;
        cardTransform.rotation = info.FinalWorldRotation;
        SetWorldScale(cardTransform, info.FinalWorldScale);
        info.Completed = false;
        info.FaceRevealed = false;
        info.Card.SetOffSprite();
        info.Card.SetShadowEnabled(true);
        UpdateFlightShadow(info, info.StartWorldPosition, 0f);
        ApplyFlightSorting(info);
        RestoreRenderers(info);

        _deckReactionImpulse = Mathf.Min(
            1f,
            _deckReactionImpulse + 0.34f
        );
    }

    private static void RevealFace(CardInfo info)
    {
        if (info == null || info.Card == null || info.FaceRevealed)
            return;

        if (info.FaceSprite != null)
            info.Card.SetIcon(info.FaceSprite);

        info.FaceRevealed = true;
    }

    public void StopAndRestore()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        Action abortedCompletion = _externalDealCompleted;
        _externalDealCompleted = null;

        for (int i = 0; i < _cards.Count; i++)
        {
            CardInfo info = _cards[i];
            if (info == null || info.Card == null)
                continue;

            if (info.Bend != null)
            {
                info.Bend.End();
                info.Bend = null;
            }

            RestoreFinalHierarchy(info);
            RestoreFace(info);
            RestoreSorting(info);
            RestoreRenderers(info);
        }

        RestoreDeckReaction();
        _cards.Clear();
        _prepared = false;
        abortedCompletion?.Invoke();
    }

    private static int CompareTrayDealOrder(
        CardSlotHolder left,
        CardSlotHolder right)
    {
        // Read each visual row left-to-right, then continue top-to-bottom.
        int leftRow = Mathf.RoundToInt(left.transform.position.y * 10f);
        int rightRow = Mathf.RoundToInt(right.transform.position.y * 10f);
        int rowComparison = rightRow.CompareTo(leftRow);
        if (rowComparison != 0)
            return rowComparison;

        int horizontalComparison = left.transform.position.x.CompareTo(
            right.transform.position.x
        );
        if (horizontalComparison != 0)
            return horizontalComparison;

        return left.transform.GetSiblingIndex().CompareTo(
            right.transform.GetSiblingIndex()
        );
    }

    private void CacheDealOrigin()
    {
        if (dealOrigin != null)
        {
            _resolvedDealOrigin = dealOrigin;
            return;
        }

        CardDesk cardDesk = _level != null
            ? _level.GetComponentInChildren<CardDesk>(true)
            : null;
        _resolvedDealOrigin = cardDesk != null
            ? FindChildRecursive(cardDesk.transform, "SpawnPoint")
            : null;

        if (_resolvedDealOrigin == null && cardDesk != null)
            _resolvedDealOrigin = cardDesk.transform;
    }

    private void EnsureFlightLayer()
    {
        if (_flightLayer != null)
            return;

        Transform existing = transform.Find("InitialDealAnimationLayer");
        if (existing != null)
        {
            _flightLayer = existing;
            return;
        }

        GameObject layerObject = new GameObject("InitialDealAnimationLayer");
        layerObject.layer = gameObject.layer;
        _flightLayer = layerObject.transform;
        _flightLayer.SetParent(transform, false);
        _flightLayer.localPosition = Vector3.zero;
        _flightLayer.localRotation = Quaternion.identity;
        _flightLayer.localScale = Vector3.one;
        _flightLayer.SetAsLastSibling();
    }

    private void CacheDeckReactionTarget()
    {
        Transform target = _resolvedDealOrigin != null
            ? _resolvedDealOrigin.parent
            : null;
        if (target == null || target == transform)
        {
            CardDesk cardDesk = _level != null
                ? _level.GetComponentInChildren<CardDesk>(true)
                : null;
            target = cardDesk != null ? cardDesk.transform : null;
        }

        if (_deckReactionTarget != target)
        {
            RestoreDeckReaction();
            _deckReactionTarget = target;
            _hasDeckBaseScale = false;
        }

        if (_deckReactionTarget != null && !_hasDeckBaseScale)
        {
            _deckBaseLocalScale = _deckReactionTarget.localScale;
            _hasDeckBaseScale = true;
        }
    }

    private void UpdateDeckReaction(float deltaTime)
    {
        if (_deckReactionTarget == null || !_hasDeckBaseScale)
            return;

        float recoveryDuration = Mathf.Max(0.045f, deal.flightDuration * 0.38f);
        _deckReactionImpulse = Mathf.MoveTowards(
            _deckReactionImpulse,
            0f,
            deltaTime / recoveryDuration
        );
        float amount = Mathf.Max(0f, deal.deckReactionAmount) *
                       _deckReactionImpulse;
        _deckReactionTarget.localScale = new Vector3(
            _deckBaseLocalScale.x * (1f + amount * 0.2f),
            _deckBaseLocalScale.y * (1f - amount),
            _deckBaseLocalScale.z
        );
    }

    private void RestoreDeckReaction()
    {
        if (_deckReactionTarget != null && _hasDeckBaseScale)
            _deckReactionTarget.localScale = _deckBaseLocalScale;

        _deckReactionImpulse = 0f;
    }

    private void ApplyFlightSorting(CardInfo info)
    {
        if (info == null || info.Renderers == null ||
            info.FinalSortingOrders == null)
        {
            return;
        }

        int count = Mathf.Min(
            info.Renderers.Length,
            info.FinalSortingOrders.Length
        );
        int offset = Mathf.Max(1, inFlightSortingOffset) +
                     info.SequenceIndex *
                     Mathf.Max(1, inFlightSortingStep);

        for (int i = 0; i < count; i++)
        {
            if (info.Renderers[i] != null)
            {
                info.Renderers[i].sortingOrder =
                    info.FinalSortingOrders[i] + offset;
            }
        }
    }

    private static void RestoreSorting(CardInfo info)
    {
        if (info == null || info.Renderers == null ||
            info.FinalSortingOrders == null)
        {
            return;
        }

        int count = Mathf.Min(
            info.Renderers.Length,
            info.FinalSortingOrders.Length
        );
        for (int i = 0; i < count; i++)
        {
            if (info.Renderers[i] != null)
                info.Renderers[i].sortingOrder = info.FinalSortingOrders[i];
        }
    }

    private static void RestoreFinalHierarchy(CardInfo info)
    {
        if (info == null || info.Card == null || info.TargetSlot == null)
            return;

        Transform cardTransform = info.Card.transform;
        cardTransform.SetParent(info.TargetSlot.transform, false);
        cardTransform.localPosition = info.FinalLocalPosition;
        cardTransform.localRotation = info.FinalLocalRotation;
        cardTransform.localScale = info.FinalLocalScale;

        int siblingIndex = Mathf.Clamp(
            info.FinalSiblingIndex,
            0,
            Mathf.Max(0, cardTransform.parent.childCount - 1)
        );
        cardTransform.SetSiblingIndex(siblingIndex);
    }

    private static void SnapCardToSlot(Card card, CardSlot slot)
    {
        if (card == null || slot == null)
            return;

        Transform cardTransform = card.transform;
        cardTransform.SetParent(slot.transform, false);
        cardTransform.localPosition = Vector3.zero;
        cardTransform.localRotation = Quaternion.identity;
        cardTransform.localScale = Vector3.one;
    }

    private static void RestoreFace(CardInfo info)
    {
        if (info == null || info.Card == null || info.FaceSprite == null)
            return;

        info.Card.SetIcon(info.FaceSprite);
        info.FaceRevealed = true;
    }

    private static void HideRenderers(CardInfo info)
    {
        if (info == null || info.Renderers == null)
            return;

        for (int i = 0; i < info.Renderers.Length; i++)
        {
            if (info.Renderers[i] != null)
                info.Renderers[i].enabled = false;
        }
    }

    private static void RestoreRenderers(CardInfo info)
    {
        if (info == null || info.Renderers == null ||
            info.RendererStates == null)
        {
            return;
        }

        int count = Mathf.Min(
            info.Renderers.Length,
            info.RendererStates.Length
        );
        for (int i = 0; i < count; i++)
        {
            if (info.Renderers[i] != null)
                info.Renderers[i].enabled = info.RendererStates[i];
        }
    }

    private static void SetWorldScale(
        Transform target,
        Vector3 worldScale)
    {
        if (target == null)
            return;

        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            SafeDivide(worldScale.x, parentScale.x),
            SafeDivide(worldScale.y, parentScale.y),
            SafeDivide(worldScale.z, parentScale.z)
        );
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
    }

    private static Transform FindChildRecursive(
        Transform root,
        string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(
                root.GetChild(i),
                childName
            );
            if (result != null)
                return result;
        }

        return null;
    }
}
