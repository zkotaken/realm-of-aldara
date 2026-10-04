using UnityEngine;

namespace Aldara
{
    // The hero's effects, from the browser: pfxHit (the spark of every blow on a monster), knightSlash (the three-hit
    // combo's arcs and the overhead chop's crack), and pfxSkill (each class skill's look when it is cast and when it lands).
    public static class AldaraSkillFx
    {
        static Color Hx(string h) { return AldaraVfx.Hx(h); }
        static float R() { return Random.value; }
        const float PI = Mathf.PI, PH_Y = AldaraVfx.PH_Y, LIFT = AldaraVfx.LIFT;
        static void M(AldaraVfx.Mf f) { AldaraVfx.Mfx(f); }
        static void Later(float t, System.Action fn) { if (AldaraSkills.I) AldaraSkills.I.Later_(t, fn); else fn(); }

        /// the weapon's trail colour (WEAPON_LOOK glow, the Legendary or Mythical glow, its runes; '#ffe07a' bare-handed)
        public static Color TrailC
        {
            get
            {
                var H = AldaraHero.I; var w = H ? H.Eq("weapon") : null; if (w == null || H.cls != "knight") return Hx("#ffe07a");
                string g = null, ru = null;
                switch (w.name) { case "Runed Greatsword": ru = "#c07aff"; break; case "Dragonfang Blade": g = "#ff5a3a"; break; case "Frostbrand": g = "#8fdfff"; ru = "#ffffff"; break; case "Hellfire Cleaver": g = "#ff4a1a"; ru = "#ff7a2a"; break; case "Celestial Claymore": g = "#ffe07a"; ru = "#e0b64b"; break; case "Voidreaver": g = "#9a5aff"; ru = "#c07aff"; break; }
                string rg = w.rarity == "Legendary" ? "#ff9a2a" : w.rarity == "Mythical" ? "#ff3a7a" : null;
                return Hx(g ?? rg ?? ru ?? "#dfe8ff");
            }
        }

        static int pfxF = -1, pfxN;
        public static void PfxHit(AldaraMonsters.Mon m, Color c)
        {
            if (m == null) return; if (pfxF != Time.frameCount) { pfxF = Time.frameCount; pfxN = 0; } if (pfxN++ > 6) return;
            var P = AldaraPlayer.I; float h = AldaraMonsters.Chest(m), a0 = Mathf.Atan2(m.y - P.y, m.x - P.x);
            M(new AldaraVfx.Mf { k = "impact", x = m.x, y = m.y, h = h, a = a0, c = c, c2 = Color.white, T = 0.2f });
            for (int k = 0; k < 6; k++) { float a = a0 + (R() - 0.5f) * 1.6f, v = 150 + R() * 220; AldaraVfx.Mpush(new AldaraVfx.Mp { x = m.x, y = m.y, z = h, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, vz = 40 + R() * 140, g = 600, life = 0.35f, max = 0.35f, c = c, s = 1.8f, glow = true }); }
        }
        public static void KnightSlash(float tx, float ty, int cb, float dur)
        {
            var P = AldaraPlayer.I; var col = TrailC; float a = P.facing, reach = Mathf.Max(44, Mathf.Min(90, Mathf.Sqrt((tx - P.x) * (tx - P.x) + (ty - P.y) * (ty - P.y)) + 18)), dl = dur * 0.38f;
            if (cb == 2)
            {
                M(new AldaraVfx.Mf { k = "arcV", x = P.x, y = P.y, h = PH_Y * 1.6f, a = a, r = reach * 0.9f, c = col, c2 = Color.white, w = 17, T = 0.3f, dl = dl });
                Later(dur * 0.56f, () =>
                {
                    float ix = P.x + Mathf.Cos(a) * 38, iy = P.y + Mathf.Sin(a) * 38;
                    M(new AldaraVfx.Mf { k = "crack", x = ix, y = iy, a = a, len = 46, c = col, T = 1.3f, n = 3, r = 32 }); M(new AldaraVfx.Mf { k = "shock", x = ix, y = iy, r = 52, c = col, c2 = Color.white, T = 0.35f });
                    AldaraVfx.Debris(ix, iy, 6, Hx("#6a5a4a")); M(new AldaraVfx.Mf { k = "dust", x = ix, y = iy, r = 24, T = 0.6f });
                });
            }
            else
            {
                float hh = PH_Y * (cb == 1 ? 0.8f : 1.1f), aa = a + (cb == 0 ? -0.2f : 0.1f), dir = cb == 1 ? -1 : 1;
                M(new AldaraVfx.Mf { k = "arc", x = P.x, y = P.y, h = hh, a = aa, r = reach, arc = 1.15f, dir = dir, c = col, c2 = Color.white, w = 16, T = 0.34f, dl = dl });
                M(new AldaraVfx.Mf { k = "arc", x = P.x, y = P.y, h = hh, a = aa, r = reach * 1.15f, arc = 1.0f, dir = dir, c = col, c2 = col, w = 6, T = 0.3f, dl = dl + 0.03f });
            }
        }
        static void Rays(float x, float y, float h, float r, string c, int n, float T) { M(new AldaraVfx.Mf { k = "rays", x = x, y = y, h = h, r = r, c = Hx(c), n = n, T = T }); }
        static void Shock(float x, float y, float r, string c, string c2, float T) { M(new AldaraVfx.Mf { k = "shock", x = x, y = y, r = r, c = Hx(c), c2 = Hx(c2), T = T }); }
        static void Flash(float x, float y, float h, float r, string c, string c2, float T, bool follow = false) { M(new AldaraVfx.Mf { k = "flash", x = x, y = y, h = h, r = r, c = Hx(c), c2 = Hx(c2), T = T, follow = follow }); }
        static void Aura(float r, float H, string c, float T) { var P = AldaraPlayer.I; M(new AldaraVfx.Mf { k = "aura", follow = true, x = P.x, y = P.y, r = r, H = H, c = Hx(c), T = T }); }
        static void Glyph(string c, string type, float T) { var P = AldaraPlayer.I; M(new AldaraVfx.Mf { k = "glyph", follow = true, x = P.x, y = P.y, h = PH_Y * 2.6f, c = Hx(c), type = type, T = T }); }
        static void Dust(float x, float y, float r, float T) { M(new AldaraVfx.Mf { k = "dust", x = x, y = y, r = r, T = T }); }
        static void Shake(float a, float l) { AldaraVfx.Shake(a, l); }

        /// pfxSkill: t is the monster (or null), (tx, ty) where it lands; col is the colour argument some calls pass
        public static void Pfx(string id, AldaraMonsters.Mon t, float tx, float ty, SkillDef sk, string ph = null, string col = null)
        {
            var P = AldaraPlayer.I; float a = P.facing, th = t != null ? AldaraMonsters.Chest(t) : 18, rad = sk != null ? sk.radius : 0;
            if (t == null && tx == 0 && ty == 0) { tx = P.x; ty = P.y; }
            switch (id)
            {
                // knight
                case "power_strike":
                    M(new AldaraVfx.Mf { k = "arc", x = P.x, y = P.y, h = PH_Y * 1.2f, a = a, r = Mathf.Sqrt((tx - P.x) * (tx - P.x) + (ty - P.y) * (ty - P.y)) + 30, arc = 1.3f, dir = 1, c = Hx("#ffd35a"), c2 = Color.white, w = 22, T = 0.34f, dl = 0.08f });
                    Later(0.14f, () => { Rays(tx, ty, th, 70, "#ffe07a", 12, 0.35f); Shock(tx, ty, 60, "#ffd35a", "#ffffff", 0.4f); AldaraVfx.Sparks(tx, ty, th, a, 14, Hx("#ffe8a0"), 300, 1.4f); Shake(4, 0.2f); }); break;
                case "shield_bash":
                    M(new AldaraVfx.Mf { k = "bash", x = P.x, y = P.y, h = PH_Y, a = a, r = 90, c = Hx("#cfe0ff"), T = 0.35f });
                    Later(0.08f, () => { Flash(tx, ty, th, 34, "#cfe0ff", "#ffffff", 0.22f); AldaraVfx.Sparks(tx, ty, th, a, 16, Color.white, 320, 1.2f); M(new AldaraVfx.Mf { k = "ringA", x = tx, y = ty, h = th, r = 44, c = Hx("#ffe07a"), T = 0.3f }); Shake(3, 0.18f); }); break;
                case "ground_slam": Later(0.18f, () => { AldaraVfx.QuakeFx(tx, ty, rad, Hx("#ffb04a"), Hx("#fff0c0"), 6, false); Rays(tx, ty, 6, rad * 0.8f, "#ffd35a", 10, 0.35f); }); break;
                case "cleave":
                    {
                        float d = Mathf.Sqrt((tx - P.x) * (tx - P.x) + (ty - P.y) * (ty - P.y));
                        M(new AldaraVfx.Mf { k = "arc", x = P.x, y = P.y, h = PH_Y * 0.8f, a = a, r = d + rad * 0.6f, arc = 1.7f, dir = 1, c = Color.white, c2 = Color.white, w = 24, T = 0.34f, dl = 0.06f });
                        M(new AldaraVfx.Mf { k = "arc", x = P.x, y = P.y, h = PH_Y * 0.8f, a = a, r = d + rad * 0.8f, arc = 1.6f, dir = 1, c = TrailC, c2 = Color.white, w = 9, T = 0.36f, dl = 0.1f });
                        Later(0.12f, () => { foreach (var m in AldaraSkills.I.NearbyPublic(tx, ty, rad)) AldaraVfx.Sparks(m.x, m.y, AldaraMonsters.Chest(m), a, 6, Color.white, 240, 1.2f); Shake(3, 0.18f); }); break;
                    }
                case "leap_strike":
                    if (ph == "cast") { Dust(P.x, P.y, 30, 0.6f); Shock(P.x, P.y, 40, "#d8c8a8", "#ffffff", 0.35f); }
                    else { AldaraVfx.QuakeFx(P.x, P.y, rad, Hx("#ffb04a"), Hx("#fff0c0"), 7, false); Rays(P.x, P.y, 6, rad, "#ffd35a", 14, 0.4f); }
                    break;
                case "judgement":
                    AldaraVfx.Rune(tx, ty, 70, Hx("#ffd35a"), 1.1f, false, 2);
                    Later(0.12f, () =>
                    {
                        M(new AldaraVfx.Mf { k = "pillar", x = tx, y = ty, r = 44, c = Hx("#ffd35a"), c2 = Hx("#fffbe8"), H = 460, T = 0.85f, holy = true }); Rays(tx, ty, th, 110, "#fff4b0", 16, 0.5f); Shock(tx, ty, 90, "#ffd35a", "#ffffff", 0.5f);
                        AldaraVfx.Motes(tx, ty, 50, 18, Hx("#fff0a0"), 140); Shake(6, 0.3f);
                    }); break;
                case "whirlwind":
                    {
                        var c = TrailC; for (int q = 0; q < 3; q++) M(new AldaraVfx.Mf { k = "spin", x = P.x, y = P.y, h = PH_Y * (0.7f + q * 0.2f), r = rad * (0.55f + q * 0.15f), c = c, c2 = Color.white, w = 15 - q * 3, T = 0.34f, a0 = a + q * 2.1f, dl = q * 0.1f, follow = true });
                        M(new AldaraVfx.Mf { k = "shock", x = P.x, y = P.y, r = rad, c = c, c2 = Color.white, T = 0.45f }); for (int k = 0; k < 6; k++) { float aa = k / 6f * PI * 2; Dust(P.x + Mathf.Cos(aa) * rad * 0.6f, P.y + Mathf.Sin(aa) * rad * 0.6f, 26, 0.7f); }
                        break;
                    }
                case "battle_roar":
                    M(new AldaraVfx.Mf { k = "roar", x = P.x, y = P.y, h = PH_Y * 1.4f, r = rad, c = Hx("#ff9a3a"), T = 0.8f }); Shock(P.x, P.y, rad, "#ff9a3a", "#ffe0a0", 0.5f); Aura(30, 90, "#ff7a2a", 0.9f);
                    for (int k = 0; k < 8; k++) { float aa = k / 8f * PI * 2; Dust(P.x + Mathf.Cos(aa) * rad * 0.5f, P.y + Mathf.Sin(aa) * rad * 0.5f, 30, 0.8f); }
                    Shake(5, 0.35f); break;
                case "war_cry": M(new AldaraVfx.Mf { k = "roar", x = P.x, y = P.y, h = PH_Y * 1.4f, r = 110, c = Hx("#ff4a2a"), T = 0.7f }); Aura(28, 110, "#ff4a2a", 1.1f); AldaraVfx.Rune(P.x, P.y, 56, Hx("#ff5a3a"), 1.2f, true); AldaraVfx.Motes(P.x, P.y, 30, 20, Hx("#ff8a5a"), 120); break;
                case "second_wind": case "rejuvenate":
                    { string c = id == "rejuvenate" ? "#7fffb0" : "#8aff8a"; AldaraVfx.Rune(P.x, P.y, 60, Hx(c), 1.4f, true); Aura(26, 100, c, 1.2f); Glyph(c, "cross", 1.1f); AldaraVfx.Motes(P.x, P.y, 34, 26, Hx(c), 110); break; }
                case "iron_skin": AldaraVfx.Rune(P.x, P.y, 50, Hx("#ffd35a"), 1.0f, true); Flash(P.x, P.y, PH_Y, 40, "#ffd35a", "#ffffff", 0.3f, true); AldaraVfx.Sparks(P.x, P.y, PH_Y, 0, 18, Hx("#fff0b0"), 200); break;
                case "arcane_barrier": AldaraVfx.Rune(P.x, P.y, 54, Hx("#8ab4ff"), 1.0f, true); Flash(P.x, P.y, PH_Y, 44, "#8ab4ff", "#ffffff", 0.3f, true); AldaraVfx.Motes(P.x, P.y, 30, 16, Hx("#cfe0ff"), 80); break;
                // mage
                case "fireball": M(new AldaraVfx.Mf { k = "scorch", x = tx, y = ty, r = rad * 0.7f, T = 3.5f, c = Hx("#ff6a1a") }); Shock(tx, ty, rad, "#ff8a3a", "#ffe08a", 0.4f); AldaraVfx.Debris(tx, ty, 8, Hx("#2a1a10"), "fire"); break;
                case "ice_shard":
                    M(new AldaraVfx.Mf { k = "spikes", x = tx, y = ty, r = Mathf.Max(30, (t != null ? t.r : 16) * 1.6f), n = 8, c = Hx("#dff6ff"), c2 = Hx("#6ac8ff"), T = 0.9f });
                    for (int k = 0; k < 12; k++) { float aa = R() * PI * 2, v = 80 + R() * 180; AldaraVfx.Mpush(new AldaraVfx.Mp { x = tx, y = ty, z = th, vx = Mathf.Cos(aa) * v, vy = Mathf.Sin(aa) * v, vz = 80 + R() * 120, g = 700, life = 0.6f, max = 0.6f, c = Hx("#e8f8ff"), s = 2.2f, rock = true, hasRot = true, vr = 12 }); }
                    break;
                case "chain_node": Flash(tx, ty, th, 26, "#8ab4ff", "#ffffff", 0.25f); M(new AldaraVfx.Mf { k = "ringA", x = tx, y = ty, h = th, r = 30, c = Hx("#cfe0ff"), T = 0.3f }); AldaraVfx.Sparks(tx, ty, th, 0, 6, Hx("#cfe0ff"), 200); break;
                case "meteor": AldaraVfx.QuakeFx(tx, ty, rad, Hx("#ff7a2a"), Hx("#ffe08a"), 0, true); M(new AldaraVfx.Mf { k = "pillar", x = tx, y = ty, r = rad * 0.5f, c = Hx("#ff6a1a"), c2 = Hx("#ffe08a"), H = 160, T = 0.6f }); break;
                case "meteor_mark": AldaraVfx.Rune(tx, ty, rad * 0.9f, Hx("#ff7a2a"), 0.7f, false, 3); break;
                case "frost_nova":
                    M(new AldaraVfx.Mf { k = "spikes", x = P.x, y = P.y, r = rad * 0.9f, n = 14, c = Hx("#dff6ff"), c2 = Hx("#6ac8ff"), T = 1.1f }); Shock(P.x, P.y, rad, "#8fdfff", "#ffffff", 0.5f);
                    M(new AldaraVfx.Mf { k = "nova", x = P.x, y = P.y, h = PH_Y, r = rad, c = Hx("#6ac8ff"), c2 = Hx("#effbff"), T = 0.5f }); break;
                case "bolt_hit": M(new AldaraVfx.Mf { k = "ringA", x = tx, y = ty, h = th, r = 26, c = Hx(col ?? "#8ab4ff"), T = 0.26f }); AldaraVfx.Motes(tx, ty, 12, 6, Hx(col ?? "#8ab4ff"), 70); break;
                case "blink":
                    if (ph == "cast") { M(new AldaraVfx.Mf { k = "blink", x = P.x, y = P.y, h = PH_Y, c = Hx("#b07aff"), c2 = Color.white, T = 0.45f }); AldaraVfx.Rune(P.x, P.y, 36, Hx("#b07aff"), 0.6f); }
                    else { M(new AldaraVfx.Mf { k = "blink", x = P.x, y = P.y, h = PH_Y, c = Hx("#b07aff"), c2 = Color.white, T = 0.45f }); Shock(P.x, P.y, 48, "#b07aff", "#ffffff", 0.35f); }
                    break;
                case "evasive_roll":
                    if (ph == "cast") Dust(P.x, P.y, 24, 0.6f);
                    else { for (int k = 0; k < 3; k++) Dust(P.x - Mathf.Cos(a) * k * 14, P.y - Mathf.Sin(a) * k * 14, 18, 0.5f); M(new AldaraVfx.Mf { k = "speed", x = P.x, y = P.y, a = a, r = 16, T = 0.25f, c = Color.white }); }
                    break;
                case "blizzard_wave": for (int k = 0; k < 4; k++) { float aa = R() * PI * 2, d = R() * rad * 0.8f; M(new AldaraVfx.Mf { k = "spikes", x = tx + Mathf.Cos(aa) * d, y = ty + Mathf.Sin(aa) * d, r = 18, n = 3, c = Hx("#dff6ff"), c2 = Hx("#6ac8ff"), T = 0.7f }); } break;
                case "cata_charge": AldaraVfx.Rune(tx, ty, rad, Hx("#c07aff"), 0.9f, false, 4); break;
                case "cataclysm":
                    M(new AldaraVfx.Mf { k = "pillar", x = tx, y = ty, r = rad * 0.6f, c = Hx("#c07aff"), c2 = Hx("#f6e4ff"), H = 300, T = 0.8f }); AldaraVfx.QuakeFx(tx, ty, rad, Hx("#c07aff"), Hx("#f6e4ff"), 0, false);
                    Rays(tx, ty, 20, rad * 1.2f, "#e0b0ff", 18, 0.5f); break;
                // archer
                case "muzzle": { float x = P.x + Mathf.Cos(a) * 20, y = P.y + Mathf.Sin(a) * 20; Flash(x, y, PH_Y, 14, col ?? "#fff0c0", "#ffffff", 0.14f); break; }
                case "frost_arrow": M(new AldaraVfx.Mf { k = "spikes", x = tx, y = ty, r = 28, n = 6, c = Hx("#dff6ff"), c2 = Hx("#6ac8ff"), T = 0.8f }); Flash(tx, ty, th, 24, "#8fdfff", "#ffffff", 0.22f); break;
                case "eagle_eye": Glyph("#ffd35a", "eye", 1.2f); AldaraVfx.Rune(P.x, P.y, 50, Hx("#ffd35a"), 1.1f, true); Aura(24, 90, "#ffd35a", 0.9f); break;
                case "rain_land":
                    for (int k = 0; k < 9; k++) { float aa = R() * PI * 2, d = R() * rad; M(new AldaraVfx.Mf { k = "stuck", x = tx + Mathf.Cos(aa) * d, y = ty + Mathf.Sin(aa) * d, a = R() * 0.6f - 0.3f, T = 1.8f }); }
                    for (int k = 0; k < 5; k++) { float aa = R() * PI * 2, d = R() * rad * 0.7f; Dust(tx + Mathf.Cos(aa) * d, ty + Mathf.Sin(aa) * d, 20, 0.6f); }
                    Shock(tx, ty, rad, "#d8c8a8", "#ffffff", 0.35f); break;
                case "explosive_arrow": AldaraVfx.Effect("explode", tx, ty, rad * 0.8f, 0.55f, Color.white); M(new AldaraVfx.Mf { k = "scorch", x = tx, y = ty, r = rad * 0.7f, T = 3.5f, c = Hx("#ff6a1a") }); AldaraVfx.Debris(tx, ty, 10, Hx("#2a1a10"), "fire"); Shake(3, 0.2f); break;
                case "poison_arrow":
                    for (int k = 0; k < 10; k++) { float aa = R() * PI * 2, v = 20 + R() * 40; AldaraVfx.Mpush(new AldaraVfx.Mp { x = tx, y = ty, z = th * 0.8f, vx = Mathf.Cos(aa) * v, vy = Mathf.Sin(aa) * v * 0.6f, vz = 15, life = 1.1f, max = 1.1f, c = Hx("#4a8a2a"), s = 6, grow = 16, smoke = true }); }
                    Flash(tx, ty, th, 22, "#8ad040", "#eaffb8", 0.25f); AldaraVfx.Motes(tx, ty, 20, 8, Hx("#9aff6a"), 50); break;
                case "trap_set": AldaraVfx.Rune(tx, ty, 34, Hx("#ff8a3a"), 0.85f, false, 1.5f, true); break;
                case "trap_boom": { var f = AldaraVfx.Effect("explode", tx, ty, rad, 0.6f, Color.white); f.big = true; AldaraVfx.QuakeFx(tx, ty, rad * 0.8f, Hx("#ff8a3a"), Hx("#ffe08a"), 5, true); break; }
                case "starfall_arrow":
                    M(new AldaraVfx.Mf { k = "pillar", x = tx, y = ty, r = 30, c = Hx("#ffe07a"), c2 = Hx("#fffbe8"), H = 420, T = 0.7f, holy = true }); Rays(tx, ty, th, rad, "#fff4b0", 14, 0.45f);
                    for (int k = 0; k < 14; k++) { float aa = R() * PI * 2, v = 100 + R() * 200; AldaraVfx.Mpush(new AldaraVfx.Mp { x = tx, y = ty, z = th, vx = Mathf.Cos(aa) * v, vy = Mathf.Sin(aa) * v, vz = 60 + R() * 120, g = 200, life = 0.8f, max = 0.8f, c = Hx(R() < 0.5f ? "#fff4b0" : "#ffd35a"), s = 2.5f, glow = true }); }
                    break;
            }
        }
    }
}
