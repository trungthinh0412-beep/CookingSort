using UnityEngine;

namespace CookingSort2D
{
    /// <summary>
    /// Adjusts the decorative Left/Right positions from the screen aspect ratio.
    /// It never changes or reads the Camera position/size.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraEdgeAnchors : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform top;
        [SerializeField] private Transform left;
        [SerializeField] private Transform right;

        [Header("Reference Layout (9:16)")]
        [SerializeField, Min(0.01f)] private float referenceAspect = 9f / 16f;
        [SerializeField] private Vector3 topPosition = new Vector3(0f, 13.46f, 0f);
        [SerializeField] private Vector3 leftPosition = new Vector3(-5.42f, 8.69f, 0f);
        [SerializeField] private Vector3 rightPosition = new Vector3(5.52f, 8.69f, 0f);

        private int _screenWidth = -1;
        private int _screenHeight = -1;
        private float _aspect = -1f;

        private void Awake()
        {
            ResolveReferences();
            ApplyAnchors();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyAnchors();
        }

        private void LateUpdate()
        {
            if (_screenWidth != Screen.width ||
                _screenHeight != Screen.height ||
                !Mathf.Approximately(_aspect, CurrentAspect))
            {
                ApplyAnchors();
            }
        }

        [ContextMenu("Apply Camera Anchors")]
        public void ApplyAnchors()
        {
            ResolveReferences();
            float widthFactor = CurrentAspect / Mathf.Max(0.01f, referenceAspect);

            if (top != null) top.localPosition = topPosition;
            if (left != null)
                left.localPosition = new Vector3(
                    leftPosition.x * widthFactor,
                    leftPosition.y,
                    leftPosition.z);
            if (right != null)
                right.localPosition = new Vector3(
                    rightPosition.x * widthFactor,
                    rightPosition.y,
                    rightPosition.z);

            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _aspect = CurrentAspect;
        }

        private void ResolveReferences()
        {
            Transform background = transform.Find("BG");
            if (background == null) return;
            if (top == null) top = background.Find("Top");
            if (left == null) left = background.Find("Left");
            if (right == null) right = background.Find("Right");
        }

        private static float CurrentAspect =>
            Screen.height > 0 ? Screen.width / (float)Screen.height : 9f / 16f;
    }
}
