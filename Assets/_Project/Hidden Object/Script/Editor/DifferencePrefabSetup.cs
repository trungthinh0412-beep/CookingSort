using System;
using System.IO;
using MagicSoft.Differences;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class DifferencePrefabSetup
{
    public const string Root = "Assets/_Project/Hidden Object";
    public const string LevelDirectory = Root + "/Prefabs/Level";
    public const string BasePath = LevelDirectory + "/LevelBase/LevelBase.prefab";
    public const string SpotPath = Root + "/Prefabs/Elements/DifferenceSpot.prefab";
    public const string LevelPath = LevelDirectory + "/Level 1.prefab";
    public const string PreviewPath = Root + "/Scenes/DifferencePreview.unity";

    [MenuItem("Tools/Find Differences/Create Sample Assets")]
    public static void CreateSampleAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory(LevelDirectory + "/LevelBase");
        Directory.CreateDirectory(Root + "/Prefabs/Elements");
        Directory.CreateDirectory(Root + "/Sprite/Sample");
        Directory.CreateDirectory(Root + "/Scenes");
        AssetDatabase.Refresh();
        CreateSpotPrefab(false);
        if (!File.Exists(LevelPath))
        {
            var bounds = new[] { new Rect(.75f, .7f, .14f, .2f), new Rect(.33f, .3f, .14f, .2f), new Rect(.69f, .1f, .14f, .2f) };
            CreateWorldLevel(LevelPath, CreateSamplePicture(false), CreateSamplePicture(true),
                new[] { "sun_color", "missing_window", "flower_color" }, bounds, bounds, 3);
        }
        CreateBase();
        CreatePreviewScene(false);
        LevelAddressablesSetup.SyncLevelEntries();
        AssetDatabase.SaveAssets();
    }

    internal static void CreateSpotPrefab(bool replace)
    {
        if (!replace && File.Exists(SpotPath)) return;
        var obj = new GameObject("DifferenceSpot");
        var spot = obj.AddComponent<DifferenceSpotView>();
        var hit = obj.AddComponent<BoxCollider2D>();
        hit.enabled = false; // Authored hit geometry, checked by the controller. Picture handles raycasts.
        var markerObject = new GameObject("FoundMarker");
        markerObject.transform.SetParent(obj.transform, false);
        var marker = markerObject.AddComponent<SpriteRenderer>();
        marker.sprite = CreateRing();
        markerObject.SetActive(false);
        SetRef(spot, "hitArea", hit);
        SetRef(spot, "foundMarker", marker);
        PrefabUtility.SaveAsPrefabAsset(obj, SpotPath);
        Object.DestroyImmediate(obj);
    }

    internal static void CreateWorldLevel(string path, Sprite spriteA, Sprite spriteB,
        string[] ids, Rect[] regionsA, Rect[] regionsB, int mistakes)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        try
        {
            var level = root.AddComponent<Level>();
            var board = root.AddComponent<DifferenceLevelController>();
            var worldBoard = root.AddComponent<DifferenceWorldBoard>();
            var content = new GameObject("Board").transform;
            content.SetParent(root.transform, false);
            var a = Picture("Picture A", content, spriteA, new Vector3(0, 3.35f, 0));
            var b = Picture("Picture B", content, spriteB, new Vector3(0, -3.35f, 0));
            SetRef(level, "differenceController", board);
            SetRef(level, "worldBoard", worldBoard);
            SetRef(worldBoard, "contentRoot", content);
            SetRef(board, "panelA", a);
            SetRef(board, "panelB", b);
            for (int i = 0; i < ids.Length; i++)
            {
                int index = DifferenceLevelEditing.AddPair(board, regionsA[i]);
                DifferenceLevelEditing.SetBounds(DifferenceLevelEditing.Spot(board, index, false), b, regionsB[i]);
                var data = new SerializedObject(board);
                data.FindProperty("differences").GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue = ids[i];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var rules = new SerializedObject(board);
            rules.FindProperty("maxMistakes").intValue = mistakes;
            rules.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static DifferencePanelView Picture(string name, Transform parent, Sprite sprite, Vector3 position)
    {
        var frame = new GameObject(name);
        frame.transform.SetParent(parent, false);
        frame.transform.localPosition = position;
        var artwork = new GameObject("Artwork");
        artwork.transform.SetParent(frame.transform, false);
        var renderer = artwork.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 100;
        var collider = artwork.AddComponent<BoxCollider2D>();
        var panel = frame.AddComponent<DifferencePanelView>();
        SetRef(panel, "picture", renderer);
        SetRef(panel, "pictureCollider", collider);
        panel.RefreshGeometry();
        return panel;
    }

    internal static void CreateBase()
    {
        if (File.Exists(BasePath)) return;
        Directory.CreateDirectory(LevelDirectory + "/LevelBase");
        AssetDatabase.Refresh();
        CreateWorldLevel(BasePath, null, null, Array.Empty<string>(), Array.Empty<Rect>(), Array.Empty<Rect>(), 3);
    }

    internal static void CreatePreviewScene(bool replace)
    {
        if (!replace && File.Exists(PreviewPath)) return;
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var cameraObject = new GameObject("WorldCamera");
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .12f, .19f);
            camera.cullingMask = 1;
            cameraObject.AddComponent<Physics2DRaycaster>();
            var levelRoot = new GameObject("LevelRoot");
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LevelPath), levelRoot.transform);
            var canvasRect = Rect("PreviewHUD", null, Vector2.zero, Vector2.one);
            var canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            var status = Label("Status", canvasRect, "FIND THE DIFFERENCES", new Vector2(.05f, .87f), new Vector2(.95f, .97f), 38);
            var hint = Button("Hint", canvasRect, new Vector2(.08f, .05f), new Vector2(.45f, .12f));
            var replay = Button("Replay", canvasRect, new Vector2(.55f, .05f), new Vector2(.92f, .12f));
            var driver = canvasRect.gameObject.AddComponent<DifferencePreview>();
            SetRef(driver, "board", obj.GetComponent<DifferenceLevelController>());
            SetRef(driver, "worldBoard", obj.GetComponent<DifferenceWorldBoard>());
            SetRef(driver, "worldCamera", camera);
            SetRef(driver, "status", status);
            SetRef(driver, "hintButton", hint);
            SetRef(driver, "replayButton", replay);
            var events = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            events.AddComponent<StandaloneInputModule>();
#endif
            EditorSceneManager.SaveScene(scene, PreviewPath);
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    internal static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        if (parent != null) rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, string text, Vector2 min, Vector2 max, int size)
    {
        var label = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Button Button(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = Rect(name, parent, min, max);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.13f, .40f, .43f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Label("Label", rect, name.ToUpperInvariant(), Vector2.zero, Vector2.one, 40);
        return button;
    }

    internal static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Deterministic diagnostic artwork: exactly three known differences, easy to replace.
    private static Sprite CreateSamplePicture(bool variant)
    {
        string path = Root + "/Sprite/Sample/Picture" + (variant ? "B" : "A") + ".png";
        if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        const int w = 720, h = 480;
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + .5f) / w, v = (y + .5f) / h;
                Color c = v < .3f ? new Color(.36f, .63f, .39f) : new Color(.69f, .86f, .92f);
                if (u > .2f && u < .57f && v > .23f && v < .57f) c = new Color(.94f, .78f, .50f);
                if (v >= .57f && v < .79f && Mathf.Abs(u - .385f) < (.79f - v) * 1.1f) c = new Color(.70f, .32f, .26f);
                if (u > .25f && u < .32f && v > .23f && v < .43f) c = new Color(.38f, .25f, .18f);
                if (!variant && Mathf.Abs(u - .4f) < .045f && Mathf.Abs(v - .4f) < .065f) c = new Color(.23f, .46f, .60f);
                if (Ellipse(u, v, .82f, .8f, .055f, .0825f)) c = variant ? new Color(1f, .60f, .20f) : new Color(1f, .86f, .25f);
                if (Mathf.Abs(u - .76f) < .008f && v > .06f && v < .2f) c = new Color(.15f, .35f, .2f);
                if (Ellipse(u, v, .76f, .2f, .04f, .06f)) c = variant ? new Color(.68f, .38f, .76f) : new Color(.94f, .43f, .47f);
                if (Ellipse(u, v, .76f, .2f, .012f, .018f)) c = new Color(1f, .87f, .35f);
                pixels[y * w + x] = c;
            }
        return SaveSprite(path, w, h, pixels);
    }

    private static bool Ellipse(float u, float v, float x, float y, float rx, float ry)
        => Mathf.Pow((u - x) / rx, 2) + Mathf.Pow((v - y) / ry, 2) < 1f;

    private static Sprite CreateRing()
    {
        string path = Root + "/Sprite/Sample/FoundRing.png";
        if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var pixels = new Color[128 * 128];
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(64, 64));
                pixels[y * 128 + x] = new Color(1, 1, 1, Mathf.Clamp01(60 - r) * Mathf.Clamp01(r - 53));
            }
        return SaveSprite(path, 128, 128, pixels);
    }

    private static Sprite SaveSprite(string path, int width, int height, Color[] pixels)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
