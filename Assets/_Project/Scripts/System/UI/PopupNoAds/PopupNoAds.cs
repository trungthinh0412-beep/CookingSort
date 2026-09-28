using TMPro;
using UnityEngine;

/// <summary>
/// Remove Ads purchase popup. Its layout is based on PopupMoreLife so it
/// keeps the same popup animation and visual language as the rest of Home.
/// </summary>
public sealed class PopupNoAds : Popup
{
    // Keep the same serialized field names as the reused PopupMoreLife
    // layout so all visual references survive the prefab conversion.
    [SerializeField] private TextMeshProUGUI txtTime;
    [SerializeField] private GameObject btnAds;
    [SerializeField] private CustomButton btnCoin;

    private bool purchaseRequested;

    protected override void OnEnable()
    {
        base.OnEnable();

        Observer.PurchasePackComplete -= OnPurchasePackComplete;
        Observer.PurchasePackComplete += OnPurchasePackComplete;
    }

    protected override void OnDisable()
    {
        Observer.PurchasePackComplete -= OnPurchasePackComplete;
        purchaseRequested = false;

        base.OnDisable();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        RefreshState();
    }

    private void RefreshState()
    {
        bool canPurchase =
            Data.PlayerData != null &&
            !Data.PlayerData.IsRemoveAds;

        if (btnAds != null)
            btnAds.SetActive(canPurchase);

        if (txtTime != null)
            txtTime.text = "Ad-free forever";

        // The source layout contains a second gold button. Remove Ads is an
        // IAP-only product, so do not leave that unrelated action visible.
        if (btnCoin != null)
            btnCoin.gameObject.SetActive(false);
    }

    public void Close()
    {
        PlayClickSound();
        AfterHiddenAction = RestoreHomeAfterClose;
        Hide(PopupAnimation.None);
    }

    public void OnClickPurchaseRemoveAds()
    {
        if (purchaseRequested ||
            Data.PlayerData == null ||
            Data.PlayerData.IsRemoveAds)
        {
            RefreshState();
            return;
        }

        PlayClickSound();

        if (IAPController.Instance == null)
            return;

        purchaseRequested = true;
        IAPController.Instance.PurchasePack(PackName.RemoveAds);
    }

    private void OnPurchasePackComplete()
    {
        purchaseRequested = false;
        RefreshState();

        if (Data.PlayerData == null ||
            !Data.PlayerData.IsRemoveAds)
        {
            return;
        }

        // The purchase callback is raised after the entitlement is granted.
        // Close this popup and restore Home only after that state is valid.
        AfterHiddenAction = RestoreHomeAfterClose;
        Hide(PopupAnimation.ScaleFade);
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

    private static void PlayClickSound()
    {
        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);
    }
}
