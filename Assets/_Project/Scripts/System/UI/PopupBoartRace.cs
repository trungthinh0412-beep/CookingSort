public class PopupBoartRace : Popup
{
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
