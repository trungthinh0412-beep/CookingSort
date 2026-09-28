using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[Serializable]
public sealed class PictureLevelEntry
{
    [Tooltip("Stable save ID generated from the prefab GUID. Keep unchanged after release.")]
    public string levelId;
    [Min(1)] public int levelNumber = 1;
    public AssetReferenceGameObject levelPrefab;
    [Tooltip("Optional thumbnail override. A/B are copied from the world prefab by the editor.")]
    public Sprite thumbnail;
    [HideInInspector] public Sprite pictureA;
    [HideInInspector] public Sprite pictureB;
    public bool IsPlayable => levelPrefab != null && levelPrefab.RuntimeKeyIsValid();
}

[CreateAssetMenu(menuName = "Differences/Picture Album")]
public sealed class PictureAlbumData : ScriptableObject
{
    public string albumId;
    public string title;
    public Sprite coverSprite;
    public List<PictureLevelEntry> levels = new List<PictureLevelEntry>();
    public int Total => levels == null ? 0 : levels.Count;
    public int CompletedCount(PlayerData player)
    {
        int count = 0;
        if (levels != null && player != null)
            foreach (var entry in levels)
                if (entry != null && player.HasCompletedPicture(entry.levelId)) count++;
        return count;
    }
}
