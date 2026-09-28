using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PictureAlbumCard : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image cover;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text progress;
    [SerializeField] private GameObject locked;
    private Action onClick;
    private void Awake() => button.onClick.AddListener(() => onClick?.Invoke());
    public void Bind(PictureAlbumData album, bool unlocked, PlayerData player, Action click)
    {
        title.text = album.title;
        progress.text = $"{album.CompletedCount(player)}/{album.Total}";
        cover.sprite = unlocked ? album.coverSprite : null;
        cover.enabled = unlocked && cover.sprite != null;
        locked.SetActive(!unlocked);
        button.interactable = unlocked && album.Total > 0;
        onClick = click;
    }
}
