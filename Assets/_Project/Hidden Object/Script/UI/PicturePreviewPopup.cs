using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PicturePreviewPopup : Popup
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text status;
    [SerializeField] private Image pictureA;
    [SerializeField] private Image pictureB;
    [SerializeField] private Button backButton;
    [SerializeField] private Button replayButton;
    public PictureAlbumData Album { get; private set; }
    public PictureLevelEntry Entry { get; private set; }
    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        backButton.onClick.AddListener(() => PopupController.Instance.ReturnAlbumFromPreview());
        replayButton.onClick.AddListener(() => GameManager.Instance.PlayPictureReplay(Album, Entry));
    }
    public void SelectPicture(PictureAlbumData album, PictureLevelEntry entry, string message = "")
    {
        Album = album;
        Entry = entry;
        title.text = $"{album.title} · {entry.levelNumber}";
        status.text = message;
        pictureA.sprite = entry.pictureA;
        pictureB.sprite = entry.pictureB;
        pictureA.enabled = pictureA.sprite != null;
        pictureB.enabled = pictureB.sprite != null;
        replayButton.interactable = entry.IsPlayable && Data.PlayerData.HasCompletedPicture(entry.levelId);
    }
}
