using UnityEngine;

[CreateAssetMenu(fileName = "HeartConfig", menuName = "ScriptableObject/HeartConfig")]
public class HeartConfig : ScriptableObject
{
    public int maxHeart = 5;
    public int refillTimeInSeconds = 600; // 10 minutes
}
