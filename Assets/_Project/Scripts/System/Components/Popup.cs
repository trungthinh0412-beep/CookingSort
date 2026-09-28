using System;
using CustomTween;
using UnityEngine;
using UnityEngine.Serialization;

public class Popup : MonoBehaviour
{
    [SerializeField] protected RectTransform background;
    [SerializeField] protected RectTransform container;
    [FormerlySerializedAs("hideScaleMaxValue")]
    [FormerlySerializedAs("hideScaleStartValue")]
    [Header("Hide animation config")] 
    [SerializeField] private Vector3 scaleMaxValue = Vector3.one;
    [SerializeField] private Vector3 scaleMinValue = new Vector3(.8f, .8f, .8f);
    [SerializeField] private float alphaMaxValue = 1;
    [SerializeField] private float alphaMinValue = 0;
    [SerializeField] private float animationDuration = .2f;
    [SerializeField] private float delayDuration;
    [SerializeField] private Ease showEase;
    [SerializeField] private Ease hideEase;

    
    protected bool IsShowing;
    protected bool IsHiding;
        
    private bool _isFirstShow = true;
    private Action _afterHiddenAction;
    private Action _beforeHiddenAction;
    private Tween _alphaTween;
    private Tween _scaleTween;
    private int _visualAnimationVersion;

    public Action AfterHiddenAction
    {
        get => _afterHiddenAction;
        set => _afterHiddenAction = value;
    }

    public Action BeforeHiddenAction
    {
        get => _beforeHiddenAction;
        set => _beforeHiddenAction = value;
    }

    public bool IsFirstShow
    {
        get => _isFirstShow;
        set => _isFirstShow = value;
    }
    
    public CanvasGroup CanvasGroup => GetComponent<CanvasGroup>();
    public Canvas Canvas => GetComponent<Canvas>();

    protected virtual void OnEnable()
    {
        CanvasGroup canvasGroup = CanvasGroup;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alphaMaxValue;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        transform.localScale = Vector3.one;

        if (container != null)
            container.localScale = scaleMaxValue;

        if (_isFirstShow)
        {
            OnInstantiate();
        }
    }

    protected virtual void OnDisable()
    {
        _visualAnimationVersion++;
        IsShowing = false;
        IsHiding = false;
        _isFirstShow = false;
    }

    protected virtual void OnInstantiate()
    {
        
    }

    public virtual void Show(PopupAnimation animation = PopupAnimation.None)
    {
        StopVisualTweens();
        BeforeShow();

        gameObject.SetActive(true);

        CanvasGroup canvasGroup = CanvasGroup;
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        int animationVersion = _visualAnimationVersion;

        switch (animation)
        {
            case PopupAnimation.None:
                SetVisualState(alphaMaxValue, scaleMaxValue);
                AfterShown();
                break;
            case PopupAnimation.ScaleFade:
                IsShowing = true;
                SetVisualState(alphaMinValue, scaleMinValue);
                _alphaTween = Tween.Alpha(canvasGroup, alphaMinValue, alphaMaxValue, animationDuration,
                    useUnscaledTime: true);
                _scaleTween = Tween.Scale(container, scaleMinValue, scaleMaxValue, animationDuration, showEase,
                    startDelay: delayDuration, useUnscaledTime: true).OnComplete(
                    () =>
                    {
                        if (animationVersion != _visualAnimationVersion)
                            return;

                        IsShowing = false;
                        SetVisualState(alphaMaxValue, scaleMaxValue);
                        AfterShown();
                    });
                break;
            case PopupAnimation.ScaleFade2:
                IsShowing = true;
                SetVisualState(alphaMinValue, scaleMaxValue);
                _alphaTween = Tween.Alpha(canvasGroup, alphaMinValue, alphaMaxValue, animationDuration,
                    useUnscaledTime: true);
                _scaleTween = Tween.Scale(container, scaleMaxValue, scaleMinValue, animationDuration, showEase,
                    startDelay: delayDuration, useUnscaledTime: true).OnComplete(
                    () =>
                    {
                        if (animationVersion != _visualAnimationVersion)
                            return;

                        IsShowing = false;
                        SetVisualState(alphaMaxValue, scaleMinValue);
                        AfterShown();
                    });
                break;
            case PopupAnimation.FadeOnly:
                IsShowing = true;
                SetVisualState(alphaMinValue, scaleMaxValue);
                _alphaTween = Tween.Alpha(canvasGroup, alphaMinValue, alphaMaxValue, animationDuration,
                        useUnscaledTime: true)
                    .OnComplete(() =>
                    {
                        if (animationVersion != _visualAnimationVersion)
                            return;

                        IsShowing = false;
                        SetVisualState(alphaMaxValue, scaleMaxValue);
                        AfterShown();
                    });
                break;
        }
    }

    public virtual void Hide(PopupAnimation animation = PopupAnimation.None)
    {
        if (IsHiding && (_alphaTween.isAlive || _scaleTween.isAlive))
            return;

        StopVisualTweens();
        BeforeHide();

        CanvasGroup canvasGroup = CanvasGroup;
        int animationVersion = _visualAnimationVersion;

        switch (animation)
        {
            case PopupAnimation.None:
                SetVisualState(alphaMaxValue, scaleMaxValue);
                gameObject.SetActive(false);
                AfterHidden();
                break;
            case PopupAnimation.ScaleFade:
                IsHiding = true;
                SetVisualState(alphaMaxValue, scaleMaxValue);
                _alphaTween = Tween.Alpha(canvasGroup, alphaMaxValue, alphaMinValue, animationDuration,
                    useUnscaledTime: true);
                _scaleTween = Tween.Scale(container, scaleMaxValue, scaleMinValue, animationDuration, hideEase,
                    startDelay: delayDuration, useUnscaledTime: true).OnComplete(
                    () =>
                    {
                        if (animationVersion != _visualAnimationVersion)
                            return;

                        IsHiding = false;
                        gameObject.SetActive(false);
                        AfterHidden();
                    });
                break;
            case PopupAnimation.ScaleFade2:
                IsHiding = true;
                SetVisualState(alphaMinValue, scaleMinValue);
                _alphaTween = Tween.Alpha(canvasGroup, alphaMinValue, alphaMaxValue, animationDuration,
                    useUnscaledTime: true);
                _scaleTween = Tween.Scale(container, scaleMinValue, scaleMaxValue, animationDuration, showEase,
                    startDelay: delayDuration, useUnscaledTime: true).OnComplete(
                    () =>
                    {
                        if (animationVersion != _visualAnimationVersion)
                            return;

                        IsHiding = false;
                        gameObject.SetActive(false);
                        AfterHidden();
                    });
                break;
            case PopupAnimation.FadeOnly:
                IsHiding = true;
                SetVisualState(alphaMaxValue, scaleMaxValue);
                _alphaTween = Tween.Alpha(canvasGroup, alphaMaxValue, alphaMinValue, animationDuration,
                        useUnscaledTime: true)
                    .OnComplete(() =>
                    {
                        if (animationVersion != _visualAnimationVersion)
                            return;

                        IsHiding = false;
                        gameObject.SetActive(false);
                        AfterHidden();
                    });
                break;
        }
    }

    private void StopVisualTweens()
    {
        _visualAnimationVersion++;

        if (_alphaTween.isAlive)
            _alphaTween.Stop();

        if (_scaleTween.isAlive)
            _scaleTween.Stop();

        IsShowing = false;
        IsHiding = false;
    }

    private void SetVisualState(float alpha, Vector3 scale)
    {
        CanvasGroup canvasGroup = CanvasGroup;
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;

        if (container != null)
            container.localScale = scale;
    }

    protected virtual void BeforeShow()
    {
    }

    protected virtual void AfterShown()
    {
    }

    protected virtual void BeforeHide()
    {
        BeforeHiddenAction?.Invoke();
        BeforeHiddenAction = null;
    }

    protected virtual void AfterHidden()
    {
        AfterHiddenAction?.Invoke();
        AfterHiddenAction = null;
    }
}

public enum PopupAnimation
{
    None,
    ScaleFade,
    ScaleFade2,
    FadeOnly,
}
