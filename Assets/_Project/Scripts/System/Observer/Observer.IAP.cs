using System;

public partial class Observer
{
    public static Action PurchasePackComplete;
    public static Action IAPProductsUpdated;
    public static Action<PackName, int> PurchasePackGoldGranted;
}
