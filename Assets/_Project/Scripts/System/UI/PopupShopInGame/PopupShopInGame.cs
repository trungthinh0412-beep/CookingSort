using UnityEngine;

public class PopupShopInGame : PopupShop
{
    [Header("Shop Data")]
    [SerializeField] private IAPConfig shopData;

    public IAPConfig ShopData => shopData;

    protected override void OnEnable()
    {
        base.OnEnable();
        RefreshShopItems();
    }

    [ContextMenu("Refresh Shop Items")]
    public void RefreshShopItems()
    {
        ShopPackItem[] items =
            GetComponentsInChildren<ShopPackItem>(true);

        foreach (ShopPackItem item in items)
            item.Setup(shopData);
    }

    public void OnClickBack()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        Hide();
    }
}
