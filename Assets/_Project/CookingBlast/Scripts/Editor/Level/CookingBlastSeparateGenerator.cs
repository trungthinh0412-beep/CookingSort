using System;
using System.Collections.Generic;
using CookingSort2D;
using UnityEditor;
using UnityEngine;

// The Board Editor serializes quantities by prefab reference, never by Color
// or list index. Different prefabs of one Type remain separately selectable.
[Serializable]
internal sealed class CookingBlastSpecialTileCount
{
    public FoodTileView prefab;
    [Min(0)] public int quantity;
}

internal static class CookingBlastSeparateGenerator
{
    private static readonly Vector2Int[] Adjacent = {
        Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
    };

    internal static bool GenerateAnimals(CookingBlastLevel level, int laneCount,
        bool specialAnimals, int seed, out string message)
    {
        message = "Open a valid level.";
        if (level == null || level.CustomerQueue == null) return false;
        var available = new Dictionary<FoodColor, List<CustomerView>>();
        for (int i = 0; i < level.CustomerQueue.ConfiguredAnimalPrefabCount; i++)
        {
            CustomerView prefab = level.CustomerQueue.GetConfiguredAnimalPrefab(i);
            if (prefab == null || prefab.Type == FoodColor.Neutral || prefab.Demand < 1) continue;
            if (!available.TryGetValue(prefab.Type, out List<CustomerView> list))
                available[prefab.Type] = list = new List<CustomerView>();
            list.Add(prefab);
        }
        var counts = CookingBlastLevelEditing.TileCounts(level);
        var orders = new List<CustomerOrderData>();
        var random = new System.Random(seed);
        int variants = 0;
        foreach (KeyValuePair<FoodColor, int> entry in counts)
        {
            if (entry.Key == FoodColor.Neutral || entry.Value <= 0) continue;
            if (!available.TryGetValue(entry.Key, out List<CustomerView> prefabs))
            {
                message = "No Animal prefab configured for " + entry.Key + ".";
                return false;
            }

            // First configured prefab of a Type is the regular one.
            CustomerView regular = level.CustomerQueue.GetAnimalPrefab(entry.Key);
            var candidates = new List<CustomerView>();
            if (regular != null && regular.Demand > 0) candidates.Add(regular);
            if (specialAnimals)
                foreach (CustomerView prefab in prefabs)
                    if (prefab != regular && prefab.Demand > 0)
                        candidates.Add(prefab);

            // Exact unbounded coin-change DP. No overshooting the Tile Value,
            // no missing value, and variants are chosen by exact prefab ref.
            var reachable = new bool[entry.Value + 1];
            reachable[0] = true;
            for (int value = 1; value <= entry.Value; value++)
                foreach (CustomerView animal in candidates)
                    if (animal.Demand <= value && reachable[value - animal.Demand])
                    {
                        reachable[value] = true;
                        break;
                    }
            if (!reachable[entry.Value])
            {
                message = entry.Key + " has Tile Value " + entry.Value +
                    " but no exact combination of available Animal Demands" +
                    (specialAnimals ? "." : " (Special Animals is OFF).");
                return false;
            }
            int remaining = entry.Value;
            while (remaining > 0)
            {
                var options = new List<CustomerView>();
                foreach (CustomerView animal in candidates)
                    if (animal.Demand <= remaining && reachable[remaining - animal.Demand])
                        options.Add(animal);
                CustomerView chosen = options[0];
                if (specialAnimals && options.Count > 1)
                {
                    // Prefer variations while retaining a completely solvable
                    // remainder. Multiple same-Type animals remain distinct.
                    var specialOptions = new List<CustomerView>();
                    foreach (CustomerView animal in options)
                        if (animal != regular) specialOptions.Add(animal);
                    if (specialOptions.Count > 0 &&
                        (variants == 0 || random.NextDouble() < .65))
                        chosen = specialOptions[random.Next(specialOptions.Count)];
                    else chosen = options[random.Next(options.Count)];
                }
                var order = new CustomerOrderData(entry.Key, chosen.Demand);
                order.animalPrefab = chosen == regular ? null : chosen;
                orders.Add(order);
                if (chosen != regular) variants++;
                remaining -= chosen.Demand;
            }
        }
        if (orders.Count == 0)
        {
            message = "Board has no Tile Value from which to generate Animals.";
            return false;
        }
        Shuffle(orders, random);
        int lanes = Mathf.Min(Mathf.Clamp(laneCount, 1, 8), orders.Count);
        var newLanes = new List<CustomerLaneData>();
        for (int i = 0; i < lanes; i++)
            newLanes.Add(new CustomerLaneData { frontToBack = new List<CustomerOrderData>() });
        for (int i = 0; i < orders.Count; i++)
            newLanes[i % lanes].frontToBack.Add(orders[i]);

        var oldLanes = new List<CustomerLaneData>(level.CustomerLanes);
        Undo.RecordObject(level, "Generate Cooking Blast Animals");
        level.CustomerLanes.Clear();
        level.CustomerLanes.AddRange(newLanes);
        if (!level.TryValidate(out string reason))
        {
            level.CustomerLanes.Clear();
            level.CustomerLanes.AddRange(oldLanes);
            message = "Animals not applied: " + reason;
            return false;
        }
        CookingBlastLevelEditing.Dirty(level);
        message = "Generated " + orders.Count + " Animals (" + variants +
            " special) in " + lanes + " lanes. Board unchanged. Ctrl+Z to undo.";
        return true;
    }



    private static bool TryPlace(FoodTileView prefab, List<Vector2Int> cells,
        HashSet<Vector2Int> cellSet, HashSet<Vector2Int> occupied,
        List<BoardClusterData> clusters, System.Random random)
    {
        int w = Mathf.Max(1, prefab.FootprintWidth);
        int h = Mathf.Max(1, prefab.FootprintHeight);
        for (int trial = 0; trial < 650; trial++)
        {
            Vector2Int anchor = cells[random.Next(cells.Count)];
            bool valid = true;
            for (int c = anchor.x; c < anchor.x + w && valid; c++)
            for (int r = anchor.y; r < anchor.y + h; r++)
                if (!cellSet.Contains(new Vector2Int(c, r)) ||
                    occupied.Contains(new Vector2Int(c, r)))
                {
                    valid = false;
                    break;
                }
            if (!valid) continue;
            clusters.Add(new BoardClusterData {
                color = prefab.Type, value = prefab.FootprintValue,
                anchorColumn = anchor.x, anchorRow = anchor.y,
                width = w, height = h
            });
            for (int c = anchor.x; c < anchor.x + w; c++)
            for (int r = anchor.y; r < anchor.y + h; r++)
                occupied.Add(new Vector2Int(c, r));
            return true;
        }
        return false;
    }

    private static void Shuffle<T>(List<T> values, System.Random random)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = random.Next(i+1);
            T old = values[i]; values[i] = values[j]; values[j] = old;
        }
    }
}
