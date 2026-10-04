using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(UISpriteAnimator))]
public sealed class UISpriteAnimatorEditor : Editor
{
    private UISpriteAnimator animator;
    private Image previewImage;
    private Sprite originalSprite;
    private int previewFrame;
    private int previewDirection = 1;
    private bool isPreviewing;
    private double nextFrameTime;

    private void OnEnable()
    {
        animator = (UISpriteAnimator)target;
        previewFrame = FindCurrentFrame();
    }

    private void OnDisable()
    {
        StopPreview();
        RestoreOriginalSprite();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawReferences();
        DrawAnimationTiming();
        DrawDelays();
        DrawFrameFx();

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Sprite preview is available while Edit Mode is active.",
                MessageType.Info
            );
            return;
        }

        int frameCount = animator.EditorPreviewFrameCount;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Scene Preview",
            EditorStyles.boldLabel
        );

        if (animator.targetImage == null || frameCount == 0)
        {
            EditorGUILayout.HelpBox(
                "Assign Target Image and a sliced Sprite Sheet to preview frames.",
                MessageType.Warning
            );
            return;
        }

        previewFrame = Mathf.Clamp(previewFrame, 0, frameCount - 1);

        EditorGUILayout.LabelField(
            $"Frame {previewFrame + 1} / {frameCount}"
        );

        EditorGUI.BeginChangeCheck();
        int selectedFrame = EditorGUILayout.IntSlider(
            "Preview Frame",
            previewFrame,
            0,
            frameCount - 1
        );
        if (EditorGUI.EndChangeCheck())
        {
            StopPreview();
            previewDirection = 1;
            SetPreviewFrame(selectedFrame);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("|< Reset"))
        {
            StopPreview();
            previewDirection = 1;
            SetPreviewFrame(0);
        }

        if (GUILayout.Button("< Previous"))
        {
            StopPreview();
            previewDirection = 1;
            SetPreviewFrame(previewFrame - 1);
        }

        if (GUILayout.Button(isPreviewing ? "Stop" : "Play"))
        {
            if (isPreviewing)
                StopPreview();
            else
                StartPreview();
        }

        if (GUILayout.Button("Next >"))
        {
            StopPreview();
            previewDirection = 1;
            SetPreviewFrame(previewFrame + 1);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawReferences()
    {
        EditorGUILayout.LabelField("Reference", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("targetImage")
        );
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("spriteSheet")
        );

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("frames"),
            true
        );
        EditorGUI.EndDisabledGroup();
    }

    private void DrawAnimationTiming()
    {
        SerializedProperty fps = serializedObject.FindProperty("fps");
        int frameCount = animator.EditorPreviewFrameCount;
        float currentFps = Mathf.Max(fps.floatValue, .01f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        float editedFps = EditorGUILayout.FloatField("FPS", currentFps);
        if (EditorGUI.EndChangeCheck())
        {
            currentFps = Mathf.Max(editedFps, .01f);
            fps.floatValue = currentFps;
        }

        EditorGUI.BeginDisabledGroup(frameCount == 0);
        EditorGUI.BeginChangeCheck();
        float editedDuration = EditorGUILayout.FloatField(
            "Duration (seconds)",
            frameCount == 0 ? 0f : frameCount / currentFps
        );
        if (EditorGUI.EndChangeCheck() && frameCount > 0)
        {
            currentFps = frameCount / Mathf.Max(editedDuration, .01f);
            fps.floatValue = currentFps;
        }
        EditorGUI.EndDisabledGroup();

        if (frameCount == 0)
        {
            EditorGUILayout.HelpBox(
                "Duration becomes available after the Sprite Sheet has frames.",
                MessageType.Info
            );
        }
        else
        {
            EditorGUILayout.LabelField($"Frames: {frameCount}");
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("loop"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pingPong"));
    }

    private void DrawDelays()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Delay", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("useStartDelay")
        );
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("startDelay")
        );
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("useLoopDelay")
        );
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("loopDelay")
        );
    }

    private void DrawFrameFx()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Frame FX", EditorStyles.boldLabel);

        SerializedProperty useFrameFx =
            serializedObject.FindProperty("useFrameFx");
        EditorGUILayout.PropertyField(useFrameFx, new GUIContent("Use Frame FX"));

        if (!useFrameFx.boolValue)
            return;

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("frameFx"),
            new GUIContent("FX")
        );
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("playFxAtFrame"),
            new GUIContent("Play At Frame")
        );
        EditorGUI.indentLevel--;

        int frameCount = animator.EditorPreviewFrameCount;
        if (frameCount > 0)
        {
            SerializedProperty playFxAtFrame =
                serializedObject.FindProperty("playFxAtFrame");
            playFxAtFrame.intValue = Mathf.Clamp(
                playFxAtFrame.intValue,
                1,
                frameCount
            );
            EditorGUILayout.HelpBox(
                $"Valid frame range: 1 - {frameCount}",
                MessageType.Info
            );
        }
    }

    private void StartPreview()
    {
        if (animator == null || animator.targetImage == null ||
            animator.EditorPreviewFrameCount == 0)
        {
            return;
        }

        CaptureOriginalSprite();
        isPreviewing = true;
        nextFrameTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += UpdatePreview;
    }

    private void StopPreview()
    {
        if (!isPreviewing)
            return;

        isPreviewing = false;
        EditorApplication.update -= UpdatePreview;
        RepaintScene();
    }

    private void UpdatePreview()
    {
        if (animator == null || Application.isPlaying ||
            animator.targetImage == null || animator.EditorPreviewFrameCount == 0)
        {
            StopPreview();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now < nextFrameTime)
            return;

        bool reachedLoopPoint = AdvanceFrame();
        float delay = reachedLoopPoint && animator.useLoopDelay
            ? Mathf.Max(0f, animator.loopDelay)
            : 1f / Mathf.Max(animator.fps, .01f);

        nextFrameTime = now + delay;
        Repaint();
        RepaintScene();
    }

    private bool AdvanceFrame()
    {
        int frameCount = animator.EditorPreviewFrameCount;
        if (frameCount <= 1)
        {
            SetPreviewFrame(0);
            return false;
        }

        int nextFrame = previewFrame + previewDirection;
        bool reachedLoopPoint = false;

        if (animator.pingPong)
        {
            if (nextFrame >= frameCount)
            {
                nextFrame = frameCount - 2;
                previewDirection = -1;
                reachedLoopPoint = true;
            }
            else if (nextFrame < 0)
            {
                nextFrame = 1;
                previewDirection = 1;
                reachedLoopPoint = true;
            }
        }
        else if (nextFrame >= frameCount)
        {
            reachedLoopPoint = true;

            if (animator.loop)
            {
                nextFrame = 0;
            }
            else
            {
                nextFrame = frameCount - 1;
                StopPreview();
            }
        }

        SetPreviewFrame(nextFrame);
        return reachedLoopPoint;
    }

    private void SetPreviewFrame(int frameIndex)
    {
        int frameCount = animator.EditorPreviewFrameCount;
        if (frameCount == 0)
            return;

        CaptureOriginalSprite();
        previewFrame = (frameIndex % frameCount + frameCount) % frameCount;
        animator.EditorPreviewSetFrame(previewFrame);
        RepaintScene();
    }

    private int FindCurrentFrame()
    {
        if (animator == null || animator.targetImage == null)
            return 0;

        SerializedProperty frames = serializedObject.FindProperty("frames");
        for (int index = 0; index < frames.arraySize; index++)
        {
            Sprite frame = frames.GetArrayElementAtIndex(index)
                .objectReferenceValue as Sprite;

            if (frame == animator.targetImage.sprite)
                return index;
        }

        return 0;
    }

    private void CaptureOriginalSprite()
    {
        if (previewImage == animator.targetImage)
            return;

        RestoreOriginalSprite();
        previewImage = animator.targetImage;
        originalSprite = previewImage != null ? previewImage.sprite : null;
    }

    private void RestoreOriginalSprite()
    {
        if (previewImage != null)
            previewImage.sprite = originalSprite;

        previewImage = null;
        originalSprite = null;
    }

    private static void RepaintScene()
    {
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
