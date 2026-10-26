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
    internal static SerializedProperty BoardClusters(SerializedObject so) => so.FindProperty("boardClusters");
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
        RemoveInvalidClusters(level);
        Dirty(level);
    }

    internal static void SetCell(CookingBlastLevel level, int column, int row, FoodColor color)
    {
        if (level == null) return;
        if (level.IsClusterCell(column, row, out _)) return;
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

    internal static bool NormalizeCluster40Sizes(CookingBlastLevel level)
    {
        if (level == null || level.BoardClusters == null) return false;
        bool needsMigration = false;
        for (int i = 0; i < level.BoardClusters.Count; i++)
        {
            BoardClusterData cluster = level.BoardClusters[i];
            if (cluster != null &&
                (cluster.value != BoardClusterData.DefaultValue ||
                 cluster.width != BoardClusterData.DefaultWidth ||
                 cluster.height != BoardClusterData.DefaultHeight))
            {
                needsMigration = true;
                break;
            }
        }
        if (!needsMigration) return false;

        Undo.RecordObject(level, "Migrate Cooking Blast Cluster 40 to 2x2");
        for (int i = 0; i < level.BoardClusters.Count; i++)
        {
            BoardClusterData cluster = level.BoardClusters[i];
            if (cluster == null) continue;
            cluster.value = BoardClusterData.DefaultValue;
            cluster.width = BoardClusterData.DefaultWidth;
            cluster.height = BoardClusterData.DefaultHeight;
        }
        Dirty(level);
        return true;
    }

    internal static bool AddCluster40(CookingBlastLevel level, int anchorColumn,
        int anchorRow, FoodColor color, out string message)
    {
        message = string.Empty;
        if (level == null) return false;
        const int width = BoardClusterData.DefaultWidth;
        const int height = BoardClusterData.DefaultHeight;
        if (anchorColumn < 0 || anchorColumn + width > level.BoardColumns.Count)
        {
            message = "Cluster 40 needs two columns and does not fit here.";
            return false;
        }
        for (int column = anchorColumn; column < anchorColumn + width; column++)
        {
            BoardColumnData data = level.BoardColumns[column];
            if (data == null || data.bottomToTop == null ||
                anchorRow < 0 || anchorRow + height > data.bottomToTop.Count)
            {
                message = "Cluster 40 needs the clicked row and the row above it.";
                return false;
            }
            for (int row = anchorRow; row < anchorRow + height; row++)
                if (level.IsClusterCell(column, row, out _))
                {
                    message = "Cluster 40 overlaps an existing cluster.";
                    return false;
                }
        }

        Undo.RecordObject(level, "Place Cooking Blast Cluster 40");
        level.BoardClusters.Add(new BoardClusterData
        {
            color = color,
            value = BoardClusterData.DefaultValue,
            anchorColumn = anchorColumn,
            anchorRow = anchorRow,
            width = width,
            height = height
        });
        for (int column = anchorColumn; column < anchorColumn + width; column++)
            for (int row = anchorRow; row < anchorRow + height; row++)
                level.BoardColumns[column].bottomToTop[row] = color;
        Dirty(level);
        message = "Placed a 2 x 2 Cluster 40 using " + color + ".";
        return true;
    }

    internal static bool RemoveClusterAt(CookingBlastLevel level, int column, int row)
    {
        if (level == null || level.BoardClusters == null) return false;
        for (int i = level.BoardClusters.Count - 1; i >= 0; i--)
        {
            BoardClusterData cluster = level.BoardClusters[i];
            if (cluster == null || !cluster.Contains(column, row)) continue;
            Undo.RecordObject(level, "Remove Cooking Blast Cluster");
            level.BoardClusters.RemoveAt(i);
            Dirty(level);
            return true;
        }
        return false;
    }

    private static void RemoveInvalidClusters(CookingBlastLevel level)
    {
        if (level == null || level.BoardClusters == null) return;
        for (int i = level.BoardClusters.Count - 1; i >= 0; i--)
        {
            BoardClusterData cluster = level.BoardClusters[i];
            bool valid = cluster != null && cluster.anchorRow >= 0 &&
                         cluster.anchorColumn >= 0 &&
                         cluster.anchorColumn + cluster.width <= level.BoardColumns.Count;
            if (valid)
                for (int column = cluster.anchorColumn;
                     column < cluster.anchorColumn + cluster.width; column++)
                    valid &= level.BoardColumns[column] != null &&
                             level.BoardColumns[column].bottomToTop != null &&
                             cluster.anchorRow + cluster.height <=
                             level.BoardColumns[column].bottomToTop.Count;
            if (!valid) level.BoardClusters.RemoveAt(i);
        }
    }

    private static bool RejectAutoFillWithClusters(CookingBlastLevel level, out string message)
    {
        if (level != null && level.BoardClusters != null && level.BoardClusters.Count > 0)
        {
            message = "Remove Cluster 40 before Auto Fill, then place it again after filling the regular Tiles.";
            return true;
        }
        message = string.Empty;
        return false;
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
        if (RejectAutoFillWithClusters(level, out message)) return false;
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
        bool vertical, out string message)
    {
        message = "Open a level prefab first.";
        if (level == null || level.BoardColumns == null || level.CustomerLanes == null)
            return false;
        if (RejectAutoFillWithClusters(level, out message)) return false;

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
                if (customer == null)
                {
                    message = "Lane " + (lane + 1) + " has an invalid Customer.";
                    return false;
                }
                required += level.GetCustomerDemand(customer.color);
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
            remaining[lane] = level.GetCustomerDemand(orders[lane][0].color);

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
                    remaining[lane] = level.GetCustomerDemand(
                        orders[lane][orderIndex[lane]].color);
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
                        remaining[lane] = level.GetCustomerDemand(
                            orders[lane][orderIndex[lane]].color);
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

        // Place the Animal-order color array either by columns (vertical) or
        // by rows (horizontal). In both modes, index 0 starts at the bottom.
        Undo.RecordObject(level, "Fill Tile Colors in Customer Queue Order");
        int tileIndex = 0;
        int maxRows = MaxRows(level);
        if (vertical)
        {
            for (int col = 0; col < level.BoardColumns.Count; col++)
            {
                BoardColumnData column = level.BoardColumns[col];
                if (column == null || column.bottomToTop == null) continue;
                for (int row = 0; row < column.bottomToTop.Count; row++)
                    column.bottomToTop[row] = result[tileIndex++];
            }
        }
        else
        {
            for (int row = 0; row < maxRows; row++)
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
                  " Tiles " + (vertical ? "vertically" : "horizontally") +
                  " following Animal Front-to-Back order, with chunks of " +
                  clusterSize + " (balanced by color).";
        return true;
    }

internal static bool TryAutoFillByPriority(CookingBlastLevel level,
        IReadOnlyList<Vector2Int> priority, int tilesPerTurn, bool interleave,
        out string message, bool vertical = false, int seed = 0,
        int patchShape = 0)
    {
        message = "Open a valid Cooking Blast level.";
        if (level == null || priority == null ||
            level.BoardColumns == null || level.CustomerLanes == null)
            return false;
        if (RejectAutoFillWithClusters(level, out message)) return false;

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
            if (customer == null)
            {
                message = "A Customer has invalid Demand.";
                return false;
            }
            all.Add(customer);
            totalDemand += level.GetCustomerDemand(customer.color);
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
            remaining[i] = level.GetCustomerDemand(all[i].color);

        tilesPerTurn = Mathf.Clamp(tilesPerTurn, 1, 32);
        if (!interleave)
        {
            for (int i = 0; i < all.Count; i++)
                while (remaining[i]-- > 0)
                    result.Add(all[i].color);
        }
        else
        {
            var random = new System.Random(seed);
            int variation = Mathf.RoundToInt(
                tilesPerTurn * Mathf.Clamp01(patchShape / 100f));
            int pending = totalDemand;
            while (pending > 0)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    int desiredChunk = variation > 0
                        ? tilesPerTurn + random.Next(-variation, variation + 1)
                        : tilesPerTurn;
                    int chunk = Mathf.Min(
                        Mathf.Max(1, desiredChunk), remaining[i]);
                    for (int j = 0; j < chunk; j++)
                        result.Add(all[i].color);
                    remaining[i] -= chunk;
                    pending -= chunk;
                }
            }
        }

        // Assign the same legacy priority array either across rows or up columns.
        // No Customer Queue data is modified.
        Undo.RecordObject(level, "Auto Fill Tiles in Custom Customer Priority");
        int index = 0;
        int maxRows = MaxRows(level);
        if (vertical)
        {
            for (int col = 0; col < level.BoardColumns.Count; col++)
            {
                BoardColumnData column = level.BoardColumns[col];
                if (column == null || column.bottomToTop == null) continue;
                for (int row = 0; row < column.bottomToTop.Count; row++)
                    column.bottomToTop[row] = result[index++];
            }
        }
        else
        {
            for (int row = 0; row < maxRows; row++)
            for (int col = 0; col < level.BoardColumns.Count; col++)
            {
                BoardColumnData column = level.BoardColumns[col];
                if (column == null || column.bottomToTop == null ||
                    row >= column.bottomToTop.Count) continue;
                column.bottomToTop[row] = result[index++];
            }
        }
        Dirty(level);
        message = "Filled " + index + " Tiles " +
                  (vertical ? "vertically" : "horizontally") +
                  " in Animal order. " +
                  (interleave ? "Grouped in batches of " + tilesPerTurn + "." : "Entire Customer demands filled in sequence.");
        return true;
    }

internal static bool TryAutoFillByAnimalOrder(
        CookingBlastLevel level, int tilesPerTurn, bool vertical, int seed,
        int patchShape,
        out string message)
    {
        List<Vector2Int> priority = BuildAnimalOrderPriority(level);
        return TryAutoFillByPriority(
            level, priority, tilesPerTurn, true, out message, vertical,
            seed, patchShape);
    }

private static List<Vector2Int> BuildAnimalOrderPriority(CookingBlastLevel level)
    {
        var priority = new List<Vector2Int>();
        if (level == null || level.CustomerLanes == null) return priority;

        int maxDepth = 0;
        foreach (CustomerLaneData lane in level.CustomerLanes)
            if (lane != null && lane.frontToBack != null)
                maxDepth = Mathf.Max(maxDepth, lane.frontToBack.Count);

        for (int row = 0; row < maxDepth; row++)
        for (int lane = 0; lane < level.CustomerLanes.Count; lane++)
        {
            CustomerLaneData laneData = level.CustomerLanes[lane];
            if (laneData == null || laneData.frontToBack == null ||
                row >= laneData.frontToBack.Count) continue;
            priority.Add(new Vector2Int(lane, row));
        }
        return priority;
    }

internal static bool TryAutoFillOrganicByCustomerOrder(
        CookingBlastLevel level, int tilesPerTurn, int seed,
        int organicStrength, out string message)
    {
        if (level == null || level.CustomerLanes == null)
        {
            message = "Open a valid Cooking Blast level.";
            return false;
        }

        List<Vector2Int> priority = BuildAnimalOrderPriority(level);

        return TryAutoFillOrganicByPriority(
            level, priority, tilesPerTurn, true, seed,
            organicStrength, out message);
    }

internal static bool TryAutoFillOrganicByPriority(
        CookingBlastLevel level, IReadOnlyList<Vector2Int> priority, int tilesPerTurn,
        bool interleave, int seed, int organicStrength, out string message)
    {
        // Use the existing exact-count / Customer order validation. Organic
        // generation below overrides its temporary row-wise layout.
        if (!TryAutoFillByPriority(level, priority, tilesPerTurn, interleave, out message))
            return false;

        var cells = new List<Vector2Int>();
        var lookup = new Dictionary<Vector2Int, int>();
        int maxRows = MaxRows(level);
        for (int row = 0; row < maxRows; row++)
        for (int col = 0; col < level.BoardColumns.Count; col++)
        {
            BoardColumnData boardColumn = level.BoardColumns[col];
            if (boardColumn == null || boardColumn.bottomToTop == null ||
                row >= boardColumn.bottomToTop.Count) continue;
            Vector2Int position = new Vector2Int(col, row);
            lookup[position] = cells.Count;
            cells.Add(position);
        }

        if (cells.Count == 0)
        {
            message = "No Tiles available for Organic Fill.";
            return false;
        }

        // Group the exact Customer demands by color. Customer order determines
        // seed priority only; it must NOT force placement in horizontal rows.
        var colors = new List<FoodColor>();
        var quantities = new List<int>();
        var colorIndices = new Dictionary<FoodColor, int>();
        foreach (Vector2Int id in priority)
        {
            CustomerOrderData order = level.CustomerLanes[id.x].frontToBack[id.y];
            if (!colorIndices.TryGetValue(order.color, out int colorIndex))
            {
                colorIndex = colors.Count;
                colorIndices.Add(order.color, colorIndex);
                colors.Add(order.color);
                quantities.Add(0);
            }
            quantities[colorIndex] += level.GetCustomerDemand(order.color);
        }

        var random = new System.Random(seed);
        organicStrength = Mathf.Clamp(organicStrength, 0, 100);

        var owners = new int[cells.Count];
        for (int i = 0; i < owners.Length; i++) owners[i] = -1;

        var neighbors = new int[cells.Count, CardinalDirections.Length];
        for (int i = 0; i < cells.Count; i++)
        for (int d = 0; d < CardinalDirections.Length; d++)
            neighbors[i, d] = lookup.TryGetValue(
                cells[i] + CardinalDirections[d], out int neighbor) ? neighbor : -1;

        var remaining = quantities.ToArray();
        var frontiers = new HashSet<int>[colors.Count];
        var seedCounts = new int[colors.Count];
        var seedsByColor = new List<int>[colors.Count];
        for (int i = 0; i < colors.Count; i++)
        {
            frontiers[i] = new HashSet<int>();
            seedsByColor[i] = new List<int>();
        }
        var placedSeeds = new List<int>();

        // First selected Customer should have a playable Tile at bottom-left.
        PlaceOrganicCell(0, 0, owners, remaining, frontiers, neighbors);
        placedSeeds.Add(0);
        seedsByColor[0].Add(0);
        seedCounts[0]++;

        // Expose the other FRONT Customers along the bottom edge if space
        // permits, before allowing the clusters to grow independently.
        var frontColors = new HashSet<FoodColor>();
        foreach (CustomerLaneData lane in level.CustomerLanes)
        {
            if (lane == null || lane.frontToBack == null ||
                lane.frontToBack.Count == 0 || lane.frontToBack[0] == null)
                continue;
            frontColors.Add(lane.frontToBack[0].color);
        }
        foreach (FoodColor frontColor in frontColors)
        {
            if (!colorIndices.TryGetValue(frontColor, out int index) ||
                seedCounts[index] > 0 || remaining[index] == 0) continue;
            int candidate = SelectOrganicSeed(cells, owners, placedSeeds, random, true);
            if (candidate < 0) continue;
            PlaceOrganicCell(candidate, index, owners, remaining, frontiers, neighbors);
            placedSeeds.Add(candidate);
            seedsByColor[index].Add(candidate);
            seedCounts[index]++;
        }

        // Seed ALL other colors across the full board, not on the first
        // available row. Farthest-point sampling keeps patches dispersed.
        for (int color = 0; color < colors.Count; color++)
        {
            if (seedCounts[color] != 0 || remaining[color] == 0) continue;
            int candidate = SelectOrganicSeed(cells, owners, placedSeeds, random, false);
            if (candidate < 0) continue;
            PlaceOrganicCell(candidate, color, owners, remaining, frontiers, neighbors);
            placedSeeds.Add(candidate);
            seedCounts[color]++;
        }

        // A larger color population needs several independent island seeds.
        // This creates recognizable, irregular regions rather than stripes.
        int patchTarget = Mathf.Max(
            Mathf.RoundToInt(Mathf.Lerp(35f, 17f, organicStrength / 100f)),
            Mathf.CeilToInt(cells.Count / 70f)); // Cap islands for large Boards.
        var seedTargets = new int[colors.Count];
        for (int color = 0; color < colors.Count; color++)
            seedTargets[color] = Mathf.Min(quantities[color],
                Mathf.Max(1, Mathf.CeilToInt(quantities[color] / (float)patchTarget)));

        bool anyMore = true;
        while (anyMore)
        {
            anyMore = false;
            for (int color = 0; color < colors.Count; color++)
            {
                if (seedCounts[color] >= seedTargets[color] || remaining[color] == 0)
                    continue;
                int candidate = SelectOrganicSeed(cells, owners, placedSeeds, random, false);
                if (candidate < 0) continue;
                PlaceOrganicCell(candidate, color, owners, remaining, frontiers, neighbors);
                placedSeeds.Add(candidate);
            seedsByColor[color].Add(candidate);
            seedCounts[color]++;
                anyMore = true;
            }
        }

        int filled = 0;
        for (int i = 0; i < owners.Length; i++)
            if (owners[i] != -1) filled++;

        // Grow every color by one Tile per round. Frontiers favor adjacent
        // same-color Tiles, while seeded jitter creates non-rectangular edges.
        // If an island gets boxed in, create another seed in unused space.
        while (filled < cells.Count)
        {
            bool progress = false;
            int startColor = random.Next(colors.Count);
            for (int offset = 0; offset < colors.Count; offset++)
            {
                int color = (startColor + offset) % colors.Count;
                if (remaining[color] == 0) continue;
                int next = SelectOrganicFrontier(
                    color, owners, frontiers[color], neighbors, random, organicStrength,
                    cells, seedsByColor[color]);

                if (next < 0)
                {
                    next = SelectOrganicSeed(cells, owners, placedSeeds, random, false);
                    if (next < 0) continue;
                    placedSeeds.Add(next);
                    seedsByColor[color].Add(next);
                    seedCounts[color]++;
                }
                PlaceOrganicCell(next, color, owners, remaining, frontiers, neighbors);
                filled++;
                progress = true;
            }
            if (!progress)
            {
                message = "Could not grow all Organic Tile regions.";
                return false;
            }
        }

        var assigned = new FoodColor[cells.Count];
        for (int i = 0; i < cells.Count; i++)
            assigned[i] = colors[owners[i]];

        // Normally all Front colors are already seeded in the bottom row.
        // For a narrow Board, this best-effort fallback keeps them accessible.
        int exposed = ExposeFrontCustomerColors(level, cells, assigned);
        Undo.RecordObject(level, "Generate Cooking Blast Color Patches");
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            level.BoardColumns[cell.x].bottomToTop[cell.y] = assigned[i];
        }
        Dirty(level);
        message = "Generated " + cells.Count + " Tiles as " +
                  placedSeeds.Count + " irregular color patches (Seed " + seed +
                  "). Exact Customer Demand preserved; " + exposed +
                  " additional Front colors exposed.";
        return true;
    }

private static void PlaceOrganicCell(int cell, int color,
        int[] owners, int[] remaining, HashSet<int>[] frontiers, int[,] neighbors)
    {
        owners[cell] = color;
        remaining[color]--;
        for (int c = 0; c < frontiers.Length; c++)
            frontiers[c].Remove(cell);

        for (int d = 0; d < neighbors.GetLength(1); d++)
        {
            int adjacent = neighbors[cell, d];
            if (adjacent >= 0 && owners[adjacent] == -1)
                frontiers[color].Add(adjacent);
        }
    }

private static int SelectOrganicSeed(List<Vector2Int> cells, int[] owners,
        List<int> seeds, System.Random random, bool bottomOnly)
    {
        int best = -1;
        double bestScore = double.NegativeInfinity;
        for (int cell = 0; cell < cells.Count; cell++)
        {
            if (owners[cell] >= 0 || (bottomOnly && cells[cell].y != 0))
                continue;

            float distance = float.PositiveInfinity;
            for (int s = 0; s < seeds.Count; s++)
            {
                Vector2Int difference = cells[cell] - cells[seeds[s]];
                distance = Mathf.Min(distance, difference.sqrMagnitude);
            }
            if (float.IsPositiveInfinity(distance)) distance = 0f;
            // The random tie-breaker makes layouts reproducible from the Seed
            // without sacrificing the spatial distribution of patch centers.
            double jitter = random.NextDouble() *
                (4.0 + Mathf.Sqrt(distance) * 0.40);
            double score = distance + jitter;
            if (score <= bestScore) continue;
            bestScore = score;
            best = cell;
        }
        return best;
    }

private static int SelectOrganicFrontier(int color, int[] owners,
        HashSet<int> frontier, int[,] neighbors, System.Random random,
        int organicStrength, List<Vector2Int> cells, List<int> seedsOfColor)
    {
        int best = -1;
        double bestScore = double.NegativeInfinity;
        foreach (int candidate in frontier)
        {
            if (owners[candidate] >= 0) continue;
            int same = 0;
            int different = 0;
            for (int d = 0; d < neighbors.GetLength(1); d++)
            {
                int adjacent = neighbors[candidate, d];
                if (adjacent < 0) continue;
                if (owners[adjacent] == color) same++;
                else if (owners[adjacent] >= 0) different++;
            }
            if (same == 0) continue;

            int nearestSeedDistance = int.MaxValue;
            for (int s = 0; s < seedsOfColor.Count; s++)
            {
                Vector2Int delta = cells[candidate] - cells[seedsOfColor[s]];
                nearestSeedDistance = Mathf.Min(nearestSeedDistance,
                    delta.sqrMagnitude);
            }
            if (nearestSeedDistance == int.MaxValue) nearestSeedDistance = 0;

            // Compact islands, not tall columns: prefer growing around each
            // seed rather than stretching a long branch of the same color.
            // Noise on the contour makes the island edges irregular.
            double score = same * 2.4 - different * 1.0 -
                           nearestSeedDistance * .22 +
                           random.NextDouble() * (1.3 + organicStrength * .055);
            if (score <= bestScore) continue;
            bestScore = score;
            best = candidate;
        }
        return best;
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

        // Use the same logical totals shown by TILES / NEEDED. Covered cells
        // do not count individually; each Cluster 40 contributes 40 units of
        // its selected color.
        var counts = TileCounts(level);

        Undo.RecordObject(level, "Generate Cooking Blast customers");
        var so = new SerializedObject(level);
        var lanes = CustomerLanes(so);
        lanes.arraySize = laneCount;
        for (int l = 0; l < laneCount; l++) lanes.GetArrayElementAtIndex(l).FindPropertyRelative("frontToBack").ClearArray();

        int lane = 0;
        foreach (var pair in counts)
        {
            int remaining = pair.Value;
            int animalDemand = level.GetCustomerDemand(pair.Key);
            while (remaining > 0)
            {
                var list = lanes.GetArrayElementAtIndex(lane % laneCount).FindPropertyRelative("frontToBack");
                int index = list.arraySize++;
                var order = list.GetArrayElementAtIndex(index);
                order.FindPropertyRelative("color").enumValueIndex = (int)pair.Key;
                order.FindPropertyRelative("demand").intValue = animalDemand;
                remaining -= animalDemand;
                lane++;
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
            for (int row = 0; row < column.bottomToTop.Count; row++)
            {
                if (level.IsClusterCell(level.BoardColumns.IndexOf(column), row, out _)) continue;
                FoodColor color = column.bottomToTop[row];
                if (!result.ContainsKey(color)) result[color] = 0;
                result[color]++;
            }
        }
        if (level.BoardClusters != null)
            foreach (BoardClusterData cluster in level.BoardClusters)
            {
                if (cluster == null) continue;
                if (!result.ContainsKey(cluster.color)) result[cluster.color] = 0;
                result[cluster.color] += Mathf.Max(0, cluster.value);
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
                result[order.color] += level.GetCustomerDemand(order.color);
            }
        }
        return result;
    }

    internal static void SyncCustomerDemandsFromAnimals(CookingBlastLevel level)
    {
        if (level == null || level.CustomerLanes == null) return;
        bool changed = false;
        foreach (CustomerLaneData lane in level.CustomerLanes)
        {
            if (lane == null || lane.frontToBack == null) continue;
            foreach (CustomerOrderData order in lane.frontToBack)
            {
                if (order == null) continue;
                int animalDemand = level.GetCustomerDemand(order.color);
                if (order.demand == animalDemand) continue;
                order.demand = animalDemand;
                changed = true;
            }
        }
        if (changed) Dirty(level);
    }

    internal static void Dirty(Component component)
    {
        if (component == null) return;
        EditorUtility.SetDirty(component);
        if (component.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        SceneView.RepaintAll();
    }
}
