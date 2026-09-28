using System;
using System.Collections;
using System.Collections.Generic;
using Lean.Touch;
using UnityEngine;

namespace MagicSoft.LoopSort
{
    public sealed class LoopContainerManager : MonoBehaviour
    {
        private readonly List<LoopContainer> _containers =
            new List<LoopContainer>();

        private LoopCubePool _cubePool;
        private LoopColorDatabase _colorDatabase;
        private Camera _camera;
        private Material _fallbackMaterial;
        private Vector3 _activeSlotPosition;
        private int _completedContainerCount;
        private bool _initialized;

        public event Action ProgressChanged;

        public LoopContainer ActiveContainer { get; private set; }
        public int CompletedContainerCount => _completedContainerCount;
        public int TotalContainerCount => _containers.Count;
        public bool AllContainersCompleted =>
            _containers.Count > 0 &&
            _completedContainerCount >= _containers.Count;

        public void Initialize(
            IReadOnlyList<LoopContainerLevelData> containerData,
            LoopCubePool cubePool,
            LoopColorDatabase colorDatabase,
            Camera worldCamera,
            Vector3 activeSlotPosition,
            float loopWidth)
        {
            _cubePool = cubePool;
            _colorDatabase = colorDatabase;
            _camera = worldCamera;
            _activeSlotPosition = activeSlotPosition;

            Shader shader = Shader.Find("Standard");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            _fallbackMaterial = new Material(shader)
            {
                name = "LoopContainerRuntimeMaterial",
                enableInstancing = true
            };

            CreateActiveSlotMarker();
            CreateContainers(containerData, loopWidth);
            _initialized = true;
        }

        public void InitializeAuthored(
            IReadOnlyList<LoopContainer> containers,
            LoopCubePool cubePool,
            LoopColorDatabase colorDatabase,
            Camera worldCamera,
            Vector3 activeSlotPosition)
        {
            _cubePool = cubePool;
            _colorDatabase = colorDatabase;
            _camera = worldCamera;
            _activeSlotPosition = activeSlotPosition;

            Shader shader = Shader.Find("Standard");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            _fallbackMaterial = new Material(shader)
            {
                name = "LoopContainerRuntimeMaterial",
                enableInstancing = true
            };

            _containers.Clear();
            int count = containers != null ? containers.Count : 0;

            for (int i = 0; i < count; i++)
            {
                LoopContainer container = containers[i];

                if (container == null)
                    continue;

                container.Initialize(
                    new LoopContainerLevelData
                    {
                        color = container.AuthoringColor,
                        capacity = container.AuthoringCapacity
                    },
                    _colorDatabase,
                    _fallbackMaterial,
                    _camera);
                _containers.Add(container);
            }

            _initialized = true;
        }

        public void ReceiveReservedCube(
            LoopCube cube,
            Action onArrived)
        {
            if (cube == null || ActiveContainer == null)
            {
                onArrived?.Invoke();
                return;
            }

            StartCoroutine(AnimateCubeIntoContainer(
                cube,
                ActiveContainer,
                onArrived));
        }

        private void CreateContainers(
            IReadOnlyList<LoopContainerLevelData> containerData,
            float loopWidth)
        {
            int count = containerData != null ? containerData.Count : 0;
            float spacing = Mathf.Min(2.15f, loopWidth / Mathf.Max(1, count));
            float startX = -spacing * (count - 1) * 0.5f;
            float waitingZ = _activeSlotPosition.z - 2.05f;

            for (int i = 0; i < count; i++)
            {
                GameObject containerObject = new GameObject(
                    $"Container_{containerData[i].color}");
                containerObject.transform.SetParent(transform, false);
                containerObject.transform.position = new Vector3(
                    startX + spacing * i,
                    0f,
                    waitingZ);
                containerObject.transform.localScale =
                    Vector3.one * (count >= 5 ? 0.82f : 0.95f);

                LoopContainer container =
                    containerObject.AddComponent<LoopContainer>();
                container.Initialize(
                    containerData[i],
                    _colorDatabase,
                    _fallbackMaterial,
                    _camera);
                _containers.Add(container);
            }
        }

        private void CreateActiveSlotMarker()
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "ActiveContainerSlot";
            marker.transform.SetParent(transform, false);
            marker.transform.position = _activeSlotPosition +
                                        Vector3.down * 0.04f;
            marker.transform.localScale = new Vector3(1.9f, 0.06f, 2.3f);

            Collider collider = marker.GetComponent<Collider>();

            if (collider != null)
                Destroy(collider);

            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _fallbackMaterial;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            Color slotColor = new Color32(105, 115, 133, 255);
            properties.SetColor("_Color", slotColor);
            properties.SetColor("_BaseColor", slotColor);
            renderer.SetPropertyBlock(properties);
        }

        private void OnEnable()
        {
            LeanTouch.OnFingerTap += HandleFingerTap;
        }

        private void OnDisable()
        {
            LeanTouch.OnFingerTap -= HandleFingerTap;
        }

        private void HandleFingerTap(LeanFinger finger)
        {
            if (!_initialized ||
                finger == null ||
                finger.IsOverGui ||
                _camera == null ||
                ActiveContainer != null)
            {
                return;
            }

            if (GameManager.Instance != null &&
                GameManager.Instance.gameState != GameState.PlayingGame)
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(finger.ScreenPosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
                return;

            LoopContainer container =
                hit.collider.GetComponentInParent<LoopContainer>();

            if (container == null ||
                container.State != LoopContainerState.Waiting)
            {
                return;
            }

            SelectContainer(container);
        }

        private void SelectContainer(LoopContainer container)
        {
            ActiveContainer = container;
            container.SetState(LoopContainerState.MovingToActive);
            StartCoroutine(MoveContainerToActive(container));
        }

        private IEnumerator MoveContainerToActive(LoopContainer container)
        {
            Vector3 start = container.transform.position;
            float duration = 0.32f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                container.transform.position = Vector3.Lerp(
                    start,
                    _activeSlotPosition,
                    eased);
                yield return null;
            }

            container.transform.position = _activeSlotPosition;
            container.SetState(LoopContainerState.Active);
            ProgressChanged?.Invoke();
        }

        private IEnumerator AnimateCubeIntoContainer(
            LoopCube cube,
            LoopContainer target,
            Action onArrived)
        {
            Vector3 start = cube.transform.position;
            Vector3 end = target.EntrancePosition;
            Vector3 control = (start + end) * 0.5f +
                              Vector3.up * 1.8f;
            Vector3 startScale = cube.transform.localScale;
            const float duration = 0.38f;
            float elapsed = 0f;

            while (elapsed < duration && cube != null && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                float inverse = 1f - eased;
                cube.transform.position =
                    inverse * inverse * start +
                    2f * inverse * eased * control +
                    eased * eased * end;
                cube.transform.localScale = Vector3.Lerp(
                    startScale,
                    startScale * 0.25f,
                    eased);
                cube.transform.Rotate(0f, 360f * Time.deltaTime, 0f);
                yield return null;
            }

            if (cube != null)
            {
                cube.MarkInsideContainer();
                _cubePool.Release(cube);
            }

            if (target != null)
            {
                target.NotifyCubeArrived();
                ProgressChanged?.Invoke();

                if (target.IsFilled &&
                    target.State == LoopContainerState.Active)
                {
                    StartCoroutine(DepartCompletedContainer(target));
                }
            }

            onArrived?.Invoke();
        }

        private IEnumerator DepartCompletedContainer(
            LoopContainer container)
        {
            container.SetState(LoopContainerState.Departing);
            yield return new WaitForSeconds(0.25f);

            Vector3 start = container.transform.position;
            Vector3 end = start + Vector3.right * 13f;
            const float duration = 0.55f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                container.transform.position = Vector3.Lerp(
                    start,
                    end,
                    t * t);
                yield return null;
            }

            container.SetState(LoopContainerState.Completed);
            container.gameObject.SetActive(false);
            _completedContainerCount++;

            if (ActiveContainer == container)
                ActiveContainer = null;

            ProgressChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (_fallbackMaterial != null)
                Destroy(_fallbackMaterial);
        }
    }
}
