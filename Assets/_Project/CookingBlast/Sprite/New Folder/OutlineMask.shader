Shader "Hidden/CookingBlast/URP Outline Mask"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Blend One OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_OutlineAtlas);
            SAMPLER(sampler_OutlineAtlas);
            float4x4 _OutlineViewProjection;
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float alpha:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = mul(_OutlineViewProjection, float4(TransformObjectToWorld(v.positionOS.xyz), 1));
                o.uv = v.uv;
                o.alpha = v.color.a;
                return o;
            }
            float4 Frag(Varyings i):SV_Target
            {
                // Integrate alpha across the mask pixel before generating seeds.
                // Explicit linear sampling also avoids inheriting point filtering
                // from an atlas imported for pixel-art tiles.
                float2 dx = ddx(i.uv) * 0.25;
                float2 dy = ddy(i.uv) * 0.25;
                float alpha = (
                    SAMPLE_TEXTURE2D(_OutlineAtlas, sampler_LinearClamp, i.uv - dx - dy).a +
                    SAMPLE_TEXTURE2D(_OutlineAtlas, sampler_LinearClamp, i.uv + dx - dy).a +
                    SAMPLE_TEXTURE2D(_OutlineAtlas, sampler_LinearClamp, i.uv - dx + dy).a +
                    SAMPLE_TEXTURE2D(_OutlineAtlas, sampler_LinearClamp, i.uv + dx + dy).a
                ) * (0.25 * i.alpha);
                return float4(alpha, alpha, alpha, alpha);
            }
            ENDHLSL
        }
    }
}
