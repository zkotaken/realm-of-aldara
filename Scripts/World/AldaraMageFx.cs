using System.Collections.Generic;
using UnityEngine;
using KF = Aldara.AldaraKnightFx;

namespace Aldara
{
    // The mage's enhanced effects (Graphics, Enhanced effects), on the same 3D effects as the knight's (AldaraKnightFx):
    // every bolt, fireball, ice shard and arcane missile flies as a glowing 3D shot with a trail (fire sheds embers and
    // smoke, ice is a spinning crystal, arcane wraps a turning rune) and bursts with a flare and light where it lands;
    // Fireball blooms into a fiery blast that scorches the ground; Ice Shard and Frost Nova burst ice crystals out of
    // the ground; Chain Lightning forks and re-strikes between its targets; Meteor falls burning out of the sky onto
    // its marked sigil and cracks the earth; Blizzard rains falling ice over a frost sigil; Arcane Cataclysm gathers
    // over a turning violet sigil and erupts in a pillar of light; Rejuvenate spirals green light up round the mage,
    // Arcane Barrier raises a blue hex ward, and Blink leaves a streak of violet light between where you were and are.
    public static class AldaraMageFx
    {
        public static bool On { get { var H = AldaraHero.I; return KF.Live && H && H.cls == "mage"; } }
        static Color Hx(string h) { return AldaraRules.Hex(h); }
        static float R() { return Random.value; }
        static float U(float px) { return px / AldaraWorld.PX; }
        static Color Hot(Color c, float k) { return new Color(Mathf.Lerp(c.r, 1, k), Mathf.Lerp(c.g, 1, k), Mathf.Lerp(c.b, 1, k), 1); }
        static Vector3 Hero() { var P = AldaraPlayer.I; return KF.G(P.x, P.y); }
        static Vector3 Hand() { var P = AldaraPlayer.I; return KF.G(P.x, P.y) + Vector3.up * 1.05f + KF.Fwd(P.facing) * 0.45f; }
        static void Light(Vector3 at, Color c, float i, float r, float life) { AldaraVfxPlus.Flash(at, Hot(c, 0.25f), i, r, life); }
        static void Later(float t, System.Action fn) { if (AldaraSkills.I) AldaraSkills.I.Later_(t, fn); else fn(); }
        static readonly Color FIRE = new Color(1f, 0.5f, 0.16f), EMBER = new Color(1f, 0.72f, 0.3f), ICE = new Color(0.56f, 0.86f, 1f), FROST = new Color(0.82f, 0.95f, 1f),
            ARC = new Color(0.75f, 0.48f, 1f), BOLT = new Color(0.54f, 0.71f, 1f), HEAL = new Color(0.5f, 1f, 0.69f), WARD = new Color(0.54f, 0.71f, 1f);

        // ---------- shots ----------
        /// a shot leaving the mage's hand (bolt, fireball, ice; a coloured bolt is an arcane missile)
        public static KF.Fx Proj(string kind, string color)
        {
            if (!On) return null; var h = Hand();
            int n = kind == "fireball" ? 1 : kind == "ice" ? 2 : color != null ? 3 : 0;
            var c = n == 1 ? FIRE : n == 2 ? ICE : n == 3 ? Hx(color) : BOLT; float size = n == 1 ? 0.42f : n == 2 ? 0.26f : n == 3 ? 0.2f : 0.22f;
            KF.Flare(h, n == 1 ? 1.2f : 0.8f, Hot(c, 0.4f), 0.14f, 0, 2f); Light(h, c, 1.2f, 3, 0.12f);
            return KF.Orb(h, size, c, n);
        }
        public static void Move(KF.Fx f, float x, float y) { if (f != null) KF.Move(f, KF.G(x, y) + Vector3.up * 0.95f); }
        public static void End(KF.Fx f) { KF.End(f); }

        static void Burst(Vector3 at, Color c, float size)
        {
            var hot = Hot(c, 0.5f); KF.Flare(at, size, hot, 0.2f, 0, 2.2f); KF.Glow(at, size * 0.8f, c, 0.22f, 0, 1.4f);
            AldaraVfxPlus.SparkBurst(at, hot, 10, 4.5f); Light(at, c, 1.8f, 4, 0.2f);
        }
        static void FireBlast(Vector3 g, float rr, float big)
        {
            var at = g + Vector3.up * 0.5f;
            KF.Glow(at, rr * 1.1f * Mathf.Sqrt(big), FIRE, 0.3f, 0, 1.3f); KF.Glow(at, rr * 0.55f * Mathf.Sqrt(big), EMBER, 0.2f, 0, 1.6f); KF.Flare(at, 2.4f * Mathf.Sqrt(big), Hot(EMBER, 0.5f), 0.22f, 0, 1.8f);
            KF.Ring(g, 0.3f, rr * 1.1f, 0.7f, Hot(FIRE, 0.3f), 0.45f); KF.Wall(g, 0.3f, rr * 0.9f, 1.0f * big, FIRE, 0.38f);
            KF.Decal(g, KF.C_CRACK, rr * 0.8f, new Color(1, 0.35f, 0.1f), EMBER, 2.6f, 0, 0, 1.2f);
            KF.Beam(g, rr * 0.35f, 2.6f * big, FIRE, 0.32f, 0, 1.2f);
            AldaraVfxPlus.Embers(at, EMBER, (int)(26 * big), rr * 0.4f, 2.2f); AldaraVfxPlus.Smoke(at, new Color(0.17f, 0.14f, 0.13f, 0.5f), (int)(8 * big), rr * 0.35f, 0.9f, 0.55f * big);
            AldaraVfxPlus.SparkBurst(at, EMBER, (int)(22 * big), 7 * big); Light(at + Vector3.up, FIRE, 4.2f * big, rr * 3 + 4, 0.5f);
        }
        static void IceBurst(Vector3 g, float rr, int n, float hgt)
        {
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2 + R() * 0.4f, d = rr * (0.45f + R() * 0.55f); var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                KF.Shard(g + dir * d * 0.5f, dir, hgt * (0.6f + R() * 0.6f), 0.32f + R() * 0.2f, Hot(ICE, 0.25f), 1.1f + R() * 0.4f, R() * 0.06f);
            }
            KF.Flare(g + Vector3.up * 0.6f, 2f, FROST, 0.22f, 0, 2f); KF.Ring(g, 0.2f, rr * 1.1f, 0.5f, FROST, 0.4f, 0, 1.2f);
            AldaraVfxPlus.Shards(g + Vector3.up * 0.4f, FROST, 14, 4.5f); AldaraVfxPlus.Smoke(g + Vector3.up * 0.2f, new Color(0.85f, 0.94f, 1f, 0.32f), 6, rr * 0.5f, 0.3f, 0.6f);
            Light(g + Vector3.up, ICE, 2.4f, rr * 2 + 3, 0.35f);
        }

        // ---------- the chain's lightning ----------
        public static void Chain(List<Vector2> pts)
        {
            if (!On || pts == null || pts.Count < 2) return; var nodes = new List<Vector3> { Hand() };
            for (int i = 1; i < pts.Count; i++) nodes.Add(KF.G(pts[i].x, pts[i].y) + Vector3.up * 0.8f);
            KF.Bolt(nodes, BOLT, 0.42f, 0.09f); KF.Bolt(new List<Vector3>(nodes), Hx("#cfe0ff"), 0.3f, 0.05f);
            Light(nodes[0], BOLT, 2f, 4, 0.15f);
            for (int i = 1; i < nodes.Count; i++) Light(nodes[i], BOLT, 3f, 5, 0.2f);
        }

        static Vector3 blinkFrom; static bool blinkSet;
        static Vector3 Sky(Vector3 g, float h) { return g + new Vector3(-0.35f, 1f, 0.2f).normalized * h; }

        /// a mage spell's look (pfxSkill): returns false for anything it does not draw, which then keeps its old look
        public static bool Skill(string id, AldaraMonsters.Mon t, float tx, float ty, SkillDef sk, string ph, string col)
        {
            float rad = sk != null ? sk.radius : 0, rr = Mathf.Max(0.8f, U(rad)); var tg = KF.G(tx, ty); float th = t != null ? U(AldaraMonsters.Chest(t)) : 0.7f; var hero = Hero();
            switch (id)
            {
                case "bolt_hit": Burst(tg + Vector3.up * th, col != null ? Hx(col) : BOLT, 1.4f); KF.Ring(tg, 0.15f, 1.0f, 0.3f, Hot(col != null ? Hx(col) : BOLT, 0.3f), 0.3f, 0, 1.1f); return true;
                case "fireball": FireBlast(tg, rr, 1); AldaraVfx.Shake(2.5f, 0.18f); return true;
                case "ice_shard": IceBurst(tg, 1.0f, 7, 1.3f); return true;
                case "chain_node": Burst(tg + Vector3.up * th, Hx("#cfe0ff"), 1.2f); return true;
                case "meteor_mark":
                    {
                        KF.Decal(tg, KF.C_RUNE, rr * 0.95f, FIRE, EMBER, 0.8f, 2.4f, 0, 1.1f); KF.Ring(tg, rr * 1.2f, rr * 0.5f, 0.3f, FIRE, 0.6f, 0, 1.1f);
                        KF.Fall(Sky(tg, 16), tg + Vector3.up * 0.3f, 0.58f, 0.85f, FIRE, 1);
                        return true;
                    }
                case "meteor":
                    {
                        FireBlast(tg, rr, 1.5f); KF.Quake(tg, rr, FIRE, EMBER, 0);
                        KF.Beam(tg, rr * 0.45f, 7, FIRE, 0.5f, 0, 0.9f); KF.Beam(tg, rr * 0.18f, 7, EMBER, 0.4f, 0, 1.3f);
                        return true;
                    }
                case "frost_nova":
                    {
                        var c = hero; int n = 18;
                        for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); float d = rr * (0.55f + R() * 0.35f); KF.Shard(c + dir * d, dir, 0.9f + R() * 0.8f, 0.36f + R() * 0.2f, Hot(ICE, 0.25f), 1.3f + R() * 0.3f, d / rr * 0.08f); }
                        KF.Ring(c, 0.3f, rr * 1.1f, 0.8f, FROST, 0.5f); KF.Wall(c, 0.4f, rr, 1.3f, ICE, 0.45f); KF.Decal(c, KF.C_RUNE, rr * 0.6f, ICE, ICE, 1.1f, 1.6f, 0, 0.8f);
                        AldaraVfxPlus.Smoke(c + Vector3.up * 0.2f, new Color(0.86f, 0.95f, 1f, 0.35f), 12, rr * 0.7f, 0.25f, 0.7f); AldaraVfxPlus.Shards(c + Vector3.up * 0.5f, FROST, 20, 6);
                        KF.Flare(c + Vector3.up * 0.9f, 2.6f, FROST, 0.25f); Light(c + Vector3.up, ICE, 3.2f, rr * 2.5f + 3, 0.5f);
                        return true;
                    }
                case "blizzard_wave":
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            float a = R() * Mathf.PI * 2, d = Mathf.Sqrt(R()) * rr * 0.85f; var at = tg + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                            KF.Fall(Sky(at, 7 + R() * 3), at + Vector3.up * 0.1f, 0.22f + R() * 0.1f, 0.22f, ICE, 2, () => { KF.Shard(at, new Vector3(R() - 0.5f, 0, R() - 0.5f), 0.6f + R() * 0.4f, 0.28f, Hot(ICE, 0.3f), 0.8f); AldaraVfxPlus.Shards(at + Vector3.up * 0.2f, FROST, 4, 3); AldaraVfxPlus.Smoke(at, new Color(0.88f, 0.95f, 1f, 0.3f), 1, 0.2f, 0.2f, 0.45f); }, R() * 0.2f);
                        }
                        KF.Ring(tg, 0.4f, rr, 0.5f, FROST, 0.45f, 0.2f, 1.0f); Light(tg + Vector3.up, ICE, 1.6f, rr * 2 + 3, 0.4f);
                        return true;
                    }
                case "cata_charge":
                    {
                        KF.Decal(tg, KF.C_RUNE, rr, ARC, Hot(ARC, 0.3f), 0.75f, 3.2f, 0, 1.0f); KF.Ring(tg, rr * 1.3f, rr * 0.2f, 0.35f, ARC, 0.6f, 0, 1.2f);
                        var sp = KF.Spin(0.8f, rr * 0.7f, 0.12f, 0, 14, ARC, 0.6f, 0, KF.C_WIND, 0.5f, 1.2f); sp.follow = false; sp.at = tg;
                        AldaraVfxPlus.Motes(tg + Vector3.up * 0.2f, Hot(ARC, 0.3f), 28, rr * 0.8f, 2.4f); Light(tg + Vector3.up, ARC, 2.2f, rr * 2 + 3, 0.6f);
                        return true;
                    }
                case "cataclysm":
                    {
                        KF.Beam(tg, rr * 0.55f, 12, ARC, 0.85f, 0, 1.1f); KF.Beam(tg, rr * 0.22f, 12, Hx("#f6e4ff"), 0.7f, 0, 1.8f);
                        KF.Quake(tg, rr, ARC, Hx("#f6e4ff"), 0); KF.Ring(tg, 0.3f, rr * 1.5f, 0.9f, Hot(ARC, 0.3f), 0.6f);
                        KF.Flare(tg + Vector3.up * 1.2f, 3.4f, Hot(ARC, 0.5f), 0.32f, 0, 1.7f); KF.Glow(tg + Vector3.up * 0.8f, rr * 1.6f, ARC, 0.4f, 0, 1.5f);
                        AldaraVfxPlus.Motes(tg + Vector3.up * 0.3f, Hot(ARC, 0.3f), 30, rr * 0.7f, 3f); Light(tg + Vector3.up * 2, ARC, 5f, rr * 3 + 6, 0.7f);
                        return true;
                    }
                case "rejuvenate":
                    KF.Decal(hero, KF.C_RUNE, 1.9f, HEAL, HEAL, 1.5f, 1.2f, 0, 0.9f, true); KF.Helix(0.75f, 2.5f, 3, HEAL, 1.2f).bright = 1.2f;
                    AldaraVfxPlus.Motes(hero + Vector3.up * 0.3f, Hx("#c8ffd8"), 30, 0.8f, 1.8f); Light(hero + Vector3.up, HEAL, 2.4f, 6, 0.9f);
                    return true;
                case "arcane_barrier":
                    KF.Dome(1.15f, 0.75f, WARD, 1.2f).bright = 0.75f; KF.Decal(hero, KF.C_RUNE, 1.7f, WARD, WARD, 1.1f, -1.8f, 0, 0.9f, true);
                    KF.Flare(hero + Vector3.up * 0.85f, 2.0f, Hot(WARD, 0.5f), 0.25f); AldaraVfxPlus.Motes(hero + Vector3.up * 0.4f, Hx("#cfe0ff"), 18, 0.7f, 1.2f); Light(hero + Vector3.up, WARD, 2.6f, 6, 0.5f);
                    return true;
                case "blink":
                    if (ph == "cast") { blinkFrom = hero; blinkSet = true; KF.Flare(hero + Vector3.up * 0.9f, 2f, Hot(ARC, 0.5f), 0.22f); KF.Ring(hero, 0.2f, 1.4f, 0.4f, ARC, 0.35f); AldaraVfxPlus.Motes(hero + Vector3.up * 0.5f, ARC, 16, 0.5f, 1.2f); }
                    else
                    {
                        if (blinkSet) { KF.Line(blinkFrom + Vector3.up * 0.9f, hero + Vector3.up * 0.9f, 0.7f, ARC, 0.35f, 1.7f); KF.Line(blinkFrom + Vector3.up * 0.9f, hero + Vector3.up * 0.9f, 0.2f, Color.white, 0.25f, 1.8f); blinkSet = false; }
                        KF.Flare(hero + Vector3.up * 0.9f, 2.2f, Hot(ARC, 0.5f), 0.24f); KF.Ring(hero, 0.2f, 1.6f, 0.45f, ARC, 0.4f); AldaraVfxPlus.Motes(hero + Vector3.up * 0.5f, ARC, 18, 0.6f, 1.4f);
                        Light(hero + Vector3.up, ARC, 2.4f, 5, 0.3f);
                    }
                    return true;
            }
            return false;
        }
        /// Blizzard's sigil and drifting snow over the whole storm
        public static void Blizzard(float x, float y, float radius, float dur)
        {
            if (!On) return; var g = KF.G(x, y); float rr = Mathf.Max(1, U(radius));
            KF.Decal(g, KF.C_RUNE, rr, ICE, ICE, dur + 0.3f, 0.6f, 0, 0.85f);
            var sp = KF.Spin(1.6f, rr * 0.8f, 0.08f, 0, 3, FROST, dur, 0, KF.C_WIND, 0.5f, 0.9f); sp.follow = false; sp.at = g;
            AldaraVfxPlus.Smoke(g + Vector3.up * 0.5f, new Color(0.88f, 0.95f, 1f, 0.28f), 14, rr * 0.8f, 0.15f, 0.9f);
        }
    }
}
