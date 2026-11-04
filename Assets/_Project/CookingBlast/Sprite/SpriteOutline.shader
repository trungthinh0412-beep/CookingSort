// Assign this companion material to SpineSilhouetteOutline, not the atlas.
Shader "World/Custom/Screen Space Outline"
{
    Properties
    {
        _FillColor ("Outline Color", Color) = (1,1,1,1)
        _FillWidth ("Outline Width", Range(0,64)) = 6
        _OutlineAlpha ("Outline Alpha", Range(0,1)) = 1
        _Softness ("Soft Edge", Range(0.25,8)) = 1.25
        [HDR] _GlowColor ("Glow Color", Color) = (1,1,1,0.35)
        _GlowWidth ("Glow Width", Range(0,64)) = 4
        _GlowStrength ("Glow Strength", Range(0,1)) = 0.15
        [HideInInspector] _MainTex ("Generated Seeds", 2D) = "black" {}
        [HideInInspector] _MaskTex ("Generated Silhouette", 2D) = "black" {}
        [HideInInspector] _OutlineColor ("Runtime Color", Color) = (1,1,1,1)
        [HideInInspector] _OutlineWidth ("Runtime Width", Float) = 6
        [HideInInspector] _Opacity ("Skeleton Opacity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Lighting Off
        Blend One OneMinusSrcAlpha
        UsePass "CookingBlast/Spine/Silhouette Outline/SilhouetteOutline"
    }
}
