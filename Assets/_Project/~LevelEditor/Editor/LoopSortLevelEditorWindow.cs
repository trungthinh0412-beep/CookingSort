using System;
using System.IO;
using MagicSoft.LoopSort;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class LoopSortLevelEditorWindow : EditorWindow
{
    private enum EditMode
    {
        Select,
        AddCube,
        DeleteCube,
        AddContainer,
        DeleteContainer
    }

    private LevelEditorSetting _settings;
    private Level _level;
    private LoopLevelAuthoring _authoring;
    private EditMode _mode;
    private LoopCubeColor _selectedColor;
    private int _containerCapacity = 8;
    private int _newLevelIndex = 1;
    private Vector2 _scroll;
    private string _status;
    private MessageType _statusType = MessageType.Info;

    [MenuItem("Tools/Legacy/Loop Sort Editor")]
    public static void OpenWindow()
    {
        GetWindow<LoopSortLevelEditorWindow>("Loop Sort Level Editor");
    }

    [MenuItem("CONTEXT/Level/Open Loop Sort Editor")]
    private static void OpenFromLevelContext(MenuCommand command)
    {
        LoopSortLevelEditorWindow window =
            GetWindow<LoopSortLevelEditorWindow>("Loop Sort Level Editor");
        window.SetLevel(command.context as Level);
    }

    private void OnEnable()
    {
        _settings = Resources.Load<LevelEditorSetting>("LevelEditorSetting");
        minSize = _settings != null
            ? _settings.windowMinSize
            : new Vector2(440f, 560f);
        SceneView.duringSceneGui += DuringSceneGUI;
        Selection.selectionChanged += OnSelectionChanged;
        ResolveLevelFromContext();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawHeader();
        EditorGUILayout.Space(8f);
        DrawCreateLevel();
        EditorGUILayout.Space(8f);

        if (_authoring == null)
        {
            EditorGUILayout.HelpBox(
                "Open a LoopSort level prefab or select a Level containing LoopLevelAuthoring.",
                MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawLevelSettings();
        EditorGUILayout.Space(8f);
        DrawPalette();
        EditorGUILayout.Space(8f);
        DrawModes();
        EditorGUILayout.Space(8f);
        DrawTools();

        if (!string.IsNullOrEmpty(_status))
            EditorGUILayout.HelpBox(_status, _statusType);

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("LOOP SORT PREFAB LEVEL EDITOR", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        Level selected = (Level)EditorGUILayout.ObjectField(
            "Current Level", _level, typeof(Level), true);

        if (EditorGUI.EndChangeCheck())
            SetLevel(selected);

        if (_level != null)
            EditorGUILayout.LabelField("Editing", GetEditingAssetPath(_level.gameObject));
    }

    private void DrawCreateLevel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Level Prefab", EditorStyles.boldLabel);
        _newLevelIndex = Mathf.Max(1, EditorGUILayout.IntField("Level Number", _newLevelIndex));

        using (new EditorGUI.DisabledScope(
                   _settings == null || _settings.levelBasePrefab == null))
        {
            if (GUILayout.Button("Create Level From LevelBase"))
                CreateLevelFromBase();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawLevelSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Runtime Settings", EditorStyles.boldLabel);
        SerializedObject serialized = new SerializedObject(_authoring);
        serialized.Update();
        EditorGUILayout.PropertyField(serialized.FindProperty("loopPath"));
        EditorGUILayout.PropertyField(serialized.FindProperty("cubeRoot"));
        EditorGUILayout.PropertyField(serialized.FindProperty("containerRoot"));
        EditorGUILayout.PropertyField(serialized.FindProperty("activeSlot"));
        EditorGUILayout.PropertyField(serialized.FindProperty("colorDatabase"));
        EditorGUILayout.PropertyField(serialized.FindProperty("loopSpeed"));
        EditorGUILayout.PropertyField(serialized.FindProperty("cubeSpacing"));
        EditorGUILayout.PropertyField(serialized.FindProperty("gapCloseSpeed"));
        EditorGUILayout.PropertyField(serialized.FindProperty("cubeScale"));

        if (serialized.ApplyModifiedProperties())
            MarkChanged("Edit LoopSort Level Settings");

        EditorGUILayout.EndVertical();
    }

    private void DrawPalette()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
        Array colors = Enum.GetValues(typeof(LoopCubeColor));
        const int columns = 3;

        for (int row = 0; row < Mathf.CeilToInt(colors.Length / (float)columns); row++)
        {
            EditorGUILayout.BeginHorizontal();

            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;

                if (index >= colors.Length)
                {
                    GUILayout.FlexibleSpace();
                    continue;
                }

                LoopCubeColor color = (LoopCubeColor)colors.GetValue(index);
                Color old = GUI.backgroundColor;
                GUI.backgroundColor = color == _selectedColor
                    ? LoopColorDatabase.GetFallbackColor(color)
                    : old;

                if (GUILayout.Button(color.ToString(), GUILayout.Height(28f)))
                    _selectedColor = color;

                GUI.backgroundColor = old;
            }

            EditorGUILayout.EndHorizontal();
        }

        _containerCapacity = Mathf.Max(
            1,
            EditorGUILayout.IntField("New Container Capacity", _containerCapacity));
        EditorGUILayout.EndVertical();
    }

    private void DrawModes()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Scene Editing Mode", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawModeButton("Select", EditMode.Select);
        DrawModeButton("+ Cube", EditMode.AddCube);
        DrawModeButton("- Cube", EditMode.DeleteCube);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        DrawModeButton("+ Container", EditMode.AddContainer);
        DrawModeButton("- Container", EditMode.DeleteContainer);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            _mode == EditMode.AddCube
                ? "Click Scene view to append a Cube prefab. Hierarchy order is loop order."
                : _mode == EditMode.AddContainer
                    ? "Click the ground to place a Container prefab in its waiting position."
                    : "Use the selected mode directly in Scene view.",
            MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void DrawModeButton(string label, EditMode mode)
    {
        Color old = GUI.backgroundColor;

        if (_mode == mode)
            GUI.backgroundColor = new Color(0.35f, 0.8f, 0.55f);

        if (GUILayout.Button(label, GUILayout.Height(30f)))
            _mode = mode;

        GUI.backgroundColor = old;
    }

    private void DrawTools()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            "Cube Prefab Instances", _authoring.GetOrderedCubes().Count.ToString());
        EditorGUILayout.LabelField(
            "Container Prefab Instances", _authoring.GetOrderedContainers().Count.ToString());

        if (GUILayout.Button("Refresh Prefab Preview"))
        {
            Undo.RecordObjects(
                _authoring.GetComponentsInChildren<Transform>(true),
                "Refresh LoopSort Preview");
            _authoring.RefreshPreview();
            MarkChanged("Refresh LoopSort Preview");
        }

        if (GUILayout.Button("Validate Level"))
        {
            bool valid = _authoring.TryValidate(out string error);
            SetStatus(valid ? "Level is valid." : error,
                valid ? MessageType.Info : MessageType.Error);
        }

        EditorGUILayout.EndVertical();
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (_authoring == null || _mode == EditMode.Select)
            return;

        Event current = Event.current;

        if (current.type != EventType.MouseDown || current.button != 0 || current.alt)
            return;

        if (_mode == EditMode.AddCube)
            AddCube();
        else if (_mode == EditMode.AddContainer &&
                 TryGetGroundPosition(current.mousePosition, out Vector3 position))
            AddContainer(position);
        else if (_mode == EditMode.DeleteCube)
            DeletePicked<LoopCube>(current.mousePosition);
        else if (_mode == EditMode.DeleteContainer)
            DeletePicked<LoopContainer>(current.mousePosition);

        current.Use();
        Repaint();
    }

    private void AddCube()
    {
        if (_settings == null || _settings.cubePrefab == null ||
            _authoring.CubeRoot == null)
        {
            SetStatus("Cube prefab or CubeRoot is missing.", MessageType.Error);
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(
            _settings.cubePrefab,
            _authoring.CubeRoot) as GameObject;

        if (instance == null)
            return;

        Undo.RegisterCreatedObjectUndo(instance, "Add Loop Cube");
        LoopCube cube = instance.GetComponentInChildren<LoopCube>(true);

        if (cube != null)
            cube.SetAuthoringColor(_selectedColor);

        instance.name = $"Cube {_authoring.CubeRoot.childCount:000} - {_selectedColor}";
        _authoring.RefreshPreview();
        MarkChanged("Add Loop Cube");
    }

    private void AddContainer(Vector3 position)
    {
        if (_settings == null || _settings.containerPrefab == null ||
            _authoring.ContainerRoot == null)
        {
            SetStatus("Container prefab or ContainerRoot is missing.", MessageType.Error);
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(
            _settings.containerPrefab,
            _authoring.ContainerRoot) as GameObject;

        if (instance == null)
            return;

        Undo.RegisterCreatedObjectUndo(instance, "Add Loop Container");
        instance.transform.position = position;
        LoopContainer container = instance.GetComponentInChildren<LoopContainer>(true);

        if (container != null)
            container.SetAuthoring(_selectedColor, _containerCapacity);

        instance.name = $"Container {_authoring.ContainerRoot.childCount:00} - {_selectedColor}";
        MarkChanged("Add Loop Container");
    }

    private void DeletePicked<T>(Vector2 mousePosition) where T : Component
    {
        GameObject picked = HandleUtility.PickGameObject(mousePosition, false);
        T component = picked != null ? picked.GetComponentInParent<T>() : null;

        if (component == null || !component.transform.IsChildOf(_authoring.transform))
            return;

        Undo.DestroyObjectImmediate(component.gameObject);
        _authoring.RefreshPreview();
        MarkChanged($"Delete {typeof(T).Name}");
    }

    private void CreateLevelFromBase()
    {
        string directory = _settings.levelDirectory.Replace('\\', '/');
        string destination = $"{directory}/Level {_newLevelIndex}.prefab";

        if (File.Exists(destination))
        {
            SetStatus($"{destination} already exists.", MessageType.Warning);
            return;
        }

        string source = AssetDatabase.GetAssetPath(_settings.levelBasePrefab);

        if (!AssetDatabase.CopyAsset(source, destination))
        {
            SetStatus("Could not copy LevelBase prefab.", MessageType.Error);
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(destination);
        Selection.activeObject = prefab;
        AssetDatabase.OpenAsset(prefab);
        SetStatus($"Created Level {_newLevelIndex}.prefab. Addressables syncs automatically.", MessageType.Info);
    }

    private static bool TryGetGroundPosition(Vector2 guiPosition, out Vector3 worldPosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        Plane plane = new Plane(Vector3.up, Vector3.zero);

        if (plane.Raycast(ray, out float distance))
        {
            worldPosition = ray.GetPoint(distance);
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    private void MarkChanged(string changeName)
    {
        if (_authoring == null)
            return;

        EditorUtility.SetDirty(_authoring);
        PrefabUtility.RecordPrefabInstancePropertyModifications(_authoring);

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(_authoring.gameObject.scene);

        SceneView.RepaintAll();
        SetStatus(changeName, MessageType.Info);
    }

    private void OnSelectionChanged()
    {
        Level selected = FindLevelFromSelection();

        if (selected != null)
            SetLevel(selected);
    }

    private void ResolveLevelFromContext()
    {
        Level selected = FindLevelFromSelection();

        if (selected == null)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            selected = stage != null
                ? stage.prefabContentsRoot.GetComponentInChildren<Level>(true)
                : null;
        }

        SetLevel(selected);
    }

    private void SetLevel(Level level)
    {
        _level = level;
        _authoring = level != null
            ? level.GetComponent<LoopLevelAuthoring>()
            : null;
        Repaint();
    }

    private static Level FindLevelFromSelection()
    {
        GameObject selected = Selection.activeGameObject;
        return selected != null ? selected.GetComponentInParent<Level>() : null;
    }

    private static string GetEditingAssetPath(GameObject target)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();

        if (stage != null)
            return stage.assetPath;

        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
        return string.IsNullOrEmpty(path) ? target.scene.path : path;
    }

    private void SetStatus(string message, MessageType type)
    {
        _status = message;
        _statusType = type;
        Repaint();
    }
}
