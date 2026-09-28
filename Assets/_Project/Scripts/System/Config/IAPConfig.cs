using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "IAPConfig", menuName = "ScriptableObject/IAPConfig")]
public class IAPConfig : ScriptableObject
{
    public List<PackData> packData;

    public PackData GetPackData(PackName name)
    {
        return packData.Find(item => item.packName == name);
    }
}

[Serializable]
public class PackData
{
    public PackName packName;
    public string packId;
}

public enum PackName
{
    RemoveAds,
    SmallBundle,
    BigBundle,
    Gold1,
    Gold2,
    Gold3,
    Gold4,
    Gold5,
    Gold6
}
