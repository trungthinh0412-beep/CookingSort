using System;
using CookingSort2D;
using UnityEditor;
using UnityEngine;

// Editor-only presentation. Level data remains on CookingBlastLevel.
internal sealed class CookingBlastBoardPanel
{
    private int selectedColumn;
    private bool showLists;
    private bool showRandom;
    private int strokeGroup = -1;
    private bool painting;
    private int lastColumn = -1;
    private int lastRow = -1;

    private Vector2 gridScroll;
    private static readonly FoodColor[] Colors = (FoodColor[])Enum.GetValues(typeof(FoodColor));

    internal void Draw(CookingBlastLevel level, ref FoodColor brush, ref int columns, ref int rows,
        ref int mask, ref int seed, float width)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("BOARD DESIGNER", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Choose a color • Drag to paint • Right-click to pick • Ctrl+Z to undo", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.labelWidth = 60;
                columns = Mathf.Clamp(EditorGUILayout.IntField("Columns", columns), 1, 14);
                rows = Mathf.Clamp(EditorGUILayout.IntField("Rows", rows), 1, 18);
                EditorGUIUtility.labelWidth = 0;
                if (GUILayout.Button("Resize", GUILayout.Width(75)))
                    CookingBlastLevelEditing.ResizeBoard(level, columns, rows, brush);
            }
            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(170)))
                {
                    EditorGUILayout.LabelField("COLOR LIST", EditorStyles.miniBoldLabel);
                    var counts = CookingBlastLevelEditing.TileCounts(level);
                    var demands = CookingBlastLevelEditing.DemandCounts(level);
                    foreach (var color in Colors)
                    {
                        int count = counts.TryGetValue(color, out var n) ? n : 0;
                        int demand = demands.TryGetValue(color, out var d) ? d : 0;
                        Rect rect = GUILayoutUtility.GetRect(166, 29);
                        EditorGUI.DrawRect(rect, color == brush ? new Color(.24f,.36f,.48f) : new Color(.16f,.18f,.22f));
                        EditorGUI.DrawRect(new Rect(rect.x+5, rect.y+5, 19, 19), CookingSortPalette.Get(color));
                        GUI.Label(new Rect(rect.x+30,rect.y+4,88,22), (color == brush ? "• " : "") + color);
                        GUI.Label(new Rect(rect.xMax-44,rect.y+4,40,22), count.ToString(), EditorStyles.boldLabel);
                        if (GUI.Button(rect, new GUIContent("", color + ": " + count + " tiles / " + demand + " demand"), GUIStyle.none))
                            brush = color;
                    }
                    GUILayout.Space(6);
                    EditorGUILayout.LabelField("Brush: " + brush, EditorStyles.boldLabel);
                    if (GUILayout.Button("Fill board", GUILayout.Height(26)))
                        CookingBlastLevelEditing.FillBoard(level, brush);
                    EditorGUILayout.HelpBox("Highlighted bottom row is served first. Counts update as you paint.", MessageType.None);
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

            showLists = EditorGUILayout.Foldout(showLists, "Column color lists — bottom to top", true);
            if (showLists) DrawColumnList(level, brush);
            showRandom = EditorGUILayout.Foldout(showRandom, "Random generation", true);
            if (showRandom)
            {
                mask = EditorGUILayout.MaskField("Allowed colors", mask, Enum.GetNames(typeof(FoodColor)));
                seed = EditorGUILayout.IntField("Seed", seed);
                using (new EditorGUI.DisabledScope(mask == 0))
                if (GUILayout.Button("Randomize board"))
                {
                    var palette = new System.Collections.Generic.List<FoodColor>();
                    foreach (var color in Colors)
                        if ((mask & (1 << (int)color)) != 0) palette.Add(color);
                    CookingBlastLevelEditing.RandomizeBoard(level, palette, seed);
                }
            }
        }
        if (Event.current.rawType == EventType.MouseUp) EndStroke();
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
            Color color = CookingSortPalette.Get(data.bottomToTop[r]);
            if (r == 0) EditorGUI.DrawRect(new Rect(cell.x-1,cell.y-1,cell.width+2,cell.height+2), new Color(.65f,.85f,1));
            EditorGUI.DrawRect(cell, Color.Lerp(color, Color.black, .25f));
            EditorGUI.DrawRect(new Rect(cell.x+2,cell.y+2,cell.width-4,cell.height-5), color);
            EditorGUI.DrawRect(new Rect(cell.x+3,cell.y+2,cell.width-6,2), Color.Lerp(color, Color.white,.25f));
            if (size >= 27)
            {
                var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                style.normal.textColor = color.grayscale > .6f ? Color.black : Color.white;
                GUI.Label(cell, CookingSortPalette.ShortName(data.bottomToTop[r]), style);
            }
            if (!GUI.enabled || !cell.Contains(ev.mousePosition)) continue;
            if (ev.type == EventType.MouseDown && ev.button == 1)
            {
                brush = data.bottomToTop[r];
                ev.Use();
            }
            else if ((ev.type == EventType.MouseDown || (ev.type == EventType.MouseDrag && painting)) && ev.button == 0)
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
                EditorGUI.DrawRect(swatch, CookingSortPalette.Get((FoodColor)list.GetArrayElementAtIndex(i).enumValueIndex));
                EditorGUILayout.PropertyField(list.GetArrayElementAtIndex(i),GUIContent.none);
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
}
