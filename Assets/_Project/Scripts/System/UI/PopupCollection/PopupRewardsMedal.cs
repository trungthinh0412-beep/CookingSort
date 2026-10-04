using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public sealed class PopupRewardsMedal : Popup
{
    [Header("Reward Medal")]
    [SerializeField] private RectTransform medalRoot;
    [SerializeField] private Vector2 medalCenterPosition = Vector2.zero;
    [SerializeField, Min(0.1f)] private float medalPopupScale = 2f;
    [SerializeField, Min(0f)] private float floatingAmplitude = 28f;
    [SerializeField, Min(0.1f)] private float floatingDuration = 1.6f;
    [SerializeField, Range(0f, 10f)] private float floatingTiltAngle = 3f;

    private Transform _medalOriginalParent;
    private int _medalOriginalSiblingIndex;
    private Vector2 _medalOriginalAnchorMin;
    private Vector2 _medalOriginalAnchorMax;
    private Vector2 _medalOriginalPivot;
    private Vector2 _medalOriginalAnchoredPosition;
    private Vector3 _medalOriginalLocalScale;
    private Quaternion _medalOriginalLocalRotation;
    private CustomButton _medalButton;
    private bool _medalButtonOriginalInteractable;
    private readonly List<Graphic> _medalGraphics = new List<Graphic>();
    private readonly List<bool> _medalGraphicRaycastTargets = new List<bool>();
    private bool _hasMedalOriginalState;
    private float _floatingTimer;

    protected override void BeforeShow()
    {
        base.BeforeShow();
        RefreshState();
        ApplyMedalPopupLayout();
    }

    private void Update()
    {
        AnimateFloatingMedal();
    }

    public void Setup(RectTransform medal)
    {
        medalRoot = medal;
    }

    private void RefreshState()
    {
     
    }

    public void Close()
    {
        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        // Match PopupNoAds: a background click dismisses the popup immediately.
        Hide(PopupAnimation.None);
    }

    protected override void AfterHidden()
    {
        RestoreMedal();
        base.AfterHidden();

        PopupController controller = PopupController.Instance;
        if (controller != null && controller.currentPopup == this &&
            controller.Get<PopupCollection>() is PopupCollection collection &&
            collection.isActiveAndEnabled)
        {
            controller.Show<PopupCollection>(PopupAnimation.None);
        }
    }

    private void ApplyMedalPopupLayout()
    {
        if (medalRoot == null || container == null)
            return;

        if (!_hasMedalOriginalState)
            CacheMedalOriginalState();

        medalRoot.SetParent(container, false);
        medalRoot.SetAsLastSibling();
        medalRoot.anchorMin = new Vector2(0.5f, 0.5f);
        medalRoot.anchorMax = new Vector2(0.5f, 0.5f);
        medalRoot.pivot = new Vector2(0.5f, 0.5f);
        medalRoot.anchoredPosition = medalCenterPosition;
        medalRoot.localScale = Vector3.one * medalPopupScale;
        medalRoot.localRotation = Quaternion.identity;

        _floatingTimer = 0f;

        _medalButton = medalRoot.GetComponentInChildren<CustomButton>(true);
        if (_medalButton != null)
        {
            _medalButtonOriginalInteractable = _medalButton.Interactable;
            _medalButton.Interactable = false;
        }

        DisableMedalRaycasts();
    }

    private void AnimateFloatingMedal()
    {
        if (medalRoot == null || !_hasMedalOriginalState || !isActiveAndEnabled)
            return;

        _floatingTimer += Time.unscaledDeltaTime;
        float cycle = Mathf.Max(0.1f, floatingDuration);
        float phase = _floatingTimer * Mathf.PI * 2f / cycle;
        float y = Mathf.Sin(phase) * floatingAmplitude;
        float tilt = Mathf.Sin(phase * 0.75f) * floatingTiltAngle;

        medalRoot.anchoredPosition = medalCenterPosition + new Vector2(0f, y);
        medalRoot.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }

    private void CacheMedalOriginalState()
    {
        _medalOriginalParent = medalRoot.parent;
        _medalOriginalSiblingIndex = medalRoot.GetSiblingIndex();
        _medalOriginalAnchorMin = medalRoot.anchorMin;
        _medalOriginalAnchorMax = medalRoot.anchorMax;
        _medalOriginalPivot = medalRoot.pivot;
        _medalOriginalAnchoredPosition = medalRoot.anchoredPosition;
        _medalOriginalLocalScale = medalRoot.localScale;
        _medalOriginalLocalRotation = medalRoot.localRotation;
        _hasMedalOriginalState = true;
    }

    private void DisableMedalRaycasts()
    {
        _medalGraphics.Clear();
        _medalGraphicRaycastTargets.Clear();

        medalRoot.GetComponentsInChildren(true, _medalGraphics);
        foreach (Graphic graphic in _medalGraphics)
        {
            if (graphic == null)
                continue;

            _medalGraphicRaycastTargets.Add(graphic.raycastTarget);
            graphic.raycastTarget = false;
        }
    }

    private void RestoreMedalRaycasts()
    {
        int count = Mathf.Min(_medalGraphics.Count, _medalGraphicRaycastTargets.Count);
        for (int i = 0; i < count; i++)
        {
            Graphic graphic = _medalGraphics[i];
            if (graphic != null)
                graphic.raycastTarget = _medalGraphicRaycastTargets[i];
        }

        _medalGraphics.Clear();
        _medalGraphicRaycastTargets.Clear();
    }

    private void RestoreMedal()
    {
        if (medalRoot == null || !_hasMedalOriginalState)
            return;

        medalRoot.SetParent(_medalOriginalParent, false);
        medalRoot.SetSiblingIndex(_medalOriginalSiblingIndex);
        medalRoot.anchorMin = _medalOriginalAnchorMin;
        medalRoot.anchorMax = _medalOriginalAnchorMax;
        medalRoot.pivot = _medalOriginalPivot;
        medalRoot.anchoredPosition = _medalOriginalAnchoredPosition;
        medalRoot.localScale = _medalOriginalLocalScale;
        medalRoot.localRotation = _medalOriginalLocalRotation;
        RestoreMedalRaycasts();

        if (_medalButton != null)
            _medalButton.Interactable = _medalButtonOriginalInteractable;

        _medalButton = null;
        _hasMedalOriginalState = false;
    }
}
