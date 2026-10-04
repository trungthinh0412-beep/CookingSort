using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardAddStatus { NotInitialized, Success, InvalidAmount, UnknownCard, Locked, QuantityLimit, Busy, SaveFailed }
public enum CollectionRewardState { Locked, ReadyToClaim, Claimed }

// Immutable presentation snapshots. These are never written into the save file.
public readonly struct CollectionCardProgress
{
    public int Quantity { get; }
    public bool IsNew { get; }
    public bool IsUnlocked { get; }
    public CollectionCardState State => !IsUnlocked ? CollectionCardState.Locked :
        Quantity > 1 ? CollectionCardState.Duplicate : Quantity == 1 ? CollectionCardState.Owned : CollectionCardState.NotOwned;

    public CollectionCardProgress(int quantity, bool isNew, bool isUnlocked = true)
    {
        Quantity = Mathf.Max(0, quantity);
        IsNew = Quantity > 0 && isNew;
        IsUnlocked = isUnlocked;
    }
}

public readonly struct CardAddResult
{
    public CardAddStatus Status { get; }
    public bool Success => Status == CardAddStatus.Success;
    public CollectionData Collection { get; }
    public CollectionCardData Card { get; }
    public string CollectionId => Collection == null ? string.Empty : Collection.Id;
    public string CardId => Card == null ? string.Empty : Card.Id;
    public CollectionCardProgress Before { get; }
    public CollectionCardProgress After { get; }
    public int OldQuantity => Before.Quantity;
    public int NewQuantity => After.Quantity;
    public bool IsNewCard => Success && OldQuantity == 0;
    public bool IsDuplicate => Success && OldQuantity > 0;
    public int OwnedUniqueCardCount { get; }
    public bool CollectionCompletedNow { get; }

    internal CardAddResult(CardAddStatus status, CollectionData collection = null, CollectionCardData card = null,
        CollectionCardProgress before = default, CollectionCardProgress after = default,
        int ownedUniqueCardCount = 0, bool completedNow = false)
    {
        Status = status;
        Collection = collection;
        Card = card;
        Before = before;
        After = after;
        OwnedUniqueCardCount = ownedUniqueCardCount;
        CollectionCompletedNow = completedNow;
    }
}

/// <summary>
/// Collection operations over the existing Data.PlayerData and encrypted Data.SaveData pipeline.
/// Called on Unity's main thread. No UI, animation, separate save file or scene singleton is required.
/// </summary>
public static class CollectionManager
{
    private static CollectionConfig _config;
    private static bool _validConfig;
    private static bool _mutating;
    private static bool _publishing;
    private static readonly Queue<CardAddResult> PendingNotifications = new Queue<CardAddResult>();

    public static CollectionConfig Config => _config;
    public static bool IsInitialized => _validConfig && _config != null && Data.PlayerData != null;
    public static bool IsFeatureUnlocked => IsInitialized && Data.PlayerData.CurrentLevelIndex >= _config.UnlockLevel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        _config = null;
        _validConfig = false;
        _mutating = false;
        _publishing = false;
        PendingNotifications.Clear();
    }

    public static void Initialize(CollectionConfig config)
    {
        _config = config;
        var errors = new List<string>();
        _validConfig = config != null && config.ValidateData(errors);
        foreach (string error in errors)
            Warn(error);
        if (config == null)
            Warn("CollectionConfig is not assigned to PlayerDataController.");
        ValidatePlayerProgress();
    }

    public static bool IsCollectionUnlocked(string collectionId)
    {
        CollectionData collection = _config == null ? null : _config.GetCollection(collectionId);
        return IsFeatureUnlocked && Data.PlayerData.IsCollectionUnlocked(collection);
    }

    public static int GetOwnedUniqueCardCount(string collectionId)
    {
        CollectionData collection = _config == null ? null : _config.GetCollection(collectionId);
        return Data.PlayerData == null ? 0 : Data.PlayerData.GetOwnedCollectionCardCount(collection);
    }

    public static int GetTotalCardCount(string collectionId)
    {
        CollectionData collection = _config == null ? null : _config.GetCollection(collectionId);
        return collection == null ? 0 : collection.TotalCards;
    }

    public static bool IsCollectionComplete(string collectionId)
    {
        int total = GetTotalCardCount(collectionId);
        return total > 0 && GetOwnedUniqueCardCount(collectionId) == total;
    }

    public static CollectionCardProgress GetCardProgress(string cardId)
    {
        if (!IsInitialized || !_config.TryGetCard(cardId, out CollectionData collection, out CollectionCardData card))
            return new CollectionCardProgress(0, false, false);
        PlayerData player = Data.PlayerData;
        return new CollectionCardProgress(player.GetCollectionCardQuantity(cardId), player.IsCollectionCardNew(cardId),
            IsCollectionUnlocked(collection.Id) && player.CurrentLevelIndex >= card.UnlockLevel);
    }

    public static CollectionRewardState GetRewardState(string collectionId)
    {
        if (Data.PlayerData != null && Data.PlayerData.IsCollectionRewardClaimed(collectionId))
            return CollectionRewardState.Claimed;
        CollectionData collection = _config == null ? null : _config.GetCollection(collectionId);
        return collection != null && collection.HasReward && IsCollectionUnlocked(collectionId) && IsCollectionComplete(collectionId)
            ? CollectionRewardState.ReadyToClaim : CollectionRewardState.Locked;
    }

    public static CardAddResult AddCard(string cardId, int amount = 1)
    {
        if (_mutating)
            return new CardAddResult(CardAddStatus.Busy);
        if (!IsInitialized)
            return new CardAddResult(CardAddStatus.NotInitialized);
        if (amount <= 0)
            return new CardAddResult(CardAddStatus.InvalidAmount);
        if (!_config.TryGetCard(cardId, out CollectionData collection, out CollectionCardData card))
        {
            Warn($"Unknown or ambiguous card ID '{cardId}'. The grant was rejected.");
            return new CardAddResult(CardAddStatus.UnknownCard);
        }
        CollectionCardProgress before = GetCardProgress(cardId);
        if (!before.IsUnlocked)
            return new CardAddResult(CardAddStatus.Locked, collection, card);
        if ((long)before.Quantity + amount > int.MaxValue)
            return new CardAddResult(CardAddStatus.QuantityLimit, collection, card);

        PlayerData player = Data.PlayerData;
        bool hadRecord = player.FindCollectionCardProgress(cardId) != null;
        bool wasComplete = IsCollectionComplete(collection.Id);
        var after = new CollectionCardProgress(before.Quantity + amount, before.IsNew || before.Quantity == 0);
        CardAddResult result;
        _mutating = true;
        try
        {
            player.SetCollectionCardProgress(cardId, after.Quantity, after.IsNew);
            Data.SaveData();
            result = new CardAddResult(CardAddStatus.Success, collection, card, before, after,
                GetOwnedUniqueCardCount(collection.Id), !wasComplete && IsCollectionComplete(collection.Id));
        }
        catch (Exception exception)
        {
            if (hadRecord)
                player.SetCollectionCardProgress(cardId, before.Quantity, before.IsNew);
            else
                player.CollectionCards.RemoveAll(item => item != null && item.cardId == cardId);
            Warn($"Card '{cardId}' was not saved: {exception.Message}");
            return new CardAddResult(CardAddStatus.SaveFailed, collection, card);
        }
        finally { _mutating = false; }

        PublishCard(result);
        return result;
    }

    public static bool TryClaimReward(string collectionId)
    {
        if (_mutating || !IsInitialized || GetRewardState(collectionId) != CollectionRewardState.ReadyToClaim)
            return false;
        PlayerData player = Data.PlayerData;
        CollectionData collection = _config.GetCollection(collectionId);
        RewardData reward = collection.Reward;
        if (reward.totalGold < 0 || reward.totalStar < 0 ||
            (long)player.CurrentGold + reward.totalGold > int.MaxValue ||
            (long)player.CurrentStar + reward.totalStar > int.MaxValue)
            return false;

        int oldGold = player.CurrentGold;
        int oldStars = player.CurrentStar;
        bool hadRecord = player.CollectionProgress.Exists(item => item != null && item.collectionId == collectionId);
        _mutating = true;
        try
        {
            // Reserve before currency callbacks run. Reentrant/spam claims cannot grant twice.
            player.SetCollectionRewardClaimed(collectionId, true);
            reward.CheckAndClaim();
            Data.SaveData(); // Balances and claimed flag are serialized together.
        }
        catch (Exception exception)
        {
            player.RestoreCollectionRewardBalances(oldGold, oldStars);
            if (hadRecord)
                player.SetCollectionRewardClaimed(collectionId, false);
            else
                player.CollectionProgress.RemoveAll(item => item != null && item.collectionId == collectionId);
            Warn($"Reward for '{collectionId}' was not saved: {exception.Message}");
            return false;
        }
        finally { _mutating = false; }

        PublishChanged();
        return true;
    }

    public static bool MarkCardsSeen(IEnumerable<string> cardIds)
    {
        if (_mutating || !IsInitialized || cardIds == null)
            return false;
        var changed = new List<CollectionCardSaveData>();
        var uniqueIds = new HashSet<string>(cardIds, StringComparer.Ordinal);
        foreach (string id in uniqueIds)
        {
            if (!GetCardProgress(id).IsUnlocked)
                continue;
            CollectionCardSaveData card = Data.PlayerData.FindCollectionCardProgress(id);
            if (card != null && card.quantity > 0 && card.isNew)
                changed.Add(card);
        }
        if (changed.Count == 0)
            return false;
        _mutating = true;
        try
        {
            foreach (CollectionCardSaveData card in changed)
                card.isNew = false;
            Data.SaveData();
        }
        catch (Exception exception)
        {
            foreach (CollectionCardSaveData card in changed)
                card.isNew = true;
            Warn($"Viewed cards were not saved: {exception.Message}");
            return false;
        }
        finally { _mutating = false; }
        PublishChanged();
        return true;
    }

    public static bool UnlockCollection(string collectionId)
    {
        if (_mutating || !IsInitialized || _config.GetCollection(collectionId) == null ||
            Data.PlayerData.UnlockedCollections.Contains(collectionId))
            return false;
        PlayerData player = Data.PlayerData;
        _mutating = true;
        try
        {
            player.UnlockedCollections.Add(collectionId);
            Data.SaveData();
        }
        catch (Exception exception)
        {
            player.UnlockedCollections.Remove(collectionId);
            Warn($"Unlock '{collectionId}' was not saved: {exception.Message}");
            return false;
        }
        finally { _mutating = false; }
        PublishChanged();
        return true;
    }

    public static void ValidatePlayerProgress()
    {
        if (_config == null || Data.PlayerData == null)
            return;
        foreach (CollectionCardSaveData card in Data.PlayerData.CollectionCards)
        {
            if (card != null && !_config.TryGetCard(card.cardId, out _, out _))
                Warn($"Save contains retired/unknown card '{card.cardId}'. It is preserved and ignored by Collection UI.");
        }
        foreach (CollectionSaveData collection in Data.PlayerData.CollectionProgress)
        {
            if (collection != null && _config.GetCollection(collection.collectionId) == null)
                Warn($"Save contains retired/unknown collection '{collection.collectionId}'. It is preserved.");
        }
    }

    private static void PublishCard(CardAddResult result)
    {
        PendingNotifications.Enqueue(result);
        if (_publishing)
            return;
        _publishing = true;
        try
        {
            while (PendingNotifications.Count > 0)
            {
                CardAddResult next = PendingNotifications.Dequeue();
                Delegate[] listeners = Observer.CollectionCardAdded?.GetInvocationList();
                if (listeners != null)
                {
                    foreach (Delegate listener in listeners)
                    {
                        try { ((Action<CardAddResult>)listener)(next); }
                        catch (Exception exception) { Debug.LogException(exception); }
                    }
                }
                PublishChanged();
            }
        }
        finally { _publishing = false; }
    }

    private static void PublishChanged()
    {
        Delegate[] listeners = Observer.CollectionChanged?.GetInvocationList();
        if (listeners == null)
            return;
        foreach (Delegate listener in listeners)
        {
            try { ((Action)listener)(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void Warn(string message) => Debug.LogWarning("[CollectionManager] " + message);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool DebugResetCollection(string collectionId, bool rewardOnly = false)
    {
        if (_mutating || !IsInitialized)
            return false;
        CollectionData collection = _config.GetCollection(collectionId);
        if (collection == null)
            return false;
        var cards = new List<CollectionCardSaveData>(Data.PlayerData.CollectionCards);
        var progress = new List<CollectionSaveData>(Data.PlayerData.CollectionProgress);
        try
        {
            if (!rewardOnly)
                Data.PlayerData.CollectionCards.RemoveAll(item => item != null && collection.GetCard(item.cardId) != null);
            Data.PlayerData.CollectionProgress.RemoveAll(item => item != null && item.collectionId == collectionId);
            Data.SaveData();
        }
        catch
        {
            Data.PlayerData.CollectionCards = cards;
            Data.PlayerData.CollectionProgress = progress;
            throw;
        }
        PublishChanged();
        return true;
    }
#endif
}
