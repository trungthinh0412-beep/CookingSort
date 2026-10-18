using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelEditorSetting", menuName = "ScriptableObject/LevelEditorSetting")]
public class LevelEditorSetting : ScriptableObject
{
    public Vector2 windowMinSize = new Vector2(440, 560);
    public string levelDirectory = "Assets/_Project/Levels";
    public CardConfig cardConfig;
    public string clickBlockerPath;
    public List<GameObject> trayPrefabs = new List<GameObject>();

}
