using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public partial class PreviewGenerator
{
    public static readonly PreviewGenerator Default = new PreviewGenerator();

    private const int MaxTextureSize = 2048;
    private static int LatePreviewQueued = 0;

    public Vector3 previewPosition = new Vector3(9999, 9999, -9999);
    public Vector3 latePreviewOffset = new Vector3(100, 100, 0);

    public bool transparentBackground = true;
    public Color solidBackgroundColor = new Color(0.3f, 0.3f, 0.3f);
    public FilterMode imageFilterMode = FilterMode.Point;
    public ImageSizeType sizingType = ImageSizeType.PixelsPerUnit;
    public int pixelPerUnit = 32;
    public int width = 256;
    public int height = 256;

    public CaptureTiming captureTiming = CaptureTiming.Instant;
    public float timingCounter = 1;

    public System.Action<Texture2D> onCapturedCallback;
    public System.Action<GameObject> onPreCaptureCallback;


    public enum ImageSizeType
    {
        PixelsPerUnit,
        Fit,
        Fill,
        Stretch,
    }

    public enum CaptureTiming
    {
        Instant,
        EndOfFrame,
        NextFrame,
        NextSecond,
        NextSecondRealtime
    }


    public PreviewGenerator Copy()
    {
        return new PreviewGenerator()
        {
            previewPosition = previewPosition,
            latePreviewOffset = latePreviewOffset,
            transparentBackground = transparentBackground,
            solidBackgroundColor = solidBackgroundColor,
            imageFilterMode = imageFilterMode,
            sizingType = sizingType,
            pixelPerUnit = pixelPerUnit,
            width = width,
            height = height,
            captureTiming = captureTiming,
            timingCounter = timingCounter,
            onCapturedCallback = onCapturedCallback,
            onPreCaptureCallback = onPreCaptureCallback,
        };
    }

    public PreviewGenerator OnCaptured(
        System.Action<Texture2D> callback)
    {
        onCapturedCallback = callback;
        return this;
    }

    public PreviewGenerator OnPreCaptured(
        System.Action<GameObject> callback)
    {
        onPreCaptureCallback = callback;
        return this;
    }

    public PreviewGenerator SetTiming(
        CaptureTiming timing,
        float? counter = null)
    {
        captureTiming = timing;
        timingCounter = counter ?? timingCounter;
        return this;
    }


    public Texture2D CreatePreview(
        GameObject obj,
        bool clone = true)
    {
        if (!CanCreatePreview(obj))
        {
            onCapturedCallback?.Invoke(null);
            return null;
        }

        var cachedPosition = obj.transform.position;
        var prevObj = clone ? Object.Instantiate(obj, null) : obj;
        prevObj.transform.position = previewPosition + LatePreviewQueued * latePreviewOffset;

        var bounds = ObjectUtils.GetRendererBounds(prevObj, false);
        Vector2Int size = GetImageSize(bounds);
        Camera cam = CreatePreviewCamera(bounds);

        LatePreviewQueued++;
        var dummy = DummyBehaviour.Create("Preview dummy");

        void callback()
        {
            LatePreviewQueued--;
            if (clone)
            {
                Object.DestroyImmediate(prevObj);
            }
            else
            {
                prevObj.transform.position = cachedPosition;
            }

            Object.DestroyImmediate(cam.gameObject);
            Object.DestroyImmediate(dummy.gameObject);
        }

        if (captureTiming == CaptureTiming.Instant)
            return WrappedCapture(prevObj,
                cam,
                size.x,
                size.y,
                callback);
        else
        {
            dummy.StartCoroutine(TimedCapture(prevObj,
                cam,
                size.x,
                size.y,
                callback));
            return null;
        }
    }


    private void NotifyPreviewTaking(
        GameObject go,
        System.Action<IPreviewComponent> action)
    {
        var allComps = go.GetComponentsInChildren<Component>();
        foreach (var comp in allComps)
        {
            if (comp is IPreviewComponent) action?.Invoke(comp as IPreviewComponent);
        }
    }


    private bool CanCreatePreview(
        GameObject obj)
    {
        return obj != null && obj.GetComponentsInChildren<Renderer>()
            .Any(r => r != null && r.enabled);
    }

    private Camera CreatePreviewCamera(
        Bounds bounds)
    {
        GameObject camObj = new GameObject("Preview generator camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.name = "Preview generator camera";

        cam.transform.position = bounds.center + Vector3.back * (bounds.extents.z + 2);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = bounds.size.z + 4;

        cam.orthographic = true;
        cam.orthographicSize = bounds.extents.y;
        cam.aspect = bounds.extents.x / bounds.extents.y;

        cam.clearFlags = CameraClearFlags.Color;
        cam.backgroundColor = solidBackgroundColor;
        if (transparentBackground) cam.backgroundColor *= 0;

        cam.enabled = false;

        return cam;
    }

    private Vector2Int GetImageSize(
        Bounds bounds)
    {
        int w = 1;
        int h = 1;

        if (sizingType == ImageSizeType.PixelsPerUnit)
        {
            w = Mathf.CeilToInt(bounds.size.x * pixelPerUnit);
            h = Mathf.CeilToInt(bounds.size.y * pixelPerUnit);
        }
        else if (sizingType == ImageSizeType.Stretch)
        {
            w = width;
            h = height;
        }
        else if (sizingType == ImageSizeType.Fit || sizingType == ImageSizeType.Fill)
        {
            float widthFactor = width / bounds.size.x;
            float heightFactor = height / bounds.size.y;
            float factor = sizingType == ImageSizeType.Fit ? Mathf.Min(widthFactor, heightFactor) : Mathf.Max(widthFactor, heightFactor);

            w = Mathf.CeilToInt(bounds.size.x * factor);
            h = Mathf.CeilToInt(bounds.size.y * factor);
        }

        if (w > MaxTextureSize || h > MaxTextureSize)
        {
            float downscaleWidthFactor = (float) MaxTextureSize / w;
            float downscaleHeightFactor = (float) MaxTextureSize / h;
            float downscaleFactor = Mathf.Min(downscaleWidthFactor, downscaleHeightFactor);

            w = Mathf.CeilToInt(w * downscaleFactor);
            h = Mathf.CeilToInt(h * downscaleFactor);
        }

        return new Vector2Int(w, h);
    }

    private IEnumerator TimedCapture(
        GameObject obj,
        Camera cam,
        int w,
        int h,
        System.Action callback)
    {
        if (captureTiming == CaptureTiming.Instant)
        {
        }
        else if (captureTiming == CaptureTiming.EndOfFrame) yield return new WaitForEndOfFrame();
        else if (captureTiming == CaptureTiming.NextFrame)
            for (int i = 0; i < timingCounter; i++)
                yield return null;
        else if (captureTiming == CaptureTiming.NextSecond) yield return new WaitForSeconds(timingCounter);
        else if (captureTiming == CaptureTiming.NextSecondRealtime) yield return new WaitForSecondsRealtime(timingCounter);

        WrappedCapture(obj,
            cam,
            w,
            h,
            callback);
    }

    private Texture2D WrappedCapture(
        GameObject obj,
        Camera cam,
        int w,
        int h,
        System.Action callback)
    {
        onPreCaptureCallback?.Invoke(obj);

        NotifyPreviewTaking(obj, i => i.OnPreviewCapturing(this));
        Texture2D tex = DoCapture(cam, w, h);
        NotifyPreviewTaking(obj, i => i.OnPreviewCaptured(this));

        callback?.Invoke();
        onCapturedCallback?.Invoke(tex);

        return tex;
    }

    private Texture2D DoCapture(
        Camera cam,
        int w,
        int h)
    {
        RenderTexture temp = RenderTexture.active;
        RenderTexture renderTex = null;
        try
        {
            renderTex = RenderTexture.GetTemporary(w, h, 16);
        }
        catch (Exception)
        {
            //
        }
        RenderTexture.active = renderTex;

        cam.enabled = true;
        cam.targetTexture = renderTex;
        cam.Render();
        cam.targetTexture = null;
        cam.enabled = false;

        var tex = new Texture2D(w, h, transparentBackground ? TextureFormat.RGBA32 : TextureFormat.RGB24, false) {filterMode = imageFilterMode};
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
        tex.Apply(false, false);

        RenderTexture.active = temp;
        RenderTexture.ReleaseTemporary(renderTex);
        return tex;
    }
}

public interface IPreviewComponent
{
    void OnPreviewCapturing(PreviewGenerator preview);
    void OnPreviewCaptured(PreviewGenerator preview);
}
