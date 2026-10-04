using UnityEngine;

public class PopupShop : Popup
{
    [Header("Pack ScrollRect References")]
    [SerializeField] private UnityEngine.UI.ScrollRect offerPackScrollRect;
    [SerializeField] private UnityEngine.UI.ScrollRect treasuryScrollRect;
    [SerializeField] private UnityEngine.UI.ScrollRect royalPackScrollRect;

    private RectTransform _offerPackPanel;
    private RectTransform _treasuryPanel;
    private RectTransform _royalPackPanel;
    private Transform _offerPackTab;
    private Transform _treasuryTab;
    private Transform _royalPackTab;
    private bool _packTabsConfigured;
    [Header("References")]
    [SerializeField] private GameObject contentRemoveAds;


    protected virtual void Awake()
    {
        SetupShopPackTabs();
        CheckRemoveAds();

        Observer.PurchasePackComplete += CheckRemoveAds;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetupShopPackTabs();
        ShowDefaultShopPack();
    }

    protected override void AfterShown()
    {
        base.AfterShown();
        SetupShopPackTabs();
        ShowDefaultShopPack();
    }

    public void ShowGoldPackages()
    {
        SetupShopPackTabs();

        if (_offerPackPanel != null && _offerPackTab != null)
            ShowShopPack(_offerPackPanel, _offerPackTab);
        else
            ResetShopScrollPosition();

        ScrollToGoldPackages();
    }

    private void ScrollToGoldPackages()
    {
        UnityEngine.UI.ScrollRect scrollRect = offerPackScrollRect;
        if (scrollRect == null || scrollRect.content == null)
            return;

        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();
        scrollRect.velocity = Vector2.zero;
        scrollRect.horizontalNormalizedPosition = 0f;
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private void ShowDefaultShopPack()
    {
        if (!_packTabsConfigured ||
            _offerPackPanel == null ||
            _offerPackTab == null)
        {
            ResetShopScrollPosition();
            return;
        }

        ShowShopPack(_offerPackPanel, _offerPackTab);
    }

    private void ResetShopScrollPosition()
    {
        ResetShopScrollPosition(_offerPackPanel ?? container);
    }

    private void ResetShopScrollPosition(RectTransform panel)
    {
        UnityEngine.UI.ScrollRect scrollRect = GetScrollRect(panel);

        if (scrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();
        scrollRect.horizontalNormalizedPosition = 0f;
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private UnityEngine.UI.ScrollRect GetScrollRect(RectTransform panel)
    {
        if (panel == _treasuryPanel)
            return treasuryScrollRect;
        if (panel == _royalPackPanel)
            return royalPackScrollRect;
        return offerPackScrollRect;
    }

    protected virtual void OnDestroy()
    {
        Observer.PurchasePackComplete -= CheckRemoveAds;
    }

    private void SetupShopPackTabs()
    {
        if (_packTabsConfigured)
            return;

        Transform tabBar = FindShopTabBar();
        if (tabBar == null)
            return;

        _offerPackPanel = FindRootPanel("OffersPanel") ?? container;
        _treasuryPanel = FindRootPanel("TreasuryPanel");
        _royalPackPanel = FindRootPanel("RoyalPackagesPanel");

        // Prefer the existing hierarchy names, but fall back to the three
        // tab slots in their visual order. This allows the designer to rename
        // the tab objects and labels freely in the Inspector.
        System.Collections.Generic.List<Transform> packTabs =
            FindPackTabs(tabBar);
        _offerPackTab = FindTab(tabBar, "OffersTab", "OfferPack") ??
            (packTabs.Count > 0 ? packTabs[0] : null);
        _royalPackTab = FindTab(tabBar, "RoyalPackagesTab", "RoyalPack") ??
            (packTabs.Count > 1 ? packTabs[1] : null);
        _treasuryTab = FindTab(tabBar, "TreasuryTab", "Treasury") ??
            (packTabs.Count > 2 ? packTabs[2] : null);

        // Do not mark the setup complete until every pack has both a panel
        // and a tab. This lets a late-created/instantiated hierarchy retry on
        // the next enable instead of silently leaving dead buttons.
        if (_offerPackPanel == null || _treasuryPanel == null ||
            _royalPackPanel == null || _offerPackTab == null ||
            _treasuryTab == null || _royalPackTab == null)
            return;

        AddShopPackTabListeners(_offerPackTab, _offerPackPanel);
        AddShopPackTabListeners(_treasuryTab, _treasuryPanel);
        AddShopPackTabListeners(_royalPackTab, _royalPackPanel);

        _packTabsConfigured = true;
        ShowShopPack(_offerPackPanel, _offerPackTab);
    }

    private static System.Collections.Generic.List<Transform> FindPackTabs(
        Transform tabBar)
    {
        var tabs = new System.Collections.Generic.List<Transform>();
        if (tabBar == null)
            return tabs;

        foreach (Transform candidate in tabBar.GetComponentsInChildren<Transform>(true))
        {
            if (candidate == tabBar ||
                candidate.GetComponent<CustomButton>() != null ||
                candidate.GetComponent<UnityEngine.UI.Button>() != null)
                continue;

            // A pack tab is the parent of the selected/unselected button
            // states. The check deliberately does not inspect names.
            for (int index = 0; index < candidate.childCount; index++)
            {
                Transform child = candidate.GetChild(index);
                if (child.GetComponent<CustomButton>() != null ||
                    child.GetComponent<UnityEngine.UI.Button>() != null)
                {
                    tabs.Add(candidate);
                    break;
                }
            }
        }

        return tabs;
    }

    private Transform FindShopTabBar()
    {
        foreach (Transform child in transform)
        {
            if (IsShopTabBar(child))
                return child;
        }

        // If the tab bar itself has been renamed, identify it by structure:
        // it contains the three tab-slot parents rather than relying on a
        // hard-coded GameObject name.
        foreach (Transform child in transform)
        {
            if (FindPackTabs(child).Count == 3)
                return child;
        }

        return null;
    }

    private RectTransform FindRootPanel(string panelName)
    {
        Transform panel = transform.Find(panelName);
        return panel as RectTransform;
    }

    private static Transform FindTab(
        Transform tabBar,
        string renamedName,
        string originalName)
    {
        // In the prefab the three tab roots are nested inside the tab bar's
        // background Image, so Transform.Find (direct children only) cannot
        // resolve them. Search the complete hierarchy instead.
        Transform tab = FindDescendant(tabBar, renamedName);
        return tab != null ? tab : FindDescendant(tabBar, originalName);
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate != root &&
                candidate.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private void AddShopPackTabListeners(
        Transform tab,
        RectTransform panel)
    {
        if (tab == null || panel == null)
            return;

        CustomButton[] buttons =
            tab.GetComponentsInChildren<CustomButton>(true);

        foreach (CustomButton button in buttons)
        {
            button.Click.AddListener(() =>
                ShowShopPack(panel, tab));
        }

        // Support a standard Unity Button as well, so the runtime wiring is
        // not dependent on which button component the prefab uses.
        if (buttons.Length == 0)
        {
            UnityEngine.UI.Button[] unityButtons =
                tab.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (UnityEngine.UI.Button button in unityButtons)
            {
                button.onClick.AddListener(() =>
                    ShowShopPack(panel, tab));
            }
        }
    }

    private void ShowShopPack(
        RectTransform selectedPanel,
        Transform selectedTab)
    {
        SetPackPanelActive(_offerPackPanel, selectedPanel == _offerPackPanel);
        SetPackPanelActive(_treasuryPanel, selectedPanel == _treasuryPanel);
        SetPackPanelActive(_royalPackPanel, selectedPanel == _royalPackPanel);

        SetPackTabSelected(_offerPackTab, selectedTab == _offerPackTab,
            "OffersTab");
        SetPackTabSelected(_treasuryTab, selectedTab == _treasuryTab,
            "TreasuryTab");
        SetPackTabSelected(_royalPackTab, selectedTab == _royalPackTab,
            "RoyalPackagesTab");

        ResetShopScrollPosition(selectedPanel);
    }

    private static void SetPackPanelActive(
        RectTransform panel,
        bool active)
    {
        if (panel == null)
            return;

        if (panel.gameObject.activeSelf != active)
            panel.gameObject.SetActive(active);
    }

    private static void SetPackTabSelected(
        Transform tab,
        bool selected,
        string statePrefix)
    {
        if (tab == null)
            return;

        Transform selectedState = FindDescendant(tab, statePrefix + "_Selected");
        Transform unselectedState = FindDescendant(tab, statePrefix + "_Unselected");

        // Keep compatibility with the original prefab state names. This also
        // makes the tab switch work if the runtime rename has not happened yet.
        if (selectedState == null || unselectedState == null)
        {
            string legacyPrefix = statePrefix == "OffersTab"
                ? "FriendTabButton"
                : statePrefix == "TreasuryTab"
                    ? "LocalTabButton"
                    : "WorldTabButton";
            selectedState ??= FindDescendant(tab, legacyPrefix + "_Selected");
            unselectedState ??= FindDescendant(tab, legacyPrefix + "_Unselected");
        }

        // Last fallback: the selected and unselected state objects are the
        // first two button children in the tab. Their names can therefore be
        // changed freely without breaking the visual state.
        if (selectedState == null || unselectedState == null)
        {
            var stateObjects = new System.Collections.Generic.List<Transform>();
            for (int index = 0; index < tab.childCount; index++)
            {
                Transform child = tab.GetChild(index);
                if (child.GetComponent<CustomButton>() != null ||
                    child.GetComponent<UnityEngine.UI.Button>() != null)
                    stateObjects.Add(child);
            }

            selectedState ??= stateObjects.Count > 0 ? stateObjects[0] : null;
            unselectedState ??= stateObjects.Count > 1 ? stateObjects[1] : null;
        }

        if (selectedState != null)
            selectedState.gameObject.SetActive(selected);
        if (unselectedState != null)
            unselectedState.gameObject.SetActive(!selected);
    }

    // Legacy runtime layout builder. The prefab now owns this hierarchy.
#if false
    private void SetupShopScrollView()
    {
        if (container == null || _shopScrollRect != null)
            return;

        var scrollItems = new System.Collections.Generic.List<RectTransform>();
        RectTransform topbar = null;
        RectTransform tabBar = null;

        for (int index = 0; index < container.childCount; index++)
        {
            RectTransform child = container.GetChild(index) as RectTransform;
            if (child == null)
                continue;

            if (child.name.Equals("Topbar", System.StringComparison.OrdinalIgnoreCase) ||
                child.name.Equals("ShopTopBar", System.StringComparison.OrdinalIgnoreCase))
                topbar = child;
            else if (IsShopTabBar(child))
                tabBar = child;
            else
                scrollItems.Add(child);
        }

        if (topbar == null)
            topbar = FindRootTopbar();

        // The category tabs are intentionally kept outside the scroll view.
        // Support both the original Container layout and a root-level tab bar.
        if (tabBar == null)
        {
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform child = transform.GetChild(index);
                if (child != container && IsShopTabBar(child))
                {
                    tabBar = child as RectTransform;
                    break;
                }
            }
        }

        GameObject scrollViewObject = new GameObject(
            "ShopScrollView",
            typeof(RectTransform),
            typeof(UnityEngine.UI.ScrollRect));
        scrollViewObject.layer = container.gameObject.layer;
        RectTransform scrollView = scrollViewObject.GetComponent<RectTransform>();
        scrollView.SetParent(container, false);
        StretchToParent(scrollView);
        scrollView.SetAsFirstSibling();

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.RectMask2D));
        viewportObject.layer = container.gameObject.layer;
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(scrollView, false);
        StretchToParent(viewport);
        UnityEngine.UI.Image viewportImage =
            viewportObject.GetComponent<UnityEngine.UI.Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        Canvas.ForceUpdateCanvases();
        SetViewportBelowTopbar(viewport, scrollView, topbar);
        SetViewportAboveTabBar(viewport, scrollView, tabBar);

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.layer = container.gameObject.layer;
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 2400f);

        foreach (RectTransform item in scrollItems)
            AttachItemToContent(item, content);

        Canvas.ForceUpdateCanvases();
        ResizeContentToFit(content, viewport, tabBar);

        _shopScrollRect = scrollViewObject.GetComponent<UnityEngine.UI.ScrollRect>();
        _shopScrollRect.viewport = viewport;
        _shopScrollRect.content = content;
        _shopScrollRect.horizontal = false;
        _shopScrollRect.vertical = true;
        _shopScrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Elastic;
        _shopScrollRect.elasticity = .1f;
        _shopScrollRect.inertia = true;
        _shopScrollRect.decelerationRate = .135f;
        _shopScrollRect.scrollSensitivity = 30f;
        _packScrollRects[container] = _shopScrollRect;
        Canvas.ForceUpdateCanvases();
        _shopScrollRect.verticalNormalizedPosition = 1f;

        if (tabBar != null)
            tabBar.SetAsLastSibling();

        if (topbar != null)
        {
            topbar.name = "ShopTopBar";
            topbar.SetAsLastSibling();
        }

        RenameShopItems(scrollItems);
        // Tab names and labels are designer-owned. Do not rename anything at
        // runtime; pack tabs are resolved by name when available and by their
        // visual order as a fallback.
    }

    private void SetupAdditionalPackScrollViews()
    {
        RectTransform tabBar = FindShopTabBar() as RectTransform;
        RectTransform topbar = FindRootTopbar();

        EnsurePackScrollView(FindRootPanel("TreasuryPanel"), tabBar, topbar);
        EnsurePackScrollView(FindRootPanel("RoyalPackagesPanel"), tabBar, topbar);
    }

    private void EnsurePackScrollView(
        RectTransform panel,
        RectTransform tabBar,
        RectTransform topbar)
    {
        if (panel == null || panel == container ||
            _packScrollRects.ContainsKey(panel))
            return;

        UnityEngine.UI.ScrollRect existing =
            panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        if (existing != null)
        {
            _packScrollRects[panel] = existing;
            return;
        }

        var scrollItems = new System.Collections.Generic.List<RectTransform>();
        for (int index = 0; index < panel.childCount; index++)
        {
            RectTransform child = panel.GetChild(index) as RectTransform;
            if (child != null)
                scrollItems.Add(child);
        }

        GameObject scrollViewObject = new GameObject(
            "ShopScrollView",
            typeof(RectTransform),
            typeof(UnityEngine.UI.ScrollRect));
        scrollViewObject.layer = panel.gameObject.layer;
        RectTransform scrollView = scrollViewObject.GetComponent<RectTransform>();
        scrollView.SetParent(panel, false);
        StretchToParent(scrollView);
        scrollView.SetAsFirstSibling();

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.RectMask2D));
        viewportObject.layer = panel.gameObject.layer;
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(scrollView, false);
        StretchToParent(viewport);
        UnityEngine.UI.Image viewportImage =
            viewportObject.GetComponent<UnityEngine.UI.Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        Canvas.ForceUpdateCanvases();
        SetViewportBelowTopbar(viewport, scrollView, topbar);
        SetViewportAboveTabBar(viewport, scrollView, tabBar);

        GameObject contentObject = new GameObject(
            "Content",
            typeof(RectTransform));
        contentObject.layer = panel.gameObject.layer;
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(.5f, 1f);
        content.anchorMax = new Vector2(.5f, 1f);
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 1200f);

        foreach (RectTransform item in scrollItems)
            AttachItemToContent(item, content);

        Canvas.ForceUpdateCanvases();
        ResizeContentToFit(content, viewport, tabBar);

        UnityEngine.UI.ScrollRect scrollRect =
            scrollViewObject.GetComponent<UnityEngine.UI.ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = .1f;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = .135f;
        scrollRect.scrollSensitivity = 30f;
        _packScrollRects[panel] = scrollRect;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private RectTransform FindRootTopbar()
    {
        foreach (Transform child in transform)
        {
            if (child == container || IsShopTabBar(child))
                continue;

            if (child.name.Equals("Topbar", System.StringComparison.OrdinalIgnoreCase) ||
                child.name.Equals("ShopTopBar", System.StringComparison.OrdinalIgnoreCase))
                return child as RectTransform;
        }

        return null;
    }

#endif

    private static bool IsShopTabBar(Transform candidate)
    {
        if (candidate == null)
            return false;

        if (candidate.name.Equals("ShopTabBar", System.StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Equals("RankingTabBar", System.StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Equals("ShoppingTabBar", System.StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Equals("ShopingTabBar", System.StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Equals("ShopCategoryTabs", System.StringComparison.OrdinalIgnoreCase))
            return true;

        return candidate.Find("FriendTab") != null &&
            candidate.Find("LocalTab") != null &&
            candidate.Find("WorldTab") != null;
    }

    // Legacy layout helpers are intentionally excluded: edit the ScrollRect,
    // Viewport and Content directly in PopupShop.prefab instead.
#if false
    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void SetViewportAboveTabBar(
        RectTransform viewport,
        RectTransform scrollView,
        RectTransform tabBar)
    {
        if (tabBar == null)
            return;

        float tabBarTop = float.NegativeInfinity;
        var corners = new Vector3[4];

        // Use the complete tab hierarchy so Friend, World and Local tabs all
        // get the same clearance from the scroll mask.
        foreach (RectTransform tabElement in
                 tabBar.GetComponentsInChildren<RectTransform>(true))
        {
            tabElement.GetWorldCorners(corners);
            for (int index = 0; index < corners.Length; index++)
                tabBarTop = Mathf.Max(
                    tabBarTop,
                    scrollView.InverseTransformPoint(corners[index]).y);
        }

        if (float.IsNegativeInfinity(tabBarTop))
            return;

        float bottomInset = tabBarTop - scrollView.rect.yMin + TabBarMaskGap;
        float maxInset = Mathf.Max(0f, scrollView.rect.height - 1f);

        viewport.offsetMin = new Vector2(
            viewport.offsetMin.x,
            Mathf.Clamp(bottomInset, 0f, maxInset));
    }

    private static void SetViewportBelowTopbar(
        RectTransform viewport,
        RectTransform scrollView,
        RectTransform topbar)
    {
        if (topbar == null)
            return;

        float topbarBottom = float.PositiveInfinity;
        var corners = new Vector3[4];
        topbar.GetWorldCorners(corners);
        for (int index = 0; index < corners.Length; index++)
        {
            topbarBottom = Mathf.Min(
                topbarBottom,
                scrollView.InverseTransformPoint(corners[index]).y);
        }

        if (float.IsPositiveInfinity(topbarBottom))
            return;

        // Keep the scrollable content flush with the bottom edge of the
        // fixed top bar. The lower tab bar keeps its own separate clearance.
        float topInset = scrollView.rect.yMax - topbarBottom + TopbarMaskGap;
        float maxInset = Mathf.Max(0f, scrollView.rect.height - 1f);
        viewport.offsetMax = new Vector2(
            viewport.offsetMax.x,
            -Mathf.Clamp(topInset, 0f, maxInset));
    }

    private static void AttachItemToContent(
        RectTransform item,
        RectTransform content)
    {
        Vector3 worldPosition = item.position;
        item.SetParent(content, true);

        // All shop cards use a fixed top anchor so changing Content height
        // cannot move them around their parent's center.
        item.anchorMin = new Vector2(.5f, 1f);
        item.anchorMax = new Vector2(.5f, 1f);
        item.position = worldPosition;
    }

    private static void ResizeContentToFit(
        RectTransform content,
        RectTransform viewport,
        RectTransform tabBar)
    {
        float lowestY = 0f;
        var corners = new Vector3[4];

        foreach (RectTransform child in content.GetComponentsInChildren<RectTransform>(true))
        {
            if (child == content)
                continue;

            child.GetWorldCorners(corners);
            for (int index = 0; index < corners.Length; index++)
                lowestY = Mathf.Min(lowestY, content.InverseTransformPoint(corners[index]).y);
        }

        float viewportHeight = viewport.rect.height;
        // Extra room keeps the final shop card clear of the fixed tab bar.
        float bottomPadding = 600f;
        if (tabBar != null)
            bottomPadding = Mathf.Max(bottomPadding, tabBar.rect.height + 80f);

        // Leave enough scrollable space for the last item to pass the fixed
        // category bar instead of stopping directly behind it.
        float requiredHeight = Mathf.Max(viewportHeight, -lowestY + bottomPadding);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, requiredHeight);
    }

    private static void RenameShopItems(
        System.Collections.Generic.List<RectTransform> items)
    {
        int itemIndex = 1;
        foreach (RectTransform item in items)
        {
            string title = null;
            foreach (global::TMPro.TMP_Text text in
                     item.GetComponentsInChildren<global::TMPro.TMP_Text>(true))
            {
                string value = text.text.Trim();
                if (string.IsNullOrEmpty(value) || value == "New Text" ||
                    float.TryParse(value, out _))
                    continue;

                title = value.Replace("<color=#484848>", string.Empty)
                    .Replace("</color>", string.Empty)
                    .Trim();
                break;
            }

            string safeTitle = string.IsNullOrEmpty(title)
                ? $"Item_{itemIndex}"
                : title.Replace(" ", string.Empty)
                    .Replace(".", string.Empty);
            item.name = $"ShopItem_{safeTitle}";
            itemIndex++;
        }
    }

#endif

    private void CheckRemoveAds()
    {
        if (contentRemoveAds == null)
            return;

        contentRemoveAds.SetActive(
            !Data.PlayerData.IsRemoveAds
        );
    }


    public void OnClickPurchaseRemoveAds()
    {
        PurchasePack(PackName.RemoveAds);
    }

    public void OnClickPurchaseSuperDeal()
    {
        PurchasePack(PackName.SuperDeal);
    }

    public void OnClickPurchaseStarterBenefits()
    {
        PurchasePack(PackName.StarterBenefits);
    }

    public void OnClickPurchaseVIPPack()
    {
        PurchasePack(PackName.VIPPack);
    }

    public void OnClickPurchaseProPack()
    {
        PurchasePack(PackName.ProPack);
    }

    public void OnClickPurchaseChampionPack()
    {
        PurchasePack(PackName.ChampionPack);
    }

    public void OnClickPurchaseMasterPack()
    {
        PurchasePack(PackName.MasterPack);
    }

    public void OnClickPurchaseCardMasterPack()
    {
        PurchasePack(PackName.CardMasterPack);
    }

    // Keep legacy UnityEvent bindings working on older prefab instances.
    public void OnClickPurchaseSmallBundle()
    {
        OnClickPurchaseSuperDeal();
    }

    public void OnClickPurchaseBigBundle()
    {
        OnClickPurchaseStarterBenefits();
    }

    public void OnClickPurchaseGold1()
    {
        PurchasePack(PackName.Gold1);
    }

    public void OnClickPurchaseGold2()
    {
        PurchasePack(PackName.Gold2);
    }

    public void OnClickPurchaseGold3()
    {
        PurchasePack(PackName.Gold3);
    }

    public void OnClickPurchaseGold4()
    {
        PurchasePack(PackName.Gold4);
    }

    public void OnClickPurchaseGold5()
    {
        PurchasePack(PackName.Gold5);
    }

    public void OnClickPurchaseGold6()
    {
        PurchasePack(PackName.Gold6);
    }

    private void PurchasePack(PackName packName)
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        IAPController.Instance.PurchasePack(packName);
    }
}
