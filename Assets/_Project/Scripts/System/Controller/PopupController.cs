using System;
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
    [SerializeField] private BottomTabBar bottomBarPrefab;

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

    private GameObject _currentHighlightObj = null;

    private bool _addedCanvasToCurrent = false;
    private bool _addedRaycasterToCurrent = false;

    private readonly Dictionary<Type, Popup> _dictionary =
        new Dictionary<Type, Popup>();
    private readonly Dictionary<Type, Popup> _popupPrefabs =
        new Dictionary<Type, Popup>();
    private readonly Dictionary<Type, int> _popupSortingOrders =
        new Dictionary<Type, int>();
    private readonly List<Type> _inactivePopupTypes =
        new List<Type>();

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
        InitializeBottomBar();
        InitializeMainTabSwipe();
    }

    private void InitializeMainTabSwipe()
    {
        if (bottomBarInstance == null)
            return;

        if (mainTabSwipeController == null)
        {
            mainTabSwipeController =
                GetComponent<MainTabSwipeController>();
        }

        if (mainTabSwipeController == null)
        {
            mainTabSwipeController =
                gameObject.AddComponent<MainTabSwipeController>();
        }

        mainTabSwipeController.Initialize(
            this,
            bottomBarInstance
        );

        bottomBarInstance.BindSwipeController(
            mainTabSwipeController
        );
    }


    // ============================================================
    // INITIALIZE POPUPS
    // ============================================================

    private void InitializePopups()
    {
        if (popupConfig == null || popupConfig.popups == null)
        {
            Debug.LogError(
                "[PopupController] PopupConfig or its popup list is null."
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

        int sortingOrder = 0;

        for (int i = 0; i < popupConfig.popups.Count; i++)
        {
            Popup popup = popupConfig.popups[i];
            if (popup == null)
                continue;

            Type popupType = popup.GetType();

            // Keep prefab metadata only. Instantiating every popup at startup
            // retains all of their canvases, scripts and referenced textures
            // in memory even when the player never opens them.
            if (!_popupPrefabs.ContainsKey(popupType))
            {
                _popupPrefabs.Add(popupType, popup);
                _popupSortingOrders.Add(popupType, sortingOrder);
            }

            sortingOrder++;

            // Initialize can be called again after a popup prefab has been
            // imported while the editor is already running. Existing runtime
            // instances remain registered and are not duplicated.
            if (_dictionary.TryGetValue(popupType, out Popup existing) &&
                existing != null)
            {
                AttachLifecycleNotifier(existing);
            }
        }
    }

    private Popup GetOrCreatePopup(Type popupType)
    {
        if (popupType == null)
            return null;

        // Other singleton Start methods can request a popup before this
        // controller's Start method has run. Register prefab metadata on
        // demand so script execution order cannot make that request fail.
        if (!_dictionary.ContainsKey(popupType) &&
            !_popupPrefabs.ContainsKey(popupType))
        {
            InitializePopups();
        }

        if (_dictionary.TryGetValue(popupType, out Popup existing) &&
            existing != null)
        {
            return existing;
        }

        if (!_popupPrefabs.TryGetValue(popupType, out Popup popupPrefab) ||
            popupPrefab == null || canvasTransform == null)
        {
            return null;
        }

        Popup popupInstance = Instantiate(popupPrefab, canvasTransform);
        AttachLifecycleNotifier(popupInstance);
        popupInstance.gameObject.SetActive(false);

        if (popupInstance.Canvas != null &&
            _popupSortingOrders.TryGetValue(popupType, out int sortingOrder))
        {
            popupInstance.Canvas.sortingOrder = sortingOrder;
        }

        _dictionary[popupType] = popupInstance;
        return popupInstance;
    }

    private void AttachLifecycleNotifier(Popup popup)
    {
        if (popup == null)
            return;

        PopupLifecycleNotifier notifier =
            popup.GetComponent<PopupLifecycleNotifier>();

        if (notifier == null)
        {
            notifier =
                popup.gameObject.AddComponent<PopupLifecycleNotifier>();
        }

        notifier.Initialize(this, popup);
    }


    // ============================================================
    // INITIALIZE BOTTOM BAR
    // ============================================================

    private void InitializeBottomBar()
    {
        if (bottomBarPrefab == null)
        {
            Debug.LogWarning(
                "[PopupController] BottomBar Prefab chưa được gán."
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

        if (bottomBarInstance != null)
            return;


        bottomBarInstance =
            Instantiate(
                bottomBarPrefab,
                canvasTransform
            );


        GameObject bottomBarObject =
            bottomBarInstance.gameObject;


        // ========================================================
        // CANVAS RIÊNG
        // ========================================================

        Canvas bottomCanvas =
            bottomBarObject.GetComponent<Canvas>();

        if (bottomCanvas == null)
        {
            bottomCanvas =
                bottomBarObject.AddComponent<Canvas>();
        }

        bottomCanvas.overrideSorting = true;

        bottomCanvas.sortingOrder =
            bottomBarSortingOrder;


        // ========================================================
        // GRAPHIC RAYCASTER
        // ========================================================

        GraphicRaycaster raycaster =
            bottomBarObject.GetComponent<GraphicRaycaster>();

        if (raycaster == null)
        {
            bottomBarObject
                .AddComponent<GraphicRaycaster>();
        }


        // ========================================================
        // ĐƯA VỀ CUỐI HIERARCHY
        // ========================================================

        bottomBarObject.transform
            .SetAsLastSibling();


        // ========================================================
        // RESET VISUAL VỀ HOME
        // ========================================================

        bottomBarInstance.ResetToHome();


        // ========================================================
        // BAN ĐẦU ẨN
        //
        // PopupHome khi Show sẽ bật nó lên.
        // ========================================================

        bottomBarObject.SetActive(false);


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            "[PopupController] Persistent BottomBar created at HOME " +
            $"| SortingOrder = {bottomBarSortingOrder}"
        );
#endif
    }


    // ============================================================
    // BOTTOM BAR VISIBILITY
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[PopupController] BottomBar = {(visible ? "ON" : "OFF")}");
#endif
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
    // 4 MAIN POPUPS
    // ============================================================

    private bool IsMainBottomBarPopup(Type popupType)
    {
        return
            popupType == typeof(PopupLeague) ||
            popupType == typeof(PopupHome) ||
            popupType == typeof(PopupCollection) ||
            popupType == typeof(PopupKingdom);
    }

    public Popup GetMainTabPopup(int index)
    {
        switch (index)
        {
            case 0:
                return Get<PopupLeague>();
            case 1:
                return Get<PopupHome>();
            case 2:
                return Get<PopupCollection>();
            case 3:
                return Get<PopupKingdom>();
            default:
                return null;
        }
    }

    public int GetMainTabIndex(Popup popup)
    {
        if (popup is PopupLeague)
            return 0;

        if (popup is PopupHome)
            return 1;

        if (popup is PopupCollection)
            return 2;

        if (popup is PopupKingdom)
            return 3;

        return -1;
    }

    public int GetCurrentMainTabIndex()
    {
        return GetMainTabIndex(currentPopup);
    }

    public void NotifyPopupDisabled(Popup disabledPopup)
    {
        if (disabledPopup == null || currentPopup != disabledPopup)
            return;

        currentPopup = FindTopActivePopup(disabledPopup);

        bool hasActiveMainPopup = HasActiveMainBottomBarPopup();
        SetBottomBarVisible(hasActiveMainPopup);

        int mainTabIndex = GetMainTabIndex(currentPopup);

        if (mainTabIndex >= 0 && bottomBarInstance != null &&
            (mainTabSwipeController == null ||
             !mainTabSwipeController.IsTransitioning))
        {
            bottomBarInstance.SetSelectedInstant(mainTabIndex);
        }
    }

    private Popup FindTopActivePopup(Popup excludedPopup)
    {
        Popup topPopup = null;
        int topSortingOrder = int.MinValue;
        int topSiblingIndex = int.MinValue;

        foreach (Popup candidate in _dictionary.Values)
        {
            if (candidate == null || candidate == excludedPopup ||
                !candidate.isActiveAndEnabled)
            {
                continue;
            }

            Canvas candidateCanvas = candidate.Canvas;
            int sortingOrder = candidateCanvas != null
                ? candidateCanvas.sortingOrder
                : 0;
            int siblingIndex = candidate.transform.GetSiblingIndex();

            if (topPopup != null && sortingOrder < topSortingOrder)
                continue;

            if (topPopup != null && sortingOrder == topSortingOrder &&
                siblingIndex <= topSiblingIndex)
            {
                continue;
            }

            topPopup = candidate;
            topSortingOrder = sortingOrder;
            topSiblingIndex = siblingIndex;
        }

        return topPopup;
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
        Type popupType =
            typeof(T);

        Popup popup = GetOrCreatePopup(popupType);
        if (popup == null)
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

    /// <summary>
    /// Destroys popup hierarchies that are currently hidden. The prefab metadata is
    /// kept, so the popup is recreated automatically the next time it is requested.
    /// Intended for an operating system low-memory event, not normal navigation.
    /// </summary>
    public int ReleaseInactivePopups()
    {
        _inactivePopupTypes.Clear();

        foreach (KeyValuePair<Type, Popup> pair in _dictionary)
        {
            Popup popup = pair.Value;

            if (popup == null || !popup.isActiveAndEnabled)
            {
                _inactivePopupTypes.Add(pair.Key);
            }
        }

        int releasedCount = 0;

        foreach (Type popupType in _inactivePopupTypes)
        {
            if (!_dictionary.TryGetValue(popupType, out Popup popup))
                continue;

            _dictionary.Remove(popupType);
            _savingPopups.Remove(popup);

            if (currentPopup == popup)
                currentPopup = null;

            if (popup != null)
                Destroy(popup.gameObject);

            releasedCount++;
        }

        _inactivePopupTypes.Clear();
        return releasedCount;
    }


    // ============================================================
    // GET
    // ============================================================

    public Popup Get<T>()
    {
        return GetOrCreatePopup(typeof(T));
    }

    public Popup Get(Type popupType)
    {
        return GetOrCreatePopup(popupType);
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

[DisallowMultipleComponent]
internal sealed class PopupLifecycleNotifier : MonoBehaviour
{
    private PopupController owner;
    private Popup popup;

    public void Initialize(PopupController popupController, Popup targetPopup)
    {
        owner = popupController;
        popup = targetPopup;
    }

    private void OnDisable()
    {
        if (owner != null && popup != null)
            owner.NotifyPopupDisabled(popup);
    }
}
