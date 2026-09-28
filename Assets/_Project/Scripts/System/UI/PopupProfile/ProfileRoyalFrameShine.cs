using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A light pass used only by the New_frame_royal frame in PopupProfile.
/// </summary>
public sealed class ProfileRoyalFrameShine : MonoBehaviour
{
    private const float TravelDistance = 170f;
    private const float TravelDuration = 1.5f;
    private const float PauseDuration = 1.2f;

    private Image shineImage;
    private RectTransform shineRect;
    private Coroutine shineRoutine;
    private static Sprite whiteSprite;

    private void Awake()
    {
        CreateShine();
    }

    private void OnEnable()
    {
        CreateShine();
        if (shineRoutine == null && shineImage != null)
            shineRoutine = StartCoroutine(PlayShine());
    }

    private void OnDisable()
    {
        if (shineRoutine != null)
            StopCoroutine(shineRoutine);
        shineRoutine = null;
    }

    private void CreateShine()
    {
        // Use the frame sprite's alpha as a stencil mask. The shine is a
        // child of this object, so this confines it to the visible frame
        // border instead of letting it draw over the avatar/outside area.
        Mask frameMask = GetComponent<Mask>();
        if (frameMask == null)
            frameMask = gameObject.AddComponent<Mask>();

        frameMask.showMaskGraphic = true;

        if (shineImage != null)
            return;

        GameObject line = new GameObject(
            "RoyalFrameShineLine",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        line.transform.SetParent(transform, false);

        shineRect = line.GetComponent<RectTransform>();
        shineRect.anchorMin = new Vector2(0.5f, 0.5f);
        shineRect.anchorMax = new Vector2(0.5f, 0.5f);
        shineRect.pivot = new Vector2(0.5f, 0.5f);
        shineRect.sizeDelta = new Vector2(12f, 260f);
        shineRect.localRotation = Quaternion.Euler(0f, 0f, -35f);
        shineRect.anchoredPosition = new Vector2(-TravelDistance, 0f);

        shineImage = line.GetComponent<Image>();
        shineImage.sprite = GetWhiteSprite();
        shineImage.color = new Color(1f, 0.94f, 0.58f, 0f);
        shineImage.raycastTarget = false;
    }

    private IEnumerator PlayShine()
    {
        while (shineImage != null && shineImage.gameObject.activeInHierarchy)
        {
            float elapsed = 0f;
            while (elapsed < TravelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / TravelDuration);
                float eased = progress * progress * (3f - 2f * progress);

                shineRect.anchoredPosition = new Vector2(
                    Mathf.Lerp(-TravelDistance, TravelDistance, eased),
                    0f
                );

                float alpha = Mathf.Sin(progress * Mathf.PI) * 0.72f;
                Color color = shineImage.color;
                color.a = alpha;
                shineImage.color = color;
                yield return null;
            }

            shineRect.anchoredPosition = new Vector2(-TravelDistance, 0f);
            Color hiddenColor = shineImage.color;
            hiddenColor.a = 0f;
            shineImage.color = hiddenColor;
            yield return new WaitForSecondsRealtime(PauseDuration);
        }

        shineRoutine = null;
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
        whiteSprite.name = "RoyalFrameRuntimeWhiteSprite";
        return whiteSprite;
    }
}
