using UnityEngine;

[CreateAssetMenu(fileName = "AdsConfig", menuName = "ScriptableObject/AdsConfig")]
public class AdsConfig : ScriptableObject
{
    [Header("Ads Config")]
    public int levelTurnOnInterstitialAds = 2;
    public float timeBetweenTwoInterstitialAds = 30;

    [Header("Android Config")] 
    public string androidBannerId = "ca-app-pub-3940256099942544/6300978111";
    public string androidInterstitialId = "ca-app-pub-3940256099942544/1033173712";
    public string androidRewardId = "ca-app-pub-3940256099942544/5224354917";
    
    [Header("IOS Config")]
    public string iosBannerId = "ca-app-pub-3940256099942544/2934735716";
    public string iosInterstitialId = "ca-app-pub-3940256099942544/4411468910";
    public string iosRewardId = "ca-app-pub-3940256099942544/1712485313";
    
    public string GetBannerId()
    {
#if UNITY_IOS
        return iosBannerId;
#endif

        return androidBannerId;
    }
    
    public string GetInterstitialId()
    {
#if UNITY_IOS
        return iosInterstitialId;
#endif

        return androidInterstitialId;
    }

    public string GetRewardId()
    {
#if UNITY_IOS
        return iosRewardId;
#endif

        return androidRewardId;
    }
}