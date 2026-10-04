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
        // high-resolution surface layers (AldaraDetailBuilder): 0 grass, 1 dirt, 2 rock, 3 sand, 4 snow, 5 ash, 6 mud, 7 gravel
        TEXTURE2D_ARRAY(_DetailAlb); SAMPLER(sampler_DetailAlb); TEXTURE2D_ARRAY(_DetailNor); SAMPLER(sampler_DetailNor);
        float _DetailOn; float4 _DetailSize0, _DetailSize1;
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
                // enhanced: the painted map keeps its colours and gains real surfaces. Each pixel's paint is read as
                // grass, dirt, rock, sand, snow, ash, mud or gravel; photo-scanned layers of those surfaces are blended in
                // at a fine scale (their detail, not their colour, so the map's look stays), with their relief lighting
                // the ground. Slopes take rock, projected from the side so cliffs are not stretched.
                float3 wp = i.wp, n0 = n;
                float slope = saturate((1 - n0.y - 0.16) / 0.34);
                float2 g = wp.xz;
                float clump = AFbm2(g * 0.9, 3) + 0.5;
                float mx = max(c.r, max(c.g, c.b)), mn = min(c.r, min(c.g, c.b)), ch = mx - mn;
                float sat = ch / max(mx, 1e-3), lum = dot(c, float3(0.3, 0.59, 0.11));
                float hue = 0;
                if (ch > 1e-4) { hue = mx == c.r ? (c.g - c.b) / ch : mx == c.g ? 2 + (c.b - c.r) / ch : 4 + (c.r - c.g) / ch; hue = frac(hue / 6 + 1); }
                float w[8];
                float grassy = smoothstep(0.16, 0.22, hue) * (1 - smoothstep(0.45, 0.52, hue)) * smoothstep(0.10, 0.22, sat);
                float warm = (1 - smoothstep(0.13, 0.17, hue)) * smoothstep(0.0, 0.03, hue) + smoothstep(0.95, 1.0, hue);
                w[0] = grassy * smoothstep(0.16, 0.26, lum);
                w[1] = warm * smoothstep(0.22, 0.34, sat) * smoothstep(0.14, 0.24, lum) * (1 - smoothstep(0.50, 0.62, lum));
                w[2] = smoothstep(0.30, 0.62, slope) * 2.5;
                w[3] = smoothstep(0.09, 0.12, hue) * (1 - smoothstep(0.17, 0.21, hue)) * smoothstep(0.12, 0.22, sat) * smoothstep(0.46, 0.58, lum);
                w[4] = smoothstep(0.62, 0.76, lum) * (1 - smoothstep(0.10, 0.22, sat));
                w[5] = 1 - smoothstep(0.10, 0.20, lum);
                w[6] = (grassy + warm) * smoothstep(0.08, 0.14, lum) * (1 - smoothstep(0.17, 0.26, lum)) * 0.9;
                w[7] = (1 - smoothstep(0.09, 0.20, sat)) * smoothstep(0.16, 0.26, lum) * (1 - smoothstep(0.60, 0.74, lum)) * 0.8;
                float wsum = 0; [unroll] for (int k = 0; k < 8; k++) { w[k] = w[k] * w[k]; wsum += w[k]; }
                float3 ratio = 1; float3 bend = 0; float lr = 1;
                if (_DetailOn > 0.5 && wsum > 0.02)
                {
                    float3 acc = 0; float3 nacc = 0; float size[8] = { _DetailSize0.x, _DetailSize0.y, _DetailSize0.z, _DetailSize0.w, _DetailSize1.x, _DetailSize1.y, _DetailSize1.z, _DetailSize1.w };
                    float3 tw = pow(abs(n0), 4); tw /= (tw.x + tw.y + tw.z);
                    float3 dx = ddx(wp), dy = ddy(wp);
                    [loop] for (int ly = 0; ly < 8; ly++)
                    {
                        float wl = w[ly] / wsum; if (wl < 0.03) continue;
                        float sc = 1.0 / size[ly];
                        float3 a, avg, nt;
                        if (ly == 2)
                        {   // rock: three projections blended by the facing, so walls show rock, not streaks
                            float2 ux = wp.zy * sc, uy = wp.xz * sc, uz = wp.xy * sc;
                            a = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailAlb, sampler_DetailAlb, ux, ly, dx.zy * sc, dy.zy * sc).rgb * tw.x + SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailAlb, sampler_DetailAlb, uy, ly, dx.xz * sc, dy.xz * sc).rgb * tw.y + SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailAlb, sampler_DetailAlb, uz, ly, dx.xy * sc, dy.xy * sc).rgb * tw.z;
                            float3 nx = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailNor, sampler_DetailNor, ux, ly, dx.zy * sc, dy.zy * sc).rgb * 2 - 1, ny = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailNor, sampler_DetailNor, uy, ly, dx.xz * sc, dy.xz * sc).rgb * 2 - 1, nz = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailNor, sampler_DetailNor, uz, ly, dx.xy * sc, dy.xy * sc).rgb * 2 - 1;
                            nt = float3(0, nx.y, nx.x) * tw.x + float3(ny.x, 0, ny.y) * tw.y + float3(nz.x, nz.y, 0) * tw.z;
                        }
                        else
                        {
                            float2 uv = wp.xz * sc;
                            a = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailAlb, sampler_DetailAlb, uv, ly, dx.xz * sc, dy.xz * sc).rgb;
                            float3 nn = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailNor, sampler_DetailNor, uv, ly, dx.xz * sc, dy.xz * sc).rgb * 2 - 1;
                            nt = float3(nn.x, 0, nn.y);
                        }
                        avg = SAMPLE_TEXTURE2D_ARRAY_LOD(_DetailAlb, sampler_DetailAlb, float2(0.5, 0.5), ly, 12).rgb;
                        acc += wl * a / max(avg, 0.03); nacc += wl * nt;
                    }
                    float cover = saturate(wsum * 3);
                    // on slopes the paint is stretched into streaks: take its colour from a softer level and let the rock
                    // carry the detail
                    float rockW = w[2] / wsum;
                    float3 soft = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, i.uv, 3.0).rgb;
                    c = lerp(c, soft, rockW * 0.75);
                    float3 rr = acc; lr = dot(rr, float3(0.3, 0.59, 0.11));
                    float3 rg = lerp(float3(lr, lr, lr), rr, 0.3);
                    rg = pow(max(rg, 0.001), 1.0 + 0.45 * rockW + 0.2) ; // a touch more contrast, most on rock
                    float snowW = w[4] / wsum;
                    ratio = lerp(1, rg, (0.9 - 0.45 * snowW) * cover);
                    bend = nacc * cover;
                }
                n = normalize(n0 + bend * 0.9);
                nl = saturate(dot(n, L.direction));
                c *= ratio * (1 + (clump - 0.5) * 0.10);
                float crack = 0;
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