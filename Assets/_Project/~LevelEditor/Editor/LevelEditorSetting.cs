using UnityEngine;
using MagicSoft.LoopSort;

[CreateAssetMenu(fileName = "LevelEditorSetting", menuName = "ScriptableObject/LevelEditorSetting")]
public class LevelEditorSetting : ScriptableObject
{
    public Vector2 windowMinSize = new Vector2(440, 560);
    public string levelDirectory = "Assets/_Project/Legacy/LoopSort/Levels";
    public GameObject levelBasePrefab;
    public GameObject loopPrefab;
    public GameObject cubePrefab;
    public GameObject containerPrefab;
    public LoopColorDatabase colorDatabase;
}
