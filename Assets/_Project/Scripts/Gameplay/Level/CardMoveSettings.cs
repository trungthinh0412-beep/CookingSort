using System;
using UnityEngine;

[Serializable]
public class CardMoveSettings
{
    [Header("Timing")]
    [Min(0.05f)] public float flightDuration = 0.325f;
    [Min(0f)] public float staggerPerCard = 0.035f;

    [Header("Path")]
    public AnimationCurve positionCurve = BuildCurve(
        new[] { 0f, 0.025f, 0.077f, 0.154f, 0.206f, 0.255f, 0.308f, 0.36f, 0.409f, 0.462f,
                0.514f, 0.563f, 0.615f, 0.692f, 0.745f, 0.794f, 0.846f, 0.898f, 0.948f, 1f },
        new[] { 0f, 0.001f, 0.015f, 0.084f, 0.14f, 0.205f, 0.285f, 0.372f, 0.465f, 0.566f,
                0.665f, 0.748f, 0.815f, 0.869f, 0.913f, 0.946f, 0.971f, 0.987f, 0.997f, 1f }
    );

    public AnimationCurve heightCurve = BuildCurve(
        new[] { 0f, 0.025f, 0.077f, 0.154f, 0.206f, 0.255f, 0.308f, 0.36f, 0.409f, 0.462f,
                0.514f, 0.563f, 0.615f, 0.692f, 0.745f, 0.794f, 0.846f, 0.898f, 0.948f, 1f },
        new[] { 0f, 0.003f, 0.039f, 0.224f, 0.365f, 0.515f, 0.679f, 0.824f, 0.937f, 1f,
                0.988f, 0.904f, 0.778f, 0.628f, 0.466f, 0.316f, 0.183f, 0.084f, 0.022f, 0f }
    );

    public float arcHeight = 0.2f;
    public float arcHeightPerDistance = 0.16f;
    [Range(0f, 1f)] public float verticalMoveArcScale = 0.3f;

    [Header("Spin")]
    public bool spin = true;
    [Min(1)] public int spinRounds = 1;
    public AnimationCurve spinCurve = BuildCurve(
        new[] { 0f, 0.025f, 0.077f, 0.154f, 0.206f, 0.255f, 0.308f, 0.36f, 0.409f, 0.462f,
                0.514f, 0.563f, 0.615f, 0.692f, 0.745f, 0.794f, 0.846f, 0.898f, 0.948f, 1f },
        new[] { 0f, 0.035f, 0.118f, 0.283f, 0.366f, 0.441f, 0.518f, 0.589f, 0.654f, 0.717f,
                0.774f, 0.825f, 0.87f, 0.909f, 0.941f, 0.967f, 0.986f, 0.996f, 1f, 1f }
    );

    [Header("Scale (world axes)")]
    public AnimationCurve scaleXCurve = BuildCurve(
        new[] { 0f, 0.514f, 0.563f, 0.615f, 0.692f, 0.745f, 0.794f, 0.846f, 0.898f,
                0.948f, 1f, 1.052f, 1.102f, 1.154f, 1.231f, 1.285f },
        new[] { 1.19f, 1.19f, 1.148f, 1.1f, 1.062f, 1.034f, 1.018f, 1.011f, 1.107f,
                1.167f, 1.189f, 1.094f, 1.025f, 1.02f, 1.012f, 1f }
    );

    public AnimationCurve scaleYCurve = BuildCurve(
        new[] { 0f, 0.514f, 0.563f, 0.615f, 0.692f, 0.745f, 0.794f, 0.846f, 0.898f,
                0.948f, 1f, 1.052f, 1.102f, 1.154f, 1.231f, 1.285f },
        new[] { 1.19f, 1.19f, 1.244f, 1.308f, 1.359f, 1.396f, 1.419f, 1.427f, 1.205f,
                1.059f, 1.011f, 1.021f, 1.011f, 1.015f, 1.008f, 1f }
    );

    public float referenceAirborneScale = 1.19f;

    [Range(0.2f, 0.95f)] public float airborneReleaseTau = 0.5f;

    [Header("Shadow")]
    public float shadowDropPerHeight = 1.2f;
    public float shadowSidePerHeight = 0.25f;
    public float shadowScalePerHeight = 0.15f;
    public float landShadowHeight = 0.12f;

    public float SettleTau
    {
        get
        {
            Keyframe[] keys = scaleYCurve != null ? scaleYCurve.keys : null;
            float last = keys != null && keys.Length > 0
                ? keys[keys.Length - 1].time
                : 1f;
            return Mathf.Max(1f, last);
        }
    }

    public float LandDuration => (SettleTau - 1f) * flightDuration;

    private static AnimationCurve BuildCurve(float[] time, float[] value)
    {
        int n = time.Length;
        float[] secant = new float[n - 1];
        for (int i = 0; i < n - 1; i++)
            secant[i] = (value[i + 1] - value[i]) / (time[i + 1] - time[i]);

        Keyframe[] keys = new Keyframe[n];
        for (int i = 0; i < n; i++)
        {
            float tangent;
            if (i == 0)
            {
                tangent = secant[0];
            }
            else if (i == n - 1)
            {
                tangent = secant[n - 2];
            }
            else if (secant[i - 1] * secant[i] <= 0f)
            {
                tangent = 0f;
            }
            else
            {
                float average = (secant[i - 1] + secant[i]) * 0.5f;
                float limit = 3f * Mathf.Min(
                    Mathf.Abs(secant[i - 1]),
                    Mathf.Abs(secant[i])
                );
                tangent = Mathf.Sign(average) * Mathf.Min(Mathf.Abs(average), limit);
            }

            keys[i] = new Keyframe(time[i], value[i], tangent, tangent);
        }

        return new AnimationCurve(keys);
    }
}
