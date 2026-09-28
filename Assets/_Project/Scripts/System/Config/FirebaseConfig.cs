using System;
using System.Collections.Generic;
using CustomInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "FirebaseConfig", menuName = "ScriptableObject/FirebaseConfig")]
public class FirebaseConfig : ScriptableObject
{
    public List<CustomRemoteData> defaultRemoteData;
}

[Serializable]
public class CustomRemoteData
{
    [EnumExtend] public CustomRemoteDataKey key;
    public string value;
}

public enum CustomRemoteDataKey
{
    Test
}