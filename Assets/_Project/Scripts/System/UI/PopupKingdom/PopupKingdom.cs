using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PopupKingdom : Popup
{
    [Header("Kingdom Scroll View")]
    [SerializeField] private ScrollRect kingdomScrollView;
    [SerializeField] private RectTransform kingdomViewport;
    [SerializeField] private RectTransform kingdomContent;
    [SerializeField] private Scrollbar kingdomVerticalScrollbar;

    [Header("Kingdom Room View Buttons")]
    [SerializeField] private List<CustomButton> roomViewButtons =
        new List<CustomButton>();

    private bool roomViewButtonsBound;

    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        ConfigureScrollView();
        BindRoomViewButtons();
        RefreshRoomViewButtonStates();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        ConfigureScrollView();
        BindRoomViewButtons();
        RefreshRoomViewButtonStates();
    }

    protected override void AfterShown()
    {
        base.AfterShown();
        ResetScrollPosition();
    }

    private void ConfigureScrollView()
    {
        if (kingdomScrollView == null)
        {
            Transform scrollViewTransform =
                transform.Find("KingdomContainer/KingdomScrollView");

            if (scrollViewTransform != null)
            {
                kingdomScrollView = scrollViewTransform
                    .GetComponent<ScrollRect>();
            }

            if (kingdomScrollView == null)
            {
                Debug.LogWarning("[PopupKingdom] KingdomScrollView is not assigned.");
                return;
            }
        }

        if (kingdomViewport == null)
        {
            Transform viewportTransform =
                kingdomScrollView.transform.Find("KingdomViewport");

            if (viewportTransform != null)
                kingdomViewport = viewportTransform.GetComponent<RectTransform>();
        }

        if (kingdomContent == null && kingdomViewport != null)
        {
            Transform contentTransform =
                kingdomViewport.Find("KingdomContent");

            if (contentTransform != null)
                kingdomContent = contentTransform.GetComponent<RectTransform>();
        }

        if (kingdomVerticalScrollbar == null)
        {
            Transform scrollbarTransform =
                kingdomScrollView.transform.Find("KingdomVerticalScrollbar");

            if (scrollbarTransform != null)
            {
                kingdomVerticalScrollbar = scrollbarTransform
                    .GetComponent<Scrollbar>();
            }
        }

        kingdomScrollView.viewport = kingdomViewport;
        kingdomScrollView.content = kingdomContent;
        kingdomScrollView.horizontal = false;
        kingdomScrollView.vertical = true;
        kingdomScrollView.verticalScrollbar = kingdomVerticalScrollbar;
        kingdomScrollView.verticalScrollbarVisibility =
            ScrollRect.ScrollbarVisibility.Permanent;
    }

    private void ResetScrollPosition()
    {
        if (kingdomScrollView == null)
            return;

        Canvas.ForceUpdateCanvases();

        if (kingdomContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                kingdomContent
            );
        }

        kingdomScrollView.StopMovement();
        kingdomScrollView.verticalNormalizedPosition = 1f;
    }

    private void BindRoomViewButtons()
    {
        if (roomViewButtonsBound)
            return;

        if (roomViewButtons == null)
            roomViewButtons = new List<CustomButton>();

        if (roomViewButtons.Count == 0)
            DiscoverRoomViewButtons();

        if (roomViewButtons.Count == 0)
            return;

        for (int i = 0; i < roomViewButtons.Count; i++)
        {
            CustomButton roomViewButton = roomViewButtons[i];

            if (roomViewButton == null)
                continue;

            int roomIndex = i;
            roomViewButton.Click.AddListener(() => OnClickRoomView(roomIndex));
        }

        roomViewButtonsBound = true;
    }

    private void DiscoverRoomViewButtons()
    {
        if (kingdomContent == null)
            return;

        for (int i = 0; i < kingdomContent.childCount; i++)
        {
            Transform roomPanel = kingdomContent.GetChild(i);
            CustomButton roomViewButton =
                roomPanel.GetComponentInChildren<CustomButton>(true);

            if (roomViewButton != null)
                roomViewButtons.Add(roomViewButton);
        }
    }

    public void OnClickRoomView(int roomIndex)
    {
        PopupController popupController = PopupController.Instance;

        if (popupController == null)
            return;

        PopupKingdomBuild buildPopup =
            popupController.Get<PopupKingdomBuild>() as PopupKingdomBuild;

        if (buildPopup == null)
        {
            Debug.LogWarning(
                "[PopupKingdom] PopupKingdomBuild is not registered in PopupConfig."
            );
            return;
        }

        if (!buildPopup.CanOpenRoom(roomIndex))
        {
            RefreshRoomViewButtonStates();
            return;
        }

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        buildPopup.OpenRoom(roomIndex);
        popupController.Show<PopupKingdomBuild>(PopupAnimation.ScaleFade);
    }

    public void RefreshRoomViewButtonStates()
    {
        if (roomViewButtons == null || roomViewButtons.Count == 0)
            return;

        PopupController popupController = PopupController.Instance;
        PopupKingdomBuild buildPopup = popupController?.Get<PopupKingdomBuild>() as PopupKingdomBuild;

        if (buildPopup == null)
            return;

        for (int i = 0; i < roomViewButtons.Count; i++)
        {
            CustomButton roomViewButton = roomViewButtons[i];

            if (roomViewButton != null)
                roomViewButton.Interactable = buildPopup.CanOpenRoom(i);
        }
    }
}
