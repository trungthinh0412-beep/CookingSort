using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PictureAlbumPopup : Popup
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private Button backButton;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private PictureLevelCard cardPrefab;
    private readonly List<PictureLevelCard> cards = new List<PictureLevelCard>();
    public PictureAlbumData Album { get; private set; }
    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        backButton.onClick.AddListener(() => PopupController.Instance.ReturnLibraryFromAlbum());
    }
    public void SelectAlbum(PictureAlbumData album)
    {
        bool changed = Album != album;
        Album = album;
        Refresh();
        if (changed) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1; }
    }
    protected override void BeforeShow() { base.BeforeShow(); Refresh(); }
    private void Refresh()
    {
        if (Album == null) return;
        title.text = Album.title;
        for (int i = 0; i < Album.Total; i++)
        {
            if (i == cards.Count) cards.Add(Instantiate(cardPrefab, scroll.content));
            var entry = Album.levels[i];
            cards[i].gameObject.SetActive(entry != null);
            if (entry != null) cards[i].Bind(entry, Data.PlayerData.HasCompletedPicture(entry.levelId),
                () => PopupController.Instance.ShowPicturePreview(Album, entry));
        }
        for (int i = Album.Total; i < cards.Count; i++) cards[i].gameObject.SetActive(false);
    }
}
