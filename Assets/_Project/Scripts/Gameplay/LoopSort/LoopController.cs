using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicSoft.LoopSort
{
    public sealed class LoopController : MonoBehaviour
    {
        private readonly List<LoopCube> _cubes =
            new List<LoopCube>();
        private readonly List<LoopCube> _collectCandidates =
            new List<LoopCube>();

        private LoopLevelRuntimeData _data;
        private LoopPath _path;
        private LoopCubePool _cubePool;
        private LoopContainerManager _containerManager;
        private LoopColorDatabase _colorDatabase;
        private Transform _cubeRoot;
        private float _headDistance;
        private float _collectDistance;
        private float _effectiveSpacing;
        private int _inFlightCount;
        private int _totalCubeCount;
        private bool _completed;
        private bool _initialized;

        public event Action ProgressChanged;
        public event Action Completed;

        public int RemainingCubeCount => _cubes.Count;
        public int InFlightCubeCount => _inFlightCount;
        public int TotalCubeCount => _totalCubeCount;

        public void Initialize(
            LoopLevelRuntimeData data,
            LoopPath path,
            LoopCubePool cubePool,
            LoopContainerManager containerManager,
            LoopColorDatabase colorDatabase,
            Transform cubeRoot,
            Vector3 activeSlotPosition)
        {
            _data = data;
            _path = path;
            _cubePool = cubePool;
            _containerManager = containerManager;
            _colorDatabase = colorDatabase;
            _cubeRoot = cubeRoot;
            _collectDistance = _path.FindClosestDistance(
                activeSlotPosition);
            _effectiveSpacing = CalculateEffectiveSpacing();
            _headDistance = _collectDistance - _effectiveSpacing * 0.5f;
            _totalCubeCount = data.CubeSequence.Count;

            SpawnCubes();
            _containerManager.ProgressChanged +=
                HandleContainerProgressChanged;
            _initialized = true;
            ProgressChanged?.Invoke();
        }

        public void InitializeAuthored(
            LoopLevelRuntimeData data,
            LoopPath path,
            LoopCubePool cubePool,
            LoopContainerManager containerManager,
            LoopColorDatabase colorDatabase,
            Transform cubeRoot,
            Vector3 activeSlotPosition,
            IReadOnlyList<LoopCube> authoredCubes)
        {
            _data = data;
            _path = path;
            _cubePool = cubePool;
            _containerManager = containerManager;
            _colorDatabase = colorDatabase;
            _cubeRoot = cubeRoot;
            _collectDistance = _path.FindClosestDistance(activeSlotPosition);
            _effectiveSpacing = CalculateEffectiveSpacing();
            _headDistance = _collectDistance - _effectiveSpacing * 0.5f;
            _totalCubeCount = authoredCubes != null ? authoredCubes.Count : 0;

            _cubes.Clear();

            for (int i = 0; i < _totalCubeCount; i++)
            {
                LoopCube cube = authoredCubes[i];

                if (cube == null)
                    continue;

                float offset = -i * _effectiveSpacing;
                cube.transform.SetParent(_cubeRoot, true);
                cube.Setup(
                    cube.AuthoringColor,
                    _data.CubeScale,
                    offset,
                    _headDistance,
                    _colorDatabase,
                    _cubePool.FallbackMaterial);
                cube.TickFollow(
                    _path,
                    _headDistance,
                    _data.GapCloseSpeed,
                    0f);
                _cubes.Add(cube);
            }

            _containerManager.ProgressChanged += HandleContainerProgressChanged;
            _initialized = true;
            ProgressChanged?.Invoke();
        }

        private float CalculateEffectiveSpacing()
        {
            float requested = Mathf.Max(0.05f, _data.CubeSpacing);
            float requiredLength = requested * _data.CubeSequence.Count;
            float availableLength = _path.Length * 0.94f;

            if (requiredLength <= availableLength)
                return requested;

            float adjusted = availableLength /
                             Mathf.Max(1, _data.CubeSequence.Count);
            Debug.LogWarning(
                $"[LoopController] Cube sequence needs {requiredLength:F2}m " +
                $"but the path offers {availableLength:F2}m. " +
                $"Spacing was reduced to {adjusted:F3}m.");
            return adjusted;
        }

        private void SpawnCubes()
        {
            _cubes.Clear();

            for (int i = 0; i < _data.CubeSequence.Count; i++)
            {
                float offset = -i * _effectiveSpacing;
                LoopCube cube = _cubePool.Get(
                    _data.CubeSequence[i],
                    _data.CubeScale,
                    offset,
                    _headDistance,
                    _cubeRoot,
                    _colorDatabase);
                cube.TickFollow(
                    _path,
                    _headDistance,
                    _data.GapCloseSpeed,
                    0f);
                _cubes.Add(cube);
            }
        }

        private void Update()
        {
            if (!_initialized || _completed || !CanRunGameplay())
                return;

            float deltaTime = Time.deltaTime;
            _headDistance += _data.LoopSpeed * deltaTime;
            _collectCandidates.Clear();
            LoopContainer activeContainer =
                _containerManager.ActiveContainer;

            for (int i = 0; i < _cubes.Count; i++)
            {
                LoopCube cube = _cubes[i];
                cube.TickFollow(
                    _path,
                    _headDistance,
                    _data.GapCloseSpeed,
                    deltaTime);

                if (activeContainer == null ||
                    activeContainer.State != LoopContainerState.Active ||
                    cube.Color != activeContainer.Color ||
                    !activeContainer.HasSpace)
                {
                    continue;
                }

                if (!LoopCollectSystem.CrossedDistance(
                        cube.PreviousAbsoluteDistance,
                        cube.AbsoluteDistance,
                        _collectDistance,
                        _path.Length))
                {
                    continue;
                }

                if (activeContainer.TryReserveCube(cube.Color))
                    _collectCandidates.Add(cube);
            }

            if (_collectCandidates.Count > 0)
                CollectCandidates();
        }

        private void CollectCandidates()
        {
            for (int i = 0; i < _collectCandidates.Count; i++)
            {
                LoopCube cube = _collectCandidates[i];
                int index = _cubes.IndexOf(cube);

                if (index < 0)
                    continue;

                _cubes.RemoveAt(index);
                cube.BeginTransfer();
                _inFlightCount++;
                _containerManager.ReceiveReservedCube(
                    cube,
                    HandleCubeArrived);
            }

            for (int i = 0; i < _cubes.Count; i++)
                _cubes[i].TargetOffset = -i * _effectiveSpacing;

            ProgressChanged?.Invoke();
            TryCompleteLevel();
        }

        private void HandleCubeArrived()
        {
            _inFlightCount = Mathf.Max(0, _inFlightCount - 1);
            ProgressChanged?.Invoke();
            TryCompleteLevel();
        }

        private void HandleContainerProgressChanged()
        {
            ProgressChanged?.Invoke();
            TryCompleteLevel();
        }

        private void TryCompleteLevel()
        {
            if (_completed ||
                _cubes.Count > 0 ||
                _inFlightCount > 0 ||
                !_containerManager.AllContainersCompleted)
            {
                return;
            }

            _completed = true;
            Completed?.Invoke();
        }

        private static bool CanRunGameplay()
        {
            return GameManager.Instance == null ||
                   GameManager.Instance.gameState == GameState.PlayingGame;
        }

        private void OnDestroy()
        {
            if (_containerManager != null)
            {
                _containerManager.ProgressChanged -=
                    HandleContainerProgressChanged;
            }
        }
    }
}
