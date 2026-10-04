using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PopupKingdomBuildComplete : Popup
{
    [Header("Complete Popup")]
    [SerializeField] private Button backgroundButton;
    [SerializeField] private RectTransform chestRoot;
    [SerializeField] private List<Image> rewardImages = new List<Image>();
    [SerializeField] private List<Sprite> rewardSprites = new List<Sprite>();
    [Tooltip("Sorting Order added above PopupKingdomBuild while this popup is shown.")]
    [SerializeField] [Min(1)] private int ownerSortingOrderOffset = 1;

    [Header("Intro Timing")]
    [SerializeField] [Min(0f)] private float chestScaleDuration = .22f;
    [SerializeField] [Min(0f)] private float rewardPopDuration = .12f;
    [SerializeField] [Min(0f)] private float rewardPopInterval = .05f;
    [SerializeField] private bool useUnscaledTime = true;

    private PopupKingdomBuild owner;
    private int roomIndex = -1;
    private bool canContinue;
    private Coroutine introCoroutine;

    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        CacheReferences();
        BindButton();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        PlaceAboveOwner();
        CacheReferences();
        BindButton();
        PrepareIntroState();
    }

    private void PlaceAboveOwner()
    {
        Canvas completeCanvas = Canvas;
        Canvas ownerCanvas = owner != null ? owner.Canvas : null;

        if (completeCanvas == null || ownerCanvas == null)
            return;

        completeCanvas.overrideSorting = true;
        completeCanvas.sortingLayerID = ownerCanvas.sortingLayerID;
        completeCanvas.sortingOrder = ownerCanvas.sortingOrder +
                                      Mathf.Max(1, ownerSortingOrderOffset);
        transform.SetAsLastSibling();
    }

    protected override void AfterShown()
    {
        base.AfterShown();
        PlayIntro();
    }

    protected override void OnDisable()
    {
        if (introCoroutine != null)
        {
            StopCoroutine(introCoroutine);
            introCoroutine = null;
        }

        canContinue = false;
        base.OnDisable();
    }

    public void Configure(PopupKingdomBuild buildPopup, int completedRoomIndex)
    {
        owner = buildPopup;
        roomIndex = completedRoomIndex;
    }

    public void ConfigureRewards(IReadOnlyList<Sprite> sprites)
    {
        rewardSprites.Clear();

        if (sprites == null)
            return;

        for (int i = 0; i < sprites.Count; i++)
            rewardSprites.Add(sprites[i]);
    }

    private void CacheReferences()
    {
        if (backgroundButton == null)
            backgroundButton = GetComponentInChildren<Button>(true);

        if (chestRoot == null)
        {
            Transform chest = FindNamedTransform("CompleteChest");
            if (chest != null)
                chestRoot = chest as RectTransform;
        }

        if (rewardImages == null)
            rewardImages = new List<Image>();

        if (rewardImages.Count == 0)
        {
            Transform rewardRoot = FindNamedTransform("RewardRoot");
            if (rewardRoot != null)
            {
                Image[] images = rewardRoot.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                    rewardImages.Add(images[i]);
            }
        }
    }

    private void BindButton()
    {
        if (backgroundButton == null)
            return;

        backgroundButton.onClick.RemoveListener(OnClickBackground);
        backgroundButton.onClick.AddListener(OnClickBackground);
    }

    private void PrepareIntroState()
    {
        canContinue = false;

        if (chestRoot != null)
            chestRoot.localScale = Vector3.zero;

        for (int i = 0; i < rewardImages.Count; i++)
        {
            Image rewardImage = rewardImages[i];
            if (rewardImage == null)
                continue;

            if (i < rewardSprites.Count && rewardSprites[i] != null)
                rewardImage.sprite = rewardSprites[i];

            rewardImage.gameObject.SetActive(i < 6);
            rewardImage.transform.localScale = Vector3.zero;
        }
    }

    private void PlayIntro()
    {
        if (introCoroutine != null)
            StopCoroutine(introCoroutine);

        introCoroutine = StartCoroutine(PlayIntroRoutine());
    }

    private IEnumerator PlayIntroRoutine()
    {
        if (chestRoot != null)
            yield return ScaleRoutine(chestRoot, Vector3.zero, Vector3.one * 2f, chestScaleDuration);

        for (int i = 0; i < rewardImages.Count && i < 6; i++)
        {
            Image rewardImage = rewardImages[i];
            if (rewardImage == null)
                continue;

            rewardImage.gameObject.SetActive(true);
            yield return ScaleRoutine(rewardImage.transform, Vector3.zero, Vector3.one, rewardPopDuration);

            if (rewardPopInterval > 0f)
                yield return Wait(rewardPopInterval);
        }

        canContinue = true;
        introCoroutine = null;
    }

    private IEnumerator ScaleRoutine(Transform target, Vector3 from, Vector3 to, float duration)
    {
        if (target == null)
            yield break;

        if (duration <= 0f)
        {
            target.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            target.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        target.localScale = to;
    }

    private IEnumerator Wait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }
    }

    public void OnClickBackground()
    {
        if (!canContinue)
            return;

        PopupKingdomBuild buildPopup = owner != null
            ? owner
            : PopupController.Instance?.Get<PopupKingdomBuild>() as PopupKingdomBuild;

        Hide(PopupAnimation.None);
        buildPopup?.OpenCompletedRoomViewFromComplete(roomIndex);
    }

    private Transform FindNamedTransform(string objectName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == objectName)
                return transforms[i];
        }

        return null;
    }
}
