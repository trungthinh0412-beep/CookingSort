using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

public class ChangeNameAndAddressableEditor : EditorWindow
{
    [SerializeField] private int levelFrom;
    [SerializeField] private List<GameObject> levelNormals = new();
    [SerializeField] private string moreString;
    private SerializedObject so;
    private SerializedProperty levelNormalsProp;
    private SerializedProperty levelFromField;
    private SerializedProperty moreStringField;
    private Vector2 scroll;
    [MenuItem("Tools/Change Name And Addressable")]
    public static void Open()
    {
        GetWindow<ChangeNameAndAddressableEditor>("Change Name And Addressable");
    }

    private void OnEnable()
    {
        so = new SerializedObject(this);
        levelNormalsProp = so.FindProperty("levelNormals");
        levelFromField = so.FindProperty("levelFrom");
        moreStringField = so.FindProperty("moreString");
    }

    private void OnGUI()
    {
        so.Update();

        EditorGUILayout.LabelField("Level From", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(levelFromField);

        GUILayout.Space(5);
        EditorGUILayout.LabelField("Suffix", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(moreStringField, GUIContent.none);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Level Normals", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(300));
        EditorGUILayout.PropertyField(levelNormalsProp, true);
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);

        if (GUILayout.Button("Change Name And Addressable"))
        {
            so.ApplyModifiedProperties();
            EditorApplication.delayCall += ChangeName;
        }

        so.ApplyModifiedProperties();
    }

    private void ChangeName()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressable Settings not found!");
            return;
        }

        var suffix = (moreString ?? string.Empty).Trim();
        var changedCount = 0;

        for (int i = levelNormals.Count - 1; i >= 0; i--)
        {
            var level = levelNormals[i];
            if (level == null) continue;

            int levelNumber = levelFrom + i;
            string newName = $"Level {levelNumber}{suffix}";

            string path = AssetDatabase.GetAssetPath(level);
            string guid = AssetDatabase.AssetPathToGUID(path);

            // Rename file
            string error = AssetDatabase.RenameAsset(path, newName);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(error);
                continue;
            }

            string newPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(newPath);

            if (prefab != null)
            {
                prefab.name = newName;
                EditorUtility.SetDirty(prefab);
            }

            var entry = settings.FindAssetEntry(guid);
            if (entry != null)
            {
                entry.address = newName;
            }

            changedCount++;
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Renamed {changedCount} level(s) with suffix '{suffix}' and updated Addressables.");
    }
}
