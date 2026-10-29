using System.Collections.Generic;
using Lean.Touch;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class DebugConsoleShortcut : MonoBehaviour
{
    [SerializeField] private UnityEvent action;

    private RectTransform _rectTransform;
    private Canvas _canvas;
    private Vector2 _fingerDownPos;
    private bool _isOverThisObject;
    private int _activeFingerIndex = int.MinValue;

    private const float TapThreshold = 10f;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
    }

    private bool IsFingerOverThisObject(LeanFinger finger)
    {
        if (finger == null || EventSystem.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = finger.ScreenPosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult hit in results)
        {
            Transform hitTransform = hit.gameObject.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    private void OnFingerDown(LeanFinger finger)
    {
        if (_isOverThisObject || !IsFingerOverThisObject(finger))
            return;

        _isOverThisObject = true;
        _activeFingerIndex = finger.Index;
        _fingerDownPos = finger.ScreenPosition;
    }

    private void OnFingerUp(LeanFinger finger)
    {
        if (!_isOverThisObject || finger == null ||
            finger.Index != _activeFingerIndex)
            return;

        float distance = Vector2.Distance(_fingerDownPos, finger.ScreenPosition);
        if (distance < TapThreshold)
            action?.Invoke();

        ResetFinger();
    }

    private void OnFingerDrag(LeanFinger finger)
    {
        if (!_isOverThisObject || finger == null ||
            finger.Index != _activeFingerIndex ||
            _rectTransform == null || _canvas == null)
            return;

        float scaleFactor = Mathf.Max(_canvas.scaleFactor, 0.0001f);
        Vector2 delta = finger.ScreenDelta / scaleFactor;
        if (finger.Index == LeanTouch.HOVER_FINGER_INDEX)
            delta *= 0.6f;

        _rectTransform.anchoredPosition += delta;
    }

    private void ResetFinger()
    {
        _isOverThisObject = false;
        _activeFingerIndex = int.MinValue;
    }

    private void OnEnable()
    {
        LeanTouch.OnFingerDown += OnFingerDown;
        LeanTouch.OnFingerUp += OnFingerUp;
        LeanTouch.OnFingerUpdate += OnFingerDrag;
    }

    private void OnDisable()
    {
        LeanTouch.OnFingerDown -= OnFingerDown;
        LeanTouch.OnFingerUp -= OnFingerUp;
        LeanTouch.OnFingerUpdate -= OnFingerDrag;
        ResetFinger();
    }
}
