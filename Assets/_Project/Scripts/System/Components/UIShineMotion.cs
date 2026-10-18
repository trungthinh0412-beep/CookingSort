using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class UIShineMotion : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField, Min(0.1f)] private float pulseDuration = 4f;
    [SerializeField, Range(0f, 0.2f)] private float scaleAmount = 0.04f;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.75f;
    [SerializeField, Range(0f, 360f)] private float phaseOffset;

    private Image _image;
    private RectTransform _rect;
    private Quaternion _baseRotation;
    private Vector3 _baseScale;
    private Color _baseColor;
    private float _angle;
    private float _phase;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _rect = _image.rectTransform;
        _baseRotation = _rect.localRotation;
        _baseScale = _rect.localScale;
        _baseColor = _image.color;
        _phase = phaseOffset * Mathf.Deg2Rad;
        _image.raycastTarget = false;
    }

    private void Update()
    {
        if (!_image.isActiveAndEnabled)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        _angle = Mathf.Repeat(_angle + rotationSpeed * deltaTime, 360f);
        _phase = Mathf.Repeat(_phase + deltaTime * Mathf.PI * 2f / Mathf.Max(0.1f, pulseDuration), Mathf.PI * 2f);
        float pulse = (1f - Mathf.Cos(_phase)) * 0.5f;
        _rect.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, _angle);
        _rect.localScale = _baseScale * (1f + scaleAmount * pulse);
        Color color = _baseColor;
        color.a *= Mathf.Lerp(minAlpha, 1f, pulse);
        _image.color = color;
    }
}
