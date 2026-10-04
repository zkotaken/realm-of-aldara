using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // The browser's combat effects, drawn the way its canvas draws them: 'effects' (rings, zaps, slashes, slams, meteors,
    // falling arrows and the FX3 kinds: fire explosions, frost, lightning, healing light, blink streaks, blizzards,
    // cataclysm), square 'particles', the monster/hero effect list MFX (arcs, chops, spins, streaks, claws, bites, impacts,
    // flashes, novas, roars, light pillars, ice spikes, dust, speed lines, scorch marks, fissures, shocks, runes, rays,
    // glyphs, auras) and the physical particles MP (rock chunks, smoke, flames, glowing motes), plus screen shake.
    // Positions are in browser px; 'h' is height above the ground in px, drawn h px higher like the canvas.
    public class AldaraVfx : MonoBehaviour
    {
        public static AldaraVfx I;
        public const float LIFT = 16, PH_Y = 24;

        // ---------- the lists ----------
        public class Fx
        {
            public string kind; public float x, y, x2, y2, x1, y1, r, life, max = 0.3f, a, dir = 1, amp; public Color color = Color.white, fill, c1, c2, core, col;
            public bool hasFill, straight, wide, big, ring, hasCol; public int n; public List<Vector2> pts;
            public List<Vector4> tg, sm, sh, fl; public List<List<Vector2>> cracks; public float d0 = -1, wob; public Fx() { }
        }
        public class Part { public float x, y, vx, vy, life, size; public Color c; }
        public class Mf
        {
            public string k, type; public float x, y, h, a, r, arc = 1.1f, dir = 1, w, T, t, len, H, a0, spin = 1, tall = 1, dl; public int n; public Color c = Color.white, c2 = Color.white;
            public bool follow, holy, dark, wet, big, trap, sp, landed;
            public List<Vector4> S; public List<List<Vector2>> L; public object Q;
        }
        public class Mp { public float x, y, z, vx, vy, vz, g, drag, life, max, s, grow, rot, vr; public Color c, c2; public bool hasC2, rock, flame, smoke, glow, ember, chunk, hasRot; public Vector2[] pts; }
        public readonly List<Fx> effects = new List<Fx>();
        public readonly List<Part> particles = new List<Part>();
        public readonly List<Mf> mfx = new List<Mf>();
        public readonly List<Mp> mp = new List<Mp>();
        /// the particle budget (MAX_PARTICLES): low 80, medium 160, high 240, ultra 420
        static int MAX_PARTICLES { get { var p = AldaraSettings.Get("particles"); string s = p != null ? (string)p : "high"; return s == "low" ? 80 : s == "medium" ? 160 : s == "ultra" ? 420 : 240; } }

        void Awake() { I = this; }
        static AldaraVfx V { get { if (!I) { var go = new GameObject("AldaraVfx"); I = go.AddComponent<AldaraVfx>(); } return I; } }

        // ---------- the browser's helpers ----------
        public static Color Hx(string h, float a = 1) { var c = AldaraRules.Hex(h); c.a = a; return c; }
        public static Color A(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }
        public static float EIO(float x) { return x < 0.5f ? 2 * x * x : 1 - Mathf.Pow(-2 * x + 2, 2) / 2; }
        public static float EOut(float x) { return 1 - Mathf.Pow(1 - x, 3); }
        public static float EIn(float x) { return x * x * x; }
        static float R() { return Random.value; }
        /// burst: square sparks flying out
        public static void Burst(float x, float y, Color col, int n, float speed)
        {
            var P = V.particles;
            for (int i = 0; i < n && P.Count < MAX_PARTICLES; i++) { float a = R() * Mathf.PI * 2, v = speed * (0.3f + R() * 0.7f); P.Add(new Part { x = x, y = y, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, life = 0.35f + R() * 0.25f, c = col, size = 2 + R() * 2 }); }
        }
        public static void Particle(float x, float y, float vx, float vy, float life, Color col, float size) { if (V.particles.Count < MAX_PARTICLES) V.particles.Add(new Part { x = x, y = y, vx = vx, vy = vy, life = life, c = col, size = size }); }
        public static Fx Effect(string kind, float x, float y, float r, float life, Color col) { var f = new Fx { kind = kind, x = x, y = y, r = r, life = life, max = life, color = col }; V.effects.Add(f); return f; }
        public static void Ring(float x, float y, float r, Color col, float life, Color? fill = null) { var f = Effect("ring", x, y, r, life, col); if (fill.HasValue) { f.hasFill = true; f.fill = fill.Value; } }
        public static void Zap(float x, float y, float x2, float y2, Color col, float life, bool straight) { var f = Effect("zap", x, y, 0, life, col); f.x2 = x2; f.y2 = y2; f.straight = straight; }
        public static void Shake(float amp, float life) { var f = new Fx { kind = "shake", amp = amp, life = life, max = life }; V.effects.Add(f); }
        public static Mf Mfx(Mf f) { f.t = -f.dl; if (V.mfx.Count < 220) V.mfx.Add(f); return f; }
        public static void Mpush(Mp p) { if (V.mp.Count < Mathf.Max(120, MAX_PARTICLES * 1.6f)) V.mp.Add(p); }
        public static void Debris(float x, float y, int n, Color col, string el = null)
        {
            for (int k = 0; k < n; k++) { float a = R() * Mathf.PI * 2, v = 60 + R() * 170; Mpush(new Mp { x = x, y = y, z = 4, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, vz = 180 + R() * 260, g = 900, life = 1.1f, max = 1.1f, c = col, s = 2.5f + R() * 3.5f, rock = true, rot = R() * 6, hasRot = true, vr = (R() - 0.5f) * 14, ember = el == "fire" }); }
        }
        public static void Sparks(float x, float y, float h, float a0, int n, Color col, float spd = 220, float spread = Mathf.PI * 2)
        {
            for (int k = 0; k < n; k++) { float a = a0 + (R() - 0.5f) * spread, v = spd * (0.5f + R() * 0.7f); Mpush(new Mp { x = x, y = y, z = h, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, vz = 60 + R() * 160, g = 650, life = 0.45f, max = 0.45f, c = col, s = 1.8f, glow = true }); }
        }
        public static void Motes(float x, float y, float r, int n, Color col, float up = 60)
        {
            for (int k = 0; k < n; k++) { float a = R() * Mathf.PI * 2, d = Mathf.Sqrt(R()) * r; Mpush(new Mp { x = x + Mathf.Cos(a) * d, y = y + Mathf.Sin(a) * d * 0.6f, z = R() * 10, vz = up * (0.5f + R()), life = 0.9f + R() * 0.5f, max = 1.4f, c = col, s = 2 + R() * 1.5f, glow = true }); }
        }
        public static void QuakeFx(float x, float y, float Rr, Color c1, Color c2, float shake, bool scorch)
        {
            Mfx(new Mf { k = "crack", x = x, y = y, a = R() * 6, len = Rr, c = c1, T = 2.4f, n = 7, r = Rr }); Mfx(new Mf { k = "shock", x = x, y = y, r = Rr * 1.15f, c = c1, c2 = c2, T = 0.45f }); Mfx(new Mf { k = "flash", x = x, y = y, h = 4, r = Rr * 0.6f, c = c1, c2 = c2, T = 0.22f });
            Debris(x, y, 10 + Mathf.RoundToInt(Rr / 10), Hx("#6a5a4a"), scorch ? "fire" : null);
            for (int k = 0; k < 5; k++) { float a = k / 5f * Mathf.PI * 2 + R(); Mfx(new Mf { k = "dust", x = x + Mathf.Cos(a) * Rr * 0.6f, y = y + Mathf.Sin(a) * Rr * 0.6f, r = Rr * 0.35f, T = 0.8f }); }
            if (scorch) Mfx(new Mf { k = "scorch", x = x, y = y, r = Rr * 0.8f, T = 4, c = c1 });
            if (shake > 0) Shake(shake, 0.3f);
        }
        public static Mf Rune(float x, float y, float r, Color c, float T = 1.2f, bool follow = false, float spin = 1, bool trap = false) { return Mfx(new Mf { k = "rune", x = x, y = y, r = r, c = c, T = T, follow = follow, spin = spin, trap = trap }); }
        /// the sum of every live shake, like the browser's camera
        public static Vector2 ShakeOffset
        {
            get { if (!I) return Vector2.zero; float s = 0; foreach (var f in I.effects) if (f.kind == "shake" && f.life <= f.max) s += f.amp * f.life / f.max; return s <= 0 ? Vector2.zero : new Vector2((R() - 0.5f) * 2 * s, (R() - 0.5f) * 2 * s); }
        }
        public void Clear() { effects.Clear(); particles.Clear(); mfx.Clear(); mp.Clear(); }

        // ---------- update (updateFx and the MFX/MP part of updEnemy) ----------
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            for (int i = effects.Count - 1; i >= 0; i--) { effects[i].life -= dt; if (effects[i].life <= 0) effects.RemoveAt(i); }
            float dec = Mathf.Pow(0.9f, dt * 60);
            for (int i = particles.Count - 1; i >= 0; i--) { var q = particles[i]; q.x += q.vx * dt; q.y += q.vy * dt; q.vx *= dec; q.vy *= dec; q.life -= dt; if (q.life <= 0) particles.RemoveAt(i); }
            for (int i = mfx.Count - 1; i >= 0; i--) { var f = mfx[i]; f.t += dt; if (f.t >= f.T) mfx.RemoveAt(i); }
            for (int i = mp.Count - 1; i >= 0; i--)
            {
                var p = mp[i]; p.life -= dt; if (p.life <= 0) { mp.RemoveAt(i); continue; }
                p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt; if (p.g != 0) p.vz -= p.g * dt; if (p.drag > 0) { float k = Mathf.Max(0, 1 - p.drag * dt); p.vx *= k; p.vy *= k; p.vz *= k; }
                if (p.hasRot) p.rot += p.vr * dt;
                if (p.z < 0) { p.z = 0; if (p.g != 0) { if (p.chunk && p.vz < -90 && mp.Count < MAX_PARTICLES * 1.5f) mp.Add(new Mp { x = p.x, y = p.y, z = 1, vz = 14, drag = 1, life = 0.45f, max = 0.45f, c = p.hasC2 ? p.c2 : Hx("#8a8278"), s = 3 + p.s * 0.5f, grow = 8, smoke = true }); p.vz = -p.vz * (p.chunk ? 0.42f : 0.3f); p.vx *= 0.55f; p.vy *= 0.55f; p.vr *= 0.5f; } }
            }
        }

        // ---------- drawing ----------
        AldaraSketch gN, gA, aN, aA;
        public AldaraSketch GroundN { get { return gN; } }
        public AldaraSketch GroundA { get { return gA; } }
        public AldaraSketch AirN { get { return aN; } }
        public AldaraSketch AirA { get { return aA; } }
        void Sketches()
        {
            if (gN != null) return;
            gN = new AldaraSketch("normal", false, 3005) { conform = true }; gA = new AldaraSketch("add", false, 3006) { conform = true };
            aN = new AldaraSketch("normal", true, 3115); aA = new AldaraSketch("add", true, 3116);
        }
        /// the ground height under a point, the canvas's LZ (0 in a dungeon)
        public static float LZ(float x, float y) { return AldaraWorld.Dun ? 0 : AldaraWorld.HeightPx(x, y); }
        /// other systems draw into the same layers before they are sent (monster telegraphs, hazards, projectiles)
        public static event System.Action<AldaraVfx> DrawGround, DrawAir;
        void LateUpdate()
        {
            var cam = Camera.main; if (!cam || !AldaraSave.Ready) return; Sketches();
            gN.Clear(); gA.Clear(); aN.Clear(); aA.Clear(); float now = Time.time;
            var P = AldaraPlayer.I;
            // ground: scorch marks, fissures, shocks, stuck arrows, runes
            foreach (var f in mfx) { if (f.t < 0) continue; if (f.follow && P) { f.x = P.x; f.y = P.y; } DrawMfxGround(f, f.t / f.T); }
            if (DrawGround != null) DrawGround(this);
            // air: the MFX kinds, the physical particles, effects, particles
            if (DrawAir != null) DrawAir(this);
            foreach (var f in mfx) { if (f.t < 0) continue; DrawMfxAir(f, f.t / f.T, now); }
            foreach (var p in mp) DrawMp(p);
            foreach (var fx in effects) { if (fx.life > fx.max || fx.kind == "shake") continue; DrawEffect(fx, Mathf.Max(0, fx.life / fx.max), now); }
            foreach (var q in particles) { float h = LZ(q.x, q.y); aN.Rect(q.x - q.size / 2, q.y - q.size / 2, q.size, q.size, A(q.c, Mathf.Clamp01(q.life * 3)), h); }
            gN.Draw(cam); gA.Draw(cam); aN.Draw(cam); aA.Draw(cam);
        }

        // ---------- canvas-like drawing (circles are circles on screen; the 0.4 squash is the ground ellipse) ----------
        public static void Glow(AldaraSketch s, float x, float y, float r, Color col, float a, float h, float ry = -1) { if (r <= 0.5f || a <= 0.01f) return; s.Glow(x, y, r, ry < 0 ? r : ry, new[] { 0f, 1f }, new[] { A(col, Mathf.Min(1, a)), A(col, 0) }, h, 20); }
        public static void Circle(AldaraSketch s, float x, float y, float r, Color col, float h) { if (r <= 0.1f) return; s.Ellipse(x, y, r, r, col, h, 0, 16); }
        public static void Ring(AldaraSketch s, float x, float y, float rx, float ry, float w, Color col, float h, float rot = 0) { if (rx <= 0.1f) return; s.Arc(x, y, rx, ry, 0, Mathf.PI * 2, w, col, h, rot); }
        static Vector2 V2(float x, float y) { return new Vector2(x, y); }

        void DrawEffect(Fx fx, float k, float now)
        {
            var P = AldaraPlayer.I; float ex = fx.x, ey = fx.y; float h = LZ(ex, ey);
            switch (fx.kind)
            {
                case "ring":
                    {
                        float rr = fx.r * (1.1f - k * 0.4f);
                        if (fx.hasFill) Circle(aN, ex, ey, rr, A(fx.fill, k * 0.35f), h);
                        Ring(aN, ex, ey, rr, rr, 3, A(fx.color, k), h); break;
                    }
                case "zap":
                    {
                        var c = A(fx.color, k); float w = fx.straight ? 3 : 2;
                        if (fx.straight) aN.Line(ex, ey, fx.x2, fx.y2, w, c, h);
                        else { var pts = new List<Vector2> { V2(ex, ey) }; for (int j = 1; j < 6; j++) { float u = j / 6f; pts.Add(V2(ex + (fx.x2 - ex) * u + (R() - 0.5f) * 18, ey + (fx.y2 - ey) * u + (R() - 0.5f) * 18)); } pts.Add(V2(fx.x2, fx.y2)); aN.Polyline(pts, w, c, false, h); }
                        break;
                    }
                case "meteor":
                    {
                        float mx = ex + k * 90, my = ey - k * 300; Ring(aN, ex, ey, 40 * (1 - k) + 10, 40 * (1 - k) + 10, 2, Hx("#ff6a2a", 0.33f), h);
                        Glow(aA, mx, my, 30, Hx("#ff6a2a"), 0.6f, h); Circle(aN, mx, my, 14, Hx("#ff8a3a"), h); Circle(aN, mx, my, 6, Hx("#ffe07a"), h); break;
                    }
                case "arrowfall": { float ay = ey - k * 160; aN.Line(ex, ay - 18, ex, ay, 2, fx.hasCol ? fx.color : Hx("#d8c8a0"), h); break; }
                case "slash":
                    {
                        float span = fx.wide ? 2.7f : 1.9f, prog = Mathf.Min(1, (1 - k) * 2.8f), dir = fx.dir;
                        float cx = ex - Mathf.Cos(fx.a) * fx.r * 0.55f, cy = ey - Mathf.Sin(fx.a) * fx.r * 0.55f, a0 = fx.a - dir * span / 2, a1 = a0 + dir * span * prog;
                        System.Action<float, float, Color, float> cres = (Rr, th, col, al) =>
                        {
                            var outer = new List<Vector2>(); var inner = new List<Vector2>(); var cols = new List<Color>();
                            for (int i = 0; i <= 16; i++) { float u = i / 16f, a = a0 + (a1 - a0) * u, w = th * Mathf.Pow(u, 0.8f) * (1.15f - u * 0.35f); outer.Add(V2(cx + Mathf.Cos(a) * (Rr + w / 2), cy + Mathf.Sin(a) * (Rr + w / 2) * 0.8f)); inner.Add(V2(cx + Mathf.Cos(a) * (Rr - w / 2), cy + Mathf.Sin(a) * (Rr - w / 2) * 0.8f)); cols.Add(A(col, al)); }
                            aA.Strip(outer, inner, cols, h);
                        };
                        cres(fx.r, fx.wide ? 22 : 15, fx.color, k * 0.55f); cres(fx.r, fx.wide ? 9 : 6, Color.white, k * 0.95f); cres(fx.r * 1.18f, fx.wide ? 6 : 4, fx.color, k * 0.35f); break;
                    }
                case "slam":
                    {
                        float rr = fx.r * (1.15f - k * 0.75f);
                        aA.Ellipse(ex, ey, rr * 0.7f, rr * 0.28f, A(fx.color, k * 0.5f), h); Ring(aA, ex, ey, rr, rr * 0.4f, 3, A(fx.color, k), h); Ring(aA, ex, ey, rr * 0.8f, rr * 0.32f, 1.5f, A(Color.white, k), h);
                        if (fx.cracks == null) { fx.cracks = new List<List<Vector2>>(); for (int j = 0; j < 7; j++) { float a = j / 7f * Mathf.PI * 2 + R() * 0.5f, r = 0; var pts = new List<Vector2> { Vector2.zero }; for (int q = 0; q < 3; q++) { r += fx.r * (0.18f + R() * 0.14f); pts.Add(V2(Mathf.Cos(a + (R() - 0.5f) * 0.5f) * r, Mathf.Sin(a + (R() - 0.5f) * 0.5f) * r * 0.4f)); } fx.cracks.Add(pts); } }
                        foreach (var pts in fx.cracks) { var w = new List<Vector2>(); foreach (var p in pts) w.Add(V2(ex + p.x, ey + p.y)); aN.Polyline(w, 2.2f, A(Hx("#1a0e06"), Mathf.Min(1, k * 1.6f)), false, h); aN.Polyline(w, 1, A(fx.color, k * 0.9f), false, h); }
                        break;
                    }
                case "explode": Explode(fx, k, h); break;
                case "frost": Frost(fx, k, h); break;
                case "lightning": Lightning(fx, k, h); break;
                case "heal": Heal(fx, k); break;
                case "meteor3": Meteor3(fx, k, h); break;
                case "blink": Blink(fx, k, h); break;
                case "blizzard": Blizzard(fx, k, h, now); break;
                case "cataCharge": CataCharge(fx, k, h, now); break;
                case "cataBoom": CataBoom(fx, k, h); break;
                case "pulse": { float u = 1 - k, r = fx.r > 0 ? fx.r : 30; if (!P) break; float hh = LZ(P.x, P.y); Ring(aA, P.x, P.y - 8, r * (0.6f + u), r * (0.6f + u) * 1.3f, 3, A(fx.col, k), hh); Glow(aA, P.x, P.y - 8, r * (1 + u), fx.col, 0.4f * k, hh); break; }
            }
        }
        void Explode(Fx fx, float k, float h)
        {
            float u = 1 - k, r = fx.r, x = fx.x, y = fx.y; Color c1 = fx.c1.a > 0 ? fx.c1 : Hx("#ff8a2a"), c2 = fx.c2.a > 0 ? fx.c2 : Hx("#ff3a0a"), core = fx.core.a > 0 ? fx.core : Hx("#fff4c0");
            if (fx.tg == null) { fx.tg = new List<Vector4>(); int n = fx.big ? 14 : 10; for (int i = 0; i < n; i++) fx.tg.Add(new Vector4(i / (float)n * Mathf.PI * 2 + R() * 0.4f, 0.6f + R() * 0.6f)); fx.sm = new List<Vector4>(); for (int i = 0; i < 6; i++) fx.sm.Add(new Vector4(R() * 6.28f, R() * 0.5f, 0.3f + R() * 0.3f)); }
            foreach (var s in fx.sm) Circle(aN, x + Mathf.Cos(s.x) * r * s.y * (0.5f + u), y - LIFT * 0.5f + Mathf.Sin(s.x) * r * s.y * 0.4f - u * r * 0.5f, r * s.z * (0.5f + u * 0.7f), A(Hx("#1a1410"), k * 0.35f), h);
            float rr = r * (0.25f + u * 1.05f); Ring(aA, x, y, rr, rr * 0.4f, 2 + 5 * k, A(c1, k), h); Glow(aA, x, y, r * 1.1f, c2, 0.35f * k, h, r * 1.1f * 0.4f);
            float fr = r * (0.35f + Mathf.Pow(u, 0.5f) * 0.55f);
            foreach (var t in fx.tg) { float L = fr * (1 + t.y * (0.4f + u)); aA.Tri(V2(x + Mathf.Cos(t.x - 0.25f) * fr * 0.5f, y - LIFT + Mathf.Sin(t.x - 0.25f) * fr * 0.5f), V2(x + Mathf.Cos(t.x) * L, y - LIFT + Mathf.Sin(t.x) * L * 0.8f), V2(x + Mathf.Cos(t.x + 0.25f) * fr * 0.5f, y - LIFT + Mathf.Sin(t.x + 0.25f) * fr * 0.5f), A(t.y > 1 ? c1 : c2, 0.75f * k), h); }
            aA.Glow(x, y - LIFT, fr, fr, new[] { 0, 0.35f, 0.75f, 1 }, new[] { A(core, k), A(c1, 0.9f * k), A(c2, 0.6f * k), A(c2, 0) }, h, 20);
            if (u < 0.25f) Glow(aA, x, y - LIFT, r * 0.8f * (1 - u * 4) + 10, Color.white, 0.9f * (1 - u * 4), h);
        }
        void Frost(Fx fx, float k, float h)
        {
            float u = 1 - k, x = fx.x, y = fx.y, r = fx.r;
            if (fx.sh == null) { fx.sh = new List<Vector4>(); int n = fx.n > 0 ? fx.n : 9; for (int i = 0; i < n; i++) fx.sh.Add(new Vector4(i / (float)n * Mathf.PI * 2 + R() * 0.5f, fx.ring ? 1 : 0.2f + R() * 0.6f, 0.5f + R() * 0.7f, 0.1f + R() * 0.08f)); }
            float grow = Mathf.Min(1, u * 6), fade = Mathf.Min(1, k * 2.2f), Rr = fx.ring ? r * (0.3f + 0.7f * Mathf.Min(1, u * 3)) : r;
            Glow(aA, x, y, Rr * 1.15f, Hx("#8fdfff"), 0.35f * fade, h, Rr * 1.15f * 0.42f); Ring(aA, x, y, Rr, Rr * 0.42f, 2.5f, Hx("#dff6ff", 0.8f * fade), h);
            var list = new List<Vector4>(fx.sh); list.Sort((a, b) => Mathf.Sin(a.x).CompareTo(Mathf.Sin(b.x)));
            foreach (var s in list)
            {
                float bx = x + Mathf.Cos(s.x) * Rr * s.y, by = y + Mathf.Sin(s.x) * Rr * s.y * 0.42f, H = r * 0.55f * s.z * grow, W = r * s.w, tx = bx + Mathf.Cos(s.x) * H * 0.35f, ty = by - H;
                aN.Tri(V2(bx - W, by), V2(tx, ty), V2(bx + W * 0.2f, by + W * 0.3f), Hx("#bfe9ff", fade), h); aN.Tri(V2(bx + W * 0.2f, by + W * 0.3f), V2(tx, ty), V2(bx + W, by), A(Color.white, fade), h);
                aN.Line(bx - W, by, tx, ty, 1, Hx("#5ab0e0", fade), h); aN.Line(tx, ty, bx + W, by, 1, Hx("#5ab0e0", fade), h);
            }
            foreach (var s in list) { float bx = x + Mathf.Cos(s.x) * Rr * s.y, by = y + Mathf.Sin(s.x) * Rr * s.y * 0.42f; Glow(aA, bx, by - r * 0.25f * s.z * grow, r * 0.3f, Hx("#8fdfff"), 0.3f * fade, h); }
        }
        static List<Vector2> Jag(float x1, float y1, float x2, float y2, int n, float amp)
        {
            var pts = new List<Vector2> { V2(x1, y1) }; float dx = x2 - x1, dy = y2 - y1, l = Mathf.Max(1e-4f, Mathf.Sqrt(dx * dx + dy * dy)), px = -dy / l, py = dx / l;
            for (int i = 1; i < n; i++) { float u = i / (float)n, o = (R() - 0.5f) * amp * Mathf.Sin(u * Mathf.PI) * 2; pts.Add(V2(x1 + dx * u + px * o, y1 + dy * u + py * o)); }
            pts.Add(V2(x2, y2)); return pts;
        }
        void Lightning(Fx fx, float k, float h)
        {
            var Pp = fx.pts; if (Pp == null || Pp.Count < 2) return; float fl = 0.6f + R() * 0.4f; var col = fx.hasCol ? fx.col : Hx("#8ab4ff");
            for (int i = 0; i < Pp.Count - 1; i++)
            {
                var a = Pp[i]; var b = Pp[i + 1]; float L = Vector2.Distance(a, b); var pts = Jag(a.x, a.y - LIFT, b.x, b.y - LIFT, Mathf.Max(4, Mathf.RoundToInt(L / 18)), Mathf.Min(26, L * 0.12f));
                aA.Polyline(pts, 12, A(col, 0.28f * k * fl), false, h); aA.Polyline(pts, 4, A(col, 0.8f * k), false, h); aA.Polyline(pts, 1.6f, A(Color.white, k), false, h);
                for (int f = 0; f < 2; f++) { if (pts.Count < 3) break; var q = pts[1 + Random.Range(0, pts.Count - 2)]; float an = R() * 6.28f; aA.Polyline(Jag(q.x, q.y, q.x + Mathf.Cos(an) * L * 0.25f, q.y + Mathf.Sin(an) * L * 0.25f, 4, 10), 1.5f, A(col, 0.6f * k), false, h); }
                Glow(aA, b.x, b.y - LIFT, 34, col, 0.7f * k, h); Glow(aA, b.x, b.y - LIFT, 12, Color.white, 0.9f * k, h);
            }
            Glow(aA, Pp[0].x, Pp[0].y - LIFT, 26, Hx("#cfe0ff"), 0.8f * k, h);
        }
        void Heal(Fx fx, float k)
        {
            var P = AldaraPlayer.I; if (!P) return; float u = 1 - k, x = P.x, y = P.y + 15 * 1.2f, h = LZ(P.x, P.y); var col = fx.hasCol ? fx.col : Hx("#7fe07f");
            float w = 46 * (0.6f + 0.4f * Mathf.Sin(u * Mathf.PI));
            // a pillar of light: transparent at the top, white at the foot
            var pts = new List<Vector2>(); var cs = new List<Color>(); for (int i = 0; i <= 24; i++) { float a = i / 24f * Mathf.PI * 2, px = x + Mathf.Cos(a) * w, py = y - 75 + Mathf.Sin(a) * 75; pts.Add(V2(px, py)); float g = (py - (y - 150)) / 150; cs.Add(g < 0.6f ? A(col, 0.3f * k * g / 0.6f) : Color.Lerp(A(col, 0.3f * k), A(Color.white, 0.45f * k), (g - 0.6f) / 0.4f)); }
            aA.PolyC(pts, cs, h);
            Glow(aA, x, y, 70, col, 0.45f * k, h, 70 * 0.4f); Ring(aA, x, y, 40 + u * 25, (40 + u * 25) * 0.4f, 2.5f, A(col, k), h);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2 + u * 6, ph = (i * 0.37f + u * 1.4f) % 1, hh = ph * 130, rr = 26 - hh * 0.1f, mx = x + Mathf.Cos(a) * rr, my = y - hh + Mathf.Sin(a) * rr * 0.35f, al = k * Mathf.Sin(ph * Mathf.PI);
                aA.Poly(new[] { V2(mx, my - 4), V2(mx + 2.2f, my), V2(mx, my + 4), V2(mx - 2.2f, my) }, A(i % 3 != 0 ? col : Color.white, al), h);
            }
        }
        void Meteor3(Fx fx, float k, float h)
        {
            float u = 1 - k, x = fx.x, y = fx.y, e = u * u, sx = x + 180, sy = y - 520, mx = sx + (x - sx) * e, my = sy + (y - LIFT - sy) * e;
            var c = Hx("#ff5a1a", 0.4f + 0.5f * u); Ring(aA, x, y, fx.r, fx.r * 0.4f, 3, c, h); Ring(aA, x, y, fx.r * 0.75f, fx.r * 0.75f * 0.4f, 1.5f, c, h);
            for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2 + u * 3; aA.Line(x + Mathf.Cos(a) * fx.r * 0.75f, y + Mathf.Sin(a) * fx.r * 0.75f * 0.4f, x + Mathf.Cos(a + 0.4f) * fx.r, y + Mathf.Sin(a + 0.4f) * fx.r * 0.4f, 1.5f, c, h); }
            Glow(aA, x, y, fx.r, Hx("#ff3a0a"), 0.25f * u, h, fx.r * 0.4f);
            aN.Ellipse(x, y, 20 + u * 40, (20 + u * 40) * 0.4f, new Color(0, 0, 0, 0.15f + 0.35f * u), h);
            float ang = Mathf.Atan2(y - sy, x - sx), tl = 200 + u * 80;
            aA.LineC(mx, my, mx - Mathf.Cos(ang) * tl, my - Mathf.Sin(ang) * tl, 26, new Color(1, 220 / 255f, 140 / 255f, 0.9f), new Color(1, 60 / 255f, 10 / 255f, 0), h);
            aA.LineC(mx, my, mx - Mathf.Cos(ang) * tl * 0.6f, my - Mathf.Sin(ang) * tl * 0.6f, 10, new Color(1, 220 / 255f, 140 / 255f, 0.9f), new Color(1, 110 / 255f, 30 / 255f, 0.3f), h);
            Glow(aA, mx, my, 48, Hx("#ff7a2a"), 0.8f, h); Circle(aN, mx, my, 13, Hx("#3a1a10"), h); aN.Arc(mx, my, 13, 13, ang + Mathf.PI * 0.5f, ang + Mathf.PI * 1.5f, 3, Hx("#ffd35a"), h);
            if (R() < 0.8f) Particle(mx + (R() - 0.5f) * 14, my + (R() - 0.5f) * 14, -Mathf.Cos(ang) * 60 + (R() - 0.5f) * 40, -Mathf.Sin(ang) * 60, 0.4f, R() < 0.5f ? Hx("#ffd35a") : Hx("#ff7a2a"), 3);
        }
        void Blink(Fx fx, float k, float h)
        {
            var a = V2(fx.x1, fx.y1 - LIFT); var b = V2(fx.x2, fx.y2 - LIFT);
            foreach (var L in new[] { new Vector4(26, 0.18f, 0), new Vector4(10, 0.5f, 1), new Vector4(3, 0.9f, 2) }) aA.Line(a.x, a.y, b.x, b.y, L.x * k + 1, A(L.z == 0 ? Hx("#8a4aff") : L.z == 1 ? Hx("#c07aff") : Color.white, L.y * k), h);
            for (int i = 0; i < 2; i++) { float px = i == 0 ? fx.x1 : fx.x2, py = i == 0 ? fx.y1 : fx.y2, s = i == 0 ? 1 - k : k; Glow(aA, px, py - 22, 40, Hx("#c07aff"), 0.6f * k, h); Ring(aA, px, py + 15, 20 + s * 40, (20 + s * 40) * 0.4f, 2.5f, Hx("#e0c0ff", k), h); }
            aA.Ellipse(fx.x1, fx.y1 - 20, 11, 26, Hx("#b07aff", 0.45f * k), h);
        }
        void Blizzard(Fx fx, float k, float h, float t)
        {
            float x = fx.x, y = fx.y, r = fx.r, fade = Mathf.Min(1, Mathf.Min(k * 4, (1 - k) * 6));
            if (fx.fl == null) { fx.fl = new List<Vector4>(); for (int i = 0; i < 60; i++) fx.fl.Add(new Vector4(R() * 6.28f, R(), R(), 0.6f + R() * 0.8f)); }
            Glow(aA, x, y, r * 1.1f, Hx("#8fdfff"), 0.3f * fade, h, r * 1.1f * 0.42f); Ring(aA, x, y, r, r * 0.42f, 2, Hx("#dff6ff", 0.6f * fade), h);
            for (int i = 0; i < 7; i++) { float a = i / 7f * 6.28f + t * 0.4f; aN.Ellipse(x + Mathf.Cos(a) * r * 0.5f, y - 170 + Mathf.Sin(a) * 14, r * 0.45f, 26, Hx(i % 2 == 1 ? "#5a6a80" : "#7a8aa0", 0.28f * fade), h); }
            foreach (var f in fx.fl) { float a = f.x + t * 1.6f * f.w, d = r * f.y, hh = (f.z + t * 0.6f * f.w) % 1; float px = x + Mathf.Cos(a) * d, py = y + Mathf.Sin(a) * d * 0.42f - hh * 160; aA.Rect(px - 1.2f, py - 1.2f, 2.4f, 2.4f, A(Color.white, fade * Mathf.Sin(hh * Mathf.PI)), h); }
            for (int i = 0; i < 6; i++) { float a = R() * 6.28f, d = R() * r, px = x + Mathf.Cos(a) * d, py = y + Mathf.Sin(a) * d * 0.42f, hh = R() * 140 + 20; aA.Line(px + 10, py - hh - 26, px, py - hh, 2, Hx("#dff6ff", 0.8f * fade), h); }
        }
        void CataCharge(Fx fx, float k, float h, float t)
        {
            float u = 1 - k, x = fx.x, y = fx.y, r = fx.r;
            Glow(aA, x, y, r * 1.1f, Hx("#8a3aff"), 0.4f * u, h, r * 1.1f * 0.42f);
            float R0 = r * (0.4f + 0.6f * u); Ring(aA, x, y, R0, R0 * 0.42f, 3, Hx("#c07aff", 0.5f + 0.5f * u), h);
            var star = new List<Vector2>(); for (int i = 0; i <= 5; i++) { float an = t * 2 + i * Mathf.PI * 4 / 5; star.Add(V2(x + Mathf.Cos(an) * R0 * 0.92f, y + Mathf.Sin(an) * R0 * 0.92f * 0.42f)); } aA.Polyline(star, 1.5f, Hx("#c07aff", 0.5f + 0.5f * u), false, h);
            Ring(aA, x, y, r * 0.35f, r * 0.35f * 0.42f, 1.5f, Hx("#c07aff", 0.5f + 0.5f * u), h);
            for (int i = 0; i < 24; i++) { float a = i / 24f * Mathf.PI * 2 + t * 3, d = r * (1.2f - ((u * 1.6f + i * 0.13f) % 1)); aA.Rect(x + Mathf.Cos(a) * d - 1.5f, y + Mathf.Sin(a) * d * 0.42f - LIFT - 1.5f, 3, 3, Hx(i % 2 == 1 ? "#c07aff" : "#ffffff", 0.8f), h); }
            for (int i = 0; i < 4; i++) { float a = i / 4f * Mathf.PI * 2 + t, px = x + Mathf.Cos(a) * r * 0.6f, py = y + Mathf.Sin(a) * r * 0.25f; aA.Rect(px - 3, py - 120 * u, 6, 120 * u, Hx("#c07aff", 0.35f * u), h); }
            Glow(aA, x, y - LIFT, 30 + 50 * u, Hx("#e0c0ff"), 0.6f * u, h);
        }
        void CataBoom(Fx fx, float k, float h)
        {
            float u = 1 - k, x = fx.x, y = fx.y, r = fx.r, w = r * 0.5f * (1 - u * 0.6f);
            aA.PolyC(new[] { V2(x - w, y), V2(x - w * 0.4f, y - 700), V2(x + w * 0.4f, y - 700), V2(x + w, y) }, new[] { A(Color.white, 0.9f * k), Hx("#8a3aff", 0), Hx("#8a3aff", 0), A(Color.white, 0.9f * k) }, h);
            foreach (var sa in new[] { new Vector2(1, 1), new Vector2(0.7f, 0.6f) }) { float rr = r * (0.2f + u * 1.2f) * sa.x; Ring(aA, x, y, rr, rr * 0.42f, 5 * k + 1, Hx("#e0c0ff", sa.y * k), h); }
            Glow(aA, x, y, r * 1.3f, Hx("#8a3aff"), 0.4f * k, h, r * 1.3f * 0.42f);
        }

        // ---------- MFX: ground kinds ----------
        void DrawMfxGround(Mf f, float u)
        {
            float a = 1 - u;
            switch (f.k)
            {
                case "scorch":
                    {
                        float al = u < 0.1f ? u / 0.1f : 1 - Mathf.Max(0, (u - 0.6f) / 0.4f);
                        if (f.wet) gN.Glow(f.x, f.y, f.r, f.r, new[] { 0, 0.7f, 1 }, new[] { A(f.c, 0.45f * al), A(f.c, 0.2f * al), new Color(0, 0, 0, 0) }, 1, 24);
                        else gN.Glow(f.x, f.y, f.r, f.r, new[] { 0, 0.7f, 1 }, new[] { new Color(12 / 255f, 8 / 255f, 6 / 255f, 0.55f * al), new Color(20 / 255f, 12 / 255f, 8 / 255f, 0.3f * al), new Color(0, 0, 0, 0) }, 1, 24);
                        if (!f.wet && u < 0.5f) Glow(gA, f.x, f.y, f.r * 0.7f, f.c, 0.35f * (1 - u * 2), 1);
                        break;
                    }
                case "crack": Crack(f, u); break;
                case "shock":
                    {
                        float e = EOut(u), r = f.r * (0.25f + 0.85f * e);
                        Ring(gA, f.x, f.y, r, r, 10 * (1 - u) + 2, A(f.c, 0.55f * (1 - u)), 1); Ring(gA, f.x, f.y, r, r, 2, A(f.c2, 0.8f * (1 - u)), 1); break;
                    }
                case "stuck":
                    {
                        float al = u > 0.7f ? 1 - (u - 0.7f) / 0.3f : 1, h = LZ(f.x, f.y); float tx = f.x + Mathf.Sin(f.a) * 6, ty = f.y - 16;
                        aN.Line(f.x, f.y, tx, ty, 2, Hx("#6a5030", al), h); aN.Tri(V2(tx, ty), V2(tx - 3, ty - 5), V2(tx + 3, ty - 5), Hx("#e8e0d0", al), h); break;
                    }
                case "rune":
                    {
                        float al = u < 0.15f ? u / 0.15f : u > 0.7f ? 1 - (u - 0.7f) / 0.3f : 1, t = f.t * f.spin, Rr = f.r * (0.85f + 0.15f * EOut(Mathf.Min(1, u * 4)));
                        float blink = f.trap ? (0.55f + 0.45f * Mathf.Sin(f.t * (10 + f.t * 30))) : 1; var c = A(f.c, 0.8f * al * blink);
                        // canvas draws the rune flat (a circle on screen), not squashed
                        Ring(gA, f.x, f.y, Rr, Rr, 2, c, 1); Ring(gA, f.x, f.y, Rr * 0.82f, Rr * 0.82f, 1.2f, c, 1);
                        for (int q = 0; q < 12; q++) { float aa = q / 12f * Mathf.PI * 2 + t; gA.Line(f.x + Mathf.Cos(aa) * Rr * 0.84f, f.y + Mathf.Sin(aa) * Rr * 0.84f, f.x + Mathf.Cos(aa) * Rr * 0.97f, f.y + Mathf.Sin(aa) * Rr * 0.97f, 1.2f, c, 1); }
                        for (int s = 0; s < 2; s++) { var tri = new List<Vector2>(); for (int q = 0; q <= 3; q++) { float aa = q / 3f * Mathf.PI * 2 + (s == 0 ? Mathf.PI / 2 : -Mathf.PI / 2) + t; tri.Add(V2(f.x + Mathf.Cos(aa) * Rr * 0.8f, f.y + Mathf.Sin(aa) * Rr * 0.8f)); } gA.Polyline(tri, 1.2f, c, false, 1); }
                        Ring(gA, f.x, f.y, Rr * 0.4f, Rr * 0.4f, 1.2f, c, 1); Glow(gA, f.x, f.y, Rr * 1.1f, f.c, 0.22f * al * blink, 1);
                        break;
                    }
            }
        }
        class CrackQ { public float R; public List<List<Vector3>> arms = new List<List<Vector3>>(); public List<Vector2[]> slabs = new List<Vector2[]>(); public List<Vector2> sh = new List<Vector2>(); public List<Vector4> rub = new List<Vector4>(); public Color soil; }
        static Color Soil(float x, float y)
        {
            if (AldaraWorld.Dun && AldaraDungeon.DM != null) return AldaraRules.Hex(AldaraDungeon.DM.mm ?? "#5a4a3a");
            switch (AldaraWorld.ZoneAt(x, y)) { case 1: return Hx("#8a7a5a"); case 2: return Hx("#3a4a32"); case 3: return Hx("#c8d0d8"); case 4: return Hx("#4a3028"); case 5: return Hx("#3a2a4a"); case 6: return Hx("#8ab07a"); default: return Hx("#5a7a3a"); }
        }
        static Color S(Color soil, float k, float a) { return new Color(Mathf.Clamp01(soil.r * k), Mathf.Clamp01(soil.g * k), Mathf.Clamp01(soil.b * k), Mathf.Clamp01(a)); }
        /// PFXG.crack: a fissure network with heaved slabs and rim rubble, and real debris thrown out
        void Crack(Mf f, float u)
        {
            var Q = f.Q as CrackQ;
            if (Q == null)
            {
                float Rr = Mathf.Max(24, f.r > 0 ? f.r : f.len > 0 ? f.len : 40); bool dir = f.n <= 3; Q = new CrackQ { R = Rr, soil = Soil(f.x, f.y) }; f.Q = Q;
                System.Action<float, float, float, int> arm = null;
                arm = (a0, len, w0, depth) =>
                {
                    var pts = new List<Vector3>(); int seg = 6 + Mathf.FloorToInt(len / 18); float x = 0, y = 0, a = a0, w = w0;
                    for (int j = 0; j <= seg; j++) { pts.Add(new Vector3(x, y, w)); float d = len / seg * (0.75f + R() * 0.5f); a += (R() - 0.5f) * 0.55f; x += Mathf.Cos(a) * d; y += Mathf.Sin(a) * d * 0.62f; w = w0 * (1 - j / (float)seg) * (0.7f + R() * 0.5f) + 0.4f; }
                    Q.arms.Add(pts);
                    if (depth < 2) for (int b = 0; b < (R() < 0.7f ? 1 : 2); b++)
                        {
                            int j = 1 + Random.Range(0, Mathf.Max(1, pts.Count - 3)); var p = pts[j]; var br = new List<Vector3>(); float bx = p.x, by = p.y, ba = a0 + (R() < 0.5f ? -1 : 1) * (0.6f + R() * 0.6f), bw = p.z * 0.7f; int bs = 3 + Mathf.FloorToInt(len / 40);
                            for (int q = 0; q <= bs; q++) { br.Add(new Vector3(bx, by, bw)); float d = len * 0.45f / bs * (0.7f + R() * 0.6f); ba += (R() - 0.5f) * 0.5f; bx += Mathf.Cos(ba) * d; by += Mathf.Sin(ba) * d * 0.62f; bw = bw * 0.72f + 0.3f; }
                            Q.arms.Add(br);
                        }
                };
                int N = dir ? 3 : 5 + Mathf.FloorToInt(Rr / 40);
                for (int q = 0; q < N; q++) { float a = dir ? (f.a + (q - 1) * 0.42f + (R() - 0.5f) * 0.3f) : (f.a + q / (float)N * Mathf.PI * 2 + (R() - 0.5f) * 0.6f); arm(a, Rr * (dir ? 0.9f : (0.7f + R() * 0.4f)), Mathf.Min(9, 2.5f + Rr / 16), 0); }
                int NS = dir ? 2 : N;
                for (int q = 0; q < NS; q++)
                {
                    float a = dir ? (f.a + (q != 0 ? 0.55f : -0.55f)) : (f.a + (q + 0.5f) / N * Mathf.PI * 2), r0 = Rr * 0.12f, r1 = Rr * (0.3f + R() * 0.22f), sp = (dir ? 0.5f : Mathf.PI / N) * 0.8f;
                    Q.slabs.Add(new[] { V2(Mathf.Cos(a - sp) * r0, Mathf.Sin(a - sp) * r0 * 0.62f), V2(Mathf.Cos(a - sp * 0.9f) * r1, Mathf.Sin(a - sp * 0.9f) * r1 * 0.62f), V2(Mathf.Cos(a) * r1 * 1.08f, Mathf.Sin(a) * r1 * 0.67f), V2(Mathf.Cos(a + sp * 0.9f) * r1, Mathf.Sin(a + sp * 0.9f) * r1 * 0.62f), V2(Mathf.Cos(a + sp) * r0, Mathf.Sin(a + sp) * r0 * 0.62f) });
                    Q.sh.Add(V2(Rr * (0.09f + R() * 0.07f), R() * 0.15f));
                }
                for (int q = 0; q < Mathf.Min(30, 8 + Rr / 4); q++) { float a = R() * Mathf.PI * 2, d = Rr * (0.25f + R() * 0.85f); Q.rub.Add(new Vector4(Mathf.Cos(a) * d, Mathf.Sin(a) * d * 0.62f, 1 + R() * 2.2f, R() < 0.5f ? 0.5f : 0.75f)); }
                bool hot = f.c != Color.white && f.c.r > f.c.b + 40 / 255f;
                QkDebris(f.x, f.y, Rr, f.c, hot, f.n > 3 && Rr > 50, Q.soil);
            }
            float grow = EOut(Mathf.Min(1, f.t / 0.16f)), fade = u < 0.55f ? 1 : 1 - (u - 0.55f) / 0.45f; var Sl = Q.soil; float X = f.x, Y = f.y;
            foreach (var r in Q.rub) gN.Rect(X + r.x - r.z / 2, Y + r.y - r.z / 2, r.z, r.z * 0.7f, S(Sl, 0.72f, 0.28f * fade * r.w), 1);
            for (int i = 0; i < Q.slabs.Count; i++)
            {
                var P = Q.slabs[i]; float kk = Mathf.Max(0, f.t - Q.sh[i].y), lift = Mathf.Sin(Mathf.Min(1, kk / 0.42f) * Mathf.PI) * Q.sh[i].x * grow; if (lift < 0.3f && kk > 0.5f) continue;
                var bp = new List<Vector2>(); var tp = new List<Vector2>(); foreach (var p in P) { bp.Add(V2(X + p.x, Y + p.y)); tp.Add(V2(X + p.x, Y + p.y - lift)); }
                gN.Poly(bp, S(Sl, 0.45f, 0.9f * fade), 1);
                for (int j = 0; j < P.Length; j++) { var a = P[j]; var b = P[(j + 1) % P.Length]; if (b.y + a.y < 0) continue; gN.Poly(new[] { V2(X + a.x, Y + a.y), V2(X + b.x, Y + b.y), V2(X + b.x, Y + b.y - lift), V2(X + a.x, Y + a.y - lift) }, S(Sl, 0.38f, 0.95f * fade), 1); }
                gN.Poly(tp, S(Sl, 1.22f, 0.95f * fade), 1); gN.Polyline(tp, 1, S(Sl, 1.45f, 0.7f * fade), true, 1);
            }
            System.Action<List<Vector3>, float, float, Color, AldaraSketch> rib = (pts, wk, off, col, sk) =>
            {
                int n = Mathf.Max(2, Mathf.RoundToInt(pts.Count * grow)); if (n < 2) return; var A1 = new List<Vector2>(); var B1 = new List<Vector2>(); var cs = new List<Color>();
                for (int i = 0; i < n; i++) { var p = pts[i]; var q = pts[Mathf.Min(n - 1, i + 1)]; if (i == n - 1 && n > 1) q = pts[i] + (pts[i] - pts[i - 1]); float w = p.z * wk, dx = q.x - p.x, dy = q.y - p.y, l = Mathf.Max(1e-4f, Mathf.Sqrt(dx * dx + dy * dy)), nx = -dy / l, ny = dx / l; A1.Add(V2(X + p.x + nx * w + off, Y + p.y + ny * w + off)); B1.Add(V2(X + p.x - nx * w + off, Y + p.y - ny * w + off)); cs.Add(col); }
                sk.Strip(A1, B1, cs, 1);
            };
            gN.Glow(X, Y, Q.R * 0.42f, Q.R * 0.27f, new[] { 0f, 1f }, new[] { S(Sl, 0.55f, 0.55f * fade), S(Sl, 0.55f, 0) }, 1, 20);
            foreach (var Am in Q.arms) rib(Am, 1.05f, 1.3f, S(Sl, 0.2f, 0.5f * fade), gN);
            foreach (var Am in Q.arms) rib(Am, 1.0f, -1.5f, S(Sl, 1.55f, 0.7f * fade), gN);
            foreach (var Am in Q.arms) rib(Am, 1.0f, 0, S(Sl, 0.3f, 0.95f * fade), gN);
            foreach (var Am in Q.arms) rib(Am, 0.55f, 0.8f, new Color(4 / 255f, 2 / 255f, 1 / 255f, 0.92f * fade), gN);
            if (f.c != Color.white || true) { float gl = Mathf.Max(0, 1 - u / 0.75f) * (0.7f + 0.3f * Mathf.Sin(f.t * 18)); if (gl > 0.02f) { foreach (var Am in Q.arms) rib(Am, 0.55f, 0.3f, A(f.c, 0.85f * gl), gA); foreach (var Am in Q.arms) rib(Am, 0.2f, 0.3f, A(Color.white, 0.6f * gl), gA); Glow(gA, X, Y, Q.R * 0.55f, f.c, 0.35f * gl, 1); } }
            if (u < 0.5f) { float e = EOut(u / 0.5f), rr = Q.R * (0.3f + 0.9f * e); Ring(gN, X, Y, rr, rr * 0.62f, Q.R * 0.22f * (0.4f + e), S(Sl, 1.2f, (1 - e) * 0.35f), 1); }
        }
        static Vector2[] ChunkPts(float s) { int n = 5 + Random.Range(0, 3); var P = new Vector2[n]; for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2, r = s * (0.6f + R() * 0.5f); P[i] = V2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * (0.7f + R() * 0.3f)); } return P; }
        static void QkDebris(float x, float y, float r, Color col, bool hot, bool big, Color soil)
        {
            Color dark = S(soil, 0.55f, 1), mid = S(soil, 0.8f, 1), lit = S(soil, 1.15f, 1); Color[] stone = { Hx("#6a6258"), Hx("#7a7268"), Hx("#5a5450") };
            float load = Mathf.Max(0.35f, 1 - V.mp.Count / Mathf.Max(120f, MAX_PARTICLES * 1.6f)); int n = Mathf.RoundToInt(Mathf.Min(26, (big ? 10 : 6) + r / 9) * load); float spd = Mathf.Min(1.6f, 0.7f + r / 70);
            for (int k = 0; k < n; k++)
            {
                float a = R() * Mathf.PI * 2, v = (40 + R() * 190) * spd, s = 2.5f + R() * (big ? 6 : 4.5f); bool rock = R() < 0.45f;
                Mpush(new Mp { x = x + Mathf.Cos(a) * r * 0.15f, y = y + Mathf.Sin(a) * r * 0.15f, z = 3, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v * 0.7f, vz = (160 + R() * 300) * spd, g = 900, drag = 0.4f, life = 1.3f + R() * 0.7f, max = 2, c = rock ? stone[k % 3] : (R() < 0.5f ? mid : dark), c2 = rock ? Hx("#9a928a") : lit, hasC2 = true, s = s, rock = true, pts = ChunkPts(s), rot = R() * 6, hasRot = true, vr = (R() - 0.5f) * 18, ember = hot && R() < 0.5f, chunk = true });
            }
            for (int k = 0; k < n * 1.5f; k++) { float a = R() * Mathf.PI * 2, v = (80 + R() * 220) * spd; Mpush(new Mp { x = x, y = y, z = 2, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v * 0.7f, vz = (120 + R() * 260) * spd, g = 900, drag = 0.3f, life = 0.8f + R() * 0.6f, max = 1.4f, c = R() < 0.6f ? dark : Hx("#5a5450"), s = 1.2f + R() * 1.6f, rock = true, rot = R() * 6, hasRot = true, vr = (R() - 0.5f) * 20 }); }
            for (int k = 0; k < Mathf.Min(16, 6 + r / 10); k++) { float a = R() * Mathf.PI * 2, d = r * (0.2f + R() * 0.6f); Mpush(new Mp { x = x + Mathf.Cos(a) * d, y = y + Mathf.Sin(a) * d * 0.7f, z = 2 + R() * 10, vx = Mathf.Cos(a) * (30 + R() * 60), vy = Mathf.Sin(a) * (20 + R() * 40), vz = 18 + R() * 30, drag = 1.2f, life = 1.1f + R() * 0.9f, max = 2, c = S(soil, 1.05f, 1), s = 5 + R() * 7, grow = 14 + R() * 10, smoke = true }); }
            if (hot) for (int k = 0; k < 12; k++) { float a = R() * Mathf.PI * 2, v = 30 + R() * 90; Mpush(new Mp { x = x, y = y, z = 4, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v * 0.7f, vz = 120 + R() * 180, g = 220, drag = 0.6f, life = 0.9f + R() * 0.8f, max = 1.7f, c = col, s = 1.4f + R() * 1.4f, glow = true }); }
        }

        // ---------- MFX: air kinds ----------
        /// ribbon: a sweeping crescent, thin at the tail and thick at the head, with a bright edge
        public static void Ribbon(AldaraSketch s, float cx, float cy, float r, float a0, float a1, float w, Color col, Color c2, float alpha, float flat, float h)
        {
            int n = 18; var o = new List<Vector2>(); var inn = new List<Vector2>(); var cs = new List<Color>();
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, a = a0 + (a1 - a0) * u, ww = w * Mathf.Pow(u, 0.8f); o.Add(V2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r * flat)); inn.Add(V2(cx + Mathf.Cos(a) * (r - ww), cy + Mathf.Sin(a) * (r - ww) * flat));
                cs.Add(u < 0.7f ? A(col, 0.55f * alpha * u / 0.7f) : Color.Lerp(A(col, 0.55f * alpha), A(col, 0.9f * alpha), (u - 0.7f) / 0.3f));
            }
            s.Strip(o, inn, cs, h);
            var edge = new List<Vector2>(); for (int i = Mathf.FloorToInt(n * 0.35f); i <= n; i++) edge.Add(o[i]); s.Polyline(edge, 2, A(c2, 0.95f * alpha), false, h);
        }
        void DrawMfxAir(Mf f, float u, float now)
        {
            float a = 1 - u, h = LZ(f.x, f.y); float PI = Mathf.PI;
            switch (f.k)
            {
                case "arc": { float p = Mathf.Min(1, u * 3.2f), s = f.a - f.dir * f.arc, e = s + f.dir * 2 * f.arc * EOut(p); Ribbon(aA, f.x, f.y - f.h, f.r, s, e, f.w, f.c, f.c2, a, 0.5f, h); Ribbon(aA, f.x, f.y - f.h, f.r * 0.82f, s, e, f.w * 0.5f, f.c, f.c2, a * 0.45f, 0.5f, h); break; }
                case "arcV":
                    {
                        float p = Mathf.Min(1, u * 3.2f), sd = Mathf.Cos(f.a) >= 0 ? 1 : -1, sx = Mathf.Max(0.35f, Mathf.Abs(Mathf.Cos(f.a))), cx = f.x, cy = f.y - f.h * 0.55f;
                        // a vertical swing: the ribbon in the screen plane, squeezed sideways by how much it faces you
                        int n = 18; float a0 = -PI / 2 - 0.6f, a1 = a0 + (PI * 0.95f + 0.6f) * EOut(p); var o = new List<Vector2>(); var inn = new List<Vector2>(); var cs = new List<Color>();
                        for (int i = 0; i <= n; i++) { float uu = i / (float)n, aa = a0 + (a1 - a0) * uu, ww = f.w * Mathf.Pow(uu, 0.8f); o.Add(V2(cx + Mathf.Cos(aa) * f.r * sd * sx, cy + Mathf.Sin(aa) * f.r)); inn.Add(V2(cx + Mathf.Cos(aa) * (f.r - ww) * sd * sx, cy + Mathf.Sin(aa) * (f.r - ww))); cs.Add(uu < 0.7f ? A(f.c, 0.55f * a * uu / 0.7f) : Color.Lerp(A(f.c, 0.55f * a), A(f.c, 0.9f * a), (uu - 0.7f) / 0.3f)); }
                        aA.Strip(o, inn, cs, h); var edge = new List<Vector2>(); for (int i = Mathf.FloorToInt(n * 0.35f); i <= n; i++) edge.Add(o[i]); aA.Polyline(edge, 2, A(f.c2, 0.95f * a), false, h); break;
                    }
                case "spin": { float h0 = f.a0 + u * PI * 3; Ribbon(aA, f.x, f.y - f.h, f.r, h0 - PI * 1.3f, h0, f.w, f.c, f.c2, a, 0.5f, h); Ribbon(aA, f.x, f.y - f.h, f.r * 0.8f, h0, h0 + PI * 0.1f, f.w * 0.5f, f.c, f.c2, a * 0.4f, 0.5f, h); break; }
                case "streak":
                    {
                        float p = Mathf.Min(1, u * 4), cs = Mathf.Cos(f.a), sn = Mathf.Sin(f.a) * 0.55f, Ln = f.len * EOut(p), x0 = f.x + cs * f.len * 0.25f, y0 = f.y - f.h + sn * f.len * 0.25f, x1 = f.x + cs * Ln, y1 = f.y - f.h + sn * Ln;
                        aA.LineC(x0, y0, x1, y1, f.w, A(f.c, 0), A(f.c, 0.9f * a), h); aA.Line(x0, y0, x1, y1, 2, A(f.c2, a), h); Glow(aA, x1, y1, 14, f.c, a, h); break;
                    }
                case "claw":
                    {
                        float p = Mathf.Min(1, u * 4), ra = f.a + PI / 2, ca = Mathf.Cos(ra), sa = Mathf.Sin(ra); float cx = f.x, cy = f.y - f.h;
                        System.Func<float, float, Vector2> T = (x, y) => V2(cx + x * ca - y * sa, cy + x * sa + y * ca);
                        for (int q = -1; q <= 1; q++)
                        {
                            float x = q * 7, Ln = f.len * (1 - Mathf.Abs(q) * 0.18f), y0 = -Ln / 2, y1 = y0 + Ln * EOut(p); var pts = new List<Vector2>(); var cols = new List<Color>();
                            for (int i = 0; i <= 8; i++) { float t = i / 8f, m = 1 - t; float qx = m * m * (x - 2) + 2 * m * t * (x + 3) + t * t * (x - 1), qy = m * m * y0 + 2 * m * t * ((y0 + y1) / 2) + t * t * y1; pts.Add(T(qx, qy)); cols.Add(A(f.c, 0.9f * a * t)); }
                            for (int i = 1; i < pts.Count; i++) aA.LineC(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, 4, cols[i - 1], cols[i], h);
                            aA.Polyline(pts, 1.2f, A(f.c2, a), false, h);
                        }
                        break;
                    }
                case "bite":
                    {
                        float p = EOut(Mathf.Min(1, u * 3)), r = f.r, o = r * (1 - p) * 0.7f, ca = Mathf.Cos(f.a), sa = Mathf.Sin(f.a), cx = f.x, cy = f.y - f.h;
                        System.Func<float, float, Vector2> T = (x, y) => V2(cx + x * ca - y * sa, cy + x * sa + y * ca); var col = A(f.c, 0.9f * a);
                        var up = new List<Vector2>(); var dn = new List<Vector2>(); for (int i = 0; i <= 12; i++) { float t = 0.3f + (PI - 0.6f) * i / 12f; up.Add(T(Mathf.Cos(t) * r, -o + Mathf.Sin(t) * r)); float t2 = PI + 0.3f + (PI - 0.6f) * i / 12f; dn.Add(T(Mathf.Cos(t2) * r, o + Mathf.Sin(t2) * r)); }
                        aA.Polyline(up, 3, col, false, h); aA.Polyline(dn, 3, col, false, h);
                        for (int q = -2; q <= 2; q++) { aA.Tri(T(q * r * 0.3f - 3, -o + r * 0.72f), T(q * r * 0.3f, -o + r * 0.72f + 6), T(q * r * 0.3f + 3, -o + r * 0.72f), col, h); aA.Tri(T(q * r * 0.3f - 3, o - r * 0.72f), T(q * r * 0.3f, o - r * 0.72f - 6), T(q * r * 0.3f + 3, o - r * 0.72f), col, h); }
                        break;
                    }
                case "impact":
                    {
                        float r = (f.big ? 30 : 20) * (0.5f + EOut(u)); Glow(aA, f.x, f.y - f.h, r * 1.4f, f.c, a * 0.9f, h); int n = f.big ? 8 : 6;
                        for (int q = 0; q < n; q++) { float aa = q / (float)n * PI * 2 + f.a, r0 = r * 0.3f, r1 = r * (q % 2 == 1 ? 0.8f : 1.2f); aA.Line(f.x + Mathf.Cos(aa) * r0, f.y - f.h + Mathf.Sin(aa) * r0, f.x + Mathf.Cos(aa) * r1, f.y - f.h + Mathf.Sin(aa) * r1, 2, A(f.c2, a), h); }
                        break;
                    }
                case "flash": Glow(aA, f.x, f.y - f.h, f.r * (0.6f + u * 0.8f), f.c, a, h); Glow(aA, f.x, f.y - f.h, f.r * 0.4f, f.c2, a, h); break;
                case "ringA": { float rr = f.r * (0.3f + 0.7f * EOut(u)); Ring(aA, f.x, f.y - f.h, rr, rr, 3 * a + 0.5f, A(f.c, a), h); break; }
                case "nova": { float r = f.r * (0.2f + 0.95f * EOut(u)); aA.Glow(f.x, f.y, r, r, new[] { 0, 0.6f, 1 }, new[] { A(f.c2, 0.5f * a), A(f.c, 0.35f * a), A(f.c, 0) }, h, 24); Glow(aA, f.x, f.y - f.h, f.r * 0.5f * (1 - u * 0.5f), f.c2, a, h); break; }
                case "roar": for (int q = 0; q < 3; q++) { float uu = u * 1.4f - q * 0.18f; if (uu < 0 || uu > 1) continue; Ring(aA, f.x, f.y - f.h * (1 - uu * 0.8f), f.r * uu, f.r * uu * 0.7f, 3, A(q == 1 ? f.c : Color.white, 0.5f * (1 - uu)), h); } break;
                case "pillar":
                    {
                        float rise = EOut(Mathf.Min(1, u * 4)), H = f.H * rise, w = f.r * 0.85f * (1 - u * 0.6f), x = f.x, y = f.y; var sk = f.dark ? aN : aA;
                        var pts = new List<Vector2>(); var cs = new List<Color>(); Color bot = A(f.dark ? Hx("#12061e") : f.c, 0.85f * a), mid = A(f.c, 0.55f * a), top = A(f.c, 0);
                        for (int i = 0; i <= 8; i++) { float t = i / 8f, m = 1 - t; pts.Add(V2(m * m * (x - w) + 2 * m * t * (x - w * 0.7f) + t * t * (x - w * 0.25f), m * m * y + 2 * m * t * (y - H * 0.5f) + t * t * (y - H))); cs.Add(t < 0.5f ? Color.Lerp(bot, mid, t * 2) : Color.Lerp(mid, top, (t - 0.5f) * 2)); }
                        for (int i = 8; i >= 0; i--) { float t = i / 8f, m = 1 - t; pts.Add(V2(m * m * (x + w) + 2 * m * t * (x + w * 0.7f) + t * t * (x + w * 0.25f), m * m * y + 2 * m * t * (y - H * 0.5f) + t * t * (y - H))); cs.Add(t < 0.5f ? Color.Lerp(bot, mid, t * 2) : Color.Lerp(mid, top, (t - 0.5f) * 2)); }
                        // fill as a strip between the two sides
                        var L1 = pts.GetRange(0, 9); var R1 = pts.GetRange(9, 9); R1.Reverse(); sk.Strip(L1, R1, cs.GetRange(0, 9), h);
                        aA.PolyC(new[] { V2(x - w * 0.22f, y), V2(x + w * 0.22f, y), V2(x + w * 0.22f, y - H), V2(x - w * 0.22f, y - H) }, new[] { A(f.c2, 0.9f * a), A(f.c2, 0.9f * a), A(f.c2, 0), A(f.c2, 0) }, h);
                        Glow(aA, x, y, f.r * 1.4f, f.c, a, h, f.r * 1.4f * 0.4f);
                        if (f.t < 0.05f && !f.sp) { f.sp = true; for (int q = 0; q < 14; q++) Mpush(new Mp { x = x + (R() - 0.5f) * f.r, y = y + (R() - 0.5f) * f.r * 0.5f, z = R() * 20, vx = (R() - 0.5f) * 40, vz = 120 + R() * (f.H * 1.5f), g = 120, life = 0.8f, max = 0.8f, c = R() < 0.5f ? f.c : f.c2, s = 2.5f, glow = true }); }
                        break;
                    }
                case "spikes":
                    {
                        if (f.S == null) { f.S = new List<Vector4>(); for (int q = 0; q < f.n; q++) { float aa = R() * PI * 2, d = Mathf.Sqrt(R()) * f.r; f.S.Add(new Vector4(f.x + Mathf.Cos(aa) * d, f.y + Mathf.Sin(aa) * d * 0.8f, (26 + R() * 26) * f.tall, (5 + R() * 5) + Mathf.Round((R() - 0.5f) * 0.4f * 1000) / 1000f * 0)); } f.S.Sort((p, q) => p.y.CompareTo(q.y)); f.L = new List<List<Vector2>>(); foreach (var s in f.S) f.L.Add(new List<Vector2> { V2((R() - 0.5f) * 0.4f, 0) }); }
                        float up = u < 0.15f ? EOut(u / 0.15f) : u < 0.7f ? 1 : 1 - EIn((u - 0.7f) / 0.3f);
                        for (int i = 0; i < f.S.Count; i++) { var s = f.S[i]; float H = s.z * up; if (H < 1) continue; float lean = f.L[i][0].x, tx = s.x + lean * H; aN.Tri(V2(s.x - s.w, s.y), V2(tx, s.y - H), V2(s.x + s.w, s.y), f.c, h); aN.Tri(V2(s.x, s.y), V2(tx, s.y - H), V2(s.x + s.w, s.y), f.c2, h); }
                        break;
                    }
                case "blink":
                    {
                        float r = 26 * (1 - u * 0.5f); Glow(aA, f.x, f.y - f.h, r * 1.5f, f.c, a, h); aA.Line(f.x, f.y - f.h - 60 * (1 - u), f.x, f.y + 4, 3 * a, A(f.c2, a), h);
                        Ring(aA, f.x, f.y, 30 * (0.4f + u), 10 * (0.4f + u), 2, A(f.c, 0.7f * a), h); break;
                    }
                case "dust": { float r = f.r * (0.4f + 0.8f * EOut(u)); Circle(aN, f.x, f.y - r * 0.3f - u * 10, r, new Color(150 / 255f, 135 / 255f, 110 / 255f, 0.3f * a), h); break; }
                case "speed":
                    {
                        float cs = Mathf.Cos(f.a), sn = Mathf.Sin(f.a);
                        for (int q = -1; q <= 1; q++) { float ox = -sn * q * f.r * 0.7f, oy = cs * q * f.r * 0.5f - f.r; aA.Line(f.x + ox - cs * f.r * 0.8f, f.y + oy - sn * f.r * 0.8f, f.x + ox - cs * f.r * 2.2f, f.y + oy - sn * f.r * 2.2f, 1.5f, A(f.c, 0.5f * a), h); }
                        break;
                    }
                case "rays":
                    {
                        var P = AldaraPlayer.I; if (f.follow && P) { f.x = P.x; f.y = P.y; }
                        float rot = f.t * 0.6f, cx = f.x, cy = f.y - f.h;
                        for (int q = 0; q < f.n; q++) { float aa = q / (float)f.n * PI * 2 + rot, Ln = f.r * (0.4f + 0.8f * EOut(u)) * (q % 2 == 1 ? 0.7f : 1), w = 0.07f; aA.PolyC(new[] { V2(cx, cy), V2(cx + Mathf.Cos(aa - w) * Ln, cy + Mathf.Sin(aa - w) * Ln * 0.7f), V2(cx + Mathf.Cos(aa + w) * Ln, cy + Mathf.Sin(aa + w) * Ln * 0.7f) }, new[] { A(Color.white, 0.9f * a), A(f.c, 0), A(f.c, 0) }, h); }
                        Glow(aA, cx, cy, f.r * 0.35f, f.c, a, h); break;
                    }
                case "glyph":
                    {
                        float al = u < 0.2f ? u / 0.2f : 1 - Mathf.Max(0, (u - 0.6f) / 0.4f), y = f.y - f.h - u * 18, s = 10 + EOut(Mathf.Min(1, u * 3)) * 4;
                        Glow(aA, f.x, y, s * 2.2f, f.c, 0.5f * al, h);
                        if (f.type == "cross") { aA.Rect(f.x - s * 0.25f, y - s, s * 0.5f, s * 2, A(f.c, 0.9f * al), h); aA.Rect(f.x - s, y - s * 0.25f, s * 2, s * 0.5f, A(f.c, 0.9f * al), h); }
                        else { var e = new List<Vector2>(); for (int i = 0; i <= 10; i++) { float t = i / 10f, m = 1 - t; e.Add(V2(m * m * (f.x - s) + 2 * m * t * f.x + t * t * (f.x + s), m * m * y + 2 * m * t * (y - s * 0.9f) + t * t * y)); } for (int i = 1; i <= 10; i++) { float t = i / 10f, m = 1 - t; e.Add(V2(m * m * (f.x + s) + 2 * m * t * f.x + t * t * (f.x - s), m * m * y + 2 * m * t * (y + s * 0.9f) + t * t * y)); } aA.Polyline(e, 2, A(Color.white, al), false, h); Circle(aA, f.x, y, s * 0.35f, A(f.c, 0.9f * al), h); }
                        break;
                    }
                case "aura":
                    {
                        float al = (u < 0.15f ? u / 0.15f : 1 - Mathf.Max(0, (u - 0.5f) / 0.5f)) * (0.8f + 0.2f * Mathf.Sin(f.t * 20));
                        var pts = new List<Vector2>(); var cs = new List<Color>(); for (int i = 0; i <= 24; i++) { float t = i / 24f * PI * 2, px = f.x + Mathf.Cos(t) * f.r, py = f.y - f.H * 0.45f + Mathf.Sin(t) * f.H * 0.55f; pts.Add(V2(px, py)); float g = Mathf.Clamp01((f.y - py) / f.H); cs.Add(A(f.c, 0.45f * al * (1 - g))); }
                        aA.PolyC(pts, cs, h); Ring(aA, f.x, f.y, f.r * 1.2f, f.r * 0.45f, 2, A(f.c, 0.6f * al), h); break;
                    }
                case "bash": { float R0 = 10 + f.r * EOut(u); aA.Arc(f.x, f.y - f.h, R0, R0 * 0.6f, f.a - 0.8f, f.a + 0.8f, 10 * a + 2, A(f.c, 0.8f * a), h); aA.Arc(f.x, f.y - f.h, R0 + 4, (R0 + 4) * 0.6f, f.a - 0.7f, f.a + 0.7f, 2, A(Color.white, a), h); break; }
            }
        }
        void DrawMp(Mp p)
        {
            float u = 1 - p.life / p.max, h = LZ(p.x, p.y), x = p.x, y = p.y - p.z;
            if (p.rock)
            {
                float al = Mathf.Min(1, p.life * 2.5f); bool hot = p.ember && u < 0.5f;
                if (p.pts != null)
                {
                    float sh = Mathf.Max(0, 1 - p.z / 70) * 0.35f * al; if (sh > 0.02f) aN.Ellipse(p.x, p.y, p.s * (0.9f - p.z / 200), p.s * 0.4f, new Color(0, 0, 0, sh), h, 0, 10);
                    float ca = Mathf.Cos(p.rot), sa = Mathf.Sin(p.rot); var P = new Vector2[p.pts.Length]; for (int i = 0; i < P.Length; i++) P[i] = V2(x + p.pts[i].x * ca - p.pts[i].y * sa, y + p.pts[i].x * sa + p.pts[i].y * ca);
                    aN.Poly(P, A(hot ? Hx("#ff8a3a") : p.c, al), h);
                    if (hot) Glow(aA, x, y, p.s * 2.2f, Hx("#ff7a2a"), 0.5f * al, h);
                }
                else { float ca = Mathf.Cos(p.rot), sa = Mathf.Sin(p.rot); aN.RotRect(x, y, p.rot, -p.s / 2, -p.s / 2, p.s / 2, p.s * 0.25f, A(hot ? Hx("#ff8a3a") : p.c, al), h); }
                return;
            }
            if (p.smoke) { Circle(aN, x, y, p.s + p.grow * u, A(p.c, 0.35f * (1 - u)), h); return; }
            if (p.flame) { float r = p.s + p.grow * u; Glow(aA, x, y, r * 1.6f, u < 0.3f ? (p.hasC2 ? p.c2 : Hx("#fff0c0")) : p.c, (1 - u) * 0.8f, h); return; }
            Circle(aA, x, y, p.s * (1 - u * 0.4f), A(p.c, Mathf.Min(1, (1 - u) * 1.4f)), h); if (p.glow) Glow(aA, x, y, p.s * 3, p.c, (1 - u) * 0.4f, h);
        }
    }
}
