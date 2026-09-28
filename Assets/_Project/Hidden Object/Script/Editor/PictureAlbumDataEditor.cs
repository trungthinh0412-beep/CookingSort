using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PictureAlbumData))]
public sealed class PictureAlbumDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Drag a world Level prefab into each Level Prefab reference. Set its main level number. Sync reads A/B directly from its SpriteRenderers. Keep IDs stable after release.",MessageType.Info);
        if(GUILayout.Button("Sync A/B From Level Prefabs"))
        { PictureCollectionPrefabSetup.SyncAlbum((PictureAlbumData)target); AssetDatabase.SaveAssets(); }
        var album=(PictureAlbumData)target;
        if(album.levels==null) return;
        foreach(var entry in album.levels)
            if(entry!=null && entry.IsPlayable && (entry.pictureA==null || entry.pictureB==null))
                EditorGUILayout.HelpBox("Missing cached A/B: assign the level's sprites and press Sync.",MessageType.Warning);
    }
}
