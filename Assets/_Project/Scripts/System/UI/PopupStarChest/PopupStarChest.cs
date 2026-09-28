using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupStarChest : Popup
{
    [SerializeField] private TextMeshProUGUI txtValueStar;
    [SerializeField] private Image progressFill;
    [SerializeField] private StarChestConfig starChestConfig;
    protected override void BeforeShow()
    {
        base.BeforeShow();
        Setup();
    }
    void Setup()
    {
        progressFill.fillAmount = (float)Data.PlayerData.CurrentStar / starChestConfig.targetStar;
        txtValueStar.text = $"{Data.PlayerData.CurrentStar}/{starChestConfig.targetStar}";

    }
    public void Close()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        Hide();
    }
}

