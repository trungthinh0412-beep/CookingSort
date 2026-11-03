public class PopupInGame : Popup
{
    [UnityEngine.SerializeField] private IngamePauseMenu pauseMenu;
    [UnityEngine.Header("Cooking Blast Boosters")]
    [UnityEngine.SerializeField] private UnityEngine.GameObject orderBellButton;
    [UnityEngine.SerializeField] private UnityEngine.GameObject lineUpButton;
    [UnityEngine.SerializeField] private UnityEngine.GameObject rewindButton;
    [UnityEngine.SerializeField] private UnityEngine.GameObject trashAwayButton;
    private CookingSort2D.CookingSortGameController _game;

    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        _game = UnityEngine.Object.FindFirstObjectByType<CookingSort2D.CookingSortGameController>();
        BindBoosterButton(orderBellButton, OnClickOrderBell);
        BindBoosterButton(lineUpButton, OnClickLineUp);
        BindBoosterButton(rewindButton, OnClickRewind);
        BindBoosterButton(trashAwayButton, OnClickTrashAway);
        if (pauseMenu != null) pauseMenu.CloseImmediate();
    }

    public void OnClickSetting()
    {
        if (pauseMenu != null) pauseMenu.Open();
    }

    public void OnClickOrderBell() => UseBooster(BoosterType.OrderBell);
    public void OnClickLineUp() => UseBooster(BoosterType.LineUp);
    public void OnClickRewind() => UseBooster(BoosterType.Rewind);
    public void OnClickTrashAway() => UseBooster(BoosterType.TrashAway);

    public void OnClickCancelBooster()
    {
        if (_game != null) _game.CancelActiveBooster();
    }

    private void UseBooster(BoosterType type)
    {
        if (_game == null)
            _game = UnityEngine.Object.FindFirstObjectByType<CookingSort2D.CookingSortGameController>();
        if (_game != null) _game.TryUseBooster(type);
    }

    private void BindBoosterButton(
        UnityEngine.GameObject buttonObject,
        UnityEngine.Events.UnityAction callback)
    {
        if (buttonObject == null) return;
        CustomButton button = buttonObject.GetComponent<CustomButton>();
        if (button == null) button = buttonObject.AddComponent<CustomButton>();
        button.Click.RemoveListener(callback);
        button.Click.AddListener(callback);
    }
}
