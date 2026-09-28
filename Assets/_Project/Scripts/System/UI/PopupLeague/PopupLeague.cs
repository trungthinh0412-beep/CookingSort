using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PopupLeague : Popup
{
    private const int MaxRankingPlayerCount = 999;
    private const float RankingTabMaskGap = 12f;

    [Header("References")]

    [SerializeField] private RectTransform content;

    [Header("Ranking Tabs")]
    [SerializeField] private RectTransform localRankingScrollView;
    [SerializeField] private RectTransform worldRankingScrollView;
    [SerializeField] private RectTransform friendRankingScrollView;
    [SerializeField] private CustomButton localTabButton;
    [SerializeField] private CustomButton worldTabButton;
    [SerializeField] private CustomButton friendTabButton;
    [SerializeField] private Sprite selectedTabSprite;
    [SerializeField] private Sprite unselectedTabSprite;
    [SerializeField] private float tabSpacing = 20f;

    [Header("Sticky Player Rank")]
    [SerializeField] private RectTransform localPlayerRankItem;
    [SerializeField] private RectTransform worldPlayerRankItem;
    [SerializeField, Min(1)] private int visibleTopRankCount = 5;
    [SerializeField] private float stickyPlayerScale = 1.07f;
    [SerializeField] private float normalPlayerScale = 1f;
    [SerializeField] private float stickyStartScale = 1f;
    [SerializeField] private float stickyShowDuration = 0.18f;
    [SerializeField] private float stickyBottomOffset = 8f;

    [Header("Test Ranking Data")]
    [SerializeField] private ProfileConfig profileConfig;
    [SerializeField] private RectTransform playerNameLayoutTemplate;
    [SerializeField] private RectTransform teamNameLayoutTemplate;
    [SerializeField, Range(6, MaxRankingPlayerCount)] private int localTestPlayerCount = MaxRankingPlayerCount;
    [SerializeField, Range(1, MaxRankingPlayerCount)] private int localTestPlayerRank = MaxRankingPlayerCount;
    [SerializeField, Range(6, MaxRankingPlayerCount)] private int worldTestPlayerCount = MaxRankingPlayerCount;
    [SerializeField, Range(1, MaxRankingPlayerCount)] private int worldTestPlayerRank = MaxRankingPlayerCount;
    [SerializeField, Min(1)] private int virtualListBufferRows = 1;

    [Header("League Score Tiers")]
    [SerializeField, Min(1)] private int highestLeagueScore = 2000;
    [SerializeField, Min(0)] private int lowestLeagueScore = 160;
    [SerializeField, Min(0)] private int purpleFlagScore = 650;
    [SerializeField, Min(0)] private int blueFlagScore = 300;
    [SerializeField] private Sprite purpleFlagSprite;
    [SerializeField] private Sprite blueFlagSprite;
    [SerializeField] private Sprite redFlagSprite;

    [Header("Rank Fade Animation")]
    [SerializeField] private float startDelay = 0.08f;
    [SerializeField] private float fadeDuration = 0.16f;
    [SerializeField] private float dominoDelay = 0.035f;
    [SerializeField] private float startScale = 0.98f;

    private readonly List<RectTransform> rankItems =
        new List<RectTransform>();

    private readonly List<CanvasGroup> rankCanvasGroups =
        new List<CanvasGroup>();

    private Coroutine animationCoroutine;
    private ScrollRect activeScrollRect;
    private RectTransform activePlayerRankItem;
    private RectTransform stickyPlayerRankItem;
    private CanvasGroup stickyPlayerCanvasGroup;
    private Coroutine stickyShowCoroutine;
    private bool isStickyPlayerShown;
    private bool testRankingsBuilt;
    private VirtualRanking localVirtualRanking;
    private VirtualRanking worldVirtualRanking;
    private bool friendRankingReferencesReady;
    private RectTransform rankingTabBar;
    private RectTransform[] rankingTabElements;
    private readonly Vector3[] rankingTabCorners = new Vector3[4];
    private Vector2 lastRankingTabPosition;
    private bool rankingViewportMaskInitialized;

    private readonly Dictionary<RectTransform, RankItemView>
        rankItemViews =
            new Dictionary<RectTransform, RankItemView>();

    private sealed class RankItemView
    {
        public CanvasGroup canvasGroup;
        public TextMeshProUGUI rankText;
        public TextMeshProUGUI playerNameText;
        public TextMeshProUGUI teamNameText;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI cupScoreText;
        public Image avatarImage;
        public Image avatarFrame;
        public Image cupScoreBadge;
        public PurpleAvatarShine purpleAvatarShine;
    }

    private sealed class VirtualRanking
    {
        public readonly List<RectTransform> featuredItems =
            new List<RectTransform>();

        public readonly List<RectTransform> pooledItems =
            new List<RectTransform>();

        public ScrollRect scrollRect;
        public RectTransform content;
        public RectTransform playerItem;
        public int playerCount;
        public int playerRank;
        public string prefix;
        public float itemHeight;
        public float itemStep;
        public int paddingTop;
        public int firstPooledRank = -1;
    }

    private static readonly string[] TestPlayerNames =
    {
        "Arthur", "Lancelot", "Guinevere", "Merlin",
        "Gawain", "Percival", "Tristan", "Galahad",
        "Morgana", "Leon", "Freya", "Rowan"
    };

    private static readonly string[] TestTeamNames =
    {
        "Royal Hearts", "Golden Crown", "Emerald Court",
        "Moon Knights", "Crystal Castle", "Lucky Cards"
    };

    private static readonly string[] LocalPlayerNames =
    {
        "Oliver", "Amelia", "Henry", "Sophia",
        "Jack", "Isla", "George", "Mia",
        "Charlie", "Ella", "Oscar", "Grace"
    };

    private static readonly string[] LocalTeamNames =
    {
        "Crown Keepers", "Royal Path", "Castle Guard",
        "Golden Table", "Kingdom Stars", "Crown Makers"
    };

    protected override void OnEnable()
    {
        base.OnEnable();

        AttachClockAnimator();

        EnsureFriendRankingReferences();
        ApplyTabSpacing();
        rankingTabBar = FindRankingTabBar();
        ApplyRankingViewportMask(true);

        AddTabClickListeners(localTabButton, OnClickLocalTab);
        AddTabClickListeners(worldTabButton, OnClickWorldTab);
        AddTabClickListeners(friendTabButton, OnClickFriendTab);

        Observer.LevelChanged += UpdatePlayerLevelTexts;
        Observer.ProfileChanged += UpdatePlayerProfileVisuals;

        BuildTestRankings();
        RegisterVirtualScrollListeners();

        UpdatePlayerLevelTexts(
            Data.PlayerData != null
                ? Data.PlayerData.CurrentLevelIndex
                : 1
        );

        // Friend is the default tab whenever League is opened.
        ShowRankingTab(
            friendRankingScrollView,
            friendTabButton,
            false
        );
    }

    private void AttachClockAnimator()
    {
        if (GetComponent<ProfileClockAnimator>() == null)
            gameObject.AddComponent<ProfileClockAnimator>();
    }

    private void EnsureFriendRankingReferences()
    {
        if (friendRankingReferencesReady)
        {
            return;
        }

        if (friendTabButton == null)
        {
            Transform friendButtonTransform =
                FindChildByName(transform, "FriendTabButton");

            if (friendButtonTransform != null)
            {
                friendTabButton =
                    friendButtonTransform.GetComponent<CustomButton>();
            }
        }

        if (friendRankingScrollView == null &&
            localRankingScrollView != null)
        {
            Transform existingFriendView =
                FindChildByName(transform, "FriendRankingScrollView");

            if (existingFriendView != null)
            {
                friendRankingScrollView =
                    existingFriendView as RectTransform;
            }
        }

        if (friendRankingScrollView == null &&
            localRankingScrollView != null)
        {
            RectTransform parent =
                localRankingScrollView.parent as RectTransform;

            if (parent != null)
            {
                GameObject friendViewObject =
                    new GameObject(
                        "FriendRankingScrollView",
                        typeof(RectTransform)
                    );

                friendRankingScrollView =
                    friendViewObject.GetComponent<RectTransform>();

                friendRankingScrollView.SetParent(parent, false);
                friendRankingScrollView.anchorMin =
                    localRankingScrollView.anchorMin;
                friendRankingScrollView.anchorMax =
                    localRankingScrollView.anchorMax;
                friendRankingScrollView.pivot =
                    localRankingScrollView.pivot;
                friendRankingScrollView.anchoredPosition =
                    localRankingScrollView.anchoredPosition;
                friendRankingScrollView.sizeDelta =
                    localRankingScrollView.sizeDelta;
                friendRankingScrollView.localRotation =
                    localRankingScrollView.localRotation;
                friendRankingScrollView.localScale =
                    localRankingScrollView.localScale;
                friendRankingScrollView.SetSiblingIndex(
                    localRankingScrollView.GetSiblingIndex()
                );
            }
        }

        if (friendRankingScrollView != null)
        {
            // This is intentionally only a RectTransform. It must remain an
            // empty Friend tab without a ScrollRect or ranking content.
            ScrollRect scrollRect =
                friendRankingScrollView.GetComponent<ScrollRect>();

            if (scrollRect != null)
            {
                scrollRect.enabled = false;
            }

            friendRankingScrollView.gameObject.SetActive(false);
        }

        friendRankingReferencesReady = true;
    }

    private void ApplyTabSpacing()
    {
        if (localTabButton == null)
        {
            return;
        }

        Transform tabBar = localTabButton.transform.parent != null
            ? localTabButton.transform.parent.parent
            : null;

        HorizontalLayoutGroup layoutGroup =
            tabBar != null
                ? tabBar.GetComponent<HorizontalLayoutGroup>()
                : null;

        if (layoutGroup != null)
        {
            layoutGroup.spacing = tabSpacing;
        }
    }

    private static Transform FindChildByName(
        Transform root,
        string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children =
            root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private void RegisterVirtualScrollListeners()
    {
        if (localVirtualRanking != null &&
            localVirtualRanking.scrollRect != null)
        {
            localVirtualRanking.scrollRect.onValueChanged
                .RemoveListener(OnLocalRankingScrolled);

            localVirtualRanking.scrollRect.onValueChanged
                .AddListener(OnLocalRankingScrolled);
        }

        if (worldVirtualRanking != null &&
            worldVirtualRanking.scrollRect != null)
        {
            worldVirtualRanking.scrollRect.onValueChanged
                .RemoveListener(OnWorldRankingScrolled);

            worldVirtualRanking.scrollRect.onValueChanged
                .AddListener(OnWorldRankingScrolled);
        }
    }

    private void UnregisterVirtualScrollListeners()
    {
        if (localVirtualRanking != null &&
            localVirtualRanking.scrollRect != null)
        {
            localVirtualRanking.scrollRect.onValueChanged
                .RemoveListener(OnLocalRankingScrolled);
        }

        if (worldVirtualRanking != null &&
            worldVirtualRanking.scrollRect != null)
        {
            worldVirtualRanking.scrollRect.onValueChanged
                .RemoveListener(OnWorldRankingScrolled);
        }
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();

        StopRankAnimation();

        CacheRankItems();
        PrepareRankItems();
    }

    protected override void AfterShown()
    {
        base.AfterShown();

        ApplyRankingViewportMask(true);

        animationCoroutine =
            StartCoroutine(
                RankAnimationRoutine()
            );
    }

    private void LateUpdate()
    {
        // RankingTabBar slides in from the bottom. Keep the viewport edge
        // aligned with it while that transition is running.
        ApplyRankingViewportMask();
    }

    private RectTransform FindRankingTabBar()
    {
        Transform tabBar = FindChildByName(transform, "RankingTabBar");
        if (tabBar == null && localTabButton != null)
        {
            Transform tabParent = localTabButton.transform.parent;
            tabBar = tabParent != null ? tabParent.parent : null;
        }

        rankingTabElements = tabBar != null
            ? tabBar.GetComponentsInChildren<RectTransform>(true)
            : null;

        return tabBar as RectTransform;
    }

    private void ApplyRankingViewportMask(bool force = false)
    {
        if (rankingTabBar == null)
            return;

        Vector2 currentPosition = rankingTabBar.anchoredPosition;
        if (!force && rankingViewportMaskInitialized &&
            currentPosition == lastRankingTabPosition)
            return;

        lastRankingTabPosition = currentPosition;
        rankingViewportMaskInitialized = true;

        SetRankingViewportBottom(localRankingScrollView);
        SetRankingViewportBottom(worldRankingScrollView);
    }

    private void SetRankingViewportBottom(RectTransform scrollView)
    {
        if (scrollView == null)
            return;

        ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
        if (scrollRect == null || scrollRect.viewport == null)
            return;

        RectTransform viewport = scrollRect.viewport;

        float tabBarTop = float.NegativeInfinity;

        if (rankingTabElements == null)
            rankingTabElements =
                rankingTabBar.GetComponentsInChildren<RectTransform>(true);

        foreach (RectTransform tabElement in rankingTabElements)
        {
            tabElement.GetWorldCorners(rankingTabCorners);
            for (int index = 0; index < rankingTabCorners.Length; index++)
                tabBarTop = Mathf.Max(
                    tabBarTop,
                    scrollView.InverseTransformPoint(rankingTabCorners[index]).y);
        }

        if (float.IsNegativeInfinity(tabBarTop))
            return;

        float bottomInset = tabBarTop - scrollView.rect.yMin + RankingTabMaskGap;
        float maxInset = Mathf.Max(0f, scrollView.rect.height - 1f);
        Vector2 offsetMin = viewport.offsetMin;
        offsetMin.y = Mathf.Clamp(bottomInset, 0f, maxInset);
        viewport.offsetMin = offsetMin;
    }

    private RankItemView GetRankItemView(
        RectTransform rankItem)
    {
        if (rankItem == null)
            return null;

        if (rankItemViews.TryGetValue(
                rankItem,
                out RankItemView cachedView))
        {
            return cachedView;
        }

        RankItemView view =
            new RankItemView
            {
                canvasGroup =
                    rankItem.GetComponent<CanvasGroup>()
            };

        if (view.canvasGroup == null)
        {
            view.canvasGroup =
                rankItem.gameObject.AddComponent<CanvasGroup>();
        }

        TextMeshProUGUI[] texts =
            rankItem.GetComponentsInChildren<TextMeshProUGUI>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].raycastTarget = false;

            switch (texts[i].gameObject.name)
            {
                case "RankText":
                    view.rankText = texts[i];
                    break;
                case "PlayerNameText":
                    view.playerNameText = texts[i];
                    break;
                case "TeamNameText":
                    view.teamNameText = texts[i];
                    break;
                case "LevelText":
                    view.levelText = texts[i];
                    break;
                case "CupScoreText":
                    view.cupScoreText = texts[i];
                    break;
            }
        }

        Image[] images =
            rankItem.GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            images[i].raycastTarget = false;

            switch (images[i].gameObject.name)
            {
                case "AvatarImage":
                    view.avatarImage = images[i];
                    break;
                case "AvatarFrame":
                    view.avatarFrame = images[i];
                    break;
                case "CupScoreBadge":
                    view.cupScoreBadge = images[i];
                    break;
            }
        }

        if (view.avatarFrame != null)
        {
            view.purpleAvatarShine =
                view.avatarFrame.GetComponent<PurpleAvatarShine>();

            if (view.purpleAvatarShine == null)
            {
                view.purpleAvatarShine =
                    view.avatarFrame.gameObject
                        .AddComponent<PurpleAvatarShine>();
            }

            // Clip the shine by the alpha of the avatar frame. This keeps
            // the diagonal light on the frame border instead of drawing it
            // across the avatar image underneath.
            Mask avatarFrameMask =
                view.avatarFrame.GetComponent<Mask>();

            if (avatarFrameMask == null)
            {
                avatarFrameMask =
                    view.avatarFrame.gameObject.AddComponent<Mask>();
            }

            avatarFrameMask.showMaskGraphic = true;
        }

        ApplyTextLayout(
            view.playerNameText,
            playerNameLayoutTemplate
        );

        ApplyTextLayout(
            view.teamNameText,
            teamNameLayoutTemplate
        );

        rankItemViews.Add(rankItem, view);
        return view;
    }

    private void CacheRankItems(
        RectTransform viewport = null)
    {
        rankItems.Clear();
        rankCanvasGroups.Clear();

        if (content == null)
            return;

        for (int i = 0; i < content.childCount; i++)
        {
            RectTransform item =
                content.GetChild(i) as RectTransform;

            if (item == null)
                continue;

            if (!item.gameObject.activeSelf)
                continue;

            RankItemView itemView =
                GetRankItemView(item);

            if (itemView == null ||
                itemView.canvasGroup == null)
            {
                continue;
            }

            if (viewport != null &&
                !IsVisibleInsideViewport(item, viewport))
            {
                itemView.canvasGroup.alpha = 1f;
                item.localScale = Vector3.one;
                continue;
            }

            rankItems.Add(item);
            rankCanvasGroups.Add(itemView.canvasGroup);
        }
    }

    private static bool IsVisibleInsideViewport(
        RectTransform item,
        RectTransform viewport)
    {
        Bounds bounds =
            RectTransformUtility
                .CalculateRelativeRectTransformBounds(
                    viewport,
                    item
                );

        Rect rect = viewport.rect;

        return bounds.max.y > rect.yMin &&
               bounds.min.y < rect.yMax;
    }

    private void PrepareRankItems()
    {
        for (int i = 0; i < rankItems.Count; i++)
        {
            RectTransform item =
                rankItems[i];

            CanvasGroup canvasGroup =
                rankCanvasGroups[i];

            if (item == null ||
                canvasGroup == null)
            {
                continue;
            }

            canvasGroup.alpha = 0f;

            item.localScale =
                Vector3.one * startScale;
        }
    }

    private IEnumerator RankAnimationRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                startDelay
            );
        }

        float elapsed = 0f;

        int itemCount =
            rankItems.Count;

        float totalDuration =
            fadeDuration +
            dominoDelay *
            Mathf.Max(
                0,
                Mathf.Min(itemCount, visibleTopRankCount) - 1
            );

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < itemCount; i++)
            {
                if (rankItems[i] == null ||
                    rankCanvasGroups[i] == null)
                {
                    continue;
                }

                float itemStartTime =
                    Mathf.Min(
                        i,
                        visibleTopRankCount - 1
                    ) * dominoDelay;

                float itemTime =
                    elapsed - itemStartTime;

                float t =
                    Mathf.Clamp01(
                        itemTime /
                        Mathf.Max(
                            fadeDuration,
                            0.001f
                        )
                    );

                float eased =
                    1f -
                    Mathf.Pow(
                        1f - t,
                        3f
                    );

                rankCanvasGroups[i].alpha =
                    eased;

                float targetScale =
                    rankItems[i] == activePlayerRankItem
                        ? normalPlayerScale
                        : 1f;

                rankItems[i].localScale =
                    Vector3.Lerp(
                        Vector3.one * startScale,
                        Vector3.one * targetScale,
                        eased
                    );
            }

            UpdateStickyPlayerRank();

            yield return null;
        }

        for (int i = 0; i < itemCount; i++)
        {
            if (rankItems[i] != null)
            {
                float targetScale =
                    rankItems[i] == activePlayerRankItem
                        ? normalPlayerScale
                        : 1f;

                rankItems[i].localScale =
                    Vector3.one * targetScale;
            }

            if (rankCanvasGroups[i] != null)
            {
                rankCanvasGroups[i].alpha =
                    1f;
            }
        }

        UpdateStickyPlayerRank();

        animationCoroutine = null;
    }

    private void StopRankAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(
                animationCoroutine
            );

            animationCoroutine = null;
        }
    }

    private void BuildTestRankings()
    {
        if (testRankingsBuilt)
            return;

        ScrollRect localScrollRect =
            localRankingScrollView != null
                ? localRankingScrollView.GetComponent<ScrollRect>()
                : null;

        ScrollRect worldScrollRect =
            worldRankingScrollView != null
                ? worldRankingScrollView.GetComponent<ScrollRect>()
                : null;

        localVirtualRanking = BuildVirtualRanking(
            localScrollRect,
            localPlayerRankItem,
            localTestPlayerCount,
            localTestPlayerRank,
            "Local"
        );

        worldVirtualRanking = BuildVirtualRanking(
            worldScrollRect,
            worldPlayerRankItem,
            worldTestPlayerCount,
            worldTestPlayerRank,
            "World"
        );

        testRankingsBuilt = true;
    }

    private VirtualRanking BuildVirtualRanking(
        ScrollRect scrollRect,
        RectTransform playerRankItem,
        int playerCount,
        int playerRank,
        string rankingPrefix)
    {
        if (scrollRect == null ||
            scrollRect.content == null ||
            playerRankItem == null)
        {
            return null;
        }

        RectTransform rankingContent =
            scrollRect.content;

        int safePlayerCount =
            Mathf.Clamp(
                playerCount,
                visibleTopRankCount + 1,
                MaxRankingPlayerCount
            );

        int safePlayerRank =
            Mathf.Clamp(playerRank, 1, safePlayerCount);

        RectTransform cloneTemplate =
            FindNormalRankTemplate(
                rankingContent,
                playerRankItem
            );

        if (cloneTemplate == null)
            return null;

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            rankingContent
        );

        VerticalLayoutGroup layoutGroup =
            rankingContent.GetComponent<VerticalLayoutGroup>();

        ContentSizeFitter contentSizeFitter =
            rankingContent.GetComponent<ContentSizeFitter>();

        float itemHeight = cloneTemplate.rect.height;

        if (itemHeight <= 0f)
        {
            itemHeight =
                LayoutUtility.GetPreferredHeight(cloneTemplate);
        }

        itemHeight = Mathf.Max(1f, itemHeight);

        float spacing = layoutGroup != null
            ? layoutGroup.spacing
            : 0f;

        int paddingTop = layoutGroup != null
            ? layoutGroup.padding.top
            : 0;

        int paddingBottom = layoutGroup != null
            ? layoutGroup.padding.bottom
            : 0;

        float itemStep =
            Mathf.Max(1f, itemHeight + spacing);

        float viewportHeight =
            scrollRect.viewport != null
                ? scrollRect.viewport.rect.height
                : scrollRect.GetComponent<RectTransform>().rect.height;

        int visibleRowCount =
            Mathf.CeilToInt(
                Mathf.Max(1f, viewportHeight) / itemStep
            ) + 1;

        int poolSize =
            Mathf.Min(
                safePlayerCount,
                visibleRowCount +
                Mathf.Max(1, virtualListBufferRows) * 2
            );

        VirtualRanking ranking =
            new VirtualRanking
            {
                scrollRect = scrollRect,
                content = rankingContent,
                playerItem = playerRankItem,
                playerCount = safePlayerCount,
                playerRank = safePlayerRank,
                prefix = rankingPrefix,
                itemHeight = itemHeight,
                itemStep = itemStep,
                paddingTop = paddingTop
            };

        for (int i = 0; i < rankingContent.childCount; i++)
        {
            RectTransform item =
                rankingContent.GetChild(i) as RectTransform;

            if (item == null || item == playerRankItem)
                continue;

            if (i < 3)
            {
                ranking.featuredItems.Add(item);
                continue;
            }

            if (ranking.pooledItems.Count < poolSize)
            {
                ranking.pooledItems.Add(item);
            }
            else
            {
                item.gameObject.SetActive(false);
            }
        }

        while (ranking.pooledItems.Count < poolSize)
        {
            RectTransform clone =
                Instantiate(cloneTemplate, rankingContent);

            clone.localScale = Vector3.one;
            clone.gameObject.SetActive(true);
            ranking.pooledItems.Add(clone);
        }

        if (layoutGroup != null)
            layoutGroup.enabled = false;

        if (contentSizeFitter != null)
            contentSizeFitter.enabled = false;

        float contentHeight =
            paddingTop +
            paddingBottom +
            safePlayerCount * itemHeight +
            Mathf.Max(0, safePlayerCount - 1) * spacing;

        Vector2 contentSize = rankingContent.sizeDelta;
        contentSize.y = contentHeight;
        rankingContent.sizeDelta = contentSize;

        scrollRect.verticalNormalizedPosition = 1f;
        RefreshVirtualRanking(ranking, true);

        return ranking;
    }

    private static RectTransform FindNormalRankTemplate(
        RectTransform rankingContent,
        RectTransform playerRankItem)
    {
        for (int i = rankingContent.childCount - 1; i >= 0; i--)
        {
            RectTransform item =
                rankingContent.GetChild(i) as RectTransform;

            if (item != null && item != playerRankItem)
                return item;
        }

        return null;
    }

    private void OnLocalRankingScrolled(Vector2 _)
    {
        RefreshVirtualRanking(localVirtualRanking, false);

        if (localVirtualRanking != null &&
            activeScrollRect == localVirtualRanking.scrollRect)
        {
            UpdateStickyPlayerRank();
        }
    }

    private void OnWorldRankingScrolled(Vector2 _)
    {
        RefreshVirtualRanking(worldVirtualRanking, false);

        if (worldVirtualRanking != null &&
            activeScrollRect == worldVirtualRanking.scrollRect)
        {
            UpdateStickyPlayerRank();
        }
    }

    private void RefreshVirtualRanking(
        VirtualRanking ranking,
        bool force)
    {
        if (ranking == null ||
            ranking.content == null ||
            ranking.pooledItems.Count == 0)
        {
            return;
        }

        float scrollOffset =
            Mathf.Max(0f, ranking.content.anchoredPosition.y);

        int firstRank =
            Mathf.FloorToInt(
                (scrollOffset - ranking.paddingTop) /
                ranking.itemStep
            ) + 1 - Mathf.Max(1, virtualListBufferRows);

        int highestFirstRank =
            Mathf.Max(
                1,
                ranking.playerCount -
                ranking.pooledItems.Count + 1
            );

        firstRank =
            Mathf.Clamp(firstRank, 1, highestFirstRank);

        if (!force &&
            firstRank == ranking.firstPooledRank)
        {
            return;
        }

        ranking.firstPooledRank = firstRank;

        int avatarCount =
            GetProfileSpriteCount(ProfileType.Avatar);

        int frameCount =
            GetProfileSpriteCount(ProfileType.AvatarFrame);

        int lastPooledRank =
            firstRank + ranking.pooledItems.Count - 1;

        bool playerIsPooled =
            ranking.playerRank >= firstRank &&
            ranking.playerRank <= lastPooledRank;

        for (int i = 0;
             i < ranking.featuredItems.Count;
             i++)
        {
            RectTransform featuredItem =
                ranking.featuredItems[i];

            if (featuredItem == null)
                continue;

            int featuredRank = i + 1;

            bool shouldShow =
                featuredRank >= firstRank &&
                featuredRank <= lastPooledRank &&
                featuredRank <= ranking.playerCount &&
                featuredRank != ranking.playerRank;

            featuredItem.gameObject.SetActive(shouldShow);

            if (!shouldShow)
                continue;

            PositionVirtualRankItem(
                featuredItem,
                ranking,
                featuredRank
            );

            ConfigureTestRankItem(
                featuredItem,
                featuredRank,
                ranking.playerCount,
                ranking.prefix,
                avatarCount,
                frameCount,
                false
            );
        }

        for (int i = 0;
             i < ranking.pooledItems.Count;
             i++)
        {
            RectTransform item = ranking.pooledItems[i];

            if (item == null)
                continue;

            int rank = firstRank + i;
            bool isValidRank = rank <= ranking.playerCount;
            bool isFeaturedRank =
                rank >= 1 &&
                rank <= ranking.featuredItems.Count;

            bool isPlayerRank =
                isValidRank && rank == ranking.playerRank;

            item.gameObject.SetActive(
                isValidRank &&
                !isFeaturedRank &&
                !isPlayerRank
            );

            if (!isValidRank ||
                isFeaturedRank ||
                isPlayerRank)
            {
                continue;
            }

            PositionVirtualRankItem(item, ranking, rank);

            ConfigureTestRankItem(
                item,
                rank,
                ranking.playerCount,
                ranking.prefix,
                avatarCount,
                frameCount,
                false
            );
        }

        if (ranking.playerItem == null)
            return;

        ranking.playerItem.gameObject.SetActive(playerIsPooled);

        if (playerIsPooled)
        {
            PositionVirtualRankItem(
                ranking.playerItem,
                ranking,
                ranking.playerRank
            );
        }

        ConfigureTestRankItem(
            ranking.playerItem,
            ranking.playerRank,
            ranking.playerCount,
            ranking.prefix,
            avatarCount,
            frameCount,
            true
        );
    }

    private static void PositionVirtualRankItem(
        RectTransform item,
        VirtualRanking ranking,
        int rank)
    {
        item.anchorMin = new Vector2(0.5f, 1f);
        item.anchorMax = new Vector2(0.5f, 1f);

        Vector2 position = Vector2.zero;
        position.y = -(
            ranking.paddingTop +
            (rank - 1) * ranking.itemStep +
            ranking.itemHeight * (1f - item.pivot.y)
        );

        item.anchoredPosition = position;
        item.localScale = Vector3.one;
    }

    private VirtualRanking GetVirtualRanking(
        ScrollRect scrollRect)
    {
        if (localVirtualRanking != null &&
            localVirtualRanking.scrollRect == scrollRect)
        {
            return localVirtualRanking;
        }

        if (worldVirtualRanking != null &&
            worldVirtualRanking.scrollRect == scrollRect)
        {
            return worldVirtualRanking;
        }

        return null;
    }

    private int GetProfileSpriteCount(ProfileType type)
    {
        if (profileConfig == null)
            return 0;

        ProfileUnitData data =
            profileConfig.GetProfileUnitData(type);

        return data != null && data.sprites != null
            ? data.sprites.Count
            : 0;
    }

    private void ConfigureTestRankItem(
        RectTransform rankItem,
        int rank,
        int playerCount,
        string rankingPrefix,
        int avatarCount,
        int frameCount,
        bool isPlayer)
    {
        RankItemView view = GetRankItemView(rankItem);

        if (view == null)
            return;

        string playerName =
            PlayerData.NormalizeName(
                Data.PlayerData != null
                    ? Data.PlayerData.CurrentName
                    : "Player"
            );

        if (view.rankText != null)
            view.rankText.text = rank.ToString();

        if (view.playerNameText != null)
        {
            view.playerNameText.text = isPlayer
                ? playerName
                : GetTestPlayerName(rankingPrefix, rank);
        }

        if (view.teamNameText != null)
        {
            view.teamNameText.text = isPlayer
                ? "Royal Kingdom"
                : GetTestTeamName(rankingPrefix, rank);
        }

        if (view.levelText != null)
        {
            int level = isPlayer && Data.PlayerData != null
                ? Data.PlayerData.CurrentLevelIndex
                : Mathf.Max(1, 70 - rank / 2);

            view.levelText.text =
                PlayerData.FormatLevel(level);
        }

        int score = CalculateLeagueScore(rank, playerCount);

        if (view.cupScoreText != null)
            view.cupScoreText.text = score.ToString();

        ApplyLeagueFlag(view.cupScoreBadge, score);

        if (view.purpleAvatarShine != null)
        {
            view.purpleAvatarShine.SetVisible(
                score >= purpleFlagScore
            );
        }

        if (profileConfig == null)
            return;

        if (view.avatarImage != null && avatarCount > 0)
        {
            int avatarIndex = isPlayer && Data.PlayerData != null
                ? Data.PlayerData.CurrentIndexAvatar
                : (rank * 3 + (rankingPrefix == "Local" ? 5 : 0)) % avatarCount;

            view.avatarImage.sprite =
                profileConfig.GetSprite(
                    ProfileType.Avatar,
                    avatarIndex
                );

            view.avatarImage.SetNativeSize();
        }

        if (view.avatarFrame != null && frameCount > 0)
        {
            int frameIndex = isPlayer && Data.PlayerData != null
                ? Data.PlayerData.CurrentIndexFrame
                : (rank - 1 + (rankingPrefix == "Local" ? 3 : 0)) % frameCount;

            view.avatarFrame.sprite =
                profileConfig.GetSprite(
                    ProfileType.AvatarFrame,
                    frameIndex
                );
        }

        if (isPlayer)
            return;

        rankItem.name =
            $"{rankingPrefix}RankItem_{rank:000}_Runtime";
    }

    private void UpdatePlayerProfileVisuals()
    {
        if (!isActiveAndEnabled || !testRankingsBuilt)
            return;

        int avatarCount = GetProfileSpriteCount(ProfileType.Avatar);
        int frameCount = GetProfileSpriteCount(ProfileType.AvatarFrame);

        UpdatePlayerProfileVisuals(
            localVirtualRanking,
            avatarCount,
            frameCount
        );

        UpdatePlayerProfileVisuals(
            worldVirtualRanking,
            avatarCount,
            frameCount
        );
    }

    private void UpdatePlayerProfileVisuals(
        VirtualRanking ranking,
        int avatarCount,
        int frameCount)
    {
        if (ranking == null || ranking.playerItem == null)
            return;

        ConfigureTestRankItem(
            ranking.playerItem,
            ranking.playerRank,
            ranking.playerCount,
            ranking.prefix,
            avatarCount,
            frameCount,
            true
        );

        if (stickyPlayerRankItem != null &&
            activePlayerRankItem == ranking.playerItem)
        {
            ConfigureTestRankItem(
                stickyPlayerRankItem,
                ranking.playerRank,
                ranking.playerCount,
                ranking.prefix,
                avatarCount,
                frameCount,
                true
            );
        }
    }

    private static string GetTestPlayerName(
        string rankingPrefix,
        int rank)
    {
        string[] names = rankingPrefix == "Local"
            ? LocalPlayerNames
            : TestPlayerNames;

        return PlayerData.NormalizeName(
            names[(rank - 1) % names.Length]
        );
    }

    private static string GetTestTeamName(
        string rankingPrefix,
        int rank)
    {
        string[] teams = rankingPrefix == "Local"
            ? LocalTeamNames
            : TestTeamNames;

        return teams[(rank - 1) % teams.Length];
    }

    private int CalculateLeagueScore(
        int rank,
        int playerCount)
    {
        if (playerCount <= 1)
            return highestLeagueScore;

        float rankProgress =
            Mathf.Clamp01(
                (rank - 1f) /
                (playerCount - 1f)
            );

        return Mathf.RoundToInt(
            Mathf.Lerp(
                highestLeagueScore,
                lowestLeagueScore,
                rankProgress
            )
        );
    }

    private void ApplyLeagueFlag(
        Image cupScoreBadge,
        int score)
    {
        Sprite flagSprite = score >= purpleFlagScore
            ? purpleFlagSprite
            : score >= blueFlagScore
                ? blueFlagSprite
                : redFlagSprite;

        if (flagSprite == null || cupScoreBadge == null)
            return;

        cupScoreBadge.sprite = flagSprite;
    }

    private static void ApplyTextLayout(
        TextMeshProUGUI targetText,
        RectTransform layoutTemplate)
    {
        if (targetText == null ||
            layoutTemplate == null)
        {
            return;
        }

        RectTransform target =
            targetText.rectTransform;

        target.anchorMin =
            layoutTemplate.anchorMin;

        target.anchorMax =
            layoutTemplate.anchorMax;

        target.pivot =
            layoutTemplate.pivot;

        target.anchoredPosition =
            layoutTemplate.anchoredPosition;

        target.sizeDelta =
            layoutTemplate.sizeDelta;

        target.localRotation =
            layoutTemplate.localRotation;

        target.localScale =
            layoutTemplate.localScale;

        if (target.parent != null)
        {
            target.SetSiblingIndex(
                Mathf.Min(
                    layoutTemplate.GetSiblingIndex(),
                    target.parent.childCount - 1
                )
            );
        }

        TextMeshProUGUI templateText =
            layoutTemplate
                .GetComponent<TextMeshProUGUI>();

        if (templateText == null)
            return;

        targetText.enableAutoSizing =
            templateText.enableAutoSizing;

        targetText.fontSize =
            templateText.fontSize;

        targetText.fontSizeMin =
            templateText.fontSizeMin;

        targetText.fontSizeMax =
            templateText.fontSizeMax;

        targetText.alignment =
            templateText.alignment;

        targetText.overflowMode =
            templateText.overflowMode;
    }

    private void OnClickLocalTab()
    {
        ShowRankingTab(
            localRankingScrollView,
            localTabButton,
            false
        );
    }

    private void OnClickWorldTab()
    {
        ShowRankingTab(
            worldRankingScrollView,
            worldTabButton,
            false
        );
    }

    private void OnClickFriendTab()
    {
        ShowRankingTab(
            friendRankingScrollView,
            friendTabButton,
            false
        );
    }

    private void ShowRankingTab(
        RectTransform selectedScrollView,
        CustomButton selectedButton,
        bool playAnimation)
    {
        if (selectedScrollView == null)
            return;

        StopRankAnimation();

        SetScrollViewActive(
            localRankingScrollView,
            selectedScrollView == localRankingScrollView
        );

        SetScrollViewActive(
            worldRankingScrollView,
            selectedScrollView == worldRankingScrollView
        );

        SetScrollViewActive(
            friendRankingScrollView,
            selectedScrollView == friendRankingScrollView
        );

        SetTabSelected(
            localTabButton,
            selectedButton == localTabButton
        );

        SetTabSelected(
            worldTabButton,
            selectedButton == worldTabButton
        );

        SetTabSelected(
            friendTabButton,
            selectedButton == friendTabButton
        );

        ScrollRect scrollRect =
            selectedScrollView.GetComponent<ScrollRect>();

        content = scrollRect != null
            ? scrollRect.content
            : null;

        if (scrollRect != null)
        {
            RefreshVirtualRanking(
                GetVirtualRanking(scrollRect),
                true
            );
        }
        else
        {
            ReleaseStickyPlayerRank();
            content = null;
            CacheRankItems(null);
        }

        CacheRankItems(
            scrollRect != null
                ? scrollRect.viewport
                : null
        );
        if (playAnimation)
            PrepareRankItems();
        else
            ShowRankItemsImmediately();

        RectTransform playerRankItem =
            selectedScrollView == localRankingScrollView
                ? localPlayerRankItem
                : selectedScrollView == worldRankingScrollView
                    ? worldPlayerRankItem
                    : null;

        SetupStickyPlayerRank(
            scrollRect,
            playerRankItem
        );

        if (playAnimation &&
            isActiveAndEnabled)
        {
            animationCoroutine =
                StartCoroutine(
                    RankAnimationRoutine()
                );
        }
    }

    private void ShowRankItemsImmediately()
    {
        for (int i = 0; i < rankItems.Count; i++)
        {
            RectTransform item = rankItems[i];
            if (item != null)
                item.localScale = Vector3.one;

            CanvasGroup canvasGroup = rankCanvasGroups[i];
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }
    }

    private static void SetScrollViewActive(
        RectTransform scrollView,
        bool isActive)
    {
        if (scrollView != null &&
            scrollView.gameObject.activeSelf != isActive)
        {
            scrollView.gameObject.SetActive(isActive);
        }
    }

    private static void AddTabClickListeners(
        CustomButton tabButton,
        UnityAction clickAction)
    {
        if (tabButton == null || clickAction == null)
        {
            return;
        }

        CustomButton[] stateButtons =
            tabButton.transform.parent.GetComponentsInChildren<CustomButton>(true);
        HashSet<GameObject> registeredObjects =
            new HashSet<GameObject>();

        for (int i = 0; i < stateButtons.Length; i++)
        {
            if (!registeredObjects.Add(stateButtons[i].gameObject))
            {
                continue;
            }

            stateButtons[i].Click.AddListener(clickAction);
        }
    }

    private static void RemoveTabClickListeners(
        CustomButton tabButton,
        UnityAction clickAction)
    {
        if (tabButton == null || clickAction == null)
        {
            return;
        }

        CustomButton[] stateButtons =
            tabButton.transform.parent.GetComponentsInChildren<CustomButton>(true);
        HashSet<GameObject> registeredObjects =
            new HashSet<GameObject>();

        for (int i = 0; i < stateButtons.Length; i++)
        {
            if (!registeredObjects.Add(stateButtons[i].gameObject))
            {
                continue;
            }

            stateButtons[i].Click.RemoveListener(clickAction);
        }
    }

    private void SetTabSelected(
        CustomButton button,
        bool isSelected)
    {
        if (button == null)
        {
            return;
        }

        Transform tabRoot = button.transform.parent;
        string stateButtonPrefix = tabRoot.name.Replace("Tab", "TabButton");
        Transform selectedState = tabRoot.Find(stateButtonPrefix + "_Selected");
        Transform unselectedState = tabRoot.Find(stateButtonPrefix + "_Unselected");

        if (selectedState != null && unselectedState != null)
        {
            selectedState.gameObject.SetActive(isSelected);
            unselectedState.gameObject.SetActive(!isSelected);
            return;
        }

        Image buttonImage = button.GetComponent<Image>();

        if (buttonImage != null)
        {
            buttonImage.sprite = isSelected
                ? selectedTabSprite
                : unselectedTabSprite;
        }
    }

    private void SetupStickyPlayerRank(
        ScrollRect scrollRect,
        RectTransform playerRankItem)
    {
        ReleaseStickyPlayerRank();

        activeScrollRect = scrollRect;
        activePlayerRankItem = playerRankItem;

        VirtualRanking activeRanking =
            GetVirtualRanking(activeScrollRect);

        if (activeScrollRect == null ||
            activeScrollRect.viewport == null ||
            activePlayerRankItem == null ||
            activeRanking == null)
        {
            return;
        }

        activePlayerRankItem.localScale =
            Vector3.one * normalPlayerScale;

        if (activeRanking.playerRank <= visibleTopRankCount)
            return;

        stickyPlayerRankItem =
            Instantiate(
                activePlayerRankItem,
                activeScrollRect.viewport
            );

        stickyPlayerRankItem.name =
            $"{activePlayerRankItem.name}_Sticky";

        stickyPlayerRankItem.anchorMin =
            new Vector2(0.5f, 0f);

        stickyPlayerRankItem.anchorMax =
            new Vector2(0.5f, 0f);

        stickyPlayerRankItem.pivot =
            new Vector2(0.5f, 0f);

        stickyPlayerRankItem.anchoredPosition =
            new Vector2(0f, stickyBottomOffset);

        stickyPlayerRankItem.localRotation =
            Quaternion.identity;

        stickyPlayerRankItem.localScale =
            Vector3.one * stickyPlayerScale;

        stickyPlayerRankItem.SetAsLastSibling();

        stickyPlayerCanvasGroup =
            stickyPlayerRankItem.GetComponent<CanvasGroup>();

        if (stickyPlayerCanvasGroup == null)
        {
            stickyPlayerCanvasGroup =
                stickyPlayerRankItem.gameObject
                    .AddComponent<CanvasGroup>();
        }

        stickyPlayerCanvasGroup.alpha = 1f;
        stickyPlayerCanvasGroup.interactable = false;
        stickyPlayerCanvasGroup.blocksRaycasts = false;

        stickyPlayerRankItem.gameObject.SetActive(false);
        isStickyPlayerShown = false;

        GetRankItemView(stickyPlayerRankItem);

        SetStickyAvatarNativeSize();
        UpdateStickyPlayerRank();
    }

    private void SetStickyAvatarNativeSize()
    {
        if (stickyPlayerRankItem == null)
            return;

        RankItemView stickyView =
            GetRankItemView(stickyPlayerRankItem);

        if (stickyView != null &&
            stickyView.avatarImage != null)
        {
            stickyView.avatarImage.SetNativeSize();
        }
    }

    private void UpdateStickyPlayerRank()
    {
        if (activeScrollRect == null ||
            activeScrollRect.viewport == null ||
            activePlayerRankItem == null ||
            stickyPlayerRankItem == null ||
            stickyPlayerCanvasGroup == null)
        {
            return;
        }

        Bounds playerBounds =
            RectTransformUtility
                .CalculateRelativeRectTransformBounds(
                    activeScrollRect.viewport,
                    activePlayerRankItem
                );

        Rect viewportRect =
            activeScrollRect.viewport.rect;

        float visibleBottom =
            Mathf.Max(
                playerBounds.min.y,
                viewportRect.yMin
            );

        float visibleTop =
            Mathf.Min(
                playerBounds.max.y,
                viewportRect.yMax
            );

        float visibleHeight =
            Mathf.Max(
                0f,
                visibleTop - visibleBottom
            );

        activePlayerRankItem.localScale =
            Vector3.one * normalPlayerScale;

        stickyPlayerCanvasGroup.alpha =
            1f;

        bool isPlayerVisible =
            visibleHeight > 0.001f;

        float scrollOffset =
            Mathf.Max(
                0f,
                activeScrollRect.content != null
                    ? activeScrollRect.content.anchoredPosition.y
                    : 0f
            );

        // Do not show the green sticky player row at the initial top
        // position. It appears only after the player starts scrolling down,
        // and disappears again when the real player row becomes visible.
        bool hasStartedScrolling = scrollOffset > 1f;

        if (isPlayerVisible || !hasStartedScrolling)
        {
            HideStickyPlayerRank();
        }
        else
        {
            ShowStickyPlayerRank();
        }
    }

    private void ShowStickyPlayerRank()
    {
        if (stickyPlayerRankItem == null ||
            isStickyPlayerShown)
        {
            return;
        }

        isStickyPlayerShown = true;
        stickyPlayerRankItem.gameObject.SetActive(true);

        if (stickyShowCoroutine != null)
        {
            StopCoroutine(stickyShowCoroutine);
        }

        stickyShowCoroutine =
            StartCoroutine(
                StickyShowRoutine()
            );
    }

    private void HideStickyPlayerRank()
    {
        isStickyPlayerShown = false;

        if (stickyShowCoroutine != null)
        {
            StopCoroutine(stickyShowCoroutine);
            stickyShowCoroutine = null;
        }

        if (stickyPlayerRankItem != null)
        {
            stickyPlayerRankItem.gameObject.SetActive(false);
        }
    }

    private IEnumerator StickyShowRoutine()
    {
        float hiddenPosition =
            -stickyPlayerRankItem.rect.height *
            stickyPlayerScale;

        float elapsed = 0f;
        float duration =
            Mathf.Max(stickyShowDuration, 0.001f);

        while (elapsed < duration &&
               isStickyPlayerShown)
        {
            elapsed += Time.unscaledDeltaTime;

            float transition =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration)
                );

            stickyPlayerRankItem.localScale =
                Vector3.one *
                Mathf.Lerp(
                    stickyStartScale,
                    stickyPlayerScale,
                    transition
                );

            stickyPlayerRankItem.anchoredPosition =
                new Vector2(
                    0f,
                    Mathf.Lerp(
                        hiddenPosition,
                        stickyBottomOffset,
                        transition
                    )
                );

            yield return null;
        }

        if (isStickyPlayerShown &&
            stickyPlayerRankItem != null)
        {
            stickyPlayerRankItem.localScale =
                Vector3.one * stickyPlayerScale;

            stickyPlayerRankItem.anchoredPosition =
                new Vector2(0f, stickyBottomOffset);
        }

        stickyShowCoroutine = null;
    }

    private void UpdatePlayerLevelTexts(int level)
    {
        UpdateRankItemLevel(
            localPlayerRankItem,
            level
        );

        UpdateRankItemLevel(
            worldPlayerRankItem,
            level
        );

        UpdateRankItemLevel(
            stickyPlayerRankItem,
            level
        );
    }

    private void UpdateRankItemLevel(
        RectTransform rankItem,
        int level)
    {
        if (rankItem == null)
            return;

        RankItemView view = GetRankItemView(rankItem);

        if (view != null && view.levelText != null)
            view.levelText.text = PlayerData.FormatLevel(level);
    }

    private void ReleaseStickyPlayerRank()
    {
        HideStickyPlayerRank();

        if (activePlayerRankItem != null)
        {
            activePlayerRankItem.localScale =
                Vector3.one * normalPlayerScale;
        }

        if (stickyPlayerRankItem != null)
        {
            rankItemViews.Remove(stickyPlayerRankItem);
            stickyPlayerRankItem.gameObject.SetActive(false);
            Destroy(stickyPlayerRankItem.gameObject);
        }

        activeScrollRect = null;
        activePlayerRankItem = null;
        stickyPlayerRankItem = null;
        stickyPlayerCanvasGroup = null;
        isStickyPlayerShown = false;
    }

    protected override void OnDisable()
    {
        RemoveTabClickListeners(localTabButton, OnClickLocalTab);
        RemoveTabClickListeners(worldTabButton, OnClickWorldTab);
        RemoveTabClickListeners(friendTabButton, OnClickFriendTab);

        Observer.LevelChanged -= UpdatePlayerLevelTexts;
        Observer.ProfileChanged -= UpdatePlayerProfileVisuals;

        UnregisterVirtualScrollListeners();

        StopRankAnimation();
        ReleaseStickyPlayerRank();

        for (int i = 0; i < rankItems.Count; i++)
        {
            if (rankItems[i] != null)
            {
                rankItems[i].localScale =
                    Vector3.one * startScale;
            }

            if (i < rankCanvasGroups.Count &&
                rankCanvasGroups[i] != null)
            {
                rankCanvasGroups[i].alpha =
                    0f;
            }
        }

        base.OnDisable();
    }
}
