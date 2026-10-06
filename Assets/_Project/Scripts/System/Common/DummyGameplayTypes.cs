using System.Collections.Generic;
using UnityEngine;

public enum CardType
{
    None = 0,
    WildCard = 1,
    DowngradeCard = 2,
    ChainCard = 3,
    FrozenCard = 4,
    IronCard = 5,
    DarkingCard = 6, StackCard = 7, UpgradeCard = 8, KingCard = 9
}

[System.Serializable]
public class CardData
{
    public CardType cardType;
    public Color glowColor = Color.white;
}

[System.Serializable]
public class PreLevelCardData
{
    public CardType cardType;
}

[System.Serializable]
public class ObstacleLevelCardData
{
    public CardType cardType;
    public List<GameObject> customVisualPrefabs;
}
