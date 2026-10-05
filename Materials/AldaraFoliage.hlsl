// Trees, bushes and the swaying props (Aldara/Foliage, Aldara/FoliageFade). Everything Aldara/VertexLit does, plus,
// from the tree pass (AldaraTreeRemaster) in uv0 = (atlas u, atlas v, kind, occlusion):
// kind 1, leaf cards: cut out of the leaf atlas, shaded round their crown, fluttering, lit through from behind;
// kind 2, bark: furrowed along the trunk; and on every tree a little colour of its own and darkening deep in the crown.
#ifndef ALDARA_FOLIAGE
#define ALDARA_FOLIAGE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "AldaraLighting.hlsl"
#include "AldaraNoise.hlsl"
CBUFFER_START(UnityPerMaterial) float4 _Tint; float _Wind; float _Emit; float _Alpha; CBUFFER_END
TEXTURE2D(_LeafTex); SAMPLER(sampler_LeafTex);
float3 Sway(float3 wp, float3 op){ float h = max(0, op.y); float k = _Wind * h * h * 0.01;
    wp.x += sin(_Time.y * 1.6 + wp.x * 0.3 + wp.z * 0.2) * k; wp.z += cos(_Time.y * 1.3 + wp.z * 0.3) * k * 0.7; return wp; }
float3 Flutter(float3 wp, float3 op, float3 wn, float kind)
{
    if (kind < 0.5 || kind > 1.5) return wp;
    float ph = dot(op, float3(3.1, 1.7, 2.3)); float k = 0.025 + _Wind * 0.03;
    return wp + wn * sin(_Time.y * 3.3 + ph) * k + float3(sin(_Time.y * 2.1 + ph * 1.3), 0, cos(_Time.y * 1.9 + ph)) * k * 0.6;
}
struct FA { float4 pos : POSITION; float3 n : NORMAL; float4 col : COLOR; float4 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct FV { float4 pos : SV_POSITION; float4 col : COLOR; float3 wp : TEXCOORD0; float3 n : TEXCOORD1; float fog : TEXCOORD2; float em : TEXCOORD3; float4 uv : TEXCOORD4; float3 op : TEXCOORD5; float tint : TEXCOORD6; UNITY_VERTEX_INPUT_INSTANCE_ID };
FV FoliageVert(FA i)
{
    FV o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
    float3 wn = TransformObjectToWorldNormal(i.n);
    float3 wp = Flutter(Sway(TransformObjectToWorld(i.pos.xyz), i.pos.xyz), i.pos.xyz, wn, i.uv.z);
    o.wp = wp; o.pos = TransformWorldToHClip(wp); o.n = wn; o.col = i.col * _Tint;
    o.fog = ComputeFogFactor(o.pos.z); o.em = i.uv2.x; o.uv = i.uv; o.op = i.pos.xyz;
    float2 org = float2(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m23); o.tint = frac(sin(dot(org, float2(12.9898, 78.233))) * 43758.5453);
    return o;
}
float4 FoliageColour(FV i, bool front)
{
    float kind = round(i.uv.z); float ao = i.uv.w > 0 ? i.uv.w : 1;
    float3 alb = i.col.rgb; float3 n = normalize(i.n);
    if (kind == 1) { float4 t = SAMPLE_TEXTURE2D(_LeafTex, sampler_LeafTex, i.uv.xy); clip(t.a - 0.5); alb *= 0.6 + 0.42 * t.r; }
    else n = front ? n : -n;
    bool hq = _AldaraHQ > 0.5;
    if (hq)
    {   // every tree its own shade, a little warmer or cooler, lighter or darker
        float h = i.tint - 0.5; alb *= 1 + h * 0.16; alb.r *= 1 + h * 0.1; alb.b *= 1 - h * 0.08;
        if (kind == 2) { float3 q = i.op * float3(10, 1.2, 10); float fur = abs(AGrad3(q)); alb *= 0.72 + 0.4 * saturate(fur * 3) + 0.08 * AGrad3(i.op * 6); }
        alb *= ao;
    }
    float3 c = AldaraShade(alb, n, i.wp, i.pos, i.em + _Emit, kind == 1 ? 0.08 : 0.16);
    if (hq && kind == 1)
    {   // sunlight through the leaves
        Light L = GetMainLight(TransformWorldToShadowCoord(i.wp));
        c += alb * L.color * saturate(dot(-n, L.direction) * 0.6 + 0.25) * 0.14 * (0.4 + 0.6 * L.shadowAttenuation);
    }
    c = MixFog(c, i.fog);
    return float4(c, 1);
}
#endif
