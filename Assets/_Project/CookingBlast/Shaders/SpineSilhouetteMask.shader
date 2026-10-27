Shader "Hidden/CookingBlast/Spine Silhouette Mask"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _LocalBounds ("Local XY centre and size", Vector) = (0,0,1,1)
        _SkeletonAlpha ("Skeleton alpha", Float) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _LocalBounds;
            float _SkeletonAlpha;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float alpha:TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                float2 xy = (v.vertex.xy - _LocalBounds.xy) / _LocalBounds.zw * 2;
                // Use a fixed UV orientation, independent of the current scene camera.
                #if UNITY_UV_STARTS_AT_TOP
                xy.y = -xy.y;
                #endif
                o.pos = float4(xy, 0.5, 1);
                o.uv = v.uv;
                o.alpha = saturate(v.color.a / max(_SkeletonAlpha, 0.0001));
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float alpha = tex2D(_MainTex, i.uv).a * i.alpha;
                return float4(alpha, alpha, alpha, alpha);
            }
            ENDCG
        }
    }
}
