Shader "Hidden/CookingBlast/Camera Outline Mask"
{
    Properties { _MainTex ("Atlas", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _OutlineId, _AlphaThreshold;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float alpha:TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.alpha = v.color.a;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float a = tex2D(_MainTex, i.uv).a * i.alpha;
                clip(a - _AlphaThreshold);
                return float4(_OutlineId, 0, 0, 1);
            }
            ENDCG
        }
    }
}
