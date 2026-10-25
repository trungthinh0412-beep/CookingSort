using UnityEngine;

namespace CookingSort2D
{
    [DisallowMultipleComponent]
    public sealed class ScreenRatioScaler : MonoBehaviour
    {
        public enum ScaleMode
        {
            Uniform,
            HorizontalOnly
        }

        [Header("Target")]
        [Tooltip("Để trống thì scale chính GameObject đang gắn component.")]
        [SerializeField] private Transform target;

        [Header("Reference: Portrait 1080 x 1920")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);
        [SerializeField] private Vector3 scaleAtReference = Vector3.one;
        [SerializeField] private ScaleMode scaleMode = ScaleMode.Uniform;

        private int _lastWidth = -1;
        private int _lastHeight = -1;

        private void Awake()
        {
            ApplyScale();
        }

        private void OnEnable()
        {
            ApplyScale();
        }

        private void LateUpdate()
        {
            if (_lastWidth != Screen.width || _lastHeight != Screen.height)
                ApplyScale();
        }

        [ContextMenu("Apply Screen Ratio Scale")]
        public void ApplyScale()
        {
            Transform item = target != null ? target : transform;
            float referenceAspect = referenceResolution.x /
                                    Mathf.Max(1f, referenceResolution.y);
            float currentAspect = Screen.height > 0
                ? Screen.width / (float)Screen.height
                : referenceAspect;
            float factor = currentAspect / referenceAspect;

            item.localScale = scaleMode == ScaleMode.HorizontalOnly
                ? new Vector3(scaleAtReference.x * factor,
                    scaleAtReference.y, scaleAtReference.z)
                : scaleAtReference * factor;

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
        }
    }
}
