using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Avatar picker opened directly from the Home profile button.
/// The selected avatar is only previewed until the blue save button is pressed.
/// </summary>
public sealed class PopupAvatar : Popup
{
    private const int UnlockedAvatarCount = 3;
    private static readonly Color LockedAvatarTint = new Color(
        0.45f,
        0.45f,
        0.45f,
        1f
    );

    [Header("Avatar Selection")]
    [SerializeField] private ProfileConfig profileConfig;
    [SerializeField] private RectTransform avatarContent;
    [SerializeField] private Image avatarPlayer;
    [SerializeField] private GameObject greySaveButton;
    [SerializeField] private GameObject activeSaveButton;

    [Header("Player Name")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private GameObject changeNamePanel;

    private readonly List<Transform> avatarItems = new List<Transform>();
    private readonly Dictionary<Graphic, Color> avatarGraphicColors =
        new Dictionary<Graphic, Color>();

    private CustomButton greySaveButtonComponent;
    private CustomButton activeSaveButtonComponent;
    private CustomButton editNameButtonComponent;
    private ContentSizeFitter avatarContentSizeFitter;
    private int originalAvatarIndex;
    private int selectedAvatarIndex;
    private bool hasPendingSelection;
    private string currentName;
    private bool isEditingName;
    private Coroutine pendingNameCommit;

    protected override void OnEnable()
    {
        base.OnEnable();

        AttachNameEditor();
        Observer.ProfileChanged -= RefreshNameDisplay;
        Observer.ProfileChanged += RefreshNameDisplay;
        RefreshNameDisplay();
    }

    protected override void OnDisable()
    {
        CancelPendingNameCommit();

        if (nameInputField != null)
            nameInputField.onEndEdit.RemoveListener(OnNameInputEndEdit);

        if (editNameButtonComponent != null)
            editNameButtonComponent.Click.RemoveListener(OnClickEditName);

        Observer.ProfileChanged -= RefreshNameDisplay;
        base.OnDisable();
    }

    protected override void OnInstantiate()
    {
        base.OnInstantiate();

        CacheReferences();
        BuildAvatarItems();
        BindSaveButton();
        AttachNameEditor();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();

        CacheReferences();
        BuildAvatarItems();
        AttachNameEditor();
        SetNameEditing(false);
        ResetSelection();
        RefreshNameDisplay();
    }

    private void CacheReferences()
    {
        if (avatarContent == null)
        {
            GridLayoutGroup gridLayoutGroup =
                GetComponentInChildren<GridLayoutGroup>(true);

            if (gridLayoutGroup != null)
            {
                avatarContent = gridLayoutGroup.GetComponent<RectTransform>();
            }
            else
            {
                Transform contentTransform =
                    FindChildByName(transform, "Content");

                avatarContent = contentTransform as RectTransform;
            }
        }

        if (avatarContent != null && avatarContentSizeFitter == null)
        {
            avatarContentSizeFitter =
                avatarContent.GetComponent<ContentSizeFitter>();

            if (avatarContentSizeFitter == null)
            {
                avatarContentSizeFitter =
                    avatarContent.gameObject.AddComponent<ContentSizeFitter>();
            }

            avatarContentSizeFitter.horizontalFit =
                ContentSizeFitter.FitMode.Unconstrained;
            avatarContentSizeFitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
        }

        if (avatarPlayer == null)
        {
            Transform playerTransform = FindChildByName(transform, "AvatarPlayer");
            if (playerTransform != null)
                avatarPlayer = playerTransform.GetComponent<Image>();
        }

        if (greySaveButton == null)
        {
            Transform buttonTransform =
                FindChildByName(transform, "SaveButtonBackground_grey");

            if (buttonTransform != null)
                greySaveButton = buttonTransform.gameObject;
        }

        if (activeSaveButton == null)
        {
            Transform buttonTransform =
                FindChildByName(transform, "SaveButtonBackground");

            if (buttonTransform != null)
                activeSaveButton = buttonTransform.gameObject;
        }

        if (greySaveButtonComponent == null && greySaveButton != null)
            greySaveButtonComponent = greySaveButton.GetComponent<CustomButton>();

        if (activeSaveButtonComponent == null && activeSaveButton != null)
            activeSaveButtonComponent = activeSaveButton.GetComponent<CustomButton>();

        CacheNameReferences();
    }

    private void CacheNameReferences()
    {
        Transform nameTransform =
            FindChildByName(transform, "ProfileText_SaveMessage");

        if (playerNameText == null && nameTransform != null)
            playerNameText = nameTransform.GetComponent<TextMeshProUGUI>();

        if (nameInputField == null && nameTransform != null)
            nameInputField = nameTransform.GetComponent<TMP_InputField>();

        if (nameInputField == null && playerNameText != null)
            nameInputField = playerNameText.GetComponent<TMP_InputField>();

        if (changeNamePanel == null && nameTransform != null)
            changeNamePanel = nameTransform.gameObject;
    }

    private void AttachNameEditor()
    {
        CacheNameReferences();

        if (editNameButtonComponent == null)
        {
            Transform editButtonTransform =
                FindChildByName(transform, "EditNameButton");

            if (editButtonTransform != null)
            {
                editNameButtonComponent =
                    editButtonTransform.GetComponent<CustomButton>();

                if (editNameButtonComponent == null)
                {
                    // The current prefab contains only an Image on this
                    // object. Add the shared button behaviour at runtime so
                    // the name editor does not depend on a missing prefab
                    // UnityEvent.
                    editNameButtonComponent =
                        editButtonTransform.gameObject.AddComponent<CustomButton>();
                }
            }
        }

        if (editNameButtonComponent != null)
        {
            editNameButtonComponent.Click.RemoveListener(OnClickEditName);
            editNameButtonComponent.Click.AddListener(OnClickEditName);
            editNameButtonComponent.Interactable = true;
        }

        if (nameInputField == null)
            return;

        if (nameInputField.textComponent == null && playerNameText != null)
            nameInputField.textComponent = playerNameText;

        if (nameInputField.textViewport == null)
        {
            RectTransform inputRectTransform =
                nameInputField.GetComponent<RectTransform>();

            if (inputRectTransform != null)
                nameInputField.textViewport = inputRectTransform;
        }

        nameInputField.characterLimit = PlayerData.MaxNameLength;
        nameInputField.transition = Selectable.Transition.None;
        nameInputField.onEndEdit.RemoveListener(OnNameInputEndEdit);
        nameInputField.onEndEdit.AddListener(OnNameInputEndEdit);
        SetNameEditing(isEditingName);
    }

    private void RefreshNameDisplay()
    {
        if (Data.PlayerData != null)
            currentName = Data.PlayerData.CurrentName;

        string displayName = PlayerData.GetDisplayName(currentName);

        if (playerNameText != null)
        {
            playerNameText.gameObject.SetActive(true);

            if (!isEditingName)
                playerNameText.text = displayName;

            playerNameText.ForceMeshUpdate();
        }

        if (nameInputField != null && !isEditingName)
            nameInputField.SetTextWithoutNotify(displayName);
    }

    private void SetNameEditing(bool editing)
    {
        isEditingName = editing;

        if (nameInputField != null)
        {
            nameInputField.characterLimit = editing
                ? PlayerData.MaxNameLength
                : 0;
            nameInputField.readOnly = !editing;
            nameInputField.interactable = editing;
        }

        // In the current prefab this is the visible name object itself. It
        // must remain active in both states so the saved name stays visible.
        if (changeNamePanel != null && !changeNamePanel.activeSelf)
            changeNamePanel.SetActive(true);
    }

    private void OnNameInputEndEdit(string unused)
    {
        if (!isEditingName)
            return;

        // TMP can fire EndEdit before the click event of EditNameButton or
        // the blue avatar Save button. Defer one frame so those handlers can
        // commit the current text without immediately reopening edit mode.
        CancelPendingNameCommit();
        pendingNameCommit = StartCoroutine(CommitNameAfterInputEndEdit());
    }

    private IEnumerator CommitNameAfterInputEndEdit()
    {
        yield return null;
        pendingNameCommit = null;

        if (isEditingName)
            CommitNameEdit(true);
    }

    private void CancelPendingNameCommit()
    {
        if (pendingNameCommit == null)
            return;

        StopCoroutine(pendingNameCommit);
        pendingNameCommit = null;
    }

    private bool CommitNameEdit(bool saveData)
    {
        CancelPendingNameCommit();

        if (!isEditingName || nameInputField == null)
            return false;

        string normalizedName =
            PlayerData.NormalizeName(nameInputField.text);

        currentName = normalizedName;
        SetNameEditing(false);

        if (Data.PlayerData != null)
        {
            Data.PlayerData.CurrentName = normalizedName;

            if (saveData)
                Data.SaveData();
        }

        RefreshNameDisplay();
        return true;
    }

    public void OnClickEditName()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);

        if (nameInputField == null)
            return;

        if (isEditingName)
        {
            CommitNameEdit(true);
            return;
        }

        SetNameEditing(true);
        nameInputField.SetTextWithoutNotify(
            Data.PlayerData != null
                ? Data.PlayerData.CurrentName
                : PlayerData.NormalizeName(currentName)
        );
        nameInputField.Select();
        nameInputField.ActivateInputField();
    }

    private void BuildAvatarItems()
    {
        avatarItems.Clear();

        if (avatarContent == null)
        {
            Debug.LogWarning("[PopupAvatar] Avatar Content was not found.");
            return;
        }

        ProfileUnitData avatarData = GetAvatarData();
        if (avatarData == null || avatarData.sprites == null)
        {
            Debug.LogWarning("[PopupAvatar] Avatar sprites are not configured.");
            return;
        }

        int avatarCount = avatarData.sprites.Count;
        if (avatarCount == 0)
            return;

        int itemCount = avatarContent.childCount;
        if (itemCount < avatarCount)
        {
            Debug.LogWarning(
                $"[PopupAvatar] Content has {itemCount} avatar items, " +
                $"but ProfileConfig contains {avatarCount} avatars. " +
                "Add the missing items to PopupAvatar/Content."
            );
        }

        for (int i = 0; i < itemCount; i++)
        {
            RectTransform item = avatarContent.GetChild(i) as RectTransform;

            if (item == null)
                continue;

            CustomButton button = item.GetComponent<CustomButton>();
            if (i >= avatarCount)
            {
                item.gameObject.SetActive(false);

                if (button != null)
                    button.Click.RemoveAllListeners();

                continue;
            }

            item.name = $"AvatarItem_{i:00}";
            item.gameObject.SetActive(true);

            Image avatarImage =
                FindChildByName(item, "Avatar")?.GetComponent<Image>();

            if (avatarImage != null)
            {
                avatarImage.sprite = avatarData.sprites[i];
                avatarImage.SetNativeSize();
            }
            else
            {
                Debug.LogWarning(
                    $"[PopupAvatar] Avatar image is missing on item {i}."
                );
            }

            Transform tickBox = FindChildByName(item, "TickeyBox");

            if (tickBox != null)
                tickBox.gameObject.SetActive(false);
            else
                Debug.LogWarning(
                    $"[PopupAvatar] TickeyBox is missing on item {i}."
                );

            if (button != null)
            {
                button.Click.RemoveAllListeners();

                int selectionIndex = i;
                button.Click.AddListener(
                    () => OnSelectAvatar(selectionIndex)
                );
            }

            avatarItems.Add(item);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(avatarContent);
        UpdateAvatarAvailabilityVisuals();
    }

    private void ResetSelection()
    {
        int avatarCount = GetAvatarCount();

        originalAvatarIndex = Data.PlayerData != null
            ? Data.PlayerData.CurrentIndexAvatar
            : 0;

        if (avatarCount > 0)
        {
            originalAvatarIndex =
                Mathf.Clamp(originalAvatarIndex, 0, avatarCount - 1);
        }
        else
        {
            originalAvatarIndex = 0;
        }

        selectedAvatarIndex = originalAvatarIndex;
        hasPendingSelection = false;

        ApplyAvatarPreview();
        UpdateSelectionTick();
        UpdateSaveButton();
    }

    private void OnSelectAvatar(int avatarIndex)
    {
        int avatarCount = GetAvatarCount();
        if (avatarIndex < 0 || avatarIndex >= avatarCount)
            return;

        SoundController.Instance.PlayFX(SoundName.ClickButton);

        selectedAvatarIndex = avatarIndex;
        hasPendingSelection = selectedAvatarIndex != originalAvatarIndex;

        ApplyAvatarPreview();
        UpdateSelectionTick();
        UpdateSaveButton();
    }

    private void ApplyAvatarPreview()
    {
        if (avatarPlayer == null || profileConfig == null)
            return;

        avatarPlayer.sprite =
            profileConfig.GetSprite(
                ProfileType.Avatar,
                selectedAvatarIndex
            );
    }

    private void UpdateSelectionTick()
    {
        for (int i = 0; i < avatarItems.Count; i++)
        {
            Transform tickBox = FindChildByName(avatarItems[i], "TickeyBox");
            if (tickBox != null)
                tickBox.gameObject.SetActive(i == selectedAvatarIndex);
        }
    }

    private void UpdateAvatarAvailabilityVisuals()
    {
        for (int i = 0; i < avatarItems.Count; i++)
        {
            Transform avatarItem = avatarItems[i];
            bool isUnlocked = IsAvatarUnlocked(i);

            foreach (Graphic graphic in
                     avatarItem.GetComponentsInChildren<Graphic>(true))
            {
                if (IsPartOfSelectionTick(graphic.transform, avatarItem))
                    continue;

                if (!avatarGraphicColors.TryGetValue(
                        graphic,
                        out Color originalColor))
                {
                    originalColor = graphic.color;
                    avatarGraphicColors.Add(graphic, originalColor);
                }

                graphic.color = isUnlocked
                    ? originalColor
                    : new Color(
                        originalColor.r * LockedAvatarTint.r,
                        originalColor.g * LockedAvatarTint.g,
                        originalColor.b * LockedAvatarTint.b,
                        originalColor.a
                    );
            }
        }
    }

    private static bool IsPartOfSelectionTick(
        Transform target,
        Transform avatarItem
    )
    {
        Transform tickBox = FindChildByName(avatarItem, "TickeyBox");
        return tickBox != null && target.IsChildOf(tickBox);
    }

    private static bool IsAvatarUnlocked(int avatarIndex)
    {
        return avatarIndex >= 0 && avatarIndex < UnlockedAvatarCount;
    }

    private void BindSaveButton()
    {
        CacheReferences();

        if (activeSaveButtonComponent == null)
            return;

        activeSaveButtonComponent.Click.RemoveListener(OnClickSave);
        activeSaveButtonComponent.Click.AddListener(OnClickSave);
    }

    private void UpdateSaveButton()
    {
        CacheReferences();

        bool canSave = hasPendingSelection &&
                       Data.PlayerData != null &&
                       IsAvatarUnlocked(selectedAvatarIndex);

        if (greySaveButton != null)
            greySaveButton.SetActive(!canSave);

        if (activeSaveButton != null)
            activeSaveButton.SetActive(canSave);

        if (greySaveButtonComponent != null)
            greySaveButtonComponent.Interactable = false;

        if (activeSaveButtonComponent != null)
        {
            activeSaveButtonComponent.Interactable = canSave;

            activeSaveButtonComponent.Click.RemoveListener(OnClickSave);
            activeSaveButtonComponent.Click.AddListener(OnClickSave);
        }
    }

    public void OnClickSave()
    {
        if (isEditingName)
            CommitNameEdit(true);

        if (!hasPendingSelection || Data.PlayerData == null ||
            !IsAvatarUnlocked(selectedAvatarIndex))
        {
            UpdateSaveButton();
            return;
        }

        PlayMenuBarSound();

        Data.PlayerData.CurrentIndexAvatar = selectedAvatarIndex;
        Data.SaveData();

        originalAvatarIndex = selectedAvatarIndex;
        hasPendingSelection = false;

        CloseAndReturnHome();
    }

    public void OnClose()
    {
        PlayMenuBarSound();

        if (isEditingName)
            CommitNameEdit(true);

        CloseAndReturnHome();
    }

    private void CloseAndReturnHome()
    {
        Hide(PopupAnimation.None);

        if (PopupController.Instance != null)
        {
            PopupController.Instance.Show<PopupHome>(PopupAnimation.None);
        }
    }

    private static void PlayMenuBarSound()
    {
        SoundController.Instance?.PlayFX(SoundName.MenuBar);
    }

    private int GetAvatarCount()
    {
        ProfileUnitData avatarData = GetAvatarData();
        return avatarData != null && avatarData.sprites != null
            ? avatarData.sprites.Count
            : 0;
    }

    private ProfileUnitData GetAvatarData()
    {
        return profileConfig == null
            ? null
            : profileConfig.GetProfileUnitData(ProfileType.Avatar);
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }

        return null;
    }
}
