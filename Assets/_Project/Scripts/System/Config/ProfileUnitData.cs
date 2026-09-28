using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ProfileUnitData", menuName = "ScriptableObject/Profile/ProfileUnitData")]
public class ProfileUnitData : ScriptableObject
{
    public ProfileType type;
    public List<Sprite> sprites;
}
