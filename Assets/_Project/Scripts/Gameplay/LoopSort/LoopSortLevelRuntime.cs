using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MagicSoft.LoopSort
{
    public sealed class LoopSortLevelRuntime : MonoBehaviour
    {
        private LoopLevelRuntimeData _runtimeData;
        private LoopController _loopController;
        private LoopContainerManager _containerManager;
        private Material _groundMaterial;
        private TextMeshPro _instructionText;
        private Camera _camera;
        private bool _completionRaised;

        public event Action ProgressChanged;
        public event Action Completed;

        public int RemainingCubeCount =>
            _loopController != null
                ? _loopController.RemainingCubeCount
                : 0;
        public int InFlightCubeCount =>
            _loopController != null
                ? _loopController.InFlightCubeCount
                : 0;
        public int TotalCubeCount =>
            _loopController != null
                ? _loopController.TotalCubeCount
                : 0;
        public int CompletedContainerCount =>
            _containerManager != null
                ? _containerManager.CompletedContainerCount
                : 0;
        public int TotalContainerCount =>
            _containerManager != null
                ? _containerManager.TotalContainerCount
                : 0;

        public void Initialize(
            int levelIndex,
            LoopColorDatabase colorDatabase)
        {
            _runtimeData = LoopLevelFactory.Create(levelIndex);

            if (!_runtimeData.TryValidate(out string error))
            {
                Debug.LogError(
                    $"[LoopSortLevelRuntime] Invalid level data: {error}. " +
                    "A generated fallback level will be used.");
                _runtimeData = LoopLevelFactory.Create(levelIndex);
            }

            _camera = Camera.main;
            ConfigureCamera();
            CreateLighting();
            CreateGround();

            GameObject pathObject = new GameObject("LoopPath");
            pathObject.transform.SetParent(transform, false);
            LoopPath path = pathObject.AddComponent<LoopPath>();
            path.InitializeEllipse(
                _runtimeData.LoopWidth,
                _runtimeData.LoopHeight,
                _runtimeData.PathControlPointCount,
                0.42f);

            GameObject cubePoolObject = new GameObject("CubePool");
            cubePoolObject.transform.SetParent(transform, false);
            LoopCubePool cubePool =
                cubePoolObject.AddComponent<LoopCubePool>();
            cubePool.Initialize(_runtimeData.CubeSequence.Count);

            GameObject cubeRootObject = new GameObject("ActiveCubes");
            cubeRootObject.transform.SetParent(transform, false);

            Vector3 activeSlotPosition = new Vector3(
                0f,
                0f,
                -_runtimeData.LoopHeight * 0.5f - 1.05f);

            GameObject containerManagerObject =
                new GameObject("ContainerManager");
            containerManagerObject.transform.SetParent(transform, false);
            _containerManager =
                containerManagerObject.AddComponent<LoopContainerManager>();
            _containerManager.Initialize(
                _runtimeData.Containers,
                cubePool,
                colorDatabase,
                _camera,
                activeSlotPosition,
                _runtimeData.LoopWidth);

            GameObject controllerObject = new GameObject("LoopController");
            controllerObject.transform.SetParent(transform, false);
            _loopController =
                controllerObject.AddComponent<LoopController>();
            _loopController.ProgressChanged += HandleProgressChanged;
            _loopController.Completed += HandleCompleted;
            _loopController.Initialize(
                _runtimeData,
                path,
                cubePool,
                _containerManager,
                colorDatabase,
                cubeRootObject.transform,
                activeSlotPosition);

            CreateInstruction(activeSlotPosition);
            HandleProgressChanged();
        }

        public void InitializeAuthored(LoopLevelAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (!authoring.TryValidate(out string error))
            {
                Debug.LogError($"[LoopSortLevelRuntime] Invalid prefab level: {error}", authoring);
                return;
            }

            List<LoopCube> cubes = authoring.GetOrderedCubes();
            List<LoopContainer> containers = authoring.GetOrderedContainers();
            _runtimeData = new LoopLevelRuntimeData
            {
                LoopSpeed = authoring.LoopSpeed,
                CubeSpacing = authoring.CubeSpacing,
                GapCloseSpeed = authoring.GapCloseSpeed,
                CubeScale = authoring.CubeScale,
                LoopWidth = authoring.Path.Width,
                LoopHeight = authoring.Path.Height,
                PathControlPointCount = 16
            };

            for (int i = 0; i < cubes.Count; i++)
                _runtimeData.CubeSequence.Add(cubes[i].AuthoringColor);

            for (int i = 0; i < containers.Count; i++)
            {
                _runtimeData.Containers.Add(new LoopContainerLevelData
                {
                    color = containers[i].AuthoringColor,
                    capacity = containers[i].AuthoringCapacity
                });
            }

            _camera = Camera.main;
            ConfigureCamera();
            CreateLighting();
            CreateGround();
            authoring.Path.RebuildAuthoringPath();

            GameObject cubePoolObject = new GameObject("CubePool");
            cubePoolObject.transform.SetParent(transform, false);
            LoopCubePool cubePool = cubePoolObject.AddComponent<LoopCubePool>();
            cubePool.Initialize(0);

            Vector3 activeSlotPosition = authoring.ActiveSlot.position;

            GameObject containerManagerObject = new GameObject("ContainerManager");
            containerManagerObject.transform.SetParent(transform, false);
            _containerManager = containerManagerObject.AddComponent<LoopContainerManager>();
            _containerManager.InitializeAuthored(
                containers,
                cubePool,
                authoring.ColorDatabase,
                _camera,
                activeSlotPosition);

            GameObject controllerObject = new GameObject("LoopController");
            controllerObject.transform.SetParent(transform, false);
            _loopController = controllerObject.AddComponent<LoopController>();
            _loopController.ProgressChanged += HandleProgressChanged;
            _loopController.Completed += HandleCompleted;
            _loopController.InitializeAuthored(
                _runtimeData,
                authoring.Path,
                cubePool,
                _containerManager,
                authoring.ColorDatabase,
                authoring.CubeRoot,
                activeSlotPosition,
                cubes);

            CreateInstruction(activeSlotPosition);
            HandleProgressChanged();
        }

        private void ConfigureCamera()
        {
            if (_camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                _camera = cameraObject.AddComponent<Camera>();
            }

            _camera.orthographic = true;
            float aspect = Mathf.Max(0.5f, _camera.aspect);
            float widthRequirement =
                (_runtimeData.LoopWidth + 2.6f) * 0.5f / aspect;
            _camera.orthographicSize = Mathf.Max(7.5f, widthRequirement);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color32(226, 232, 240, 255);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.transform.position = new Vector3(0f, 11.5f, -10.5f);
            _camera.transform.LookAt(new Vector3(0f, 0f, -1.1f));
        }

        private void CreateLighting()
        {
            GameObject lightObject = new GameObject("GameplayLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation =
                Quaternion.Euler(48f, -32f, 0f);
            Light lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.15f;
            lightComponent.shadows = LightShadows.Soft;
        }

        private void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "LoopSortGround";
            ground.transform.SetParent(transform, false);
            ground.transform.position = new Vector3(0f, -0.08f, -1.1f);
            ground.transform.localScale = new Vector3(
                (_runtimeData.LoopWidth + 5f) / 10f,
                1f,
                (_runtimeData.LoopHeight + 7f) / 10f);

            Collider collider = ground.GetComponent<Collider>();

            if (collider != null)
                Destroy(collider);

            Shader shader = Shader.Find("Standard");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            _groundMaterial = new Material(shader)
            {
                name = "LoopSortGroundMaterial",
                color = new Color32(245, 247, 250, 255)
            };
            ground.GetComponent<MeshRenderer>().sharedMaterial =
                _groundMaterial;
        }

        private void CreateInstruction(Vector3 activeSlotPosition)
        {
            GameObject textObject = new GameObject("Instruction");
            textObject.transform.SetParent(transform, false);
            textObject.transform.position = activeSlotPosition +
                                            Vector3.left * 2.5f +
                                            Vector3.up * 0.12f;
            _instructionText = textObject.AddComponent<TextMeshPro>();
            _instructionText.text = "TAP A CONTAINER";
            _instructionText.fontSize = 2.2f;
            _instructionText.fontStyle = FontStyles.Bold;
            _instructionText.alignment = TextAlignmentOptions.Center;
            _instructionText.color = new Color32(51, 65, 85, 255);
            _instructionText.rectTransform.sizeDelta =
                new Vector2(5f, 0.7f);
        }

        private void HandleProgressChanged()
        {
            ProgressChanged?.Invoke();
        }

        private void HandleCompleted()
        {
            if (_completionRaised)
                return;

            _completionRaised = true;
            Completed?.Invoke();
        }

        private void LateUpdate()
        {
            if (_instructionText != null && _camera != null)
                _instructionText.transform.rotation =
                    _camera.transform.rotation;
        }

        private void OnDestroy()
        {
            if (_loopController != null)
            {
                _loopController.ProgressChanged -= HandleProgressChanged;
                _loopController.Completed -= HandleCompleted;
            }

            if (_groundMaterial != null)
                Destroy(_groundMaterial);
        }
    }
}
