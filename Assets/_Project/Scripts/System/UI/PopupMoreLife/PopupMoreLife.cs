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
        if (Data.PlayerData == null || HeartController.Instance == null)
            return;

        bool canReceiveHeart =
            !Data.PlayerData.IsInfiniteHeart() &&
            Data.PlayerData.CurrentHeart < HeartController.Instance.MaxHeart;

        if (btnAds != null && btnAds.activeSelf != canReceiveHeart)
            btnAds.SetActive(canReceiveHeart);

        if (btnCoin != null && btnCoin.gameObject.activeSelf != canReceiveHeart)
            btnCoin.gameObject.SetActive(canReceiveHeart);
    }
    void Update()
    {
        if (txtTime != null && HeartController.Instance != null)
            txtTime.text = HeartController.Instance.GetRemainingTime();

        Setup();
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
        if (Data.PlayerData == null || HeartController.Instance == null)
            return;

        if (Data.PlayerData.IsInfiniteHeart() ||
            Data.PlayerData.CurrentHeart >= HeartController.Instance.MaxHeart)
        {
            Setup();
            return;
        }

        if (Data.PlayerData.CurrentGold < goldFullHeart)
        {
            Observer.Notify?.Invoke("Not enough gold!", Vector3.zero);
            Observer.Notify?.Invoke("Not enough gold!", Vector3.zero);
            return;
        }
        Data.PlayerData.CurrentGold -= goldFullHeart;
        if (HeartController.Instance.RefillHearts())
        {
            HideAfterReceivingHeart();
            return;
        }

        // Keep the transaction atomic if the heart state changed before the
        // purchase could be completed.
        Data.PlayerData.CurrentGold += goldFullHeart;
        Setup();
    }
    public void OnClickBuyHeartByAds()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        if (Data.PlayerData == null || HeartController.Instance == null)
            return;

        if (Data.PlayerData.IsInfiniteHeart() ||
            Data.PlayerData.CurrentHeart >= HeartController.Instance.MaxHeart)
        {
            Setup();
            return;
        }

        AdsController.Instance.ShowRewardAds(() =>
        {
            bool receivedHeart =
                HeartController.Instance != null &&
                HeartController.Instance.AddHeart();

            if (receivedHeart)
            {
                HideAfterReceivingHeart();
                return;
            }

            Setup();
        }, placement: "PopupMoreLife_OnClickBuyHeartByAds");
    }

    private void HideAfterReceivingHeart()
    {
        if (!isActiveAndEnabled)
            return;

        AfterHiddenAction = RestoreHomeAfterClose;
        Hide(PopupAnimation.None);
    }
}
