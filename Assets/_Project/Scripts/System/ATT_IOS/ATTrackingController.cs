using System;
using UnityEngine;
#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

public class ATTrackingController : MonoBehaviour
{
    /// <summary>
    /// Requests the iOS App Tracking Transparency (ATT) authorization.
    /// This should be called before initializing ads (AdMob) or analytics (Firebase).
    /// </summary>
    /// <param name="onComplete">Callback executed after the user accepts, rejects, or if the platform is not iOS.</param>
    public static void RequestTracking(Action onComplete)
    {
#if UNITY_IOS && !UNITY_EDITOR
        var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
        if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
        {
            Debug.Log("[ATT] App Tracking Transparency status is NOT_DETERMINED. Requesting authorization...");
            ATTrackingStatusBinding.RequestAuthorizationTracking(result =>
            {
                Debug.Log($"[ATT] App Tracking Transparency authorization result: {result}");
                onComplete?.Invoke();
            });
        }
        else
        {
            Debug.Log($"[ATT] App Tracking Transparency tracking already determined. Current status: {status}");
            onComplete?.Invoke();
        }
#else
        Debug.Log("[ATT] ATT is only supported on iOS devices. Skipping...");
        onComplete?.Invoke();
#endif
    }
}
