using UnityEditor;
using UnityEngine;

public class CleanMissingScripts
{
    [MenuItem("Tools/Clean All Missing Scripts")]
    public static void Clean()
    {
        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int count = 0;
        foreach (string guid in prefabPaths)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                int cleaned = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefab);
                if (cleaned > 0)
                {
                    count += cleaned;
                    Debug.Log($"Cleaned {cleaned} missing scripts from {path}");
                    EditorUtility.SetDirty(prefab);
                }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Totally cleaned {count} missing scripts.");
    }
}