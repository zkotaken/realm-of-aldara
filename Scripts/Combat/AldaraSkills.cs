using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldara
{
    [System.Serializable] public class SkillDef
    {
        public string cls, id, name, kind, buff, desc; public int back;
        public float price, cost, cd, mult, radius, stun, dur, heal, splash, slow, jumps, shield, count, dist, waves, extra, reach, dot, dotT;
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
        public SkillDef Get(string id) { foreach (var s in classSkills) if (s.id == id) return s; return null; }
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
        void Hit(AldaraMonsters.Mon m, float mult, string col) { AldaraMonsters.I.HitMonster(m, H.RollDmg(mult), AldaraRules.Hex(col)); }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            var keys = new List<string>(cds.Keys); foreach (var k in keys) if (cds[k] > 0) cds[k] -= dt;
            for (int i = delayed.Count - 1; i >= 0; i--) { delayed[i].t -= dt; if (delayed[i].t <= 0) { var d = delayed[i]; delayed.RemoveAt(i); d.fn(); } }
            var kb = Keyboard.current; if (kb == null || !H.alive || !AldaraSave.Ready || AldaraHud.Typing) return;
            var slots = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key, kb.digit0Key };
            for (int i = 0; i < slots.Length; i++) if (slots[i].wasPressedThisFrame) UseSlot(i);
        }
        public void UseSlot(int i)
        {
            if (i >= equipped.Count) return; var sk = Get(equipped[i]); if (sk == null) return;
            if (Cd(sk.id) > 0) { AldaraFx.Text(P.x, P.y - 26, sk.name + " not ready", AldaraRules.Hex("#aaaabb")); return; }
            if (H.mana < sk.cost) { AldaraFx.Text(P.x, P.y - 26, "Not enough mana", AldaraRules.Hex("#8aa0ff")); return; }
            if (sk.kind == "target")
            {
                if (H.target != null && !H.target.dead) { queued = sk.id; return; }
                // no target: at the cursor, or the way you face; knights hit whatever is in front
                float ang = H.AimAngle(); P.facing = ang;
                AldaraMonsters.Mon t = null; Vector2 at = H.AimPoint(false);
                if (H.cls == "knight") t = H.MeleeArcTarget(70);
                CastTarget(t, at, sk);
            }
            else CastSelf(sk);
        }
        /// called by the hero's attack tick when in range of its target
        public bool TryQueued(AldaraMonsters.Mon t)
        {
            if (queued == null) return false; var sk = Get(queued); queued = null;
            if (!Ready(sk)) return false; CastTarget(t, new Vector2(t.x, t.y), sk); return true;
        }
        void CastSelf(SkillDef sk)
        {
            Spend(sk); AldaraFx.Text(P.x, P.y - 42, sk.name, AldaraRules.Hex("#9fb4ff")); H.PlaySkill(sk.id);
            switch (sk.kind)
            {
                case "heal": { float amt = Mathf.Round(H.maxHp * sk.heal); H.hp = Mathf.Min(H.maxHp, H.hp + amt); AldaraFx.Text(P.x, P.y - 58, "+" + amt + " HP", AldaraRules.Hex("#7fe07f")); AldaraFx.Ring(P.x, P.y, 60, AldaraRules.Hex("#9fffa0"), 0.6f); break; }
                case "buff": if (sk.buff == "atk") H.atkBuff = sk.dur; else H.hasteBuff = sk.dur; AldaraFx.Ring(P.x, P.y, 50, AldaraRules.Hex(sk.buff == "atk" ? "#ff6a4a" : "#7fe07f"), 0.5f); break;
                case "shield": H.shield = Mathf.Round(H.maxHp * sk.shield); H.shieldT = sk.dur; AldaraFx.Ring(P.x, P.y, 40, AldaraRules.Hex(H.cls == "mage" ? "#8ab4ff" : "#ffd35a"), 0.5f); break;
                case "self_aoe":
                    {
                        string col = sk.id == "frost_nova" ? "#8fdfff" : sk.id == "battle_roar" ? "#ff9a3a" : "#ffe07a";
                        AldaraFx.Ring(P.x, P.y, sk.radius, AldaraRules.Hex(col), 0.45f);
                        foreach (var m in Nearby(P.x, P.y, sk.radius)) { Hit(m, sk.mult, col); if (!m.dead) { if (sk.stun > 0) m.stunT = sk.stun; if (sk.slow > 0) m.slowT = sk.slow; } }
                        break;
                    }
                case "blink":
                    {
                        float a = H.AimAngle(); if (sk.back != 0) a += Mathf.PI; AldaraFx.Burst(P.x, P.y, 30, AldaraRules.Hex(H.cls == "mage" ? "#b07aff" : "#d8c8a0"));
                        H.DashTo(a, sk.dist); AldaraFx.Burst(P.x, P.y, 30, AldaraRules.Hex(H.cls == "mage" ? "#b07aff" : "#d8c8a0")); H.target = null; queued = null; break;
                    }
            }
        }
        void CastTarget(AldaraMonsters.Mon t, Vector2 at, SkillDef sk)
        {
            Spend(sk); AldaraFx.Text(P.x, P.y - 42, sk.name, AldaraRules.Hex("#9fb4ff")); H.PlaySkill(sk.id); H.MarkFight();
            float tx = t != null ? t.x : at.x, ty = t != null ? t.y : at.y;
            if (t != null) P.facing = Mathf.Atan2(t.y - P.y, t.x - P.x);
            switch (sk.id)
            {
                // ---- knight ----
                case "power_strike": case "judgement": case "shield_bash":
                    if (t == null) { AldaraFx.Slash(tx, ty, P.facing, AldaraRules.Hex("#ffe07a"), 40, false); break; }
                    Hit(t, sk.mult, sk.id == "judgement" ? "#fff4b0" : "#ffe07a"); AldaraFx.Burst(t.x, t.y, 40, AldaraRules.Hex("#ffe07a"));
                    if (!t.dead && sk.stun > 0) { t.stunT = sk.stun; AldaraFx.Text(t.x, t.y - t.r - 26, "Stunned", AldaraRules.Hex("#ffe07a")); }
                    break;
                case "ground_slam":
                    AldaraFx.Ring(tx, ty, sk.radius, AldaraRules.Hex("#ffe07a"), 0.45f);
                    if (t != null) Hit(t, sk.mult, "#ffe07a"); foreach (var m in Nearby(tx, ty, sk.radius, t)) Hit(m, sk.mult, "#ffe07a"); break;
                case "cleave":
                    AldaraFx.Ring(tx, ty, sk.radius, AldaraRules.Hex("#ffe07a"), 0.35f);
                    if (t != null) Hit(t, sk.mult, "#ffe07a"); foreach (var m in Nearby(tx, ty, sk.radius, t)) Hit(m, sk.splash, "#ffe07a"); break;
                case "leap_strike":
                    {
                        float a = Mathf.Atan2(ty - P.y, tx - P.x), d = Mathf.Max(0, Mathf.Sqrt((tx - P.x) * (tx - P.x) + (ty - P.y) * (ty - P.y)) - 30);
                        H.DashTo(a, d); AldaraFx.Ring(P.x, P.y, sk.radius, AldaraRules.Hex("#ffe07a"), 0.45f);
                        foreach (var m in Nearby(P.x, P.y, sk.radius)) Hit(m, sk.mult, "#ffe07a"); break;
                    }
                // ---- mage ----
                case "fireball":
                    H.Shoot(t, at, AldaraRules.Hex("#ff8a3a"), 430, 0.5f, m => { AldaraFx.Ring(m.x, m.y, sk.radius * 0.75f, AldaraRules.Hex("#ffb14a"), 0.4f); var sp = Nearby(m.x, m.y, sk.radius, m); Hit(m, sk.mult, "#ffb14a"); foreach (var o in sp) Hit(o, sk.splash, "#ffb14a"); }); break;
                case "ice_shard":
                    H.Shoot(t, at, AldaraRules.Hex("#bfefff"), 540, 0.3f, m => { Hit(m, sk.mult, "#bfefff"); if (!m.dead) m.slowT = sk.slow; }); break;
                case "chain_lightning":
                    {
                        if (t == null) break; var chain = new List<AldaraMonsters.Mon> { t }; var cur = t;
                        for (int j = 0; j < sk.jumps; j++)
                        {
                            AldaraMonsters.Mon next = null; float bd = 1e9f;
                            foreach (var m in Nearby(cur.x, cur.y, 190)) { if (chain.Contains(m)) continue; float d = (m.x - cur.x) * (m.x - cur.x) + (m.y - cur.y) * (m.y - cur.y); if (d < bd) { bd = d; next = m; } }
                            if (next == null) break; chain.Add(next); cur = next;
                        }
                        foreach (var m in chain) { AldaraFx.Burst(m.x, m.y, 26, AldaraRules.Hex("#cfe0ff")); Hit(m, sk.mult, "#cfe0ff"); }
                        break;
                    }
                case "meteor": case "arcane_cataclysm":
                    {
                        string col = sk.id == "meteor" ? "#ffb14a" : "#d0a0ff"; float delay = sk.id == "meteor" ? 0.6f : 0.55f;
                        AldaraFx.Disc(tx, ty, sk.radius, new Color(1, 0.5f, 0.2f, 0.25f), delay);
                        var tt = t; Later_(delay, () => { AldaraFx.Ring(tx, ty, sk.radius, AldaraRules.Hex(col), 0.5f); foreach (var m in Nearby(tx, ty, sk.radius)) Hit(m, m == tt ? sk.mult : sk.splash, col); });
                        break;
                    }
                case "arcane_missiles": case "barrage":
                    for (int k = 0; k < sk.count; k++)
                    {
                        var tt = t; var a0 = at; Later_(k * 0.12f, () =>
                        {
                            var x = tt; if (x != null && x.dead) x = NearestTo(x.x, x.y, 250) ?? x;
                            H.Shoot(x != null && !x.dead ? x : null, a0, AldaraRules.Hex(sk.id == "barrage" ? "#ffd35a" : "#c07aff"), 700, 0.2f, m => Hit(m, sk.mult, sk.id == "barrage" ? "#ffd35a" : "#d0a0ff"));
                        });
                    }
                    break;
                case "blizzard":
                    for (int w = 0; w < sk.waves; w++) Later_(0.15f + w * 0.45f, () => { AldaraFx.Ring(tx, ty, sk.radius, AldaraRules.Hex("#cfefff"), 0.4f); foreach (var m in Nearby(tx, ty, sk.radius)) { Hit(m, sk.mult, "#cfefff"); if (!m.dead) m.slowT = sk.slow; } });
                    break;
                // ---- archer ----
                case "multi_shot":
                    {
                        var list = new List<AldaraMonsters.Mon>(); if (t != null) list.Add(t);
                        foreach (var m in Nearby(P.x, P.y, sk.reach)) { if (list.Count >= 1 + sk.extra) break; if (!list.Contains(m)) list.Add(m); }
                        if (list.Count == 0) H.Shoot(null, at, AldaraRules.Hex("#ffd35a"), 780, 0.18f, m => Hit(m, sk.mult, "#ffd35a"));
                        foreach (var m in list) H.Shoot(m, at, AldaraRules.Hex("#ffd35a"), 780, 0.18f, x => Hit(x, sk.mult, "#ffd35a"));
                        break;
                    }
                case "piercing_arrow":
                    {   // everything on the line out to the target and beyond
                        float ang = Mathf.Atan2(ty - P.y, tx - P.x), len = 520;
                        foreach (var o in AldaraMonsters.I.all)
                        {
                            if (o.dead) continue; float px = o.x - P.x, py = o.y - P.y, along = px * Mathf.Cos(ang) + py * Mathf.Sin(ang), off = Mathf.Abs(-px * Mathf.Sin(ang) + py * Mathf.Cos(ang));
                            if (along > 0 && along < len && off < o.r + 14) Hit(o, sk.mult, "#ffd35a");
                        }
                        H.Shoot(null, new Vector2(P.x + Mathf.Cos(ang) * len, P.y + Mathf.Sin(ang) * len), AldaraRules.Hex("#ffe07a"), 1100, 0.2f, null);
                        break;
                    }
                case "frost_arrow":
                    H.Shoot(t, at, AldaraRules.Hex("#bfefff"), 780, 0.2f, m => { Hit(m, sk.mult, "#bfefff"); if (!m.dead) m.slowT = sk.slow; }); break;
                case "rain_of_arrows":
                    AldaraFx.Disc(tx, ty, sk.radius, new Color(1, 0.85f, 0.4f, 0.22f), 0.4f);
                    Later_(0.4f, () => { foreach (var m in Nearby(tx, ty, sk.radius)) Hit(m, sk.mult, "#ffd35a"); }); break;
                case "explosive_arrow": case "starfall_arrow":
                    {
                        string col = sk.id == "starfall_arrow" ? "#fff4b0" : "#ffb14a";
                        H.Shoot(t, at, AldaraRules.Hex(col), 780, 0.22f, m => { AldaraFx.Ring(m.x, m.y, sk.radius, AldaraRules.Hex(col), 0.4f); var sp = Nearby(m.x, m.y, sk.radius, m); Hit(m, sk.mult, col); foreach (var o in sp) Hit(o, sk.splash, col); });
                        break;
                    }
                case "poison_arrow":
                    H.Shoot(t, at, AldaraRules.Hex("#6aca3a"), 780, 0.2f, m => { Hit(m, sk.mult, "#9aff6a"); if (!m.dead) { m.dotT = sk.dotT; m.dotTick = 1; m.dotDmg = H.RollDmg(sk.dot); AldaraFx.Text(m.x, m.y - m.r - 26, "Poisoned", AldaraRules.Hex("#9aff6a")); } }); break;
                case "explosive_trap":
                    AldaraFx.Disc(tx, ty, sk.radius, new Color(1, 0.6f, 0.2f, 0.25f), 0.8f);
                    Later_(0.8f, () => { AldaraFx.Ring(tx, ty, sk.radius, AldaraRules.Hex("#ffb14a"), 0.4f); foreach (var m in Nearby(tx, ty, sk.radius)) { Hit(m, sk.mult, "#ffb14a"); if (!m.dead) m.stunT = sk.stun; } }); break;
            }
        }
        AldaraMonsters.Mon NearestTo(float x, float y, float rad)
        {
            AldaraMonsters.Mon b = null; float bd = 1e9f; foreach (var m in Nearby(x, y, rad)) { float d = (m.x - x) * (m.x - x) + (m.y - y) * (m.y - y); if (d < bd) { bd = d; b = m; } } return b;
        }
    }
}
