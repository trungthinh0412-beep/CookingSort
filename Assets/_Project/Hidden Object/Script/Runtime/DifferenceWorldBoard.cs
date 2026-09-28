using UnityEngine;

namespace MagicSoft.Differences
{
    // Only transforms existing world objects. The scene loader supplies the camera explicitly.
    [DisallowMultipleComponent]
    public sealed class DifferenceWorldBoard : MonoBehaviour
    {
        [SerializeField] private Transform contentRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private bool fitToCamera = true;
        [SerializeField] private Vector2 referenceSize = new Vector2(9.4f, 12.9f);
        [SerializeField] private Rect viewportArea = new Rect(.06f, .17f, .88f, .67f);
        public Transform ContentRoot => contentRoot;
        public void BindCamera(Camera camera) { worldCamera = camera; Fit(); }
        public void SetVisible(bool visible) { if (contentRoot != null) contentRoot.gameObject.SetActive(visible); }
        private void LateUpdate() => Fit();
        public void Fit()
        {
            if (!fitToCamera || contentRoot == null || worldCamera == null) return;
            var plane = new Plane(transform.forward, transform.position);
            Ray lowerRay = worldCamera.ViewportPointToRay(viewportArea.min);
            Ray upperRay = worldCamera.ViewportPointToRay(viewportArea.max);
            if (!plane.Raycast(lowerRay, out float lowerDistance) || !plane.Raycast(upperRay, out float upperDistance)) return;
            Vector3 lower = transform.InverseTransformPoint(lowerRay.GetPoint(lowerDistance));
            Vector3 upper = transform.InverseTransformPoint(upperRay.GetPoint(upperDistance));
            float scale = Mathf.Min(Mathf.Abs(upper.x - lower.x) / Mathf.Max(.01f, referenceSize.x),
                Mathf.Abs(upper.y - lower.y) / Mathf.Max(.01f, referenceSize.y));
            Vector3 position = (lower + upper) * .5f;
            Vector3 targetScale = Vector3.one * scale;
            if (contentRoot.localPosition != position) contentRoot.localPosition = position;
            if (contentRoot.localScale != targetScale) contentRoot.localScale = targetScale;
        }
    }
}
