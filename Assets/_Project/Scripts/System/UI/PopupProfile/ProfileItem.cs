using System;
using UnityEngine;
using UnityEngine.UI;

public class ProfileItem : MonoBehaviour
{
    [SerializeField] private Image img;
    [SerializeField] private GameObject selected;
    private int _index;
    private ProfileType _type;
    private Action<ProfileType, int> _actionSelect;
    public void Init(int index, ProfileType type, Sprite sprite, Action<ProfileType, int> actionSelect)
    {
        _index = index;
        _type = type;
        img.sprite = sprite;
        _actionSelect = actionSelect;
        UpdateDisplayState();
    }
    public void UpdateDisplayState(int stateIndex = -1) // stateIndex=-1 => check data from real data. stateIndex!=-1 => set stateSelect by stateIndex
    {
        if (stateIndex == -1)
        {
            if (_type == ProfileType.Avatar) stateIndex = Data.PlayerData.CurrentIndexAvatar;
            if (_type == ProfileType.AvatarFrame) stateIndex = Data.PlayerData.CurrentIndexFrame;
        }
        bool isSetected = stateIndex == _index;
        selected.SetActive(isSetected);
    }
    public void OnSelect()
    {
        _actionSelect?.Invoke(_type, _index);
    }
}