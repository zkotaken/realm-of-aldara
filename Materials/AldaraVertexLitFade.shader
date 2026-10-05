Shader "Aldara/VertexLitFade"
{
    Properties { _Tint("Tint", Color) = (1,1,1,1) _Wind("Wind sway", Float) = 0 _Emit("Emission boost", Float) = 0 _Alpha("Alpha", Float) = 0.45 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "AldaraLighting.hlsl"
        CBUFFER_START(UnityPerMaterial) float4 _Tint; float _Wind; float _Emit; float _Alpha; CBUFFER_END
        float3 Sway(float3 wp, float3 op){ float h = max(0, op.y); float k = _Wind * h * h * 0.01;
            wp.x += sin(_Time.y * 1.6 + wp.x * 0.3 + wp.z * 0.2) * k; wp.z += cos(_Time.y * 1.3 + wp.z * 0.3) * k * 0.7; return wp; }
        ENDHLSL
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
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            struct A { float4 pos : POSITION; float3 n : NORMAL; float4 col : COLOR; float2 uv2 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float3 wp : TEXCOORD0; float3 n : TEXCOORD1; float fog : TEXCOORD2; float em : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A i){ V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 wp = Sway(TransformObjectToWorld(i.pos.xyz), i.pos.xyz);
                o.wp = wp; o.pos = TransformWorldToHClip(wp); o.n = TransformObjectToWorldNormal(i.n); o.col = i.col * _Tint;
                o.fog = ComputeFogFactor(o.pos.z); o.em = i.uv2.x; return o; }
            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(front ? i.n : -i.n);
                float3 c = AldaraShade(i.col.rgb, n, i.wp, i.pos, i.em + _Emit, 0.16);
                c = MixFog(c, i.fog);
                return half4(c, _Alpha);
            }
            ENDHLSL
        }
    }
}
