Shader "Aldara/Water"
{
    Properties { _Glint("Glint", Float) = 0.6 _Speed("Speed", Float) = 0.4 _HQ("Enhanced look", Float) = 1 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "../Materials/AldaraNoise.hlsl"
            float _Glint, _Speed, _HQ; float _AldaraHQ; float _WaterRefract;
            struct A { float4 pos : POSITION; float4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float3 wp : TEXCOORD0; float4 sp : TEXCOORD1; };
            V vert(A i){ V o; float3 wp = TransformObjectToWorld(i.pos.xyz);
                float lava = step(0.9, i.col.r) * step(i.col.b, 0.3);
                wp.y += sin(wp.x * 0.7 + _Time.y * 1.3) * 0.04 + cos(wp.z * 0.6 + _Time.y) * 0.04;
                o.wp = wp; o.pos = TransformWorldToHClip(wp); o.col = i.col; o.sp = ComputeScreenPos(o.pos); return o; }
            // eye depth for the perspective or orthographic camera
            float EyeDepth(float raw) {
                if (unity_OrthoParams.w > 0.5) {
                    #if UNITY_REVERSED_Z
                    raw = 1 - raw;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, raw); }
                return LinearEyeDepth(raw, _ZBufferParams); }
            float h21(float2 p){ return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vn(float2 p){ float2 i = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(h21(i), h21(i+float2(1,0)), f.x), lerp(h21(i+float2(0,1)), h21(i+float2(1,1)), f.x), f.y); }
            half4 frag(V i) : SV_Target {
                float t = _Time.y * _Speed;
                float n = vn(i.wp.xz * 0.9 + float2(t, t*0.7)) * 0.6 + vn(i.wp.xz * 2.3 - float2(t*1.3, t)) * 0.4;
                float lava = step(0.9, i.col.r) * step(i.col.b, 0.3);
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float fres = pow(1 - saturate(V.y), 3);
                Light L = GetMainLight();
                float3 c = i.col.rgb * (0.75 + 0.35 * n) * lerp(0.6 + 0.5 * L.color, 1, lava);
                float gl = smoothstep(0.78, 0.95, n) * _Glint * (1 - lava);
                c += gl * L.color + fres * 0.25 * (1 - lava);
                c += lava * (n * n) * float3(1.2, 0.5, 0.1);
                if (_AldaraHQ * _HQ < 0.5) return half4(c, saturate(i.col.a + fres * 0.2 + gl * 0.3));
                // enhanced: rippled normals catch the sun, deeper water darkens, the shore foams
                float e = 0.35; float2 q = i.wp.xz;
                float hx = vn(q * 1.7 + float2(t * 1.1, 0)) - vn(q * 1.7 + float2(t * 1.1 + e, 0)), hz = vn(q * 1.7 + float2(0, t)) - vn(q * 1.7 + float2(0, t + e));
                float hx2 = vn(q * 4.1 - float2(t * 1.7, t)) - vn(q * 4.1 - float2(t * 1.7 - e, t)), hz2 = vn(q * 4.1 - float2(t, t * 1.3)) - vn(q * 4.1 - float2(t, t * 1.3 - e));
                float3 wn = normalize(float3((hx + hx2 * 0.5) * 1.6, 1, (hz + hz2 * 0.5) * 1.6));
                float3 Hh = normalize(L.direction + V);
                float dayL = smoothstep(0.55, 0.95, dot(L.color, float3(0.3, 0.5, 0.2)));
                float sp = (pow(saturate(dot(wn, Hh)), 180) * 2.2 * lerp(0.18, 1, dayL) + pow(saturate(dot(wn, Hh)), 24) * 0.12);
                float2 suv = i.sp.xy / i.sp.w;
                float sceneZ = EyeDepth(SampleSceneDepth(suv)), waterZ = EyeDepth(i.pos.z);
                float thick = max(0, sceneZ - waterZ);
                float shore = 1 - saturate(thick / 0.55);
                float foamN = vn(q * 3.3 + float2(t * 0.6, -t * 0.4)) * 0.6 + vn(q * 7.1 - t) * 0.4;
                float foam = saturate(shore * 1.4 - foamN * 0.9) * (1 - lava);
                if (lava > 0.5)
                {   // enhanced lava: a slow molten flow under drifting plates of cooled crust, the cracks between the
                    // plates glowing, a bright seam along the shore where it meets the rock, all bright enough to bloom
                    float tl = _Time.y;
                    float2 lq = q * 0.42;
                    float2 w = float2(AFbm2(lq * 0.7 + tl * 0.03, 3), AFbm2(lq * 0.7 + 5.2 - tl * 0.025, 3));
                    float2 fp = lq + w * 1.8 + float2(tl * 0.05, tl * 0.035);
                    float2 vc = AVoronoi(fp * 1.15);
                    float seam = 1 - smoothstep(0.0, 0.11, vc.y - vc.x);
                    float heat = AFbm2(fp * 0.8 - tl * 0.04, 4) + 0.5;
                    float molten = smoothstep(0.44, 0.68, heat);
                    float pulse = 0.88 + 0.12 * sin(tl * 1.7 + heat * 7.0);
                    float3 hot = lerp(float3(1.05, 0.22, 0.03), float3(1.75, 0.95, 0.32), saturate((heat - 0.45) * 2.2));
                    float3 basalt = float3(0.11, 0.04, 0.03) * (0.75 + 0.5 * (AGrad2(fp * 7.0) + 0.5)) + float3(0.10, 0.025, 0.0) * (1 - vc.x);
                    float glowAmt = saturate(molten + seam * (1 - molten) * 0.95);
                    float3 lc = lerp(basalt, hot * 1.2 * pulse, glowAmt);
                    // the shore: a white-hot line, then a charred rim on the rock side
                    float rim = smoothstep(0.25, 0.75, shore) * (1 - smoothstep(0.85, 1.0, shore));
                    lc = lerp(lc, float3(1.9, 1.0, 0.35) * pulse, rim * 0.75);
                    lc = lerp(lc, float3(0.12, 0.04, 0.03), smoothstep(0.88, 1.0, shore) * 0.7);
                    return half4(lc, 1);
                }
                // water, enhanced: the bed seen through the surface (bent by the waves, fading into deep colour with depth),
                // the sky and sun reflected off gradient-noise waves, light patterns on the shallow bed, and foam that
                // breaks along the shore in moving lines
                float tw = _Time.y;
                float2 wq = q;
                // three wave scales drifting in different directions; normal from finite differences
                #define WH(p) (AGrad2((p) * 0.45 + float2(tw * 0.05, tw * 0.035)) * 0.55 + AGrad2((p) * 1.15 - float2(tw * 0.09, -tw * 0.06)) * 0.30 + AGrad2((p) * 2.9 + float2(-tw * 0.17, tw * 0.13)) * 0.15)
                float ee = 0.05;
                float h0 = WH(wq), hxw = (WH(wq + float2(ee, 0)) - h0) / ee, hzw = (WH(wq + float2(0, ee)) - h0) / ee;
                float3 wnn = normalize(float3(-hxw * 0.28, 1, -hzw * 0.28));
                float3 Hw = normalize(L.direction + V);
                float ndv = saturate(dot(wnn, V));
                float fresW = 0.04 + 0.96 * pow(1 - ndv, 5);
                // the bed through the water
                float depthK = saturate(thick / 2.5);
                float2 ruv = suv + wnn.xz * 0.018 * saturate(thick * 2);
                float rz = EyeDepth(SampleSceneDepth(ruv));
                if (rz < waterZ) ruv = suv;           // never pull in things standing in front of the water
                float3 bed = SampleSceneColor(ruv);
                float thickR = max(0, EyeDepth(SampleSceneDepth(ruv)) - waterZ);
                // light patterns on the shallow bed
                float2 c1 = AVoronoi(wq * 1.1 + float2(tw * 0.35, tw * 0.22) + wnn.xz * 2), c2 = AVoronoi(wq * 1.45 - float2(tw * 0.26, tw * 0.31));
                float caust = (1 - smoothstep(0.0, 0.10, c1.y - c1.x)) * (1 - smoothstep(0.0, 0.16, c2.y - c2.x));
                caust *= saturate(1 - thickR / 1.2) * dayL;
                bed *= 1 + caust * 0.55;
                // absorption: red goes first, then green; the lake's own colour fills the depth
                float3 absorb = exp(-thickR * float3(1.9, 0.75, 0.45));
                float3 deepCol = i.col.rgb * float3(0.55, 0.74, 0.92) * (0.55 + 0.45 * L.color);
                float3 under = lerp(deepCol, bed * float3(0.86, 0.98, 1.0), absorb);
                // reflection: the sky's colour, brighter toward the horizon, and the sun
                float3 skyUp = SampleSH(float3(0, 1, 0)), skyH = SampleSH(normalize(float3(0, 0.25, 1)));
                float3 refl = lerp(skyUp, skyH, 0.5) * 1.15 + float3(0.04, 0.06, 0.08);
                float3 wc = lerp(under, refl, saturate(fresW * 1.8 + 0.06));
                float spec = pow(saturate(dot(wnn, Hw)), 420) * 5.0 * lerp(0.15, 1, dayL) + pow(saturate(dot(wnn, Hw)), 60) * 0.18 * dayL;
                wc += spec * L.color;
                // foam: moving lines that break up as they reach the shore, plus a soft wet edge
                float shoreW = 1 - saturate(thick / 0.6);
                float fN = AFbm2(wq * 2.2 + float2(tw * 0.25, -tw * 0.18), 3) + 0.5;
                float lines = smoothstep(0.55, 0.85, frac(thick * 2.6 - tw * 0.35 + fN * 0.6)) * shoreW;
                float foamW = saturate(lines * 1.2 + smoothstep(0.75, 1.0, shoreW) * 0.9 - fN * 0.55) * (1 - lava);
                wc = lerp(wc, float3(0.94, 0.97, 1.0) * (0.7 + 0.3 * L.color), foamW * 0.9);
                float aW = saturate(thick / 0.07);
                return half4(wc, aW);
            }
            ENDHLSL
        }
    }
}