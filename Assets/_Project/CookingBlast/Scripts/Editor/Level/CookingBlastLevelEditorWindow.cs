using System;
using System.IO;
using System.Linq;
using CookingSort2D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CookingBlastLevelEditorWindow : EditorWindow
{
    [SerializeField] private GameObject levelAsset;
    [SerializeField] private int newLevelNumber = 2;
    [SerializeField] private FoodColor paintColor = FoodColor.Blue;
    [SerializeField] private int boardColumns = 10;
    [SerializeField] private int boardRows = 10;
    [SerializeField] private int laneCount = 4;
    [SerializeField] private int minDemand = 12;
    [SerializeField] private int maxDemand = 18;
    [SerializeField] private int randomSeed = 1001;
    [SerializeField] private int paletteMask = (1 << (int)FoodColor.Blue) | (1 << (int)FoodColor.Pink);

    private readonly CookingBlastBoardPanel boardPanel = new CookingBlastBoardPanel();
    [SerializeField] private bool showCustomerDetails;
    private Vector2 customerGridScroll;
    private bool customerPainting;
    private int customerStrokeGroup = -1;
    private int lastCustomerLane = -1;
    private int lastCustomerRow = -1;
    private CookingBlastLevel level;
    private Vector2 scroll;
    private string status;
    private MessageType statusType;

    [MenuItem("Tools/Level Editor/Cooking Blast Editor")]
    [MenuItem("Tools/Cooking Blast/Level Editor")]
    public static void OpenWindow()
    {
        var window = GetWindow<CookingBlastLevelEditorWindow>("Cooking Blast Level");
        if (window.levelAsset == null)
            window.levelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CookingBlastPrefabSetup.Level1Path);
        window.Show();
    }

    private void OnEnable()
    {
        minSize = new Vector2(760, 700);
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.hierarchyChanged += Repaint;
    }

private void OnDisable()
    {
        boardPanel.EndStroke();
        EndCustomerStroke();
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.hierarchyChanged -= Repaint;
    }

private void OnLostFocus()
    {
        boardPanel.EndStroke();
        EndCustomerStroke();
    }


    private void OnUndoRedo()
    {
        Repaint();
    }

    private void OnInspectorUpdate() => Repaint();

    private void OnGUI()
    {
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            DrawToolbar();
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var current = stage != null ? stage.prefabContentsRoot.GetComponent<CookingBlastLevel>() : null;
            if (level != current)
            {
                level = current;
                if (level != null)
                {
                    boardColumns = Mathf.Max(1, level.BoardColumns.Count);
                    boardRows = Mathf.Max(1, CookingBlastLevelEditing.MaxRows(level));
                    laneCount = Mathf.Max(1, level.CustomerLanes.Count);
                }
            }

            if (level == null)
            {
                EditorGUILayout.HelpBox("Choose a Cooking Blast level prefab and click Open. Like the Hidden Object editor, this edits the real prefab directly in Prefab Mode; there is no second level-data copy.", MessageType.Info);
                DrawStatus();
                return;
            }

            EditorGUILayout.LabelField("Editing: " + stage.assetPath, EditorStyles.miniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSettings();
            GUILayout.Space(6);
            DrawBoardEditor();
            GUILayout.Space(8);
            DrawCustomerEditor();
            GUILayout.Space(8);
            DrawStats();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate", GUILayout.Height(30))) ValidateLevel();
            if (GUILayout.Button("Save Prefab + Sync Levels", GUILayout.Height(30))) SaveLevel();
            if (GUILayout.Button("Test In Gameplay", GUILayout.Height(30))) TestInGameplayScene();
            if (GUILayout.Button("Select Level Root", GUILayout.Height(30))) Selection.activeGameObject = level.gameObject;
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
            if (levelAsset != null && levelAsset.GetComponent<CookingBlastLevel>() != null && path.EndsWith(".prefab"))
                PrefabStageUtility.OpenPrefab(path);
            else
                SetStatus("Choose a prefab containing CookingBlastLevel.", MessageType.Warning);
            GUIUtility.ExitGUI();
        }
        if (GUILayout.Button("Setup", EditorStyles.toolbarButton, GUILayout.Width(55)))
        {
            CookingBlastPrefabSetup.SetupLevelSystem();
            levelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CookingBlastPrefabSetup.Level1Path);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        newLevelNumber = Mathf.Max(1, EditorGUILayout.IntField("New level number", newLevelNumber));
        if (GUILayout.Button("Create Empty Level", GUILayout.Width(165)))
        {
            Run(() =>
            {
                levelAsset = CookingBlastPrefabSetup.CreateEmptyLevel(newLevelNumber);
                PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(levelAsset));
                SetStatus("Created from LevelBase. Resize board, paint colors, generate/edit customers, then Save Prefab + Sync Levels.", MessageType.Info);
            });
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();
    }

private void DrawSettings()
    {
        EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);
        var so = new SerializedObject(level);
        so.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(so.FindProperty("levelNumber"), new GUIContent("Level Number"));
        EditorGUILayout.PropertyField(so.FindProperty("seatCount"), new GUIContent("Seat Count"));
        if (so.ApplyModifiedProperties()) CookingBlastLevelEditing.Dirty(level);

        GUILayout.Space(4);
        EditorGUILayout.LabelField("Level Prefab Pieces", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        DrawPieceButton("BOARD", level.Board != null ? level.Board.gameObject : null, CookingBlastPaletteColor(new Color32(224, 214, 174, 255)));
        DrawPieceButton("SEAT BAR", level.SeatBar != null ? level.SeatBar.gameObject : null, CookingBlastPaletteColor(new Color32(190, 121, 54, 255)));
        DrawPieceButton("CUSTOMER QUEUE", level.CustomerQueue != null ? level.CustomerQueue.gameObject : null, CookingBlastPaletteColor(new Color32(125, 185, 120, 255)));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Board, SeatBar and CustomerQueue are nested inside this level prefab. Click a piece above to select it in Prefab Mode.", MessageType.None);
    }

private void DrawBoardEditor()
    {
        boardPanel.Draw(level, ref paintColor, ref boardColumns, ref boardRows, ref paletteMask, ref randomSeed, position.width);
    }

private void DrawCustomerEditor()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("CUSTOMER DESIGNER", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Choose a color • Click/drag to paint • Right-click to pick • Ctrl+Z to undo", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("Each column is one lane, FRONT at the top and BACK below. Click + to append a customer. Color and demand are saved directly in the level prefab.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                float laneLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 42;
                laneCount = Mathf.Clamp(EditorGUILayout.IntField("Lanes", laneCount, GUILayout.Width(145)), 1, 8);
                EditorGUIUtility.labelWidth = laneLabelWidth;
                if (GUILayout.Button("Apply Lanes", GUILayout.Width(95)))
                {
                    EndCustomerStroke();
                    CookingBlastLevelEditing.EnsureLaneCount(level, laneCount);
                    GUIUtility.ExitGUI();
                }
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 30;
                minDemand = Mathf.Max(1, EditorGUILayout.IntField("Min", minDemand, GUILayout.Width(90)));
                maxDemand = Mathf.Max(minDemand, EditorGUILayout.IntField("Max", maxDemand, GUILayout.Width(90)));
                EditorGUIUtility.labelWidth = previousLabelWidth;
                if (GUILayout.Button("Auto Generate From Board", GUILayout.Width(180)))
                {
                    EndCustomerStroke();
                    CookingBlastLevelEditing.AutoGenerateCustomers(level, laneCount, minDemand, maxDemand);
                    GUIUtility.ExitGUI();
                }
            }

            GUILayout.Space(7);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(170)))
                {
                    DrawCustomerPalette();
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("FRONT / FIRST TO SELECT", EditorStyles.centeredGreyMiniLabel);
                    customerGridScroll = EditorGUILayout.BeginScrollView(customerGridScroll, GUILayout.Height(335));
                    DrawCustomerGrid(Mathf.Max(280, position.width - 230));
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.LabelField("BACK / LAST TO SELECT", EditorStyles.centeredGreyMiniLabel);
                }
            }

            GUILayout.Space(5);
            showCustomerDetails = EditorGUILayout.Foldout(showCustomerDetails, "Customer details — demand / order / remove", true);
            if (showCustomerDetails) DrawCustomerDetails();
        }
        if (Event.current.rawType == EventType.MouseUp) EndCustomerStroke();
    }

    private void DrawStats()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Level Stats   •   Tiles: {level.TotalTileCount}   •   Columns: {level.BoardColumns.Count}   •   Lanes: {level.CustomerLanes.Count}", EditorStyles.boldLabel);
        var tiles = CookingBlastLevelEditing.TileCounts(level);
        var demand = CookingBlastLevelEditing.DemandCounts(level);
        foreach (FoodColor color in Enum.GetValues(typeof(FoodColor)))
        {
            int t = tiles.TryGetValue(color, out int tv) ? tv : 0;
            int d = demand.TryGetValue(color, out int dv) ? dv : 0;
            if (t == 0 && d == 0) continue;
            Color old = GUI.color;
            if (t != d) GUI.color = new Color(1f, .55f, .45f);
            EditorGUILayout.LabelField($"{color}: board {t}  /  demand {d}" + (t == d ? "  ✓" : "  ✗"));
            GUI.color = old;
        }
        EditorGUILayout.EndVertical();
    }

    private bool ValidateLevel()
    {
        if (level == null) return false;
        if (!level.TryValidate(out string error))
        {
            SetStatus(error, MessageType.Error);
            return false;
        }
        SetStatus($"Valid level: {level.TotalTileCount} tiles, {level.CustomerLanes.Count} lanes, {level.SeatCount} seats.", MessageType.Info);
        return true;
    }

    private void SaveLevel()
    {
        if (!ValidateLevel()) return;
        Run(() =>
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath, out bool saved);
            if (!saved) throw new InvalidOperationException("Could not save level prefab.");
            int count = SyncPlayableLevels();
            SetStatus($"Saved. Progression now contains {count} consecutive valid Cooking Blast level(s).", MessageType.Info);
        });
    }

    internal static int SyncPlayableLevels()
    {
        CookingBlastPrefabSetup.EnsureFolders();
        CookingBlastLevelConfig config = CookingBlastPrefabSetup.CreateConfig();
        int count = 0;
        while (true)
        {
            string path = CookingBlastPrefabSetup.LevelDirectory + $"/Level {count + 1}.prefab";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CookingBlastLevel candidate = asset != null ? asset.GetComponent<CookingBlastLevel>() : null;
            if (candidate == null || !candidate.TryValidate(out _)) break;
            count++;
        }

        if (config != null)
        {
            Undo.RecordObject(config, "Update Cooking Blast level progression");
            config.maxLevel = Mathf.Max(1, count);
            config.startLoopLevel = Mathf.Clamp(config.startLoopLevel, 1, config.maxLevel);
            EditorUtility.SetDirty(config);
        }
        CookingBlastLevelAddressablesSetup.SyncLevelEntries();
        AssetDatabase.SaveAssets();
        return count;
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (ExitGUIException) { throw; }
        catch (Exception exception)
        {
            SetStatus(exception.Message, MessageType.Error);
            Debug.LogException(exception);
        }
    }

    private void SetStatus(string value, MessageType type)
    {
        status = value;
        statusType = type;
        Repaint();
    }

    private void DrawStatus()
    {
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);
    }


private void TestInGameplayScene()
    {
        if (!ValidateLevel()) return;
        Run(() =>
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            string levelPath = stage.assetPath;
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath, out bool saved);
            if (!saved) throw new InvalidOperationException("Could not save level prefab before playtest.");

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(levelPath);
            CookingBlastLevel levelAsset = asset != null ? asset.GetComponent<CookingBlastLevel>() : null;
            if (levelAsset == null) throw new InvalidOperationException("Level prefab is missing CookingBlastLevel.");

            SyncPlayableLevels();
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameplayScene.unity", OpenSceneMode.Single);
            CookingSortGameController game = UnityEngine.Object.FindFirstObjectByType<CookingSortGameController>();
            if (game == null) throw new InvalidOperationException("GameplayScene is missing CookingSortGameController.");

            CookingBlastLevelLoader loader = UnityEngine.Object.FindFirstObjectByType<CookingBlastLevelLoader>();
            if (loader == null)
            {
                loader = game.gameObject.AddComponent<CookingBlastLevelLoader>();
                Transform root = game.transform.Find("LevelRoot");
                if (root == null)
                {
                    var rootGo = new GameObject("LevelRoot");
                    rootGo.transform.SetParent(game.transform, false);
                    root = rootGo.transform;
                }
                var loaderSo = new SerializedObject(loader);
                loaderSo.FindProperty("config").objectReferenceValue = CookingBlastPrefabSetup.CreateConfig();
                loaderSo.FindProperty("game").objectReferenceValue = game;
                loaderSo.FindProperty("levelRoot").objectReferenceValue = root;
                loaderSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(loader);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            PlayerPrefs.SetInt("CookingBlast.CurrentLevel", levelAsset.LevelNumber);
            PlayerPrefs.Save();
            Selection.activeGameObject = loader.gameObject;
            SetStatus($"Gameplay loader will start from Level {levelAsset.LevelNumber}. Press Play to test through the real Addressables load flow.", MessageType.Info);
        });
    }


private void DrawPieceButton(string label, GameObject target, Color color)
    {
        Color oldBg = GUI.backgroundColor;
        Color oldContent = GUI.contentColor;
        GUI.backgroundColor = color;
        GUI.contentColor = Color.black;
        using (new EditorGUI.DisabledScope(target == null))
        {
            if (GUILayout.Button(target == null ? label + "\nMISSING" : label + "\nSelect", GUILayout.Height(48)))
            {
                Selection.activeGameObject = target;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }
        GUI.backgroundColor = oldBg;
        GUI.contentColor = oldContent;
    }







    private static Color CookingBlastPaletteColor(Color color) => color;


private void AddCustomer(int lane, FoodColor color, int demand)
    {
        if (lane < 0 || lane >= level.CustomerLanes.Count) return;
        Undo.RecordObject(level, "Add Cooking Blast customer");
        if (level.CustomerLanes[lane] == null) level.CustomerLanes[lane] = new CustomerLaneData();
        if (level.CustomerLanes[lane].frontToBack == null)
            level.CustomerLanes[lane].frontToBack = new System.Collections.Generic.List<CustomerOrderData>();
        level.CustomerLanes[lane].frontToBack.Add(new CustomerOrderData(color, demand));
        CookingBlastLevelEditing.Dirty(level);
    }


private void PaintCustomer(int lane, int row, FoodColor color)
    {
        if (lane == lastCustomerLane && row == lastCustomerRow) return;
        lastCustomerLane = lane;
        lastCustomerRow = row;
        if (lane < 0 || lane >= level.CustomerLanes.Count) return;
        var orders = level.CustomerLanes[lane]?.frontToBack;
        if (orders == null || row < 0 || row >= orders.Count || orders[row] == null || orders[row].color == color) return;
        Undo.RecordObject(level, "Paint customer color");
        orders[row].color = color;
        CookingBlastLevelEditing.Dirty(level);
    }


private void EndCustomerStroke()
    {
        if (customerStrokeGroup >= 0) Undo.CollapseUndoOperations(customerStrokeGroup);
        customerStrokeGroup = -1;
        customerPainting = false;
        lastCustomerLane = -1;
        lastCustomerRow = -1;
    }


private void BeginCustomerStroke()
    {
        if (customerPainting) return;
        Undo.IncrementCurrentGroup();
        customerStrokeGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Paint customer colors");
        customerPainting = true;
        lastCustomerLane = -1;
        lastCustomerRow = -1;
    }


private void DrawCustomerDetails()
    {
        var so = new SerializedObject(level);
        so.Update();
        var lanes = so.FindProperty("customerLanes");
        for (int lane = 0; lane < lanes.arraySize; lane++)
        {
            var laneProp = lanes.GetArrayElementAtIndex(lane);
            var list = laneProp.FindPropertyRelative("frontToBack");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Lane " + (lane + 1) + " (" + list.arraySize + " customers)", EditorStyles.miniBoldLabel);
            int remove = -1;
            int moveIndex = -1;
            int move = 0;
            for (int i = 0; i < list.arraySize; i++)
            {
                var order = list.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(i == 0 ? "FRONT" : (i + 1).ToString(), GUILayout.Width(45));
                var colorProp = order.FindPropertyRelative("color");
                Rect swatch = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
                EditorGUI.DrawRect(swatch, CookingSortPalette.Get((FoodColor)colorProp.enumValueIndex));
                EditorGUILayout.PropertyField(colorProp, GUIContent.none, GUILayout.Width(100));
                EditorGUILayout.PropertyField(order.FindPropertyRelative("demand"), GUIContent.none, GUILayout.Width(70));
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("↑", GUILayout.Width(25))) { moveIndex = i; move = -1; }
                using (new EditorGUI.DisabledScope(i == list.arraySize - 1))
                    if (GUILayout.Button("↓", GUILayout.Width(25))) { moveIndex = i; move = 1; }
                if (GUILayout.Button("X", GUILayout.Width(25))) remove = i;
                EditorGUILayout.EndHorizontal();
            }
            if (remove >= 0) list.DeleteArrayElementAtIndex(remove);
            else if (moveIndex >= 0) list.MoveArrayElement(moveIndex, moveIndex + move);
            if (GUILayout.Button("+ Add Customer", GUILayout.Width(120)))
            {
                int index = list.arraySize++;
                var order = list.GetArrayElementAtIndex(index);
                order.FindPropertyRelative("color").enumValueIndex = (int)paintColor;
                order.FindPropertyRelative("demand").intValue = Mathf.Max(1, minDemand);
            }
            EditorGUILayout.EndVertical();
        }
        if (so.ApplyModifiedProperties()) CookingBlastLevelEditing.Dirty(level);
    }


private void DrawCustomerGrid(float available)
    {
        int lanes = level.CustomerLanes.Count;
        if (lanes == 0)
        {
            EditorGUILayout.HelpBox("Set the lane count and press Apply Lanes.", MessageType.Info);
            return;
        }

        int maxDepth = 0;
        for (int lane = 0; lane < lanes; lane++)
        {
            var orders = level.CustomerLanes[lane]?.frontToBack;
            if (orders != null) maxDepth = Mathf.Max(maxDepth, orders.Count);
        }

        float size = Mathf.Clamp((available - 35) / lanes, 46f, 74f);
        int visibleRows = maxDepth + 1; // The last row is an append (+) button for each lane.
        Rect area = GUILayoutUtility.GetRect(28 + lanes * size, 31 + visibleRows * size, GUILayout.ExpandWidth(false));
        EditorGUI.DrawRect(area, new Color(.07f, .09f, .15f));

        Event ev = Event.current;
        for (int lane = 0; lane < lanes; lane++)
        {
            var orders = level.CustomerLanes[lane]?.frontToBack;
            int count = orders?.Count ?? 0;
            GUI.Label(new Rect(area.x + 27 + lane * size, area.y + 2, size - 2, 20),
                "LANE " + (lane + 1), EditorStyles.centeredGreyMiniLabel);

            for (int row = 0; row <= count; row++)
            {
                bool append = row == count;
                Rect cell = new Rect(area.x + 28 + lane * size, area.y + 26 + row * size, size - 3, size - 3);
                if (append)
                {
                    EditorGUI.DrawRect(cell, new Color(.18f, .22f, .27f));
                    GUI.Label(cell, "+", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    CustomerOrderData customer = orders[row];
                    if (customer == null) continue;
                    Color color = CookingSortPalette.Get(customer.color);
                    if (row == 0)
                        EditorGUI.DrawRect(new Rect(cell.x - 1, cell.y - 1, cell.width + 2, cell.height + 2),
                            new Color(.65f, .85f, 1f));
                    EditorGUI.DrawRect(cell, Color.Lerp(color, Color.black, .23f));
                    EditorGUI.DrawRect(new Rect(cell.x + 2, cell.y + 2, cell.width - 4, cell.height - 4), color);
                    var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                    style.normal.textColor = color.grayscale > .6f ? Color.black : Color.white;
                    GUI.Label(cell, CookingSortPalette.ShortName(customer.color) + " " + customer.demand, style);
                }

                if (!GUI.enabled || !cell.Contains(ev.mousePosition)) continue;
                if (!append && ev.type == EventType.MouseDown && ev.button == 1)
                {
                    paintColor = orders[row].color;
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 0)
                {
                    if (append)
                    {
                        EndCustomerStroke();
                        AddCustomer(lane, paintColor, minDemand);
                    }
                    else
                    {
                        BeginCustomerStroke();
                        PaintCustomer(lane, row, paintColor);
                    }
                    ev.Use();
                }
                else if (!append && customerPainting && ev.type == EventType.MouseDrag && ev.button == 0)
                {
                    PaintCustomer(lane, row, paintColor);
                    ev.Use();
                }
            }
        }
        for (int row = 0; row < visibleRows; row++)
            GUI.Label(new Rect(area.x, area.y + 26 + row * size, 25, size - 3),
                row == 0 ? "F" : (row + 1).ToString(), EditorStyles.centeredGreyMiniLabel);
    }


private void DrawCustomerPalette()
    {
        EditorGUILayout.LabelField("COLOR LIST", EditorStyles.miniBoldLabel);
        var demandCounts = CookingBlastLevelEditing.DemandCounts(level);
        var tileCounts = CookingBlastLevelEditing.TileCounts(level);
        foreach (FoodColor color in Enum.GetValues(typeof(FoodColor)))
        {
            int demand = demandCounts.TryGetValue(color, out int d) ? d : 0;
            int tiles = tileCounts.TryGetValue(color, out int t) ? t : 0;
            Rect rect = GUILayoutUtility.GetRect(166, 29);
            EditorGUI.DrawRect(rect, color == paintColor ? new Color(.24f, .36f, .48f) : new Color(.16f, .18f, .22f));
            EditorGUI.DrawRect(new Rect(rect.x + 5, rect.y + 5, 19, 19), CookingSortPalette.Get(color));
            GUI.Label(new Rect(rect.x + 30, rect.y + 4, 94, 22), (color == paintColor ? "• " : "") + color);
            GUI.Label(new Rect(rect.xMax - 38, rect.y + 4, 35, 22), demand.ToString(), EditorStyles.boldLabel);
            if (GUI.Button(rect, new GUIContent("", color + ": " + demand + " customer demand / " + tiles + " tiles"), GUIStyle.none))
                paintColor = color;
        }
        GUILayout.Space(6);
        EditorGUILayout.LabelField("Brush: " + paintColor, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Numbers show total demand for each color. Board tiles and customer demand must match to validate.", MessageType.None);
    }
}
