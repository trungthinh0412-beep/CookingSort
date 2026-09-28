using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class PurpleAvatarShine : MonoBehaviour
{
    private const float TravelDistance = 190f;
    private const float TravelDuration = 1.7f;

    private Image shineLine;
    private RectTransform shineRect;
    private Coroutine shineRoutine;
    private bool isVisible;
    private static Sprite whiteSprite;

    private void Awake()
    {
        CreateLineIfNeeded();
    }

    private void OnEnable()
    {
        if (isVisible)
            StartShineIfPossible();
    }

    public void SetVisible(bool visible)
    {
        CreateLineIfNeeded();

        if (shineLine == null)
        {
            return;
        }

        if (!visible)
        {
            isVisible = false;
            StopShine();
            shineLine.gameObject.SetActive(false);
            return;
        }

        // ConfigureTestRankItem can be called repeatedly while a ranking is
        // refreshed. Do not restart/reset a shine that is already running.
        if (isVisible)
        {
            shineLine.gameObject.SetActive(true);
            StartShineIfPossible();
            return;
        }

        isVisible = true;
        shineLine.gameObject.SetActive(true);

        Color resetColor = shineLine.color;
        resetColor.a = 0.82f;
        shineLine.color = resetColor;

        StartShineIfPossible();
    }

    private void OnDisable()
    {
        StopShine();
    }

    private void StartShineIfPossible()
    {
        if (!isActiveAndEnabled ||
            shineLine == null ||
            !shineLine.gameObject.activeSelf ||
            shineRoutine != null)
        {
            return;
        }

        shineRoutine = StartCoroutine(PlayShine());
    }

    private void StopShine()
    {
        if (shineRoutine == null)
            return;

        StopCoroutine(shineRoutine);
        shineRoutine = null;
    }

    private void CreateLineIfNeeded()
    {
        if (shineLine != null)
        {
            return;
        }

        GameObject lineObject =
            new GameObject(
                "PurpleAvatarShineLine",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        lineObject.transform.SetParent(transform, false);

        shineRect = lineObject.GetComponent<RectTransform>();
        shineRect.anchorMin = new Vector2(0.5f, 0.5f);
        shineRect.anchorMax = new Vector2(0.5f, 0.5f);
        shineRect.pivot = new Vector2(0.5f, 0.5f);
        shineRect.sizeDelta = new Vector2(13f, 170f);
        shineRect.localRotation = Quaternion.Euler(0f, 0f, -35f);
        shineRect.anchoredPosition =
            new Vector2(-TravelDistance, 0f);

        shineLine = lineObject.GetComponent<Image>();
        shineLine.sprite = GetWhiteSprite();
        shineLine.color = new Color(1f, 1f, 1f, 0.82f);
        shineLine.raycastTarget = false;
        shineLine.type = Image.Type.Simple;
        lineObject.SetActive(false);
    }

    private IEnumerator PlayShine()
    {
        while (shineLine != null && shineLine.gameObject.activeSelf)
        {
            float elapsed = 0f;
            Color baseColor = shineLine.color;

            while (elapsed < TravelDuration &&
                   shineLine != null &&
                   shineLine.gameObject.activeSelf)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(
                    elapsed / TravelDuration
                );

                float eased =
                    progress * progress * (3f - 2f * progress);

                shineRect.anchoredPosition =
                    new Vector2(
                        Mathf.Lerp(
                            -TravelDistance,
                            TravelDistance,
                            eased
                        ),
                        0f
                    );

                // Fade at the beginning and end of the pass. The line is
                // invisible while it is reset to the starting position, so
                // the loop does not visibly snap or jerk.
                float fadeIn = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(progress / 0.18f)
                );

                float fadeOut = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01((1f - progress) / 0.18f)
                );

                Color animatedColor = baseColor;
                animatedColor.a = baseColor.a * fadeIn * fadeOut;
                shineLine.color = animatedColor;

                yield return null;
            }

            if (shineRect != null)
            {
                shineRect.anchoredPosition =
                    new Vector2(-TravelDistance, 0f);
            }

            if (shineLine != null)
            {
                Color hiddenColor = baseColor;
                hiddenColor.a = 0f;
                shineLine.color = hiddenColor;
            }

            yield return new WaitForSecondsRealtime(0.2f);

            if (shineLine != null)
            {
                shineLine.color = baseColor;
            }
        }

        shineRoutine = null;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            whiteSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f
            );
            whiteSprite.name = "RuntimeWhiteSprite";
        }

        return whiteSprite;
    }
}
