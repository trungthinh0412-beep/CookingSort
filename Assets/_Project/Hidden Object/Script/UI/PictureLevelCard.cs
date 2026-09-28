using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PictureLevelCard : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image picture;
    [SerializeField] private TMP_Text caption;
    [SerializeField] private GameObject locked;
    private Action onClick;
    private void Awake() => button.onClick.AddListener(() => onClick?.Invoke());
    public void Bind(PictureLevelEntry level, bool completed, Action click)
    {
        caption.text = $"Level {level.levelNumber}";
        picture.sprite = completed ? level.thumbnail != null ? level.thumbnail : level.pictureA : null;
        picture.enabled = completed && picture.sprite != null;
        locked.SetActive(!completed);
        button.interactable = completed && level.IsPlayable;
        onClick = click;
    }
}
