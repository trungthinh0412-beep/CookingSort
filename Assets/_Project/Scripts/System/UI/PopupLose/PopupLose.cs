using System;
using UnityEngine;

public class PopupLose : Popup
{
    public void OnClickBack()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        Data.PlayerData.CurrentHeart--;
        AdsController.Instance.ShowInterstitial(() =>
        {
            GameManager.Instance.ReturnHome();

        }, placement: "PopupLose_OnClickBack");
    }
    public void OnClickResume()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        Hide();
        PopupController.Instance.Show<PopupContinue>();
    }
}
