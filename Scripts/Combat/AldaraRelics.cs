using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // What the relics do, from the browser (relicOnHit, relicOnKill, relicOnDamage, relicAfterDamage, relicSummon,
    // relicUpdate, relicDmgMult, relicSpeedMult and the Seraph's barrier in castSkill): lightning, life drinking,
    // thorns, frost novas, shadow steps, rebirth, a skeleton army, haste stacks, gold hoards, poison, wisp healing,
    // titan quakes. Their numbers come from the worn relic's rarity (AldaraGear.RelicV).
    public class AldaraRelics : MonoBehaviour
    {
        public static AldaraRelics I;
        static readonly Dictionary<string, float> cd = new Dictionary<string, float>();
        static readonly List<float> haste = new List<float>();
        static float quakeT, inCombat, wispCd, t;
        public static int busy;
        public class Poison { public int st; public float t, v, acc; }
        static readonly Dictionary<AldaraMonsters.Mon, Poison> poison = new Dictionary<AldaraMonsters.Mon, Poison>();
        public class Skel { public float x, y, r = 18, life, cd, lunge, lungeA, face = Mathf.PI / 2, rise = 0.7f, walk; public AldaraMonsters.Mon target; public bool necro; public GameObject view; public AldaraMonsterAnimator anim; public Transform model; public float lx, ly; }
        public static readonly List<Skel> skel = new List<Skel>();
        /// relicPop: a power going off, for the status strip
        public static event System.Action<string, string, string> Popped;

        void Awake() { I = this; AldaraVfx.DrawGround += DrawGround; }
        void OnDestroy() { AldaraVfx.DrawGround -= DrawGround; }
        static float V(string id) { return AldaraGear.RelicV(id); }
        static bool Has(string id) { return AldaraGear.RelicWorn(id) != null; }
        static Color Hx(string h) { return AldaraRules.Hex(h); }
        static AldaraHero H { get { return AldaraHero.I; } }
        static AldaraPlayer P { get { return AldaraPlayer.I; } }
        static void Pop(string n, string g, string c) { if (Popped != null) Popped(n, g, c); }
        static bool Cd(string id, float secs) { float v; if (cd.TryGetValue(id, out v) && v > t) return false; cd[id] = t + secs; return true; }
        static void Heal(float amt, string label)
        {
            if (!(amt > 0) || !H.alive) return; float b = H.hp; H.hp = Mathf.Min(H.maxHp, H.hp + amt); float got = H.hp - b; DStat.Heal(got);
            if (got > 0) AldaraFx.Text(P.x + 12, P.y - 46, "+" + Mathf.Round(got) + " HP", Hx(label ?? "#7fe07f"));
        }
        static List<AldaraMonsters.Mon> Nearby(float x, float y, float rad, AldaraMonsters.Mon except = null)
        {
            var o = new List<AldaraMonsters.Mon>(); foreach (var m in AldaraMonsters.I.all) if (!m.dead && m != except && Mathf.Sqrt((m.x - x) * (m.x - x) + (m.y - y) * (m.y - y)) <= rad + m.r) o.Add(m); return o;
        }
        static void Hit(AldaraMonsters.Mon m, float dmg, string col) { busy++; try { AldaraMonsters.I.HitMonster(m, dmg, Hx(col)); } finally { busy--; } }
        static void Ring(float x, float y, float r, string col, float life) { AldaraVfx.Ring(x, y, r, Hx(col), life); }

        public static int HasteCount { get { return haste.Count; } }
        public static float HasteLeft { get { float m = 0; foreach (var h in haste) m = Mathf.Max(m, h); return m - t; } }
        public static float CdLeft(string id) { float v; return cd.TryGetValue(id, out v) ? v - t : 0; }
        public static float DmgMult()
        {
            float k = 1, b = V("berserkskull"); if (b > 0) { float miss = 1 - Mathf.Max(0, H.hp) / H.maxHp; k *= 1 + b / 100 * Mathf.Min(1, miss / 0.9f); }
            float h = V("hourglass"); if (h > 0 && haste.Count > 0) k *= 1 + h / 100 * 0.5f * Mathf.Min(3, haste.Count); return k;
        }
        public static float SpeedMult() { float h = V("hourglass"); return h > 0 && haste.Count > 0 ? 1 + h / 100 * Mathf.Min(3, haste.Count) : 1; }
        public static bool PoisonedNow(AldaraMonsters.Mon m) { return poison.ContainsKey(m); }

        // ---- on hit ----
        public static void OnHit(AldaraMonsters.Mon m, float dmg)
        {
            if (busy > 0 || m.dead || m.dummy) return; inCombat = 4;
            float vf = V("vampfang"); if (vf > 0) Heal(Mathf.Round(H.maxHp * vf / 100 * 0.2f * Mathf.Min(1, dmg / Mathf.Max(1, H.atk))), "#ff8aa0");
            float st = V("stormidol");
            if (st > 0 && Random.value * 100 < st)
            {
                Pop("Storm Strike", "stormidol", "#8ab4ff"); busy++;
                try
                {
                    float d = Mathf.Round(H.atk * 2 * (0.9f + Random.value * 0.2f));
                    AldaraVfx.Zap(m.x + (Random.value - 0.5f) * 40, m.y - 320, m.x, m.y - m.r * 0.6f, Hx("#b8d8ff"), 0.3f, true);
                    AldaraVfx.Burst(m.x, m.y - m.r * 0.5f, Hx("#b8d8ff"), 14, 200); Ring(m.x, m.y, 40, "#8ab4ff", 0.3f); AldaraMonsters.I.HitMonster(m, d, Hx("#b8d8ff"));
                    var prev = m; var ns = Nearby(m.x, m.y, 140, m);
                    for (int i = 0; i < ns.Count && i < 2; i++) { var o = ns[i]; AldaraVfx.Zap(prev.x, prev.y - prev.r * 0.6f, o.x, o.y - o.r * 0.6f, Hx("#b8d8ff"), 0.25f, false); AldaraMonsters.I.HitMonster(o, Mathf.Round(d * 0.6f), Hx("#b8d8ff")); prev = o; }
                }
                finally { busy--; }
            }
            float ps = V("serpenteye");
            if (ps > 0 && !m.dead) { Poison p; if (!poison.TryGetValue(m, out p)) p = new Poison(); p.st = Mathf.Min(5, p.st + 1); p.t = 5; p.v = ps; poison[m] = p; }
        }
        // ---- on kill ----
        public static void OnKill(AldaraMonsters.Mon k)
        {
            if (busy > 1) return;
            float vf = V("vampfang"); if (vf > 0) { Heal(Mathf.Round(H.maxHp * vf / 100), "#ff8aa0"); AldaraVfx.Zap(k.x, k.y - k.r * 0.5f, P.x, P.y - 20, Hx("#ff4a6a"), 0.3f, true); }
            if (Has("hourglass")) { haste.Add(t + 5); if (haste.Count > 3) haste.RemoveAt(0); AldaraFx.Text(P.x - 10, P.y - 62, "Haste x" + haste.Count, Hx("#ffe08a")); }
            float gc = V("greedcoin");
            if (gc > 0 && Random.value * 100 < gc)
            {
                float g = Mathf.Round((k.gold > 0 ? k.gold : 5) * 25); H.gold += g; AldaraFx.Text(k.x, k.y - k.r - 60, "+" + g + " Gold hoard!", Hx("#ffd84a")); Pop("Gold Hoard", "greedcoin", "#ffd84a");
                for (int q = 0; q < 18; q++) AldaraVfx.Mpush(new AldaraVfx.Mp { x = k.x + (Random.value - 0.5f) * 20, y = k.y, z = 6, vx = (Random.value - 0.5f) * 160, vy = (Random.value - 0.5f) * 160, vz = 120 + Random.value * 160, g = 420, life = 1.4f, max = 1.4f, c = Hx(q % 2 == 1 ? "#ffd84a" : "#ffb02a"), s = 3, glow = true, rock = true, hasRot = true, vr = 6 });
            }
        }
        // ---- on damage taken: returns the damage that gets through ----
        public static float OnDamage(float dmg, float ang)
        {
            if (dmg <= 0 || !H.alive) return dmg; inCombat = 4;
            float mir = V("shadowmirror");
            if (mir > 0 && Random.value * 100 < mir)
            {
                AldaraFx.Text(P.x, P.y - 32, "Shadow", Hx("#c0a0ff")); Pop("Shadow Step", "shadowmirror", "#c0a0ff"); AldaraVfx.Burst(P.x, P.y - 10, Hx("#7a5ad0"), 16, 140);
                var ns = Nearby(P.x, P.y, 110); var m = ns.Count > 0 ? ns[0] : H.target;
                if (m != null && !m.dead) { var f = AldaraVfx.Effect("slash", m.x, m.y, 30, 0.22f, Hx("#c0a0ff")); f.a = Mathf.Atan2(m.y - P.y, m.x - P.x); Hit(m, Mathf.Round(H.atk * 1.5f), "#c0a0ff"); }
                return 0;
            }
            float th = V("thornheart"); if (th > 0) { float back = Mathf.Round(dmg * th / 100); if (back > 0) foreach (var m in Nearby(P.x, P.y, 70)) Hit(m, back, "#8ae070"); }
            float fr = V("frostheart");
            if (fr > 0 && Nearby(P.x, P.y, 150).Count > 0 && Cd("frostheart", 6))
            {
                Pop("Frost Nova", "frostheart", "#8ae0ff"); Ring(P.x, P.y, 150, "#8ae0ff", 0.5f); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "shock", x = P.x, y = P.y, r = 150, c = Hx("#8ae0ff"), c2 = Color.white, T = 0.5f }); AldaraVfx.Burst(P.x, P.y - 8, Hx("#cfefff"), 24, 220);
                foreach (var m in Nearby(P.x, P.y, 150)) { Hit(m, Mathf.Round(H.atk * fr / 100), "#8ae0ff"); if (!m.dead) { m.stunT = Mathf.Max(m.stunT, 2); m.slowT = Mathf.Max(m.slowT, 4); } }
            }
            return dmg;
        }
        /// after the hit lands: the ember saves you (true), or the army rises
        public static bool AfterDamage()
        {
            if (H.hp <= 0)
            {
                float pv = V("phoenix");
                if (pv > 0 && Cd("phoenix", pv))
                {
                    H.hp = Mathf.Round(H.maxHp * 0.5f); AldaraHud.Banner("The Phoenix Ember burns: you rise again!"); Pop("Phoenix Rebirth", "phoenix", "#ff9a3a");
                    AldaraVfx.Burst(P.x, P.y - 10, Hx("#ff9a3a"), 40, 260); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "shock", x = P.x, y = P.y, r = 220, c = Hx("#ff9a3a"), c2 = Hx("#ffe08a"), T = 0.6f }); Ring(P.x, P.y, 220, "#ff9a3a", 0.6f);
                    foreach (var m in Nearby(P.x, P.y, 220)) Hit(m, Mathf.Round(H.atk * 3), "#ff9a3a"); return true;
                }
            }
            float bc = V("bonecrown"); if (bc > 0 && H.hp > 0 && H.hp < H.maxHp * 0.3f && Cd("bonecrown", 60)) Summon((int)bc);
            return false;
        }
        /// casting a skill: the Seraph's barrier
        public static void OnCast()
        {
            float sv = V("seraphtear"); if (!(sv > 0) || !Cd("seraphtear", 10)) return;
            H.shield = Mathf.Max(H.shield, Mathf.Round(H.maxHp * sv / 100)); H.shieldT = Mathf.Max(H.shieldT, 8); H.shieldName = "Seraph Barrier"; Pop("Seraph Barrier", "seraphtear", "#fff0b0");
            AldaraFx.Text(P.x + 14, P.y - 60, "Seraph's barrier", Hx("#fff0b0")); AldaraVfx.Burst(P.x, P.y - 20, Hx("#fff0b0"), 12, 120);
        }
        // ---- the skeleton army ----
        public static void Summon(int n)
        {
            AldaraHud.Banner("The Bone King answers: " + n + " skeletons rise!"); Pop("Skeleton Army", "bonecrown", "#e8e4d0"); AldaraVfx.Shake(4, 0.4f);
            for (int k = 0; k < n; k++)
            {
                float a = k / (float)n * Mathf.PI * 2 + Random.value * 0.5f, d = 44 + Random.value * 30, x = P.x + Mathf.Cos(a) * d, y = P.y + Mathf.Sin(a) * d;
                AddSkel(x, y, 18, false);
                AldaraVfx.Burst(x, y, Hx("#e8e4d0"), 12, 120); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "dust", x = x, y = y, r = 24, T = 0.6f });
                for (int q = 0; q < 5; q++) AldaraVfx.Mpush(new AldaraVfx.Mp { x = x + (Random.value - 0.5f) * 20, y = y, z = 2, vx = (Random.value - 0.5f) * 60, vy = (Random.value - 0.5f) * 60, vz = 60 + Random.value * 80, g = 300, life = 0.9f, max = 0.9f, c = Hx("#8a7a5a"), s = 2.5f, rock = true, hasRot = true, vr = 5 });
            }
        }
        public static Skel AddSkel(float x, float y, float life, bool necro) { var s = new Skel { x = x, y = y, life = life, cd = 0.6f + Random.value * 0.6f, necro = necro, lx = x, ly = y }; skel.Add(s); return s; }
        static AldaraMonsters.Mon SkelTarget(Skel s)
        {
            AldaraMonsters.Mon best = null; float bd = 380;
            foreach (var m in AldaraMonsters.I.all) { if (m.dead || !AldaraDungeon.Reachable(m)) continue; float d = Mathf.Sqrt((m.x - s.x) * (m.x - s.x) + (m.y - s.y) * (m.y - s.y)); if (!(m.aggroT > 0 || d < 260)) continue; if (d < bd) { bd = d; best = m; } }
            return best;
        }
        public static void ClearSkel() { foreach (var s in skel) if (s.view) Destroy(s.view); skel.Clear(); poison.Clear(); }

        void Update()
        {
            if (!AldaraSave.Ready || !H || !P) return; float dt = Mathf.Min(Time.deltaTime, 0.1f);
            t += dt; if (inCombat > 0) inCombat -= dt;
            while (haste.Count > 0 && haste[0] < t) haste.RemoveAt(0);
            if (!H.alive) { UpdateViews(dt); return; }
            float wl = V("wisplantern");
            if (wl > 0)
            {
                if (H.hp < H.maxHp) H.hp = Mathf.Min(H.maxHp, H.hp + H.maxHp * wl / 100 * dt);
                if (wispCd > 0) wispCd -= dt;
                if (H.hp < H.maxHp * 0.25f && wispCd <= 0) { wispCd = 12; Pop("Wisp Mend", "wisplantern", "#a0ffd0"); Heal(Mathf.Round(H.maxHp * wl * 4 / 100), "#a0ffd0"); Ring(P.x, P.y - 20, 50, "#a0ffd0", 0.5f); AldaraVfx.Burst(P.x, P.y - 20, Hx("#a0ffd0"), 16, 120); }
            }
            float tb = V("titanbone");
            if (tb > 0)
            {
                quakeT += dt;
                if (quakeT >= 8 && inCombat > 0 && Nearby(P.x, P.y, 130).Count > 0)
                {
                    quakeT = 0; Pop("Titan Quake", "titanbone", "#e0c080");
                    AldaraVfx.Mfx(new AldaraVfx.Mf { k = "crack", x = P.x, y = P.y, a = Random.value * 6, len = 120, c = Hx("#d0b070"), T = 2.2f, n = 7, r = 120 }); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "shock", x = P.x, y = P.y, r = 130, c = Hx("#d0b070"), c2 = Color.white, T = 0.45f }); AldaraVfx.Shake(6, 0.4f);
                    foreach (var m in Nearby(P.x, P.y, 130)) { Hit(m, Mathf.Round(H.atk * tb / 100), "#e0c080"); if (!m.dead) { float a = Mathf.Atan2(m.y - P.y, m.x - P.x); m.x += Mathf.Cos(a) * 40; m.y += Mathf.Sin(a) * 40; m.stunT = Mathf.Max(m.stunT, 0.6f); } }
                }
            }
            // poison ticks
            if (poison.Count > 0)
            {
                foreach (var kv in new List<KeyValuePair<AldaraMonsters.Mon, Poison>>(poison))
                {
                    var m = kv.Key; var p = kv.Value; if (m.dead) { poison.Remove(m); continue; }
                    p.t -= dt; p.acc += dt;
                    if (p.acc >= 1) { p.acc -= 1; Hit(m, Mathf.Max(1, Mathf.Round(H.atk * p.v / 100 * p.st * 0.5f)), "#70d060"); AldaraVfx.Burst(m.x, m.y - m.r * 0.5f, Hx("#70d060"), 3, 60); }
                    if (p.t <= 0) poison.Remove(m);
                }
            }
            // skeletons
            for (int i = skel.Count - 1; i >= 0; i--)
            {
                var s = skel[i]; s.life -= dt; if (s.rise > 0) s.rise -= dt;
                if (s.life <= 0) { AldaraVfx.Burst(s.x, s.y, Hx("#e8e4d0"), 10, 100); if (s.view) Destroy(s.view); skel.RemoveAt(i); continue; }
                if (s.lunge > 0) s.lunge -= dt; s.cd -= dt; if (s.rise > 0) continue;
                if (s.target == null || s.target.dead || Mathf.Sqrt((s.target.x - P.x) * (s.target.x - P.x) + (s.target.y - P.y) * (s.target.y - P.y)) > 600) s.target = SkelTarget(s);
                var tg = s.target;
                if (tg != null)
                {
                    float d = Mathf.Sqrt((tg.x - s.x) * (tg.x - s.x) + (tg.y - s.y) * (tg.y - s.y)), reach = tg.r + 14;
                    if (d > reach) { float a = Mathf.Atan2(tg.y - s.y, tg.x - s.x), sp = 250 * dt; s.x += Mathf.Cos(a) * sp; s.y += Mathf.Sin(a) * sp; }
                    else if (s.cd <= 0) { s.cd = 0.9f; s.lunge = 0.2f; s.lungeA = Mathf.Atan2(tg.y - s.y, tg.x - s.x); Hit(tg, Mathf.Max(1, Mathf.Round(H.atk * 0.45f * (0.8f + Random.value * 0.4f))), "#e8e4d0"); AldaraVfx.Burst(tg.x, tg.y, Hx("#e8e4d0"), 4, 90); }
                }
                else { float d = Mathf.Sqrt((P.x - s.x) * (P.x - s.x) + (P.y - s.y) * (P.y - s.y)); if (d > 90) { float a = Mathf.Atan2(P.y - s.y, P.x - s.x), sp = 230 * dt; s.x += Mathf.Cos(a) * sp; s.y += Mathf.Sin(a) * sp; } }
                foreach (var o in skel) { if (o == s) continue; float dx = s.x - o.x, dy = s.y - o.y, d = Mathf.Sqrt(dx * dx + dy * dy); if (d < 22 && d > 0.01f) { s.x += dx / d * (22 - d) * 0.5f; s.y += dy / d * (22 - d) * 0.5f; } }
                if (AldaraDungeon.Active) AldaraDungeon.Push(ref s.x, ref s.y, 8, 0, 0, false);
            }
            UpdateViews(dt);
        }
        void UpdateViews(float dt)
        {
            foreach (var s in skel)
            {
                if (!s.view)
                {
                    var pf = AldaraMonsters.PrefabOf("Skeleton Lord"); if (!pf) continue;
                    s.view = new GameObject("Skeleton"); s.view.transform.SetParent(transform, false); s.view.transform.localScale = AldaraView.Squash;
                    var inst = Instantiate(pf, s.view.transform, false); s.model = inst.transform; s.anim = inst.GetComponent<AldaraMonsterAnimator>();
                }
                float mdx = s.x - s.lx, mdy = s.y - s.ly; bool moving = mdx * mdx + mdy * mdy > 0.02f * dt * 60; s.lx = s.x; s.ly = s.y;
                if (s.lunge > 0) s.face = s.lungeA; else if (s.target != null && !s.target.dead) s.face = Mathf.Atan2(s.target.y - s.y, s.target.x - s.x); else if (moving) s.face = Mathf.Atan2(mdy, mdx);
                float lk = s.lunge > 0 ? Mathf.Sin(Mathf.PI * (1 - s.lunge / 0.2f)) * 10 : 0, x = s.x + Mathf.Cos(s.lungeA) * lk, y = s.y + Mathf.Sin(s.lungeA) * lk;
                var pos = AldaraWorld.ToUnity(x, y + 15 * s.r / 16); if (s.rise > 0) pos.y -= (s.rise / 0.7f) * 22 / AldaraWorld.PX;
                s.view.transform.position = pos; if (s.model) s.model.localRotation = Quaternion.Euler(0, 90 + s.face * Mathf.Rad2Deg, 0);
                if (s.anim) { s.anim.ClearAction(); if (moving && s.anim.Has("walk")) s.anim.SetBase("walk", Mathf.Sqrt(mdx * mdx + mdy * mdy) / Mathf.Max(dt, 1e-4f) * 0.11f / (Mathf.PI * 2)); else s.anim.SetBase("idle", 0); }
            }
        }
        void DrawGround(AldaraVfx V)
        {
            foreach (var s in skel) AldaraVfx.Ring(V.GroundN, s.x, s.y + s.r * 0.95f, s.r * 1.1f, s.r * 0.36f, 1.5f, Hx("#a8f0d0", 0.55f), 1);
            foreach (var kv in poison) { var m = kv.Key; if (m.dead || !m.view || !m.view.activeSelf) continue; AldaraVfx.Ring(V.GroundN, m.x, m.y + m.r * 0.95f, m.r, m.r * 0.32f, 2, Hx("#70d060", 0.7f), 1); }
        }
        static Color Hx(string h, float a) { var c = AldaraRules.Hex(h); c.a = a; return c; }
    }
}
