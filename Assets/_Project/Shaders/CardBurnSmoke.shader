Shader "SolitaireSort/Card Burn Smoke"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
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
                fixed4 color : COLOR;
            };

            fixed4 _Color;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float angle = atan2(centered.y, centered.x);
                float ripple = sin(angle * 3.0 + _Time.y * 0.7) * 0.07;
                float distanceFromCenter = length(centered) + ripple;
                float softShape = 1.0 - smoothstep(0.28, 1.0, distanceFromCenter);
                float centerSoftness = smoothstep(0.0, 0.2, distanceFromCenter);
                float alpha = softShape * lerp(0.72, 1.0, centerSoftness) *
                              input.color.a;
                return fixed4(input.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
