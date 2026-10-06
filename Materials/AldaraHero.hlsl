// The heroes' materials (AldaraHeroRemaster marks each vertex; Aldara/VertexLit with _HeroHD): every piece keeps its
// painted colour and gains a real surface. Steel is brushed and hammered and mirrors the sky; gold is engraved with
// filigree; dark iron is scratched; painted plate has a lacquer gloss and worn chips; chain mail is woven links; dragon
// scale overlaps; leather has grain and worn edges; cloth is woven with a soft sheen; skin glows warm at the shadow's
// edge; hair has strands and a sliding highlight; gems sparkle from within; wood has grain; feathers have barbs.
// All detail is in each part's own space, so it rides the animation without swimming.
#ifndef ALDARA_HERO
#define ALDARA_HERO

float AHSat(float x) { return saturate(x); }
float3 AHSky(float3 r)
{   // a stand-in for a reflection probe: the sky's own light above, the ground's below, a bright horizon band
    float3 up = SampleSH(float3(0, 1, 0)), dn = SampleSH(float3(0, -1, 0)), side = SampleSH(normalize(float3(r.x, 0.001, r.z)));
    float y = r.y;
    float3 c = lerp(lerp(dn, side, 0.3) * 0.5, side * 1.1, saturate(y * 3 + 1)); c = lerp(c, up * 1.35, saturate(y * 1.6));
    c += up * 0.5 * exp(-abs(y - 0.05) * 12);   // the bright horizon band polished metal shows
    return c;
}
// GGX highlight of the sun (and its colour, shadow)
float AHSpec(float3 n, float3 V, float3 L, float rough)
{
    float3 H = normalize(L + V); float nh = saturate(dot(n, H)), a = max(0.03, rough * rough), a2 = a * a;
    float d = nh * nh * (a2 - 1) + 1; float D = a2 / (3.14159 * d * d);
    float nl = saturate(dot(n, L)), nv = saturate(dot(n, V)) + 1e-3; float k = a * 0.5; float Gv = nv / (nv * (1 - k) + k), Gl = nl / (nl * (1 - k) + k);
    return D * Gv * Gl * nl / (4 * nv + 1e-3) * 3.14159;
}
float3 AHFres(float3 f0, float vh) { return f0 + (1 - f0) * pow(1 - vh, 5); }
// how much of a pattern of this frequency (per unit of the part) can show at this pixel size
float AHFine(float freq, float fw) { return saturate(1.2 - freq * fw * 3.5); }
float _HeroDbg;

/// albedo (painted), the smoothed normal n, world point, object point op and object normal on, the material code
float3 AldaraHeroShade(float3 alb, float3 n, float3 wp, float4 posCS, float em, float3 op, float3 on, float code)
{
    int m = (int)round(code);
    float3 V = GetWorldSpaceNormalizeViewDir(wp);
    float dk = 0.35, metal = 0, rough = 0.6, h = 0, bumpK = 0, sheen = 0, sss = 0, aniso = 0, sparkle = 0, clear = 0;
    float3 ao3 = abs(on) + 0.05; ao3 /= (ao3.x + ao3.y + ao3.z);
    // a plane of the part to lay patterns on (the face's dominant axis)
    float2 pl = ao3.x > ao3.y && ao3.x > ao3.z ? op.zy : ao3.y > ao3.z ? op.xz : op.xy;
    float lum = dot(alb, float3(0.3, 0.59, 0.11)); float fw = max(1e-5, length(fwidth(op)));
    // how sharply the surface turns here: the worn, polished edges of plates and rims
    float curv = saturate(length(fwidth(on)) / fw * 0.012 - 0.1);
    if (m == 1 || m == 8)
    {   // steel / dark iron: brushed along the part, hammered dents, worn bright edges and dark seams
        float brush = (AGrad3(float3(op.x * 7, op.y * 160, op.z * 7)) * 0.5 + AGrad3(float3(op.x * 24, op.y * 420, op.z * 24)) * 0.25 * AHFine(420, fw)) * AHFine(160, fw);
        float2 vor = AVoronoi(pl * (m == 1 ? 7 : 5)); float dent = lerp(0.5, smoothstep(0.05, 0.6, vor.x), AHFine(7, fw));
        float wear = saturate(AFbm3(op * 18, 3) * 2.2 + 0.1);
        h = brush * 0.3 + dent * 0.25; bumpK = m == 1 ? 0.004 : 0.007;
        metal = 1; rough = (m == 1 ? 0.26 : 0.46) + brush * 0.08 + (1 - dent) * 0.06;
        alb = lerp(alb, alb * 1.15 + 0.04, wear * 0.25) * (0.96 + brush * 0.08);
        alb += curv * 0.14; rough -= curv * 0.1;
        if (m == 8) { float scr = pow(saturate(1 - abs(AGrad3(float3(op.x * 60, op.y * 9, op.z * 60))) * 9), 6) * AHFine(60, fw); alb += scr * 0.12; rough -= scr * 0.15; }
    }
    else if (m == 2)
    {   // gold: polished, engraved with scrolling filigree (dark grooves, bright raised ridges)
        float f = AFbm3(op * 4, 2); float fil = abs(sin(f * 14 + op.y * 12));
        float groove = (1 - smoothstep(0.0, 0.2, fil)) * AHFine(40, fw);
        h = -groove * 0.5 + AGrad3(op * 90) * 0.04; bumpK = 0.005;
        metal = 1; rough = 0.24 + groove * 0.25; dk = 0.42;
        float mx = max(alb.r, max(alb.g, alb.b)); alb = lerp(alb, float3(1.0, 0.8, 0.38) * (lum * 0.6 + mx * 0.45), 0.85);   // true gold, whatever the paint alb *= 1 - groove * 0.4;
        alb += float3(0.12, 0.09, 0.03) * curv; rough -= curv * 0.08;
    }
    else if (m == 9)
    {   // painted (enamelled) plate: a lacquer gloss over metal, with small chips showing the steel beneath
        float chip = smoothstep(0.62, 0.7, AFbm3(op * 26, 3) + 0.5) * AHFine(30, fw);
        h = AGrad3(op * 40) * 0.05 - chip * 0.3; bumpK = 0.004;
        metal = chip; rough = lerp(0.32, 0.3, chip); clear = 1 - chip;
        alb = lerp(alb * (0.95 + AGrad3(op * 12) * 0.1), float3(0.62, 0.64, 0.68), chip);
    }
    else if (m == 10)
    {   // chain mail: rows of interlocking rings
        float2 g = pl * 70; g.x += floor(g.y) * 0.5; float2 f = frac(g) - 0.5;
        float ring = abs(length(f * float2(1, 1.25)) - 0.33); float fc = AHFine(70, fw); float link = lerp(0.62, 1 - smoothstep(0.04, 0.12, ring), fc);
        h = link * fc * 0.6; bumpK = 0.004;
        metal = 1; rough = lerp(0.45, 0.34, fc);
        alb = lerp(alb * 0.18, alb * 1.15 + 0.04, link);
    }
    else if (m == 11)
    {   // dragon scale: overlapping scales, each a little domed, darker at the edges, a faint sheen of oil
        float2 g = pl * 30; g.x += floor(g.y) * 0.5; float2 f = frac(g); float2 c = f - float2(0.5, 0.15);
        float d = length(c * float2(1, 0.8)); float scaleIn = lerp(0.75, 1 - smoothstep(0.42, 0.55, d), AHFine(30, fw));
        h = scaleIn * (1 - d) * 0.6; bumpK = 0.005;
        metal = 0.35; rough = 0.34; clear = 0.4;
        alb *= lerp(0.45, 1.1, scaleIn * (0.7 + 0.3 * (1 - f.y)));
        alb += float3(0.05, 0.02, 0.08) * scaleIn;
    }
    else if (m == 3)
    {   // leather: fine grain, creases, worn lighter on the raised parts
        float2 vg = AVoronoi(pl * 95); float grain = (vg.y - vg.x) * AHFine(95, fw);
        float crease = AFbm3(op * float3(9, 22, 9), 3);
        h = grain * 0.25 + crease * 0.3; bumpK = 0.005;
        rough = 0.62 - grain * 0.15; metal = 0; clear = 0.15;
        alb *= 0.88 + crease * 0.3 + grain * 0.08;
    }
    else if (m == 4)
    {   // cloth: a woven weave, slubs in the thread, a soft fuzz sheen
        float2 w = pl * 260; float warp = sin(w.x * 3.14159) * 0.5 + 0.5, weft = sin(w.y * 3.14159) * 0.5 + 0.5;
        float weave = lerp(0.5, lerp(warp, weft, step(0.5, frac(floor(w.x) * 0.5 + floor(w.y) * 0.5))), AHFine(260, fw));
        float slub = AFbm3(op * float3(26, 3, 26), 3);
        h = weave * 0.2 + slub * 0.15; bumpK = 0.003;
        rough = 0.92; sheen = 0.6;
        alb *= 0.95 + weave * 0.08 + slub * 0.1;
    }
    else if (m == 5)
    {   // skin: a little colour in the cheeks and knuckles, warm light bleeding at the shadow's edge
        h = AGrad3(op * 140) * 0.04 * AHFine(140, fw); bumpK = 0.004; rough = 0.52; sss = 1;
        alb *= 1 + AGrad3(op * 8) * 0.06;
    }
    else if (m == 6)
    {   // hair: strands, a highlight sliding along them
        float str = AGrad3(float3(op.x * 140, op.y * 9, op.z * 140)) * AHFine(140, fw); h = str * 0.4; bumpK = 0.005; rough = 0.42; aniso = 1;
        alb *= 0.85 + str * 0.3;
    }
    else if (m == 7)
    {   // gems and glowing runes: cut facets that sparkle, the glow pulsing slowly
        float2 vg = AVoronoi(pl * 18 + _Time.y * 0.05); sparkle = pow(saturate(1 - vg.x * 3), 8) * (0.5 + 0.5 * sin(_Time.y * 4 + vg.y * 30)) * AHFine(18, fw);
        h = vg.x * 0.3; bumpK = 0.01; rough = 0.08; metal = 0.2; clear = 1;
        em = max(em, 0.25) * (0.85 + 0.15 * sin(_Time.y * 2 + op.y * 6));
    }
    else if (m == 12)
    {   // wood: grain running along the shaft, darker growth rings
        float ring = lerp(0.5, sin((op.x + op.z) * 140 + AFbm3(op * float3(6, 1.5, 6), 3) * 18) * 0.5 + 0.5, AHFine(140, fw));
        h = ring * 0.3 + AGrad3(op * float3(60, 6, 60)) * 0.15; bumpK = 0.005; rough = 0.66; clear = 0.2;
        alb *= 0.78 + ring * 0.3;
    }
    else if (m == 13)
    {   // feathers: barbs off each vane, soft and bright at the tips
        float barb = lerp(0.5, sin(op.y * 360 + abs(op.x) * 260) * 0.5 + 0.5, AHFine(360, fw));
        h = barb * 0.3; bumpK = 0.006; rough = 0.7; sheen = 0.5;
        alb *= 0.9 + barb * 0.12;
    }
    float3 nb = bumpK > 0 ? ABump(n, wp, h, bumpK * 0.6) : n;   // h in units of the part: bumpK is the relief depth

    // diffuse in the world's own light (sun, sky, shadows, torches), metals keeping little of it
    float3 diff = AldaraShade(alb * lerp(1, dk, metal), nb, wp, posCS, em, 0);
    Light L = GetMainLight(TransformWorldToShadowCoord(wp)); float sh = L.shadowAttenuation;
    float3 f0 = lerp(float3(0.04, 0.04, 0.04), alb, metal);
    float vh = saturate(dot(V, normalize(L.direction + V)));
    float3 F = AHFres(f0, vh);
    float3 c = diff;
    // the sun's highlight
    float sp = AHSpec(nb, V, L.direction, rough);
    if (aniso > 0)
    {   // Kajiya-Kay along the strands (the part's up)
        float3 T = normalize(TransformObjectToWorldDir(float3(0, 1, 0))); float3 Hh = normalize(L.direction + V);
        float th = dot(T, Hh); sp = pow(sqrt(saturate(1 - th * th)), 60) * 0.6 + pow(sqrt(saturate(1 - th * th)), 12) * 0.15;
    }
    c += L.color * sh * sp * F * (metal > 0.5 ? 1.0 : 0.6);
    if (clear > 0) c += L.color * sh * AHSpec(n, V, L.direction, 0.12) * 0.04 * clear;
    // reflections of the sky (metals, gems, lacquer)
    float nv = saturate(dot(nb, V)); float3 R = reflect(-V, nb);
    float3 env = AHSky(R) * lerp(1, 0.55, rough);
    float3 Fe = f0 + (max(1 - rough, f0) - f0) * pow(1 - nv, 5);
    float3 envT = env * Fe * (metal * 0.85 + clear * 0.35 + 0.08) * lerp(0.55, 1, sh); c += envT;
    // cloth sheen, skin's warm edge, gem sparkle
    if (sheen > 0) c += SampleSH(nb) * alb * pow(1 - nv, 3) * sheen * 0.8;
    if (sss > 0) { float ndl = dot(nb, L.direction); float edge = saturate(1 - abs(ndl * 2.2 - 0.25)); c += alb * float3(0.6, 0.18, 0.08) * edge * 0.35 * L.color * lerp(0.5, 1, sh); }
    if (sparkle > 0) c += lerp(alb, 1, 0.5) * sparkle * 1.6;
    if (_HeroDbg > 5.5) c = _HeroDbg < 6.5 ? nv.xxx : _HeroDbg < 7.5 ? nb * 0.5 + 0.5 : n * 0.5 + 0.5;
    else if (_HeroDbg > 1.5) c = _HeroDbg < 2.5 ? alb : _HeroDbg < 3.5 ? diff : _HeroDbg < 4.5 ? envT : c - diff - envT;
    else if (_HeroDbg > 0.5)
    {   // a debug view of the material codes
        float3 pc = m == 1 ? float3(0.8,0.8,0.9) : m == 2 ? float3(1,0.8,0.1) : m == 3 ? float3(0.5,0.25,0.1) : m == 4 ? float3(0.9,0.2,0.2) : m == 5 ? float3(1,0.7,0.6)
            : m == 6 ? float3(0.3,0.15,0.05) : m == 7 ? float3(0,1,1) : m == 8 ? float3(0.3,0.3,0.35) : m == 9 ? float3(0.2,0.4,1) : m == 10 ? float3(0.6,0.6,0.3)
            : m == 11 ? float3(0.2,0.8,0.3) : m == 12 ? float3(0.6,0.4,0.2) : m == 13 ? float3(1,1,1) : float3(0,0,0);
        c = pc * (0.6 + 0.4 * saturate(dot(n, float3(0.3, 0.8, -0.4))));
    }
    return c;
}
#endif
