Shader "Aldara/FoliageFade"
{
    // a tree in front of the hero, drawn see-through (AldaraOccluders), its leaves still cut out
    Properties { _Tint("Tint", Color) = (1,1,1,1) _Wind("Wind sway", Float) = 0 _Emit("Emission boost", Float) = 0 _Alpha("Alpha", Float) = 0.45 _LeafTex("Leaf atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #include "AldaraFoliage.hlsl"
            FV vert(FA i) { return FoliageVert(i); }
            half4 frag(FV i, bool front : SV_IsFrontFace) : SV_Target { UNITY_SETUP_INSTANCE_ID(i); float4 c = FoliageColour(i, front); return half4(c.rgb, _Alpha); }
            ENDHLSL
        }
    }
}
