using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CollectionData", menuName = "ScriptableObject/Collection/CollectionData")]
public sealed class CollectionData : ScriptableObject
{
    [Tooltip("Stable save key. Do not change after releasing this collection.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite coverSprite;
    [SerializeField] private Sprite buttonSprite;
    [Min(1)] [SerializeField] private int unlockLevel = 39;
    [SerializeField] private List<CollectionCardData> cards = new List<CollectionCardData>();
    [SerializeField] private RewardData rewardData = new RewardData();

    public string Id => id;
    public string DisplayName => displayName;
    public Sprite CoverSprite => coverSprite;
    public Sprite ButtonSprite => buttonSprite;
    public int UnlockLevel => Mathf.Max(1, unlockLevel);
    public IReadOnlyList<CollectionCardData> Cards => cards;
    public int TotalCards => cards == null ? 0 : cards.Count;

    public bool HasReward => rewardData != null && (rewardData.totalGold > 0 || rewardData.totalStar > 0);
    public bool HasRewardData => rewardData != null;

    public CollectionCardData GetCard(string cardId)
    {
        if (cards == null || string.IsNullOrWhiteSpace(cardId))
            return null;
        foreach (CollectionCardData card in cards)
        {
            if (card != null && string.Equals(card.Id, cardId, StringComparison.Ordinal))
                return card;
        }
        return null;
    }

    // RewardData.CheckAndClaim resets its values, so never return the asset's instance.
    public RewardData Reward => new RewardData(
        rewardData == null ? 0 : rewardData.totalGold,
        rewardData == null ? 0 : rewardData.totalStar);

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");

        unlockLevel = Mathf.Max(1, unlockLevel);
        if (cards == null)
            cards = new List<CollectionCardData>();

        foreach (CollectionCardData card in cards)
            card?.EnsureId();
    }
}

[Serializable]
public sealed class CollectionCardData
{
    [Tooltip("Unique across all collections. Keep this key stable for saved progress.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [Min(1)] [SerializeField] private int unlockLevel = 1;

    public string Id => id;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public int UnlockLevel => Mathf.Max(1, unlockLevel);

    internal void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");
        unlockLevel = Mathf.Max(1, unlockLevel);
    }
}
