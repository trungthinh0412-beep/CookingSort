using System;
using System.Collections;
using CustomInspector;
using UnityEngine;
using UnityEngine.Networking;

public class InternetController : SingletonDontDestroy<InternetController>
{
    [SerializeField] private InternetConfig internetConfig;

    [Header("No Internet Popup")]
    [SerializeField] private bool showNoInternetPopup;

    [ReadOnly] public bool isConnected;

    private const float TimeStart = 0f;
    private const string ConnectivityUrl = "https://www.google.com/generate_204";
    private const int ConnectivityTimeoutSeconds = 5;
    private bool _isChecking;

    void Start()
    {
        var repeatRate = internetConfig.repeatRate;
        InvokeRepeating(nameof(CheckInternet), TimeStart, repeatRate);
    }

    private void CheckInternet()
    {
        if (_isChecking)
            return;

        StartCoroutine(CheckInternetConnection((isConnect) =>
        {
            isConnected = isConnect;
            RefreshNoInternetPopup();
        }));
    }

    private void RefreshNoInternetPopup()
    {
        if (PopupController.Instance == null)
            return;

        if (!showNoInternetPopup || isConnected)
        {
            PopupController.Instance.Hide<PopupNoInternet>();
            return;
        }

        PopupController.Instance.Show<PopupNoInternet>();
    }

    private IEnumerator CheckInternetConnection(Action<bool> action)
    {
        _isChecking = true;

        using (UnityWebRequest request = UnityWebRequest.Head(ConnectivityUrl))
        {
            request.timeout = ConnectivityTimeoutSeconds;
            yield return request.SendWebRequest();

            bool hasConnection = request.result != UnityWebRequest.Result.ConnectionError &&
                                 request.result != UnityWebRequest.Result.DataProcessingError;
            action?.Invoke(hasConnection);
        }

        _isChecking = false;
    }
}
