using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CollectionConfig", menuName = "ScriptableObject/Collection/CollectionConfig")]
public sealed class CollectionConfig : ScriptableObject
{
    [Min(1)] [SerializeField] private int unlockLevel = 39;
    [SerializeField] private List<CollectionData> collections = new List<CollectionData>();

    public int UnlockLevel => Mathf.Max(1, unlockLevel);
    public IReadOnlyList<CollectionData> Collections => collections;

    public CollectionData GetCollection(string id)
    {
        if (collections == null || string.IsNullOrWhiteSpace(id))
            return null;

        foreach (CollectionData collection in collections)
        {
            if (collection != null && string.Equals(collection.Id, id, StringComparison.Ordinal))
                return collection;
        }

        return null;
    }

    public bool ValidateData(List<string> errors)
    {
        int initialCount = errors.Count;
        var collectionIds = new HashSet<string>(StringComparer.Ordinal);
        var cardIds = new HashSet<string>(StringComparer.Ordinal);

        if (collections == null)
        {
            errors.Add($"{name}: collection list is missing.");
            return false;
        }

        foreach (CollectionData collection in collections)
        {
            if (collection == null)
            {
                errors.Add($"{name}: a collection reference is missing.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(collection.Id) || !collectionIds.Add(collection.Id))
                errors.Add($"{collection.name}: missing or duplicate collection ID '{collection.Id}'.");

            if (!collection.HasRewardData)
                errors.Add($"{collection.name}: RewardData is null; use zero amounts for a collection without rewards.");
            RewardData reward = collection.Reward;
            if (reward.totalGold < 0 || reward.totalStar < 0)
                errors.Add($"{collection.name}: reward amounts cannot be negative.");

            if (collection.Cards == null)
                continue;

            foreach (CollectionCardData card in collection.Cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.Id) || !cardIds.Add(card.Id))
                    errors.Add($"{collection.name}: missing or duplicate card ID '{card?.Id}'.");
            }
        }

        return errors.Count == initialCount;
    }

    public bool TryGetCard(string cardId, out CollectionData collection, out CollectionCardData card)
    {
        collection = null;
        card = null;
        if (collections == null || string.IsNullOrWhiteSpace(cardId))
            return false;
        foreach (CollectionData candidate in collections)
        {
            CollectionCardData found = candidate == null ? null : candidate.GetCard(cardId);
            if (found == null)
                continue;
            // An ambiguous ID must never grant a card to the wrong collection.
            if (card != null)
            {
                collection = null;
                card = null;
                return false;
            }
            collection = candidate;
            card = found;
        }
        return card != null;
    }

    private void OnValidate()
    {
        unlockLevel = Mathf.Max(1, unlockLevel);
        var errors = new List<string>();
        ValidateData(errors);
        foreach (string error in errors)
            Debug.LogError($"[CollectionConfig] {error}", this);
    }
}
