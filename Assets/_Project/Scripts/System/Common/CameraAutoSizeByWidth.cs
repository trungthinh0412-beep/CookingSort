using System;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraAutoSizeByWidth : MonoBehaviour
{
    [SerializeField] private float baseOrthographicSize = 9.5f;

    // Màn chuẩn portrait của bạn
    [SerializeField] private float baseWidth = 1080f;
    [SerializeField] private float baseHeight = 1920f;

    private Camera _cam;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    public float BaseOrthographicSize => baseOrthographicSize;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        UpdateCameraSize();
    }

    private void OnEnable()
    {
        if (_cam == null)
            _cam = GetComponent<Camera>();

        UpdateCameraSize();
    }

    private void Update()
    {
        if (Screen.width == _lastScreenWidth &&
            Screen.height == _lastScreenHeight)
        {
            return;
        }

        UpdateCameraSize();
    }
    
    private void UpdateCameraSize()
    {
        if (_cam == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        float baseAspect = baseWidth / baseHeight;
        float currentAspect = (float)Screen.width / Screen.height;

        _cam.orthographicSize = baseOrthographicSize * (baseAspect / currentAspect);
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
    }
}
