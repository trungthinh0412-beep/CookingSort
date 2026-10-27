using System;
using System.IO;
using System.Linq;
using System.Text;
using CookingSort2D;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit, repeatable checks of the character without running gameplay startup.</summary>
internal static class SpineOutlinePreviewTools
{
    private const string ScenePath = "Assets/_Project/Scenes/GameplayScene.unity";
    private const string ObjectName = "Spine GameObject (character)";
    private const string OutputDirectory = "Library/OutlineValidation";
    private static readonly string[] ShaderNames =
    {
        "Hidden/CookingBlast/Spine Silhouette Mask",
        "Hidden/CookingBlast/Spine Silhouette Distance",
        "CookingBlast/Spine/Silhouette Outline"
    };

    private static SkeletonAnimation FindCharacter()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Open GameplayScene before running the outline tools.");
        var found = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SkeletonAnimation>(true))
            .Where(x => x.name == ObjectName).ToArray();
        if (found.Length != 1) throw new InvalidOperationException("Expected exactly one " + ObjectName);
        return found[0];
    }

    [MenuItem("Tools/CookingBlast/Outline/Apply To Gameplay Character %&o")]
    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Apply the outline in Edit Mode.");
        SkeletonAnimation character = FindCharacter();
        var outline = character.GetComponent<SpineSilhouetteOutline>();
        if (outline == null) outline = Undo.AddComponent<SpineSilhouetteOutline>(character.gameObject);
        var serialized = new SerializedObject(outline);
        string[] fields = { "maskShader", "distanceShader", "outlineShader" };
        for (int i = 0; i < fields.Length; i++)
        {
            Shader shader = Shader.Find(ShaderNames[i]);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + ShaderNames[i]);
            serialized.FindProperty(fields[i]).objectReferenceValue = shader;
        }
        serialized.ApplyModifiedProperties();
        character.Initialize(false);
        character.LateUpdate();
        outline.RefreshOutline();
        EditorUtility.SetDirty(outline);
        EditorSceneManager.MarkSceneDirty(character.gameObject.scene);
        EditorSceneManager.SaveScene(character.gameObject.scene);
        Selection.activeGameObject = character.gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
        SceneView.RepaintAll();
        Debug.Log("[SpineOutline] Applied and saved on GameplayScene/" + ObjectName, character);
    }

    [MenuItem("Tools/CookingBlast/Outline/Validate And Capture Character %&p")]
    private static void ValidateAndCapture()
    {
        Directory.CreateDirectory(OutputDirectory);
        var report = new StringBuilder();
        report.AppendLine("Spine silhouette validation " + DateTime.UtcNow.ToString("O"));
        int errors = 0;
        foreach (string name in ShaderNames)
        {
            Shader shader = Shader.Find(name);
            report.AppendLine(name + ": supported=" + (shader != null && shader.isSupported));
            if (shader == null) { errors++; continue; }
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
            {
                report.AppendLine(message.severity + " " + message.message + " line=" + message.line);
                if (message.severity.ToString() == "Error") errors++;
            }
        }
        if (errors != 0)
        {
            File.WriteAllText(Path.Combine(OutputDirectory, "report.txt"), report.ToString());
            throw new InvalidOperationException("Outline shader errors; see Library/OutlineValidation/report.txt");
        }

        SkeletonAnimation source = FindCharacter();
        PreviewRenderUtility preview = null;
        GameObject clone = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            preview = new PreviewRenderUtility();
            clone = UnityEngine.Object.Instantiate(source.gameObject);
            clone.name = "Outline validation clone";
            clone.transform.SetParent(null);
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clone.transform.localScale = Vector3.one;
            preview.AddSingleGO(clone);
            SkeletonAnimation skeleton = clone.GetComponent<SkeletonAnimation>();
            skeleton.Initialize(true);
            var outline = clone.GetComponent<SpineSilhouetteOutline>();
            if (outline == null) outline = clone.AddComponent<SpineSilhouetteOutline>();
            Camera camera = preview.camera;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.22f, 0.29f, 0.38f, 1f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            string animation = string.IsNullOrEmpty(source.AnimationName) ? "idle_0" : source.AnimationName;
            float[] times = { 0f, 0.35f, 0.7f, 1.05f };
            for (int frame = 0; frame < times.Length; frame++)
            {
                skeleton.AnimationState.SetAnimation(0, animation, true);
                skeleton.Update(times[frame]);
                skeleton.LateUpdate();
                outline.RefreshOutline();
                Capture(preview, clone, "pose_" + frame + ".png");
                report.AppendLine("Pose " + times[frame] + " mesh vertices=" + clone.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            }

            var so = new SerializedObject(outline);
            so.FindProperty("outlineWidth").floatValue = 18f;
            so.ApplyModifiedPropertiesWithoutUndo();
            outline.RefreshOutline();
            Capture(preview, clone, "thick_18.png");
            so.FindProperty("outlineWidth").floatValue = 6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            outline.enabled = false;
            Capture(preview, clone, "disabled.png");
            outline.enabled = true;
            skeleton.LateUpdate();
            outline.RefreshOutline();
            Capture(preview, clone, "reenabled.png");

            if (skeleton.Skeleton.Data.FindSkin("1") != null)
            {
                skeleton.Skeleton.SetSkin("1");
                skeleton.Skeleton.SetSlotsToSetupPose();
                skeleton.Update(0f);
                skeleton.LateUpdate();
                outline.RefreshOutline();
                Capture(preview, clone, "skin_1.png");
                report.AppendLine("Skin switch 0 -> 1 captured.");
            }
            report.AppendLine("Disable/re-enable and width 18 captured. Original scene pose/materials unchanged.");
        }
        catch (Exception exception)
        {
            report.AppendLine("FAIL: " + exception);
            throw;
        }
        finally
        {
            RenderTexture.active = previous;
            if (preview != null) preview.Cleanup();
            else if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            File.WriteAllText(Path.Combine(OutputDirectory, "report.txt"), report.ToString());
        }
        Debug.Log("[SpineOutline] Validation captures saved to " + Path.GetFullPath(OutputDirectory));
    }

    private static void Capture(PreviewRenderUtility preview, GameObject clone, string filename)
    {
        Bounds bounds = clone.GetComponent<MeshRenderer>().bounds;
        preview.camera.transform.SetPositionAndRotation(bounds.center + new Vector3(0, 0, -10), Quaternion.identity);
        preview.camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) + 0.35f;
        preview.BeginPreview(new Rect(0, 0, 640, 640), GUIStyle.none);
        preview.Render(true);
        Texture image = preview.EndPreview();
        RenderTexture previous = RenderTexture.active;
        var pixels = new Texture2D(640, 640, TextureFormat.RGBA32, false);
        RenderTexture copy = RenderTexture.GetTemporary(640, 640, 0, RenderTextureFormat.ARGB32);
        try
        {
            Graphics.Blit(image, copy);
            RenderTexture.active = copy;
            pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, filename), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(copy);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
