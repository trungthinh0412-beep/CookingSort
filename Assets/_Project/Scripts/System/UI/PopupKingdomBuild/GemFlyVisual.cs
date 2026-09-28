using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GemFlyVisual : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private CanvasGroup canvasGroup;

    public RectTransform RectTransform => transform as RectTransform;

    private void Awake()
    {
        CacheReferences();
    }

    public void Prepare(
        Sprite sprite,
        Color tint,
        Vector2 size,
        float scale
    )
    {
        CacheReferences();

        if (icon == null)
            return;

        if (sprite != null)
            icon.sprite = sprite;

        icon.color = tint;
        icon.enabled = icon.sprite != null;

        if (RectTransform != null)
        {
            RectTransform.sizeDelta = size;
            RectTransform.localScale = Vector3.one * scale;
            RectTransform.localRotation = Quaternion.identity;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    public void SetMotion(Vector3 worldPosition, float rotation, float scale)
    {
        if (RectTransform == null)
            return;

        RectTransform.position = worldPosition;
        RectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        RectTransform.localScale = Vector3.one * scale;
    }

    public void ResetVisual()
    {
        CacheReferences();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (icon != null)
            icon.enabled = false;

        if (RectTransform != null)
        {
            RectTransform.localRotation = Quaternion.identity;
            RectTransform.localScale = Vector3.one;
        }
    }

    private void CacheReferences()
    {
        if (icon == null)
            icon = GetComponentInChildren<Image>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}
