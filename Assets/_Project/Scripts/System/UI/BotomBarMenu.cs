using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BottomBarMenu : MonoBehaviour
{
    [System.Serializable]
    public class TabData
    {
        [Header("References")]
        public GameObject root;
        public Image background;
        public Image icon;
        public TMP_Text label;

        [Header("Icon Sprites")]
        public Sprite normalIcon;
        public Sprite selectedIcon;

        [HideInInspector] public LayoutElement layout;
        [HideInInspector] public RectTransform iconRect;
        [HideInInspector] public RectTransform labelRect;
        [HideInInspector] public CanvasGroup labelGroup;
        [HideInInspector] public CanvasGroup tabCanvasGroup;

        [HideInInspector] public Vector2 normalIconPosition;
        [HideInInspector] public Vector3 normalIconScale;

        [HideInInspector] public Vector2 normalLabelPosition;
        [HideInInspector] public Vector3 normalLabelScale;
    }

    // =========================================================
    // TABS
    // =========================================================

    [Header("Tabs")]
    [SerializeField] private TabData shop;
    [SerializeField] private TabData league;
    [SerializeField] private TabData home;
    [SerializeField] private TabData collection;
    [SerializeField] private TabData kingdom;

    // =========================================================
    // BACKGROUND
    // =========================================================

    [Header("Background")]
    [SerializeField] private Sprite normalBackgroundSprite;
    [SerializeField] private Sprite selectedBackgroundSprite;

    // =========================================================
    // CURRENT TAB
    // =========================================================

    [Header("Current Tab")]
    [Tooltip("Shop=0, League=1, Home=2, Collection=3, Kingdom=4")]
    [SerializeField] private int currentTabIndex = 2;

    // =========================================================
    // TAB WIDTH
    // =========================================================

    [Header("Tab Width")]

    // Kích thước cuối cùng của tab selected
    [SerializeField] private float selectedWidth = 336f;

    // Chỉ pop rất nhẹ và nhanh
    [SerializeField] private float selectedPopWidth = 344f;

    [SerializeField] private float fallbackTotalWidth = 1080f;

    // =========================================================
    // ICON
    // =========================================================

    [Header("Icon")]

    // Vị trí cuối cùng
    [SerializeField] private float iconLift = 40f;

    // Icon nhảy cao hơn một chút lúc pop
    [SerializeField] private float iconPopLift = 55f;

    // Scale cuối cùng
    [SerializeField] private float selectedIconScale = 1.18f;

    // Scale lớn nhất lúc pop
    [SerializeField] private float iconPopScale = 1.32f;

    // Scale lúc bắt đầu
    [SerializeField] private float iconStartScale = 0.94f;

    // =========================================================
    // TEXT
    // =========================================================

    [Header("Text")]

    [SerializeField] private float selectedTextLift = 15f;

    [SerializeField] private float selectedTextScale = 1.08f;

    [SerializeField] private float textPopScale = 1.14f;

    [SerializeField] private float textStartScale = 0.88f;

    [SerializeField] private float textStartOffsetY = -8f;

    // =========================================================
    // TIMING
    // =========================================================

    [Header("Animation Timing")]

    // Tab chỉ pop ngang rất nhanh
    [SerializeField] private float tabPopOutDuration = 0.055f;
    [SerializeField] private float tabPopBackDuration = 0.075f;

    // Icon + text
    [SerializeField] private float iconPopDuration = 0.11f;
    [SerializeField] private float iconSettleDuration = 0.10f;

    [Range(0f, 1f)]
    [SerializeField] private float iconSwapPoint = 0.25f;

    // =========================================================

    private TabData[] tabs;

    private Coroutine entranceRoutine;

    private bool initialized;
    private bool isOpening;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();

        RefreshTabInteraction();

        if (!Application.isPlaying)
        {
            SetFinalStateImmediate();
            return;
        }

        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
        }

        entranceRoutine =
            StartCoroutine(PlaySelectedEntrance());
    }

    private void OnDisable()
    {
        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

        isOpening = false;
    }

    // =========================================================
    // INITIALIZE
    // =========================================================

    private void Initialize()
    {
        if (initialized)
            return;

        tabs = new TabData[]
        {
            shop,
            league,
            home,
            collection,
            kingdom
        };

        for (int i = 0; i < tabs.Length; i++)
        {
            TabData tab = tabs[i];

            if (tab == null || tab.root == null)
                continue;

            tab.layout =
                tab.root.GetComponent<LayoutElement>();

            // CanvasGroup của toàn tab
            tab.tabCanvasGroup =
                tab.root.GetComponent<CanvasGroup>();

            if (tab.tabCanvasGroup == null)
            {
                tab.tabCanvasGroup =
                    tab.root.AddComponent<CanvasGroup>();
            }

            if (tab.background == null)
            {
                tab.background =
                    tab.root.GetComponent<Image>();
            }

            // ICON
            if (tab.icon != null)
            {
                tab.iconRect =
                    tab.icon.rectTransform;

                tab.normalIconPosition =
                    tab.iconRect.anchoredPosition;

                tab.normalIconScale =
                    tab.iconRect.localScale;
            }

            // TEXT
            if (tab.label != null)
            {
                tab.labelRect =
                    tab.label.rectTransform;

                tab.normalLabelPosition =
                    tab.labelRect.anchoredPosition;

                tab.normalLabelScale =
                    tab.labelRect.localScale;

                tab.labelGroup =
                    tab.label.GetComponent<CanvasGroup>();

                if (tab.labelGroup == null)
                {
                    tab.labelGroup =
                        tab.label.gameObject
                            .AddComponent<CanvasGroup>();
                }
            }
        }

        initialized = true;
    }

    // =========================================================
    // INTERACTION
    // =========================================================

    private void RefreshTabInteraction()
    {
        if (tabs == null)
            return;

        for (int i = 0; i < tabs.Length; i++)
        {
            TabData tab = tabs[i];

            if (tab == null ||
                tab.tabCanvasGroup == null)
                continue;

            bool selected =
                i == currentTabIndex;

            // Selected tab không nhận click
            tab.tabCanvasGroup.blocksRaycasts =
                !selected;

            tab.tabCanvasGroup.interactable =
                !selected;
        }
    }

    // =========================================================
    // WIDTH
    // =========================================================

    private float GetTotalWidth()
    {
        RectTransform rect =
            transform as RectTransform;

        if (rect != null &&
            rect.rect.width > 0.01f)
        {
            return rect.rect.width;
        }

        return fallbackTotalWidth;
    }

    private float GetOtherWidth(
        float selectedCurrentWidth)
    {
        int otherCount =
            tabs.Length - 1;

        if (otherCount <= 0)
            return 0f;

        return
            (GetTotalWidth() - selectedCurrentWidth) /
            otherCount;
    }

    // =========================================================
    // PREPARE
    // =========================================================

    private void PrepareEntrance()
    {
        // QUAN TRỌNG:
        // WIDTH được set NGAY lập tức về trạng thái selected.
        // Không còn animation từ normal width -> selected width.

        float otherWidth =
            GetOtherWidth(selectedWidth);

        for (int i = 0; i < tabs.Length; i++)
        {
            TabData tab = tabs[i];

            if (tab == null)
                continue;

            bool selected =
                i == currentTabIndex;

            // =============================================
            // WIDTH - SNAP NGAY
            // =============================================

            if (tab.layout != null)
            {
                tab.layout.preferredWidth =
                    selected
                        ? selectedWidth
                        : otherWidth;
            }

            // =============================================
            // BACKGROUND
            // =============================================

            if (tab.background != null)
            {
                tab.background.sprite =
                    selected
                        ? selectedBackgroundSprite
                        : normalBackgroundSprite;
            }

            // =============================================
            // ICON
            // =============================================

            if (tab.icon != null &&
                tab.iconRect != null)
            {
                if (selected)
                {
                    if (tab.normalIcon != null)
                    {
                        tab.icon.sprite =
                            tab.normalIcon;
                    }

                    tab.iconRect.anchoredPosition =
                        tab.normalIconPosition +
                        Vector2.up * iconLift;

                    tab.iconRect.localScale =
                        tab.normalIconScale *
                        iconStartScale;
                }
                else
                {
                    if (tab.normalIcon != null)
                    {
                        tab.icon.sprite =
                            tab.normalIcon;
                    }

                    tab.iconRect.anchoredPosition =
                        tab.normalIconPosition;

                    tab.iconRect.localScale =
                        tab.normalIconScale;
                }

                tab.iconRect.localRotation =
                    Quaternion.identity;
            }

            // =============================================
            // TEXT
            // =============================================

            if (tab.labelGroup != null)
            {
                tab.labelGroup.alpha =
                    selected ? 0f : 0f;
            }

            if (tab.labelRect != null)
            {
                if (selected)
                {
                    Vector2 finalPos =
                        tab.normalLabelPosition +
                        Vector2.up *
                        selectedTextLift;

                    tab.labelRect.anchoredPosition =
                        finalPos +
                        Vector2.up *
                        textStartOffsetY;

                    tab.labelRect.localScale =
                        tab.normalLabelScale *
                        textStartScale;
                }
                else
                {
                    tab.labelRect.anchoredPosition =
                        tab.normalLabelPosition;

                    tab.labelRect.localScale =
                        tab.normalLabelScale;
                }
            }
        }

        ForceLayout();
    }

    // =========================================================
    // ENTRANCE
    // =========================================================

    private IEnumerator PlaySelectedEntrance()
    {
        if (currentTabIndex < 0 ||
            currentTabIndex >= tabs.Length)
        {
            yield break;
        }

        TabData selectedTab =
            tabs[currentTabIndex];

        if (selectedTab == null)
            yield break;

        RefreshTabInteraction();

        // Tab lập tức về đúng layout
        PrepareEntrance();

        // Cho Unity rebuild layout trước
        yield return null;

        // Chạy animation nhỏ
        Coroutine widthRoutine =
            StartCoroutine(
                PlayTabWidthPop(selectedTab)
            );

        Coroutine iconRoutine =
            StartCoroutine(
                PlayIconAndTextPop(selectedTab)
            );

        // Chờ cả animation xong
        if (widthRoutine != null)
            yield return widthRoutine;

        if (iconRoutine != null)
            yield return iconRoutine;

        SetFinalStateImmediate();

        RefreshTabInteraction();

        entranceRoutine = null;
    }

    // =========================================================
    // QUICK TAB WIDTH POP
    // =========================================================

    private IEnumerator PlayTabWidthPop(
        TabData selectedTab)
    {
        if (selectedTab == null ||
            selectedTab.layout == null)
        {
            yield break;
        }

        // -----------------------------------------------------
        // 336 -> 344
        // cực nhanh
        // -----------------------------------------------------

        float elapsed = 0f;

        while (elapsed < tabPopOutDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    Mathf.Max(
                        tabPopOutDuration,
                        0.001f
                    )
                );

            float ease =
                EaseOutCubic(t);

            float currentSelectedWidth =
                Mathf.Lerp(
                    selectedWidth,
                    selectedPopWidth,
                    ease
                );

            ApplyWidths(
                currentSelectedWidth
            );

            yield return null;
        }

        // -----------------------------------------------------
        // 344 -> 336
        // settle nhanh
        // -----------------------------------------------------

        elapsed = 0f;

        while (elapsed < tabPopBackDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    Mathf.Max(
                        tabPopBackDuration,
                        0.001f
                    )
                );

            float ease =
                SmoothStep(t);

            float currentSelectedWidth =
                Mathf.Lerp(
                    selectedPopWidth,
                    selectedWidth,
                    ease
                );

            ApplyWidths(
                currentSelectedWidth
            );

            yield return null;
        }

        ApplyWidths(selectedWidth);
    }

    // =========================================================
    // APPLY WIDTH
    // =========================================================

    private void ApplyWidths(
        float currentSelectedWidth)
    {
        float otherWidth =
            GetOtherWidth(
                currentSelectedWidth
            );

        for (int i = 0; i < tabs.Length; i++)
        {
            TabData tab = tabs[i];

            if (tab == null ||
                tab.layout == null)
                continue;

            tab.layout.preferredWidth =
                i == currentTabIndex
                    ? currentSelectedWidth
                    : otherWidth;
        }

        ForceLayout();
    }

    // =========================================================
    // ICON + TEXT
    // =========================================================

    private IEnumerator PlayIconAndTextPop(
        TabData selectedTab)
    {
        bool iconSwapped = false;

        Vector2 iconStartPos =
            selectedTab.normalIconPosition +
            Vector2.up * iconLift;

        Vector2 iconPopPos =
            selectedTab.normalIconPosition +
            Vector2.up * iconPopLift;

        Vector2 textFinalPos =
            selectedTab.normalLabelPosition +
            Vector2.up * selectedTextLift;

        Vector2 textStartPos =
            textFinalPos +
            Vector2.up * textStartOffsetY;

        // =====================================================
        // POP
        // =====================================================

        float elapsed = 0f;

        while (elapsed < iconPopDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    Mathf.Max(
                        iconPopDuration,
                        0.001f
                    )
                );

            float ease =
                EaseOutCubic(t);

            // ICON POSITION
            if (selectedTab.iconRect != null)
            {
                selectedTab.iconRect.anchoredPosition =
                    Vector2.Lerp(
                        iconStartPos,
                        iconPopPos,
                        ease
                    );

                selectedTab.iconRect.localScale =
                    Vector3.Lerp(
                        selectedTab.normalIconScale *
                        iconStartScale,
                        selectedTab.normalIconScale *
                        iconPopScale,
                        ease
                    );
            }

            // SWAP ICON
            if (!iconSwapped &&
                t >= iconSwapPoint)
            {
                iconSwapped = true;

                if (selectedTab.icon != null &&
                    selectedTab.selectedIcon != null)
                {
                    selectedTab.icon.sprite =
                        selectedTab.selectedIcon;
                }
            }

            // TEXT
            if (selectedTab.labelGroup != null)
            {
                selectedTab.labelGroup.alpha =
                    ease;
            }

            if (selectedTab.labelRect != null)
            {
                selectedTab.labelRect.anchoredPosition =
                    Vector2.Lerp(
                        textStartPos,
                        textFinalPos,
                        ease
                    );

                selectedTab.labelRect.localScale =
                    Vector3.Lerp(
                        selectedTab.normalLabelScale *
                        textStartScale,
                        selectedTab.normalLabelScale *
                        textPopScale,
                        ease
                    );
            }

            yield return null;
        }

        // =====================================================
        // SETTLE
        // =====================================================

        elapsed = 0f;

        while (elapsed < iconSettleDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    Mathf.Max(
                        iconSettleDuration,
                        0.001f
                    )
                );

            float ease =
                SmoothStep(t);

            // ICON
            if (selectedTab.iconRect != null)
            {
                Vector2 finalIconPos =
                    selectedTab.normalIconPosition +
                    Vector2.up * iconLift;

                selectedTab.iconRect.anchoredPosition =
                    Vector2.Lerp(
                        iconPopPos,
                        finalIconPos,
                        ease
                    );

                selectedTab.iconRect.localScale =
                    Vector3.Lerp(
                        selectedTab.normalIconScale *
                        iconPopScale,
                        selectedTab.normalIconScale *
                        selectedIconScale,
                        ease
                    );
            }

            // TEXT
            if (selectedTab.labelGroup != null)
            {
                selectedTab.labelGroup.alpha = 1f;
            }

            if (selectedTab.labelRect != null)
            {
                selectedTab.labelRect.anchoredPosition =
                    textFinalPos;

                selectedTab.labelRect.localScale =
                    Vector3.Lerp(
                        selectedTab.normalLabelScale *
                        textPopScale,
                        selectedTab.normalLabelScale *
                        selectedTextScale,
                        ease
                    );
            }

            yield return null;
        }
    }

    // =========================================================
    // FINAL STATE
    // =========================================================

    private void SetFinalStateImmediate()
    {
        float otherWidth =
            GetOtherWidth(selectedWidth);

        for (int i = 0; i < tabs.Length; i++)
        {
            TabData tab = tabs[i];

            if (tab == null)
                continue;

            bool selected =
                i == currentTabIndex;

            // WIDTH
            if (tab.layout != null)
            {
                tab.layout.preferredWidth =
                    selected
                        ? selectedWidth
                        : otherWidth;
            }

            // BACKGROUND
            if (tab.background != null)
            {
                tab.background.sprite =
                    selected
                        ? selectedBackgroundSprite
                        : normalBackgroundSprite;
            }

            // ICON
            if (tab.icon != null &&
                tab.iconRect != null)
            {
                if (selected)
                {
                    if (tab.selectedIcon != null)
                    {
                        tab.icon.sprite =
                            tab.selectedIcon;
                    }

                    tab.iconRect.anchoredPosition =
                        tab.normalIconPosition +
                        Vector2.up *
                        iconLift;

                    tab.iconRect.localScale =
                        tab.normalIconScale *
                        selectedIconScale;
                }
                else
                {
                    if (tab.normalIcon != null)
                    {
                        tab.icon.sprite =
                            tab.normalIcon;
                    }

                    tab.iconRect.anchoredPosition =
                        tab.normalIconPosition;

                    tab.iconRect.localScale =
                        tab.normalIconScale;
                }

                tab.iconRect.localRotation =
                    Quaternion.identity;
            }

            // TEXT
            if (tab.labelGroup != null)
            {
                tab.labelGroup.alpha =
                    selected ? 1f : 0f;
            }

            if (tab.labelRect != null)
            {
                tab.labelRect.anchoredPosition =
                    selected
                        ? tab.normalLabelPosition +
                          Vector2.up *
                          selectedTextLift
                        : tab.normalLabelPosition;

                tab.labelRect.localScale =
                    selected
                        ? tab.normalLabelScale *
                          selectedTextScale
                        : tab.normalLabelScale;
            }
        }

        ForceLayout();

        RefreshTabInteraction();
    }

    // =========================================================
    // CLICK
    // =========================================================

    private void OpenTab(int index)
    {
        // Đang mở popup -> chặn spam
        if (isOpening)
            return;

        // Đang ở tab này -> không cho bấm lại
        if (index == currentTabIndex)
            return;

        isOpening = true;

        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        OpenPopup(index);
    }

    // =========================================================
    // POPUP
    // =========================================================

    private void OpenPopup(int index)
    {
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
                PopupController.Instance
                    .Show<PopupHome>(
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

    // =========================================================
    // BUTTON EVENTS
    // =========================================================

    public void OnClickShop()
    {
        OpenTab(0);
    }

    public void OnClickLeague()
    {
        OpenTab(1);
    }

    public void OnClickHome()
    {
        OpenTab(2);
    }

    public void OnClickCollection()
    {
        OpenTab(3);
    }

    public void OnClickKingdom()
    {
        OpenTab(4);
    }

    // =========================================================
    // EASING
    // =========================================================

    private float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);

        return
            1f -
            Mathf.Pow(
                1f - t,
                3f
            );
    }

    private float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);

        return
            t *
            t *
            (3f - 2f * t);
    }

    // =========================================================
    // LAYOUT
    // =========================================================

    private void ForceLayout()
    {
        RectTransform rect =
            transform as RectTransform;

        if (rect != null)
        {
            LayoutRebuilder
                .ForceRebuildLayoutImmediate(
                    rect
                );
        }
    }
}
