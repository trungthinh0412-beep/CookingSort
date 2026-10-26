using System;
using CookingSort2D;
using UnityEditor;
using UnityEngine;

// Editor-only presentation. Level data remains on CookingBlastLevel.
[Serializable]
internal sealed class CookingBlastBoardPanel
{
    private int selectedColumn;
    private bool showLists;
    
    
    
    [SerializeField] private System.Collections.Generic.List<Vector2Int> chosenPriority = new System.Collections.Generic.List<Vector2Int>();
    [SerializeField] private string priorityLevelKey;
    [SerializeField] private int manualPriorityCount;
    [SerializeField] private bool showAdvancedFillOptions;
    [SerializeField] private int priorityGroupSize = 3;
    [SerializeField] private int animalOrderLayout;
    
    [SerializeField] private int organicStrength = 72;
[SerializeField] private bool priorityInterleave = true;
    private string fillStatus;
    private MessageType fillStatusType;
    private int strokeGroup = -1;
    
    private bool bucketMode;
    [SerializeField] private bool cluster40Mode;
private bool painting;
    private int lastColumn = -1;
    private int lastRow = -1;

    private Vector2 gridScroll;
    private static FoodColor[] Colors
    {
        get
        {
            var colors = CookingBlastTileFromImageWindow.GetAvailableTilePalette();
            if (!colors.Contains(FoodColor.Neutral)) colors.Add(FoodColor.Neutral);
            return colors.ToArray();
        }
    }

internal void Draw(CookingBlastLevel level, ref FoodColor brush, ref int columns, ref int rows,
        ref int mask, ref int seed, float width, int laneCount, int minDemand, int maxDemand)
    {
        CookingBlastLevelEditing.NormalizeCluster40Sizes(level);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("BOARD DESIGNER", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Fill Tile follows Animal order automatically.", EditorStyles.miniLabel);
            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                seed = EditorGUILayout.IntField("Fill Order Seed", seed);
                if (GUILayout.Button("New Seed", GUILayout.Width(90)))
                    seed = UnityEngine.Random.Range(1, int.MaxValue);
            }
            DrawBoardFillButton(level, ref seed);
            if (GUILayout.Button(new GUIContent("Import From Image...",
                "Open pixel-art-to-Tile preview. Apply only after reviewing the sampled colors."),
                GUILayout.Height(31)))
            {
                EndStroke();
                CookingBlastTileFromImageWindow.Open(level);
                GUIUtility.ExitGUI();
            }
            GUILayout.Space(8);

            int currentTool = cluster40Mode ? 2 : bucketMode ? 1 : 0;
            int tool = GUILayout.Toolbar(currentTool,
                new[] { "Brush  •  Tiles", "Bucket  •  Region", "Cluster 40  •  2x2" },
                GUILayout.Height(28), GUILayout.Width(Mathf.Min(610f, Mathf.Max(330f, width - 38f))));
            bool nextBucketMode = tool == 1;
            bool nextClusterMode = tool == 2;
            if (nextBucketMode != bucketMode || nextClusterMode != cluster40Mode)
            {
                EndStroke();
                bucketMode = nextBucketMode;
                cluster40Mode = nextClusterMode;
            }
            if (cluster40Mode)
                EditorGUILayout.HelpBox("Click any cell to use it as the bottom-left corner of a 2 x 2 Cluster with value 40. The cell to its right and the two cells above are included. Click an existing Cluster to remove it. The current Brush color is used.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                float oldWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 60;
                columns = Mathf.Clamp(EditorGUILayout.IntField("Columns", columns), 1, 64);
                rows = Mathf.Clamp(EditorGUILayout.IntField("Rows", rows), 1, 64);
                EditorGUIUtility.labelWidth = oldWidth;
                if (GUILayout.Button("Resize", GUILayout.Width(75)))
                    CookingBlastLevelEditing.ResizeBoard(level, columns, rows, brush);
            }

            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(170)))
                {
                    EditorGUILayout.LabelField("TILES  /  NEEDED", EditorStyles.miniBoldLabel);
                    var counts = CookingBlastLevelEditing.TileCounts(level);
                    var demands = CookingBlastLevelEditing.DemandCounts(level);
                    string missingSpriteNames = "";
                    foreach (FoodColor type in Enum.GetValues(typeof(FoodColor)))
                    {
                        if (type == FoodColor.Neutral ||
                            !counts.TryGetValue(type, out int number) || number == 0 ||
                            CookingBlastTileSpriteEditorPreview.GetSprite(type) != null)
                            continue;
                        if (missingSpriteNames.Length > 0) missingSpriteNames += ", ";
                        missingSpriteNames += type.ToString();
                    }
                    if (missingSpriteNames.Length > 0)
                        EditorGUILayout.HelpBox("Missing custom Tile prefab for: " +
                            missingSpriteNames + ". Gameplay uses TileBase for those types.",
                            MessageType.Warning);
                    foreach (FoodColor color in Colors)
                    {
                        int count = counts.TryGetValue(color, out int n) ? n : 0;
                        int demand = demands.TryGetValue(color, out int d) ? d : 0;
                        Rect rect = GUILayoutUtility.GetRect(166, 29);
                        EditorGUI.DrawRect(rect, color == brush
                            ? new Color(.24f, .36f, .48f)
                            : new Color(.16f, .18f, .22f));
                        Rect tileSwatch = new Rect(rect.x + 5, rect.y + 5, 19, 19);
                        if (color == FoodColor.Neutral ||
                            !CookingBlastTileSpriteEditorPreview.Draw(tileSwatch, color))
                            EditorGUI.DrawRect(tileSwatch, CookingSortPalette.Get(color));
                        GUI.Label(new Rect(rect.x + 30, rect.y + 4, 88, 22),
                            (color == brush ? "• " : "") + color);
                        var valueStyle = new GUIStyle(EditorStyles.miniBoldLabel);
                        if (color != FoodColor.Neutral && count != demand)
                        {
                            valueStyle.normal.textColor = new Color(1f, .56f, .49f);
                            EditorGUI.DrawRect(new Rect(rect.xMax - 3, rect.y + 3, 3, rect.height - 6),
                                new Color(.91f, .33f, .31f));
                        }
                        else if (count > 0)
                            valueStyle.normal.textColor = new Color(.60f, .86f, .64f);
                        GUI.Label(new Rect(rect.xMax - 58, rect.y + 4, 54, 22),
                            color == FoodColor.Neutral ? count + "/--" : count + "/" + demand,
                            valueStyle);
                        if (GUI.Button(rect,
                            new GUIContent("", color + ": " + count + " tiles / " + demand + " demand"),
                            GUIStyle.none))
                            brush = color;
                    }
                    GUILayout.Space(6);
                    EditorGUILayout.LabelField("Brush: " + brush, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Right click picks color. Shift+click bucket fills. Ctrl+Z undoes.", EditorStyles.wordWrappedMiniLabel);
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("TOP", EditorStyles.centeredGreyMiniLabel);
                    gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.Height(420));
                    DrawGrid(level, ref brush, Mathf.Max(280, width - 230));
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.LabelField("BOTTOM / FIRST SERVED", EditorStyles.centeredGreyMiniLabel);
                }
            }
            showLists = EditorGUILayout.Foldout(showLists,
                "Advanced: Edit individual column colors", true);
            if (showLists) DrawColumnList(level, brush);
        }

        if (Event.current.rawType == EventType.MouseUp)
            EndStroke();
    }

    private void DrawGrid(CookingBlastLevel level, ref FoodColor brush, float available)
    {
        int columns = level.BoardColumns.Count;
        int rows = CookingBlastLevelEditing.MaxRows(level);
        if (columns == 0 || rows == 0)
        {
            EditorGUILayout.HelpBox("Set dimensions and press Resize to start.", MessageType.Info);
            return;
        }
        float size = Mathf.Clamp((available-32) / columns, 20, 38);
        Rect area = GUILayoutUtility.GetRect(columns*size+30, rows*size+30, GUILayout.ExpandWidth(false));
        EditorGUI.DrawRect(area, new Color(.07f,.09f,.15f));
        var ev = Event.current;
        for (int c=0; c<columns; c++)
        for (int r=0; r<rows; r++)
        {
            Rect cell = new Rect(area.x+26+c*size, area.y+4+(rows-1-r)*size, size-2, size-2);
            var data = level.BoardColumns[c];
            if (data == null || data.bottomToTop == null || r >= data.bottomToTop.Count)
                continue;
            if (level.IsClusterCell(c, r, out _)) continue;
            Color color = CookingSortPalette.Get(data.bottomToTop[r]);
            if (r == 0) EditorGUI.DrawRect(new Rect(cell.x-1,cell.y-1,cell.width+2,cell.height+2), new Color(.65f,.85f,1));
            EditorGUI.DrawRect(cell, new Color(.12f, .14f, .18f));
            if (data.bottomToTop[r] == FoodColor.Neutral ||
                !CookingBlastTileSpriteEditorPreview.Draw(cell, data.bottomToTop[r]))
            {
                EditorGUI.DrawRect(new Rect(cell.x + 2, cell.y + 2, cell.width - 4, cell.height - 4), color);
                if (size >= 27)
                {
                    var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                    style.normal.textColor = color.grayscale > .6f ? Color.black : Color.white;
                    GUI.Label(cell, CookingSortPalette.ShortName(data.bottomToTop[r]), style);
                }
            }
            if (!GUI.enabled || !cell.Contains(ev.mousePosition)) continue;
            if (ev.type == EventType.MouseDown && ev.button == 1)
            {
                brush = data.bottomToTop[r];
                ev.Use();
            }
            else if (cluster40Mode && ev.type == EventType.MouseDown && ev.button == 0)
            {
                EndStroke();
                CookingBlastLevelEditing.AddCluster40(level, c, r, brush, out fillStatus);
                fillStatusType = fillStatus.StartsWith("Placed") ? MessageType.Info : MessageType.Warning;
                ev.Use();
            }
            else if (!cluster40Mode && ev.type == EventType.MouseDown && ev.button == 0 &&
                     (bucketMode || ev.shift))
            {
                EndStroke();
                int filled = CookingBlastLevelEditing.FloodFillBoard(level, c, r, brush);
                fillStatus = filled > 0
                    ? "Bucket filled " + filled + " connected tile(s) with " + brush + ". Ctrl+Z to undo."
                    : "No tiles changed (this region already has " + brush + ").";
                fillStatusType = MessageType.Info;
                ev.Use();
            }
            else if (!cluster40Mode && !bucketMode &&
                     (ev.type == EventType.MouseDown || (ev.type == EventType.MouseDrag && painting)) &&
                     ev.button == 0)
            {
                if (!painting)
                {
                    Undo.IncrementCurrentGroup();
                    strokeGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Paint board stroke");
                    painting = true;
                }
                PaintLine(level, c, r, brush);
                ev.Use();
            }
        }
        if (level.BoardClusters != null)
        {
            foreach (BoardClusterData cluster in level.BoardClusters)
            {
                if (cluster == null) continue;
                Rect clusterRect = new Rect(
                    area.x + 26 + cluster.anchorColumn * size,
                    area.y + 4 + (rows - cluster.anchorRow - cluster.height) * size,
                    cluster.width * size - 2,
                    cluster.height * size - 2);
                EditorGUI.DrawRect(clusterRect, new Color(.10f, .11f, .15f));
                Rect inset = new Rect(clusterRect.x + 2, clusterRect.y + 2,
                    clusterRect.width - 4, clusterRect.height - 4);
                EditorGUI.DrawRect(inset, CookingSortPalette.Get(cluster.color));
                var clusterStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(size * .65f), 15, 26)
                };
                clusterStyle.normal.textColor = Color.white;
                GUI.Label(inset, cluster.value.ToString(), clusterStyle);

                if (!GUI.enabled || !clusterRect.Contains(ev.mousePosition)) continue;
                if (ev.type == EventType.MouseDown && ev.button == 1)
                {
                    brush = cluster.color;
                    ev.Use();
                }
                else if (cluster40Mode && ev.type == EventType.MouseDown && ev.button == 0)
                {
                    CookingBlastLevelEditing.RemoveClusterAt(level,
                        cluster.anchorColumn, cluster.anchorRow);
                    fillStatus = "Removed Cluster 40. Ctrl+Z to undo.";
                    fillStatusType = MessageType.Info;
                    ev.Use();
                    break;
                }
            }
        }
        for (int r=0; r<rows; r++)
            GUI.Label(new Rect(area.x,area.y+4+(rows-1-r)*size,24,size), (r+1).ToString(), EditorStyles.centeredGreyMiniLabel);
        for (int c=0;c<columns;c++)
            GUI.Label(new Rect(area.x+26+c*size,area.y+rows*size+5,size,20), (c+1).ToString(),EditorStyles.centeredGreyMiniLabel);
    }

    internal void EndStroke()
    {
        if (strokeGroup >= 0) Undo.CollapseUndoOperations(strokeGroup);
        strokeGroup = -1;
        painting = false;
        lastColumn = lastRow = -1;
    }

    private void DrawColumnList(CookingBlastLevel level, FoodColor brush)
    {
        if (level.BoardColumns.Count == 0) return;
        selectedColumn = Mathf.Clamp(selectedColumn, 0, level.BoardColumns.Count-1);
        selectedColumn = EditorGUILayout.IntSlider("Column", selectedColumn+1, 1, level.BoardColumns.Count)-1;
        var so = new SerializedObject(level);
        so.Update();
        var list = so.FindProperty("boardColumns").GetArrayElementAtIndex(selectedColumn).FindPropertyRelative("bottomToTop");
        int remove = -1;
        int move = 0;
        int moveIndex = -1;
        for (int i=0;i<list.arraySize;i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(i == 0 ? "1 • BOTTOM" : (i+1).ToString(), GUILayout.Width(95));
                Rect swatch = GUILayoutUtility.GetRect(20,20,GUILayout.Width(20));
                FoodColor swatchColor = (FoodColor)list.GetArrayElementAtIndex(i).enumValueIndex;
                if (!CookingBlastTileSpriteEditorPreview.Draw(swatch, swatchColor))
                    EditorGUI.DrawRect(swatch, CookingSortPalette.Get(swatchColor));
                FoodColor editedColor = CookingBlastTileSpriteEditorPreview.DrawAvailableColorPopup(
                    swatchColor, GUILayout.Width(150));
                if (swatchColor != editedColor)
                    list.GetArrayElementAtIndex(i).enumValueIndex = (int)editedColor;
                using(new EditorGUI.DisabledScope(i==0))
                    if(GUILayout.Button("↑",GUILayout.Width(25))) {moveIndex=i;move=-1;}
                using(new EditorGUI.DisabledScope(i==list.arraySize-1))
                    if(GUILayout.Button("↓",GUILayout.Width(25))) {moveIndex=i;move=1;}
                using(new EditorGUI.DisabledScope(list.arraySize<=1))
                    if(GUILayout.Button("×",GUILayout.Width(25))) remove=i;
            }
        }
        if(remove>=0) list.DeleteArrayElementAtIndex(remove);
        if(moveIndex>=0) list.MoveArrayElement(moveIndex,moveIndex+move);
        using(new EditorGUI.DisabledScope(list.arraySize>=18))
        if(GUILayout.Button("+ Add color at top"))
        {
            int index=list.arraySize++;
            list.GetArrayElementAtIndex(index).enumValueIndex=(int)brush;
        }
        if(so.ApplyModifiedProperties()) CookingBlastLevelEditing.Dirty(level);
    }


private void PaintLine(CookingBlastLevel level, int column, int row, FoodColor brush)
    {
        int startColumn = lastColumn < 0 ? column : lastColumn;
        int startRow = lastRow < 0 ? row : lastRow;
        int steps = Mathf.Max(Mathf.Abs(column-startColumn), Mathf.Abs(row-startRow));
        for (int i=0; i<=steps; i++)
        {
            float t = steps == 0 ? 1f : (float)i/steps;
            int c = Mathf.RoundToInt(Mathf.Lerp(startColumn,column,t));
            int r = Mathf.RoundToInt(Mathf.Lerp(startRow,row,t));
            if (CookingBlastLevelEditing.GetCell(level,c,r) != brush)
                CookingBlastLevelEditing.SetCell(level,c,r,brush);
        }
        lastColumn=column;
        lastRow=row;
    }





internal void DrawGridPriorityTools(CookingBlastLevel level, int seed)
    {
        EnsurePriority(level);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("CUSTOMER FILL ORDER", EditorStyles.boldLabel,
                    GUILayout.Width(190));
                GUILayout.FlexibleSpace();
                GUILayout.Label(manualPriorityCount + " picked / " + chosenPriority.Count,
                    EditorStyles.miniLabel, GUILayout.Width(105));
            }

            // Live preview of the order selected by clicking the Customer grid.
            if (chosenPriority.Count > 0)
            {
                string order = "";
                int shown = Mathf.Min(chosenPriority.Count, 7);
                for (int i = 0; i < shown; i++)
                {
                    Vector2Int id = chosenPriority[i];
                    var c = level.CustomerLanes[id.x].frontToBack[id.y];
                    if (i > 0) order += "  >  ";
                    order += (i + 1) + ":" + c.color;
                }
                if (chosenPriority.Count > shown) order += "  >  ...";
                EditorGUILayout.LabelField(order, EditorStyles.wordWrappedMiniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(manualPriorityCount == 0))
                {
                    if (GUILayout.Button("Undo Pick"))
                    {
                        manualPriorityCount--;
                        GUIUtility.ExitGUI();
                    }
                }
                if (GUILayout.Button("Reset Order"))
                {
                    ResetPriority(level);
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(new GUIContent("Randomize Rest",
                    "Keep the manually picked customer priority; shuffle other customers with the Seed.")))
                {
                    var random = new System.Random(seed);
                    for (int i = chosenPriority.Count - 1; i > manualPriorityCount; i--)
                    {
                        int j = random.Next(manualPriorityCount, i + 1);
                        Vector2Int temp = chosenPriority[i];
                        chosenPriority[i] = chosenPriority[j];
                        chosenPriority[j] = temp;
                    }
                    fillStatus = "Other Customer priorities reshuffled. Manual picks preserved.";
                    fillStatusType = MessageType.Info;
                    GUIUtility.ExitGUI();
                }
            }

            showAdvancedFillOptions = EditorGUILayout.Foldout(showAdvancedFillOptions,
                "More options: Tile batch / alternating colors", true);
            if (showAdvancedFillOptions)
            {
                priorityGroupSize = EditorGUILayout.IntSlider(
                    new GUIContent("Tiles per batch",
                        "When alternating customers, use this number of tiles per customer each round."),
                    priorityGroupSize, 1, 12);
                priorityInterleave = EditorGUILayout.ToggleLeft(
                    new GUIContent("Alternate customer colors",
                        "If unchecked, fill all tiles for Customer #1, then #2. Organic Clusters follows the same order but grows connected groups."),
                    priorityInterleave);
            }
        }
    }


private void EnsurePriority(CookingBlastLevel level)
    {
        if (level == null) return;
        string key = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage()?.assetPath;
        if (string.IsNullOrEmpty(key))
            key = level.gameObject.name + ":" + level.LevelNumber;
        if (priorityLevelKey != key)
        {
            priorityLevelKey = key;
            chosenPriority.Clear();
            manualPriorityCount = 0;
        }

        var available = new System.Collections.Generic.List<Vector2Int>();
        int depth = 0;
        foreach (CustomerLaneData lane in level.CustomerLanes)
            if (lane != null && lane.frontToBack != null)
                depth = Mathf.Max(depth, lane.frontToBack.Count);
        for (int row = 0; row < depth; row++)
            for (int lane = 0; lane < level.CustomerLanes.Count; lane++)
                if (level.CustomerLanes[lane] != null &&
                    level.CustomerLanes[lane].frontToBack != null &&
                    row < level.CustomerLanes[lane].frontToBack.Count)
                    available.Add(new Vector2Int(lane, row));

        int remainingPins = 0;
        for (int i = 0; i < manualPriorityCount && i < chosenPriority.Count; i++)
            if (available.Contains(chosenPriority[i])) remainingPins++;

        chosenPriority.RemoveAll(id => !available.Contains(id));
        manualPriorityCount = Mathf.Clamp(remainingPins, 0, chosenPriority.Count);
        foreach (Vector2Int id in available)
            if (!chosenPriority.Contains(id))
                chosenPriority.Add(id);
    }


internal void ResetPriority(CookingBlastLevel level)
    {
        chosenPriority.Clear();
        manualPriorityCount = 0;
        EnsurePriority(level);
    }


internal int GetPriorityRank(CookingBlastLevel level, int lane, int row)
    {
        EnsurePriority(level);
        int index = chosenPriority.IndexOf(new Vector2Int(lane, row));
        return index >= 0 && index < manualPriorityCount ? index + 1 : 0;
    }


internal void TogglePriority(CookingBlastLevel level, int lane, int row)
    {
        EnsurePriority(level);
        var id = new Vector2Int(lane, row);
        int index = chosenPriority.IndexOf(id);
        if (index < 0) return;

        chosenPriority.RemoveAt(index);
        if (index < manualPriorityCount)
        {
            // Clicking a pinned customer again unpins them.
            manualPriorityCount--;
            chosenPriority.Add(id);
        }
        else
        {
            // The next customer clicked gets the next priority number.
            chosenPriority.Insert(manualPriorityCount, id);
            manualPriorityCount++;
        }
    }


private void DrawBoardFillButton(CookingBlastLevel level, ref int seed)
    {
        EnsurePriority(level);
        int demand = 0;
        var requested = CookingBlastLevelEditing.DemandCounts(level);
        foreach (int count in requested.Values) demand += count;
        bool ready = level.TotalTileCount > 0 && demand == level.TotalTileCount;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("AUTO FILL  |  ANIMAL ORDER", EditorStyles.boldLabel);
            animalOrderLayout = EditorGUILayout.Popup("Layout", animalOrderLayout,
                new[] { "Color Patches", "Vertical", "Horizontal" });
            organicStrength = EditorGUILayout.IntSlider(
                new GUIContent("Patch Shape",
                    animalOrderLayout == 0
                        ? "Controls the size and dispersion of randomized color regions."
                        : "Controls variation in each vertical/horizontal color group around Tiles Per Turn."),
                organicStrength, 0, 100);

            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button(new GUIContent("Fill Tile",
                    "Fill the board using the Customer priority above; Ctrl+Z can undo."),
                    GUILayout.Height(38)))
                {
                    EndStroke();
                    seed = UnityEngine.Random.Range(1, int.MaxValue);
                    Undo.IncrementCurrentGroup();
                    int group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Auto Fill Cooking Blast Tiles");
                    string message;
                    bool success;
                    if (animalOrderLayout == 0)
                        success = CookingBlastLevelEditing.TryAutoFillOrganicByCustomerOrder(
                            level, priorityGroupSize, seed, organicStrength,
                            out message);
                    else
                        success = CookingBlastLevelEditing.TryAutoFillByAnimalOrder(
                            level, priorityGroupSize, animalOrderLayout == 1,
                            seed, organicStrength,
                            out message);
                    Undo.CollapseUndoOperations(group);
                    fillStatus = message;
                    fillStatusType = success ? MessageType.Info : MessageType.Warning;
                    GUIUtility.ExitGUI();
                }
            }

            if (!ready)
                EditorGUILayout.HelpBox(
                    "Tile count: " + level.TotalTileCount + "  |  Customer demand: " + demand +
                    ". These must match before filling.", MessageType.Warning);
            else
                EditorGUILayout.LabelField("Balanced demand  •  Ctrl+Z to undo  •  New Seed for another layout",
                    EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(fillStatus))
                EditorGUILayout.HelpBox(fillStatus, fillStatusType);
        }
    }
}
