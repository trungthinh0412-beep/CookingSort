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

    [SerializeField] private CookingBlastBoardPanel boardPanel = new CookingBlastBoardPanel();
    
    
    [SerializeField] private bool deleteCustomerTool;
[SerializeField] private bool customerPriorityPicking;
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
                CookingBlastTileSpriteEditorPreview.SetLevelContext(null);
                EditorGUILayout.HelpBox("Choose a Cooking Blast level prefab and click Open. Like the Hidden Object editor, this edits the real prefab directly in Prefab Mode; there is no second level-data copy.", MessageType.Info);
                DrawStatus();
                return;
            }

            CookingBlastTileSpriteEditorPreview.SetLevelContext(level);
            CookingBlastLevelEditing.SyncCustomerDemandsFromAnimals(level);

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
        if (GUILayout.Button("Undo", EditorStyles.toolbarButton, GUILayout.Width(49))) Undo.PerformUndo();
        if (GUILayout.Button("Redo", EditorStyles.toolbarButton, GUILayout.Width(49))) Undo.PerformRedo();
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
        boardPanel.Draw(level, ref paintColor, ref boardColumns, ref boardRows, ref paletteMask, ref randomSeed, position.width, laneCount, minDemand, maxDemand);
    }

internal static void RefreshBoardSizeFromImport(CookingBlastLevel modifiedLevel)
    {
        if (modifiedLevel == null) return;
        foreach (var window in Resources.FindObjectsOfTypeAll<CookingBlastLevelEditorWindow>())
        {
            if (window.level != modifiedLevel) continue;
            window.boardColumns = Mathf.Max(1, modifiedLevel.BoardColumns.Count);
            window.boardRows = Mathf.Max(1, CookingBlastLevelEditing.MaxRows(modifiedLevel));
            window.Repaint();
        }
    }

internal static void RefreshCustomersFromImageImport(CookingBlastLevel changedLevel)
    {
        if (changedLevel == null) return;
        foreach (var window in Resources.FindObjectsOfTypeAll<CookingBlastLevelEditorWindow>())
        {
            if (window.level != changedLevel) continue;
            window.laneCount = Mathf.Max(1, changedLevel.CustomerLanes.Count);
            window.boardPanel.ResetPriority(changedLevel);
            window.Repaint();
        }
    }



private void DrawCustomerEditor()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("ANIMAL / CUSTOMER DESIGNER", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(customerPriorityPicking
                ? "Click Customers to set priority #1, #2, #3... • Click again to unpin"
                : deleteCustomerTool
                    ? "DELETE TOOL ACTIVE • Click a Customer to remove it • Ctrl+Z to undo"
                    : "Click/drag = paint • Red circle = Egg • Right-click = pick color • Shift+click = delete • Ctrl+Z = Undo",
                EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(customerPriorityPicking
                ? "Choose Tile fill priority directly on the Customer grid; numbered badges show your picks."
                : deleteCustomerTool
                    ? "Delete Customer is selected from COLOR LIST. Click a Customer to remove it. Click a color to switch back to painting."
                    : "Each lane is a column. FRONT at top, BACK below. Click the circle on an Animal to toggle its Egg state; its configured color remains visible here.",
                MessageType.None);
            customerPriorityPicking = false;

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
                if (GUILayout.Button("Auto Generate From Board", GUILayout.Width(180)))
                {
                    EndCustomerStroke();
                    CookingBlastLevelEditing.AutoGenerateCustomers(level, laneCount, minDemand, maxDemand);
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(new GUIContent("Shuffle Animal Positions",
                        "Randomize existing Animals across all current lane slots without changing their count, color, or demand."),
                    GUILayout.Width(185)))
                {
                    EndCustomerStroke();
                    if (CookingBlastLevelEditing.ShuffleCustomerPositions(level))
                    {
                        boardPanel.ResetPriority(level);
                        SetStatus("Shuffled Animal positions. Customer count and lane sizes are unchanged.",
                            MessageType.Info);
                    }
                    else
                    {
                        SetStatus("Add at least two Animals before shuffling.", MessageType.Warning);
                    }
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
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            int tileTotal = level.TotalTileCount;
            var tiles = CookingBlastLevelEditing.TileCounts(level);
            var demand = CookingBlastLevelEditing.DemandCounts(level);
            var supported = CookingBlastTileSpriteEditorPreview.GetConfiguredColors();
            var unsupported = new System.Collections.Generic.List<string>();
            foreach (FoodColor type in Enum.GetValues(typeof(FoodColor)))
            {
                if (type == FoodColor.Neutral || supported.Contains(type)) continue;
                int amount = tiles.TryGetValue(type, out int tileCount) ? tileCount : 0;
                int need = demand.TryGetValue(type, out int customerCount)
                    ? customerCount : 0;
                if (amount > 0 || need > 0)
                    unsupported.Add(type + " (" + amount + " Tile / " + need + " demand)");
            }
            if (unsupported.Count > 0)
                EditorGUILayout.HelpBox(
                    "Level contains Match Types without a complete Tile + Animal prefab pair: " +
                    string.Join(", ", unsupported.ToArray()) +
                    ". Existing level data is preserved; these entries must be replaced " +
                    "before the level can spawn reliably.", MessageType.Warning);
            int demandTotal = 0;
            foreach (int count in demand.Values) demandTotal += count;

            EditorGUILayout.LabelField("LEVEL HEALTH  |  " + tileTotal + " Tiles  •  " +
                level.BoardColumns.Count + " Columns  •  " + level.CustomerLanes.Count +
                " Lanes  •  " + level.SeatCount + " Seats", EditorStyles.boldLabel);

            bool balanced = tileTotal == demandTotal && tileTotal > 0;
            foreach (FoodColor color in Enum.GetValues(typeof(FoodColor)))
            {
                int t = tiles.TryGetValue(color, out int tv) ? tv : 0;
                int d = demand.TryGetValue(color, out int dv) ? dv : 0;
                if (t == 0 && d == 0) continue;
                bool match = t == d;
                if (!match) balanced = false;

                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect swatch = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
                    if (!CookingBlastTileSpriteEditorPreview.Draw(swatch, color))
                        EditorGUI.DrawRect(swatch, CookingSortPalette.Get(color));
                    GUILayout.Label(
                        color.ToString(),
                        GUILayout.Width(80));
                    GUILayout.Label("Tiles  " + t + "   /   Need  " + d, GUILayout.Width(185));
                    string delta = match ? "OK" : (t > d ? "+" : "") + (t - d);
                    Color previous = GUI.contentColor;
                    GUI.contentColor = match ? new Color(.43f, .78f, .48f) : new Color(1f, .48f, .39f);
                    GUILayout.Label(delta, EditorStyles.boldLabel);
                    GUI.contentColor = previous;
                }
            }
            EditorGUILayout.HelpBox(balanced
                ? "Tile colors and customer demand are balanced. Validate to check the whole level."
                : "Color totals do not match. Use AUTO FILL from Customers or rebuild Customers from the Board.",
                balanced ? MessageType.Info : MessageType.Warning);
        }
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
            CookingBlastLevelAddressablesSetup.UseAssetDatabaseForPlaytest();
            var scene = EditorSceneManager.OpenScene("Assets/_Project/CookingBlast/Scenes/CookingBlastGameplay.unity", OpenSceneMode.Single);
            CookingSortGameController game = UnityEngine.Object.FindFirstObjectByType<CookingSortGameController>();
            if (game == null) throw new InvalidOperationException("CookingBlastGameplay scene is missing CookingSortGameController.");

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


private void AddCustomer(int lane, FoodColor color, int ignoredDemand)
    {
        if (lane < 0 || lane >= level.CustomerLanes.Count) return;
        Undo.RecordObject(level, "Add Cooking Blast customer");
        if (level.CustomerLanes[lane] == null) level.CustomerLanes[lane] = new CustomerLaneData();
        if (level.CustomerLanes[lane].frontToBack == null)
            level.CustomerLanes[lane].frontToBack = new System.Collections.Generic.List<CustomerOrderData>();
        level.CustomerLanes[lane].frontToBack.Add(
            new CustomerOrderData(color, level.GetCustomerDemand(color)));
        CookingBlastLevelEditing.Dirty(level);
    }

private void RemoveCustomer(int lane, int row)
    {
        if (lane < 0 || lane >= level.CustomerLanes.Count) return;
        var orders = level.CustomerLanes[lane]?.frontToBack;
        if (orders == null || row < 0 || row >= orders.Count) return;
        Undo.RecordObject(level, "Delete Cooking Blast customer");
        orders.RemoveAt(row);
        CookingBlastLevelEditing.Dirty(level);
    }

private void ToggleCustomerEgg(int lane, int row)
    {
        if (lane < 0 || lane >= level.CustomerLanes.Count) return;
        var orders = level.CustomerLanes[lane]?.frontToBack;
        if (orders == null || row < 0 || row >= orders.Count || orders[row] == null)
            return;
        Undo.RecordObject(level, "Toggle mystery Egg");
        orders[row].isEgg = !orders[row].isEgg;
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
        orders[row].demand = level.GetCustomerDemand(color);
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
                FoodColor animalColor = (FoodColor)colorProp.enumValueIndex;
                if (!CookingBlastTileSpriteEditorPreview.DrawAnimal(swatch, animalColor))
                    EditorGUI.DrawRect(swatch, CookingSortPalette.Get(animalColor));
                FoodColor updatedColor = CookingBlastTileSpriteEditorPreview.DrawAvailableColorPopup(
                    (FoodColor)colorProp.enumValueIndex, GUILayout.Width(140));
                if ((FoodColor)colorProp.enumValueIndex != updatedColor)
                {
                    colorProp.enumValueIndex = (int)updatedColor;
                    order.FindPropertyRelative("demand").intValue = level.GetCustomerDemand(updatedColor);
                }
                int animalDemand = level.GetCustomerDemand(updatedColor);
                order.FindPropertyRelative("demand").intValue = animalDemand;
                GUILayout.Label(animalDemand.ToString(), GUILayout.Width(70));
                EditorGUILayout.PropertyField(order.FindPropertyRelative("isEgg"),
                    new GUIContent("Egg", "Display as EGG_Mystic in gameplay until clicked."),
                    GUILayout.Width(55));
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
                order.FindPropertyRelative("demand").intValue = level.GetCustomerDemand(paintColor);
                order.FindPropertyRelative("isEgg").boolValue = false;
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
                Rect eggToggleRect = default;
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
                    EditorGUI.DrawRect(cell, new Color(.14f, .16f, .20f));
                    bool hasAnimalSprite = CookingBlastTileSpriteEditorPreview.DrawAnimal(cell, customer.color);
                    if (!hasAnimalSprite)
                        EditorGUI.DrawRect(new Rect(cell.x + 2, cell.y + 2,
                            cell.width - 4, cell.height - 4), color);
                    var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                    style.normal.textColor = color.grayscale > .6f ? Color.black : Color.white;
                    int rank = boardPanel.GetPriorityRank(level, lane, row);
                    if (rank > 0)
                    {
                        Rect rankRect = new Rect(cell.xMax - 29, cell.y + 3, 26, 17);
                        EditorGUI.DrawRect(rankRect, new Color(.96f, .78f, .20f, 1f));
                        var rankStyle = new GUIStyle(EditorStyles.miniBoldLabel);
                        rankStyle.alignment = TextAnchor.MiddleCenter;
                        rankStyle.normal.textColor = Color.black;
                        GUI.Label(rankRect, "#" + rank, rankStyle);
                    }
                    Rect labelRect = rank > 0
                        ? new Rect(cell.x + 1, cell.y + 18, cell.width - 2, cell.height - 19)
                        : cell;
                    if (hasAnimalSprite)
                    {
                        Rect demandRect = new Rect(cell.x + 7, cell.yMax - 19, cell.width - 14, 17);
                        EditorGUI.DrawRect(demandRect, new Color(0f, 0f, 0f, .70f));
                        style.normal.textColor = Color.white;
                        GUI.Label(demandRect, level.GetCustomerDemand(customer.color).ToString(), style);
                    }
                    else
                        GUI.Label(labelRect, CookingSortPalette.ShortName(customer.color) + " " + level.GetCustomerDemand(customer.color), style);

                    eggToggleRect = new Rect(cell.x + 3, cell.y + 3, 18, 18);
                    var eggMarkerStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 16
                    };
                    eggMarkerStyle.normal.textColor = customer.isEgg
                        ? new Color(1f, .18f, .18f)
                        : new Color(.58f, .58f, .58f, .9f);
                    GUI.Label(eggToggleRect, new GUIContent(customer.isEgg ? "●" : "○",
                        customer.isEgg
                            ? "Mystery Egg enabled. Click to show the normal Animal in gameplay."
                            : "Click to mark this Animal as a mystery Egg."), eggMarkerStyle);
                }

                if (!GUI.enabled || !cell.Contains(ev.mousePosition)) continue;
                if (!append && eggToggleRect.Contains(ev.mousePosition) &&
                    ev.type == EventType.MouseDown && ev.button == 0)
                {
                    EndCustomerStroke();
                    ToggleCustomerEgg(lane, row);
                    ev.Use();
                    Repaint();
                    continue;
                }
                if (!append && ev.type == EventType.MouseDown && ev.button == 1)
                {
                    paintColor = orders[row].color;
                    deleteCustomerTool = false;
                    customerPriorityPicking = false;
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 0)
                {
                    if (append)
                    {
                        if (!customerPriorityPicking && !deleteCustomerTool)
                        {
                            EndCustomerStroke();
                            AddCustomer(lane, paintColor, minDemand);
                        }
                    }
                    else if (deleteCustomerTool)
                    {
                        EndCustomerStroke();
                        RemoveCustomer(lane, row);
                        ev.Use();
                        GUIUtility.ExitGUI(); // Rebuild the grid after the list size changes.
                    }
                    else if (customerPriorityPicking)
                    {
                        EndCustomerStroke();
                        boardPanel.TogglePriority(level, lane, row);
                        Repaint();
                    }
                    else if (ev.shift)
                    {
                        EndCustomerStroke();
                        RemoveCustomer(lane, row);
                        ev.Use();
                        GUIUtility.ExitGUI();
                    }
                    else
                    {
                        BeginCustomerStroke();
                        PaintCustomer(lane, row, paintColor);
                    }
                    ev.Use();
                }
                else if (!customerPriorityPicking && !deleteCustomerTool && !append && customerPainting && ev.type == EventType.MouseDrag && ev.button == 0)
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
        var animals = CookingBlastTileSpriteEditorPreview.GetConfiguredAnimalPrefabs();
        EditorGUILayout.LabelField("ANIMAL PREFABS (" + animals.Count + ")",
            EditorStyles.miniBoldLabel);
        var demandCounts = CookingBlastLevelEditing.DemandCounts(level);
        var tileCounts = CookingBlastLevelEditing.TileCounts(level);
        var playable = CookingBlastTileSpriteEditorPreview.GetConfiguredColors();

        foreach (CustomerView animal in animals)
        {
            FoodColor type = animal.Type;
            int demand = demandCounts.TryGetValue(type, out int d) ? d : 0;
            int tiles = tileCounts.TryGetValue(type, out int t) ? t : 0;
            Rect row = GUILayoutUtility.GetRect(166, 39);
            EditorGUI.DrawRect(row,
                !deleteCustomerTool && type == paintColor ?
                    new Color(.24f, .36f, .48f) : new Color(.16f, .18f, .22f));

            Rect iconRect = new Rect(row.x + 3, row.y + 3, 31, 31);
            if (!CookingBlastTileSpriteEditorPreview.DrawAnimal(iconRect, type))
                EditorGUI.DrawRect(iconRect, CookingSortPalette.Get(type));
            GUI.Label(new Rect(row.x + 38, row.y + 3, 122, 17),
                animal.name, EditorStyles.miniBoldLabel);
            string suffix = playable.Contains(type) ? "" : " (no Tile)";
            GUI.Label(new Rect(row.x + 38, row.y + 19, 123, 17),
                type + " " + demand + "/" + tiles + suffix, EditorStyles.miniLabel);

            if (GUI.Button(row, new GUIContent("",
                animal.name + " [" + type + "] - Demand " + demand +
                ", Tiles " + tiles), GUIStyle.none))
            {
                if (!playable.Contains(type))
                    continue; // Show unmatched prefab, but never author broken orders.
                EndCustomerStroke();
                paintColor = type;
                deleteCustomerTool = false;
                customerPriorityPicking = false;
            }
        }

        GUILayout.Space(6);
        Rect deleteRect = GUILayoutUtility.GetRect(166, 30);
        EditorGUI.DrawRect(deleteRect, deleteCustomerTool ?
            new Color(.50f, .22f, .23f) : new Color(.23f, .17f, .19f));
        EditorGUI.DrawRect(new Rect(deleteRect.x + 5, deleteRect.y + 5, 19, 19),
            new Color(.88f, .27f, .30f));
        var style = new GUIStyle(EditorStyles.miniBoldLabel);
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(deleteRect.x + 5, deleteRect.y + 5, 19, 19), "X", style);
        GUI.Label(new Rect(deleteRect.x + 30, deleteRect.y + 4, 131, 23),
            (deleteCustomerTool ? "• " : "") + "Delete Customer",
            EditorStyles.miniBoldLabel);
        if (GUI.Button(deleteRect, new GUIContent("",
            "Select Delete Customer and click an Animal in the grid."), GUIStyle.none))
        {
            EndCustomerStroke();
            deleteCustomerTool = true;
            customerPriorityPicking = false;
        }
        GUILayout.Space(5);
        EditorGUILayout.LabelField(deleteCustomerTool ?
            "Tool: Delete Customer" : "Animal: " + paintColor, EditorStyles.boldLabel);
    }
}
