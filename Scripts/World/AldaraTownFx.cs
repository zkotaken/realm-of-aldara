using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // The life in Lorenmar and Valcrest that the browser draws on top of its buildings (townFxFor, kdFxFor and the
    // town ticks): the fountains' jets and ripples, the windmills' turning sails, flags in the wind, forge glow and
    // sparks, chimney smoke, the pigeons round Lorenmar's fountain that scatter when you come near, and the chickens
    // by the windmill. Points come from the browser (export_townfx.js) as canvas offsets from each building's foot.
    public class AldaraTownFx : MonoBehaviour
    {
        class Obj { public float x, y, L, fR; public bool kd, dark; public string key, fcol; public Vector2[] ch, flags; public Vector2? hub, forge, anvil, top; public Vector2[][] jets; }
        class Pig { public float x, y, h, vx, vy, t, tx, ty, f = 1; public int st; }
        class Ck { public float x, y, hx, hy, t, f = 1, peck, vx, vy, mv; public Color col; }
        class Smoke { public float x, y, vx, vy, r, life, max, h; public bool dark, kd; }
        readonly List<Obj> objs = new List<Obj>(); readonly List<Pig> pigs = new List<Pig>(); readonly List<Ck> cks = new List<Ck>(); readonly List<Smoke> smoke = new List<Smoke>(), kdSmoke = new List<Smoke>();
        Vector2 center; float townR, tsp = 0.6f; Vector3 kdc; bool loaded;
        void Awake() { AldaraVfx.DrawAir += Draw; }
        void OnDestroy() { AldaraVfx.DrawAir -= Draw; }
        static Color Hx(string h) { return AldaraRules.Hex(h); }
        static Vector2 V(JToken t) { return new Vector2((float)t[0], (float)t[1]); }
        static Vector2[] Vs(JToken t) { if (t == null) return null; var a = (JArray)t; var o = new Vector2[a.Count]; for (int i = 0; i < a.Count; i++) o[i] = V(a[i]); return o; }
        void Load()
        {
            loaded = true; var ta = Resources.Load<TextAsset>("townfx"); if (!ta) return; var j = JObject.Parse(ta.text);
            foreach (JObject o in (JArray)j["objs"])
            {
                var b = new Obj { x = (float)o["x"], y = (float)o["y"], kd = (int)o["kd"] != 0, key = (string)o["key"], dark = o["dark"] != null && (int)o["dark"] != 0, fcol = (string)o["fcol"], ch = Vs(o["ch"]), flags = Vs(o["flags"]), L = o["L"] != null ? (float)o["L"] : 0, fR = o["fR"] != null ? (float)o["fR"] : 34 };
                if (o["hub"] != null) b.hub = V(o["hub"]); if (o["forge"] != null) b.forge = V(o["forge"]); if (o["anvil"] != null) b.anvil = V(o["anvil"]); if (o["top"] != null) b.top = V(o["top"]);
                if (o["jets"] != null) { var ja = (JArray)o["jets"]; b.jets = new Vector2[ja.Count][]; for (int i = 0; i < ja.Count; i++) b.jets[i] = Vs(ja[i]); }
                objs.Add(b);
            }
            center = V(j["center"]); townR = (float)j["town"][0]; tsp = (float)j["tsp"]; kdc = new Vector3((float)j["kd"][0], (float)j["kd"][1], (float)j["kd"][2]);
            foreach (JArray p in (JArray)j["pigeons"]) pigs.Add(new Pig { x = (float)p[0], y = (float)p[1], t = Random.value * 5, f = Random.value < 0.5f ? 1 : -1 });
            foreach (JArray c in (JArray)j["chickens"]) cks.Add(new Ck { x = (float)c[0], y = (float)c[1], hx = (float)c[2], hy = (float)c[3], col = Hx((string)c[4]), t = Random.value * 5, f = Random.value < 0.5f ? 1 : -1 });
        }
        bool TownNear(AldaraPlayer P) { return Mathf.Abs(P.x - center.x) < townR + 900 && Mathf.Abs(P.y - center.y) < townR + 700; }
        bool KdNear(AldaraPlayer P) { return Mathf.Abs(P.x - kdc.x) < kdc.z + 900 && Mathf.Abs(P.y - kdc.y) < kdc.z + 700; }
        static bool OnScreen(AldaraPlayer P, float x, float y, float mx = 200, float my = 500) { return Mathf.Abs(x - P.x) < 960 + mx && Mathf.Abs(y - P.y) < 540 + my; }

        void Update()
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I; if (!AldaraSave.Ready || !P || !H || AldaraWorld.Dun) return; if (!loaded) Load();
            float dt = Mathf.Min(0.1f, Time.deltaTime);
            if (TownNear(P))
            {
                foreach (var p in pigs)
                {
                    p.t += dt;
                    if (p.st == 0)
                    {
                        if (Dist(P.x, P.y, p.x, p.y) < 80 && H.alive) { p.st = 1; float a = Mathf.Atan2(p.y - P.y, p.x - P.x) + (Random.value - 0.5f); p.vx = Mathf.Cos(a) * 170; p.vy = Mathf.Sin(a) * 120; p.t = 0; }
                        else if (Random.value < dt * 0.6f) { p.x += (Random.value - 0.5f) * 14; p.y += (Random.value - 0.5f) * 8; p.f = Random.value < 0.5f ? 1 : -1; }
                    }
                    else if (p.st == 1) { p.x += p.vx * dt; p.y += p.vy * dt; p.h = Mathf.Min(160, p.h + 140 * dt); if (p.t > 2.2f) { p.st = 2; p.t = 0; float a = Random.value * Mathf.PI * 2, r = 90 + Random.value * 120; p.tx = center.x + Mathf.Cos(a) * r; p.ty = center.y + Mathf.Sin(a) * r; } }
                    else if (p.t > 4)
                    {
                        float dx = p.tx - p.x, dy = p.ty - p.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > 4) { p.x += dx / d * Mathf.Min(d, 150 * dt); p.y += dy / d * Mathf.Min(d, 150 * dt); p.f = dx < 0 ? -1 : 1; }
                        p.h = Mathf.Max(0, p.h - (d < 120 ? 120 : 0) * dt); if (d <= 4 && p.h <= 0) p.st = 0;
                    }
                }
                foreach (var k in cks)
                {
                    k.t += dt; if (k.peck > 0) k.peck -= dt; else if (Random.value < dt * 0.8f) k.peck = 0.5f;
                    if (Random.value < dt * 0.5f) { k.vx = (Random.value - 0.5f) * 40; k.vy = (Random.value - 0.5f) * 24; k.mv = 0.6f; }
                    if (k.mv > 0) { k.mv -= dt; k.x += k.vx * dt; k.y += k.vy * dt; if (k.vx != 0) k.f = k.vx < 0 ? -1 : 1; float dx = k.x - k.hx, dy = k.y - k.hy; if (Mathf.Sqrt(dx * dx + dy * dy * 2.56f) > 150) { k.x -= k.vx * dt * 2; k.y -= k.vy * dt * 2; } }
                }
                SmokeTick(P, dt, false, 14, smoke, 160);
            }
            if (KdNear(P)) SmokeTick(P, dt, true, 18, kdSmoke, 200);
        }
        void SmokeTick(AldaraPlayer P, float dt, bool kd, float rate, List<Smoke> L, int cap)
        {
            if (Random.value < dt * rate)
            {
                var vis = objs.FindAll(o => o.kd == kd && o.ch != null && o.ch.Length > 0 && OnScreen(P, o.x, o.y));
                if (vis.Count > 0)
                {
                    var o = vis[Random.Range(0, vis.Count)]; var c = o.ch[Random.Range(0, o.ch.Length)]; bool dk = kd && o.dark;
                    L.Add(new Smoke { x = o.x + c.x, y = o.y + c.y, vx = 8 + Random.value * 6, vy = -18 - Random.value * 8, r = dk ? 7 : 4, max = (dk ? 5 : 3.5f) + Random.value * 2, dark = dk, kd = kd, h = AldaraVfx.LZ(o.x, o.y) });
                }
            }
            for (int i = L.Count - 1; i >= 0; i--) { var s = L[i]; s.life += dt; s.x += s.vx * dt; s.y += s.vy * dt; s.r += dt * (s.dark ? 10 : 7); if (s.life > s.max) L.RemoveAt(i); }
            if (L.Count > cap) L.RemoveRange(0, L.Count - cap);
        }
        static float Dist(float ax, float ay, float bx, float by) { return Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)); }

        // ---------- drawing ----------
        void Draw(AldaraVfx V)
        {
            var P = AldaraPlayer.I; if (!loaded || !P || AldaraWorld.Dun) return; var N = V.AirN; var A = V.AirA; float now = Time.time;
            bool tn = TownNear(P), kn = KdNear(P); if (!tn && !kn) return;
            foreach (var o in objs)
            {
                if (o.kd ? !kn : !tn) continue; if (!OnScreen(P, o.x, o.y, 300, 400)) continue; float h = AldaraVfx.LZ(o.x, o.y); fa = AldaraOccluders.IsHidden(o.x, o.y) ? 0.45f : 1;
                if (o.jets != null) Fountain(N, A, o, h, now);
                if (o.hub.HasValue) Sails(N, o, h, now);
                if (o.flags != null) Flags(N, o, h, now);
                if (o.forge.HasValue) Forge(A, o, h, now);
            }
            if (tn) foreach (var s in smoke) SmokePuff(N, s);
            if (kn) foreach (var s in kdSmoke) SmokePuff(N, s);
            if (tn) { foreach (var p in pigs) if (OnScreen(P, p.x, p.y)) Pigeon(N, p, now); foreach (var k in cks) if (OnScreen(P, k.x, k.y)) Chicken(N, k); }
        }
        float fa = 1;
        Color Fa(Color c) { c.a *= fa; return c; }
        static Color Rgba(float r, float g, float b, float a) { return new Color(r / 255f, g / 255f, b / 255f, a); }
        static Vector2 Q(Vector2 a, Vector2 c, Vector2 b, float t) { float u = 1 - t; return u * u * a + 2 * u * t * c + t * t * b; }
        void Curve(AldaraSketch s, Vector2 a, Vector2 c, Vector2 b, float w, Color col, float h, float dashOn = 0, float dashOff = 0, float phase = 0)
        {
            const int n = 14; var pts = new Vector2[n + 1]; float[] len = new float[n + 1];
            for (int i = 0; i <= n; i++) { pts[i] = Q(a, c, b, i / (float)n); if (i > 0) len[i] = len[i - 1] + Vector2.Distance(pts[i], pts[i - 1]); }
            for (int i = 0; i < n; i++)
            {
                if (dashOn > 0) { float m = (len[i] + len[i + 1]) / 2 + phase, per = dashOn + dashOff; float u = ((m % per) + per) % per; if (u > dashOn) continue; }
                s.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, w, col, h);
            }
        }
        void Fountain(AldaraSketch N, AldaraSketch A, Obj o, float h, float now)
        {
            var O = new Vector2(o.x, o.y);
            for (int k = 0; k < o.jets.Length; k++)
            {
                var J = o.jets[k];
                if (!o.kd)
                {
                    Vector2 p0 = O + J[0], mid = O + J[1], p1 = O + J[2];
                    Curve(A, p0, mid, p1, 3, Rgba(170, 215, 255, 0.38f), h); Curve(A, p0, mid, p1, 1, Rgba(230, 245, 255, 0.5f), h, 3, 6, now * 40);
                    float ph = (now * 1.3f + k * 0.37f) % 1; AldaraVfx.Ring(A, p1.x, p1.y, 4 + ph * 16, (4 + ph * 16) * tsp, 1.2f, Rgba(210, 235, 255, 0.5f * (1 - ph)), h);
                }
                else
                {
                    Vector2 p0 = O + J[0], mid = O + J[1], p1 = O + J[2], q0 = O + J[3], q1 = O + J[4];
                    Curve(A, p0, mid, p1, 3, Rgba(170, 215, 255, 0.4f), h); Curve(A, p0, mid, p1, 1, Rgba(230, 245, 255, 0.5f), h, 3, 6, now * 40);
                    Curve(A, q0, new Vector2(q0.x * 0.4f + q1.x * 0.6f, q0.y - 4), q1, 2.2f, Rgba(190, 225, 255, 0.3f), h);
                    float ph = (now * 1.2f + k * 0.37f) % 1; AldaraVfx.Ring(A, q1.x, q1.y, 5 + ph * 20, (5 + ph * 20) * tsp, 1.2f, Rgba(210, 235, 255, 0.5f * (1 - ph)), h);
                }
            }
            if (o.top.HasValue) { var top = O + o.top.Value; for (int k = 0; k < 6; k++) { float a = now * 3 + k, r = 6 + ((now * 20 + k * 7) % 14); A.Rect(top.x + Mathf.Cos(a) * r, top.y - Mathf.Abs(Mathf.Sin(a * 1.7f)) * 10, 2, 2, Rgba(220, 240, 255, 0.5f), h); } }
        }
        void Sails(AldaraSketch N, Obj o, float h, float now)
        {
            var c = new Vector2(o.x, o.y) + o.hub.Value; float L = o.L, rot = now * 0.55f;
            System.Func<float, float, float, Vector2> R = (ang, x, y) => { float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang); return c + new Vector2(x * cs - y * sn, x * sn + y * cs); };
            for (int k = 0; k < 4; k++)
            {
                float a = rot + (k + 1) * Mathf.PI / 2;
                System.Action<float, float, float, float, Color> rect = (x, y, w, hh, col) => N.Poly(new[] { R(a, x, y), R(a, x + w, y), R(a, x + w, y + hh), R(a, x, y + hh) }, col, h);
                rect(-3, -L, 6, L, Fa(Hx("#4a3420"))); rect(4, -L + 8, L * 0.24f, L * 0.8f, Fa(Rgba(235, 225, 200, 0.92f)));
                var sc = Fa(Hx("#5a4028")); for (int s = 0; s < 6; s++) { float y = -L + 8 + s * L * 0.8f / 6; var p0 = R(a, 4, y); var p1 = R(a, 4 + L * 0.24f, y); N.Line(p0.x, p0.y, p1.x, p1.y, 1.2f, sc, h); }
                var m0 = R(a, 4 + L * 0.12f, -L + 8); var m1 = R(a, 4 + L * 0.12f, -L * 0.2f); N.Line(m0.x, m0.y, m1.x, m1.y, 1.2f, sc, h);
            }
            AldaraVfx.Circle(N, c.x, c.y, 7, Fa(Hx("#3a2a1a")), h);
        }
        void Flags(AldaraSketch N, Obj o, float h, float now)
        {
            bool kd = o.kd; float pole = kd ? 38 : 34, len = kd ? 32 : 26, bot = kd ? 21 : 20, amp = kd ? 3.4f : 3; int seg = kd ? 7 : 6; var col = Fa(Hx(o.fcol ?? "#8a1a1a"));
            foreach (var f in o.flags)
            {
                var p = new Vector2(o.x, o.y) + f; N.Line(p.x, p.y, p.x, p.y - pole, 2, Fa(Hx("#2a2a2e")), h);
                var pts = new List<Vector2>(); for (int s = 0; s <= seg; s++) { float u = s / (float)seg; pts.Add(new Vector2(p.x + u * len, p.y - pole + Mathf.Sin(now * 4 + u * 4 + p.x * 0.01f) * amp * u)); }
                for (int s = seg; s >= 0; s--) { float u = s / (float)seg; pts.Add(new Vector2(p.x + u * len, p.y - bot + Mathf.Sin(now * 4 + u * 4 + p.x * 0.01f) * amp * u)); }
                // the flag as a strip of quads (it is not convex once it waves)
                for (int s = 0; s < seg; s++) { int t0 = s, t1 = s + 1, b1 = 2 * seg + 1 - (s + 1), b0 = 2 * seg + 1 - s; N.Poly(new[] { pts[t0], pts[t1], pts[b1], pts[b0] }, col, h); }
                var gold = Fa(Hx("#e0b64b"));
                if (kd) { AldaraVfx.Circle(N, p.x + 13, p.y - 30 + Mathf.Sin(now * 4 + 1.8f + p.x * 0.01f) * 1.4f, 3.6f, gold, h); N.Rect(p.x + 9, p.y - 36 + Mathf.Sin(now * 4 + 1.4f + p.x * 0.01f) * 1.2f, 8, 2, gold, h); }
                else AldaraVfx.Circle(N, p.x + 11, p.y - 27 + Mathf.Sin(now * 4 + 1.7f + p.x * 0.01f) * 1.3f, 3, gold, h);
            }
        }
        void Forge(AldaraSketch A, Obj o, float h, float now)
        {
            var p = new Vector2(o.x, o.y) + o.forge.Value; float f = 0.7f + 0.3f * Mathf.Sin(now * 9) * Mathf.Sin(now * 5.7f);
            AldaraVfx.Glow(A, p.x, p.y, o.fR * f, Rgba(255, 170, 60, 1), o.kd ? 0.6f : 0.55f, h);
            if (o.kd) { if (Mathf.Sin(now * 2.3f + o.x) > 0.92f) for (int k = 0; k < 8; k++) { float a = -Mathf.PI / 2 + (Random.value - 0.5f) * 2.2f, d = Random.value * 24; A.Rect(p.x + Mathf.Cos(a) * d, p.y - 10 + Mathf.Sin(a) * d, 2, 2, Rgba(255, 160 + Random.value * 80, 60, 0.9f), h); } }
            else if (o.anvil.HasValue && AldaraFolk.I != null)
            {
                AldaraFolk.Folk smith = null; foreach (var n in AldaraFolk.I.town) if (n.role == "smith") { smith = n; break; }
                if (smith != null && smith.spark > 0) { var an = new Vector2(o.x, o.y) + o.anvil.Value; for (int k = 0; k < 10; k++) { float a = -Mathf.PI / 2 + (Random.value - 0.5f) * 2.4f, d = Random.value * 26 * (1 - smith.spark); A.Rect(an.x + Mathf.Cos(a) * d, an.y - 6 + Mathf.Sin(a) * d, 2, 2, Rgba(255, 160 + Random.value * 80, 60, 0.9f), h); } }
            }
        }
        static void SmokePuff(AldaraSketch N, Smoke s)
        {
            float k = s.life / s.max, a = (k < 0.15f ? k / 0.15f : 1 - (k - 0.15f) / 0.85f) * (s.dark ? 0.34f : s.kd ? 0.26f : 0.28f); if (a <= 0) return;
            AldaraVfx.Circle(N, s.x, s.y, s.r, s.dark ? Rgba(70, 64, 62, a) : Rgba(200, 200, 205, a), s.h);
        }
        void Pigeon(AldaraSketch N, Pig p, float now)
        {
            float h = AldaraVfx.LZ(p.x, p.y), y = p.y - p.h, f = p.f; System.Func<float, float, Vector2> T = (x, yy) => new Vector2(p.x + x * f, y + yy);
            if (p.h < 1) N.Ellipse(p.x, p.y + 1, 5, 1.6f, Rgba(0, 0, 0, 0.25f), h);
            N.Ellipse(p.x, y - 4, 5.5f, 3.4f, Hx("#8a8a96"), h);
            N.Poly(new[] { T(-5, -4), T(-9, -3), T(-5, -2) }, Hx("#6a6a78"), h);
            AldaraVfx.Circle(N, p.x + 4.5f * f, y - 7, 2.3f, Hx("#5a6a7a"), h); N.Rect(p.x + (f > 0 ? 6.5f : -8.3f), y - 7.3f, 1.8f, 0.9f, Hx("#e0a040"), h);
            if (p.st > 0 && p.h > 0) { float w = Mathf.Sin(now * 28) * 6; N.Poly(new[] { T(-1, -5), T(-4, -11 - w), T(3, -5) }, Hx("#9a9aa6"), h); }
            else { N.Rect(p.x + (f > 0 ? -1 : 0), y - 1, 1, 2, Hx("#e0a040"), h); N.Rect(p.x + (f > 0 ? 1.5f : -2.5f), y - 1, 1, 2, Hx("#e0a040"), h); }
            if (p.h > 1) N.Ellipse(p.x, p.y, 4, 1.4f, Rgba(0, 0, 0, 0.18f), h);
        }
        void Chicken(AldaraSketch N, Ck k)
        {
            float h = AldaraVfx.LZ(k.x, k.y), f = k.f, pk = k.peck > 0 ? Mathf.Sin((0.5f - k.peck) / 0.5f * Mathf.PI) : 0; System.Func<float, float, Vector2> T = (x, y) => new Vector2(k.x + x * f, k.y + y);
            N.Ellipse(k.x, k.y + 1, 7, 2, Rgba(0, 0, 0, 0.25f), h);
            var leg = Hx("#e0a040"); N.Poly(new[] { T(-2, -3), T(-0.8f, -3), T(-0.8f, 1), T(-2, 1) }, leg, h); N.Poly(new[] { T(1, -3), T(2.2f, -3), T(2.2f, 1), T(1, 1) }, leg, h);
            N.Ellipse(k.x, k.y - 7, 7, 5, k.col, h); N.Poly(new[] { T(-6, -9), T(-10, -14), T(-5, -6) }, k.col, h);
            var hd = T(5, -12 + pk * 6); AldaraVfx.Circle(N, hd.x, hd.y, 3.4f, k.col, h);
            N.Poly(new[] { T(4, -16.5f + pk * 6), T(6.5f, -16.5f + pk * 6), T(6.5f, -14.1f + pk * 6), T(4, -14.1f + pk * 6) }, Hx("#d02a2a"), h);
            N.Poly(new[] { T(8, -12 + pk * 6), T(11, -11 + pk * 6), T(8, -10.5f + pk * 6) }, Hx("#f0b030"), h);
            N.Poly(new[] { T(5.5f, -13 + pk * 6), T(6.6f, -13 + pk * 6), T(6.6f, -11.9f + pk * 6), T(5.5f, -11.9f + pk * 6) }, Hx("#1a1a1a"), h);
        }
    }
}
