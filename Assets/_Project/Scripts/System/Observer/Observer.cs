using System;
using UnityEngine;

public static partial class Observer
{
    public static Action DebugChanged;
    public static Action<int> GoldChanged;
    public static Action GoldChangedDone;
    public static Action<int> StarChanged;
    public static Action StarChangedDone;
    public static Action<int> HeartChanged;
    public static Action HeartChangedDone;
    public static Action<Vector3> SpawnResourcesChanged;
    public static Action CurrentChapterChanged;
    public static Action ProfileChanged;
    public static Action<int> LevelChanged;
    public static Action<string, Vector3> Notify;
}
