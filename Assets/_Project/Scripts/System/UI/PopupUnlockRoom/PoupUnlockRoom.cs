using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PopupUnlockRoom : Popup
{
    [Header("Unlock Room")]
    [SerializeField] private Button unlockButton;
    [SerializeField] private List<GameObject> roomVisuals =
        new List<GameObject>();

    private PopupHome owner;
    private PopupKingdomBuild buildPopup;
    private int pendingRoomIndex = -1;
    private string pendingRoomId = string.Empty;

    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        BindUnlockButton();
        RefreshRoomVisual();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        BindUnlockButton();
        RefreshRoomVisual();
    }

    public void Configure(
        PopupHome popupHome,
        PopupKingdomBuild popupKingdomBuild,
        int roomIndex,
        string roomId)
    {
        owner = popupHome;
        buildPopup = popupKingdomBuild;
        pendingRoomIndex = roomIndex;
        pendingRoomId = string.IsNullOrWhiteSpace(roomId)
            ? string.Empty
            : roomId.Trim();

        RefreshRoomVisual();
    }

    public void OnClickUnlock()
    {
        if (buildPopup == null || string.IsNullOrEmpty(pendingRoomId))
            return;

        if (!buildPopup.TryUnlockRoom(pendingRoomId))
            return;

        if (unlockButton != null)
            unlockButton.interactable = false;

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        owner?.RefreshKingdomBuildDecorations();
        Hide(PopupAnimation.ScaleFade);
    }

    private void BindUnlockButton()
    {
        if (unlockButton == null)
            return;

        unlockButton.onClick.RemoveListener(OnClickUnlock);
        unlockButton.onClick.AddListener(OnClickUnlock);
        unlockButton.interactable = true;
    }

    private void RefreshRoomVisual()
    {
        if (roomVisuals == null)
            return;

        for (int i = 0; i < roomVisuals.Count; i++)
        {
            if (roomVisuals[i] != null)
                roomVisuals[i].SetActive(i == pendingRoomIndex);
        }

        if (pendingRoomIndex >= roomVisuals.Count &&
            pendingRoomIndex >= 0)
        {
            Debug.LogWarning(
                $"[PopupUnlockRoom] Room index {pendingRoomIndex} has no visual. " +
                "Add a GameObject at the same index in Room Visuals."
            );
        }
    }
}
