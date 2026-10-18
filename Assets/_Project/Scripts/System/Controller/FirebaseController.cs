using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CustomInspector;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;

public class FirebaseController : SingletonDontDestroy<FirebaseController>
{
    [SerializeField] private FirebaseConfig firebaseConfig;
    [ReadOnly] public DependencyStatus dependencyStatus = DependencyStatus.UnavailableOther;

    public bool IsInitializedFirebaseApp { get; set; }
    public bool IsInitializedFirebaseRemoteConfig { get; set; }

    public Dictionary<string, object> RemoteConfigData = new Dictionary<string, object>();

    private float _playerTimePlayCounter;
    protected override void Awake()
    {
        base.Awake();
        Initialize();
    }

    private void Update()
    {
        _playerTimePlayCounter += Time.deltaTime;
    }

    private void Initialize()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                InitializeFirebaseRemoteConfig();
                FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLogin);
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
            }

            IsInitializedFirebaseApp = true;
        });
    }

    private async void InitializeFirebaseRemoteConfig()
    {
        FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);

        foreach (var data in firebaseConfig.defaultRemoteData)
        {
            if (int.TryParse(data.value, out int intValue))
            {
                RemoteConfigData[data.key.ToString()] = intValue;
            }
            else if (bool.TryParse(data.value, out bool boolValue))
            {
                RemoteConfigData[data.key.ToString()] = boolValue;
            }
            else if (float.TryParse(data.value, out float floatValue))
            {
                RemoteConfigData[data.key.ToString()] = floatValue;
            }
            else
            {
                RemoteConfigData[data.key.ToString()] = data.value;
            }
        }

        await FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(RemoteConfigData).ContinueWithOnMainThread(task => { Debug.Log("<color=green> RemoteConfig configured and ready! </color>"); });
        IsInitializedFirebaseRemoteConfig = true;
        await FetchDataAsync();
    }

    private Task FetchDataAsync()
    {
        Debug.Log("<color=green> Fetching data from Firebase ... </color>");
        Task fetchTask = FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.Zero);
        if (fetchTask.IsCanceled)
        {
            Debug.Log("Fetch canceled.");
        }
        else if (fetchTask.IsFaulted)
        {
            Debug.Log("Fetch encountered an error.");
        }
        else if (fetchTask.IsCompleted)
        {
            Debug.Log("Fetch completed successfully!");
        }

        return fetchTask.ContinueWithOnMainThread(task =>
        {
            var info = FirebaseRemoteConfig.DefaultInstance.Info;

            if (info.LastFetchStatus == LastFetchStatus.Success)
            {
                FirebaseRemoteConfig.DefaultInstance.ActivateAsync()
                    .ContinueWithOnMainThread(task =>
                    {
                        Debug.LogWarning($"Remote data loaded and ready (last fetch time {info.FetchTime}).");
                    });

                Debug.LogWarning("<color=green> Firebase Remote Config Fetching Values</color>");

                // Try catch for crashlytics
                try
                {
                    foreach (var data in firebaseConfig.defaultRemoteData)
                    {
                        var remoteValue = FirebaseRemoteConfig.DefaultInstance.GetValue(data.key.ToString());
                        if (remoteValue.Source == ValueSource.RemoteValue)
                        {
                            RemoteConfigData[data.key.ToString()] = remoteValue.StringValue;
                            Debug.LogWarning($"<color=green>{data.key.ToString()}: {RemoteConfigData[data.key.ToString()]}</color>");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                Debug.Log("<color=green> Fetching data from Firebase succeed ... </color>");

                SetDataRemote();
            }
            else
            {
                Debug.Log("<color=red> Fetching data did not completed! </color>");
            }

            IsInitializedFirebaseRemoteConfig = true;
        });
    }

    private void SetDataRemote()
    {
        Debug.Log("<color=green> Set data from remote succeed </color>");
    }

    #region Event

    public void TrackingStartLevel(string levelName)
    {
        // Tracking start level
        LogEvent("start_level", new Parameter[]
        {
            new Parameter("level_name", levelName),
        });

        // Tracking first start level
        if (PlayerPrefs.GetInt(levelName, 0) == 0)
        {
            PlayerPrefs.SetInt(levelName, 1);
            LogEvent("first_start_level", new Parameter[]
            {
                new Parameter("level_name", levelName),
            });
        }
    }

    public void TrackingWinLevel(string levelName)
    {
        LogEvent("win_level", new Parameter[]
        {
            new Parameter("level_name", levelName),
        });
    }

    public void TrackingLoseLevel(string levelName)
    {
        LogEvent("lose_level", new Parameter[]
        {
            new Parameter("level_name", levelName),
        });
    }

    public void TrackingAdsRevenue(string playMode, string placement, string value, string adFormat, string adNetwork)
    {
        LogEvent("ads_revenue", new Parameter[]
        {
            new Parameter("play_mode", playMode),
            new Parameter("placement", placement),
            new Parameter("value", value),
            new Parameter("ad_format", adFormat),
            new Parameter("ad_network", adNetwork),
        });
    }

    public void TrackingIapRevenue(string placement, string packId)
    {
        LogEvent("iap_revenue", new Parameter[]
        {
            new Parameter("placement", placement),
            new Parameter("pack_id", packId)
        });
    }

    #endregion

    #region Base

    private bool IsMobile()
    {
        return (Application.platform == RuntimePlatform.Android ||
                Application.platform == RuntimePlatform.IPhonePlayer);
    }

    private void SetUserProperty(string name, string property)
    {
        if (!IsMobile() || dependencyStatus != DependencyStatus.Available) return;
        try
        {
            FirebaseAnalytics.SetUserProperty(name, property);
        }
        catch (Exception e)
        {
            Debug.LogError("User property log error: " + e.ToString());
            throw;
        }
    }

    private void LogEvent(string paramName, Parameter[] parameters)
    {
        if (!IsMobile() || dependencyStatus != DependencyStatus.Available) return;
        try
        {
            FirebaseAnalytics.LogEvent(paramName, parameters);
        }
        catch (Exception e)
        {
            Debug.LogError("Event log error: " + e.ToString());
            throw;
        }
    }

    private void LogEvent(string paramName)
    {
        if (!IsMobile() || dependencyStatus != DependencyStatus.Available) return;
        try
        {
            FirebaseAnalytics.LogEvent(paramName);
        }
        catch (Exception e)
        {
            Debug.LogError("Event log error: " + e.ToString());
            throw;
        }
    }

    #endregion
}
