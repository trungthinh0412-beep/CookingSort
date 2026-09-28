using System;
using MagicSoft.Differences;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DifferenceEditorVerification
{
    [MenuItem("Tools/Find Differences/Verify Level Editor")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        Undo.IncrementCurrentGroup();
        string path = DifferencePrefabSetup.LevelDirectory + "/__EditorVerification_" + Guid.NewGuid().ToString("N") + ".prefab";
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        GameObject instance = null;
        try
        {
            var asset = DifferenceLevelEditorWindow.CreateEmptyLevel(DifferencePrefabSetup.BasePath, path, 99);
            var draft = asset.GetComponent<DifferenceLevelController>();
            Check(draft.TotalCount == 0 && DifferenceLevelEditing.Panel(draft, true).Picture.sprite == null &&
                DifferenceLevelEditing.Panel(draft, false).Picture.sprite == null, "New level clears template pictures and spots");
            bool rejected = false;
            try { DifferenceLevelEditorWindow.CreateEmptyLevel(DifferencePrefabSetup.LevelPath, path, 99); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Existing prefab is never overwritten");
            instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            var board = instance.GetComponent<DifferenceLevelController>();
            var a = DifferenceLevelEditing.Panel(board, true);
            var b = DifferenceLevelEditing.Panel(board, false);
            var sample = AssetDatabase.LoadAssetAtPath<GameObject>(DifferencePrefabSetup.LevelPath).GetComponent<DifferenceLevelController>();
            DifferenceLevelEditing.AssignSprite(a, DifferenceLevelEditing.Panel(sample, true).Picture.sprite);
            DifferenceLevelEditing.AssignSprite(b, DifferenceLevelEditing.Panel(sample, false).Picture.sprite);
            var initial = new Rect(.15f, .25f, .2f, .3f);
            int index = DifferenceLevelEditing.AddPair(board, initial);
            Check(index == 0 && board.TotalCount == 1 && board.TryValidate(out _), "Draw adds a valid linked pair");
            Near(DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, 0, true), a), initial, "A bounds");
            Near(DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, 0, false), b), initial, "B bounds");
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Check(board.TotalCount == 0 && instance.GetComponentsInChildren<DifferenceSpotView>(true).Length == 0, "Undo add removes both spots and pair");
            Undo.PerformRedo();
            Check(board.TotalCount == 1 && board.TryValidate(out _), "Redo add restores pair references");
            var linked = new Rect(.35f, .45f, .12f, .18f);
            Undo.IncrementCurrentGroup();
            DifferenceLevelEditing.SetPairBounds(board, 0, false, linked, true);
            Undo.FlushUndoRecordObjects();
            Near(DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, 0, true), a), linked,
                "Editing B synchronizes A");
            Near(DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, 0, false), b), linked,
                "Editing B updates B");
            Undo.PerformUndo();
            Near(DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, 0, true), a), initial,
                "Undo synchronized edit restores A");
            Near(DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, 0, false), b), initial,
                "Undo synchronized edit restores B");
            Undo.IncrementCurrentGroup();
            var moved = new Rect(.55f, .5f, .1f, .15f);
            DifferenceLevelEditing.SetBounds(DifferenceLevelEditing.Spot(board, 0, false), b, moved);
            Undo.FlushUndoRecordObjects();
            Near(DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, 0, true), a), initial, "Editing B leaves A unchanged");
            Near(DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, 0, false), b), moved, "B move and resize");
            Undo.PerformUndo();
            Near(DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, 0, false), b), initial, "Undo area edit");
            DifferenceLevelEditing.DeletePair(board, 0);
            Undo.FlushUndoRecordObjects();
            Check(board.TotalCount == 0 && instance.GetComponentsInChildren<DifferenceSpotView>(true).Length == 0, "Delete removes pair and both spots");
            Undo.PerformUndo();
            Check(board.TotalCount == 1 && board.TryValidate(out _), "Undo delete restores pair references");
            Rect image = new Rect(20, 40, 700, 450);
            Rect gui = DifferenceLevelEditing.ToPreview(initial, image);
            var lower = DifferenceLevelEditing.FromPreview(new Vector2(gui.xMin, gui.yMax), image);
            var upper = DifferenceLevelEditing.FromPreview(new Vector2(gui.xMax, gui.yMin), image);
            Near(Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y), initial, "Preview coordinates flip Y correctly");
            var clamped = DifferenceLevelEditing.ClampBounds(new Rect(-.5f, .95f, .3f, .2f));
            Near(clamped, new Rect(0, .8f, .3f, .2f), "Drag clamped within image");
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            var reopened = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var saved = reopened.GetComponent<DifferenceLevelController>();
                Check(saved.TotalCount == 1 && saved.TryValidate(out _), "Saved prefab reloads with all references");
                Near(DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(saved, 0, true), DifferenceLevelEditing.Panel(saved, true)), initial, "Saved area retains proportions");
            }
            finally { PrefabUtility.UnloadPrefabContents(reopened); }
            Debug.Log("[Differences Editor] PASS: empty level, overwrite protection, assign sprites, synchronized/independent areas, Undo/Redo, delete, coordinates and prefab save/reload.");
        }
        finally
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.DeleteAsset(path);
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("[Differences Editor verification] " + message);
    }
    private static void Near(Rect actual, Rect expected, string message)
        => Check(Vector2.Distance(actual.min, expected.min) < .001f && Vector2.Distance(actual.max, expected.max) < .001f, message + $" actual={actual} expected={expected}");
}
