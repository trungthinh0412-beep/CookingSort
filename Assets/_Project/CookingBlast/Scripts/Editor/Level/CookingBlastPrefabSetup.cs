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

    // Layout measured from the 442x787 ingame reference. The board keeps its
    // vertical tile spacing but is narrower and lower. The five-seat bar is
    // kept wider than the four-seat reference so gameplay remains readable.
    private static readonly Vector3 ReferenceBoardPosition =
        new Vector3(-0.02f, -3.83f, 0f);
    private static readonly Vector3 ReferenceBoardScale =
        new Vector3(0.95f, 0.88f, 1f);
    private static readonly Vector3 ReferenceTileRootPosition =
        new Vector3(0f, -4.83f, 0f);
    private const float ReferenceBoardBackgroundHeight = 11.90f;
    private const float ReferenceBoardGapY = -0.38f;
    private static readonly Vector3 ReferenceSeatBarPosition =
        new Vector3(0f, -2.85f, 0f);
    private static readonly Vector3 ReferenceSeatBarScale =
        new Vector3(0.92f, 0.95f, 1f);
    private const float ReferenceSeatedAnimalScale = 1.20f;
    private static readonly Vector3 ReferenceCustomerQueuePosition =
        new Vector3(0f, -4.62f, 0f);

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

    [MenuItem("Tools/Cooking Blast/Apply Reference Ingame Layout %&l")]
    internal static void ApplyReferenceIngameLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode first.");

        string[] guids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { LevelDirectory });
        int updated = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponent<CookingBlastLevel>() == null)
                continue;

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                ApplyReferenceLayout(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                updated++;
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"[CookingBlast] Applied reference ingame layout to {updated} level prefabs.");
    }

    private static void ApplyReferenceLayout(GameObject root)
    {
        BoardController board = root.GetComponentInChildren<BoardController>(true);
        SeatBar seatBar = root.GetComponentInChildren<SeatBar>(true);
        CustomerQueue queue = root.GetComponentInChildren<CustomerQueue>(true);

        if (board != null)
        {
            board.transform.localPosition = ReferenceBoardPosition;
            board.transform.localRotation = Quaternion.identity;
            board.transform.localScale = ReferenceBoardScale;

            SpriteRenderer boardRenderer =
                board.GetComponent<SpriteRenderer>();
            if (boardRenderer != null)
            {
                Vector2 size = boardRenderer.size;
                size.y = ReferenceBoardBackgroundHeight;
                boardRenderer.size = size;
                EditorUtility.SetDirty(boardRenderer);
            }

            Transform tileRoot = board.transform.Find("Tiles");
            if (tileRoot != null)
            {
                tileRoot.localPosition = ReferenceTileRootPosition;
                EditorUtility.SetDirty(tileRoot);
            }

            SerializedObject boardObject = new SerializedObject(board);
            boardObject.FindProperty("gapY").floatValue = ReferenceBoardGapY;
            boardObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(board);

            ScreenRatioScaler ratioScaler =
                board.GetComponent<ScreenRatioScaler>();
            if (ratioScaler != null)
            {
                SerializedObject scalerObject =
                    new SerializedObject(ratioScaler);
                scalerObject.FindProperty("scaleAtReference").vector3Value =
                    ReferenceBoardScale;
                scalerObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ratioScaler);
            }
        }

        if (seatBar != null)
        {
            seatBar.transform.localPosition = ReferenceSeatBarPosition;
            seatBar.transform.localRotation = Quaternion.identity;
            seatBar.transform.localScale = ReferenceSeatBarScale;

            SerializedObject seatBarObject = new SerializedObject(seatBar);
            seatBarObject.FindProperty("seatedAnimalScale").floatValue =
                ReferenceSeatedAnimalScale;
            seatBarObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seatBar);
        }

        if (queue != null)
        {
            queue.transform.localPosition = ReferenceCustomerQueuePosition;
            queue.transform.localRotation = Quaternion.identity;
            queue.transform.localScale = Vector3.one;
        }
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

        BoardController board = EnsureNested<BoardController>(
            root.transform, "Board", BoardPrefabPath, ReferenceBoardPosition);
        SeatBar seatBar = EnsureNested<SeatBar>(
            root.transform, "SeatBar", SeatBarPrefabPath, ReferenceSeatBarPosition);
        CustomerQueue queue = EnsureNested<CustomerQueue>(
            root.transform, "CustomerQueue", CustomerQueuePrefabPath,
            ReferenceCustomerQueuePosition);

        ApplyReferenceLayout(root);

        var so = new SerializedObject(level);
        so.FindProperty("board").objectReferenceValue = board;
        so.FindProperty("seatBar").objectReferenceValue = seatBar;
        so.FindProperty("customerQueue").objectReferenceValue = queue;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);
    }

    private static T EnsureNested<T>(Transform root, string childName, string prefabPath, Vector3 localPosition) where T : Component
    {
        T component = root.GetComponentInChildren<T>(true);
        Transform child = component != null ? component.transform : null;
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
