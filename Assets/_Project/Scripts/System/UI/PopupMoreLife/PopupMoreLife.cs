using TMPro;
using UnityEngine;

public class PopupMoreLife : Popup
{
    [SerializeField] private int goldFullHeart = 900;
    [SerializeField] private TextMeshProUGUI txtTime;
    [SerializeField] private GameObject btnAds;
    [SerializeField] private CustomButton btnCoin;
    protected override void BeforeShow()
    {
        base.BeforeShow();
        Setup();
    }
    void Setup()
    {
        btnAds.SetActive(Data.PlayerData.CurrentHeart < HeartController.Instance.MaxHeart);
        btnCoin.gameObject.SetActive(Data.PlayerData.CurrentHeart < HeartController.Instance.MaxHeart);
    }
    void Update()
    {
        txtTime.text = HeartController.Instance.GetRemainingTime();
    }
    public void Close()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        AfterHiddenAction = RestoreHomeAfterClose;
        Hide(PopupAnimation.None);
    }

    private void RestoreHomeAfterClose()
    {
        if (PopupController.Instance != null)
        {
            PopupController.Instance.Show<PopupHome>(
                PopupAnimation.None
            );
        }
    }

    public void OnClickBuyHeartByCoin()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        if (Data.PlayerData.CurrentGold < goldFullHeart)
        {
            Observer.Notify?.Invoke("Not enough gold!", Vector3.zero);
            PopupController.Instance.Show<PopupShopInGame>();
            return;
        }
        Data.PlayerData.CurrentGold -= goldFullHeart;
        Data.PlayerData.CurrentHeart = 5;
        Setup();
    }
    public void OnClickBuyHeartByAds()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        AdsController.Instance.ShowRewardAds(() =>
        {
            Data.PlayerData.CurrentHeart++;
            Setup();
        }, placement: "PopupMoreLife_OnClickBuyHeartByAds");
    }
}

