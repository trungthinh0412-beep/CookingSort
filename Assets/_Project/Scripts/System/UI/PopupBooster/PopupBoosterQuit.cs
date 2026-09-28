using UnityEngine;

public class PopupBoosterQuit : PopupBooster
{
    public override void OnClickClose()
    {
        PlayClickSound();

        // PopupBoosterQuit duoc mo khi game dang pause. Ve Home phai huy
        // toan bo popup gameplay va tra lai timeScale binh thuong.
        Time.timeScale = 1f;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnHome(playHomeEntrance: true);
            return;
        }

        // Fallback de tranh popup bi ket neu GameManager dang chua san sang.
        if (PopupController.Instance != null)
        {
            PopupController.Instance.Hide<PopupBoosterQuit>(PopupAnimation.None);
            PopupController.Instance.Show<PopupBackground>(PopupAnimation.None);
            PopupController.Instance.ShowHomeWithEntrance();
        }
    }

    protected override void RestoreHomeAfterClose()
    {
    }

    public override void OnClickPlay()
    {
        PlayClickSound();

        if (PopupController.Instance != null)
            PopupController.Instance.Hide<PopupBoosterQuit>(PopupAnimation.None);

        GameManager.Instance.StartPreparedLevelFromPopupBoosterQuit();
    }
}
