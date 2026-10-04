using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // The browser's monster attack effects: doHit / meleeFx / hurtPlayer, enemy projectiles (eproj, lobs included),
    // ground hazards and shockwaves (updEnemy, lobLand, hzBoom, projPop, chanFx), and their drawing (drawMonGround:
    // hazards, telegraphs, lob targets, enrage auras; drawMonAir: cast glow, beams, falling hazards, drawProj).
    public partial class AldaraMonsters
    {
        // ---- element colours (ELC / elc / teleCol) ----
        static readonly Dictionary<string, Color[]> ELC = new Dictionary<string, Color[]>();
        public static Color[] Elc(string el)
        {
            if (ELC.Count == 0)
            {
                string[] t = { "phys", "#e6dcc8", "#ffffff", "fire", "#ff6a1a", "#ffe08a", "frost", "#6ac8ff", "#effbff", "shadow", "#8a4aff", "#e6ccff", "void", "#a86aff", "#ffe05a",
                    "poison", "#8ad040", "#eaffb8", "holy", "#ffc84a", "#fffbe8", "nature", "#6ad06a", "#e4ffc8", "arcane", "#c07aff", "#f6e4ff", "water", "#3ac0e0", "#dcffff",
                    "bone", "#d8d0b8", "#ffffff", "blood", "#e0304a", "#ffc0b0", "earth", "#b08a5a", "#f0e0c0" };
                for (int i = 0; i < t.Length; i += 3) ELC[t[i]] = new[] { AldaraRules.Hex(t[i + 1]), AldaraRules.Hex(t[i + 2]) };
            }
            Color[] c; return el != null && ELC.TryGetValue(el, out c) ? c : ELC["phys"];
        }
        public static Color ElCol(string el) { return Elc(el)[0]; }
        static Color TeleCol(MoveDef mv) { return string.IsNullOrEmpty(mv.el) || mv.el == "phys" || mv.el == "bone" || mv.el == "earth" ? AldaraRules.Hex("#ff3a24") : Elc(mv.el)[0]; }
        /// monChest: how high the chest is above the feet, from the model's height (the browser uses the sprite's top)
        public static float Chest(Mon m) { return m.chest > 0 ? Mathf.Max(10, m.chest) : m.r * 1.3f; }
        static Color Hx(string h, float a = 1) { return AldaraVfx.Hx(h, a); }
        static Color Al(Color c, float a) { return AldaraVfx.A(c, a); }
        static float Rn() { return Random.value; }
        const float PI = Mathf.PI;

        // ---- the lists ----
        public class Proj
        {
            public float x, y, vx, vy, life, dmg, rad = 5, slow, kb, h = AldaraVfx.LIFT, rot; public Color col = Color.white, c2 = Color.white; public string look = "orb", el;
            public bool pierce, hitP, ground, lob; public List<Vector2> tr;
            public float sx, sy, tx, ty, t, T, H, h0, aoe, pool, shake;
        }
        public class Hz
        {
            public float x, y, r, delay, max = 1, dmg, slow, dur, dps, tick; public Color col = Color.white, c2 = Color.white; public string fx, el;
            public bool icicle, scorch, line, pool, ring, hit; public float rr, r1, spd, w, kb;
        }
        readonly List<Proj> eproj = new List<Proj>(); readonly List<Hz> hazards = new List<Hz>();

        void FxHooks(bool on)
        {
            if (on) { AldaraVfx.DrawGround += DrawGroundFx; AldaraVfx.DrawAir += DrawAirFx; }
            else { AldaraVfx.DrawGround -= DrawGroundFx; AldaraVfx.DrawAir -= DrawAirFx; }
        }
        void OnDestroy() { FxHooks(false); }

        static AldaraVfx.Mf MF(string k, float x, float y) { return new AldaraVfx.Mf { k = k, x = x, y = y }; }
        static void Mfx(AldaraVfx.Mf f) { AldaraVfx.Mfx(f); }
        static void Shake(float amp, float life) { AldaraVfx.Shake(amp, life); }

        // ---- doHit: what each move shape does when it lands ----
        void DoHit(Mon m, Act A, int i)
        {
            var mv = A.mv; float a = A.a; var P = AldaraPlayer.I; var E = Elc(mv.el); float h = Chest(m), c = Mathf.Cos(a), s = Mathf.Sin(a), pd = Mathf.Sqrt((P.x - m.x) * (P.x - m.x) + (P.y - m.y) * (P.y - m.y));
            switch (mv.shape)
            {
                case "arc": case "line": case "circle":
                    MeleeFx(m, mv, mv.fx, mv.rad, mv.fwd, A, i, h, E);
                    if (HitTest(m, mv, a)) HurtPlayer(m, mv, Mathf.Atan2(P.y - m.y, P.x - m.x));
                    break;
                case "roar":
                    Mfx(new AldaraVfx.Mf { k = "roar", x = m.x, y = m.y, h = h * 1.2f, r = mv.rad, c = E[0], T = 0.8f }); Shake(mv.shake > 0 ? mv.shake : 3, 0.35f);
                    if (mv.buff > 0) { m.enrT = mv.buff; AldaraFx.Text(m.x, m.y - m.r - 40, "Enraged", AldaraRules.Hex("#ff8a5a")); }
                    if (pd < mv.rad + 16) { if (mv.slow > 0) AldaraHero.I.slowT = Mathf.Max(AldaraHero.I.slowT, mv.slow); if (mv.dmg > 0) HurtPlayer(m, mv, Mathf.Atan2(P.y - m.y, P.x - m.x), 1, true); }
                    break;
                case "proj":
                    {
                        int n = Mathf.Max(1, (int)mv.n); float sp = mv.spd > 0 ? mv.spd : 400, lead = AimLead(m, sp);
                        float bse = mv.hits.Length > 1 ? lead : (Mathf.Abs(AldaraRules.AngD(lead, a)) < 0.5f ? lead : a);
                        for (int k = 0; k < n; k++) SpawnProj(m, mv, bse + (k - (n - 1) / 2f) * mv.spread + (mv.hits.Length > 1 ? (Rn() - 0.5f) * (mv.spread > 0 ? mv.spread : 0.1f) : 0), false);
                        Mfx(new AldaraVfx.Mf { k = "flash", x = m.x + c * m.r * 0.9f, y = m.y + s * m.r * 0.9f, h = h * 1.1f, r = 18, c = E[0], c2 = E[1], T = 0.18f });
                        break;
                    }
                case "gwave":
                    {
                        int n = Mathf.Max(1, (int)mv.n); for (int k = 0; k < n; k++) SpawnProj(m, mv, a + (k - (n - 1) / 2f) * mv.spread, true);
                        Mfx(new AldaraVfx.Mf { k = "crack", x = m.x + c * m.r, y = m.y + s * m.r, a = a, len = 60, c = E[0], T = 1.2f, n = 3, r = 40 }); Shake(4, 0.25f); break;
                    }
                case "lob":
                    eproj.Add(new Proj { lob = true, sx = m.x + c * m.r * 0.6f, sy = m.y + s * m.r * 0.6f, x = m.x, y = m.y, tx = A.tx, ty = A.ty, T = mv.T > 0 ? mv.T : 0.9f, H = mv.H > 0 ? mv.H : 120, h0 = h * 1.3f, h = h * 1.3f,
                        aoe = mv.rad > 0 ? mv.rad : 60, dmg = Dmg(m, mv), col = E[0], c2 = E[1], look = string.IsNullOrEmpty(mv.look) ? "rock" : mv.look, pool = mv.pool, slow = mv.slow, el = mv.el, tr = new List<Vector2>(), shake = mv.shake });
                    break;
                case "rain":
                    {
                        int n = (int)(mv.n > 0 ? mv.n : 4);
                        for (int k = 0; k < n; k++)
                        {
                            float x, y; if (k == 0) { x = P.x + pvx * 0.35f; y = P.y + pvy * 0.35f; } else { float aa = Rn() * PI * 2, dd = 50 + Rn() * 110; x = A.tx + Mathf.Cos(aa) * dd; y = A.ty + Mathf.Sin(aa) * dd; }
                            HzPush(new Hz { x = x, y = y, r = mv.rad, delay = mv.delay + k * 0.13f, dmg = Dmg(m, mv), col = E[0], c2 = E[1], fx = mv.hz, el = mv.el, slow = mv.slow, icicle = mv.hz == "icicle", scorch = mv.el == "fire" });
                        }
                        Mfx(new AldaraVfx.Mf { k = "flash", x = m.x, y = m.y, h = h * 1.8f, r = 30, c = E[0], c2 = E[1], T = 0.3f }); break;
                    }
                case "target": HzPush(new Hz { x = P.x + pvx * 0.25f, y = P.y + pvy * 0.25f, r = mv.rad, delay = mv.delay, dmg = Dmg(m, mv), col = E[0], c2 = E[1], fx = mv.hz, el = mv.el, slow = mv.slow, scorch = mv.el == "fire" }); break;
                case "spikeline":
                    for (int k = 1; k <= (mv.n > 0 ? mv.n : 7); k++) { float d = m.r + k * (mv.gap > 0 ? mv.gap : 40); HzPush(new Hz { x = m.x + c * d, y = m.y + s * d, r = mv.rad, delay = mv.delay + k * 0.075f, dmg = Dmg(m, mv), col = E[0], c2 = E[1], fx = string.IsNullOrEmpty(mv.hz) ? "spikes" : mv.hz, el = mv.el, slow = mv.slow, line = true }); }
                    MeleeFx(m, mv, "quake", 40, m.r * 0.8f, A, i, h, E); break;
                case "ringz":
                    {
                        int n = (int)(mv.n > 0 ? mv.n : 6); string hz = string.IsNullOrEmpty(mv.hz) ? "pillar" : mv.hz;
                        for (int k = 0; k < n; k++) { float aa = k / (float)n * PI * 2 + Rn() * 0.3f, d = (mv.dist > 0 ? mv.dist : 110) * (0.8f + Rn() * 0.4f); HzPush(new Hz { x = m.x + Mathf.Cos(aa) * d, y = m.y + Mathf.Sin(aa) * d, r = mv.rad, delay = mv.delay + k * 0.06f, dmg = Dmg(m, mv), col = E[0], c2 = E[1], fx = hz, el = mv.el, scorch = mv.el == "fire" }); }
                        HzPush(new Hz { x = P.x, y = P.y, r = mv.rad, delay = mv.delay + 0.25f, dmg = Dmg(m, mv), col = E[0], c2 = E[1], fx = hz, el = mv.el, scorch = mv.el == "fire" });
                        MeleeFx(m, mv, "quake", 70, 30, A, i, h, E); break;
                    }
                case "wave":
                    hazards.Add(new Hz { ring = true, x = m.x, y = m.y, rr = m.r, r1 = mv.rad, spd = mv.spd, w = 30, dmg = Dmg(m, mv), col = E[0], c2 = E[1], kb = mv.kb, slow = mv.slow });
                    Mfx(new AldaraVfx.Mf { k = "nova", x = m.x, y = m.y, h = h, r = m.r * 2, c = E[0], c2 = E[1], T = 0.35f }); Shake(3, 0.25f); break;
            }
        }
        /// meleeFx: the swing, chop, spin, claw, bite, quake, nova, splash or dust a melee move leaves
        void MeleeFx(Mon m, MoveDef mv, string fx0, float rad, float fwd, Act A, int i, float h, Color[] E)
        {
            float a = A.a, c = Mathf.Cos(a), s = Mathf.Sin(a), reach = mv.reach > 0 ? mv.reach : 30, R = m.r + reach;
            string fx = fx0 == "combo" ? new[] { "arc", "arcB", "arcV" }[i % 3] : fx0;
            switch (fx)
            {
                case "arc": case "arcB": Mfx(new AldaraVfx.Mf { k = "arc", x = m.x, y = m.y, h = h * 0.85f, a = a, r = R * 0.95f, arc = mv.arc > 0 ? mv.arc : 1.1f, dir = fx == "arcB" ? -1 : 1, c = E[0], c2 = E[1], w = 13 + m.r * 0.2f, T = 0.3f }); break;
                case "arcW":
                    Mfx(new AldaraVfx.Mf { k = "arc", x = m.x, y = m.y, h = h * 0.45f, a = a, r = R, arc = mv.arc > 0 ? mv.arc : 1.8f, dir = 1, c = E[0], c2 = E[1], w = 16 + m.r * 0.25f, T = 0.36f });
                    Mfx(new AldaraVfx.Mf { k = "dust", x = m.x + c * R * 0.7f, y = m.y + s * R * 0.7f, r = m.r, T = 0.6f }); break;
                case "arcV": case "chop":
                    Mfx(new AldaraVfx.Mf { k = "arcV", x = m.x, y = m.y, h = h * 1.3f, a = a, r = R * 0.9f, c = E[0], c2 = E[1], w = 12 + m.r * 0.2f, T = 0.3f });
                    if (fx == "chop" || mv.shape == "line")
                    {
                        float rch = mv.reach > 0 ? mv.reach : 40, d = m.r + rch * 0.8f;
                        Mfx(new AldaraVfx.Mf { k = "crack", x = m.x + c * d, y = m.y + s * d, a = a, len = rch * 0.9f, c = E[0], T = 1.4f, n = 3, r = 24 }); Mfx(new AldaraVfx.Mf { k = "dust", x = m.x + c * d, y = m.y + s * d, r = 22, T = 0.6f });
                        AldaraVfx.Debris(m.x + c * d, m.y + s * d, 6, Hx("#6a5a4a")); Shake(mv.shake > 0 ? mv.shake : 3, 0.2f);
                    }
                    break;
                case "streak": Mfx(new AldaraVfx.Mf { k = "streak", x = m.x, y = m.y, h = h, a = a, len = m.r + (mv.reach > 0 ? mv.reach : 60), c = E[0], c2 = E[1], w = 10, T = 0.22f }); break;
                case "spin": Mfx(new AldaraVfx.Mf { k = "spin", x = m.x, y = m.y, h = h * 0.7f, r = rad >= 0 ? rad : m.r + mv.reach, c = E[0], c2 = E[1], w = 14, T = 0.34f, a0 = Rn() * 6 }); break;
                case "claw": { float d = m.r + reach * 0.6f; Mfx(new AldaraVfx.Mf { k = "claw", x = m.x + c * d, y = m.y + s * d, h = h * 0.8f, a = a + (i % 2 == 1 ? 0.5f : -0.5f), len = 26 + m.r * 0.6f, c = E[0], c2 = E[1], T = 0.28f }); break; }
                case "bite": { float d = m.r + reach * 0.6f; bool phys = E[0] == Elc("phys")[0]; Mfx(new AldaraVfx.Mf { k = "bite", x = m.x + c * d, y = m.y + s * d, h = h * 0.7f, a = a, r = 12 + m.r * 0.35f, c = phys ? Color.white : E[0], T = 0.26f }); break; }
                case "impact":
                    {
                        float d = m.r + reach * 0.7f;
                        Mfx(new AldaraVfx.Mf { k = "flash", x = m.x + c * d, y = m.y + s * d, h = h * 0.8f, r = 16 + m.r * 0.3f, c = E[0], c2 = E[1], T = 0.2f }); Mfx(new AldaraVfx.Mf { k = "ringA", x = m.x + c * d, y = m.y + s * d, h = h * 0.8f, r = 26 + m.r * 0.4f, c = E[1], T = 0.25f }); break;
                    }
                case "quake":
                    {
                        float fw = fwd > 0 ? fwd : 0, cx = m.x + c * fw, cy = m.y + s * fw, R2 = rad > 0 ? rad : 70;
                        Mfx(new AldaraVfx.Mf { k = "crack", x = cx, y = cy, a = Rn() * 6, len = R2, c = E[0], T = 1.8f, n = 7, r = R2 }); Mfx(new AldaraVfx.Mf { k = "shock", x = cx, y = cy, r = R2 * 1.15f, c = E[0], c2 = E[1], T = 0.45f });
                        Mfx(new AldaraVfx.Mf { k = "flash", x = cx, y = cy, h = 4, r = R2 * 0.6f, c = E[0], c2 = E[1], T = 0.2f }); AldaraVfx.Debris(cx, cy, 10 + Mathf.RoundToInt(R2 / 10), Hx(mv.el == "frost" ? "#cfefff" : mv.el == "fire" ? "#4a2a1a" : "#6a5a4a"), mv.el);
                        for (int k = 0; k < 5; k++) { float aa = k / 5f * PI * 2 + Rn(); Mfx(new AldaraVfx.Mf { k = "dust", x = cx + Mathf.Cos(aa) * R2 * 0.6f, y = cy + Mathf.Sin(aa) * R2 * 0.6f, r = R2 * 0.35f, T = 0.8f }); }
                        if (mv.el == "fire") Mfx(new AldaraVfx.Mf { k = "scorch", x = cx, y = cy, r = R2 * 0.8f, T = 4, c = E[0] });
                        if (mv.el == "frost") Mfx(new AldaraVfx.Mf { k = "spikes", x = cx, y = cy, r = R2 * 0.7f, n = 9, c = Hx("#cfefff"), c2 = Hx("#6ac8ff"), T = 1.0f });
                        Shake(mv.shake > 0 ? mv.shake : 5, 0.35f); break;
                    }
                case "nova":
                    {
                        float rr = rad > 0 ? rad : 120;
                        Mfx(new AldaraVfx.Mf { k = "nova", x = m.x, y = m.y, h = h * 0.8f, r = rr, c = E[0], c2 = E[1], T = 0.55f }); Mfx(new AldaraVfx.Mf { k = "shock", x = m.x, y = m.y, r = rr * 1.05f, c = E[0], c2 = E[1], T = 0.5f });
                        for (int k = 0; k < 22; k++) { float aa = Rn() * PI * 2, v = 160 + Rn() * 220; AldaraVfx.Mpush(new AldaraVfx.Mp { x = m.x, y = m.y, z = h * 0.8f, vx = Mathf.Cos(aa) * v, vy = Mathf.Sin(aa) * v, vz = (Rn() - 0.3f) * 120, drag = 3, life = 0.6f, max = 0.6f, c = Rn() < 0.5f ? E[0] : E[1], s = 3, glow = true }); }
                        if (mv.el == "fire") Mfx(new AldaraVfx.Mf { k = "scorch", x = m.x, y = m.y, r = rr * 0.7f, T = 3, c = E[0] });
                        break;
                    }
                case "splash":
                    {
                        float rr = rad > 0 ? rad : 80;
                        Mfx(new AldaraVfx.Mf { k = "shock", x = m.x, y = m.y, r = rr * 1.05f, c = E[0], c2 = E[1], T = 0.5f });
                        for (int k = 0; k < 26; k++) { float aa = Rn() * PI * 2, v = 80 + Rn() * 200; AldaraVfx.Mpush(new AldaraVfx.Mp { x = m.x + Mathf.Cos(aa) * m.r * 0.8f, y = m.y + Mathf.Sin(aa) * m.r * 0.8f, z = 10, vx = Mathf.Cos(aa) * v, vy = Mathf.Sin(aa) * v, vz = 120 + Rn() * 160, g = 520, life = 0.8f, max = 0.8f, c = E[0], s = 3.5f }); }
                        Mfx(new AldaraVfx.Mf { k = "scorch", x = m.x, y = m.y, r = rr * 0.8f, T = 2.5f, c = E[0], wet = true }); break;
                    }
                case "dust":
                    {
                        float rr = rad > 0 ? rad : 50; Mfx(new AldaraVfx.Mf { k = "shock", x = m.x, y = m.y, r = rr * 1.1f, c = Hx("#d8c8a8"), c2 = Color.white, T = 0.4f });
                        for (int k = 0; k < 5; k++) { float aa = k / 5f * PI * 2; Mfx(new AldaraVfx.Mf { k = "dust", x = m.x + Mathf.Cos(aa) * m.r, y = m.y + Mathf.Sin(aa) * m.r, r = m.r * 0.8f, T = 0.7f }); }
                        Shake(2.5f, 0.2f); break;
                    }
            }
        }
        /// hurtPlayer: the damage, slow and knockback, the impact flash, a hit-stop and a shake for heavy blows
        void HurtPlayer(Mon m, MoveDef mv, float a, float mult = 1, bool roar = false)
        {
            var H = AldaraHero.I; if (!H.alive) return;
            AldaraHero.LvSrc = m; try { H.Damage(Dmg(m, mv, mult), a, m.lvl); } finally { AldaraHero.LvSrc = null; }
            if (mv.slow > 0) H.slowT = Mathf.Max(H.slowT, mv.slow);
            if (mv.kb > 0 && !roar) Knock(a, mv.kb);
            var E = Elc(mv.el); var P = AldaraPlayer.I; float dm = mv.dmg > 0 ? mv.dmg : 1;
            Mfx(new AldaraVfx.Mf { k = "impact", x = P.x, y = P.y, h = 22, a = a, c = E[0], c2 = E[1], T = 0.24f, big = dm >= 1.4f });
            if (m.act != null && mv.chan <= 0) m.act.hs = dm >= 1.4f ? 0.09f : 0.05f;
            float sh = roar ? 0 : mv.shake; if (sh > 0 || dm >= 1.4f) Shake((sh > 0 ? sh : 3) * 0.8f, 0.22f);
        }
        void ChanFx(Mon m, Act A, float dt)
        {
            var mv = A.mv; var E = Elc(mv.el); float h = Chest(m);
            if (mv.shape == "cone")
            {
                int n = Mathf.Min(10, Mathf.CeilToInt(dt * 110));
                for (int k = 0; k < n; k++) { float aa = A.a + (Rn() - 0.5f) * mv.arc * 1.6f, v = mv.len / 0.5f * (0.7f + Rn() * 0.4f); AldaraVfx.Mpush(new AldaraVfx.Mp { x = m.x + Mathf.Cos(A.a) * m.r * 0.9f, y = m.y + Mathf.Sin(A.a) * m.r * 0.9f, z = h * 0.9f, vx = Mathf.Cos(aa) * v, vy = Mathf.Sin(aa) * v, vz = -h * 1.4f, life = 0.5f, max = 0.5f, c = E[0], c2 = E[1], hasC2 = true, s = 4, grow = 34, flame = true }); }
            }
            if (mv.shape == "beam")
            {
                A.sp -= dt; if (A.sp <= 0)
                {
                    A.sp = 0.04f; float L = BeamLen(m, A.a, mv.len), x = m.x + Mathf.Cos(A.a) * L, y = m.y + Mathf.Sin(A.a) * L;
                    for (int k = 0; k < 3; k++) { float aa = Rn() * PI * 2, v = 60 + Rn() * 120; AldaraVfx.Mpush(new AldaraVfx.Mp { x = x, y = y, z = h * 0.9f, vx = Mathf.Cos(aa) * v, vy = Mathf.Sin(aa) * v, vz = 60 + Rn() * 80, g = 200, life = 0.4f, max = 0.4f, c = Rn() < 0.5f ? E[0] : E[1], s = 2.5f, glow = true }); }
                }
            }
        }
        float BeamLen(Mon m, float a, float len)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            for (float d = m.r; d < len; d += 14) { float x = m.x + c * d, y = m.y + s * d; if (m.dun && AldaraDungeon.Active ? AldaraDungeon.WallD(x, y) < 0 : AldaraWorld.BlockedAt(x, y)) return d; }
            return len;
        }

        // ---- spawning ----
        void SpawnProj(Mon m, MoveDef mv, float ang, bool ground)
        {
            var E = Elc(mv.el); float sp = mv.spd > 0 ? mv.spd : 400; string look = string.IsNullOrEmpty(mv.look) ? "orb" : mv.look;
            eproj.Add(new Proj { x = m.x + Mathf.Cos(ang) * m.r * 0.8f, y = m.y + Mathf.Sin(ang) * m.r * 0.8f, vx = Mathf.Cos(ang) * sp, vy = Mathf.Sin(ang) * sp, dmg = Dmg(m, mv), col = E[0], c2 = E[1],
                life = (MvMax(m, mv) + 160) / sp, look = look, rad = look == "flamewave" ? 26 : look == "lance" || look == "spear" ? 9 : 7, h = look == "flamewave" ? 0 : Chest(m) * 1.1f, slow = mv.slow, kb = mv.kb,
                tr = new List<Vector2>(), el = mv.el, rot = Rn() * 6, pierce = look == "flamewave", ground = ground });
        }
        void HzPush(Hz h) { h.max = h.delay; hazards.Add(h); }
        /// a ground hazard from a dungeon or raid (a boss's nova, falling icicles or meteors, a pool that burns while you stand in it)
        public void AddHazard(float x, float y, float r, float delay, float dmg, Color col, float slow, bool icicle, float poolDur, float dps, string fx = null)
        {
            hazards.Add(new Hz { x = x, y = y, r = r, delay = delay, max = delay, dmg = dmg, col = col, slow = slow, icicle = icicle, pool = poolDur > 0, dur = poolDur, dps = dps, fx = fx });
        }
        /// a shockwave ring spreading from (x, y) (raids)
        public void AddRing(float x, float y, float rr, float r1, float spd, float w, float dmg, Color col) { hazards.Add(new Hz { ring = true, x = x, y = y, rr = rr, r1 = r1, spd = spd, w = w, dmg = dmg, col = col }); }
        /// enemyShoot: a bolt from a dungeon boss
        public void EnemyShoot(Mon m, float ang, float dmg, Color col, float spd)
        {
            if (spd <= 0) spd = 380;
            eproj.Add(new Proj { x = m.x + Mathf.Cos(ang) * m.r, y = m.y + Mathf.Sin(ang) * m.r, vx = Mathf.Cos(ang) * spd, vy = Mathf.Sin(ang) * spd, dmg = dmg, col = col, life = 2.2f, slow = m.slowHit ? 2 : 0 });
        }

        // ---- updEnemy: projectiles, lobs, hazards ----
        static bool Near(float ax, float ay, float bx, float by, float r) { return (ax - bx) * (ax - bx) + (ay - by) * (ay - by) < r * r; }
        void UpdateEnemyFx(float dt)
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I; const float PR = 16;
            for (int i = eproj.Count - 1; i >= 0; i--)
            {
                var e = eproj[i];
                if (e.lob)
                {
                    e.t += dt; float u = Mathf.Min(1, e.t / e.T); e.x = e.sx + (e.tx - e.sx) * u; e.y = e.sy + (e.ty - e.sy) * u; e.h = e.h0 * (1 - u) + 4 * e.H * u * (1 - u); e.rot += dt * 7;
                    e.tr.Add(new Vector2(e.x, e.y - e.h)); if (e.tr.Count > 10) e.tr.RemoveAt(0);
                    if (e.look == "magma" && Rn() < 0.7f) AldaraVfx.Mpush(new AldaraVfx.Mp { x = e.x, y = e.y, z = e.h, vx = (Rn() - 0.5f) * 30, vy = (Rn() - 0.5f) * 30, vz = 20, life = 0.5f, max = 0.5f, c = Hx("#3a3030"), s = 5, grow = 12, smoke = true });
                    if (u >= 1) { LobLand(e); eproj.RemoveAt(i); }
                    continue;
                }
                float ox = e.x, oy = e.y; e.x += e.vx * dt; e.y += e.vy * dt; e.life -= dt; e.rot += dt * 12;
                if (e.tr != null) { e.tr.Add(new Vector2(e.x, e.y)); if (e.tr.Count > 9) e.tr.RemoveAt(0); }
                if (e.look == "fire" && Rn() < 0.8f) AldaraVfx.Mpush(new AldaraVfx.Mp { x = e.x, y = e.y, z = e.h, vx = (Rn() - 0.5f) * 40 - e.vx * 0.1f, vy = (Rn() - 0.5f) * 40 - e.vy * 0.1f, vz = 10, life = 0.35f, max = 0.35f, c = e.col, c2 = e.c2, hasC2 = true, s = 3, grow = 8, flame = true });
                if (e.look == "flamewave" && Rn() < 0.9f) AldaraVfx.Mpush(new AldaraVfx.Mp { x = e.x + (Rn() - 0.5f) * 40, y = e.y + (Rn() - 0.5f) * 20, vz = 60, life = 0.45f, max = 0.45f, c = e.col, c2 = e.c2, hasC2 = true, s = 5, grow = 14, flame = true });
                if (e.look == "skull" && Rn() < 0.6f) AldaraVfx.Mpush(new AldaraVfx.Mp { x = e.x, y = e.y, z = e.h + 4, vz = 30, life = 0.4f, max = 0.4f, c = e.col, c2 = e.c2, hasC2 = true, s = 3, grow = 6, flame = true });
                if (H.alive && !e.hitP && Near(e.x, e.y, P.x, P.y, PR * 0.7f + e.rad))
                {
                    float a = Mathf.Atan2(e.vy, e.vx); H.Damage(e.dmg, a, 0); if (e.slow > 0) H.slowT = Mathf.Max(H.slowT, e.slow); if (e.kb > 0) Knock(a, e.kb);
                    Mfx(new AldaraVfx.Mf { k = "impact", x = P.x, y = P.y, h = 22, a = a, c = e.col, c2 = e.c2, T = 0.22f });
                    if (!e.pierce) { eproj.RemoveAt(i); continue; }
                    e.hitP = true;
                }
                if (e.life <= 0 || (!e.ground && ProjWall(e, ox, oy))) { if (e.life > 0) ProjPop(e); eproj.RemoveAt(i); }
            }
            for (int i = hazards.Count - 1; i >= 0; i--)
            {
                var h = hazards[i];
                if (h.ring)
                {
                    h.rr += h.spd * dt; float d = Mathf.Sqrt((P.x - h.x) * (P.x - h.x) + (P.y - h.y) * (P.y - h.y));
                    if (!h.hit && H.alive && Mathf.Abs(d - h.rr) < h.w * 0.5f + PR * 0.6f)
                    {
                        h.hit = true; float a = Mathf.Atan2(P.y - h.y, P.x - h.x); H.Damage(h.dmg, a, 0); if (h.slow > 0) H.slowT = Mathf.Max(H.slowT, h.slow); if (h.kb > 0) Knock(a, h.kb);
                        Mfx(new AldaraVfx.Mf { k = "impact", x = P.x, y = P.y, h = 22, a = a, c = h.col, c2 = h.c2, T = 0.22f });
                    }
                    if (h.rr >= h.r1) hazards.RemoveAt(i);
                    continue;
                }
                if (h.delay > 0)
                {
                    h.delay -= dt;
                    if (h.delay <= 0)
                    {
                        if (string.IsNullOrEmpty(h.fx) || h.fx == "pillar") AldaraVfx.Burst(h.x, h.y, h.col, h.pool ? 10 : 18, h.pool ? 80 : 200);
                        if (h.dmg > 0 && H.alive && Near(P.x, P.y, h.x, h.y, h.r + PR * 0.4f))
                        {
                            H.Damage(h.dmg, Mathf.Atan2(P.y - h.y, P.x - h.x), 0); if (h.slow > 0) H.slowT = Mathf.Max(H.slowT, h.slow);
                            Mfx(new AldaraVfx.Mf { k = "impact", x = P.x, y = P.y, h = 22, c = h.col, c2 = h.c2, T = 0.22f });
                        }
                        HzBoom(h);
                        if (h.icicle || !string.IsNullOrEmpty(h.fx)) Shake(h.fx == "meteor" ? 4.5f : 1.8f, 0.18f);
                        if (!(h.dur > 0)) { hazards.RemoveAt(i); continue; }
                        h.tick = 0;
                    }
                    continue;
                }
                h.dur -= dt; h.tick -= dt;
                if (h.tick <= 0) { h.tick = 0.5f; if (H.alive && Near(P.x, P.y, h.x, h.y, h.r)) H.Damage(Mathf.Max(1, Mathf.Round(h.dps * 0.5f)), 0, 0); }
                if (Rn() < dt * 6) AldaraVfx.Mpush(new AldaraVfx.Mp { x = h.x + (Rn() - 0.5f) * h.r * 1.4f, y = h.y + (Rn() - 0.5f) * h.r * 1.4f, vz = 24, life = 0.6f, max = 0.6f, c = h.col, s = 3, grow = 6, flame = true });
                if (h.dur <= 0) hazards.RemoveAt(i);
            }
        }
        static bool ProjWall(Proj e, float ox, float oy) { return AldaraDungeon.Active ? AldaraDungeon.ShotWall(e.x, e.y, ox, oy) : AldaraWorld.BlockedAt(e.x, e.y); }
        static void ProjPop(Proj e) { Mfx(new AldaraVfx.Mf { k = "flash", x = e.x, y = e.y, h = e.h, r = 16, c = e.col, c2 = e.c2, T = 0.18f }); AldaraVfx.Burst(e.x, e.y, e.col, 6, 110); }
        void LobLand(Proj e)
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I; Color c0 = e.col, c1 = e.c2;
            if (H.alive && Near(P.x, P.y, e.x, e.y, e.aoe + 16 * 0.6f))
            {
                H.Damage(e.dmg, Mathf.Atan2(P.y - e.y, P.x - e.x), 0); if (e.slow > 0) H.slowT = Mathf.Max(H.slowT, e.slow);
                Mfx(new AldaraVfx.Mf { k = "impact", x = P.x, y = P.y, h = 22, c = c0, c2 = c1, T = 0.24f, big = true });
            }
            if (e.look == "magma") { var f = AldaraVfx.Effect("explode", e.x, e.y, e.aoe, 0.6f, Color.white); Mfx(new AldaraVfx.Mf { k = "scorch", x = e.x, y = e.y, r = e.aoe * 0.9f, T = 4, c = c0 }); }
            else if (e.look == "glob")
            {
                Mfx(new AldaraVfx.Mf { k = "shock", x = e.x, y = e.y, r = e.aoe, c = c0, c2 = c1, T = 0.4f });
                for (int k = 0; k < 18; k++) { float a = Rn() * PI * 2, v = 60 + Rn() * 160; AldaraVfx.Mpush(new AldaraVfx.Mp { x = e.x, y = e.y, z = 6, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, vz = 100 + Rn() * 150, g = 520, life = 0.7f, max = 0.7f, c = c0, s = 3 }); }
            }
            else
            {
                Mfx(new AldaraVfx.Mf { k = "crack", x = e.x, y = e.y, a = Rn() * 6, len = e.aoe, c = c0, T = 1.6f, n = 6, r = e.aoe }); Mfx(new AldaraVfx.Mf { k = "shock", x = e.x, y = e.y, r = e.aoe * 1.1f, c = c0, c2 = c1, T = 0.45f });
                AldaraVfx.Debris(e.x, e.y, 12, Hx(e.look == "ice" ? "#dff6ff" : "#7a6a5a"), e.el);
                for (int k = 0; k < 4; k++) { float a = k / 4f * PI * 2; Mfx(new AldaraVfx.Mf { k = "dust", x = e.x + Mathf.Cos(a) * e.aoe * 0.5f, y = e.y + Mathf.Sin(a) * e.aoe * 0.5f, r = e.aoe * 0.4f, T = 0.8f }); }
                if (e.look == "ice") Mfx(new AldaraVfx.Mf { k = "spikes", x = e.x, y = e.y, r = e.aoe * 0.6f, n = 8, c = Hx("#dff6ff"), c2 = Hx("#6ac8ff"), T = 1.1f });
            }
            if (e.pool > 0) hazards.Add(new Hz { x = e.x, y = e.y, r = e.aoe * 0.85f, delay = 0, max = 1, dur = e.pool, dps = Mathf.Round(e.dmg * 0.35f), col = c0, pool = true, tick = 0.3f });
            Shake(e.shake > 0 ? e.shake : 3.5f, 0.3f);
        }
        static void HzBoom(Hz h)
        {
            Color c0 = h.col, c1 = h.c2;
            switch (h.fx)
            {
                case "pillar":
                    Mfx(new AldaraVfx.Mf { k = "pillar", x = h.x, y = h.y, r = h.r, c = c0, c2 = c1, H = 110 + h.r, T = 0.75f }); Mfx(new AldaraVfx.Mf { k = "shock", x = h.x, y = h.y, r = h.r * 1.15f, c = c0, c2 = c1, T = 0.4f });
                    if (h.scorch) Mfx(new AldaraVfx.Mf { k = "scorch", x = h.x, y = h.y, r = h.r * 0.9f, T = 3.5f, c = c0 }); break;
                case "holy": Mfx(new AldaraVfx.Mf { k = "pillar", x = h.x, y = h.y, r = h.r * 0.8f, c = c0, c2 = c1, H = 420, T = 0.8f, holy = true }); Mfx(new AldaraVfx.Mf { k = "shock", x = h.x, y = h.y, r = h.r * 1.2f, c = c0, c2 = c1, T = 0.45f }); break;
                case "void": Mfx(new AldaraVfx.Mf { k = "pillar", x = h.x, y = h.y, r = h.r * 0.9f, c = c0, c2 = c1, H = 150, T = 0.8f, dark = true }); Mfx(new AldaraVfx.Mf { k = "shock", x = h.x, y = h.y, r = h.r * 1.2f, c = c0, c2 = c1, T = 0.5f }); break;
                case "meteor":
                    { var f = AldaraVfx.Effect("explode", h.x, h.y, h.r * 1.2f, 0.65f, Color.white); f.big = true; }
                    Mfx(new AldaraVfx.Mf { k = "scorch", x = h.x, y = h.y, r = h.r, T = 4.5f, c = c0 }); Mfx(new AldaraVfx.Mf { k = "crack", x = h.x, y = h.y, a = 0, len = h.r, c = c0, T = 2, n = 6, r = h.r }); AldaraVfx.Debris(h.x, h.y, 10, Hx("#3a2a1a"), "fire"); break;
                case "spikes":
                    {
                        string[] col = h.el == "frost" ? new[] { "#dff6ff", "#6ac8ff" } : h.el == "nature" ? new[] { "#6a4a2a", "#8ae06a" } : h.el == "bone" ? new[] { "#eee6d0", "#b8a888" } : h.el == "void" || h.el == "shadow" ? new[] { "#2a1a44", "#b07aff" } : new[] { "#8a7a60", "#c8b898" };
                        Mfx(new AldaraVfx.Mf { k = "spikes", x = h.x, y = h.y, r = h.r, n = h.line ? 4 : 7, c = Hx(col[0]), c2 = Hx(col[1]), T = 1.0f, tall = h.el == "nature" ? 1.3f : 1 }); Mfx(new AldaraVfx.Mf { k = "dust", x = h.x, y = h.y, r = h.r * 0.9f, T = 0.6f }); break;
                    }
                case "arrow":
                    for (int k = 0; k < 5; k++) { float a = Rn() * PI * 2, d = Rn() * h.r * 0.8f; Mfx(new AldaraVfx.Mf { k = "stuck", x = h.x + Mathf.Cos(a) * d, y = h.y + Mathf.Sin(a) * d, a = Rn() * 0.6f - 0.3f, T = 1.6f }); }
                    Mfx(new AldaraVfx.Mf { k = "dust", x = h.x, y = h.y, r = h.r * 0.8f, T = 0.5f }); break;
                default:
                    if (h.icicle)
                    {
                        Mfx(new AldaraVfx.Mf { k = "spikes", x = h.x, y = h.y, r = h.r * 0.55f, n = 6, c = Hx("#dff6ff"), c2 = Hx("#6ac8ff"), T = 0.8f });
                        for (int k = 0; k < 12; k++) { float a = Rn() * PI * 2, v = 80 + Rn() * 160; AldaraVfx.Mpush(new AldaraVfx.Mp { x = h.x, y = h.y, z = 8, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, vz = 140 + Rn() * 120, g = 700, life = 0.7f, max = 0.7f, c = Hx("#e8f8ff"), s = 2.5f, rock = true, hasRot = true, vr = 10 }); }
                    }
                    break;
            }
        }
        void ClearFx() { eproj.Clear(); hazards.Clear(); foreach (var k in corpses) if (k.go) Destroy(k.go); corpses.Clear(); if (AldaraVfx.I) AldaraVfx.I.Clear(); }

        // ---------- drawing ----------
        static float LZ(float x, float y) { return AldaraVfx.LZ(x, y); }
        static List<Vector2> Sector(float x, float y, float R, float a0, float a1, int n = 20)
        {
            var p = new List<Vector2> { new Vector2(x, y) }; for (int i = 0; i <= n; i++) { float a = a0 + (a1 - a0) * i / n; p.Add(new Vector2(x + Mathf.Cos(a) * R, y + Mathf.Sin(a) * R)); }
            return p;
        }
        static List<Vector2> RotBox(float x, float y, float a, float x0, float y0, float x1, float y1)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a); var p = new List<Vector2>();
            foreach (var q in new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) }) p.Add(new Vector2(x + c * q.x - s * q.y, y + s * q.x + c * q.y));
            return p;
        }
        static Vector2 Rt(float x, float y, float a, float lx, float ly) { float c = Mathf.Cos(a), s = Mathf.Sin(a); return new Vector2(x + c * lx - s * ly, y + s * lx + c * ly); }
        void DrawGroundFx(AldaraVfx V)
        {
            var gN = V.GroundN; var gA = V.GroundA; float now = Time.time; const float G = 1;
            // hazards: marked circles that fill before they go off, burning pools, shockwaves
            foreach (var h in hazards)
            {
                if (h.ring)
                {
                    float u = h.rr / Mathf.Max(1, h.r1);
                    AldaraVfx.Ring(gA, h.x, h.y, h.rr, h.rr, h.w, Al(h.col, 0.35f * (1 - u * 0.6f)), G); AldaraVfx.Ring(gA, h.x, h.y, h.rr + h.w * 0.3f, h.rr + h.w * 0.3f, 2.5f, Al(h.c2, 0.85f * (1 - u * 0.5f)), G); continue;
                }
                if (h.delay > 0)
                {
                    float k = 1 - h.delay / (h.max > 0 ? h.max : 1), pul = 0.5f + 0.5f * Mathf.Sin(now * 14 + h.x);
                    AldaraVfx.Circle(gN, h.x, h.y, h.r, Al(h.col, 0.1f + 0.1f * k), G); AldaraVfx.Circle(gN, h.x, h.y, h.r * AldaraVfx.EIO(k), Al(h.col, 0.22f + 0.12f * k), G);
                    AldaraVfx.Ring(gN, h.x, h.y, h.r, h.r, 2 + k * 1.5f, Al(h.col, 0.6f + 0.3f * pul * k), G);
                    if (k > 0.75f) AldaraVfx.Glow(gA, h.x, h.y, h.r * 1.1f, h.col, (k - 0.75f) * 1.4f, G);
                }
                else if (h.pool)
                {
                    float a = Mathf.Min(1, h.dur / 0.6f);
                    gN.Glow(h.x, h.y, h.r, h.r, new[] { 0, 0.35f, 1 }, new[] { Hx("#fff0b0", 0.55f * a), Al(h.col, 0.6f * a), Al(h.col, 0) }, G, 24);
                    AldaraVfx.Ring(gN, h.x, h.y, h.r * 0.92f, h.r * 0.92f, 2, Al(h.col, (0.3f + 0.15f * Mathf.Sin(now * 6 + h.x)) * a), G);
                }
            }
            // telegraphs for monsters winding up big moves
            foreach (var m in all)
            {
                var A = m.act; if (m.dead || A == null || !m.view || !m.view.activeSelf) continue; var mv = A.mv; if (!(mv.tele > 0)) continue;
                float W = mv.wind; bool inW = A.t < W, lp = mv.move == "leap" && A.t < W + mv.act; if (!inW && !lp && !(mv.chan > 0 && A.t < W + mv.act)) continue;
                float p = Mathf.Min(1, A.t / W), a = A.a, cs = Mathf.Cos(a), sn = Mathf.Sin(a), pul = 0.5f + 0.5f * Mathf.Sin(now * 16); var col = TeleCol(mv);
                System.Action<List<Vector2>> edge = pts => { gN.Polyline(pts, 2, Al(col, 0.55f + 0.35f * pul * p), true, G); gN.Polyline(pts, 7, Al(col, 0.14f), true, G); };
                switch (mv.shape)
                {
                    case "arc":
                        {
                            float R = m.r + mv.reach; var s = Sector(m.x, m.y, R, a - mv.arc, a + mv.arc); gN.Poly(s, Al(col, 0.1f + 0.08f * p), G); edge(s);
                            gN.Poly(Sector(m.x, m.y, R * AldaraVfx.EIO(p), a - mv.arc, a + mv.arc), Al(col, 0.24f), G); break;
                        }
                    case "line":
                        {
                            float L = m.r + mv.reach, w = mv.wid > 0 ? mv.wid : 20; var b = RotBox(m.x, m.y, a, 0, -w, L, w); gN.Poly(b, Al(col, 0.1f + 0.08f * p), G); edge(b);
                            gN.Poly(RotBox(m.x, m.y, a, 0, -w, L * AldaraVfx.EIO(p), w), Al(col, 0.24f), G); break;
                        }
                    case "circle":
                        {
                            float R = mv.rad >= 0 ? mv.rad : m.r + mv.reach, fw = Mathf.Max(0, mv.fwd), cx = mv.move == "leap" ? A.tx : m.x + cs * fw, cy = mv.move == "leap" ? A.ty : m.y + sn * fw, pp = mv.move == "leap" ? Mathf.Min(1, A.t / (W + mv.act)) : p;
                            AldaraVfx.Circle(gN, cx, cy, R, Al(col, 0.1f + 0.08f * pp), G);
                            AldaraVfx.Ring(gN, cx, cy, R, R, 2, Al(col, 0.55f + 0.35f * pul * p), G); AldaraVfx.Ring(gN, cx, cy, R, R, 7, Al(col, 0.14f), G);
                            AldaraVfx.Circle(gN, cx, cy, R * AldaraVfx.EIO(pp), Al(col, 0.24f), G);
                            if (mv.move == "leap") { gN.Line(cx - R * 0.3f, cy, cx + R * 0.3f, cy, 2, Al(col, 0.7f), G); gN.Line(cx, cy - R * 0.3f, cx, cy + R * 0.3f, 2, Al(col, 0.7f), G); }
                            break;
                        }
                    case "dash":
                        {
                            float L = Mathf.Min(mv.spd * mv.act, MvMax(m, mv) + 80), w = m.r * 0.9f; var b = RotBox(m.x, m.y, a, 0, -w, L, w); gN.Poly(b, Al(col, 0.1f + 0.1f * p), G); gN.Polyline(b, 2, Al(col, 0.6f), true, G);
                            for (int q = 0; q < 4; q++) { float x0 = ((q / 4f + now * 1.5f) % 1) * L; gN.Poly(new[] { Rt(m.x, m.y, a, x0, -w * 0.6f), Rt(m.x, m.y, a, x0 + w * 0.7f, G), Rt(m.x, m.y, a, x0, w * 0.6f), Rt(m.x, m.y, a, x0 + w * 0.25f, G) }, Al(col, 0.35f + 0.3f * pul), G); }
                            break;
                        }
                    case "cone": { var s = Sector(m.x, m.y, mv.len, a - mv.arc, a + mv.arc); gN.Poly(s, Al(col, inW ? 0.1f + 0.1f * p : 0.08f), G); if (inW) edge(s); break; }
                    case "beam":
                        {
                            if (!inW) break; float L = BeamLen(m, a, mv.len);
                            for (float d = 0; d < L; d += 18) { float e2 = Mathf.Min(L, d + 10); gN.Line(m.x + cs * d, m.y + sn * d, m.x + cs * e2, m.y + sn * e2, 1.5f + p * 2, Al(col, 0.3f + 0.5f * p * pul), G); }
                            break;
                        }
                }
            }
            // where lobbed shots will land
            foreach (var e in eproj) if (e.lob)
                {
                    float k = Mathf.Min(1, e.t / e.T); AldaraVfx.Circle(gN, e.tx, e.ty, e.aoe, Al(e.col, 0.1f + 0.15f * k), G);
                    AldaraVfx.Ring(gN, e.tx, e.ty, e.aoe, e.aoe, 2, Al(e.col, 0.5f + 0.4f * k), G); AldaraVfx.Circle(gN, e.tx, e.ty, e.aoe * k, Al(e.col, 0.25f), G);
                }
            // a gold ring marks bosses; slowed monsters stand in a frost ring
            foreach (var m in all)
            {
                if (m.dead || !m.view || !m.view.activeSelf) continue;
                if (m.boss) AldaraVfx.Ring(gN, m.x, m.y + m.r * 0.95f, m.r * 1.2f, m.r * 0.35f, 2.5f, Hx("#e0b64b", 0.8f), G);
                if (m.slowT > 0) AldaraVfx.Ring(gN, m.x, m.y + m.r * 0.95f, m.r * 0.9f, m.r * 0.3f, 2, Hx("#8fdfff"), G);
            }
            // enraged monsters burn with a red aura
            foreach (var m in all) if (!m.dead && m.enrT > 0 && m.view && m.view.activeSelf) AldaraVfx.Glow(gA, m.x, m.y + m.r * 0.8f, m.r * 1.8f, Hx("#ff3a1a"), 0.3f + 0.1f * Mathf.Sin(now * 9), G);
        }
        void DrawAirFx(AldaraVfx V)
        {
            var aN = V.AirN; var aA = V.AirA; float now = Time.time;
            // wind-up glow in the casting hand, beams and breath
            foreach (var m in all)
            {
                var A = m.act; if (m.dead || A == null || !m.view || !m.view.activeSelf) continue; var mv = A.mv; var E = Elc(mv.el); float h = Chest(m), bz = LZ(m.x, m.y), gy = m.y + 15 * m.r / 16 - m.lift;
                if (mv.cast > 0 && A.t < mv.wind + mv.act * 0.5f)
                {
                    float p = Mathf.Min(1, A.t / mv.wind), up = mv.pose == "summon" ? 1.9f : 1.15f, hx = m.x + Mathf.Cos(A.a) * m.r * 0.55f, hy = gy - 15 * m.r / 16 - h * up + Mathf.Sin(A.a) * m.r * 0.3f;
                    AldaraVfx.Glow(aA, hx, hy, 10 + p * 22, E[0], 0.35f + 0.5f * p, bz); AldaraVfx.Circle(aA, hx, hy, 2 + p * 4, Al(E[1], 0.5f + 0.5f * p), bz);
                    for (int q = 0; q < 5; q++) { float aa = now * 5 + q * 1.26f, rr = (1 - ((now * 1.8f + q * 0.2f) % 1)) * 28; AldaraVfx.Circle(aA, hx + Mathf.Cos(aa) * rr, hy + Mathf.Sin(aa) * rr * 0.7f, 1.6f, Al(E[0], 0.8f), bz); }
                }
                if (mv.shape == "beam" && A.t >= mv.wind && A.t < mv.wind + mv.act)
                {
                    float L = BeamLen(m, A.a, mv.len), cs = Mathf.Cos(A.a), sn = Mathf.Sin(A.a), x0 = m.x + cs * m.r * 0.7f, y0 = gy - 15 * m.r / 16 - h * 1.1f + sn * m.r * 0.3f, x1 = m.x + cs * L, y1 = m.y + sn * L - h * 0.6f;
                    float fl = 0.85f + 0.15f * Mathf.Sin(now * 47), k = Mathf.Min(1, (A.t - mv.wind) / 0.12f) * Mathf.Min(1, (mv.wind + mv.act - A.t) / 0.12f);
                    aA.Line(x0, y0, x1, y1, mv.wid * 3 * fl, Al(E[0], 0.25f * k), bz); aA.Line(x0, y0, x1, y1, mv.wid * 1.2f * fl, Al(E[0], 0.7f * k), bz); aA.Line(x0, y0, x1, y1, mv.wid * 0.4f, Al(E[1], 0.95f * k), bz);
                    AldaraVfx.Glow(aA, x0, y0, 26, E[0], 0.8f * k, bz); AldaraVfx.Glow(aA, x1, y1, 34 * fl, E[0], 0.8f * k, bz);
                }
            }
            // falling icicles, meteors, arrows and light over marked circles
            foreach (var h in hazards)
            {
                if (!(h.delay > 0)) continue; float k = 1 - h.delay / (h.max > 0 ? h.max : 1), bz = LZ(h.x, h.y);
                if ((h.icicle || h.fx == "icicle") && k > 0.45f)
                {
                    float f = (k - 0.45f) / 0.55f, y = h.y - (1 - f * f) * 420;
                    aN.Tri(new Vector2(h.x - 9, y - 46), new Vector2(h.x + 9, y - 46), new Vector2(h.x, y), Hx("#e8f8ff"), bz); aN.Tri(new Vector2(h.x, y - 46), new Vector2(h.x + 9, y - 46), new Vector2(h.x, y), Hx("#9fd8ff"), bz);
                }
                else if (h.fx == "meteor" && k > 0.35f)
                {
                    float f = (k - 0.35f) / 0.65f, ff = f * f, x = h.x - (1 - ff) * 260, y = h.y - (1 - ff) * 520;
                    for (int q = 1; q < 6; q++) { float qq = Mathf.Max(0, ff - q * 0.05f); AldaraVfx.Glow(aA, h.x - (1 - qq) * 260, h.y - (1 - qq) * 520, 18 - q * 2, Hx(q < 2 ? "#ffd35a" : "#ff5a1a"), 0.5f, bz); }
                    AldaraVfx.Glow(aA, x, y, 30, Hx("#ff8a2a"), 0.9f, bz); AldaraVfx.Circle(aN, x, y, 9, Hx("#2a1a10"), bz); AldaraVfx.Circle(aN, x + 2, y + 2, 4, Hx("#ffb04a"), bz);
                }
                else if (h.fx == "arrow" && k > 0.55f)
                {
                    float f = (k - 0.55f) / 0.45f;
                    for (int q = 0; q < 4; q++) { float ox = ((q * 37) % 30) - 15, oy = ((q * 53) % 24) - 12, y = h.y + oy - (1 - f) * 380; aN.Line(h.x + ox + (1 - f) * 60, y - 18, h.x + ox + (1 - f) * 60 - 3, y, 2, Hx("#6a5030"), bz); }
                }
                else if (h.fx == "holy" && k > 0.5f) { float f = (k - 0.5f) / 0.5f; aA.Rect(h.x - 3 - f * 4, h.y - 420, 6 + f * 8, 420, Al(h.col, 0.25f * f), bz); }
                else if (h.fx == "void" && k > 0.3f)
                {
                    for (int q = 0; q < 3; q++) { float rr = h.r * (0.3f + q * 0.25f) * (1.3f - k * 0.3f), a0 = q * 2 + now * 4; aN.Arc(h.x, h.y, rr, rr * 0.5f, a0, a0 + 2.4f, 2, Al(h.col, 0.6f * k), bz); }
                }
            }
            foreach (var e in eproj) DrawProj(aN, aA, e);
            // stunned monsters: three stars circling over the head
            float st = Time.time * 1000 / 200;
            foreach (var m in all)
            {
                if (m.dead || !(m.stunT > 0) || !m.view || !m.view.activeSelf || m.stunT > 100) continue; float top = m.y + 15 * m.r / 16 - m.lift - Chest(m) / 0.55f + 4, bz = LZ(m.x, m.y);
                for (int j = 0; j < 3; j++) { float a = st + j * 2.1f; AldaraVfx.Circle(aN, m.x + Mathf.Cos(a) * m.r * 0.7f, top + 2 + Mathf.Sin(a) * 3, 2.5f, Hx("#ffe07a"), bz); }
            }
        }
        static void DrawProj(AldaraSketch aN, AldaraSketch aA, Proj e)
        {
            float h = e.h, x = e.x, y = e.y - h, ang = Mathf.Atan2(e.vy, e.vx), bz = LZ(e.x, e.y); Color col = e.col, c2 = e.c2;
            if (h > 2) aN.Ellipse(e.x, e.y, 6, 2.5f, new Color(0, 0, 0, 0.3f), bz, 0, 12);
            System.Action<float, float> trail = (w, al) =>
            {
                if (e.tr == null || e.tr.Count < 2) return;
                for (int i = 1; i < e.tr.Count; i++) { float u = i / (float)e.tr.Count; aA.Line(e.tr[i - 1].x, e.tr[i - 1].y - (e.lob ? 0 : h), e.tr[i].x, e.tr[i].y - (e.lob ? 0 : h), w * u, Al(col, al * u), bz); }
            };
            System.Func<float, float, float, Vector2> T = (lx, ly, a) => Rt(x, y, a, lx, ly);
            switch (e.look)
            {
                case "arrow": case "spear": case "lance":
                    {
                        float L = e.look == "arrow" ? 20 : e.look == "spear" ? 34 : 40; trail(e.look == "lance" ? 6 : 2, e.look == "lance" ? 0.6f : 0.35f);
                        if (e.look == "lance") AldaraVfx.Glow(aA, x, y, 26, col, 0.6f, bz);
                        var p0 = T(-L * 0.6f, 0, ang); var p1 = T(L * 0.4f, 0, ang); aN.Line(p0.x, p0.y, p1.x, p1.y, e.look == "arrow" ? 2 : 3, e.look == "lance" ? c2 : Hx("#6a5030"), bz);
                        aN.Tri(T(L * 0.4f + 8, 0, ang), T(L * 0.4f - 2, -4, ang), T(L * 0.4f - 2, 4, ang), e.look == "lance" ? col : Hx("#d8d8e0"), bz);
                        if (e.look == "arrow") aN.Poly(new[] { T(-L * 0.6f, 0, ang), T(-L * 0.6f - 6, -4, ang), T(-L * 0.6f + 2, 0, ang), T(-L * 0.6f - 6, 4, ang) }, Hx("#e8e0d0"), bz);
                        break;
                    }
                case "dagger": case "thorn": case "feather":
                    {
                        trail(2, 0.3f); float r = e.look == "dagger" ? e.rot : ang;
                        if (e.look == "dagger") { aN.Tri(T(9, 0, r), T(-2, -3, r), T(-2, 3, r), Hx("#d8dce8"), bz); aN.Poly(new[] { T(-7, -1.5f, r), T(-2, -1.5f, r), T(-2, 1.5f, r), T(-7, 1.5f, r) }, Hx("#4a3020"), bz); }
                        else if (e.look == "thorn") { aN.Poly(new[] { T(12, 0, r), T(-6, -4, r), T(-3, 0, r), T(-6, 4, r) }, Hx("#3a5a2a"), bz); aN.Tri(T(12, 0, r), T(2, -1.5f, r), T(2, 1.5f, r), Hx("#9ae06a"), bz); }
                        else { r += Mathf.Sin(e.rot * 0.7f) * 0.3f; aN.Ellipse(x, y, 10, 3.5f, Hx("#f4f4ff"), bz, r, 12); var a0 = T(-10, 0, r); var a1 = T(10, 0, r); aN.Line(a0.x, a0.y, a1.x, a1.y, 1, Hx("#a8b8d8"), bz); }
                        break;
                    }
                case "shard": case "ice":
                    {
                        if (!e.lob) trail(3, 0.4f); float r = e.lob ? e.rot : ang; AldaraVfx.Glow(aA, x, y, 16, col, 0.5f, bz);
                        float s = e.lob ? 1.6f : 1; aN.Poly(new[] { T(12 * s, 0, r), T(0, -4 * s, r), T(-8 * s, 0, r), T(0, 4 * s, r) }, Hx("#e8f8ff"), bz); aN.Poly(new[] { T(12 * s, 0, r), T(0, 1 * s, r), T(-8 * s, 0, r), T(0, 4 * s, r) }, col, bz);
                        break;
                    }
                case "rock": case "magma":
                    {
                        float r = 11; var pts = new List<Vector2>(); for (int i = 0; i < 7; i++) { float a = i / 7f * PI * 2, rr = r * (0.75f + ((i * 37) % 10) / 28f); pts.Add(T(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, e.rot)); }
                        aN.Poly(pts, Hx(e.look == "magma" ? "#2a1a14" : "#6a5a48"), bz);
                        if (e.look == "magma")
                        {
                            AldaraVfx.Glow(aA, x, y, 22, Hx("#ff6a1a"), 0.7f, bz); var cl = Hx("#ffb04a");
                            aA.Polyline(new[] { T(-5, -3, e.rot), T(2, 1, e.rot), T(6, -2, e.rot) }, 1.5f, cl, false, bz); var q0 = T(-2, 5, e.rot); var q1 = T(1, 1, e.rot); aA.Line(q0.x, q0.y, q1.x, q1.y, 1.5f, cl, bz);
                        }
                        else { var c0 = T(-3, -3, e.rot); AldaraVfx.Circle(aN, c0.x, c0.y, 4, Hx("#8a7a66"), bz); }
                        break;
                    }
                case "glob": trail(5, 0.35f); AldaraVfx.Glow(aA, x, y, 16, col, 0.5f, bz); AldaraVfx.Circle(aN, x, y, 6 + Mathf.Sin(e.rot) * 0.8f, col, bz); AldaraVfx.Circle(aN, x - 2, y - 2, 2, c2, bz); break;
                case "star":
                    {
                        trail(4, 0.5f); AldaraVfx.Glow(aA, x, y, 20, col, 0.7f, bz); var pts = new List<Vector2>(); for (int i = 0; i < 8; i++) { float a = i / 8f * PI * 2, r = i % 2 == 1 ? 3 : 10; pts.Add(T(Mathf.Cos(a) * r, Mathf.Sin(a) * r, e.rot * 0.5f)); }
                        aA.Poly(pts, c2, bz); break;
                    }
                case "skull":
                    AldaraVfx.Glow(aA, x, y, 20, col, 0.7f, bz); AldaraVfx.Circle(aN, x, y - 1, 6, Hx("#e8f0d8"), bz); aN.Rect(x - 3.5f, y + 2, 7, 4, Hx("#e8f0d8"), bz);
                    AldaraVfx.Circle(aN, x - 2.2f, y - 1, 1.6f, Hx("#0a1a0a"), bz); AldaraVfx.Circle(aN, x + 2.2f, y - 1, 1.6f, Hx("#0a1a0a"), bz); break;
                case "flamewave":
                    {
                        for (int i = -3; i <= 3; i++) { float fl = 8 + 6 * Mathf.Sin(e.rot * 2 + i); var q = Rt(e.x, e.y, ang, 0, i * 8); AldaraVfx.Glow(aA, q.x, q.y, fl + 8, col, 0.5f, bz); }
                        var c0 = Rt(e.x, e.y, ang, 4, 0); aA.Ellipse(c0.x, c0.y, 5, 24, Al(c2, 0.8f), bz, ang, 16); break;
                    }
                default:
                    {
                        bool fire = e.look == "fire"; trail(6, 0.45f); AldaraVfx.Glow(aA, x, y, fire ? 20 : 16, col, 0.75f, bz); AldaraVfx.Circle(aA, x, y, fire ? 5 : 4, c2, bz);
                        AldaraVfx.Circle(aA, x, y, fire ? 7 : 5.5f, Al(col, 0.9f), bz); AldaraVfx.Circle(aA, x, y, 2.8f, c2, bz); break;
                    }
            }
        }

        // ---- death: the body topples (or dissolves upward for spirits), hits the ground and fades into motes (spawnCorpse / drawCorpse) ----
        class Corpse { public GameObject go; public Material[] fades; public Vector3 pos; public float x, y, dir, t, T, T1, fade, len, h; public bool fl, big, landed, faded; public Color c; }
        readonly List<Corpse> corpses = new List<Corpse>();
        void SpawnCorpse(Mon t)
        {
            var go = t.view; if (!go || !go.activeSelf) { Hide(t); AldaraVfx.Burst(t.x, t.y, Hx(t.def.color ?? "#ffffff"), 14, 160); return; }
            t.view = null; var anim = t.anim; t.anim = null; t.model = null; if (anim) { anim.ClearAction(); anim.Hurt(); }
            bool fl = t.def.floater != 0 || t.def.flyer != 0, big = t.boss; float dir = t.x >= AldaraPlayer.I.x ? 1 : -1;
            float hh = Chest(t) / 0.55f;
            var k = new Corpse { go = go, pos = go.transform.position, x = t.x, y = t.y + 15 * t.r / 16, dir = dir, T = fl ? 1.1f : (big ? 3.2f : 2.3f), T1 = big ? 0.7f : 0.45f, fade = fl ? 0.15f : (big ? 1.9f : 1.25f), fl = fl, big = big, len = hh * 0.7f, h = hh, c = Hx(t.def.color ?? "#ffffff") };
            corpses.Add(k); if (corpses.Count > 14) { var o = corpses[0]; if (o.go) Destroy(o.go); corpses.RemoveAt(0); }
            AldaraVfx.Burst(t.x, t.y, k.c, big ? 30 : 12, big ? 260 : 170);
        }
        void UpdateCorpses(float dt)
        {
            var cam = Camera.main;
            for (int i = corpses.Count - 1; i >= 0; i--)
            {
                var k = corpses[i]; k.t += dt; if (!k.go) { corpses.RemoveAt(i); continue; }
                if (k.t >= k.T) { Destroy(k.go); corpses.RemoveAt(i); continue; }
                if (!k.fl && k.t > k.T1 && !k.landed) { k.landed = true; for (int q = 0; q < 4; q++) Mfx(new AldaraVfx.Mf { k = "dust", x = k.x + k.dir * k.len * (0.2f + q * 0.2f), y = k.y, r = k.len * 0.35f, T = 0.7f }); if (k.big) Shake(6, 0.4f); }
                else if (k.t > k.fade && Random.value < dt * (k.big ? 40 : 18)) { float u = Random.value; AldaraVfx.Mpush(new AldaraVfx.Mp { x = k.x + (k.fl ? (Random.value - 0.5f) * k.len : k.dir * k.len * u), y = k.y - (k.fl ? Random.value * k.len : 4), z = Random.value * 8, vx = (Random.value - 0.5f) * 14, vz = 30 + Random.value * 50, life = 0.9f, max = 0.9f, c = k.c, s = 2 + Random.value * 2, glow = true }); }
                float a = 1; if (k.t > k.fade) a = Mathf.Max(0, 1 - (k.t - k.fade) / (k.T - k.fade));
                if (a < 1 && !k.faded)
                {   // from here it is drawn see-through: a depth pass, then the colours at the alpha
                    k.faded = true; var rs = k.go.GetComponentsInChildren<Renderer>(); var list = new List<Material>();
                    foreach (var r in rs) { var m0 = r.sharedMaterial; var f = new Material(AldaraOccluders.FadeOf(m0)); list.Add(f); r.sharedMaterials = new[] { AldaraOccluders.Prime, f }; }
                    k.fades = list.ToArray(); var an = k.go.GetComponentInChildren<AldaraMonsterAnimator>(); if (an) an.enabled = false;
                }
                if (k.fades != null) foreach (var f in k.fades) f.SetFloat("_Alpha", a);
                var tr = k.go.transform;
                if (k.fl)
                {
                    float u = Mathf.Min(1, k.t / k.T), s = 1 - u * 0.5f; tr.position = k.pos + Vector3.up * (u * 36 / AldaraWorld.PX);
                    tr.localScale = Vector3.Scale(AldaraView.Squash, new Vector3(s * (1 + u * 0.2f), s, s * (1 + u * 0.2f)));
                }
                else
                {
                    float p = Mathf.Min(1, k.t / k.T1), th = p * p; if (k.t > k.T1) th = 1 - 0.07f * Mathf.Sin((k.t - k.T1) * 20) * Mathf.Exp(-(k.t - k.T1) * 7);
                    float sink = k.t > k.fade ? (k.t - k.fade) * 6 : 0;
                    var axis = cam ? cam.transform.forward : new Vector3(0, -0.7071f, 0.7071f);
                    tr.rotation = Quaternion.AngleAxis(k.dir * 1.38f * th * Mathf.Rad2Deg, axis);
                    tr.position = k.pos + Vector3.down * (sink / AldaraWorld.PX);
                }
            }
        }
    }
}
