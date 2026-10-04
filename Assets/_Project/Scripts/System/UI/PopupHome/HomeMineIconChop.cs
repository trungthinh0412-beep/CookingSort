using System.Collections;
using UnityEngine;

/// <summary>
/// Gives the mine axe a short lift, downward chop and impact settle.
/// </summary>
public sealed class HomeMineIconChop : MonoBehaviour
{
    private const float LiftHeight = 12f;
    private const float LiftDuration = 0.2f;
    private const float ChopDuration = 0.14f;
    private const float SettleDuration = 0.1f;
    // The sprite faces down-right from its handle base. Negative rotation
    // lifts the pick head; positive rotation drives the head into the ground.
    private const float LiftAngle = -24f;
    private const float ImpactAngle = 24f;
    private const float ImpactOffset = 2f;
    private const float RepeatDelay = 4.5f;
    private RectTransform rectTransform;
    private Coroutine chopRoutine;
    private Vector2 startPosition;
    private Quaternion startRotation;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
    }

    private void OnEnable()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (rectTransform == null)
            return;

        startPosition = rectTransform.anchoredPosition;
        startRotation = rectTransform.localRotation;

        if (chopRoutine == null)
            chopRoutine = StartCoroutine(PlayLoop());
    }

    private void OnDisable()
    {
        if (chopRoutine != null)
            StopCoroutine(chopRoutine);

        chopRoutine = null;

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = startPosition;
            rectTransform.localRotation = startRotation;
        }
    }

    private IEnumerator PlayLoop()
    {
        yield return new WaitForSecondsRealtime(Random.Range(0.2f, 0.8f));

        while (rectTransform != null && gameObject.activeInHierarchy)
        {
            startPosition = rectTransform.anchoredPosition;
            startRotation = rectTransform.localRotation;

            yield return MoveAndRotate(
                startPosition,
                startPosition + new Vector2(0f, LiftHeight),
                0f,
                LiftAngle,
                LiftDuration,
                EaseOutQuad
            );

            yield return MoveAndRotate(
                rectTransform.anchoredPosition,
                startPosition + new Vector2(0f, -ImpactOffset),
                LiftAngle,
                ImpactAngle,
                ChopDuration,
                EaseInCubic
            );

            yield return MoveAndRotate(
                rectTransform.anchoredPosition,
                startPosition,
                ImpactAngle,
                0f,
                SettleDuration,
                EaseOutQuad
            );

            yield return new WaitForSecondsRealtime(RepeatDelay);
        }

        chopRoutine = null;
    }

    private IEnumerator MoveAndRotate(
        Vector2 fromPosition,
        Vector2 toPosition,
        float fromAngle,
        float toAngle,
        float duration,
        System.Func<float, float> easing)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = easing(progress);

            rectTransform.anchoredPosition =
                Vector2.Lerp(fromPosition, toPosition, eased);
            rectTransform.localRotation = startRotation *
                Quaternion.Euler(0f, 0f, Mathf.Lerp(fromAngle, toAngle, eased));

            yield return null;
        }

        rectTransform.anchoredPosition = toPosition;
        rectTransform.localRotation = startRotation *
            Quaternion.Euler(0f, 0f, toAngle);
    }

    private static float EaseOutQuad(float progress)
    {
        return 1f - (1f - progress) * (1f - progress);
    }

    private static float EaseInCubic(float progress)
    {
        return progress * progress * progress;
    }
}
