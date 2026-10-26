using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpriteAnimator))]
[CanEditMultipleObjects]
public sealed class SpriteAnimatorEditor : Editor
{
    private SpriteAnimator animator;
    private int previewAnimationIndex;
    private int previewFrame;
    private int previewDirection = 1;
    private bool previewPassedFirstForwardRun;
    private bool isPreviewing;
    private bool hasOriginalSprite;
    private Sprite originalSprite;
    private double nextFrameTime;

    private void OnEnable()
    {
        animator = targets.Length == 1 ? target as SpriteAnimator : null;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        StopPreview();
        RestoreOriginalSprite();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        SerializedProperty targetType = serializedObject.FindProperty("targetType");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Use As", EditorStyles.boldLabel);
        DrawTargetButtons(targetType);

        // The buttons can change a selected component, so refresh its
        // SerializedObject before drawing the fields for the new mode.
        serializedObject.Update();
        targetType = serializedObject.FindProperty("targetType");

        if ((SpriteAnimatorTargetType)targetType.enumValueIndex == SpriteAnimatorTargetType.UI)
            DrawUiInspector();
        else
            DrawWorldInspector();

        serializedObject.ApplyModifiedProperties();

        DrawAnimationPreview();
    }

    private void DrawAnimationPreview()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Animation Preview", EditorStyles.boldLabel);

        if (targets.Length != 1 || animator == null)
        {
            EditorGUILayout.HelpBox(
                "Select one SpriteAnimator to preview its frames.",
                MessageType.Info
            );
            return;
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Frame preview is available in Edit Mode.",
                MessageType.Info
            );
            return;
        }

        int animationCount = animator.EditorPreviewAnimationCount;
        if (animationCount <= 0)
        {
            EditorGUILayout.HelpBox(
                "Add an animation and assign its frames before previewing.",
                MessageType.Warning
            );
            return;
        }

        previewAnimationIndex = Mathf.Clamp(previewAnimationIndex, 0, animationCount - 1);

        if (animationCount > 1)
        {
            string[] animationNames = new string[animationCount];
            for (int i = 0; i < animationCount; i++)
                animationNames[i] = animator.EditorPreviewAnimationName(i);

            int selectedAnimation = EditorGUILayout.Popup(
                "Test Animation",
                Mathf.Clamp(previewAnimationIndex, 0, animationCount - 1),
                animationNames
            );
            if (selectedAnimation != previewAnimationIndex)
            {
                StopPreview();
                previewAnimationIndex = selectedAnimation;
                previewFrame = 0;
                previewDirection = 1;
                previewPassedFirstForwardRun = false;
                SetPreviewFrame(0);
            }
        }
        else
        {
            previewAnimationIndex = 0;
            EditorGUILayout.LabelField(
                "Test Animation",
                animator.EditorPreviewAnimationName(0)
            );
        }

        int frameCount = animator.EditorPreviewFrameCount(
            previewAnimationIndex
        );
        if (frameCount <= 0)
        {
            EditorGUILayout.HelpBox(
                "The selected animation has no frames.",
                MessageType.Warning
            );
            return;
        }

        previewFrame = Mathf.Clamp(previewFrame, 0, frameCount - 1);
        float previewFps = animator.EditorPreviewFps(previewAnimationIndex);
        EditorGUILayout.LabelField(
            "Preview Info",
            $"Frame {previewFrame + 1}/{frameCount}  |  {previewFps:0.##} FPS"
        );

        EditorGUI.BeginChangeCheck();
        int selectedFrame = EditorGUILayout.IntSlider(
            "Frame",
            previewFrame,
            0,
            frameCount - 1
        );
        if (EditorGUI.EndChangeCheck())
        {
            StopPreview();
            previewDirection = 1;
            previewPassedFirstForwardRun = false;
            SetPreviewFrame(selectedFrame);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("|< First"))
        {
            StopPreview();
            previewDirection = 1;
            previewPassedFirstForwardRun = false;
            SetPreviewFrame(0);
        }
        if (GUILayout.Button("< Prev"))
        {
            StopPreview();
            StepManual(-1);
        }
        if (GUILayout.Button(isPreviewing ? "Pause" : "Play"))
        {
            if (isPreviewing)
                StopPreview();
            else
                StartPreview();
        }
        if (GUILayout.Button("Next >"))
        {
            StopPreview();
            StepManual(1);
        }
        if (GUILayout.Button("Last >|"))
        {
            StopPreview();
            previewDirection = -1;
            previewPassedFirstForwardRun = true;
            SetPreviewFrame(frameCount - 1);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Restore Original Sprite"))
        {
            StopPreview();
            RestoreOriginalSprite();
        }
    }

    private void StartPreview()
    {
        if (animator == null ||
            animator.EditorPreviewFrameCount(previewAnimationIndex) <= 0)
        {
            return;
        }

        CaptureOriginalSprite();
        isPreviewing = true;
        nextFrameTime = EditorApplication.timeSinceStartup;
        EditorApplication.update -= UpdatePreview;
        EditorApplication.update += UpdatePreview;
    }

    private void StopPreview()
    {
        isPreviewing = false;
        EditorApplication.update -= UpdatePreview;
        RepaintAllViews();
    }

    private void UpdatePreview()
    {
        if (animator == null || Application.isPlaying)
        {
            StopPreview();
            return;
        }

        int frameCount = animator.EditorPreviewFrameCount(
            previewAnimationIndex
        );
        if (frameCount <= 0)
        {
            StopPreview();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now < nextFrameTime)
            return;

        bool reachedLoopPoint = AdvancePreviewFrame(frameCount);
        float delay = reachedLoopPoint
            ? animator.EditorPreviewLoopDelay(previewAnimationIndex)
            : 0f;
        nextFrameTime = now + delay +
                        1f / animator.EditorPreviewFps(previewAnimationIndex);
        Repaint();
        RepaintAllViews();
    }

    private bool AdvancePreviewFrame(int frameCount)
    {
        if (frameCount <= 1)
        {
            SetPreviewFrame(0);
            return true;
        }

        bool pingPong = animator.EditorPreviewPingPongs(
            previewAnimationIndex
        );
        bool loops = animator.EditorPreviewLoops(previewAnimationIndex);
        bool usesLoopRange = animator.EditorPreviewUsesLoopRange(
            previewAnimationIndex
        );
        int loopStart = usesLoopRange && previewPassedFirstForwardRun
            ? animator.EditorPreviewLoopStart(previewAnimationIndex)
            : 0;
        int loopEnd = usesLoopRange
            ? animator.EditorPreviewLoopEnd(previewAnimationIndex)
            : frameCount - 1;
        int nextFrame = previewFrame + previewDirection;
        bool reachedLoopPoint = false;

        if (previewDirection > 0 && nextFrame > loopEnd)
        {
            previewPassedFirstForwardRun = true;
            reachedLoopPoint = true;
            if (pingPong)
            {
                previewDirection = -1;
                nextFrame = Mathf.Max(loopStart, loopEnd - 1);
            }
            else if (loops)
            {
                nextFrame = loopStart;
            }
            else
            {
                nextFrame = loopEnd;
                StopPreview();
            }
        }
        else if (previewDirection < 0 && nextFrame < loopStart)
        {
            reachedLoopPoint = true;
            if (pingPong || loops)
            {
                previewDirection = 1;
                nextFrame = Mathf.Min(loopEnd, loopStart + 1);
            }
            else
            {
                nextFrame = loopStart;
                StopPreview();
            }
        }

        SetPreviewFrame(nextFrame);
        return reachedLoopPoint;
    }

    private void StepManual(int delta)
    {
        int frameCount = animator.EditorPreviewFrameCount(
            previewAnimationIndex
        );
        if (frameCount <= 0)
            return;

        previewDirection = delta >= 0 ? 1 : -1;
        SetPreviewFrame(
            (previewFrame + delta + frameCount) % frameCount
        );
    }

    private void SetPreviewFrame(int frameIndex)
    {
        int frameCount = animator.EditorPreviewFrameCount(
            previewAnimationIndex
        );
        if (frameCount <= 0)
            return;

        CaptureOriginalSprite();
        previewFrame = Mathf.Clamp(frameIndex, 0, frameCount - 1);
        animator.EditorPreviewSetFrame(
            previewAnimationIndex,
            previewFrame
        );
        RepaintAllViews();
    }

    private void CaptureOriginalSprite()
    {
        if (hasOriginalSprite || animator == null)
            return;

        originalSprite = animator.EditorPreviewDisplayedSprite;
        hasOriginalSprite = true;
    }

    private void RestoreOriginalSprite()
    {
        if (!hasOriginalSprite || animator == null)
            return;

        animator.EditorPreviewSetDisplayedSprite(originalSprite);
        originalSprite = null;
        hasOriginalSprite = false;
        RepaintAllViews();
    }

    private void HandlePlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
        {
            StopPreview();
            RestoreOriginalSprite();
        }
    }

    private static void RepaintAllViews()
    {
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private void DrawTargetButtons(SerializedProperty targetType)
    {
        int selected = GUILayout.Toolbar(
            targetType.enumValueIndex,
            new[] { "UI", "World" },
            GUILayout.Height(28f));

        if (selected != targetType.enumValueIndex)
        {
            StopPreview();
            RestoreOriginalSprite();

            if (selected == (int)SpriteAnimatorTargetType.UI)
            {
                SetTargetType(SpriteAnimatorTargetType.UI);
            }
            else
            {
                SetTargetType(SpriteAnimatorTargetType.World);
            }
        }
    }

    private void SetTargetType(SpriteAnimatorTargetType newType)
    {
        foreach (Object selectedTarget in targets)
        {
            SpriteAnimator animator = (SpriteAnimator)selectedTarget;
            Undo.RecordObject(animator, "Change Sprite Animator Target");

            SerializedObject animatorObject = new SerializedObject(animator);
            animatorObject.FindProperty("targetType").enumValueIndex = (int)newType;

            if (newType == SpriteAnimatorTargetType.UI)
            {
                CopyWorldFramesToUi(animatorObject);
                SerializedProperty image = animatorObject.FindProperty("targetImage");
                if (image.objectReferenceValue == null)
                    image.objectReferenceValue = animator.GetComponent<UnityEngine.UI.Image>();
            }
            else
            {
                CopyUiFramesToWorld(animatorObject);
                SerializedProperty renderer = animatorObject.FindProperty("targetRenderer");
                if (renderer.objectReferenceValue == null)
                    renderer.objectReferenceValue = animator.GetComponent<SpriteRenderer>();
            }

            animatorObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(animator);
        }
    }

    private static void CopyWorldFramesToUi(SerializedObject animatorObject)
    {
        SerializedProperty uiFrames = animatorObject.FindProperty("frames");
        SerializedProperty animations = animatorObject.FindProperty("animations");
        if (uiFrames.arraySize > 0 || animations.arraySize == 0)
            return;

        string defaultName = animatorObject.FindProperty("playOnEnableAnimation").stringValue;
        SerializedProperty sourceFrames = null;
        for (int i = 0; i < animations.arraySize; i++)
        {
            SerializedProperty animation = animations.GetArrayElementAtIndex(i);
            if (sourceFrames == null || animation.FindPropertyRelative("animationName").stringValue == defaultName)
                sourceFrames = animation.FindPropertyRelative("frames");
        }

        CopySpriteArray(sourceFrames, uiFrames);
    }

    private static void CopyUiFramesToWorld(SerializedObject animatorObject)
    {
        SerializedProperty uiFrames = animatorObject.FindProperty("frames");
        SerializedProperty animations = animatorObject.FindProperty("animations");
        if (uiFrames.arraySize == 0 || animations.arraySize > 0)
            return;

        animations.arraySize = 1;
        SerializedProperty animation = animations.GetArrayElementAtIndex(0);
        animation.FindPropertyRelative("animationName").stringValue = "Default";
        animation.FindPropertyRelative("fps").floatValue =
            animatorObject.FindProperty("fps").floatValue;
        animation.FindPropertyRelative("loop").boolValue =
            animatorObject.FindProperty("loop").boolValue;
        animation.FindPropertyRelative("pingPong").boolValue =
            animatorObject.FindProperty("pingPong").boolValue;
        CopySpriteArray(uiFrames, animation.FindPropertyRelative("frames"));
        animatorObject.FindProperty("playOnEnableAnimation").stringValue = "Default";
    }

    private static void CopySpriteArray(SerializedProperty source, SerializedProperty destination)
    {
        if (source == null)
            return;

        destination.arraySize = source.arraySize;
        for (int i = 0; i < source.arraySize; i++)
        {
            destination.GetArrayElementAtIndex(i).objectReferenceValue =
                source.GetArrayElementAtIndex(i).objectReferenceValue;
        }
    }

    private void DrawUiInspector()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("UI Animation", EditorStyles.boldLabel);
        Draw("targetImage");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Frame Source", EditorStyles.boldLabel);
        DrawFrameSourceButtons();

        SerializedProperty frameSource = serializedObject.FindProperty("frameSource");
        if ((SpriteAnimatorFrameSource)frameSource.enumValueIndex ==
            SpriteAnimatorFrameSource.SpriteSheet)
        {
            Draw("spriteSheet");
            Draw("generateGridFrames");

            if (serializedObject.FindProperty("generateGridFrames").boolValue)
            {
                Draw("gridColumns");
                Draw("gridRows");
                Draw("gridPixelsPerUnit");
            }
        }
        else
        {
            Draw("frames", true);
        }

        Draw("fps");
        Draw("loop");
        Draw("pingPong");
        Draw("playOnEnable");
        Draw("ignoreTimeScale");
        Draw("useStartDelay");
        Draw("startDelay");
        Draw("useLoopDelay");
        Draw("loopDelay");
        Draw("useFrameFx");

        if (serializedObject.FindProperty("useFrameFx").boolValue)
        {
            Draw("frameFx");
            Draw("playFxAtFrame");
        }
    }

    private void DrawFrameSourceButtons()
    {
        SerializedProperty source = serializedObject.FindProperty("frameSource");
        source.enumValueIndex = GUILayout.Toolbar(
            source.enumValueIndex,
            new[] { "Sprite Sheet", "Frame by Frame" },
            GUILayout.Height(26f));
    }

    private void DrawWorldInspector()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("World Animation", EditorStyles.boldLabel);
        Draw("targetRenderer");
        Draw("animations", true);
        Draw("playOnEnable");
        Draw("playOnEnableAnimation");
        Draw("ignoreTimeScale");
        Draw("finishCurrentAnimationBeforeSwitching");

        Draw("useLinkedIdleChain");
        if (serializedObject.FindProperty("useLinkedIdleChain").boolValue)
        {
            Draw("linkedIdle1");
            Draw("linkedIdle2");
            Draw("linkedIdle3");
            Draw("idle1Chance");
            Draw("idle2Chance");
            Draw("idle3Chance");
        }
        Draw("enableIdleVariation");

        if (serializedObject.FindProperty("enableIdleVariation").boolValue)
        {
            Draw("primaryIdleAnimation");
            Draw("alternateIdleAnimation");
            Draw("idleVariationChance");
        }
    }

    private void Draw(string propertyName, bool includeChildren = false)
    {
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(propertyName),
            includeChildren);
    }
}

