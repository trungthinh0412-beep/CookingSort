using System;
using UnityEngine;

[Serializable]
public class CardDealSettings
{
    [Header("Timing")]
    [Min(0f)] public float startDelay = 0.03f;
    [Min(0.005f)] public float dealInterval = 0.022f;
    [Min(0.05f)] public float flightDuration = 0.44f;

    [Header("Path")]
    public AnimationCurve travelCurve = BuildCurve(
        new[] { 0f, 0.26f, 0.30f, 0.34f, 0.38f, 0.42f, 0.53f, 0.57f, 0.61f, 0.65f,
                0.69f, 0.73f, 0.77f, 0.81f, 0.84f, 0.88f, 0.92f, 0.96f, 1f },
        new[] { 0f, 0.288f, 0.347f, 0.404f, 0.464f, 0.531f, 0.603f, 0.656f, 0.708f, 0.747f,
                0.787f, 0.825f, 0.873f, 0.905f, 0.924f, 0.943f, 0.961f, 0.982f, 1f }
    );

    public AnimationCurve arcCurve = BuildCurve(
        new[] { 0f, 0.26f, 0.30f, 0.34f, 0.38f, 0.42f, 0.47f, 0.53f, 0.57f, 0.61f,
                0.65f, 0.69f, 0.73f, 0.77f, 0.81f, 0.84f, 0.88f, 0.92f, 0.96f, 1f },
        new[] { 0f, 0.72f, 0.84f, 0.93f, 0.97f, 0.99f, 1f, 1f, 0.98f, 0.94f,
                0.90f, 0.85f, 0.75f, 0.60f, 0.47f, 0.35f, 0.25f, 0.15f, 0.07f, 0f }
    );

    [Min(0f)] public float arcHeight = 0f;
    [Min(0f)] public float arcHeightPerDistance = 0.245f;

    [Header("Spin")]
    [Min(0)] public int turns = 1;
    public AnimationCurve turnCurve = BuildCurve(
        new[] { 0f, 0.2f, 0.4f, 0.6f, 0.79f, 0.85f, 0.9f, 0.95f, 1f },
        new[] { 0f, 0.238f, 0.476f, 0.714f, 0.94f, 0.968f, 0.985f, 0.996f, 1f }
    );

    [Header("Airborne Scale")]
    [Min(1f)] public float flightScale = 1.25f;

    public AnimationCurve flightScaleCurve = BuildCurve(
        new[] { 0f, 0.26f, 0.30f, 0.34f, 0.38f, 0.42f, 0.47f, 0.53f, 0.57f, 0.61f,
                0.65f, 0.69f, 0.73f, 0.77f, 0.81f, 0.84f, 0.88f, 0.92f, 0.96f, 1f },
        new[] { 0f, 0.72f, 0.84f, 0.93f, 0.97f, 0.99f, 1f, 1f, 0.98f, 0.94f,
                0.90f, 0.85f, 0.75f, 0.60f, 0.47f, 0.35f, 0.25f, 0.15f, 0.07f, 0f }
    );

    [Header("Landing Stretch")]
    public AnimationCurve stretchXCurve = BuildCurve(
        new[] { 0f, 0.514f, 0.615f, 0.745f, 0.846f, 0.898f, 0.948f,
                1f, 1.07f, 1.15f, 1.26f, 1.40f },
        new[] { 1f, 1f, 0.917f, 0.861f, 0.842f, 0.958f, 1.070f,
                1.200f, 1.080f, 0.940f, 0.985f, 1f }
    );

    public AnimationCurve stretchYCurve = BuildCurve(
        new[] { 0f, 0.514f, 0.615f, 0.745f, 0.846f, 0.898f, 0.948f,
                1f, 1.07f, 1.15f, 1.26f, 1.40f },
        new[] { 1f, 1f, 1.090f, 1.162f, 1.188f, 1.043f, 0.930f,
                0.820f, 0.950f, 1.090f, 1.020f, 1f }
    );

    public float SettleTau
    {
        get
        {
            Keyframe[] keys = stretchYCurve != null ? stretchYCurve.keys : null;
            float last = keys != null && keys.Length > 0
                ? keys[keys.Length - 1].time
                : 1f;
            return Mathf.Max(1f, last);
        }
    }

    [Header("Flight Shadow")]
    public float shadowDropPerHeight = 1.2f;
    public float shadowSidePerHeight = 0.25f;
    public float shadowScalePerHeight = 0.15f;

    [Header("Face Flip")]
    [Min(0f)] public float flipDelay = 0.05f;
    [Min(0.02f)] public float flipDuration = 0.1f;

    public AnimationCurve flipCurve = BuildCurve(
        new[] { 0f, 0.15f, 0.3f, 0.5f, 0.7f, 0.85f, 1f },
        new[] { 0f, 0.06f, 0.22f, 0.5f, 0.78f, 0.94f, 1f }
    );

    [Range(0f, 160f)] public float flipCurveAngle = 120f;

    [Range(0.3f, 1f)] public float flipCurveShade = 0.72f;

    [Range(0f, 20f)] public float flipLeanAngle = 4f;
    [Range(0f, 0.4f)] public float flipBendStretch = 0.05f;

    [Min(1f)] public float flipLiftScale = 1.08f;
    [Min(0f)] public float flipLiftDuration = 0.05f;
    [Min(0f)] public float flipSettleDuration = 0.085f;

    [Min(0f)] public float flipPunchDuration = 0.3f;
    [Range(0f, 0.3f)] public float flipPunchAmount = 0.05f;
    [Range(0.5f, 4f)] public float flipPunchOscillations = 1.5f;

    [Header("Deck")]
    [Range(0f, 0.08f)] public float deckReactionAmount = 0.012f;

    public static AnimationCurve BuildCurve(float[] time, float[] value)
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
