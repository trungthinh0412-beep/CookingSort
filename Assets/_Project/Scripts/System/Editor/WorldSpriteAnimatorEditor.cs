using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldSpriteAnimator))]
[CanEditMultipleObjects]
public class WorldSpriteAnimatorEditor : Editor
{
    private SerializedProperty _script;
    private SerializedProperty _targetRenderer;
    private SerializedProperty _animations;
    private SerializedProperty _playOnEnable;
    private SerializedProperty _playOnEnableAnimation;
    private SerializedProperty _ignoreTimeScale;

    private void OnEnable()
    {
        _script = serializedObject.FindProperty("m_Script");
        _targetRenderer = serializedObject.FindProperty("targetRenderer");
        _animations = serializedObject.FindProperty("animations");
        _playOnEnable = serializedObject.FindProperty("playOnEnable");
        _playOnEnableAnimation = serializedObject.FindProperty("playOnEnableAnimation");
        _ignoreTimeScale = serializedObject.FindProperty("ignoreTimeScale");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(_script);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Reference", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_targetRenderer);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Animation Library", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Moi phan tu la mot animation rieng: dat Name, Frames, FPS, Loop, Ping Pong va Loop Delay.",
            MessageType.Info
        );
        EditorGUILayout.PropertyField(_animations, true);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUIStyle toggleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            fixedHeight = 26f
        };

        bool newPlayOnEnable = EditorGUILayout.ToggleLeft(
            new GUIContent(
                "PLAY ON ENABLE - Tu dong chay khi GameObject duoc bat",
                "Bat de animation tu chay trong OnEnable."
            ),
            _playOnEnable.boolValue,
            toggleStyle
        );

        if (newPlayOnEnable != _playOnEnable.boolValue)
        {
            _playOnEnable.boolValue = newPlayOnEnable;
        }

        EditorGUILayout.EndVertical();

        DrawDefaultAnimationPopup();
        EditorGUILayout.PropertyField(_ignoreTimeScale);

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying && targets.Length == 1)
        {
            DrawRuntimeControls((WorldSpriteAnimator)target);
        }
    }

    private void DrawDefaultAnimationPopup()
    {
        if (_playOnEnable.hasMultipleDifferentValues || _animations.arraySize == 0)
        {
            EditorGUILayout.PropertyField(
                _playOnEnableAnimation,
                new GUIContent("Default Animation")
            );
            return;
        }

        List<string> animationNames = new List<string>();

        for (int i = 0; i < _animations.arraySize; i++)
        {
            SerializedProperty element = _animations.GetArrayElementAtIndex(i);
            SerializedProperty nameProperty =
                element.FindPropertyRelative("animationName");
            string animationName = nameProperty.stringValue;

            if (string.IsNullOrWhiteSpace(animationName))
            {
                animationName = $"Animation {i + 1}";
            }

            animationNames.Add(animationName);
        }

        int selectedIndex = animationNames.IndexOf(_playOnEnableAnimation.stringValue);
        selectedIndex = Mathf.Max(0, selectedIndex);

        int newIndex = EditorGUILayout.Popup(
            "Default Animation",
            selectedIndex,
            animationNames.ToArray()
        );

        _playOnEnableAnimation.stringValue = animationNames[newIndex];
    }

    private static void DrawRuntimeControls(WorldSpriteAnimator animator)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Current Animation", animator.CurrentAnimationName);
        EditorGUILayout.LabelField("Current Frame", animator.CurrentFrame.ToString());

        string playbackState = animator.IsWaitingForLoop
            ? "Waiting For Loop"
            : animator.IsPlaying
                ? "Playing"
                : "Paused / Stopped";

        EditorGUILayout.LabelField(
            "State",
            playbackState
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(animator.IsPlaying ? "Pause" : "Play"))
        {
            if (animator.IsPlaying)
            {
                animator.Pause();
            }
            else
            {
                animator.Play();
            }
        }

        if (GUILayout.Button("Restart"))
        {
            animator.Restart();
        }

        if (GUILayout.Button("Stop"))
        {
            animator.Stop();
        }

        EditorGUILayout.EndHorizontal();
    }
}
