using System;
using System.Collections.Generic;
using Coffee.UIExtensions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class PopupHome : Popup
{
    private const string RightButtonGroupPath = "Container/Group_btn_Right";
    private const string LeftButtonGroupPath = "Container/Group_btn_Left";
    private const string HelpKingButtonName = "Btn_Help King";
    private const string RelicHuntButtonName = "Btn_Relic_Hunt";
    private const string HomeSnowObjectName = "HomeSnowFX";

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

    [Header("Home snow")]
    [SerializeField] private GameObject homeSnowPrefab;
    [SerializeField, Min(1f)] private float homeSnowUiScale = 100f;
    [SerializeField, Min(0f)] private float homeSnowEmissionRate = 4f;
    [SerializeField, Min(0f)] private float homeSpecialSnowflakeEmissionRate = 0.15f;
    [SerializeField, Min(0.01f)] private float homeSnowParticleSize = 0.15f;
    [SerializeField] private Material homeSnowflakeMaterial;
    [SerializeField] private Material homeSnowflake2Material;

    private HomeEntranceAnimator homeEntranceAnimator;
    private UIParticle homeSnowParticle;
    

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
        EnsureHomeSnow();
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

    private void EnsureHomeSnow()
    {
        if (homeSnowPrefab == null)
            return;

        Transform existingSnow = transform.Find(HomeSnowObjectName);

        if (existingSnow != null)
        {
            homeSnowParticle = existingSnow.GetComponent<UIParticle>();
            return;
        }

        GameObject snowObject = new GameObject(
            HomeSnowObjectName,
            typeof(RectTransform),
            typeof(UIParticle)
        );

        snowObject.layer = gameObject.layer;

        RectTransform snowRect = snowObject.GetComponent<RectTransform>();
        snowRect.SetParent(transform, false);
        snowRect.anchorMin = Vector2.zero;
        snowRect.anchorMax = Vector2.one;
        snowRect.anchoredPosition = Vector2.zero;
        snowRect.sizeDelta = Vector2.zero;

        int snowSiblingIndex = homebackground != null
            ? homebackground.GetSiblingIndex() + 1
            : 0;
        snowRect.SetSiblingIndex(snowSiblingIndex);

        homeSnowParticle = snowObject.GetComponent<UIParticle>();
        homeSnowParticle.scale = Mathf.Max(1f, homeSnowUiScale);
        homeSnowParticle.autoScalingMode = UIParticle.AutoScalingMode.Transform;
        homeSnowParticle.SetParticleSystemPrefab(homeSnowPrefab);
        AddOccasionalSnowflakeSystems();

        ConfigureHomeSnow(snowRect.rect);
    }

    private void AddOccasionalSnowflakeSystems()
    {
        if (homeSnowParticle == null || homeSnowParticle.particles.Count == 0)
            return;

        ParticleSystem source = homeSnowParticle.particles[0];
        AddOccasionalSnowflakeSystem(
            source,
            homeSnowflakeMaterial,
            "HomeSnowflake"
        );
        AddOccasionalSnowflakeSystem(
            source,
            homeSnowflake2Material,
            "HomeSnowflake2"
        );

        homeSnowParticle.RefreshParticles();
    }

    private void AddOccasionalSnowflakeSystem(
        ParticleSystem source,
        Material material,
        string systemName)
    {
        if (source == null || material == null)
            return;

        GameObject variantObject = Instantiate(
            source.gameObject,
            source.transform.parent,
            false
        );
        variantObject.name = systemName;
        variantObject.layer = gameObject.layer;

        ParticleSystemRenderer variantRenderer =
            variantObject.GetComponent<ParticleSystemRenderer>();

        if (variantRenderer != null)
            variantRenderer.sharedMaterial = material;
    }

    private void ConfigureHomeSnow(Rect homeRect)
    {
        if (homeSnowParticle == null)
            return;

        float uiScale = Mathf.Max(1f, homeSnowUiScale);
        float emitterWidth = homeRect.width / uiScale + 2f;
        float emitterY = homeRect.height * 0.5f / uiScale + 1f;
        float fullScreenTravelDistance = homeRect.height / uiScale + 2f;

        foreach (ParticleSystem snowSystem in homeSnowParticle.particles)
        {
            if (snowSystem == null)
                continue;

            snowSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            snowSystem.gameObject.layer = gameObject.layer;
            snowSystem.transform.localPosition = new Vector3(0f, emitterY, 0f);

            ParticleSystem.MainModule main = snowSystem.main;
            main.prewarm = true;

            float fallSpeed = Mathf.Max(
                0.1f,
                Mathf.Abs(main.startSpeed.constant)
            );
            float requiredLifetime = fullScreenTravelDistance / fallSpeed;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                requiredLifetime + 0.5f,
                requiredLifetime + 1.5f
            );
            main.startSize = new ParticleSystem.MinMaxCurve(
                homeSnowParticleSize * 0.8f,
                homeSnowParticleSize * 1.1f
            );

            ParticleSystem.EmissionModule emission = snowSystem.emission;
            emission.rateOverTime =
                snowSystem.name.StartsWith("HomeSnowflake", StringComparison.Ordinal)
                    ? homeSpecialSnowflakeEmissionRate
                    : homeSnowEmissionRate;

            ParticleSystem.ShapeModule shape = snowSystem.shape;
            Vector3 shapeScale = shape.scale;
            shapeScale.x = Mathf.Max(1f, emitterWidth);
            shape.scale = shapeScale;

            snowSystem.Play(true);
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

        if (PopupController.Instance != null)
            PopupController.Instance.HideAll();
        SceneManager.LoadScene("CookingBlastGameplay");
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
        OpenPopup<PopupSpecials>();
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
