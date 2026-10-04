using System;
using System.Collections;
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
    [SerializeField] private Image progressBuildFill;
    [SerializeField] private GameObject completedState;
    [SerializeField] private CanvasGroup buildMarkerCanvasGroup;
    [Tooltip("Image inside the circular area of BuildButton that shows the next slot's Build Icon.")]
    [SerializeField] private Image buildButtonItemIcon;
    [SerializeField] private Color buildMarkerTextColor = new Color32(155, 74, 71, 255);
    [SerializeField] private BuildRevealController buildRevealController;
    [Tooltip("Looping dust and sparkle shown around BuildButton while the current cost is affordable.")]
    [SerializeField] private BuildButtonAvailabilityVFX buildButtonAvailabilityVfx;
    [Tooltip("Material applied to BuildButton images while the player cannot afford the current slot.")]
    [SerializeField] private Material buildButtonDisableMaterial;

    [Header("Available build icon pulse")]
    [SerializeField] [Min(0f)] private float buildIconPulseMinScale = .65f;
    [SerializeField] [Min(0f)] private float buildIconPulseMaxScale = .8f;
    [SerializeField] [Min(.01f)] private float buildIconPulseHalfDuration = .4f;

    [Header("Build marker intro")]
    [SerializeField] private RectTransform tagBuildButton;
    [SerializeField] private CanvasGroup tagBuildButtonCanvasGroup;
    [SerializeField] [Min(0f)] private float tagBuildIntroDelay;
    [SerializeField] [Min(0f)] private float tagBuildMoveDuration = .15f;
    [SerializeField] [Min(0f)] private float tagBuildFadeDuration = .2f;
    [SerializeField] [Min(0f)] private float tagBuildHiddenOffset = 220f;

    [Header("Room complete popup")]
    [SerializeField] private Transform completionSourceChest;
    [SerializeField] private List<Sprite> completionRewardSprites = new List<Sprite>();
    [SerializeField] [Min(0f)] private float completionSourceChestShrinkDuration = .18f;

    [Header("Resource transfer")]
    [SerializeField] private ResourceTransferAnimator resourceTransferAnimator;
    [Tooltip("The BuildHandler that owns the Gem icon in the HUD. Used as the explicit transfer source.")]
    [SerializeField] private BuildHandler resourceHandler;
    [Tooltip("Gem icon inside BuildButton. Gems are aimed here before the marker is consumed.")]
    [SerializeField] private RectTransform buildButtonGemIcon;
    [Tooltip("Object that punches scale whenever one flying Gem arrives. Assign it manually; leave empty to disable the target scale feedback.")]
    [SerializeField] private Transform arrivalScaleTarget;
    [Tooltip("Micro punch applied to the requirement number when one Gem arrives.")]
    [SerializeField] [Range(1f, 1.1f)] private float requirementTextPunchScale = 1.06f;
    [SerializeField] [Min(0f)] private float requirementTextPunchUpDuration = .03f;
    [SerializeField] [Min(0f)] private float requirementTextPunchDownDuration = .04f;
    [Tooltip("Scale punch applied to the destination Gem icon whenever one flying Gem arrives.")]
    [SerializeField] [Range(1f, 1.3f)] private float arrivalTargetPunchScale = 1.14f;
    [SerializeField] [Min(0f)] private float arrivalTargetPunchUpDuration = .035f;
    [SerializeField] [Min(0f)] private float arrivalTargetPunchDownDuration = .065f;

    [Header("Build Progress Trail")]
    [Tooltip("Particle/Trail prefab spawned after an item finishes building. Leave empty to skip this step.")]
    [SerializeField] private GameObject progressTrailPrefab;
    [Tooltip("Parent used to spawn and move the trail. Assign a transform under the popup Canvas.")]
    [SerializeField] private RectTransform progressTrailRoot;
    [Tooltip("Destination of the trail, normally the Progress bar or its icon.")]
    [SerializeField] private RectTransform progressTrailTarget;
    [SerializeField] [Min(.01f)] private float progressTrailDuration = .45f;
    [Tooltip("Control-point offset in Progress Trail Root local units. Positive X bends the flight to the right; negative X bends it to the left.")]
    [SerializeField] private Vector2 progressTrailCurveOffset = new Vector2(80f, 100f);
    [SerializeField] private bool rotateProgressTrailAlongPath;
    [SerializeField] private float progressTrailRotationOffset;
    [Tooltip("Keeps emitted particles alive briefly after reaching progress. Progress updates immediately on arrival.")]
    [SerializeField] [Min(0f)] private float progressTrailTailDuration = .12f;
    [SerializeField] [Range(.65f, .95f)] private float consumeShrinkScale = .85f;
    [SerializeField] [Range(1f, 1.1f)] private float consumeReboundScale = 1.03f;
    [SerializeField] [Min(0f)] private float consumeShrinkDuration = .08f;
    [SerializeField] [Min(0f)] private float consumeReboundDuration = .06f;
    [Tooltip("Time used to fade and scale the consumed BuildButton smoothly to zero.")]
    [SerializeField] [Min(0f)] private float consumeFadeOutDuration = .12f;
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

    [Header("Completed room view")]
    [SerializeField] [Min(0f)] private float roomViewRevealStartDelay = .15f;
    [SerializeField] [Min(.01f)] private float roomViewRevealDuration = .32f;
    [SerializeField] [Min(0f)] private float roomViewRevealInterval = .08f;

    private int currentRoomIndex = -1;
    private bool isBuilding;
    private bool isShowingNextMarker;
    private bool pendingBuildSlotCommitted;
    private bool pendingResourceSpent;
    private bool resetRoomScrollOnNextRefresh = true;
    private Sequence markerSequence;
    private Sequence requirementFeedbackSequence;
    private Sequence arrivalTargetFeedbackSequence;
    private Tween markerFeedbackTween;
    private Coroutine progressTrailRoutine;
    private GameObject activeProgressTrail;
    private Vector3 buildButtonInitialScale = Vector3.one;
    private bool buildButtonScaleCached;
    private Vector3 buildCostTextInitialScale = Vector3.one;
    private bool buildCostTextScaleCached;
    private Transform arrivalTargetTransform;
    private Vector3 arrivalTargetInitialScale = Vector3.one;
    private BuildSlot pendingBuildSlot;
    private KingdomRoom pendingBuildRoom;
    private bool openedFromHomeView;
    private bool isRoomViewMode;
    private int roomIndexBeforeView = -1;
    private bool restoreRoomIndexAfterView;
    private int roomViewRevealVersion;
    private Sequence roomViewRevealDelaySequence;
    private readonly List<BuildSlot> roomViewRevealSlots =
        new List<BuildSlot>();
    private bool resourceHandlerActiveStateCached;
    private bool resourceHandlerWasActive;
    private Coroutine completionPopupRoutine;
    private Vector3 completionSourceChestInitialScale = Vector3.one;
    private bool completionSourceChestScaleCached;
    private Vector2 tagBuildShownPosition;
    private bool tagBuildPositionCached;
    private Tween tagBuildMoveTween;
    private Tween tagBuildFadeTween;
    private Tween buildIconPulseTween;
    private Vector3 buildIconInitialScale = Vector3.one;
    private bool buildIconScaleCached;
    private readonly List<Image> buildButtonVisualImages = new List<Image>();
    private readonly List<Material> buildButtonNormalMaterials = new List<Material>();

    public int CurrentRoomIndex => currentRoomIndex;

    public KingdomRoom CurrentRoom => GetCurrentRoom();

    public KingdomRoomState CurrentRoomState => GetRoomState(GetCurrentRoom());

    public bool IsOpenedFromHomeView => openedFromHomeView;

    public bool IsRoomViewMode => isRoomViewMode;

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

        StopRoomViewReveal(true);
        StopBuildTween();
        StopTagBuildIntro();
        RestoreTagBuildIntroState();
        StopCompletionPopupRoutine();
        isBuilding = false;
        SetBuildButtonAvailabilityVfx(false);
        resetRoomScrollOnNextRefresh = true;

        if (openedFromHomeView)
        {
            PopupHome homePopup =
                PopupController.Instance?.Get<PopupHome>() as PopupHome;

            homePopup?.NotifyKingdomBuildViewHidden(this);
        }

        if (restoreRoomIndexAfterView)
            currentRoomIndex = roomIndexBeforeView;

        isRoomViewMode = false;
        roomIndexBeforeView = -1;
        restoreRoomIndexAfterView = false;

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
        ApplyRoomModeUi();

        PrepareTagBuildIntro();

        if (isRoomViewMode)
            PrepareRoomViewReveal();
    }

    protected override void AfterShown()
    {
        base.AfterShown();

        if (isRoomViewMode)
        {
            StartRoomViewReveal();
        }
        else
        {
            // BeforeShow is called while the popup is still inactive. At that
            // point BuildButton.activeInHierarchy is false, so the availability
            // effect must be refreshed once the popup has actually entered the
            // hierarchy and is visible.
            RefreshBuildButtonAvailabilityVfx();
            PlayTagBuildIntro();
        }
    }

    public void OpenRoom(int roomIndex)
    {
        if (isBuilding)
            return;

        StopRoomViewReveal(true);
        isRoomViewMode = false;
        roomIndexBeforeView = -1;
        restoreRoomIndexAfterView = false;

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
        {
            RefreshCurrentRoom();
            ApplyRoomModeUi();
        }
    }

    public bool OpenRoomView(int roomIndex)
    {
        if (isBuilding ||
            rooms == null ||
            roomIndex < 0 ||
            roomIndex >= rooms.Count ||
            !CanOpenRoom(roomIndex) ||
            !IsRoomCompleted(roomIndex))
        {
            return false;
        }

        StopRoomViewReveal(true);
        StopBuildTween();

        roomIndexBeforeView = currentRoomIndex;
        restoreRoomIndexAfterView = true;
        isRoomViewMode = true;
        openedFromHomeView = false;
        resetRoomScrollOnNextRefresh = true;
        currentRoomIndex = roomIndex;

        if (isActiveAndEnabled)
        {
            RefreshCurrentRoom();
            ApplyRoomModeUi();
            PrepareRoomViewReveal();
        }

        return true;
    }

    public void OpenCompletedRoomViewFromComplete(int roomIndex)
    {
        if (rooms == null ||
            roomIndex < 0 ||
            roomIndex >= rooms.Count ||
            !IsRoomCompleted(roomIndex))
        {
            return;
        }

        StopRoomViewReveal(true);
        StopBuildTween();

        roomIndexBeforeView = currentRoomIndex;
        restoreRoomIndexAfterView = true;
        isRoomViewMode = true;
        resetRoomScrollOnNextRefresh = true;
        currentRoomIndex = roomIndex;

        RestoreCompletionSourceChestScale();
        RefreshCurrentRoom();
        ApplyRoomModeUi();
        PrepareRoomViewReveal();
        StartRoomViewReveal();
    }

    public bool CanOpenRoom(int roomIndex)
    {
        if (rooms == null || roomIndex < 0 || roomIndex >= rooms.Count)
            return false;

        KingdomRoom room = rooms[roomIndex];

        if (!AreRoomSlotsValid(room))
            return false;

        // Room 1 is available from the beginning. Completed rooms stay
        // viewable for save-data compatibility; every later unfinished room
        // must be explicitly confirmed through PopupUnlockRoom.
        return roomIndex == 0 ||
               IsRoomCompleted(roomIndex) ||
               Data.PlayerData != null &&
               Data.PlayerData.IsKingdomRoomUnlocked(GetRoomId(room));
    }

    public bool TryGetPendingRoomUnlock(
        out int roomIndex,
        out string roomId)
    {
        roomIndex = -1;
        roomId = string.Empty;

        if (rooms == null || Data.PlayerData == null)
            return false;

        for (int i = 1; i < rooms.Count; i++)
        {
            KingdomRoom room = rooms[i];

            if (!CanUnlockRoom(i) ||
                Data.PlayerData.IsKingdomRoomUnlocked(GetRoomId(room)))
            {
                continue;
            }

            roomIndex = i;
            roomId = GetRoomId(room);
            return true;
        }

        return false;
    }

    public bool TryUnlockRoom(string roomId)
    {
        if (rooms == null || Data.PlayerData == null ||
            string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        string safeRoomId = roomId.Trim();

        for (int i = 0; i < rooms.Count; i++)
        {
            KingdomRoom room = rooms[i];

            if (room == null || GetRoomId(room) != safeRoomId)
                continue;

            if (i > 0 && !CanUnlockRoom(i))
                return false;

            Data.PlayerData.SetKingdomRoomUnlocked(safeRoomId);
            Data.SaveData();

            currentRoomIndex = i;
            resetRoomScrollOnNextRefresh = true;

            if (isActiveAndEnabled)
                RefreshCurrentRoom();

            return true;
        }

        Debug.LogWarning(
            $"[PopupKingdomBuild] Cannot unlock missing Room ID '{safeRoomId}'."
        );
        return false;
    }

    public bool IsRoomCompleted(int roomIndex)
    {
        if (rooms == null || roomIndex < 0 || roomIndex >= rooms.Count)
            return false;

        return GetRoomState(rooms[roomIndex]) == KingdomRoomState.Completed;
    }

    public bool TryGetRoomProgress(
        int roomIndex,
        out int builtSlotCount,
        out int totalSlotCount)
    {
        builtSlotCount = 0;
        totalSlotCount = 0;

        if (rooms == null || roomIndex < 0 || roomIndex >= rooms.Count)
            return false;

        KingdomRoom room = rooms[roomIndex];
        List<BuildSlot> slots = room?.BuildSlots;

        if (slots == null || slots.Count == 0)
            return false;

        totalSlotCount = slots.Count;

        foreach (BuildSlot slot in slots)
        {
            if (slot != null && IsSlotCompleted(room, slot))
                builtSlotCount++;
        }

        return true;
    }

    public int GetAffordableBuildCountForCurrentRoom()
    {
        if (Data.PlayerData == null || rooms == null || rooms.Count == 0)
            return 0;

        int roomIndex = currentRoomIndex;
        bool currentRoomCanBeBuilt =
            roomIndex >= 0 &&
            roomIndex < rooms.Count &&
            CanOpenRoom(roomIndex) &&
            !IsRoomCompleted(roomIndex);

        if (!currentRoomCanBeBuilt)
            roomIndex = GetFirstUnlockedIncompleteRoomIndex();

        if (roomIndex < 0 || roomIndex >= rooms.Count)
            return 0;

        KingdomRoom room = rooms[roomIndex];

        if (!AreRoomSlotsValid(room))
            return 0;

        int remainingGem = Mathf.Max(0, Data.PlayerData.CurrentStar);
        int affordableBuildCount = 0;

        foreach (BuildSlot slot in room.BuildSlots)
        {
            if (slot == null || IsSlotCompleted(room, slot))
                continue;

            if (remainingGem < slot.Cost)
                break;

            remainingGem -= slot.Cost;
            affordableBuildCount++;
        }

        return affordableBuildCount;
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
        StopRoomViewReveal(true);
        isRoomViewMode = false;
        roomIndexBeforeView = -1;
        restoreRoomIndexAfterView = false;
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
        if (isRoomViewMode || isBuilding || isShowingNextMarker)
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

        UpdateBuildMarkerContent(nextSlot);

        if (progressText != null)
            progressText.text = $"{builtSlotCount}/{totalSlotCount}";

        if (progressBuildFill != null)
        {
            progressBuildFill.type = Image.Type.Filled;
            progressBuildFill.fillMethod = Image.FillMethod.Horizontal;
            progressBuildFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressBuildFill.fillAmount = totalSlotCount > 0
                ? Mathf.Clamp01((float)builtSlotCount / totalSlotCount)
                : 0f;
        }

        if (completedState != null)
            completedState.SetActive(isRoomCompleted && !isRoomViewMode);

        if (buildButton != null)
        {
            SetBuildButtonPosition(nextSlot);

            bool canShowMarker =
                !isRoomViewMode &&
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

    private void ApplyRoomModeUi()
    {
        if (resourceHandler != null)
        {
            bool showResourceHandler =
                !isRoomViewMode && resourceHandlerWasActive;
            resourceHandler.gameObject.SetActive(showResourceHandler);
        }

        if (!isRoomViewMode)
            return;

        HideBuildMarkerInstant();

        if (completedState != null)
            completedState.SetActive(false);
    }

    private void PrepareRoomViewReveal()
    {
        StopRoomViewReveal(true);
        roomViewRevealSlots.Clear();

        KingdomRoom room = GetCurrentRoom();

        if (!isRoomViewMode || room?.BuildSlots == null)
            return;

        foreach (BuildSlot slot in room.BuildSlots)
        {
            if (slot?.TargetImage == null ||
                slot.Sprite == null ||
                !IsSlotCompleted(room, slot))
            {
                continue;
            }

            Image targetImage = slot.TargetImage;
            targetImage.sprite = slot.Sprite;
            targetImage.enabled = false;
            SetImageAlpha(targetImage, 1f);
            targetImage.rectTransform.localScale = Vector3.one;
            roomViewRevealSlots.Add(slot);
        }
    }

    private void StartRoomViewReveal()
    {
        if (!isRoomViewMode || roomViewRevealSlots.Count == 0)
            return;

        int revealVersion = ++roomViewRevealVersion;
        ScheduleRoomViewReveal(
            0,
            Mathf.Max(0f, roomViewRevealStartDelay),
            revealVersion
        );
    }

    private void ScheduleRoomViewReveal(
        int slotIndex,
        float delay,
        int revealVersion)
    {
        if (!CanContinueRoomViewReveal(slotIndex, revealVersion))
            return;

        if (delay <= 0f)
        {
            PlayRoomViewReveal(slotIndex, revealVersion);
            return;
        }

        if (roomViewRevealDelaySequence.isAlive)
            roomViewRevealDelaySequence.Stop();

        roomViewRevealDelaySequence = Sequence.Create(
            useUnscaledTime: useUnscaledTime
        )
            .ChainDelay(delay)
            .ChainCallback(() =>
            {
                roomViewRevealDelaySequence = default;
                PlayRoomViewReveal(slotIndex, revealVersion);
            });
    }

    private void PlayRoomViewReveal(int slotIndex, int revealVersion)
    {
        if (!CanContinueRoomViewReveal(slotIndex, revealVersion))
            return;

        BuildSlot slot = roomViewRevealSlots[slotIndex];

        if (slot?.TargetImage == null || slot.Sprite == null)
        {
            ScheduleNextRoomViewReveal(slotIndex, revealVersion);
            return;
        }

        Image targetImage = slot.TargetImage;
        targetImage.sprite = slot.Sprite;
        targetImage.enabled = true;
        SetImageAlpha(targetImage, 1f);
        targetImage.rectTransform.localScale = Vector3.one;

        if (buildRevealController == null)
        {
            ScheduleNextRoomViewReveal(slotIndex, revealVersion);
            return;
        }

        buildRevealController.PlayBuild(
            targetImage.transform,
            Mathf.Max(.01f, roomViewRevealDuration),
            () => ScheduleNextRoomViewReveal(slotIndex, revealVersion)
        );
    }

    private void ScheduleNextRoomViewReveal(
        int currentSlotIndex,
        int revealVersion)
    {
        if (revealVersion != roomViewRevealVersion || !isRoomViewMode)
            return;

        int nextSlotIndex = currentSlotIndex + 1;

        if (nextSlotIndex >= roomViewRevealSlots.Count)
            return;

        ScheduleRoomViewReveal(
            nextSlotIndex,
            Mathf.Max(0f, roomViewRevealInterval),
            revealVersion
        );
    }

    private bool CanContinueRoomViewReveal(
        int slotIndex,
        int revealVersion)
    {
        return isRoomViewMode &&
               isActiveAndEnabled &&
               revealVersion == roomViewRevealVersion &&
               slotIndex >= 0 &&
               slotIndex < roomViewRevealSlots.Count;
    }

    private void StopRoomViewReveal(bool showAllBuiltSlots)
    {
        roomViewRevealVersion++;

        if (roomViewRevealDelaySequence.isAlive)
            roomViewRevealDelaySequence.Stop();

        roomViewRevealDelaySequence = default;

        if (buildRevealController != null)
            buildRevealController.StopBuild();

        if (showAllBuiltSlots)
        {
            KingdomRoom room = GetCurrentRoom();

            if (room?.BuildSlots != null)
            {
                foreach (BuildSlot slot in room.BuildSlots)
                {
                    if (slot != null && IsSlotCompleted(room, slot))
                        ApplyBuiltVisual(slot);
                }
            }
        }

        roomViewRevealSlots.Clear();
    }

    public void PrepareRoomForHome()
    {
        if (rooms == null || rooms.Count == 0)
            return;

        int buildableRoomIndex = GetFirstUnlockedIncompleteRoomIndex();
        bool currentRoomUnavailable = currentRoomIndex < 0 ||
                                      currentRoomIndex >= rooms.Count ||
                                      !CanOpenRoom(currentRoomIndex);

        if (buildableRoomIndex >= 0 &&
            (currentRoomUnavailable || IsRoomCompleted(currentRoomIndex)))
        {
            currentRoomIndex = buildableRoomIndex;
        }

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

    private int GetFirstUnlockedIncompleteRoomIndex()
    {
        if (rooms == null)
            return -1;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (CanOpenRoom(i) && !IsRoomCompleted(i))
                return i;
        }

        return -1;
    }

    private bool CanUnlockRoom(int roomIndex)
    {
        if (rooms == null || roomIndex <= 0 || roomIndex >= rooms.Count ||
            !AreRoomSlotsValid(rooms[roomIndex]))
        {
            return false;
        }

        return IsRoomCompleted(roomIndex - 1) &&
               !IsRoomCompleted(roomIndex);
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
        StopArrivalTargetFeedback(true);

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
        float safeFadeOutDuration = Mathf.Max(0f, consumeFadeOutDuration);
        float safeAfterConsumeDelay = Mathf.Max(0f, afterConsumeBuildDelay);

        if (safeShrinkDuration <= 0f &&
            safeReboundDuration <= 0f &&
            safeFadeOutDuration <= 0f)
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
                Ease.OutCubic))
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
                Ease.OutCubic))
            .Chain(Tween.Scale(
                markerTransform,
                buildButtonInitialScale * Mathf.Clamp(
                    consumeReboundScale,
                    1f,
                    1.1f
                ),
                Vector3.zero,
                safeFadeOutDuration,
                Ease.InCubic))
            .Group(CreateMarkerFadeTween(0f, safeFadeOutDuration))
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
        PrepareArrivalTargetFeedback(arrivalScaleTarget);
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
        if (!isBuilding)
            return;

        PlayArrivalTargetFeedback();

        if (buildCostText == null ||
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

    private void PrepareArrivalTargetFeedback(Transform target)
    {
        StopArrivalTargetFeedback(true);
        arrivalTargetTransform = target;

        if (arrivalTargetTransform != null)
            arrivalTargetInitialScale = arrivalTargetTransform.localScale;
    }

    private void PlayArrivalTargetFeedback()
    {
        if (arrivalTargetTransform == null ||
            !arrivalTargetTransform.gameObject.activeInHierarchy)
        {
            return;
        }

        if (arrivalTargetFeedbackSequence.isAlive)
            arrivalTargetFeedbackSequence.Stop();

        Transform target = arrivalTargetTransform;
        Vector3 peakScale = arrivalTargetInitialScale * Mathf.Clamp(
            arrivalTargetPunchScale,
            1f,
            1.3f
        );
        float upDuration = Mathf.Max(0f, arrivalTargetPunchUpDuration);
        float downDuration = Mathf.Max(0f, arrivalTargetPunchDownDuration);

        arrivalTargetFeedbackSequence = Sequence.Create(
            useUnscaledTime: useUnscaledTime
        )
            .Group(Tween.Scale(
                target,
                target.localScale,
                peakScale,
                upDuration,
                Ease.OutQuad))
            .Chain(Tween.Scale(
                target,
                peakScale,
                arrivalTargetInitialScale,
                downDuration,
                Ease.InQuad))
            .ChainCallback(() => arrivalTargetFeedbackSequence = default);
    }

    private void StopArrivalTargetFeedback(bool restoreScale)
    {
        if (arrivalTargetFeedbackSequence.isAlive)
            arrivalTargetFeedbackSequence.Stop();

        arrivalTargetFeedbackSequence = default;

        if (restoreScale && arrivalTargetTransform != null)
            arrivalTargetTransform.localScale = arrivalTargetInitialScale;
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

        Ease fadeEase = targetAlpha >= buildMarkerCanvasGroup.alpha
            ? Ease.OutCubic
            : Ease.InCubic;

        return Tween.Alpha(
            buildMarkerCanvasGroup,
            buildMarkerCanvasGroup.alpha,
            targetAlpha,
            duration,
            fadeEase
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
            StartProgressTrailOrFinish();
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
        StartProgressTrailOrFinish();
    }

    private void StartProgressTrailOrFinish()
    {
        StopProgressTrail();

        RectTransform source = pendingBuildSlot?.TargetImage != null
            ? pendingBuildSlot.TargetImage.rectTransform
            : null;

        if (source == null || progressTrailPrefab == null ||
            progressTrailRoot == null || progressTrailTarget == null)
        {
            FinishBuildFlow();
            return;
        }

        progressTrailRoutine = StartCoroutine(
            PlayProgressTrailRoutine(source)
        );
    }

    private IEnumerator PlayProgressTrailRoutine(RectTransform source)
    {
        Transform root = progressTrailRoot;
        Vector3 start = root.InverseTransformPoint(
            source.TransformPoint(source.rect.center)
        );
        Vector3 end = root.InverseTransformPoint(
            progressTrailTarget.TransformPoint(progressTrailTarget.rect.center)
        );
        Vector3 control = (start + end) * .5f +
                          new Vector3(
                              progressTrailCurveOffset.x,
                              progressTrailCurveOffset.y,
                              0f
                          );

        activeProgressTrail = Instantiate(
            progressTrailPrefab,
            progressTrailRoot,
            false
        );

        if (activeProgressTrail == null)
        {
            progressTrailRoutine = null;
            FinishBuildFlow();
            yield break;
        }

        activeProgressTrail.SetActive(true);
        Transform visual = activeProgressTrail.transform;
        Quaternion initialRotation = visual.localRotation;
        visual.localPosition = start;

        float elapsed = 0f;
        float duration = Mathf.Max(.01f, progressTrailDuration);

        while (elapsed < duration && visual != null)
        {
            elapsed += useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float inverse = 1f - eased;
            visual.localPosition = inverse * inverse * start +
                                   2f * inverse * eased * control +
                                   eased * eased * end;

            if (rotateProgressTrailAlongPath)
            {
                Vector3 tangent = 2f * inverse * (control - start) +
                                  2f * eased * (end - control);
                if (tangent.sqrMagnitude > .0001f)
                {
                    float angle = Mathf.Atan2(tangent.y, tangent.x) *
                                  Mathf.Rad2Deg +
                                  progressTrailRotationOffset;
                    visual.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
            else
            {
                visual.localRotation = initialRotation;
            }

            yield return null;
        }

        if (visual != null)
            visual.localPosition = end;

        // The saved slot is already committed, but its progress UI has not been
        // refreshed yet. Update it exactly when the trail reaches the target.
        FinishBuildFlow();

        if (activeProgressTrail != null)
        {
            ParticleSystem[] particles =
                activeProgressTrail.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting
                );
            }
        }

        float tailElapsed = 0f;
        float tailDuration = Mathf.Max(0f, progressTrailTailDuration);
        while (tailElapsed < tailDuration)
        {
            tailElapsed += useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            yield return null;
        }

        if (activeProgressTrail != null)
            Destroy(activeProgressTrail);

        activeProgressTrail = null;
        progressTrailRoutine = null;
    }

    private void StopProgressTrail()
    {
        if (progressTrailRoutine != null)
            StopCoroutine(progressTrailRoutine);

        progressTrailRoutine = null;

        if (activeProgressTrail != null)
            Destroy(activeProgressTrail);

        activeProgressTrail = null;
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

        kingdomPopup?.RefreshKingdomState();
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
            ShowCompletePopupForCurrentRoom();
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

        StopMarkerAnimation();
        SetBuildButtonPosition(nextSlot);
        UpdateBuildMarkerContent(nextSlot);
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

        markerSequence = Sequence.Create(useUnscaledTime: useUnscaledTime)
            .Group(Tween.Scale(
                markerTransform,
                Vector3.zero,
                buildButtonInitialScale,
                safeShowDuration,
                Ease.OutCubic))
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
        UpdateBuildMarkerContent(nextSlot);
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
            !isRoomViewMode &&
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
            !isRoomViewMode &&
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

        ApplyBuildButtonMaterial(enabled);

        if (enabled)
            StartBuildIconPulse();
        else
            StopBuildIconPulse(true);
    }

    private void StartBuildIconPulse()
    {
        if (buildButtonItemIcon == null || buildIconPulseTween.isAlive)
            return;

        float minScale = Mathf.Min(
            buildIconPulseMinScale,
            buildIconPulseMaxScale
        );
        float maxScale = Mathf.Max(
            buildIconPulseMinScale,
            buildIconPulseMaxScale
        );

        Vector3 maxValue = Vector3.one * maxScale;
        Vector3 minValue = Vector3.one * minScale;
        buildButtonItemIcon.transform.localScale = maxValue;

        buildIconPulseTween = Tween.Scale(
            buildButtonItemIcon.transform,
            maxValue,
            minValue,
            Mathf.Max(.01f, buildIconPulseHalfDuration),
            Ease.InOutSine,
            cycles: -1,
            cycleMode: CycleMode.Yoyo,
            useUnscaledTime: useUnscaledTime
        );
    }

    private void StopBuildIconPulse(bool restoreScale)
    {
        if (buildIconPulseTween.isAlive)
            buildIconPulseTween.Stop();

        buildIconPulseTween = default;

        if (restoreScale && buildButtonItemIcon != null &&
            buildIconScaleCached)
        {
            buildButtonItemIcon.transform.localScale = buildIconInitialScale;
        }
    }

    private void CacheBuildButtonVisuals()
    {
        if (buildButton == null || buildButtonVisualImages.Count > 0)
            return;

        foreach (Image image in
                 buildButton.GetComponentsInChildren<Image>(true))
        {
            if (image == null ||
                image.name.StartsWith("BuildButtonUi", StringComparison.Ordinal))
            {
                continue;
            }

            buildButtonVisualImages.Add(image);
            buildButtonNormalMaterials.Add(image.material);
        }
    }

    private void ApplyBuildButtonMaterial(bool available)
    {
        CacheBuildButtonVisuals();

        for (int index = 0; index < buildButtonVisualImages.Count; index++)
        {
            Image image = buildButtonVisualImages[index];

            if (image == null)
                continue;

            image.material = available || buildButtonDisableMaterial == null
                ? buildButtonNormalMaterials[index]
                : buildButtonDisableMaterial;
        }
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

    private void UpdateBuildMarkerContent(BuildSlot nextSlot)
    {
        UpdateBuildMarkerCost(nextSlot);

        if (buildButtonItemIcon == null)
            return;

        Sprite icon = nextSlot?.BuildIcon;
        buildButtonItemIcon.sprite = icon;
        buildButtonItemIcon.enabled = icon != null;
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
        StopProgressTrail();

        if (resourceTransferAnimator != null)
            resourceTransferAnimator.StopTransfer();

        StopMarkerAnimation();

        if (requirementFeedbackSequence.isAlive)
            requirementFeedbackSequence.Stop();

        requirementFeedbackSequence = default;
        ResetBuildCostTextScale();
        StopArrivalTargetFeedback(true);

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

    private void ShowCompletePopupForCurrentRoom()
    {
        if (isRoomViewMode || currentRoomIndex < 0)
            return;

        StopCompletionPopupRoutine();
        completionPopupRoutine = StartCoroutine(
            ShowCompletePopupRoutine(currentRoomIndex)
        );
    }

    private IEnumerator ShowCompletePopupRoutine(int completedRoomIndex)
    {
        if (completionSourceChest != null)
        {
            CacheCompletionSourceChestScale();
            Vector3 fromScale = completionSourceChest.localScale;
            float duration = Mathf.Max(0f, completionSourceChestShrinkDuration);

            if (duration <= 0f)
            {
                completionSourceChest.localScale = Vector3.zero;
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += useUnscaledTime
                        ? Time.unscaledDeltaTime
                        : Time.deltaTime;

                    float progress = Mathf.Clamp01(elapsed / duration);
                    float eased = progress * progress * progress;
                    completionSourceChest.localScale =
                        Vector3.LerpUnclamped(fromScale, Vector3.zero, eased);

                    yield return null;
                }

                completionSourceChest.localScale = Vector3.zero;
            }
        }

        PopupKingdomBuildComplete completePopup =
            PopupController.Instance?.Get<PopupKingdomBuildComplete>() as
                PopupKingdomBuildComplete;

        if (completePopup == null)
        {
            Debug.LogWarning(
                "[PopupKingdomBuild] PopupKingdomBuildComplete is not registered in PopupConfig."
            );
            completionPopupRoutine = null;
            yield break;
        }

        completePopup.Configure(this, completedRoomIndex);
        completePopup.ConfigureRewards(completionRewardSprites);
        PopupController.Instance.Show<PopupKingdomBuildComplete>(
            PopupAnimation.None
        );

        completionPopupRoutine = null;
    }

    private void StopCompletionPopupRoutine()
    {
        if (completionPopupRoutine == null)
            return;

        StopCoroutine(completionPopupRoutine);
        completionPopupRoutine = null;
    }

    private void CacheCompletionSourceChestScale()
    {
        if (completionSourceChest == null || completionSourceChestScaleCached)
            return;

        completionSourceChestInitialScale = completionSourceChest.localScale;
        completionSourceChestScaleCached = true;
    }

    private void RestoreCompletionSourceChestScale()
    {
        if (completionSourceChest == null || !completionSourceChestScaleCached)
            return;

        completionSourceChest.localScale = completionSourceChestInitialScale;
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

    private void PrepareTagBuildIntro()
    {
        StopTagBuildIntro();
        RestoreTagBuildIntroState();

        if (isRoomViewMode || tagBuildButton == null ||
            buildButton == null || !buildButton.gameObject.activeSelf)
        {
            return;
        }

        Vector2 hiddenPosition = tagBuildShownPosition;
        hiddenPosition.x -= Mathf.Max(0f, tagBuildHiddenOffset);
        tagBuildButton.anchoredPosition = hiddenPosition;

        if (tagBuildButtonCanvasGroup != null)
            tagBuildButtonCanvasGroup.alpha = 0f;
    }

    private void PlayTagBuildIntro()
    {
        if (isRoomViewMode || tagBuildButton == null ||
            !tagBuildButton.gameObject.activeInHierarchy)
        {
            return;
        }

        float moveDuration = Mathf.Max(0f, tagBuildMoveDuration);
        float fadeDuration = Mathf.Max(0f, tagBuildFadeDuration);
        float delay = Mathf.Max(0f, tagBuildIntroDelay);
        Vector2 hiddenPosition = tagBuildShownPosition;
        hiddenPosition.x -= Mathf.Max(0f, tagBuildHiddenOffset);

        tagBuildMoveTween = Tween.UIAnchoredPosition(
            tagBuildButton,
            hiddenPosition,
            tagBuildShownPosition,
            moveDuration,
            Ease.OutCubic,
            startDelay: delay,
            useUnscaledTime: useUnscaledTime
        );

        if (tagBuildButtonCanvasGroup != null)
        {
            tagBuildFadeTween = Tween.Alpha(
                tagBuildButtonCanvasGroup,
                0f,
                1f,
                fadeDuration,
                Ease.OutQuad,
                startDelay: delay,
                useUnscaledTime: useUnscaledTime
            );
        }
    }

    private void StopTagBuildIntro()
    {
        tagBuildMoveTween.Stop();
        tagBuildFadeTween.Stop();
    }

    private void RestoreTagBuildIntroState()
    {
        if (tagBuildButton != null && tagBuildPositionCached)
            tagBuildButton.anchoredPosition = tagBuildShownPosition;

        if (tagBuildButtonCanvasGroup != null)
            tagBuildButtonCanvasGroup.alpha = 1f;
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

        if (resourceHandler != null && !resourceHandlerActiveStateCached)
        {
            resourceHandlerWasActive = resourceHandler.gameObject.activeSelf;
            resourceHandlerActiveStateCached = true;
        }

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

        if (tagBuildButton == null)
            tagBuildButton = FindNamedComponent<RectTransform>("Tagbuild_Btn");

        if (tagBuildButton != null)
        {
            if (tagBuildButtonCanvasGroup == null)
            {
                tagBuildButtonCanvasGroup =
                    tagBuildButton.GetComponent<CanvasGroup>();

                if (tagBuildButtonCanvasGroup == null)
                {
                    tagBuildButtonCanvasGroup =
                        tagBuildButton.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (!tagBuildPositionCached)
            {
                tagBuildShownPosition = tagBuildButton.anchoredPosition;
                tagBuildPositionCached = true;
            }
        }

        if (buildButtonItemIcon == null)
            buildButtonItemIcon = FindNamedComponent<Image>("Build_Slot_icon");

        if (buildButtonItemIcon != null && !buildIconScaleCached)
        {
            buildIconInitialScale = buildButtonItemIcon.transform.localScale;
            buildIconScaleCached = true;
        }

        CacheBuildButtonVisuals();

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

        if (progressBuildFill == null)
            progressBuildFill = FindNamedComponent<Image>("Progress_build_fill");

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

        if (completionSourceChest == null)
        {
            Transform chest = FindNamedTransform("Chest");
            if (chest != null)
                completionSourceChest = chest;
        }

        CacheCompletionSourceChestScale();

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

    private Transform FindNamedTransform(string objectName)
    {
        GameObject namedGameObject = FindNamedGameObject(objectName);
        return namedGameObject != null ? namedGameObject.transform : null;
    }
}
