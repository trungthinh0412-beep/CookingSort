// Screen-space outline for Spine skeletons and sprites. Composites the distance map built by OutlineFeature
// (_OutlineSeeds: per map texel, the offset in texels to the nearest silhouette texel) into outline + glow.
// SkeletonOutline / SpriteOutline draws a quad over the source bounds; the vertex shader grows it on screen by
// Outline Width + Glow Width so the outline outside the bounds is covered too (uv = corner direction).
//
// Setup:
//   1. Add OutlineFeature to the URP renderer.
//   2. Create a material with "World/Custom/Screen Space Outline".
//   3. Add a child GameObject (local position/rotation zero, scale one) under the skeleton or sprite,
//      add the SkeletonOutline / SpriteOutline component and assign this material.
//   4. Widths are pixels at 1080p. Keep Outline Width + Glow Width <= the feature's Max Distance.
//   5. Glow Color alpha is the glow strength at the outline edge, fading to zero at Glow Width.
Shader "World/Custom/Screen Space Outline"
{
    Properties
    {
        _FillColor("Outline Color", Color) = (1, 1, 1, 1)
        _FillWidth("Outline Width", Range(0, 64)) = 4
        _OutlineAlpha("Outline Alpha", Range(0, 1)) = 1
        _OutlineSoftness("Outline Edge Softness", Range(0.5, 4)) = 1.25
        [HDR] _GlowColor("Glow Color", Color) = (1, 1, 1, 0.5)
        _GlowWidth("Glow Width", Range(0, 64)) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-1" // Same sorting order as the skeleton, drawn right before it.
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Outline"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define REFERENCE_HEIGHT 1080.0
            #define NO_SEED 30000.0

            TEXTURE2D(_OutlineSeeds);
            float4 _OutlineSeedsSize; // xy = size, zw = 1 / size

            CBUFFER_START(UnityPerMaterial)
                half4 _FillColor;
                float _FillWidth;
                float _OutlineAlpha;
                half4 _GlowColor;
                float _GlowWidth;
                float _OutlineSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0;
            };

            float2 ScreenAxis(float3 axisOS)
            {
                float2 axis = mul(GetWorldToHClipMatrix(), float4(TransformObjectToWorldDir(axisOS, false), 0)).xy * _ScaledScreenParams.xy;
                return axis * rsqrt(max(dot(axis, axis), 1e-12));
            }

            float4 vert(Attributes input) : SV_POSITION
            {
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);

                float2 outward = ScreenAxis(float3(1, 0, 0)) * input.corner.x + ScreenAxis(float3(0, 1, 0)) * input.corner.y;
                float2 aspect = float2(_ScaledScreenParams.y / _ScaledScreenParams.x, 1);
                float extentNdc = (_FillWidth + _GlowWidth + _OutlineSoftness) * 2.0 / REFERENCE_HEIGHT;
                positionCS.xy += outward * extentNdc * aspect * positionCS.w;
                return positionCS;
            }

            float SeedDistance(float2 texel, float2 positionPixels)
            {
                if (any(texel < 0) || any(texel >= _OutlineSeedsSize.xy)) return 1e10;
                float2 uv = (texel + 0.5) * _OutlineSeedsSize.zw;
                float2 offset = SAMPLE_TEXTURE2D_LOD(_OutlineSeeds, sampler_PointClamp, uv, 0).xy;
                if (offset.x >= NO_SEED) return 1e10;
                float2 seed = (texel + 0.5 + offset) * _OutlineSeedsSize.zw * _ScaledScreenParams.xy;
                return length(seed - positionPixels);
            }

            half4 frag(float4 positionCS : SV_POSITION) : SV_Target
            {
                float2 uv = positionCS.xy / _ScaledScreenParams.xy;
                float2 cell = floor(uv * _OutlineSeedsSize.xy - 0.5);
                // Reconstruct distance from neighbouring seed positions rather
                // than interpolating offsets (which refer to different texels).
                float distancePixels = min(
                    min(SeedDistance(cell, positionCS.xy), SeedDistance(cell + float2(1,0), positionCS.xy)),
                    min(SeedDistance(cell + float2(0,1), positionCS.xy), SeedDistance(cell + 1, positionCS.xy)));
                float pixel = REFERENCE_HEIGHT / _ScaledScreenParams.y;
                float dist = min(distancePixels * pixel, _FillWidth + _GlowWidth + 8);
                // Do not let missing seeds at the field boundary inflate AA.
                float edge = max(clamp(fwidth(dist), pixel, pixel * 2), _OutlineSoftness * pixel);

                half fill = 1 - smoothstep(_FillWidth - edge * 0.5, _FillWidth + edge * 0.5, dist);
                fill *= step(0.0001, _FillWidth);
                half t = saturate((dist - _FillWidth) / max(_GlowWidth, 1e-4));
                half glow = _GlowColor.a * (1 - t) * (1 - t) * (1 - fill);

                half fillAlpha = fill * _OutlineAlpha;
                return half4(_FillColor.rgb * fillAlpha + _GlowColor.rgb * glow, fillAlpha + glow);
            }
            ENDHLSL
        }
    }
}
