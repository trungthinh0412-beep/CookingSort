using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BottomTabBar : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        [Header("Button")]
        public Button button;
        public LayoutElement buttonLayout;

        [Header("Under Image")]
        public LayoutElement underLayout;

        [Header("Visual")]
        public RectTransform icon;
        public RectTransform label;
    }

    [Header("Tabs")]
    [SerializeField] private List<Tab> tabs = new List<Tab>();

    [Header("Layout Roots")]
    [SerializeField] private RectTransform buttonLayoutRoot;
    [SerializeField] private RectTransform underLayoutRoot;

    [Header("Overlay")]
    [SerializeField] private RectTransform overlaySelected;
    [SerializeField] private float overlayOffsetX = 0f;

    [Header("Tab Width")]
    [SerializeField] private float normalWidth = 198f;
    [SerializeField] private float selectedWidth = 288f;

    [Header("Animation")]
    [SerializeField] private float widthDuration = 0.22f;
    [SerializeField] private float overlayDuration = 0.28f;

    [Header("Selected Icon")]
    [SerializeField] private float selectedIconYOffset = 70f;

    // Scale cuối cùng
    [SerializeField] private float selectedIconScale = 1.4f;

    // Scale overshoot
    [SerializeField] private float selectedIconOvershootScale = 1.5f;

    [Header("Selected Label")]
    [SerializeField] private float selectedLabelYOffset = 10f;
    [SerializeField] private float selectedLabelScale = 1.2f;

    [Header("Visual Speed")]
    [SerializeField] private float selectVisualDuration = 0.20f;
    [SerializeField] private float deselectVisualDuration = 0.07f;

    [Header("Default Tab")]
    [SerializeField] private int defaultIndex = 2;


    // ============================================================
    // RUNTIME
    // ============================================================

    private int currentIndex = -1;
    private bool isAnimating = false;

    private Coroutine transitionRoutine;

    private Vector2[] normalIconPositions;
    private Vector3[] normalIconScales;

    private Vector2[] normalLabelPositions;
    private Vector3[] normalLabelScales;
    private MainTabSwipeController swipeController;
    private RectTransform responsiveRoot;
    private Canvas responsiveCanvas;
    private HorizontalLayoutGroup underLayoutGroup;
    private Vector2 authoredRootSize;
    private Vector2 authoredButtonPosition;
    private Vector2 authoredUnderPosition;
    private Vector2 authoredUnderSize;
    private float authoredRootBottom;
    private int authoredUnderBottomPadding;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private bool hasResponsiveBaseline;


    // ============================================================
    // CONSTANT
    // ============================================================

    private const int HOME_INDEX = 2;

    public int CurrentIndex => currentIndex;
    public int TabCount => tabs.Count;
    public bool IsAnimating => isAnimating;
    private int DefaultIndex => tabs.Count == 0
        ? 0
        : Mathf.Clamp(defaultIndex, 0, tabs.Count - 1);


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        InitializeSafeAreaOffset();

        CacheNormalState();

        RegisterButtons();

        // ========================================================
        // LUÔN KHỞI ĐỘNG Ở HOME
        // ========================================================

        currentIndex = DefaultIndex;

        ApplyStateInstant(currentIndex);
    }

    private void OnEnable()
    {
        InitializeSafeAreaOffset();
        ApplySafeAreaOffset();
    }

    private void Update()
    {
        Rect safeArea = Screen.safeArea;
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (safeArea == lastSafeArea && screenSize == lastScreenSize)
        {
            return;
        }

        ApplySafeAreaOffset();
    }

    private void InitializeSafeAreaOffset()
    {
        if (responsiveRoot == null)
        {
            responsiveRoot = transform as RectTransform;
        }

        if (responsiveCanvas == null)
        {
            responsiveCanvas = GetComponentInParent<Canvas>();
        }

        if (!hasResponsiveBaseline && responsiveRoot != null)
        {
            authoredRootSize = responsiveRoot.sizeDelta;
            authoredRootBottom = responsiveRoot.anchoredPosition.y -
                                 authoredRootSize.y * responsiveRoot.pivot.y;

            if (buttonLayoutRoot != null)
            {
                authoredButtonPosition = buttonLayoutRoot.anchoredPosition;
            }

            if (underLayoutRoot != null)
            {
                authoredUnderPosition = underLayoutRoot.anchoredPosition;
                authoredUnderSize = underLayoutRoot.sizeDelta;
                underLayoutGroup = underLayoutRoot.GetComponent<HorizontalLayoutGroup>();
                if (underLayoutGroup != null)
                {
                    authoredUnderBottomPadding = underLayoutGroup.padding.bottom;
                }
            }

            hasResponsiveBaseline = true;
        }
    }

    private void ApplySafeAreaOffset()
    {
        if (!hasResponsiveBaseline || responsiveRoot == null)
        {
            return;
        }

        float canvasHeight = Screen.height;
        if (responsiveCanvas != null &&
            responsiveCanvas.transform is RectTransform canvasRect)
        {
            canvasHeight = canvasRect.rect.height;
        }

        float bottomInset = Screen.height > 0
            ? Mathf.Max(0f, Screen.safeArea.yMin / Screen.height * canvasHeight)
            : 0f;

        // Keep the blue bar touching the physical bottom edge. Only its
        // interactive content moves above the home indicator. Expanding the
        // opaque underlay prevents a blank strip from appearing below it.
        Vector2 rootSize = authoredRootSize;
        rootSize.y += bottomInset;
        responsiveRoot.sizeDelta = rootSize;

        Vector2 rootPosition = responsiveRoot.anchoredPosition;
        rootPosition.y = authoredRootBottom + rootSize.y * responsiveRoot.pivot.y;
        responsiveRoot.anchoredPosition = rootPosition;

        if (buttonLayoutRoot != null)
        {
            Vector2 buttonPosition = authoredButtonPosition;
            buttonPosition.y += bottomInset;
            buttonLayoutRoot.anchoredPosition = buttonPosition;
        }

        if (underLayoutRoot != null)
        {
            Vector2 underSize = authoredUnderSize;
            underSize.y += bottomInset;
            underLayoutRoot.sizeDelta = underSize;

            Vector2 underPosition = authoredUnderPosition;
            underPosition.y += bottomInset * 0.5f;
            underLayoutRoot.anchoredPosition = underPosition;
        }

        if (underLayoutGroup != null)
        {
            RectOffset padding = underLayoutGroup.padding;
            underLayoutGroup.padding = new RectOffset(
                padding.left,
                padding.right,
                padding.top,
                authoredUnderBottomPadding + Mathf.RoundToInt(bottomInset)
            );
        }

        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }


    // ============================================================
    // PUBLIC RESET HOME
    //
    // PopupController có thể gọi hàm này sau khi instantiate.
    // Không mở PopupHome.
    // Chỉ reset visual BottomBar.
    // ============================================================

    public void ResetToHome()
    {
        // Nếu đang chạy animation thì dừng
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);

            transitionRoutine = null;
        }

        isAnimating = false;

        currentIndex = DefaultIndex;

        ApplyStateInstant(currentIndex);
    }


    // ============================================================
    // CACHE NORMAL STATE
    // ============================================================

    private void CacheNormalState()
    {
        int count = tabs.Count;

        normalIconPositions = new Vector2[count];
        normalIconScales = new Vector3[count];

        normalLabelPositions = new Vector2[count];
        normalLabelScales = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            if (tabs[i].icon != null)
            {
                normalIconPositions[i] =
                    tabs[i].icon.anchoredPosition;

                normalIconScales[i] =
                    tabs[i].icon.localScale;
            }

            if (tabs[i].label != null)
            {
                normalLabelPositions[i] =
                    tabs[i].label.anchoredPosition;

                normalLabelScales[i] =
                    tabs[i].label.localScale;
            }
        }
    }


    // ============================================================
    // REGISTER BUTTONS
    // ============================================================

    private void RegisterButtons()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;

            if (tabs[i].button == null)
                continue;

            tabs[i].button.onClick.AddListener(
                () => SelectTab(index)
            );
        }
    }


    // ============================================================
    // SELECT TAB
    // ============================================================

    public void SelectTab(int newIndex)
    {
        if (!IsValidIndex(newIndex))
            return;

        if (newIndex == currentIndex)
            return;

        SoundController.Instance.PlayFX(SoundName.MenuBar);

        if (swipeController != null)
        {
            swipeController.GoToTab(newIndex);
            return;
        }

        if (isAnimating)
            return;

        transitionRoutine =
            StartCoroutine(
                AnimateToTab(newIndex)
            );
    }

    public void BindSwipeController(
        MainTabSwipeController controller)
    {
        swipeController = controller;
    }

    public void SetSelectedInstant(int index)
    {
        if (!IsValidIndex(index))
            return;

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        isAnimating = false;
        ApplyStateInstant(index);
    }

    public void SetInteractiveProgress(
        int fromIndex,
        int toIndex,
        float progress)
    {
        if (!IsValidIndex(fromIndex) ||
            !IsValidIndex(toIndex))
        {
            return;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        isAnimating = true;

        float t = Mathf.Clamp01(progress);

        for (int i = 0; i < tabs.Count; i++)
        {
            float selectedAmount = 0f;

            if (i == fromIndex)
                selectedAmount = 1f - t;

            if (i == toIndex)
                selectedAmount = t;

            float width =
                Mathf.Lerp(
                    normalWidth,
                    selectedWidth,
                    selectedAmount
                );

            SetWidth(tabs[i].buttonLayout, width);
            SetWidth(tabs[i].underLayout, width);
        }

        ForceLayoutNow();

        for (int i = 0; i < tabs.Count; i++)
        {
            float selectedAmount = 0f;

            if (i == fromIndex)
                selectedAmount = 1f - t;

            if (i == toIndex)
                selectedAmount = t;

            if (tabs[i].icon != null)
            {
                tabs[i].icon.anchoredPosition =
                    normalIconPositions[i] +
                    Vector2.up *
                    selectedIconYOffset *
                    selectedAmount;

                tabs[i].icon.localScale =
                    normalIconScales[i] *
                    Mathf.Lerp(
                        1f,
                        selectedIconScale,
                        selectedAmount
                    );
            }

            if (tabs[i].label != null)
            {
                // Keep only the destination label visible during a swipe.
                // Showing both labels while their positions animate makes
                // the old and new tab names overlap for a few frames.
                tabs[i].label.gameObject.SetActive(
                    i == toIndex && selectedAmount > 0.001f
                );

                tabs[i].label.anchoredPosition =
                    normalLabelPositions[i] +
                    Vector2.up *
                    selectedLabelYOffset *
                    selectedAmount;

                tabs[i].label.localScale =
                    normalLabelScales[i] *
                    Mathf.Lerp(
                        1f,
                        selectedLabelScale,
                        selectedAmount
                    );
            }
        }

        if (overlaySelected != null)
        {
            float currentX =
                overlaySelected.anchoredPosition.x;

            float fromX =
                CalculateOverlayTargetFromIcon(
                    fromIndex,
                    currentX
                );

            float toX =
                CalculateOverlayTargetFromIcon(
                    toIndex,
                    currentX
                );

            Vector2 position =
                overlaySelected.anchoredPosition;

            position.x = Mathf.Lerp(fromX, toX, t);
            overlaySelected.anchoredPosition = position;
        }
    }

    public void CompleteInteractiveTransition(int index)
    {
        isAnimating = false;
        ApplyStateInstant(index);
    }

    public void CancelInteractiveTransition(int index)
    {
        isAnimating = false;
        ApplyStateInstant(index);
    }


    // ============================================================
    // MAIN ANIMATION
    // ============================================================

    private IEnumerator AnimateToTab(int newIndex)
    {
        isAnimating = true;

        int oldIndex = currentIndex;
        int count = tabs.Count;


        // ========================================================
        // CACHE START VALUES
        // ========================================================

        float[] startButtonWidths =
            new float[count];

        float[] startUnderWidths =
            new float[count];

        Vector2[] startIconPositions =
            new Vector2[count];

        Vector3[] startIconScales =
            new Vector3[count];


        for (int i = 0; i < count; i++)
        {
            startButtonWidths[i] =
                GetWidth(
                    tabs[i].buttonLayout,
                    normalWidth
                );

            startUnderWidths[i] =
                GetWidth(
                    tabs[i].underLayout,
                    normalWidth
                );

            if (tabs[i].icon != null)
            {
                startIconPositions[i] =
                    tabs[i].icon.anchoredPosition;

                startIconScales[i] =
                    tabs[i].icon.localScale;
            }
        }


        // ========================================================
        // OVERLAY START
        // ========================================================

        float overlayStartX =
            overlaySelected != null
                ? overlaySelected.anchoredPosition.x
                : 0f;


        // ========================================================
        // CALCULATE FINAL OVERLAY TARGET
        // ========================================================

        for (int i = 0; i < count; i++)
        {
            float finalWidth =
                i == newIndex
                    ? selectedWidth
                    : normalWidth;

            SetWidth(
                tabs[i].buttonLayout,
                finalWidth
            );

            SetWidth(
                tabs[i].underLayout,
                finalWidth
            );
        }


        ForceLayoutNow();


        float overlayTargetX =
            CalculateOverlayTargetFromIcon(
                newIndex,
                overlayStartX
            );


        // ========================================================
        // RESTORE START WIDTH
        // ========================================================

        for (int i = 0; i < count; i++)
        {
            SetWidth(
                tabs[i].buttonLayout,
                startButtonWidths[i]
            );

            SetWidth(
                tabs[i].underLayout,
                startUnderWidths[i]
            );
        }


        ForceLayoutNow();


        if (overlaySelected != null)
        {
            Vector2 pos =
                overlaySelected.anchoredPosition;

            pos.x =
                overlayStartX;

            overlaySelected.anchoredPosition =
                pos;
        }


        // ========================================================
        // LABEL OLD
        // ========================================================

        if (IsValidIndex(oldIndex) &&
            tabs[oldIndex].label != null)
        {
            tabs[oldIndex].label.gameObject
                .SetActive(false);

            tabs[oldIndex].label.anchoredPosition =
                normalLabelPositions[oldIndex];

            tabs[oldIndex].label.localScale =
                normalLabelScales[oldIndex];
        }


        // ========================================================
        // LABEL NEW
        // ========================================================

        if (tabs[newIndex].label != null)
        {
            tabs[newIndex].label.gameObject
                .SetActive(true);

            tabs[newIndex].label.anchoredPosition =
                normalLabelPositions[newIndex];

            tabs[newIndex].label.localScale =
                normalLabelScales[newIndex];
        }


        // ========================================================
        // DURATION
        // ========================================================

        float totalDuration =
            Mathf.Max(
                widthDuration,
                overlayDuration,
                selectVisualDuration,
                deselectVisualDuration
            );

        float time = 0f;


        // ========================================================
        // ANIMATION LOOP
        // ========================================================

        while (time < totalDuration)
        {
            time += Time.unscaledDeltaTime;


            float widthT =
                EaseInOutCubic(
                    Mathf.Clamp01(
                        time /
                        Mathf.Max(
                            widthDuration,
                            0.001f
                        )
                    )
                );


            float overlayT =
                EaseInOutCubic(
                    Mathf.Clamp01(
                        time /
                        Mathf.Max(
                            overlayDuration,
                            0.001f
                        )
                    )
                );


            float selectT =
                EaseOutCubic(
                    Mathf.Clamp01(
                        time /
                        Mathf.Max(
                            selectVisualDuration,
                            0.001f
                        )
                    )
                );


            float deselectT =
                EaseOutCubic(
                    Mathf.Clamp01(
                        time /
                        Mathf.Max(
                            deselectVisualDuration,
                            0.001f
                        )
                    )
                );


            // ====================================================
            // WIDTH
            // ====================================================

            for (int i = 0; i < count; i++)
            {
                bool selected =
                    i == newIndex;

                float targetWidth =
                    selected
                        ? selectedWidth
                        : normalWidth;


                SetWidth(
                    tabs[i].buttonLayout,
                    Mathf.Lerp(
                        startButtonWidths[i],
                        targetWidth,
                        widthT
                    )
                );


                SetWidth(
                    tabs[i].underLayout,
                    Mathf.Lerp(
                        startUnderWidths[i],
                        targetWidth,
                        widthT
                    )
                );
            }


            RebuildLayouts();


            // ====================================================
            // ICON
            // ====================================================

            for (int i = 0; i < count; i++)
            {
                if (tabs[i].icon == null)
                    continue;


                bool selected =
                    i == newIndex;


                // ================================================
                // POSITION
                // ================================================

                Vector2 targetPosition =
                    normalIconPositions[i];


                if (selected)
                {
                    targetPosition +=
                        Vector2.up *
                        selectedIconYOffset;
                }


                float positionT =
                    selected
                        ? selectT
                        : deselectT;


                tabs[i].icon.anchoredPosition =
                    Vector2.Lerp(
                        startIconPositions[i],
                        targetPosition,
                        positionT
                    );


                // ================================================
                // SCALE
                //
                // selected:
                // current -> 1.5 -> 1.4
                //
                // deselected:
                // current -> normal
                // ================================================

                if (selected)
                {
                    Vector3 overshootScale =
                        normalIconScales[i] *
                        selectedIconOvershootScale;


                    Vector3 finalScale =
                        normalIconScales[i] *
                        selectedIconScale;


                    float rawSelectT =
                        Mathf.Clamp01(
                            time /
                            Mathf.Max(
                                selectVisualDuration,
                                0.001f
                            )
                        );


                    const float overshootPoint =
                        0.65f;


                    if (rawSelectT < overshootPoint)
                    {
                        float t =
                            rawSelectT /
                            overshootPoint;


                        t =
                            EaseOutCubic(t);


                        tabs[i].icon.localScale =
                            Vector3.Lerp(
                                startIconScales[i],
                                overshootScale,
                                t
                            );
                    }
                    else
                    {
                        float t =
                            (
                                rawSelectT -
                                overshootPoint
                            ) /
                            (
                                1f -
                                overshootPoint
                            );


                        t =
                            EaseOutCubic(t);


                        tabs[i].icon.localScale =
                            Vector3.Lerp(
                                overshootScale,
                                finalScale,
                                t
                            );
                    }
                }
                else
                {
                    tabs[i].icon.localScale =
                        Vector3.Lerp(
                            startIconScales[i],
                            normalIconScales[i],
                            deselectT
                        );
                }
            }


            // ====================================================
            // LABEL
            // ====================================================

            if (tabs[newIndex].label != null)
            {
                Vector2 targetPosition =
                    normalLabelPositions[newIndex] +
                    Vector2.up *
                    selectedLabelYOffset;


                Vector3 targetScale =
                    normalLabelScales[newIndex] *
                    selectedLabelScale;


                tabs[newIndex].label.anchoredPosition =
                    Vector2.Lerp(
                        normalLabelPositions[newIndex],
                        targetPosition,
                        selectT
                    );


                tabs[newIndex].label.localScale =
                    Vector3.Lerp(
                        normalLabelScales[newIndex],
                        targetScale,
                        selectT
                    );
            }


            // ====================================================
            // OVERLAY
            // ====================================================

            if (overlaySelected != null)
            {
                Vector2 pos =
                    overlaySelected.anchoredPosition;


                pos.x =
                    Mathf.Lerp(
                        overlayStartX,
                        overlayTargetX,
                        overlayT
                    );


                overlaySelected.anchoredPosition =
                    pos;
            }


            yield return null;
        }


        // ========================================================
        // FINAL
        // ========================================================

        currentIndex =
            newIndex;


        ApplyStateInstant(
            newIndex
        );


        transitionRoutine =
            null;


        isAnimating =
            false;


        // ========================================================
        // OPEN POPUP AFTER ANIMATION
        // ========================================================

        OpenPopup(
            newIndex
        );
    }


    // ============================================================
    // APPLY STATE INSTANT
    // ============================================================

    private void ApplyStateInstant(int index)
    {
        if (!IsValidIndex(index))
            return;


        currentIndex =
            index;


        // ========================================================
        // WIDTH
        // ========================================================

        for (int i = 0; i < tabs.Count; i++)
        {
            bool selected =
                i == index;


            float width =
                selected
                    ? selectedWidth
                    : normalWidth;


            SetWidth(
                tabs[i].buttonLayout,
                width
            );


            SetWidth(
                tabs[i].underLayout,
                width
            );
        }


        ForceLayoutNow();


        // ========================================================
        // ICON + LABEL
        // ========================================================

        for (int i = 0; i < tabs.Count; i++)
        {
            bool selected =
                i == index;


            if (tabs[i].icon != null)
            {
                Vector2 pos =
                    normalIconPositions[i];


                Vector3 scale =
                    normalIconScales[i];


                if (selected)
                {
                    pos +=
                        Vector2.up *
                        selectedIconYOffset;


                    scale =
                        normalIconScales[i] *
                        selectedIconScale;
                }


                tabs[i].icon.anchoredPosition =
                    pos;


                tabs[i].icon.localScale =
                    scale;
            }


            if (tabs[i].label != null)
            {
                tabs[i].label.gameObject
                    .SetActive(selected);


                if (selected)
                {
                    tabs[i].label.anchoredPosition =
                        normalLabelPositions[i] +
                        Vector2.up *
                        selectedLabelYOffset;


                    tabs[i].label.localScale =
                        normalLabelScales[i] *
                        selectedLabelScale;
                }
                else
                {
                    tabs[i].label.anchoredPosition =
                        normalLabelPositions[i];


                    tabs[i].label.localScale =
                        normalLabelScales[i];
                }
            }
        }


        // ========================================================
        // OVERLAY
        // ========================================================

        if (overlaySelected != null)
        {
            float startX =
                overlaySelected.anchoredPosition.x;


            float targetX =
                CalculateOverlayTargetFromIcon(
                    index,
                    startX
                );


            Vector2 pos =
                overlaySelected.anchoredPosition;


            pos.x =
                targetX;


            overlaySelected.anchoredPosition =
                pos;
        }
    }


    // ============================================================
    // OVERLAY TARGET FROM ICON CENTER
    // ============================================================

    private float CalculateOverlayTargetFromIcon(
        int index,
        float currentOverlayX)
    {
        if (!IsValidIndex(index))
            return currentOverlayX;


        if (overlaySelected == null)
            return currentOverlayX;


        RectTransform icon =
            tabs[index].icon;


        if (icon == null)
            return currentOverlayX;


        RectTransform parent =
            overlaySelected.parent
            as RectTransform;


        if (parent == null)
            return currentOverlayX;


        // ========================================================
        // ICON CENTER
        // ========================================================

        Vector3[] iconCorners =
            new Vector3[4];


        icon.GetWorldCorners(
            iconCorners
        );


        Vector3 iconWorldCenter =
            (
                iconCorners[0] +
                iconCorners[1] +
                iconCorners[2] +
                iconCorners[3]
            ) / 4f;


        // ========================================================
        // OVERLAY CENTER
        // ========================================================

        Vector3[] overlayCorners =
            new Vector3[4];


        overlaySelected.GetWorldCorners(
            overlayCorners
        );


        Vector3 overlayWorldCenter =
            (
                overlayCorners[0] +
                overlayCorners[1] +
                overlayCorners[2] +
                overlayCorners[3]
            ) / 4f;


        // ========================================================
        // SAME LOCAL SPACE
        // ========================================================

        Vector3 iconLocal =
            parent.InverseTransformPoint(
                iconWorldCenter
            );


        Vector3 overlayLocal =
            parent.InverseTransformPoint(
                overlayWorldCenter
            );


        float deltaX =
            iconLocal.x -
            overlayLocal.x;


        return
            currentOverlayX +
            deltaX +
            overlayOffsetX;
    }


    // ============================================================
    // OPEN POPUP
    // ============================================================

    private void OpenPopup(int index)
    {
        if (PopupController.Instance == null)
        {
            Debug.LogError(
                "[BottomTabBar] PopupController.Instance is null."
            );

            return;
        }


        PopupController.Instance.HideAll();


        switch (index)
        {
            case 0:
                PopupController.Instance
                    .Show<PopupShop>(
                        PopupAnimation.None
                    );
                break;


            case 1:
                PopupController.Instance
                    .Show<PopupLeague>(
                        PopupAnimation.None
                    );
                break;


            case 2:
                PopupController.Instance.Show<PopupHome>(
                    PopupAnimation.None
                );
                break;


            case 3:
                PopupController.Instance
                    .Show<PopupCollection>(
                        PopupAnimation.None
                    );
                break;


            case 4:
                PopupController.Instance
                    .Show<PopupKingdom>(
                        PopupAnimation.None
                    );
                break;
        }
    }


    // ============================================================
    // WIDTH
    // ============================================================

    private void SetWidth(
        LayoutElement element,
        float width)
    {
        if (element == null)
            return;


        element.flexibleWidth =
            0f;


        element.preferredWidth =
            width;
    }


    private float GetWidth(
        LayoutElement element,
        float fallback)
    {
        if (element == null)
            return fallback;


        if (element.preferredWidth >= 0f)
            return element.preferredWidth;


        RectTransform rect =
            element.transform
            as RectTransform;


        if (rect != null)
            return rect.rect.width;


        return fallback;
    }


    // ============================================================
    // LAYOUT
    // ============================================================

    private void RebuildLayouts()
    {
        if (underLayoutRoot != null)
        {
            LayoutRebuilder
                .ForceRebuildLayoutImmediate(
                    underLayoutRoot
                );
        }


        if (buttonLayoutRoot != null)
        {
            LayoutRebuilder
                .ForceRebuildLayoutImmediate(
                    buttonLayoutRoot
                );
        }
    }


    private void ForceLayoutNow()
    {
        RebuildLayouts();

        Canvas.ForceUpdateCanvases();

        RebuildLayouts();
    }


    // ============================================================
    // EASING
    // ============================================================

    private float EaseOutCubic(float x)
    {
        return
            1f -
            Mathf.Pow(
                1f - x,
                3f
            );
    }


    private float EaseInOutCubic(float x)
    {
        return
            x < 0.5f
                ? 4f * x * x * x
                : 1f -
                  Mathf.Pow(
                      -2f * x + 2f,
                      3f
                  ) / 2f;
    }


    // ============================================================
    // VALIDATION
    // ============================================================

    private bool IsValidIndex(int index)
    {
        return
            index >= 0 &&
            index < tabs.Count;
    }
}
