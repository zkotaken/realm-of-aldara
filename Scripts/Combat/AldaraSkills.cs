using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldara
{
    [System.Serializable] public class SkillDef
    {
        public string cls, id, name, kind, buff, desc; public int back;
        public float price, cost, cd, mult, radius, stun, dur, heal, splash, slow, jumps, shield, count, dist, waves, extra, reach, dot, dotT;
        // subclass skills: the signature (its subclass and the skill it grows from) or a path ability (its node and effects)
        [System.NonSerialized] public AldaraTree.Sub sub; [System.NonSerialized] public AldaraTree.SubNode subNode; [System.NonSerialized] public Newtonsoft.Json.Linq.JArray custom; [System.NonSerialized] public string baseId;
    }
    [System.Serializable] class SkillBook { public SkillDef[] skills; }

    // The class skills, ported from the browser (SKILL_DB, useSlot, castSelf, castSkill, updateSkills): keys 1-9 and 0,
    // cooldowns and mana, a target skill is queued until you are in range (or fires at the cursor with no target),
    // self skills go off at once. Skills are learned in the skill tree (AldaraTree). 
    public class AldaraSkills : MonoBehaviour
    {
        public static AldaraSkills I;
        public List<SkillDef> classSkills = new List<SkillDef>();
        public List<string> owned = new List<string>(), equipped = new List<string>();
        public readonly Dictionary<string, float> cds = new Dictionary<string, float>();
        public string queued;
        class Later { public float t; public System.Action fn; }
        readonly List<Later> delayed = new List<Later>();
        AldaraHero H; AldaraPlayer P;

        void Awake() { I = this; H = GetComponent<AldaraHero>(); P = GetComponent<AldaraPlayer>(); }
        void Start() { Load(H.cls); }
        public void Load(string cls)
        {
            var book = JsonUtility.FromJson<SkillBook>(Resources.Load<TextAsset>("skills").text);
            classSkills.Clear(); foreach (var s in book.skills) if (s.cls == cls) classSkills.Add(s);
            owned.Clear(); equipped.Clear(); if (classSkills.Count > 0) { owned.Add(classSkills[0].id); equipped.Add(classSkills[0].id); }
        }
        public SkillDef Get(string id) { foreach (var s in classSkills) if (s.id == id) return s; return AldaraSubclass.Find(id); }
        public SkillDef BaseGet(string id) { foreach (var s in classSkills) if (s.id == id) return s; return null; }
        public void SpendPublic(SkillDef s) { Spend(s); }
        public void CastTargetRaw(AldaraMonsters.Mon t, SkillDef sk) { CastTarget(t, sk, true); }
        public void CastSelfRaw(SkillDef sk) { CastSelf(sk, true); }
        public float Cd(string id) { float v; return cds.TryGetValue(id, out v) ? v : 0; }
        public bool Ready(SkillDef s) { return s != null && Cd(s.id) <= 0 && H.mana >= s.cost; }
        void Spend(SkillDef s) { H.mana -= Mathf.Round(s.cost * (1 - AldaraTree.T("mcost"))); cds[s.id] = s.cd * (1 - AldaraTree.T("cdr")); }
        public void Later_(float t, System.Action fn) { delayed.Add(new Later { t = t, fn = fn }); }
        List<AldaraMonsters.Mon> Nearby(float x, float y, float rad, AldaraMonsters.Mon except = null)
        {
            var o = new List<AldaraMonsters.Mon>();
            foreach (var m in AldaraMonsters.I.all) if (!m.dead && m != except && Mathf.Sqrt((m.x - x) * (m.x - x) + (m.y - y) * (m.y - y)) <= rad + m.r) o.Add(m);
            return o;
        }
        void Hit(AldaraMonsters.Mon m, float mult, string col) { if (m == null || m.dummy) return; AldaraMonsters.I.HitMonster(m, H.RollDmg(mult), AldaraRules.Hex(col)); }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            var keys = new List<string>(cds.Keys); foreach (var k in keys) if (cds[k] > 0) cds[k] -= dt;
            for (int i = delayed.Count - 1; i >= 0; i--) { delayed[i].t -= dt; if (delayed[i].t <= 0) { var d = delayed[i]; delayed.RemoveAt(i); d.fn(); } }
            var kb = Keyboard.current; if (kb == null || !H.alive || !AldaraSave.Ready || AldaraHud.Typing) return;
            for (int i = 0; i < 10; i++) if (AldaraKeys.Pressed("slot" + (i + 1))) UseSlot(i);
            if (AldaraKeys.Pressed("quick")) UseSlot(0);
        }
        public void UseSlot(int i)
        {
            if (!H.alive || i >= equipped.Count) return; var sk = Get(equipped[i]); if (sk == null) return;
            if (Cd(sk.id) > 0) { AldaraFx.Text(P.x, P.y - 26, sk.name + " not ready", AldaraRules.Hex("#aaaabb")); return; }
            if (H.mana < sk.cost) { AldaraFx.Text(P.x, P.y - 26, "Not enough mana", AldaraRules.Hex("#8aa0ff")); return; }
            if (sk.kind == "target")
            {
                if (H.target != null && !H.target.dead) { queued = sk.id; return; }
                // no target: cast right away toward the cursor (or the way you face)
                P.facing = H.AimAngle(); var at = H.AimPoint(false); AldaraMonsters.Mon t = Dummy(at.x, at.y);
                if (H.cls == "knight") { var m = H.MeleeArcTarget(70); if (m != null) t = m; }
                CastTarget(t, sk);
            }
            else CastSelf(sk);
        }
        /// a point on the ground standing in for a target (the browser's dummy)
        public static AldaraMonsters.Mon Dummy(float x, float y) { return new AldaraMonsters.Mon { x = x, y = y, r = 6, dummy = true, name = "" }; }
        /// called by the hero's attack tick when in range of its target
        public bool TryQueued(AldaraMonsters.Mon t)
        {
            if (queued == null) return false; var sk = Get(queued); queued = null;
            if (!Ready(sk)) return false; CastTarget(t, sk); return true;
        }
        public void CastSelfPublic(SkillDef sk) { CastSelf(sk); }
        public void CastTargetPublic(AldaraMonsters.Mon t, SkillDef sk) { CastTarget(t, sk); }
        public List<AldaraMonsters.Mon> NearbyPublic(float x, float y, float rad) { return Nearby(x, y, rad); }
        static Color Hx(string h) { return AldaraRules.Hex(h); }
        static void Burst(float x, float y, string c, int n, float spd) { AldaraVfx.Burst(x, y, Hx(c), n, spd); }
        static readonly Dictionary<string, string> SKCOL = new Dictionary<string, string> { { "fireball", "#ff8a3a" }, { "ice_shard", "#8fdfff" }, { "chain_lightning", "#cfe0ff" }, { "meteor", "#ff6a1a" }, { "frost_nova", "#8fdfff" }, { "arcane_missiles", "#c07aff" }, { "blizzard", "#bfefff" }, { "arcane_cataclysm", "#c07aff" }, { "rejuvenate", "#7fffb0" }, { "arcane_barrier", "#8ab4ff" }, { "blink", "#b07aff" } };
        void CastSelf(SkillDef sk, bool raw = false)
        {
            if (!raw) AldaraSound.Skill(sk);
            if (!raw && (sk.sub != null || sk.custom != null)) { AldaraSubclass.CastSub(sk, null); return; }
            Spend(sk); AldaraFx.Text(P.x, P.y - 42, sk.name, Hx("#9fb4ff")); H.PlaySkill(sk.id);
            string sc; if (SKCOL.TryGetValue(sk.id, out sc)) H.castCol = Hx(sc);
            AldaraSkillFx.Pfx(sk.id, null, P.x, P.y, sk, "cast");
            switch (sk.kind)
            {
                case "heal":
                    {
                        float amt = Mathf.Round(H.maxHp * sk.heal), hb = H.hp; H.hp = Mathf.Min(H.maxHp, H.hp + amt); DStat.Heal(H.hp - hb); AldaraFx.Text(P.x, P.y - 58, "+" + amt + " HP", Hx("#7fe07f"));
                        Burst(P.x, P.y - 10, "#9fffa0", 22, 150); if (!AldaraMageFx.On && !AldaraKnightFx.On) { var f = AldaraVfx.Effect("heal", P.x, P.y, 0, 1.1f, Color.white); f.hasCol = true; f.col = Hx(H.cls == "mage" ? "#7fffb0" : "#7fe07f"); }
                        if (H.cls == "mage") H.Cast(0.5f); break;
                    }
                case "buff": if (sk.buff == "atk") H.atkBuff = sk.dur; else H.hasteBuff = sk.dur; Burst(P.x, P.y, sk.buff == "atk" ? "#ff6a4a" : "#7fe07f", 14, 160); break;
                case "shield":
                    {
                        H.shield = Mathf.Round(H.maxHp * sk.shield); H.shieldT = sk.dur; H.shieldName = null; string bc = H.cls == "mage" ? "#8ab4ff" : "#ffd35a";
                        if (!AldaraMageFx.On && !AldaraKnightFx.On) { var f = AldaraVfx.Effect("pulse", P.x, P.y, 34, 0.5f, Color.white); f.col = Hx(bc); } Burst(P.x, P.y - 10, bc, 16, 140); if (H.cls == "mage") H.Cast(0.4f); break;
                    }
                case "self_aoe":
                    {
                        string col = sk.id == "frost_nova" ? "#8fdfff" : sk.id == "battle_roar" ? "#ff9a3a" : "#ffe07a";
                        if (H.cls == "knight") H.StartSwing(0.4f, true, -1, sk.id == "whirlwind"); else H.Cast(0.4f);
                        if (sk.id == "frost_nova") { if (!AldaraMageFx.On) { var f = AldaraVfx.Effect("frost", P.x, P.y + 14, sk.radius, 1.0f, Color.white); f.ring = true; f.n = 20; } AldaraVfx.Shake(2.5f, 0.2f); }
                        Burst(P.x, P.y, col, 20, sk.radius * 1.4f);
                        foreach (var m in Nearby(P.x, P.y, sk.radius)) { Hit(m, sk.mult, col); Burst(m.x, m.y, col, 5, 120); if (!m.dead) { if (sk.stun > 0) m.stunT = sk.stun; if (sk.slow > 0) m.slowT = sk.slow; } }
                        break;
                    }
                case "blink":
                    {
                        float a = H.AimAngle(); if (sk.back != 0) a += Mathf.PI; string bc = H.cls == "mage" ? "#b07aff" : "#d8c8a0";
                        Burst(P.x, P.y, bc, 14, 160); float bx1 = P.x, by1 = P.y;
                        H.DashTo(a, sk.dist); AldaraSkillFx.Pfx(sk.id, null, P.x, P.y, sk, "land");
                        if (H.cls == "mage" && !AldaraMageFx.On) { var f = AldaraVfx.Effect("blink", P.x, P.y, 0, 0.5f, Color.white); f.x1 = bx1; f.y1 = by1; f.x2 = P.x; f.y2 = P.y; }
                        Burst(P.x, P.y, bc, 14, 160); H.target = null; queued = null; break;
                    }
            }
        }
        void Fire(string kind, AldaraMonsters.Mon t, System.Action<AldaraMonsters.Mon> fn, string glow = null, string color = null, float spd = 0) { H.Fire(kind, t, fn, glow, color, spd); }
        void Muzzle() { H.Release(0.2f); AldaraSkillFx.Pfx("muzzle", null, 0, 0, null, null, "#fff0c0"); }
        void Explode(float x, float y, float r, float life, bool big = false, string c1 = null, string c2 = null, string core = null)
        {
            var f = AldaraVfx.Effect("explode", x, y, r, life, Color.white); f.big = big; if (c1 != null) f.c1 = Hx(c1); if (c2 != null) f.c2 = Hx(c2); if (core != null) f.core = Hx(core);
        }
        void CastTarget(AldaraMonsters.Mon t, SkillDef sk, bool raw = false)
        {
            if (!raw) AldaraSound.Skill(sk);
            if (!raw && (sk.sub != null || sk.custom != null)) { AldaraSubclass.CastSub(sk, t); return; }
            if (t.dummy && H.cls == "knight" && sk.id != "ground_slam") { var mm = H.MeleeArcTarget(70); if (mm != null) t = mm; }
            Spend(sk); AldaraRelics.OnCast();
            AldaraFx.Text(P.x, P.y - 42, sk.name, Hx("#9fb4ff")); H.PlaySkill(sk.id); H.MarkFight();
            string sc; if (SKCOL.TryGetValue(sk.id, out sc)) H.castCol = Hx(sc);
            if (!t.dummy) P.facing = Mathf.Atan2(t.y - P.y, t.x - P.x);
            switch (sk.id)
            {
                // ---- knight ----
                case "power_strike":
                    H.StartSwing(0.35f, true); AldaraSkillFx.Pfx("power_strike", t, t.x, t.y, sk);
                    Hit(t, sk.mult, "#ffe07a"); Burst(t.x, t.y, "#ffe07a", 16, 220); break;
                case "shield_bash":
                    H.StartSwing(0.25f, false); AldaraSkillFx.Pfx("shield_bash", t, t.x, t.y, sk);
                    Hit(t, sk.mult, "#ffe07a"); if (!t.dead && !t.dummy) { t.stunT = sk.stun; AldaraFx.Text(t.x, t.y - t.r - 26, "Stunned", Hx("#ffe07a")); }
                    break;
                case "ground_slam":
                    H.StartSwing(0.4f, true); AldaraSkillFx.Pfx("ground_slam", t, t.x, t.y, sk);
                    Hit(t, sk.mult, "#ffe07a"); foreach (var m in Nearby(t.x, t.y, sk.radius, t)) Hit(m, sk.mult, "#ffe07a"); break;
                case "cleave":
                    H.StartSwing(0.3f, true); AldaraSkillFx.Pfx("cleave", t, t.x, t.y, sk);
                    { var sp = Nearby(t.x, t.y, sk.radius, t); Hit(t, sk.mult, "#ffe07a"); foreach (var m in sp) Hit(m, sk.splash, "#ffe07a"); }
                    break;
                case "leap_strike":
                    {
                        float a = Mathf.Atan2(t.y - P.y, t.x - P.x), d = Mathf.Max(0, Mathf.Sqrt((t.x - P.x) * (t.x - P.x) + (t.y - P.y) * (t.y - P.y)) - 30);
                        AldaraSkillFx.Pfx("leap_strike", null, P.x, P.y, sk, "cast"); float lx0 = P.x, ly0 = P.y; Burst(P.x, P.y, "#c9a36a", 10, 120); H.DashTo(a, Mathf.Min(d, 420));
                        if (AldaraKnightFx.On) AldaraKnightFx.Dash(AldaraKnightFx.G(lx0, ly0), AldaraKnightFx.G(P.x, P.y), AldaraSkillFx.TrailC);
                        else AldaraVfx.Mfx(new AldaraVfx.Mf { k = "streak", x = lx0, y = ly0, h = 30, a = a, len = Mathf.Sqrt((P.x - lx0) * (P.x - lx0) + (P.y - ly0) * (P.y - ly0)), c = AldaraSkillFx.TrailC, c2 = Color.white, w = 14, T = 0.3f });
                        H.StartSwing(0.35f, true); AldaraSkillFx.Pfx("leap_strike", null, P.x, P.y, sk, "land");
                        foreach (var m in Nearby(P.x, P.y, sk.radius)) Hit(m, sk.mult, "#ffe07a"); break;
                    }
                case "judgement":
                    H.StartSwing(0.4f, true); AldaraSkillFx.Pfx("judgement", t, t.x, t.y, sk); Burst(t.x, t.y, "#fff4b0", 26, 260);
                    Hit(t, sk.mult, "#fff4b0"); if (!t.dead && !t.dummy) { t.stunT = sk.stun; AldaraFx.Text(t.x, t.y - t.r - 26, "Stunned", Hx("#ffe07a")); }
                    break;
                // ---- mage ----
                case "fireball":
                    H.Cast(0.4f);
                    Fire("fireball", t, m =>
                    {
                        if (!AldaraMageFx.On) { Explode(m.x, m.y, sk.radius * 0.75f, 0.6f); AldaraVfx.Shake(2.5f, 0.18f); } AldaraSkillFx.Pfx("fireball", m, m.x, m.y, sk);
                        Burst(m.x, m.y - 16, "#ffb14a", 26, 260); Burst(m.x, m.y - 16, "#ff5a2a", 14, 160);
                        var sp = Nearby(m.x, m.y, sk.radius, m); Hit(m, sk.mult, "#ffb14a"); foreach (var o in sp) Hit(o, sk.splash, "#ffb14a");
                    }); break;
                case "ice_shard":
                    H.Cast(0.3f);
                    Fire("ice", t, m =>
                    {
                        Hit(m, sk.mult, "#bfefff"); Burst(m.x, m.y - 16, "#dff6ff", 14, 160); if (!m.dead) m.slowT = sk.slow;
                        if (!AldaraMageFx.On) { var f = AldaraVfx.Effect("frost", m.x, m.y + 8, Mathf.Max(56, m.r * 2.4f), 0.8f, Color.white); f.n = 7; } AldaraSkillFx.Pfx("ice_shard", m, m.x, m.y, sk);
                    }); break;
                case "chain_lightning":
                    {
                        H.Cast(0.3f); var chain = new List<AldaraMonsters.Mon> { t }; var cur = t;
                        for (int j = 0; j < sk.jumps; j++)
                        {
                            AldaraMonsters.Mon next = null; float bd = 1e9f;
                            foreach (var m in Nearby(cur.x, cur.y, 190)) { if (chain.Contains(m)) continue; float d = (m.x - cur.x) * (m.x - cur.x) + (m.y - cur.y) * (m.y - cur.y); if (d < bd) { bd = d; next = m; } }
                            if (next == null) break; chain.Add(next); cur = next;
                        }
                        var lpts = new List<Vector2> { new Vector2(P.x + Mathf.Cos(P.facing) * 24, P.y + Mathf.Sin(P.facing) * 24) }; foreach (var m in chain) lpts.Add(new Vector2(m.x, m.y));
                        if (AldaraMageFx.On) AldaraMageFx.Chain(lpts); else { var lf = AldaraVfx.Effect("lightning", P.x, P.y, 0, 0.4f, Color.white); lf.hasCol = true; lf.col = Hx("#8ab4ff"); lf.pts = lpts; }
                        AldaraVfx.Shake(1.5f, 0.15f);
                        foreach (var m in chain) { AldaraSkillFx.Pfx("chain_node", m, m.x, m.y, sk); Hit(m, sk.mult, "#cfe0ff"); Burst(m.x, m.y, "#cfe0ff", 6, 120); }
                        break;
                    }
                case "meteor":
                    {
                        H.Cast(0.5f); float ix = t.x, iy = t.y; var tt = t;
                        if (!AldaraMageFx.On) AldaraVfx.Effect("meteor3", ix, iy, sk.radius, 0.6f, Color.white); AldaraSkillFx.Pfx("meteor_mark", null, ix, iy, sk);
                        Later_(0.6f, () =>
                        {
                            if (!AldaraMageFx.On) { Explode(ix, iy, sk.radius, 0.8f, true);
                            AldaraVfx.Mfx(new AldaraVfx.Mf { k = "crack", x = ix, y = iy, a = Random.value * 6, len = sk.radius * 0.9f, c = Hx("#ff7a2a"), T = 2.2f, n = 7, r = sk.radius * 0.9f }); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "shock", x = ix, y = iy, r = sk.radius, c = Hx("#ff7a2a"), c2 = Color.white, T = 0.45f }); }
                            AldaraVfx.Shake(7, 0.35f); AldaraSkillFx.Pfx("meteor", null, ix, iy, sk);
                            Burst(ix, iy - 16, "#ffb14a", 36, 340); Burst(ix, iy - 16, "#ff5a2a", 22, 240); Burst(ix, iy, "#3a2a20", 12, 160);
                            foreach (var m in Nearby(ix, iy, sk.radius)) Hit(m, m == tt ? sk.mult : sk.splash, "#ffb14a");
                        });
                        break;
                    }
                case "arcane_missiles":
                    H.Cast(0.6f);
                    for (int k = 0; k < sk.count; k++)
                    {
                        var t0 = t; Later_(k * 0.12f, () =>
                        {
                            var tt = t0; if (tt.dead) tt = NearestTo(tt.x, tt.y, 250) ?? tt;
                            Fire("bolt", tt, m => { Hit(m, sk.mult, "#d0a0ff"); Burst(m.x, m.y - 16, "#c07aff", 8, 140); if (!AldaraMageFx.On) Explode(m.x, m.y, 30, 0.35f, false, "#c07aff", "#7a2aff", "#ffffff"); AldaraSkillFx.Pfx("bolt_hit", m, m.x, m.y, null, null, "#c07aff"); }, null, "#c07aff");
                        });
                    }
                    break;
                case "blizzard":
                    {
                        H.Cast(0.6f); float ix = t.x, iy = t.y;
                        if (AldaraMageFx.On) AldaraMageFx.Blizzard(ix, iy, sk.radius, 0.4f + sk.waves * 0.45f); else AldaraVfx.Effect("blizzard", ix, iy, sk.radius, 0.4f + sk.waves * 0.45f, Color.white);
                        for (int w = 0; w < sk.waves; w++) Later_(0.15f + w * 0.45f, () =>
                        {
                            if (!AldaraMageFx.On) { var f = AldaraVfx.Effect("frost", ix, iy, sk.radius * 0.85f, 0.6f, Color.white); f.n = 8; } AldaraSkillFx.Pfx("blizzard_wave", null, ix, iy, sk);
                            foreach (var m in Nearby(ix, iy, sk.radius)) { Hit(m, sk.mult, "#cfefff"); if (!m.dead) m.slowT = sk.slow; }
                        });
                        break;
                    }
                case "arcane_cataclysm":
                    {
                        H.Cast(0.7f); float ix = t.x, iy = t.y; var tt = t;
                        if (!AldaraMageFx.On) AldaraVfx.Effect("cataCharge", ix, iy, sk.radius, 0.6f, Color.white); AldaraSkillFx.Pfx("cata_charge", null, ix, iy, sk);
                        Later_(0.55f, () =>
                        {
                            if (!AldaraMageFx.On) { AldaraVfx.Effect("cataBoom", ix, iy, sk.radius, 0.9f, Color.white); Explode(ix, iy, sk.radius * 0.9f, 0.8f, true, "#c07aff", "#6a1aff", "#ffffff");
                            AldaraVfx.Mfx(new AldaraVfx.Mf { k = "crack", x = ix, y = iy, a = Random.value * 6, len = sk.radius, c = Hx("#c07aff"), T = 2.4f, n = 8, r = sk.radius }); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "shock", x = ix, y = iy, r = sk.radius * 1.1f, c = Hx("#c07aff"), c2 = Color.white, T = 0.45f }); }
                            AldaraSkillFx.Pfx("cataclysm", null, ix, iy, sk);
                            AldaraVfx.Shake(8, 0.4f); Burst(ix, iy - 16, "#c07aff", 40, 360); Burst(ix, iy - 16, "#ffffff", 16, 220);
                            foreach (var m in Nearby(ix, iy, sk.radius)) Hit(m, m == tt ? sk.mult : sk.splash, "#d0a0ff");
                        });
                        break;
                    }
                // ---- archer ----
                case "multi_shot":
                    {
                        Muzzle(); var extra = Nearby(P.x, P.y, sk.reach, t); extra.Sort((a, b) => ((a.x - P.x) * (a.x - P.x) + (a.y - P.y) * (a.y - P.y)).CompareTo((b.x - P.x) * (b.x - P.x) + (b.y - P.y) * (b.y - P.y)));
                        var list = new List<AldaraMonsters.Mon> { t }; for (int k = 0; k < extra.Count && k < sk.extra; k++) list.Add(extra[k]);
                        foreach (var m in list) Fire("arrow", m, o => { Hit(o, sk.mult, "#b8f0b8"); Burst(o.x, o.y, "#b8f0b8", 6, 130); }, "#9fe09f");
                        break;
                    }
                case "piercing_arrow":
                    {
                        Muzzle(); float sx = P.x, sy = P.y;
                        Fire("arrow", t, m =>
                        {
                            float ang = Mathf.Atan2(m.y - sy, m.x - sx), len = Mathf.Sqrt((m.x - sx) * (m.x - sx) + (m.y - sy) * (m.y - sy)) + 140;
                            var hits = new List<AldaraMonsters.Mon>();
                            foreach (var o in AldaraMonsters.I.all) { if (o.dead) continue; float px = o.x - sx, py = o.y - sy, along = px * Mathf.Cos(ang) + py * Mathf.Sin(ang), off = Mathf.Abs(-px * Mathf.Sin(ang) + py * Mathf.Cos(ang)); if (along > 0 && along < len && off < o.r + 14) hits.Add(o); }
                            if (!hits.Contains(m)) hits.Add(m);
                            AldaraVfx.Mfx(new AldaraVfx.Mf { k = "streak", x = sx, y = sy, h = AldaraVfx.LIFT, a = ang, len = len, c = Hx("#ffd35a"), c2 = Color.white, w = 9, T = 0.3f });
                            for (int k = 1; k < 5; k++) AldaraVfx.Mfx(new AldaraVfx.Mf { k = "ringA", x = sx + Mathf.Cos(ang) * k * len / 5, y = sy + Mathf.Sin(ang) * k * len / 5, h = AldaraVfx.LIFT, r = 16, c = Hx("#ffe07a"), T = 0.3f, dl = k * 0.03f });
                            foreach (var o in hits) { Hit(o, sk.mult, "#ffe07a"); Burst(o.x, o.y, "#ffe07a", 5, 120); }
                        }, "#ffe07a", null, 1100); break;
                    }
                case "frost_arrow":
                    Muzzle(); Fire("arrow", t, m => { Hit(m, sk.mult, "#bfefff"); Burst(m.x, m.y, "#bfefff", 8, 130); if (!m.dead) m.slowT = sk.slow; AldaraSkillFx.Pfx("frost_arrow", m, m.x, m.y, sk); }, "#8fdfff"); break;
                case "rain_of_arrows":
                    {
                        Muzzle(); float ix = t.x, iy = t.y;
                        for (int k = 0; k < 14; k++) { float a = Random.value * Mathf.PI * 2, r = Random.value * sk.radius; var f = AldaraVfx.Effect("arrowfall", ix + Mathf.Cos(a) * r, iy + Mathf.Sin(a) * r, 0, 0.35f + Random.value * 0.2f, Hx("#d8c8a0")); f.max = 0.5f; f.hasCol = true; }
                        Later_(0.4f, () => { AldaraSkillFx.Pfx("rain_land", null, ix, iy, sk); foreach (var m in Nearby(ix, iy, sk.radius)) { Hit(m, sk.mult, "#ffd35a"); Burst(m.x, m.y, "#e8d8b0", 4, 90); } });
                        break;
                    }
                case "poison_arrow":
                    Muzzle();
                    Fire("arrow", t, m =>
                    {
                        Hit(m, sk.mult, "#9aff6a"); Burst(m.x, m.y, "#6aca3a", 8, 120); AldaraSkillFx.Pfx("poison_arrow", m, m.x, m.y, sk);
                        if (!m.dead) { m.dotT = sk.dotT; m.dotTick = 1; m.dotDmg = H.RollDmg(sk.dot); AldaraFx.Text(m.x, m.y - m.r - 26, "Poisoned", Hx("#9aff6a")); }
                    }, "#6aca3a"); break;
                case "explosive_trap":
                    {
                        Muzzle(); float ix = t.x, iy = t.y; AldaraSkillFx.Pfx("trap_set", null, ix, iy, sk);
                        Later_(0.8f, () => { AldaraSkillFx.Pfx("trap_boom", null, ix, iy, sk); Burst(ix, iy, "#ffb14a", 26, 260); foreach (var m in Nearby(ix, iy, sk.radius)) { Hit(m, sk.mult, "#ffb14a"); if (!m.dead) m.stunT = sk.stun; } });
                        break;
                    }
                case "barrage":
                    for (int k = 0; k < sk.count; k++)
                    {
                        var t0 = t; Later_(k * 0.12f, () =>
                        {
                            H.Release(0.1f); AldaraSkillFx.Pfx("muzzle", null, 0, 0, null, null, "#fff0c0");
                            var tt = t0; if (tt.dead) tt = NearestTo(tt.x, tt.y, 250) ?? tt;
                            Fire("arrow", tt, m => { Hit(m, sk.mult, "#ffd35a"); Burst(m.x, m.y, "#e8d8b0", 4, 90); });
                        });
                    }
                    break;
                case "starfall_arrow":
                    Muzzle();
                    Fire("arrow", t, m => { AldaraSkillFx.Pfx("starfall_arrow", m, m.x, m.y, sk); Burst(m.x, m.y, "#fff4b0", 30, 300); var sp = Nearby(m.x, m.y, sk.radius, m); Hit(m, sk.mult, "#fff4b0"); foreach (var o in sp) Hit(o, sk.splash, "#fff4b0"); }, "#fff4b0"); break;
                case "explosive_arrow":
                    Muzzle();
                    Fire("arrow", t, m => { AldaraSkillFx.Pfx("explosive_arrow", m, m.x, m.y, sk); Burst(m.x, m.y, "#ffb14a", 22, 240); var sp = Nearby(m.x, m.y, sk.radius, m); Hit(m, sk.mult, "#ffb14a"); foreach (var o in sp) Hit(o, sk.splash, "#ffb14a"); }, "#ff8a3a"); break;
            }
        }
        AldaraMonsters.Mon NearestTo(float x, float y, float rad)
        {
            AldaraMonsters.Mon b = null; float bd = 1e9f; foreach (var m in Nearby(x, y, rad)) { float d = (m.x - x) * (m.x - x) + (m.y - y) * (m.y - y); if (d < bd) { bd = d; b = m; } } return b;
        }
    }
}
