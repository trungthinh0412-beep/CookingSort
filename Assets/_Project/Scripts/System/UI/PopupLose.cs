using System;

public class PopupLose : Popup
{
    private Action _onClaim;

    public void Configure(Action onClaim)
    {
        _onClaim = onClaim;
    }

    public void OnClickClaim()
    {
        Action callback = _onClaim;
        _onClaim = null;
        Hide(PopupAnimation.ScaleFade);
        callback?.Invoke();
    }
}
