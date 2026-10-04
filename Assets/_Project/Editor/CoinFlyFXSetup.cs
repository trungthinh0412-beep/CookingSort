using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public static class CoinFlyFXSetup
{
    private const string RootPath = "Assets/_Project/VFX/CoinFly";
    private const string SpriteSheetPath =
        "Assets/_Project/Textures/GUI/19_kingdom/coin_animation (fx).png";
    private const string SettingsPath = RootPath + "/CoinFlyFXSettings.asset";
    private const string ClipPath = RootPath + "/CoinSpin.anim";
    private const string ControllerPath = RootPath + "/CoinSpin.controller";
    private const string CoinViewPath = RootPath + "/CoinView.prefab";
    private const string CoinFlyFxPath = RootPath + "/CoinFlyFX.prefab";
    private const string PopupInGamePath =
        "Assets/_Project/Prefabs/Popup/PopupInGame.prefab";

    [MenuItem("Tools/Game FX/Setup Coin Collect FX")]
    private static void Setup()
    {
        Sprite[] frames = FindCoinFrames();
        if (frames == null)
            return;

        Directory.CreateDirectory(RootPath);

        CoinFlyFXSettings settings = EnsureSettings(frames);
        AnimationClip clip = EnsureClip(frames);
        AnimatorController controller = EnsureController(clip);
        RectTransform coinView = EnsureCoinView(controller, frames[0]);
        CoinFlyFXTemplate template = EnsureTemplate(settings, coinView);
        BindPopupInGameIfEmpty(template);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CoinFlyFX] Setup completed. Existing assets were kept unchanged.");
    }

    [MenuItem("Tools/Game FX/Validate Coin Collect FX")]
    private static void ValidateAssets()
    {
        Sprite[] frames = FindCoinFrames();
        if (frames == null)
            return;

        CoinFlyFXSettings settings =
            AssetDatabase.LoadAssetAtPath<CoinFlyFXSettings>(SettingsPath);
        if (settings == null || !settings.HasExpectedCoinSpinFrames)
        {
            Debug.LogError(
                "[CoinFlyFX] CoinFlyFXSettings is missing or does not contain 11 frames."
            );
            return;
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        RectTransform coinView =
            AssetDatabase.LoadAssetAtPath<GameObject>(CoinViewPath)
                ?.GetComponent<RectTransform>();
        CoinFlyFXTemplate template =
            AssetDatabase.LoadAssetAtPath<GameObject>(CoinFlyFxPath)
                ?.GetComponent<CoinFlyFXTemplate>();

        if (clip == null || controller == null || coinView == null || template == null)
        {
            Debug.LogError("[CoinFlyFX] One or more reusable FX assets are missing.");
            return;
        }

        Debug.Log("[CoinFlyFX] Validation passed: 11 numeric coin frames and all FX assets are present.");
    }

    private static Sprite[] FindCoinFrames()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SpriteSheetPath);
        Dictionary<int, Sprite> byIndex = new Dictionary<int, Sprite>();
        Regex suffix = new Regex(@"_(\d+)$");

        foreach (Object asset in assets)
        {
            if (asset is not Sprite sprite)
                continue;

            Match match = suffix.Match(sprite.name);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int index))
                continue;

            byIndex[index] = sprite;
        }

        Sprite[] frames = new Sprite[11];
        for (int i = 0; i < frames.Length; i++)
        {
            if (!byIndex.TryGetValue(i, out Sprite frame) || frame == null)
            {
                Debug.LogError(
                    $"[CoinFlyFX] Missing sprite frame {i} in {SpriteSheetPath}."
                );
                return null;
            }

            frames[i] = frame;
        }

        return frames;
    }

    private static CoinFlyFXSettings EnsureSettings(Sprite[] frames)
    {
        CoinFlyFXSettings settings =
            AssetDatabase.LoadAssetAtPath<CoinFlyFXSettings>(SettingsPath);
        if (settings != null)
            return settings;

        settings = ScriptableObject.CreateInstance<CoinFlyFXSettings>();
        settings.ConfigureForSetup(frames, 24f, 12);
        AssetDatabase.CreateAsset(settings, SettingsPath);
        return settings;
    }

    private static AnimationClip EnsureClip(Sprite[] frames)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip != null)
            return clip;

        clip = new AnimationClip { name = "CoinSpin", frameRate = 24f };
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / 24f,
                value = frames[i]
            };
        }

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(
            string.Empty,
            typeof(Image),
            "m_Sprite"
        );
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AssetDatabase.CreateAsset(clip, ClipPath);

        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty settings = serializedClip.FindProperty(
            "m_AnimationClipSettings"
        );
        if (settings != null)
        {
            SerializedProperty loop = settings.FindPropertyRelative("m_LoopTime");
            if (loop != null)
                loop.boolValue = true;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
        }

        return clip;
    }

    private static AnimatorController EnsureController(AnimationClip clip)
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
            return controller;

        controller = AnimatorController.CreateAnimatorControllerAtPath(
            ControllerPath
        );
        AnimatorState state = controller.layers[0].stateMachine.AddState("CoinSpin");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static RectTransform EnsureCoinView(
        AnimatorController controller,
        Sprite firstFrame)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinViewPath);
        if (prefab != null)
            return prefab.GetComponent<RectTransform>();

        GameObject view = new GameObject(
            "CoinView",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Animator)
        );
        RectTransform rect = view.GetComponent<RectTransform>();
        rect.sizeDelta = Vector2.one * 56f;
        Image image = view.GetComponent<Image>();
        image.sprite = firstFrame;
        image.preserveAspect = true;
        image.raycastTarget = false;
        Animator animator = view.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        PrefabUtility.SaveAsPrefabAsset(view, CoinViewPath);
        Object.DestroyImmediate(view);
        return AssetDatabase.LoadAssetAtPath<GameObject>(CoinViewPath)
            .GetComponent<RectTransform>();
    }

    private static CoinFlyFXTemplate EnsureTemplate(
        CoinFlyFXSettings settings,
        RectTransform coinView)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinFlyFxPath);
        if (prefab != null)
            return prefab.GetComponent<CoinFlyFXTemplate>();

        GameObject root = new GameObject(
            "CoinFlyFX",
            typeof(RectTransform),
            typeof(CoinFlyFXTemplate)
        );
        CoinFlyFXTemplate template = root.GetComponent<CoinFlyFXTemplate>();
        template.ConfigureForSetup(settings, coinView);
        PrefabUtility.SaveAsPrefabAsset(root, CoinFlyFxPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(CoinFlyFxPath)
            .GetComponent<CoinFlyFXTemplate>();
    }

    private static void BindPopupInGameIfEmpty(CoinFlyFXTemplate template)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PopupInGamePath);
        try
        {
            PopupInGame popup = root.GetComponent<PopupInGame>();
            if (popup == null)
            {
                Debug.LogError("[CoinFlyFX] PopupInGame component was not found.");
                return;
            }

            SerializedObject serializedPopup = new SerializedObject(popup);
            SerializedProperty templateProperty = serializedPopup.FindProperty(
                "mergeCoinFxTemplate"
            );
            if (templateProperty != null &&
                templateProperty.objectReferenceValue == null)
            {
                templateProperty.objectReferenceValue = template;
                serializedPopup.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PopupInGamePath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
