Shader "Aldara/VertexLit"
{
    Properties { _Tint("Tint", Color) = (1,1,1,1) _Wind("Wind sway", Float) = 0 _Emit("Emission boost", Float) = 0 _NpcRim("NPC aura (rgb, strength)", Color) = (0,0,0,0) _FoeFx("Monster menace", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "AldaraLighting.hlsl"
        #include "AldaraNoise.hlsl"
        CBUFFER_START(UnityPerMaterial) float4 _Tint; float _Wind; float _Emit; float4 _NpcRim; float _FoeFx; CBUFFER_END
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
                float3 alb = i.col.rgb; float em = i.em + _Emit; float foeVein = 0;
                if (_AldaraHQ > 0.5 && _NpcRim.a > 0)
                {   // the people of Aldara (AldaraNpcFx): richer cloth, gilded trim that glows, grounded feet and a soft aura
                    float hgt = i.wp.y - UNITY_MATRIX_M._m13;
                    float mx = max(alb.r, max(alb.g, alb.b)), mn = min(alb.r, min(alb.g, alb.b)); float sat = (mx - mn) / max(mx, 1e-3);
                    alb = lerp(dot(alb, float3(0.3, 0.59, 0.11)).xxx, alb, 1.12) * (0.97 + 0.06 * sin(i.wp.y * 38 + i.wp.x * 9));
                    float gold = saturate((alb.r - alb.b) * 3 - 0.6) * saturate((alb.g - alb.b) * 3 - 0.3) * saturate(sat * 2 - 0.6);
                    em += gold * 0.35 * _NpcRim.a;
                    alb *= lerp(0.62, 1, saturate(hgt / 0.45));
                    if (_FoeFx > 0)
                    {   // monsters (AldaraFoeFx): darker, harsher hide with glowing veins of their land's colour crawling over it
                        float lum = dot(alb, float3(0.3, 0.59, 0.11));
                        alb = lerp(lum.xxx, alb, 1.25) * 0.8;
                        // ridged noise, warped, slowly flowing upward: thin branching veins, sparse, only on the darker hide
                        float3 q = i.wp * 2.6 + float3(0, -_Time.y * 0.25, 0);
                        q += float3(AGrad3(q * 0.7 + 11.3), AGrad3(q * 0.7 + 27.1), AGrad3(q * 0.7 + 3.7)) * 0.9;
                        float r1 = 1 - abs(AGrad3(q)), r2 = 1 - abs(AGrad3(q * 2.1 + 5.2));
                        float vein = pow(saturate((r1 - 0.9) / 0.1), 2.2) + 0.45 * pow(saturate((r2 - 0.93) / 0.07), 2.5) * r1;
                        vein *= saturate(1.15 - lum * 1.5) * (0.6 + 0.4 * saturate(AGrad3(i.wp * 0.8 + _Time.y * 0.15) + 0.6));
                        foeVein = vein * _FoeFx;
                    }
                }
                float3 c = AldaraShade(alb, n, i.wp, i.pos, em, 0.16 + 0.2 * saturate(_NpcRim.a));
                if (_AldaraHQ > 0.5 && _NpcRim.a > 0)
                {
                    float3 V = GetWorldSpaceNormalizeViewDir(i.wp);
                    float fr = pow(1 - saturate(abs(dot(n, V))), 2.6);
                    float pulse = 0.75 + 0.25 * sin(_Time.y * 1.7 + i.wp.x * 0.7 + i.wp.z * 0.5);
                    float3 tint = lerp(_NpcRim.rgb, _NpcRim.rgb.gbr, 0.18 * (0.5 + 0.5 * sin(_Time.y * 0.6 + i.wp.y * 3)));
                    c += tint * fr * min(_NpcRim.a, 1.0) * pulse;
                    if (_FoeFx > 0)
                    {
                        float hgt2 = i.wp.y - UNITY_MATRIX_M._m13;
                        float under = saturate(-n.y * 0.7 + 0.45) * saturate(1 - hgt2 / 0.9);          // lit from the glow on the ground
                        float back = pow(1 - saturate(abs(dot(n, V))), 1.4) * saturate(dot(n, -_MainLightPosition.xyz) + 0.6);
                        c += _NpcRim.rgb * (under * 0.4 + back * 0.4) * saturate(_FoeFx) * pulse;
                        c += lerp(_NpcRim.rgb, 1, 0.3) * foeVein * (1.3 + 0.5 * sin(_Time.y * 2.4 + i.wp.x * 2));
                    }
                }
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
                // characters stand squashed in depth (AldaraView.Squash) so they read like the browser's sprites: the
                // usual normal bias eats such thin bodies, so theirs is much gentler and a hero, a townsman or a goblin
                // throws a solid shadow rather than a faint sliver
                float kz = length(UNITY_MATRIX_M[2].xyz) / max(1e-4, length(UNITY_MATRIX_M[0].xyz));
                float nb = 1;
                if (kz < 0.7) nb = 0.15;
                float invNdotL = 1.0 - saturate(dot(_LightDirection, wn));
                wp = _LightDirection * _ShadowBias.xxx + wp; wp = wn * (invNdotL * _ShadowBias.y * nb) + wp;
                float4 p = TransformWorldToHClip(wp);
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