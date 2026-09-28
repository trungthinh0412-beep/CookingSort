using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Switcher : MonoBehaviour
{
    [SerializeField] private SettingType settingType;
    [SerializeField] private Image buttonImage;

    [Header("Visual")]
    [SerializeField] private Material disableMaterial;

    private Coroutine _refreshCoroutine;

    private void OnEnable()
    {
        Refresh();
    }

    public void OnClickSwitcher()
    {
        switch (settingType)
        {
            case SettingType.BackgroundMusic:
                Data.PlayerData.MusicState =
                    !Data.PlayerData.MusicState;
                break;

            case SettingType.FxSound:
                Data.PlayerData.SoundState =
                    !Data.PlayerData.SoundState;
                break;

            case SettingType.Vibration:
                Data.PlayerData.VibrationState =
                    !Data.PlayerData.VibrationState;
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        Data.SaveData();

        if (_refreshCoroutine != null)
            StopCoroutine(_refreshCoroutine);

        _refreshCoroutine =
            StartCoroutine(RefreshAfterClick());
    }

    private IEnumerator RefreshAfterClick()
    {
        yield return new WaitForSecondsRealtime(0.12f);

        Refresh();

        _refreshCoroutine = null;
    }

    private void Refresh()
    {
        if (buttonImage == null)
            return;

        bool isOn = GetState();

        buttonImage.material =
            isOn ? null : disableMaterial;

        buttonImage.SetMaterialDirty();
    }

    private bool GetState()
    {
        switch (settingType)
        {
            case SettingType.BackgroundMusic:
                return Data.PlayerData.MusicState;

            case SettingType.FxSound:
                return Data.PlayerData.SoundState;

            case SettingType.Vibration:
                return Data.PlayerData.VibrationState;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public enum SettingType
{
    BackgroundMusic,
    FxSound,
    Vibration
}