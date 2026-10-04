using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisplayProfileUI : MonoBehaviour
{
    [SerializeField] private ProfileConfig profileConfig;
    [SerializeField] private Image imgAvatar;
    [SerializeField] private Image imgAvatarFrame;
    [SerializeField] private TextMeshProUGUI txtPlayerName;

    private void OnEnable()
    {
        Observer.ProfileChanged += UpdateData;
        UpdateData();
    }

    private void OnDisable()
    {
        Observer.ProfileChanged -= UpdateData;
    }

    public void UpdateData()
    {
        if (Data.PlayerData == null)
            return;

        UpdatePreview(
            Data.PlayerData.CurrentIndexAvatar,
            Data.PlayerData.CurrentIndexFrame,
            Data.PlayerData.CurrentName
        );
    }

    public void UpdatePreview(int avatarIndex, int frameIndex, string playerName)
    {
        if (profileConfig != null)
        {
            if (imgAvatar != null)
                imgAvatar.sprite = profileConfig.GetSprite(ProfileType.Avatar, avatarIndex);

            if (imgAvatarFrame != null)
                imgAvatarFrame.sprite = profileConfig.GetSprite(ProfileType.AvatarFrame, frameIndex);
        }

        if (txtPlayerName != null)
            txtPlayerName.text = PlayerData.GetDisplayName(playerName);
    }
}
