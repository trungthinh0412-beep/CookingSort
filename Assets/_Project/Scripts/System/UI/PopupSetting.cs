using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PopupSetting : Popup
{
    [Header("Setting Icons")]
    [SerializeField] private Image musicIcon;
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;

    [SerializeField] private Image soundIcon;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;

    [SerializeField] private Image vibrationIcon;
    [SerializeField] private Sprite vibrationOnSprite;
    [SerializeField] private Sprite vibrationOffSprite;

    [Header("Touch Notification")]
    [SerializeField] private float notificationSlideDuration = 0.2f;
    [SerializeField] private RectTransform notificationToggle;
    [SerializeField] private RectTransform notificationLiner;
    [SerializeField] private GameObject notificationTextOn;
    [SerializeField] private GameObject notificationTextOff;
    [SerializeField] private CanvasGroup notificationSlideOne;
    private RectTransform notificationRect;
    private Vector2 notificationBasePosition;
    private Coroutine notificationSlideRoutine;
    private bool notificationPositionInitialized;

    [SerializeField] private Image timeScaleIcon;
    [SerializeField] private Sprite normalTimeScaleSprite;
    [SerializeField] private Sprite fastTimeScaleSprite;

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureNotificationButton();
        RefreshSettingIcons();
    }

    public void OnClickNotification()
    {
        if (Data.PlayerData == null)
            return;

        PlayClickSound();
        Data.PlayerData.NotificationState = !Data.PlayerData.NotificationState;
        Data.SaveData();
        RefreshSettingIcons();
        AnimateNotificationSlide();
    }

    public void OnClickMusic()
    {
        if (Data.PlayerData == null)
            return;

        PlayClickSound();
        Data.PlayerData.MusicState = !Data.PlayerData.MusicState;
        Data.SaveData();
        RefreshSettingIcons();
    }

    public void OnClickSound()
    {
        if (Data.PlayerData == null)
            return;

        PlayClickSound();
        Data.PlayerData.SoundState = !Data.PlayerData.SoundState;
        Data.SaveData();
        RefreshSettingIcons();
    }

    public void OnClickVibration()
    {
        if (Data.PlayerData == null)
            return;

        PlayClickSound();
        Data.PlayerData.VibrationState = !Data.PlayerData.VibrationState;
        Data.SaveData();
        RefreshSettingIcons();
    }

    public void OnClickTimeScale()
    {
        if (Data.PlayerData == null)
            return;

        PlayClickSound();
        Data.PlayerData.FastGameSpeed = !Data.PlayerData.FastGameSpeed;
        Data.SaveData();
        RefreshSettingIcons();
    }

    public void OnClickBack()
    {
        PlayMenuBarSound();

        // Restore the main popup after the setting popup is fully hidden.
        // This also updates PopupController.currentPopup so BottomTabBar
        // can receive clicks and swipe input again.
        AfterHiddenAction = RestoreHomeAfterClose;
        Hide();
    }

    private void RefreshSettingIcons()
    {
        if (Data.PlayerData == null)
            return;

        SetIconSprite(
            musicIcon,
            Data.PlayerData.MusicState,
            musicOnSprite,
            musicOffSprite
        );

        SetIconSprite(
            soundIcon,
            Data.PlayerData.SoundState,
            soundOnSprite,
            soundOffSprite
        );

        SetIconSprite(
            vibrationIcon,
            Data.PlayerData.VibrationState,
            vibrationOnSprite,
            vibrationOffSprite
        );

        bool notificationOn = Data.PlayerData.NotificationState;
        if (notificationTextOn != null)
            notificationTextOn.SetActive(notificationOn);
        if (notificationTextOff != null)
            notificationTextOff.SetActive(!notificationOn);
        if (notificationSlideOne != null)
            notificationSlideOne.alpha = notificationOn ? 1f : 0f;

        SetIconSprite(
            timeScaleIcon,
            Data.PlayerData.FastGameSpeed,
            fastTimeScaleSprite,
            normalTimeScaleSprite
        );
    }

    private void EnsureNotificationButton()
    {
        if (notificationToggle == null || notificationLiner == null)
        {
            foreach (RectTransform rect in GetComponentsInChildren<RectTransform>(true))
            {
                if (notificationToggle == null && rect.name == "Button_touch_Noti")
                    notificationToggle = rect;
                if (notificationLiner == null && rect.name == "Liner")
                    notificationLiner = rect;
                if (notificationTextOn == null && rect.name.ToLowerInvariant() == "text_on")
                    notificationTextOn = rect.gameObject;
                if (notificationTextOff == null && rect.name.ToLowerInvariant() == "text_off")
                    notificationTextOff = rect.gameObject;
                if (notificationSlideOne == null &&
                    rect.name.ToLowerInvariant() == "setting_barr_touch_slide 1")
                    notificationSlideOne = rect.GetComponent<CanvasGroup>() ??
                                           rect.gameObject.AddComponent<CanvasGroup>();
                // Current prefab names the green ON background Green_tab.
                if (notificationSlideOne == null &&
                    rect.name.ToLowerInvariant() == "green_tab")
                    notificationSlideOne = rect.GetComponent<CanvasGroup>() ??
                                           rect.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (notificationToggle == null)
            return;

        notificationRect = notificationToggle;
        if (!notificationPositionInitialized)
        {
            notificationBasePosition = notificationRect.anchoredPosition;
            notificationPositionInitialized = true;
        }

        Button button = notificationToggle.GetComponent<Button>();
        if (button == null)
            button = notificationToggle.gameObject.AddComponent<Button>();

        button.onClick.RemoveListener(OnClickNotification);
        button.onClick.AddListener(OnClickNotification);
        if (Data.PlayerData != null)
            notificationRect.anchoredPosition = GetNotificationEndpoint(Data.PlayerData.NotificationState);
    }

    private void AnimateNotificationSlide()
    {
        if (notificationRect == null)
            return;

        if (notificationSlideRoutine != null)
            StopCoroutine(notificationSlideRoutine);
        notificationSlideRoutine = StartCoroutine(SlideNotificationRoutine());
    }

    private IEnumerator SlideNotificationRoutine()
    {
        Vector2 start = notificationRect.anchoredPosition;
        Vector2 end = GetNotificationEndpoint(Data.PlayerData != null && Data.PlayerData.NotificationState);
        float duration = Mathf.Max(0.01f, notificationSlideDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            notificationRect.anchoredPosition = Vector2.LerpUnclamped(start, end, t);
            yield return null;
        }
        notificationRect.anchoredPosition = end;
        notificationSlideRoutine = null;
    }

    private Vector2 GetNotificationEndpoint(bool isOn)
    {
        if (notificationToggle == null || notificationLiner == null)
            return notificationBasePosition;

        float linerHalfWidth = notificationLiner.rect.width * 0.5f;
        float toggleHalfWidth = notificationToggle.rect.width * 0.5f;
        // In this UI the left end is the ON state and the right end is OFF.
        float x = notificationLiner.anchoredPosition.x +
                  (isOn ? -1f : 1f) * Mathf.Max(0f, linerHalfWidth - toggleHalfWidth);
        return new Vector2(x, notificationLiner.anchoredPosition.y);
    }

    private static void SetIconSprite(
        Image icon,
        bool isOn,
        Sprite onSprite,
        Sprite offSprite
    )
    {
        if (icon == null)
            return;

        Sprite targetSprite = isOn ? onSprite : offSprite;
        if (targetSprite == null)
            return;

        icon.sprite = targetSprite;
        icon.SetVerticesDirty();
    }

    private static void PlayClickSound()
    {
        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);
    }

    private static void PlayMenuBarSound()
    {
        SoundController.Instance?.PlayFX(SoundName.MenuBar);
    }

    private void RestoreHomeAfterClose()
    {
        if (PopupController.Instance != null)
        {
            PopupController.Instance.Show<PopupHome>(
                PopupAnimation.None
            );
        }
    }
}
