using System;
using UnityEngine;

public static partial class Observer
{
    public static Action<GameObject> StartLevel;
    public static Action<GameObject> ReplayLevel;
    public static Action<GameObject> SkipLevel;
    public static Action<GameObject> WinLevel;
    public static Action<GameObject> LoseLevel;

    public static Action<Vector3> FoodBoxCompleted;
}
