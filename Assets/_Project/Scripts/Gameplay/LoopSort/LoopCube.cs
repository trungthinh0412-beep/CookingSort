using UnityEngine;

namespace MagicSoft.LoopSort
{
    public enum LoopCubeState
    {
        Pooled,
        OnLoop,
        MovingToContainer,
        InsideContainer
    }

    public sealed class LoopCube : MonoBehaviour
    {
        private static readonly int ColorProperty =
            Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty =
            Shader.PropertyToID("_BaseColor");

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propertyBlock;

        [SerializeField] private LoopCubeColor authoringColor;

        public LoopCubeColor Color { get; private set; }
        public LoopCubeColor AuthoringColor => authoringColor;
        public LoopCubeState State { get; private set; } =
            LoopCubeState.Pooled;
        public float CurrentOffset { get; private set; }
        public float TargetOffset { get; set; }
        public float PreviousAbsoluteDistance { get; private set; }
        public float AbsoluteDistance { get; private set; }

        public void CacheComponents()
        {
            if (_renderer == null)
                _renderer = GetComponent<MeshRenderer>();

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }

        public void Setup(
            LoopCubeColor color,
            float scale,
            float initialOffset,
            float headDistance,
            LoopColorDatabase colorDatabase,
            Material fallbackMaterial)
        {
            CacheComponents();
            authoringColor = color;
            Color = color;
            State = LoopCubeState.OnLoop;
            CurrentOffset = initialOffset;
            TargetOffset = initialOffset;
            AbsoluteDistance = headDistance + initialOffset;
            PreviousAbsoluteDistance = AbsoluteDistance;
            transform.localScale = Vector3.one * scale;

            Material configuredMaterial = colorDatabase != null
                ? colorDatabase.GetCubeMaterial(color)
                : null;
            _renderer.sharedMaterial = configuredMaterial != null
                ? configuredMaterial
                : fallbackMaterial;

            UnityEngine.Color displayColor = colorDatabase != null
                ? colorDatabase.GetColor(color)
                : LoopColorDatabase.GetFallbackColor(color);
            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorProperty, displayColor);
            _propertyBlock.SetColor(BaseColorProperty, displayColor);
            _renderer.SetPropertyBlock(_propertyBlock);
            gameObject.SetActive(true);
        }

        public void SetAuthoringColor(LoopCubeColor color)
        {
            authoringColor = color;
            RefreshAuthoringVisual(null);
        }

        public void RefreshAuthoringVisual(LoopColorDatabase colorDatabase)
        {
            CacheComponents();

            if (_renderer == null)
                return;

            UnityEngine.Color displayColor = colorDatabase != null
                ? colorDatabase.GetColor(authoringColor)
                : LoopColorDatabase.GetFallbackColor(authoringColor);
            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorProperty, displayColor);
            _propertyBlock.SetColor(BaseColorProperty, displayColor);
            _renderer.SetPropertyBlock(_propertyBlock);
        }

        public void TickFollow(
            LoopPath path,
            float headDistance,
            float gapCloseSpeed,
            float deltaTime)
        {
            if (State != LoopCubeState.OnLoop || path == null)
                return;

            PreviousAbsoluteDistance = AbsoluteDistance;
            CurrentOffset = Mathf.MoveTowards(
                CurrentOffset,
                TargetOffset,
                gapCloseSpeed * deltaTime);
            AbsoluteDistance = headDistance + CurrentOffset;

            transform.position = path.GetPosition(AbsoluteDistance);
            Vector3 tangent = path.GetTangent(AbsoluteDistance);

            if (tangent.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(
                    tangent,
                    Vector3.up);
            }
        }

        public void BeginTransfer()
        {
            State = LoopCubeState.MovingToContainer;
        }

        public void MarkInsideContainer()
        {
            State = LoopCubeState.InsideContainer;
        }

        public void MarkPooled()
        {
            State = LoopCubeState.Pooled;
            gameObject.SetActive(false);
        }

        private void OnValidate()
        {
            RefreshAuthoringVisual(null);
        }
    }
}
