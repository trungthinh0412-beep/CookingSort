using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProfilePopup : Popup
{
    [Header("Name (Display Only)")]
    [SerializeField] private TextMeshProUGUI txtPlayerName;

    [Header("Level")]
    [SerializeField] private TextMeshProUGUI levelText;

    // Kept serialized for existing prefab data. Name editing is intentionally
    // disabled here; PopupAvatar is the single owner of name changes.
    [Header("Legacy Name References (Read Only)")]
    [SerializeField] private GameObject changeNamePanel;
    [SerializeField] private TMP_InputField nameInputField;

    [Header("Display")]
    [SerializeField] private DisplayProfileUI displayProfileUI;

    [Header("Avatar")]
    [SerializeField] private PopupAvatar avatarPopupPrefab;

    private string _currentName;
    private Coroutine _initialProfileRefresh;
    private PopupAvatar _avatarPopup;

    protected override void OnEnable()
    {
        base.OnEnable();
        AttachAvatarButton();
        DisableProfileNameEditor();
        Observer.LevelChanged += UpdateLevelText;
        Observer.ProfileChanged += RefreshProfileDisplay;
        UpdateLevelText(
            Data.PlayerData != null
                ? Data.PlayerData.CurrentLevelIndex
                : 1
        );

        _initialProfileRefresh = StartCoroutine(RefreshProfileWhenReady());
    }

    protected override void OnDisable()
    {
        if (_initialProfileRefresh != null)
        {
            StopCoroutine(_initialProfileRefresh);
            _initialProfileRefresh = null;
        }

        Observer.LevelChanged -= UpdateLevelText;
        Observer.ProfileChanged -= RefreshProfileDisplay;
        DisableProfileNameEditor();
        base.OnDisable();
    }

    private void Awake()
    {
        if (GetComponent<ProfileClockAnimator>() == null)
            gameObject.AddComponent<ProfileClockAnimator>();

        if (GetComponent<ProfileScrollViewSystem>() == null)
            gameObject.AddComponent<ProfileScrollViewSystem>();

        AttachRoyalFrameShine();
        AttachAvatarButton();
        DisableProfileNameEditor();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();

        DisableProfileNameEditor();
        RefreshProfileDisplay();
    }

    private void UpdateLevelText(int level)
    {
        if (levelText != null)
        {
            levelText.text = PlayerData.FormatLevel(level);
        }
    }

    private void UpdateNameDisplay()
    {
        string displayName = PlayerData.NormalizeName(_currentName);

        // Keep the legacy input's internal value in sync for prefabs that
        // still contain it, but never allow it to receive user input here.
        if (nameInputField != null)
            nameInputField.SetTextWithoutNotify(displayName);

        if (txtPlayerName == null)
            return;

        txtPlayerName.gameObject.SetActive(true);
        txtPlayerName.text = displayName;
        txtPlayerName.ForceMeshUpdate();
    }

    private IEnumerator RefreshProfileWhenReady()
    {
        while (Data.PlayerData == null)
            yield return null;

        RefreshProfileDisplay();
        Canvas.ForceUpdateCanvases();
        _initialProfileRefresh = null;
    }

    private void RefreshProfileDisplay()
    {
        if (Data.PlayerData == null)
            return;

        _currentName = Data.PlayerData.CurrentName;
        UpdateNameDisplay();

        if (displayProfileUI != null)
            displayProfileUI.UpdateData();
    }

    private void AttachRoyalFrameShine()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "New_frame_royal")
                continue;

            if (child.GetComponent<ProfileRoyalFrameShine>() == null)
                child.gameObject.AddComponent<ProfileRoyalFrameShine>();
            return;
        }
    }

    private void AttachAvatarButton()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "Btn_avatar")
                continue;

            CustomButton button = child.GetComponent<CustomButton>();
            if (button == null)
                return;

            button.Click.RemoveListener(OnClickAvatar);
            button.Click.AddListener(OnClickAvatar);
            return;
        }

        Debug.LogWarning("[ProfilePopup] Btn_avatar was not found.");
    }

    private void DisableProfileNameEditor()
    {
        if (nameInputField != null)
        {
            nameInputField.readOnly = true;
            nameInputField.interactable = false;
            nameInputField.transition = Selectable.Transition.None;
        }

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "EditNameButton")
                continue;

            CustomButton button = child.GetComponent<CustomButton>();
            if (button != null)
            {
                button.Click.RemoveListener(OnClickEditName);
                button.Interactable = false;
            }

            Graphic[] graphics = child.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }
    }

    public void OnClickAvatar()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);

        if (_avatarPopup == null)
        {
            // PopupAvatar is registered in PopupConfig. Reuse that instance
            // so Profile does not create a second picker with a separate
            // selection state. The prefab remains a fallback for scenes that
            // do not use PopupController.
            if (PopupController.Instance != null)
            {
                _avatarPopup = PopupController.Instance.Get<PopupAvatar>()
                    as PopupAvatar;
            }

            if (_avatarPopup == null && avatarPopupPrefab == null)
            {
                Debug.LogWarning("[ProfilePopup] Avatar popup prefab is not assigned.");
                return;
            }

            if (_avatarPopup == null)
            {
                _avatarPopup = Instantiate(avatarPopupPrefab, transform);
                _avatarPopup.gameObject.SetActive(false);
            }
        }

        // Avatar is instantiated as a child of Profile, not on Home's canvas.
        _avatarPopup.transform.SetAsLastSibling();

        Canvas avatarCanvas = _avatarPopup.Canvas;
        Canvas profileCanvas = Canvas;
        if (avatarCanvas != null && profileCanvas != null)
        {
            // Both popups own an override-sorting Canvas. Without this,
            // Avatar retains its prefab order (0) and is rendered behind
            // the Profile canvas that PopupController has already ordered.
            avatarCanvas.overrideSorting = true;
            avatarCanvas.sortingOrder = profileCanvas.sortingOrder + 1;
        }

        _avatarPopup.Show(PopupAnimation.None);
    }

    // Compatibility methods for old UnityEvent references. They deliberately
    // do not edit or save a name; PopupAvatar owns that workflow now.
    public void OnClickEditName()
    {
        DisableProfileNameEditor();
    }

    public void OnClickConfirmName()
    {
        DisableProfileNameEditor();
        RefreshProfileDisplay();
    }

    public void OnClickCancelName()
    {
        DisableProfileNameEditor();
        RefreshProfileDisplay();
    }

    public void OnClickSave()
    {
        RefreshProfileDisplay();
    }

    public void OnClose()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);

        if (_avatarPopup != null && _avatarPopup.isActiveAndEnabled)
            _avatarPopup.Hide(PopupAnimation.None);

        RefreshProfileDisplay();

        Hide();

        // OnClose is called directly by the X button. Restore Home so the
        // bottom tabs remain usable after ProfilePopup is closed.
        if (PopupController.Instance != null)
        {
            PopupController.Instance.Show<PopupHome>(PopupAnimation.None);
        }
    }
}
