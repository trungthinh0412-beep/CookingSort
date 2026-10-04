using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupKingdom : Popup
{
    private const int DefaultUnlockLevel = 2;

    [Header("Kingdom State")]
    [SerializeField, Min(1)] private int unlockLevel = DefaultUnlockLevel;
    [SerializeField] private GameObject unlockPopup;
    [SerializeField] private GameObject lockPopup;
    [SerializeField] private Button unlockToolButton;

    [Header("Kingdom Scroll View")]
    [SerializeField] private ScrollRect kingdomScrollView;
    [SerializeField] private RectTransform kingdomViewport;
    [SerializeField] private RectTransform kingdomContent;
    [SerializeField] private Scrollbar kingdomVerticalScrollbar;

    [Header("Kingdom Room View Buttons")]
    [SerializeField] private List<CustomButton> roomViewButtons =
        new List<CustomButton>();

    private bool roomViewButtonsBound;
    private bool unlockToolButtonBound;

    protected override void OnEnable()
    {
        base.OnEnable();
        Observer.LevelChanged += OnLevelChanged;
        RefreshKingdomState();
    }

    protected override void OnDisable()
    {
        Observer.LevelChanged -= OnLevelChanged;
        base.OnDisable();
    }

    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        ResolveKingdomStateObjects();
        BindUnlockToolButton();
        RefreshKingdomState();
        ConfigureScrollView();
        BindRoomViewButtons();
        RefreshRoomViewButtonStates();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        BindUnlockToolButton();
        RefreshKingdomState();
        ConfigureScrollView();
        BindRoomViewButtons();
        RefreshRoomViewButtonStates();

        EnsurePanelLayoutElements();
        Canvas.ForceUpdateCanvases();
        if (kingdomContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(kingdomContent);
        }
    }

    private void OnLevelChanged(int level)
    {
        RefreshKingdomState();
        RefreshRoomViewButtonStates();
    }

    public void RefreshKingdomState()
    {
        ResolveKingdomStateObjects();

        bool unlocked = Data.PlayerData != null &&
            Data.PlayerData.CurrentLevelIndex >= Mathf.Max(1, unlockLevel) &&
            Data.PlayerData.HasBuiltAnyKingdomItem;

        if (unlockPopup != null)
            unlockPopup.SetActive(unlocked);

        if (lockPopup != null)
            lockPopup.SetActive(!unlocked);

        if (unlockToolButton != null)
            unlockToolButton.interactable = false;
    }

    private void ResolveKingdomStateObjects()
    {
        if (unlockPopup != null && lockPopup != null)
            return;

        Transform[] childTransforms =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform childTransform in childTransforms)
        {
            if (childTransform == null)
                continue;

            if (unlockPopup == null &&
                childTransform.name == "Kingdom_Unlock")
            {
                unlockPopup = childTransform.gameObject;
            }
            else if (lockPopup == null &&
                     childTransform.name == "Kingdom_lock")
            {
                lockPopup = childTransform.gameObject;
            }

            if (unlockPopup != null && lockPopup != null)
                return;
        }
    }

    private void BindUnlockToolButton()
    {
        if (unlockToolButtonBound)
            return;

        ResolveKingdomStateObjects();

        if (unlockToolButton == null)
            unlockToolButton = FindUnlockToolButton();

        if (unlockToolButton == null)
            return;

        if (!HasPersistentUnlockToolListener(unlockToolButton))
        {
            unlockToolButton.onClick.RemoveListener(OnClickUnlockTool);
            unlockToolButton.onClick.AddListener(OnClickUnlockTool);
        }

        unlockToolButtonBound = true;
    }

    private bool HasPersistentUnlockToolListener(Button button)
    {
        if (button == null)
            return false;

        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == this &&
                button.onClick.GetPersistentMethodName(i) ==
                nameof(OnClickUnlockTool))
            {
                return true;
            }
        }

        return false;
    }

    private Button FindUnlockToolButton()
    {
        if (lockPopup == null)
            return null;

        Button[] buttons = lockPopup.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null &&
                label.text.Trim().Equals(
                    "Unlock",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return buttons.Length > 0 ? buttons[0] : null;
    }

    public void OnClickUnlockTool()
    {
        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        RefreshKingdomState();
        ConfigureScrollView();
        BindRoomViewButtons();
        RefreshRoomViewButtonStates();

        EnsurePanelLayoutElements();
        Canvas.ForceUpdateCanvases();
        if (kingdomContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(kingdomContent);
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

        kingdomScrollView.viewport = kingdomViewport;
        kingdomScrollView.content = kingdomContent;
        kingdomScrollView.horizontal = false;
        kingdomScrollView.vertical = true;
        kingdomScrollView.movementType = ScrollRect.MovementType.Elastic;
        kingdomScrollView.elasticity = 0.07f;
        kingdomScrollView.inertia = true;
        kingdomScrollView.decelerationRate = 0.135f;
        kingdomScrollView.scrollSensitivity = 30f;
        kingdomScrollView.verticalScrollbar = null;
        kingdomScrollView.verticalScrollbarVisibility =
            ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        EnsurePanelLayoutElements();
    }

    private void EnsurePanelLayoutElements()
    {
        if (kingdomContent == null)
            return;

        for (int i = 0; i < kingdomContent.childCount; i++)
        {
            Transform child = kingdomContent.GetChild(i);
            if (child == null) continue;

            LayoutElement le = child.GetComponent<LayoutElement>();
            if (le == null)
            {
                le = child.gameObject.AddComponent<LayoutElement>();
            }
            RectTransform childRect = child as RectTransform;
            float height = childRect != null && childRect.sizeDelta.y > 0 ? childRect.sizeDelta.y : 850f;
            le.minHeight = height;
            le.preferredHeight = height;
        }
    }

    private void ResetScrollPosition()
    {
        if (kingdomScrollView == null)
            return;

        EnsurePanelLayoutElements();

        Canvas.ForceUpdateCanvases();

        if (kingdomContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(kingdomContent);
        }

        kingdomScrollView.StopMovement();
        kingdomScrollView.verticalNormalizedPosition = 1f;

        Canvas.ForceUpdateCanvases();
        if (kingdomScrollView.verticalScrollbar != null)
        {
            kingdomScrollView.verticalScrollbar.value = 1f;
        }
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

        if (!buildPopup.CanOpenRoom(roomIndex) ||
            !buildPopup.IsRoomCompleted(roomIndex))
        {
            RefreshRoomViewButtonStates();
            return;
        }

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        if (!buildPopup.OpenRoomView(roomIndex))
        {
            RefreshRoomViewButtonStates();
            return;
        }

        popupController.Show<PopupKingdomBuild>(PopupAnimation.ScaleFade);
    }

    public void RefreshRoomViewButtonStates()
    {
        if (kingdomContent == null)
            return;

        PopupController popupController = PopupController.Instance;
        PopupKingdomBuild buildPopup = popupController?.Get<PopupKingdomBuild>() as PopupKingdomBuild;

        if (buildPopup == null)
            return;

        for (int i = 0; i < kingdomContent.childCount; i++)
        {
            Transform roomPanel = kingdomContent.GetChild(i);
            CustomButton roomViewButton = GetRoomViewButton(i, roomPanel);
            bool canOpen = buildPopup.CanOpenRoom(i);
            bool isCompleted = canOpen && buildPopup.IsRoomCompleted(i);

            if (roomViewButton != null)
            {
                roomViewButton.Interactable = isCompleted;

                TMP_Text viewButtonText =
                    roomViewButton.GetComponentInChildren<TMP_Text>(true);

                if (viewButtonText != null)
                    viewButtonText.text = "View";

                GetViewButtonRoot(roomViewButton, roomPanel)
                    .SetActive(isCompleted);
            }

            if (TryGetProgressUi(
                    roomPanel,
                    out GameObject progressRoot,
                    out Image progressFill,
                    out TMP_Text progressText))
            {
                bool hasProgress = buildPopup.TryGetRoomProgress(
                    i,
                    out int builtSlotCount,
                    out int totalSlotCount
                );
                float progress = hasProgress && totalSlotCount > 0
                    ? (float)builtSlotCount / totalSlotCount
                    : 0f;

                progressFill.fillAmount = Mathf.Clamp01(progress);

                if (progressText != null)
                {
                    int progressPercent = builtSlotCount >= totalSlotCount
                        ? 100
                        : Mathf.FloorToInt(progress * 100f);
                    progressText.text = $"{progressPercent}%";
                }

                progressRoot.SetActive(
                    canOpen && hasProgress && !isCompleted
                );
            }

            SetRoomLockState(roomPanel, !canOpen);
        }
    }

    private CustomButton GetRoomViewButton(int roomIndex, Transform roomPanel)
    {
        if (roomViewButtons != null &&
            roomIndex >= 0 &&
            roomIndex < roomViewButtons.Count &&
            roomViewButtons[roomIndex] != null)
        {
            return roomViewButtons[roomIndex];
        }

        return roomPanel != null
            ? roomPanel.GetComponentInChildren<CustomButton>(true)
            : null;
    }

    private static GameObject GetViewButtonRoot(
        CustomButton roomViewButton,
        Transform roomPanel)
    {
        Transform buttonTransform = roomViewButton.transform;
        Transform buttonParent = buttonTransform.parent;

        return buttonParent != null && buttonParent != roomPanel
            ? buttonParent.gameObject
            : buttonTransform.gameObject;
    }

    private static bool TryGetProgressUi(
        Transform roomPanel,
        out GameObject progressRoot,
        out Image progressFill,
        out TMP_Text progressText)
    {
        progressRoot = null;
        progressFill = null;
        progressText = null;

        if (roomPanel == null)
            return false;

        Image[] images = roomPanel.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image == null || image.type != Image.Type.Filled)
                continue;

            progressFill = image;
            Transform rootTransform = image.transform.parent;
            progressRoot = rootTransform != null
                ? rootTransform.gameObject
                : image.gameObject;
            progressText = progressRoot.GetComponentInChildren<TMP_Text>(true);
            return true;
        }

        return false;
    }

    private static void SetRoomLockState(
        Transform roomPanel,
        bool isLocked)
    {
        if (roomPanel == null)
            return;

        Transform[] roomTransforms =
            roomPanel.GetComponentsInChildren<Transform>(true);

        foreach (Transform roomTransform in roomTransforms)
        {
            if (roomTransform == null ||
                (roomTransform.name != "LockIcon" &&
                 roomTransform.name != "LockIconTop"))
            {
                continue;
            }

            roomTransform.gameObject.SetActive(isLocked);
        }
    }
}
