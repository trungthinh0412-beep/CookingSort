using System.Collections;
using UnityEngine;

public class TopBarSlideIn : MonoBehaviour
{
    [SerializeField] private RectTransform target;

    [Header("Direction")]
    [SerializeField] private bool slideFromBottom = false;

    [Header("Animation")]
    [SerializeField] private float hiddenOffset = 260f;
    [SerializeField] private float delay = 0.05f;
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private float overshoot = 12f;

    private float shownY;
    private Coroutine routine;

    private bool waitingForTransition;
    private bool hasPlayed;

    private void Awake()
    {
        if (target == null)
            target = transform as RectTransform;

        if (target == null)
            return;

        shownY = target.anchoredPosition.y;
    }

    private void OnEnable()
    {
        if (target == null)
            return;

        SetHiddenPosition();

        TransitionManager.OnTransitionFinished += HandleTransitionFinished;

        if (TransitionManager.Instance != null &&
            TransitionManager.Instance.IsPlaying)
        {
            waitingForTransition = true;
            return;
        }

        waitingForTransition = false;

        Play();
    }

    private void OnDisable()
    {
        TransitionManager.OnTransitionFinished -= HandleTransitionFinished;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        waitingForTransition = false;
        hasPlayed = false;
    }

    private void HandleTransitionFinished()
    {
        if (!isActiveAndEnabled)
            return;

        if (!waitingForTransition)
            return;

        waitingForTransition = false;

        Play();
    }

    private void SetHiddenPosition()
    {
        Vector2 pos = target.anchoredPosition;

        if (slideFromBottom)
        {
            pos.y = shownY - hiddenOffset;
        }
        else
        {
            pos.y = shownY + hiddenOffset;
        }

        target.anchoredPosition = pos;
    }

    public void Play()
    {
        if (!isActiveAndEnabled)
            return;

        if (target == null)
            return;

        if (hasPlayed)
            return;

        hasPlayed = true;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        SetHiddenPosition();

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        float hiddenY;

        if (slideFromBottom)
        {
            hiddenY = shownY - hiddenOffset;
        }
        else
        {
            hiddenY = shownY + hiddenOffset;
        }

        float overshootY;

        if (slideFromBottom)
        {
            overshootY = shownY + overshoot;
        }
        else
        {
            overshootY = shownY - overshoot;
        }

        Vector2 pos = target.anchoredPosition;

        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            float value = Mathf.Clamp01(
                t / Mathf.Max(duration, 0.001f)
            );

            value =
                1f -
                Mathf.Pow(
                    1f - value,
                    3f
                );

            pos = target.anchoredPosition;

            pos.y = Mathf.Lerp(
                hiddenY,
                overshootY,
                value
            );

            target.anchoredPosition = pos;

            yield return null;
        }

        t = 0f;

        const float settleDuration = 0.1f;

        while (t < settleDuration)
        {
            t += Time.unscaledDeltaTime;

            float value = Mathf.Clamp01(
                t / settleDuration
            );

            value =
                value *
                value *
                (3f - 2f * value);

            pos = target.anchoredPosition;

            pos.y = Mathf.Lerp(
                overshootY,
                shownY,
                value
            );

            target.anchoredPosition = pos;

            yield return null;
        }

        pos = target.anchoredPosition;

        pos.y = shownY;

        target.anchoredPosition = pos;

        routine = null;
    }
}