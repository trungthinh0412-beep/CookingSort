using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the Home background scale independently from PopupHome.
/// Add popup prefabs to blockScaleForPopupPrefabs; their runtime clones are
/// detected through PopupController while Home is visible.
/// </summary>
[RequireComponent(typeof(PopupHome))]
public sealed class HomeBackgroundScaleBlocker : MonoBehaviour
{
    [Header("Background scale")]
    [SerializeField] private RectTransform background;
    [SerializeField] private float startScale = 1f;
    [SerializeField] private float endScale = 1.2f;
    [SerializeField] private float duration = 1.2f;

    [Header("Do not scale while these popups are open")]
    [SerializeField] private List<Popup> blockScaleForPopupPrefabs =
        new List<Popup>();

    private Coroutine scaleRoutine;
    private bool wasBlocked;
    private bool transitionOverride;

    private void Awake()
    {
        if (background == null)
            background = GetComponent<PopupHome>().HomeBackground;
    }

    private void OnEnable()
    {
        if (transitionOverride)
            return;

        wasBlocked = IsBlocked();
        if (!wasBlocked)
            StartScaleAnimation();
    }

    private void OnDisable()
    {
        StopScaleAnimation();
    }

    private void Update()
    {
        if (transitionOverride)
            return;

        bool isBlocked = IsBlocked();

        if (isBlocked && !wasBlocked)
        {
            // The popup covers Home, so finish the scale immediately and do
            // not replay it when that popup closes.
            StopScaleAnimation();
            if (background != null)
                background.localScale = Vector3.one * endScale;
        }

        wasBlocked = isBlocked;
    }

    private bool IsBlocked()
    {
        if (PopupController.Instance == null)
            return false;

        foreach (Popup popupPrefab in blockScaleForPopupPrefabs)
        {
            if (popupPrefab == null)
                continue;

            Popup runtimePopup = PopupController.Instance.Get(popupPrefab.GetType());
            if (runtimePopup != null && runtimePopup.isActiveAndEnabled)
                return true;
        }

        return false;
    }

    private void StartScaleAnimation()
    {
        if (background == null || transitionOverride)
            return;

        StopScaleAnimation();
        background.localScale = Vector3.one * startScale;
        scaleRoutine = StartCoroutine(ScaleAnimation());
    }

    private void StopScaleAnimation()
    {
        if (scaleRoutine == null)
            return;

        StopCoroutine(scaleRoutine);
        scaleRoutine = null;
    }

    private IEnumerator ScaleAnimation()
    {
        float elapsed = 0f;
        Vector3 from = Vector3.one * startScale;
        Vector3 to = Vector3.one * endScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            background.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }

        background.localScale = to;
        scaleRoutine = null;
    }

    /// <summary>
    /// Temporarily gives the Home/Kingdom transition full ownership of the
    /// background scale. This prevents the normal Home breathing animation
    /// from fighting the transition tween.
    /// </summary>
    public void BeginTransitionOverride()
    {
        transitionOverride = true;
        StopScaleAnimation();
    }

    /// <summary>
    /// Releases the transition override while preserving the scale reached by
    /// the reverse animation.
    /// </summary>
    public void EndTransitionOverride(Vector3 restoredScale)
    {
        if (background != null)
            background.localScale = restoredScale;

        transitionOverride = false;
        wasBlocked = IsBlocked();
    }
}
