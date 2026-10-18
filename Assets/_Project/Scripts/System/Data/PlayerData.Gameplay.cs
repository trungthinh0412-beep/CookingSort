using System;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

public partial class PlayerData
{
    [SerializeField] private int currentShuffle;
    [SerializeField] private int currentBomb;
    [SerializeField] private int currentMoreDeal = 3;
    [SerializeField] private int currentMagicSwap = 3;
    [SerializeField] private int currentMagnet = 3;
    [SerializeField] private int currentLighter = 3;
    [SerializeField] private int currentWildCard = 3;
    [SerializeField] private int currentStackCard;
    [SerializeField] private int currentUpgradeCard;
    [SerializeField] private int currentKingCard;
    [SerializeField] private int currentExtraTray = 3;
    [SerializeField] private int currentFreeMoves = 3;
    [SerializeField, JsonProperty("stackCardExpiryPoint")]
    private string stackCardExpiryPoint = string.Empty;
    [SerializeField, JsonProperty("upgradeCardExpiryPoint")]
    private string upgradeCardExpiryPoint = string.Empty;
    [SerializeField, JsonProperty("kingCardExpiryPoint")]
    private string kingCardExpiryPoint = string.Empty;

    public int CurrentShuffle
    {
        get => currentShuffle;
        set
        {
            currentShuffle = value;
            Observer.UseBooster?.Invoke(BoosterType.Shuffle);
        }
    }

    public int CurrentBomb
    {
        get => currentBomb;
        set
        {
            currentBomb = value;
            Observer.UseBooster?.Invoke(BoosterType.Bomb);
        }
    }

    public int CurrentMoreDeal
    {
        get => currentMoreDeal;
        set
        {
            currentMoreDeal = Mathf.Max(0, value);
            Observer.UseBooster?.Invoke(BoosterType.MoreDeal);
        }
    }

    public int CurrentMagicSwap
    {
        get => currentMagicSwap;
        set
        {
            currentMagicSwap = Mathf.Max(0, value);
            Observer.UseBooster?.Invoke(BoosterType.MagicMove);
        }
    }

    public int CurrentMagnet
    {
        get => currentMagnet;
        set
        {
            currentMagnet = Mathf.Max(0, value);
            Observer.UseBooster?.Invoke(BoosterType.Magnet);
        }
    }

    public int CurrentLighter
    {
        get => currentLighter;
        set
        {
            currentLighter = Mathf.Max(0, value);
            Observer.UseBooster?.Invoke(BoosterType.Lighter);
        }
    }

    public int GetPreLevelCardAmount(CardType cardType)
    {
        return cardType switch
        {
            CardType.WildCard => currentWildCard,
            CardType.StackCard => currentStackCard,
            CardType.UpgradeCard => currentUpgradeCard,
            CardType.KingCard => currentKingCard,
            _ => 0
        };
    }

    public void SetPreLevelCardAmount(CardType cardType, int amount)
    {
        int safeAmount = Mathf.Max(0, amount);

        switch (cardType)
        {
            case CardType.WildCard:
                currentWildCard = safeAmount;
                break;
            case CardType.StackCard:
                currentStackCard = safeAmount;
                break;
            case CardType.UpgradeCard:
                currentUpgradeCard = safeAmount;
                break;
            case CardType.KingCard:
                currentKingCard = safeAmount;
                break;
            default:
                return;
        }

        Observer.PreLevelCardChanged?.Invoke(cardType);
    }

    public bool IsTimedPreLevelCardActive(CardType cardType)
    {
        return GetTimedPreLevelCardExpiry(cardType) > DateTime.UtcNow;
    }

    public TimeSpan GetTimedPreLevelCardRemaining(CardType cardType)
    {
        TimeSpan remaining = GetTimedPreLevelCardExpiry(cardType) - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public void AddTimedPreLevelCardTime(CardType cardType, float hours)
    {
        if (hours <= 0f || !IsTimedPreLevelCardType(cardType))
            return;

        DateTime expiry = GetTimedPreLevelCardExpiry(cardType);
        if (expiry < DateTime.UtcNow)
            expiry = DateTime.UtcNow;

        SetTimedPreLevelCardExpiry(cardType, expiry.AddHours(hours));
        Observer.PreLevelCardChanged?.Invoke(cardType);
    }

    private DateTime GetTimedPreLevelCardExpiry(CardType cardType)
    {
        string value = cardType switch
        {
            CardType.StackCard => stackCardExpiryPoint,
            CardType.UpgradeCard => upgradeCardExpiryPoint,
            CardType.KingCard => kingCardExpiryPoint,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(value))
            return DateTime.MinValue;

        return DateTime.TryParseExact(
            value,
            Utility.DateTimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime expiry)
            ? expiry
            : DateTime.MinValue;
    }

    private void SetTimedPreLevelCardExpiry(CardType cardType, DateTime expiry)
    {
        string value = expiry.ToUniversalTime().ToString(
            Utility.DateTimeFormat,
            CultureInfo.InvariantCulture);

        switch (cardType)
        {
            case CardType.StackCard:
                stackCardExpiryPoint = value;
                break;
            case CardType.UpgradeCard:
                upgradeCardExpiryPoint = value;
                break;
            case CardType.KingCard:
                kingCardExpiryPoint = value;
                break;
        }
    }

    private static bool IsTimedPreLevelCardType(CardType cardType)
    {
        return cardType == CardType.StackCard ||
               cardType == CardType.UpgradeCard ||
               cardType == CardType.KingCard;
    }

    public int CurrentExtraTray
    {
        get => currentExtraTray;
        set
        {
            currentExtraTray = Mathf.Max(0, value);
            Observer.UseBooster?.Invoke(BoosterType.ExtraTray);
        }
    }

    public int CurrentFreeMoves
    {
        get => currentFreeMoves;
        set
        {
            currentFreeMoves = Mathf.Max(0, value);
            Observer.UseBooster?.Invoke(BoosterType.FreeMoves);
        }
    }
}
