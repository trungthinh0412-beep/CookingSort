using UnityEngine;

[CreateAssetMenu(
    fileName = "CardBurnSettings",
    menuName = "Solitaire Sort/FX/Card Burn Settings")]
public sealed class CardBurnSettings : ScriptableObject
{
    [Header("Timing")]
    [Min(0.08f)] public float duration = .525f;
    [Min(0.1f)] public float dissolveSpeed = 0.78f;
    [Range(0f, 0.35f)] public float flashDuration = 0.18f;

    [Header("Burn Edge")]
    [Range(0.005f, 0.3f)] public float burnEdgeWidth = 0.2f;
    [ColorUsage(true, true)]
    public Color burnEdgeColor = new Color(1f, 0.22f, 0.015f, 1f);
    [ColorUsage(true, true)]
    public Color burnCoreColor = new Color(1f, 0.96f, 0.68f, 1f);
    [Range(0f, 4f)] public float emissionStrength = 2.5f;
    [Range(1f, 1.3f)] public float additiveGlowScale = 1.12f;
    [Range(0f, 5f)] public float additiveGlowStrength = 2.2f;

    [Header("Noise")]
    [Range(1f, 20f)] public float noiseScale = 3.6f;
    [Range(0f, 4f)] public float noiseSpeed = 0.22f;
    [Range(0f, 0.5f)] public float noiseStrength = 0.34f;

    [Header("Direction")]
    public BurnDirection direction = BurnDirection.BottomUp;
    [Tooltip("Used by Corner Spread. (0,0) is bottom-left, (1,1) is top-right.")]
    public Vector2 burnOrigin = new Vector2(0f, 0f);

    [Header("Sparks")]
    [Range(0, 1800)] public int sparkCount = 5;
    [Range(0f, 1f)]
    [Tooltip("Dissolve progress at which embers start rising from the card.")]
    public float sparkStartProgress = 0.8f;
    [Range(0.015f, 0.3f)] public float sparkSize = 0.13f;
    [Range(0.05f, 1.5f)] public float sparkLifetime = 0.78f;
    [Range(0f, 3f)] public float sparkRiseSpeed = 1.02f;
    public Color sparkColor = new Color(1f, 0.82f, 0.16f, 1f);

    public enum BurnDirection
    {
        BottomUp,
        CornerSpread
    }

    private void OnValidate()
    {
        duration = Mathf.Max(0.08f, duration);
        dissolveSpeed = Mathf.Max(0.1f, dissolveSpeed);
        burnOrigin.x = Mathf.Clamp01(burnOrigin.x);
        burnOrigin.y = Mathf.Clamp01(burnOrigin.y);
        sparkStartProgress = Mathf.Clamp01(sparkStartProgress);
    }
}
