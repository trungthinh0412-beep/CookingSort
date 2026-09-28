using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupNewFeature : Popup
{
    [SerializeField] private Image featureIcon;
    [SerializeField] private TextMeshProUGUI featureDescriptionText;
    [SerializeField] private TextMeshProUGUI featureTitleText;

    private FeatureData _currentFeatureData;

    public void Setup(FeatureData featureData)
    {
        _currentFeatureData = featureData;

        featureIcon.sprite = featureData.sprite;
        featureIcon.SetNativeSize();
        featureDescriptionText.text = featureData.description;
        featureTitleText.text = featureData.featureName;
    }

    public void OnClickOk()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        Hide();
    }
}
