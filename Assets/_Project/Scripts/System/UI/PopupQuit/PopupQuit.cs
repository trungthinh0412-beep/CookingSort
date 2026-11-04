using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Confirmation dialog shown from the Exit button in the in-game pause menu.
/// </summary>
public class PopupQuit : Popup
{
    private InGamePauseMenu _pauseMenu;
    private bool _isQuitting;

    public void SetPauseMenu(InGamePauseMenu pauseMenu)
    {
        _pauseMenu = pauseMenu;
    }

    protected override void BeforeShow()
    {
        _isQuitting = false;
        base.BeforeShow();
    }

    public void OnClickResume()
    {
        PlayClickSound();
        Hide(PopupAnimation.ScaleFade);

        if (_pauseMenu != null && _pauseMenu.IsOpen)
        {
            _pauseMenu.Close();
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    public void OnClickQuit()
    {
        if (_isQuitting)
        {
            return;
        }

        _isQuitting = true;
        PlayClickSound();
        Hide(PopupAnimation.None);

        if (_pauseMenu != null)
        {
            _pauseMenu.CloseImmediately();
        }
        else
        {
            Time.timeScale = 1f;
        }

        if (PopupController.Instance != null)
        {
            PopupController.Instance.HideAll();
            PopupController.Instance.Show<PopupHome>(PopupAnimation.None);
        }

        SceneManager.LoadScene("GameplayScene");
    }

    private static void PlayClickSound()
    {
        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayFX(SoundName.ClickButton);
        }
    }
}
