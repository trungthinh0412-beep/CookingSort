using System;

public static partial class Observer
{
    public static Action<BoosterType> UseBooster;
    public static Action<BoosterType?> ActiveBoosterChanged;
    public static Action<CardType> PreLevelCardChanged;
}
