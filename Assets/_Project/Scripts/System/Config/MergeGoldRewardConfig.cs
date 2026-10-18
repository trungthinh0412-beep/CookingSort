using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MergeGoldRewardConfig",
    menuName = "ScriptableObject/Merge Gold Reward Config"
)]
public class MergeGoldRewardConfig : ScriptableObject
{
    [SerializeField, Min(0)] private int defaultGoldReward;
    [SerializeField, Range(2, 8)] private int defaultVisualCoinCount = 8;
    [SerializeField] private List<MergeGoldRewardEntry> rewards =
        new List<MergeGoldRewardEntry>();

    public MergeGoldRewardEntry GetReward(CardType resultCardType)
    {
        if (rewards != null)
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].cardType == resultCardType)
                    return rewards[i];
            }
        }

        return new MergeGoldRewardEntry
        {
            cardType = resultCardType,
            goldAmount = defaultGoldReward,
            visualCoinCount = defaultVisualCoinCount
        };
    }
}

[Serializable]
public struct MergeGoldRewardEntry
{
    public CardType cardType;
    [Min(0)] public int goldAmount;
    [Range(2, 8)] public int visualCoinCount;
}
