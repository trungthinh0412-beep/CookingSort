using System;
using UnityEngine;

public class PopupWin : Popup
{
    [Header("Buttons")]
    [Tooltip("Drag the Next Level CustomButton here. Its Click event is bound automatically at runtime.")]
    [SerializeField] private CustomButton nextLevelButton;

    private Action _onClaim;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (nextLevelButton != null)
            nextLevelButton.Click.AddListener(OnClickNextLevel);
    }

    protected override void OnDisable()
    {
        if (nextLevelButton != null)
            nextLevelButton.Click.RemoveListener(OnClickNextLevel);
        base.OnDisable();
    }

    public void Configure(Action onClaim)
    {
        _onClaim = onClaim;
    }

    public void OnClickClaim()
    {
        ExecuteConfiguredAction();
    }

    public void OnClickNextLevel()
    {
        ExecuteConfiguredAction();
    }

    private void ExecuteConfiguredAction()
    {
        Action callback = _onClaim;
        _onClaim = null;
        Hide(PopupAnimation.ScaleFade);
        callback?.Invoke();
    }
}
