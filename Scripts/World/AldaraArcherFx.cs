using System.Collections.Generic;
using UnityEngine;
using KF = Aldara.AldaraKnightFx;

namespace Aldara
{
    // The archer's enhanced effects (Graphics, Enhanced effects), on the same 3D effects as the knight's and the mage's:
    // every arrow flies as an arrow of light with a wind streak behind it (gold for a plain shot, the skill's colour for
    // a skill's) and strikes with a spark and a flash; the bow flares as it looses. Piercing Arrow tears a blazing line
    // through everything in its path; Rain of Arrows falls out of the sky onto a sigil and leaves the ground bristling;
    // Frost Arrow bursts ice; Poison Arrow bursts into a toxic cloud; Explosive Arrow and the Explosive Trap blow up in
    // fire (the trap first sits armed on its turning sigil); Starfall Arrow calls a falling star down in a pillar of
    // light; Eagle Eye wreathes the archer in gold; Evasive Roll leaves a streak of wind.
    public static class AldaraArcherFx
    {
        public static bool On { get { var H = AldaraHero.I; return KF.Live && H && H.cls == "archer"; } }
        static Color Hx(string h) { return AldaraRules.Hex(h); }
        static float R() { return Random.value; }
        static float U(float px) { return px / AldaraWorld.PX; }
        static Color Hot(Color c, float k) { return new Color(Mathf.Lerp(c.r, 1, k), Mathf.Lerp(c.g, 1, k), Mathf.Lerp(c.b, 1, k), 1); }
        static Vector3 Hero() { var P = AldaraPlayer.I; return KF.G(P.x, P.y); }
        static Vector3 Bow() { var P = AldaraPlayer.I; return KF.G(P.x, P.y) + Vector3.up * 0.95f + KF.Fwd(P.facing) * 0.5f; }
        static void Light(Vector3 at, Color c, float i, float r, float life) { AldaraVfxPlus.Flash(at, Hot(c, 0.25f), i, r, life); }
        static void Later(float t, System.Action fn) { if (AldaraSkills.I) AldaraSkills.I.Later_(t, fn); else fn(); }
        static readonly Color GOLD = new Color(1f, 0.84f, 0.45f), WIND = new Color(0.86f, 0.95f, 0.88f), ICE = new Color(0.56f, 0.87f, 1f), FROST = new Color(0.84f, 0.96f, 1f),
            VENOM = new Color(0.45f, 0.85f, 0.25f), FIRE = new Color(1f, 0.52f, 0.18f), EMBER = new Color(1f, 0.74f, 0.32f), STAR = new Color(1f, 0.95f, 0.66f),
            DUST = new Color(0.45f, 0.4f, 0.33f, 0.45f);

        // ---------- arrows ----------
        /// an arrow leaving the bow: glow is the skill's colour (null for a plain shot)
        public static KF.Fx Proj(string glow)
        {
            if (!On) return null; var b = Bow(); var c = glow != null ? Hx(glow) : GOLD;
            var f = KF.Orb(b, glow != null ? 0.1f : 0.085f, c, 4); f.c2 = Hot(c, glow != null ? 0.7f : 0.8f); f.hot = glow != null ? 1 : 0; f.w = glow != null ? 0.2f : 0.11f; f.bright = glow != null ? 1.9f : 1.6f;
            return f;
        }
        public static void Move(KF.Fx f, float x, float y) { if (f != null) KF.Move(f, KF.G(x, y) + Vector3.up * 0.9f); }
        /// where an arrow strikes
        public static void Impact(AldaraMonsters.Mon m, string glow)
        {
            if (!On || m == null) return; var c = glow != null ? Hx(glow) : GOLD; var p = KF.G(m.x, m.y) + Vector3.up * U(AldaraMonsters.Chest(m)); var hot = Hot(c, 0.55f);
            KF.Flare(p, glow != null ? 1.0f : 0.7f, hot, 0.15f, 0, 2.1f); AldaraVfxPlus.SparkBurst(p, hot, glow != null ? 10 : 6, 4.5f);
            if (glow != null) KF.Ring(KF.G(m.x, m.y), 0.15f, 1.0f, 0.3f, hot, 0.3f, 0, 1.1f);
            Light(p, c, glow != null ? 1.4f : 0.8f, 3.5f, 0.15f);
        }

        // ---------- skills drawn from AldaraSkills ----------
        /// Piercing Arrow's blazing line from the archer through everything in its path
        public static void Pierce(float sx, float sy, float ang, float len)
        {
            if (!On) return; var a = KF.G(sx, sy) + Vector3.up * 0.9f; var b = KF.G(sx + Mathf.Cos(ang) * len, sy + Mathf.Sin(ang) * len) + Vector3.up * 0.9f;
            var c = Hx("#ffd35a"); KF.Line(a, b, 0.9f, c, 0.4f, 1.6f); KF.Line(a, b, 0.28f, Color.white, 0.3f, 2f);
            for (int k = 1; k < 5; k++) { var p = Vector3.Lerp(a, b, k / 5f); KF.Ring(p - Vector3.up * 0.9f, 0.1f, 0.9f, 0.25f, Hot(c, 0.4f), 0.3f, k * 0.03f, 1.1f); AldaraVfxPlus.SparkBurst(p, Hot(c, 0.5f), 3, 3, 0.25f); }
            KF.Flare(a, 1.6f, Hot(c, 0.6f), 0.2f, 0, 2.2f); Light(Vector3.Lerp(a, b, 0.5f), c, 2.6f, len / AldaraWorld.PX * 0.6f + 3, 0.3f);
        }
        /// Rain of Arrows: a sigil on the ground and arrows of light falling onto it out of the sky
        public static void Rain(float x, float y, float radius, float fall)
        {
            if (!On) return; var g = KF.G(x, y); float rr = Mathf.Max(0.9f, U(radius));
            KF.Decal(g, KF.C_RUNE, rr * 0.95f, GOLD, Hot(GOLD, 0.4f), fall + 0.6f, 1.4f, 0, 0.9f); KF.Ring(g, rr * 1.15f, rr * 0.6f, 0.25f, GOLD, fall, 0, 1.0f);
            for (int k = 0; k < 18; k++)
            {
                float a = R() * Mathf.PI * 2, d = Mathf.Sqrt(R()) * rr; var at = g + new Vector3(Mathf.Cos(a) * d, 0.05f, Mathf.Sin(a) * d);
                var from = at + new Vector3(-0.25f + R() * 0.1f, 1f, 0.15f).normalized * (8 + R() * 3);
                float dl = R() * 0.18f; KF.Fall(from, at, Mathf.Max(0.15f, fall - dl), 0.085f, Hx("#ffe6a0"), 4, () =>
                {
                    KF.Shard(at, new Vector3(R() - 0.5f, 0, R() - 0.5f) * 0.5f, 0.45f + R() * 0.15f, 0.07f, Hx("#e8d8b0"), 1.6f);   // stuck in the ground
                    AldaraVfxPlus.SparkBurst(at + Vector3.up * 0.1f, Hot(GOLD, 0.5f), 2, 2.5f, 0.2f); if (R() < 0.3f) AldaraVfxPlus.Dust(at, 0.25f, 2, DUST);
                }, dl);
            }
        }

        /// an archer skill's look (pfxSkill): returns false for anything it does not draw, which then keeps its old look
        public static bool Skill(string id, AldaraMonsters.Mon t, float tx, float ty, SkillDef sk, string ph, string col)
        {
            float rad = sk != null ? sk.radius : 0, rr = Mathf.Max(0.8f, U(rad)); var tg = KF.G(tx, ty); float th = t != null ? U(AldaraMonsters.Chest(t)) : 0.7f; var hero = Hero();
            switch (id)
            {
                case "muzzle":
                    {   // the bow flares as it looses
                        var b = Bow(); var c = col != null ? Hx(col) : Hx("#fff0c0"); var P = AldaraPlayer.I;
                        KF.Flare(b, 0.9f, Hot(c, 0.4f), 0.12f, 0, 2f); KF.Plate(b, KF.Fwd(P.facing), 0.5f, 0.25f, Hot(GOLD, 0.3f), 0.18f, KF.C_RING);
                        AldaraVfxPlus.SparkBurst(b, Hot(GOLD, 0.5f), 3, 2.5f, 0.2f); Light(b, GOLD, 0.9f, 3, 0.1f);
                        return true;
                    }
                case "frost_arrow":
                    {
                        for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2 + R() * 0.5f; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); KF.Shard(tg + dir * 0.35f, dir, 0.7f + R() * 0.5f, 0.26f + R() * 0.12f, Hot(ICE, 0.25f), 1.1f + R() * 0.3f, R() * 0.05f); }
                        KF.Flare(tg + Vector3.up * th, 1.6f, FROST, 0.2f, 0, 2f); KF.Ring(tg, 0.2f, 1.3f, 0.4f, FROST, 0.4f, 0, 1.2f);
                        AldaraVfxPlus.Shards(tg + Vector3.up * 0.4f, FROST, 10, 4); AldaraVfxPlus.Smoke(tg + Vector3.up * 0.2f, new Color(0.86f, 0.95f, 1f, 0.3f), 4, 0.5f, 0.3f, 0.5f);
                        Light(tg + Vector3.up, ICE, 2.2f, 5, 0.3f);
                        return true;
                    }
                case "rain_land":
                    KF.Ring(tg, 0.3f, rr * 1.1f, 0.5f, Hot(GOLD, 0.3f), 0.4f); KF.Wall(tg, 0.3f, rr, 0.6f, GOLD, 0.35f);
                    AldaraVfxPlus.Dust(tg, rr * 0.8f, 12, DUST); AldaraVfxPlus.SparkBurst(tg + Vector3.up * 0.3f, Hot(GOLD, 0.5f), 14, 5); Light(tg + Vector3.up, GOLD, 2.6f, rr * 2 + 4, 0.35f);
                    return true;
                case "poison_arrow":
                    {   // a toxic splash and a cloud that hangs on the target
                        var p = tg + Vector3.up * th;
                        KF.Glow(p, 1.6f, VENOM, 0.35f, 0, 1.3f); KF.Flare(p, 1.2f, Hot(VENOM, 0.5f), 0.18f, 0, 2f);
                        KF.Decal(tg, KF.C_CRACK, 1.1f, VENOM, Hot(VENOM, 0.4f), 2.4f, 0, 0, 1.0f);
                        AldaraVfxPlus.Smoke(p, new Color(0.3f, 0.55f, 0.15f, 0.45f), 10, 0.7f, 0.35f, 0.7f); AldaraVfxPlus.Motes(p, Hot(VENOM, 0.3f), 16, 0.6f, 1.6f);
                        AldaraVfxPlus.SparkBurst(p, Hot(VENOM, 0.4f), 8, 3.5f); Light(p, VENOM, 2.2f, 5, 0.5f);
                        return true;
                    }
                case "explosive_arrow": FireBlast(tg, rr, 1); AldaraVfx.Shake(3, 0.2f); return true;
                case "trap_set":
                    {   // the trap armed on its turning sigil, ticking
                        KF.Decal(tg, KF.C_RUNE, 1.1f, FIRE, EMBER, 0.95f, 3f, 0, 1.2f); KF.Ring(tg, 1.3f, 0.6f, 0.2f, FIRE, 0.8f, 0, 1.1f);
                        KF.Flare(tg + Vector3.up * 0.25f, 0.8f, Hot(FIRE, 0.5f), 0.85f, 0, 1.6f); AldaraVfxPlus.Embers(tg, EMBER, 8, 0.3f, 0.8f);
                        Light(tg + Vector3.up * 0.5f, FIRE, 1.2f, 3, 0.85f);
                        return true;
                    }
                case "trap_boom": FireBlast(tg, rr, 1.3f); KF.Quake(tg, rr * 0.8f, FIRE, EMBER, 0); return true;
                case "starfall_arrow":
                    {   // a falling star in a pillar of light
                        KF.Fall(tg + new Vector3(-0.3f, 1f, 0.2f).normalized * 14, tg + Vector3.up * 0.3f, 0.22f, 0.6f, STAR, 0);
                        Later(0.2f, () =>
                        {
                            KF.Beam(tg, rr * 0.5f, 10, GOLD, 0.7f, 0, 1.2f); KF.Beam(tg, rr * 0.2f, 10, STAR, 0.6f, 0, 1.9f);
                            KF.Quake(tg, rr, GOLD, STAR, 0); KF.Flare(tg + Vector3.up * 1.0f, 3.2f, Hot(STAR, 0.5f), 0.3f, 0, 1.8f);
                            KF.Decal(tg, KF.C_RUNE, rr * 0.9f, GOLD, STAR, 1.0f, 1.2f, 0, 1.1f);
                            AldaraVfxPlus.Motes(tg + Vector3.up * 0.3f, STAR, 28, rr * 0.7f, 2.6f); AldaraVfxPlus.SparkBurst(tg + Vector3.up * 0.8f, STAR, 22, 7);
                            Light(tg + Vector3.up * 2, GOLD, 5f, rr * 3 + 6, 0.6f); AldaraVfx.Shake(4, 0.25f);
                        });
                        return true;
                    }
                case "eagle_eye":
                    KF.Decal(hero, KF.C_RUNE, 1.7f, GOLD, Color.white, 1.2f, 1.4f, 0, 1.2f, true); KF.Helix(0.6f, 2.2f, 2, GOLD, 1.0f).bright = 1.2f;
                    KF.Flare(hero + Vector3.up * 1.45f, 1.4f, Hot(GOLD, 0.5f), 0.4f, 0, 2f); AldaraVfxPlus.Motes(hero + Vector3.up * 0.3f, Hot(GOLD, 0.3f), 22, 0.7f, 1.6f);
                    Light(hero + Vector3.up, GOLD, 2.4f, 6, 0.6f);
                    return true;
                case "evasive_roll":
                    if (ph == "cast") { rollFrom = hero; rollSet = true; AldaraVfxPlus.Dust(hero, 0.6f, 8, DUST); KF.Ring(hero, 0.2f, 1.2f, 0.3f, WIND, 0.3f); }
                    else
                    {
                        if (rollSet) { KF.Line(rollFrom + Vector3.up * 0.5f, hero + Vector3.up * 0.5f, 0.8f, WIND, 0.32f, 1.3f); KF.Line(rollFrom + Vector3.up * 0.5f, hero + Vector3.up * 0.5f, 0.2f, Color.white, 0.24f, 1.6f); rollSet = false; }
                        AldaraVfxPlus.Dust(hero, 0.7f, 10, DUST); KF.Ring(hero, 0.2f, 1.4f, 0.35f, WIND, 0.35f); AldaraVfxPlus.Motes(hero + Vector3.up * 0.4f, WIND, 10, 0.5f, 1f);
                    }
                    return true;
            }
            return false;
        }
        static Vector3 rollFrom; static bool rollSet;

        static void FireBlast(Vector3 g, float rr, float big)
        {
            var at = g + Vector3.up * 0.5f; float sb = Mathf.Sqrt(big);
            KF.Glow(at, rr * 1.1f * sb, FIRE, 0.3f, 0, 1.3f); KF.Glow(at, rr * 0.55f * sb, EMBER, 0.2f, 0, 1.6f); KF.Flare(at, 2.4f * sb, Hot(EMBER, 0.5f), 0.22f, 0, 1.8f);
            KF.Ring(g, 0.3f, rr * 1.1f, 0.7f, Hot(FIRE, 0.3f), 0.45f); KF.Wall(g, 0.3f, rr * 0.9f, 1.0f * big, FIRE, 0.38f);
            KF.Decal(g, KF.C_CRACK, rr * 0.8f, new Color(1, 0.35f, 0.1f), EMBER, 2.6f, 0, 0, 1.2f);
            AldaraVfxPlus.Embers(at, EMBER, (int)(22 * big), rr * 0.4f, 2f); AldaraVfxPlus.Smoke(at, new Color(0.17f, 0.14f, 0.13f, 0.5f), (int)(8 * big), rr * 0.35f, 0.9f, 0.55f * big);
            AldaraVfxPlus.SparkBurst(at, EMBER, (int)(20 * big), 7 * big); Light(at + Vector3.up, FIRE, 4f * big, rr * 3 + 4, 0.5f);
        }

        // ---------- the archer's subclass pieces ----------
        /// a subclass strike for an archer: a shot of light straight into the target
        public static void SubShot(AldaraMonsters.Mon m, Color c)
        {
            var b = Bow(); var p = KF.G(m.x, m.y) + Vector3.up * U(AldaraMonsters.Chest(m)); var hot = Hot(c, 0.5f);
            KF.Line(b, p, 0.7f, c, 0.3f, 1.6f); KF.Line(b, p, 0.2f, Color.white, 0.22f, 2f); KF.Flare(b, 1.1f, hot, 0.14f, 0, 2f);
            KF.Flare(p, 2.1f, hot, 0.22f, 0.04f, 2.3f); KF.Ring(KF.G(m.x, m.y), 0.2f, 1.6f, 0.45f, hot, 0.36f, 0.04f); AldaraVfxPlus.SparkBurst(p, hot, 16, 6); Light(p, c, 3, 6, 0.3f);
        }
    }
}
