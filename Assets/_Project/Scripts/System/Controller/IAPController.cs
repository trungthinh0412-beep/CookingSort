using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.Purchasing;

public class IAPController : SingletonDontDestroy<IAPController>
{
    private const int PermanentRemoveAdsYears = 100;

    [SerializeField] private IAPConfig iapConfig;

    private StoreController _store;
    private PackName? _queuedPurchase;
    private bool _productsReady;
    private bool _purchaseInProgress;

    private readonly Dictionary<string, PackName> _productIdToPack = new();
    private readonly HashSet<PackName> _activePack = new();

    protected override void Awake()
    {
        base.Awake();
        Initialize();
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
            if (string.IsNullOrEmpty(d.packId)) continue;

            defs.Add(new ProductDefinition(d.packId, d.productType));
            _productIdToPack[d.packId] = d.packName;
        }

        return defs;
    }

    public void PurchasePack(PackName packName)
    {
        PackData packData = iapConfig?.GetPackData(packName);
        if (packData == null || string.IsNullOrEmpty(packData.packId))
        {
            Debug.LogError($"Unknown product id for {packName}");
            return;
        }

#if UNITY_EDITOR
        CompleteEditorPurchase(packName);
        return;
#endif

        if (_store == null || !_productsReady)
        {
            _queuedPurchase ??= packName;
            Debug.Log($"[IAP] Queued {packName} until products are ready.");
            return;
        }

        StartPurchase(packName, packData.packId);
    }

#if UNITY_EDITOR
    private void CompleteEditorPurchase(PackName packName)
    {
        if (_purchaseInProgress)
            return;

        _purchaseInProgress = true;
        try
        {
            int goldAmount = HandlePurchase(packName);
            Data.SaveData();
            AdsController.Instance?.HideBanner();

            if (goldAmount > 0)
                Observer.PurchasePackGoldGranted?.Invoke(packName, goldAmount);

            Observer.PurchasePackComplete?.Invoke();
            Debug.Log($"[IAP] Editor purchase completed immediately: {packName}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            _purchaseInProgress = false;
        }
    }
#endif

    private void StartPurchase(PackName packName, string productId)
    {
        if (_purchaseInProgress)
        {
            Debug.LogWarning($"[IAP] A purchase is already in progress. Ignored {packName}.");
            return;
        }

        var products = _store?.GetProducts();
        var product = products?.FirstOrDefault(
            item => item.definition.id == productId);
        if (product == null)
        {
            _queuedPurchase ??= packName;
            Debug.Log($"[IAP] Product not fetched yet: {productId}. Purchase queued.");
            return;
        }

        if (!product.availableToPurchase)
        {
            Debug.LogError("Product is not available for purchase.");
            return;
        }

        _purchaseInProgress = true;
        _store.PurchaseProduct(product);
    }

    private void StartQueuedPurchaseIfReady()
    {
        if (!_productsReady || !_queuedPurchase.HasValue)
            return;

        PackName packName = _queuedPurchase.Value;
        _queuedPurchase = null;
        string productId = iapConfig?.GetPackData(packName)?.packId;
        if (string.IsNullOrEmpty(productId))
        {
            Debug.LogError($"Unknown product id for queued pack {packName}");
            return;
        }

        StartPurchase(packName, productId);
    }

    public string GetLocalizedPrice(PackName packName)
    {
        if (_store == null)
            return null;

        string productId = iapConfig?.GetPackData(packName)?.packId;
        if (string.IsNullOrEmpty(productId))
            return null;

        var products = _store.GetProducts();
        if (products == null)
            return null;

        Product product = products.FirstOrDefault(
            item => item.definition.id == productId);
        return product?.metadata?.localizedPriceString;
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
        _productsReady = true;
        Observer.IAPProductsUpdated?.Invoke();

        // Proceed to fetch previous purchases/entitlements
        _store.FetchPurchases();
        StartQueuedPurchaseIfReady();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        _productsReady = false;
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
                        var packData = iapConfig.GetPackData(pack);
                        if (packData != null &&
                            (packData.productType == ProductType.Subscription ||
                             packData.productType == ProductType.NonConsumable))
                        {
                            _activePack.Add(pack);
                            Debug.Log($"<color=cyan>[IAP] Active product: {pack}</color>");
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
            List<(PackName packName, int goldAmount)> grantedGoldPacks =
                new List<(PackName, int)>();

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

                int goldAmount = HandlePurchase(packName);
                if (goldAmount > 0)
                    grantedGoldPacks.Add((packName, goldAmount));
            }

            // Always confirm after you have granted rewards & saved state
            Data.SaveData();
            AdsController.Instance?.HideBanner();
            _store.ConfirmPurchase(order);

            foreach (var grantedPack in grantedGoldPacks)
            {
                Observer.PurchasePackGoldGranted?.Invoke(
                    grantedPack.packName,
                    grantedPack.goldAmount);
            }

            // Update UI
            Observer.PurchasePackComplete?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            // DO NOT confirm here if you failed to grant rewards; allow retry on next init
        }
        finally
        {
            _purchaseInProgress = false;
        }
    }

    private void OnPurchaseFailed(FailedOrder failed)
    {
        _purchaseInProgress = false;
        // FirebaseController.Instance.TrackingPurchaseFailIap(failed.ToString());
        Debug.LogError($"Purchase failed: {failed}");
    }

    private void OnPurchaseDeferred(DeferredOrder deferred)
    {
        _purchaseInProgress = false;
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
    // Data-driven reward logic
    // =========================
    private int HandlePurchase(PackName packName)
    {
        PackData pack = iapConfig?.GetPackData(packName);
        if (pack == null)
        {
            throw new InvalidOperationException($"Missing IAP config for {packName}.");
        }

        int grantedGold = 0;
        if (pack.rewards != null)
        {
            foreach (ShopRewardData reward in pack.rewards)
            {
                ApplyReward(reward);
                if (reward != null && reward.type == ShopRewardType.Gold)
                    grantedGold += Mathf.Max(0, reward.amount);
            }
        }

        // Every successful IAP pack removes ads permanently.
        EnsureRemoveAdsForYears(PermanentRemoveAdsYears);
        return grantedGold;
    }

    private static void ApplyReward(ShopRewardData reward)
    {
        if (reward == null || reward.amount <= 0)
            return;

        switch (reward.type)
        {
            case ShopRewardType.Gold:
                
                break;
            case ShopRewardType.Shuffle:
                Data.PlayerData.CurrentShuffle += reward.amount;
                break;
            case ShopRewardType.Bomb:
                Data.PlayerData.CurrentBomb += reward.amount;
                break;
            case ShopRewardType.MoreDeal:
                Data.PlayerData.CurrentMoreDeal += reward.amount;
                break;
            case ShopRewardType.MagicSwap:
                Data.PlayerData.CurrentMagicSwap += reward.amount;
                break;
            case ShopRewardType.Magnet:
                Data.PlayerData.CurrentMagnet += reward.amount;
                break;
            case ShopRewardType.InfiniteHeartHours:
                Data.PlayerData.AddInfiniteHeartTime(reward.amount);
                break;
            case ShopRewardType.RemoveAdsYears:
                ExtendRemoveAds(years: reward.amount);
                break;
            case ShopRewardType.StackCardHours:
                Data.PlayerData.AddTimedPreLevelCardTime(
                    CardType.StackCard,
                    reward.amount);
                break;
            case ShopRewardType.UpgradeCardHours:
                Data.PlayerData.AddTimedPreLevelCardTime(
                    CardType.UpgradeCard,
                    reward.amount);
                break;
            case ShopRewardType.KingCardHours:
                Data.PlayerData.AddTimedPreLevelCardTime(
                    CardType.KingCard,
                    reward.amount);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
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

    private static void EnsureRemoveAdsForYears(int years)
    {
        System.DateTime minimumExpiry = System.DateTime.UtcNow.AddYears(years);
        if (!string.IsNullOrEmpty(Data.PlayerData.RemoveAdsExpiryDate) &&
            System.DateTime.TryParseExact(
                Data.PlayerData.RemoveAdsExpiryDate,
                Utility.DateTimeFormat,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal |
                System.Globalization.DateTimeStyles.AdjustToUniversal,
                out System.DateTime existing) &&
            existing >= minimumExpiry)
        {
            return;
        }

        Data.PlayerData.RemoveAdsExpiryDate = minimumExpiry.ToString(
            Utility.DateTimeFormat,
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
