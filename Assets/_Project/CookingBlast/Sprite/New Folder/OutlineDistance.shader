Shader "Hidden/CookingBlast/URP Outline Distance"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #pragma target 3.0
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float4 _OutlineSeedsSize;
        float _OutlineAlphaThreshold, _OutlineJumpStep;
        #define NO_SEED 30000.0
        float4 Seed(Varyings i):SV_Target
        {
            float a = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, i.texcoord, 0).a;
            if (a < _OutlineAlphaThreshold) return float4(NO_SEED,NO_SEED,0,0);
            float2 offset = 0;
            if (a < 0.999)
            {
                // Preserve fractional alpha coverage at the silhouette edge.
                float left = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, i.texcoord - float2(_OutlineSeedsSize.z,0), 0).a;
                float right = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, i.texcoord + float2(_OutlineSeedsSize.z,0), 0).a;
                float down = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, i.texcoord - float2(0,_OutlineSeedsSize.w), 0).a;
                float up = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, i.texcoord + float2(0,_OutlineSeedsSize.w), 0).a;
                float2 gradient = float2(right-left, up-down) * 0.5;
                offset = clamp((0.5-a) * gradient / max(dot(gradient,gradient), 1e-4), -0.5, 0.5);
            }
            return float4(offset,0,1);
        }
        float4 Jump(Varyings i):SV_Target
        {
            float2 bestOffset = NO_SEED;
            float bestDistance = 1e20;
            [unroll] for (int y=-1; y<=1; y++)
            {
                [unroll] for (int x=-1; x<=1; x++)
                {
                    float2 delta = float2(x,y) * _OutlineJumpStep;
                    float2 uv = i.texcoord + delta * _OutlineSeedsSize.zw;
                    if (any(uv < 0) || any(uv >= 1)) continue;
                    float2 candidate = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).xy;
                    if (candidate.x >= NO_SEED) continue;
                    candidate += delta;
                    float distanceSquared = dot(candidate,candidate);
                    if (distanceSquared < bestDistance)
                    {
                        bestDistance = distanceSquared;
                        bestOffset = candidate;
                    }
                }
            }
            return float4(bestOffset,0,bestDistance < 1e19 ? 1 : 0);
        }
        ENDHLSL
        Pass
        {
            Name "Seed"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Seed
            ENDHLSL
        }
        Pass
        {
            Name "Jump"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Jump
            ENDHLSL
        }
    }
}
