using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class CameraUIController : SingletonDontDestroy<CameraUIController>
{
    [SerializeField] private Camera cameraUI;

    private Camera _baseCamera;
    private Camera[] _uiCameras;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Both UI cameras belong to the persistent PopupController root.
        _uiCameras = transform.parent != null
            ? transform.parent.GetComponentsInChildren<Camera>(true)
            : GetComponents<Camera>();
        RebuildStack();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RemoveFromStack();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebuildStack();
    }

    private void LateUpdate()
    {
        if (_baseCamera != Camera.main)
            RebuildStack();
    }

    private void RemoveFromStack()
    {
        if (_baseCamera == null || _uiCameras == null) return;
        if (!_baseCamera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) return;
        var stack = data.cameraStack;
        if (stack == null) return;
        foreach (var camera in _uiCameras) stack.Remove(camera);
    }

    private void RebuildStack()
    {
        if (_uiCameras == null) return;
        RemoveFromStack();
        _baseCamera = Camera.main;
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return;

        System.Array.Sort(_uiCameras, (a, b) => a.depth.CompareTo(b.depth));
        foreach (var camera in _uiCameras)
        {
            if (camera != null && camera != _baseCamera)
                camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
        }

        if (_baseCamera == null) return;
        var baseData = _baseCamera.GetUniversalAdditionalCameraData();
        baseData.renderType = CameraRenderType.Base;
        var stack = baseData.cameraStack;
        if (stack == null) return;
        foreach (var camera in _uiCameras)
        {
            if (camera != null && camera != _baseCamera && !stack.Contains(camera))
                stack.Add(camera);
        }
    }
}
