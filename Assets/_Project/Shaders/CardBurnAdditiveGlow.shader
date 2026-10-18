Shader "SolitaireSort/Card Burn Additive Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CoreColor ("Hot Core", Color) = (1, 0.98, 0.78, 1)
        _EdgeColor ("Glow Color", Color) = (1, 0.18, 0.015, 1)
        _DissolveAmount ("Dissolve", Range(0, 1)) = 0
        _EdgeWidth ("Edge Width", Range(0.005, 0.3)) = 0.2
        _GlowStrength ("Glow Strength", Range(0, 5)) = 2.2
        _NoiseScale ("Noise Scale", Range(1, 20)) = 3.6
        _NoiseSpeed ("Noise Speed", Range(0, 4)) = 0.22
        _NoiseStrength ("Noise Strength", Range(0, 0.5)) = 0.34
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
            "Queue"="Transparent+30"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

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
            float _GlowStrength;
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
                coordinates = frac(coordinates * float2(123.34, 456.21));
                coordinates += dot(coordinates, coordinates + 45.32);
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
                // The glow overlay must disappear with the card, even while
                // its sparks and smoke continue their short tail.
                if (_DissolveAmount >= 0.999)
                    discard;

                fixed4 sprite = sampleSprite(input.uv) * input.color;
                clip(sprite.a - 0.001);

                float2 spriteUV = saturate(
                    (input.localPosition - _SpriteBounds.xy) /
                    max(_SpriteBounds.zw, float2(0.0001, 0.0001))
                );
                float2 noiseUV = spriteUV * _NoiseScale + float2(
                    _NoiseSeed * 1.37 + _Time.y * _NoiseSpeed * 0.31,
                    _NoiseSeed * 2.11 - _Time.y * _NoiseSpeed
                );
                float noise = layeredNoise(noiseUV);
                float bottomField = spriteUV.y;
                float cornerField = distance(spriteUV, _BurnOrigin.xy) * 0.70710678;
                float burnField = lerp(bottomField, cornerField, saturate(_BurnMode));
                burnField += (noise - 0.5) * _NoiseStrength;

                float front = lerp(-0.34, 1.34, saturate(_DissolveAmount));
                float frontDistance = abs(burnField - front);
                float edge = max(_EdgeWidth, 0.001);
                float core = 1.0 - smoothstep(0.0, edge * 0.42, frontDistance);
                float halo = 1.0 - smoothstep(edge * 0.15, edge * 3.8, frontDistance);
                float flashGlow = saturate(_Flash) * 0.58;
                float glowAlpha = saturate(max(halo, flashGlow)) * sprite.a;
                float3 glowColor = lerp(_EdgeColor.rgb, _CoreColor.rgb, core);
                glowColor *= _GlowStrength * lerp(0.68, 1.35, core);
                return fixed4(glowColor, glowAlpha);
            }
            ENDCG
        }
    }
}
