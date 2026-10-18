using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "CardConfig", menuName = "ScriptableObject/CardConfig")]
public class CardConfig : ScriptableObject
{
    [Header("Card Prefabs")]
    [SerializeField] private GameObject numberCardPrefab;
    [SerializeField] private GameObject wildCardPrefab;
    [SerializeField] private GameObject downgradeCardPrefab;
    [SerializeField] private GameObject chainCardPrefab;
    [SerializeField] private GameObject frozenCardPrefab;
    [SerializeField] private GameObject ironCardPrefab;
    [SerializeField] private GameObject darkKingCardPrefab;

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

    public GameObject GetCardPrefab(CardType type)
    {
        // Replaced Card.IsNumberCardType(type) with a simple check since Card is deleted.
        if ((int)type >= 1 && (int)type <= 10) // Fallback heuristic or just assume it is handled below
            return numberCardPrefab;

        return type switch
        {
            CardType.WildCard => wildCardPrefab,
            CardType.DowngradeCard => downgradeCardPrefab,
            CardType.ChainCard => chainCardPrefab,
            CardType.FrozenCard => frozenCardPrefab,
            CardType.IronCard => ironCardPrefab,
            CardType.DarkingCard => darkKingCardPrefab,
            _ => numberCardPrefab // Defaulting to number card prefab
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
        return GetCardData(type);
    }
}
