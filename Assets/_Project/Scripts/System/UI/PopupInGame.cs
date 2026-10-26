public class PopupInGame : Popup
{
    [UnityEngine.SerializeField] private IngamePauseMenu pauseMenu;

    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        if (pauseMenu != null) pauseMenu.CloseImmediate();
    }

    public void OnClickSetting()
    {
        if (pauseMenu != null) pauseMenu.Open();
    }
}
