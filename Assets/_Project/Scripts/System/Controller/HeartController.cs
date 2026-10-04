using System;
using System.Globalization;
using CustomInspector;
using UnityEngine;

public class HeartController : SingletonDontDestroy<HeartController>
{
    [SerializeField] private HeartConfig heartConfig;

    // Tracking online timer
    private float _timer;
    // The refill timestamp is persisted for offline recovery. Updating it every
    // frame creates a DateTime string allocation on every rendered frame.
    private int _lastRecordedRemainingSeconds = int.MinValue;
    // Cache the max and refill time purely for shorter access
    public int MaxHeart => heartConfig != null ? heartConfig.maxHeart : 5;
    public int RefillTimeInSeconds => heartConfig != null ? heartConfig.refillTimeInSeconds : 1800;
    public bool HasPlayableHeart =>
        Data.PlayerData != null &&
        (Data.PlayerData.IsInfiniteHeart() || Data.PlayerData.CurrentHeart > 0);

    protected override void Awake()
    {
        base.Awake();
        _timer = RefillTimeInSeconds;
    }

    private void Start()
    {
        CheckOfflineHeart();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            CheckOfflineHeart();
        }
    }

    private void CheckOfflineHeart()
    {
        if (heartConfig == null || Data.PlayerData == null) return;

        int currentHeart = Mathf.Min(Data.PlayerData.CurrentHeart, MaxHeart);
        if (currentHeart != Data.PlayerData.CurrentHeart)
            Data.PlayerData.CurrentHeart = currentHeart;

        if (currentHeart >= MaxHeart)
        {
            UpdateRefillPointToNow();
            _timer = RefillTimeInSeconds;
            _lastRecordedRemainingSeconds = RefillTimeInSeconds;
            return;
        }

        if (DateTime.TryParseExact(Data.PlayerData.RefillHeartPoint, Utility.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime lastPoint))
        {
            TimeSpan diff = DateTime.UtcNow - lastPoint;
            if (diff.TotalSeconds > 0)
            {
                int heartToAdd = (int)(diff.TotalSeconds / RefillTimeInSeconds);

                if (heartToAdd > 0)
                {
                    int newHeart = Mathf.Min(currentHeart + heartToAdd, MaxHeart);
                    Data.PlayerData.CurrentHeart = newHeart;

                    if (newHeart >= MaxHeart)
                    {
                        UpdateRefillPointToNow();
                        _timer = RefillTimeInSeconds;
                    }
                    else
                    {
                        // Calculate remainder and keep progress
                        int remainingSeconds = (int)(diff.TotalSeconds % RefillTimeInSeconds);
                        Data.PlayerData.RefillHeartPoint = DateTime.UtcNow.AddSeconds(-remainingSeconds).ToString(Utility.DateTimeFormat, CultureInfo.InvariantCulture);
                        _timer = RefillTimeInSeconds - remainingSeconds;
                        _lastRecordedRemainingSeconds = Mathf.CeilToInt(_timer);
                    }

                    Data.SaveData();
                }
                else
                {
                    // No full heart added, just update timer visually based on passed time
                    _timer = RefillTimeInSeconds - (float)diff.TotalSeconds;
                    _lastRecordedRemainingSeconds = Mathf.CeilToInt(_timer);
                }
            }
        }
        else
        {
            // Fallback if parsing fails
            UpdateRefillPointToNow();
            _timer = RefillTimeInSeconds;
            _lastRecordedRemainingSeconds = RefillTimeInSeconds;
        }
    }

    private void Update()
    {
        if (heartConfig == null || Data.PlayerData == null) return;

        if (Data.PlayerData.CurrentHeart >= MaxHeart)
        {
            if (_timer != RefillTimeInSeconds)
            {
                _timer = RefillTimeInSeconds;
                UpdateRefillPointToNow();
            }
            return;
        }

        _timer -= Time.deltaTime;

        UpdateRefillPointForCurrentSecond();

        if (_timer <= 0)
        {
            Data.PlayerData.CurrentHeart = Mathf.Min(
                Data.PlayerData.CurrentHeart + 1,
                MaxHeart
            );
            _timer = RefillTimeInSeconds;
            UpdateRefillPointToNow();
            _lastRecordedRemainingSeconds = RefillTimeInSeconds;
            Data.SaveData();
        }
    }

    private void UpdateRefillPointToNow()
    {
        if (Data.PlayerData != null)
        {
            Data.PlayerData.RefillHeartPoint = DateTime.UtcNow.ToString(Utility.DateTimeFormat, CultureInfo.InvariantCulture);
        }
    }

    private void UpdateRefillPointForCurrentSecond()
    {
        int remainingSeconds = Mathf.Clamp(
            Mathf.CeilToInt(_timer),
            0,
            RefillTimeInSeconds
        );
        if (remainingSeconds == _lastRecordedRemainingSeconds)
            return;

        float elapsedInCurrentInterval = RefillTimeInSeconds - _timer;
        Data.PlayerData.RefillHeartPoint = DateTime.UtcNow
            .AddSeconds(-elapsedInCurrentInterval)
            .ToString(Utility.DateTimeFormat, CultureInfo.InvariantCulture);
        _lastRecordedRemainingSeconds = remainingSeconds;
    }

    public int GetRemainingDisplaySeconds()
    {
        if (Data.PlayerData == null)
            return int.MinValue;

        if (Data.PlayerData.IsInfiniteHeart())
        {
            TimeSpan diff = Data.PlayerData.GetInfiniteHeartExpiry() - DateTime.UtcNow;
            return Mathf.Max(0, Mathf.CeilToInt((float)diff.TotalSeconds));
        }

        return Data.PlayerData.CurrentHeart >= MaxHeart
            ? -1
            : Mathf.CeilToInt(_timer);
    }

    public string GetHeartString()
    {
        if (Data.PlayerData == null) return "0/0";
        if (Data.PlayerData.IsInfiniteHeart()) return "∞";
        return $"{Data.PlayerData.CurrentHeart}/{MaxHeart}";
    }

    public bool TryConsumeHeart()
    {
        if (Data.PlayerData == null)
            return false;

        if (Data.PlayerData.IsInfiniteHeart())
            return true;

        if (Data.PlayerData.CurrentHeart <= 0)
            return false;

        bool wasFull = Data.PlayerData.CurrentHeart >= MaxHeart;
        Data.PlayerData.CurrentHeart--;

        if (wasFull)
        {
            _timer = RefillTimeInSeconds;
            UpdateRefillPointToNow();
        }

        Data.SaveData();
        return true;
    }

    public bool AddHeart(int amount = 1)
    {
        if (Data.PlayerData == null || amount <= 0)
            return false;

        int currentHeart = Mathf.Min(Data.PlayerData.CurrentHeart, MaxHeart);
        int newHeart = Mathf.Min(currentHeart + amount, MaxHeart);
        if (newHeart <= currentHeart)
            return false;

        Data.PlayerData.CurrentHeart = newHeart;
        if (newHeart >= MaxHeart)
        {
            _timer = RefillTimeInSeconds;
            UpdateRefillPointToNow();
        }

        Data.SaveData();
        return true;
    }

    public bool RefillHearts()
    {
        if (Data.PlayerData == null || Data.PlayerData.CurrentHeart >= MaxHeart)
            return false;

        Data.PlayerData.CurrentHeart = MaxHeart;
        _timer = RefillTimeInSeconds;
        UpdateRefillPointToNow();
        Data.SaveData();
        return true;
    }

    public string GetRemainingTime()
    {
        if (Data.PlayerData == null) return "Max";

        if (Data.PlayerData.IsInfiniteHeart())
        {
            TimeSpan diff = Data.PlayerData.GetInfiniteHeartExpiry() - DateTime.UtcNow;
            if (diff.TotalSeconds > 0)
            {
                int h = (int)diff.TotalHours;
                int m = diff.Minutes;
                return string.Format("{0:00}:{1:00}", h, m);
            }
        }

        if (Data.PlayerData.CurrentHeart >= MaxHeart) return "Max";
        int totalSecondsLeft = Mathf.CeilToInt(_timer);
        int mm = totalSecondsLeft / 60;
        int ss = totalSecondsLeft % 60;
        return string.Format("{0:00}:{1:00}", mm, ss);
    }

    [Button]
    public void Test()
    {
        TryConsumeHeart();
    }
}
