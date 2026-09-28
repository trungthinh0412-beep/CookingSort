using System;
using System.IO;
using MagicSoft.Differences;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class DifferenceLevelEditorWindow : EditorWindow
{
    private enum DragMode { None, Draw, Move, Resize }
    [SerializeField] private GameObject levelAsset;
    [SerializeField] private int newLevelNumber = 2;
    [SerializeField] private bool drawMode;
    private DifferenceLevelController board;
    private int selected = -1;
    private Vector2 scroll;
    private string status;
    private MessageType statusType;
    private DragMode dragMode;
    private bool dragSideA;
    private Vector2 dragStart;
    private Rect originalBounds;
    private Rect drawnBounds;
    private int undoGroup;

    [MenuItem("Tools/Level Editor/Find Differences Editor")]
    [MenuItem("Tools/Find Differences/Level Editor")]
    public static void OpenWindow()
    {
        var window = GetWindow<DifferenceLevelEditorWindow>("Differences Editor");
        if (window.levelAsset == null)
            window.levelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DifferencePrefabSetup.LevelPath);
        window.Show();
    }

    private void OnEnable()
    {
        minSize = new Vector2(900, 740);
        Undo.undoRedoPerformed += OnUndo;
        EditorApplication.hierarchyChanged += Repaint;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndo;
        EditorApplication.hierarchyChanged -= Repaint;
    }

    private void OnUndo() { selected = -1; Repaint(); }
    private void OnInspectorUpdate() => Repaint();

    private void OnGUI()
    {
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            DrawToolbar();
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var current = stage != null ? stage.prefabContentsRoot.GetComponent<DifferenceLevelController>() : null;
            if (board != current)
            {
                board = current;
                selected = -1;
                dragMode = DragMode.None;
            }
            if (board == null)
            {
                EditorGUILayout.HelpBox("Select a difference level prefab above and click Open. Edits are made directly in Unity Prefab Mode.", MessageType.Info);
                DrawStatus();
                return;
            }
            EditorGUILayout.LabelField("Editing: " + stage.assetPath, EditorStyles.miniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSettings();
            DrawPictures();
            DrawPairs();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate")) Validate();
            if (GUILayout.Button("Save Prefab + Sync Levels")) SaveLevel();
            if (GUILayout.Button("Select in Hierarchy")) Selection.activeGameObject = board.gameObject;
            EditorGUILayout.EndHorizontal();
            DrawStatus();
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        levelAsset = (GameObject)EditorGUILayout.ObjectField(levelAsset, typeof(GameObject), false);
        if (GUILayout.Button("Open", EditorStyles.toolbarButton, GUILayout.Width(55)))
        {
            string path = AssetDatabase.GetAssetPath(levelAsset);
            if (levelAsset != null && levelAsset.GetComponent<DifferenceLevelController>() != null && path.EndsWith(".prefab"))
                PrefabStageUtility.OpenPrefab(path);
            else SetStatus("Choose a prefab containing DifferenceLevelController.", MessageType.Warning);
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        newLevelNumber = Mathf.Max(1, EditorGUILayout.IntField("New level number", newLevelNumber));
        if (GUILayout.Button("Create Empty Level", GUILayout.Width(165)))
        {
            CreateLevel();
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSettings()
    {
        var data = new SerializedObject(board);
        data.Update();
        EditorGUILayout.PropertyField(data.FindProperty("maxMistakes"), new GUIContent("Max mistakes (0 = unlimited)"));
        if (data.ApplyModifiedProperties()) DifferenceLevelEditing.Dirty(board);
        EditorGUILayout.BeginHorizontal();
        drawMode = GUILayout.Toggle(drawMode, "Draw New Pair", "Button", GUILayout.Width(150));
        if (GUILayout.Button("Add Center Pair", GUILayout.Width(150)))
        {
            Run(() => selected = DifferenceLevelEditing.AddPair(board, new Rect(.4f, .4f, .2f, .2f)));
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();
        DifferenceLevelEditing.SyncMatchingAreas = EditorGUILayout.ToggleLeft(
            "Sync A/B areas while editing", DifferenceLevelEditing.SyncMatchingAreas);
        EditorGUILayout.HelpBox(drawMode
            ? "Drag a rectangle on either picture to add a matching pair at the same relative position on A and B."
            : DifferenceLevelEditing.SyncMatchingAreas
                ? "Drag or resize a numbered area on either picture; the other picture follows. Turn off Sync A/B to edit each side separately. Ctrl+Z / Ctrl+Y undo and redo."
                : "Drag or resize a numbered area on either picture independently. Turn on Sync A/B to move both together. Ctrl+Z / Ctrl+Y undo and redo.", MessageType.Info);
    }

    private void DrawPictures()
    {
        bool horizontal = position.width >= 840;
        if (horizontal) EditorGUILayout.BeginHorizontal();
        DrawPicture(true, horizontal);
        DrawPicture(false, horizontal);
        if (horizontal) EditorGUILayout.EndHorizontal();
    }

    private void DrawPicture(bool sideA, bool horizontal)
    {
        var panel = DifferenceLevelEditing.Panel(board, sideA);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        string title = sideA ? "Picture A" : "Picture B";
        Sprite sprite = panel != null && panel.Picture != null ? panel.Picture.sprite : null;
        var next = (Sprite)EditorGUILayout.ObjectField(title, sprite, typeof(Sprite), false);
        if (next != sprite) DifferenceLevelEditing.AssignSprite(panel, next);
        sprite = next;
        float width = horizontal ? (position.width - 70) / 2 : position.width - 52;
        float ratio = sprite != null ? sprite.rect.width / sprite.rect.height : 1.5f;
        float height = Mathf.Clamp(width / ratio, 180, 480);
        Rect available = GUILayoutUtility.GetRect(100, 10000, height, height);
        EditorGUI.DrawRect(available, new Color(.1f, .12f, .15f));
        Rect imageRect = Fit(available, ratio);
        HandleSpriteDrop(available, panel);
        if (sprite == null)
        {
            GUI.Label(available, "Drop a Sprite here\nImport as Sprite (2D and UI), Single", CenteredLabel());
            EditorGUILayout.EndVertical();
            return;
        }
        // Source pictures use Single sprites. Use texture UVs, not a low-resolution asset thumbnail.
        Rect textureRect = sprite.textureRect;
        GUI.DrawTextureWithTexCoords(imageRect, sprite.texture,
            new Rect(textureRect.x / sprite.texture.width, textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width, textureRect.height / sprite.texture.height));
        for (int i = 0; i < board.TotalCount; i++)
        {
            var spot = DifferenceLevelEditing.Spot(board, i, sideA);
            if (spot == null) continue;
            Rect area = DifferenceLevelEditing.ToPreview(DifferenceLevelEditing.Bounds(spot, panel), imageRect);
            DrawArea(area, i == selected ? Color.green : new Color(1, .7f, .1f), (i + 1).ToString());
            if (i == selected)
            {
                var handle = ResizeHandle(area);
                EditorGUI.DrawRect(handle, Color.green);
                EditorGUIUtility.AddCursorRect(area, MouseCursor.MoveArrow);
                EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeUpLeft);
            }
        }
        if (dragMode == DragMode.Draw && dragSideA == sideA)
            DrawArea(DifferenceLevelEditing.ToPreview(drawnBounds, imageRect), Color.cyan, "+");
        HandlePointer(imageRect, panel, sideA);
        EditorGUILayout.EndVertical();
    }

    private void HandlePointer(Rect image, DifferencePanelView panel, bool sideA)
    {
        var e = Event.current;
        int control = GUIUtility.GetControlID(FocusType.Passive, image);
        if (e.type == EventType.MouseDown && e.button == 0 && image.Contains(e.mousePosition))
        {
            dragSideA = sideA;
            dragStart = DifferenceLevelEditing.FromPreview(e.mousePosition, image);
            dragMode = DragMode.None;
            if (drawMode)
            {
                dragMode = DragMode.Draw;
                drawnBounds = new Rect(dragStart, Vector2.zero);
            }
            else
            {
                int hit = -1;
                if (selected >= 0)
                {
                    Rect selectedBounds = DifferenceLevelEditing.ToPreview(
                        DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, selected, sideA), panel), image);
                    if (ResizeHandle(selectedBounds).Contains(e.mousePosition))
                    {
                        hit = selected;
                        dragMode = DragMode.Resize;
                    }
                }
                for (int i = board.TotalCount - 1; hit < 0 && i >= 0; i--)
                {
                    var spot = DifferenceLevelEditing.Spot(board, i, sideA);
                    if (spot != null && DifferenceLevelEditing.ToPreview(DifferenceLevelEditing.Bounds(spot, panel), image).Contains(e.mousePosition))
                        hit = i;
                }
                selected = hit;
                if (hit >= 0)
                {
                    if (dragMode != DragMode.Resize) dragMode = DragMode.Move;
                    originalBounds = DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, selected, sideA), panel);
                    Undo.IncrementCurrentGroup();
                    undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Drag difference area");
                }
            }
            if (dragMode != DragMode.None) GUIUtility.hotControl = control;
            e.Use();
            Repaint();
        }
        if (GUIUtility.hotControl != control || dragMode == DragMode.None || dragSideA != sideA) return;
        if (e.type == EventType.MouseDrag || e.type == EventType.MouseUp)
        {
            Vector2 point = DifferenceLevelEditing.FromPreview(e.mousePosition, image);
            if (dragMode == DragMode.Draw)
                drawnBounds = Rect.MinMaxRect(Mathf.Min(point.x, dragStart.x), Mathf.Min(point.y, dragStart.y),
                    Mathf.Max(point.x, dragStart.x), Mathf.Max(point.y, dragStart.y));
            else
            {
                Rect next = originalBounds;
                if (dragMode == DragMode.Move) next.position += point - dragStart;
                else
                {
                    next.xMax = Mathf.Clamp(point.x, next.xMin + .01f, 1);
                    next.yMin = Mathf.Clamp(point.y, 0, next.yMax - .01f);
                }
                DifferenceLevelEditing.SetPairBounds(board, selected, sideA, next,
                    DifferenceLevelEditing.SyncMatchingAreas);
            }
            if (e.type == EventType.MouseUp)
            {
                bool added = false;
                if (dragMode == DragMode.Draw && drawnBounds.width >= .01f && drawnBounds.height >= .01f)
                {
                    Run(() => selected = DifferenceLevelEditing.AddPair(board, drawnBounds));
                    added = true;
                }
                else if (dragMode != DragMode.Draw) Undo.CollapseUndoOperations(undoGroup);
                dragMode = DragMode.None;
                GUIUtility.hotControl = 0;
                if (added)
                {
                    e.Use();
                    Repaint();
                    GUIUtility.ExitGUI();
                }
            }
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.Ignore)
        {
            dragMode = DragMode.None;
            GUIUtility.hotControl = 0;
        }
    }

    private void DrawPairs()
    {
        EditorGUILayout.LabelField($"Difference pairs: {board.TotalCount}", EditorStyles.boldLabel);
        var data = new SerializedObject(board);
        var pairs = data.FindProperty("differences");
        for (int i = 0; i < pairs.arraySize; i++)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(i == selected, (i + 1).ToString(), "Button", GUILayout.Width(35))) selected = i;
            EditorGUILayout.PropertyField(pairs.GetArrayElementAtIndex(i).FindPropertyRelative("id"), GUIContent.none);
            if (GUILayout.Button("Select A", GUILayout.Width(70))) SelectSpot(i, true);
            if (GUILayout.Button("Select B", GUILayout.Width(70))) SelectSpot(i, false);
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                data.ApplyModifiedProperties();
                DifferenceLevelEditing.DeletePair(board, i);
                selected = -1;
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
        if (data.ApplyModifiedProperties()) DifferenceLevelEditing.Dirty(board);
        if (selected < 0 || selected >= board.TotalCount) return;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy selected area A -> B")) CopyArea(true);
        if (GUILayout.Button("Copy selected area B -> A")) CopyArea(false);
        EditorGUILayout.EndHorizontal();
    }

    private void SelectSpot(int index, bool sideA)
    {
        selected = index;
        var spot = DifferenceLevelEditing.Spot(board, index, sideA);
        if (spot != null) Selection.activeGameObject = spot.gameObject;
    }

    private void CopyArea(bool fromA)
    {
        var bounds = DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, selected, fromA), DifferenceLevelEditing.Panel(board, fromA));
        DifferenceLevelEditing.SetBounds(DifferenceLevelEditing.Spot(board, selected, !fromA), DifferenceLevelEditing.Panel(board, !fromA), bounds);
    }

    private bool Validate()
    {
        if (board == null) return false;
        if (!board.TryValidate(out string error))
        {
            SetStatus(error, MessageType.Error);
            return false;
        }
        for (int i = 0; i < board.TotalCount; i++)
            foreach (bool sideA in new[] { true, false })
            {
                Rect rect = DifferenceLevelEditing.Bounds(DifferenceLevelEditing.Spot(board, i, sideA), DifferenceLevelEditing.Panel(board, sideA));
                if (rect.width <= 0 || rect.height <= 0 || rect.xMin < -.001f || rect.yMin < -.001f || rect.xMax > 1.001f || rect.yMax > 1.001f)
                {
                    SetStatus($"Pair {i + 1}: area must be inside Picture {(sideA ? "A" : "B")}.", MessageType.Error);
                    return false;
                }
            }
        SetStatus($"Valid level: {board.TotalCount} pairs.", MessageType.Info);
        return true;
    }

    private void SaveLevel()
    {
        if (!Validate()) return;
        Run(() =>
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath, out bool saved);
            if (!saved) throw new InvalidOperationException("Could not save prefab.");
            int count = SyncPlayableLevels();
            SetStatus($"Saved prefab + Addressables. Progression contains {count} consecutive valid level(s).", MessageType.Info);
        });
    }

    internal static int SyncPlayableLevels()
    {
        // Include only the contiguous, valid sequence. Empty draft levels cannot enter gameplay.
        int count = 0;
        while (true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(DifferencePrefabSetup.LevelDirectory + $"/Level {count + 1}.prefab");
            var controller = asset != null ? asset.GetComponent<DifferenceLevelController>() : null;
            if (controller == null || !controller.TryValidate(out _)) break;
            count++;
        }
        var config = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Project/Config/LevelConfig.asset");
        if (config != null && count > 0)
        {
            Undo.RecordObject(config, "Update playable level count");
            config.maxLevel = count;
            config.startLoopLevel = Mathf.Clamp(config.startLoopLevel, 1, count);
            EditorUtility.SetDirty(config);
        }
        LevelAddressablesSetup.SyncLevelEntries();
        PictureCollectionPrefabSetup.SyncAllAlbums();
        AssetDatabase.SaveAssets();
        return count;
    }

    private void CreateLevel()
    {
        Run(() =>
        {
            string path = DifferencePrefabSetup.LevelDirectory + $"/Level {newLevelNumber}.prefab";
            string source = DifferencePrefabSetup.BasePath;
            levelAsset = CreateEmptyLevel(source, path, newLevelNumber);
            PrefabStageUtility.OpenPrefab(path);
            SetStatus("World level created from LevelBase. Assign A/B sprites, draw pairs, then Save Prefab + Sync Levels.", MessageType.Info);
        });
    }

    internal static GameObject CreateEmptyLevel(string source, string path, int number)
    {
        if (File.Exists(path)) throw new InvalidOperationException("That level already exists. Choose another number; existing levels are never overwritten.");
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if (template == null || template.GetComponent<DifferenceLevelController>() == null)
            throw new InvalidOperationException("Choose a difference level prefab as the template.");
        if (!AssetDatabase.CopyAsset(source, path)) throw new InvalidOperationException("Could not copy the template prefab.");
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(path);
            root.name = $"Level {number}";
            var controller = root.GetComponent<DifferenceLevelController>();
            var data = new SerializedObject(controller);
            data.FindProperty("differences").ClearArray();
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var spot in root.GetComponentsInChildren<DifferenceSpotView>(true)) DestroyImmediate(spot.gameObject);
            DifferenceLevelEditing.Panel(controller, true).Picture.sprite = null;
            DifferenceLevelEditing.Panel(controller, false).Picture.sprite = null;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        catch
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                root = null;
            }
            AssetDatabase.DeleteAsset(path);
            throw;
        }
        finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private void HandleSpriteDrop(Rect area, DifferencePanelView panel)
    {
        var e = Event.current;
        if (!area.Contains(e.mousePosition) || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)) return;
        Sprite sprite = null;
        foreach (var obj in DragAndDrop.objectReferences)
        {
            sprite = obj as Sprite ?? AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(obj));
            if (sprite != null) break;
        }
        DragAndDrop.visualMode = sprite != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
        if (e.type == EventType.DragPerform && sprite != null)
        {
            DragAndDrop.AcceptDrag();
            DifferenceLevelEditing.AssignSprite(panel, sprite);
        }
        e.Use();
    }

    private static Rect Fit(Rect area, float ratio)
    {
        float width = Mathf.Min(area.width, area.height * ratio);
        float height = width / ratio;
        return new Rect(area.center.x - width / 2, area.center.y - height / 2, width, height);
    }

    private static Rect ResizeHandle(Rect rect) => new Rect(rect.xMax - 10, rect.yMax - 10, 10, 10);
    private static GUIStyle CenteredLabel() => new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };

    private static void DrawArea(Rect area, Color color, string label)
    {
        EditorGUI.DrawRect(area, new Color(color.r, color.g, color.b, .15f));
        EditorGUI.DrawRect(new Rect(area.x, area.y, area.width, 2), color);
        EditorGUI.DrawRect(new Rect(area.x, area.yMax - 2, area.width, 2), color);
        EditorGUI.DrawRect(new Rect(area.x, area.y, 2, area.height), color);
        EditorGUI.DrawRect(new Rect(area.xMax - 2, area.y, 2, area.height), color);
        GUI.Label(new Rect(area.x + 3, area.y + 2, 40, 20), label, EditorStyles.whiteBoldLabel);
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (ExitGUIException) { throw; }
        catch (Exception exception) { SetStatus(exception.Message, MessageType.Error); Debug.LogException(exception); }
    }
    private void SetStatus(string value, MessageType type) { status = value; statusType = type; Repaint(); }
    private void DrawStatus() { if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType); }
}
