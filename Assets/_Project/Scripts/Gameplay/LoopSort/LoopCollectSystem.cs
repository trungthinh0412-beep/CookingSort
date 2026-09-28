using System;

namespace MagicSoft.LoopSort
{
    public static class LoopCollectSystem
    {
        public static bool CrossedDistance(
            float previousAbsoluteDistance,
            float currentAbsoluteDistance,
            float collectDistance,
            float pathLength)
        {
            if (pathLength <= 0f ||
                currentAbsoluteDistance <= previousAbsoluteDistance)
            {
                return false;
            }

            double previousLap = Math.Floor(
                (previousAbsoluteDistance - collectDistance) /
                pathLength);
            double currentLap = Math.Floor(
                (currentAbsoluteDistance - collectDistance) /
                pathLength);
            return currentLap > previousLap;
        }
    }
}
