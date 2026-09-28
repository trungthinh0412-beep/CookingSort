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
    Shuffle,
    Bomb,
    MoreDeal,
    MagicSwap,
    Magnet,
}
