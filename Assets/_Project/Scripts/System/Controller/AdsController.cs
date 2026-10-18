using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

public class AdsController : SingletonDontDestroy<AdsController>
{
    [SerializeField] private AdsConfig adsConfig;

    private InterstitialAd _interstitialAd;
    private RewardedAd _rewardAd;
    private BannerView _bannerView;
    private float _timePlay = 999;
    private Action _onInterstitialDisplay;
    private Action _onInterstitialComplete;
    private Action _onAdsRewardDisplay;
    private Action _onAdsRewardComplete;
    private Action _onAdsRewardFailed;
    private bool _rewardGrantedThisShow;
    private bool _completeRewardAfterClose;
    public string AdsPlacement { get; set; }

    private void Start()
    {
        MobileAdsEventExecutor.Initialize();
        MobileAds.Initialize(initStatus =>
        {
            RunOnMainThread(() =>
            {
                LoadBannerAds();
                LoadInterstitialAds();
                LoadRewardAds();

                Debug.Log("Init Ads Succeed");
            });
        });
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.gameState == GameState.PlayingGame)
        {
            _timePlay += Time.deltaTime;
        }
    }

    private static void RunOnMainThread(Action action)
    {
        MobileAdsEventExecutor.ExecuteInUpdate(action);
    }

    private bool IsEnableToShowInter()
    {
        return !Data.PlayerData.IsRemoveAds
        && _interstitialAd != null
        && _timePlay >= adsConfig.timeBetweenTwoInterstitialAds
        && Data.PlayerData.CountShowInterAds >= 2
        && Data.PlayerData.CurrentLevelIndex >= 5;
    }

    private void LoadInterstitialAds()
    {
        var request = new AdRequest();

        InterstitialAd.Load(adsConfig.GetInterstitialId(), request, (InterstitialAd ad, LoadAdError error) =>
        {
            RunOnMainThread(() =>
            {
                if (error != null)
                {
                    Debug.LogError($"[Ads] Interstitial load fail: {error}");
                    return;
                }

                _interstitialAd = ad;
                RegisterInterstitialCallbacks(ad);
                Debug.Log("[Ads] Interstitial loaded.");
            });
        });
    }

    private void RegisterInterstitialCallbacks(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
        {
            RunOnMainThread(() =>
            {
                Debug.Log("[Ads] Interstitial opened.");
                _onInterstitialDisplay?.Invoke();
            });
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            RunOnMainThread(() =>
            {
                Debug.Log("[Ads] Interstitial closed. Reloading...");
                ad.Destroy();
                _interstitialAd = null;
                LoadInterstitialAds();
                _onInterstitialComplete?.Invoke();
                _timePlay = 0;
            });
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            RunOnMainThread(() =>
            {
                Debug.LogError($"[Ads] Interstitial show failed: {error}. Reloading...");
                ad.Destroy();
                _interstitialAd = null;
                LoadInterstitialAds();
                _onInterstitialComplete?.Invoke();
            });
        };

        ad.OnAdPaid += adValue =>
        {
            RunOnMainThread(() =>
            {
                string adNetwork = ad.GetResponseInfo().GetMediationAdapterClassName();
                //FirebaseController.Instance.TrackingAdsRevenue("unknown", AdsPlacement, (adValue.Value / 1000000f).ToString(), "interstitial_ads", adNetwork);
                Debug.Log($"[Ads] Interstitial paid: {adValue.Value} {adValue.CurrencyCode}");
            });
        };
        ad.OnAdImpressionRecorded += () =>
            RunOnMainThread(() => Debug.Log("[Ads] Interstitial impression."));
    }


    public void ShowInterstitial(Action completeCallback, Action displayCallback = null, string placement = "unknown")
    {
        if (GameController.IsTesting)
        {
            completeCallback?.Invoke();
            if (IsEnableToShowInter())
            {
                AdsPlacement = placement;
                _onInterstitialDisplay = displayCallback;
                _onInterstitialComplete = completeCallback;
                _interstitialAd.Show();
                Data.PlayerData.CountShowInterAds = 0;
            }
            else
            {
                LoadInterstitialAds();
                completeCallback?.Invoke();
            }
        }
        else
        {
            if (IsEnableToShowInter())
            {
                Debug.Log("here show inter");
                AdsPlacement = placement;
                _onInterstitialDisplay = displayCallback;
                _onInterstitialComplete = completeCallback;
                _interstitialAd.Show();
                Data.PlayerData.CountShowInterAds = 0;
            }
            else
            {
                LoadInterstitialAds();
                completeCallback?.Invoke();
            }
        }
    }

    public void ShowInterstitialImmediate(
        Action completeCallback,
        Action displayCallback = null,
        string placement = "unknown")
    {
        if (GameController.IsTesting)
        {
            completeCallback?.Invoke();
            return;
        }

        bool canShow = !Data.PlayerData.IsRemoveAds &&
                       _interstitialAd != null;
        if (!canShow)
        {
            LoadInterstitialAds();
            completeCallback?.Invoke();
            return;
        }

        AdsPlacement = placement;
        _onInterstitialDisplay = displayCallback;
        _onInterstitialComplete = completeCallback;
        _interstitialAd.Show();
        Data.PlayerData.CountShowInterAds = 0;
    }

    private void LoadBannerAds()
    {
        if (Data.PlayerData.IsRemoveAds) return;

        _bannerView?.Destroy();
        _bannerView = new BannerView(adsConfig.GetBannerId(), AdSize.Banner, AdPosition.Bottom);

        _bannerView.OnBannerAdLoaded += () =>
            RunOnMainThread(() => Debug.Log("[Ads] Banner loaded."));
        _bannerView.OnBannerAdLoadFailed += error =>
            RunOnMainThread(() => Debug.LogError($"[Ads] Banner load fail: {error}"));
        _bannerView.OnAdPaid += adValue =>
        {
            RunOnMainThread(() =>
            {
                //FirebaseController.Instance.TrackingAdsRevenue("unknown", "unknown", (adValue.Value / 1000000f).ToString(), "interstitial_ads", "google_admob");
                Debug.Log($"[Ads] Banner paid: {adValue.Value} {adValue.CurrencyCode}");
            });
        };

        var request = new AdRequest();
        _bannerView.LoadAd(request);
        _bannerView.Hide();
    }

    public void ShowBanner()
    {
        if (Data.PlayerData.IsRemoveAds)
        {
            Debug.Log("[Ads] Banner skipped (RemoveAds active).");
            return;
        }

        if (_bannerView == null)
        {
            LoadBannerAds();
        }

        _bannerView?.Show();
        Debug.Log("[Ads] Banner shown.");
    }

    public void HideBanner()
    {
        _bannerView?.Hide();
        Debug.Log("[Ads] Banner hidden.");
    }

    private void LoadRewardAds()
    {
        var request = new AdRequest();

        RewardedAd.Load(adsConfig.GetRewardId(), request, (RewardedAd ad, LoadAdError error) =>
        {
            RunOnMainThread(() =>
            {
                if (error != null)
                {
                    Debug.LogError($"[Ads] Rewarded load fail: {error}");
                    return;
                }

                _rewardAd = ad;
                RegisterRewardedCallbacks(ad);
                Debug.Log("[Ads] Rewarded loaded.");
            });
        });
    }

    private void RegisterRewardedCallbacks(RewardedAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
        {
            RunOnMainThread(() =>
            {
                Debug.Log("[Ads] Rewarded opened.");
                _onAdsRewardDisplay?.Invoke();
            });
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            RunOnMainThread(() =>
            {
                Debug.Log("[Ads] Rewarded closed. Reloading...");
                if (_completeRewardAfterClose)
                {
                    if (_rewardGrantedThisShow)
                        _onAdsRewardComplete?.Invoke();
                    else
                        _onAdsRewardFailed?.Invoke();
                }

                _rewardGrantedThisShow = false;
                _completeRewardAfterClose = false;
                ad.Destroy();
                _rewardAd = null;
                LoadRewardAds();
            });
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            RunOnMainThread(() =>
            {
                Debug.LogError($"[Ads] Rewarded show failed: {error}. Reloading...");
                _rewardGrantedThisShow = false;
                _completeRewardAfterClose = false;
                ad.Destroy();
                _rewardAd = null;
                LoadRewardAds();
                _onAdsRewardFailed?.Invoke();
            });
        };

        ad.OnAdPaid += adValue =>
        {
            RunOnMainThread(() =>
            {
                string adNetwork = ad.GetResponseInfo().GetMediationAdapterClassName();
                //FirebaseController.Instance.TrackingAdsRevenue("unknown", AdsPlacement, (adValue.Value / 1000000f).ToString(), "reward_ads", adNetwork);
                Debug.Log($"[Ads] Rewarded paid: {adValue.Value} {adValue.CurrencyCode}");
            });
        };
        ad.OnAdImpressionRecorded += () =>
            RunOnMainThread(() => Debug.Log("[Ads] Rewarded impression."));
    }

    public void ShowRewardAds(Action completeCallback, Action displayCallback = null, Action failedCallback = null, string placement = "unknown", bool completeAfterClose = false)
    {
        if (Data.PlayerData.IsRemoveAds)
        {
            completeCallback?.Invoke();
        }
        else
        {
            if (_rewardAd != null)
            {
                AdsPlacement = placement;
                Data.PlayerData.CountShowInterAds = 0;
                _onAdsRewardComplete = completeCallback;
                _onAdsRewardDisplay = displayCallback;
                _onAdsRewardFailed = failedCallback;
                _rewardGrantedThisShow = false;
                _completeRewardAfterClose = completeAfterClose;
                _rewardAd.Show(reward =>
                {
                    RunOnMainThread(() =>
                    {
                        _rewardGrantedThisShow = true;
                        if (!_completeRewardAfterClose)
                            _onAdsRewardComplete?.Invoke();
                    });
                });
            }
            else
            {
                LoadRewardAds();
                // A caller must be released when the rewarded ad is still
                // loading; otherwise its button can remain disabled forever.
                failedCallback?.Invoke();
            }
        }
    }
}
