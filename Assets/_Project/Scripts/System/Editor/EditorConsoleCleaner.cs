using UnityEditor;
using UnityEngine;
using System.Reflection;

public static class EditorConsoleCleaner
{
    [MenuItem("Tools/Clear Console And Missing Scripts")]
    public static void CleanUp()
    {
        ClearConsole();
        RemoveMissingScriptsFromCurrentScene();
        Debug.Log("Đã dọn dẹp Console và xóa các Script bị 'Missing' trên Scene hiện tại!");
    }

    public static void ClearConsole()
    {
        var assembly = Assembly.GetAssembly(typeof(SceneView));
        var type = assembly.GetType("UnityEditor.LogEntries");
        var method = type.GetMethod("Clear");
        method.Invoke(new object(), null);
    }

    private static void RemoveMissingScriptsFromCurrentScene()
    {
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        int count = 0;
        foreach (var go in rootObjects)
        {
            count += RemoveMissingScriptsInChildren(go);
        }
        Debug.Log($"Removed {count} missing scripts from active scene.");
    }

    private static int RemoveMissingScriptsInChildren(GameObject go)
    {
        int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        foreach (Transform child in go.transform)
        {
            count += RemoveMissingScriptsInChildren(child.gameObject);
        }
        return count;
    }
}
