using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicSoft.Differences;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class DifferenceVerification
{
    [MenuItem("Tools/Find Differences/Verify Sample Gameplay")]
    public static void Verify()
    {
        using var scope = new TestScene();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DifferencePrefabSetup.LevelPath);
        Check(prefab != null, "World level exists in Assets/_Project/Hidden Object/Prefabs/Level");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Check(instance.GetComponentsInChildren<RectTransform>(true).Length == 0 && instance.GetComponentsInChildren<Canvas>(true).Length == 0,
            "Level has no UI transforms or canvases");
        var camera = NewCamera();
        camera.pixelRect = new Rect(0, 0, 540, 960);
        camera.aspect = 540f / 960f;
        var world = instance.GetComponent<DifferenceWorldBoard>();
        world.BindCamera(camera);
        Refresh(instance);
        var board = instance.GetComponent<DifferenceLevelController>();
        var a = DifferenceLevelEditing.Panel(board, true);
        var b = DifferenceLevelEditing.Panel(board, false);
        DifferenceSpotView Spot(int i, bool sideA) => DifferenceLevelEditing.Spot(board, i, sideA);
        Check(board.TryValidate(out string error), error);
        board.SendMessage("OnEnable");
        int wins = 0, losses = 0;
        board.Completed += () => wins++;
        board.Failed += () => losses++;
        Check(board.BeginLevel(), "Begin world level");
        board.RequestHint();
        Check(board.FoundCount == 0 && Spot(0, true).FoundMarker.gameObject.activeSelf && Spot(0, false).FoundMarker.gameObject.activeSelf, "Hint without score");
        Click(camera, Spot(0, true));
        Check(board.FoundCount == 1 && Spot(0, false).FoundMarker.gameObject.activeSelf, "Physics2D pointer marks both sides");
        Click(camera, Spot(0, false));
        Check(board.FoundCount == 1 && board.MistakeCount == 0, "Repeat on other side ignored");
        board.SetPaused(true);
        Click(camera, Spot(1, false));
        Check(board.FoundCount == 1, "Paused input ignored");
        board.SetPaused(false);
        Time.timeScale = 0;
        Click(camera, Spot(1, false));
        Check(board.FoundCount == 1, "Time-scale pause blocks click");
        Time.timeScale = 1;
        Click(camera, Spot(1, false));
        Click(camera, Spot(2, true));
        Click(camera, Spot(2, true));
        Check(board.State == DifferenceLevelState.Won && wins == 1, "Win exactly once");
        Check(board.BeginLevel() && board.FoundCount == 0 && !Spot(0, true).FoundMarker.gameObject.activeSelf, "Replay resets markers");
        var empty = a.Picture.transform.TransformPoint((Vector2)a.Picture.sprite.bounds.min + (Vector2)a.Picture.sprite.bounds.size * .04f);
        Vector2 point = camera.WorldToScreenPoint(empty);
        board.HandlePictureClick(a, new Vector2(-9999, -9999), camera);
        Check(board.MistakeCount == 0, "Outside camera ignored");
        for (int i = 0; i < board.MaxMistakes + 2; i++) board.HandlePictureClick(a, point, camera);
        Check(board.State == DifferenceLevelState.Lost && losses == 1 && board.MistakeCount == board.MaxMistakes, "Loss locks world input");
        var data = new SerializedObject(board);
        data.FindProperty("maxMistakes").intValue = 0;
        data.ApplyModifiedPropertiesWithoutUndo();
        board.BeginLevel();
        for (int i = 0; i < 5; i++) board.HandlePictureClick(a, point, camera);
        Check(board.State == DifferenceLevelState.Playing && board.MistakeCount == 5, "Unlimited mistakes");
        world.SetVisible(false);
        var hits = new List<RaycastResult>();
        camera.GetComponent<Physics2DRaycaster>().Raycast(new PointerEventData(null) { position = point }, hits);
        Check(hits.Count == 0, "Hidden level cannot receive physics clicks");
        world.SetVisible(true);
        board.SendMessage("OnDisable");
        VerifySceneBindings();
        Debug.Log("[Differences World] PASS: SpriteRenderer-only level, Physics2D pointer routing, paired marks, repeat, hint, pause, replay, win/loss, visibility and scene bindings.");
    }

    private static void Click(Camera camera, DifferenceSpotView spot)
    {
        var raycaster = camera.GetComponent<Physics2DRaycaster>();
        var data = new PointerEventData(null) {
            position = camera.WorldToScreenPoint(spot.HitArea.transform.TransformPoint(spot.HitArea.offset)),
            button = PointerEventData.InputButton.Left
        };
        Physics2D.SyncTransforms();
        var hits = new List<RaycastResult>();
        raycaster.Raycast(data, hits);
        Check(hits.Count > 0, "Physics ray hits a picture");
        data.pointerPressRaycast = hits[0];
        Check(ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler) != null, "Click reaches panel");
    }

    [MenuItem("Tools/Find Differences/Verify HUD and Capture Preview")]
    public static void VerifyHud()
    {
        using var scope = new TestScene();
        var camera = NewCamera();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DifferencePrefabSetup.LevelPath));
        var board = instance.GetComponent<DifferenceLevelController>();
        var world = instance.GetComponent<DifferenceWorldBoard>();
        var hudRoot = new GameObject("TestHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = hudRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 2;
        var scaler = hudRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = .5f;
        var hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Popup/PopupInGame.prefab"), hudRoot.transform);
        hud.GetComponent<Canvas>().sortingOrder = 500;
        var data = new SerializedObject(hud.GetComponent<PopupInGame>());
        ((Transform)data.FindProperty("targetGroup").objectReferenceValue).gameObject.SetActive(false);
        ((InGamePauseMenu)data.FindProperty("pauseMenu").objectReferenceValue).SendMessage("Awake");
        var panels = new[] { DifferenceLevelEditing.Panel(board, true), DifferenceLevelEditing.Panel(board, false) };
        var sizes = new[] { new Vector2Int(540, 960), new Vector2Int(540, 1200), new Vector2Int(768, 1024) };
        foreach (var size in sizes)
        {
            var target = new RenderTexture(size.x, size.y, 24);
            try
            {
                camera.targetTexture = target;
                camera.pixelRect = new Rect(0, 0, size.x, size.y);
                camera.aspect = (float)size.x / size.y;
                world.BindCamera(camera);
                Refresh(instance);
                scaler.SendMessage("Update");
                Canvas.ForceUpdateCanvases();
                camera.Render();
                foreach (var panel in panels)
                {
                    foreach (var corner in new[] { panel.Picture.sprite.bounds.min, panel.Picture.sprite.bounds.max })
                    {
                        Vector3 viewport = camera.WorldToViewportPoint(panel.Picture.transform.TransformPoint(corner));
                        Check(viewport.x >= .059f && viewport.x <= .941f && viewport.y >= .169f && viewport.y <= .841f, "World board fits below HUD on " + size);
                    }
                    var pointer = new PointerEventData(null) { position = camera.WorldToScreenPoint(panel.Picture.bounds.center) };
                    var uiHits = new List<RaycastResult>();
                    foreach (var raycaster in hud.GetComponentsInChildren<GraphicRaycaster>()) raycaster.Raycast(pointer, uiHits);
                    Check(uiHits.Count == 0, "HUD must not block world picture; hit=" + (uiHits.Count > 0 ? uiHits[0].gameObject.name : ""));
                }
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                File.WriteAllBytes($"Library/DifferenceWorld-{size.x}x{size.y}.png", image.EncodeToPNG());
                Object.DestroyImmediate(image);
                RenderTexture.active = previous;
            }
            finally { camera.targetTexture = null; Object.DestroyImmediate(target); }
        }
        Debug.Log("[Differences World] PASS: world board fits, HUD allows clicks, rendered at 3 screen ratios.");
    }

    private static void VerifySceneBindings()
    {
        const string path = "Assets/_Project/Scenes/GameplayScene.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LevelController>(true)).Single();
            var data = new SerializedObject(controller);
            var root = data.FindProperty("levelRoot").objectReferenceValue as Transform;
            var camera = data.FindProperty("worldCamera").objectReferenceValue as Camera;
            Check(root != null && root.GetComponentInParent<Canvas>() == null && camera != null && camera.GetComponent<Physics2DRaycaster>() != null, "Explicit world camera and LevelRoot scene references");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static Camera NewCamera()
    {
        var obj = new GameObject("WorldCamera", typeof(Camera), typeof(Physics2DRaycaster));
        obj.transform.position = new Vector3(0, 0, -10);
        var camera = obj.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 9.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f, .12f, .19f);
        return camera;
    }
    private static void Refresh(GameObject root)
    {
        foreach (var panel in root.GetComponentsInChildren<DifferencePanelView>(true)) panel.RefreshGeometry();
        foreach (var spot in root.GetComponentsInChildren<DifferenceSpotView>(true)) spot.RefreshGeometry();
        Physics2D.SyncTransforms();
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("[Differences World verification] " + message);
    }
    private sealed class TestScene : IDisposable
    {
        private readonly Scene previous, scene;
        private readonly float timeScale;
        public TestScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play mode.");
            previous = SceneManager.GetActiveScene();
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            timeScale = Time.timeScale;
            Time.timeScale = 1;
        }
        public void Dispose()
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
            Time.timeScale = timeScale;
        }
    }
}
