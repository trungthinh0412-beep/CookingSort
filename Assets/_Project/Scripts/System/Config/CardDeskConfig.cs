using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DealWeightProfile
{
    [Tooltip("Number of values below Max Value used by this profile.")]
    [Range(1, 5)]
    public int lowerValueCount = 5;

    [Tooltip("Index 0 = max - 1, index 1 = max - 2, ...")]
    public List<float> weights = new List<float>();
}

[CreateAssetMenu(fileName = "CardDeskConfig", menuName = "ScriptableObject/CardDeskConfig")]
public class CardDeskConfig : ScriptableObject
{
    [Header("Fill Percentage")]
    [Range(0f, 1f)]
    public float minFillPercentage = 0.4f;
    [Range(0f, 1f)]
    public float maxFillPercentage = 0.6f;

    [Header("Tray Distribution")]
    [Range(0f, 1f)]
    [Tooltip("Chance that one otherwise eligible tray intentionally receives no cards during a deal.")]
    public float leaveOneTrayEmptyChance = 0.15f;

    [Range(0f, 1f)]
    [Tooltip("Chance that a tray may contain three different numeric card values after a deal. Otherwise it is limited to two.")]
    public float allowThreeValuesPerTrayChance = 0.15f;

    [Header("Distribution Weights")]
    [Tooltip("Curve để nội suy trọng số sinh bài. Trục X là CardType (0=thấp nhất, 1=cao nhất). Trục Y là Trọng số.")]
    [HideInInspector]
    public AnimationCurve weightDistribution = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0.2f)
    );

    [Header("Deal Value Weights")]
    [Tooltip("Weights are normalized automatically. The old curve above is kept only for existing asset compatibility.")]
    public List<DealWeightProfile> dealWeightProfiles =
        new List<DealWeightProfile>
        {
            new DealWeightProfile
            {
                lowerValueCount = 5,
                weights = new List<float> { 5f, 15f, 20f, 25f, 35f }
            },
            new DealWeightProfile
            {
                lowerValueCount = 4,
                weights = new List<float> { 15f, 20f, 25f, 30f }
            },
            new DealWeightProfile
            {
                lowerValueCount = 3,
                weights = new List<float> { 20f, 30f, 50f }
            },
            new DealWeightProfile
            {
                lowerValueCount = 2,
                weights = new List<float> { 30f, 70f }
            }
        };

    public DealWeightProfile GetDealWeightProfile(int lowerValueCount)
    {
        if (dealWeightProfiles == null)
            return null;

        for (int i = 0; i < dealWeightProfiles.Count; i++)
        {
            DealWeightProfile profile = dealWeightProfiles[i];

            if (profile != null &&
                profile.lowerValueCount == lowerValueCount)
            {
                return profile;
            }
        }

        return null;
    }

    [Header("Per Holder Limit")]
    [Min(1)]
    [Tooltip("Số card tối thiểu được random cho giới hạn của mỗi CardSlotHolder trong một lần Deal")]
    public int minCardsPerHolder = 2;

    [Min(1)]
    [Tooltip("Số card tối đa được random cho giới hạn của mỗi CardSlotHolder trong một lần Deal")]
    public int maxCardsPerHolder = 5;

    private void OnValidate()
    {
        minCardsPerHolder = Mathf.Max(1, minCardsPerHolder);
        maxCardsPerHolder = Mathf.Max(minCardsPerHolder, maxCardsPerHolder);
    }
}
