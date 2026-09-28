using System.Collections.Generic;
using UnityEngine;

namespace MagicSoft.LoopSort
{
    [DisallowMultipleComponent]
    public sealed class LoopLevelAuthoring : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private LoopPath loopPath;
        [SerializeField] private Transform cubeRoot;
        [SerializeField] private Transform containerRoot;
        [SerializeField] private Transform activeSlot;
        [SerializeField] private LoopColorDatabase colorDatabase;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float loopSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float cubeSpacing = 0.42f;
        [SerializeField, Min(0.1f)] private float gapCloseSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float cubeScale = 0.34f;

        public LoopPath Path => loopPath;
        public Transform CubeRoot => cubeRoot;
        public Transform ContainerRoot => containerRoot;
        public Transform ActiveSlot => activeSlot;
        public LoopColorDatabase ColorDatabase => colorDatabase;
        public float LoopSpeed => Mathf.Max(0.1f, loopSpeed);
        public float CubeSpacing => Mathf.Max(0.1f, cubeSpacing);
        public float GapCloseSpeed => Mathf.Max(0.1f, gapCloseSpeed);
        public float CubeScale => Mathf.Max(0.1f, cubeScale);

        public List<LoopCube> GetOrderedCubes()
        {
            List<LoopCube> result = new List<LoopCube>();

            if (cubeRoot == null)
                return result;

            for (int i = 0; i < cubeRoot.childCount; i++)
            {
                LoopCube cube = cubeRoot.GetChild(i)
                    .GetComponentInChildren<LoopCube>(true);

                if (cube != null)
                    result.Add(cube);
            }

            return result;
        }

        public List<LoopContainer> GetOrderedContainers()
        {
            List<LoopContainer> result = new List<LoopContainer>();

            if (containerRoot == null)
                return result;

            for (int i = 0; i < containerRoot.childCount; i++)
            {
                LoopContainer container = containerRoot.GetChild(i)
                    .GetComponentInChildren<LoopContainer>(true);

                if (container != null)
                    result.Add(container);
            }

            return result;
        }

        public bool TryValidate(out string error)
        {
            if (loopPath == null || cubeRoot == null ||
                containerRoot == null || activeSlot == null)
            {
                error = "Level is missing Path, CubeRoot, ContainerRoot or ActiveSlot.";
                return false;
            }

            List<LoopCube> cubes = GetOrderedCubes();
            List<LoopContainer> containers = GetOrderedContainers();

            if (cubes.Count == 0 || containers.Count == 0)
            {
                error = "Level needs at least one Cube and one Container prefab instance.";
                return false;
            }

            Dictionary<LoopCubeColor, int> cubeCounts =
                new Dictionary<LoopCubeColor, int>();
            Dictionary<LoopCubeColor, int> capacities =
                new Dictionary<LoopCubeColor, int>();

            for (int i = 0; i < cubes.Count; i++)
                AddCount(cubeCounts, cubes[i].AuthoringColor, 1);

            for (int i = 0; i < containers.Count; i++)
            {
                AddCount(
                    capacities,
                    containers[i].AuthoringColor,
                    containers[i].AuthoringCapacity);
            }

            foreach (KeyValuePair<LoopCubeColor, int> pair in cubeCounts)
            {
                capacities.TryGetValue(pair.Key, out int capacity);

                if (capacity != pair.Value)
                {
                    error = $"{pair.Key}: {pair.Value} cubes but container capacity is {capacity}.";
                    return false;
                }
            }

            foreach (KeyValuePair<LoopCubeColor, int> pair in capacities)
            {
                cubeCounts.TryGetValue(pair.Key, out int cubeCount);

                if (cubeCount != pair.Value)
                {
                    error = $"{pair.Key}: capacity is {pair.Value} but level has {cubeCount} cubes.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void RefreshPreview()
        {
            if (loopPath == null)
                return;

            loopPath.RebuildAuthoringPath();
            List<LoopCube> cubes = GetOrderedCubes();

            for (int i = 0; i < cubes.Count; i++)
            {
                LoopCube cube = cubes[i];
                cube.transform.position = loopPath.GetPosition(-i * CubeSpacing);
                cube.transform.localScale = Vector3.one * CubeScale;
                cube.RefreshAuthoringVisual(colorDatabase);
            }

            List<LoopContainer> containers = GetOrderedContainers();

            for (int i = 0; i < containers.Count; i++)
                containers[i].RefreshAuthoringVisual(colorDatabase);
        }

        public void AssignReferences(
            LoopPath path,
            Transform cubes,
            Transform containers,
            Transform slot,
            LoopColorDatabase database)
        {
            loopPath = path;
            cubeRoot = cubes;
            containerRoot = containers;
            activeSlot = slot;
            colorDatabase = database;
        }

        private static void AddCount(
            Dictionary<LoopCubeColor, int> counts,
            LoopCubeColor color,
            int amount)
        {
            counts.TryGetValue(color, out int current);
            counts[color] = current + amount;
        }

        private void OnValidate()
        {
            loopSpeed = Mathf.Max(0.1f, loopSpeed);
            cubeSpacing = Mathf.Max(0.1f, cubeSpacing);
            gapCloseSpeed = Mathf.Max(0.1f, gapCloseSpeed);
            cubeScale = Mathf.Max(0.1f, cubeScale);
        }
    }
}
