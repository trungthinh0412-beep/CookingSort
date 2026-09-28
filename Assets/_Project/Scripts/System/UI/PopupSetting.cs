using UnityEngine;

public class PopupSetting : Popup
{
    private Popup sourcePopup;
    private Level pausedLevel;
    private float previousTimeScale;
    private bool previousInteractable;
    private bool previousBlocksRaycasts;
    private bool returningToSource;

    protected override void BeforeShow()
    {
        base.BeforeShow();
        var controller = PopupController.Instance;
        sourcePopup = controller != null ? controller.currentPopup : null;
        returningToSource = false;
        pausedLevel = null;

        if (sourcePopup == null || sourcePopup == this) return;
        if (sourcePopup.CanvasGroup != null)
        {
            previousInteractable = sourcePopup.CanvasGroup.interactable;
            previousBlocksRaycasts = sourcePopup.CanvasGroup.blocksRaycasts;
            sourcePopup.CanvasGroup.interactable = false;
            sourcePopup.CanvasGroup.blocksRaycasts = false;
        }
        if (Canvas != null && sourcePopup.Canvas != null)
        {
            Canvas.overrideSorting = true;
            Canvas.sortingOrder = Mathf.Max(Canvas.sortingOrder, sourcePopup.Canvas.sortingOrder + 1);
        }
        if (sourcePopup is PopupInGame && GameManager.Instance != null)
        {
            previousTimeScale = Time.timeScale;
            pausedLevel = GameManager.Instance.levelController.currentLevel;
            pausedLevel?.SetPaused(true);
            Time.timeScale = 0f;
        }
    }

    public void OnClickBack()
    {
        SoundController.Instance?.PlayFX(SoundName.ClickButton);
        returningToSource = true;
        Hide();
    }

    protected override void OnDisable()
    {
        // Also release the pause if another navigation action dismisses Settings.
        // Keep the source visible underneath the overlay; do not reload the level.
        if (sourcePopup != null && sourcePopup.CanvasGroup != null)
        {
            sourcePopup.CanvasGroup.interactable = previousInteractable;
            sourcePopup.CanvasGroup.blocksRaycasts = previousBlocksRaycasts;
        }
        if (pausedLevel != null)
        {
            if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = previousTimeScale;
            var manager = GameManager.Instance;
            if (sourcePopup != null && sourcePopup.isActiveAndEnabled && manager != null &&
                manager.gameState == GameState.PlayingGame && manager.levelController.currentLevel == pausedLevel)
                pausedLevel.SetPaused(false);
        }
        base.OnDisable();
    }

    protected override void AfterHidden()
    {
        base.AfterHidden();
        var controller = PopupController.Instance;
        if (returningToSource && controller != null && controller.currentPopup == this &&
            sourcePopup != null && sourcePopup.isActiveAndEnabled)
        {
            controller.currentPopup = sourcePopup;
        }
        sourcePopup = null;
        pausedLevel = null;
        returningToSource = false;
    }
}
