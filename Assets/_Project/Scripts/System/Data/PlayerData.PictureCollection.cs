using System.Collections.Generic;
using UnityEngine;

public partial class PlayerData
{
    [SerializeField] private List<string> completedPictureLevelIds = new List<string>();
    public List<string> CompletedPictureLevelIds
    {
        get => completedPictureLevelIds ?? (completedPictureLevelIds = new List<string>());
        set => completedPictureLevelIds = value ?? new List<string>();
    }
    public int PictureCollectionSaveVersion { get; set; }
    public bool HasCompletedPicture(string id) => !string.IsNullOrEmpty(id) && CompletedPictureLevelIds.Contains(id);
    public bool MarkPictureCompleted(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || HasCompletedPicture(id)) return false;
        CompletedPictureLevelIds.Add(id);
        return true;
    }
}
