using System;
using UnityEngine;

public static partial class Observer
{
    public static Action<Level> StartLevel;
    public static Action<Level> ReplayLevel;
    public static Action<Level> SkipLevel;
    public static Action<Level> WinLevel;
    public static Action<Level> LoseLevel;

    public static Action<Vector3> FoodBoxCompleted;
}
