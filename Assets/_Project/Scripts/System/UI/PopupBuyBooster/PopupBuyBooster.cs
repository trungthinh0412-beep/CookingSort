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

        BoosterData data = boosterConfig.GetBoosterData(_boosterType);

        if (data == null)
        {
            Debug.LogWarning(
                $"[PopupBuyBooster] Missing config for {_boosterType}."
            );
            return;
        }
        
        boosterIcon.sprite = data.sprite;
        if (_boosterType == BoosterType.Shuffle)
        {
            description.text = "Add 1 Shuffle and continue!";
        }
        else if (_boosterType == BoosterType.Bomb)
        {
            description.text = "Add 1 Bomb and continue!";
        }
        else if (_boosterType == BoosterType.MoreDeal)
        {
            description.text = "Get 5 extra moves!";
        }
        else if (_boosterType == BoosterType.MagicMove)
        {
            description.text = "Move matching cards onto a different tray!";
        }
        else if (_boosterType == BoosterType.Magnet)
        {
            description.text = "Collect matching cards into one tray!";
        }
        else if (_boosterType == BoosterType.Lighter)
        {
            description.text = "Burn every card in one tray!";
        }
        else if (_boosterType == BoosterType.ExtraTray)
        {
            description.text = "Unlock an Extra Tray!";
        }
        else if (_boosterType == BoosterType.FreeMoves)
        {
            description.text = "Get 5 extra moves!";
        }
        priceText.text = $"{data.price} <sprite name=\"gold\">";
    }

    private void SetupPreLevelCard(CardType cardType)
    {
        PreLevelCardData data =
            GetPreLevelCardConfig()?.GetPreLevelCardData(cardType);
        if (data == null)
        {
            Debug.LogWarning(
                $"[PopupBuyBooster] Missing pre-level config for {cardType}."
            );
            return;
        }

        boosterIcon.sprite = GetPreLevelCardConfig()?.GetPreLevelIcon(cardType);
        description.text = cardType == CardType.WildCard
            ? "Start the level with a Wild Card!"
            : $"Start the level with {cardType}!";
        priceText.text = $"{data.preLevelPrice} <sprite name=\"gold\">";
    }

    public void OnClickBack()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        Hide(PopupAnimation.ScaleFade);
        // continue time
        Time.timeScale = 1;
    }

    public void OnClickGetBoosterBuyAds()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        AdsController.Instance.ShowRewardAds(() =>
        {
            GiveBooster();
            UseBooster();
        }, placement: "PopupBuyBooster_OnClickGetBoosterBuyAds");
    }

    public void OnClickBuyBooster()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);

        int price = GetCurrentPrice();
        if (price < 0)
        {
            Observer.Notify?.Invoke(
                "Booster is not configured.",
                Vector3.zero
            );
            return;
        }

        if (Data.PlayerData.CurrentGold < price)
        {
            Observer.Notify?.Invoke("Not enough gold!", Vector3.zero);
            PopupController.Instance.Show<PopupShopInGame>();
            return;
        }

        Data.PlayerData.CurrentGold -= price;
        GiveBooster();
        UseBooster();
    }

    private void GiveBooster()
    {
        if (_preLevelCardType.HasValue)
        {
            CardType cardType = _preLevelCardType.Value;
            Data.PlayerData.SetPreLevelCardAmount(
                cardType,
                Data.PlayerData.GetPreLevelCardAmount(cardType) + 1
            );
            Data.SaveData();
            Hide(PopupAnimation.ScaleFade);
            return;
        }

        switch (_boosterType)
        {
            case BoosterType.Shuffle:
                Data.PlayerData.CurrentShuffle++;
                // LevelController.Instance.currentLevel.Shuffle();
                break;
            case BoosterType.Bomb:
                Data.PlayerData.CurrentBomb++;
                // LevelController.Instance.currentLevel.Bomb();
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
        }

        Data.SaveData();
        Hide(PopupAnimation.ScaleFade);
    }

    private void UseBooster()
    {
        if (_preLevelCardType.HasValue)
            return;

        switch (_boosterType)
        {
            case BoosterType.Shuffle:
                Data.PlayerData.CurrentShuffle--;
                // LevelController.Instance.currentLevel.Shuffle();
                break;
            case BoosterType.Bomb:
                Data.PlayerData.CurrentBomb--;
                // LevelController.Instance.currentLevel.Bomb();
                break;
            case BoosterType.MoreDeal:
            case BoosterType.MagicMove:
            case BoosterType.Magnet:
            case BoosterType.Lighter:
            case BoosterType.ExtraTray:
            case BoosterType.FreeMoves:
                Level level = GameManager.Instance != null &&
                              GameManager.Instance.levelController != null
                    ? GameManager.Instance.levelController.currentLevel
                    : null;

                if (level != null)
                    level.ActivateBooster(_boosterType);
                break;
        }
    }

    private int GetCurrentPrice()
    {
        if (_preLevelCardType.HasValue)
        {
            PreLevelCardData data = GetPreLevelCardConfig()?.GetPreLevelCardData(
                _preLevelCardType.Value
            );
            return data != null
                ? data.preLevelPrice
                : -1;
        }

        BoosterData boosterData = boosterConfig.GetBoosterData(_boosterType);
        return boosterData != null ? boosterData.price : -1;
    }

    private CardConfig GetPreLevelCardConfig()
    {
        if (preLevelCardConfig != null)
            return preLevelCardConfig;

        Level level = GameManager.Instance != null &&
                      GameManager.Instance.levelController != null
            ? GameManager.Instance.levelController.currentLevel
            : null;
        return level != null ? level.CardConfig : null;
    }
}
