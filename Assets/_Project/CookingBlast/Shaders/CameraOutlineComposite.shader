Shader "Hidden/CookingBlast/Camera Outline Composite"
{
    Properties
    {
        _MainTex ("Seeds", 2D) = "black" {}
        _MaskTex ("Mask", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _MaskTex;
            float4 _MainTex_TexelSize, _FillColor, _GlowColor;
            float _FillWidth, _OutlineAlpha, _GlowWidth, _Softness, _GlowStrength;
            float _ReferencePixelsPerTexel;
            float _FlipMaskY;
            float _OutlineId;
            struct v2f { float4 pos:SV_POSITION; float4 screen:TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                uv.y = lerp(uv.y, 1-uv.y, _FlipMaskY);
                float4 seed = tex2D(_MainTex, uv);
                if (seed.a < 0.5 || abs(seed.b - _OutlineId) > 0.25) return 0;
                float d = max(0, length((uv - seed.xy) * _MainTex_TexelSize.zw) - 0.5);
                d *= _ReferencePixelsPerTexel;
                float soft = max(_ReferencePixelsPerTexel, _Softness);
                float fill = 1 - smoothstep(max(0, _FillWidth - soft * 0.5), _FillWidth + soft * 0.5, d);
                fill *= step(0.0001, _FillWidth) * _FillColor.a * _OutlineAlpha;
                float t = saturate((d - _FillWidth) / max(_GlowWidth, 0.0001));
                float glow = (1-t)*(1-t) * _GlowColor.a * _GlowStrength * step(0.0001, _GlowWidth);
                glow *= 1-fill;
                float outside = 1-tex2D(_MaskTex, uv).a;
                return float4(_FillColor.rgb*fill + _GlowColor.rgb*glow, fill+glow) * outside;
            }
            ENDCG
        }
    }
}
