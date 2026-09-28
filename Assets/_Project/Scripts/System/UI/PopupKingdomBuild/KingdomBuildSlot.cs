using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class BuildSlot
{
    [Tooltip("Stable ID. Do not change it after players have built this slot.")]
    [SerializeField] private string id;
    [Tooltip("The Image already positioned in the room.")]
    [SerializeField] private Image targetImage;
    [Tooltip("Shared BuildMarker position for this slot. Falls back to Target Image when empty.")]
    [SerializeField] private RectTransform markerAnchor;
    [Tooltip("Decor sprite assigned when this slot is built.")]
    [SerializeField] private Sprite sprite;
    [Tooltip("Optional ID of the Image target in PopupHome that receives this decor.")]
    [SerializeField] private string homeTargetId;
    [Tooltip("Build gems required to build this slot.")]
    [SerializeField] [Min(0)] private int cost = 1;

    public string Id => id;
    public Image TargetImage => targetImage;
    public RectTransform MarkerAnchor => markerAnchor;
    public Sprite Sprite => sprite;
    public string HomeTargetId => homeTargetId;
    public int Cost => Mathf.Max(0, cost);
}

[Serializable]
public class KingdomRoom
{
    [Tooltip("Stable room ID used by the save system.")]
    [SerializeField] private string id;
    [Tooltip("Room root containing the background and fixed target Images.")]
    [SerializeField] private RectTransform roomRoot;
    [Tooltip("Background Image used by this room. It replaces PopupHome Background Sprite only.")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private List<BuildSlot> buildSlots = new List<BuildSlot>();

    public string Id => id;
    public RectTransform RoomRoot => roomRoot;
    public Image BackgroundImage => backgroundImage;
    public List<BuildSlot> BuildSlots => buildSlots;
}

public enum KingdomRoomState
{
    NotConfigured,
    InProgress,
    Completed,
}
