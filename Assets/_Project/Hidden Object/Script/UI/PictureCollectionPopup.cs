using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class PictureCollectionPopup : Popup
{
    [SerializeField] private PictureCollectionConfig collection;
    [SerializeField] private Button backButton;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private PictureAlbumCard cardPrefab;
    private readonly List<PictureAlbumCard> cards = new List<PictureAlbumCard>();
    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        backButton.onClick.AddListener(OnClickClose);
    }
    protected override void BeforeShow()
    {
        base.BeforeShow();
        if (collection == null) return;
        int count = 0;
        foreach (var album in collection.albums)
        {
            if (album == null) continue;
            if (count == cards.Count) cards.Add(Instantiate(cardPrefab, scroll.content));
            cards[count].gameObject.SetActive(true);
            cards[count++].Bind(album, collection.IsUnlocked(album, Data.PlayerData), Data.PlayerData,
                () => PopupController.Instance.ShowPictureAlbum(album));
        }
        for (int i = count; i < cards.Count; i++) cards[i].gameObject.SetActive(false);
    }

    public void OnClickClose()
    {
        PopupController.Instance.ReturnHomeFromPictureCollection();
    }
}
