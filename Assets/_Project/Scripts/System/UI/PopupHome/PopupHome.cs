using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


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

    private HomeEntranceAnimator homeEntranceAnimator;
    

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
        SetHomeInputEnabled(true);
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

        homeEntranceAnimator?.RestoreHomeState();

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
                false
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

        int affordableBuildCount = 0;

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

        if (helpKingButton != null)
        {
            helpKingButton.Click.AddListener(OnClickHelpKing);
        }
    }

    protected override void AfterShown()
    {
        base.AfterShown();
        SetHomeInputEnabled(true);

        if (btnRemoveAds != null)
        {
            btnRemoveAds.SetActive(!Data.PlayerData.IsRemoveAds);
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
    }

    public void SetHomeInputEnabled(bool enabled)
    {
        CanvasGroup homeCanvasGroup = CanvasGroup;

        if (homeCanvasGroup != null)
        {
            homeCanvasGroup.interactable = enabled;
            homeCanvasGroup.blocksRaycasts = enabled;
        }
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
            return;
        }
    }

    public void OnClickDebug()
    {
        PopupController.Instance.Show<PopupDebug>();
    }

    public void OnClickSetting()
    {
        PopupController.Instance.Show<PopupSetting>();
    }

    public void OnClickHelpKing()
    {
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
    }

    public void OnClickBoartRaceEvent()
    {
    }

    public void OnClickRelicHuntEvent()
    {
    }

    public void OnClickDailyReward()
    {
        PopupController.Instance.Show<PopupDailyReward>();
    }

    public void OnClickShop()
    {
        Observer.Notify?.Invoke("Shop is unavailable.", Vector3.zero);
    }

    public void OnClickLeague()
    {
        OpenPopup<PopupLeague>();
    }

    public void OnClickCollection()
    {
    }

    public void OnClickKingdom()
    {
    }

    public void OnClickHome()
    {
    }

    private void OpenPopup<T>() where T : Popup
    {
        PopupController.Instance.Show<T>();
    }
}
