Shader "Aldara/Grass"
{
    // Instanced grass tufts. The vertex shader reads the ground paint under each tuft's root: the tuft takes that colour
    // and only stands up where the paint is green. Wind gusts bend the tips; the blades part around the hero (_Hero).
    Properties { _BaseMap("Ground paint", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "../../Materials/AldaraNoise.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        float4 _Hero;
        CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST; float4 _TileO; CBUFFER_END
        struct A { float4 pos : POSITION; float4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
        float3 Bend(A i, out float3 ground, out float green)
        {
            float3 root = TransformObjectToWorld(float3(0, 0, 0));
            float2 uv = (root.xz - _TileO.xy) / 128.0;
            ground = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 0).rgb;
            float mx = max(ground.r, ground.b);
            green = saturate((ground.g - mx * 0.95) * 10.0) * saturate((ground.g - 0.10) * 8.0) * saturate((0.80 - ground.r) * 6.0);
            float h = i.col.a;
            float3 wp = TransformObjectToWorld(i.pos.xyz * float3(1, green, 1));
            // gusts roll across the field; each tuft sways a little on its own
            float gust = AGrad2(root.xz * 0.06 - float2(_Time.y * 0.22, _Time.y * 0.13)) + 0.5;
            float sway = sin(_Time.y * 2.1 + dot(root.xz, float2(0.9, 0.6))) * 0.5 + 0.5;
            float2 bend = float2(0.8, 0.45) * (0.05 + 0.18 * gust * gust + 0.05 * sway);
            // parting around the hero
            float2 d = root.xz - _Hero.xz; float dist = length(d) + 1e-4; float push = saturate(1 - dist / 0.95) * _Hero.w;
            bend += d / dist * push * 0.45;
            float k = h * h * green;
            wp.xz += bend * k; wp.y -= (push * 0.12 + dot(bend, bend) * 0.25) * k;
            return wp;
        }
        ENDHLSL
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
            struct V { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 g : TEXCOORD1; float4 hv : TEXCOORD2; };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); float3 ground; float green;
                float3 wp = Bend(i, ground, green);
                o.wp = wp; o.pos = TransformWorldToHClip(wp); o.g = ground; o.hv = float4(i.col.a, i.col.r, green, 0);
                if (green < 0.02) o.pos = float4(0, 0, -1, 1);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                Light L = GetMainLight(TransformWorldToShadowCoord(i.wp));
                float h = i.hv.x;
                // root matches the ground paint, the tips catch more sun and turn a touch yellow
                float3 c = i.g * lerp(0.80, 1.22, h) * i.hv.y + float3(0.035, 0.03, -0.01) * h;
                float aoD = 1, aoI = 1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.pos)); aoI = ao.indirectAmbientOcclusion; aoD = ao.directAmbientOcclusion;
                #endif
                float sh = L.shadowAttenuation * aoD;
                float3 tint = lerp(float3(0.80, 0.86, 1.02), float3(1, 1, 1), sh);
                float3 col = c * lerp(0.55, 1.0, sh) * 1.08 * tint * lerp(1, aoI, 0.85);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
