using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupBuyBooster : Popup
{
    [SerializeField] private Image boosterIcon;
    [SerializeField] private TextMeshProUGUI description;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private BoosterConfig boosterConfig;

    private BoosterType _boosterType;

    public void Init(BoosterType boosterType)
    {
        _boosterType = boosterType;
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        Setup();
    }

    private void Setup()
    {
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
            description.text = "Get 1 free Deal!";
        }
        else if (_boosterType == BoosterType.MagicSwap)
        {
            description.text = "Move a card stack to another tray!";
        }
        else if (_boosterType == BoosterType.Magnet)
        {
            description.text = "Collect matching cards into one tray!";
        }
        priceText.text = $"{data.price} <sprite name=\"gold\">";
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

        BoosterData data = boosterConfig.GetBoosterData(_boosterType);

        if (data == null)
        {
            Observer.Notify?.Invoke(
                "Booster is not configured.",
                Vector3.zero
            );
            return;
        }

        if (Data.PlayerData.CurrentGold < data.price)
        {
            Observer.Notify?.Invoke("Not enough gold!", Vector3.zero);
            PopupController.Instance.Show<PopupShopInGame>();
            return;
        }

        Data.PlayerData.CurrentGold -= data.price;
        GiveBooster();
        UseBooster();
    }

    private void GiveBooster()
    {
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
            case BoosterType.MagicSwap:
                Data.PlayerData.CurrentMagicSwap++;
                break;
            case BoosterType.Magnet:
                Data.PlayerData.CurrentMagnet++;
                break;
        }

        Data.SaveData();
        Hide(PopupAnimation.ScaleFade);
    }

    private void UseBooster()
    {
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
            case BoosterType.MagicSwap:
            case BoosterType.Magnet:
                Level level = GameManager.Instance != null &&
                              GameManager.Instance.levelController != null
                    ? GameManager.Instance.levelController.currentLevel
                    : null;

                if (level != null)
                    level.ActivateBooster(_boosterType);
                break;
        }
    }
}
