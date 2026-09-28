using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Differences/Picture Collection Config")]
public sealed class PictureCollectionConfig : ScriptableObject
{
    public List<PictureAlbumData> albums = new List<PictureAlbumData>();
    public PictureLevelEntry FindLevel(int number)
    {
        foreach (var album in albums)
            if (album != null && album.levels != null)
                foreach (var level in album.levels)
                    if (level != null && level.levelNumber == number) return level;
        return null;
    }
    public PictureAlbumData FindAlbum(PictureLevelEntry entry)
    {
        foreach (var album in albums)
            if (album != null && album.levels != null && album.levels.Contains(entry)) return album;
        return null;
    }
    public bool IsUnlocked(PictureAlbumData target, PlayerData player)
    {
        foreach (var album in albums)
        {
            if (album == null) continue;
            if (album == target) return true;
            if (album.Total == 0 || album.CompletedCount(player) < album.Total) return false;
        }
        return false;
    }
    public PictureAlbumData HomeAlbum(PlayerData player)
    {
        var next = FindAlbum(FindLevel(player.CurrentLevelIndex));
        if (next != null && IsUnlocked(next, player)) return next;
        PictureAlbumData last = null;
        foreach (var album in albums)
        {
            if (album == null) continue;
            last = album;
            if (album.CompletedCount(player) < album.Total) return album;
        }
        return last;
    }
    public bool MigrateProgress(PlayerData player)
    {
        if (player.PictureCollectionSaveVersion >= 1) return false;
        foreach (var album in albums)
            if (album != null && album.levels != null)
                foreach (var entry in album.levels)
                    if (entry != null && entry.levelNumber < player.CurrentLevelIndex)
                        player.MarkPictureCompleted(entry.levelId);
        player.PictureCollectionSaveVersion = 1;
        return true;
    }
}
