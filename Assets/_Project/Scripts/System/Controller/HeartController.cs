using System;
using System.Globalization;
using CustomInspector;
using UnityEngine;

public class HeartController : SingletonDontDestroy<HeartController>
{
    [SerializeField] private HeartConfig heartConfig;

    // Tracking online timer
    private float _timer;
    // Cache the max and refill time purely for shorter access
    public int MaxHeart => heartConfig != null ? heartConfig.maxHeart : 5;
    public int RefillTimeInSeconds => heartConfig != null ? heartConfig.refillTimeInSeconds : 600;

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

        int currentHeart = Data.PlayerData.CurrentHeart;
        if (currentHeart >= MaxHeart)
        {
            UpdateRefillPointToNow();
            _timer = RefillTimeInSeconds;
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
                    }
                }
                else
                {
                    // No full heart added, just update timer visually based on passed time
                    _timer = RefillTimeInSeconds - (float)diff.TotalSeconds;
                }
            }
        }
        else
        {
            // Fallback if parsing fails
            UpdateRefillPointToNow();
            _timer = RefillTimeInSeconds;
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

        // Frequently update the RefillHeartPoint to the exact current time (minus whatever progress we made in the current timer)
        // so that if the app is abruptly closed, the PlayerDataController saves the correct timestamp.
        float elapsedInCurrentInterval = RefillTimeInSeconds - _timer;
        Data.PlayerData.RefillHeartPoint = DateTime.UtcNow.AddSeconds(-elapsedInCurrentInterval).ToString(Utility.DateTimeFormat, CultureInfo.InvariantCulture);

        if (_timer <= 0)
        {
            Data.PlayerData.CurrentHeart++;
            _timer = RefillTimeInSeconds;
            UpdateRefillPointToNow();
        }
    }

    private void UpdateRefillPointToNow()
    {
        if (Data.PlayerData != null)
        {
            Data.PlayerData.RefillHeartPoint = DateTime.UtcNow.ToString(Utility.DateTimeFormat, CultureInfo.InvariantCulture);
        }
    }

    public string GetHeartString()
    {
        if (Data.PlayerData == null) return "0/0";
        if (Data.PlayerData.IsInfiniteHeart()) return "∞";
        return $"{Data.PlayerData.CurrentHeart}/{MaxHeart}";
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
        Data.PlayerData.CurrentHeart -= 1;
    }
}
