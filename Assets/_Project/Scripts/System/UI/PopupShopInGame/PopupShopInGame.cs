using System;
using System.Collections.Generic;
using UnityEngine;

public class PopupShopInGame : Popup
{
    [SerializeField] private GameObject contentRemoveAds;
    
    void Awake()
    {
        CheckRemoveAds();
        
        Observer.PurchasePackComplete += CheckRemoveAds;
    }

    private void OnDestroy()
    {
        Observer.PurchasePackComplete -= CheckRemoveAds;
    }

    void CheckRemoveAds()
    {
        contentRemoveAds.SetActive(!Data.PlayerData.IsRemoveAds);
    }
    
    public void OnClickBack()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        Hide();
    }
    
    public void OnClickPurchaseRemoveAds()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        IAPController.Instance.PurchasePack(PackName.RemoveAds);
    }

    public void OnClickPurchaseSmallBundle()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        IAPController.Instance.PurchasePack(PackName.SmallBundle);
    }

    public void OnClickPurchaseBigBundle()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        IAPController.Instance.PurchasePack(PackName.BigBundle);
    }

    public void OnClickPurchaseGold1()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        IAPController.Instance.PurchasePack(PackName.Gold1);
    }

    public void OnClickPurchaseGold2()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        IAPController.Instance.PurchasePack(PackName.Gold2);
    }

    public void OnClickPurchaseGold3()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        IAPController.Instance.PurchasePack(PackName.Gold3);
    }

    public void OnClickPurchaseGold4()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        IAPController.Instance.PurchasePack(PackName.Gold4);
    }

    public void OnClickPurchaseGold5()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        IAPController.Instance.PurchasePack(PackName.Gold5);
    }

    public void OnClickPurchaseGold6()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        IAPController.Instance.PurchasePack(PackName.Gold6);
    }
}
