Shader "SolitaireSort/Card Burn Dissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CoreColor ("Hot Core", Color) = (1, 0.96, 0.72, 1)
        _EdgeColor ("Burn Edge", Color) = (1, 0.25, 0.015, 1)
        _DissolveAmount ("Dissolve", Range(0, 1)) = 0
        _EdgeWidth ("Edge Width", Range(0.005, 0.25)) = 0.075
        _EmissionStrength ("Emission", Range(0, 4)) = 1.6
        _NoiseScale ("Noise Scale", Range(1, 20)) = 6
        _NoiseSpeed ("Noise Speed", Range(0, 4)) = 0.45
        _NoiseStrength ("Noise Strength", Range(0, 0.5)) = 0.2
        _NoiseSeed ("Noise Seed", Float) = 0
        _Flash ("Flash", Range(0, 1)) = 0
        _BurnMode ("Burn Mode", Range(0, 1)) = 0
        _BurnOrigin ("Burn Origin", Vector) = (0, 0, 0, 0)
        _SpriteBounds ("Sprite Bounds", Vector) = (-0.5, -0.5, 1, 1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float _EnableExternalAlpha;
            fixed4 _CoreColor;
            fixed4 _EdgeColor;
            float _DissolveAmount;
            float _EdgeWidth;
            float _EmissionStrength;
            float _NoiseScale;
            float _NoiseSpeed;
            float _NoiseStrength;
            float _NoiseSeed;
            float _Flash;
            float _BurnMode;
            float4 _BurnOrigin;
            float4 _SpriteBounds;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.localPosition = input.vertex.xy;
                output.color = input.color;
                return output;
            }

            float hash21(float2 coordinates)
            {
                coordinates = frac(
                    coordinates * float2(123.34, 456.21)
                );
                coordinates += dot(
                    coordinates,
                    coordinates + 45.32
                );
                return frac(coordinates.x * coordinates.y);
            }

            float valueNoise(float2 coordinates)
            {
                float2 cell = floor(coordinates);
                float2 interpolation = frac(coordinates);
                interpolation = interpolation * interpolation *
                                (3.0 - 2.0 * interpolation);
                float bottom = lerp(
                    hash21(cell),
                    hash21(cell + float2(1, 0)),
                    interpolation.x
                );
                float top = lerp(
                    hash21(cell + float2(0, 1)),
                    hash21(cell + 1.0),
                    interpolation.x
                );
                return lerp(bottom, top, interpolation.y);
            }

            float layeredNoise(float2 coordinates)
            {
                float result = valueNoise(coordinates) * 0.62;
                result += valueNoise(coordinates * 2.03 + 17.7) * 0.27;
                result += valueNoise(coordinates * 4.11 + 39.2) * 0.11;
                return result;
            }

            fixed4 sampleSprite(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);
                #if ETC1_EXTERNAL_ALPHA
                    fixed4 alpha = tex2D(_AlphaTex, uv);
                    color.a = lerp(color.a, alpha.r, _EnableExternalAlpha);
                #endif
                return color;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // Clamp the terminal frame to a fully empty sprite. The noise
                // field can otherwise leave isolated pixels past the front.
                if (_DissolveAmount >= 0.999)
                    discard;

                float2 spriteUV = saturate(
                    (input.localPosition - _SpriteBounds.xy) /
                    max(_SpriteBounds.zw, float2(0.0001, 0.0001))
                );

                float2 animatedNoiseUV = spriteUV * _NoiseScale;
                animatedNoiseUV += float2(
                    _NoiseSeed * 1.37 + _Time.y * _NoiseSpeed * 0.31,
                    _NoiseSeed * 2.11 - _Time.y * _NoiseSpeed
                );
                float noise = layeredNoise(animatedNoiseUV);

                float bottomField = spriteUV.y;
                float cornerField = distance(spriteUV, _BurnOrigin.xy) * 0.70710678;
                float burnField = lerp(bottomField, cornerField, saturate(_BurnMode));
                burnField += (noise - 0.5) * _NoiseStrength;

                float front = lerp(-0.34, 1.34, saturate(_DissolveAmount));
                float distanceToFront = burnField - front;
                clip(distanceToFront);

                fixed4 spriteColor = sampleSprite(input.uv) * input.color;
                clip(spriteColor.a - 0.001);

                float edge = max(_EdgeWidth, 0.001);
                float hotCore = 1.0 - smoothstep(0.0, edge * 0.5, distanceToFront);
                float warmEdge = 1.0 - smoothstep(edge * 0.18, edge * 3.2, distanceToFront);
                float outerEdge = saturate(warmEdge - hotCore * 0.35);

                float3 color = spriteColor.rgb;
                color = lerp(color, _EdgeColor.rgb, outerEdge * _EdgeColor.a);
                color = lerp(color, _CoreColor.rgb, hotCore * _CoreColor.a);
                float glowMask = saturate(hotCore + outerEdge * 0.65);
                color *= 1.0 + glowMask * _EmissionStrength;
                color = lerp(color, float3(1.0, 0.98, 0.88), saturate(_Flash) * 0.48);
                color *= 1.0 + saturate(_Flash) * 0.32;

                spriteColor.rgb = color * spriteColor.a;
                return spriteColor;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
