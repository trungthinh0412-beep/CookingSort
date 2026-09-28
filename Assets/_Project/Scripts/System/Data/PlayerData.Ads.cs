using System;
using System.Globalization;
using UnityEngine;

public partial class PlayerData
{
    [SerializeField] private int countShowInterAds;
    public int CountShowInterAds // reset when show reward or inter
    {
        get => countShowInterAds;
        set => countShowInterAds = value;
    }
}
