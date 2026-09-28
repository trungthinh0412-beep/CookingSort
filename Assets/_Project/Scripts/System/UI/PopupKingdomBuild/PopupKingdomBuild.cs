using System;
using System.Collections.Generic;
using CustomTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupKingdomBuild : Popup
{
    [Header("Rooms")]
    [SerializeField] private List<KingdomRoom> rooms = new List<KingdomRoom>();
    [SerializeField] [Min(0)] private int defaultRoomIndex;

    [Header("Build Marker")]
    [SerializeField] private CustomButton buildButton;
    [SerializeField] private CustomButton closeButton;
    [SerializeField] private TextMeshProUGUI roomNameText;
    [Tooltip("Cost text shown next to the currency icon inside the shared BuildMarker.")]
    [SerializeField] private TextMeshProUGUI buildCostText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private GameObject completedState;
    [SerializeField] private CanvasGroup buildMarkerCanvasGroup;
    [SerializeField] private Color buildMarkerTextColor = new Color32(155, 74, 71, 255);
    [SerializeField] private BuildRevealController buildRevealController;
    [Tooltip("Looping dust and sparkle shown around BuildButton while the current cost is affordable.")]
    [SerializeField] private BuildButtonAvailabilityVFX buildButtonAvailabilityVfx;

    [Header("Resource transfer")]
    [SerializeField] private ResourceTransferAnimator resourceTransferAnimator;
    [Tooltip("The BuildHandler that owns the Gem icon in the HUD. Used as the explicit transfer source.")]
    [SerializeField] private BuildHandler resourceHandler;
    [Tooltip("Gem icon inside BuildButton. Gems are aimed here before the marker is consumed.")]
    [SerializeField] private RectTransform buildButtonGemIcon;
    [Tooltip("Micro punch applied to the requirement number when one Gem arrives.")]
    [SerializeField] [Range(1f, 1.1f)] private float requirementTextPunchScale = 1.06f;
    [SerializeField] [Min(0f)] private float requirementTextPunchUpDuration = .03f;
    [SerializeField] [Min(0f)] private float requirementTextPunchDownDuration = .04f;
    [SerializeField] [Range(.65f, .95f)] private float consumeShrinkScale = .85f;
    [SerializeField] [Range(1f, 1.1f)] private float consumeReboundScale = 1.03f;
    [SerializeField] [Min(0f)] private float consumeShrinkDuration = .08f;
    [SerializeField] [Min(0f)] private float consumeReboundDuration = .06f;
    [SerializeField] [Min(0f)] private float afterConsumeBuildDelay = .04f;

    [Header("Room viewport")]
    [Tooltip("Optional ScrollRect used when a room is larger than the visible popup area.")]
    [SerializeField] private ScrollRect roomScrollRect;
    [Tooltip("Content RectTransform that contains all RoomRoot objects.")]
    [SerializeField] private RectTransform roomContent;
    [Tooltip("Prevents the room from being dragged beyond the ScrollRect bounds.")]
    [SerializeField] private ScrollRect.MovementType roomMovementType =
        ScrollRect.MovementType.Clamped;
    [Tooltip("Allows the player to pan a room that is larger than the viewport.")]
    [SerializeField] private bool allowRoomPanning;
    [SerializeField] private bool resetScrollPositionOnRoomOpen = true;
    [Header("Build Marker")]
    [Tooltip("Moves the shared BuildMarker to the MarkerAnchor of the next unfinished slot.")]
    [SerializeField] private bool moveBuildButtonToTarget = true;

    [Header("Build flow timing")]
    [SerializeField] [Min(0f)] private float beforeBuildDelay = .10f;
    [SerializeField] [Min(0f)] private float markerShowDuration = .20f;

    [SerializeField] private bool useUnscaledTime = true;

    [Header("Marker feedback")]
    [SerializeField] [Min(0f)] private float insufficientCurrencyPunchDuration = .18f;
    [SerializeField] [Min(0f)] private float insufficientCurrencyPunchScale = .08f;

    private int currentRoomIndex = -1;
    private bool isBuilding;
    private bool isShowingNextMarker;
    private bool pendingBuildSlotCommitted;
    private bool pendingResourceSpent;
    private bool resetRoomScrollOnNextRefresh = true;
    private Sequence markerSequence;
    private Sequence requirementFeedbackSequence;
    private Tween markerFeedbackTween;
    private Vector3 buildButtonInitialScale = Vector3.one;
    private bool buildButtonScaleCached;
    private Vector3 buildCostTextInitialScale = Vector3.one;
    private bool buildCostTextScaleCached;
    private BuildSlot pendingBuildSlot;
    private KingdomRoom pendingBuildRoom;
    private bool openedFromHomeView;

    public int CurrentRoomIndex => currentRoomIndex;

    public KingdomRoom CurrentRoom => GetCurrentRoom();

    public KingdomRoomState CurrentRoomState => GetRoomState(GetCurrentRoom());

    public bool IsOpenedFromHomeView => openedFromHomeView;

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheUiReferences();
        BindButtons();
        Observer.StarChangedDone += RefreshCurrentRoom;
    }

    protected override void OnDisable()
    {
        Observer.StarChangedDone -= RefreshCurrentRoom;

        if (roomScrollRect != null)
            roomScrollRect.onValueChanged.RemoveListener(OnRoomScrollChanged);

        StopBuildTween();
        isBuilding = false;
        SetBuildButtonAvailabilityVfx(false);
        resetRoomScrollOnNextRefresh = true;

        if (openedFromHomeView)
        {
            PopupHome homePopup =
                PopupController.Instance?.Get<PopupHome>() as PopupHome;

            homePopup?.NotifyKingdomBuildViewHidden(this);
        }

        base.OnDisable();
    }

    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        CacheUiReferences();
        BindButtons();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        CacheUiReferences();
        BindButtons();

        if (rooms == null || rooms.Count == 0)
        {
            currentRoomIndex = -1;
            RefreshCurrentRoom();
            return;
        }

        if (currentRoomIndex < 0 ||
            currentRoomIndex >= rooms.Count ||
            !CanOpenRoom(currentRoomIndex))
        {
            int firstAvailableRoomIndex = GetFirstAvailableRoomIndex();
            currentRoomIndex = firstAvailableRoomIndex >= 0
                ? firstAvailableRoomIndex
                : Mathf.Clamp(defaultRoomIndex, 0, rooms.Count - 1);
        }

        resetRoomScrollOnNextRefresh = true;
        RefreshCurrentRoom();
    }

    protected override void AfterShown()
    {
        base.AfterShown();

        // BeforeShow is called while the popup is still inactive. At that
        // point BuildButton.activeInHierarchy is false, so the availability
        // effect must be refreshed once the popup has actually entered the
        // hierarchy and is visible.
        RefreshBuildButtonAvailabilityVfx();
    }

    public void OpenRoom(int roomIndex)
    {
        if (isBuilding)
            return;

        if (rooms == null || rooms.Count == 0)
        {
            currentRoomIndex = -1;
            Debug.LogWarning("[PopupKingdomBuild] No room has been configured.");
            return;
        }

        if (roomIndex < 0 || roomIndex >= rooms.Count)
        {
            Debug.LogWarning(
                $"[PopupKingdomBuild] Room index {roomIndex} is outside the configured room list."
            );
            return;
        }

        if (!CanOpenRoom(roomIndex))
        {
            Debug.LogWarning(
                $"[PopupKingdomBuild] Room index {roomIndex} is locked. " +
                "Complete the previous room first."
            );
            return;
        }

        StopBuildTween();
        isBuilding = false;
        openedFromHomeView = false;
        resetRoomScrollOnNextRefresh = true;
        currentRoomIndex = Mathf.Clamp(roomIndex, 0, rooms.Count - 1);

        if (isActiveAndEnabled)
            RefreshCurrentRoom();
    }

    public bool CanOpenRoom(int roomIndex)
    {
        if (rooms == null || roomIndex < 0 || roomIndex >= rooms.Count)
            return false;

        KingdomRoom room = rooms[roomIndex];

        if (!AreRoomSlotsValid(room))
            return false;

        return roomIndex == 0 || IsRoomCompleted(roomIndex - 1);
    }

    public bool IsRoomCompleted(int roomIndex)
    {
        if (rooms == null || roomIndex < 0 || roomIndex >= rooms.Count)
            return false;

        return GetRoomState(rooms[roomIndex]) == KingdomRoomState.Completed;
    }

    public void OpenRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || rooms == null)
            return;

        string safeRoomId = roomId.Trim();

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] != null &&
                !string.IsNullOrWhiteSpace(rooms[i].Id) &&
                rooms[i].Id.Trim() == safeRoomId)
            {
                OpenRoom(i);
                return;
            }
        }

        Debug.LogWarning($"[PopupKingdomBuild] Room '{safeRoomId}' was not found.");
    }

    public void OpenFromHomeView()
    {
        openedFromHomeView = true;
        PrepareRoomForHome();
    }

    public void SetBuildViewInputEnabled(bool enabled)
    {
        CanvasGroup popupCanvasGroup = CanvasGroup;

        if (popupCanvasGroup != null)
        {
            popupCanvasGroup.interactable = enabled;
            popupCanvasGroup.blocksRaycasts = enabled;
        }

        if (closeButton != null)
            closeButton.Interactable = enabled && !isBuilding;

        if (!enabled)
        {
            if (buildButton != null)
                buildButton.Interactable = false;

            SetBuildMarkerRaycastState(false);
            SetBuildButtonAvailabilityVfx(false);
        }
        else
        {
            RefreshBuildButtonAvailabilityVfx();
        }
    }

    public void OnClickBuild()
    {
        if (isBuilding || isShowingNextMarker)
            return;

        KingdomRoom room = GetCurrentRoom();
        BuildSlot slot = GetNextBuildSlot(room);

        if (room == null || slot == null)
        {
            RefreshCurrentRoom();
            return;
        }

        if (!IsValidBuildSlot(room, slot))
        {
            Debug.LogWarning(
                $"[PopupKingdomBuild] BuildSlot '{slot.Id}' is not configured correctly. " +
                "Assign an ID, Target Image and Sprite."
            );
            RefreshCurrentRoom();
            return;
        }

        if (Data.PlayerData == null)
        {
            Debug.LogWarning("[PopupKingdomBuild] PlayerData is not available.");
            return;
        }

        if (Data.PlayerData.CurrentStar < slot.Cost)
        {
            PlayInsufficientCurrencyFeedback();
            Observer.Notify?.Invoke("Not enough gems!", Vector3.zero);
            return;
        }

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        isBuilding = true;
        SetBuildButtonAvailabilityVfx(false);
        if (buildButton != null)
            buildButton.Interactable = false;
        if (closeButton != null)
            closeButton.Interactable = false;

        pendingBuildRoom = room;
        pendingBuildSlot = slot;
        pendingBuildSlotCommitted = false;

        // CurrentStar invokes the existing resource UI update exactly once.
        // No tween below changes PlayerData; tweens only animate presentation.
        Data.PlayerData.CurrentStar -= slot.Cost;
        pendingResourceSpent = true;
        StartResourceTransfer();
    }

    public void OnClickClose()
    {
        if (isBuilding)
            return;

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        if (openedFromHomeView)
        {
            PopupHome homePopup =
                PopupController.Instance?.Get<PopupHome>() as PopupHome;

            if (homePopup != null)
            {
                homePopup.ExitKingdomBuildView();
                return;
            }
        }

        PopupController popupController = PopupController.Instance;

        if (popupController == null)
        {
            Hide(PopupAnimation.None);
            return;
        }

        PopupKingdom kingdomPopup =
            popupController.Get<PopupKingdom>() as PopupKingdom;

        if (kingdomPopup != null)
        {
            AfterHiddenAction = () => popupController.Show<PopupKingdom>(PopupAnimation.None);
        }

        popupController.Hide<PopupKingdomBuild>(PopupAnimation.None);
    }

    private void RefreshCurrentRoom()
    {
        KingdomRoom room = GetCurrentRoom();

        if (room == null)
        {
            SetRoomVisibility();
            SetRoomUi(null, 0, 0, false, null);
            return;
        }

        SetRoomVisibility();
        UpdateRoomViewport(room, resetRoomScrollOnNextRefresh);
        resetRoomScrollOnNextRefresh = false;

        List<BuildSlot> slots = room.BuildSlots;
        int totalSlotCount = slots == null ? 0 : slots.Count;
        int builtSlotCount = 0;
        bool allSlotsValid = AreRoomSlotsValid(room);
        BuildSlot nextSlot = null;

        if (slots != null)
        {
            foreach (BuildSlot slot in slots)
            {
                if (slot == null)
                {
                    allSlotsValid = false;
                    continue;
                }

                bool isBuilt = IsSlotCompleted(room, slot);

                if (isBuilt)
                {
                    builtSlotCount++;
                    ApplyBuiltVisual(slot);
                }
                else
                {
                    ApplyUnbuiltVisual(slot);

                    if (nextSlot == null)
                        nextSlot = slot;
                }
            }
        }

        bool isRoomCompleted =
            totalSlotCount > 0 &&
            allSlotsValid &&
            nextSlot == null;

        SetRoomUi(
            room,
            builtSlotCount,
            totalSlotCount,
            isRoomCompleted,
            nextSlot
        );
    }

    private void SetRoomUi(
        KingdomRoom room,
        int builtSlotCount,
        int totalSlotCount,
        bool isRoomCompleted,
        BuildSlot nextSlot)
    {
        if (roomNameText != null)
        {
            roomNameText.text = room == null || string.IsNullOrWhiteSpace(room.Id)
                ? string.Empty
                : room.Id.Trim();
        }

        if (buildCostText != null)
            buildCostText.text = nextSlot == null ? string.Empty : nextSlot.Cost.ToString();

        if (progressText != null)
            progressText.text = $"{builtSlotCount}/{totalSlotCount}";

        if (completedState != null)
            completedState.SetActive(isRoomCompleted);

        if (buildButton != null)
        {
            SetBuildButtonPosition(nextSlot);

            bool canShowMarker =
                !isRoomCompleted &&
                room != null &&
                nextSlot != null &&
                IsValidBuildSlot(room, nextSlot);

            if (!canShowMarker)
            {
                buildButton.Interactable = false;

                if (!isBuilding)
                    HideBuildMarkerInstant();
            }
            else
            {
                if (!isBuilding && !isShowingNextMarker)
                    ShowBuildMarkerInstant(nextSlot);

                buildButton.Interactable = !isBuilding && !isShowingNextMarker;
            }

            RefreshBuildButtonAvailabilityVfx(nextSlot, canShowMarker);
        }
        else
        {
            SetBuildButtonAvailabilityVfx(false);
        }

        if (closeButton != null)
            closeButton.Interactable = !isBuilding;

        ApplyBuildMarkerTextColor();
    }

    public void ApplyBuiltDecorationsToHome(PopupHome homePopup)
    {
        if (homePopup == null || rooms == null)
            return;

        foreach (KingdomRoom room in rooms)
        {
            if (room?.BuildSlots == null)
                continue;

            foreach (BuildSlot slot in room.BuildSlots)
            {
                if (slot == null || string.IsNullOrWhiteSpace(slot.HomeTargetId))
                    continue;

                bool isBuilt = IsSlotCompleted(room, slot);
                homePopup.SetKingdomBuildDecoration(
                    slot.HomeTargetId,
                    slot.Sprite,
                    isBuilt
                );
            }
        }
    }

    private void SetRoomVisibility()
    {
        if (rooms == null)
            return;

        for (int i = 0; i < rooms.Count; i++)
        {
            KingdomRoom room = rooms[i];

            if (room?.RoomRoot == null)
                continue;

            room.RoomRoot.gameObject.SetActive(i == currentRoomIndex);
        }
    }

    private void UpdateRoomViewport(KingdomRoom room, bool resetScrollPosition)
    {
        if (roomScrollRect == null)
            return;

        if (roomContent == null)
            roomContent = roomScrollRect.content;

        if (roomContent == null)
            return;

        RectTransform viewport = roomScrollRect.viewport;
        if (viewport == null)
            viewport = roomScrollRect.transform as RectTransform;

        if (room?.RoomRoot != null && viewport != null)
        {
            Vector2 roomSize = room.RoomRoot.rect.size;
            Vector2 viewportSize = viewport.rect.size;

            roomContent.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Max(roomSize.x, viewportSize.x)
            );
            roomContent.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(roomSize.y, viewportSize.y)
            );
        }

        roomScrollRect.StopMovement();

        if (resetScrollPositionOnRoomOpen && resetScrollPosition)
            roomScrollRect.normalizedPosition = new Vector2(.5f, .5f);
    }

    private void ApplyBuiltVisual(BuildSlot slot)
    {
        if (slot.TargetImage == null)
            return;

        Image targetImage = slot.TargetImage;
        targetImage.sprite = slot.Sprite;
        targetImage.enabled = slot.Sprite != null;
        SetImageAlpha(targetImage, targetImage.sprite == null ? 0f : 1f);
        targetImage.rectTransform.localScale = Vector3.one;
    }

    private void ApplyUnbuiltVisual(BuildSlot slot)
    {
        if (slot.TargetImage == null)
            return;

        slot.TargetImage.enabled = false;
        SetImageAlpha(slot.TargetImage, 0f);
        slot.TargetImage.rectTransform.localScale = Vector3.one;
    }

    public void PrepareRoomForHome()
    {
        if (rooms == null || rooms.Count == 0)
            return;

        if (currentRoomIndex < 0 || currentRoomIndex >= rooms.Count ||
            !CanOpenRoom(currentRoomIndex))
        {
            int firstAvailableRoomIndex = GetFirstAvailableRoomIndex();
            currentRoomIndex = firstAvailableRoomIndex >= 0
                ? firstAvailableRoomIndex
                : Mathf.Clamp(defaultRoomIndex, 0, rooms.Count - 1);
        }

        RefreshCurrentRoom();
    }

    public void CopyBuiltRoomDecorToHome(PopupHome homePopup)
    {
        if (homePopup == null || rooms == null)
            return;

        KingdomRoom room = GetCurrentRoom();

        if (room?.RoomRoot == null)
        {
            homePopup.SetKingdomRoomDecorVisual(null, null);
            return;
        }

        if (room.BackgroundImage != null)
            homePopup.SetKingdomRoomBackground(room.BackgroundImage.sprite);

        List<Image> builtDecorImages = new List<Image>();

        if (room.BuildSlots != null)
        {
            foreach (BuildSlot slot in room.BuildSlots)
            {
                if (slot?.TargetImage != null &&
                    IsSlotCompleted(room, slot))
                {
                    builtDecorImages.Add(slot.TargetImage);
                }
            }
        }

        homePopup.SetKingdomRoomDecorVisual(
            room.RoomRoot,
            builtDecorImages
        );
    }

    private void SetBuildButtonPosition(BuildSlot nextSlot)
    {
        if (!moveBuildButtonToTarget || buildButton == null ||
            nextSlot == null)
        {
            return;
        }

        RectTransform buildButtonRect = buildButton.transform as RectTransform;
        RectTransform targetRect = GetMarkerAnchor(nextSlot);

        if (buildButtonRect == null || targetRect == null)
            return;

        buildButtonRect.position = targetRect.position;
    }

    private static RectTransform GetMarkerAnchor(BuildSlot slot)
    {
        if (slot == null)
            return null;

        return slot.MarkerAnchor != null
            ? slot.MarkerAnchor
            : slot.TargetImage?.rectTransform;
    }

    private BuildSlot GetNextBuildSlot(KingdomRoom room)
    {
        if (room?.BuildSlots == null)
            return null;

        foreach (BuildSlot slot in room.BuildSlots)
        {
            if (slot != null && !IsSlotCompleted(room, slot))
                return slot;
        }

        return null;
    }

    private KingdomRoom GetCurrentRoom()
    {
        if (rooms == null || currentRoomIndex < 0 || currentRoomIndex >= rooms.Count)
            return null;

        return rooms[currentRoomIndex];
    }

    private int GetFirstAvailableRoomIndex()
    {
        if (rooms == null)
            return -1;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (CanOpenRoom(i))
                return i;
        }

        return -1;
    }

    private KingdomRoomState GetRoomState(KingdomRoom room)
    {
        if (room == null || room.BuildSlots == null || room.BuildSlots.Count == 0)
            return KingdomRoomState.NotConfigured;

        if (!AreRoomSlotsValid(room))
            return KingdomRoomState.InProgress;

        foreach (BuildSlot slot in room.BuildSlots)
        {
            if (slot == null || !IsValidBuildSlot(room, slot) || !IsSlotCompleted(room, slot))
                return KingdomRoomState.InProgress;
        }

        return KingdomRoomState.Completed;
    }

    private bool AreRoomSlotsValid(KingdomRoom room)
    {
        if (room == null || room.BuildSlots == null || room.BuildSlots.Count == 0)
            return false;

        if (!IsValidId(GetRoomId(room)))
            return false;

        HashSet<string> slotIds = new HashSet<string>();

        foreach (BuildSlot slot in room.BuildSlots)
        {
            if (!IsValidBuildSlot(room, slot))
                return false;

            if (!slotIds.Add(GetSlotId(slot)))
                return false;
        }

        return true;
    }

    private bool IsSlotCompleted(KingdomRoom room, BuildSlot slot)
    {
        if (Data.PlayerData == null || !IsValidId(GetRoomId(room)) || !IsValidId(GetSlotId(slot)))
            return false;

        return Data.PlayerData.IsKingdomBuildSlotCompleted(
            GetRoomId(room),
            GetSlotId(slot)
        );
    }

    private bool IsValidBuildSlot(KingdomRoom room, BuildSlot slot)
    {
        return room != null &&
               slot != null &&
               IsValidId(GetRoomId(room)) &&
               IsValidId(GetSlotId(slot)) &&
               slot.TargetImage != null &&
               slot.Sprite != null;
    }

    private static bool IsValidId(string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string GetRoomId(KingdomRoom room)
    {
        return room == null || string.IsNullOrWhiteSpace(room.Id)
            ? string.Empty
            : room.Id.Trim();
    }

    private static string GetSlotId(BuildSlot slot)
    {
        return slot == null || string.IsNullOrWhiteSpace(slot.Id)
            ? string.Empty
            : slot.Id.Trim();
    }

    private void StartBuildMarkerHide()
    {
        if (markerFeedbackTween.isAlive)
            markerFeedbackTween.Stop();

        markerFeedbackTween = default;

        if (requirementFeedbackSequence.isAlive)
            requirementFeedbackSequence.Stop();

        requirementFeedbackSequence = default;
        ResetBuildCostTextScale();

        if (buildButton == null)
        {
            StartObjectBuildAfterMarkerDelay();
            return;
        }

        StopMarkerAnimation();
        buildButton.Interactable = false;
        SetBuildMarkerRaycastState(false);

        Transform markerTransform = buildButton.transform;
        float safeShrinkDuration = Mathf.Max(0f, consumeShrinkDuration);
        float safeReboundDuration = Mathf.Max(0f, consumeReboundDuration);
        float safeAfterConsumeDelay = Mathf.Max(0f, afterConsumeBuildDelay);

        if (safeShrinkDuration <= 0f && safeReboundDuration <= 0f)
        {
            SetBuildMarkerInactive();
            StartObjectBuildAfterConsumeDelay();
            return;
        }

        markerSequence = Sequence.Create(useUnscaledTime: useUnscaledTime)
            .Group(Tween.Scale(
                markerTransform,
                markerTransform.localScale,
                buildButtonInitialScale * Mathf.Clamp(
                    consumeShrinkScale,
                    .65f,
                    .95f
                ),
                safeShrinkDuration,
                Ease.InQuad))
            .Chain(Tween.Scale(
                markerTransform,
                buildButtonInitialScale * Mathf.Clamp(
                    consumeShrinkScale,
                    .65f,
                    .95f
                ),
                buildButtonInitialScale * Mathf.Clamp(
                    consumeReboundScale,
                    1f,
                    1.1f
                ),
                safeReboundDuration,
                Ease.OutBack))
            .ChainCallback(SetBuildMarkerInactive)
            .ChainDelay(safeAfterConsumeDelay)
            .ChainCallback(() =>
            {
                markerSequence = default;
                StartObjectBuildAnimation();
            });
    }

    private void StartResourceTransfer()
    {
        SetBuildMarkerRaycastState(false);

        BuildSlot slot = pendingBuildSlot;
        RectTransform targetButton = buildButton != null
            ? buildButton.transform as RectTransform
            : null;

        if (slot == null || targetButton == null ||
            resourceTransferAnimator == null)
        {
            SetBuildCostDisplay(0);
            StartBuildMarkerHide();
            return;
        }

        RectTransform resourceIcon = FindResourceGemIcon();
        RectTransform targetGemIcon = FindBuildButtonGemIcon(resourceIcon);
        SetBuildCostDisplay(slot.Cost);

        if (resourceIcon == null)
        {
            SetBuildCostDisplay(0);
            StartBuildMarkerHide();
            return;
        }

        resourceTransferAnimator.PlayTransfer(
            resourceIcon,
            targetButton,
            targetGemIcon,
            buildCostText,
            slot.Cost,
            StartBuildMarkerHide,
            PlayRequirementArrivalFeedback
        );
    }

    private void PlayRequirementArrivalFeedback(int _)
    {
        if (buildCostText == null || !isBuilding ||
            !buildCostText.gameObject.activeInHierarchy)
        {
            return;
        }

        if (requirementFeedbackSequence.isAlive)
            requirementFeedbackSequence.Stop();

        Transform requirementTransform = buildCostText.transform;
        Vector3 peakScale = buildCostTextInitialScale * Mathf.Clamp(
            requirementTextPunchScale,
            1f,
            1.1f
        );
        float safeScaleUpDuration = Mathf.Max(
            0f,
            requirementTextPunchUpDuration
        );
        float safeScaleDownDuration = Mathf.Max(
            0f,
            requirementTextPunchDownDuration
        );

        requirementFeedbackSequence = Sequence.Create(
            useUnscaledTime: useUnscaledTime
        )
            .Group(Tween.Scale(
                requirementTransform,
                requirementTransform.localScale,
                peakScale,
                safeScaleUpDuration,
                Ease.OutQuad))
            .Chain(Tween.Scale(
                requirementTransform,
                peakScale,
                buildCostTextInitialScale,
                safeScaleDownDuration,
                Ease.InQuad))
            .ChainCallback(() => requirementFeedbackSequence = default);
    }

    private RectTransform FindResourceGemIcon()
    {
        if (resourceHandler != null &&
            resourceHandler.BuildTargetTransform is RectTransform explicitIcon)
        {
            return explicitIcon;
        }

        BuildHandler[] buildHandlers =
            GetComponentsInChildren<BuildHandler>(true);

        for (int i = 0; i < buildHandlers.Length; i++)
        {
            BuildHandler buildHandler = buildHandlers[i];

            if (buildHandler == null ||
                buildHandler.BuildTargetTransform == null)
            {
                continue;
            }

            RectTransform resourceIcon =
                buildHandler.BuildTargetTransform as RectTransform;

            if (resourceIcon != null)
                return resourceIcon;
        }

        return null;
    }

    private RectTransform FindBuildButtonGemIcon(RectTransform resourceIcon)
    {
        if (buildButton == null)
            return null;

        if (buildButtonGemIcon != null &&
            buildButtonGemIcon.IsChildOf(buildButton.transform))
        {
            return buildButtonGemIcon;
        }

        Sprite resourceSprite = FindSprite(resourceIcon);
        Image fallback = null;
        Image[] images = buildButton.GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];

            if (image == null || image.transform == buildButton.transform ||
                image.sprite == null)
            {
                continue;
            }

            if (resourceSprite != null && image.sprite == resourceSprite)
                return image.rectTransform;

            if (fallback == null)
                fallback = image;
        }

        return fallback != null
            ? fallback.rectTransform
            : buildButton.transform as RectTransform;
    }

    private static Sprite FindSprite(RectTransform root)
    {
        if (root == null)
            return null;

        Image image = root.GetComponentInChildren<Image>(true);

        if (image != null && image.sprite != null)
            return image.sprite;

        SpriteRenderer spriteRenderer =
            root.GetComponentInChildren<SpriteRenderer>(true);

        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    private void SetBuildCostDisplay(int value)
    {
        if (buildCostText == null)
            return;

        buildCostText.text = Mathf.Max(0, value).ToString();
        ApplyBuildMarkerTextColor();
    }

    private Tween CreateMarkerFadeTween(float targetAlpha, float duration)
    {
        if (buildMarkerCanvasGroup == null)
            return Tween.Delay(duration);

        return Tween.Alpha(
            buildMarkerCanvasGroup,
            buildMarkerCanvasGroup.alpha,
            targetAlpha,
            duration,
            Ease.InQuad
        );
    }

    private void StartObjectBuildAfterMarkerDelay()
    {
        float safeDelay = Mathf.Max(0f, beforeBuildDelay);

        if (safeDelay <= 0f)
        {
            StartObjectBuildAnimation();
            return;
        }

        markerSequence = Sequence.Create(useUnscaledTime: useUnscaledTime)
            .ChainDelay(safeDelay)
            .ChainCallback(() =>
            {
                markerSequence = default;
                StartObjectBuildAnimation();
            });
    }

    private void StartObjectBuildAfterConsumeDelay()
    {
        float safeDelay = Mathf.Max(0f, afterConsumeBuildDelay);

        if (safeDelay <= 0f)
        {
            StartObjectBuildAnimation();
            return;
        }

        markerSequence = Sequence.Create(useUnscaledTime: useUnscaledTime)
            .ChainDelay(safeDelay)
            .ChainCallback(() =>
            {
                markerSequence = default;
                StartObjectBuildAnimation();
            });
    }

    private void StartObjectBuildAnimation()
    {
        BuildSlot slot = pendingBuildSlot;

        if (slot == null || slot.TargetImage == null || pendingBuildRoom == null)
        {
            RefundPendingResourceIfNeeded();
            FinishBuildFlow();
            return;
        }

        Image targetImage = slot.TargetImage;

        targetImage.sprite = slot.Sprite;
        targetImage.enabled = true;
        SetImageAlpha(targetImage, 1f);

        if (buildRevealController == null)
        {
            CommitBuiltSlot();
            FinishBuildFlow();
            return;
        }

        buildRevealController.PlayBuild(
            targetImage.transform,
            buildRevealController.BuildDuration,
            OnBuildRevealCompleted
        );
    }

    private void OnBuildRevealCompleted()
    {
        CommitBuiltSlot();
        FinishBuildFlow();
    }

    private void CommitBuiltSlot()
    {
        if (pendingBuildSlot == null || pendingBuildSlotCommitted)
            return;

        if (Data.PlayerData != null)
        {
            Data.PlayerData.SetKingdomBuildSlotCompleted(
                GetRoomId(pendingBuildRoom),
                GetSlotId(pendingBuildSlot)
            );
            Data.SaveData();
        }

        pendingBuildSlotCommitted = true;
        pendingResourceSpent = false;
    }

    private void FinishBuildFlow()
    {
        if (pendingBuildSlot?.TargetImage != null)
            SetImageAlpha(pendingBuildSlot.TargetImage, 1f);

        isBuilding = false;
        if (closeButton != null)
            closeButton.Interactable = true;

        isShowingNextMarker = true;
        RefreshCurrentRoom();

        PopupHome homePopup =
            PopupController.Instance?.Get<PopupHome>() as PopupHome;

        homePopup?.RefreshKingdomBuildDecorations();

        PopupKingdom kingdomPopup =
            PopupController.Instance?.Get<PopupKingdom>() as PopupKingdom;

        kingdomPopup?.RefreshRoomViewButtonStates();

        pendingBuildSlot = null;
        pendingBuildRoom = null;
        pendingBuildSlotCommitted = false;

        BuildSlot nextSlot = GetNextBuildSlot(GetCurrentRoom());

        if (nextSlot == null)
        {
            isShowingNextMarker = false;
            HideBuildMarkerInstant();
            RefreshCurrentRoom();
            return;
        }

        ShowNextBuildMarker(nextSlot);
    }

    private void ShowNextBuildMarker(BuildSlot nextSlot)
    {
        if (buildButton == null || nextSlot == null)
        {
            SetBuildButtonAvailabilityVfx(false);
            isShowingNextMarker = false;
            RefreshCurrentRoom();
            return;
        }

        SetBuildButtonPosition(nextSlot);
        UpdateBuildMarkerCost(nextSlot);
        buildButton.gameObject.SetActive(true);
        SetBuildButtonAvailabilityVfx(false);
        SetBuildMarkerRaycastState(false);
        buildButton.Interactable = false;

        Transform markerTransform = buildButton.transform;
        markerTransform.localScale = Vector3.zero;

        if (buildMarkerCanvasGroup != null)
            buildMarkerCanvasGroup.alpha = 0f;

        float safeShowDuration = Mathf.Max(0f, markerShowDuration);

        if (safeShowDuration <= 0f)
        {
            ShowBuildMarkerInstant(nextSlot);
            isShowingNextMarker = false;
            RefreshBuildButtonAvailabilityVfx(nextSlot, true);
            return;
        }

        StopMarkerAnimation();
        markerSequence = Sequence.Create(useUnscaledTime: useUnscaledTime)
            .Group(Tween.Scale(
                markerTransform,
                Vector3.zero,
                buildButtonInitialScale,
                safeShowDuration,
                Ease.OutBack))
            .Group(CreateMarkerFadeTween(1f, safeShowDuration))
            .ChainCallback(() =>
            {
                markerSequence = default;
                isShowingNextMarker = false;
                SetBuildMarkerRaycastState(true);
                buildButton.Interactable = true;
                ApplyBuildMarkerTextColor();
                RefreshBuildButtonAvailabilityVfx(nextSlot, true);
            });
    }

    private void ShowBuildMarkerInstant(BuildSlot nextSlot)
    {
        if (buildButton == null || nextSlot == null)
            return;

        SetBuildButtonPosition(nextSlot);
        UpdateBuildMarkerCost(nextSlot);
        buildButton.gameObject.SetActive(true);
        buildButton.transform.localScale = buildButtonInitialScale;

        if (buildMarkerCanvasGroup != null)
            buildMarkerCanvasGroup.alpha = 1f;

        SetBuildMarkerRaycastState(true);
        buildButton.Interactable = true;
        ApplyBuildMarkerTextColor();
        RefreshBuildButtonAvailabilityVfx(nextSlot, true);
    }

    private void HideBuildMarkerInstant()
    {
        if (buildButton == null)
            return;

        SetBuildButtonAvailabilityVfx(false);
        StopMarkerAnimation();
        buildButton.Interactable = false;
        SetBuildMarkerRaycastState(false);
        SetBuildMarkerInactive();
    }

    private void SetBuildMarkerInactive()
    {
        if (buildButton == null)
            return;

        SetBuildButtonAvailabilityVfx(false);
        buildButton.transform.localScale = Vector3.zero;

        if (buildMarkerCanvasGroup != null)
            buildMarkerCanvasGroup.alpha = 0f;

        SetBuildMarkerRaycastState(false);
        buildButton.gameObject.SetActive(false);
    }

    private void RefreshBuildButtonAvailabilityVfx()
    {
        KingdomRoom room = GetCurrentRoom();
        BuildSlot nextSlot = GetNextBuildSlot(room);
        bool canShowMarker =
            room != null &&
            nextSlot != null &&
            !IsRoomCompleted(currentRoomIndex) &&
            IsValidBuildSlot(room, nextSlot);

        RefreshBuildButtonAvailabilityVfx(nextSlot, canShowMarker);
    }

    private void RefreshBuildButtonAvailabilityVfx(
        BuildSlot nextSlot,
        bool canShowMarker
    )
    {
        bool hasEnoughGem =
            Data.PlayerData != null &&
            nextSlot != null &&
            Data.PlayerData.CurrentStar >= nextSlot.Cost;

        bool shouldPlay =
            canShowMarker &&
            hasEnoughGem &&
            !isBuilding &&
            !isShowingNextMarker &&
            buildButton != null &&
            buildButton.gameObject.activeInHierarchy;

        SetBuildButtonAvailabilityVfx(shouldPlay);
    }

    private void SetBuildButtonAvailabilityVfx(bool enabled)
    {
        if (buildButtonAvailabilityVfx != null)
            buildButtonAvailabilityVfx.SetAvailable(enabled);
    }

    private void SetBuildMarkerRaycastState(bool enabled)
    {
        if (buildMarkerCanvasGroup == null)
            return;

        buildMarkerCanvasGroup.interactable = enabled;
        buildMarkerCanvasGroup.blocksRaycasts = enabled;
    }

    private void StopMarkerAnimation()
    {
        if (markerSequence.isAlive)
            markerSequence.Stop();

        markerSequence = default;
    }

    private void PlayInsufficientCurrencyFeedback()
    {
        if (buildButton == null || insufficientCurrencyPunchDuration <= 0f ||
            insufficientCurrencyPunchScale <= 0f)
        {
            return;
        }

        if (markerFeedbackTween.isAlive)
            markerFeedbackTween.Stop();

        markerFeedbackTween = Tween.PunchScale(
            buildButton.transform,
            Vector3.one * insufficientCurrencyPunchScale,
            insufficientCurrencyPunchDuration,
            frequency: 8f,
            useUnscaledTime: useUnscaledTime
        );
    }

    private void UpdateBuildMarkerCost(BuildSlot nextSlot)
    {
        if (buildCostText != null)
        {
            ApplyBuildMarkerTextColor();
            buildCostText.text = nextSlot == null ? string.Empty : nextSlot.Cost.ToString();
        }
    }

    private void ApplyBuildMarkerTextColor()
    {
        if (buildCostText == null)
            return;

        buildCostText.color = buildMarkerTextColor;
    }

    private void ResetBuildCostTextScale()
    {
        if (buildCostText == null || !buildCostTextScaleCached)
            return;

        buildCostText.transform.localScale = buildCostTextInitialScale;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private void StopBuildTween()
    {
        RefundPendingResourceIfNeeded();

        if (resourceTransferAnimator != null)
            resourceTransferAnimator.StopTransfer();

        StopMarkerAnimation();

        if (requirementFeedbackSequence.isAlive)
            requirementFeedbackSequence.Stop();

        requirementFeedbackSequence = default;
        ResetBuildCostTextScale();

        if (buildRevealController != null)
            buildRevealController.StopBuild();

        if (markerFeedbackTween.isAlive)
            markerFeedbackTween.Stop();

        markerFeedbackTween = default;
        pendingBuildRoom = null;
        pendingBuildSlot = null;
        pendingBuildSlotCommitted = false;
        pendingResourceSpent = false;
        isShowingNextMarker = false;

        RestoreBuildMarkerState();
    }

    private void RestoreBuildMarkerState()
    {
        ResetBuildCostTextScale();

        if (buildButton == null || !buildButtonScaleCached)
            return;

        buildButton.gameObject.SetActive(true);
        buildButton.transform.localScale = buildButtonInitialScale;

        if (buildMarkerCanvasGroup != null)
            buildMarkerCanvasGroup.alpha = 1f;
    }

    private void RefundPendingResourceIfNeeded()
    {
        if (!pendingResourceSpent || pendingBuildSlotCommitted ||
            pendingBuildSlot == null || Data.PlayerData == null)
        {
            return;
        }

        Data.PlayerData.CurrentStar += pendingBuildSlot.Cost;
        pendingResourceSpent = false;
    }

    private void CacheUiReferences()
    {
        if (resourceHandler == null)
            resourceHandler = GetComponentInChildren<BuildHandler>(true);

        if (resourceTransferAnimator == null)
            resourceTransferAnimator = GetComponent<ResourceTransferAnimator>();

        if (resourceTransferAnimator == null)
            resourceTransferAnimator = gameObject.AddComponent<ResourceTransferAnimator>();

        if (buildRevealController == null)
            buildRevealController = GetComponent<BuildRevealController>();

        if (buildRevealController == null)
            buildRevealController = gameObject.AddComponent<BuildRevealController>();

        if (buildButton == null)
            buildButton = FindNamedComponent<CustomButton>("BuildButton");

        if (buildButton != null)
        {
            BuildButtonAvailabilityVFX buttonAvailabilityVfx =
                buildButton.GetComponent<BuildButtonAvailabilityVFX>();

            if (buttonAvailabilityVfx != null)
                buildButtonAvailabilityVfx = buttonAvailabilityVfx;

            if (buttonAvailabilityVfx == null &&
                (buildButtonAvailabilityVfx == null ||
                 buildButtonAvailabilityVfx.gameObject != buildButton.gameObject))
            {
                // The prefab contains this component, but keep the runtime
                // fallback so older instantiated popup assets also receive
                // the complete, preconfigured effect.
                buildButtonAvailabilityVfx =
                    buildButton.gameObject.AddComponent<BuildButtonAvailabilityVFX>();
            }
        }

        if (buildRevealController != null)
            buildRevealController.UseUnscaledTime = useUnscaledTime;

        if (closeButton == null)
            closeButton = FindNamedComponent<CustomButton>("CloseButton");

        if (roomNameText == null)
            roomNameText = FindNamedComponent<TextMeshProUGUI>("RoomNameText");

        if (buildCostText == null)
            buildCostText = FindNamedComponent<TextMeshProUGUI>("BuildCostText");

        if (buildCostText == null && buildButton != null)
        {
            TextMeshProUGUI[] markerTexts =
                buildButton.GetComponentsInChildren<TextMeshProUGUI>(true);

            if (markerTexts.Length > 0)
                buildCostText = markerTexts[0];
        }

        if (buildCostText != null && !buildCostTextScaleCached)
        {
            buildCostTextInitialScale = buildCostText.transform.localScale;
            buildCostTextScaleCached = true;
        }

        if (progressText == null)
            progressText = FindNamedComponent<TextMeshProUGUI>("ProgressText");

        if (completedState == null)
            completedState = FindNamedGameObject("CompletedState");

        if (buildMarkerCanvasGroup == null && buildButton != null)
        {
            buildMarkerCanvasGroup = buildButton.GetComponent<CanvasGroup>();

            if (buildMarkerCanvasGroup == null)
                buildMarkerCanvasGroup = buildButton.gameObject.AddComponent<CanvasGroup>();
        }

        if (buildButton != null && !buildButtonScaleCached)
        {
            buildButtonInitialScale = buildButton.transform.localScale;
            buildButtonScaleCached = true;
        }

        if (roomScrollRect == null)
            roomScrollRect = FindNamedComponent<ScrollRect>("RoomScrollView");

        if (roomContent == null && roomScrollRect != null)
            roomContent = roomScrollRect.content;

        ConfigureRoomScroll();
    }

    private void ConfigureRoomScroll()
    {
        if (roomScrollRect == null)
            return;

        roomScrollRect.movementType = roomMovementType;
        roomScrollRect.horizontal = allowRoomPanning;
        roomScrollRect.vertical = allowRoomPanning;

        if (!allowRoomPanning)
            roomScrollRect.StopMovement();
    }

    private void BindButtons()
    {
        if (buildButton != null)
        {
            buildButton.Click.RemoveListener(OnClickBuild);
            buildButton.Click.AddListener(OnClickBuild);
        }

        if (closeButton != null)
        {
            closeButton.Click.RemoveListener(OnClickClose);
            closeButton.Click.AddListener(OnClickClose);
        }

        if (roomScrollRect != null)
        {
            roomScrollRect.onValueChanged.RemoveListener(OnRoomScrollChanged);
            roomScrollRect.onValueChanged.AddListener(OnRoomScrollChanged);
        }
    }

    private void OnRoomScrollChanged(Vector2 _)
    {
        SetBuildButtonPosition(GetNextBuildSlot(GetCurrentRoom()));
    }

    private T FindNamedComponent<T>(string objectName) where T : Component
    {
        Transform namedTransform = transform.Find(objectName);

        if (namedTransform == null)
            namedTransform = transform.Find($"Container/{objectName}");

        if (namedTransform != null && namedTransform.TryGetComponent(out T component))
            return component;

        T[] components = GetComponentsInChildren<T>(true);

        foreach (T item in components)
        {
            if (item != null && item.name == objectName)
                return item;
        }

        return null;
    }

    private GameObject FindNamedGameObject(string objectName)
    {
        Transform namedTransform = transform.Find(objectName);

        if (namedTransform == null)
            namedTransform = transform.Find($"Container/{objectName}");

        if (namedTransform != null)
            return namedTransform.gameObject;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == objectName)
                return child.gameObject;
        }

        return null;
    }
}
