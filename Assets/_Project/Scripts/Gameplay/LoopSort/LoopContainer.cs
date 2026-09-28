using TMPro;
using UnityEngine;

namespace MagicSoft.LoopSort
{
    public enum LoopContainerState
    {
        Waiting,
        MovingToActive,
        Active,
        Departing,
        Completed
    }

    public sealed class LoopContainer : MonoBehaviour
    {
        private static readonly int ColorProperty =
            Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty =
            Shader.PropertyToID("_BaseColor");

        private TextMeshPro _counterText;
        private Transform _entrance;
        private Camera _camera;

        [SerializeField] private LoopCubeColor authoringColor;
        [SerializeField, Min(1)] private int authoringCapacity = 10;

        public LoopCubeColor Color { get; private set; }
        public LoopCubeColor AuthoringColor => authoringColor;
        public int AuthoringCapacity => Mathf.Max(1, authoringCapacity);
        public int Capacity { get; private set; }
        public int AcceptedCount { get; private set; }
        public int ArrivedCount { get; private set; }
        public LoopContainerState State { get; private set; }
        public Vector3 EntrancePosition =>
            _entrance != null
                ? _entrance.position
                : transform.position + Vector3.up;
        public bool HasSpace => AcceptedCount < Capacity;
        public bool IsFilled =>
            AcceptedCount >= Capacity && ArrivedCount >= Capacity;

        public void Initialize(
            LoopContainerLevelData data,
            LoopColorDatabase colorDatabase,
            Material fallbackMaterial,
            Camera worldCamera)
        {
            authoringColor = data.color;
            authoringCapacity = Mathf.Max(1, data.capacity);
            Color = authoringColor;
            Capacity = authoringCapacity;
            AcceptedCount = 0;
            ArrivedCount = 0;
            State = LoopContainerState.Waiting;
            _camera = worldCamera;

            UnityEngine.Color displayColor = colorDatabase != null
                ? colorDatabase.GetColor(Color)
                : LoopColorDatabase.GetFallbackColor(Color);
            Material configuredMaterial = colorDatabase != null
                ? colorDatabase.GetContainerMaterial(Color)
                : null;
            Material material = configuredMaterial != null
                ? configuredMaterial
                : fallbackMaterial;

            EnsureRuntimeParts(displayColor, material);
            RefreshCounter();
        }

        public void SetAuthoring(LoopCubeColor color, int capacity)
        {
            authoringColor = color;
            authoringCapacity = Mathf.Max(1, capacity);
            RefreshAuthoringVisual(null);
        }

        public void RefreshAuthoringVisual(LoopColorDatabase colorDatabase)
        {
            UnityEngine.Color displayColor = colorDatabase != null
                ? colorDatabase.GetColor(authoringColor)
                : LoopColorDatabase.GetFallbackColor(authoringColor);
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(properties);
                UnityEngine.Color color = renderers[i].name == "Cab"
                    ? displayColor * 0.86f
                    : displayColor;
                properties.SetColor(ColorProperty, color);
                properties.SetColor(BaseColorProperty, color);
                renderers[i].SetPropertyBlock(properties);
            }
        }

        private void EnsureRuntimeParts(
            UnityEngine.Color displayColor,
            Material material)
        {
            Transform cargo = transform.Find("Cargo");
            Transform cab = transform.Find("Cab");

            if (cargo == null)
            {
                CreatePart("Cargo", new Vector3(0f, 0.45f, 0f),
                    new Vector3(1.35f, 0.8f, 1.25f), displayColor, material);
            }

            if (cab == null)
            {
                CreatePart("Cab", new Vector3(0f, 0.3f, 0.88f),
                    new Vector3(0.95f, 0.6f, 0.55f), displayColor * 0.86f, material);
            }

            _entrance = transform.Find("EntrancePoint");

            if (_entrance == null)
            {
                GameObject entranceObject = new GameObject("EntrancePoint");
                entranceObject.transform.SetParent(transform, false);
                entranceObject.transform.localPosition = new Vector3(0f, 0.95f, -0.1f);
                _entrance = entranceObject.transform;
            }

            BoxCollider hitCollider = GetComponent<BoxCollider>();

            if (hitCollider == null)
                hitCollider = gameObject.AddComponent<BoxCollider>();

            hitCollider.center = new Vector3(0f, 0.45f, 0.2f);
            hitCollider.size = new Vector3(1.55f, 1.15f, 2f);

            _counterText = GetComponentInChildren<TextMeshPro>(true);

            if (_counterText == null)
                CreateCounter();

            RefreshAuthoringVisual(null);
        }

        public bool TryReserveCube(LoopCubeColor cubeColor)
        {
            if (State != LoopContainerState.Active ||
                cubeColor != Color ||
                !HasSpace)
            {
                return false;
            }

            AcceptedCount++;
            RefreshCounter();
            return true;
        }

        public void NotifyCubeArrived()
        {
            ArrivedCount = Mathf.Min(AcceptedCount, ArrivedCount + 1);
            RefreshCounter();
        }

        public void SetState(LoopContainerState state)
        {
            State = state;
        }

        private void CreatePart(
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            UnityEngine.Color color,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = objectName;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();

            if (collider != null)
                Destroy(collider);

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor(ColorProperty, color);
            properties.SetColor(BaseColorProperty, color);
            renderer.SetPropertyBlock(properties);
        }

        private void CreateCounter()
        {
            GameObject labelObject = new GameObject("Counter");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition =
                new Vector3(0f, 1.25f, 0f);

            _counterText = labelObject.AddComponent<TextMeshPro>();
            _counterText.alignment = TextAlignmentOptions.Center;
            _counterText.fontSize = 3.4f;
            _counterText.color = UnityEngine.Color.white;
            _counterText.enableAutoSizing = false;
            _counterText.rectTransform.sizeDelta = new Vector2(2.8f, 0.7f);
        }

        private void RefreshCounter()
        {
            if (_counterText != null)
                _counterText.text = $"{ArrivedCount}/{Capacity}";
        }

        private void LateUpdate()
        {
            if (_counterText != null && _camera != null)
                _counterText.transform.rotation = _camera.transform.rotation;
        }

        private void OnValidate()
        {
            authoringCapacity = Mathf.Max(1, authoringCapacity);
            RefreshAuthoringVisual(null);
        }
    }
}
