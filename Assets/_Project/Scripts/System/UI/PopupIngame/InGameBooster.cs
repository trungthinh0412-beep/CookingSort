using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InGameBoosterItem : MonoBehaviour
{
    private const string LighterIconSpriteName = "Booster_icon_0";
    private const int BoosterUiDimSortingOrder = 31999;
    private const int FocusedBoosterSortingOrder = 32001;
    private const float BoosterUiDimHeight = 330f;
    private const float BoosterUiDimAlpha = 0.8f;

    [SerializeField] private BoosterType boosterType;
    [SerializeField] private TextMeshProUGUI boosterAmountText;
    [SerializeField] private GameObject plusImage;

    [Header("Level Unlock")]
    [SerializeField, Min(1)] private int unlockLevel = 1;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private TextMeshProUGUI unlockLevelText;
    [SerializeField] private GameObject boosterIcon;

    [Header("Selected Booster FX")]
    [SerializeField] private ParticleSystem selectedGoldDustPrefab;
    [SerializeField] private GameObject glowBoosterButton;
    [SerializeField, Range(0f, 15f)] private float selectedIconWobbleAngle = 7f;
    [SerializeField, Min(0.5f)] private float selectedIconWobbleCycleDuration = 2.8f;
    [SerializeField, Min(0.1f)] private float selectedGoldDustScale = 1f;

    private static InGameBoosterItem _lighterItem;
    private static InGameBoosterItem _focusedItem;
    private static readonly Dictionary<CustomButton, bool> DisabledCustomButtons =
        new Dictionary<CustomButton, bool>();
    private static readonly Dictionary<Selectable, bool> LockedSelectables =
        new Dictionary<Selectable, bool>();
    private static Canvas _focusedBoosterCanvas;
    private static bool _focusedCanvasWasAdded;
    private static bool _focusedCanvasOriginalOverrideSorting;
    private static int _focusedCanvasOriginalSortingLayerId;
    private static int _focusedCanvasOriginalSortingOrder;
    private static GameObject _boosterUiDimObject;
    private static Image _boosterUiDimImage;
    private static Coroutine _boosterUiDimFadeRoutine;
    private static InGameBoosterItem _boosterUiDimFadeOwner;

    private Image _boosterIcon;
    private Quaternion _boosterIconBaseRotation = Quaternion.identity;
    private bool _boosterIconRotationCached;
    private ParticleSystem _selectedGoldDust;
    private bool _isSelected;
    private CustomButton _button;
    private EventTrigger _lockClickTrigger;
    private EventTrigger.Entry _lockClickEntry;

    public BoosterType BoosterType => boosterType;

    public Sprite GetDisplaySprite()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null &&
                image.gameObject != gameObject &&
                image.sprite != null)
            {
                return image.sprite;
            }
        }

        return null;
    }

    private void OnEnable()
    {
        Observer.UseBooster += OnBoosterAmountChanged;
        Observer.ActiveBoosterChanged += OnActiveBoosterChanged;
        Observer.LevelChanged += OnLevelChanged;

        if (boosterType == BoosterType.Lighter)
            _lighterItem = this;

        _button = GetComponent<CustomButton>();
        if (_button != null)
        {
            _button.Click.RemoveListener(OnClickBooster);
            _button.Click.AddListener(OnClickBooster);
        }

        ConfigureLockClickTrigger();

        SetGlowBoosterButtonActive(false);
        Setup();
        SyncSelectedState();
    }

    private void OnDisable()
    {
        Observer.UseBooster -= OnBoosterAmountChanged;
        Observer.ActiveBoosterChanged -= OnActiveBoosterChanged;
        Observer.LevelChanged -= OnLevelChanged;

        if (_button != null)
            _button.Click.RemoveListener(OnClickBooster);

        RemoveLockClickTrigger();

        if (_lighterItem == this)
            _lighterItem = null;

        SetFocusLayer(false);
        SetSelected(false);
    }

    private void Update()
    {
        if (!_isSelected)
            return;

        if (_boosterIcon != null)
        {
            float cycleDuration = Mathf.Max(
                0.5f,
                selectedIconWobbleCycleDuration
            );
            float angle = Mathf.Sin(
                Time.unscaledTime * Mathf.PI * 2f / cycleDuration
            ) * selectedIconWobbleAngle;
            _boosterIcon.rectTransform.localRotation =
                _boosterIconBaseRotation * Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void OnBoosterAmountChanged(BoosterType changedType)
    {
        if (changedType == boosterType)
            Setup();
    }

    private void OnLevelChanged(int level)
    {
        Setup();
    }

    private void OnActiveBoosterChanged(BoosterType? activeType)
    {
        SetFocusLayer(activeType == boosterType);

        SetSelected(activeType == boosterType);
    }

    private void SyncSelectedState()
    {
        Level level = GetCurrentLevel();
        BoosterType? activeBooster = level != null
            ? level.ActiveBooster
            : null;

        SetFocusLayer(activeBooster == boosterType);
        SetSelected(activeBooster == boosterType);
    }

    private void SetFocusLayer(bool focused)
    {
        if (focused)
        {
            RestoreUiFocusState();
            _focusedItem = this;
            ApplyUiFocusState();
            return;
        }

        if (_focusedItem != this)
            return;

        RestoreUiFocusState();
        _focusedItem = null;
    }

    private void ApplyUiFocusState()
    {
        Canvas currentCanvas = GetComponentInParent<Canvas>();
        if (currentCanvas == null)
            return;

        Transform focusRoot = transform.parent != null
            ? transform.parent
            : transform;
        Canvas rootCanvas = currentCanvas.rootCanvas != null
            ? currentCanvas.rootCanvas
            : currentCanvas;
        PopupInGame popupInGame = rootCanvas.GetComponent<PopupInGame>();
        if (popupInGame == null)
            popupInGame = rootCanvas.GetComponentInChildren<PopupInGame>(true);
        Transform useBoosterRoot = popupInGame != null
            ? popupInGame.UseBoosterRoot
            : null;

        CustomButton[] customButtons =
            rootCanvas.GetComponentsInChildren<CustomButton>(true);
        for (int i = 0; i < customButtons.Length; i++)
        {
            CustomButton button = customButtons[i];
            if (button == null ||
                IsPartOfFocusUi(button.transform, focusRoot, useBoosterRoot))
                continue;

            DisabledCustomButtons[button] = button.enabled;
            button.enabled = false;
        }

        Selectable[] selectables =
            rootCanvas.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
        {
            Selectable selectable = selectables[i];
            if (selectable == null ||
                IsPartOfFocusUi(selectable.transform, focusRoot, useBoosterRoot))
            {
                continue;
            }

            LockedSelectables[selectable] = selectable.interactable;
            selectable.interactable = false;
        }

        RaiseSelectedBooster(this, rootCanvas);
    }

    public static void SetBoosterFocusDimVisible(
        bool visible,
        float targetAlpha,
        float fadeDuration)
    {
        if (visible && _boosterUiDimObject == null)
        {
            Canvas rootCanvas = _focusedItem != null
                ? _focusedItem.GetComponentInParent<Canvas>()?.rootCanvas
                : null;
            ShowBoosterUiDim(rootCanvas);
        }

        if (_boosterUiDimImage == null)
            return;

        if (_boosterUiDimFadeRoutine != null &&
            _boosterUiDimFadeOwner != null)
        {
            _boosterUiDimFadeOwner.StopCoroutine(_boosterUiDimFadeRoutine);
        }

        _boosterUiDimFadeRoutine = null;
        _boosterUiDimFadeOwner = null;

        float endAlpha = visible
            ? Mathf.Clamp01(targetAlpha)
            : 0f;
        InGameBoosterItem owner = _focusedItem;
        if (owner == null || !owner.isActiveAndEnabled || fadeDuration <= 0f)
        {
            Color immediateColor = _boosterUiDimImage.color;
            immediateColor.a = endAlpha;
            _boosterUiDimImage.color = immediateColor;
            if (!visible)
                DestroyBoosterUiDim();
            return;
        }

        _boosterUiDimFadeOwner = owner;
        _boosterUiDimFadeRoutine = owner.StartCoroutine(
            FadeBoosterUiDim(
                _boosterUiDimObject,
                _boosterUiDimImage,
                endAlpha,
                Mathf.Max(0.01f, fadeDuration),
                visible
            )
        );
    }

    private static void ShowBoosterUiDim(Canvas rootCanvas)
    {
        if (rootCanvas == null)
            return;

        _boosterUiDimObject = new GameObject(
            "BoosterFocusDim_UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        _boosterUiDimObject.layer = rootCanvas.gameObject.layer;

        RectTransform dimRect =
            _boosterUiDimObject.GetComponent<RectTransform>();
        dimRect.SetParent(rootCanvas.transform, false);
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = new Vector2(1f, 0f);
        dimRect.pivot = new Vector2(0.5f, 0f);
        dimRect.anchoredPosition = Vector2.zero;
        dimRect.sizeDelta = new Vector2(0f, BoosterUiDimHeight);

        Canvas dimCanvas = _boosterUiDimObject.GetComponent<Canvas>();
        dimCanvas.overrideSorting = true;
        dimCanvas.sortingLayerID = rootCanvas.sortingLayerID;
        dimCanvas.sortingOrder = BoosterUiDimSortingOrder;

        _boosterUiDimImage = _boosterUiDimObject.GetComponent<Image>();
        _boosterUiDimImage.color = new Color(0f, 0f, 0f, 0f);
        _boosterUiDimImage.raycastTarget = true;

        Button dimButton = _boosterUiDimObject.GetComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(CancelFocusedBooster);
    }

    private static System.Collections.IEnumerator FadeBoosterUiDim(
        GameObject dimObject,
        Image dimImage,
        float targetAlpha,
        float duration,
        bool keepVisible)
    {
        float startAlpha = dimImage != null ? dimImage.color.a : 0f;
        float elapsed = 0f;
        while (elapsed < duration && dimImage != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            Color color = dimImage.color;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, progress);
            dimImage.color = color;
            yield return null;
        }

        if (dimImage != null)
        {
            Color color = dimImage.color;
            color.a = targetAlpha;
            dimImage.color = color;
        }

        if (!keepVisible && dimObject != null)
            Destroy(dimObject);

        if (_boosterUiDimObject == dimObject)
        {
            if (!keepVisible)
            {
                _boosterUiDimObject = null;
                _boosterUiDimImage = null;
            }

            _boosterUiDimFadeRoutine = null;
            _boosterUiDimFadeOwner = null;
        }
    }

    private static void DestroyBoosterUiDim()
    {
        if (_boosterUiDimObject != null)
            Destroy(_boosterUiDimObject);

        _boosterUiDimObject = null;
        _boosterUiDimImage = null;
        _boosterUiDimFadeRoutine = null;
        _boosterUiDimFadeOwner = null;
    }

    private static void CancelFocusedBooster()
    {
        Level level = GetCurrentLevel();
        if (level != null && level.ActiveBooster.HasValue)
            level.CancelActiveBooster();
    }

    private static void RaiseSelectedBooster(
        InGameBoosterItem selectedItem,
        Canvas rootCanvas)
    {
        if (selectedItem == null)
            return;

        Transform focusRoot = selectedItem.transform.parent != null
            ? selectedItem.transform.parent
            : selectedItem.transform;

        _focusedBoosterCanvas = focusRoot.GetComponent<Canvas>();
        _focusedCanvasWasAdded = _focusedBoosterCanvas == null;
        if (_focusedCanvasWasAdded)
            _focusedBoosterCanvas = focusRoot.gameObject.AddComponent<Canvas>();

        _focusedCanvasOriginalOverrideSorting =
            _focusedBoosterCanvas.overrideSorting;
        _focusedCanvasOriginalSortingLayerId =
            _focusedBoosterCanvas.sortingLayerID;
        _focusedCanvasOriginalSortingOrder =
            _focusedBoosterCanvas.sortingOrder;

        _focusedBoosterCanvas.overrideSorting = true;
        _focusedBoosterCanvas.sortingLayerID = rootCanvas.sortingLayerID;
        _focusedBoosterCanvas.sortingOrder = FocusedBoosterSortingOrder;
    }

    private static bool IsPartOfFocusUi(
        Transform candidate,
        Transform focusRoot,
        Transform useBoosterRoot)
    {
        return candidate == focusRoot ||
               candidate.IsChildOf(focusRoot) ||
               useBoosterRoot != null &&
               (candidate == useBoosterRoot ||
                candidate.IsChildOf(useBoosterRoot));
    }

    private static void RestoreUiFocusState()
    {
        if (_focusedBoosterCanvas != null)
        {
            if (_focusedCanvasWasAdded)
            {
                _focusedBoosterCanvas.overrideSorting = false;
                Destroy(_focusedBoosterCanvas);
            }
            else
            {
                _focusedBoosterCanvas.overrideSorting =
                    _focusedCanvasOriginalOverrideSorting;
                _focusedBoosterCanvas.sortingLayerID =
                    _focusedCanvasOriginalSortingLayerId;
                _focusedBoosterCanvas.sortingOrder =
                    _focusedCanvasOriginalSortingOrder;
            }

            _focusedBoosterCanvas = null;
            _focusedCanvasWasAdded = false;
        }

        foreach (KeyValuePair<CustomButton, bool> pair in DisabledCustomButtons)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }

        DisabledCustomButtons.Clear();

        foreach (KeyValuePair<Selectable, bool> pair in LockedSelectables)
        {
            if (pair.Key != null)
                pair.Key.interactable = pair.Value;
        }

        LockedSelectables.Clear();

        SetBoosterFocusDimVisible(
            false,
            BoosterUiDimAlpha,
            0.07f
        );

    }

    private void Setup()
    {
        bool unlocked = IsUnlocked();
        if (lockIcon != null)
            lockIcon.SetActive(!unlocked);

        if (boosterIcon != null)
            boosterIcon.SetActive(unlocked);

        if (unlockLevelText != null)
            unlockLevelText.text = $"Lv{Mathf.Max(1, unlockLevel)}";

        int amount = GetBoosterAmount(boosterType);
        bool hasBooster = amount > 0;
        bool showAmount = unlocked && hasBooster;

        if (boosterAmountText != null)
        {
            boosterAmountText.text = hasBooster ? amount.ToString() : string.Empty;
            GameObject amountRoot = boosterAmountText.transform.parent != null
                ? boosterAmountText.transform.parent.gameObject
                : boosterAmountText.gameObject;
            amountRoot.SetActive(showAmount);
        }

        if (plusImage != null)
            plusImage.SetActive(unlocked && !hasBooster);
    }

    public void OnClickBooster()
    {
        if (!IsUnlocked())
        {
            Observer.Notify?.Invoke(
                $"Unlocks at Level {Mathf.Max(1, unlockLevel)}",
                Vector3.zero
            );
            return;
        }

        Level currentLevel = GetCurrentLevel();

        if (currentLevel != null &&
            currentLevel.InitialBoardDealAnimator != null &&
            currentLevel.InitialBoardDealAnimator.IsBusy)
        {
            return;
        }

        SoundController.Instance.PlayFX(SoundName.ClickButton);

        if (GetBoosterAmount(boosterType) <= 0)
        {
            PopupBuyBooster popup =
                PopupController.Instance.Get<PopupBuyBooster>()
                as PopupBuyBooster;

            if (popup != null)
            {
                popup.Init(boosterType);
                PopupController.Instance.Show<PopupBuyBooster>(
                    PopupAnimation.None
                );
            }

            return;
        }

        bool activated = currentLevel != null &&
                         currentLevel.ActivateBooster(boosterType);

        // The Glow instance is placed directly in this booster's hierarchy.
        // Toggle that exact instance immediately with the button click so its
        // visual state never depends on a delayed observer callback.
        SetGlowBoosterButtonActive(activated);
    }

    public static bool TryGetLighterVisual(
        out Sprite sprite,
        out Vector2 screenPosition)
    {
        sprite = null;
        screenPosition = Vector2.zero;

        if (_lighterItem == null || !_lighterItem.isActiveAndEnabled)
            return false;

        _lighterItem.ResolveBoosterIcon();
        if (_lighterItem._boosterIcon == null ||
            _lighterItem._boosterIcon.sprite == null)
        {
            return false;
        }

        Canvas canvas = _lighterItem._boosterIcon.canvas;
        Camera uiCamera = canvas != null &&
                          canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        sprite = _lighterItem._boosterIcon.sprite;
        screenPosition = RectTransformUtility.WorldToScreenPoint(
            uiCamera,
            _lighterItem._boosterIcon.rectTransform.position
        );
        return true;
    }

    private void SetSelected(bool selected)
    {
        _isSelected = selected;

        ResolveBoosterIcon();
        if (selected)
        {
            EnsureSelectedGoldDust();
            if (_selectedGoldDust != null)
            {
                _selectedGoldDust.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
                _selectedGoldDust.Play(true);
            }

            SetGlowBoosterButtonActive(true);
        }
        else
        {
            if (_selectedGoldDust != null)
            {
                _selectedGoldDust.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }
            SetGlowBoosterButtonActive(false);

            ResetBoosterIconRotation();
        }

    }

    private void EnsureSelectedGoldDust()
    {
        if (_selectedGoldDust != null ||
            selectedGoldDustPrefab == null ||
            _boosterIcon == null)
        {
            return;
        }

        RectTransform iconTransform = _boosterIcon.rectTransform;
        Transform dustParent = iconTransform.parent;
        _selectedGoldDust = Instantiate(selectedGoldDustPrefab, dustParent);
        _selectedGoldDust.name = "SelectedBoosterGoldDust";

        RectTransform dustTransform =
            _selectedGoldDust.transform as RectTransform;
        if (dustTransform != null)
        {
            dustTransform.anchorMin = iconTransform.anchorMin;
            dustTransform.anchorMax = iconTransform.anchorMax;
            dustTransform.pivot = iconTransform.pivot;
            dustTransform.anchoredPosition = iconTransform.anchoredPosition;
            dustTransform.localRotation = Quaternion.identity;
            dustTransform.localScale =
                Vector3.one * Mathf.Max(0.1f, selectedGoldDustScale);
            dustTransform.SetAsFirstSibling();
        }
    }

    private void SetGlowBoosterButtonActive(bool active)
    {
        if (glowBoosterButton == null)
            return;

        glowBoosterButton.SetActive(active);
    }

    private void ResetBoosterIconRotation()
    {
        if (_boosterIcon != null && _boosterIconRotationCached)
        {
            _boosterIcon.rectTransform.localRotation =
                _boosterIconBaseRotation;
        }
    }

    private void ResolveBoosterIcon()
    {
        if (_boosterIcon != null)
            return;

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].gameObject.name == "Image_Icon")
            {
                _boosterIcon = images[i];
                break;
            }
        }

        if (_boosterIcon == null)
        {
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null &&
                    images[i].sprite != null &&
                    images[i].sprite.name == LighterIconSpriteName)
                {
                    _boosterIcon = images[i];
                    break;
                }
            }
        }

        if (_boosterIcon != null)
        {
            _boosterIconBaseRotation =
                _boosterIcon.rectTransform.localRotation;
            _boosterIconRotationCached = true;
        }
    }

    private static int GetBoosterAmount(BoosterType type)
    {
        if (Data.PlayerData == null)
            return 0;

        return type switch
        {
            BoosterType.Shuffle => Data.PlayerData.CurrentShuffle,
            BoosterType.Bomb => Data.PlayerData.CurrentBomb,
            BoosterType.MoreDeal => Data.PlayerData.CurrentMoreDeal,
            BoosterType.MagicMove => Data.PlayerData.CurrentMagicSwap,
            BoosterType.Magnet => Data.PlayerData.CurrentMagnet,
            BoosterType.Lighter => Data.PlayerData.CurrentLighter,
            BoosterType.ExtraTray => Data.PlayerData.CurrentExtraTray,
            BoosterType.FreeMoves => Data.PlayerData.CurrentFreeMoves,
            _ => 0
        };
    }

    private bool IsUnlocked()
    {
        return unlockLevel <= 1 ||
               Data.PlayerData != null &&
               Data.PlayerData.CurrentLevelIndex >= unlockLevel;
    }

    private void ConfigureLockClickTrigger()
    {
        if (lockIcon == null)
            return;

        _lockClickTrigger = lockIcon.GetComponent<EventTrigger>();
        if (_lockClickTrigger == null)
            _lockClickTrigger = lockIcon.AddComponent<EventTrigger>();

        _lockClickTrigger.triggers ??= new List<EventTrigger.Entry>();
        _lockClickEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick,
            callback = new EventTrigger.TriggerEvent()
        };
        _lockClickEntry.callback.AddListener(_ => OnClickBooster());
        _lockClickTrigger.triggers.Add(_lockClickEntry);
    }

    private void RemoveLockClickTrigger()
    {
        if (_lockClickTrigger != null && _lockClickEntry != null)
            _lockClickTrigger.triggers.Remove(_lockClickEntry);

        _lockClickEntry = null;
        _lockClickTrigger = null;
    }

    private static Level GetCurrentLevel()
    {
        return GameManager.Instance != null &&
               GameManager.Instance.levelController != null
            ? GameManager.Instance.levelController.currentLevel
            : null;
    }
}
