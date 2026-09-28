using System.Collections;
using System.Collections.Generic;
using Lean.Touch;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainTabSwipeController : MonoBehaviour
{
    [Header("Gesture")]
    [SerializeField] private float directionThreshold = 18f;
    [SerializeField] private float horizontalDirectionRatio = 1.15f;
    [SerializeField, Range(0.1f, 0.5f)]
    private float commitDistance = 0.28f;
    [SerializeField] private float commitVelocity = 900f;

    [Header("Snap")]
    [SerializeField] private float snapDuration = 0.22f;

    private readonly List<RaycastResult> raycastResults =
        new List<RaycastResult>();

    private PopupController popupController;
    private BottomTabBar bottomTabBar;
    private RectTransform canvasRect;
    private Canvas rootCanvas;

    private LeanFinger activeFinger;
    private bool gestureRejected;
    private bool horizontalGesture;

    private Popup fromPopup;
    private Popup toPopup;
    private RectTransform fromRect;
    private RectTransform toRect;
    private Vector2 fromBasePosition;
    private Vector2 toBasePosition;

    private int fromIndex = -1;
    private int toIndex = -1;
    private float pageWidth;
    private float targetPlacement;
    private float currentDrag;
    private Coroutine settleCoroutine;

    public bool IsTransitioning { get; private set; }

    public void Initialize(
        PopupController controller,
        BottomTabBar tabBar)
    {
        popupController = controller;
        bottomTabBar = tabBar;

        canvasRect = popupController != null
            ? popupController.CanvasTransform as RectTransform
            : null;

        rootCanvas = canvasRect != null
            ? canvasRect.GetComponentInParent<Canvas>()
            : null;
    }

    private void OnEnable()
    {
        LeanTouch.OnFingerDown += OnFingerDown;
        LeanTouch.OnFingerUpdate += OnFingerUpdate;
        LeanTouch.OnFingerUp += OnFingerUp;
    }

    private void OnDisable()
    {
        LeanTouch.OnFingerDown -= OnFingerDown;
        LeanTouch.OnFingerUpdate -= OnFingerUpdate;
        LeanTouch.OnFingerUp -= OnFingerUp;
    }

    public void GoToTab(int targetIndex)
    {
        if (!CanStartTransition() ||
            targetIndex < 0 ||
            targetIndex >= bottomTabBar.TabCount)
        {
            return;
        }

        int currentIndex =
            popupController.GetCurrentMainTabIndex();

        if (targetIndex == currentIndex)
        {
            bottomTabBar.SetSelectedInstant(currentIndex);
            return;
        }

        if (!BeginTransition(
                currentIndex,
                targetIndex))
        {
            return;
        }

        settleCoroutine =
            StartCoroutine(
                SettleTransition(true)
            );
    }

    private void OnFingerDown(LeanFinger finger)
    {
        if (activeFinger != null ||
            !CanStartTransition())
        {
            return;
        }

        activeFinger = finger;
        gestureRejected =
            StartedOnHorizontalScroll(
                finger.ScreenPosition
            );

        horizontalGesture = false;
    }

    private void OnFingerUpdate(LeanFinger finger)
    {
        if (finger != activeFinger ||
            gestureRejected ||
            settleCoroutine != null)
        {
            return;
        }

        Vector2 totalDelta =
            finger.SwipeScreenDelta;

        if (!horizontalGesture)
        {
            if (totalDelta.magnitude < directionThreshold)
                return;

            if (Mathf.Abs(totalDelta.x) <=
                Mathf.Abs(totalDelta.y) *
                horizontalDirectionRatio)
            {
                gestureRejected = true;
                return;
            }

            int currentIndex =
                popupController.GetCurrentMainTabIndex();

            int targetIndex = totalDelta.x < 0f
                ? currentIndex + 1
                : currentIndex - 1;

            if (targetIndex < 0 ||
                targetIndex >= bottomTabBar.TabCount ||
                !BeginTransition(
                    currentIndex,
                    targetIndex))
            {
                gestureRejected = true;
                return;
            }

            horizontalGesture = true;
        }

        ApplyFingerDrag(totalDelta.x);
    }

    private void OnFingerUp(LeanFinger finger)
    {
        if (finger != activeFinger)
            return;

        if (!horizontalGesture ||
            gestureRejected ||
            !IsTransitioning)
        {
            ResetGesture();
            return;
        }

        float progress =
            Mathf.Clamp01(
                Mathf.Abs(currentDrag) /
                Mathf.Max(pageWidth, 0.001f)
            );

        float canvasScale = GetCanvasScaleFactor();

        float velocity =
            (finger.SwipeScreenDelta.x / canvasScale) /
            Mathf.Max(finger.Age, 0.05f);

        bool hasCommitVelocity =
            Mathf.Abs(velocity) >= commitVelocity &&
            Mathf.Sign(velocity) ==
            Mathf.Sign(-targetPlacement);

        bool commit =
            progress >= commitDistance ||
            hasCommitVelocity;

        activeFinger = null;

        settleCoroutine =
            StartCoroutine(
                SettleTransition(commit)
            );
    }

    private bool BeginTransition(
        int sourceIndex,
        int targetIndex)
    {
        if (IsTransitioning ||
            popupController == null ||
            bottomTabBar == null)
        {
            return false;
        }

        if (!popupController.PrepareMainTabTransition(
                sourceIndex,
                targetIndex,
                out fromPopup,
                out toPopup))
        {
            return false;
        }

        fromRect = fromPopup.transform as RectTransform;
        toRect = toPopup.transform as RectTransform;

        if (fromRect == null || toRect == null)
        {
            popupController.CancelMainTabTransition(
                fromPopup,
                toPopup
            );

            return false;
        }

        fromIndex = sourceIndex;
        toIndex = targetIndex;
        pageWidth = GetPageWidth();
        targetPlacement = targetIndex > sourceIndex
            ? pageWidth
            : -pageWidth;

        fromBasePosition = fromRect.anchoredPosition;
        toBasePosition = toRect.anchoredPosition;
        currentDrag = 0f;

        fromRect.anchoredPosition = fromBasePosition;
        toRect.anchoredPosition =
            toBasePosition +
            Vector2.right * targetPlacement;

        bottomTabBar.SetInteractiveProgress(
            fromIndex,
            toIndex,
            0f
        );

        IsTransitioning = true;
        return true;
    }

    private void ApplyFingerDrag(float screenDrag)
    {
        float drag =
            screenDrag /
            GetCanvasScaleFactor();

        currentDrag = targetPlacement > 0f
            ? Mathf.Clamp(drag, -pageWidth, 0f)
            : Mathf.Clamp(drag, 0f, pageWidth);

        ApplyTransitionVisuals();
    }

    private void ApplyTransitionVisuals()
    {
        if (fromRect == null || toRect == null)
            return;

        fromRect.anchoredPosition =
            fromBasePosition +
            Vector2.right * currentDrag;

        toRect.anchoredPosition =
            toBasePosition +
            Vector2.right *
            (targetPlacement + currentDrag);

        float progress =
            Mathf.Clamp01(
                Mathf.Abs(currentDrag) /
                Mathf.Max(pageWidth, 0.001f)
            );

        bottomTabBar.SetInteractiveProgress(
            fromIndex,
            toIndex,
            progress
        );
    }

    private IEnumerator SettleTransition(bool commit)
    {
        float startDrag = currentDrag;
        float targetDrag = commit
            ? -targetPlacement
            : 0f;

        float remainingDistance =
            Mathf.Abs(targetDrag - startDrag) /
            Mathf.Max(pageWidth, 0.001f);

        float duration =
            Mathf.Max(
                0.08f,
                snapDuration * remainingDistance
            );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / duration)
            );

            currentDrag =
                Mathf.Lerp(
                    startDrag,
                    targetDrag,
                    t
                );

            ApplyTransitionVisuals();
            yield return null;
        }

        currentDrag = targetDrag;
        ApplyTransitionVisuals();
        FinishTransition(commit);
    }

    private void FinishTransition(bool commit)
    {
        if (fromRect != null)
            fromRect.anchoredPosition = fromBasePosition;

        if (toRect != null)
            toRect.anchoredPosition = toBasePosition;

        if (commit)
        {
            popupController.CompleteMainTabTransition(
                fromPopup,
                toPopup
            );

            bottomTabBar.CompleteInteractiveTransition(
                toIndex
            );
        }
        else
        {
            popupController.CancelMainTabTransition(
                fromPopup,
                toPopup
            );

            bottomTabBar.CancelInteractiveTransition(
                fromIndex
            );
        }

        IsTransitioning = false;
        settleCoroutine = null;
        ResetTransitionState();
        ResetGesture();
    }

    private bool CanStartTransition()
    {
        if (IsTransitioning ||
            settleCoroutine != null ||
            popupController == null ||
            bottomTabBar == null ||
            !bottomTabBar.gameObject.activeInHierarchy)
        {
            return false;
        }

        int currentIndex =
            popupController.GetCurrentMainTabIndex();

        return currentIndex >= 0 &&
               currentIndex < bottomTabBar.TabCount;
    }

    private bool StartedOnHorizontalScroll(
        Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerData =
            new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

        raycastResults.Clear();
        EventSystem.current.RaycastAll(
            pointerData,
            raycastResults
        );

        for (int i = 0; i < raycastResults.Count; i++)
        {
            ScrollRect scrollRect =
                raycastResults[i]
                    .gameObject
                    .GetComponentInParent<ScrollRect>();

            if (scrollRect != null &&
                scrollRect.isActiveAndEnabled &&
                scrollRect.horizontal)
            {
                return true;
            }
        }

        return false;
    }

    private float GetPageWidth()
    {
        if (canvasRect != null &&
            canvasRect.rect.width > 0f)
        {
            return canvasRect.rect.width;
        }

        return Screen.width /
               GetCanvasScaleFactor();
    }

    private float GetCanvasScaleFactor()
    {
        if (rootCanvas == null && canvasRect != null)
        {
            rootCanvas =
                canvasRect.GetComponentInParent<Canvas>();
        }

        return rootCanvas != null
            ? Mathf.Max(rootCanvas.rootCanvas.scaleFactor, 0.001f)
            : 1f;
    }

    private void ResetGesture()
    {
        activeFinger = null;
        gestureRejected = false;
        horizontalGesture = false;
    }

    private void ResetTransitionState()
    {
        fromPopup = null;
        toPopup = null;
        fromRect = null;
        toRect = null;
        fromIndex = -1;
        toIndex = -1;
        pageWidth = 0f;
        targetPlacement = 0f;
        currentDrag = 0f;
    }
}
