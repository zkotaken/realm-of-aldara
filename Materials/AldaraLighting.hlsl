// The game's shading, shared by its lit shaders. The original look is the browser's: a soft sun (wrap lighting), sky
// ambient and shadows. The enhanced look (_AldaraHQ, switched by AldaraGraphics) keeps those colours and adds what a
// modern renderer brings: screen-space ambient occlusion, torch and fire light on the models, a soft sun highlight, a
// sky-coloured rim, and a warm bounce from the ground.
#ifndef ALDARA_LIGHTING
#define ALDARA_LIGHTING
float _AldaraHQ;
float3 AldaraShade(float3 albedo, float3 n, float3 wp, float4 posCS, float em, float specK)
{
    Light L = GetMainLight(TransformWorldToShadowCoord(wp));
    float ndl = dot(n, L.direction);
    float nl = saturate(ndl) * 0.8 + 0.2 * saturate(ndl * 0.5 + 0.5);
    float3 amb = SampleSH(n);
    if (_AldaraHQ < 0.5) return albedo * (amb + L.color * nl * L.shadowAttenuation) + albedo * em;
    float aoI = 1, aoD = 1;
    #if defined(_SCREEN_SPACE_OCCLUSION)
    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(posCS)); aoI = ao.indirectAmbientOcclusion; aoD = ao.directAmbientOcclusion;
    #endif
    float3 V = GetWorldSpaceNormalizeViewDir(wp);
    float sh = L.shadowAttenuation;
    // a touch more shape from the sun, the shadowed side keeping the sky's colour
    float3 direct = L.color * nl * sh * aoD;
    float3 bounce = float3(0.55, 0.45, 0.32) * saturate(-n.y * 0.6 + 0.25) * 0.10;
    float3 c = albedo * (amb * aoI * 0.97 + direct + bounce);
    // soft highlight and rim
    float3 Hh = normalize(L.direction + V);
    // only on the sides of things (walls, bodies, armour): roofs and the ground keep their painted flatness
    float side = saturate(1 - abs(n.y) * 1.15);
    float spec = pow(saturate(dot(n, Hh)), 32) * specK * sh * saturate(ndl * 4) * side;
    c += L.color * spec;
    float fr = pow(1 - saturate(dot(n, V)), 3);
    c += SampleSH(float3(0, 1, 0)) * fr * 0.18 * side * (0.4 + 0.6 * sh) * aoI;
    // local lights: lamps, campfires, lava, the hero's glow
    #if defined(_ADDITIONAL_LIGHTS)
    uint cnt = GetAdditionalLightsCount();
    for (uint li = 0; li < cnt; li++)
    {
        Light A = GetAdditionalLight(li, wp, half4(1, 1, 1, 1));   // with its realtime shadow, when the light casts one
        float an = saturate(dot(n, A.direction) * 0.75 + 0.25) * lerp(0.15, 1, A.shadowAttenuation);
        c += albedo * A.color * A.distanceAttenuation * an;
        c += A.color * A.distanceAttenuation * A.shadowAttenuation * pow(saturate(dot(n, normalize(A.direction + V))), 24) * specK * 0.6;
    }
    #endif
    return c + albedo * em;
}
#endif
