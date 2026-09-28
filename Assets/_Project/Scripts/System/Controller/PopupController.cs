using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

using CustomInspector;
using CustomTween;
using Lean.Pool;
using Lean.Touch;

public class PopupController : SingletonDontDestroy<PopupController>
{
    // ============================================================
    // ATTACHMENTS
    // ============================================================

    [Header("Attachments")]
    [SerializeField] private Camera uiCamera;
    [SerializeField] private Transform canvasTransform;

    [SerializeField] private Camera uiCamera2;
    [SerializeField] private Transform canvasTransform2;

    [SerializeField] private BlockUI blockUI;
    [SerializeField] private BlockUI blockUI2;

    [SerializeField] private NotifyTagItem notifyTagItemPrefab;

    [SerializeField] private PopupConfig popupConfig;
    [SerializeField] private GameConfig gameConfig;


    // ============================================================
    // PERSISTENT BOTTOM BAR
    // ============================================================

    [Header("Persistent Bottom Bar")]
    [Tooltip("Sorting Order của BottomBar.")]
    [SerializeField] private int bottomBarSortingOrder = 500;

    [Tooltip("Sorting Order của popup mở đè lên BottomBar.")]
    [SerializeField] private int overlayPopupSortingOrder = 600;


    // ============================================================
    // RUNTIME
    // ============================================================

    [ReadOnly]
    public Popup currentPopup;

    private BottomTabBar bottomBarInstance;
    private MainTabSwipeController mainTabSwipeController;
    private Coroutine pictureCollectionSlide;
    private const float PictureCollectionSlideDuration = .22f;
    private Popup slideFrom, slideTo;
    private Vector2 slideFromPosition, slideToPosition;
    public bool IsPicturePageSliding => pictureCollectionSlide != null;

    private GameObject _currentHighlightObj = null;

    private bool _addedCanvasToCurrent = false;
    private bool _addedRaycasterToCurrent = false;

    private readonly Dictionary<Type, Popup> _dictionary =
        new Dictionary<Type, Popup>();

    private readonly List<Popup> _savingPopups =
        new List<Popup>();


    // ============================================================
    // PUBLIC PROPERTIES
    // ============================================================

    public Transform CanvasTransform
    {
        get => canvasTransform;
        set => canvasTransform = value;
    }

    public Transform CanvasTransform2
    {
        get => canvasTransform2;
        set => canvasTransform2 = value;
    }

    public BottomTabBar BottomBarInstance
    {
        get => bottomBarInstance;
    }


    // ============================================================
    // UNITY
    // ============================================================

    protected void Start()
    {
        Initialize();

        if (blockUI2 != null)
        {
            blockUI2.SetBlockUIState(false);
        }

        if (gameConfig != null &&
            gameConfig.isTesting)
        {
            InitializeDebugConsole();
        }

        Observer.Notify += SpawnNotifyText;
    }


    private void OnDestroy()
    {
        Observer.Notify -= SpawnNotifyText;
    }


    // ============================================================
    // INITIALIZE
    // ============================================================

    public void Initialize()
    {
        InitializePopups();
    }


    // ============================================================
    // INITIALIZE POPUPS
    // ============================================================

    private void InitializePopups()
    {
        if (popupConfig == null)
        {
            Debug.LogError(
                "[PopupController] PopupConfig is null."
            );

            return;
        }

        if (canvasTransform == null)
        {
            Debug.LogError(
                "[PopupController] Canvas Transform is null."
            );

            return;
        }

        int index = 0;

        popupConfig.popups.ForEach(popup =>
        {
            if (popup == null)
                return;

            Popup popupInstance =
                Instantiate(
                    popup,
                    canvasTransform
                );

            popupInstance.gameObject
                .SetActive(false);

            popupInstance.Canvas.sortingOrder =
                index++;

            Type popupType =
                popupInstance.GetType();

            if (_dictionary.ContainsKey(popupType))
            {
                Debug.LogWarning(
                    $"[PopupController] Duplicate popup: {popupType.Name}"
                );

                Destroy(
                    popupInstance.gameObject
                );

                return;
            }

            _dictionary.Add(
                popupType,
                popupInstance
            );
        });
    }


    // ============================================================
    // HOME / PICTURE COLLECTION PAGE SLIDE
    // ============================================================

    public bool ShowPictureCollectionFromHome()
    {
        return TrySlidePictureCollection(Get<PopupHome>(), Get<PictureCollectionPopup>(), false);
    }

    public bool ReturnHomeFromPictureCollection()
    {
        return TrySlidePictureCollection(Get<PictureCollectionPopup>(), Get<PopupHome>(), true);
    }

    public bool ShowPictureAlbum(PictureAlbumData album)
    {
        var target = Get<PictureAlbumPopup>() as PictureAlbumPopup;
        var collection = LevelController.Instance != null ? LevelController.Instance.PictureCollection : null;
        if (IsPicturePageSliding || currentPopup != Get<PictureCollectionPopup>() || target == null ||
            collection == null || album == null || !collection.IsUnlocked(album, Data.PlayerData)) return false;
        target.SelectAlbum(album);
        return TrySlidePictureCollection(currentPopup, target, false);
    }

    public bool ReturnLibraryFromAlbum() => TrySlidePictureCollection(Get<PictureAlbumPopup>(), Get<PictureCollectionPopup>(), true);
    public bool ReturnAlbumFromPreview() => TrySlidePictureCollection(Get<PicturePreviewPopup>(), Get<PictureAlbumPopup>(), true);

    public bool ShowPicturePreview(PictureAlbumData album, PictureLevelEntry entry)
    {
        var target = Get<PicturePreviewPopup>() as PicturePreviewPopup;
        if (IsPicturePageSliding || currentPopup != Get<PictureAlbumPopup>() || target == null || album == null ||
            entry == null || !album.levels.Contains(entry) || !Data.PlayerData.HasCompletedPicture(entry.levelId)) return false;
        target.SelectPicture(album, entry);
        return TrySlidePictureCollection(currentPopup, target, false);
    }

    public void ShowPictureReplayReturn(PictureAlbumData album, PictureLevelEntry entry, string message)
    {
        if (!(Get<PicturePreviewPopup>() is PicturePreviewPopup target)) return;
        HideAll();
        if (Get<PictureAlbumPopup>() is PictureAlbumPopup albumPage) albumPage.SelectAlbum(album);
        target.SelectPicture(album, entry, message);
        Show<PicturePreviewPopup>();
    }

    private void CancelPictureSlide()
    {
        if (pictureCollectionSlide == null) return;
        StopCoroutine(pictureCollectionSlide);
        pictureCollectionSlide = null;
        if (slideFrom != null)
        {
            ((RectTransform)slideFrom.transform).anchoredPosition = slideFromPosition;
            slideFrom.CanvasGroup.interactable = slideFrom.CanvasGroup.blocksRaycasts = true;
            if (slideFrom is PopupHome home) home.SetHomeInputEnabled(true);
        }
        if (slideTo != null)
        {
            ((RectTransform)slideTo.transform).anchoredPosition = slideToPosition;
            slideTo.Hide(PopupAnimation.None);
        }
        slideFrom = slideTo = null;
    }

    private bool TrySlidePictureCollection(Popup from, Popup to, bool returningHome)
    {
        if (pictureCollectionSlide != null || from == null || to == null ||
            currentPopup != from || !from.isActiveAndEnabled ||
            !(from.transform is RectTransform) || !(to.transform is RectTransform) ||
            from.CanvasGroup == null || to.CanvasGroup == null)
            return false;

        pictureCollectionSlide = StartCoroutine(SlidePictureCollection(from, to, returningHome));
        return true;
    }

    private IEnumerator SlidePictureCollection(Popup from, Popup to, bool returningHome)
    {
        var fromRect = (RectTransform)from.transform;
        var toRect = (RectTransform)to.transform;
        var fromBase = fromRect.anchoredPosition;
        var toBase = toRect.anchoredPosition;
        slideFrom = from; slideTo = to;
        slideFromPosition = fromBase; slideToPosition = toBase;
        var canvasRect = canvasTransform as RectTransform;
        var canvas = canvasRect != null ? canvasRect.GetComponentInParent<Canvas>() : null;
        float scale = canvas != null ? Mathf.Max(.001f, canvas.rootCanvas.scaleFactor) : 1f;
        float width = canvasRect != null && canvasRect.rect.width > 0f
            ? canvasRect.rect.width : Screen.width / scale;
        float direction = returningHome ? 1f : -1f;

        if (from is PopupHome home) home.SetHomeInputEnabled(false);
        from.CanvasGroup.interactable = false;
        from.CanvasGroup.blocksRaycasts = false;

        to.Show(PopupAnimation.None);
        if (to is PopupHome returningHomePopup) returningHomePopup.SetHomeInputEnabled(false);
        to.CanvasGroup.interactable = false;
        to.CanvasGroup.blocksRaycasts = false;
        if (from.Canvas != null) from.Canvas.sortingOrder = bottomBarSortingOrder - 1;
        if (to.Canvas != null) to.Canvas.sortingOrder = bottomBarSortingOrder;
        to.transform.SetAsLastSibling();

        toRect.anchoredPosition = toBase - Vector2.right * width * direction;
        float elapsed = 0f;
        while (elapsed < PictureCollectionSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / PictureCollectionSlideDuration));
            fromRect.anchoredPosition = fromBase + Vector2.right * width * direction * progress;
            toRect.anchoredPosition = toBase - Vector2.right * width * direction * (1f - progress);
            yield return null;
        }

        fromRect.anchoredPosition = fromBase;
        toRect.anchoredPosition = toBase;
        from.Hide(PopupAnimation.None);
        if (to.Canvas != null) to.Canvas.sortingOrder = bottomBarSortingOrder - 1;
        currentPopup = to;
        to.CanvasGroup.interactable = true;
        to.CanvasGroup.blocksRaycasts = true;
        if (to is PopupHome shownHome) shownHome.SetHomeInputEnabled(true);
        pictureCollectionSlide = null;
        slideFrom = slideTo = null;
    }

    // ============================================================
    // BOTTOM BAR VISIBILITY (legacy callers)
    // ============================================================

    public void SetBottomBarVisible(bool visible)
    {
        if (bottomBarInstance == null)
            return;

        GameObject go =
            bottomBarInstance.gameObject;

        if (go.activeSelf == visible)
        {
            if (visible)
            {
                go.transform.SetAsLastSibling();
            }

            return;
        }

        go.SetActive(visible);

        if (visible)
        {
            go.transform.SetAsLastSibling();
        }

        Debug.Log(
            $"[PopupController] BottomBar = {(visible ? "ON" : "OFF")}"
        );
    }


    // ============================================================
    // RESET BOTTOM BAR TO HOME
    // ============================================================

    public void ResetBottomBarToHome()
    {
        if (bottomBarInstance == null)
            return;

        bottomBarInstance.ResetToHome();
    }


    // ============================================================
    // 5 MAIN POPUPS
    // ============================================================

    private bool IsMainBottomBarPopup(Type popupType)
    {
        return
            popupType == typeof(PopupShop) ||
            popupType == typeof(PopupLeague) ||
            popupType == typeof(PopupHome) ||
            popupType == typeof(PictureCollectionPopup) ||
            popupType == typeof(PictureAlbumPopup) ||
            popupType == typeof(PicturePreviewPopup) ||
            popupType == typeof(PopupCollection) ||
            popupType == typeof(PopupKingdom);
    }

    public Popup GetMainTabPopup(int index)
    {
        switch (index)
        {
            case 0:
                return Get<PopupShop>();
            case 1:
                return Get<PopupLeague>();
            case 2:
                return Get<PopupHome>();
            case 3:
                return Get<PopupCollection>();
            case 4:
                return Get<PopupKingdom>();
            default:
                return null;
        }
    }

    public int GetMainTabIndex(Popup popup)
    {
        if (popup is PopupShop)
            return 0;

        if (popup is PopupLeague)
            return 1;

        if (popup is PopupHome)
            return 2;

        if (popup is PopupCollection)
            return 3;

        if (popup is PopupKingdom)
            return 4;

        return -1;
    }

    public int GetCurrentMainTabIndex()
    {
        return GetMainTabIndex(currentPopup);
    }

    public bool PrepareMainTabTransition(
        int fromIndex,
        int toIndex,
        out Popup fromPopup,
        out Popup toPopup)
    {
        fromPopup = GetMainTabPopup(fromIndex);
        toPopup = GetMainTabPopup(toIndex);

        if (fromPopup == null ||
            toPopup == null ||
            currentPopup != fromPopup ||
            !fromPopup.isActiveAndEnabled)
        {
            return false;
        }

        SetBottomBarVisible(true);
        PutMainPopupBelowBottomBar(fromPopup);
        PutMainPopupBelowBottomBar(toPopup);

        if (!toPopup.isActiveAndEnabled)
        {
            toPopup.Show(PopupAnimation.None);
        }

        CanvasGroup sourceCanvasGroup =
            fromPopup.CanvasGroup;

        if (sourceCanvasGroup != null)
        {
            sourceCanvasGroup.interactable = false;
            sourceCanvasGroup.blocksRaycasts = false;
        }

        CanvasGroup targetCanvasGroup =
            toPopup.CanvasGroup;

        if (targetCanvasGroup != null)
        {
            targetCanvasGroup.interactable = false;
            targetCanvasGroup.blocksRaycasts = false;
        }

        toPopup.transform.SetAsLastSibling();

        if (bottomBarInstance != null)
        {
            bottomBarInstance.transform.SetAsLastSibling();
        }

        return true;
    }

    public void CompleteMainTabTransition(
        Popup fromPopup,
        Popup toPopup)
    {
        if (fromPopup != null &&
            fromPopup != toPopup &&
            fromPopup.isActiveAndEnabled)
        {
            fromPopup.Hide(PopupAnimation.None);
        }

        if (toPopup == null)
            return;

        CanvasGroup targetCanvasGroup =
            toPopup.CanvasGroup;

        if (targetCanvasGroup != null)
        {
            targetCanvasGroup.interactable = true;
            targetCanvasGroup.blocksRaycasts = true;
        }

        currentPopup = toPopup;
        SetBottomBarVisible(true);
    }

    public void ShowHomeWithEntrance(
        PopupAnimation popupAnimation = PopupAnimation.None)
    {
        Show<PopupHome>(popupAnimation);
        PlayHomeEntranceAnimation();
    }

    public void PlayHomeEntranceAnimation()
    {
        if (Get<PopupHome>() is PopupHome popupHome &&
            popupHome.isActiveAndEnabled)
        {
            popupHome.PlayEntranceAnimation();
        }
    }

    public void CancelMainTabTransition(
        Popup fromPopup,
        Popup toPopup)
    {
        if (toPopup != null &&
            toPopup != fromPopup &&
            toPopup.isActiveAndEnabled)
        {
            toPopup.Hide(PopupAnimation.None);
        }

        if (fromPopup != null)
        {
            CanvasGroup sourceCanvasGroup =
                fromPopup.CanvasGroup;

            if (sourceCanvasGroup != null)
            {
                sourceCanvasGroup.interactable = true;
                sourceCanvasGroup.blocksRaycasts = true;
            }

            currentPopup = fromPopup;
        }

        SetBottomBarVisible(true);
    }


    // ============================================================
    // CHECK ACTIVE MAIN POPUP
    // ============================================================

    private bool HasActiveMainBottomBarPopup()
    {
        foreach (KeyValuePair<Type, Popup> pair
                 in _dictionary)
        {
            Popup popup =
                pair.Value;

            if (popup == null)
                continue;

            if (!popup.isActiveAndEnabled)
                continue;

            if (IsMainBottomBarPopup(pair.Key))
            {
                return true;
            }
        }

        return false;
    }

    // ============================================================
    // POPUP PHỤ NẰM TRÊN BOTTOM BAR
    // ============================================================

    private void PutPopupAboveBottomBar(Popup popup)
    {
        if (popup == null)
            return;

        Canvas popupCanvas =
            popup.Canvas;

        if (popupCanvas == null)
            return;

        popupCanvas.overrideSorting = true;

        popupCanvas.sortingOrder =
            overlayPopupSortingOrder;
    }


    // ============================================================
    // MAIN POPUP NẰM DƯỚI BOTTOM BAR
    // ============================================================

    private void PutMainPopupBelowBottomBar(Popup popup)
    {
        if (popup == null)
            return;

        Canvas popupCanvas =
            popup.Canvas;

        if (popupCanvas == null)
            return;

        popupCanvas.overrideSorting = true;

        popupCanvas.sortingOrder =
            bottomBarSortingOrder - 1;
    }


    // ============================================================
    // SHOW
    // ============================================================

    public void Show<T>(
        PopupAnimation popupAnimation =
            PopupAnimation.None)
    {
        CancelPictureSlide();
        Type popupType =
            typeof(T);

        if (!_dictionary.TryGetValue(
            popupType,
            out Popup popup))
        {
            Debug.LogWarning(
                $"[PopupController] Popup not found: {popupType.Name}"
            );

            return;
        }


        // ========================================================
        // 5 MAIN POPUPS
        // ========================================================

        if (IsMainBottomBarPopup(popupType))
        {
            SetBottomBarVisible(true);

            PutMainPopupBelowBottomBar(
                popup
            );
        }

        // ========================================================
        // POPUP KHÁC
        // ========================================================

        else
        {
            // Nếu vẫn còn main popup phía dưới,
            // popup này là overlay.
            if (HasActiveMainBottomBarPopup())
            {
                SetBottomBarVisible(true);

                PutPopupAboveBottomBar(
                    popup
                );
            }
            else
            {
                // Không có Home / Shop / League /
                // Collection / Kingdom phía dưới.
                //
                // Ví dụ:
                // InGame
                // Win
                // Lose
                // Loading
                //
                // => Ẩn BottomBar.
                SetBottomBarVisible(false);
            }
        }


        // ========================================================
        // SHOW POPUP
        // ========================================================

        // A popup can still be active in the hierarchy while its previous
        // alpha/scale animation has left it visually hidden. This notably
        // happened to PopupBooster after returning from InGame: the first
        // click found an active object and skipped Show(), so only a later
        // click appeared to work. When another popup is currently on top,
        // always run Show() again to restore and animate the requested popup.
        if (!popup.isActiveAndEnabled || currentPopup != popup)
        {
            popup.Show(
                popupAnimation
            );
        }

        currentPopup =
            popup;

        int mainTabIndex =
            GetMainTabIndex(popup);

        if (mainTabIndex >= 0 &&
            bottomBarInstance != null &&
            (mainTabSwipeController == null ||
             !mainTabSwipeController.IsTransitioning))
        {
            bottomBarInstance.SetSelectedInstant(
                mainTabIndex
            );
        }
    }


    // ============================================================
    // HIDE
    // ============================================================

    public void Hide<T>(
        PopupAnimation popupAnimation =
            PopupAnimation.None)
    {
        if (!_dictionary.TryGetValue(
            typeof(T),
            out Popup popup))
        {
            return;
        }

        if (popup.isActiveAndEnabled)
        {
            popup.Hide(
                popupAnimation
            );
        }
    }


    // ============================================================
    // HIDE ALL
    // ============================================================

    public void HideAll()
    {
        CancelPictureSlide();
        foreach (Popup item
                 in _dictionary.Values)
        {
            if (!item.isActiveAndEnabled)
                continue;

            item.Hide(
                PopupAnimation.None
            );
        }
    }

    public void HideAllExcept<T>() where T : Popup
    {
        CancelPictureSlide();
        foreach (Popup item
                 in _dictionary.Values)
        {
            if (item is T || !item.isActiveAndEnabled)
                continue;

            item.Hide(
                PopupAnimation.None
            );
        }
    }


    // ============================================================
    // GET
    // ============================================================

    public Popup Get<T>()
    {
        if (_dictionary.TryGetValue(
            typeof(T),
            out Popup popup))
        {
            return popup;
        }

        return null;
    }

    public Popup Get(Type popupType)
    {
        if (popupType != null &&
            _dictionary.TryGetValue(popupType, out Popup popup))
        {
            return popup;
        }

        return null;
    }


    // ============================================================
    // SAVE POPUPS
    // ============================================================

    public void SavePopups()
    {
        _savingPopups.Clear();

        foreach (Popup item
                 in _dictionary.Values)
        {
            if (item.isActiveAndEnabled)
            {
                _savingPopups.Add(
                    item
                );
            }
        }
    }


    // ============================================================
    // LOAD POPUPS
    // ============================================================

    public void LoadPopups()
    {
        foreach (Popup popup
                 in _savingPopups.ToList())
        {
            popup.Show(
                PopupAnimation.None
            );
        }

        if (HasActiveMainBottomBarPopup())
        {
            SetBottomBarVisible(true);
        }
        else
        {
            SetBottomBarVisible(false);
        }
    }


    // ============================================================
    // DEBUG CONSOLE
    // ============================================================

    public void InitializeDebugConsole()
    {
        if (popupConfig == null ||
            popupConfig.popupDebugConsole == null ||
            canvasTransform == null)
        {
            return;
        }

        PopupDebugConsole popupDebugConsole =
            Instantiate(
                popupConfig.popupDebugConsole,
                canvasTransform
            );

        popupDebugConsole.Canvas.sortingOrder =
            999;

        popupDebugConsole.Show(
            PopupAnimation.None
        );
    }


    // ============================================================
    // NOTIFY
    // ============================================================

    private void SpawnNotifyText(
        string content,
        Vector3 position)
    {
        if (string.IsNullOrEmpty(content))
            return;

        if (notifyTagItemPrefab == null ||
            canvasTransform2 == null)
        {
            return;
        }

        NotifyTagItem notifyText =
            LeanPool.Spawn(
                notifyTagItemPrefab,
                canvasTransform2
            );

        notifyText.SetText(
            content
        );

        notifyText.Action(
            position
        );
    }


    // ============================================================
    // TARGET UI
    // ============================================================

    public void SetTargetUI(
        bool isActive,
        TargetUIType targetUIType =
            TargetUIType.Frame,
        RectTransform targetTransform =
            null,
        float timeDelay =
            0)
    {
        if (blockUI2 == null)
            return;

        blockUI2.SetTargetUI(
            isActive,
            targetUIType,
            targetTransform,
            timeDelay
        );
    }


    // ============================================================
    // BLOCK UI
    // ============================================================

    public void SetBlockUIState(
        bool isActive,
        BlockUIType blockUIType =
            BlockUIType.Black,
        bool useFetching =
            false,
        float timeDelay =
            0,
        Action onComplete =
            null)
    {
        if (blockUI2 == null)
            return;

        blockUI2.SetBlockUIState(
            isActive,
            blockUIType,
            useFetching,
            timeDelay,
            onComplete
        );
    }


    // ============================================================
    // ADD CANVAS FOR HIGHLIGHT
    // ============================================================

    public void AddCanvasForHighlight(
        GameObject go)
    {
        if (go == null)
            return;

        if (_currentHighlightObj != null &&
            _currentHighlightObj != go)
        {
            RemoveCanvasFromHighlight(
                _currentHighlightObj
            );
        }

        _currentHighlightObj =
            go;

        _addedCanvasToCurrent =
            false;

        _addedRaycasterToCurrent =
            false;


        if (!go.TryGetComponent(
            out Canvas canvas))
        {
            canvas =
                go.AddComponent<Canvas>();

            _addedCanvasToCurrent =
                true;
        }


        if (!go.TryGetComponent(
            out GraphicRaycaster _))
        {
            go.AddComponent<GraphicRaycaster>();

            _addedRaycasterToCurrent =
                true;
        }


        StartCoroutine(
            DelaySetOverrideSorting(
                canvas
            )
        );
    }


    // ============================================================
    // REMOVE CANVAS FROM HIGHLIGHT
    // ============================================================

    public void RemoveCanvasFromHighlight(
        GameObject go)
    {
        if (go == null)
            return;

        if (go == _currentHighlightObj)
        {
            if (_addedRaycasterToCurrent &&
                go.TryGetComponent(
                    out GraphicRaycaster raycaster))
            {
                DestroyImmediate(
                    raycaster
                );
            }

            _addedRaycasterToCurrent =
                false;


            if (go.TryGetComponent(
                out Canvas canvas))
            {
                canvas.overrideSorting =
                    false;

                if (_addedCanvasToCurrent)
                {
                    DestroyImmediate(
                        canvas
                    );
                }
            }

            _addedCanvasToCurrent =
                false;

            _currentHighlightObj =
                null;
        }
        else
        {
            if (go.TryGetComponent(
                out Canvas canvas))
            {
                canvas.overrideSorting =
                    false;
            }
        }
    }


    // ============================================================
    // DELAY SORTING
    // ============================================================

    private System.Collections.IEnumerator
        DelaySetOverrideSorting(
            Canvas canvas)
    {
        yield return
            new WaitForEndOfFrame();

        if (canvas != null)
        {
            canvas.overrideSorting =
                true;

            canvas.sortingOrder =
                999;
        }
    }


    // ============================================================
    // BRING TO FRONT
    // ============================================================

    public void BringToFront(
        GameObject go)
    {
        if (go == null ||
            canvasTransform2 == null)
        {
            return;
        }

        go.transform.SetParent(
            canvasTransform2
        );

        int layer =
            LayerMask.NameToLayer(
                "UI2"
            );

        go.layer =
            layer;

        Utility.SetLayerRecursively(
            go,
            layer
        );
    }


    // ============================================================
    // BRING BACK
    // ============================================================

    public void BringBack(
        GameObject go,
        RectTransform rectTransform)
    {
        if (go == null ||
            rectTransform == null)
        {
            return;
        }

        go.transform.SetParent(
            rectTransform
        );

        int layer =
            LayerMask.NameToLayer(
                "UI"
            );

        go.layer =
            layer;

        Utility.SetLayerRecursively(
            go,
            layer
        );
    }


    // ============================================================
    // ENABLE BLOCK UI
    // ============================================================

    public void EnableBlockUIRaycast(
        GameObject go,
        TargetUIType targetUIType =
            TargetUIType.Frame)
    {
        if (go == null ||
            blockUI == null)
        {
            return;
        }

        blockUI.SetBlockUIState(
            true
        );

        blockUI.SetTargetUI(
            true,
            targetUIType,
            go.GetComponent<RectTransform>(),
            timeDelay: .5f
        );

        AddCanvasForHighlight(
            go
        );
    }


    // ============================================================
    // DISABLE BLOCK UI
    // ============================================================

    public void DisableBlockUIRaycast(
        GameObject go)
    {
        if (go == null ||
            blockUI == null)
        {
            return;
        }

        blockUI.SetBlockUIState(
            false
        );

        blockUI.SetTargetUI(
            false
        );

        RemoveCanvasFromHighlight(
            go
        );
    }
}
