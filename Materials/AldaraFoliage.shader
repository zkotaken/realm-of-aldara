Shader "Aldara/Foliage"
{
    Properties { _Tint("Tint", Color) = (1,1,1,1) _Wind("Wind sway", Float) = 0 _Emit("Emission boost", Float) = 0 _Alpha("Alpha", Float) = 1 _LeafTex("Leaf atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #include "AldaraFoliage.hlsl"
            FV vert(FA i) { return FoliageVert(i); }
            half4 frag(FV i, bool front : SV_IsFrontFace) : SV_Target { UNITY_SETUP_INSTANCE_ID(i); return FoliageColour(i, front); }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            Cull Off ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "AldaraFoliage.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct SV { float4 pos : SV_POSITION; float4 uv : TEXCOORD0; };
            SV vert(FA i){ SV o; UNITY_SETUP_INSTANCE_ID(i);
                float3 wn = TransformObjectToWorldNormal(i.n);
                float3 wp = Flutter(Sway(TransformObjectToWorld(i.pos.xyz), i.pos.xyz), i.pos.xyz, wn, i.uv.z);
                float4 p = TransformWorldToHClip(ApplyShadowBias(wp, wn, _LightDirection));
                #if UNITY_REVERSED_Z
                p.z = min(p.z, UNITY_NEAR_CLIP_VALUE);
                #else
                p.z = max(p.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.pos = p; o.uv = i.uv; return o; }
            half4 frag(SV i) : SV_Target { if (round(i.uv.z) == 1) clip(SAMPLE_TEXTURE2D(_LeafTex, sampler_LeafTex, i.uv.xy).a - 0.5); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            Cull Off ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "AldaraFoliage.hlsl"
            struct SV { float4 pos : SV_POSITION; float4 uv : TEXCOORD0; };
            SV vert(FA i){ SV o; UNITY_SETUP_INSTANCE_ID(i); float3 wn = TransformObjectToWorldNormal(i.n);
                o.pos = TransformWorldToHClip(Flutter(Sway(TransformObjectToWorld(i.pos.xyz), i.pos.xyz), i.pos.xyz, wn, i.uv.z)); o.uv = i.uv; return o; }
            half4 frag(SV i) : SV_Target { if (round(i.uv.z) == 1) clip(SAMPLE_TEXTURE2D(_LeafTex, sampler_LeafTex, i.uv.xy).a - 0.5); return 0; }
            ENDHLSL
        }
    }
}
