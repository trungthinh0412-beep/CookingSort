using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BoosterConfig", menuName = "ScriptableObject/BoosterConfig")]
public class BoosterConfig : ScriptableObject
{
    public int levelTutBomb = 4;
    public int levelTutShuffer = 2;
    public List<BoosterData> boosterData;

    public BoosterData GetBoosterData(BoosterType type)
    {
        return boosterData != null
            ? boosterData.Find(item => item.boosterType == type)
            : null;
    }
}

[Serializable]
public class BoosterData
{
    public BoosterType boosterType;
    public Sprite sprite;
    public int price;
}

public enum BoosterType
{
    Shuffle = 0,
    Bomb = 1,
    MoreDeal = 2,
    MagicMove = 3,
    Magnet = 4,
    // Reuse the retired inventory WildCard value so the following saved
    // booster values remain compatible.
    Lighter = 5,
    ExtraTray = 6,
    FreeMoves = 7,
}
