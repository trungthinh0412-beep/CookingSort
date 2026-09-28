#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class RopeNodeSelectorWindow : EditorWindow
{
    public static void Open(RopeMaker rope)
    {
        var window = GetWindow<RopeNodeSelectorWindow>(true, "Rope node selector");
        if (window)
        {
            window.SetTarget(rope);
            window.Show();
        }
    }


    static PreviewGenerator _PreviewGenerator = new PreviewGenerator()
    {
        width = 512,
        height = 512,
        sizingType = PreviewGenerator.ImageSizeType.Fill,
        transparentBackground = true,
        imageFilterMode = FilterMode.Bilinear,
    };


    RopeMaker targetRope;
    List<RopeNode> originalPrefabs;
    List<RopeNode> currentPrefabs;

    List<RopeNode> assetPrefabs;
    Dictionary<RopeNode, Texture2D> assetPreviews;

    Vector2 overalScrollPos;
    Vector2 currentPreviewScrollPos;
    Vector2 assetPreviewScrollPos;




    private void OnDestroy()
    {
        ClearPreviews();
    }

    void SetTarget(RopeMaker rope)
    {
        targetRope = rope;
        originalPrefabs = targetRope ? targetRope.ropeNodePrefabs : default;
        currentPrefabs = new List<RopeNode>(originalPrefabs);
        RefindAssets();
    }

    void RefindAssets()
    {
        assetPrefabs = AssetUtils.FindAllAssetComponents<RopeNode>();

        ClearPreviews();
        assetPreviews = assetPrefabs.ToDictionary(pr => pr, pr => _PreviewGenerator.CreatePreview(pr.gameObject));
    }

    void ClearPreviews()
    {
        if (assetPreviews != null)
        {
            foreach (var pr in assetPreviews)
            {
                DestroyImmediate(pr.Value);
            }
            assetPreviews.Clear();
        }
    }


    private void OnGUI()
    {
        if (assetPreviews == null)
        {
            Close();
            return;
        }
        if (currentPrefabs == null) currentPrefabs = new List<RopeNode>();

        EditorUtilEx.Section("Rope node selector");
        EditorGUILayout.ObjectField("Target ropeMaker", targetRope, typeof(RopeMaker), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Current nodes (grey are repeated) (click to delete)", EditorStyles.boldLabel);
        if (currentPrefabs.Count > 0)
        {
            currentPreviewScrollPos = EditorGUILayout.BeginScrollView(currentPreviewScrollPos, GUILayout.Height(80));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            {
                int renderCount = Mathf.Max(5, currentPrefabs.Count * 2);
                float width = Mathf.Clamp((Screen.width - 80) / renderCount, 40, 80);
                float height = width * 0.95f;
                for (int i = 0; i < renderCount; i++)
                {
                    var prefab = currentPrefabs.GetRepeat(i);
                    bool isBase = i < currentPrefabs.Count;
                    var gc = GUI.color;
                    GUI.color = isBase ? Color.white : Color.grey;
                    assetPreviews.TryGetValue(prefab, out var preview);
                    if (GUILayout.Button(preview, GUILayout.Height(height), GUILayout.Width(width)))
                    {
                        if (isBase && currentPrefabs.Count > 1) currentPrefabs.Remove(prefab);
                    }
                    GUI.color = gc;
                }
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndScrollView();
        }

        if (assetPrefabs != null && assetPreviews != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Available nodes", EditorStyles.boldLabel);

            UtilEx.CalculateIdealCount(Screen.width - 50, 60, 120, 6, out var perRow, out var cellWidth);
            float cellHeight = cellWidth;
            int xCount = 0;
            assetPreviewScrollPos = EditorGUILayout.BeginScrollView(assetPreviewScrollPos, GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(GUILayout.Height(cellHeight));
            GUILayout.FlexibleSpace();
            foreach (var kvp in assetPreviews)
            {
                if (xCount >= perRow)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal(GUILayout.Height(cellHeight));
                    GUILayout.FlexibleSpace();
                    xCount = 0;
                }
                if (GUILayout.Button(kvp.Value, GUILayout.Width(cellWidth), GUILayout.Height(cellHeight)))
                {
                    if (kvp.Key) currentPrefabs.Add(kvp.Key);
                }
                xCount++;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("No nodes available", EditorStyles.boldLabel);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        var ec = Event.current;
        if (GUILayout.Button("Reset")) DoReset();
        if (GUILayout.Button("Accept (enter)") || ec.type == EventType.KeyDown && ec.keyCode == KeyCode.Return) Accept();
        if (GUILayout.Button("Cancel (esc)") || ec.type == EventType.KeyDown && ec.keyCode == KeyCode.Escape) Cancel();
        EditorGUILayout.EndHorizontal();

    }



    void DoReset()
    {
        currentPrefabs = new List<RopeNode>(originalPrefabs);
    }

    void Accept()
    {
        if (targetRope)
        {
            Undo.RegisterCompleteObjectUndo(targetRope, "Set nodes");
            targetRope.ropeNodePrefabs = new List<RopeNode>(currentPrefabs);
            EditorUtility.SetDirty(targetRope);
            RopeMakerEditor.TryCreateRope(targetRope);
        }
        Close();
    }

    void Cancel()
    {
        Close();
    }
}

#endif