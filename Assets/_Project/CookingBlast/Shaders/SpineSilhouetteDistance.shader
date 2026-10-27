Shader "Hidden/CookingBlast/Spine Silhouette Distance"
{
    Properties
    {
        _MainTex ("Mask or seed field", 2D) = "black" {}
        _AlphaThreshold ("Alpha threshold", Range(0,1)) = 0.1
        _Step ("Jump distance in pixels", Float) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        float _AlphaThreshold;
        float _Step;
        float4 Seed(v2f_img i):SV_Target
        {
            float a = tex2D(_MainTex, i.uv).a;
            return a >= _AlphaThreshold ? float4(i.uv, a, 1) : float4(0,0,0,0);
        }
        float4 Jump(v2f_img i):SV_Target
        {
            float4 nearest = float4(0,0,0,0);
            float best = 1e20;
            [unroll] for (int y = -1; y <= 1; y++)
            {
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 sampleUV = i.uv + float2(x,y) * _Step * abs(_MainTex_TexelSize.xy);
                    float4 candidate = tex2D(_MainTex, sampleUV);
                    float2 delta = (candidate.xy - i.uv) * _MainTex_TexelSize.zw;
                    float squaredDistance = dot(delta, delta);
                    if (candidate.a > 0.5 && all(sampleUV >= 0) && all(sampleUV <= 1) && squaredDistance < best)
                    {
                        nearest = candidate;
                        best = squaredDistance;
                    }
                }
            }
            return nearest;
        }
        ENDCG
        Pass
        {
            Name "SEED"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Seed
            ENDCG
        }
        Pass
        {
            Name "JUMP"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Jump
            ENDCG
        }
    }
}
