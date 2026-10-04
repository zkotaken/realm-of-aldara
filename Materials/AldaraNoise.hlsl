// Smooth procedural noise for the enhanced look: gradient noise in 2D and 3D, fractal sums of it, and cell (Voronoi)
// distances. The hashes avoid sin() so they stay stable far from the origin.
#ifndef ALDARA_NOISE
#define ALDARA_NOISE
float2 AHash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy) * 2 - 1;
}
float3 AHash33(float3 p)
{
    float3 p3 = frac(p * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return frac((p3.xxy + p3.yxx) * p3.zyx) * 2 - 1;
}
// gradient noise, about -0.7 .. 0.7
float AGrad2(float2 p)
{
    float2 i = floor(p), f = frac(p); float2 u = f * f * f * (f * (f * 6 - 15) + 10);
    float a = dot(AHash22(i), f), b = dot(AHash22(i + float2(1, 0)), f - float2(1, 0));
    float c = dot(AHash22(i + float2(0, 1)), f - float2(0, 1)), d = dot(AHash22(i + 1), f - 1);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}
float AGrad3(float3 p)
{
    float3 i = floor(p), f = frac(p); float3 u = f * f * f * (f * (f * 6 - 15) + 10);
    float n000 = dot(AHash33(i), f), n100 = dot(AHash33(i + float3(1, 0, 0)), f - float3(1, 0, 0));
    float n010 = dot(AHash33(i + float3(0, 1, 0)), f - float3(0, 1, 0)), n110 = dot(AHash33(i + float3(1, 1, 0)), f - float3(1, 1, 0));
    float n001 = dot(AHash33(i + float3(0, 0, 1)), f - float3(0, 0, 1)), n101 = dot(AHash33(i + float3(1, 0, 1)), f - float3(1, 0, 1));
    float n011 = dot(AHash33(i + float3(0, 1, 1)), f - float3(0, 1, 1)), n111 = dot(AHash33(i + 1), f - 1);
    return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y), lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
}
// fractal sums, about -0.6 .. 0.6
float AFbm2(float2 p, int oct)
{
    float s = 0, a = 0.5;
    for (int o = 0; o < oct; o++) { s += a * AGrad2(p); p = float2(p.x * 1.6 - p.y * 1.2, p.x * 1.2 + p.y * 1.6) + 17.13; a *= 0.5; }
    return s;
}
float AFbm3(float3 p, int oct)
{
    float s = 0, a = 0.5;
    for (int o = 0; o < oct; o++) { s += a * AGrad3(p); p = p * 2.03 + 11.7; a *= 0.5; }
    return s;
}
// distance to the nearest and second nearest cell point
float2 AVoronoi(float2 p)
{
    float2 i = floor(p), f = frac(p); float F1 = 8, F2 = 8;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            float2 g = float2(x, y); float2 r = g + AHash22(i + g) * 0.45 + 0.5 - f; float d = dot(r, r);
            if (d < F1) { F2 = F1; F1 = d; } else if (d < F2) F2 = d;
        }
    return float2(sqrt(F1), sqrt(F2));
}
// a height field's surface gradient turned into a bent normal (screen-space derivatives, no tangents needed)
float3 ABump(float3 n, float3 wp, float h, float k)
{
    float3 dpx = ddx(wp), dpy = ddy(wp);
    float3 r1 = cross(dpy, n), r2 = cross(n, dpx); float det = dot(dpx, r1);
    float3 g = sign(det) * (ddx(h) * r1 + ddy(h) * r2);
    return normalize(abs(det) * n - g * k);
}
#endif
