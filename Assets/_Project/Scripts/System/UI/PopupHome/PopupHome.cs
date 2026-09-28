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
    [SerializeField] private GameObject btnRemoveAds;
    [SerializeField] private RectTransform homebackground;
    [SerializeField] private CustomButton playButton;
    [SerializeField] private CustomButton taskButton;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private HomeAlbumPictureView albumPicture;
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

        AttachClockAnimator();

        Observer.LevelChanged += UpdateLevelText;
        UpdateLevelText(
            Data.PlayerData != null
                ? Data.PlayerData.CurrentLevelIndex
                : 1
        );

        SetHomeInputEnabled(homeViewState == HomeViewState.Home);
        if (albumPicture != null) albumPicture.Refresh();
    }

    private void AttachClockAnimator()
    {
        if (GetComponent<ProfileClockAnimator>() == null)
            gameObject.AddComponent<ProfileClockAnimator>();
    }

    protected override void OnDisable()
    {
        Observer.LevelChanged -= UpdateLevelText;
        ClearKingdomRoomVisual();

        if (homeViewState != HomeViewState.Home)
        {
            homeEntranceAnimator?.RestoreHomeState();
            activeKingdomBuildPopup = null;
            homeViewState = HomeViewState.Home;
        }

        base.OnDisable();
    }

    private void UpdateLevelText(int level)
    {
        if (levelText != null)
        {
            levelText.text = $"Level {Mathf.Max(1, level)}";
        }
    }

    protected override void OnInstantiate()
    {
        base.OnInstantiate();

        homeEntranceAnimator = GetComponent<HomeEntranceAnimator>();

        BindHelpKingButton();
        BindTaskButton();

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

    private void BindHelpKingButton()
    {
        if (helpKingButton == null)
        {
            foreach (CustomButton button in
                     GetComponentsInChildren<CustomButton>(true))
            {
                if (button.name != "Btn_Help King")
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
            return;

        buildPopup.PrepareRoomForHome();
        buildPopup.ApplyBuiltDecorationsToHome(this);
        buildPopup.CopyBuiltRoomDecorToHome(this);
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

        PopupController popupController = PopupController.Instance;

        if (popupController == null)
        {
            Debug.LogWarning("[PopupHome] PopupController is not ready for Task.");
            return;
        }

        if (popupController.Get<PictureCollectionPopup>() == null)
        {
            Debug.LogWarning(
                "[PopupHome] PictureCollectionPopup is not registered in PopupConfig."
            );
            return;
        }

        if (popupController.ShowPictureCollectionFromHome())
            PlayClickSound();
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
        if (Data.PlayerData.CurrentHeart <= 0)
        {
            PopupController.Instance.Show<PopupMoreLife>();
            return;
        }

        PlayClickSound();

        // Keep the existing prefab's OnClickBooster binding; Play now starts the level directly.
        GameManager.Instance.PlayCurrentLevel(usePopupTransition: true);
    }

    public void OnClickDebug()
    {
        PlayClickSound();
        PopupController.Instance.Show<PopupDebug>();
    }

    public void OnClickSetting()
    {
        PlayClickSound();

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
        OpenPopup<PopupShop>();
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
        PlayClickSound();
        PopupController.Instance.Show<ProfilePopup>();
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
