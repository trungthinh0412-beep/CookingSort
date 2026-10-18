using UnityEngine;

namespace CookingSort2D
{
    public enum CookingBlastLevelLoopType
    {
        Recycle,
        Random
    }

    [CreateAssetMenu(menuName = "Cooking Blast/Level Config", fileName = "CookingBlastLevelConfig")]
    public sealed class CookingBlastLevelConfig : ScriptableObject
    {
        [Min(1)] public int maxLevel = 1;
        [Min(1)] public int startLoopLevel = 1;
        public CookingBlastLevelLoopType loopType = CookingBlastLevelLoopType.Recycle;

        public int ResolveLevelIndex(int playerLevel)
        {
            int safeMax = Mathf.Max(1, maxLevel);
            playerLevel = Mathf.Max(1, playerLevel);
            if (playerLevel <= safeMax) return playerLevel;

            if (loopType == CookingBlastLevelLoopType.Random)
                return Random.Range(Mathf.Clamp(startLoopLevel, 1, safeMax), safeMax + 1);

            int start = Mathf.Clamp(startLoopLevel, 1, safeMax);
            int length = safeMax - start + 1;
            return start + (playerLevel - start) % length;
        }
    }
}
