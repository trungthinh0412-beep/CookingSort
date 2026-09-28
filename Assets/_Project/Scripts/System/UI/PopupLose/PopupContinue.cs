using UnityEngine;

public sealed class PopupContinue : Popup
{
    [SerializeField] private GameObject btnCoin;
    [SerializeField] private GameObject btnAds;

    protected override void BeforeShow()
    {
        base.BeforeShow();

        if (btnCoin != null)
            btnCoin.SetActive(false);

        if (btnAds != null)
            btnAds.SetActive(false);
    }

    public static bool HasAvailableOption(Level level)
    {
        return false;
    }

    public void OnClickCoin()
    {
        ConfirmLose();
    }

    public void OnClickAds()
    {
        ConfirmLose();
    }

    public void OnClickX()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        ConfirmLose();
    }

    private void ConfirmLose()
    {
        Hide();

        if (GameManager.Instance != null)
            GameManager.Instance.ConfirmLoseAfterWarning();
        else
            PopupController.Instance.Show<PopupLose>();
    }
}
