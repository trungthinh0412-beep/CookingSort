using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A subtle diagonal shine pass for Home side buttons.
/// </summary>
public sealed class HomeButtonDiagonalShine : MonoBehaviour
{
    private const float DefaultTravelDuration = 1.25f;
    private const float DefaultPauseDuration = 2.8f;
    private const float DefaultLineWidth = 34f;
    private const float DefaultMaxAlpha = 0.5f;

    [SerializeField, Range(0f, 1f)] private float maxAlpha = DefaultMaxAlpha;
    [SerializeField] private float travelDuration = DefaultTravelDuration;
    [SerializeField] private float pauseDuration = DefaultPauseDuration;
    [SerializeField] private float lineWidth = DefaultLineWidth;

    private Image shineImage;
    private RectTransform shineRect;
    private RectTransform targetRect;
    private Coroutine shineRoutine;
    private static Sprite whiteSprite;

    private void Awake()
    {
        CreateShineIfNeeded();
    }

    private void OnEnable()
    {
        CreateShineIfNeeded();

        if (shineRoutine == null && shineImage != null)
            shineRoutine = StartCoroutine(PlayShine());
    }

    private void OnDisable()
    {
        if (shineRoutine != null)
            StopCoroutine(shineRoutine);

        shineRoutine = null;

        if (shineImage != null)
        {
            Color hiddenColor = shineImage.color;
            hiddenColor.a = 0f;
            shineImage.color = hiddenColor;
        }
    }

    private void CreateShineIfNeeded()
    {
        if (shineImage != null)
            return;

        targetRect = transform as RectTransform;
        if (targetRect == null)
            return;

        Mask buttonMask = GetComponent<Mask>();
        if (buttonMask == null)
            buttonMask = gameObject.AddComponent<Mask>();

        buttonMask.showMaskGraphic = true;

        GameObject line = new GameObject(
            "HomeButtonShineLine",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        line.transform.SetParent(transform, false);
        line.transform.SetAsLastSibling();

        shineRect = line.GetComponent<RectTransform>();
        shineRect.anchorMin = new Vector2(0.5f, 0.5f);
        shineRect.anchorMax = new Vector2(0.5f, 0.5f);
        shineRect.pivot = new Vector2(0.5f, 0.5f);
        shineRect.localRotation = Quaternion.Euler(0f, 0f, -35f);

        shineImage = line.GetComponent<Image>();
        shineImage.sprite = GetWhiteSprite();
        shineImage.type = Image.Type.Simple;
        shineImage.raycastTarget = false;
        shineImage.color = new Color(1f, 0.96f, 0.72f, 0f);

        UpdateLineSize();
    }

    private IEnumerator PlayShine()
    {
        yield return new WaitForSecondsRealtime(Random.Range(0f, pauseDuration));

        while (shineImage != null && shineImage.gameObject.activeInHierarchy)
        {
            UpdateLineSize();

            float travelDistance = GetTravelDistance();
            float elapsed = 0f;

            while (elapsed < travelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / travelDuration);
                float eased = progress * progress * (3f - 2f * progress);

                shineRect.anchoredPosition = new Vector2(
                    Mathf.Lerp(-travelDistance, travelDistance, eased),
                    0f
                );

                float fadeIn = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(progress / 0.22f)
                );

                float fadeOut = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01((1f - progress) / 0.22f)
                );

                Color color = shineImage.color;
                color.a = maxAlpha * fadeIn * fadeOut;
                shineImage.color = color;

                yield return null;
            }

            shineRect.anchoredPosition = new Vector2(-travelDistance, 0f);
            Color hiddenColor = shineImage.color;
            hiddenColor.a = 0f;
            shineImage.color = hiddenColor;

            yield return new WaitForSecondsRealtime(pauseDuration);
        }

        shineRoutine = null;
    }

    private void UpdateLineSize()
    {
        if (targetRect == null || shineRect == null)
            return;

        Vector2 size = targetRect.rect.size;
        shineRect.sizeDelta = new Vector2(lineWidth, Mathf.Max(size.y * 1.55f, 80f));
    }

    private float GetTravelDistance()
    {
        if (targetRect == null)
            return 180f;

        Vector2 size = targetRect.rect.size;
        return Mathf.Max(size.x, size.y) * 0.72f;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        whiteSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
        whiteSprite.name = "HomeButtonRuntimeWhiteSprite";
        return whiteSprite;
    }
}
