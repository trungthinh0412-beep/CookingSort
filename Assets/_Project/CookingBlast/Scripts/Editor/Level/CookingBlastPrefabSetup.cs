using System;
using System.IO;
using CookingSort2D;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class CookingBlastPrefabSetup
{
    internal const string Root = "Assets/_Project/CookingBlast";
    internal const string LevelDirectory = Root + "/Prefabs/Level";
    internal const string BasePath = LevelDirectory + "/LevelBase/LevelBase.prefab";
    internal const string Level1Path = LevelDirectory + "/Level 1.prefab";
    
    internal const string BoardPrefabPath = Root + "/Prefabs/Board.prefab";
    internal const string SeatBarPrefabPath = Root + "/Prefabs/SeatBar.prefab";
    internal const string CustomerQueuePrefabPath = Root + "/Prefabs/CustomerQueue.prefab";
internal const string ConfigPath = Root + "/Config/CookingBlastLevelConfig.asset";

[MenuItem("Tools/Cooking Blast/Setup Level System")]
    internal static void SetupLevelSystem()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode first.");

        EnsureFolders();
        CreateConfig();
        CreateBase();
        CreateLevel1FromLegacy();
        EnsureAllLevelStructures();
        CookingBlastLevelEditorWindow.SyncPlayableLevels();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CookingBlast] Level prefab system is ready. Board, SeatBar and CustomerQueue live inside every level prefab.");
    }

    internal static void EnsureFolders()
    {
        EnsureFolder(Root);
        EnsureFolder(Root + "/Config");
        EnsureFolder(Root + "/Prefabs");
        EnsureFolder(LevelDirectory);
        EnsureFolder(LevelDirectory + "/LevelBase");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    internal static CookingBlastLevelConfig CreateConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<CookingBlastLevelConfig>(ConfigPath);
        if (config != null) return config;
        config = ScriptableObject.CreateInstance<CookingBlastLevelConfig>();
        AssetDatabase.CreateAsset(config, ConfigPath);
        EditorUtility.SetDirty(config);
        return config;
    }

internal static GameObject CreateBase()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
        if (existing == null || existing.GetComponent<CookingBlastLevel>() == null)
        {
            GameObject root = new GameObject("LevelBase");
            try
            {
                root.AddComponent<CookingBlastLevel>();
                PrefabUtility.SaveAsPrefabAsset(root, BasePath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
        EnsureLevelStructure(BasePath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
    }

internal static GameObject CreateLevel1FromLegacy()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(Level1Path);
        if (existing == null || existing.GetComponent<CookingBlastLevel>() == null)
        {
            GameObject root = new GameObject("Level 1");
            try
            {
                CookingBlastLevel level = root.AddComponent<CookingBlastLevel>();
                CreateFallbackData(level);
                PrefabUtility.SaveAsPrefabAsset(root, Level1Path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
        EnsureLevelStructure(Level1Path);
        return AssetDatabase.LoadAssetAtPath<GameObject>(Level1Path);
    }

internal static GameObject CreateEmptyLevel(int number)
    {
        EnsureFolders();
        CreateBase();
        string path = LevelDirectory + $"/Level {number}.prefab";
        if (File.Exists(path))
            throw new InvalidOperationException("That level already exists. Existing levels are never overwritten.");
        if (!AssetDatabase.CopyAsset(BasePath, path))
            throw new InvalidOperationException("Could not copy LevelBase prefab.");

        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(path);
            root.name = $"Level {number}";
            var level = root.GetComponent<CookingBlastLevel>();
            var so = new SerializedObject(level);
            so.FindProperty("levelNumber").intValue = number;
            so.FindProperty("seatCount").intValue = 5;
            so.FindProperty("boardColumns").ClearArray();
            so.FindProperty("customerLanes").ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();
            EnsureScenePieces(root);
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
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void CreateFallbackData(CookingBlastLevel level)
    {
        var so = new SerializedObject(level);
        so.FindProperty("levelNumber").intValue = 1;
        so.FindProperty("seatCount").intValue = 5;
        so.ApplyModifiedPropertiesWithoutUndo();
        CookingBlastLevelEditing.ResizeBoard(level, 10, 10, FoodColor.Blue);
        var palette = new[] { FoodColor.Blue, FoodColor.Pink };
        CookingBlastLevelEditing.RandomizeBoard(level, palette, 1001);
        CookingBlastLevelEditing.AutoGenerateCustomers(level, 4, 12, 18);
    }


internal static void EnsureAllLevelStructures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { LevelDirectory });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null && asset.GetComponent<CookingBlastLevel>() != null)
                EnsureLevelStructure(path);
        }
    }

    internal static void EnsureLevelStructure(string path)
    {
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(path);
            if (root.GetComponentInChildren<CameraEdgeAnchors>(true) == null)
                root.AddComponent<CameraEdgeAnchors>();
            EnsureScenePieces(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureScenePieces(GameObject root)
    {
        if (root == null) return;
        CookingBlastLevel level = root.GetComponent<CookingBlastLevel>();
        if (level == null) level = root.AddComponent<CookingBlastLevel>();

        BoardController board = EnsureBoard(root.transform);
        SeatBar seatBar = EnsureNested<SeatBar>(root.transform, "SeatBar", SeatBarPrefabPath, new Vector3(0f, -2.25f, 0f));
        CustomerQueue queue = EnsureNested<CustomerQueue>(root.transform, "CustomerQueue", CustomerQueuePrefabPath, new Vector3(0f, -4.35f, 0f));

        var so = new SerializedObject(level);
        so.FindProperty("board").objectReferenceValue = board;
        so.FindProperty("seatBar").objectReferenceValue = seatBar;
        so.FindProperty("customerQueue").objectReferenceValue = queue;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);
    }

    private static BoardController EnsureBoard(Transform root)
    {
        Transform authoredTransform = root.Find("BG/Top/Board");
        BoardController authoredBoard = authoredTransform != null
            ? authoredTransform.GetComponent<BoardController>()
            : null;

        BoardController board = authoredBoard != null
            ? authoredBoard
            : EnsureNested<BoardController>(root, "Board", BoardPrefabPath,
                new Vector3(0f, 1.35f, 0f));

        // Older setup runs created a second Board directly under the level
        // because they only searched root/Board. The authored, screen-scaled
        // Board under BG/Top is the one gameplay should initialize.
        Transform redundantRootBoard = root.Find("Board");
        if (authoredBoard != null && redundantRootBoard != null &&
            redundantRootBoard != authoredBoard.transform)
            UnityEngine.Object.DestroyImmediate(redundantRootBoard.gameObject);

        return board;
    }

    private static T EnsureNested<T>(Transform root, string childName, string prefabPath, Vector3 localPosition) where T : Component
    {
        Transform child = root.Find(childName);
        T component = child != null ? child.GetComponent<T>() : null;
        if (component == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Missing required prefab: {prefabPath}");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            instance.name = childName;
            child = instance.transform;
            component = instance.GetComponent<T>();
        }
        child.localPosition = localPosition;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return component;
    }
}
