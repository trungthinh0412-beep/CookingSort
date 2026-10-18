using System;
using System.Collections.Generic;
using CookingSort2D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class CookingBlastLevelEditing
{
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
        columns = Mathf.Clamp(columns, 1, 14);
        rows = Mathf.Clamp(rows, 1, 18);
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
