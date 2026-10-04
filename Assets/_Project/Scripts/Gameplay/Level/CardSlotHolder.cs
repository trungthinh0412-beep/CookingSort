using System.Collections.Generic;
using UnityEngine;
using CustomInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

public interface ICardSlotHolderRule
{
    bool CanReceiveCard(CardType cardType);
    void OnHolderCardsChanged();
}

public class CardSlotHolder : MonoBehaviour
{
    [Header("Card Setup")]
    [SerializeField] private List<CardType> cardTypes = new List<CardType>();

    public const int MAX_SLOTS = 10;
    private const float TOP_SLOT_LOCAL_Y = 1.074f;
    private const float SLOT_VERTICAL_STEP = 0.2f;
    private const int FIRST_CARD_SORTING_ORDER = 10;
    private const int CARD_SORTING_STEP = 10;

    [SerializeField] private List<CardSlot> cardSlots = new List<CardSlot>();
    [OnValueChanged(nameof(UpdateVisual))]
    [SerializeField] private bool isLocked;

    [Header("Visual")]
    [SerializeField] private GameObject unlockVisual;
    [SerializeField] private GameObject lockVisual;

    [Header("Visual Sorting")]
    [SerializeField] private int trayFrameSortingOrder = 1;
    [SerializeField] private int trayCanvasSortingOrder = 2;

    [Header("Visual Effects")]
    [SerializeField] private GameObject activeSoftFx;

    public List<CardSlot> CardSlots => cardSlots;
    public bool IsLocked => isLocked;
    public bool IsBlockedByIron =>
        GetTopCardType() == CardType.IronCard;

    private Coroutine _fxRoutine;
    private readonly List<ICardSlotHolderRule> _rules =
        new List<ICardSlotHolderRule>();
    private bool _rulesCached;

    private void Awake()
    {
        if (cardSlots == null || cardSlots.Count == 0)
        {
            cardSlots = new List<CardSlot>(GetComponentsInChildren<CardSlot>());
        }

        NormalizeSlotLayout();
        EnsureVisualsRenderAboveTable();

        if (activeSoftFx != null && activeSoftFx.activeSelf)
        {
            activeSoftFx.SetActive(false);
        }

        CacheRules();
        UpdateVisual();
        NotifyCardsChanged();
    }

    private void EnsureVisualsRenderAboveTable()
    {
        ApplyVisualSorting(unlockVisual);
        ApplyVisualSorting(lockVisual);
    }

    private void ApplyVisualSorting(GameObject visualRoot)
    {
        if (visualRoot == null)
            return;

        int frameOrder = Mathf.Max(1, trayFrameSortingOrder);
        int canvasOrder = Mathf.Max(frameOrder + 1, trayCanvasSortingOrder);

        SpriteRenderer[] renderers =
            visualRoot.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sortingOrder = Mathf.Max(
                renderers[i].sortingOrder,
                frameOrder
            );

        Canvas[] canvases =
            visualRoot.GetComponentsInChildren<Canvas>(true);

        for (int i = 0; i < canvases.Length; i++)
        {
            canvases[i].overrideSorting = true;
            canvases[i].sortingOrder = Mathf.Max(
                canvases[i].sortingOrder,
                canvasOrder
            );
        }
    }

    public void NormalizeSlotLayout()
    {
        if (cardSlots == null)
            cardSlots = new List<CardSlot>();

        cardSlots.RemoveAll(slot => slot == null);

        // The holder's logical order is top-to-bottom. Keep this deterministic
        // even when a prefab instance serializes its children in another order.
        cardSlots.Sort((left, right) =>
            right.transform.localPosition.y.CompareTo(
                left.transform.localPosition.y
            )
        );

        int slotCount = Mathf.Min(MAX_SLOTS, cardSlots.Count);
        for (int i = 0; i < slotCount; i++)
        {
            CardSlot slot = cardSlots[i];
            Transform slotTransform = slot.transform;
            slotTransform.localPosition = new Vector3(
                0f,
                TOP_SLOT_LOCAL_Y - i * SLOT_VERTICAL_STEP,
                0f
            );
            slotTransform.localRotation = Quaternion.identity;
            slotTransform.localScale = Vector3.one;

            Card card = slot.Card;
            if (card == null)
                continue;

            Transform cardTransform = card.transform;
            cardTransform.SetParent(slotTransform, false);
            cardTransform.localPosition = Vector3.zero;
            cardTransform.localRotation = Quaternion.identity;
            cardTransform.localScale = Vector3.one;

            int targetSortingOrder =
                FIRST_CARD_SORTING_ORDER + i * CARD_SORTING_STEP;
            card.SetSortingOrder(targetSortingOrder);
        }
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (unlockVisual != null) unlockVisual.SetActive(!isLocked);
        if (lockVisual != null) lockVisual.SetActive(isLocked);
    }

    /// <summary>
    /// Kích hoạt hiệu ứng sáng khung viền khi gộp thẻ thành công (Giữ nguyên vị trí và scale, chỉ chạy alpha)
    /// </summary>
    public void PlayActiveSoftEffect()
    {
        if (activeSoftFx == null) return;

        if (_fxRoutine != null)
        {
            StopCoroutine(_fxRoutine);
        }
        _fxRoutine = StartCoroutine(ActiveSoftFxAlphaOnlyRoutine());
    }

    private System.Collections.IEnumerator ActiveSoftFxAlphaOnlyRoutine()
    {
        activeSoftFx.SetActive(true);
        SpriteRenderer sr = activeSoftFx.GetComponent<SpriteRenderer>();

        float duration = 0.4f;
        float elapsed = 0f;

        if (sr != null)
        {
            Color c = sr.color;
            c.a = 0f;
            sr.color = c;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Chỉ thay đổi độ trong suốt (alpha), giữ nguyên vị trí và scale ban đầu
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Sin(t * Mathf.PI);
                sr.color = c;
            }

            yield return null;
        }

        activeSoftFx.SetActive(false);
        _fxRoutine = null;
    }

    public int GetTopCardIndex()
    {
        for (int i = cardSlots.Count - 1; i >= 0; i--)
        {
            if (!cardSlots[i].IsEmpty)
            {
                return i;
            }
        }

        return -1;
    }

    public List<Card> GetTopMatchingCards()
    {
        List<Card> result = new List<Card>();

        int topIndex = GetTopCardIndex();

        if (topIndex < 0)
            return result;

        Card topCard = cardSlots[topIndex].Card;
        if (topCard.IsFrozen)
            return result;
        CardType topType = topCard.CardType;
        result.Add(topCard);

        // An exposed obstacle always moves by itself. In particular, an Iron
        // Card must remain movable even if another obstacle is buried below.
        if (topCard.IsObstacleCard)
            return result;

        int chainIndex = FindCardIndex(CardType.ChainCard, topIndex);

        // A Chain Card on top can move by itself. Once another card is
        // placed above it, the chain binds the whole tray into one group.
        if (chainIndex >= 0 && chainIndex != topIndex)
        {
            result.Clear();

            for (int i = topIndex; i >= 0; i--)
            {
                CardSlot slot = cardSlots[i];
                if (slot == null || slot.Card == null)
                    break;

                // A buried Iron Card is never carried by another obstacle's
                // group. It only becomes movable after it is exposed.
                if (slot.Card.CardType == CardType.IronCard || slot.Card.IsFrozen)
                    break;

                result.Add(slot.Card);
            }

            return result;
        }

        // Wild chua gan gia tri chi duoc di chuyen mot minh.
        if (topCard.IsUnassignedWild)
            return result;

        for (int i = topIndex - 1; i >= 0; i--)
        {
            if (cardSlots[i].IsEmpty)
                break;

            if (!cardSlots[i].Card.IsFrozen && cardSlots[i].Card.CardType == topType)
            {
                result.Add(cardSlots[i].Card);
            }
            else
            {
                break;
            }
        }

        return result;
    }

    private int FindCardIndex(CardType type, int maxIndex)
    {
        int lastIndex = Mathf.Min(maxIndex, cardSlots.Count - 1);

        for (int i = lastIndex; i >= 0; i--)
        {
            CardSlot slot = cardSlots[i];
            if (slot != null && slot.Card != null &&
                slot.Card.CardType == type)
            {
                return i;
            }
        }

        return -1;
    }

    public CardType? GetTopCardType()
    {
        int topIndex = GetTopCardIndex();

        if (topIndex < 0)
            return null;

        return cardSlots[topIndex].Card.CardType;
    }

    public Card GetTopCard()
    {
        int topIndex = GetTopCardIndex();

        return topIndex >= 0 && topIndex < cardSlots.Count
            ? cardSlots[topIndex].Card
            : null;
    }

    public bool CanReceiveCards(CardType cardType)
    {
        if (!CanReceiveCardsIgnoringTopType(cardType))
            return false;

        CardType? topType = GetTopCardType();

        if (topType == null)
            return true;

        Card topCard = GetTopCard();
        if (topCard != null && topCard.IsUnassignedWild)
            return true;

        return topType.Value == cardType;
    }

    public bool CanReceiveCardsIgnoringTopType(CardType cardType)
    {
        // Iron locks only incoming cards. It must remain selectable so the
        // exposed Iron Card can move away and unlock this tray immediately.
        if (isLocked || IsBlockedByIron ||
            GetTopCard()?.IsFrozen == true)
            return false;

        CacheRules();

        for (int i = 0; i < _rules.Count; i++)
        {
            if (!_rules[i].CanReceiveCard(cardType))
                return false;
        }

        return true;
    }

    public int GetEmptySlotCount()
    {
        int count = 0;

        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (cardSlots[i].IsEmpty)
                count++;
        }

        return count;
    }

    public List<CardSlot> GetEmptySlots(int maxCount)
    {
        List<CardSlot> result = new List<CardSlot>();

        for (int i = 0; i < cardSlots.Count && result.Count < maxCount; i++)
        {
            if (cardSlots[i].IsEmpty)
            {
                result.Add(cardSlots[i]);
            }
        }

        return result;
    }

    public int ReceiveCards(List<Card> cards)
    {
        if (cards == null || cards.Count == 0 || IsBlockedByIron)
            return 0;

        int received = 0;

        for (int i = 0; i < cardSlots.Count && received < cards.Count; i++)
        {
            if (cardSlots[i].IsEmpty)
            {
                cardSlots[i].SetCard(cards[received]);
                received++;
            }
        }

        if (received > 0)
        {
            NormalizeSlotLayout();
            NotifyCardsChanged();
        }

        return received;
    }

    public void RemoveCards(List<Card> cards)
    {
        bool removedAny = false;

        foreach (var card in cards)
        {
            for (int i = 0; i < cardSlots.Count; i++)
            {
                if (cardSlots[i].Card == card)
                {
                    cardSlots[i].RemoveCard();
                    removedAny = true;
                    break;
                }
            }
        }

        if (removedAny)
            NotifyCardsChanged();
    }

    public bool IsEmpty()
    {
        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (!cardSlots[i].IsEmpty)
                return false;
        }

        return true;
    }

    public void ClearAllCards()
    {
        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (!cardSlots[i].IsEmpty)
            {
                Destroy(cardSlots[i].Card.gameObject);
                cardSlots[i].RemoveCard();
            }
        }

        NotifyCardsChanged();
    }

    public void NotifyCardsChanged()
    {
        CacheRules();

        for (int i = 0; i < _rules.Count; i++)
        {
            _rules[i].OnHolderCardsChanged();
        }
    }

    private void CacheRules()
    {
        if (_rulesCached)
            return;

        _rules.Clear();

        MonoBehaviour[] behaviours =
            GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICardSlotHolderRule rule)
            {
                _rules.Add(rule);
            }
        }

        _rulesCached = true;
    }

#if UNITY_EDITOR
    [Button("Init Cards")]
    public void InitCards()
    {
        Level level = GetComponentInParent<Level>();

        if (level == null)
        {
            Debug.LogError("CardSlotHolder: Không tìm thấy Level ở parent!");
            return;
        }

        if (level.CardPrefab == null)
        {
            Debug.LogError("CardSlotHolder: Level chưa gán CardPrefab!");
            return;
        }

        if (level.CardConfig == null)
        {
            Debug.LogError("CardSlotHolder: Level chưa gán CardConfig!");
            return;
        }

        if (cardSlots == null || cardSlots.Count == 0)
        {
            cardSlots = new List<CardSlot>(GetComponentsInChildren<CardSlot>());
        }

        NormalizeSlotLayout();

        for (int i = 0; i < cardSlots.Count; i++)
        {
            Utility.Clear(cardSlots[i].transform);
            cardSlots[i].Card = null;
        }

        int count = Mathf.Min(cardTypes.Count, cardSlots.Count);

        for (int i = 0; i < count; i++)
        {
            Card prefab = level.GetCardPrefab(cardTypes[i]);
            if (prefab == null)
            {
                Debug.LogError($"CardSlotHolder: Khong tim thay prefab cho {cardTypes[i]}!");
                continue;
            }

            Card newCard = (Card)PrefabUtility.InstantiatePrefab(
                prefab,
                cardSlots[i].transform
            );

            newCard.transform.localPosition = Vector3.zero;
            CardData data = level.CardConfig.GetEditorCardData(cardTypes[i]);
            if (data != null && cardTypes[i] == CardType.WildCard)
                newCard.SetAsWild(data.icon);
            else
            {
                newCard.CardType = cardTypes[i];
                newCard.SetIcon(data != null ? data.icon : null);
            }

            cardSlots[i].Card = newCard;

            EditorUtility.SetDirty(cardSlots[i]);
            EditorUtility.SetDirty(newCard);
        }

        NormalizeSlotLayout();
        EditorUtility.SetDirty(this);

        Debug.Log($"CardSlotHolder: Đã tạo {count} Card thành công!");
    }
#endif
}
