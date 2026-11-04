using System;
using UnityEngine;

public class PopupLose : Popup
{
    [Header("Buttons")]
    [Tooltip("Drag the Replay CustomButton here. Its Click event is bound automatically at runtime.")]
    [SerializeField] private CustomButton replayButton;

    private Action _onClaim;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (replayButton != null)
            replayButton.Click.AddListener(OnClickReplay);
    }

    protected override void OnDisable()
    {
        if (replayButton != null)
            replayButton.Click.RemoveListener(OnClickReplay);
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

    public void OnClickReplay()
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
