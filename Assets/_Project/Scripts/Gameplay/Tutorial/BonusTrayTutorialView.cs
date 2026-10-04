using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public sealed class BonusTrayTutorialView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image overlay;
    [SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.78f;
    [SerializeField] private Image tutorialImage;
    [SerializeField] private TMP_Text tapHintText;
    [SerializeField] private bool showTapHint = true;

    public event Action Dismissed;

    private void Awake()
    {
        ApplyOverlayAlpha();
        UpdateTutorialImage();
    }

    private void OnValidate()
    {
        ApplyOverlayAlpha();
        UpdateTutorialImage();
    }

    private void ApplyOverlayAlpha()
    {
        if (overlay == null)
            return;

        overlay.color = new Color(0f, 0f, 0f, overlayAlpha);
    }

    public void SetTutorialSprite(Sprite sprite)
    {
        if (tutorialImage != null)
        {
            tutorialImage.sprite = sprite;
            UpdateTutorialImage();
        }
    }

    private void UpdateTutorialImage()
    {
        bool hasSprite = tutorialImage != null && tutorialImage.sprite != null;
        if (tutorialImage != null)
            tutorialImage.enabled = hasSprite;
        if (tapHintText != null)
            tapHintText.enabled = showTapHint;
    }

    public void OnTapToContinue()
    {
        Dismissed?.Invoke();
        Destroy(gameObject);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnTapToContinue();
    }
}
