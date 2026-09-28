using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class ProfileClockAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform secondHand;
    [SerializeField] private float stepDuration = 1f;
    [SerializeField] private float overshootAngle = 7f;
    [SerializeField] private float overshootDuration = 0.12f;
    [SerializeField] private float snapDuration = 0.14f;

    private Coroutine animationRoutine;

    private void Awake() => FindSecondHand();

    private void OnEnable()
    {
        FindSecondHand();
        if (secondHand != null)
            animationRoutine = StartCoroutine(AnimateClock());
    }

    private void OnDisable()
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = null;
    }

    private void FindSecondHand()
    {
        if (secondHand != null)
            return;

        foreach (Image image in GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null)
                continue;

            string spriteName = image.sprite.name.ToLowerInvariant();
            if (!spriteName.Contains("clock_second") && !spriteName.Contains("clocksecond"))
                continue;

            secondHand = image.rectTransform;
            image.gameObject.name = "ClockSecondHand";
            if (image.transform.parent != null)
            {
                image.transform.parent.gameObject.name = "ClockIcon";
                secondHand.anchorMin = new Vector2(0.5f, 0.5f);
                secondHand.anchorMax = new Vector2(0.5f, 0.5f);
                secondHand.pivot = new Vector2(0.5f, 0.5f);
            }
            break;
        }
    }

    private IEnumerator AnimateClock()
    {
        // One complete turn has four stops, each 90 degrees apart.
        float[] angles = { 90f, 180f, 270f, 360f };
        int index = 0;

        while (true)
        {
            float startAngle = secondHand.localEulerAngles.z;
            // UI rotation is inverted relative to the clock direction.
            float targetAngle = -angles[index];
            // Wait almost one second, then make the 45-degree movement a
            // short, strong mechanical tick.
            float holdDuration = Mathf.Max(0.05f, stepDuration - snapDuration - overshootDuration * 2f);
            float elapsed = 0f;

            while (elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < snapDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, snapDuration));
                // Fast start, softer arrival at the target.
                t = 1f - Mathf.Pow(1f - t, 3f);
                secondHand.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(startAngle, targetAngle, t));
                yield return null;
            }

            // Recoil backwards a few degrees, then settle precisely on the
            // 90-degree target.
            yield return Nudge(targetAngle + overshootAngle, targetAngle);
            secondHand.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
            index = (index + 1) % angles.Length;
        }
    }

    private IEnumerator Nudge(float overshootTarget, float target)
    {
        float elapsed = 0f;
        while (elapsed < overshootDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, overshootDuration));
            t = Mathf.Sin(t * Mathf.PI * 0.5f);
            secondHand.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(target, overshootTarget, t));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < overshootDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, overshootDuration));
            t = t * t * (3f - 2f * t);
            secondHand.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(overshootTarget, target, t));
            yield return null;
        }
    }
}
