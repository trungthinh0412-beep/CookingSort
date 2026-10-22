using System;
using System.Collections.Generic;
using CookingSort2D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
internal static class CookingBlastLevelEditing
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
    };
    internal static SerializedProperty BoardColumns(SerializedObject so) => so.FindProperty("boardColumns");
    internal static SerializedProperty CustomerLanes(SerializedObject so) => so.FindProperty("customerLanes");

    internal static int MaxRows(CookingBlastLevel level)
    {
        if (level == null) return 0;
        int max = 0;
        foreach (BoardColumnData column in level.BoardColumns)
            if (column != null && column.bottomToTop != null) max = Mathf.Max(max, column.bottomToTop.Count);
        return max;
    }

    internal static void ResizeBoard(CookingBlastLevel level, int columns, int rows, FoodColor fill)
    {
        if (level == null) return;
        columns = Mathf.Clamp(columns, 1, CookingBlastPixelGridDetector.MaxCellsX);
        rows = Mathf.Clamp(rows, 1, CookingBlastPixelGridDetector.MaxCellsY);
        Undo.RecordObject(level, "Resize Cooking Blast board");
        var so = new SerializedObject(level);
        var cols = BoardColumns(so);
        cols.arraySize = columns;
        for (int c = 0; c < columns; c++)
        {
            var cells = cols.GetArrayElementAtIndex(c).FindPropertyRelative("bottomToTop");
            int old = cells.arraySize;
            cells.arraySize = rows;
            for (int r = old; r < rows; r++) cells.GetArrayElementAtIndex(r).enumValueIndex = (int)fill;
        }
        so.ApplyModifiedProperties();
        Dirty(level);
    }

    internal static void SetCell(CookingBlastLevel level, int column, int row, FoodColor color)
    {
        if (level == null) return;
        var so = new SerializedObject(level);
        var cols = BoardColumns(so);
        if (column < 0 || column >= cols.arraySize) return;
        var cells = cols.GetArrayElementAtIndex(column).FindPropertyRelative("bottomToTop");
        if (row < 0 || row >= cells.arraySize) return;
        Undo.RecordObject(level, "Paint Cooking Blast cell");
        cells.GetArrayElementAtIndex(row).enumValueIndex = (int)color;
        so.ApplyModifiedProperties();
        Dirty(level);
    }

    internal static FoodColor GetCell(CookingBlastLevel level, int column, int row)
    {
        if (level == null || column < 0 || column >= level.BoardColumns.Count) return FoodColor.Blue;
        var col = level.BoardColumns[column];
        if (col == null || col.bottomToTop == null || row < 0 || row >= col.bottomToTop.Count) return FoodColor.Blue;
        return col.bottomToTop[row];
    }

    internal static void FillBoard(CookingBlastLevel level, FoodColor color)
    {
        if (level == null) return;
        Undo.RecordObject(level, "Fill Cooking Blast board");
        foreach (var column in level.BoardColumns)
        {
            if (column == null || column.bottomToTop == null) continue;
            for (int i = 0; i < column.bottomToTop.Count; i++) column.bottomToTop[i] = color;
        }
        Dirty(level);
    }

internal static int FloodFillBoard(CookingBlastLevel level, int startColumn, int startRow, FoodColor newColor)
    {
        if (level == null || level.BoardColumns == null) return 0;
        if (startColumn < 0 || startColumn >= level.BoardColumns.Count) return 0;

        BoardColumnData originColumn = level.BoardColumns[startColumn];
        if (originColumn == null || originColumn.bottomToTop == null ||
            startRow < 0 || startRow >= originColumn.bottomToTop.Count) return 0;

        FoodColor originalColor = originColumn.bottomToTop[startRow];
        if (originalColor == newColor) return 0;

        // Collect the complete 4-connected region before editing its colors.
        var stack = new Stack<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        var region = new List<Vector2Int>();
        stack.Push(new Vector2Int(startColumn, startRow));

        while (stack.Count > 0)
        {
            Vector2Int cell = stack.Pop();
            if (!visited.Add(cell) || cell.x < 0 || cell.x >= level.BoardColumns.Count)
                continue;

            BoardColumnData column = level.BoardColumns[cell.x];
            if (column == null || column.bottomToTop == null ||
                cell.y < 0 || cell.y >= column.bottomToTop.Count ||
                column.bottomToTop[cell.y] != originalColor)
                continue;

            region.Add(cell);
            stack.Push(new Vector2Int(cell.x - 1, cell.y));
            stack.Push(new Vector2Int(cell.x + 1, cell.y));
            stack.Push(new Vector2Int(cell.x, cell.y - 1));
            stack.Push(new Vector2Int(cell.x, cell.y + 1));
        }

        if (region.Count == 0) return 0;
        Undo.RecordObject(level, "Bucket Fill Cooking Blast Tile Region");
        foreach (Vector2Int cell in region)
            level.BoardColumns[cell.x].bottomToTop[cell.y] = newColor;
        Dirty(level);
        return region.Count;
    }


    internal static void RandomizeBoard(CookingBlastLevel level, IReadOnlyList<FoodColor> palette, int seed)
    {
        if (level == null || palette == null || palette.Count == 0) return;
        Undo.RecordObject(level, "Randomize Cooking Blast board");
        var random = new System.Random(seed);
        foreach (var column in level.BoardColumns)
        {
            if (column == null || column.bottomToTop == null) continue;
            for (int i = 0; i < column.bottomToTop.Count; i++)
                column.bottomToTop[i] = palette[random.Next(0, palette.Count)];
        }
        Dirty(level);
    }

internal static bool TryAutoFillFromCustomers(CookingBlastLevel level, int seed, bool spreadColors, out string message)
    {
        message = "Open a level prefab first.";
        if (level == null) return false;
        var demand = DemandCounts(level);
        int required = 0;
        foreach (var pair in demand) required += pair.Value;
        if (required != level.TotalTileCount)
        {
            message = "Cannot match customers: board has " + level.TotalTileCount + " tiles but customers require " + required + ". Adjust demand or use Random Fill + Rebuild Customers.";
            return false;
        }
        if (required == 0) { message = "Add customer demand before auto filling."; return false; }
        Undo.RecordObject(level, "Auto Fill Tile Colors From Customers");
        ApplyColorDistribution(level, demand, seed, spreadColors);
        Dirty(level);
        message = "Filled " + required + " tiles matching customer demand exactly.";
        return true;
    }

internal static bool TryAutoFillByCustomerOrder(CookingBlastLevel level, int clusterSize,
        out string message)
    {
        message = "Open a level prefab first.";
        if (level == null || level.BoardColumns == null || level.CustomerLanes == null)
            return false;

        int slots = level.TotalTileCount;
        int lanes = level.CustomerLanes.Count;
        if (slots == 0 || lanes == 0)
        {
            message = "Create a Board and Customer lanes first.";
            return false;
        }

        int required = 0;
        var orders = new List<List<CustomerOrderData>>();
        for (int lane = 0; lane < lanes; lane++)
        {
            CustomerLaneData data = level.CustomerLanes[lane];
            if (data == null || data.frontToBack == null || data.frontToBack.Count == 0)
            {
                message = "Lane " + (lane + 1) + " has no customers.";
                return false;
            }

            var laneOrders = data.frontToBack;
            foreach (CustomerOrderData customer in laneOrders)
            {
                if (customer == null || customer.demand <= 0)
                {
                    message = "Lane " + (lane + 1) + " has an invalid Customer.";
                    return false;
                }
                required += customer.demand;
            }
            orders.Add(laneOrders);
        }
        if (required != slots)
        {
            message = "Cannot fill: Board has " + slots +
                      " Tiles but Customers require " + required +
                      ". Balance the total demand first.";
            return false;
        }

        // Each lane advances Front -> Back independently. Distribute chunks
        // across lanes so several front customers get playable bottom tiles.
        clusterSize = Mathf.Clamp(clusterSize, 1, 10);
        var orderIndex = new int[lanes];
        var remaining = new int[lanes];
        for (int lane = 0; lane < lanes; lane++)
            remaining[lane] = orders[lane][0].demand;

        var result = new List<FoodColor>(slots);
        int cursor = 0;
        int firstRowWidth = 0;
        foreach (BoardColumnData column in level.BoardColumns)
            if (column != null && column.bottomToTop != null &&
                column.bottomToTop.Count > 0) firstRowWidth++;

        // Where space allows, reserve at least one exposed bottom tile for
        // every front lane before filling larger color clusters.
        int frontTileCount = Mathf.Min(firstRowWidth, lanes);
        for (int lane = 0; lane < frontTileCount; lane++)
        {
            result.Add(orders[lane][orderIndex[lane]].color);
            remaining[lane]--;
            if (remaining[lane] == 0)
            {
                orderIndex[lane]++;
                if (orderIndex[lane] < orders[lane].Count)
                    remaining[lane] = orders[lane][orderIndex[lane]].demand;
            }
        }

        // Continue in lane order, advancing to the next customer only after
        // that lane's current demand has been assigned.
        while (result.Count < slots)
        {
            bool found = false;
            for (int attempt = 0; attempt < lanes; attempt++)
            {
                int lane = (cursor + attempt) % lanes;
                if (orderIndex[lane] >= orders[lane].Count) continue;
                int count = Mathf.Min(clusterSize, remaining[lane]);
                FoodColor color = orders[lane][orderIndex[lane]].color;
                for (int i = 0; i < count; i++) result.Add(color);
                remaining[lane] -= count;
                if (remaining[lane] == 0)
                {
                    orderIndex[lane]++;
                    if (orderIndex[lane] < orders[lane].Count)
                        remaining[lane] = orders[lane][orderIndex[lane]].demand;
                }
                cursor = (lane + 1) % lanes;
                found = true;
                break;
            }
            if (!found)
            {
                message = "Could not distribute all customer demand.";
                return false;
            }
        }

        // The first list items become the lowest visible Tile row, left to right.
        Undo.RecordObject(level, "Fill Tile Colors in Customer Queue Order");
        int tileIndex = 0;
        int maxRows = MaxRows(level);
        for (int row = 0; row < maxRows; row++)
        {
            for (int col = 0; col < level.BoardColumns.Count; col++)
            {
                BoardColumnData column = level.BoardColumns[col];
                if (column == null || column.bottomToTop == null ||
                    row >= column.bottomToTop.Count) continue;
                column.bottomToTop[row] = result[tileIndex++];
            }
        }
        Dirty(level);
        message = "Filled " + tileIndex +
                  " Tiles following Customer Front-to-Back order, with chunks of " +
                  clusterSize + " (balanced by color).";
        return true;
    }

internal static bool TryAutoFillByPriority(CookingBlastLevel level,
        IReadOnlyList<Vector2Int> priority, int tilesPerTurn, bool interleave,
        out string message)
    {
        message = "Open a valid Cooking Blast level.";
        if (level == null || priority == null ||
            level.BoardColumns == null || level.CustomerLanes == null)
            return false;

        var all = new List<CustomerOrderData>();
        var visited = new HashSet<Vector2Int>();
        int totalDemand = 0;
        for (int i = 0; i < priority.Count; i++)
        {
            Vector2Int id = priority[i];
            if (!visited.Add(id) || id.x < 0 || id.x >= level.CustomerLanes.Count)
            {
                message = "Fill order contains a duplicate or invalid Customer.";
                return false;
            }
            CustomerLaneData lane = level.CustomerLanes[id.x];
            if (lane == null || lane.frontToBack == null ||
                id.y < 0 || id.y >= lane.frontToBack.Count)
            {
                message = "Fill order no longer matches the Customer Queue. Reset the order.";
                return false;
            }
            CustomerOrderData customer = lane.frontToBack[id.y];
            if (customer == null || customer.demand < 1)
            {
                message = "A Customer has invalid Demand.";
                return false;
            }
            all.Add(customer);
            totalDemand += customer.demand;
        }

        int customerTotal = 0;
        foreach (CustomerLaneData lane in level.CustomerLanes)
            if (lane != null && lane.frontToBack != null)
                customerTotal += lane.frontToBack.Count;

        if (all.Count != customerTotal || customerTotal == 0)
        {
            message = "The order must include every Customer exactly once.";
            return false;
        }
        if (totalDemand != level.TotalTileCount || totalDemand == 0)
        {
            message = "Cannot Auto Fill: " + level.TotalTileCount +
                      " Tile slots but Customer Demand totals " + totalDemand + ".";
            return false;
        }

        var result = new List<FoodColor>(totalDemand);
        int[] remaining = new int[all.Count];
        for (int i = 0; i < all.Count; i++)
            remaining[i] = all[i].demand;

        tilesPerTurn = Mathf.Clamp(tilesPerTurn, 1, 32);
        if (!interleave)
        {
            for (int i = 0; i < all.Count; i++)
                while (remaining[i]-- > 0)
                    result.Add(all[i].color);
        }
        else
        {
            int pending = totalDemand;
            while (pending > 0)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    int chunk = Mathf.Min(tilesPerTurn, remaining[i]);
                    for (int j = 0; j < chunk; j++)
                        result.Add(all[i].color);
                    remaining[i] -= chunk;
                    pending -= chunk;
                }
            }
        }

        // Assign in the exact order the Board scans: bottom row, left to right,
        // followed by higher rows. No Customer Queue data is modified.
        Undo.RecordObject(level, "Auto Fill Tiles in Custom Customer Priority");
        int index = 0;
        int maxRows = MaxRows(level);
        for (int row = 0; row < maxRows; row++)
        {
            for (int col = 0; col < level.BoardColumns.Count; col++)
            {
                BoardColumnData column = level.BoardColumns[col];
                if (column == null || column.bottomToTop == null ||
                    row >= column.bottomToTop.Count) continue;
                column.bottomToTop[row] = result[index++];
            }
        }
        Dirty(level);
        message = "Filled " + index + " Tiles in your chosen Customer order. " +
                  (interleave ? "Grouped in batches of " + tilesPerTurn + "." : "Entire Customer demands filled in sequence.");
        return true;
    }

internal static bool TryAutoFillOrganicByPriority(
        CookingBlastLevel level, IReadOnlyList<Vector2Int> priority, int tilesPerTurn,
        bool interleave, int seed, int organicStrength, out string message)
    {
        // Reuse strict Customer/Board validation. Any invalid order or mismatched
        // demand returns BEFORE we attempt to generate organic patterns.
        if (!TryAutoFillByPriority(level, priority, tilesPerTurn, interleave, out message))
            return false;

        var cells = new List<Vector2Int>();
        var lookup = new Dictionary<Vector2Int, int>();
        int maxRows = MaxRows(level);
        for (int row = 0; row < maxRows; row++)
        {
            for (int col = 0; col < level.BoardColumns.Count; col++)
            {
                BoardColumnData column = level.BoardColumns[col];
                if (column == null || column.bottomToTop == null ||
                    row >= column.bottomToTop.Count) continue;
                var pos = new Vector2Int(col, row);
                lookup[pos] = cells.Count;
                cells.Add(pos);
            }
        }
        if (cells.Count == 0) return true;

        var orders = new List<CustomerOrderData>();
        foreach (Vector2Int id in priority)
            orders.Add(level.CustomerLanes[id.x].frontToBack[id.y]);

        var remaining = new int[orders.Count];
        for (int i = 0; i < orders.Count; i++)
            remaining[i] = orders[i].demand;

        var taken = new bool[cells.Count];
        var result = new FoodColor[cells.Count];
        var random = new System.Random(seed);
        organicStrength = Mathf.Clamp(organicStrength, 0, 100);
        int count = 0;
        tilesPerTurn = Mathf.Clamp(tilesPerTurn, 1, 32);
        int safety = 0;

        // Grow each Customer's patches from already-placed tiles of the same
        // color. A seeded jitter in frontier selection makes irregular shapes,
        // while neighbor scoring keeps large areas visually coherent.
        while (count < cells.Count && safety++ < cells.Count + orders.Count + 2)
        {
            bool progress = false;
            for (int customer = 0; customer < orders.Count; customer++)
            {
                int batch = Mathf.Min(remaining[customer],
                    interleave ? tilesPerTurn : remaining[customer]);
                FoodColor color = orders[customer].color;

                for (int step = 0; step < batch; step++)
                {
                    int best = -1;
                    float bestScore = float.NegativeInfinity;
                    bool hasFrontier = false;

                    if (count == 0)
                    {
                        best = 0; // Ensure first priority color begins bottom-left.
                    }
                    else
                    {
                        for (int candidate = 0; candidate < cells.Count; candidate++)
                        {
                            if (taken[candidate]) continue;
                            Vector2Int pos = cells[candidate];
                            int matched = 0;
                            int touched = 0;
                            for (int n = 0; n < CardinalDirections.Length; n++)
                            {
                                if (!lookup.TryGetValue(pos + CardinalDirections[n], out int neighbor) ||
                                    !taken[neighbor]) continue;
                                touched++;
                                if (result[neighbor] == color) matched++;
                            }

                            if (matched == 0) continue;
                            hasFrontier = true;
                            // A connected frontier with several matching
                            // neighbors is preferable. Organic Shape controls
                            // how wavy vs compact its edge becomes.
                            float jitter = (float)random.NextDouble() *
                                           (1.8f + organicStrength * 0.12f);
                            float score = matched * 5.5f + jitter -
                                          touched * 0.1f;
                            if (score > bestScore)
                            {
                                bestScore = score;
                                best = candidate;
                            }
                        }

                        // A color appearing for the first time has no existing
                        // frontier. Seed it near the next available bottom cell,
                        // then future tiles grow out into their own patch.
                        if (!hasFrontier)
                        {
                            for (int candidate = 0; candidate < cells.Count; candidate++)
                            {
                                if (taken[candidate]) continue;
                                Vector2Int pos = cells[candidate];
                                float jitter = (float)random.NextDouble() *
                                               (0.2f + organicStrength * 0.01f);
                                float score = -(pos.y * level.BoardColumns.Count + pos.x) +
                                              jitter;
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    best = candidate;
                                }
                            }
                        }
                    }

                    if (best < 0)
                    {
                        message = "Organic Fill could not locate an empty Board cell.";
                        return false;
                    }
                    taken[best] = true;
                    result[best] = color;
                    remaining[customer]--;
                    count++;
                    progress = true;
                }
            }
            if (!progress) break;
        }

        if (count != cells.Count)
        {
            message = "Organic Fill could not satisfy every Customer demand.";
            return false;
        }

        int exposedFrontColors = ExposeFrontCustomerColors(level, cells, result);
        Undo.RecordObject(level, "Shape Cooking Blast Organic Tile Regions");
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int pos = cells[i];
            level.BoardColumns[pos.x].bottomToTop[pos.y] = result[i];
        }
        Dirty(level);
        message = "Organic Clusters: " + count + " tiles, Seed " + seed +
                  ". Customer demand preserved; first priority stays at the bottom. " +
                  exposedFrontColors + " missing Front color(s) exposed.";
        return true;
    }

private static int ExposeFrontCustomerColors(
        CookingBlastLevel level, List<Vector2Int> positions,
        FoodColor[] assigned)
    {
        if (level == null || positions.Count == 0) return 0;

        var desired = new HashSet<FoodColor>();
        foreach (CustomerLaneData lane in level.CustomerLanes)
            if (lane != null && lane.frontToBack != null &&
                lane.frontToBack.Count > 0 && lane.frontToBack[0] != null)
                desired.Add(lane.frontToBack[0].color);

        var bottomCounts = new Dictionary<FoodColor, int>();
        for (int i = 0; i < positions.Count; i++)
        {
            if (positions[i].y != 0) continue;
            FoodColor color = assigned[i];
            if (!bottomCounts.ContainsKey(color)) bottomCounts[color] = 0;
            bottomCounts[color]++;
        }
        int changed = 0;
        foreach (FoodColor desiredColor in desired)
        {
            if (bottomCounts.TryGetValue(desiredColor, out int existing) && existing > 0)
                continue;

            // Prefer a source near the front rather than moving a Tile from
            // the top of the board. Never move the first priority tile.
            int source = -1;
            for (int i = 1; i < positions.Count; i++)
            {
                if (positions[i].y > 0 && assigned[i] == desiredColor)
                {
                    source = i;
                    break;
                }
            }
            if (source < 0) continue;

            int target = -1;
            for (int i = positions.Count - 1; i >= 1; i--)
            {
                if (positions[i].y != 0) continue;
                FoodColor displaced = assigned[i];
                bottomCounts.TryGetValue(displaced, out int count);
                // Don't hide the only exposed Tile for another front Customer.
                if (count < 2 && desired.Contains(displaced)) continue;
                target = i;
                break;
            }
            if (target < 0) continue;

            FoodColor replaced = assigned[target];
            assigned[target] = desiredColor;
            assigned[source] = replaced;
            bottomCounts[replaced]--;
            bottomCounts[desiredColor] = 1;
            changed++;
        }
        return changed;
    }








internal static bool ShuffleBoardKeepingCounts(CookingBlastLevel level, int seed, bool spreadColors)
    {
        if (level == null || level.TotalTileCount == 0) return false;
        var totals = TileCounts(level);
        Undo.RecordObject(level, "Shuffle Cooking Blast Tile Colors");
        ApplyColorDistribution(level, totals, seed, spreadColors);
        Dirty(level);
        return true;
    }

private static void ApplyColorDistribution(CookingBlastLevel level, Dictionary<FoodColor, int> available, int seed, bool spreadColors)
    {
        var remaining = new Dictionary<FoodColor, int>(available);
        var random = new System.Random(seed);
        int maxRows = MaxRows(level);
        for (int row = 0; row < maxRows; row++)
        {
            for (int col = 0; col < level.BoardColumns.Count; col++)
            {
                var column = level.BoardColumns[col];
                if (column == null || column.bottomToTop == null || row >= column.bottomToTop.Count) continue;
                int totalWeight = 0;
                foreach (FoodColor color in Enum.GetValues(typeof(FoodColor)))
                    if (remaining.TryGetValue(color, out int count) && count > 0)
                        totalWeight += ColorPlacementWeight(level, remaining, color, col, row, spreadColors);
                if (totalWeight <= 0) return;
                int choice = random.Next(totalWeight);
                FoodColor chosen = default(FoodColor);
                foreach (FoodColor color in Enum.GetValues(typeof(FoodColor)))
                {
                    if (!remaining.TryGetValue(color, out int count) || count <= 0) continue;
                    choice -= ColorPlacementWeight(level, remaining, color, col, row, spreadColors);
                    if (choice < 0) { chosen = color; break; }
                }
                column.bottomToTop[row] = chosen;
                remaining[chosen]--;
            }
        }
    }

private static int ColorPlacementWeight(CookingBlastLevel level, Dictionary<FoodColor, int> remaining, FoodColor color, int column, int row, bool spreadColors)
    {
        int count = remaining[color];
        if (!spreadColors) return count;
        bool sameBelow = row > 0 && GetCell(level, column, row - 1) == color;
        bool sameLeft = column > 0 && level.BoardColumns[column - 1] != null && level.BoardColumns[column - 1].bottomToTop != null &&
                        row < level.BoardColumns[column - 1].bottomToTop.Count && GetCell(level, column - 1, row) == color;
        int factor = sameBelow && sameLeft ? 1 : (sameBelow || sameLeft ? 4 : 16);
        return count * factor;
    }





    internal static void EnsureLaneCount(CookingBlastLevel level, int laneCount)
    {
        if (level == null) return;
        laneCount = Mathf.Clamp(laneCount, 1, 8);
        Undo.RecordObject(level, "Resize customer lanes");
        var so = new SerializedObject(level);
        CustomerLanes(so).arraySize = laneCount;
        so.ApplyModifiedProperties();
        Dirty(level);
    }

    internal static void AutoGenerateCustomers(CookingBlastLevel level, int laneCount, int minDemand, int maxDemand)
    {
        if (level == null) return;
        laneCount = Mathf.Clamp(laneCount, 1, 8);
        minDemand = Mathf.Max(1, minDemand);
        maxDemand = Mathf.Max(minDemand, maxDemand);

        var counts = new Dictionary<FoodColor, int>();
        foreach (var column in level.BoardColumns)
        {
            if (column == null || column.bottomToTop == null) continue;
            foreach (FoodColor color in column.bottomToTop)
            {
                if (!counts.ContainsKey(color)) counts[color] = 0;
                counts[color]++;
            }
        }

        Undo.RecordObject(level, "Generate Cooking Blast customers");
        var so = new SerializedObject(level);
        var lanes = CustomerLanes(so);
        lanes.arraySize = laneCount;
        for (int l = 0; l < laneCount; l++) lanes.GetArrayElementAtIndex(l).FindPropertyRelative("frontToBack").ClearArray();

        int lane = 0;
        foreach (var pair in counts)
        {
            int remaining = pair.Value;
            bool toggle = false;
            while (remaining > 0)
            {
                int desired = toggle ? minDemand : maxDemand;
                int demand = Mathf.Min(desired, remaining);
                if (remaining - demand > 0 && remaining - demand < minDemand)
                    demand = Mathf.Max(1, remaining - minDemand);

                var list = lanes.GetArrayElementAtIndex(lane % laneCount).FindPropertyRelative("frontToBack");
                int index = list.arraySize++;
                var order = list.GetArrayElementAtIndex(index);
                order.FindPropertyRelative("color").enumValueIndex = (int)pair.Key;
                order.FindPropertyRelative("demand").intValue = demand;
                remaining -= demand;
                lane++;
                toggle = !toggle;
            }
        }
        so.ApplyModifiedProperties();
        Dirty(level);
    }

internal static bool TryGenerateExactCustomerCount(
        CookingBlastLevel level, int requestedCustomers, int requestedLanes, out string message)
    {
        message = "Open a Cooking Blast Level first.";
        if (level == null) return false;

        var counts = TileCounts(level);
        var colors = new List<FoodColor>();
        int total = 0;
        foreach (FoodColor color in Enum.GetValues(typeof(FoodColor)))
        {
            if (!counts.TryGetValue(color, out int amount) || amount <= 0) continue;
            colors.Add(color);
            total += amount;
        }

        if (colors.Count == 0)
        {
            message = "No Tiles on Board.";
            return false;
        }
        if (requestedCustomers < colors.Count || requestedCustomers > total)
        {
            message = "Customer count must be between " + colors.Count +
                      " (one per used Tile color) and " + total +
                      " (every Customer needs at least one Tile).";
            return false;
        }

        int lanesCount = Mathf.Clamp(requestedLanes, 1, 8);
        if (lanesCount > requestedCustomers) lanesCount = requestedCustomers;

        // Each used FoodColor gets at least one Customer. Add the remaining
        // Customers to the color with the largest average Tile count per
        // Customer. This avoids generating a large number of single-Tile
        // Customers while another color has disproportionately high demand.
        var slots = new Dictionary<FoodColor, int>();
        foreach (FoodColor color in colors) slots[color] = 1;

        int allocated = colors.Count;
        while (allocated < requestedCustomers)
        {
            FoodColor chosen = colors[0];
            double highest = double.NegativeInfinity;
            bool found = false;
            foreach (FoodColor color in colors)
            {
                int pieces = counts[color];
                int groups = slots[color];
                if (groups >= pieces) continue;
                double score = (double)pieces / groups;
                if (!found || score > highest)
                {
                    highest = score;
                    chosen = color;
                    found = true;
                }
            }
            if (!found)
            {
                message = "Could not assign the requested Customer count.";
                return false;
            }
            slots[chosen]++;
            allocated++;
        }

        var ordersByColor = new Dictionary<FoodColor, Queue<CustomerOrderData>>();
        foreach (FoodColor color in colors)
        {
            int pieces = counts[color];
            int groups = slots[color];
            int perGroup = pieces / groups;
            int remainder = pieces % groups;
            var queue = new Queue<CustomerOrderData>();
            for (int i = 0; i < groups; i++)
                queue.Enqueue(new CustomerOrderData(color, perGroup + (i < remainder ? 1 : 0)));
            ordersByColor[color] = queue;
        }

        // Build all queues before mutating Unity data.
        var newLanes = new List<CustomerLaneData>();
        for (int i = 0; i < lanesCount; i++)
            newLanes.Add(new CustomerLaneData { frontToBack = new List<CustomerOrderData>() });

        // Round-robin by color, then lane. Each lane gets a Front Customer.
        int customerIndex = 0;
        bool hasMore = true;
        while (hasMore)
        {
            hasMore = false;
            foreach (FoodColor color in colors)
            {
                Queue<CustomerOrderData> colorQueue = ordersByColor[color];
                if (colorQueue.Count == 0) continue;
                newLanes[customerIndex % lanesCount].frontToBack.Add(colorQueue.Dequeue());
                customerIndex++;
                hasMore = true;
            }
        }

        Undo.RecordObject(level, "Generate Exact Cooking Blast Customer Count");
        level.CustomerLanes.Clear();
        level.CustomerLanes.AddRange(newLanes);
        Dirty(level);
        message = "Generated " + customerIndex + " Customers in " + lanesCount +
                  " lanes, matching " + total + " Tiles of " + colors.Count + " colors.";
        return true;
    }


    internal static Dictionary<FoodColor, int> TileCounts(CookingBlastLevel level)
    {
        var result = new Dictionary<FoodColor, int>();
        if (level == null) return result;
        foreach (var column in level.BoardColumns)
        {
            if (column == null || column.bottomToTop == null) continue;
            foreach (FoodColor color in column.bottomToTop)
            {
                if (!result.ContainsKey(color)) result[color] = 0;
                result[color]++;
            }
        }
        return result;
    }

    internal static Dictionary<FoodColor, int> DemandCounts(CookingBlastLevel level)
    {
        var result = new Dictionary<FoodColor, int>();
        if (level == null) return result;
        foreach (var lane in level.CustomerLanes)
        {
            if (lane == null || lane.frontToBack == null) continue;
            foreach (var order in lane.frontToBack)
            {
                if (order == null) continue;
                if (!result.ContainsKey(order.color)) result[order.color] = 0;
                result[order.color] += Mathf.Max(0, order.demand);
            }
        }
        return result;
    }

    internal static void Dirty(Component component)
    {
        if (component == null) return;
        EditorUtility.SetDirty(component);
        if (component.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        SceneView.RepaintAll();
    }
}
