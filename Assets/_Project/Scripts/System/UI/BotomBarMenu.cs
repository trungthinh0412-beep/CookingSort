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
    [Tooltip("Shop=0, League=1, Home=2, Collection=3")]
    [SerializeField] private int currentTabIndex = 2;

    // =========================================================
    // TAB WIDTH
    // =========================================================

    [Header("Tab Width")]
    [SerializeField] private float selectedWidth = 336f;
    [SerializeField] private float widthOvershootValue = 350f;
    [SerializeField] private float widthOvershootTime = 0.1f;
    [SerializeField] private float normalWidth = 260f;
    [SerializeField] private float widthSettleTime = 0.15f;

    // =========================================================
    // ICON 
    // =========================================================

    [Header("Selected Icon")]
    [SerializeField] private float selectedIconYOffset = 69f;
    [SerializeField] private float selectedIconScale = 1.35f;
    [SerializeField] private float iconOvershootScale = 1.55f;
    [SerializeField] private float iconOvershootTime = 0.15f;
    [SerializeField] private float iconSettleTime = 0.1f;

    // =========================================================
    // LABEL 
    // =========================================================

    [Header("Selected Label")]
    [SerializeField] private float selectedLabelYOffset = 5f;
    [SerializeField] private float selectedLabelScale = 1.0f;
    [SerializeField] private float labelFadeTime = 0.1f;

    // =========================================================
    // GLOBAL
    // =========================================================

    [Header("Visual Speed Global")]
    [SerializeField] private float deselectTime = 0.12f;
    [SerializeField] private float interactionCooldown = 0.3f; // Khóa UI khi dang anim

    private TabData[] _tabs;
    private Coroutine[] _tabCoroutines;
    private float _lastInteractionTime;

    private void Awake()
    {
        _tabs = new TabData[]
        {
            shop,
            league,
            home,
            collection
        };

        _tabCoroutines = new Coroutine[_tabs.Length];
        
        InitTabsData();
    }

    private void Start()
    {
        ForceTabImmediate(currentTabIndex);
    }
    
    // ============================================================
    // ON ENABLE HOOK
    // ============================================================

    private void OnEnable()
    {
        SetupHooks();
        UpdateSelectedTabToMatchPopup();
    }
    
    private void OnDisable()
    {
        RemoveHooks();
    }
    
    private void SetupHooks()
    {
        //LevelStart += OnHideTabsUI;
    }
    
    private void RemoveHooks()
    {
        //LevelStart -= OnHideTabsUI;
    }
    
    private void OnHideTabsUI(GameObject obj)
    {
        gameObject.SetActive(false);
    }

    private void UpdateSelectedTabToMatchPopup()
    {
        if (PopupController.Instance == null)
            return;
            
        int targetIndex = PopupController.Instance.GetCurrentMainTabIndex();
        
        if (targetIndex >= 0 && targetIndex != currentTabIndex && targetIndex < _tabs.Length)
        {
            OpenTab(targetIndex);
        }
    }


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void InitTabsData()
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i].root == null) continue;

            _tabs[i].layout = _tabs[i]
                .root.GetComponent<LayoutElement>();

            _tabs[i].iconRect = _tabs[i]
                .icon.GetComponent<RectTransform>();

            _tabs[i].labelRect = _tabs[i]
                .label.GetComponent<RectTransform>();


            _tabs[i].labelGroup = _tabs[i]
                .label.GetComponent<CanvasGroup>();

            if (_tabs[i].labelGroup == null)
            {
                _tabs[i].labelGroup = _tabs[i]
                    .label.gameObject.AddComponent<CanvasGroup>();
            }

            _tabs[i].tabCanvasGroup = _tabs[i]
                .root.GetComponent<CanvasGroup>();

            if (_tabs[i].tabCanvasGroup == null)
            {
                _tabs[i].tabCanvasGroup = _tabs[i]
                    .root.gameObject.AddComponent<CanvasGroup>();
            }


            _tabs[i].normalIconPosition = _tabs[i].iconRect.anchoredPosition;
            _tabs[i].normalIconScale = _tabs[i].iconRect.localScale;

            _tabs[i].normalLabelPosition = _tabs[i].labelRect.anchoredPosition;
            _tabs[i].normalLabelScale = _tabs[i].labelRect.localScale;
        }
    }

    // =========================================================
    // FORCE (START)
    // =========================================================

    private void ForceTabImmediate(int index)
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i].root == null) continue;

            if (_tabCoroutines[i] != null)
            {
                StopCoroutine(_tabCoroutines[i]);
                _tabCoroutines[i] = null;
            }

            bool isSel = (i == index);

            if (_tabs[i].background != null)
            {
                _tabs[i].background.sprite = isSel
                    ? selectedBackgroundSprite
                    : normalBackgroundSprite;
            }


            if (_tabs[i].icon != null)
            {
                _tabs[i].icon.sprite = isSel
                    ? _tabs[i].selectedIcon
                    : _tabs[i].normalIcon;
            }

            // Width
            _tabs[i].layout.preferredWidth = isSel
                ? selectedWidth
                : normalWidth;


            Vector2 iPos = _tabs[i].normalIconPosition;
            if (isSel) iPos.y += selectedIconYOffset;
            _tabs[i].iconRect.anchoredPosition = iPos;
            _tabs[i].iconRect.localScale = isSel
                ? Vector3.one * selectedIconScale
                : _tabs[i].normalIconScale;


            Vector2 lPos = _tabs[i].normalLabelPosition;
            if (isSel) lPos.y += selectedLabelYOffset;
            _tabs[i].labelRect.anchoredPosition = lPos;
            _tabs[i].labelRect.localScale = isSel
                ? Vector3.one * selectedLabelScale
                : _tabs[i].normalLabelScale;


            _tabs[i].labelGroup.alpha = isSel ? 1f : 0f;
        }

        RebuildLayoutRoot();
    }


    // =========================================================
    // OPEN TAB -> COROUTINE MAIN
    // =========================================================

    public void OpenTab(int index)
    {
        if (index == currentTabIndex) return;
        if (Time.time - _lastInteractionTime < interactionCooldown) return;
        _lastInteractionTime = Time.time;

        int old = currentTabIndex;
        currentTabIndex = index;


        if (old >= 0 && old < _tabs.Length)
        {
            if (_tabCoroutines[old] != null)
                StopCoroutine(_tabCoroutines[old]);

            _tabCoroutines[old] = StartCoroutine(
                DeselectRoutine(_tabs[old])
            );
        }

        if (_tabCoroutines[index] != null)
            StopCoroutine(_tabCoroutines[index]);
            
        _tabCoroutines[index] = StartCoroutine(
            SelectRoutine(_tabs[index])
        );

        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );


        switch (index)
        {
            case 0:
                PopupController.Instance
                    .Show<PopupLeague>(
                        PopupAnimation.None
                    );
                break;

            case 1:
                PopupController.Instance
                    .Show<PopupHome>(
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


    private void RebuildLayoutRoot()
    {
        
    }


    // =========================================================
    // DESELECT ROUTINE
    // =========================================================

    private IEnumerator DeselectRoutine(TabData tab)
    {
        if (tab.background != null)
            tab.background.sprite = normalBackgroundSprite;


        float startW = tab.layout.preferredWidth;
        float startIY = tab.iconRect.anchoredPosition.y;
        Vector3 startISc = tab.iconRect.localScale;
        float startLY = tab.labelRect.anchoredPosition.y;
        float startLA = tab.labelGroup.alpha;

        float dur = deselectTime;
        float el = 0f;

        while (el < dur)
        {
            el += Time.deltaTime;
            float t = SmoothStep(el / dur);

            tab.layout.preferredWidth = Mathf.Lerp(
                startW,
                normalWidth,
                t
            );

            Vector2 iPos = tab.iconRect.anchoredPosition;
            iPos.y = Mathf.Lerp(
                startIY,
                tab.normalIconPosition.y,
                t
            );
            tab.iconRect.anchoredPosition = iPos;
            tab.iconRect.localScale = Vector3.LerpUnclamped(
                startISc,
                tab.normalIconScale,
                t
            );

            Vector2 lPos = tab.labelRect.anchoredPosition;
            lPos.y = Mathf.Lerp(
                startLY,
                tab.normalLabelPosition.y,
                t
            );
            tab.labelRect.anchoredPosition = lPos;
            tab.labelGroup.alpha = Mathf.Lerp(
                startLA,
                0f,
                t
            );

            RebuildLayoutRoot();
            yield return null;
        }

        tab.layout.preferredWidth = normalWidth;

        Vector2 fi = tab.iconRect.anchoredPosition;
        fi.y = tab.normalIconPosition.y;
        tab.iconRect.anchoredPosition = fi;
        tab.iconRect.localScale = tab.normalIconScale;
        if (tab.icon != null) tab.icon.sprite = tab.normalIcon;


        Vector2 fl = tab.labelRect.anchoredPosition;
        fl.y = tab.normalLabelPosition.y;
        tab.labelRect.anchoredPosition = fl;
        tab.labelRect.localScale = tab.normalLabelScale;
        tab.labelGroup.alpha = 0f;
    }


    // =========================================================
    // SELECT ROUTINE (OVERSHOOT)
    // =========================================================

    private IEnumerator SelectRoutine(TabData tab)
    {
        if (tab.background != null)
            tab.background.sprite = selectedBackgroundSprite;


        if (tab.icon != null)
            tab.icon.sprite = tab.normalIcon;

        // --- PHASE 1: WIDTH OVERSHOOT ---
        float startW = tab.layout.preferredWidth;
        float startIY = tab.iconRect.anchoredPosition.y;
        Vector3 startISc = tab.iconRect.localScale;

        float targetIY = tab.normalIconPosition.y +
                         selectedIconYOffset;
        Vector3 peakISc = Vector3.one *
                          iconOvershootScale;

        float el = 0f;
        while (el < widthOvershootTime)
        {
            el += Time.deltaTime;
            float t = EaseOutCubic(el / widthOvershootTime);

            tab.layout.preferredWidth = Mathf.Lerp(
                startW,
                widthOvershootValue,
                t
            );

            Vector2 iPos = tab.iconRect.anchoredPosition;
            iPos.y = Mathf.Lerp(
                startIY,
                targetIY,
                t
            );
            tab.iconRect.anchoredPosition = iPos;


            float tIcon = EaseOutCubic(el / iconOvershootTime);
            tab.iconRect.localScale = Vector3.LerpUnclamped(
                startISc,
                peakISc,
                tIcon
            );

            RebuildLayoutRoot();
            yield return null;
        }


        if (tab.icon != null)
            tab.icon.sprite = tab.selectedIcon;


        // --- PHASE 2: WIDTH SETTLE & ICON SETTLE & LABEL ---
        el = 0f;
        float finalIY = targetIY;
        Vector3 finalISc = Vector3.one * selectedIconScale;

        float startLY = tab.labelRect.anchoredPosition.y;
        float finalLY = tab.normalLabelPosition.y +
                        selectedLabelYOffset;
        Vector3 finalLSc = Vector3.one * selectedLabelScale;

        while (el < widthSettleTime)
        {
            el += Time.deltaTime;


            float tW = SmoothStep(el / widthSettleTime);
            tab.layout.preferredWidth = Mathf.Lerp(
                widthOvershootValue,
                selectedWidth,
                tW
            );


            float tIcon = SmoothStep(
                Mathf.Clamp01(el / iconSettleTime)
            );
            tab.iconRect.localScale = Vector3.LerpUnclamped(
                peakISc,
                finalISc,
                tIcon
            );

            Vector2 iPos = tab.iconRect.anchoredPosition;
            iPos.y = Mathf.Lerp(
                iPos.y,
                finalIY,
                tIcon
            );
            tab.iconRect.anchoredPosition = iPos;


            float tLabel = SmoothStep(
                Mathf.Clamp01(el / labelFadeTime)
            );
            tab.labelGroup.alpha = tLabel;

            Vector2 lPos = tab.labelRect.anchoredPosition;
            lPos.y = Mathf.Lerp(
                startLY,
                finalLY,
                tLabel
            );
            tab.labelRect.anchoredPosition = lPos;
            tab.labelRect.localScale = Vector3.LerpUnclamped(
                tab.normalLabelScale,
                finalLSc,
                tLabel
            );

            RebuildLayoutRoot();
            yield return null;
        }

        tab.layout.preferredWidth = selectedWidth;

        Vector2 fi = tab.iconRect.anchoredPosition;
        fi.y = finalIY;
        tab.iconRect.anchoredPosition = fi;
        tab.iconRect.localScale = finalISc;


        Vector2 fl = tab.labelRect.anchoredPosition;
        fl.y = finalLY;
        tab.labelRect.anchoredPosition = fl;
        tab.labelRect.localScale = finalLSc;
        tab.labelGroup.alpha = 1f;

        RebuildLayoutRoot();
    }
}
