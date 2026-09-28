using System;
using MagicSoft.Differences;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Shared authoring operations. These edit real prefab objects, never a second level-data format.
internal static class DifferenceLevelEditing
{
    private const string SyncAreasKey = "FindDifferences.SyncMatchingAreas";
    internal static bool SyncMatchingAreas
    {
        get => EditorPrefs.GetBool(SyncAreasKey, true);
        set => EditorPrefs.SetBool(SyncAreasKey, value);
    }

    internal static DifferencePanelView Panel(DifferenceLevelController board, bool sideA)
        => new SerializedObject(board).FindProperty(sideA ? "panelA" : "panelB")
            .objectReferenceValue as DifferencePanelView;

    internal static DifferenceSpotView Spot(DifferenceLevelController board, int index, bool sideA)
    {
        var pairs = new SerializedObject(board).FindProperty("differences");
        if (index < 0 || index >= pairs.arraySize) return null;
        return pairs.GetArrayElementAtIndex(index).FindPropertyRelative(sideA ? "spotA" : "spotB")
            .objectReferenceValue as DifferenceSpotView;
    }

    internal static int FindPairIndex(DifferenceLevelController board, DifferenceSpotView spot)
    {
        if (board == null || spot == null) return -1;
        for (int i = 0; i < board.TotalCount; i++)
            if (Spot(board, i, true) == spot || Spot(board, i, false) == spot) return i;
        return -1;
    }

    internal static int AddPair(DifferenceLevelController board, Rect bounds)
    {
        var a = Panel(board, true);
        var b = Panel(board, false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DifferencePrefabSetup.SpotPath);
        if (a == null || b == null || a.Picture == null || b.Picture == null || prefab == null)
            throw new InvalidOperationException("Assign both pictures and the DifferenceSpot prefab first.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add difference pair");
        var data = new SerializedObject(board);
        var pairs = data.FindProperty("differences");
        int index = pairs.arraySize++;
        var pair = pairs.GetArrayElementAtIndex(index);
        pair.FindPropertyRelative("id").stringValue = "difference_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        foreach (bool sideA in new[] { true, false })
        {
            var panel = sideA ? a : b;
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel.Picture.transform);
            Undo.RegisterCreatedObjectUndo(obj, "Add difference spot");
            obj.name = $"Spot_{index + 1:00}";
            var spot = obj.GetComponent<DifferenceSpotView>();
            SetBounds(spot, panel, bounds);
            pair.FindPropertyRelative(sideA ? "spotA" : "spotB").objectReferenceValue = spot;
        }
        data.ApplyModifiedProperties();
        Undo.CollapseUndoOperations(group);
        Dirty(board);
        return index;
    }

    internal static void DeletePair(DifferenceLevelController board, int index)
    {
        var a = Spot(board, index, true);
        var b = Spot(board, index, false);
        var data = new SerializedObject(board);
        var pairs = data.FindProperty("differences");
        if (index < 0 || index >= pairs.arraySize) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Delete difference pair");
        pairs.DeleteArrayElementAtIndex(index);
        data.ApplyModifiedProperties();
        if (a != null) Undo.DestroyObjectImmediate(a.gameObject);
        if (b != null && b != a) Undo.DestroyObjectImmediate(b.gameObject);
        Undo.CollapseUndoOperations(group);
        Dirty(board);
    }

    internal static void AssignSprite(DifferencePanelView panel, Sprite sprite)
    {
        if (panel == null || panel.Picture == null) return;
        Undo.RecordObject(panel.Picture, "Change difference picture");
        panel.Picture.sprite = sprite;
        PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Picture);
        panel.RefreshGeometry();
        foreach (var spot in panel.GetComponentsInChildren<DifferenceSpotView>(true)) spot.RefreshGeometry();
        Dirty(panel);
    }

    internal static Rect Bounds(DifferenceSpotView spot, DifferencePanelView panel)
        => spot != null ? spot.NormalizedBounds : default;

    internal static void SetBounds(DifferenceSpotView spot, DifferencePanelView panel, Rect bounds)
    {
        if (spot == null || panel == null) return;
        var data = new SerializedObject(spot);
        data.FindProperty("picture").objectReferenceValue = panel.Picture;
        data.FindProperty("normalizedBounds").rectValue = ClampBounds(bounds);
        data.ApplyModifiedProperties();
        spot.RefreshGeometry();
        PrefabUtility.RecordPrefabInstancePropertyModifications(spot);
        Dirty(spot);
    }

    // Apply one normalized rectangle to the matching pair when linked editing is enabled.
    // Each panel still keeps its own serialized region so either side can be adjusted later.
    internal static void SetPairBounds(DifferenceLevelController board, int index, bool sideA, Rect bounds,
        bool syncMatchingArea)
    {
        var source = Spot(board, index, sideA);
        var sourcePanel = Panel(board, sideA);
        SetBounds(source, sourcePanel, bounds);
        if (syncMatchingArea)
            SetBounds(Spot(board, index, !sideA), Panel(board, !sideA), bounds);
    }

    internal static Rect ClampBounds(Rect rect)
    {
        float w = Mathf.Clamp(rect.width, .01f, 1), h = Mathf.Clamp(rect.height, .01f, 1);
        return new Rect(Mathf.Clamp(rect.x, 0, 1 - w), Mathf.Clamp(rect.y, 0, 1 - h), w, h);
    }

    internal static Rect ToPreview(Rect bounds, Rect image)
        => new Rect(image.x + bounds.x * image.width, image.y + (1 - bounds.yMax) * image.height,
            bounds.width * image.width, bounds.height * image.height);

    internal static Vector2 FromPreview(Vector2 point, Rect image)
        => new Vector2(Mathf.Clamp01((point.x - image.x) / image.width),
            1 - Mathf.Clamp01((point.y - image.y) / image.height));

    internal static void Dirty(Component component)
    {
        EditorUtility.SetDirty(component);
        if (component.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        SceneView.RepaintAll();
    }
}
