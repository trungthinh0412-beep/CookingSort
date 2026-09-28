using UnityEngine;

namespace MagicSoft.Differences
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DifferenceSpotView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer picture;
        [SerializeField] private BoxCollider2D hitArea;
        [SerializeField] private SpriteRenderer foundMarker;
        [Tooltip("Coordinates relative to the sprite: bottom-left = (0,0), top-right = (1,1). Edit with the Level Editor or Scene handles.")]
        [SerializeField] private Rect normalizedBounds = new Rect(.4f, .4f, .2f, .2f);
        [SerializeField] private Color foundColor = new Color(.1f, .85f, .4f);
        [SerializeField] private Color hintColor = new Color(1f, .75f, .1f);
        public SpriteRenderer Picture => picture;
        public BoxCollider2D HitArea => hitArea;
        public SpriteRenderer FoundMarker => foundMarker;
        public Rect NormalizedBounds => normalizedBounds;
        private void OnEnable() => RefreshGeometry();
        private void LateUpdate() => RefreshGeometry();
        private void OnValidate() => RefreshGeometry();

        public void RefreshGeometry()
        {
            if (picture == null || picture.sprite == null || hitArea == null) return;
            var bounds = picture.sprite.bounds;
            Vector2 size = Vector2.Scale(normalizedBounds.size, bounds.size);
            Vector2 center = (Vector2)bounds.min + Vector2.Scale(normalizedBounds.center, bounds.size);
            if (transform.localPosition != (Vector3)center) transform.localPosition = center;
            if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
            if (hitArea.offset != Vector2.zero) hitArea.offset = Vector2.zero;
            var hitSize = new Vector2(Mathf.Max(.001f, size.x), Mathf.Max(.001f, size.y));
            if (hitArea.size != hitSize) hitArea.size = hitSize;
            if (foundMarker == null || foundMarker.sprite == null) return;
            var markerBounds = foundMarker.sprite.bounds;
            var scale = new Vector3(size.x / markerBounds.size.x, size.y / markerBounds.size.y, 1);
            if (foundMarker.transform.localScale != scale) foundMarker.transform.localScale = scale;
            var markerPosition = -Vector3.Scale(markerBounds.center, scale);
            if (foundMarker.transform.localPosition != markerPosition) foundMarker.transform.localPosition = markerPosition;
            if (foundMarker.sortingLayerID != picture.sortingLayerID) foundMarker.sortingLayerID = picture.sortingLayerID;
            if (foundMarker.sortingOrder != picture.sortingOrder + 1) foundMarker.sortingOrder = picture.sortingOrder + 1;
        }

        public bool ContainsWorldPoint(Vector3 world)
        {
            if (hitArea == null) return false;
            Vector2 local = hitArea.transform.InverseTransformPoint(world);
            return new Rect(hitArea.offset - hitArea.size / 2, hitArea.size).Contains(local);
        }
        public void SetFound(bool found)
        {
            if (foundMarker == null) return;
            foundMarker.color = foundColor;
            foundMarker.gameObject.SetActive(found);
        }
        public void ShowHint()
        {
            if (foundMarker == null) return;
            foundMarker.color = hintColor;
            foundMarker.gameObject.SetActive(true);
        }
        public void ResetView() => SetFound(false);
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Application.isPlaying || hitArea == null) return;
            var old = Gizmos.matrix;
            Gizmos.matrix = hitArea.transform.localToWorldMatrix;
            Gizmos.color = new Color(1, .65f, 0, .85f);
            Gizmos.DrawWireCube(hitArea.offset, hitArea.size);
            Gizmos.matrix = old;
        }
#endif
    }
}
