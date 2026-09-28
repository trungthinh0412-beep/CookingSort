using UnityEngine;

public class PopupCollection : Popup
{
    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        BindInsectButtons();
    }

    protected override void AfterShown()
    {
        base.AfterShown();
    }

    private void BindInsectButtons()
    {
        CustomButton[] buttons =
            GetComponentsInChildren<CustomButton>(true);

        foreach (CustomButton button in buttons)
        {
            if (!button.name.StartsWith("CollectionButton_Insect_"))
                continue;

            button.Click.RemoveListener(OnClickInsect);
            button.Click.AddListener(OnClickInsect);
        }
    }

    private void OnClickInsect()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        PopupController.Instance.Show<PopupCollectionInfor>(
            PopupAnimation.ScaleFade
        );
    }
}
 
