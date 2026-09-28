using UnityEngine;

public partial class PlayerData
{
    [SerializeField] private int currentShuffle;
    [SerializeField] private int currentBomb;
    [SerializeField] private int currentMoreDeal = 3;
    [SerializeField] private int currentMagicSwap = 3;
    [SerializeField] private int currentMagnet = 3;

    public int CurrentShuffle
    {
        get => currentShuffle;
        set
        {
            currentShuffle = value;
            Observer.UseBooster?.Invoke(BoosterType.Shuffle);
        }
    }

    public int CurrentBomb
    {
        get => currentBomb;
        set
        {
            currentBomb = value;
            Observer.UseBooster?.Invoke(BoosterType.Bomb);
        }
    }

    public int CurrentMoreDeal
    {
        get => currentMoreDeal;
        set
        {
            currentMoreDeal = Mathf.Max(0, value);
            Observer.UseBooster?.Invoke(BoosterType.MoreDeal);
        }
    }

    public int CurrentMagicSwap
    {
        get => currentMagicSwap;
        set
        {
            currentMagicSwap = Mathf.Max(0, value);
            Observer.UseBooster?.Invoke(BoosterType.MagicSwap);
        }
    }

    public int CurrentMagnet
    {
        get => currentMagnet;
        set
        {
            currentMagnet = Mathf.Max(0, value);
            Observer.UseBooster?.Invoke(BoosterType.Magnet);
        }
    }
}
