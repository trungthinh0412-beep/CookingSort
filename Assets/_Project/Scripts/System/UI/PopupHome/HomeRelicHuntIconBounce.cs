using System.Collections;
using UnityEngine;

public sealed class HomeRelicHuntIconBounce : MonoBehaviour
{
    private const float JumpHeight = 24f;
    private const float JumpUpDuration = 0.13f;
    private const float FallDuration = 0.18f;
    private const float SettleDuration = 0.08f;
    private const float LandingSquash = 2f;
    private const float RightTiltAngle = -5f;
    private const float LandingTiltAngle = -1.2f;
    private const float RepeatDelay = 10f;

    private RectTransform rectTransform;
    private Coroutine bounceRoutine;
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

        if (bounceRoutine == null)
            bounceRoutine = StartCoroutine(PlayLoop());
    }

    private void OnDisable()
    {
        if (bounceRoutine != null)
            StopCoroutine(bounceRoutine);

        bounceRoutine = null;

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = startPosition;
            rectTransform.localRotation = startRotation;
        }
    }

    private IEnumerator PlayLoop()
    {
        yield return new WaitForSecondsRealtime(Random.Range(0.4f, 1.2f));

        while (rectTransform != null && gameObject.activeInHierarchy)
        {
            startPosition = rectTransform.anchoredPosition;
            startRotation = rectTransform.localRotation;

            yield return PlayBounce();

            rectTransform.anchoredPosition = startPosition;
            rectTransform.localRotation = startRotation;

            yield return new WaitForSecondsRealtime(RepeatDelay);
        }

        bounceRoutine = null;
    }

    private IEnumerator PlayBounce()
    {
        Vector2 topPosition = startPosition + new Vector2(0f, JumpHeight);
        Vector2 squashPosition = startPosition + new Vector2(0f, -LandingSquash);

        yield return MoveAndRotate(
            startPosition,
            topPosition,
            0f,
            RightTiltAngle,
            JumpUpDuration,
            EaseOutQuad
        );

        yield return MoveAndRotate(
            topPosition,
            squashPosition,
            RightTiltAngle,
            LandingTiltAngle,
            FallDuration,
            EaseInCubic
        );

        yield return MoveAndRotate(
            squashPosition,
            startPosition,
            LandingTiltAngle,
            0f,
            SettleDuration,
            EaseOutQuad
        );
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
            rectTransform.localRotation =
                startRotation * Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(fromAngle, toAngle, eased)
                );

            yield return null;
        }

        rectTransform.anchoredPosition = toPosition;
        rectTransform.localRotation =
            startRotation * Quaternion.Euler(0f, 0f, toAngle);
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
