using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CollectionCardSaveData
{
    public string cardId;
    public int quantity;
    public bool isNew;
}

[Serializable]
public sealed class CollectionSaveData
{
    public string collectionId;
    public bool rewardClaimed;
}

public enum CollectionCardState
{
    NotOwned,
    Owned,
    Locked,
    Duplicate
}

public partial class PlayerData
{
    [SerializeField] private List<CollectionCardSaveData> collectionCards = new List<CollectionCardSaveData>();
    [SerializeField] private List<string> unlockedCollections = new List<string>();
    [SerializeField] private List<CollectionSaveData> collectionProgress = new List<CollectionSaveData>();

    // Public properties participate in the existing Newtonsoft JSON save contract.
    // Missing fields in older saves start empty.
    public List<CollectionCardSaveData> CollectionCards
    {
        get => collectionCards ?? (collectionCards = new List<CollectionCardSaveData>());
        set => collectionCards = value ?? new List<CollectionCardSaveData>();
    }

    public List<string> UnlockedCollections
    {
        get => unlockedCollections ?? (unlockedCollections = new List<string>());
        set => unlockedCollections = value ?? new List<string>();
    }

    public List<CollectionSaveData> CollectionProgress
    {
        get => collectionProgress ?? (collectionProgress = new List<CollectionSaveData>());
        set => collectionProgress = value ?? new List<CollectionSaveData>();
    }

    public bool IsCollectionCardNew(string cardId)
    {
        CollectionCardSaveData card = FindCollectionCardProgress(cardId);
        return card != null && card.quantity > 0 && card.isNew;
    }

    public bool IsCollectionRewardClaimed(string collectionId)
    {
        return CollectionProgress.Exists(item => item != null && item.collectionId == collectionId && item.rewardClaimed);
    }

    internal CollectionCardSaveData FindCollectionCardProgress(string cardId)
    {
        return CollectionCards.Find(item => item != null && item.cardId == cardId);
    }

    internal void SetCollectionCardProgress(string cardId, int quantity, bool isNew)
    {
        CollectionCardSaveData card = FindCollectionCardProgress(cardId);
        if (card == null)
        {
            card = new CollectionCardSaveData { cardId = cardId };
            CollectionCards.Add(card);
        }
        card.quantity = Mathf.Max(0, quantity);
        card.isNew = quantity > 0 && isNew;
    }

    internal void SetCollectionRewardClaimed(string collectionId, bool claimed)
    {
        CollectionSaveData progress = CollectionProgress.Find(item => item != null && item.collectionId == collectionId);
        if (progress == null)
        {
            progress = new CollectionSaveData { collectionId = collectionId };
            CollectionProgress.Add(progress);
        }
        progress.rewardClaimed = claimed;
    }

    // Restore a failed Collection claim without granting currency a second time or firing grant effects.
    internal void RestoreCollectionRewardBalances(int gold, int stars)
    {
        currentGold = gold;
        currentStar = stars;
    }

    public int GetCollectionCardQuantity(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return 0;

        foreach (CollectionCardSaveData card in CollectionCards)
        {
            if (card != null && string.Equals(card.cardId, cardId, StringComparison.Ordinal))
                return Mathf.Max(0, card.quantity);
        }

        return 0;
    }

    public bool AddCollectionCard(CollectionCardData card, int quantity = 1)
    {
        // Compatibility with Part 1: all live grants now use the validated, saving entry point.
        return ReferenceEquals(this, Data.PlayerData) && card != null &&
               CollectionManager.AddCard(card.Id, quantity).Success;
    }

    public bool IsCollectionUnlocked(CollectionData collection)
    {
        return collection != null && !string.IsNullOrWhiteSpace(collection.Id) &&
               (CurrentLevelIndex >= collection.UnlockLevel || UnlockedCollections.Contains(collection.Id));
    }

    // Explicit unlocks are saved by ID. Level unlocks use the already-saved player level.
    public bool UnlockCollection(CollectionData collection)
    {
        return ReferenceEquals(this, Data.PlayerData) && collection != null &&
               CollectionManager.UnlockCollection(collection.Id);
    }

    public int GetOwnedCollectionCardCount(CollectionData collection)
    {
        if (collection == null || collection.Cards == null)
            return 0;

        var ownedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CollectionCardData card in collection.Cards)
        {
            if (card != null && GetCollectionCardQuantity(card.Id) > 0)
                ownedIds.Add(card.Id);
        }

        return ownedIds.Count;
    }

    public CollectionCardState GetCollectionCardState(
        CollectionData collection, CollectionCardData card, int featureUnlockLevel = 1)
    {
        if (card == null || CurrentLevelIndex < featureUnlockLevel ||
            !IsCollectionUnlocked(collection) || CurrentLevelIndex < card.UnlockLevel)
            return CollectionCardState.Locked;

        int quantity = GetCollectionCardQuantity(card.Id);
        if (quantity > 1)
            return CollectionCardState.Duplicate;

        return quantity == 1 ? CollectionCardState.Owned : CollectionCardState.NotOwned;
    }
}
