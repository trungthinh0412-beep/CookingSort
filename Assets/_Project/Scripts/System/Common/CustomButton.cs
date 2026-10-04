using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CustomInspector;
using CustomTween;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[AddComponentMenu("UI/Custom Button", 30)]
public class CustomButton : UIBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler,
    IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
{
    [SerializeField] private bool interactable = true;
    [SerializeField] private Color normalColor = Color.white;

    [SerializeField] private ButtonDisableType disableType = ButtonDisableType.None;
    [SerializeField][ShowIf("disableType", ButtonDisableType.Color)] private Color disableColor = new Color(200 / 255f, 200 / 255f, 200 / 255f, 128 / 255f);
    [SerializeField][ShowIf("disableType", ButtonDisableType.Sprite)] private Sprite normalSprite;
    [SerializeField][ShowIf("disableType", ButtonDisableType.Sprite)] private Sprite disableSprite;
    [SerializeField][ShowIf("disableType", ButtonDisableType.Material)] private Material normalMaterial;
    [SerializeField][ShowIf("disableType", ButtonDisableType.Material)] private Material disableMaterial;
    [SerializeField] private List<TextMeshProUGUI> changeColorTexts;
    [SerializeField][ShowIf(nameof(UseChangeColorTexts))] private Color normalTextColor = Color.white;
    [SerializeField][ShowIf(nameof(UseChangeColorTexts))] private Color disableTextColor = Color.white;
    [SerializeField] private List<TextMeshProUGUI> changeColorTexts2;
    [SerializeField][ShowIf(nameof(UseChangeColorTexts2))] private Color normalTextColor2 = Color.white;
    [SerializeField][ShowIf(nameof(UseChangeColorTexts2))] private Color disableTextColor2 = Color.white;

    [SerializeField] private bool useFadeColorMotion = true;
    [SerializeField][ShowIf("useFadeColorMotion")] private bool affectSelf;
    [SerializeField][ShowIf("useFadeColorMotion")] private float fadeDuration = .1f;
    [SerializeField][ShowIf("useFadeColorMotion")] private Color pressColor = new Color(200 / 255f, 200 / 255f, 200 / 255f, 255 / 255f);

    [SerializeField] private bool useScaleMotion = true;
    [SerializeField][ShowIf("useScaleMotion")] private float scaleDuration = .2f;
    [SerializeField][ShowIf("useScaleMotion")] private float scalePercent = .9f;
    [SerializeField][ShowIf("useScaleMotion")] private Ease scaleEase = Ease.Default;

    [SerializeField] private bool ignoreTimeScale;
    [SerializeField] private bool useHoldPress;
    [SerializeField][ShowIf("useHoldPress")] private float holdDuration = 0.5f;
    [SerializeField] private string enablePressContent;
    [SerializeField] private string disablePressContent;
    [SerializeField][ReadOnly] private ButtonPressState buttonPressState;

    private RectTransform _rectTransform;

    private bool UseChangeColorTexts()
    {
        return changeColorTexts is { Count: > 0 };
    }

    private bool UseChangeColorTexts2()
    {
        return changeColorTexts2 is { Count: > 0 };
    }

    public bool Interactable
    {
        get => interactable;
        set
        {
            interactable = value;
            SetupButtonInteractable(interactable, false);
        }
    }

    public Color NormalColor
    {
        get => normalColor;
        set
        {
            normalColor = value;
            SetupButtonInteractable(interactable, true);
        }
    }

    public float ScaleDuration
    {
        get => scaleDuration;
        set => scaleDuration = Mathf.Max(0f, value);
    }

    public RectTransform RectTransform
    {
        get => _rectTransform;
        set => _rectTransform = value;
    }

    public string DisablePressContent
    {
        get => disablePressContent;
        set => disablePressContent = value;
    }

    private Image _targetImage;
    private List<MaskableGraphic> _maskableGraphics;

    private Image targetImage => _targetImage ??= GetComponent<Image>();
    private List<MaskableGraphic> MaskableGraphic =>
        _maskableGraphics ??= GetComponentsInChildren<MaskableGraphic>(true).ToList();
    private bool isHolding;
    private float holdTimer;
    private Vector3 _currentScale;

    // 🎯 New Events
    [Serializable] public class ButtonClickedEvent : UnityEvent { }
    [Serializable] public class ButtonHoldEvent : UnityEvent { }
    [Serializable] public class ButtonMouseUpEvent : UnityEvent { }
    [Serializable] public class ButtonPointerDownEvent : UnityEvent { }  // ✅ NEW

    [SerializeField] private ButtonClickedEvent m_OnClick = new ButtonClickedEvent();
    [SerializeField] private ButtonHoldEvent m_OnHold = new ButtonHoldEvent();
    [SerializeField] private ButtonMouseUpEvent m_OnMouseUp = new ButtonMouseUpEvent();
    [SerializeField] private ButtonPointerDownEvent m_OnPointerDown = new ButtonPointerDownEvent();  // ✅ NEW

    public ButtonClickedEvent Click => m_OnClick;
    public ButtonHoldEvent Hold => m_OnHold;
    public ButtonMouseUpEvent MouseUp => m_OnMouseUp;
    public ButtonPointerDownEvent onPointerDown => m_OnPointerDown;  // ✅ NEW
    public ButtonPressState PressState => buttonPressState;
    public float HoldDuration => holdDuration;

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetupButtonInteractable(interactable, true);
    }
#endif

    protected override void Awake()
    {
        base.Awake();
        _targetImage = GetComponent<Image>();
        _currentScale = transform.localScale;
        _rectTransform = GetComponent<RectTransform>();
    }

    private void OnTransformChildrenChanged()
    {
        _maskableGraphics = null;
    }

    private void Press()
    {
        if (!IsActive() || !IsInteractable()) return;
        m_OnClick.Invoke();
    }

    private bool IsInteractable() => interactable;

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        Press();
    }

    public virtual void OnPointerDown(PointerEventData eventData)
    {
        if (useScaleMotion) Tween.Scale(transform, _currentScale * scalePercent, scaleDuration, scaleEase, useUnscaledTime: ignoreTimeScale);
        if (!interactable) return;
        buttonPressState = ButtonPressState.Pressed;
        if (useFadeColorMotion) DoStateTransition(ButtonPressState.Pressed, false, affectSelf);
        if (useHoldPress) { isHolding = true; holdTimer = 0f; StartCoroutine(HoldPressRoutine()); }

        // ✅ Invoke the OnPointerDown event
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            m_OnPointerDown.Invoke();
        }
    }

    public virtual void OnPointerUp(PointerEventData eventData)
    {
        buttonPressState = ButtonPressState.Normal;
        if (useFadeColorMotion) DoStateTransition(ButtonPressState.Normal, false, affectSelf);
        if (useScaleMotion) Tween.Scale(transform, _currentScale, scaleDuration, scaleEase, useUnscaledTime: ignoreTimeScale);
        isHolding = false;

        // ✅ Invoke the Mouse Up event
        if (eventData.button == PointerEventData.InputButton.Left && interactable)
        {
            m_OnMouseUp.Invoke();
        }

        
    }

    private IEnumerator HoldPressRoutine()
    {
        while (isHolding)
        {
            holdTimer += Time.unscaledDeltaTime;
            m_OnHold.Invoke();
            yield return null;
        }
    }

    protected virtual void DoStateTransition(ButtonPressState state, bool instant, bool affectSelf)
    {
        if (!gameObject.activeInHierarchy) return;

        Color targetColor = state == ButtonPressState.Pressed ? pressColor : normalColor;
        if (affectSelf)
        {
            Tween.Color(targetImage, targetColor, fadeDuration, useUnscaledTime: ignoreTimeScale);
        }
        else
        {
            foreach (var img in MaskableGraphic)
            {
                Tween.Color(img, targetColor, fadeDuration, useUnscaledTime: ignoreTimeScale);
            }
        }
    }

    private void SetupButtonInteractable(bool isInteracted, bool instant)
    {
        if (isInteracted)
        {
            switch (disableType)
            {
                case ButtonDisableType.None:
                    break;
                case ButtonDisableType.Color:
                    foreach (var maskableGraphic in MaskableGraphic) maskableGraphic.color = normalColor;
                    break;
                case ButtonDisableType.Sprite:
                    targetImage.sprite = normalSprite;
                    break;
                case ButtonDisableType.Material:
                    if (affectSelf)
                    {
                        targetImage.material = normalMaterial;
                    }
                    else
                    {
                        foreach (var img in MaskableGraphic)
                        {
                            img.material = normalMaterial;
                        }
                    }

                    break;
            }
        }
        else
        {
            switch (disableType)
            {
                case ButtonDisableType.None:
                    break;
                case ButtonDisableType.Color:
                    foreach (var maskableGraphic in MaskableGraphic) maskableGraphic.color = disableColor;
                    break;
                case ButtonDisableType.Sprite:
                    targetImage.sprite = disableSprite;
                    break;
                case ButtonDisableType.Material:
                    if (affectSelf)
                    {
                        targetImage.material = disableMaterial;
                    }
                    else
                    {
                        foreach (var img in MaskableGraphic)
                        {
                            img.material = disableMaterial;
                        }
                    }

                    break;
            }
        }

        if (disableType == ButtonDisableType.None || changeColorTexts == null || changeColorTexts.Count == 0) return;
        if (isInteracted)
        {
            foreach (var textMeshProUGUI in changeColorTexts)
            {
                textMeshProUGUI.color = normalTextColor;
            }

            if (changeColorTexts2 == null || changeColorTexts2.Count == 0) return;
            foreach (var textMeshProUGUI in changeColorTexts2)
            {
                textMeshProUGUI.color = normalTextColor2;
            }
        }
        else
        {
            foreach (var textMeshProUGUI in changeColorTexts)
            {
                textMeshProUGUI.color = disableTextColor;
            }
            if (changeColorTexts2 == null || changeColorTexts2.Count == 0) return;
            foreach (var textMeshProUGUI in changeColorTexts2)
            {
                textMeshProUGUI.color = disableTextColor2;
            }
        }
    }

    public void OnSubmit(BaseEventData eventData) => Press();
    public void OnPointerEnter(PointerEventData eventData) { }
    public void OnPointerExit(PointerEventData eventData) { }
    public void OnSelect(BaseEventData eventData) { }
    public void OnDeselect(BaseEventData eventData) { }
}

public enum ButtonPressState
{
    Normal,
    Pressed,
}

public enum ButtonDisableType
{
    None,
    Color,
    Sprite,
    Material,
}
