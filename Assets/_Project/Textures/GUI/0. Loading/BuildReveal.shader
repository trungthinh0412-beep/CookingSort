Shader "Sprites/BuildReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [PerRendererData] _BuildProgress ("Build Progress", Range(0, 1)) = 1
        [PerRendererData] _RevealTopY ("Reveal Top Y", Float) = 1
        [PerRendererData] _RevealBottomY ("Reveal Bottom Y", Float) = 0
        [PerRendererData] _EdgeSoftness ("Edge Softness", Float) = 0.02
        [PerRendererData] _EdgeGlowWidth ("Edge Glow Width", Float) = 0.05
        _EdgeGlowColor ("Edge Glow Color", Color) = (1, 0.78, 0.35, 1)
        _EdgeGlowStrength ("Edge Glow Strength", Range(0, 1)) = 0.22

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "BuildReveal"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float _BuildProgress;
            float _RevealTopY;
            float _RevealBottomY;
            float _EdgeSoftness;
            float _EdgeGlowWidth;
            fixed4 _EdgeGlowColor;
            float _EdgeGlowStrength;

            v2f vert(appdata_t input)
            {
                v2f output;

                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.texcoord = input.texcoord;
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;

                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.texcoord) * input.color;
                float progress = saturate(_BuildProgress);
                float currentY = lerp(_RevealTopY, _RevealBottomY, progress);
                float softness = max(_EdgeSoftness, 0.0001);
                float reveal = smoothstep(
                    currentY - softness,
                    currentY + softness,
                    input.worldPosition.y
                );

                // Avoid leaking a soft band at the exact start and make the
                // completed state completely opaque to the reveal mask.
                if (progress <= 0.0001)
                    reveal = 0;
                else if (progress >= 0.9999)
                    reveal = 1;

                float glow = 0;
                if (progress > 0.0001 && progress < 0.9999)
                {
                    float glowWidth = max(_EdgeGlowWidth, 0.0001);
                    float edgeDistance = abs(input.worldPosition.y - currentY);
                    glow = 1 - smoothstep(0, glowWidth, edgeDistance);
                    glow *= reveal;
                }

                color.rgb +=
                    _EdgeGlowColor.rgb * glow * _EdgeGlowStrength * color.a;
                color.a *= reveal;

                #ifdef UNITY_UI_CLIP_RECT
                float2 insideClipRect = step(_ClipRect.xy, input.worldPosition.xy) *
                                        step(input.worldPosition.xy, _ClipRect.zw);
                color.a *= insideClipRect.x * insideClipRect.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                clip(color.a - 0.001);
                return color;
            }
            ENDCG
        }
    }
}
