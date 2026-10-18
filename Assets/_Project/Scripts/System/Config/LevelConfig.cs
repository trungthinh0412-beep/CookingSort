using UnityEngine;

[CreateAssetMenu(fileName = "LevelConfig", menuName = "ScriptableObject/LevelConfig")]
public partial class LevelConfig : ScriptableObject
{
    public LevelLoopType levelLoopType = LevelLoopType.Recycle;
    public int maxLevel;
    public int startLoopLevel;
}

public enum LevelLoopType
{
    Recycle,
    Random,
}public partial class LevelConfig { public System.Collections.Generic.List<int> loopLevels; }
