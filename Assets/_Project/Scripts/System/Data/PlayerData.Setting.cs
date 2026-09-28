using UnityEngine;

public partial class PlayerData
{
    [SerializeField] private bool musicState = true;
    [SerializeField] private bool soundState = true;
    [SerializeField] private bool vibrationState = true;
    [SerializeField] private bool fastGameSpeed;

    public bool MusicState
    {
        get => musicState;
        set
        {
            musicState = value;
            Observer.MusicChanged?.Invoke();
        }
    }

    public bool SoundState
    {
        get => soundState;
        set
        {
            soundState = value;
            Observer.SoundChanged?.Invoke();
        }
    }

    public bool VibrationState
    {
        get => vibrationState;
        set
        {
            vibrationState = value;
            Observer.VibrationChanged?.Invoke();
        }
    }

    public bool FastGameSpeed
    {
        get => fastGameSpeed;
        set => fastGameSpeed = value;
    }
}
