using UnityEngine;

/// <summary>
/// Information popup shown when a collection insect card is selected.
/// Its visual layout is based on PopupAvatar.
/// </summary>
public sealed class PopupCollectionInfor : Popup
{
    public void OnClose()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        AfterHiddenAction = RestoreCollectionAfterClose;
        Hide(PopupAnimation.None);
    }

    private void RestoreCollectionAfterClose()
    {
        PopupController popupController = PopupController.Instance;
        if (popupController == null)
            return;

        PopupCollection collectionPopup =
            popupController.Get<PopupCollection>() as PopupCollection;

        if (collectionPopup != null)
        {
            popupController.Show<PopupCollection>(PopupAnimation.None);
            return;
        }

        popupController.SetBottomBarVisible(true);
    }
}
