using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupBuyBooster : Popup
{
    [SerializeField] private Image boosterIcon;
    [SerializeField] private TextMeshProUGUI description;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private BoosterConfig boosterConfig;
    [SerializeField] private CardConfig preLevelCardConfig;

    private BoosterType _boosterType;
    private CardType? _preLevelCardType;

    public void Init(BoosterType boosterType)
    {
        _boosterType = boosterType;
        _preLevelCardType = null;
    }

    public void InitPreLevelCard(CardType cardType)
    {
        _preLevelCardType = cardType;
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        Setup();
    }

    private void Setup()
    {
        if (_preLevelCardType.HasValue)
        {
            SetupPreLevelCard(_preLevelCardType.Value);
            return;
        }

        BoosterData data = boosterConfig != null
            ? boosterConfig.GetBoosterData(_boosterType)
            : null;
        if (data == null)
        {
            Debug.LogWarning(
                $"[PopupBuyBooster] Missing config for {_boosterType}."
            );
            return;
        }

        if (boosterIcon != null)
            boosterIcon.sprite = data.sprite;
        if (description != null)
            description.text = GetDescription(_boosterType);
        if (priceText != null)
            priceText.text = $"{data.price} <sprite name=\"gold\">";
    }

    private void SetupPreLevelCard(CardType cardType)
    {
        PreLevelCardData data = preLevelCardConfig != null
            ? preLevelCardConfig.GetPreLevelCardData(cardType)
            : null;

        if (description != null)
        {
            description.text = data != null
                ? $"Get one {cardType} card!"
                : "This card is unavailable.";
        }

        if (priceText != null)
            priceText.text = data != null ? "Free" : "--";
    }

    private static string GetDescription(BoosterType boosterType)
    {
        return boosterType switch
        {
            BoosterType.Shuffle => "Add 1 Shuffle and continue!",
            BoosterType.Bomb => "Add 1 Bomb and continue!",
            BoosterType.MoreDeal => "Get 5 extra moves!",
            BoosterType.MagicMove =>
                "Move matching cards onto a different tray!",
            BoosterType.Magnet =>
                "Collect matching cards into one tray!",
            BoosterType.Lighter => "Burn every card in one tray!",
            BoosterType.ExtraTray => "Unlock an Extra Tray!",
            BoosterType.FreeMoves => "Get 5 extra moves!",
            BoosterType.OrderBell => "Move a chosen waiting animal to the seat row!",
            BoosterType.LineUp => "Reorder waiting animals to match the bottom food!",
            BoosterType.Rewind => "Return the last seated animal to its queue!",
            BoosterType.TrashAway => "Remove a connected group of matching food!",
            _ => "Get this booster!"
        };
    }

    public void OnClickBack()
    {
        PlayClickSound();
        Hide(PopupAnimation.ScaleFade);
        Time.timeScale = 1f;
    }

    public void OnClickGetBoosterBuyAds()
    {
        PlayClickSound();

        if (AdsController.Instance == null)
            return;

        AdsController.Instance.ShowRewardAds(
            () =>
            {
                GiveBooster();
                Hide(PopupAnimation.ScaleFade);
            },
            placement: "PopupBuyBooster_OnClickGetBoosterBuyAds"
        );
    }

    public void OnClickBuyBooster()
    {
        PlayClickSound();

        int price = GetCurrentPrice();
        if (price < 0)
        {
            Observer.Notify?.Invoke(
                "Booster is not configured.",
                Vector3.zero
            );
            return;
        }

        if (Data.PlayerData == null ||
            Data.PlayerData.CurrentGold < price)
        {
            Observer.Notify?.Invoke("Not enough gold!", Vector3.zero);
            return;
        }

        Data.PlayerData.CurrentGold -= price;
        GiveBooster();
        Hide(PopupAnimation.ScaleFade);
    }

    private int GetCurrentPrice()
    {
        if (_preLevelCardType.HasValue || boosterConfig == null)
            return -1;

        BoosterData data = boosterConfig.GetBoosterData(_boosterType);
        return data != null ? data.price : -1;
    }

    private void GiveBooster()
    {
        if (Data.PlayerData == null)
            return;

        if (_preLevelCardType.HasValue)
        {
            CardType cardType = _preLevelCardType.Value;
            Data.PlayerData.SetPreLevelCardAmount(
                cardType,
                Data.PlayerData.GetPreLevelCardAmount(cardType) + 1
            );
            Data.SaveData();
            return;
        }

        switch (_boosterType)
        {
            case BoosterType.Shuffle:
                Data.PlayerData.CurrentShuffle++;
                break;
            case BoosterType.Bomb:
                Data.PlayerData.CurrentBomb++;
                break;
            case BoosterType.MoreDeal:
                Data.PlayerData.CurrentMoreDeal++;
                break;
            case BoosterType.MagicMove:
                Data.PlayerData.CurrentMagicSwap++;
                break;
            case BoosterType.Magnet:
                Data.PlayerData.CurrentMagnet++;
                break;
            case BoosterType.Lighter:
                Data.PlayerData.CurrentLighter++;
                break;
            case BoosterType.ExtraTray:
                Data.PlayerData.CurrentExtraTray++;
                break;
            case BoosterType.FreeMoves:
                Data.PlayerData.CurrentFreeMoves++;
                break;
            case BoosterType.OrderBell:
                Data.PlayerData.CurrentOrderBell++;
                break;
            case BoosterType.LineUp:
                Data.PlayerData.CurrentLineUp++;
                break;
            case BoosterType.Rewind:
                Data.PlayerData.CurrentRewind++;
                break;
            case BoosterType.TrashAway:
                Data.PlayerData.CurrentTrashAway++;
                break;
        }

        Data.SaveData();
    }

    private static void PlayClickSound()
    {
        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);
    }
}
