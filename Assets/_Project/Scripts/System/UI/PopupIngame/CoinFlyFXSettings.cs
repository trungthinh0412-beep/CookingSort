using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CoinFlyFXSettings",
    menuName = "Game FX/Coin Fly FX Settings"
)]
public sealed class CoinFlyFXSettings : ScriptableObject
{
    [Tooltip("Frames must be ordered numerically: 0 through 10.")]
    [SerializeField] private List<Sprite> coinSpinFrames = new List<Sprite>();
    [SerializeField, Min(1f)] private float spinFrameRate = 24f;
    [SerializeField, Range(1, 16)] private int previewCoinCount = 12;

    public IReadOnlyList<Sprite> CoinSpinFrames => coinSpinFrames;
    public float SpinFrameRate => spinFrameRate;
    public int PreviewCoinCount => previewCoinCount;

    public bool HasExpectedCoinSpinFrames
    {
        get
        {
            if (coinSpinFrames == null || coinSpinFrames.Count != 11)
                return false;

            for (int i = 0; i < coinSpinFrames.Count; i++)
            {
                if (coinSpinFrames[i] == null)
                    return false;
            }

            return true;
        }
    }

    public void ConfigureForSetup(
        IEnumerable<Sprite> frames,
        float frameRate,
        int defaultPreviewCount)
    {
        coinSpinFrames = frames != null
            ? new List<Sprite>(frames)
            : new List<Sprite>();
        spinFrameRate = Mathf.Max(1f, frameRate);
        previewCoinCount = Mathf.Clamp(defaultPreviewCount, 1, 16);
    }
}
