using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AssetUtils
{
#if UNITY_EDITOR

    /// <summary>
    /// Create all folders if not exist
    /// "Assets/A/B/C" or "Assets/A/B/C/"
    /// </summary>
    public static bool CreateFolderRecursive(
        string folderRelative)
    {
        folderRelative = folderRelative.TrimEnd('/');
        if (!AssetDatabase.IsValidFolder(folderRelative))
        {
            string[] ss = folderRelative.Split('/');
            for (int i = 1; i < ss.Length; i++)
            {
                string s0 = "";
                for (int j = 0; j < i; j++)
                {
                    s0 += ss[j] + (j < i - 1 ? "/" : "");
                }

                string s1 = ss[i];

                if (!AssetDatabase.IsValidFolder(s0 + "/" + s1))
                {
                    string s = AssetDatabase.CreateFolder(s0, s1);
                    if (i == ss.Length - 1 && !string.IsNullOrEmpty(s)) return true;
                }
            }
        }

        return false;
    }


    /// <summary>
    /// Get the object size of a texture2D object
    /// </summary>
    public static Vector2Int GetImageSize(
        Texture2D asset)
    {
        if (asset)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer != null)
            {
                object[] args = {0, 0};
                var mi = typeof(TextureImporter).GetMethod("GetWidthAndHeight", BindingFlags.NonPublic | BindingFlags.Instance);
                mi.Invoke(importer, args);

                var width = (int) args[0];
                var height = (int) args[1];

                return new Vector2Int(width, height);
            }
        }

        return Vector2Int.zero;
    }

    /// <summary>
    /// Get pixels of texture regardless of isReadable
    /// </summary>
    public static Color[] GetTexturePixelsSafe(
        Texture2D tex)
    {
        if (tex.isReadable) return tex.GetPixels();
        else
        {
            RenderTexture tmp = RenderTexture.GetTemporary(tex.width,
                tex.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear);
            Graphics.Blit(tex, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            Texture2D myTexture2D = new Texture2D(tex.width, tex.height);
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            myTexture2D.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);
            return myTexture2D.GetPixels();
        }
    }


    /// <summary>
    /// Open a scene in folder "Assets/Scene/"
    /// </summary>
    public static void LoadSceneInAsset(
        string name)
    {
        string path = "Assets/Scenes/" + name + ".unity";
        Scene active = SceneManager.GetActiveScene();
        if (!active.name.Equals(name) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }


    /// <summary>
    /// Get the sub assets of type <typeparamref name="T"/> inside <paramref name="asset"/>
    /// </summary>
    public static List<T> GetSubAssets<T>(
        Object asset)
        where T : Object
    {
        var path = AssetDatabase.GetAssetPath(asset);
        var subs = AssetDatabase.LoadAllAssetsAtPath(path);
        return subs.Select(o => o as T)
            .Where(o => o)
            .ToList();
    }

    /// <summary>
    /// Get the sub assets of type <typeparamref name="T"/> at <paramref name="path"/>
    /// </summary>
    public static List<T> GetSubAssets<T>(
        string path)
        where T : Object
    {
        var subs = AssetDatabase.LoadAllAssetsAtPath(path);
        return subs.Select(o => o as T)
            .Where(o => o)
            .ToList();
    }


    /// <summary>
    /// Check if an object is an asset
    /// </summary>
    public static bool IsAsset(
        Object o)
    {
        return AssetDatabase.Contains(o);
    }

    /// <summary>
    /// Get GUID quickly
    /// </summary>
    public static string GetAssetGUID(
        Object asset)
    {
        return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    }

    /// <summary>
    /// Disable model material import
    /// </summary>
    public static void DisableModelImportMaterials(
        params string[] guids)
    {
        AssetDatabase.StartAssetEditing();
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
            }
        }

        AssetDatabase.StopAssetEditing();
    }


    /// <summary>
    /// Find assets of type T in folders
    /// </summary>
    public static T[] FindAssets<T>(
        string search,
        params string[] folders)
        where T : Object
    {
        var guids = AssetDatabase.FindAssets(search, folders);
        return guids.Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(o => o)
            .ToArray();
    }


    /// <summary>
    /// Convert from Assets/abc/xyz to C://b/n/m/Assets/abc/xyz
    /// </summary>
    public static string ConvertPathToAbsolute(
        string relativePath)
    {
        var dataPath = Application.dataPath;
        dataPath = dataPath.Substring(0, dataPath.IndexOf("/Assets"));
        return System.IO.Path.Combine(dataPath, relativePath);
    }

    /// <summary>
    /// Convert from C://b/n/m/Assets/abc/xyz to Assets/abc/xyz
    /// </summary>
    public static string ConvertPathToRelative(
        string absolutePath)
    {
        if (absolutePath.StartsWith(Application.dataPath))
        {
            return "Assets" + absolutePath.Substring(Application.dataPath.Length);
        }

        return absolutePath.Substring(absolutePath.LastIndexOf("Assets/"));
    }
#endif


    /// <summary>
    /// Find all assets of type <typeparamref name="T"/>.
    /// In editor it uses AssetDatabase.
    /// In runtime it uses Resources.FindObjectsOfTypeAll
    /// </summary>
    public static List<T> FindAllAssets<T>()
        where T : Object
    {
        List<T> l = new List<T>();
#if UNITY_EDITOR
        var typeStr = typeof(T).ToString();
        typeStr = typeStr.Replace("UnityEngine.", "");

        if (typeof(T) == typeof(SceneAsset)) typeStr = "Scene";
        else if (typeof(T) == typeof(GameObject)) typeStr = "gameobject";

        var guids = AssetDatabase.FindAssets("t:" + typeStr);
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            T obj = AssetDatabase.LoadAssetAtPath<T>(path);
            if (obj != null) l.Add(obj);
        }
#else
            l.AddRange(Resources.FindObjectsOfTypeAll<T>());
#endif
        return l;
    }


    /// <summary>
    /// Find all components <typeparamref name="T"/> of root prefab GameObjects
    /// </summary>
    public static List<T> FindAllAssetComponents<T>()
        where T : Component
    {
        var gos = FindAllAssets<GameObject>();
        return gos.SelectMany(go => go.GetComponents<T>())
            .ToList();
    }


    /// <summary>
    /// Set obj as dirty
    /// </summary>
    /// <param name="obj"></param>
    public static void SetDirty(
        Object obj)
    {
#if UNITY_EDITOR
        EditorUtility.SetDirty(obj);
#endif
    }
}