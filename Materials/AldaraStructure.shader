Shader "Aldara/Structure"
{
    // Buildings, walls, camps, lairs, ruins, caves and dungeon set pieces. The original look is Aldara/VertexLit's: the
    // browser's colours under its sun. With enhanced rendering and HD structures on (_StructOn), each face also gets
    // the photographed surface the remaster pass picked for it (AldaraStructureRemaster: stone, rock, planks, timber,
    // roof tiles, slate, thatch, plaster, iron, hide, cloth, cobbles, marble, sandstone) laid under the model's own
    // colour with its relief and roughness, rounded and worn outside corners, shadowed inside corners, crevice
    // shadow from the material's height, dirt splashed up from the ground and moss on the stone that faces the sky.
    Properties { _Tint("Tint", Color) = (1,1,1,1) _Wind("Wind sway", Float) = 0 _Emit("Emission boost", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "AldaraLighting.hlsl"
        #include "AldaraNoise.hlsl"
        CBUFFER_START(UnityPerMaterial) float4 _Tint; float _Wind; float _Emit; CBUFFER_END
        float3 Sway(float3 wp, float3 op){ float h = max(0, op.y); float k = _Wind * h * h * 0.01;
            wp.x += sin(_Time.y * 1.6 + wp.x * 0.3 + wp.z * 0.2) * k; wp.z += cos(_Time.y * 1.3 + wp.z * 0.3) * k * 0.7; return wp; }
        ENDHLSL
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            TEXTURE2D_ARRAY(_StructAlb); SAMPLER(sampler_StructAlb);
            TEXTURE2D_ARRAY(_StructNra); SAMPLER(sampler_StructNra);
            float _StructOn, _StructDbg;
            //                          stone field rock  plank beam  clay  slate thatch plast metal leath cloth cobble marble sand
            static const float TILE[15] = { 2.4, 2.2, 3.5,  1.8,  1.3,  1.7,  1.8,  2.2,   2.6,  1.3,  1.6,  0.9,  2.2,   2.6,   2.2 };
            static const float DET[15]  = { 0.9, 0.9, 0.9,  0.8,  0.75, 0.85, 0.8,  0.85,  0.5,  0.6,  0.6,  0.45, 0.9,   0.45,  0.8 };
            static const float CHR[15]  = { 0.25,0.25,0.25, 0.2,  0.2,  0.3,  0.2,  0.25,  0.15, 0.1,  0.2,  0.0,  0.25,  0.3,   0.25 };
            static const float NRM[15]  = { 1.0, 1.0, 1.0,  0.9,  0.9,  1.0,  1.0,  1.0,   0.6,  0.8,  0.7,  0.6,  1.0,   0.5,   1.0 };
            static const float SPC[15]  = { 0.12,0.1, 0.1,  0.1,  0.1,  0.22, 0.3,  0.03,  0.07, 0.9,  0.25, 0.05, 0.12,  0.5,   0.1 };
            static const float CAV[15]  = { 0.55,0.6, 0.4,  0.35, 0.25, 0.5,  0.45, 0.5,   0.15, 0.3,  0.2,  0.2,  0.55,  0.25,  0.5 };
            // mean crevice shade and occlusion of each layer, so the relief darkens the joints without darkening the model
            static const float HM[15]   = { 0.929, 0.874, 0.865, 0.985, 0.968, 0.924, 0.834, 0.790, 0.794, 0.768, 0.752, 0.738, 0.843, 0.925, 0.937 };
            static const float AOM[15]  = { 0.445, 0.883, 0.915, 0.894, 0.781, 0.783, 0.691, 0.702, 0.973, 0.872, 0.553, 1.000, 0.758, 0.960, 0.946 };
            static const float MOSS[15] = { 0.6, 0.8, 0.7,  0.2,  0.3,  0.25, 0.5,  0.5,   0.1,  0.0,  0.0,  0.0,  0.7,   0.15,  0.5 };
            struct A { float4 pos : POSITION; float3 n : NORMAL; float4 col : COLOR; float2 uv2 : TEXCOORD1; float4 uv3 : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float3 wp : TEXCOORD0; float3 n : TEXCOORD1; float fog : TEXCOORD2; float2 em : TEXCOORD3;
                       float3 op : TEXCOORD4; float3 on : TEXCOORD5; float4 d : TEXCOORD6; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A i){ V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 wp = Sway(TransformObjectToWorld(i.pos.xyz), i.pos.xyz);
                o.wp = wp; o.pos = TransformWorldToHClip(wp); o.n = TransformObjectToWorldNormal(i.n); o.col = i.col * _Tint;
                o.fog = ComputeFogFactor(o.pos.z); o.em = i.uv2; o.op = i.pos.xyz; o.on = i.n; o.d = i.uv3; return o; }

            // a tangent frame from screen derivatives (no tangents needed; right for any projection)
            float3 Perturb(float3 N, float3 p, float2 uv, float2 tn)
            {
                float3 dp1 = ddx(p), dp2 = ddy(p); float2 duv1 = ddx(uv), duv2 = ddy(uv);
                float3 dp2perp = cross(dp2, N), dp1perp = cross(N, dp1);
                float3 T = dp2perp * duv1.x + dp1perp * duv2.x, B = dp2perp * duv1.y + dp1perp * duv2.y;
                float invmax = rsqrt(max(1e-12, max(dot(T, T), dot(B, B))));
                return normalize(T * invmax * tn.x + B * invmax * tn.y + N);
            }
            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(front ? i.n : -i.n);
                float3 alb = i.col.rgb; float em = i.em.x + _Emit; float specK = 0.16;
                if (_AldaraHQ > 0.5 && _StructOn > 0.5 && i.d.w > 0.5)
                {
                    int id = (int)round(i.d.w) - 2;   // w = surface + 1, layer = surface - 1
                    float3 on = normalize(front ? i.on : -i.on); float3 op = i.op;
                    float3 dPdx = ddx(i.wp), dPdy = ddy(i.wp);
                    float wear = 0, cavity = 1;
                    if (id >= 0)
                    {
                        // the face's own projection: walls and roofs run along their slope, flat faces from above
                        float s = 1.0 / TILE[id];
                        float nh = max(0.3, length(on.xz)); float flat = smoothstep(0.93, 0.97, abs(on.y));
                        float wx = saturate((abs(on.x) / max(1e-4, abs(on.x) + abs(on.z)) - 0.38) / 0.24);
                        float2 uA = float2(op.x, op.y / nh) * s, uB = float2(op.z, op.y / nh) * s, uC = op.xz * s;
                        float4 aA = SAMPLE_TEXTURE2D_ARRAY(_StructAlb, sampler_StructAlb, uA, id), nA = SAMPLE_TEXTURE2D_ARRAY(_StructNra, sampler_StructNra, uA, id);
                        float4 aB = SAMPLE_TEXTURE2D_ARRAY(_StructAlb, sampler_StructAlb, uB, id), nB = SAMPLE_TEXTURE2D_ARRAY(_StructNra, sampler_StructNra, uB, id);
                        float4 aC = SAMPLE_TEXTURE2D_ARRAY(_StructAlb, sampler_StructAlb, uC, id), nC = SAMPLE_TEXTURE2D_ARRAY(_StructNra, sampler_StructNra, uC, id);
                        float k = NRM[id];
                        float3 nPA = Perturb(n, i.wp, uA, (nA.xy * 2 - 1) * k), nPB = Perturb(n, i.wp, uB, (nB.xy * 2 - 1) * k), nPC = Perturb(n, i.wp, uC, (nC.xy * 2 - 1) * k);
                        float4 a = lerp(lerp(aA, aB, wx), aC, flat), q = lerp(lerp(nA, nB, wx), nC, flat);
                        if (_StructDbg > 1.5) { float2 uu = lerp(lerp(uA, uB, wx), uC, flat); return half4(frac(uu), wx, 1); }
                        if (_StructDbg > 0.5) return half4(frac(id * 0.37), frac(id * 0.61), frac(id * 0.83), 1);
                        n = normalize(lerp(lerp(nPA, nPB, wx), nPC, flat));
                        float lum = dot(a.rgb, float3(0.3, 0.59, 0.11));
                        float3 det = lerp(lum.xxx, a.rgb, CHR[id]) * 2;
                        alb *= lerp(1, det, DET[id]);
                        cavity = min(1.3, lerp(1, saturate(0.3 + a.a * 1.15) / HM[id], CAV[id]) * lerp(1, q.w / AOM[id], 0.6));
                        specK = SPC[id] * saturate(1.25 - q.z);
                        if (id == 9) specK *= 1 + 2 * dot(i.col.rgb, 0.33);   // polished trim shines
                        // dirt splashed up from the ground on walls, moss where stone faces the sky
                        float gn = AFbm3(op * 1.7, 3);
                        float grime = saturate(1 - op.y / 0.9) * saturate(0.6 + gn) * (1 - flat * 0.5);
                        alb = lerp(alb, alb * float3(0.72, 0.64, 0.55), grime * 0.4);
                        float mossM = saturate(on.y * 1.6 - 0.3) * saturate((AFbm3(op * 0.9 + 7.1, 3) + 0.05) * 3) * saturate(1.2 - a.a) * MOSS[id];
                        alb = lerp(alb, float3(0.26, 0.33, 0.15) * (0.7 + lum), mossM * 0.55);
                        specK *= 1 - mossM;
                        wear = saturate(AGrad3(op * 9.0) * 1.5 + 0.55);
                    }
                    // the creases: rounded, worn outside corners and shadowed inside corners
                    float3 dd = i.d.xyz, ad = abs(dd);
                    float dm = min(ad.x, min(ad.y, ad.z));
                    float sg = ad.x <= ad.y && ad.x <= ad.z ? dd.x : (ad.y <= ad.z ? dd.y : dd.z);
                    float bw = max(i.em.y, 0.004);
                    if (dm < 1)
                    {
                        float3 R1 = cross(dPdy, n), R2 = cross(n, dPdx); float det2 = dot(dPdx, R1);
                        float3 g = abs(det2) > 1e-12 ? (ddx(dm) * R1 + ddy(dm) * R2) / det2 : 0;
                        float gl = length(g);
                        if (sg >= 0)
                        {
                            float r = pow(saturate(1 - dm / bw), 1.5);
                            if (gl > 1e-4) n = normalize(n - g / gl * r * 1.2);
                            float edgeW = saturate(1 - dm / (bw * 2.2)) * wear;
                            alb = lerp(alb, saturate(alb * 1.28 + 0.035), edgeW * 0.7);
                        }
                        else cavity *= lerp(0.55, 1, saturate(dm / (bw * 3.5)));
                    }
                    alb *= cavity * 1.06;
                    em *= 1;
                }
                float3 c = AldaraShade(alb, n, i.wp, i.pos, em, specK);
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 pos : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; };
            V vert(A i){ V o; UNITY_SETUP_INSTANCE_ID(i);
                float3 wp = Sway(TransformObjectToWorld(i.pos.xyz), i.pos.xyz); float3 wn = TransformObjectToWorldNormal(i.n);
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
            Cull Off ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 pos : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; };
            V vert(A i){ V o; UNITY_SETUP_INSTANCE_ID(i); o.pos = TransformWorldToHClip(Sway(TransformObjectToWorld(i.pos.xyz), i.pos.xyz)); return o; }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
