using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // What a subclass does in a fight, from the browser: the signature skill (a class skill with its own numbers),
    // the path abilities (subRunFx: strikes, novas, areas, buffs, hastes, heals, shields, the dead rising, dashes,
    // blinks, the Hammer of Justice, pet frenzies), the traits (Holy Light, Blood Fury, Thorns, Riposte, Ignite,
    // Deep Chill, Static, Soul Harvest, Long Shot, Ambush, Pack Bond, Arcane Shot), the timed buffs (SUBB), the capstone
    // passives (Undying Rage, Wildfire), the subclass bar with its four keys, and auto combat's use of the bar.
    public class AldaraSubclass : MonoBehaviour
    {
        public static AldaraSubclass I;
        static AldaraHero H { get { return AldaraHero.I; } }
        static AldaraPlayer P { get { return AldaraPlayer.I; } }
        static Color Hx(string h) { return AldaraRules.Hex(h); }
        void Awake() { I = this; AldaraVfx.DrawAir += DrawAir; }
        void OnDestroy() { AldaraVfx.DrawAir -= DrawAir; }

        public static AldaraTree.Sub Cur { get { var S = AldaraTree.SubCur(); return S != null && H && H.lvl >= AldaraTree.B.SUB_LEVEL ? S : null; } }
        static string CurId { get { var S = Cur; return S != null ? S.id : null; } }
        /// SM: what the unlocked trait upgrades add up to
        public static float SM(string k)
        {
            var S = AldaraTree.SubCur(); if (S == null) return 0; float v = 0;
            foreach (var n in S.nodes) if (!n.root && AldaraTree.SubHas(n.id) && n.mod != null) { float x; if (n.mod.TryGetValue(k, out x)) v += x; }
            return v;
        }
        public static bool Pass(string p) { var S = Cur; return S != null && S.nodes.Any(n => n.pass == p && AldaraTree.SubHas(n.id)); }

        // ---- the skills (subSkillDef, subTreeSkills) ----
        static readonly Dictionary<string, SkillDef> made = new Dictionary<string, SkillDef>();
        public static SkillDef Signature()
        {
            var S = AldaraTree.SubCur(); if (S == null || AldaraSkills.I == null) return null; string id = (string)S.skill["id"];
            SkillDef d; if (made.TryGetValue(id, out d)) return d;
            var B = AldaraSkills.I.BaseGet((string)S.skill["base"]); if (B == null) return null;
            d = JsonUtility.FromJson<SkillDef>(JsonUtility.ToJson(B));
            var over = S.skill["over"] as JObject; if (over != null) foreach (var kv in over) { var f = typeof(SkillDef).GetField(kv.Key); if (f == null) continue; if (f.FieldType == typeof(float)) f.SetValue(d, (float)kv.Value); else if (f.FieldType == typeof(int)) f.SetValue(d, (bool)(kv.Value.Type == JTokenType.Boolean) ? ((bool)kv.Value ? 1 : 0) : (int)kv.Value); }
            float cd0 = over != null && over["cd"] != null ? (float)over["cd"] : B.cd;
            d.id = id; d.name = (string)S.skill["name"]; d.desc = (string)S.skill["desc"]; d.price = 0; d.cost = 0; d.cd = Mathf.Round(cd0 * AldaraTree.B.SUB_CD_X); d.sub = S; d.baseId = B.id;
            made[id] = d; return d;
        }
        public static List<SkillDef> PathSkills()
        {
            var o = new List<SkillDef>(); var S = AldaraTree.SubCur(); if (S == null) return o;
            foreach (var n in S.nodes)
            {
                if (n.sk == null || !AldaraTree.SubHas(n.id)) continue; SkillDef d;
                if (!made.TryGetValue(n.skillId, out d))
                {
                    d = new SkillDef { id = n.skillId, name = n.n, desc = n.d, kind = (string)n.sk["kind"], cost = 0, cd = (float)n.sk["cd"], radius = n.sk["radius"] != null ? (float)n.sk["radius"] : 0, custom = n.sk["fx"] as JArray, subNode = n };
                    made[n.skillId] = d;
                }
                o.Add(d);
            }
            return o;
        }
        public static SkillDef Find(string id) { var s = Signature(); if (s != null && s.id == id) return s; foreach (var p in PathSkills()) if (p.id == id) return p; return null; }
        public static void Reset() { made.Clear(); }

        // ---- casting (wrapCast and the path abilities' runner) ----
        public static bool CastSub(SkillDef sk, AldaraMonsters.Mon t)
        {
            var S0 = AldaraSkills.I;
            if (sk.custom != null)
            {
                S0.SpendPublic(sk); var S = AldaraTree.SubCur(); AldaraFx.Text(P.x, P.y - 42, sk.name, sk.subNode != null && S != null ? Hx(S.col) : Hx("#9fb4ff"));
                RunFx(sk.custom, t, sk); return true;
            }
            if (sk.sub == null) return false;
            var B = S0.BaseGet(sk.baseId); float prev; bool had = S0.cds.TryGetValue(B.id, out prev);
            var run = JsonUtility.FromJson<SkillDef>(JsonUtility.ToJson(sk)); run.id = B.id;
            try { if (run.kind == "target") S0.CastTargetRaw(t ?? AldaraSkills.Dummy(P.x + Mathf.Cos(P.facing) * 60, P.y + Mathf.Sin(P.facing) * 60), run); else S0.CastSelfRaw(run); }
            finally { S0.cds[sk.id] = sk.cd * (1 - AldaraTree.T("cdr")); if (had) S0.cds[B.id] = prev; else S0.cds.Remove(B.id); }
            var J = sk.sub.skill;
            if (J["heal"] != null) { float a = Mathf.Round(H.maxHp * (float)J["heal"]); H.hp = Mathf.Min(H.maxHp, H.hp + a); AldaraFx.Text(P.x, P.y - 58, "+" + a + " HP", Hx("#ffe07a")); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "pillar", x = P.x, y = P.y, r = 26, c = Hx("#ffe07a"), c2 = Color.white, H = 300, T = 0.7f, holy = true }); }
            if (J["selfDmg"] != null) { H.hp = Mathf.Max(1, H.hp - Mathf.Round(H.maxHp * (float)J["selfDmg"])); AldaraVfx.Burst(P.x, P.y - 20, Hx("#ff3a2a"), 20, 160); }
            if (J["ambush"] != null) { ambushT = (float)J["ambush"]; AldaraVfx.Burst(P.x, P.y - 10, Hx("#b07aff"), 24, 180); }
            if (J["raise"] != null) Raise(P.x, P.y, (int)J["raise"], 18, true);
            if (J["frenzy"] != null) { frenzyT = (float)J["frenzy"]; var pet = AldaraPet.I; if (pet && pet.placed) AldaraVfx.Burst(pet.x, pet.y - 10, Hx("#9fe07a"), 20, 160); }
            return true;
        }
        /// necroRaise: skeleton warriors tinted with grave-light
        public static void Raise(float x, float y, int n, float life, bool ring)
        {
            for (int k = 0; k < n; k++)
            {
                float a = k / (float)Mathf.Max(1, n) * Mathf.PI * 2 + Random.value * 0.6f, d = ring ? 50 + Random.value * 30 : Random.value * 16, sx = x + Mathf.Cos(a) * d, sy = y + Mathf.Sin(a) * d;
                AldaraRelics.AddSkel(sx, sy, life, true);
                AldaraVfx.Burst(sx, sy, Hx("#9aff9a"), 12, 120); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "dust", x = sx, y = sy, r = 24, T = 0.6f }); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "pillar", x = sx, y = sy, r = 14, c = Hx("#6aff8a"), c2 = Hx("#e8ffe8"), H = 120, T = 0.6f });
            }
            if (ring) AldaraHud.Banner(n + " skeleton warriors rise!");
        }
        static List<AldaraMonsters.Mon> Nearby(float x, float y, float rad, AldaraMonsters.Mon except = null) { return AldaraSkills.I.NearbyPublic(x, y, rad).Where(m => m != except).ToList(); }
        static AldaraMonsters.Mon NearestTo(float x, float y, float rad) { AldaraMonsters.Mon b = null; float bd = 1e9f; foreach (var m in Nearby(x, y, rad)) { float d = (m.x - x) * (m.x - x) + (m.y - y) * (m.y - y); if (d < bd) { bd = d; b = m; } } return b; }
        static float F(JObject o, string k, float d = 0) { return o[k] != null ? (float)o[k] : d; }
        public class SubBuffT { public float v, t, max; public string name, col; }
        public static readonly Dictionary<string, SubBuffT> SUBB = new Dictionary<string, SubBuffT>();
        public static float undyingCd, undyingT;
        public static float Buff(string k) { SubBuffT b; return SUBB.TryGetValue(k, out b) && b.t > 0 ? b.v : 0; }
        static void SubHit(AldaraMonsters.Mon m, float mult, JObject o)
        {
            if (m == null || m.dead) return; float exec = F(o, "exec"), om = F(o, "mult");
            float d = Mathf.Max(1, Mathf.Round(H.RollDmg(mult) * (exec > 0 && m.hp < m.maxHp * exec ? (om >= 5 ? 2 : 3) : 1)));
            if (o["stun"] != null) m.stunT = Mathf.Max(m.stunT, F(o, "stun")); if (o["slow"] != null) m.slowT = Mathf.Max(m.slowT, F(o, "slow")); if (o["mark"] != null) m.markT = F(o, "mark");
            AldaraMonsters.I.HitMonster(m, d, Hx((string)o["col"] ?? "#ffffff")); if (o["burn"] != null && !m.dead) Ignite(m, Mathf.Max(1, Mathf.Round(d * 0.25f)));
        }
        static void Later(float t, System.Action fn) { AldaraSkills.I.Later_(t, fn); }
        static void RunFx(JArray fx, AldaraMonsters.Mon t, SkillDef sk)
        {
            var S = AldaraTree.SubCur(); string col0 = S != null ? S.col : "#ffffff"; bool KF = AldaraKnightFx.On; const float UPX = AldaraWorld.PX;
            foreach (JObject o in fx)
            {
                string cs = (string)o["col"] ?? col0; var col = Hx(cs); var oo = (JObject)o.DeepClone(); oo["col"] = cs;
                switch ((string)o["op"])
                {
                    case "strike":
                        {
                            var m = t != null && !t.dummy ? t : (H.target != null && !H.target.dead ? H.target : NearestTo(P.x, P.y, 220)); if (m == null) break; H.StartSwing(0.35f, true);
                            if (KF) AldaraKnightFx.SubStrike(m, col);
                            else
                            {
                                AldaraVfx.Mfx(new AldaraVfx.Mf { k = "impact", x = m.x, y = m.y, h = AldaraMonsters.Chest(m), a = Mathf.Atan2(m.y - P.y, m.x - P.x), c = col, c2 = Color.white, T = 0.3f, big = true });
                                AldaraVfx.Mfx(new AldaraVfx.Mf { k = "flash", x = m.x, y = m.y, h = AldaraMonsters.Chest(m), r = 40, c = col, c2 = Color.white, T = 0.25f });
                            }
                            AldaraVfx.Burst(m.x, m.y, col, 18, 220); SubHit(m, F(o, "mult"), oo); if (o["exec"] != null && m.dead) AldaraFx.Text(m.x, m.y - m.r - 30, "Executed", Hx("#ff6a4a")); break;
                        }
                    case "nova":
                        {
                            float r = F(o, "r");
                            if (KF) AldaraKnightFx.SubNova(AldaraKnightFx.G(P.x, P.y), r / UPX, col);
                            else { AldaraVfx.Mfx(new AldaraVfx.Mf { k = "shock", x = P.x, y = P.y, r = r, c = col, c2 = Color.white, T = 0.5f }); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "ringA", x = P.x, y = P.y, h = 8, r = r * 0.8f, c = col, c2 = Color.white, T = 0.45f }); AldaraVfx.Ring(P.x, P.y, r, col, 0.5f); }
                            AldaraVfx.Shake(3, 0.25f);
                            foreach (var m in Nearby(P.x, P.y, r)) SubHit(m, F(o, "mult"), oo); break;
                        }
                    case "area":
                        {
                            bool self = (string)o["at"] == "self"; Vector2 at = self ? new Vector2(P.x, P.y) : (t != null ? new Vector2(t.x, t.y) : new Vector2(P.x + Mathf.Cos(P.facing) * 160, P.y + Mathf.Sin(P.facing) * 160));
                            int waves = o["waves"] != null ? (int)o["waves"] : 1; float delay = F(o, "delay"), gap = F(o, "gap", 0.4f), r = F(o, "r");
                            for (int w = 0; w < waves; w++) Later(delay + w * gap, () =>
                            {
                                var c = self ? new Vector2(P.x, P.y) : at;
                                if (KF) { AldaraKnightFx.SubSlam(AldaraKnightFx.G(c.x, c.y), r / UPX, col, delay > 0); if (delay > 0) AldaraVfx.Shake(4, 0.35f); }
                                else { AldaraVfx.Mfx(new AldaraVfx.Mf { k = "shock", x = c.x, y = c.y, r = r, c = col, c2 = Color.white, T = 0.4f }); if (delay > 0) AldaraVfx.QuakeFx(c.x, c.y, r * 0.8f, col, Color.white, 4, o["burn"] != null); AldaraVfx.Ring(c.x, c.y, r, col, 0.4f); }
                                foreach (var m in Nearby(c.x, c.y, r)) SubHit(m, F(o, "mult"), oo);
                            });
                            if (delay > 0) { if (KF) AldaraKnightFx.SubMark(AldaraKnightFx.G(at.x, at.y), r * 0.5f / UPX, col, delay); else AldaraVfx.Rune(at.x, at.y, r * 0.5f, col, delay, false, 1.5f, true); }
                            break;
                        }
                    case "buff": SUBB[(string)o["key"]] = new SubBuffT { v = F(o, "v"), t = F(o, "dur"), max = F(o, "dur"), name = sk.name, col = cs }; AldaraVfx.Burst(P.x, P.y - 20, col, 20, 160); if (KF) AldaraKnightFx.SubBuff(col); else AldaraVfx.Mfx(new AldaraVfx.Mf { k = "ringA", x = P.x, y = P.y, h = 10, r = 44, c = col, c2 = Color.white, T = 0.5f }); break;
                    case "haste": H.hasteBuff = Mathf.Max(H.hasteBuff, F(o, "dur")); break;
                    case "heal": { float a = Mathf.Round(H.maxHp * F(o, "v")); H.hp = Mathf.Min(H.maxHp, H.hp + a); AldaraFx.Text(P.x, P.y - 58, "+" + a + " HP", Hx("#7fe07f")); break; }
                    case "shield": H.shield = Mathf.Max(H.shield, Mathf.Round(H.maxHp * F(o, "v"))); H.shieldT = Mathf.Max(H.shieldT, F(o, "dur")); H.shieldName = sk.name; break;
                    case "raise": { bool tg = (string)o["at"] == "target" && t != null; int n = (int)o["n"]; Raise(tg ? t.x : P.x, tg ? t.y : P.y, n, F(o, "life", 20), n > 1); break; }
                    case "ambush": ambushT = F(o, "dur"); break;
                    case "dashto":
                        {
                            var m = t != null && !t.dummy ? t : H.target; if (m == null || m.dead) break; float a = Mathf.Atan2(P.y - m.y, P.x - m.x), d = m.r + 26;
                            AldaraVfx.Mfx(new AldaraVfx.Mf { k = "blink", x = P.x, y = P.y, h = AldaraVfx.PH_Y, c = col, c2 = Color.white, T = 0.35f });
                            float nx = m.x + Mathf.Cos(a) * d, ny = m.y + Mathf.Sin(a) * d; if (AldaraWorld.BlockedAt(nx, ny)) { var f = AldaraPlayer.FreeSpotNear(nx, ny); nx = f.x; ny = f.y; }
                            if (KF) AldaraKnightFx.Dash(AldaraKnightFx.G(P.x, P.y), AldaraKnightFx.G(nx, ny), col);
                            P.x = nx; P.y = ny; P.facing = Mathf.Atan2(m.y - ny, m.x - nx); break;
                        }
                    case "blinkback":
                        {
                            var m = t != null && !t.dummy ? t : H.target; float a = m != null ? Mathf.Atan2(P.y - m.y, P.x - m.x) : P.facing + Mathf.PI, dist = F(o, "dist");
                            float nx = P.x + Mathf.Cos(a) * dist, ny = P.y + Mathf.Sin(a) * dist; if (AldaraWorld.BlockedAt(nx, ny)) { var f = AldaraPlayer.FreeSpotNear(nx, ny); nx = f.x; ny = f.y; }
                            AldaraVfx.Mfx(new AldaraVfx.Mf { k = "blink", x = P.x, y = P.y, h = AldaraVfx.PH_Y, c = col, c2 = Color.white, T = 0.35f });
                            P.x = nx; P.y = ny; if (m != null) P.facing = Mathf.Atan2(m.y - ny, m.x - nx); break;
                        }
                    case "hammer":
                        {
                            var m = t != null && !t.dummy ? t : (H.target != null && !H.target.dead ? H.target : NearestTo(P.x, P.y, 260)); if (m == null) break; H.StartSwing(0.35f, true);
                            var hm = new Hammer { m = m, x = m.x, y = m.y, side = m.x >= P.x ? 1 : -1, c = col, T = 1.3f }; hammers.Add(hm);
                            if (KF) AldaraKnightFx.SubMark(AldaraKnightFx.G(m.x, m.y), (m.r + 34) / UPX, col, 0.65f); else AldaraVfx.Rune(m.x, m.y, m.r + 34, col, 0.65f, false, 2, true); AldaraVfx.Burst(m.x, m.y - 160, col, 16, 120);
                            float mult = F(o, "mult"), splash = F(o, "splash");
                            Later(0.64f, () =>
                            {
                                float x = hm.x, y = hm.y;
                                if (KF) AldaraKnightFx.SubSlam(AldaraKnightFx.G(x, y), 130 / UPX, col, true);
                                else
                                {
                                    AldaraVfx.QuakeFx(x, y, 120, col, Color.white, 8, true);
                                    AldaraVfx.Mfx(new AldaraVfx.Mf { k = "pillar", x = x, y = y, r = 46, c = col, c2 = Color.white, H = 260, T = 0.6f }); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "flash", x = x, y = y, h = 20, r = 90, c = col, c2 = Color.white, T = 0.35f }); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "shock", x = x, y = y, r = 150, c = col, c2 = Color.white, T = 0.5f });
                                }
                                AldaraVfx.Shake(9, 0.4f); AldaraVfx.Burst(x, y, col, 40, 300); AldaraVfx.Burst(x, y, Color.white, 18, 220);
                                if (!m.dead && Mathf.Sqrt((m.x - x) * (m.x - x) + (m.y - y) * (m.y - y)) < 90 + m.r) SubHit(m, mult, oo);
                                if (splash > 0) foreach (var q in Nearby(x, y, 130, m)) SubHit(q, splash, new JObject { ["col"] = cs });
                            });
                            break;
                        }
                    case "petfrenzy": frenzyT = Mathf.Max(frenzyT, F(o, "dur")); break;
                }
            }
        }

        // ---- traits and buffs on your hits (the hitMonster wrappers, outermost first) ----
        public static float ambushT, frenzyT; static int hits, arcN; public static int busy;
        public class Burn { public AldaraMonsters.Mon m; public float t, dps, tick; }
        public static readonly List<Burn> burns = new List<Burn>();
        public static void Ignite(AldaraMonsters.Mon m, float dps)
        {
            float dur = 3 + (CurId == "pyromancer" ? SM("burndur") : 0); var ex = burns.FirstOrDefault(b => b.m == m);
            if (ex != null) { ex.t = dur; ex.dps = Mathf.Max(ex.dps, dps); } else burns.Add(new Burn { m = m, t = dur, dps = dps, tick = 1 });
        }
        /// before the blow lands: marks, the damage and crit buffs, then the traits that change the blow
        public static float PreHit(AldaraMonsters.Mon m, float dmg, string src)
        {
            if (m.markT > 0) dmg *= 1.6f;
            if (src == "player") { float b = Buff("dmg"); if (b > 0) dmg *= 1 + b; float c = Buff("crit"); if (c > 0 && Random.value < c) { dmg *= 1.6f; AldaraFx.Text(m.x + 8, m.y - m.r - 22, "CRIT", Hx("#ffe08a")); } }
            if (src == "pet" && Buff("petx2") > 0) dmg *= 2;
            var S = Cur; if (S == null || busy > 0) return dmg;
            if (src == "player")
            {
                if (S.id == "berserker") dmg *= 1 + (0.6f + SM("fury")) * (1 - Mathf.Max(0, H.hp) / H.maxHp);
                if (S.id == "cryomancer" && m.slowT > 0) dmg *= 1.15f + SM("chill");
                if (S.id == "sharpshooter") { if (H.critFrame == Time.frameCount) dmg *= 1.35f + SM("critx"); float d = Mathf.Sqrt((m.x - P.x) * (m.x - P.x) + (m.y - P.y) * (m.y - P.y)); if (d > 150) dmg *= 1 + Mathf.Min(0.2f + SM("range"), (d - 150) / 800); }
                if (S.id == "shadowstalker" && ((!m.amb && m.hp >= m.maxHp * 0.999f) || ambushT > 0)) { dmg *= 2.5f + SM("amb"); ambushT = 0; m.amb = true; AldaraFx.Text(m.x, m.y - m.r - 24, "Ambush", Hx("#d0a0ff")); }
                if (S.id == "arcanearcher")
                {
                    arcN++; if (arcN % Mathf.Max(2, 4 - (int)SM("every")) == 0)
                    {
                        var o = Nearby(m.x, m.y, 320, m).FirstOrDefault() ?? m; float amt = Mathf.Max(1, Mathf.Round(dmg * (0.8f + SM("arcx"))));
                        H.Fire("bolt", o, q => { busy++; try { AldaraMonsters.I.HitMonster(q, amt, Hx("#6ae0ff")); AldaraVfx.Burst(q.x, q.y, Hx("#6ae0ff"), 10, 140); } finally { busy--; } }, null, "#6ae0ff");
                    }
                }
            }
            if (src == "pet" && S.id == "beastmaster") { dmg *= 1.8f + SM("pet"); float h = Mathf.Max(1, Mathf.Round(H.maxHp * (0.01f + SM("petheal")))); H.hp = Mathf.Min(H.maxHp, H.hp + h); }
            return Mathf.Max(1, Mathf.Round(dmg));
        }
        /// after it lands: Holy Light, Ignite, Deep Chill, Static, and the lifesteal buff
        public static void PostHit(AldaraMonsters.Mon m, float dealt, string src)
        {
            if (src == "player") { float ls = Buff("ls"); if (ls > 0) { float h = Mathf.Round(dealt * ls); if (h > 0) H.hp = Mathf.Min(H.maxHp, H.hp + h); } }
            var S = Cur; if (S == null || busy > 0 || src != "player" || !(dealt > 0) || m.dead) return;
            if (S.id == "paladin" && ++hits % Mathf.Max(2, 5 - (int)SM("every")) == 0) { float a = Mathf.Round(H.maxHp * (0.06f + SM("heal"))); H.hp = Mathf.Min(H.maxHp, H.hp + a); AldaraFx.Text(P.x, P.y - 56, "+" + a + " HP", Hx("#ffe07a")); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "ringA", x = P.x, y = P.y, h = 10, r = 40, c = Hx("#ffe07a"), c2 = Color.white, T = 0.45f }); }
            if (S.id == "pyromancer") Ignite(m, Mathf.Max(1, Mathf.Round(dealt * (0.25f + SM("burn")))));
            if (S.id == "cryomancer") m.slowT = Mathf.Max(m.slowT, 2 + SM("slowdur"));
            if (S.id == "stormcaller" && Random.value < 0.25f + SM("static"))
            {
                var o = Nearby(m.x, m.y, 240, m).FirstOrDefault();
                if (o != null) { busy++; try { AldaraVfx.Zap(m.x, m.y - m.r * 0.6f, o.x, o.y - o.r * 0.6f, Hx("#cfe0ff"), 0.25f, false); AldaraMonsters.I.HitMonster(o, Mathf.Max(1, Mathf.Round(dealt * (0.8f + SM("staticx")))), Hx("#cfe0ff")); } finally { busy--; } }
            }
        }
        public static void OnKill(AldaraMonsters.Mon t)
        {
            t.amb = false; var burning = burns.FirstOrDefault(b => b.m == t);
            var S = Cur; if (S == null) return;
            if (S.id == "berserker") H.hp = Mathf.Min(H.maxHp, H.hp + Mathf.Round(H.maxHp * (0.03f + SM("killheal"))));
            if (S.id == "necromancer" && Random.value < 0.30f + SM("raise") && AldaraRelics.skel.Count(q => q.necro) < 4 + SM("maxskel")) Raise(t.x, t.y, 1, 20, false);
            if (S.id == "shadowstalker") H.mana = Mathf.Min(H.maxMana, H.mana + Mathf.Round(H.maxMana * (0.08f + SM("mana"))));
            if (burning != null && Pass("wildfire")) foreach (var o in Nearby(t.x, t.y, 220, t).Take(2)) { Ignite(o, burning.dps); AldaraVfx.Zap(t.x, t.y - t.r * 0.5f, o.x, o.y - o.r * 0.5f, Hx("#ff8a3a"), 0.3f, false); }
        }
        // ---- on the blows you take (the damagePlayer wrappers, outermost first) ----
        /// damage reduction buffs and Undying Rage; returns the damage, or -1 when Riposte turned the blow aside
        public static float PreDamage(float dmg, AldaraMonsters.Mon src, bool env)
        {
            float dr = Mathf.Min(0.9f, Buff("dr") + (undyingT > 0 ? 1 : 0)); if (dr > 0) dmg = Mathf.Round(dmg * (1 - dr));
            if (dmg >= H.hp && H.alive && Pass("undying") && !(undyingCd > 0))
            {
                dmg = Mathf.Max(0, H.hp - 1); undyingCd = 60; undyingT = 3; SUBB["dmg"] = new SubBuffT { v = 0.5f, t = 6, max = 6, name = "Undying Rage", col = "#ff3a2a" }; AldaraHud.Banner("Undying Rage!");
                AldaraVfx.Burst(P.x, P.y - 20, Hx("#ff3a2a"), 40, 260); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "pillar", x = P.x, y = P.y, r = 30, c = Hx("#ff3a2a"), c2 = Color.white, H = 380, T = 0.8f });
            }
            var S = Cur;
            if (S != null && S.id == "blademaster" && !env && dmg > 0 && Random.value < 0.2f + SM("parry"))
            {
                AldaraFx.Text(P.x, P.y - 36, "Parry", Hx("#dfe8ff")); AldaraVfx.Burst(P.x, P.y - 20, Hx("#dfe8ff"), 10, 160);
                var s = src ?? Nearby(P.x, P.y, 140).FirstOrDefault();
                if (s != null && !s.dead) { busy++; try { AldaraMonsters.I.HitMonster(s, Mathf.Max(1, Mathf.Round(H.RollDmg(1.5f + SM("riposte")))), Hx("#dfe8ff")); var f = AldaraVfx.Effect("slash", s.x, s.y, 30, 0.22f, Hx("#dfe8ff")); f.a = Mathf.Atan2(s.y - P.y, s.x - P.x); } finally { busy--; } }
                return -1;
            }
            return dmg;
        }
        /// Thorns: the Guardian's attacker takes back part of what you lost
        public static void PostDamage(float took, AldaraMonsters.Mon src, bool env)
        {
            var S = Cur; if (S == null || S.id != "guardian" || env) return; var s = src ?? Nearby(P.x, P.y, 140).FirstOrDefault();
            if (s != null && !s.dead && took > 0) { busy++; try { AldaraMonsters.I.HitMonster(s, Mathf.Max(1, Mathf.Round(took * (0.3f + SM("thorns") + Buff("thorns")))), Hx("#8ab4ff")); } finally { busy--; } }
        }
        public static float SpeedMult() { return 1 + Buff("spd"); }

        // ---- subUpdate / subTreeUpdate ----
        float whirlT, stormT; int wc; float autoT;
        void Update()
        {
            if (!AldaraSave.Ready || !H || !P) return; float dt = Mathf.Min(Time.deltaTime, 0.1f);
            foreach (var b in SUBB.Values) if (b.t > 0) b.t -= dt; if (undyingCd > 0) undyingCd -= dt; if (undyingT > 0) undyingT -= dt;
            foreach (var m in AldaraMonsters.I.all) if (m.markT > 0) { m.markT -= dt; if (Random.value < dt * 6) AldaraVfx.Burst(m.x, m.y - m.r * 1.4f, Hx("#b07aff"), 1, 30); }
            float rg = Buff("regen"); if (rg > 0 && H.alive) H.hp = Mathf.Min(H.maxHp, H.hp + H.maxHp * rg * dt);
            float wh = Buff("whirl");
            if (wh > 0 && H.alive)
            {
                whirlT -= dt; if (whirlT <= 0)
                {
                    whirlT = 0.4f; AldaraVfx.Ring(P.x, P.y, 140, Hx("#dfe8ff"), 0.3f); wc = (wc + 1) % 3; AldaraSkillFx.KnightSlash(P.x + Mathf.Cos(P.facing) * 40, P.y + Mathf.Sin(P.facing) * 40, wc, 0.3f);
                    foreach (var m in Nearby(P.x, P.y, 140)) SubHit(m, wh, new JObject { ["col"] = "#dfe8ff" });
                }
            }
            float st = Buff("storm");
            if (st > 0 && H.alive)
            {
                stormT -= dt; if (stormT <= 0)
                {
                    stormT = 0.5f; var L = Nearby(P.x, P.y, 320); var m = L.Count > 0 ? L[Random.Range(0, L.Count)] : null;
                    if (m != null) { AldaraVfx.Mfx(new AldaraVfx.Mf { k = "pillar", x = m.x, y = m.y, r = 12, c = Hx("#cfe0ff"), c2 = Color.white, H = 360, T = 0.25f }); AldaraVfx.Zap(m.x + (Random.value - 0.5f) * 30, m.y - 320, m.x, m.y - m.r * 0.6f, Hx("#cfe0ff"), 0.25f, false); SubHit(m, st, new JObject { ["col"] = "#cfe0ff" }); }
                }
            }
            if (ambushT > 0) ambushT -= dt;
            if (frenzyT > 0) { frenzyT -= dt; var pet = AldaraPet.I; if (pet && pet.cd > 0) pet.cd -= dt; }
            for (int i = burns.Count - 1; i >= 0; i--)
            {
                var b = burns[i]; b.t -= dt; b.tick -= dt; if (b.m.dead || b.t <= 0) { burns.RemoveAt(i); continue; }
                if (Random.value < dt * 10) AldaraVfx.Burst(b.m.x + (Random.value - 0.5f) * b.m.r, b.m.y - b.m.r * 0.6f, Hx(Random.value < 0.5f ? "#ff8a3a" : "#ffd35a"), 1, 40);
                if (b.tick <= 0) { b.tick = 1; busy++; var d = AldaraMonsters.HitSrc; try { AldaraMonsters.HitSrc = "player"; AldaraMonsters.I.HitMonster(b.m, b.dps, Hx("#ff8a3a")); } finally { AldaraMonsters.HitSrc = d; busy--; } }
            }
            for (int i = hammers.Count - 1; i >= 0; i--) { hammers[i].t += dt; if (hammers[i].t >= hammers[i].T) hammers.RemoveAt(i); }
            // the subclass bar's keys
            if (H.alive && !AldaraHud.Typing) for (int i = 0; i < 4; i++) if (AldaraKeys.Pressed("sub" + (i + 1))) UseSlot(i);
            autoT -= dt; if (autoT <= 0) { autoT = 0.2f; AutoTick(); }
        }

        // ---- the subclass bar ----
        public static readonly string[] ORDER = { "root", "a2", "a3", "c" };
        public class Slot { public string key; public AldaraTree.SubNode n; public SkillDef sk; }
        public static List<Slot> Slots()
        {
            var o = new List<Slot>(); var S = Cur; if (S == null) return o;
            foreach (var key in ORDER)
            {
                var n = S.nodes.FirstOrDefault(x => x.key == key); if (n == null) continue; if (key == "c" && n.sk == null) continue;
                o.Add(new Slot { key = key, n = n, sk = key == "root" ? Signature() : (AldaraTree.SubHas(n.id) ? Find(n.skillId) : null) });
            }
            return o;
        }
        static float PassRange(SkillDef sk) { if (H.cls == "knight") return sk.custom != null && sk.custom.Any(o => (string)o["op"] == "dashto") ? 380 : 230; return 480; }
        public static void UseSlot(int i)
        {
            if (!H.alive) return; var S = AldaraTree.SubCur();
            if (S == null || H.lvl < AldaraTree.B.SUB_LEVEL) { AldaraHud.Banner(H.lvl < AldaraTree.B.SUB_LEVEL ? "Subclass abilities open at level " + AldaraTree.B.SUB_LEVEL : "Choose a subclass first (K)"); return; }
            var L = Slots(); if (i >= L.Count) return; var e = L[i]; if (e.sk == null) { AldaraHud.Banner("Learn " + e.n.n + " in your subclass path first"); return; }
            var sk = e.sk; var SK = AldaraSkills.I; if (SK.Cd(sk.id) > 0) { AldaraFx.Text(P.x, P.y - 26, sk.name + " not ready", Hx("#aaaabb")); return; }
            if (sk.kind == "target")
            {
                float R = PassRange(sk); var t = H.target != null && !H.target.dead ? H.target : null;
                if (t == null) { float bd = 1e9f; foreach (var m in Nearby(P.x, P.y, R)) { float d = Mathf.Sqrt((m.x - P.x) * (m.x - P.x) + (m.y - P.y) * (m.y - P.y)); if (d < bd) { bd = d; t = m; } } }
                if (t != null && Mathf.Sqrt((t.x - P.x) * (t.x - P.x) + (t.y - P.y) * (t.y - P.y)) > R + t.r) { AldaraFx.Text(P.x, P.y - 26, "Too far", Hx("#aaaabb")); return; }
                if (t == null) { P.facing = H.AimAngle(); var at = H.AimPoint(false); t = AldaraSkills.Dummy(at.x, at.y); if (H.cls == "knight") { var m = H.MeleeArcTarget(70); if (m != null) t = m; } }
                else P.facing = Mathf.Atan2(t.y - P.y, t.x - P.x);
                SK.CastTargetPublic(t, sk);
            }
            else SK.CastSelfPublic(sk);
        }
        /// subAutoTick: auto combat uses the bar too (capstone first), saving the long ones for worthy fights
        static float autoNext;
        static void AutoTick()
        {
            if (!AldaraAuto.on || !H.alive || Cur == null || Time.time < autoNext) return;
            var t = H.target != null && !H.target.dead && !H.target.dummy ? H.target : null;
            float dT = t != null ? Mathf.Sqrt((t.x - P.x) * (t.x - P.x) + (t.y - P.y) * (t.y - P.y)) : 1e9f; System.Func<float, int> near = r => Nearby(P.x, P.y, r).Count;
            bool fighting = t != null && dT < 560 || near(260) > 0; float hpF = H.hp / H.maxHp;
            if (!fighting && hpF >= 0.5f) return;
            var slots = Slots();
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                var sk = slots[i].sk; if (sk == null || AldaraSkills.I.Cd(sk.id) > 0) continue;
                System.Func<string, bool> has = op => sk.custom != null && sk.custom.Any(o => (string)o["op"] == op);
                bool big = sk.cd >= 30, worthy = t == null ? near(220) >= 3 : (t.boss || t.tier >= 11 || t.hp > H.atk * 12 || near(220) >= 3);
                if (big && !worthy) continue;
                bool use = false;
                switch (sk.kind)
                {
                    case "heal": use = hpF < 0.5f; break;
                    case "shield": use = fighting && !(H.shield > 0) && hpF < 0.9f; break;
                    case "buff": use = fighting && !(sk.buff == "atk" ? H.atkBuff > 0 : sk.buff == "haste" ? H.hasteBuff > 0 : false); break;
                    case "blink": use = hpF < 0.35f && near(160) > 0; break;
                    case "self_aoe": use = near(sk.radius > 0 ? sk.radius : 200) > 0; break;
                    default:
                        if (t == null) break; { float R = PassRange(sk); if (has("blinkback")) use = hpF < 0.4f && dT < 170; else if (has("dashto")) use = dT > 150 && dT <= R + t.r; else use = dT <= R + t.r; }
                        break;
                }
                if (use) { UseSlot(i); autoNext = Time.time + 1.2f; return; }
            }
        }

        // ---- the Hammer of Justice (PFXA.jhammer) ----
        public class Hammer { public AldaraMonsters.Mon m; public float x, y, side, t, T; public Color c; }
        static readonly List<Hammer> hammers = new List<Hammer>();
        void DrawAir(AldaraVfx V)
        {
            var aN = V.AirN; var aA = V.AirA;
            foreach (var f in hammers)
            {
                float u = f.t / f.T, IMP = 0.49f, S = 1.2f, L = 175 * S; var col = f.c;
                if (u < IMP && f.m != null && !f.m.dead) { f.x = f.m.x; f.y = f.m.y; }
                float side = f.side, x = f.x, y = f.y, px = x - side * 125 * S, py = y - 150 * S, bz = AldaraVfx.LZ(x, y);
                float ti = Mathf.Atan2((y - 28) - py, x - px), tr = ti - side * 2.5f, th, al;
                if (u < 0.26f) { float k = u / 0.26f; th = tr + side * 0.12f * Mathf.Sin(k * Mathf.PI); al = k; }
                else if (u < IMP) { float k = (u - 0.26f) / (IMP - 0.26f); th = tr + (ti - tr) * k * k * k; al = 1; }
                else { float k = u - IMP; th = ti - side * 0.06f * Mathf.Sin(Mathf.Min(1, k / 0.12f) * Mathf.PI) * Mathf.Exp(-k * 6); al = u < 0.66f ? 1 : Mathf.Max(0, 1 - (u - 0.66f) / 0.34f); }
                System.Action<float, float, bool> draw = (ang, alpha, ghost) =>
                {
                    float hx = px + Mathf.Cos(ang) * L, hy = py + Mathf.Sin(ang) * L, rot = ang - Mathf.PI / 2, cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
                    System.Func<float, float, Vector2> T = (lx, ly) => new Vector2(hx + (cr * lx - sr * ly) * S, hy + (sr * lx + cr * ly) * S);
                    System.Func<float, float, float, float, Vector2[]> R = (x0, y0, w, h) => new[] { T(x0, y0), T(x0 + w, y0), T(x0 + w, y0 + h), T(x0, y0 + h) };
                    if (ghost) { var gc = AldaraVfx.A(col, 0.22f * alpha); aA.Poly(R(-72, -40, 144, 80), gc, bz); aA.Poly(R(-8, -146, 16, 146), gc, bz); return; }
                    var c0 = T(0, 0); var c1 = T(0, -70); AldaraVfx.Glow(aA, c0.x, c0.y, 120 * S, col, 0.55f * alpha, bz); AldaraVfx.Glow(aA, c1.x, c1.y, 60 * S, col, 0.25f * alpha, bz);
                    float a9 = Mathf.Clamp01(alpha * 0.93f); Color dk = AldaraVfx.A(Hx("#6a4a12"), a9), lt = AldaraVfx.A(Hx("#fff0b0"), a9);
                    aN.PolyC(new[] { T(-8, -146), T(0, -146), T(0, -36), T(-8, -36) }, new[] { dk, lt, lt, dk }, bz); aN.PolyC(new[] { T(0, -146), T(8, -146), T(8, -36), T(0, -36) }, new[] { lt, dk, dk, lt }, bz);
                    for (int q = 0; q < 4; q++) aN.Poly(R(-10, -128 + q * 20, 20, 5), AldaraVfx.A(Hx("#fff6d0"), a9), bz);
                    var pm = T(0, -152); AldaraVfx.Circle(aN, pm.x, pm.y, 13 * S, AldaraVfx.A(col, a9), bz); AldaraVfx.Ring(aN, pm.x, pm.y, 13 * S, 13 * S, 3 * S, AldaraVfx.A(Hx("#fffbe6"), a9), bz);
                    Color gt = AldaraVfx.A(Hx("#fffbe6"), a9), gm = AldaraVfx.A(col, a9), gb = AldaraVfx.A(Hx("#9a6410"), a9);
                    aN.PolyC(new[] { T(-50, -34), T(50, -34), T(50, -3), T(-50, -3) }, new[] { gt, gt, gm, gm }, bz); aN.PolyC(new[] { T(-50, -3), T(50, -3), T(50, 34), T(-50, 34) }, new[] { gm, gm, gb, gb }, bz);
                    foreach (var sd in new[] { -1f, 1f })
                    {
                        aN.PolyC(new[] { T(sd * 48, -40), T(sd * 76, -46), T(sd * 76, 46), T(sd * 48, 40) }, new[] { gt, gt, gb, gb }, bz);
                        aN.Polyline(new[] { T(sd * 48, -40), T(sd * 76, -46), T(sd * 76, 46), T(sd * 48, 40) }, 3 * S, AldaraVfx.A(Hx("#fff3b0"), a9), true, bz);
                        aN.Poly(R(sd * 70 - (sd > 0 ? 4 : 0), -42, 4, 84), new Color(1, 1, 1, 0.55f * a9), bz);
                    }
                    aN.Polyline(R(-50, -34, 100, 68), 3 * S, AldaraVfx.A(Hx("#fff3b0"), a9), true, bz); aN.Polyline(R(-42, -26, 84, 52), 2 * S, AldaraVfx.A(Hx("#8a5a10"), a9), true, bz);
                    float pulse = 0.75f + 0.25f * Mathf.Sin(f.t * 18); AldaraVfx.Glow(aA, c0.x, c0.y, 34 * S, Color.white, 0.8f * alpha * pulse, bz);
                    AldaraVfx.Ring(aA, c0.x, c0.y, 17 * S, 17 * S, 3 * S, AldaraVfx.A(Color.white, 0.95f * alpha), bz);
                    for (int q = 0; q < 8; q++) { float a = q / 8f * Mathf.PI * 2 + f.t * 2; var p0 = T(Mathf.Cos(a) * 21, Mathf.Sin(a) * 21); var p1 = T(Mathf.Cos(a) * (q % 2 == 1 ? 27 : 32), Mathf.Sin(a) * (q % 2 == 1 ? 27 : 32)); aA.Line(p0.x, p0.y, p1.x, p1.y, 3 * S, AldaraVfx.A(Color.white, 0.95f * alpha), bz); }
                    var a0 = T(-8, 0); var a1 = T(8, 0); var b0 = T(0, -8); var b1 = T(0, 8); aA.Line(a0.x, a0.y, a1.x, a1.y, 2 * S, AldaraVfx.A(col, 0.9f * alpha), bz); aA.Line(b0.x, b0.y, b1.x, b1.y, 2 * S, AldaraVfx.A(col, 0.9f * alpha), bz);
                };
                if (u >= 0.26f && u < IMP + 0.05f) for (int q = 4; q >= 1; q--) { float k = Mathf.Max(0, (Mathf.Min(u, IMP) - 0.26f) / (IMP - 0.26f) - q * 0.07f); draw(tr + (ti - tr) * k * k * k, 1 - q / 5f, true); }
                draw(th, al, false);
                if (u < 0.3f) for (int q = 0; q < 6; q++) { float a = f.t * 3 + q, r = 60 * (1 - u / 0.3f) + 10; AldaraVfx.Glow(aA, px + Mathf.Cos(tr) * L + Mathf.Cos(a) * r, py + Mathf.Sin(tr) * L + Mathf.Sin(a) * r, 10, Color.white, 0.6f, bz); }
            }
        }
    }
}
