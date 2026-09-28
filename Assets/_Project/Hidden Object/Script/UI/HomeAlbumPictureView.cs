using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeAlbumPictureView : MonoBehaviour
{
    [SerializeField] private PictureCollectionConfig collection;
    [SerializeField] private Image cover;
    [SerializeField] private AlbumPuzzleGraphic puzzle;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text progress;
    public PictureAlbumData Album { get; private set; }
    private void OnEnable() => Refresh();
    public void Refresh()
    {
        if (collection == null || Data.PlayerData == null) return;
        Album = collection.HomeAlbum(Data.PlayerData);
        if (Album == null) { cover.enabled = false; title.text = "Picture Collection"; progress.text = ""; return; }
        cover.sprite = Album.coverSprite;
        cover.enabled = cover.sprite != null;
        var fitter = cover.GetComponentInParent<AspectRatioFitter>();
        if (fitter != null && cover.sprite != null) fitter.aspectRatio = cover.sprite.rect.width / cover.sprite.rect.height;
        title.text = Album.title;
        progress.text = $"{Album.CompletedCount(Data.PlayerData)}/{Album.Total}";
        var revealed = new bool[Album.Total];
        for (int i = 0; i < revealed.Length; i++)
            revealed[i] = Album.levels[i] != null && Data.PlayerData.HasCompletedPicture(Album.levels[i].levelId);
        puzzle.SetPieces(revealed);
    }
}
