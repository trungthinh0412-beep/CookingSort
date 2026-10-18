Shader "Custom/AbsoluteCornerFold"
{
    Properties
    {
        _MainTex ("Card Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _PullX ("Pull X (Right)", Range(-5, 5)) = 1.0
        _PullY ("Pull Y (Down)", Range(-5, 5)) = -1.0
        _PullZ ("Pull Z (Depth)", Range(-5, 5)) = -1.0
        
        _Radius ("Fold Radius", Range(0.01, 5)) = 1.0
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "DisableBatching"="True" 
        }
        
        Cull Off      
        Lighting Off
        ZWrite On     
        Blend SrcAlpha OneMinusSrcAlpha 

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            
            float _PullX;
            float _PullY;
            float _PullZ;
            float _Radius;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                
                // For a centered sprite mesh, the top-left corner vertex is located where 
                // X is at its minimum local value and Y is at its maximum local value.
                // We find the corner reference point dynamically per-vertex based on UV or local layout,
                // or we use the standard quad/mesh local bounds assumption: 
                // Left side is negative X, Top side is positive Y.
                
                // Let's target the exact top-left corner coordinate in local space (-width/2, +height/2).
                // Since we don't have exact mesh bounds inside the vertex shader blindly without uniform,
                // let's look at the UV coordinates: Top-Left UV is always (0, 1).
                // Wait, earlier atlases broke UVs, so let's use local position mapping:
                // Let's define the top-left corner structurally: 
                // We evaluate distance relative to the vertex where IN.texcoord.x is closest to 0 and IN.texcoord.y closest to 1,
                // OR we pass the size cleanly via C#. Let's use a precise C# mesh bounds vertex lookup.

                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // We will do the heavy lifting cleanly in the C# script by passing the exact local vertex position.
            ENDCG
        }
    }
}