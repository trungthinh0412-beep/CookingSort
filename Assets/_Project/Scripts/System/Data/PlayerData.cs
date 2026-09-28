using System;
using System.Globalization;
using UnityEngine;

[Serializable]
public partial class PlayerData
{
    public const int MaxNameLength = 8;

    [SerializeField] private bool isFirstPlaying = true;
    [SerializeField] private int currentLevelIndex = 1;
    [SerializeField] private int currentGold;
    [SerializeField] private int currentStar;
    [SerializeField] private int currentHeart = 5;
    [SerializeField] private int currentIndexFrame = 0;
    [SerializeField] private int currentIndexAvatar = 0;
    [SerializeField] private string currentName = "Default";
    [SerializeField] private string refillHeartPoint = DateTime.UtcNow.ToString(Utility.DateTimeFormat, CultureInfo.InvariantCulture);
    [SerializeField] private string infiniteHeartExpiryPoint = string.Empty;
    [SerializeField] private RewardData savingReward = new RewardData();

    public bool IsFirstPlaying
    {
        get => isFirstPlaying;
        set => isFirstPlaying = value;
    }

    public int CurrentLevelIndex
    {
        get
        {
            currentLevelIndex = Mathf.Max(1, currentLevelIndex);
            return currentLevelIndex;
        }
        set
        {
            int safeValue = Mathf.Max(1, value);

            if (currentLevelIndex == safeValue)
                return;

            currentLevelIndex = safeValue;
            Observer.LevelChanged?.Invoke(currentLevelIndex);
        }
    }

    public static string FormatLevel(int level)
    {
        return $"Lv. {Mathf.Max(1, level)}";
    }
    public int CurrentIndexAvatar
    {
        get => currentIndexAvatar;
        set
        {
            int safeValue = Mathf.Max(0, value);
            if (currentIndexAvatar == safeValue)
                return;

            currentIndexAvatar = safeValue;
            Observer.ProfileChanged?.Invoke();
        }
    }
    public int CurrentIndexFrame
    {
        get => currentIndexFrame;
        set
        {
            int safeValue = Mathf.Max(0, value);
            if (currentIndexFrame == safeValue)
                return;

            currentIndexFrame = safeValue;
            Observer.ProfileChanged?.Invoke();
        }
    }

    public int CurrentGold
    {
        get => currentGold;
        set
        {
            Observer.GoldChanged?.Invoke(value - currentGold);
            currentGold = value;
            Observer.GoldChangedDone?.Invoke();
        }
    }

    public int CurrentStar
    {
        get => currentStar;
        set
        {
            Observer.StarChanged?.Invoke(value - currentStar);
            currentStar = value;
            Observer.StarChangedDone?.Invoke();
        }
    }

    public int CurrentHeart
    {
        get => currentHeart;
        set
        {
            if (value < currentHeart && IsInfiniteHeart()) return;
            Observer.HeartChanged?.Invoke(value - currentHeart);
            currentHeart = Mathf.Max(0, value);
            Observer.HeartChangedDone?.Invoke();
        }
    }
    public string CurrentName
    {
        get
        {
            currentName = NormalizeName(currentName);
            return currentName;
        }
        set
        {
            string safeValue = NormalizeName(value);

            if (currentName == safeValue)
                return;

            currentName = safeValue;
            Observer.ProfileChanged?.Invoke();
        }
    }

    public static string NormalizeName(string value)
    {
        string safeValue = string.IsNullOrWhiteSpace(value)
            ? "Default"
            : value.Trim();

        int[] characterIndexes =
            StringInfo.ParseCombiningCharacters(safeValue);

        if (characterIndexes.Length > MaxNameLength)
        {
            safeValue = safeValue.Substring(
                0,
                characterIndexes[MaxNameLength]
            );
        }

        return safeValue;
    }

    public string RefillHeartPoint
    {
        get => refillHeartPoint;
        set => refillHeartPoint = value;
    }

    public string InfiniteHeartExpiryPoint
    {
        get => infiniteHeartExpiryPoint;
        set => infiniteHeartExpiryPoint = value;
    }

    public DateTime GetInfiniteHeartExpiry()
    {
        if (string.IsNullOrEmpty(infiniteHeartExpiryPoint)) return DateTime.MinValue;
        if (DateTime.TryParseExact(infiniteHeartExpiryPoint, Utility.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime expiry))
        {
            return expiry;
        }
        return DateTime.MinValue;
    }

    public bool IsInfiniteHeart()
    {
        return GetInfiniteHeartExpiry() > DateTime.UtcNow;
    }

    public void AddInfiniteHeartTime(float hours)
    {
        DateTime current = GetInfiniteHeartExpiry();
        if (current < DateTime.UtcNow)
        {
            current = DateTime.UtcNow;
        }
        current = current.AddHours(hours);
        InfiniteHeartExpiryPoint = current.ToString(Utility.DateTimeFormat, CultureInfo.InvariantCulture);
    }

    public RewardData SavingReward
    {
        get => savingReward;
        set => savingReward = value;
    }
}

[Serializable]
public class RewardData
{
    public int totalGold;
    public int totalStar;

    public void CheckAndClaim()
    {
        if (IsEmpty()) return;
        Claim();
    }

    private void Claim()
    {
        Data.PlayerData.CurrentGold += totalGold;
        Data.PlayerData.CurrentStar += totalStar;
        Reset();
    }

    private void Reset()
    {
        totalGold = 0;
        totalStar = 0;
    }

    private bool IsEmpty()
    {
        return totalGold == 0 && totalStar == 0;
    }

    public RewardData()
    {

    }

    public RewardData(int totalGold)
    {
        this.totalGold = totalGold;
    }

    public RewardData(int totalGold, int totalStar)
    {
        this.totalGold = totalGold;
        this.totalStar = totalStar;
    }
}
