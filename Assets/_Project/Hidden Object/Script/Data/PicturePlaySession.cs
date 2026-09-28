// Captures the selected level separately from the player's next main level.
public sealed class PicturePlaySession
{
    public readonly PictureAlbumData Album;
    public readonly PictureLevelEntry Entry;
    public readonly int LevelNumber;
    public readonly bool IsReplay;
    public bool CanClaimReward { get; private set; }
    private bool finished;
    public PicturePlaySession(PictureAlbumData album, PictureLevelEntry entry, int levelNumber, bool replay)
    { Album = album; Entry = entry; LevelNumber = levelNumber; IsReplay = replay; }
    public bool Complete(PlayerData player)
    {
        if (finished) return false;
        finished = true;
        if (IsReplay) return false;
        CanClaimReward = Entry == null || player.MarkPictureCompleted(Entry.levelId);
        player.CurrentLevelIndex = LevelNumber + 1;
        player.CountShowInterAds++;
        return CanClaimReward;
    }
}
