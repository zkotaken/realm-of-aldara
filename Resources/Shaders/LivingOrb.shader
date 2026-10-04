// The browser's living orb (orbDraw) painted per pixel: dark glass, the ghost of lost health or mana, two rolling wave
// layers, the crest, caustic light, bubbles, embers or the rune ring and its motes, hit ripples, the low-health throb,
// the glass's inner shadow, and the glow around a nearly empty health orb. Coordinates are the browser's 200 px canvas.
Shader "Aldara/LivingOrb"
{
    Properties { }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Hp, _T, _Lvl, _Lag, _Beat, _Low, _M, _TS, _Lin;
            float4 outc(float3 c, float a) { c = saturate(c); if (_Lin > 0.5) c = GammaToLinearSpace(c); return float4(c, saturate(a)); }
            float4 _Bub[24]; float4 _Emb[24]; float4 _Rip[6];
            float4 _Bg0, _Bg1, _L0, _L1, _L2, _Ghost, _Lay2, _Caus;
            float3 over(float3 d, float3 c, float a) { return lerp(d, c, saturate(a)); }
            float3 lerp3(float3 a, float3 b, float3 c, float u) { return u < 0.25 ? lerp(a, b, u / 0.25) : lerp(b, c, (u - 0.25) / 0.75); }
            float4 frag(v2f_img i) : SV_Target
            {
                const float S = 200, R = 97, CX = 100, CY = 100, PI2 = 6.2831853;
                float2 p = float2(i.uv.x * _TS - _M, (1 - i.uv.y) * _TS - _M);
                float t = _T, lvl = _Lvl, d = length(p - float2(CX, CY));
                float3 red = float3(1, 60 / 255.0, 40 / 255.0); float blur = (14 + 22 * _Beat) * S / 96, glowA = 0.5 + 0.5 * _Beat;
                if (d > R + 1)
                {
                    if (_Low > 0.5) { float f = exp(-pow((d - R) / (blur * 0.55), 2)); return outc(red, glowA * f); }
                    return 0;
                }
                float x = p.x, y = p.y, a = x * 0.045 + t * 1.9, b = x * 0.11 - t * 2.7;
                float y1 = lvl + 2 + sin(a) * (4 + _Beat * 3) + sin(b) * 2, y2 = lvl + sin(a + 2.1) * 3 + sin(b + 4.2) * 2.5;
                float yl = _Lag + sin(a + 1.3) * 3 + sin(b + 2.6) * 1.5, yc = lvl + sin(a) * 4 + sin(b) * 2;
                float gd = length(p - float2(CX, CY * 0.8));
                float3 c = lerp(_Bg0.rgb, _Bg1.rgb, saturate((gd - 10) / (R - 10)));
                if (_Lag < lvl - 1 && y >= yl) c = over(c, _Ghost.rgb, _Ghost.a);
                if (y >= y1) c = over(c, lerp3(_L0.rgb, _L1.rgb, _L2.rgb, saturate((y - lvl) / max(1, S - lvl))), saturate(y - y1 + 0.5));
                if (y >= y2) c = over(c, _Lay2.rgb, 0.35 * saturate(y - y2 + 0.5));
                c = over(c, float3(1, 1, 1), 0.55 * saturate(1.5 - abs(y - yc)));
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float aa = t * 0.5 + k * 2.1; float2 q = float2(CX + cos(aa) * 40, lvl + 40 + sin(aa * 1.3) * 20 + k * 25);
                    if (q.y >= lvl) { float dd = length(p - q); c += _Caus.rgb * _Caus.a * saturate(1 - dd / 34); }
                }
                // bubbles: a thin ring and a highlight
                for (int n = 0; n < 24; n++)
                {
                    float4 B = _Bub[n]; if (B.z <= 0) continue; float dd = length(p - B.xy);
                    c = over(c, float3(1, 1, 1), 0.45 * saturate(1 - abs(dd - B.z)));
                    float hr = B.z * 0.35; c = over(c, float3(1, 1, 1), 0.35 * saturate(hr + 0.5 - length(p - (B.xy - hr))));
                }
                if (_Hp > 0.5)
                {
                    for (int e = 0; e < 24; e++) { float4 E = _Emb[e]; if (E.z <= 0) continue; float dd = length(p - E.xy); c += float3(1, E.w, 80 / 255.0) * E.z * saturate(2.1 - dd); }
                }
                else
                {
                    float rot = t * 0.35; c = over(c, float3(150, 200, 255) / 255.0, 0.5 * saturate(1.25 - abs(d - 78)));
                    // the twelve runes: find the nearest slot on the ring
                    float ang = atan2(y - CY, x - CX) - rot; float slot = round(ang / (PI2 / 12)); float sa = slot * (PI2 / 12) + rot;
                    float2 g = float2(CX + cos(sa) * 78, CY + sin(sa) * 78), dv = p - g; float u = dot(dv, float2(-sin(sa), cos(sa))), w = dot(dv, float2(cos(sa), sin(sa)));
                    int si = (int)fmod(fmod(slot, 12) + 12, 12); float cov = si % 3 == 0 ? saturate((1 - (abs(u) / 3 + abs(w) / 5)) * 3 + 0.5) : saturate(1.5 - abs(u)) * saturate(3.5 - abs(w));
                    c = over(c, float3(200, 230, 255) / 255.0, 0.85 * cov);
                    [unroll] for (int m = 0; m < 5; m++)
                    {
                        float aa = t * 0.8 + m * 1.256, r = 20 + m * 9; float2 q = float2(CX + cos(aa) * r, CY * 0.9 + sin(aa * 1.7) * r * 0.5);
                        if (q.y <= lvl) c += float3(200, 225, 255) / 255.0 * (0.35 + 0.3 * sin(t * 3 + m)) * saturate(2.3 - length(p - q));
                    }
                }
                for (int j = 0; j < 6; j++)
                {
                    float4 Q = _Rip[j]; if (Q.w <= 0) continue; float u = Q.z; float rx = 12 + u * 90, ry = 4 + u * 22; float2 e2 = (p - float2(CX, lvl + 6)) / float2(rx, ry);
                    float qq = length(e2); float2 gg = e2 / float2(rx, ry); float gl = length(gg) / max(qq, 1e-4); float dd = abs(qq - 1) / max(gl, 1e-4);
                    float lw = 3 * (1 - u) + 0.5; c = over(c, _Hp > 0.5 ? float3(1, 220 / 255.0, 200 / 255.0) : float3(220 / 255.0, 235 / 255.0, 1), 0.7 * (1 - u) * saturate(lw / 2 + 0.5 - dd));
                }
                if (_Low > 0.5) c = over(c, float3(1, 50 / 255.0, 30 / 255.0), (0.15 + 0.4 * _Beat) * saturate((d - R * 0.55) / (R * 0.45)));
                c = over(c, float3(0, 0, 0), 0.75 * saturate((d - R * 0.7) / (R * 0.3)));
                float edge = saturate(R + 0.5 - d);
                if (_Low > 0.5) { float f = glowA, al = edge + (1 - edge) * f; return outc((c * edge + red * f * (1 - edge)) / max(al, 1e-4), al); }
                return outc(c, edge);
            }
            ENDCG
        }
    }
}
