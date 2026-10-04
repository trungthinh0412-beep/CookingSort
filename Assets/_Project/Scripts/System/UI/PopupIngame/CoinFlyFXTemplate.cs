using UnityEngine;

/// <summary>
/// Reusable asset root for the merge coin visual. Scene specific target and
/// canvas references remain on PopupInGame.
/// </summary>
public sealed class CoinFlyFXTemplate : MonoBehaviour
{
    [SerializeField] private CoinFlyFXSettings settings;
    [SerializeField] private RectTransform coinViewPrefab;

    public CoinFlyFXSettings Settings => settings;
    public RectTransform CoinViewPrefab => coinViewPrefab;

    public void ConfigureForSetup(
        CoinFlyFXSettings newSettings,
        RectTransform newCoinViewPrefab)
    {
        settings = newSettings;
        coinViewPrefab = newCoinViewPrefab;
    }
}
