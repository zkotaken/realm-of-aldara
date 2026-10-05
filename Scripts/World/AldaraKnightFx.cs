using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // The knight's enhanced effects (Graphics, Enhanced effects): real 3D sword trails that sweep round the hero with a
    // white-hot edge, ground shockwaves, glowing cracks torn in the earth, turning holy sigils, star flares on every
    // blow, shafts of light, a great sword of light that falls on Judgement, a golden hex ward for Iron Skin, flame
    // auras for the war shouts and rising spirals for Second Wind. Each also lights the world (AldaraVfxPlus flashes)
    // and throws sparks, shards, embers and dust. With the option off the browser's flat effects are used instead.
    public class AldaraKnightFx : MonoBehaviour
    {
        public static AldaraKnightFx I;
        public static bool On { get { var H = AldaraHero.I; return H && H.cls == "knight" && AldaraVfxPlus.On && AldaraWorld.Loaded; } }
        /// the enhanced effects are running at all (any class)
        public static bool Live { get { return AldaraHero.I && AldaraVfxPlus.On && AldaraWorld.Loaded; } }
        static AldaraKnightFx V { get { if (!I) { var go = new GameObject("AldaraKnightFx"); I = go.AddComponent<AldaraKnightFx>(); } return I; } }
        public static void Ensure() { if (!I) { var v = V; } }

        // atlas cells
        public const int C_SLASH = 0, C_RING = 1, C_CRACK = 2, C_RUNE = 3, C_FLARE = 4, C_BEAM = 5, C_SWORD = 6, C_HEX = 7, C_FLAME = 8, C_DOT = 9, C_BAND = 10, C_WIND = 11, C_SHARD = 12;
        public enum K { Slash, Ring, Wall, Decal, Flare, Beam, Sword, Dome, Flame, Helix, Spin, Line, Plate, Orb, Bolt, Shard, Fall }
        public class Fx
        {
            public K k; public float t, life, delay, r, r1, w, span, tilt, dir = 1, h, ph, spin, bright = 1, sweep = 0.11f, size, hot = 1, alpha = 1;
            public bool follow, vert, sparked, based, combo, ended; public Vector3 at, fwd, p1, e1, e2; public Color c, c2; public int cell, n;
            public List<Vector3> pts; public System.Action done;
        }
        readonly List<Fx> fx = new List<Fx>();
        static Fx Add(Fx f) { var v = V; if (v.fx.Count < 200) v.fx.Add(f); return f; }

        // ---------- places, from the browser's px ----------
        public static Vector3 G(float x, float y) { var u = AldaraWorld.ToUnity(x, y); if (AldaraWorld.Dun) u.y = 0; return u; }
        public static Vector3 Fwd(float a) { var d = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a)); return d; }
        static Vector3 Hero() { var P = AldaraPlayer.I; return P ? G(P.x, P.y) : Vector3.zero; }
        static float U(float px) { return px / AldaraWorld.PX; }
        static Color Hx(string h) { return AldaraRules.Hex(h); }
        static float R() { return Random.value; }
        static float EOut(float x) { x = Mathf.Clamp01(x); return 1 - (1 - x) * (1 - x) * (1 - x); }
        static float EIn(float x) { x = Mathf.Clamp01(x); return x * x * x; }
        static Color Hot(Color c, float k) { return new Color(Mathf.Lerp(c.r, 1, k), Mathf.Lerp(c.g, 1, k), Mathf.Lerp(c.b, 1, k), 1); }

        // ---------- the pieces ----------
        /// a sword trail: an arc of radius r round 'at' (+h up), sweeping across the front of 'fwd'; tilt rolls the arc's
        /// plane; vert makes it an overhead chop (from ph radians above the front down through span)
        public static Fx Slash(Vector3 at, Vector3 fwd, float h, float r, float span, float dir, float tilt, Color c, float w = 0.45f, float life = 0.34f, float delay = 0, float bright = 1.7f, bool follow = true, int cell = C_SLASH)
        { return Add(new Fx { k = K.Slash, at = at, fwd = fwd, h = h, r = r, span = span, dir = dir, tilt = tilt, c = c, w = w, life = life, delay = delay, bright = bright, follow = follow, cell = cell, sweep = Mathf.Min(0.12f, life * 0.36f) }); }
        public static Fx Chop(Vector3 at, Vector3 fwd, float h, float r, Color c, float w = 0.45f, float life = 0.34f, float delay = 0, float bright = 1.8f)
        { var f = Slash(at, fwd, h, r, 2.6f, 1, 0, c, w, life, delay, bright); f.vert = true; f.ph = 2.05f; return f; }
        public static Fx Ring(Vector3 at, float r0, float r1, float w, Color c, float life, float delay = 0, float bright = 1.4f, int cell = C_RING)
        { return Add(new Fx { k = K.Ring, at = at, r = r0, r1 = r1, w = w, c = c, life = life, delay = delay, bright = bright, cell = cell }); }
        public static Fx Wall(Vector3 at, float r0, float r1, float h, Color c, float life, float delay = 0, float bright = 1.5f)
        { return Add(new Fx { k = K.Wall, at = at, r = r0, r1 = r1, h = h, c = c, life = life, delay = delay, bright = bright }); }
        public static Fx Decal(Vector3 at, int cell, float size, Color c, Color hot, float life, float spin = 0, float delay = 0, float bright = 1.5f, bool follow = false)
        { return Add(new Fx { k = K.Decal, at = at, cell = cell, size = size, c = c, c2 = hot, life = life, spin = spin, delay = delay, bright = bright, ph = R() * 6.283f, follow = follow }); }
        public static Fx Flare(Vector3 at, float size, Color c, float life = 0.18f, float delay = 0, float bright = 2f)
        { return Add(new Fx { k = K.Flare, at = at, size = size, c = c, life = life, delay = delay, bright = bright, ph = (R() - 0.5f) * 0.6f, cell = C_FLARE }); }
        /// a soft round flash (a fireball's burst)
        public static Fx Glow(Vector3 at, float size, Color c, float life = 0.3f, float delay = 0, float bright = 1.6f)
        { return Add(new Fx { k = K.Flare, at = at, size = size, c = c, life = life, delay = delay, bright = bright, cell = C_DOT }); }
        /// a glowing shot in flight: n 0 spark, 1 fire, 2 ice, 3 arcane; moved by its owner with Move, finished with End
        public static Fx Orb(Vector3 at, float size, Color c, int n)
        { return Add(new Fx { k = K.Orb, at = at, size = size, c = c, c2 = Hot(c, 0.75f), n = n, life = 999, bright = 1.8f, w = size * 1.1f, pts = new List<Vector3> { at }, ph = R() * 6.28f }); }
        public static void Move(Fx f, Vector3 p) { if (f == null || f.ended) return; if (f.pts.Count == 0 || (f.pts[f.pts.Count - 1] - p).sqrMagnitude > 0.0025f) { f.pts.Add(p); if (f.pts.Count > 16) f.pts.RemoveAt(0); } f.fwd = p - f.at; f.at = p; }
        public static void End(Fx f) { if (f == null || f.ended) return; f.ended = true; f.life = f.t + 0.22f; }
        /// lightning along nodes (start, then each target), forking and re-striking as it lives
        public static Fx Bolt(List<Vector3> nodes, Color c, float life = 0.4f, float w = 0.1f)
        { return Add(new Fx { k = K.Bolt, pts = nodes, c = c, c2 = Hot(c, 0.8f), life = life, w = w, bright = 2.2f, at = nodes[0] }); }
        /// an ice crystal bursting out of the ground, leaning along lean
        public static Fx Shard(Vector3 at, Vector3 lean, float h, float w, Color c, float life, float delay = 0)
        { return Add(new Fx { k = K.Shard, at = at, fwd = lean, h = h, w = w, c = c, life = life, delay = delay, bright = 1.4f, cell = C_SHARD }); }
        /// something falling from the sky onto 'to' over 'fall' seconds (a meteor, ice), calling done as it lands
        public static Fx Fall(Vector3 from, Vector3 to, float fall, float size, Color c, int n, System.Action done = null, float delay = 0)
        { return Add(new Fx { k = K.Fall, at = from, p1 = from, fwd = to, sweep = fall, size = size, c = c, c2 = Hot(c, 0.75f), n = n, life = fall + 0.25f, delay = delay, bright = 1.9f, w = size * 1.2f, pts = new List<Vector3>(), done = done, ph = R() * 6.28f }); }
        public static Fx Beam(Vector3 at, float r, float h, Color c, float life, float delay = 0, float bright = 1.5f)
        { return Add(new Fx { k = K.Beam, at = at, r = r, h = h, c = c, life = life, delay = delay, bright = bright }); }
        public static Fx Sword(Vector3 at, float size, Color c, float fall, float life, float delay = 0)
        { return Add(new Fx { k = K.Sword, at = at, size = size, c = c, sweep = fall, life = life, delay = delay, bright = 1.8f, fwd = Fwd(R() * 6.283f) }); }
        public static Fx Dome(float r, float h, Color c, float life, float delay = 0)
        { return Add(new Fx { k = K.Dome, follow = true, r = r, h = h, c = c, life = life, delay = delay, bright = 1.3f }); }
        public static Fx FlameAura(float r, float h, Color c, float life, float delay = 0)
        { return Add(new Fx { k = K.Flame, follow = true, r = r, h = h, c = c, life = life, delay = delay, bright = 1.3f }); }
        public static Fx Helix(float r, float h, int strands, Color c, float life, float delay = 0)
        { return Add(new Fx { k = K.Helix, follow = true, r = r, h = h, n = strands, c = c, life = life, delay = delay, bright = 1.6f, ph = R() * 6.283f }); }
        public static Fx Spin(float h, float r, float tilt, float a0, float speed, Color c, float life, float delay = 0, int cell = C_SLASH, float w = 0.4f, float bright = 1.7f)
        { return Add(new Fx { k = K.Spin, follow = true, h = h, r = r, tilt = tilt, ph = a0, spin = speed, c = c, life = life, delay = delay, cell = cell, w = w, bright = bright }); }
        public static Fx Line(Vector3 p0, Vector3 p1, float w, Color c, float life, float bright = 1.5f)
        { return Add(new Fx { k = K.Line, at = p0, p1 = p1, w = w, c = c, life = life, bright = bright }); }
        public static Fx Plate(Vector3 at, Vector3 fwd, float size, float push, Color c, float life, int cell = C_RUNE)
        { return Add(new Fx { k = K.Plate, at = at, fwd = fwd, size = size, r = push, c = c, life = life, cell = cell, bright = 1.6f }); }

        static void Light(Vector3 at, Color c, float i, float range, float life) { AldaraVfxPlus.Flash(at, Hot(c, 0.25f), i, range, life); }
        static void Later(float t, System.Action fn) { if (AldaraSkills.I) AldaraSkills.I.Later_(t, fn); else fn(); }
        static readonly Color ROCK = new Color(0.42f, 0.36f, 0.3f), DUST = new Color(0.45f, 0.4f, 0.33f, 0.45f);

        // ---------- the knight's moves ----------
        /// the three-hit combo: a falling diagonal, a rising backhand, then an overhead chop that cracks the ground
        public static void Combo(float tx, float ty, int cb, float dur, Color col)
        {
            var P = AldaraPlayer.I; var fw = Fwd(P.facing); var at = Hero();
            // a fast swing cuts the last one's trail short, so quick attacks do not pile up into a ball of light
            var list = V.fx; for (int i = list.Count - 1; i >= 0; i--) { var o = list[i]; if (!o.combo) continue; if (o.delay > 0) list.RemoveAt(i); else o.life = Mathf.Min(o.life, o.t + 0.07f); }
            int first = list.Count;
            float reach = Mathf.Max(44, Mathf.Min(90, Mathf.Sqrt((tx - P.x) * (tx - P.x) + (ty - P.y) * (ty - P.y)) + 18)), R0 = U(reach) * 1.18f, dl = dur * 0.38f;
            if (cb == 2)
            {
                Chop(at, fw, 1.15f, R0 * 0.95f, col, 0.55f, 0.38f, dl, 2.2f);
                Chop(at, fw, 1.15f, R0 * 1.08f, col, 0.12f, 0.3f, dl + 0.02f, 1.2f).cell = C_BAND;
                Later(dur * 0.56f, () =>
                {
                    var p = Hero() + Fwd(P.facing) * 1.2f; var hot = Hot(col, 0.6f);
                    Decal(p, C_CRACK, 1.1f, col, Color.white, 1.5f); Ring(p, 0.2f, 1.9f, 0.45f, hot, 0.4f); Flare(p + Vector3.up * 0.25f, 1.7f, hot, 0.2f);
                    AldaraVfxPlus.Shards(p + Vector3.up * 0.1f, ROCK, 10, 4.5f); AldaraVfxPlus.Dust(p, 0.8f, 6, DUST); AldaraVfxPlus.SparkBurst(p + Vector3.up * 0.2f, hot, 12, 5);
                    Light(p + Vector3.up * 0.8f, col, 2.2f, 5, 0.25f); AldaraVfx.Debris(P.x + Mathf.Cos(P.facing) * 38, P.y + Mathf.Sin(P.facing) * 38, 6, Hx("#6a5a4a"));
                });
            }
            else
            {
                float h = cb == 1 ? 0.72f : 1.0f, tilt = cb == 1 ? -0.38f : 0.48f, dir = cb == 1 ? -1 : 1;
                Slash(at, fw, h, R0, 2.3f, dir, tilt, col, 0.52f, 0.3f, dl, 2.1f);
                Slash(at, fw, h, R0 * 1.1f, 2.1f, dir, tilt, col, 0.12f, 0.26f, dl + 0.025f, 1.1f, true, C_BAND);
            }
            for (int i = first; i < list.Count; i++) list[i].combo = true;
        }
        /// every blow on a monster: a star flare and sparks at its chest
        public static void Hit(AldaraMonsters.Mon m, Color c)
        {
            var at = G(m.x, m.y) + Vector3.up * U(AldaraMonsters.Chest(m)); var hot = Hot(c, 0.5f);
            Flare(at, 0.9f + R() * 0.35f, hot, 0.16f, 0, 2.2f); AldaraVfxPlus.SparkBurst(at, hot, 8, 4.5f);
        }

        public static bool Skill(string id, AldaraMonsters.Mon t, float tx, float ty, SkillDef sk, string ph)
        {
            var P = AldaraPlayer.I; float a = P.facing, rad = sk != null ? sk.radius : 0; var fw = Fwd(a); var hero = Hero();
            float th = t != null ? U(AldaraMonsters.Chest(t)) : 0.6f; var tg = G(tx, ty); var trail = AldaraSkillFx.TrailC;
            Color gold = Hx("#ffd35a"), pale = Hx("#fff0c0"), white = Color.white;
            switch (id)
            {
                case "power_strike":
                    {
                        float d = U(Mathf.Sqrt((tx - P.x) * (tx - P.x) + (ty - P.y) * (ty - P.y)) + 30);
                        Slash(hero, fw, 0.9f, d, 2.8f, 1, 0.3f, gold, 0.55f, 0.42f, 0.08f, 2.3f);
                        Slash(hero, fw, 0.9f, d * 1.12f, 2.6f, 1, 0.3f, trail, 0.16f, 0.36f, 0.11f, 1.3f, true, C_BAND);
                        Later(0.14f, () =>
                        {
                            var p = tg + Vector3.up * th; Flare(p, 2.6f, pale, 0.26f, 0, 2.4f); Flare(p, 1.2f, white, 0.12f, 0, 2.6f);
                            Ring(tg, 0.3f, 2.4f, 0.55f, gold, 0.45f); Wall(tg, 0.3f, 1.7f, 0.9f, gold, 0.35f);
                            AldaraVfxPlus.SparkBurst(p, pale, 22, 7); Light(p + Vector3.up * 0.5f, gold, 3.4f, 7, 0.35f); AldaraVfx.Shake(4, 0.2f);
                        });
                        return true;
                    }
                case "shield_bash":
                    {
                        var c = Hx("#cfe0ff");
                        Plate(hero + Vector3.up * 0.85f + fw * 0.5f, fw, 1.0f, 1.3f, c, 0.3f, C_RUNE); Plate(hero + Vector3.up * 0.85f + fw * 0.5f, fw, 1.5f, 1.1f, c, 0.26f, C_DOT);
                        Later(0.08f, () =>
                        {
                            var p = tg + Vector3.up * th; Flare(p, 2.0f, white, 0.22f, 0, 2.4f); Ring(tg, 0.2f, 1.5f, 0.4f, gold, 0.32f);
                            AldaraVfxPlus.SparkBurst(p, white, 16, 6); Light(p, c, 2.6f, 5, 0.25f); AldaraVfx.Shake(3, 0.18f);
                        });
                        return true;
                    }
                case "ground_slam":
                    Later(0.18f, () => Quake(tg, U(rad), Hx("#ffb04a"), pale, 6));
                    return true;
                case "cleave":
                    {
                        float d = U(Mathf.Sqrt((tx - P.x) * (tx - P.x) + (ty - P.y) * (ty - P.y)) + rad * 0.7f);
                        Slash(hero, fw, 0.75f, d, 3.6f, 1, 0.14f, trail, 0.6f, 0.42f, 0.06f, 2.4f);
                        Slash(hero, fw, 0.75f, d * 1.16f, 3.4f, 1, 0.14f, Hot(trail, 0.4f), 0.5f, 0.4f, 0.1f, 1.0f, true, C_WIND);
                        Later(0.12f, () => { foreach (var m in AldaraSkills.I.NearbyPublic(tx, ty, rad)) Hit(m, white); AldaraVfx.Shake(3, 0.18f); });
                        return true;
                    }
                case "leap_strike":
                    if (ph == "cast") { Ring(hero, 0.2f, 1.4f, 0.4f, Hx("#d8c8a8"), 0.35f, 0, 0.9f); AldaraVfxPlus.Dust(hero, 0.9f, 8, DUST); }
                    else
                    {
                        Quake(hero, U(rad), Hx("#ffb04a"), pale, 7);
                        Decal(hero, C_RUNE, U(rad) * 0.85f, gold, white, 0.9f, 1.4f); Beam(hero, 0.7f, 3.2f, gold, 0.35f, 0, 1.4f);
                    }
                    return true;
                case "judgement":
                    {
                        Decal(tg, C_RUNE, 2.4f, gold, white, 1.4f, 2.2f, 0, 1.8f);
                        Sword(tg, 5.2f, Hx("#fff0b0"), 0.13f, 1.0f, 0.03f);
                        Later(0.16f, () =>
                        {
                            Beam(tg, 1.05f, 14, gold, 0.9f, 0, 1.0f); Beam(tg, 0.45f, 14, Hx("#fffbe8"), 0.75f, 0, 1.5f);
                            Ring(tg, 0.4f, 3.4f, 0.8f, gold, 0.6f); Ring(tg, 0.2f, 2.3f, 0.4f, pale, 0.45f, 0.08f); Wall(tg, 0.4f, 2.4f, 1.4f, gold, 0.45f);
                            Decal(tg, C_CRACK, 2.0f, gold, white, 1.8f); Flare(tg + Vector3.up * 1.2f, 3.0f, pale, 0.3f, 0, 1.6f);
                            AldaraVfxPlus.Motes(tg + Vector3.up * 0.4f, Hx("#fff0a0"), 30, 1.2f, 2.6f); AldaraVfxPlus.SparkBurst(tg + Vector3.up * 0.4f, pale, 26, 8, 0.8f);
                            Light(tg + Vector3.up * 2, gold, 5.2f, 11, 0.7f); AldaraVfx.Shake(6, 0.3f);
                        });
                        return true;
                    }
                case "whirlwind":
                    {
                        float rr = U(rad);
                        for (int q = 0; q < 3; q++) Spin(0.6f + q * 0.24f, rr * (0.55f + q * 0.15f), (q - 1) * 0.16f, a + q * 2.1f, -19, trail, 0.5f, q * 0.06f, C_SLASH, 0.42f, 2.0f);
                        Spin(0.8f, rr * 0.95f, 0.05f, a, -15, Hot(trail, 0.3f), 0.55f, 0, C_WIND, 0.5f, 1.0f);
                        Ring(hero, 0.4f, rr * 1.05f, 0.6f, Hot(trail, 0.3f), 0.45f, 0, 1.0f, C_WIND);
                        AldaraVfxPlus.Dust(hero, rr * 0.8f, 12, DUST); AldaraVfxPlus.SparkBurst(hero + Vector3.up * 0.8f, Hot(trail, 0.5f), 14, 6);
                        return true;
                    }
                case "battle_roar":
                    {
                        var c = Hx("#ff9a3a"); float rr = U(rad);
                        Ring(hero, 0.3f, rr, 0.8f, c, 0.55f); Wall(hero, 0.5f, rr * 0.9f, 1.7f, c, 0.45f); Wall(hero, 0.4f, rr * 0.7f, 1.3f, Hx("#ffe0a0"), 0.42f, 0.12f);
                        FlameAura(0.55f, 2.2f, Hx("#ff7a2a"), 1.0f); AldaraVfxPlus.Embers(hero + Vector3.up * 0.5f, c, 30, 0.6f, 2.2f);
                        AldaraVfxPlus.Dust(hero, rr * 0.5f, 10, DUST); Light(hero + Vector3.up, c, 3.4f, 8, 0.5f); AldaraVfx.Shake(5, 0.35f);
                        return true;
                    }
                case "war_cry":
                    {
                        var c = Hx("#ff4a2a");
                        Decal(hero, C_RUNE, 1.9f, c, Hx("#ffd0a0"), 1.3f, -1.6f, 0, 1.6f, true); FlameAura(0.6f, 2.6f, c, 1.1f);
                        Wall(hero, 0.4f, 2.6f, 1.5f, Hx("#ff8a5a"), 0.5f); AldaraVfxPlus.Embers(hero + Vector3.up * 0.5f, Hx("#ff8a5a"), 30, 0.5f, 2.4f);
                        Light(hero + Vector3.up, c, 3.0f, 7, 0.6f);
                        return true;
                    }
                case "second_wind":
                    {
                        var c = Hx("#8aff8a");
                        Decal(hero, C_RUNE, 1.9f, c, white, 1.5f, 1.2f, 0, 1.4f, true); Helix(0.75f, 2.5f, 3, c, 1.2f);
                        AldaraVfxPlus.Motes(hero + Vector3.up * 0.3f, Hx("#c8ffc0"), 30, 0.8f, 1.8f); Light(hero + Vector3.up, c, 2.4f, 6, 0.9f);
                        return true;
                    }
                case "iron_skin":
                    {
                        Dome(1.15f, 0.75f, gold, 1.2f); Decal(hero, C_RUNE, 1.7f, gold, white, 1.1f, 1.8f, 0, 1.5f, true);
                        Flare(hero + Vector3.up * 0.85f, 2.0f, pale, 0.25f); AldaraVfxPlus.SparkBurst(hero + Vector3.up * 0.8f, pale, 18, 5); Light(hero + Vector3.up, gold, 2.8f, 6, 0.5f);
                        return true;
                    }
            }
            return false;
        }
        /// a slam: cracks torn in the ground, a shockwave and a dust wall, rock shards, a flare and a flash
        public static void Quake(Vector3 p, float rr, Color c, Color hot, float shake)
        {
            var h = Hot(c, 0.4f);
            Decal(p, C_CRACK, rr * 1.05f, c, hot, 2.1f); Ring(p, 0.3f, rr * 1.15f, 0.75f, h, 0.5f); Ring(p, 0.2f, rr * 0.8f, 0.35f, hot, 0.4f, 0.08f);
            Wall(p, 0.3f, rr * 0.95f, 0.9f, h, 0.4f); Flare(p + Vector3.up * 0.3f, 2.4f, hot, 0.24f);
            AldaraVfxPlus.Shards(p + Vector3.up * 0.15f, ROCK, 22, 6); AldaraVfxPlus.Dust(p, rr * 0.8f, 14, DUST); AldaraVfxPlus.Embers(p, c, 16, rr * 0.4f, 1.6f);
            AldaraVfxPlus.SparkBurst(p + Vector3.up * 0.3f, hot, 18, 7); Light(p + Vector3.up, c, 3.8f, rr * 2.4f + 4, 0.45f);
            var px = AldaraWorld.ToPx(p); AldaraVfx.Debris(px.x, px.y, 8 + Mathf.RoundToInt(rr * 3), Hx("#6a5a4a"));
            if (shake > 0) AldaraVfx.Shake(shake, 0.35f);
        }
        /// the subclasses' skill pieces for a knight
        public static void SubStrike(AldaraMonsters.Mon m, Color c)
        {
            var P = AldaraPlayer.I; var at = Hero(); var fw = Fwd(P.facing); float d = U(Mathf.Sqrt((m.x - P.x) * (m.x - P.x) + (m.y - P.y) * (m.y - P.y)) + 26);
            Slash(at, fw, 0.9f, Mathf.Max(1.4f, d), 2.7f, R() < 0.5f ? 1 : -1, (R() - 0.5f) * 0.9f, c, 0.55f, 0.36f, 0.06f, 2.2f);
            var p = G(m.x, m.y) + Vector3.up * U(AldaraMonsters.Chest(m)); var hot = Hot(c, 0.5f);
            Flare(p, 2.3f, hot, 0.24f, 0.08f, 2.4f); Ring(G(m.x, m.y), 0.2f, 1.8f, 0.5f, hot, 0.38f, 0.08f); AldaraVfxPlus.SparkBurst(p, hot, 18, 6); Light(p, c, 3, 6, 0.3f);
        }
        public static void SubNova(Vector3 p, float rr, Color c)
        {
            var hot = Hot(c, 0.45f);
            Ring(p, 0.3f, rr, 0.8f, hot, 0.5f); Ring(p, 0.2f, rr * 0.75f, 0.35f, Color.white, 0.4f, 0.06f); Wall(p, 0.4f, rr * 0.9f, 1.5f, c, 0.45f);
            Decal(p, C_RUNE, rr * 0.55f, c, Color.white, 0.8f, 2f); AldaraVfxPlus.SparkBurst(p + Vector3.up * 0.6f, hot, 20, 7); Light(p + Vector3.up, c, 3.2f, rr * 2 + 3, 0.4f);
        }
        public static void SubMark(Vector3 p, float rr, Color c, float life) { Decal(p, C_RUNE, rr, c, Color.white, life + 0.2f, 1.5f, 0, 1.4f); Ring(p, rr * 1.2f, rr * 0.9f, 0.25f, c, life, 0, 0.9f); }
        public static void SubSlam(Vector3 p, float rr, Color c, bool beam)
        {
            Quake(p, rr, c, Hot(c, 0.6f), 0);
            if (beam) { Beam(p, Mathf.Min(1.4f, rr * 0.4f), 9, c, 0.6f, 0, 1.3f); Beam(p, Mathf.Min(0.6f, rr * 0.18f), 9, Hot(c, 0.7f), 0.5f, 0, 2f); }
        }
        public static void SubBuff(Color c)
        {
            var at = Hero(); Decal(at, C_RUNE, 1.5f, c, Color.white, 0.9f, 1.6f, 0, 1.4f, true); Ring(at, 0.3f, 1.8f, 0.4f, Hot(c, 0.4f), 0.45f);
            Flare(at + Vector3.up * 0.85f, 1.6f, Hot(c, 0.4f), 0.22f); AldaraVfxPlus.Motes(at + Vector3.up * 0.3f, Hot(c, 0.3f), 16, 0.6f, 1.6f); Light(at + Vector3.up, c, 2.2f, 5, 0.4f);
        }
        public static void Dash(Vector3 p0, Vector3 p1, Color c)
        {
            Line(p0 + Vector3.up * 0.95f, p1 + Vector3.up * 0.95f, 0.75f, c, 0.32f, 1.6f); Line(p0 + Vector3.up * 0.95f, p1 + Vector3.up * 0.95f, 0.22f, Color.white, 0.24f, 1.8f);
            AldaraVfxPlus.Dust(p0, 0.6f, 6, DUST);
        }

        // ---------- simulate and draw ----------
        static Vector3 ToCam = new Vector3(0, 0.707f, -0.707f);
        Mesh mesh, meshB; Material mat, matB; Texture2D tex; int pass; Color32[] texPx; volatile bool texReady; bool texStarted;
        readonly List<Vector3> vs = new List<Vector3>(), ns = new List<Vector3>(); readonly List<Color> cs = new List<Color>();
        readonly List<Vector4> u0 = new List<Vector4>(), u1 = new List<Vector4>(); readonly List<Vector2> u2 = new List<Vector2>(); float pull; readonly List<int> ix = new List<int>();

        void Awake() { I = this; StartTex(); }
        void StartTex()
        {
            if (texStarted) return; texStarted = true;
            new System.Threading.Thread(() => { try { texPx = Atlas(); } catch (System.Exception e) { Debug.LogException(e); texPx = null; } texReady = true; }) { IsBackground = true }.Start();
        }

        void LateUpdate()
        {
            var cam = Camera.main; float dt = Mathf.Min(0.05f, Time.deltaTime);
            if (!Live || !cam) { fx.Clear(); return; }
            if (!tex && texReady && texPx != null)
            {
                tex = new Texture2D(AT, AT, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "KnightFxAtlas" };
                tex.SetPixels32(texPx); tex.Apply(true, true); texPx = null;
            }
            var hero = Hero();
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i];
                if (f.delay > 0) { f.delay -= dt; continue; }
                f.t += dt; if (f.t >= f.life) { fx.RemoveAt(i); continue; }
                if (f.follow && f.k != K.Line) f.at = hero;
                if (f.k == K.Fall)
                {
                    float u = Mathf.Clamp01(f.t / f.sweep); var p = Vector3.Lerp(f.p1, f.fwd, u * u);
                    if (u < 1) { f.pts.Add(p); if (f.pts.Count > 18) f.pts.RemoveAt(0); f.at = p; }
                    else { if (!f.ended) { f.ended = true; f.at = f.fwd; if (f.done != null) f.done(); } if (f.pts.Count > 0) f.pts.RemoveAt(0); }
                }
                if ((f.k == K.Orb || f.k == K.Fall) && !f.ended) Trailing(f, dt);
                if (f.k == K.Orb && f.ended && f.pts.Count > 1) f.pts.RemoveAt(0);
                if (f.k == K.Slash && f.t < f.sweep && R() < 0.8f) { var hp = SlashPoint(f, EOut(f.t / f.sweep), f.r); AldaraVfxPlus.SparkBurst(hp, Hot(f.c, 0.6f), 1, 2.5f, 0.3f, 4); }
            }
            if (!tex) return;
            if (!mat)
            {
                var sh = Resources.Load<Shader>("Shaders/AldaraKnightFx"); if (!sh) return;
                mat = new Material(sh) { renderQueue = 3105 }; mat.SetTexture("_MainTex", tex); mat.SetFloat("_Src", 1); mat.SetFloat("_Dst", 1);
                matB = new Material(sh) { renderQueue = 3104 }; matB.SetTexture("_MainTex", tex); matB.SetFloat("_Src", 5); matB.SetFloat("_Dst", 10); matB.SetFloat("_Body", 0.5f);
            }
            if (!mesh) { mesh = new Mesh { name = "KnightFx" }; mesh.MarkDynamic(); meshB = new Mesh { name = "KnightFxBody" }; meshB.MarkDynamic(); }
            var ray = cam.ScreenPointToRay(new Vector3(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f, 0)); var pullDir = -ray.direction.normalized;   // the camera's projection is sheared, so not its forward
            mat.SetVector("_Pull", pullDir); matB.SetVector("_Pull", pullDir); ToCam = pullDir;
            for (pass = 0; pass < 2; pass++)
            {
                vs.Clear(); ns.Clear(); cs.Clear(); u0.Clear(); u1.Clear(); u2.Clear(); ix.Clear();
                foreach (var f in fx) if (f.delay <= 0 && (pass == 0 || Body(f.k, f.cell))) Draw(f, cam);
                var m = pass == 0 ? mesh : meshB; m.Clear(); if (ix.Count == 0) continue;
                m.indexFormat = vs.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                m.SetVertices(vs); m.SetNormals(ns); m.SetColors(cs); m.SetUVs(0, u0); m.SetUVs(1, u1); m.SetUVs(2, u2); m.SetTriangles(ix, 0); m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
                Graphics.DrawMesh(m, Matrix4x4.identity, pass == 0 ? mat : matB, 0, cam);
            }
        }

        static bool Body(K k, int cell) { return k == K.Shard || k == K.Slash || k == K.Spin || k == K.Plate || k == K.Sword || k == K.Helix || k == K.Line || k == K.Dome || (k == K.Ring && cell != C_WIND); }
        void P(Vector3 p, Vector3 n, Color c, float a, float u, float v, int cell, float hot, float bright, float fres = 0, float scroll = 0, bool wrap = false)
        {
            vs.Add(p); ns.Add(n); cs.Add(new Color(c.r, c.g, c.b, Mathf.Clamp01(a))); u0.Add(new Vector4(u, v, cell, hot)); u1.Add(new Vector4(bright, fres, scroll, wrap ? 1 : 0)); u2.Add(new Vector2(pull, 0));
        }
        void Q(int a, int b, int c, int d) { ix.Add(a); ix.Add(b); ix.Add(c); ix.Add(a); ix.Add(c); ix.Add(d); }

        /// the arc's plane: forward and a second axis rolled round it by the tilt; rolled further when needed so the arc
        /// faces the tilted camera instead of collapsing edge-on into a sliver
        static void Basis(Fx f, out Vector3 e1, out Vector3 e2)
        {
            if (f.based) { e1 = f.e1; e2 = f.e2; return; }
            var fw = f.fwd.sqrMagnitude > 0.01f ? f.fwd.normalized : Vector3.forward; var rt = Vector3.Cross(Vector3.up, fw).normalized;
            var toCam = ToCam;
            float base0 = f.vert ? Mathf.PI / 2 : 0, best = -1, pick = base0 + f.tilt;
            for (int q = 0; q < 14; q++)
            {
                float roll = base0 + (q % 2 == 0 ? 1 : -1) * f.tilt + (q / 2) * 0.22f * (q % 4 < 2 ? 1 : -1);
                var e = rt * Mathf.Cos(roll) + Vector3.up * Mathf.Sin(roll); float vis = Mathf.Abs(Vector3.Dot(Vector3.Cross(fw, e).normalized, toCam));
                float score = vis - Mathf.Abs(roll - base0 - f.tilt) * 0.05f;
                if (vis > 0.62f && q < 2) { pick = roll; best = 9; break; }
                if (score > best) { best = score; pick = roll; }
            }
            e1 = fw; e2 = (rt * Mathf.Cos(pick) + Vector3.up * Mathf.Sin(pick)).normalized;
            f.e1 = e1; f.e2 = e2; f.based = true;
        }
        static float Theta(Fx f, float p) { float a0 = f.vert ? f.ph : f.dir * f.span * 0.5f, a1 = f.vert ? f.ph - f.span : -f.dir * f.span * 0.5f; return a0 + (a1 - a0) * p; }
        static Vector3 SlashPoint(Fx f, float p, float r) { Vector3 e1, e2; Basis(f, out e1, out e2); float th = Theta(f, p); return f.at + Vector3.up * f.h + (e1 * Mathf.Cos(th) + e2 * Mathf.Sin(th)) * r; }

        void Draw(Fx f, Camera cam)
        {
            float k = f.t / f.life;
            pull = f.k == K.Orb || f.k == K.Bolt || f.k == K.Fall ? 1.0f : f.k == K.Shard ? 0.9f : f.k == K.Ring || f.k == K.Decal ? 2.2f : f.k == K.Wall || f.k == K.Flare ? 1.6f : f.k == K.Beam || f.k == K.Sword ? 0.6f : 1.2f;
            switch (f.k)
            {
                case K.Slash:
                    {
                        float hd = EOut(f.t / f.sweep), tl = EOut((f.t - f.sweep * 0.3f) / (f.sweep * 2.2f)); if (hd - tl < 0.003f) return;
                        float g = 1 - Mathf.Clamp01((f.t - f.sweep) / Mathf.Max(0.01f, f.life - f.sweep)); g = g * g * (3 - 2 * g);
                        Vector3 e1, e2; Basis(f, out e1, out e2); var n = Vector3.Cross(e1, e2); var c0 = f.at + Vector3.up * f.h;
                        const int N = 40; int b = vs.Count; float ri = f.r * (1 - f.w);
                        for (int i = 0; i <= N; i++)
                        {
                            float s = i / (float)N, p = Mathf.Lerp(tl, hd, s), th = Theta(f, p); var d = e1 * Mathf.Cos(th) + e2 * Mathf.Sin(th);
                            float a = g * f.alpha * Mathf.SmoothStep(0, 1, s * 1.6f) * (0.5f + 0.5f * s);
                            P(c0 + d * ri, n, f.c, a, s, 0, f.cell, f.hot, f.bright); P(c0 + d * f.r, n, f.c, a, s, 1, f.cell, f.hot, f.bright);
                            if (i > 0) Q(b + (i - 1) * 2, b + (i - 1) * 2 + 1, b + i * 2 + 1, b + i * 2);
                        }
                        break;
                    }
                case K.Spin:
                    {
                        float g = Mathf.Min(1, f.t / 0.06f) * (1 - EIn(k)); float head = f.ph + f.spin * f.t, len = 1.45f * Mathf.PI * Mathf.Min(1, f.t / 0.12f);
                        var nrm = Quaternion.AngleAxis(f.tilt * Mathf.Rad2Deg, Vector3.right) * Vector3.up; var e1 = Vector3.Cross(nrm, Vector3.forward).normalized; var e2 = Vector3.Cross(nrm, e1).normalized;
                        var c0 = f.at + Vector3.up * f.h; const int N = 48; int b = vs.Count; float ri = f.r * (1 - f.w), sg = Mathf.Sign(f.spin);
                        for (int i = 0; i <= N; i++)
                        {
                            float s = i / (float)N, th = head - sg * len * (1 - s); var d = e1 * Mathf.Cos(th) + e2 * Mathf.Sin(th);
                            float a = g * Mathf.SmoothStep(0, 1, s * 1.4f) * (0.45f + 0.55f * s);
                            P(c0 + d * ri, nrm, f.c, a, s, 0, f.cell, f.hot, f.bright); P(c0 + d * f.r, nrm, f.c, a, s, 1, f.cell, f.hot, f.bright);
                            if (i > 0) Q(b + (i - 1) * 2, b + (i - 1) * 2 + 1, b + i * 2 + 1, b + i * 2);
                        }
                        break;
                    }
                case K.Ring:
                    {
                        float e = EOut(k), cur = Mathf.Lerp(f.r, f.r1, e), w = f.w * (0.5f + 0.5f * (1 - e)), a = f.alpha * Mathf.Pow(1 - k, 1.4f) * Mathf.Min(1, f.t / 0.04f);
                        float y0 = f.at.y + 0.07f; const int N = 64; int b = vs.Count; float shrink = f.r1 < f.r ? -1 : 1;
                        for (int i = 0; i <= N; i++)
                        {
                            float s = i / (float)N, th = s * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
                            var o = new Vector3(f.at.x, y0, f.at.z);
                            P(o + d * Mathf.Max(0, cur - w * shrink), Vector3.up, f.c, a, s * 3, 0, f.cell, f.hot, f.bright, 0, 0, true);
                            P(o + d * cur, Vector3.up, f.c, a, s * 3, 1, f.cell, f.hot, f.bright, 0, 0, true);
                            if (i > 0) Q(b + (i - 1) * 2, b + (i - 1) * 2 + 1, b + i * 2 + 1, b + i * 2);
                        }
                        break;
                    }
                case K.Wall:
                    {
                        float e = EOut(k), cur = Mathf.Lerp(f.r, f.r1, e), hh = f.h * (1 - 0.45f * e), a = f.alpha * (1 - k) * (1 - k) * Mathf.Min(1, f.t / 0.05f);
                        const int N = 48; int b = vs.Count;
                        for (int i = 0; i <= N; i++)
                        {
                            float s = i / (float)N, th = s * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
                            P(f.at + d * cur * 0.96f, d, f.c, a, s * 2, 0, C_BAND, f.hot * 0.6f, f.bright, -0.6f, 0, true);
                            P(f.at + d * cur + Vector3.up * hh, d, f.c, 0, s * 2, 1, C_BAND, f.hot * 0.6f, f.bright, -0.6f, 0, true);
                            if (i > 0) Q(b + (i - 1) * 2, b + (i - 1) * 2 + 1, b + i * 2 + 1, b + i * 2);
                        }
                        break;
                    }
                case K.Decal:
                    {
                        float grow = EOut(f.t / 0.16f), s = f.size * (0.55f + 0.45f * grow), fade = 1 - Mathf.Clamp01((k - 0.45f) / 0.55f);
                        float a = f.alpha * Mathf.Min(1, f.t / 0.05f) * fade * fade; float hot = Mathf.Lerp(0.55f, 0.1f, Mathf.Clamp01(k * 2.2f));
                        var col = Color.Lerp(Color.Lerp(f.c, f.c2, 0.45f), f.c, Mathf.Clamp01(k * 3f)); float rot = f.ph + f.spin * f.t;
                        var ax = new Vector3(Mathf.Cos(rot), 0, Mathf.Sin(rot)) * s; var az = new Vector3(-Mathf.Sin(rot), 0, Mathf.Cos(rot)) * s; var o = f.at + Vector3.up * 0.06f;
                        int b = vs.Count;
                        P(o - ax - az, Vector3.up, col, a, 0, 0, f.cell, hot, f.bright * (0.55f + 0.5f * hot)); P(o + ax - az, Vector3.up, col, a, 1, 0, f.cell, hot, f.bright * (0.55f + 0.5f * hot));
                        P(o + ax + az, Vector3.up, col, a, 1, 1, f.cell, hot, f.bright * (0.55f + 0.5f * hot)); P(o - ax + az, Vector3.up, col, a, 0, 1, f.cell, hot, f.bright * (0.55f + 0.5f * hot));
                        Q(b, b + 1, b + 2, b + 3); break;
                    }
                case K.Flare:
                    {
                        float s = f.cell == C_DOT ? f.size * (0.4f + 0.8f * EOut(f.t / 0.08f)) : f.size * (0.5f + 0.7f * EOut(f.t / 0.05f)) * (1 - 0.35f * k), a = f.alpha * (1 - k) * (1 - k);
                        var r = cam.transform.right; var u = cam.transform.up; float cr = Mathf.Cos(f.ph), sr = Mathf.Sin(f.ph);
                        var ax = (r * cr + u * sr) * s; var ay = (-r * sr + u * cr) * s; var n = -cam.transform.forward; int b = vs.Count; var o = f.at + ToCam * 0.6f;
                        int fc = f.cell == 0 ? C_FLARE : f.cell;
                        P(o - ax - ay, n, f.c, a, 0, 0, fc, 1, f.bright); P(o + ax - ay, n, f.c, a, 1, 0, fc, 1, f.bright); P(o + ax + ay, n, f.c, a, 1, 1, fc, 1, f.bright); P(o - ax + ay, n, f.c, a, 0, 1, fc, 1, f.bright);
                        Q(b, b + 1, b + 2, b + 3); break;
                    }
                case K.Beam:
                    {
                        float env = Mathf.Min(1, f.t / 0.06f) * (1 - EIn(k)), rr = f.r * (1 - 0.55f * k * k) * (0.7f + 0.3f * EOut(f.t / 0.08f));
                        const int N = 32; int b = vs.Count; float sc = -f.t * 1.6f;
                        for (int i = 0; i <= N; i++)
                        {
                            float s = i / (float)N, th = s * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
                            P(f.at + d * rr * 1.08f, d, f.c, env, s * 2, 0, C_BEAM, 0.8f, f.bright, -1, sc, true);
                            P(f.at + d * rr * 0.92f + Vector3.up * f.h, d, f.c, 0, s * 2, 2.5f, C_BEAM, 0.8f, f.bright, -1, sc, true);
                            if (i > 0) Q(b + (i - 1) * 2, b + (i - 1) * 2 + 1, b + i * 2 + 1, b + i * 2);
                        }
                        break;
                    }
                case K.Sword:
                    {
                        float fall = EIn(f.t / f.sweep), L = f.size, wdt = L * 0.55f, tipY = Mathf.Lerp(f.at.y + 10, f.at.y - 0.35f, fall) - (f.t > f.sweep ? (f.t - f.sweep) * 0.2f : 0);
                        float a = Mathf.Min(1, f.t / 0.05f) * (1 - Mathf.Clamp01((k - 0.55f) / 0.45f)); float hot = f.t > f.sweep ? Mathf.Lerp(1, 0.4f, (f.t - f.sweep) / 0.4f) : 1;
                        var bottom = new Vector3(f.at.x, tipY - L * 0.04f, f.at.z); var up = Vector3.up * L; var s1 = f.fwd.normalized * wdt * 0.5f; var s2 = Vector3.Cross(Vector3.up, f.fwd).normalized * wdt * 0.5f;
                        foreach (var sd in new[] { s1, s2 })
                        {
                            int b = vs.Count; var n = Vector3.Cross(sd, Vector3.up).normalized;
                            P(bottom - sd, n, f.c, a, 0, 0, C_SWORD, hot, f.bright); P(bottom + sd, n, f.c, a, 1, 0, C_SWORD, hot, f.bright);
                            P(bottom + sd + up, n, f.c, a, 1, 1, C_SWORD, hot, f.bright); P(bottom - sd + up, n, f.c, a, 0, 1, C_SWORD, hot, f.bright); Q(b, b + 1, b + 2, b + 3);
                        }
                        break;
                    }
                case K.Dome:
                    {
                        float pop = EOut(f.t / 0.14f), rr = f.r * Mathf.Lerp(1.35f, 1f, pop), a = Mathf.Min(1, f.t / 0.06f) * (1 - EIn(k)) * (0.75f + 0.25f * Mathf.Sin(f.t * 30));
                        var c0 = f.at + Vector3.up * f.h; const int LA = 12, LO = 28; int b = vs.Count;
                        for (int j = 0; j <= LA; j++)
                            for (int i = 0; i <= LO; i++)
                            {
                                float lat = Mathf.Lerp(-0.45f, 1.5f, j / (float)LA), lon = i / (float)LO * Mathf.PI * 2;
                                var d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                                P(c0 + d * rr, d, f.c, a * Mathf.Clamp01((lat + 0.45f) * 3), i / (float)LO * 4, j / (float)LA * 2, C_HEX, 0.7f, f.bright, 1, f.t * 0.25f, true);
                                if (i > 0 && j > 0) Q(b + (j - 1) * (LO + 1) + i - 1, b + (j - 1) * (LO + 1) + i, b + j * (LO + 1) + i, b + j * (LO + 1) + i - 1);
                            }
                        break;
                    }
                case K.Flame:
                    {
                        float env = Mathf.Min(1, f.t / 0.1f) * (1 - EIn(k));
                        for (int layer = 0; layer < 2; layer++)
                        {
                            const int N = 32; int b = vs.Count; float rr = f.r * (layer == 0 ? 1 : 0.78f), hh = f.h * (layer == 0 ? 1 : 0.8f) * (0.6f + 0.4f * EOut(f.t / 0.2f));
                            for (int i = 0; i <= N; i++)
                            {
                                float s = i / (float)N, th = s * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
                                P(f.at + d * rr + Vector3.up * 0.05f, d, f.c, env, s * 2 + layer * 0.37f, 0, C_FLAME, 0.9f, f.bright, -0.5f, -f.t * (1.6f + layer * 0.5f), true);
                                P(f.at + d * rr * 1.18f + Vector3.up * hh, d, f.c, 0, s * 2 + layer * 0.37f, 1.2f, C_FLAME, 0.9f, f.bright, -0.5f, -f.t * (1.6f + layer * 0.5f), true);
                                if (i > 0) Q(b + (i - 1) * 2, b + (i - 1) * 2 + 1, b + i * 2 + 1, b + i * 2);
                            }
                        }
                        break;
                    }
                case K.Helix:
                    {
                        float head = EOut(f.t / (f.life * 0.7f)) * 1.2f, env = 1 - EIn(k);
                        for (int sIdx = 0; sIdx < f.n; sIdx++)
                        {
                            const int N = 36; int b = vs.Count; float ph = f.ph + sIdx * Mathf.PI * 2 / f.n;
                            for (int i = 0; i <= N; i++)
                            {
                                float s = i / (float)N, q = Mathf.Clamp01(head - 0.5f + s * 0.5f), th = ph + q * Mathf.PI * 3.2f + f.t * 2;
                                float rr = f.r * (1 - 0.35f * q); var d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th)); var c = f.at + d * rr + Vector3.up * (q * f.h + 0.1f);
                                float a = env * Mathf.SmoothStep(0, 1, s * 1.5f) * (q < 1 ? 1 : 0.4f);
                                P(c - Vector3.up * 0.13f, d, f.c, a, s, 0, C_BAND, 0.8f, f.bright); P(c + Vector3.up * 0.13f, d, f.c, a, s, 1, C_BAND, 0.8f, f.bright);
                                if (i > 0) Q(b + (i - 1) * 2, b + (i - 1) * 2 + 1, b + i * 2 + 1, b + i * 2);
                            }
                        }
                        break;
                    }
                case K.Line:
                    {
                        float a = (1 - k) * (1 - k); var dir = f.p1 - f.at; var side = Vector3.Cross(dir.normalized, cam.transform.forward).normalized * f.w * 0.5f; var n = -cam.transform.forward;
                        int b = vs.Count; var p0 = Vector3.Lerp(f.at, f.p1, EOut(k) * 0.7f);
                        P(p0 - side, n, f.c, 0, 0, 0, C_BAND, 1, f.bright); P(f.p1 - side, n, f.c, a, 1, 0, C_BAND, 1, f.bright); P(f.p1 + side, n, f.c, a, 1, 1, C_BAND, 1, f.bright); P(p0 + side, n, f.c, 0, 0, 1, C_BAND, 1, f.bright);
                        Q(b, b + 1, b + 2, b + 3); break;
                    }
                case K.Orb: case K.Fall: DrawOrb(f, cam); break;
                case K.Bolt: DrawBolt(f, cam); break;
                case K.Shard:
                    {
                        float grow = EOut(f.t / 0.1f) * (1 - EIn(Mathf.Clamp01((k - 0.7f) / 0.3f))); if (grow <= 0.01f) break;
                        var up = (Vector3.up + f.fwd * 0.6f).normalized * f.h * grow; var sd = Vector3.Cross(up.normalized, Vector3.forward).normalized; var sd2 = Vector3.Cross(up.normalized, sd).normalized;
                        float a = Mathf.Min(1, f.t / 0.04f) * (1 - Mathf.Clamp01((k - 0.85f) / 0.15f)), ww = f.w * (0.6f + 0.4f * grow);
                        foreach (var side in new[] { sd, sd2 })
                        {
                            int b = vs.Count; var nn = Vector3.Cross(side, up).normalized; var o = f.at - up.normalized * 0.05f;
                            P(o - side * ww * 0.5f, nn, f.c, a, 0, 0, C_SHARD, 0.6f, f.bright); P(o + side * ww * 0.5f, nn, f.c, a, 1, 0, C_SHARD, 0.6f, f.bright);
                            P(o + side * ww * 0.5f + up, nn, f.c, a, 1, 1, C_SHARD, 0.6f, f.bright); P(o - side * ww * 0.5f + up, nn, f.c, a, 0, 1, C_SHARD, 0.6f, f.bright); Q(b, b + 1, b + 2, b + 3);
                        }
                        break;
                    }
                case K.Plate:
                    {
                        float e = EOut(k), s = f.size * (0.6f + 0.7f * e), a = (1 - k) * (1 - k) * Mathf.Min(1, f.t / 0.04f); var fw = f.fwd.normalized;
                        var o = f.at + fw * f.r * e; var rt = Vector3.Cross(Vector3.up, fw).normalized * s; var up = Vector3.up * s; int b = vs.Count;
                        P(o - rt - up, fw, f.c, a, 0, 0, f.cell, 0.8f, f.bright); P(o + rt - up, fw, f.c, a, 1, 0, f.cell, 0.8f, f.bright); P(o + rt + up, fw, f.c, a, 1, 1, f.cell, 0.8f, f.bright); P(o - rt + up, fw, f.c, a, 0, 1, f.cell, 0.8f, f.bright);
                        Q(b, b + 1, b + 2, b + 3); break;
                    }
            }
        }

        // ---------- shots in flight, lightning ----------
        void Trailing(Fx f, float dt)
        {
            var p = f.at;
            switch (f.n)
            {
                case 1: if (R() < 0.9f) AldaraVfxPlus.Embers(p, new Color(1, 0.55f, 0.2f), 1, 0.12f, 0.8f); if (R() < 0.35f) AldaraVfxPlus.Smoke(p, new Color(0.2f, 0.16f, 0.14f, 0.35f), 1, 0.08f, 0.5f, 0.22f * f.size / 0.4f); break;
                case 2: if (R() < 0.6f) AldaraVfxPlus.Motes(p, new Color(0.8f, 0.95f, 1f), 1, 0.1f, 0.2f); break;
                case 3: if (R() < 0.6f) AldaraVfxPlus.Motes(p, Hot(f.c, 0.3f), 1, 0.08f, 0.3f); break;
                default: if (R() < 0.5f) AldaraVfxPlus.SparkBurst(p, Hot(f.c, 0.5f), 1, 0.8f, 0.2f, 2); break;
            }
        }
        void Strip(List<Vector3> pts, float w0, float w1, Color c, float a0, float a1, int cell, float hot, float bright)
        {
            if (pts.Count < 2) return; int b = vs.Count; var n = ToCam;
            for (int i = 0; i < pts.Count; i++)
            {
                float s = i / (float)(pts.Count - 1); var d = i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]; if (d.sqrMagnitude < 1e-8f) d = Vector3.right;
                var side = Vector3.Cross(d.normalized, ToCam).normalized * Mathf.Lerp(w0, w1, s) * 0.5f; float a = Mathf.Lerp(a0, a1, s);
                P(pts[i] - side, n, c, a, s, 0, cell, hot, bright); P(pts[i] + side, n, c, a, s, 1, cell, hot, bright);
                if (i > 0) Q(b + (i - 1) * 2, b + (i - 1) * 2 + 1, b + i * 2 + 1, b + i * 2);
            }
        }
        void Bill(Vector3 at, float size, Color c, float a, int cell, float hot, float bright, float rot, Camera cam)
        {
            var r = cam.transform.right; var u = cam.transform.up; float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            var ax = (r * cr + u * sr) * size; var ay = (-r * sr + u * cr) * size; int b = vs.Count; var o = at + ToCam * 0.3f;
            P(o - ax - ay, ToCam, c, a, 0, 0, cell, hot, bright); P(o + ax - ay, ToCam, c, a, 1, 0, cell, hot, bright); P(o + ax + ay, ToCam, c, a, 1, 1, cell, hot, bright); P(o - ax + ay, ToCam, c, a, 0, 1, cell, hot, bright); Q(b, b + 1, b + 2, b + 3);
        }
        void DrawOrb(Fx f, Camera cam)
        {
            float env = f.ended ? Mathf.Clamp01((f.life - f.t) / 0.22f) : Mathf.Min(1, f.t / 0.05f), fl = 0.85f + 0.15f * Mathf.Sin(f.t * 40 + f.ph);
            if (f.k == K.Fall && f.ended) env *= 0; 
            // the trail behind it, widening to the head
            if (f.pts.Count > 1) { var tr = new List<Vector3>(f.pts); if ((tr[tr.Count - 1] - f.at).sqrMagnitude > 1e-6f && !f.ended) tr.Add(f.at); Strip(tr, 0, f.w * (f.n == 1 ? 1.4f : 1), f.c, 0, 0.9f * (f.ended ? Mathf.Clamp01((f.life - f.t) / 0.22f) : 1), C_BAND, 0.7f, f.bright); }
            if (env <= 0) return;
            Bill(f.at, f.size * 2.6f, f.c, 0.55f * env * fl, C_DOT, 0.2f, f.bright);
            Bill(f.at, f.size * (f.n == 1 ? 1.3f : 1.7f), f.c2, 0.8f * env, C_FLARE, 1, f.bright, f.ph + f.t * 6, cam);
            Bill(f.at, f.size * 0.75f, f.c2, env, C_DOT, 1, f.bright * 1.4f, 0, cam);
            if (f.n == 1) { for (int i = 0; i < 3; i++) Bill(f.at + new Vector3(Mathf.Cos(f.t * 9 + i * 2.1f), Mathf.Sin(f.t * 11 + i * 2.1f) * 0.6f, 0) * f.size * 0.35f, f.size * 0.9f, new Color(1, 0.5f, 0.15f), 0.6f * env, C_DOT, 0.5f, f.bright, 0, cam); }
            if (f.n == 3) Bill(f.at, f.size * 1.15f, f.c, 0.5f * env, C_RUNE, 0.25f, f.bright * 0.8f, f.t * 5, cam);
            if (f.n == 2)
            {   // the crystal itself, point first
                var d = f.fwd.sqrMagnitude > 1e-6f ? f.fwd.normalized : Vector3.right; var sd = Vector3.Cross(d, ToCam).normalized; float L = f.size * 2.4f, W = f.size * 0.9f; int b = vs.Count;
                var tail = f.at - d * L * 0.6f; var tip = f.at + d * L * 0.4f;
                P(tail - sd * W * 0.5f, ToCam, f.c, env, 0, 0, C_SHARD, 0.8f, f.bright); P(tail + sd * W * 0.5f, ToCam, f.c, env, 1, 0, C_SHARD, 0.8f, f.bright);
                P(tip + sd * W * 0.5f, ToCam, f.c, env, 1, 1, C_SHARD, 0.8f, f.bright); P(tip - sd * W * 0.5f, ToCam, f.c, env, 0, 1, C_SHARD, 0.8f, f.bright); Q(b, b + 1, b + 2, b + 3);
            }
        }
        void Bill(Vector3 at, float size, Color c, float a, int cell, float hot, float bright) { var cam = Camera.main; if (cam) Bill(at, size, c, a, cell, hot, bright, 0, cam); }
        void DrawBolt(Fx f, Camera cam)
        {
            float k = f.t / f.life, env = (1 - k * k) * (0.55f + 0.45f * Mathf.PerlinNoise(f.t * 30, 0.3f)); if (f.pts == null || f.pts.Count < 2) return;
            int seed = (int)(f.t / 0.05f);
            for (int sgi = 0; sgi < f.pts.Count - 1; sgi++)
            {
                var A = f.pts[sgi]; var B = f.pts[sgi + 1]; var rng = new System.Random(seed * 7919 + sgi * 131 + 17); System.Func<float> rf = () => (float)rng.NextDouble() - 0.5f;
                var d = B - A; float len = d.magnitude; if (len < 0.05f) continue; var side = Vector3.Cross(d / len, ToCam).normalized;
                var path = new List<Vector3>(); int N = Mathf.Clamp((int)(len * 3), 6, 22);
                for (int i = 0; i <= N; i++) { float s = i / (float)N, taper = Mathf.Sin(s * Mathf.PI); path.Add(A + d * s + (side * rf() * len * 0.22f + Vector3.up * rf() * len * 0.08f) * taper); }
                Strip(path, f.w * 6, f.w * 6, f.c, 0.35f * env, 0.35f * env, C_BAND, 0.3f, f.bright * 0.8f);
                Strip(path, f.w * 1.6f, f.w * 1.6f, f.c2, env, env, C_BAND, 1, f.bright * 1.5f);
                for (int q = 0; q < 2; q++)
                {   // forks
                    int at = 2 + rng.Next(Mathf.Max(1, N - 4)); var p0 = path[at]; var fd = (d / len + side * rf() * 2.4f + Vector3.up * rf()).normalized; var br = new List<Vector3> { p0 };
                    for (int i = 1; i <= 4; i++) br.Add(p0 + fd * len * 0.07f * i + side * rf() * len * 0.05f);
                    Strip(br, f.w * 1.2f, 0.01f, f.c2, 0.8f * env, 0, C_BAND, 1, f.bright);
                }
            }
        }

        // ---------- the atlas, painted once in the background ----------
        const int CS = 256, AT = 1024;
        static Color32[] Atlas()
        {
            var px = new Color32[AT * AT]; var A = new float[CS * CS]; var Rc = new float[CS * CS];
            for (int cell = 0; cell < 13; cell++)
            {
                System.Array.Clear(A, 0, A.Length); System.Array.Clear(Rc, 0, Rc.Length); Paint(cell, A, Rc);
                int cx = (cell % 4) * CS, cy = (cell / 4) * CS;
                for (int y = 0; y < CS; y++) for (int x = 0; x < CS; x++) { int i = y * CS + x; px[(cy + y) * AT + cx + x] = new Color32((byte)(Mathf.Clamp01(Rc[i]) * 255), 255, 255, (byte)(Mathf.Clamp01(A[i]) * 255)); }
            }
            return px;
        }
        static float Hs(int x, int y, int s) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041); h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16; return (h & 0xffffff) / 16777216f; } }
        static int Wr(int a, int p) { return p > 0 ? ((a % p) + p) % p : a; }
        static float VN(float x, float y, int px, int py, int s)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hs(Wr(x0, px), Wr(y0, py), s), b = Hs(Wr(x0 + 1, px), Wr(y0, py), s), c = Hs(Wr(x0, px), Wr(y0 + 1, py), s), d = Hs(Wr(x0 + 1, px), Wr(y0 + 1, py), s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        static float Fbm(float x, float y, int px, int py, int s, int oct)
        {
            float amp = 0.5f, sum = 0, norm = 0;
            for (int o = 0; o < oct; o++) { sum += amp * VN(x, y, px, py, s + o * 17); norm += amp; x *= 2; y *= 2; px *= 2; py *= 2; amp *= 0.5f; }
            return sum / norm;
        }
        static float Sq(float x) { return x * x; }
        static float SS(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        static float SegD(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, l = dx * dx + dy * dy; float t = l > 0 ? Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / l) : 0;
            float qx = ax + dx * t - px, qy = ay + dy * t - py; return Mathf.Sqrt(qx * qx + qy * qy);
        }
        static void Paint(int cell, float[] A, float[] Rc)
        {
            var rng = new System.Random(1234 + cell * 77);
            if (cell == C_CRACK) { Cracks(A, Rc, rng); return; }
            List<Vector4> segs = null; if (cell == C_RUNE) segs = RuneSegs(rng);
            float[] lines = null; if (cell == C_WIND) { lines = new float[7]; for (int i = 0; i < 7; i++) lines[i] = 0.18f + 0.64f * (float)rng.NextDouble(); }
            for (int y = 0; y < CS; y++)
                for (int x = 0; x < CS; x++)
                {
                    float u = (x + 0.5f) / CS, v = (y + 0.5f) / CS, a = 0, r = 0; int i = y * CS + x;
                    float px = u - 0.5f, py = v - 0.5f, d = Mathf.Sqrt(px * px + py * py) * 2;
                    switch (cell)
                    {
                        case C_SLASH:
                            {
                                float n = Fbm(v * 14, u * 2.5f, 0, 0, 11, 3), edge = Mathf.Exp(-Sq((v - 0.88f) / 0.05f));
                                float body = Mathf.Pow(v, 2.1f) * (1 - SS(0.93f, 1f, v)) * (0.5f + 0.7f * n);
                                float head = Mathf.Exp(-Sq((u - 0.96f) / 0.05f)) * Mathf.Pow(v, 1.2f) * 0.7f;
                                float cut = SS(0, 0.16f + 0.3f * Fbm(v * 9, 3.3f, 0, 0, 13, 2), u);
                                a = (edge + body * 0.9f + head) * cut; r = (edge * 0.95f + head * 0.6f) * cut; break;
                            }
                        case C_RING:
                            {
                                float n = Fbm(u * 8, v * 3, 8, 0, 21, 3), lead = Mathf.Exp(-Sq((v - 0.9f) / 0.06f)), body = Mathf.Pow(v, 1.8f) * (1 - SS(0.96f, 1, v));
                                a = (lead + body * 0.75f) * (0.45f + 0.75f * n); r = lead * 0.85f * (0.6f + 0.5f * n); break;
                            }
                        case C_RUNE:
                            {
                                float dp = 1e9f, rr = d * 0.5f;
                                foreach (var ring in new[] { 0.48f, 0.455f, 0.3f, 0.055f }) dp = Mathf.Min(dp, Mathf.Abs(rr - ring));
                                foreach (var s in segs) dp = Mathf.Min(dp, SegD(px, py, s.x, s.y, s.z, s.w));
                                float dpx = dp * CS; a = Mathf.Exp(-Sq(dpx / 2.8f)) + 0.6f * Mathf.Exp(-dpx / 7f) + (rr < 0.48f ? 0.02f : 0); r = Mathf.Exp(-Sq(dpx / 1.8f));
                                a *= 1 - SS(0.49f, 0.5f, rr); r *= 1 - SS(0.49f, 0.5f, rr); break;
                            }
                        case C_FLARE:
                            {
                                float glow = Mathf.Exp(-d * d * 14) + 0.28f * Mathf.Exp(-d * d * 2.6f);
                                float ax = Mathf.Abs(px) * 2, ay = Mathf.Abs(py) * 2;
                                float rays = Mathf.Exp(-Sq(ay / 0.014f)) * Sq(Mathf.Clamp01(1 - ax)) + Mathf.Exp(-Sq(ax / 0.014f)) * Sq(Mathf.Clamp01(1 - ay));
                                float q1 = Mathf.Abs(px + py) * 1.4142f, q2 = Mathf.Abs(px - py) * 1.4142f;
                                rays += 0.45f * (Mathf.Exp(-Sq(q2 / 0.012f)) * Sq(Mathf.Clamp01(1 - q1 * 1.7f)) + Mathf.Exp(-Sq(q1 / 0.012f)) * Sq(Mathf.Clamp01(1 - q2 * 1.7f)));
                                a = (glow + rays) * (1 - SS(0.9f, 1f, d)); r = Mathf.Exp(-d * d * 70) + rays * 0.55f; break;
                            }
                        case C_BEAM:
                            {
                                float n = Fbm(u * 10, v * 2, 10, 2, 31, 3), st = Mathf.Pow(n, 1.6f) * 1.4f;
                                a = 0.22f + st * 0.85f; r = SS(0.62f, 0.86f, n); break;
                            }
                        case C_SWORD:
                            {
                                float cx = Mathf.Abs(u - 0.5f), hw = 0.075f * SS(0.04f, 0.22f, v);
                                float dBlade = Mathf.Max(cx - hw, Mathf.Max(0.04f - v, v - 0.76f));
                                float gw = 0.2f + 0.02f * SS(0.1f, 0.2f, cx), dGuard = Mathf.Max(cx - gw, Mathf.Max(0.755f - v, v - 0.8f));
                                float dGrip = Mathf.Max(cx - 0.024f, Mathf.Max(0.8f - v, v - 0.925f));
                                float dPom = Mathf.Sqrt(Sq(u - 0.5f) + Sq(v - 0.95f)) - 0.032f;
                                float dd = Mathf.Min(Mathf.Min(dBlade, dGuard), Mathf.Min(dGrip, dPom));
                                a = (dd < 0 ? 0.7f : 0) + Mathf.Exp(-Mathf.Max(dd, 0) / 0.028f) * 0.65f;
                                if (v > 0.04f && v < 0.76f && cx < hw) r = Mathf.Exp(-Sq((u - 0.5f) / 0.012f)) + 0.7f * Mathf.Exp(-Sq((cx - hw) / 0.006f));
                                if (dd < 0) r = Mathf.Max(r, 0.3f); break;
                            }
                        case C_HEX:
                            {
                                float hx = u * 4, hy = v * 2 * 1.7320508f; float rx = 1, ry = 1.7320508f;
                                float ax = Mod(hx, rx) - 0.5f, ay = Mod(hy, ry) - ry * 0.5f, bx = Mod(hx - 0.5f, rx) - 0.5f, by = Mod(hy - ry * 0.5f, ry) - ry * 0.5f;
                                float gx, gy; if (ax * ax + ay * ay < bx * bx + by * by) { gx = ax; gy = ay; } else { gx = bx; gy = by; }
                                gx = Mathf.Abs(gx); gy = Mathf.Abs(gy); float hd = Mathf.Max(gx * 0.5f + gy * 0.8660254f, gx), edge = 0.5f - hd;
                                a = Mathf.Exp(-Sq(edge / 0.035f)) * 0.95f + 0.1f; r = Mathf.Exp(-Sq(edge / 0.012f)); break;
                            }
                        case C_FLAME:
                            {
                                float wv = Fbm(u * 3, v * 2, 3, 2, 45, 2), n = Fbm(u * 7 + wv * 1.5f, v * 2, 7, 2, 41, 4); float tongue = SS(0.38f, 0.72f, n) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 14 + wv * 4))); a = tongue; r = SS(0.64f, 0.86f, n); break;
                            }
                        case C_DOT: a = Mathf.Exp(-d * d * 4.5f) * (1 - SS(0.85f, 1, d)); r = Mathf.Exp(-d * d * 25); break;
                        case C_SHARD:
                            {   // an ice crystal standing up (bottom v = 0): a long faceted point with a bright edge and inner facets
                                float cx = u - 0.5f, hw = 0.2f * SS(0f, 0.12f, v) * (1 - SS(0.62f, 1f, v)) + 0.2f * SS(0.62f, 1f, v) * (1 - v) * 2.4f;
                                hw = v < 0.62f ? 0.2f * SS(0f, 0.1f, v) : 0.2f * (1 - (v - 0.62f) / 0.38f);
                                float dd = Mathf.Abs(cx) - hw; bool inside = dd < 0 && v > 0.01f && v < 0.99f;
                                float edge = Mathf.Exp(-Sq(dd / 0.012f)), facet = Mathf.Exp(-Sq((cx + 0.06f * (v - 0.4f)) / 0.01f)) * (inside ? 1 : 0), glow = Mathf.Exp(-Mathf.Max(dd, 0) / 0.03f) * 0.4f;
                                float n = Fbm(u * 6, v * 3, 0, 0, 71, 2);
                                a = (inside ? 0.42f + 0.25f * n + 0.25f * v : 0) + edge * 0.9f + facet * 0.6f + glow; r = edge * 0.8f + facet * 0.6f + (inside ? 0.15f * v : 0); break;
                            }
                        case C_BAND:
                            {
                                float n = Fbm(u * 4, v * 6, 4, 0, 51, 2);
                                a = (Mathf.Exp(-Sq((v - 0.5f) / 0.2f)) * 0.6f + Mathf.Exp(-Sq((v - 0.5f) / 0.05f))) * (0.75f + 0.35f * n) * (1 - SS(0.4f, 0.5f, Mathf.Abs(v - 0.5f)));
                                r = Mathf.Exp(-Sq((v - 0.5f) / 0.035f)); break;
                            }
                        case C_WIND:
                            {
                                float s = 0;
                                for (int l = 0; l < lines.Length; l++) s += Mathf.Exp(-Sq((v - lines[l]) / 0.012f)) * SS(0.38f, 0.62f, Fbm(u * 5, l * 3.7f, 5, 0, 61 + l, 2));
                                a = s * 0.95f + 0.1f * Mathf.Exp(-Sq((v - 0.5f) / 0.3f)); r = s * 0.5f; break;
                            }
                    }
                    A[i] = a; Rc[i] = r;
                }
        }
        static float Mod(float a, float b) { return a - b * Mathf.Floor(a / b); }
        static List<Vector4> RuneSegs(System.Random rng)
        {
            var L = new List<Vector4>(); System.Func<float> rf = () => (float)rng.NextDouble();
            for (int t = 0; t < 2; t++)   // the hexagram
                for (int k = 0; k < 3; k++)
                {
                    float a0 = t * Mathf.PI / 3 + k * Mathf.PI * 2 / 3 + Mathf.PI / 2, a1 = a0 + Mathf.PI * 2 / 3;
                    L.Add(new Vector4(Mathf.Cos(a0) * 0.3f, Mathf.Sin(a0) * 0.3f, Mathf.Cos(a1) * 0.3f, Mathf.Sin(a1) * 0.3f));
                }
            for (int k = 0; k < 48; k++) { float a = k / 48f * Mathf.PI * 2, r0 = k % 4 == 0 ? 0.42f : 0.435f; L.Add(new Vector4(Mathf.Cos(a) * r0, Mathf.Sin(a) * r0, Mathf.Cos(a) * 0.455f, Mathf.Sin(a) * 0.455f)); }
            for (int g = 0; g < 16; g++)   // runes round the band
            {
                float a = (g + 0.5f) / 16f * Mathf.PI * 2, rc = 0.37f; float cx = Mathf.Cos(a) * rc, cy = Mathf.Sin(a) * rc; var tx = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)); var rx = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                int n = 2 + rng.Next(3);
                for (int s = 0; s < n; s++)
                {
                    float x0 = (rf() - 0.5f) * 0.06f, y0 = (rf() - 0.5f) * 0.07f, x1 = (rf() - 0.5f) * 0.06f, y1 = (rf() - 0.5f) * 0.07f;
                    if (s == 0) { x0 = 0; x1 = 0; y0 = -0.035f; y1 = 0.035f; }
                    L.Add(new Vector4(cx + tx.x * x0 + rx.x * y0, cy + tx.y * x0 + rx.y * y0, cx + tx.x * x1 + rx.x * y1, cy + tx.y * x1 + rx.y * y1));
                }
            }
            return L;
        }
        static void Cracks(float[] A, float[] Rc, System.Random rng)
        {
            System.Func<float> rf = () => (float)rng.NextDouble();
            var core = new float[CS * CS]; var glow = new float[CS * CS];
            var stack = new Stack<Vector4>();   // x, y, angle, length left
            int arms = 11;
            for (int k = 0; k < arms; k++) { float a = k / (float)arms * Mathf.PI * 2 + (rf() - 0.5f) * 0.4f; stack.Push(new Vector4(0.5f + Mathf.Cos(a) * 0.04f, 0.5f + Mathf.Sin(a) * 0.04f, a, 0.3f + rf() * 0.16f)); }
            int guard = 0;
            while (stack.Count > 0 && guard++ < 200)
            {
                var s = stack.Pop(); float x = s.x, y = s.y, a = s.z, len = s.w, total = len;
                while (len > 0)
                {
                    float th = Mathf.Lerp(3.4f, 1.2f, 1 - len / Mathf.Max(0.01f, total)) * (total > 0.2f ? 1 : 0.75f);
                    Stamp(core, glow, x, y, th); a += (rf() - 0.5f) * 0.38f; x += Mathf.Cos(a) * 0.003f; y += Mathf.Sin(a) * 0.003f; len -= 0.003f;
                    if (rf() < 0.018f && total > 0.12f) stack.Push(new Vector4(x, y, a + (rf() < 0.5f ? 0.7f : -0.7f), len * (0.4f + rf() * 0.4f)));
                    if (Mathf.Abs(x - 0.5f) > 0.47f || Mathf.Abs(y - 0.5f) > 0.47f) break;
                }
            }
            for (int y = 0; y < CS; y++)
                for (int x = 0; x < CS; x++)
                {
                    int i = y * CS + x; float px = (x + 0.5f) / CS - 0.5f, py = (y + 0.5f) / CS - 0.5f, d = Mathf.Sqrt(px * px + py * py);
                    float crater = Mathf.Exp(-Sq((d - 0.05f) / 0.02f)) * 0.6f + Mathf.Exp(-d * d * 300) * 0.5f, fade = 1 - SS(0.4f, 0.5f, d);
                    A[i] = Mathf.Max(core[i], glow[i] * 0.75f + crater) * fade; Rc[i] = Mathf.Max(core[i] * 0.9f, crater * 0.7f) * fade;
                }
        }
        static void Stamp(float[] core, float[] glow, float x, float y, float th)
        {
            int cx = (int)(x * CS), cy = (int)(y * CS), R = 9;
            for (int dy = -R; dy <= R; dy++)
                for (int dx = -R; dx <= R; dx++)
                {
                    int X = cx + dx, Y = cy + dy; if (X < 0 || Y < 0 || X >= CS || Y >= CS) continue;
                    float d = Mathf.Sqrt(dx * dx + dy * dy); int i = Y * CS + X;
                    core[i] = Mathf.Max(core[i], Mathf.Exp(-Sq(d / (th * 0.75f)))); glow[i] = Mathf.Max(glow[i], Mathf.Exp(-d / 3.2f));
                }
        }
    }
}
