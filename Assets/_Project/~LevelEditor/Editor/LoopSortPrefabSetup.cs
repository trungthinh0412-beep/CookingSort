using System.Collections.Generic;
using MagicSoft.LoopSort;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class LoopSortPrefabSetup
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/LoopSort";
    private const string CubePath = PrefabFolder + "/Cube.prefab";
    private const string ContainerPath = PrefabFolder + "/Container.prefab";
    private const string LoopPath = PrefabFolder + "/Loop.prefab";
    private const string BasePath = "Assets/_Project/Legacy/LoopSort/Levels/LevelBase/LevelBase.prefab";
    private const string DatabasePath = "Assets/_Project/Config/LoopColorDatabase.asset";

    [MenuItem("Tools/Legacy/Setup LoopSort Prefabs")]
    public static void SetupAll()
    {
        if (!EditorUtility.DisplayDialog(
                "Setup LoopSort Prefabs",
                "This rebuilds LevelBase and Level 1-3. Continue?",
                "Rebuild",
                "Cancel"))
        {
            return;
        }

        EnsureFolder(PrefabFolder);
        LoopColorDatabase database = EnsureColorDatabase();
        GameObject cubePrefab = CreateCubePrefab();
        GameObject containerPrefab = CreateContainerPrefab();
        GameObject loopPrefab = CreateLoopPrefab();

        CreateLevelPrefab(BasePath, "LevelBase", LoopLevelFactory.Create(1),
            cubePrefab, containerPrefab, loopPrefab, database);

        for (int levelIndex = 1; levelIndex <= 3; levelIndex++)
        {
            string path = $"Assets/_Project/Legacy/LoopSort/Levels/Level {levelIndex}.prefab";
            LoopLevelRuntimeData data = LoopLevelFactory.Create(levelIndex);
            CreateLevelPrefab(path, $"Level {levelIndex}", data,
                cubePrefab, containerPrefab, loopPrefab, database);
        }

        UpdateEditorSettings(cubePrefab, containerPrefab, loopPrefab, database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LevelAddressablesSetup.SyncLevelEntries();
        Debug.Log("[LoopSortPrefabSetup] LoopSort prefabs and Level 1-3 are ready.");
    }

    private static LoopColorDatabase EnsureColorDatabase()
    {
        LoopColorDatabase database =
            AssetDatabase.LoadAssetAtPath<LoopColorDatabase>(DatabasePath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<LoopColorDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        database.colors = new List<LoopColorEntry>();

        foreach (LoopCubeColor color in System.Enum.GetValues(typeof(LoopCubeColor)))
        {
            database.colors.Add(new LoopColorEntry
            {
                color = color,
                displayColor = LoopColorDatabase.GetFallbackColor(color)
            });
        }

        EditorUtility.SetDirty(database);
        return database;
    }

    private static GameObject CreateCubePrefab()
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = "Cube";
        Object.DestroyImmediate(root.GetComponent<Collider>());
        LoopCube cube = root.AddComponent<LoopCube>();
        cube.SetAuthoringColor(LoopCubeColor.Red);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CubePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateContainerPrefab()
    {
        GameObject root = new GameObject("Container");
        LoopContainer container = root.AddComponent<LoopContainer>();
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.45f, 0.2f);
        collider.size = new Vector3(1.55f, 1.15f, 2f);
        CreatePrimitiveChild(root.transform, "Cargo",
            new Vector3(0f, 0.45f, 0f), new Vector3(1.35f, 0.8f, 1.25f));
        CreatePrimitiveChild(root.transform, "Cab",
            new Vector3(0f, 0.3f, 0.88f), new Vector3(0.95f, 0.6f, 0.55f));

        GameObject entrance = new GameObject("EntrancePoint");
        entrance.transform.SetParent(root.transform, false);
        entrance.transform.localPosition = new Vector3(0f, 0.95f, -0.1f);

        GameObject counter = new GameObject("Counter");
        counter.transform.SetParent(root.transform, false);
        counter.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        TextMeshPro text = counter.AddComponent<TextMeshPro>();
        text.text = "0/8";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 3.4f;
        text.rectTransform.sizeDelta = new Vector2(2.8f, 0.7f);
        container.SetAuthoring(LoopCubeColor.Red, 8);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ContainerPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateLoopPrefab()
    {
        GameObject root = new GameObject("Loop");
        root.AddComponent<LoopPath>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, LoopPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreateLevelPrefab(
        string assetPath,
        string levelName,
        LoopLevelRuntimeData data,
        GameObject cubePrefab,
        GameObject containerPrefab,
        GameObject loopPrefab,
        LoopColorDatabase database)
    {
        GameObject root = new GameObject(levelName);
        root.AddComponent<Level>();
        LoopLevelAuthoring authoring = root.AddComponent<LoopLevelAuthoring>();

        GameObject loop = (GameObject)PrefabUtility.InstantiatePrefab(loopPrefab);
        loop.transform.SetParent(root.transform, false);
        LoopPath path = loop.GetComponent<LoopPath>();
        path.SetAuthoringShape(data.LoopWidth, data.LoopHeight,
            data.PathControlPointCount);

        GameObject cubeRootObject = new GameObject("CubeRoot");
        cubeRootObject.transform.SetParent(loop.transform, false);

        for (int i = 0; i < data.CubeSequence.Count; i++)
        {
            GameObject cubeObject = (GameObject)PrefabUtility.InstantiatePrefab(cubePrefab);
            cubeObject.transform.SetParent(cubeRootObject.transform, false);
            cubeObject.name = $"Cube {i + 1:000} - {data.CubeSequence[i]}";
            cubeObject.GetComponent<LoopCube>().SetAuthoringColor(data.CubeSequence[i]);
        }

        GameObject containerSystem = new GameObject("ContainerSystem");
        containerSystem.transform.SetParent(root.transform, false);
        GameObject activeSlot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        activeSlot.name = "ActiveSlot";
        activeSlot.transform.SetParent(containerSystem.transform, false);
        activeSlot.transform.position = new Vector3(
            0f, -0.04f, -data.LoopHeight * 0.5f - 1.05f);
        activeSlot.transform.localScale = new Vector3(1.9f, 0.06f, 2.3f);
        Object.DestroyImmediate(activeSlot.GetComponent<Collider>());

        GameObject containerRootObject = new GameObject("ContainerRoot");
        containerRootObject.transform.SetParent(containerSystem.transform, false);
        float spacing = Mathf.Min(2.15f,
            data.LoopWidth / Mathf.Max(1, data.Containers.Count));
        float startX = -spacing * (data.Containers.Count - 1) * 0.5f;
        float waitingZ = activeSlot.transform.position.z - 2.05f;

        for (int i = 0; i < data.Containers.Count; i++)
        {
            LoopContainerLevelData containerData = data.Containers[i];
            GameObject containerObject =
                (GameObject)PrefabUtility.InstantiatePrefab(containerPrefab);
            containerObject.transform.SetParent(containerRootObject.transform, false);
            containerObject.transform.position =
                new Vector3(startX + spacing * i, 0f, waitingZ);
            containerObject.transform.localScale =
                Vector3.one * (data.Containers.Count >= 5 ? 0.82f : 0.95f);
            containerObject.name = $"Container {i + 1:00} - {containerData.color}";
            containerObject.GetComponent<LoopContainer>().SetAuthoring(
                containerData.color, containerData.capacity);
        }

        authoring.AssignReferences(path, cubeRootObject.transform,
            containerRootObject.transform, activeSlot.transform, database);

        SerializedObject serialized = new SerializedObject(authoring);
        serialized.FindProperty("loopSpeed").floatValue = data.LoopSpeed;
        serialized.FindProperty("cubeSpacing").floatValue = data.CubeSpacing;
        serialized.FindProperty("gapCloseSpeed").floatValue = data.GapCloseSpeed;
        serialized.FindProperty("cubeScale").floatValue = data.CubeScale;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        authoring.RefreshPreview();

        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        Object.DestroyImmediate(root);
    }

    private static void UpdateEditorSettings(
        GameObject cubePrefab,
        GameObject containerPrefab,
        GameObject loopPrefab,
        LoopColorDatabase database)
    {
        LevelEditorSetting settings = Resources.Load<LevelEditorSetting>(
            "LevelEditorSetting");

        if (settings == null)
            return;

        settings.levelBasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
        settings.cubePrefab = cubePrefab;
        settings.containerPrefab = containerPrefab;
        settings.loopPrefab = loopPrefab;
        settings.colorDatabase = database;
        EditorUtility.SetDirty(settings);
    }

    private static void CreatePrimitiveChild(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale)
    {
        GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
        child.name = objectName;
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localScale = localScale;
        Object.DestroyImmediate(child.GetComponent<Collider>());
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
