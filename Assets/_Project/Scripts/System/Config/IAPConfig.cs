using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "IAPConfig", menuName = "ScriptableObject/IAPConfig")]
public class IAPConfig : ScriptableObject
{
    public List<PackData> packData = new();

    public PackData GetPackData(PackName name)
    {
        return packData?.Find(item => item.packName == name);
    }
}

[Serializable]
public class PackData
{
    [Header("Store")]
    public PackName packName;
    public string packId;
    public ProductType productType = ProductType.Consumable;

    [Header("Shop Display")]
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    [Min(0)] public int sortOrder;
    public string badgeText;

    [Header("Price")]
    [FormerlySerializedAs("fallbackPrice")]
    [Min(0f)] public float price;
    [FormerlySerializedAs("fallbackCurrencyCode")]
    public string currencyCode = "USD";

    [Header("Rewards")]
    public List<ShopRewardData> rewards = new();
}

[Serializable]
public class ShopRewardData
{
    public ShopRewardType type;
    [Min(0)] public int amount;
}

public enum ShopRewardType
{
    Gold,
    Shuffle,
    Bomb,
    MoreDeal,
    MagicSwap,
    Magnet,
    InfiniteHeartHours,
    RemoveAdsYears,
    StackCardHours,
    UpgradeCardHours,
    KingCardHours
}

public enum PackName
{
    RemoveAds = 0,
    SuperDeal = 1,
    StarterBenefits = 2,
    VIPPack = 3,
    ProPack = 4,
    ChampionPack = 5,
    MasterPack = 6,
    CardMasterPack = 7,
    Gold1 = 8,
    Gold2 = 9,
    Gold3 = 10,
    Gold4 = 11,
    Gold5 = 12,
    Gold6 = 13
}
