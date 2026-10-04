Shader "Aldara/Ground"
{
    Properties { _BaseMap("Ground paint", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "../Materials/AldaraLighting.hlsl"
        #include "../Materials/AldaraNoise.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST; CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; float3 n : TEXCOORD2; };
            V vert(A i){ V o; o.wp = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.wp); o.uv = i.uv; o.n = TransformObjectToWorldNormal(i.n); return o; }
            half4 frag(V i) : SV_Target {
                float3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                float3 n = normalize(i.n);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.wp));
                // the paint already holds the browser's own hill shading: keep flat ground at its painted colour
                float nl = saturate(dot(n, L.direction)), flat = saturate(L.direction.y);
                float lit = lerp(0.55, 1.0, L.shadowAttenuation) * (0.62 + 0.38 * nl / max(flat, 0.35));
                if (_AldaraHQ < 0.5) return half4(c * lit * 1.08, 1);
                // enhanced: the painted ground gains surface detail the paint is too soft to hold. Slopes and cliffs get
                // layered, cracked rock lit by the sun; flat ground gets soft clumps and fine grit. Both bend the normal,
                // so the detail catches light and shadow instead of being printed on.
                float3 wp = i.wp, n0 = n;
                float slope = saturate((1 - n0.y - 0.16) / 0.34);
                // (no branch around the rock: the bump below takes screen derivatives of it)
                float warp = AFbm3(wp * 0.22, 2);
                // weathered rock: a rough face with soft layering, no hard lines
                float sv = wp.y * 1.05 + warp * 2.4 + AGrad2(wp.xz * 0.35) * 0.8;
                float strata = sin(sv * 6.2832) * 0.5 + 0.5;
                float rough = AFbm3(wp * float3(1.2, 1.9, 1.2), 3) + 0.5;
                float crack = 0;
                float hRock = strata * 0.22 + rough * 0.78;
                float2 g = wp.xz;
                float clump = AFbm2(g * 0.9, 3) + 0.5; float grit = AGrad2(g * 3.1) * 0.5 + 0.5;
                float hFlat = clump * 0.65 + grit * 0.35;
                float h = lerp(hFlat * 0.28, hRock, slope);
                n = ABump(n0, wp, h, lerp(0.35, 1.1, slope));
                nl = saturate(dot(n, L.direction));
                c *= lerp(1 + (clump - 0.5) * 0.16 + (grit - 0.5) * 0.06, (0.82 + 0.38 * hRock) * (1 - crack * 0.16), slope * 0.9);
                float grain = 1;
                float aoI = 1, aoD = 1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.pos)); aoI = ao.indirectAmbientOcclusion; aoD = ao.directAmbientOcclusion;
                #endif
                float sh = L.shadowAttenuation * aoD;
                float3 shadeTint = lerp(float3(0.80, 0.86, 1.02), float3(1, 1, 1), sh);
                float3 col = c * grain * lerp(0.55, 1.0, sh) * (0.62 + 0.38 * nl / max(flat, 0.35)) * 1.08 * shadeTint * lerp(1, aoI, 0.85);
                #if defined(_ADDITIONAL_LIGHTS)
                uint cnt = GetAdditionalLightsCount();
                for (uint li = 0; li < cnt; li++) { Light A2 = GetAdditionalLight(li, i.wp); col += c * A2.color * A2.distanceAttenuation * saturate(dot(n, A2.direction) * 0.6 + 0.4) * 0.8; }
                #endif
                return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; };
            V vert(A i){ V o; float3 wp = TransformObjectToWorld(i.pos.xyz); float3 wn = TransformObjectToWorldNormal(i.n);
                float4 p = TransformWorldToHClip(ApplyShadowBias(wp, wn, _LightDirection));
                #if UNITY_REVERSED_Z
                p.z = min(p.z, UNITY_NEAR_CLIP_VALUE);
                #else
                p.z = max(p.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.pos = p; return o; }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; };
            V vert(A i){ V o; o.pos = TransformObjectToHClip(i.pos.xyz); return o; }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}