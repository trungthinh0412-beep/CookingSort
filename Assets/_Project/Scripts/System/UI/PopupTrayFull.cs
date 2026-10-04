using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PopupTrayFull : Popup, IPointerClickHandler
{
    [SerializeField] private CustomButton okButton;

        private void Awake()
    {
        if (okButton != null)
        {
            okButton.Click.AddListener(() => Hide(PopupAnimation.None));
        }
    }
    protected override void BeforeShow()
    {
        base.BeforeShow();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Hide(PopupAnimation.None);
    }
}