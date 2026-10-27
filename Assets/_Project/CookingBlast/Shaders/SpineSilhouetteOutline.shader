Shader "CookingBlast/Spine/Silhouette Outline"
{
    Properties
    {
        _MainTex ("Distance seeds", 2D) = "black" {}
        _MaskTex ("Combined silhouette", 2D) = "black" {}
        _OutlineColor ("Outline color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline width in mask pixels", Float) = 6
        _Softness ("Soft edge in mask pixels", Float) = 1.25
        _GlowColor ("Glow color", Color) = (1,1,1,0.35)
        _GlowWidth ("Glow width", Float) = 4
        _GlowStrength ("Glow strength", Float) = 0.15
        _Opacity ("Skeleton opacity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _MaskTex;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor, _GlowColor;
            float _OutlineWidth, _Softness, _GlowWidth, _GlowStrength, _Opacity;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float4 seed = tex2D(_MainTex, i.uv);
                if (seed.a < 0.5) return 0;
                float distance = max(0, length((i.uv - seed.xy) * _MainTex_TexelSize.zw) - 0.5);
                float antialias = max(0.75, fwidth(distance));
                float soft = max(antialias, _Softness);
                float outline = (1 - smoothstep(max(0, _OutlineWidth - soft * 0.5), _OutlineWidth + soft * 0.5, distance));
                outline *= step(0.0001, _OutlineWidth) * _OutlineColor.a;
                float glow = 1 - smoothstep(_OutlineWidth, _OutlineWidth + max(0.001, _GlowWidth), distance);
                glow *= glow * _GlowColor.a * _GlowStrength * step(0.0001, _GlowWidth);
                float outside = 1 - tex2D(_MaskTex, i.uv).a;
                float alpha = outline + glow * (1 - outline);
                float3 rgb = _OutlineColor.rgb * outline + _GlowColor.rgb * glow * (1 - outline);
                return float4(rgb, alpha) * outside * _Opacity;
            }
            ENDCG
        }
    }
}
