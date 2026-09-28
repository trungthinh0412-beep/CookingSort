using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicSoft.LoopSort
{
    public enum LoopCubeColor
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
        Orange
    }

    [Serializable]
    public sealed class LoopContainerLevelData
    {
        public LoopCubeColor color;

        [Min(1)]
        public int capacity = 10;

        public LoopContainerLevelData Clone()
        {
            return new LoopContainerLevelData
            {
                color = color,
                capacity = Mathf.Max(1, capacity)
            };
        }
    }

    public sealed class LoopLevelRuntimeData
    {
        public readonly List<LoopCubeColor> CubeSequence =
            new List<LoopCubeColor>();

        public readonly List<LoopContainerLevelData> Containers =
            new List<LoopContainerLevelData>();

        public float LoopSpeed = 1.8f;
        public float CubeSpacing = 0.42f;
        public float GapCloseSpeed = 1.8f;
        public float CubeScale = 0.34f;
        public float LoopWidth = 9f;
        public float LoopHeight = 5.6f;
        public int PathControlPointCount = 16;

        public bool TryValidate(out string error)
        {
            if (CubeSequence.Count == 0)
            {
                error = "Cube sequence is empty.";
                return false;
            }

            if (Containers.Count == 0)
            {
                error = "Container list is empty.";
                return false;
            }

            Dictionary<LoopCubeColor, int> cubeCounts =
                new Dictionary<LoopCubeColor, int>();
            Dictionary<LoopCubeColor, int> capacities =
                new Dictionary<LoopCubeColor, int>();

            for (int i = 0; i < CubeSequence.Count; i++)
                AddCount(cubeCounts, CubeSequence[i], 1);

            for (int i = 0; i < Containers.Count; i++)
            {
                LoopContainerLevelData container = Containers[i];

                if (container == null || container.capacity <= 0)
                {
                    error = $"Container {i} has an invalid capacity.";
                    return false;
                }

                AddCount(
                    capacities,
                    container.color,
                    container.capacity);
            }

            foreach (KeyValuePair<LoopCubeColor, int> pair in cubeCounts)
            {
                capacities.TryGetValue(pair.Key, out int capacity);

                if (capacity != pair.Value)
                {
                    error = $"{pair.Key}: {pair.Value} cubes but " +
                            $"container capacity is {capacity}.";
                    return false;
                }
            }

            foreach (KeyValuePair<LoopCubeColor, int> pair in capacities)
            {
                cubeCounts.TryGetValue(pair.Key, out int cubeCount);

                if (cubeCount != pair.Value)
                {
                    error = $"{pair.Key}: container capacity is " +
                            $"{pair.Value} but there are {cubeCount} cubes.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static void AddCount(
            Dictionary<LoopCubeColor, int> counts,
            LoopCubeColor color,
            int amount)
        {
            counts.TryGetValue(color, out int current);
            counts[color] = current + amount;
        }
    }

    public static class LoopLevelFactory
    {
        private static readonly LoopCubeColor[] AvailableColors =
        {
            LoopCubeColor.Red,
            LoopCubeColor.Blue,
            LoopCubeColor.Green,
            LoopCubeColor.Yellow,
            LoopCubeColor.Purple,
            LoopCubeColor.Orange
        };

        public static LoopLevelRuntimeData Create(int levelIndex)
        {
            int safeLevel = Mathf.Max(1, levelIndex);
            int colorCount = Mathf.Clamp(
                3 + (safeLevel - 1) / 3,
                3,
                5);
            int cubesPerColor = Mathf.Clamp(
                8 + (safeLevel - 1) / 2,
                8,
                12);

            LoopLevelRuntimeData data = new LoopLevelRuntimeData
            {
                LoopSpeed = Mathf.Min(
                    2.7f,
                    1.65f + (safeLevel - 1) * 0.035f),
                CubeSpacing = colorCount >= 5 ? 0.36f : 0.42f,
                GapCloseSpeed = 2.2f,
                CubeScale = colorCount >= 5 ? 0.3f : 0.34f,
                LoopWidth = colorCount >= 5 ? 10.5f : 9f,
                LoopHeight = colorCount >= 5 ? 6.3f : 5.6f,
                PathControlPointCount = 16
            };

            for (int colorIndex = 0;
                 colorIndex < colorCount;
                 colorIndex++)
            {
                LoopCubeColor color = AvailableColors[colorIndex];

                data.Containers.Add(new LoopContainerLevelData
                {
                    color = color,
                    capacity = cubesPerColor
                });

                for (int cubeIndex = 0;
                     cubeIndex < cubesPerColor;
                     cubeIndex++)
                {
                    data.CubeSequence.Add(color);
                }
            }

            System.Random random = new System.Random(
                1729 + safeLevel * 7919);
            Shuffle(data.CubeSequence, random);
            Shuffle(data.Containers, random);
            return data;
        }

        private static void Shuffle<T>(
            IList<T> items,
            System.Random random)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                T temp = items[i];
                items[i] = items[other];
                items[other] = temp;
            }
        }
    }
}
