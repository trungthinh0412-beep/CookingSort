using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.Purchasing;

public class IAPController : SingletonDontDestroy<IAPController>
{
    [SerializeField] private IAPConfig iapConfig;

    private StoreController _store;

    private readonly Dictionary<string, PackName> _productIdToPack = new();
    private readonly Dictionary<PackName, ProductType> _packTypes = new();
    private readonly HashSet<PackName> _activePack = new();

    protected override void Awake()
    {
        base.Awake();
        BuildTypeMap();
        Initialize();
    }

    private void BuildTypeMap()
    {
        _packTypes[PackName.RemoveAds] = ProductType.NonConsumable;
        _packTypes[PackName.SmallBundle] = ProductType.Consumable;
        _packTypes[PackName.BigBundle] = ProductType.Consumable;
        _packTypes[PackName.Gold1] = ProductType.Consumable;
        _packTypes[PackName.Gold2] = ProductType.Consumable;
        _packTypes[PackName.Gold3] = ProductType.Consumable;
        _packTypes[PackName.Gold4] = ProductType.Consumable;
        _packTypes[PackName.Gold5] = ProductType.Consumable;
        _packTypes[PackName.Gold6] = ProductType.Consumable;
    }

    // =========================
    // Initialization (v5 style)
    // =========================
    public async void Initialize()
    {
        try
        {
            // 1) Initialize UGS (unchanged)
            var options = new InitializationOptions().SetEnvironmentName("production");
            await UnityServices.InitializeAsync(options);

            // 2) Get the StoreController instance & wire up events BEFORE calling Connect/Fetch
            _store = UnityIAPServices.StoreController();

            // Product/purchase lifecycle
            _store.OnProductsFetched += OnProductsFetched;
            _store.OnProductsFetchFailed += OnProductsFetchFailed;
            _store.OnPurchasesFetched += OnPurchasesFetched;
            _store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;

            _store.OnPurchasePending += OnPurchasePending; // replacement for ProcessPurchase
            _store.OnPurchaseFailed += OnPurchaseFailed; // replacement for OnPurchaseFailed
            _store.OnPurchaseDeferred += OnPurchaseDeferred;
            _store.OnPurchaseConfirmed += OnPurchaseConfirmed; // optional but useful for logging/analytics
            _store.OnStoreDisconnected += OnStoreDisconnected; // replacement for OnInitializeFailed

            // 3) Connect to the store
            await _store.Connect();

            // 4) Fetch products (from your ScriptableObject)
            var defs = BuildProductDefinitions();
            _store.FetchProducts(defs);
        }
        catch (Exception e)
        {
            Debug.LogError($"[IAP] Initialize exception: {e}. IMPORTANT: Please ensure your project is linked to a Unity Services Cloud Project ID in 'Project Settings > Services' (UGS is required for Unity IAP v5+).");
        }
    }

    private List<ProductDefinition> BuildProductDefinitions()
    {
        _productIdToPack.Clear();
        var defs = new List<ProductDefinition>(iapConfig.packData?.Count ?? 0);
        if (iapConfig?.packData == null) return defs;

        foreach (var d in iapConfig.packData)
        {
            if (!_packTypes.TryGetValue(d.packName, out var type)) continue;
            if (string.IsNullOrEmpty(d.packId)) continue;

            defs.Add(new ProductDefinition(d.packId, type));
            _productIdToPack[d.packId] = d.packName;
        }

        return defs;
    }

    public void PurchasePack(PackName packName)
    {
        if (_store == null)
        {
            Debug.LogError("IAP is not initialized.");
            return;
        }

        var pid = iapConfig.GetPackData(packName)?.packId;
        if (string.IsNullOrEmpty(pid))
        {
            Debug.LogError($"Unknown product id for {packName}");
            return;
        }

        var product = _store.GetProducts().FirstOrDefault(p => p.definition.id == pid);
        if (product == null)
        {
            Debug.LogError($"Product not fetched yet: {pid}");
            return;
        }

        if (!product.availableToPurchase)
        {
            Debug.LogError("Product is not available for purchase.");
            return;
        }

        _store.PurchaseProduct(product);
    }

    public void RestorePurchases()
    {
        if (_store == null)
        {
            Debug.LogError("IAP is not initialized.");
            return;
        }

        _store.FetchPurchases();
    }

    private void OnProductsFetched(List<Product> products)
    {
        // Proceed to fetch previous purchases/entitlements
        _store.FetchPurchases();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        Debug.LogError($"IAP Products fetch failed: {failure}");
    }

    private void OnPurchasesFetched(Orders orders)
    {
        _activePack.Clear();

        if (orders != null)
        {
            foreach (var confirmed in orders.ConfirmedOrders)
            {
                var items = confirmed.CartOrdered.Items();
                foreach (var item in items)
                {
                    var pid = item.Product?.definition?.id;
                    if (string.IsNullOrEmpty(pid)) continue;

                    if (_productIdToPack.TryGetValue(pid, out var pack))
                    {
                        if (_packTypes.TryGetValue(pack, out var type) && (type == ProductType.Subscription || type == ProductType.NonConsumable))
                        {
                            _activePack.Add(pack);
                            Debug.Log($"<color=cyan>[IAP] Active subscription: {pack}</color>");
                        }
                    }
                }
            }
        }
    }

    public bool IsActive(PackName packName)
    {
        return _activePack.Contains(packName);
    }

    private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
    {
        Debug.LogError($"IAP Purchases fetch failed: {failure}");
    }

    private void OnPurchasePending(PendingOrder order)
    {
        try
        {
            foreach (var item in order.CartOrdered.Items())
            {
                var product = item.Product;
                var pid = product?.definition?.id;
                if (string.IsNullOrEmpty(pid))
                {
                    Debug.LogWarning("Pending order with missing product id");
                    continue;
                }

                if (!_productIdToPack.TryGetValue(pid, out var packName))
                {
                    var found = iapConfig.packData.FirstOrDefault(p => p.packId == pid);
                    if (found != null) packName = found.packName;
                    else
                    {
                        Debug.LogWarning($"No PackName found for pid={pid}");
                        continue;
                    }
                }

                HandlePurchase(packName, product);
            }

            // Always confirm after you have granted rewards & saved state
            _store.ConfirmPurchase(order);

            // Update UI
            Observer.PurchasePackComplete?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            // DO NOT confirm here if you failed to grant rewards; allow retry on next init
        }
    }

    private void OnPurchaseFailed(FailedOrder failed)
    {
        // FirebaseController.Instance.TrackingPurchaseFailIap(failed.ToString());
        Debug.LogError($"Purchase failed: {failed}");
    }

    private void OnPurchaseDeferred(DeferredOrder deferred)
    {
        Debug.LogWarning($"Purchase deferred: {deferred}");
    }

    private void OnPurchaseConfirmed(Order order)
    {
        Debug.Log($"Purchase confirmed: {order}");
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription desc)
    {
        Debug.LogError($"IAP Store disconnected: {desc}");
    }

    // =========================
    // Reward logic (unchanged)
    // =========================
    private void HandlePurchase(PackName packName, Product purchasedProduct)
    {
        switch (packName)
        {
            case PackName.RemoveAds:
                HandleRemoveAdsPurchase(PackName.RemoveAds);
                break;
            case PackName.SmallBundle:
                HandleSmallBundlePurchase(PackName.SmallBundle);
                break;
            case PackName.BigBundle:
                HandleBigBundlePurchase(PackName.BigBundle);
                break;
            case PackName.Gold1:
                HandleGold1Purchase(PackName.Gold1);
                break;
            case PackName.Gold2:
                HandleGold2Purchase(PackName.Gold2);
                break;
            case PackName.Gold3:
                HandleGold3Purchase(PackName.Gold3);
                break;
            case PackName.Gold4:
                HandleGold4Purchase(PackName.Gold4);
                break;
            case PackName.Gold5:
                HandleGold5Purchase(PackName.Gold5);
                break;
            case PackName.Gold6:
                HandleGold6Purchase(PackName.Gold6);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(packName), packName, null);
        }
    }

    private void HandleRemoveAdsPurchase(PackName packName)
    {
        //FirebaseController.Instance.TrackingIapRevenue("HandleRemoveAdsPurchase", $"{packName}");
        ExtendRemoveAds(0, 100);
    }

    private void HandleSmallBundlePurchase(PackName packName)
    {
        //FirebaseController.Instance.TrackingIapRevenue("HandleSmallBundlePurchase", $"{packName}");
        Data.PlayerData.CurrentGold += 1500;
        Data.PlayerData.CurrentShuffle += 1;
        Data.PlayerData.CurrentBomb += 1;
        Data.PlayerData.AddInfiniteHeartTime(1);

    }

    private void HandleBigBundlePurchase(PackName packName)
    {
        //FirebaseController.Instance.TrackingIapRevenue("HandleBigBundlePurchase", $"{packName}");
        Data.PlayerData.CurrentGold += 3000;
        Data.PlayerData.CurrentShuffle += 4;
        Data.PlayerData.CurrentBomb += 4;
        Data.PlayerData.AddInfiniteHeartTime(3);
    }

    private void HandleGold1Purchase(PackName packName)
    {
        //FirebaseController.Instance.TrackingIapRevenue("HandleGold1Purchase", $"{packName}");
        Data.PlayerData.CurrentGold += 300;
    }

    private void HandleGold2Purchase(PackName packName)
    {
        //FirebaseController.Instance.TrackingIapRevenue("HandleGold2Purchase", $"{packName}");
        Data.PlayerData.CurrentGold += 900;
    }

    private void HandleGold3Purchase(PackName packName)
    {
        //FirebaseController.Instance.TrackingIapRevenue("HandleGold3Purchase", $"{packName}");
        Data.PlayerData.CurrentGold += 1900;
    }

    private void HandleGold4Purchase(PackName packName)
    {
        //FirebaseController.Instance.TrackingIapRevenue("HandleGold4Purchase", $"{packName}");
        Data.PlayerData.CurrentGold += 6000;
    }

    private void HandleGold5Purchase(PackName packName)
    {
        //FirebaseController.Instance.TrackingIapRevenue("HandleGold5Purchase", $"{packName}");
        Data.PlayerData.CurrentGold += 15000;
    }

    private void HandleGold6Purchase(PackName packName)
    {
        //FirebaseController.Instance.TrackingIapRevenue("HandleGold6Purchase", $"{packName}");
        Data.PlayerData.CurrentGold += 36000;
    }

    private static void ExtendRemoveAds(int months = 0, int years = 0)
    {
        System.DateTime current;
        if (!string.IsNullOrEmpty(Data.PlayerData.RemoveAdsExpiryDate) &&
            System.DateTime.TryParseExact(Data.PlayerData.RemoveAdsExpiryDate, Utility.DateTimeFormat,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var existing) && existing > System.DateTime.UtcNow)
        {
            current = existing;
        }
        else
        {
            current = System.DateTime.UtcNow;
        }

        current = months > 0 ? current.AddMonths(months) : current.AddYears(years);
        Data.PlayerData.RemoveAdsExpiryDate = current.ToString(Utility.DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
    }
}