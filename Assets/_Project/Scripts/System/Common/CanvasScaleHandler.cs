using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CanvasScaleHandler : MonoBehaviour
{
    private static readonly Vector2 PortraitReferenceResolution =
        new Vector2(1080f, 1920f);

    [SerializeField] private new Camera camera;
    [SerializeField] private CanvasScaler canvasScaler;

    private int _lastWidth;
    private int _lastHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureLoadedPortraitCanvases();
    }

    private void Awake()
    {
        Configure();
    }

    private void OnEnable()
    {
        Configure();
    }

    private void Update()
    {
        int width = GetCurrentWidth();
        int height = GetCurrentHeight();
        if (width == _lastWidth && height == _lastHeight)
        {
            return;
        }

        Configure();
    }

    private void OnValidate()
    {
        Configure();
    }

    private void Configure()
    {
        if (canvasScaler == null)
        {
            canvasScaler = GetComponent<CanvasScaler>();
        }

        ConfigurePortraitScaler(canvasScaler);
        _lastWidth = GetCurrentWidth();
        _lastHeight = GetCurrentHeight();
    }

    private int GetCurrentWidth()
    {
        return !Application.isPlaying && camera != null
            ? camera.pixelWidth
            : Screen.width;
    }

    private int GetCurrentHeight()
    {
        return !Application.isPlaying && camera != null
            ? camera.pixelHeight
            : Screen.height;
    }

    private static void ConfigureLoadedPortraitCanvases()
    {
        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int index = 0; index < scalers.Length; index++)
        {
            CanvasScaler scaler = scalers[index];
            if (!UsesPortraitReference(scaler))
            {
                continue;
            }

            Canvas canvas = scaler.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            ConfigurePortraitScaler(scaler);
        }
    }

    private static bool UsesPortraitReference(CanvasScaler scaler)
    {
        if (scaler == null)
        {
            return false;
        }

        Vector2 resolution = scaler.referenceResolution;
        return Mathf.Approximately(
                   resolution.x,
                   PortraitReferenceResolution.x
               ) &&
               Mathf.Approximately(
                   resolution.y,
                   PortraitReferenceResolution.y
               );
    }

    private static void ConfigurePortraitScaler(CanvasScaler scaler)
    {
        if (scaler == null)
        {
            return;
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = PortraitReferenceResolution;

        // Expand uses the smaller scale factor. The logical canvas can grow
        // beyond 1080x1920, but it can never become smaller, so anchored UI
        // remains visible on tall phones, tablets and foldable portrait screens.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }
}
