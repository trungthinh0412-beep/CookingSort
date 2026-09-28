using CustomInspector;
using CustomTween;
using UnityEngine;

public class UIEffect : MonoBehaviour
{
    [Header("Data config")]
    [SerializeField] private bool playOnAwake = true;
    [SerializeField] private bool playOnFirstInstantiate;
    [SerializeField] private Ease ease = Ease.OutBack;
    [SerializeField] private bool useUnscaledTime;
    [SerializeField] private float animTime = .5f;
    [SerializeField] private float delayAnimTime;
    [SerializeField] private Vector3 fromScale = Vector3.zero;
    [SerializeField] private Vector3 endScale = Vector3.one;

    [Header("Fade")]
    [SerializeField] private bool useFade;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeStartAlpha;
    [SerializeField] private float fadeEndAlpha = 1f;

    [Header("Bounce scale")]
    [SerializeField] private bool useBounceScale;
    [SerializeField] private bool startAtOvershoot;
    [SerializeField] private Vector3 overshootScale = Vector3.one * 1.15f;
    [SerializeField] private Vector3 undershootScale = Vector3.one * .9f;
    [SerializeField] private float overshootDuration = .12f;
    [SerializeField] private float undershootDuration = .1f;
    [SerializeField] private float settleDuration = .14f;

    private Vector3 _saveLocalScale;
    private Vector3 _saveAnchorPosition;
    private RectTransform _rectTransform;
    private Tween _tween;
    private Tween _fadeTween;
    private bool _isFirstInstantiate = true;

    public void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _saveAnchorPosition = _rectTransform.anchoredPosition;
        _saveLocalScale = _rectTransform.localScale;
    }

    public void OnEnable()
    {
        if (_isFirstInstantiate)
        {
            _isFirstInstantiate = false;
            if (playOnFirstInstantiate)
            {
                PlayAnim();
            }
        }
        else
        {
            if (playOnAwake)
            {
                PlayAnim();
            }
        }
    }

    public void PlayAnim()
    {
        PlayFade();

        if (useBounceScale)
        {
            PlayBounceScale();
            return;
        }

        if (playOnFirstInstantiate)
        {
            transform.localScale = fromScale;
            _tween = Tween.Scale(transform, fromScale, endScale, animTime, ease, startDelay: delayAnimTime, useUnscaledTime: useUnscaledTime);
        }
        else
        {
            transform.localScale = fromScale;
            _tween = Tween.Scale(transform, endScale, animTime, ease, startDelay: delayAnimTime, useUnscaledTime: useUnscaledTime);
        }
    }

    private void PlayBounceScale()
    {
        transform.localScale = startAtOvershoot
            ? overshootScale
            : endScale;

        if (startAtOvershoot)
        {
            PlayUndershootThenSettle();
            return;
        }

        _tween = Tween.Scale(
            transform,
            endScale,
            overshootScale,
            overshootDuration,
            Ease.OutQuad,
            startDelay: delayAnimTime,
            useUnscaledTime: useUnscaledTime
        ).OnComplete(() =>
        {
            PlayUndershootThenSettle();
        });
    }

    private void PlayUndershootThenSettle()
    {
        _tween = Tween.Scale(
            transform,
            overshootScale,
            undershootScale,
            undershootDuration,
            Ease.InQuad,
            startDelay: startAtOvershoot ? delayAnimTime : 0f,
            useUnscaledTime: useUnscaledTime
        ).OnComplete(() =>
        {
            _tween = Tween.Scale(
                transform,
                undershootScale,
                endScale,
                settleDuration,
                Ease.OutQuad,
                useUnscaledTime: useUnscaledTime
            );
        });
    }

    private void PlayFade()
    {
        if (!useFade || fadeCanvasGroup == null)
            return;

        _fadeTween.Stop();
        fadeCanvasGroup.alpha = fadeStartAlpha;

        float duration = useBounceScale
            ? (startAtOvershoot ? 0f : overshootDuration) +
              undershootDuration + settleDuration
            : animTime;

        _fadeTween = Tween.Alpha(
            fadeCanvasGroup,
            fadeStartAlpha,
            fadeEndAlpha,
            duration,
            startDelay: delayAnimTime,
            useUnscaledTime: useUnscaledTime
        );
    }

    public void OnDisable()
    {
        Reset();
        _tween.Stop();
        _fadeTween.Stop();
    }


    public void Reset()
    {
        if (!Application.isPlaying) return;
        _rectTransform = GetComponent<RectTransform>();
        _rectTransform.anchoredPosition = _saveAnchorPosition;
        _rectTransform.localScale = _saveLocalScale;

        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = fadeEndAlpha;
    }
}
