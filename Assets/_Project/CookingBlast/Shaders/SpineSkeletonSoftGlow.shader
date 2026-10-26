Shader "CookingBlast/Spine/Skeleton Soft Glow"
{
    Properties
    {
        _Cutoff ("Shadow Alpha Cutoff", Range(0, 1)) = 0.1
        [NoScaleOffset] _MainTex ("Main Texture", 2D) = "black" {}
        [Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Texture", Int) = 0

        [Header(Inner Outline)]
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width", Range(0, 30)) = 5
        _OutlineStrength ("Outline Strength", Range(0, 1)) = 1

        [Header(Outer Glow)]
        _GlowColor ("Glow Color", Color) = (1, 1, 1, 0.35)
        _GlowWidth ("Glow Width", Range(0, 50)) = 12
        _GlowStrength ("Glow Strength", Range(0, 2)) = 0.65
        _Softness ("Edge Softness", Range(0.01, 1)) = 0.45

        [Header(Texture Calibration)]
        _ReferenceTextureWidth ("Reference Texture Width", Float) = 380

        [HideInInspector] _StencilRef ("Stencil Reference", Float) = 1
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Fog { Mode Off }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Lighting Off

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass Keep
        }

        Pass
        {
            Name "SOFT_GLOW"

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            float4 _GlowColor;
            float _OutlineWidth;
            float _OutlineStrength;
            float _GlowWidth;
            float _GlowStrength;
            float _Softness;
            float _ReferenceTextureWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.alpha = input.color.a;
                return output;
            }

            float SampleAlpha(float2 uv)
            {
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) *
                               step(0.0, uv.y) * step(uv.y, 1.0);
                return tex2D(_MainTex, saturate(uv)).a * inside;
            }

            float SampleRing(float2 uv, float radius)
            {
                float2 stepUv = radius / max(1.0, _ReferenceTextureWidth);
                float diagonal = 0.70710678;
                float halfDiagonal = 0.92387953;
                float quarterDiagonal = 0.38268343;
                float alpha = 0.0;

                alpha = max(alpha, SampleAlpha(uv + stepUv * float2( 1.0,  0.0)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2(-1.0,  0.0)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2( 0.0,  1.0)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2( 0.0, -1.0)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2( diagonal,  diagonal)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2(-diagonal,  diagonal)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2( diagonal, -diagonal)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2(-diagonal, -diagonal)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2( halfDiagonal,  quarterDiagonal)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2(-halfDiagonal,  quarterDiagonal)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2( halfDiagonal, -quarterDiagonal)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2(-halfDiagonal, -quarterDiagonal)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2( quarterDiagonal,  halfDiagonal)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2(-quarterDiagonal,  halfDiagonal)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2( quarterDiagonal, -halfDiagonal)));
                alpha = max(alpha, SampleAlpha(uv + stepUv * float2(-quarterDiagonal, -halfDiagonal)));
                return alpha;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float center = SampleAlpha(input.uv) * input.alpha;
                float innerSample = SampleRing(input.uv, _OutlineWidth) * input.alpha;
                float outerSample = SampleRing(input.uv, _GlowWidth) * input.alpha;

                float inner = saturate(innerSample - center) * _OutlineStrength;
                float outer = saturate(outerSample - center);
                outer = smoothstep(0.0, max(0.01, _Softness), outer) *
                        _GlowStrength * _GlowColor.a;

                float outlineAlpha = saturate(max(inner * _OutlineColor.a, outer));
                float innerWeight = saturate(inner * _OutlineColor.a);
                float3 color = lerp(_GlowColor.rgb, _OutlineColor.rgb, innerWeight);

                return fixed4(color * outlineAlpha, outlineAlpha);
            }
            ENDCG
        }

        UsePass "Spine/Skeleton/NORMAL"
        UsePass "Spine/Skeleton/CASTER"
    }

    FallBack "Spine/Skeleton"
}
