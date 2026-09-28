using System;
using System.Collections;
using CustomInspector;
using UnityEngine;

public class InternetController : SingletonDontDestroy<InternetController>
{
    [SerializeField] private InternetConfig internetConfig;

    [Header("No Internet Popup")]
    [SerializeField] private bool showNoInternetPopup;

    [ReadOnly] public bool isConnected;

    private const float TimeStart = 0f;

    void Start()
    {
        var repeatRate = internetConfig.repeatRate;
        InvokeRepeating(nameof(CheckInternet), TimeStart, repeatRate);
    }

    private void CheckInternet()
    {
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

    IEnumerator CheckInternetConnection(Action<bool> action)
    {
        WWW www = new WWW("https://google.com");
        yield return www;
        action(www.error == null);
    }
}
