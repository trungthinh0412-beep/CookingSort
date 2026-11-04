using System;
using System.Collections.Generic;
using CookingSort2D;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exact-count footprint placement for the existing Fill Tile button.
/// Does not edit Animal orders; resizes the board only in Special mode.
/// </summary>
internal static class CookingBlastExactSpecialTileGenerator
{
    private static readonly Vector2Int[] Neighbors = {
        Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
    };

    internal static bool Generate(CookingBlastLevel level, int seed,
        int layout, IReadOnlyList<CookingBlastSpecialTileCount> selections,
        out string message)
    {
        message = "Open a valid Cooking Blast level.";
        if (level == null || level.Board == null || level.CustomerQueue == null ||
            level.BoardColumns == null || level.BoardColumns.Count == 0 ||
            level.CustomerLanes == null) return false;

        var demand = CookingBlastLevelEditing.DemandCounts(level);
        demand.Remove(FoodColor.Neutral);
        var remaining = new Dictionary<FoodColor, int>();
        long totalDemand = 0;
        foreach (var pair in demand)
        {
            if (pair.Value <= 0) continue;
            remaining[pair.Key] = pair.Value;
            totalDemand += pair.Value;
        }
        if (totalDemand <= 0)
        {
            message = "Generate Animals first. There is no Animal Demand to fill.";
            return false;
        }

        var allowed = new HashSet<FoodTileView>();
        var regular = new HashSet<FoodColor>();
        for (int i = 0; i < level.Board.ConfiguredTilePrefabCount; i++)
        {
            FoodTileView prefab = level.Board.GetConfiguredTilePrefab(i);
            if (prefab == null) continue;
            allowed.Add(prefab);
            if (prefab.FootprintWidth == 1 && prefab.FootprintHeight == 1 &&
                prefab.FootprintValue == 1)
                regular.Add(prefab.Type);
        }

        var pieces = new List<FoodTileView>();
        var counts = new Dictionary<FoodTileView, int>();
        long totalFootprintValue = 0, totalFootprintArea = 0;
        int largestW = 1, largestH = 1;
        if (selections != null)
        {
            foreach (CookingBlastSpecialTileCount selection in selections)
            {
                if (selection == null || selection.quantity <= 0) continue;
                FoodTileView prefab = selection.prefab;
                if (prefab == null || !allowed.Contains(prefab) ||
                    (prefab.FootprintWidth == 1 && prefab.FootprintHeight == 1) ||
                    prefab.FootprintValue < 1)
                {
                    message = "One selected special Tile prefab is invalid or missing from Board.";
                    return false;
                }
                if (!remaining.ContainsKey(prefab.Type))
                {
                    message = prefab.name + " (" + prefab.Type + ") has no Animal demand.";
                    return false;
                }
                if (counts.ContainsKey(prefab))
                {
                    message = "Duplicate special Tile prefab entry: " + prefab.name;
                    return false;
                }
                counts[prefab] = selection.quantity;
                long value = (long)selection.quantity * prefab.FootprintValue;
                long area = (long)selection.quantity *
                    prefab.FootprintWidth * prefab.FootprintHeight;
                if (value > remaining[prefab.Type])
                {
                    message = prefab.Type + ": the selected quantity of " + prefab.name +
                        " needs " + value + " Value but Animal Demand is only " +
                        remaining[prefab.Type] + ".";
                    return false;
                }
                remaining[prefab.Type] -= (int)value;
                totalFootprintValue += value;
                totalFootprintArea += area;
                largestW = Mathf.Max(largestW, prefab.FootprintWidth);
                largestH = Mathf.Max(largestH, prefab.FootprintHeight);
                for (int n = 0; n < selection.quantity; n++) pieces.Add(prefab);
            }
        }

        foreach (var pair in remaining)
        {
            if (pair.Value <= 0) continue;
            if (regular.Contains(pair.Key)) continue;
            message = pair.Key + " still needs " + pair.Value +
                " Value but has no 1x1 Tile prefab. Increase the quantities of its " +
                "special Tiles until their total Footprint Value exactly matches demand.";
            return false;
        }

        long cellsRequired = totalDemand - totalFootprintValue + totalFootprintArea;
        if (cellsRequired <= 0 || cellsRequired >
            (long)CookingBlastPixelGridDetector.MaxCellsX *
            CookingBlastPixelGridDetector.MaxCellsY)
        {
            message = "Required Board area " + cellsRequired +
                " is outside the supported grid limits.";
            return false;
        }

        int maxWidth = CookingBlastPixelGridDetector.MaxCellsX;
        int maxHeight = CookingBlastPixelGridDetector.MaxCellsY;
        int columns = Mathf.Max(largestW, level.BoardColumns.Count);
        int rows = Mathf.Max(largestH,
            Mathf.CeilToInt((float)cellsRequired / columns));
        while (rows > maxHeight && columns < maxWidth)
        {
            columns++;
            rows = Mathf.Max(largestH,
                Mathf.CeilToInt((float)cellsRequired / columns));
        }
        if (columns > maxWidth || rows > maxHeight)
        {
            message = "The selected Tile counts need more rows/columns than " +
                "the Board supports (" + maxWidth + "x" + maxHeight + ").";
            return false;
        }

        var random = new System.Random(seed);
        // Larger shapes go first. Equal-size pieces are shuffled so the
        // same set of counts can produce different layouts per Seed.
        Shuffle(pieces, random);
        pieces.Sort((a, b) =>
            (b.FootprintWidth * b.FootprintHeight)
                .CompareTo(a.FootprintWidth * a.FootprintHeight));

        var clusters = new List<BoardClusterData>();
        var occupied = new HashSet<Vector2Int>();
        bool packed = false;
        int finalRows = rows;
        // A few extra Neutral cells may be needed to pack irregular shapes.
        for (int extraRows = 0; extraRows <= 12 && !packed; extraRows++)
        {
            if (rows + extraRows > maxHeight) break;
            finalRows = rows + extraRows;
            for (int attempt = 0; attempt < 64 && !packed; attempt++)
            {
                occupied.Clear();
                clusters.Clear();
                packed = TryPack(pieces, columns, finalRows, random,
                    occupied, clusters);
            }
        }
        if (!packed)
        {
            message = "Unable to fit the exact requested special Tile quantities. " +
                "Reduce counts or Board Columns, or choose a different Seed.";
            return false;
        }

        var byCell = new Dictionary<Vector2Int, FoodColor>();
        var free = new List<Vector2Int>();
        for (int c = 0; c < columns; c++)
        for (int r = 0; r < finalRows; r++)
        {
            var cell = new Vector2Int(c, r);
            byCell[cell] = FoodColor.Neutral;
            if (!occupied.Contains(cell)) free.Add(cell);
        }
        foreach (BoardClusterData cluster in clusters)
        for (int c = cluster.anchorColumn; c < cluster.anchorColumn + cluster.width; c++)
        for (int r = cluster.anchorRow; r < cluster.anchorRow + cluster.height; r++)
            byCell[new Vector2Int(c, r)] = cluster.color;

        int normalRemaining = 0;
        foreach (var pair in remaining) normalRemaining += pair.Value;
        if (normalRemaining > free.Count)
        {
            message = "Insufficient space for remaining 1x1 Tile demand.";
            return false;
        }

        // Color Patches = organic growth around matching neighbors/footprints;
        // Vertical and Horizontal keep the old user-selectable layout intent.
        if (layout == 1)
            free.Sort((a, b) => a.x == b.x ?
                a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        else if (layout == 2)
            free.Sort((a, b) => a.y == b.y ?
                a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        else Shuffle(free, random);

        int cursor = 0;
        while (normalRemaining > 0)
        {
            FoodColor color = FoodColor.Neutral;
            int bestDemand = -1;
            foreach (var pair in remaining)
            {
                if (pair.Value <= 0) continue;
                if (pair.Value > bestDemand ||
                    (pair.Value == bestDemand && random.Next(2) == 0))
                {
                    color = pair.Key;
                    bestDemand = pair.Value;
                }
            }
            if (bestDemand <= 0) break;

            int index = cursor;
            if (layout == 0)
            {
                int best = int.MinValue;
                for (int i = 0; i < free.Count; i++)
                {
                    if (byCell[free[i]] != FoodColor.Neutral) continue;
                    int score = random.Next(11);
                    foreach (Vector2Int direction in Neighbors)
                        if (byCell.TryGetValue(free[i] + direction,
                            out FoodColor neighbor) && neighbor == color)
                            score += 25;
                    if (score <= best) continue;
                    best = score;
                    index = i;
                }
            }
            else
            {
                while (index < free.Count &&
                    byCell[free[index]] != FoodColor.Neutral) index++;
            }
            if (index >= free.Count)
            {
                message = "Not enough empty cells for 1x1 Tiles.";
                return false;
            }
            byCell[free[index]] = color;
            if (layout != 0) cursor = index + 1;
            remaining[color]--;
            normalRemaining--;
        }

        // Until this point, all work lives in temporary data. One Undo
        // operation commits new uniform Board dimensions and exact clusters.
        var priorColumns = new List<BoardColumnData>(level.BoardColumns);
        var priorClusters = new List<BoardClusterData>(level.BoardClusters);
        var newColumns = new List<BoardColumnData>();
        for (int c = 0; c < columns; c++)
        {
            var data = new BoardColumnData();
            for (int r = 0; r < finalRows; r++)
                data.bottomToTop.Add(byCell[new Vector2Int(c, r)]);
            newColumns.Add(data);
        }

        Undo.RecordObject(level, "Fill Special Tiles & Resize Board");
        level.BoardColumns.Clear();
        level.BoardColumns.AddRange(newColumns);
        level.BoardClusters.Clear();
        level.BoardClusters.AddRange(clusters);

        if (!level.TryValidate(out string validation))
        {
            level.BoardColumns.Clear();
            level.BoardColumns.AddRange(priorColumns);
            level.BoardClusters.Clear();
            level.BoardClusters.AddRange(priorClusters);
            message = "No changes applied: " + validation;
            return false;
        }
        CookingBlastLevelEditing.Dirty(level);
        CookingBlastLevelEditorWindow.RefreshBoardSizeFromImport(level);
        message = "Generated exactly " + pieces.Count + " special Tiles (" +
            clusters.Count + " footprint prefabs), Board " + columns + "x" +
            finalRows + ". Total Tile Value " + level.TotalTileCount +
            " matches Animal Demand " + totalDemand +
            ". Animal Queue unchanged. Ctrl+Z to undo.";
        return true;
    }

    private static bool TryPack(IReadOnlyList<FoodTileView> pieces, int columns,
        int rows, System.Random random, HashSet<Vector2Int> occupied,
        List<BoardClusterData> clusters)
    {
        var anchors = new List<Vector2Int>();
        for (int c = 0; c < columns; c++)
        for (int r = 0; r < rows; r++)
            anchors.Add(new Vector2Int(c, r));

        foreach (FoodTileView prefab in pieces)
        {
            Shuffle(anchors, random);
            Vector2Int chosen = default;
            bool found = false;
            int w = prefab.FootprintWidth;
            int h = prefab.FootprintHeight;
            foreach (Vector2Int anchor in anchors)
            {
                if (anchor.x + w > columns || anchor.y + h > rows)
                    continue;
                bool free = true;
                for (int c = anchor.x; c < anchor.x + w && free; c++)
                for (int r = anchor.y; r < anchor.y + h; r++)
                    if (occupied.Contains(new Vector2Int(c, r)))
                    {
                        free = false;
                        break;
                    }
                if (!free) continue;
                chosen = anchor;
                found = true;
                break;
            }
            if (!found) return false;
            clusters.Add(new BoardClusterData
            {
                color = prefab.Type, tilePrefab = prefab,
                anchorColumn = chosen.x, anchorRow = chosen.y,
                width = w, height = h, value = prefab.FootprintValue
            });
            for (int c = chosen.x; c < chosen.x + w; c++)
            for (int r = chosen.y; r < chosen.y + h; r++)
                occupied.Add(new Vector2Int(c, r));
        }
        return true;
    }

    private static void Shuffle<T>(List<T> data, System.Random random)
    {
        for (int i = data.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            T temp = data[i];
            data[i] = data[j];
            data[j] = temp;
        }
    }
}
