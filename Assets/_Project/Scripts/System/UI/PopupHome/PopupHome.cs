using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class KingdomHomeDecorTarget
{
    [Tooltip("Must match a BuildSlot Home Target ID.")]
    [SerializeField] private string id;
    [SerializeField] private Image targetImage;

    public string Id => id;
    public Image TargetImage => targetImage;
}

public class PopupHome : Popup
{
    private const string RightButtonGroupPath = "Container/Group_btn_Right";
    private const string LeftButtonGroupPath = "Container/Group_btn_Left";
    private const string HelpKingButtonName = "Btn_Help King";
    private const string RelicHuntButtonName = "Btn_Relic_Hunt";

    [SerializeField] private GameObject btnRemoveAds;
    [SerializeField] private GameObject btnBoartRace;
    [SerializeField] private bool hideSideButtonGroups = true;
    [SerializeField] private RectTransform homebackground;
    [SerializeField] private CustomButton playButton;
    [SerializeField] private CustomButton taskButton;
    [SerializeField, Min(1)] private int taskUnlockLevel = 2;
    [SerializeField] private GameObject taskNotification;
    [SerializeField] private TextMeshProUGUI taskNotificationText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private bool showProgressMission;
    [SerializeField] private GameObject progressMission;
    [SerializeField, Min(1)] private int progressMissionUnlockLevel = 12;
    [SerializeField] private CustomButton helpKingButton;
    [Header("Kingdom Decor Targets")]
    [Tooltip("Images in Home that receive decor built in PopupKingdomBuild.")]
    [SerializeField] private List<KingdomHomeDecorTarget> kingdomDecorTargets =
        new List<KingdomHomeDecorTarget>();

    private GameObject kingdomRoomVisual;

    private HomeEntranceAnimator homeEntranceAnimator;
    private PopupKingdomBuild activeKingdomBuildPopup;
    private HomeViewState homeViewState = HomeViewState.Home;

    public RectTransform HomeBackground => homebackground;

    protected override void OnEnable()
    {
        base.OnEnable();
        ApplySideButtonGroupVisibility();

        AttachClockAnimator();
        BindTaskNotification();

        Observer.LevelChanged += UpdateLevelText;
        Observer.StarChangedDone += RefreshTaskNotification;
        UpdateLevelText(
            Data.PlayerData != null
                ? Data.PlayerData.CurrentLevelIndex
                : 1
        );

        RefreshTaskNotification();
        SetHomeInputEnabled(homeViewState == HomeViewState.Home);
    }

    private void ApplySideButtonGroupVisibility()
    {
        SetButtonGroupActive(
            RightButtonGroupPath,
            !hideSideButtonGroups
        );
        SetButtonGroupActive(
            LeftButtonGroupPath,
            !hideSideButtonGroups
        );
    }

    private void SetButtonGroupActive(string groupPath, bool isActive)
    {
        Transform buttonGroup = transform.Find(groupPath);

        if (buttonGroup != null &&
            buttonGroup.gameObject.activeSelf != isActive)
        {
            buttonGroup.gameObject.SetActive(isActive);
        }
    }

    private void AttachClockAnimator()
    {
        if (GetComponent<ProfileClockAnimator>() == null)
            gameObject.AddComponent<ProfileClockAnimator>();
    }

    protected override void OnDisable()
    {
        Observer.LevelChanged -= UpdateLevelText;
        Observer.StarChangedDone -= RefreshTaskNotification;
        ClearKingdomRoomVisual();

        // Home can be hidden while its entrance animation is still moving
        // controls in from outside the screen (for example when GoodJob is
        // dismissed quickly). Always restore the authored positions so the
        // next Show does not reuse a partially animated layout.
        homeEntranceAnimator?.RestoreHomeState();

        if (homeViewState != HomeViewState.Home)
        {
            activeKingdomBuildPopup = null;
            homeViewState = HomeViewState.Home;
        }

        base.OnDisable();
    }

    private void UpdateLevelText(int level)
    {
        int safeLevel = Mathf.Max(1, level);

        if (levelText != null)
        {
            levelText.text = $"Level {safeLevel}";
        }

        ApplyProgressMissionVisibility(safeLevel);
        ApplyTaskButtonVisibility(safeLevel);
    }

    private void ApplyTaskButtonVisibility(int level)
    {
        if (taskButton == null)
        {
            Transform taskButtonTransform =
                transform.Find("Container/Btn_Task");

            if (taskButtonTransform != null)
                taskButton = taskButtonTransform.GetComponent<CustomButton>();
        }

        if (taskButton != null)
        {
            taskButton.gameObject.SetActive(
                level >= Mathf.Max(1, taskUnlockLevel)
            );
        }
    }

    private void ApplyProgressMissionVisibility(int level)
    {
        if (progressMission == null)
        {
            Transform progressTransform =
                transform.Find("Container/Topbar/Progress_Mission") ??
                transform.Find("Container/Topbar/Progress_");

            if (progressTransform != null)
                progressMission = progressTransform.gameObject;
        }

        if (progressMission != null)
        {
            progressMission.SetActive(
                showProgressMission &&
                level >= Mathf.Max(1, progressMissionUnlockLevel)
            );
        }
    }

    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        ApplySideButtonGroupVisibility();

        homeEntranceAnimator = GetComponent<HomeEntranceAnimator>();

        BindHelpKingButton();
        BindTaskButton();
        BindTaskNotification();
        AttachButtonShines(RightButtonGroupPath);
        AttachButtonShines(LeftButtonGroupPath);
        AttachMineIconChop();
        AttachRelicHuntIconBounce();

        if (playButton == null)
        {
            Transform playButtonTransform =
                transform.Find("Container/BtnPlay/Button");

            if (playButtonTransform != null)
                playButton = playButtonTransform.GetComponent<CustomButton>();
        }

        if (playButton == null)
        {
            Debug.LogError(
                "[PopupHome] CustomButton at Container/BtnPlay/Button was not found."
            );
        }
        else
        {
            bool hasPersistentPlayListener = false;

            for (int index = 0;
                 index < playButton.Click.GetPersistentEventCount();
                 index++)
            {
                if (playButton.Click.GetPersistentTarget(index) == this &&
                    playButton.Click.GetPersistentMethodName(index) ==
                    nameof(OnClickBooster))
                {
                    hasPersistentPlayListener = true;
                    break;
                }
            }

            // Runtime fallback in case Prefab Auto Save clears the persistent function.
            if (!hasPersistentPlayListener)
            {
                playButton.Click.RemoveListener(OnClickBooster);
                playButton.Click.AddListener(OnClickBooster);
            }
        }
    }

    private void BindTaskButton()
    {
        if (taskButton == null)
        {
            Transform taskButtonTransform =
                transform.Find("Container/Btn_Task");

            if (taskButtonTransform != null)
                taskButton = taskButtonTransform.GetComponent<CustomButton>();
        }

        if (taskButton == null)
        {
            Debug.LogError(
                "[PopupHome] CustomButton at Container/Btn_Task was not found."
            );
            return;
        }

        bool hasPersistentTaskListener = false;

        for (int index = 0;
             index < taskButton.Click.GetPersistentEventCount();
             index++)
        {
            if (taskButton.Click.GetPersistentTarget(index) == this &&
                taskButton.Click.GetPersistentMethodName(index) ==
                nameof(OnClickTask))
            {
                hasPersistentTaskListener = true;
                break;
            }
        }

        // Keep a runtime fallback for generated/runtime prefab instances.
        taskButton.Click.RemoveListener(OnClickTask);

        if (!hasPersistentTaskListener)
            taskButton.Click.AddListener(OnClickTask);
    }

    private void BindTaskNotification()
    {
        if (taskButton == null)
        {
            Transform taskButtonTransform =
                transform.Find("Container/Btn_Task");

            if (taskButtonTransform != null)
                taskButton = taskButtonTransform.GetComponent<CustomButton>();
        }

        if (taskNotification == null && taskButton != null)
        {
            foreach (Transform child in taskButton.transform)
            {
                if (!string.Equals(
                        child.name,
                        "noti",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                taskNotification = child.gameObject;
                break;
            }
        }

        if (taskNotificationText == null && taskNotification != null)
        {
            taskNotificationText =
                taskNotification.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    public void RefreshTaskNotification()
    {
        BindTaskNotification();

        if (taskNotification == null)
            return;

        PopupKingdomBuild buildPopup =
            PopupController.Instance?.Get<PopupKingdomBuild>() as PopupKingdomBuild;
        int affordableBuildCount = buildPopup != null
            ? buildPopup.GetAffordableBuildCountForCurrentRoom()
            : 0;

        if (taskNotificationText != null)
            taskNotificationText.text = affordableBuildCount.ToString();

        taskNotification.SetActive(affordableBuildCount > 0);
    }

    private void AttachButtonShines(string buttonGroupPath)
    {
        Transform buttonGroup = transform.Find(buttonGroupPath);

        if (buttonGroup == null)
            return;

        foreach (CustomButton button in
                 buttonGroup.GetComponentsInChildren<CustomButton>(true))
        {
            if (IsInsideHelpKingButton(button.transform, buttonGroup))
                continue;

            Image shineTarget = FindButtonShineTarget(button);

            if (shineTarget == null)
                continue;

            if (shineTarget.GetComponent<HomeButtonDiagonalShine>() == null)
                shineTarget.gameObject.AddComponent<HomeButtonDiagonalShine>();
        }
    }

    private void AttachMineIconChop()
    {
        Transform leftButtonGroup = transform.Find(LeftButtonGroupPath);

        if (leftButtonGroup == null)
            return;

        Transform mineButton = leftButtonGroup.Find("Btn_Mine");

        if (mineButton == null)
            return;

        Image mineIcon = FindImageTarget(mineButton);

        if (mineIcon != null &&
            mineIcon.GetComponent<HomeMineIconChop>() == null)
        {
            mineIcon.gameObject.AddComponent<HomeMineIconChop>();
        }
    }

    private Image FindButtonShineTarget(CustomButton button)
    {
        if (button == null)
            return null;

        foreach (Image image in button.GetComponentsInChildren<Image>(true))
        {
            if (image.transform == button.transform)
                continue;

            if (image.name == "Icon" || image.name == "Image")
                return image;
        }

        foreach (Image image in button.GetComponentsInChildren<Image>(true))
        {
            if (image.transform != button.transform)
                return image;
        }

        return null;
    }

    private void AttachRelicHuntIconBounce()
    {
        Transform rightButtonGroup = transform.Find(RightButtonGroupPath);

        if (rightButtonGroup == null)
            return;

        Transform relicButton = rightButtonGroup.Find(RelicHuntButtonName);

        if (relicButton == null)
            return;

        Image shineTarget = FindButtonShineTarget(
            relicButton.GetComponent<CustomButton>()
        );

        if (shineTarget == null)
            shineTarget = FindImageTarget(relicButton);

        if (shineTarget == null)
            return;

        if (shineTarget.GetComponent<HomeRelicHuntIconBounce>() == null)
            shineTarget.gameObject.AddComponent<HomeRelicHuntIconBounce>();
    }

    private Image FindImageTarget(Transform root)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.transform == root)
                continue;

            if (image.name == "Icon" || image.name == "Image")
                return image;
        }

        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.transform != root)
                return image;
        }

        return null;
    }

    private bool IsInsideHelpKingButton(Transform target, Transform stopAt)
    {
        Transform current = target;

        while (current != null && current != stopAt)
        {
            if (current.name == HelpKingButtonName)
                return true;

            current = current.parent;
        }

        return false;
    }

    private void BindHelpKingButton()
    {
        if (helpKingButton == null)
        {
            foreach (CustomButton button in
                     GetComponentsInChildren<CustomButton>(true))
            {
                if (button.name != HelpKingButtonName)
                    continue;

                helpKingButton = button;
                break;
            }
        }

        if (helpKingButton == null)
        {
            Debug.LogError(
                "[PopupHome] Btn_Help King CustomButton was not found."
            );
            return;
        }

        helpKingButton.Click.AddListener(OnClickHelpKing);
    }

    protected override void AfterShown()
    {
        base.AfterShown();

        if (homeViewState == HomeViewState.Home)
            SetHomeInputEnabled(true);

        RefreshKingdomBuildDecorations();

        Data.PlayerData.SavingReward.CheckAndClaim();

        if (btnRemoveAds != null)
        {
            btnRemoveAds.SetActive(!Data.PlayerData.IsRemoveAds);
        }

    }

    public void RefreshKingdomBuildDecorations()
    {
        PopupKingdomBuild buildPopup =
            PopupController.Instance?.Get<PopupKingdomBuild>() as PopupKingdomBuild;

        if (buildPopup == null)
        {
            RefreshTaskNotification();
            return;
        }

        buildPopup.PrepareRoomForHome();
        buildPopup.ApplyBuiltDecorationsToHome(this);
        buildPopup.CopyBuiltRoomDecorToHome(this);
        RefreshTaskNotification();
    }

    public void SetKingdomRoomBackground(Sprite sprite)
    {
        if (homebackground == null || sprite == null)
            return;

        Image homeBackgroundImage = homebackground.GetComponent<Image>();

        if (homeBackgroundImage == null)
            return;

        homeBackgroundImage.sprite = sprite;
        homeBackgroundImage.enabled = true;
    }

    public void SetKingdomRoomDecorVisual(
        RectTransform sourceRoomRoot,
        List<Image> sourceDecorImages)
    {
        ClearKingdomRoomVisual();

        if (homebackground == null || sourceRoomRoot == null)
            return;

        kingdomRoomVisual = new GameObject(
            "KingdomRoomVisual",
            typeof(RectTransform)
        );
        kingdomRoomVisual.transform.SetParent(homebackground, false);

        RectTransform visualRect = kingdomRoomVisual.transform as RectTransform;
        if (visualRect == null)
            return;

        visualRect.anchorMin = new Vector2(.5f, .5f);
        visualRect.anchorMax = new Vector2(.5f, .5f);
        visualRect.pivot = new Vector2(.5f, .5f);
        visualRect.anchoredPosition = Vector2.zero;
        visualRect.localRotation = Quaternion.identity;
        visualRect.localScale = Vector3.one;
        visualRect.sizeDelta = sourceRoomRoot.rect.size;

        Vector2 sourceSize = sourceRoomRoot.rect.size;
        Vector2 homeSize = homebackground.rect.size;

        if (sourceSize.x > 0f && sourceSize.y > 0f &&
            homeSize.x > 0f && homeSize.y > 0f)
        {
            float scale = Mathf.Min(
                homeSize.x / sourceSize.x,
                homeSize.y / sourceSize.y
            );
            visualRect.localScale = Vector3.one * scale;
        }

        if (sourceDecorImages != null)
        {
            foreach (Image sourceImage in sourceDecorImages)
            {
                if (sourceImage == null || !sourceImage.enabled ||
                    sourceImage.sprite == null)
                {
                    continue;
                }

                GameObject decorObject = Instantiate(
                    sourceImage.gameObject,
                    visualRect,
                    false
                );
                decorObject.name = sourceImage.gameObject.name;

                RectTransform decorRect = decorObject.transform as RectTransform;
                if (decorRect != null)
                {
                    decorRect.localPosition = sourceImage.rectTransform.localPosition;
                    decorRect.localRotation = sourceImage.rectTransform.localRotation;
                    decorRect.localScale = sourceImage.rectTransform.localScale;
                }

                foreach (Image image in
                         decorObject.GetComponentsInChildren<Image>(true))
                {
                    image.raycastTarget = false;
                }
            }
        }

        // Keep Home's existing king and table above the synced decor layer.
        visualRect.SetAsFirstSibling();
    }

    private void ClearKingdomRoomVisual()
    {
        if (kingdomRoomVisual == null)
            return;

        Destroy(kingdomRoomVisual);
        kingdomRoomVisual = null;
    }

    public void SetKingdomBuildDecoration(
        string targetId,
        Sprite sprite,
        bool isBuilt)
    {
        if (string.IsNullOrWhiteSpace(targetId) || kingdomDecorTargets == null)
            return;

        string safeTargetId = targetId.Trim();

        foreach (KingdomHomeDecorTarget target in kingdomDecorTargets)
        {
            if (target == null || target.TargetImage == null ||
                string.IsNullOrWhiteSpace(target.Id) ||
                target.Id.Trim() != safeTargetId)
            {
                continue;
            }

            target.TargetImage.sprite = sprite;
            target.TargetImage.enabled = isBuilt && sprite != null;
            target.TargetImage.raycastTarget = false;
            return;
        }
    }

    public void PlayEntranceAnimation()
    {
        if (homeEntranceAnimator == null)
            homeEntranceAnimator = GetComponent<HomeEntranceAnimator>();

        homeEntranceAnimator?.Play();
    }

    public void OnClickTask()
    {
        if (homeViewState != HomeViewState.Home)
            return;

        if (Data.PlayerData == null ||
            Data.PlayerData.CurrentLevelIndex <
            Mathf.Max(1, taskUnlockLevel))
        {
            return;
        }

        PopupController popupController = PopupController.Instance;

        if (popupController == null)
        {
            Debug.LogWarning("[PopupHome] PopupController is not ready for Task.");
            return;
        }

        PopupKingdomBuild buildPopup =
            popupController.Get<PopupKingdomBuild>() as PopupKingdomBuild;

        if (buildPopup == null)
        {
            Debug.LogWarning(
                "[PopupHome] PopupKingdomBuild is not registered in PopupConfig."
            );
            return;
        }

        if (buildPopup.TryGetPendingRoomUnlock(
                out int pendingRoomIndex,
                out string pendingRoomId))
        {
            PopupUnlockRoom unlockPopup =
                popupController.Get<PopupUnlockRoom>() as PopupUnlockRoom;

            if (unlockPopup == null)
            {
                Debug.LogWarning(
                    "[PopupHome] PopupUnlockRoom is not registered in PopupConfig."
                );
                return;
            }

            PlayClickSound();
            unlockPopup.Configure(
                this,
                buildPopup,
                pendingRoomIndex,
                pendingRoomId
            );
            popupController.Show<PopupUnlockRoom>(PopupAnimation.ScaleFade);
            return;
        }

        PlayClickSound();
        BeginEnterKingdomBuildView(buildPopup);
    }

    private void BeginEnterKingdomBuildView(PopupKingdomBuild buildPopup)
    {
        if (homeViewState != HomeViewState.Home || buildPopup == null)
            return;

        homeViewState = HomeViewState.EnteringKingdomBuild;
        activeKingdomBuildPopup = buildPopup;

        if (taskButton != null)
            taskButton.Interactable = false;

        SetHomeInputEnabled(false);

        // Preserve the current room/progress. This only prepares its existing
        // visual state and does not reset or select another room.
        buildPopup.OpenFromHomeView();

        if (homeEntranceAnimator == null)
            homeEntranceAnimator = GetComponent<HomeEntranceAnimator>();

        if (homeEntranceAnimator == null)
        {
            CompleteEnterKingdomBuildView();
            return;
        }

        homeEntranceAnimator.PlayExitToKingdom(
            CompleteEnterKingdomBuildView
        );
    }

    public void ExitKingdomBuildView()
    {
        if (homeViewState != HomeViewState.KingdomBuild)
            return;

        homeViewState = HomeViewState.ExitingKingdomBuild;
        SetHomeInputEnabled(false);

        PopupKingdomBuild buildPopup = activeKingdomBuildPopup;

        if (buildPopup == null)
        {
            buildPopup =
                PopupController.Instance?.Get<PopupKingdomBuild>() as
                PopupKingdomBuild;
        }

        buildPopup?.SetBuildViewInputEnabled(false);

        // Build view is rendered above Home, so hide it before reversing the
        // Home animation and keep the reverse movement fully visible.
        if (buildPopup != null && buildPopup.isActiveAndEnabled)
            buildPopup.Hide(PopupAnimation.None);

        if (homeEntranceAnimator == null)
            homeEntranceAnimator = GetComponent<HomeEntranceAnimator>();

        if (homeEntranceAnimator == null)
        {
            CompleteExitKingdomBuildView();
            return;
        }

        homeEntranceAnimator.PlayReturnFromKingdom(
            CompleteExitKingdomBuildView
        );
    }

    public void SetHomeInputEnabled(bool enabled)
    {
        CanvasGroup homeCanvasGroup = CanvasGroup;

        if (homeCanvasGroup != null)
        {
            homeCanvasGroup.interactable = enabled;
            homeCanvasGroup.blocksRaycasts = enabled;
        }

        if (taskButton != null)
        {
            taskButton.Interactable = enabled &&
                homeViewState == HomeViewState.Home;
        }
    }

    public void NotifyKingdomBuildViewHidden(PopupKingdomBuild buildPopup)
    {
        if (buildPopup == null || activeKingdomBuildPopup != buildPopup)
            return;

        if (homeViewState != HomeViewState.KingdomBuild)
            return;

        homeEntranceAnimator?.RestoreHomeState();
        activeKingdomBuildPopup = null;
        homeViewState = HomeViewState.Home;
        SetHomeInputEnabled(true);
    }

    private void CompleteEnterKingdomBuildView()
    {
        if (homeViewState != HomeViewState.EnteringKingdomBuild)
            return;

        PopupController popupController = PopupController.Instance;
        PopupKingdomBuild buildPopup = activeKingdomBuildPopup;

        if (popupController == null || buildPopup == null)
        {
            homeEntranceAnimator?.RestoreHomeState();
            activeKingdomBuildPopup = null;
            homeViewState = HomeViewState.Home;
            SetHomeInputEnabled(true);
            return;
        }

        popupController.Show<PopupKingdomBuild>(PopupAnimation.None);

        if (!buildPopup.isActiveAndEnabled)
        {
            homeEntranceAnimator?.RestoreHomeState();
            activeKingdomBuildPopup = null;
            homeViewState = HomeViewState.Home;
            SetHomeInputEnabled(true);
            return;
        }

        homeViewState = HomeViewState.KingdomBuild;
        SetHomeInputEnabled(false);
        buildPopup.SetBuildViewInputEnabled(true);
    }

    private void CompleteExitKingdomBuildView()
    {
        if (homeViewState != HomeViewState.ExitingKingdomBuild)
            return;

        homeViewState = HomeViewState.Home;

        PopupController popupController = PopupController.Instance;

        if (popupController != null)
            popupController.Show<PopupHome>(PopupAnimation.None);

        activeKingdomBuildPopup = null;
        SetHomeInputEnabled(true);
    }

    public void OnClickBooster()
    {
        bool hasPlayableHeart = HeartController.Instance != null
            ? HeartController.Instance.HasPlayableHeart
            : Data.PlayerData != null &&
              (Data.PlayerData.IsInfiniteHeart() ||
               Data.PlayerData.CurrentHeart > 0);

        if (!hasPlayableHeart)
        {
            PopupController.Instance.Show<PopupMoreLife>();
            return;
        }

        PlayMenuBarSound();
        
        PopupController.Instance.Show<PopupBooster>(
            PopupAnimation.None
        );
    }

    public void OnClickDebug()
    {
        PlayClickSound();
        PopupController.Instance.Show<PopupDebug>();
    }

    public void OnClickSetting()
    {
        PlayMenuBarSound();

        PopupController.Instance.Show<PopupSetting>(
            PopupAnimation.None
        );
    }

    public void OnClickHelpKing()
    {
        PlayClickSound();

        PopupController.Instance.Show<PopupHelpKing>(
            PopupAnimation.None
        );
    }

    public bool IsBoartRaceActive()
    {
        if (btnBoartRace == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Btn_Boart_Race")
                {
                    btnBoartRace = child.gameObject;
                    break;
                }
            }
        }

        if (btnBoartRace == null)
            return false;

        Transform current = btnBoartRace.transform;
        while (current != null && current != transform)
        {
            if (!current.gameObject.activeSelf)
                return false;
            current = current.parent;
        }

        return true;
    }

    public void OnClickMineEvent()
    {
        PlayClickSound();
        ShowEventPopup<PopupMineEvent>();
    }

    public void OnClickBoartRaceEvent()
    {
        PlayClickSound();
        ShowEventPopup<PopupBoartRace>();
    }

    public void OnClickRelicHuntEvent()
    {
        PlayClickSound();
        ShowEventPopup<PopupRelicHuntEvent>();
    }

    private void ShowEventPopup<T>() where T : Popup
    {
        PopupController popupController = PopupController.Instance;

        if (popupController == null)
            return;

        // The current play session can predate an imported event popup.
        // Re-initializing is safe: PopupController keeps registered instances.
        if (popupController.Get<T>() == null)
            popupController.Initialize();

        popupController.Show<T>(PopupAnimation.None);
    }

    public void OnClickDailyReward()
    {
        PlayClickSound();

        PopupController.Instance.Show<PopupDailyReward>(
            PopupAnimation.ScaleFade
        );
    }

    public void OnClickShop()
    {
        PlayClickSound();
        Observer.Notify?.Invoke("Shop is unavailable.", Vector3.zero);
    }

    public void OnClickLeague()
    {
        PlayClickSound();
        OpenPopup<PopupLeague>();
    }

    // COLLECTION
    public void OnClickCollection()
    {
        PlayClickSound();
        OpenPopup<PopupCollection>();
    }
    public void OnClickKingdom()
    {
        PlayClickSound();
        OpenPopup<PopupKingdom>();
    }

    public void OnClickHome()
    {
        PlayClickSound();
        OpenPopup<PopupHome>();
    }

    public void OnClickStarChest()
    {
        PlayClickSound();

        PopupController.Instance.Show<PopupStarChest>(
            PopupAnimation.ScaleFade
        );
    }

    public void OnClickRemoveAds()
    {
        PlayClickSound();
        OpenPopup<PopupNoAds>();
    }

    public void OnClickProfile()
    {
        PlayMenuBarSound();
        PopupController.Instance.Show<PopupAvatar>();
    }

    private void OpenPopup<T>() where T : Popup
    {
        PopupController.Instance.HideAll();
        PopupController.Instance.Show<T>();
    }

    private void PlayClickSound()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );
    }

    private static void PlayMenuBarSound()
    {
        SoundController.Instance?.PlayFX(
            SoundName.MenuBar
        );
    }
}

public enum HomeTapType
{
    Home,
    Shop,
    Lock
}

public enum HomeViewState
{
    Home,
    EnteringKingdomBuild,
    KingdomBuild,
    ExitingKingdomBuild
}
