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

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        UpdateCameraSize();
    }
    
    private void UpdateCameraSize()
    {
        float baseAspect = baseWidth / baseHeight;
        float currentAspect = (float)Screen.width / Screen.height;

        _cam.orthographicSize = baseOrthographicSize * (baseAspect / currentAspect);
    }
}