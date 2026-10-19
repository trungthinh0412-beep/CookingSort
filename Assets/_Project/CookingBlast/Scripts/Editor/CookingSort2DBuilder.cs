#if UNITY_EDITOR
using System.Collections.Generic;
using CookingSort2D;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CookingSort2DBuilder
{
    private const string PrefabFolder = "Assets/_Project/CookingBlast/Prefabs";
    private const string ScenePath = "Assets/_Project/CookingBlast/Scenes/CookingBlastGameplay.unity";

    private static Sprite _worldSprite;
    private static Sprite _uiSprite;

public static void RebuildGameplay()
    {
        EnsureFolders();
        _worldSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        _uiSprite = _worldSprite;

        GameObject tilePrefab = CreateFoodTilePrefab();
        GameObject customerPrefab = CreateCustomerPrefab();
        GameObject slotPrefab = CreateSeatSlotPrefab();
        GameObject queueSlotPrefab = CreateCustomerQueueSlotPrefab();
        CreateSeatBarPrefab(slotPrefab);
        CreateQueuePrefab(customerPrefab, queueSlotPrefab);
        CreateBoardPrefab(tilePrefab);
        GameObject hudPrefab = CreateHudPrefab();
        GameObject gameRootPrefab = CreateGameRootPrefab();

        CookingBlastPrefabSetup.SetupLevelSystem();
        CreateGameplayScene(hudPrefab, gameRootPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CookingBlast] Rebuilt gameplay with the dedicated CookingBlastLevelLoader. Levels are loaded from Addressables at runtime.");
    }

private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/CookingBlast"))
            AssetDatabase.CreateFolder("Assets/_Project", "CookingBlast");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/_Project/CookingBlast", "Prefabs");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(parent))
        {
            string[] parts = parent.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static GameObject CreateFoodTilePrefab()
    {
        GameObject root = CreateWorldSpriteObject("FoodTile", null, new Vector2(0.48f, 0.48f), Color.white, 10);
        root.AddComponent<FoodTileView>();
        CreateWorldText("Symbol", root.transform, "B", 0.065f, Color.white, 11, Vector3.zero);
        return SavePrefab(root, PrefabFolder + "/FoodTile.prefab");
    }

    private static GameObject CreateCustomerPrefab()
    {
        GameObject root = CreateWorldSpriteObject("Customer", null, new Vector2(0.78f, 0.78f), Color.white, 24);
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.82f, 0.82f);
        root.AddComponent<CustomerView>();

        CreateWorldText("Face", root.transform, "B", 0.085f, Color.white, 25, new Vector3(0f, 0.04f, 0f));

        GameObject badge = CreateWorldSpriteObject("CountBadge", root.transform, new Vector2(0.34f, 0.25f), new Color32(55, 66, 83, 245), 26);
        badge.transform.localPosition = new Vector3(-0.22f, -0.24f, 0f);
        CreateWorldText("Count", badge.transform, "20", 0.052f, Color.white, 27, Vector3.zero);
        badge.SetActive(false);

        return SavePrefab(root, PrefabFolder + "/Customer.prefab");
    }

    private static GameObject CreateSeatSlotPrefab()
    {
        GameObject root = CreateWorldSpriteObject("SeatSlot", null, new Vector2(0.86f, 0.82f), new Color32(255, 244, 222, 220), 21);
        root.AddComponent<SeatSlotView>();
        return SavePrefab(root, PrefabFolder + "/SeatSlot.prefab");
    }

private static GameObject CreateCustomerQueueSlotPrefab()
    {
        GameObject root = CreateWorldSpriteObject("CustomerQueueSlot", null, new Vector2(0.86f, 0.82f), new Color32(238, 246, 228, 235), 19);
        root.AddComponent<CustomerQueueSlotView>();
        return SavePrefab(root, PrefabFolder + "/CustomerQueueSlot.prefab");
    }


    private static GameObject CreateSeatBarPrefab(GameObject slotPrefab)
    {
        GameObject root = CreateWorldSpriteObject("SeatBar", null, new Vector2(5.25f, 1.02f), new Color32(190, 121, 54, 255), 20);
        root.AddComponent<SeatBar>();
        for (int i = 0; i < 5; i++)
        {
            GameObject slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab);
            slot.name = "SeatSlot_" + (i + 1);
            slot.transform.SetParent(root.transform, false);
            slot.transform.localPosition = new Vector3((i - 2) * 0.98f, 0f, 0f);
        }
        return SavePrefab(root, PrefabFolder + "/SeatBar.prefab");
    }

private static GameObject CreateQueuePrefab(GameObject customerPrefab, GameObject queueSlotPrefab)
    {
        GameObject root = CreateWorldSpriteObject("CustomerQueue", null, new Vector2(5.25f, 1.02f), new Color32(116, 166, 112, 255), 17);
        CustomerQueue queue = root.AddComponent<CustomerQueue>();

        GameObject slots = new GameObject("Slots");
        slots.transform.SetParent(root.transform, false);
        for (int i = 0; i < 5; i++)
        {
            GameObject slot = (GameObject)PrefabUtility.InstantiatePrefab(queueSlotPrefab);
            slot.name = "QueueSlot_" + (i + 1);
            slot.transform.SetParent(slots.transform, false);
            slot.transform.localPosition = new Vector3((i - 2) * 1.05f, 0f, 0f);
        }

        GameObject customers = new GameObject("Customers");
        customers.transform.SetParent(root.transform, false);

        SerializedObject so = new SerializedObject(queue);
        so.FindProperty("customerPrefab").objectReferenceValue = customerPrefab.GetComponent<CustomerView>();
        so.FindProperty("customersRoot").objectReferenceValue = customers.transform;
        so.FindProperty("slotsRoot").objectReferenceValue = slots.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        return SavePrefab(root, PrefabFolder + "/CustomerQueue.prefab");
    }

    private static GameObject CreateBoardPrefab(GameObject tilePrefab)
    {
        GameObject root = CreateWorldSpriteObject("Board", null, new Vector2(5.55f, 5.75f), new Color32(224, 214, 174, 255), 0);
        BoardController board = root.AddComponent<BoardController>();
        GameObject tiles = new GameObject("Tiles");
        tiles.transform.SetParent(root.transform, false);

        SerializedObject so = new SerializedObject(board);
        so.FindProperty("tilePrefab").objectReferenceValue = tilePrefab.GetComponent<FoodTileView>();
        so.FindProperty("tileRoot").objectReferenceValue = tiles.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        return SavePrefab(root, PrefabFolder + "/Board.prefab");
    }

    private static GameObject CreateHudPrefab()
    {
        GameObject root = new GameObject("GameHud", typeof(RectTransform), typeof(GameHud));
        RectTransform rr = root.GetComponent<RectTransform>();
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = rr.offsetMax = Vector2.zero;

        GameObject levelBadge = CreateUIImageObject("LevelBadge", root.transform, new Vector2(78, 72), new Color32(74, 152, 226, 255));
        SetAnchor(levelBadge.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(58, -50));
        TextMeshProUGUI levelText = CreateUIText("LevelText", levelBadge.transform, "Level\n1", 19, TextAlignmentOptions.Center, Color.white);
        Stretch(levelText.rectTransform, 4f);

        GameObject progressBg = CreateUIImageObject("ProgressBG", root.transform, new Vector2(380, 34), new Color32(56, 80, 108, 220));
        SetAnchor(progressBg.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(55, -46));
        GameObject fill = CreateUIImageObject("ProgressFill", progressBg.transform, Vector2.zero, new Color32(255, 201, 47, 255));
        RectTransform fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(4, 4);
        fr.offsetMax = new Vector2(-4, -4);
        Image fi = fill.GetComponent<Image>();
        fi.type = Image.Type.Filled;
        fi.fillMethod = Image.FillMethod.Horizontal;
        fi.fillOrigin = 0;
        fi.fillAmount = 0f;

        TextMeshProUGUI combo = CreateUIText("ComboText", root.transform, "Combo\n0 x1", 20, TextAlignmentOptions.Center, new Color32(241, 85, 42, 255));
        RectTransform cr = combo.rectTransform;
        cr.sizeDelta = new Vector2(120, 90);
        SetAnchor(cr, new Vector2(1, 1), new Vector2(-62, -82));

        GameObject overlay = CreateUIImageObject("Overlay", root.transform, Vector2.zero, new Color32(20, 24, 35, 210));
        Stretch(overlay.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI result = CreateUIText("ResultText", overlay.transform, "Excellent!", 34, TextAlignmentOptions.Center, Color.white);
        result.rectTransform.sizeDelta = new Vector2(480, 260);
        SetAnchor(result.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 70));
        GameObject retry = CreateUIImageObject("RetryButton", overlay.transform, new Vector2(230, 76), new Color32(85, 195, 55, 255));
        retry.AddComponent<Button>();
        SetAnchor(retry.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, -125));
        TextMeshProUGUI retryText = CreateUIText("RetryText", retry.transform, "Try Again", 26, TextAlignmentOptions.Center, Color.white);
        Stretch(retryText.rectTransform, 2f);
        overlay.SetActive(false);

        return SavePrefab(root, PrefabFolder + "/GameHud.prefab");
    }

    private static GameObject CreateGameRootPrefab()
    {
        GameObject root = new GameObject("GameplayRoot", typeof(CookingSortGameController));
        return SavePrefab(root, PrefabFolder + "/GameplayRoot.prefab");
    }





private static void CreateGameplayScene(GameObject hudPrefab, GameObject gameRootPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "CookingBlastGameplay";

        GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(Physics2DRaycaster));
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
        Camera cam = cameraGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6.2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(214, 231, 203, 255);
        cameraGo.tag = "MainCamera";

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        System.Type inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) eventSystem.AddComponent(inputType);
        else eventSystem.AddComponent<StandaloneInputModule>();

        GameObject canvasGo = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(580, 1280);
        scaler.matchWidthOrHeight = 1f;

        GameObject hudGo = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, canvasGo.transform);
        RectTransform hr = hudGo.GetComponent<RectTransform>();
        hr.anchorMin = Vector2.zero;
        hr.anchorMax = Vector2.one;
        hr.offsetMin = hr.offsetMax = Vector2.zero;

        GameObject gameRoot = (GameObject)PrefabUtility.InstantiatePrefab(gameRootPrefab);
        CookingSortGameController game = gameRoot.GetComponent<CookingSortGameController>();
        CookingBlastLevelLoader loader = gameRoot.GetComponent<CookingBlastLevelLoader>();
        if (loader == null) loader = gameRoot.AddComponent<CookingBlastLevelLoader>();

        GameObject levelRootGo = new GameObject("LevelRoot");
        levelRootGo.transform.SetParent(gameRoot.transform, false);

        CookingBlastLevelConfig config = AssetDatabase.LoadAssetAtPath<CookingBlastLevelConfig>(CookingBlastPrefabSetup.ConfigPath);
        SerializedObject gameSo = new SerializedObject(game);
        gameSo.FindProperty("hud").objectReferenceValue = hudGo.GetComponent<GameHud>();
        gameSo.FindProperty("board").objectReferenceValue = null;
        gameSo.FindProperty("seatBar").objectReferenceValue = null;
        gameSo.FindProperty("customerQueue").objectReferenceValue = null;
        gameSo.FindProperty("levelDefinition").objectReferenceValue = null;
        gameSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject loaderSo = new SerializedObject(loader);
        loaderSo.FindProperty("config").objectReferenceValue = config;
        loaderSo.FindProperty("game").objectReferenceValue = game;
        loaderSo.FindProperty("levelRoot").objectReferenceValue = levelRootGo.transform;
        loaderSo.FindProperty("loadOnStart").boolValue = true;
        loaderSo.FindProperty("persistProgress").boolValue = true;
        loaderSo.FindProperty("defaultPlayerLevel").intValue = 1;
        loaderSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static GameObject CreateWorldSpriteObject(string name, Transform parent, Vector2 size, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(SpriteRenderer));
        if (parent != null) go.transform.SetParent(parent, false);
        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = _worldSprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    private static TextMesh CreateWorldText(string name, Transform parent, string text, float characterSize, Color color, int sortingOrder, Vector3 localPosition)
    {
        GameObject go = new GameObject(name, typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        TextMesh tm = go.GetComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = characterSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = sortingOrder;
        return tm;
    }

    private static GameObject CreateUIImageObject(string name, Transform parent, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (parent != null) go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.sprite = _uiSprite;
        image.type = Image.Type.Sliced;
        return go;
    }

    private static TextMeshProUGUI CreateUIText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetAnchor(RectTransform rect, Vector2 anchor, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }
}
#endif
