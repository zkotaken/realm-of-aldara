Shader "Aldara/DunFloor"
{
    // A dungeon's baked floor (the browser's drawing, 512 px a tile). The original look shows it exactly as baked.
    // With enhanced rendering and HD structures on, it is given depth: the drawing's own lines (tile joints, cracks,
    // the edges of rugs and runes) become relief, a photographed surface of the dungeon's kind (worked stone, rough
    // rock, cobbles, marble, fieldstone) gives it grain, and the braziers, torches and the hero's light rake across it.
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Layer("Surface layer", Float) = 0
        _Tile("Surface size (units)", Float) = 2.4
        _Amt("Surface strength", Float) = 0.7
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "DunFloor"
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float4 _MainTex_TexelSize;
            TEXTURE2D_ARRAY(_StructAlb); SAMPLER(sampler_StructAlb);
            TEXTURE2D_ARRAY(_StructNra); SAMPLER(sampler_StructNra);
            float _AldaraHQ, _StructOn;
            static const float HM[15]  = { 0.929, 0.874, 0.865, 0.985, 0.968, 0.924, 0.834, 0.790, 0.794, 0.768, 0.752, 0.738, 0.843, 0.925, 0.937 };
            static const float AOM[15] = { 0.445, 0.883, 0.915, 0.894, 0.781, 0.783, 0.691, 0.702, 0.973, 0.872, 0.553, 1.000, 0.758, 0.960, 0.946 };
            CBUFFER_START(UnityPerMaterial) float _Layer, _Tile, _Amt; CBUFFER_END
            struct A { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; };
            V vert(A i) { V o; o.wp = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.wp); o.col = i.col; o.uv = i.uv; return o; }
            float L(float2 uv) { return dot(SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).rgb, float3(0.3, 0.59, 0.11)); }
            half4 frag(V i) : SV_Target
            {
                float4 base = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.col;
                if (_AldaraHQ < 0.5 || _StructOn < 0.5) return base;
                float2 t = _MainTex_TexelSize.xy * 1.5;
                float l0 = dot(base.rgb, float3(0.3, 0.59, 0.11));
                float hx = L(i.uv + float2(t.x, 0)) - L(i.uv - float2(t.x, 0)), hz = L(i.uv + float2(0, t.y)) - L(i.uv - float2(0, t.y));
                float lit = 0.6 + 0.4 * saturate((l0 - 0.1) * 8);  // the dark rock around the rooms gets a little less
                float mx = max(base.r, max(base.g, base.b)), sat = (mx - min(base.r, min(base.g, base.b))) / max(mx, 1e-3);
                lit *= 1 - saturate((sat - 0.45) * 4) * saturate((l0 - 0.3) * 4);   // lava, glowing runes and pools stay as drawn
                // the drawing's relief: darker lines read as joints sunk into the floor
                float3 n = normalize(float3(-hx * 5 * lit, 1, -hz * 5 * lit));
                float2 duv = i.wp.xz / _Tile; int layer = (int)_Layer;
                float4 a = SAMPLE_TEXTURE2D_ARRAY(_StructAlb, sampler_StructAlb, duv, layer);
                float4 q = SAMPLE_TEXTURE2D_ARRAY(_StructNra, sampler_StructNra, duv, layer);
                float dl = dot(a.rgb, float3(0.3, 0.59, 0.11));
                float3 c = base.rgb * lerp(1, dl * 2, _Amt * lit);
                float2 dn = (q.xy * 2 - 1) * 0.9 * lit;
                n = normalize(n + float3(dn.x, 0, dn.y));
                c *= lerp(1, saturate(0.3 + a.a * 1.15) / HM[layer], 0.45 * lit) * lerp(1, q.w / AOM[layer], 0.5 * lit);
                // a raking light from the north-west so the relief reads, and the lights in the room
                float3 Ld = normalize(float3(-0.5, 0.75, 0.45));
                float sh = saturate(dot(n, Ld)) / Ld.y;
                c *= lerp(1, sh, 0.75);
                #if defined(_ADDITIONAL_LIGHTS)
                uint cnt = GetAdditionalLightsCount();
                for (uint li = 0; li < cnt; li++)
                {
                    Light Lt = GetAdditionalLight(li, i.wp);
                    float an = saturate(dot(n, Lt.direction));
                    float3 V = normalize(GetCameraPositionWS() - i.wp);
                    c += base.rgb * Lt.color * Lt.distanceAttenuation * an * 0.8;
                    c += Lt.color * Lt.distanceAttenuation * pow(saturate(dot(n, normalize(Lt.direction + V))), 28) * saturate(1.2 - q.z) * 0.35 * lit;
                }
                #endif
                return half4(c, base.a);
            }
            ENDHLSL
        }
    }
}
