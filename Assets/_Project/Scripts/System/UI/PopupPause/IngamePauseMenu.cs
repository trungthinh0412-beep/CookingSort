using UnityEngine;

public class IngamePauseMenu : MonoBehaviour
{
    [SerializeField] private float normalGameTimeScale = 1f;
    [SerializeField] private float fastGameTimeScale = 1.5f;

    private float _selectedGameTimeScale = 1f;

    public void Open()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Close()
    {
        Time.timeScale = Mathf.Max(0.01f, _selectedGameTimeScale);
        gameObject.SetActive(false);
    }

    public void CloseImmediate()
    {
        _selectedGameTimeScale = Mathf.Max(0.01f, normalGameTimeScale);
        Time.timeScale = _selectedGameTimeScale;
        gameObject.SetActive(false);
    }

    public void OnClickCancel() => Close();

    public void OnClickMusic()
    {
        if (Data.PlayerData == null) return;
        Data.PlayerData.MusicState = !Data.PlayerData.MusicState;
        Data.SaveData();
    }

    public void OnClickSound()
    {
        if (Data.PlayerData == null) return;
        Data.PlayerData.SoundState = !Data.PlayerData.SoundState;
        Data.SaveData();
    }

    public void OnClickVibration()
    {
        if (Data.PlayerData == null) return;
        Data.PlayerData.VibrationState = !Data.PlayerData.VibrationState;
        Data.SaveData();
    }

    public void OnClickTimeScale()
    {
        _selectedGameTimeScale = Mathf.Approximately(
            _selectedGameTimeScale, normalGameTimeScale)
            ? fastGameTimeScale
            : normalGameTimeScale;
    }

    public void OnClickExit()
    {
        Close();
    }

    private void OnDisable()
    {
        if (Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = Mathf.Max(0.01f, _selectedGameTimeScale);
    }
}
