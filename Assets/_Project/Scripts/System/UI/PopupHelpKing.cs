using UnityEngine;

public class PopupHelpKing : Popup
{
    [SerializeField] private GameObject btnRestorePurchase;

    protected override void BeforeShow()
    {
        base.BeforeShow();

        // PopupHelpKing was cloned from PopupSetting, but its editable layout
        // does not have to keep the Restore Purchase button.
        if (btnRestorePurchase == null)
            return;

#if UNITY_ANDROID
        btnRestorePurchase.SetActive(false);
#elif UNITY_IOS
        btnRestorePurchase.SetActive(true);
#else
        btnRestorePurchase.SetActive(true);
#endif
    }

    public void OnClickRestorePurchase()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        IAPController.Instance.RestorePurchases();
    }

    public void OnClickBack()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        AfterHiddenAction = RestoreHomeAfterClose;
        Hide();
    }

    private void RestoreHomeAfterClose()
    {
        if (PopupController.Instance != null)
        {
            PopupController.Instance.Show<PopupHome>(
                PopupAnimation.None
            );
        }
    }
}
