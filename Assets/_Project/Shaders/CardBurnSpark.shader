Shader "SolitaireSort/Card Burn Spark"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 0.45, 0.04, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
                float horizontal = saturate(1.0 - abs(centered.x));
                float vertical = saturate(1.0 - abs(centered.y));
                float core = smoothstep(0.0, 0.78, horizontal) * smoothstep(0.0, 0.32, vertical);
                float tipFade = saturate(1.0 - abs(centered.y) * 0.82);
                float alpha = core * tipFade * input.color.a;
                return fixed4(input.color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
