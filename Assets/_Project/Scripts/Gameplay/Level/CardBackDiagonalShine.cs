using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CardBackDiagonalShine : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.32f;
    [SerializeField, Min(0.1f)] private float travelDuration = 1.1f;
    [SerializeField, Min(0f)] private float minimumPause = 4f;
    [SerializeField, Min(0f)] private float maximumPause = 5f;
    [SerializeField, Min(0.01f)] private float lineWidth = 0.32f;
    [SerializeField] private float lineAngle = -25f;

    private SpriteRenderer _targetRenderer;
    private SpriteRenderer _shineRenderer;
    private SpriteMask _spriteMask;
    private Coroutine _shineRoutine;

    private static Texture2D _gradientTexture;
    private static Sprite _gradientSprite;

    private void Awake()
    {
        EnsureVisual();
    }

    private void OnEnable()
    {
        EnsureVisual();

        if (_shineRoutine == null && _shineRenderer != null)
        {
            _shineRoutine = StartCoroutine(PlayShine());
        }
    }

    private void OnDisable()
    {
        if (_shineRoutine != null)
        {
            StopCoroutine(_shineRoutine);
        }

        _shineRoutine = null;
        HideShine();
    }

    private void OnValidate()
    {
        maximumPause = Mathf.Max(minimumPause, maximumPause);
        travelDuration = Mathf.Max(0.1f, travelDuration);
        lineWidth = Mathf.Max(0.01f, lineWidth);
    }

    private void EnsureVisual()
    {
        if (_targetRenderer == null)
        {
            _targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (_targetRenderer == null || _shineRenderer != null)
        {
            return;
        }

        _spriteMask = GetComponent<SpriteMask>();
        if (_spriteMask == null)
        {
            _spriteMask = gameObject.AddComponent<SpriteMask>();
        }

        GameObject shineObject = new GameObject(
            "CardBackShineLine",
            typeof(SpriteRenderer)
        );
        shineObject.transform.SetParent(transform, false);
        shineObject.transform.localRotation = Quaternion.Euler(
            0f,
            0f,
            lineAngle
        );

        _shineRenderer = shineObject.GetComponent<SpriteRenderer>();
        _shineRenderer.sprite = GetGradientSprite();
        _shineRenderer.maskInteraction =
            SpriteMaskInteraction.VisibleInsideMask;
        _shineRenderer.color = new Color(1f, 0.96f, 0.72f, 0f);
        _shineRenderer.enabled = false;

        RefreshVisualSettings();
    }

    private IEnumerator PlayShine()
    {
        yield return new WaitForSecondsRealtime(Random.Range(0.5f, 2f));

        while (_shineRenderer != null && gameObject.activeInHierarchy)
        {
            if (_targetRenderer == null ||
                !_targetRenderer.enabled ||
                _targetRenderer.sprite == null)
            {
                HideShine();
                yield return null;
                continue;
            }

            RefreshVisualSettings();
            _shineRenderer.enabled = true;

            float travelDistance = GetTravelDistance();
            float elapsed = 0f;

            while (elapsed < travelDuration && _shineRenderer != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / travelDuration);
                float eased = progress * progress * (3f - 2f * progress);

                _shineRenderer.transform.localPosition = new Vector3(
                    Mathf.Lerp(-travelDistance, travelDistance, eased),
                    0f,
                    0f
                );

                float fadeIn = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(progress / 0.2f)
                );
                float fadeOut = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01((1f - progress) / 0.2f)
                );

                Color color = _shineRenderer.color;
                color.a = maxAlpha * fadeIn * fadeOut;
                _shineRenderer.color = color;

                yield return null;
            }

            HideShine();
            yield return new WaitForSecondsRealtime(
                Random.Range(minimumPause, maximumPause)
            );
        }

        _shineRoutine = null;
    }

    private void RefreshVisualSettings()
    {
        if (_targetRenderer == null ||
            _shineRenderer == null ||
            _spriteMask == null)
        {
            return;
        }

        _spriteMask.sprite = _targetRenderer.sprite;
        _spriteMask.alphaCutoff = 0.01f;
        _spriteMask.isCustomRangeActive = true;
        _spriteMask.backSortingLayerID = _targetRenderer.sortingLayerID;
        _spriteMask.frontSortingLayerID = _targetRenderer.sortingLayerID;
        _spriteMask.backSortingOrder = _targetRenderer.sortingOrder;
        _spriteMask.frontSortingOrder = _targetRenderer.sortingOrder + 2;

        _shineRenderer.sortingLayerID = _targetRenderer.sortingLayerID;
        _shineRenderer.sortingOrder = _targetRenderer.sortingOrder + 1;

        Vector2 targetSize = _targetRenderer.sprite != null
            ? _targetRenderer.sprite.bounds.size
            : new Vector2(1.2f, 1.75f);
        float lineHeight = Mathf.Max(targetSize.x, targetSize.y) * 1.8f;
        float sourceHeight = Mathf.Max(
            0.01f,
            _shineRenderer.sprite.bounds.size.y
        );
        _shineRenderer.transform.localScale = new Vector3(
            lineWidth,
            lineHeight / sourceHeight,
            1f
        );
        _shineRenderer.transform.localRotation = Quaternion.Euler(
            0f,
            0f,
            lineAngle
        );
    }

    private float GetTravelDistance()
    {
        if (_targetRenderer == null || _targetRenderer.sprite == null)
        {
            return 1.5f;
        }

        Vector2 size = _targetRenderer.sprite.bounds.size;
        return Mathf.Max(size.x, size.y) * 0.85f + lineWidth;
    }

    private void HideShine()
    {
        if (_shineRenderer == null)
        {
            return;
        }

        Color color = _shineRenderer.color;
        color.a = 0f;
        _shineRenderer.color = color;
        _shineRenderer.enabled = false;
    }

    private static Sprite GetGradientSprite()
    {
        if (_gradientSprite != null)
        {
            return _gradientSprite;
        }

        const int width = 32;
        const int height = 128;
        _gradientTexture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false
        )
        {
            name = "CardBackShineGradientTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float normalized = x / (float)(width - 1);
                float alpha = 1f - Mathf.Abs(normalized * 2f - 1f);
                alpha = alpha * alpha;
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        _gradientTexture.SetPixels(pixels);
        _gradientTexture.Apply(false, true);

        _gradientSprite = Sprite.Create(
            _gradientTexture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            32f
        );
        _gradientSprite.name = "CardBackShineGradientSprite";
        _gradientSprite.hideFlags = HideFlags.HideAndDontSave;
        return _gradientSprite;
    }
}
