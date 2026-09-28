using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "FeatureConfig", menuName = "ScriptableObject/FeatureConfig")]
public class FeatureConfig : ScriptableObject
{
    public List<FeatureData> featureDataList;

    /// <summary>
    /// Get FeatureData that unlocks at the given level index, or null if none.
    /// </summary>
    public FeatureData GetFeatureDataAtLevel(int levelIndex)
    {
        return featureDataList.Find(f => f.levelIndex == levelIndex);
    }

    /// <summary>
    /// Get the next FeatureData that will unlock after the given level index, or null if no more features.
    /// </summary>
    public FeatureData GetNextFeatureData(int currentLevelIndex)
    {
        FeatureData next = null;
        foreach (var f in featureDataList)
        {
            if (f.levelIndex > currentLevelIndex)
            {
                if (next == null || f.levelIndex < next.levelIndex)
                {
                    next = f;
                }
            }
        }
        return next;
    }

    /// <summary>
    /// Check if there are any features that unlock after the given level index.
    /// </summary>
    public bool HasNextFeature(int currentLevelIndex)
    {
        return GetNextFeatureData(currentLevelIndex) != null;
    }
}

[Serializable]
public class FeatureData
{
    public FeatureType featureType;
    public string featureName;
    public Sprite sprite;
    public string description;
    public int levelIndex;
}

public enum FeatureType
{
    IceFood,
    MysteryBox,
    Rope,
    Teleport,
}
