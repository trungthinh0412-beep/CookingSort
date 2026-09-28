using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MagicSoft.Differences
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DifferencePanelView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private SpriteRenderer picture;
        [SerializeField] private BoxCollider2D pictureCollider;
        [Tooltip("World-space frame. The sprite fits inside without stretching.")]
        [SerializeField] private Vector2 frameSize = new Vector2(9.4f, 6.2f);
        public SpriteRenderer Picture => picture;
        public BoxCollider2D PictureCollider => pictureCollider;
        public bool IsConfigured => picture != null && picture.sprite != null && pictureCollider != null &&
            pictureCollider.transform == picture.transform && pictureCollider.enabled &&
            picture.transform.parent == transform && frameSize.x > 0 && frameSize.y > 0;
        public event Action<DifferencePanelView, Vector2, Camera> PictureClicked;

        private void OnEnable() => RefreshGeometry();
        private void LateUpdate() => RefreshGeometry();
        private void OnValidate() => RefreshGeometry();

        public void RefreshGeometry()
        {
            if (picture == null || picture.sprite == null || pictureCollider == null) return;
            Bounds bounds = picture.sprite.bounds;
            float scale = Mathf.Min(frameSize.x / bounds.size.x, frameSize.y / bounds.size.y);
            Vector3 targetScale = Vector3.one * Mathf.Max(.001f, scale);
            Vector3 targetPosition = -bounds.center * scale;
            if (picture.transform.localScale != targetScale) picture.transform.localScale = targetScale;
            if (picture.transform.localPosition != targetPosition) picture.transform.localPosition = targetPosition;
            if (pictureCollider.offset != (Vector2)bounds.center) pictureCollider.offset = bounds.center;
            if (pictureCollider.size != (Vector2)bounds.size) pictureCollider.size = bounds.size;
        }

        public bool TryScreenPoint(Vector2 screenPoint, Camera camera, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (camera == null || picture == null || !camera.pixelRect.Contains(screenPoint)) return false;
            var plane = new Plane(picture.transform.forward, picture.transform.position);
            Ray ray = camera.ScreenPointToRay(screenPoint);
            if (!plane.Raycast(ray, out float distance)) return false;
            worldPoint = ray.GetPoint(distance);
            return true;
        }

        public bool ContainsScreenPoint(Vector2 screenPoint, Camera camera)
        {
            if (!IsConfigured || !TryScreenPoint(screenPoint, camera, out var world)) return false;
            Vector2 local = picture.transform.InverseTransformPoint(world);
            return new Rect(pictureCollider.offset - pictureCollider.size / 2, pictureCollider.size).Contains(local);
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || data.dragging ||
                !ContainsScreenPoint(data.position, data.pressEventCamera)) return;
            PictureClicked?.Invoke(this, data.position, data.pressEventCamera);
        }
    }
}
