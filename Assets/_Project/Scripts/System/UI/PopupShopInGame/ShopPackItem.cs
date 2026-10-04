using TMPro;
using UnityEngine;

public class ShopPackItem : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("Optional. Leave empty to use Shop Data from PopupShopInGame.")]
    [SerializeField] private IAPConfig shopDataOverride;
    [SerializeField] private PackName packName;

    [Header("UI References")]
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private CustomButton purchaseButton;

    private IAPConfig _ownerShopData;
    private bool _purchasePending;

    public PackName PackName => packName;
    public PackData Data => ResolveData();

    private void OnEnable()
    {
        Observer.IAPProductsUpdated -= Refresh;
        Observer.IAPProductsUpdated += Refresh;
        Observer.PurchasePackGoldGranted -= OnPackGoldGranted;
        Observer.PurchasePackGoldGranted += OnPackGoldGranted;

        if (purchaseButton != null)
        {
            purchaseButton.Click.RemoveListener(OnClickPurchase);
            purchaseButton.Click.AddListener(OnClickPurchase);
        }

        Refresh();
    }

    private void OnDisable()
    {
        Observer.IAPProductsUpdated -= Refresh;
        Observer.PurchasePackGoldGranted -= OnPackGoldGranted;

        if (purchaseButton != null)
            purchaseButton.Click.RemoveListener(OnClickPurchase);

        _purchasePending = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Refresh();
    }
#endif

    public void Setup(IAPConfig ownerShopData)
    {
        _ownerShopData = ownerShopData;
        Refresh();
    }

    [ContextMenu("Refresh Preview")]
    public void Refresh()
    {
        PackData data = ResolveData();
        if (data == null)
            return;

        RefreshPrice(data);
    }

    public void OnClickPurchase()
    {
        if (ResolveData() == null)
        {
            Debug.LogError(
                $"[ShopPackItem] Missing data for {packName} on {name}.",
                this);
            return;
        }

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        if (IAPController.Instance == null)
        {
            Debug.LogError("[ShopPackItem] IAPController is missing.", this);
            return;
        }

        _purchasePending = true;
        IAPController.Instance.PurchasePack(packName);
    }

    private void OnPackGoldGranted(PackName grantedPack, int goldAmount)
    {
        if (!_purchasePending || grantedPack != packName)
            return;

        _purchasePending = false;
        RectTransform source = purchaseButton != null
            ? purchaseButton.transform as RectTransform
            : transform as RectTransform;
        GoldHandler.PlayCommittedRewardFromUI(source, goldAmount);
    }

    private void RefreshPrice(PackData data)
    {
        if (priceText == null)
            return;

        // A configured data price is authoritative. The Store price is used
        // only when this pack intentionally leaves Price at zero.
        if (data.price > 0f)
        {
            priceText.text = FormatConfiguredPrice(
                data.price,
                data.currencyCode);
            return;
        }

        string localizedPrice = null;
        if (Application.isPlaying && IAPController.Instance != null)
            localizedPrice = IAPController.Instance.GetLocalizedPrice(packName);

        if (!string.IsNullOrEmpty(localizedPrice))
        {
            priceText.text = localizedPrice;
            return;
        }

    }

    private static string FormatConfiguredPrice(float price, string currencyCode)
    {
        string currency = string.IsNullOrWhiteSpace(currencyCode)
            ? "USD"
            : currencyCode.Trim().ToUpperInvariant();

        return currency switch
        {
            "USD" => $"${price:0.##}",
            "EUR" => $"€{price:0.##}",
            "GBP" => $"£{price:0.##}",
            "JPY" => $"¥{price:0}",
            "VND" => $"{price:0}₫",
            _ => $"{price:0.##} {currency}"
        };
    }

    private PackData ResolveData()
    {
        IAPConfig config = shopDataOverride != null
            ? shopDataOverride
            : _ownerShopData;
        return config != null ? config.GetPackData(packName) : null;
    }
}
