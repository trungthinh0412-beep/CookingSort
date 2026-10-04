using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "CardConfig", menuName = "ScriptableObject/CardConfig")]
public class CardConfig : ScriptableObject
{
    [Header("Card Prefabs")]
    [SerializeField] private Card numberCardPrefab;
    [SerializeField] private Card wildCardPrefab;
    [SerializeField] private Card downgradeCardPrefab;
    [SerializeField] private Card chainCardPrefab;
    [SerializeField] private Card frozenCardPrefab;
    [SerializeField] private Card ironCardPrefab;
    [SerializeField] private Card darkKingCardPrefab;

    [Header("Cards")]
    public List<CardData> cards;
    [Header("Pre-Level Cards")]
    public List<PreLevelCardData> preLevelCards;
    [Header("Obstacle-Level Cards")]
    public List<ObstacleLevelCardData> obstacleLevelCards;

    [Header("Dark King Card")]
    [FormerlySerializedAs("kingMovesBeforeTrayLock")]
    [SerializeField, Min(1), Tooltip(
        "Gia tri mac dinh khi them Dark King moi vao level. Tung la co the override rieng.")]
    private int defaultDarkKingMovesBeforeTrayLock = 3;

    public int DefaultDarkKingMovesBeforeTrayLock =>
        Mathf.Max(1, defaultDarkKingMovesBeforeTrayLock);

    [Header("Frozen Card")]
    [SerializeField, Min(1), Tooltip("So move mac dinh de la so dong bang mo khoa. Tung la co the override rieng.")]
    private int defaultFrozenMovesBeforeOpen = 3;

    public int DefaultFrozenMovesBeforeOpen =>
        Mathf.Max(1, defaultFrozenMovesBeforeOpen);

    public Card GetCardPrefab(CardType type)
    {
        if (Card.IsNumberCardType(type))
            return numberCardPrefab;

        return type switch
        {
            CardType.WildCard => wildCardPrefab,
            CardType.DowngradeCard => downgradeCardPrefab,
            CardType.ChainCard => chainCardPrefab,
            CardType.FrozenCard => frozenCardPrefab,
            CardType.IronCard => ironCardPrefab,
            CardType.DarkingCard => darkKingCardPrefab,
            _ => null
        };
    }

    public CardData GetCardData(CardType type)
    {
        return cards?.Find(item => item.cardType == type);
    }

    public bool TryGetGlowColor(CardType type, out Color color)
    {
        CardData data = GetEditorCardData(type);
        if (data != null)
        {
            color = data.glowColor;
            return true;
        }

        color = Color.white;
        return false;
    }

    public PreLevelCardData GetPreLevelCardData(CardType type)
    {
        return preLevelCards?.Find(item => item.cardType == type);
    }

    public ObstacleLevelCardData GetObstacleLevelCardData(CardType type)
    {
        return obstacleLevelCards?.Find(item => item.cardType == type);
    }
    public List<GameObject> GetObstacleVisualPrefabs(CardType type)
    {
        return GetObstacleLevelCardData(type)?.customVisualPrefabs;
    }

    public GameObject GetObstacleVisualPrefab(CardType type, int index = 0)
    {
        List<GameObject> prefabs = GetObstacleVisualPrefabs(type);
        if (prefabs != null && index >= 0 && index < prefabs.Count)
        {
            return prefabs[index];
        }
        return null;
    }

    public CardData GetEditorCardData(CardType type)
    {
        return GetCardData(type) ??
               (CardData)GetPreLevelCardData(type) ??
               (CardData)GetObstacleLevelCardData(type);
    }

    public List<CardData> GetEditorCards()
    {
        List<CardData> result = new List<CardData>();
        if (cards != null)
            result.AddRange(cards);
        if (preLevelCards != null)
            result.AddRange(preLevelCards);
        if (obstacleLevelCards != null)
            result.AddRange(obstacleLevelCards);
        return result;
    }

    public bool IsPreLevelCard(CardType type)
    {
        return GetPreLevelCardData(type) != null;
    }

    public bool IsObstacleLevelCard(CardType type)
    {
        return GetObstacleLevelCardData(type) != null;
    }

    public Sprite GetPreLevelIcon(CardType type)
    {
        PreLevelCardData data = GetPreLevelCardData(type);
        if (data == null)
            return null;

        return data.preLevelIcon;
    }
}

[Serializable]
public class CardData
{
    public CardType cardType;
    public Sprite icon;
    [Tooltip("Mau cua Glow_card khi la bai duoc chon.")]
    public Color glowColor = Color.white;
}

[Serializable]
public class PreLevelCardData : CardData
{
    [Tooltip("Icon booster hien trong cac popup truoc level.")]
    public Sprite preLevelIcon;
    [Min(0), Tooltip("Gia mua mot pre-level card trong PopupBooster.")]
    public int preLevelPrice;
}

[Serializable]
public class ObstacleLevelCardData : CardData
{
    [Tooltip("Danh sach cac Prefab hieu ung / VFX / Prefab Anim rieng cho la bai chuong ngai vat (Dung dau + trong Inspector de them).")]
    public List<GameObject> customVisualPrefabs = new List<GameObject>();
}

public enum CardType
{
    Card1,
    Card2,
    Card3,
    Card4,
    Card5,
    Card6,
    Card7,
    Card8,
    Card9,
    Card10,
    Card11,
    Card12,
    Card13,
    Card14,
    Card15,
    Card16,
    Card17,
    Card18,
    Card19,
    Card20,
    WildCard,
    StackCard,
    UpgradeCard,
    KingCard,
    DowngradeCard,
    ChainCard,
    FrozenCard,
    IronCard,
    DarkingCard
}
