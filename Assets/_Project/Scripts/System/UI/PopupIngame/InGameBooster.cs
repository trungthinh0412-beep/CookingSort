using TMPro;
using UnityEngine;

public class InGameBoosterItem : MonoBehaviour
{
    [SerializeField] private BoosterType boosterType;
    [SerializeField] private TextMeshProUGUI boosterAmountText;

    public BoosterType BoosterType => boosterType;

    private void OnEnable()
    {
        Observer.UseBooster += OnBoosterAmountChanged;
        Setup();
    }

    private void OnDisable()
    {
        Observer.UseBooster -= OnBoosterAmountChanged;
    }

    private void OnBoosterAmountChanged(BoosterType changedType)
    {
        if (changedType == boosterType)
            Setup();
    }

    private void Setup()
    {
        if (boosterAmountText == null)
            return;

        int amount = boosterType switch
        {
            BoosterType.Shuffle => Data.PlayerData.CurrentShuffle,
            BoosterType.Bomb => Data.PlayerData.CurrentBomb,
            BoosterType.MoreDeal => Data.PlayerData.CurrentMoreDeal,
            BoosterType.MagicSwap => Data.PlayerData.CurrentMagicSwap,
            BoosterType.Magnet => Data.PlayerData.CurrentMagnet,
            _ => 0
        };

        boosterAmountText.text = amount > 0
            ? amount.ToString()
            : "+";
    }

    public void OnClickBooster()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);

        int amount = boosterType switch
        {
            BoosterType.Shuffle => Data.PlayerData.CurrentShuffle,
            BoosterType.Bomb => Data.PlayerData.CurrentBomb,
            BoosterType.MoreDeal => Data.PlayerData.CurrentMoreDeal,
            BoosterType.MagicSwap => Data.PlayerData.CurrentMagicSwap,
            BoosterType.Magnet => Data.PlayerData.CurrentMagnet,
            _ => 0
        };

        if (amount <= 0)
        {
            PopupBuyBooster popup =
                PopupController.Instance.Get<PopupBuyBooster>()
                as PopupBuyBooster;

            if (popup != null)
            {
                popup.Init(boosterType);
                PopupController.Instance.Show<PopupBuyBooster>(
                    PopupAnimation.ScaleFade
                );
            }

            return;
        }

        Level level = GameManager.Instance != null &&
                      GameManager.Instance.levelController != null
            ? GameManager.Instance.levelController.currentLevel
            : null;

        if (level != null)
            level.ActivateBooster(boosterType);

    }
}
