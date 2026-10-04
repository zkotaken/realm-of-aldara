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
            #include "../Materials/AldaraNoise.hlsl"
            float _Glint, _Speed, _HQ; float _AldaraHQ;
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
                // water: turquoise in the shallows deepening to blue, light dancing on the bottom, a softer sparkle
                float3 shallowC = i.col.rgb * float3(0.82, 1.08, 1.04) + float3(0.02, 0.05, 0.05);
                float3 deep = lerp(shallowC, i.col.rgb * float3(0.62, 0.72, 0.86), saturate(thick / 3.2));
                float2 c1 = AVoronoi(q * 1.25 + float2(t * 0.9, t * 0.55)), c2 = AVoronoi(q * 1.6 - float2(t * 0.6, t * 0.8));
                float caust = (1 - smoothstep(0.0, 0.07, c1.y - c1.x)) * (1 - smoothstep(0.0, 0.12, c2.y - c2.x));
                caust *= saturate(1 - thick / 0.8) * (1 - lava) * dayL;
                gl *= 0.3;
                float3 sky = SampleSH(float3(0, 1, 0));
                float3 hc = deep * (0.78 + 0.30 * n) * (0.6 + 0.5 * L.color);
                hc += sp * L.color * 0.9 + fres * 0.30 * sky + gl * L.color * 0.5 + caust * L.color * 0.16;
                hc = lerp(hc, float3(0.92, 0.96, 1.0) * (0.75 + 0.25 * L.color), foam * 0.85);
                float a = saturate(i.col.a + fres * 0.22 + gl * 0.3 + sp * 0.4 + foam * 0.6 + caust * 0.1) * saturate(thick / 0.12 + 0.35);
                return half4(hc, a);
            }
            ENDHLSL
        }
    }
}