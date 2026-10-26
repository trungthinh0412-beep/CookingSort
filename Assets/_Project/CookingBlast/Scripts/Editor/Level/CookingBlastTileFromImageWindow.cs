using System;
using System.Collections.Generic;
using CookingSort2D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Imports pixel-art color regions into the currently open CookingBlastLevel prefab.
// Previewing never modifies the level; Apply is Undo-supported.
public sealed class CookingBlastTileFromImageWindow : EditorWindow
{

    
    [SerializeField] private FoodColor tileBrush = FoodColor.Pink;
    [SerializeField] private bool bucketMode;
    [SerializeField] private int requestedCustomers = 30;
    [SerializeField] private int requestedLanes = 4;
    
    [SerializeField] private FoodColor customerBrush = FoodColor.Pink;
    [SerializeField] private int selectedCustomerLane = -1;
    [SerializeField] private int selectedCustomerIndex = -1;
    private Vector2 customerGridScroll;
[SerializeField] private bool showCustomerList = true;
    private readonly Stack<FoodColor[,]> previewUndo = new Stack<FoodColor[,]>();
    private bool isPainting;
    private int lastPaintedX = -1;
    private int lastPaintedY = -1;
[SerializeField] private Texture2D sourceImage;

    
    [SerializeField] private bool tilesApplied;
private CookingBlastLevel level;
    private FoodColor[,] preview; // [column, row], row 0 is bottom
    private int previewColumns;
    private int previewRows;
    private string status;
    private MessageType statusType = MessageType.Info;
    
    private Vector2 customerScroll;
private Vector2 scroll;

    [MenuItem("Tools/Cooking Blast/Tile From Image")]
    public static void OpenFromMenu()
    {
        Open(PrefabStageUtility.GetCurrentPrefabStage() != null
            ? PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot.GetComponent<CookingBlastLevel>()
            : null);
    }

    public static void Open(CookingBlastLevel editingLevel)
    {
        var window = GetWindow<CookingBlastTileFromImageWindow>("Tile From Image");
        window.minSize = new Vector2(440, 440);
        window.level = editingLevel;
        window.ClearPreview();
        window.LoadCurrentBoardPreview();
        window.showCustomerList = true;
        window.Show();
        window.Focus();
    }

private void OnGUI()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var active = stage == null ? null
            : stage.prefabContentsRoot.GetComponent<CookingBlastLevel>();
        if (active != level)
        {
            level = active;
            ClearPreview();
            LoadCurrentBoardPreview();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (preview != null && Event.current.type == EventType.KeyDown &&
            (Event.current.control || Event.current.command) &&
            Event.current.keyCode == KeyCode.Z)
        {
            UndoPreviewEdit();
            Event.current.Use();
        }
        GUILayout.Space(6);
        EditorGUILayout.LabelField("PIXEL IMAGE  →  TILE EDITOR  →  CUSTOMERS",
            EditorStyles.boldLabel);
        if (level == null)
        {
            EditorGUILayout.HelpBox("Open a Cooking Blast Level prefab in the Level Editor first.",
                MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.LabelField("Editing: " + level.gameObject.name,
            EditorStyles.miniLabel);
        EditorGUI.BeginChangeCheck();
        sourceImage = (Texture2D)EditorGUILayout.ObjectField(
            "Pixel Image", sourceImage, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck()) ClearPreview();

        EditorGUILayout.LabelField("Tile Palette: nearest configured Tile prefab sprite",
            EditorStyles.miniLabel);
        if (sourceImage != null)
            EditorGUILayout.LabelField("Image: " + sourceImage.width + " x " +
                sourceImage.height + " source pixels", EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(sourceImage == null))
            if (GUILayout.Button("Auto Detect Grid & Preview", GUILayout.Height(31)))
                BuildPreview();

        if (preview != null)
        {
            GUILayout.Space(7);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("EDIT TILES  •  " + previewColumns + " x " +
                    previewRows + " (" + previewColumns * previewRows + " Tiles)",
                    EditorStyles.boldLabel);
                DrawEditablePalette();

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool newBucket = GUILayout.Toolbar(bucketMode ? 1 : 0,
                        new[] { "Brush", "Bucket" }, GUILayout.Width(220),
                        GUILayout.Height(26)) == 1;
                    bucketMode = newBucket;
                    GUILayout.Space(7);
                    using (new EditorGUI.DisabledScope(previewUndo.Count == 0))
                        if (GUILayout.Button("Undo Edit", GUILayout.Width(92),
                            GUILayout.Height(26)))
                        {
                            UndoPreviewEdit();
                            GUIUtility.ExitGUI();
                        }
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.LabelField(
                    "Left-click / drag: paint  •  Right-click: pick color  •  Bucket: fill region",
                    EditorStyles.miniLabel);
                DrawPreviewGrid();
                DrawColorSummary();
            }

            EditorGUILayout.HelpBox(
                tilesApplied
                    ? "Tile edits also update the current Board. Use Save Prefab + Sync Levels in Level Editor."
                    : "Edit the preview first. Generate Tile applies the edited grid to the Board.",
                MessageType.Info);

            if (GUILayout.Button(tilesApplied
                    ? "Apply Edited Tiles To Board"
                    : "Generate Tile + Resize Board",
                    GUILayout.Height(35)))
            {
                ApplyPreview();
                GUIUtility.ExitGUI();
            }
        }

        GUILayout.Space(8);
        DrawCustomerSection();

        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, statusType);

        EditorGUILayout.EndScrollView();
        if (Event.current.rawType == EventType.MouseUp)
        {
            isPainting = false;
            lastPaintedX = -1;
            lastPaintedY = -1;
        }
    }

private void ClearPreview()
    {
        tilesApplied = false;
        preview = null;
        previewUndo.Clear();
        isPainting = false;
        status = "";
        Repaint();
    }

private void LoadCurrentBoardPreview()
    {
        if (level == null || level.BoardColumns == null ||
            level.BoardColumns.Count == 0) return;

        int columns = level.BoardColumns.Count;
        int rows = CookingBlastLevelEditing.MaxRows(level);
        if (rows == 0 || columns > 64 || rows > 64) return;

        previewColumns = columns;
        previewRows = rows;
        preview = new FoodColor[columns, rows];

        for (int column = 0; column < columns; column++)
        {
            var cells = level.BoardColumns[column]?.bottomToTop;
            for (int row = 0; row < rows; row++)
                preview[column, row] = cells != null && row < cells.Count
                    ? cells[row] : FoodColor.White;
        }

        previewUndo.Clear();
        tilesApplied = true;
        status = "Showing the current Level Board. Import an image to replace it, " +
                 "or edit the cells directly.";
        statusType = MessageType.Info;
        Repaint();
    }




private void BuildPreview()
    {
        ClearPreview();
        if (sourceImage == null || level == null)
        {
            status = "Choose an image and open a Cooking Blast Level prefab first.";
            statusType = MessageType.Warning;
            return;
        }

        // Match the color sampled from EACH pixel-art cell to the nearest
        // existing FoodColor in the game palette, regardless of the colors
        // currently used by this Board or its Customer Queue.
        List<FoodColor> palette = GetAvailableTilePalette();
        if (palette.Count == 0)
        {
            status = "No FoodColor types are defined.";
            statusType = MessageType.Warning;
            return;
        }

        Texture2D readable = null;
        bool disposable = false;
        try
        {
            readable = MakeReadable(sourceImage, out disposable);
            string details;
            if (!CookingBlastPixelGridDetector.TryRead(readable, palette,
                out preview, out details))
            {
                preview = null;
                status = details;
                statusType = MessageType.Warning;
                return;
            }
            previewColumns = preview.GetLength(0);
            previewRows = preview.GetLength(1);
            status = details + " Every image pixel mapped to nearest available Tile color.";
            statusType = MessageType.Info;
        }
        catch (Exception exception)
        {
            preview = null;
            status = "Cannot import this image: " + exception.Message;
            statusType = MessageType.Error;
        }
        finally
        {
            if (disposable && readable != null) DestroyImmediate(readable);
        }
    }

internal static List<FoodColor> GetAvailableTilePalette()
    {
        // Image import only uses tile types that have actual artwork prefabs.
        // Red/Cyan have no prefab yet, so do not generate those from an image.
        var colors = new List<FoodColor>();
        foreach (FoodColor color in Enum.GetValues(typeof(FoodColor)))
        {
            if (color == FoodColor.Neutral) continue;
            if (CookingBlastTileSpriteEditorPreview.GetSprite(color) != null)
                colors.Add(color);
        }
        return colors;
    }

private void DrawCurrentBoardPalette()
    {
        var colors = GetAvailableTilePalette();
        for (int offset = 0; offset < colors.Count; offset += 5)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = offset; i < Mathf.Min(offset + 5, colors.Count); i++)
                {
                    FoodColor color = colors[i];
                    Rect swatch = GUILayoutUtility.GetRect(12, 14, GUILayout.Width(12));
                    EditorGUI.DrawRect(swatch, CookingSortPalette.Get(color));
                    GUILayout.Label(color.ToString(), EditorStyles.miniLabel, GUILayout.Width(62));
                }
                GUILayout.FlexibleSpace();
            }
        }
    }

private void DrawEditablePalette()
    {
        var palette = GetAvailableTilePalette();
        for (int start = 0; start < palette.Count; start += 4)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = start; i < Mathf.Min(start + 4, palette.Count); i++)
                {
                    FoodColor color = palette[i];
                    Rect rect = GUILayoutUtility.GetRect(96, 27, GUILayout.Width(96));
                    EditorGUI.DrawRect(rect, color == tileBrush ?
                        new Color(.27f, .42f, .57f) : new Color(.16f, .18f, .21f));
                    Rect swatch = new Rect(rect.x + 4, rect.y + 4, 18, 18);
                    if (!CookingBlastTileSpriteEditorPreview.Draw(swatch, color))
                        EditorGUI.DrawRect(swatch, CookingSortPalette.Get(color));
                    GUI.Label(new Rect(rect.x + 26, rect.y + 5, 65, 20),
                        color.ToString(), EditorStyles.miniBoldLabel);
                    if (GUI.Button(rect, new GUIContent("",
                            "Select " + color + " for painting Tiles"), GUIStyle.none))
                        tileBrush = color;
                }
                GUILayout.FlexibleSpace();
            }
        }
    }




    private static Texture2D MakeReadable(Texture2D source, out bool disposable)
    {
        disposable = false;
        if (source.isReadable) return source;
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            disposable = true;
            return copy;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }







    private static Dictionary<FoodColor, int> CountColors(FoodColor[,] colors)
    {
        var counts = new Dictionary<FoodColor, int>();
        for (int x = 0; x < colors.GetLength(0); x++)
        for (int y = 0; y < colors.GetLength(1); y++)
        {
            FoodColor color = colors[x, y];
            if (!counts.ContainsKey(color)) counts[color] = 0;
            counts[color]++;
        }
        return counts;
    }

private void DrawPreviewGrid()
    {
        float available = Mathf.Max(330f, position.width - 72f);
        float size = Mathf.Clamp((available - 12f) / previewColumns, 10f, 30f);
        Rect area = GUILayoutUtility.GetRect(
            previewColumns * size + 2f, previewRows * size + 2f,
            GUILayout.ExpandWidth(false));
        EditorGUI.DrawRect(area, new Color(.09f, .10f, .13f));
        Event ev = Event.current;

        for (int col = 0; col < previewColumns; col++)
        for (int row = 0; row < previewRows; row++)
        {
            Rect cell = new Rect(area.x + col * size + 1,
                area.y + (previewRows - row - 1) * size + 1,
                size - 2, size - 2);
            EditorGUI.DrawRect(cell, new Color(.13f, .15f, .19f));
            if (!CookingBlastTileSpriteEditorPreview.Draw(cell, preview[col, row]))
                EditorGUI.DrawRect(cell, CookingSortPalette.Get(preview[col, row]));

            if (!cell.Contains(ev.mousePosition)) continue;

            if (ev.type == EventType.MouseDown && ev.button == 1)
            {
                tileBrush = preview[col, row];
                ev.Use();
                Repaint();
            }
            else if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                isPainting = true;
                lastPaintedX = -1;
                lastPaintedY = -1;
                PaintPreviewCell(col, row, bucketMode);
                ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && ev.button == 0 &&
                     isPainting && !bucketMode)
            {
                PaintPreviewCell(col, row, false);
                ev.Use();
            }
        }
    }

private void PaintPreviewCell(int column, int row, bool flood)
    {
        if (preview == null || column < 0 || row < 0 ||
            column >= previewColumns || row >= previewRows) return;
        if (!flood && lastPaintedX == column && lastPaintedY == row) return;
        lastPaintedX = column;
        lastPaintedY = row;
        FoodColor replaced = preview[column, row];
        if (replaced == tileBrush) return;

        FoodColor[,] before = (FoodColor[,])preview.Clone();
        var cells = new List<Vector2Int>();
        if (flood)
        {
            var visited = new HashSet<Vector2Int>();
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(column, row));
            while (stack.Count > 0)
            {
                Vector2Int pos = stack.Pop();
                if (pos.x < 0 || pos.x >= previewColumns ||
                    pos.y < 0 || pos.y >= previewRows ||
                    !visited.Add(pos) || preview[pos.x, pos.y] != replaced)
                    continue;
                cells.Add(pos);
                stack.Push(pos + Vector2Int.left);
                stack.Push(pos + Vector2Int.right);
                stack.Push(pos + Vector2Int.up);
                stack.Push(pos + Vector2Int.down);
            }
        }
        else cells.Add(new Vector2Int(column, row));

        if (cells.Count == 0) return;
        if (previewUndo.Count >= 100) previewUndo.Clear();
        previewUndo.Push(before);

        if (tilesApplied && level != null)
            Undo.RecordObject(level, flood ? "Bucket Edit Imported Tiles" :
                "Paint Imported Tile");

        foreach (Vector2Int pos in cells)
        {
            preview[pos.x, pos.y] = tileBrush;
            if (tilesApplied && level != null &&
                pos.x < level.BoardColumns.Count &&
                pos.y < level.BoardColumns[pos.x].bottomToTop.Count)
                level.BoardColumns[pos.x].bottomToTop[pos.y] = tileBrush;
        }
        if (tilesApplied) CookingBlastLevelEditing.Dirty(level);
        Repaint();
    }

private void UndoPreviewEdit()
    {
        if (preview == null || previewUndo.Count == 0) return;
        preview = previewUndo.Pop();
        if (tilesApplied && level != null &&
            level.BoardColumns.Count == previewColumns)
        {
            Undo.RecordObject(level, "Undo Imported Tile Edit");
            for (int col = 0; col < previewColumns; col++)
            for (int row = 0; row < previewRows; row++)
                level.BoardColumns[col].bottomToTop[row] = preview[col, row];
            CookingBlastLevelEditing.Dirty(level);
        }
        Repaint();
    }

private void DrawCustomerSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            int currentCount = 0;
            int totalDemand = 0;
            if (level.CustomerLanes != null)
                foreach (CustomerLaneData lane in level.CustomerLanes)
                {
                    if (lane == null || lane.frontToBack == null) continue;
                    currentCount += lane.frontToBack.Count;
                    foreach (CustomerOrderData order in lane.frontToBack)
                        if (order != null) totalDemand += order.demand;
                }

            EditorGUILayout.LabelField(
                "ANIMAL / CUSTOMER DESIGNER  |  " + currentCount + " Animals  •  " +
                totalDemand + "/" + level.TotalTileCount + " Demand",
                EditorStyles.boldLabel);

            requestedCustomers = Mathf.Clamp(
                EditorGUILayout.IntField(new GUIContent("Customer Count",
                    "Exact number of Customers to generate from Tile colors."),
                    requestedCustomers), 1, Mathf.Max(1, level.TotalTileCount));
            requestedLanes = EditorGUILayout.IntSlider("Customer Lanes",
                requestedLanes, 1, 8);

            int distinctColors = CookingBlastLevelEditing.TileCounts(level).Count;
            bool countValid = level.TotalTileCount > 0 &&
                requestedCustomers >= distinctColors &&
                requestedCustomers <= level.TotalTileCount;
            if (!countValid)
                EditorGUILayout.HelpBox(
                    "Customer Count must be between " + distinctColors + " and " +
                    level.TotalTileCount + " (at least one Customer per used Tile color).",
                    MessageType.Warning);
            if (preview != null && !tilesApplied)
                EditorGUILayout.HelpBox("Apply the image Tiles first before generating Customers.",
                    MessageType.Info);

            using (new EditorGUI.DisabledScope(!tilesApplied || !countValid))
                if (GUILayout.Button(
                    "Generate " + requestedCustomers + " Customers From Tiles",
                    GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("Replace Customer Queue?",
                        "Replace the existing Customer Queue with exactly " +
                        requestedCustomers + " Customers across up to " +
                        requestedLanes + " lanes? Ctrl+Z can undo.",
                        "Generate", "Cancel"))
                    {
                        GenerateCustomersFromTiles();
                        GUIUtility.ExitGUI();
                    }
                }

            GUILayout.Space(6);
            showCustomerList = EditorGUILayout.Foldout(showCustomerList,
                "ANIMAL GRID  •  FRONT / FIRST TO SELECT", true);
            if (showCustomerList)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Add Customer Color", GUILayout.Width(125));
                    customerBrush = CookingBlastTileSpriteEditorPreview.DrawAvailableColorPopup(
                        customerBrush, GUILayout.Width(150));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Select / Shift+Click Delete / + Add",
                        EditorStyles.miniLabel);
                }

                customerGridScroll = EditorGUILayout.BeginScrollView(
                    customerGridScroll, true, true, GUILayout.Height(315));
                DrawCustomerGrid();
                EditorGUILayout.EndScrollView();

                EditorGUILayout.LabelField("BACK / LAST TO SELECT",
                    EditorStyles.centeredGreyMiniLabel);
                DrawSelectedCustomerDetails();
            }
        }
    }

private void DrawCustomerGrid()
    {
        if (level.CustomerLanes == null || level.CustomerLanes.Count == 0)
        {
            EditorGUILayout.HelpBox("Generate Customers from Tiles to create the Customer Grid.",
                MessageType.Info);
            return;
        }

        int maxDepth = 0;
        for (int lane = 0; lane < level.CustomerLanes.Count; lane++)
        {
            var list = level.CustomerLanes[lane]?.frontToBack;
            if (list != null) maxDepth = Mathf.Max(maxDepth, list.Count);
        }

        const float size = 82f;
        int laneCount = level.CustomerLanes.Count;
        Rect area = GUILayoutUtility.GetRect(
            30f + laneCount * size,
            28f + (maxDepth + 1) * size,
            GUILayout.ExpandWidth(false));
        EditorGUI.DrawRect(area, new Color(.075f, .09f, .13f));
        Event ev = Event.current;

        for (int lane = 0; lane < laneCount; lane++)
        {
            var orders = level.CustomerLanes[lane]?.frontToBack;
            int count = orders == null ? 0 : orders.Count;
            GUI.Label(new Rect(area.x + 28 + lane * size,
                area.y + 2, size - 2, 21), "LANE " + (lane + 1),
                EditorStyles.centeredGreyMiniLabel);

            for (int row = 0; row <= count; row++)
            {
                bool append = row == count;
                Rect cell = new Rect(area.x + 29 + lane * size,
                    area.y + 26 + row * size, size - 4, size - 4);
                if (append)
                {
                    EditorGUI.DrawRect(cell, new Color(.19f, .23f, .28f));
                    GUI.Label(cell, "+ Add", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    CustomerOrderData order = orders[row];
                    if (order == null) continue;
                    Color tint = CookingSortPalette.Get(order.color);
                    bool selected = selectedCustomerLane == lane &&
                                    selectedCustomerIndex == row;
                    if (row == 0 || selected)
                        EditorGUI.DrawRect(new Rect(cell.x - 2, cell.y - 2,
                            cell.width + 4, cell.height + 4),
                            selected ? new Color(1f, .86f, .34f) :
                                new Color(.65f, .85f, 1f));
                    EditorGUI.DrawRect(cell, new Color(.13f, .15f, .19f));
                    bool hasAnimal = CookingBlastTileSpriteEditorPreview.DrawAnimal(cell, order.color);
                    if (!hasAnimal)
                        EditorGUI.DrawRect(new Rect(cell.x + 3, cell.y + 3,
                            cell.width - 6, cell.height - 6), tint);
                    var label = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                    label.fontStyle = FontStyle.Bold;
                    label.normal.textColor = Color.white;
                    if (hasAnimal)
                    {
                        Rect demandBadge = new Rect(cell.x + 10, cell.yMax - 20,
                            cell.width - 20, 17);
                        EditorGUI.DrawRect(demandBadge, new Color(0f, 0f, 0f, .75f));
                        GUI.Label(demandBadge, order.demand.ToString(), label);
                    }
                    else
                    {
                        label.normal.textColor = tint.grayscale > .55f ?
                            Color.black : Color.white;
                        GUI.Label(cell, CookingSortPalette.ShortName(order.color) +
                            "\n" + order.demand, label);
                    }
                }

                if (!cell.Contains(ev.mousePosition) ||
                    ev.type != EventType.MouseDown) continue;

                if (ev.button == 1 && !append)
                {
                    customerBrush = orders[row].color;
                    selectedCustomerLane = lane;
                    selectedCustomerIndex = row;
                    ev.Use();
                    Repaint();
                }
                else if (ev.button == 0)
                {
                    if (append)
                    {
                        if (orders != null)
                        {
                            Undo.RecordObject(level, "Add Image Tool Customer");
                            orders.Add(new CustomerOrderData(customerBrush, 1));
                            CookingBlastLevelEditing.Dirty(level);
                            selectedCustomerLane = lane;
                            selectedCustomerIndex = orders.Count - 1;
                        }
                    }
                    else if (ev.shift)
                    {
                        Undo.RecordObject(level, "Delete Image Tool Customer");
                        orders.RemoveAt(row);
                        CookingBlastLevelEditing.Dirty(level);
                        selectedCustomerLane = -1;
                        selectedCustomerIndex = -1;
                    }
                    else
                    {
                        selectedCustomerLane = lane;
                        selectedCustomerIndex = row;
                    }
                    ev.Use();
                    GUIUtility.ExitGUI();
                }
            }
        }

        for (int row = 0; row <= maxDepth; row++)
        {
            GUI.Label(new Rect(area.x + 1,
                area.y + 26 + row * size, 25, size - 3),
                row == 0 ? "F" : (row + 1).ToString(),
                EditorStyles.centeredGreyMiniLabel);
        }
    }

private void DrawSelectedCustomerDetails()
    {
        if (level.CustomerLanes == null ||
            selectedCustomerLane < 0 ||
            selectedCustomerLane >= level.CustomerLanes.Count)
            return;
        var customers = level.CustomerLanes[selectedCustomerLane]?.frontToBack;
        if (customers == null || selectedCustomerIndex < 0 ||
            selectedCustomerIndex >= customers.Count)
            return;

        CustomerOrderData selected = customers[selectedCustomerIndex];
        if (selected == null) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "Selected: Lane " + (selectedCustomerLane + 1) +
                "  •  Customer " + (selectedCustomerIndex + 1),
                EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                GUILayout.Label("Color", GUILayout.Width(45));
                FoodColor color = CookingBlastTileSpriteEditorPreview.DrawAvailableColorPopup(
                    selected.color, GUILayout.Width(145));
                int demand = Mathf.Max(1, EditorGUILayout.IntField(
                    "Demand", selected.demand, GUILayout.MaxWidth(190)));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(level, "Edit Image Tool Customer");
                    selected.color = color;
                    selected.demand = demand;
                    CookingBlastLevelEditing.Dirty(level);
                    customerBrush = color;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selectedCustomerIndex == 0))
                    if (GUILayout.Button("Move Up"))
                    {
                        Undo.RecordObject(level, "Move Customer Up");
                        int index = selectedCustomerIndex;
                        CustomerOrderData tmp = customers[index - 1];
                        customers[index - 1] = customers[index];
                        customers[index] = tmp;
                        selectedCustomerIndex--;
                        CookingBlastLevelEditing.Dirty(level);
                        GUIUtility.ExitGUI();
                    }

                using (new EditorGUI.DisabledScope(
                    selectedCustomerIndex == customers.Count - 1))
                    if (GUILayout.Button("Move Down"))
                    {
                        Undo.RecordObject(level, "Move Customer Down");
                        int index = selectedCustomerIndex;
                        CustomerOrderData tmp = customers[index + 1];
                        customers[index + 1] = customers[index];
                        customers[index] = tmp;
                        selectedCustomerIndex++;
                        CookingBlastLevelEditing.Dirty(level);
                        GUIUtility.ExitGUI();
                    }

                if (GUILayout.Button("Delete Customer"))
                {
                    Undo.RecordObject(level, "Remove Image Tool Customer");
                    customers.RemoveAt(selectedCustomerIndex);
                    selectedCustomerLane = -1;
                    selectedCustomerIndex = -1;
                    CookingBlastLevelEditing.Dirty(level);
                    GUIUtility.ExitGUI();
                }
            }
        }
    }






private void DrawColorSummary()
    {
        var counts = CountColors(preview);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Tile Colors", EditorStyles.miniBoldLabel);
            foreach (FoodColor color in Enum.GetValues(typeof(FoodColor)))
            {
                if (!counts.TryGetValue(color, out int count) || count == 0)
                    continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect rect = GUILayoutUtility.GetRect(13, 13, GUILayout.Width(13));
                    if (!CookingBlastTileSpriteEditorPreview.Draw(rect, color))
                        EditorGUI.DrawRect(rect, CookingSortPalette.Get(color));
                    GUILayout.Label(color + ": " + count);
                }
            }
        }
    }

private void ApplyPreview()
    {
        if (level == null || preview == null) return;

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null ||
            stage.prefabContentsRoot.GetComponent<CookingBlastLevel>() != level)
        {
            status = "Open the target Level prefab in Prefab Mode first.";
            statusType = MessageType.Error;
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate Cooking Blast Tiles From Image");
        CookingBlastLevelEditing.ResizeBoard(level,
            previewColumns, previewRows, preview[0, 0]);
        Undo.RecordObject(level, "Generate Cooking Blast Tiles From Image");
        for (int col = 0; col < previewColumns; col++)
        for (int row = 0; row < previewRows; row++)
            level.BoardColumns[col].bottomToTop[row] = preview[col, row];

        CookingBlastLevelEditing.Dirty(level);
        CookingBlastLevelEditorWindow.RefreshBoardSizeFromImport(level);
        Undo.CollapseUndoOperations(group);
        tilesApplied = true;
        status = "Generated " + (previewColumns * previewRows) +
                 " Tile colors. Customer Queue was not changed. " +
                 "Save Prefab + Sync Levels in the Level Editor.";
        statusType = MessageType.Info;
        Repaint();
    }

private void GenerateCustomersFromTiles()
    {
        if (level == null || !tilesApplied || preview == null) return;
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null ||
            stage.prefabContentsRoot.GetComponent<CookingBlastLevel>() != level)
        {
            status = "Open the same Level prefab in Prefab Mode.";
            statusType = MessageType.Error;
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate Exact Customer Count From Image Tiles");
        string resultMessage;
        bool generated = CookingBlastLevelEditing.TryGenerateExactCustomerCount(
            level, requestedCustomers, requestedLanes, out resultMessage);
        if (generated)
        {
            selectedCustomerLane = -1;
            selectedCustomerIndex = -1;
            CookingBlastLevelEditorWindow.RefreshCustomersFromImageImport(level);
        }
        Undo.CollapseUndoOperations(group);

        if (generated)
        {
            string reason;
            bool valid = level.TryValidate(out reason);
            status = resultMessage + (valid ?
                " Level validation passed." : " Validation warning: " + reason);
            statusType = valid ? MessageType.Info : MessageType.Warning;
        }
        else
        {
            status = resultMessage;
            statusType = MessageType.Warning;
        }
        Repaint();
    }

private static int EstimateCustomerCount(CookingBlastLevel level,
        int minimumDemand, int maximumDemand)
    {
        int total = 0;
        foreach (var pair in CookingBlastLevelEditing.TileCounts(level))
        {
            int remaining = pair.Value;
            bool toggle = false;
            while (remaining > 0)
            {
                int desired = toggle ? minimumDemand : maximumDemand;
                int demand = Mathf.Min(desired, remaining);
                if (remaining - demand > 0 && remaining - demand < minimumDemand)
                    demand = Mathf.Max(1, remaining - minimumDemand);
                remaining -= demand;
                total++;
                toggle = !toggle;
            }
        }
        return total;
    }


}
