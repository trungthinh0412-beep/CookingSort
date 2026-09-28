using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ProfileConfig", menuName = "ScriptableObject/Profile/ProfileConfig")]
public class ProfileConfig : ScriptableObject
{
    [SerializeField] private List<ProfileUnitData> profileDatas;

    public ProfileUnitData GetProfileUnitData(ProfileType type)
    {
        if (profileDatas == null)
            return null;

        for (int i = 0; i < profileDatas.Count; i++)
        {
            if (profileDatas[i] != null && type == profileDatas[i].type)
                return profileDatas[i];
        }

        Debug.LogError($"[ProfileConfig] Profile type {type} was not found.");
        return null;
    }

    public Sprite GetSprite(ProfileType type, int index)
    {
        ProfileUnitData data = GetProfileUnitData(type);

        if (data == null || data.sprites == null || data.sprites.Count == 0)
            return null;

        int safeIndex = Mathf.Clamp(index, 0, data.sprites.Count - 1);
        return data.sprites[safeIndex];
    }

    public Sprite GetCurrentSpriteDataType(ProfileType type)
    {
        if (Data.PlayerData == null)
            return null;

        int index = type == ProfileType.Avatar
            ? Data.PlayerData.CurrentIndexAvatar
            : Data.PlayerData.CurrentIndexFrame;

        return GetSprite(type, index);
    }
}
public enum ProfileType
{
    Avatar,
    AvatarFrame
}

