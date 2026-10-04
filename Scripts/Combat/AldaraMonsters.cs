using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // World monsters, simulated in the browser's pixel space with the browser's rules (camps from makeCamps / spawnCamp,
    // bosses in their lairs, monStats, aggro ranges, monCombat / startAct / monAct / doHit / hitTest, projectiles, lobs,
    // ground hazards and shockwaves, respawn timers). Models and animations are the browser's own, baked per monster.
    public partial class AldaraMonsters : MonoBehaviour
    {
        public static AldaraMonsters I;
        public float viewRangePx = 1500;     // models exist only near the hero
        public readonly List<Mon> all = new List<Mon>();
        MonsterBook book;
        readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        public class Act { public MoveDef mv; public float t, a, tx, ty, sx, sy, hs, tk, dd, sp; public int hi; public bool st, hit; }
        public class Mon
        {
            public MonDef def; public string name; public bool boss; public float r, x, y, homeX, homeY, hp, maxHp, atk, xp, gold; public int lvl, tier, camp = -1;
            public float wanderT, wx, wy, atkCd, respawnT, flash, hurtT, engT, enrT, aggroT, slowT, stunT, strT, walkT, lift, face = Mathf.PI / 2, faceA; public int str;
            public bool dead, hasFaceA; public Act act; public float[] mcd;
            public GameObject view; public AldaraMonsterAnimator anim; public Transform model;
            public float lx, ly, dotT, dotTick, dotDmg, navIgnore, chest;
            // dungeons: its section, its own speed, the boss's ability timers, line-of-sight caches
            public bool dun, enraged, summoned, slowHit, los, sees; public int room; public float spd = -1, mt, mt2, losT, seeT, spT, sp, spin; public string mech, color, bolt;
            // raids: its kind, its tag in an encounter, shielded (immune) or exposed (+30% damage)
            public string rk, rTag; public bool rImm, rVuln; public float immT; public bool lvs;
            /// a point on the ground standing in for a target (aimed skills with nothing targeted)
            public bool dummy;
            /// subclass state: Death Mark time, an Ambush already spent on it
            public float markT; public bool amb;
            /// a Kingsroad ambusher ('bandit' or 'warg'): it never comes back on its own
            public string ambush;
            public Renderer[] rends; public bool flashOn;
            /// the boss's sculpted model with its clips, when it has one
            public AldaraBossSculpt sculpt;
        }

        void Awake()
        {
            I = this; AldaraWorld.Load(); book = MonsterBook.Load(); FxHooks(true);
            SpawnAll();
        }

        // ---- spawning, as in the browser ----
        public static MonStat MonStats(MonDef t, int lvl)
        {
            const float MON_HP_MULT = 1.8f; int up = lvl - t.lv0; float hpS = Mathf.Pow(1.42f, t.tier - 1), atkS = Mathf.Pow(1.24f, t.tier - 1);
            return new MonStat { hp = Mathf.Round(t.hpBase * hpS * (1 + up * 0.16f) * MON_HP_MULT), atk = Mathf.Round(t.atk * atkS * (1 + up * 0.10f)), xp = Mathf.Round(t.xp * (1 + up * 0.10f)), gold = Mathf.Round(t.gold * (1 + up * 0.08f)) };
        }
        public struct MonStat { public float hp, atk, xp, gold; }
        void SpawnAll()
        {
            for (int ci = 0; ci < book.camps.Length; ci++)
            {
                var camp = book.camps[ci]; var t = book.Get(camp.type); if (t == null) continue;
                var R = new AldaraRng(ci * 977 + 5);
                int n = t.name == "Bat Swarm" ? 5 : t.tier == 11 ? 1 + Mathf.FloorToInt(R.Next() * 2) : 4 + Mathf.FloorToInt(R.Next() * 3);
                int clvl = t.lv0 + Mathf.FloorToInt(R.Next() * (t.lv1 - t.lv0));
                for (int k = 0; k < n; k++)
                {
                    float a = k / (float)n * Mathf.PI * 2 + R.Next() * 0.6f, d = 50 + R.Next() * 110;
                    float x = camp.x + Mathf.Cos(a) * d, y = camp.y + Mathf.Sin(a) * d;
                    if (AldaraWorld.BlockedAt(x, y)) { x = camp.x + Mathf.Cos(a) * 25; y = camp.y + Mathf.Sin(a) * 25; }
                    int lvl = Mathf.Min(t.lv1, clvl + (R.Next() < 0.35f ? 1 : 0)); var st = MonStats(t, lvl);
                    all.Add(new Mon { def = t, name = t.name, r = t.r, tier = t.tier, x = x, y = y, homeX = x, homeY = y, lvl = lvl, hp = st.hp, maxHp = st.hp, atk = st.atk, xp = st.xp, gold = st.gold, camp = ci, wanderT = R.Next() * 3 });
                }
            }
            for (int i = 0; i < book.lairs.Length; i++)
            {
                var L = book.lairs[i]; var b = book.Get(L.name); if (b == null) continue;
                float bhp = Mathf.Round(b.hpBase * Mathf.Pow(1.32f, b.tier - 1) * 1.6f), batk = Mathf.Round(b.atk * Mathf.Pow(1.16f, b.tier - 1));
                all.Add(new Mon { def = b, name = b.name, r = b.r, tier = b.tier, boss = true, lvl = b.lv0, x = L.x, y = L.y, homeX = L.x, homeY = L.y, hp = bhp, maxHp = bhp, atk = batk, xp = b.xp, gold = b.gold });
            }
        }

        // ---- level scaling (lvsNorm): every monster is capped to what a player of its level can handle ----
        Dictionary<int, float> tierHp, tierAtk;
        public static float LvsAtk(float L) { return 14 + 5.5f * (Mathf.Max(1, L) - 1); }
        public static float LvsHp(float L) { return 100 + 33 * (Mathf.Max(1, L) - 1); }
        void LvsNorm(Mon m)
        {
            m.lvs = true; if (!(m.lvl > 0) || !(m.maxHp > 0)) return;
            if (tierHp == null)
            {
                tierHp = new Dictionary<int, float>(); tierAtk = new Dictionary<int, float>(); var cnt = new Dictionary<int, int>();
                foreach (var d in book.monsters) { if (d.dun != 0 || d.boss != 0) continue; float a; tierHp.TryGetValue(d.tier, out a); tierHp[d.tier] = a + d.hpBase; tierAtk.TryGetValue(d.tier, out a); tierAtk[d.tier] = a + d.atk; int c; cnt.TryGetValue(d.tier, out c); cnt[d.tier] = c + 1; }
                foreach (var k in cnt.Keys) { tierHp[k] /= cnt[k]; tierAtk[k] /= cnt[k]; }
            }
            int L = m.lvl; var t = book.Get(m.name); if (t != null && (t.dun != 0 || t.boss != 0)) t = null;
            float fH = t != null ? Mathf.Clamp(t.hpBase / (tierHp.ContainsKey(t.tier) ? tierHp[t.tier] : t.hpBase), 0.6f, 1.8f) : 1, fA = t != null ? Mathf.Clamp(t.atk / (tierAtk.ContainsKey(t.tier) ? tierAtk[t.tier] : t.atk), 0.7f, 1.5f) : 1;
            float hits, die;
            if (m.boss && m.dun) { hits = 45 + L * 0.2f; die = 4; }
            else if (m.boss) { hits = 25 + L * 0.15f; die = 5; }
            else if (t != null && t.tier >= 11) { hits = (3.2f + L * 0.028f) * 3; die = 6; }
            else if (m.dun) { hits = (3.2f + L * 0.028f) * 1.4f; die = 9; }
            else { hits = 3.2f + L * 0.028f; die = Mathf.Max(8, 12 - L * 0.04f); }
            float hpT = Mathf.Round(LvsAtk(L) * hits * fH), atkT = Mathf.Max(1, Mathf.Round(LvsHp(L) / die * fA));
            if (m.maxHp > hpT) { float frac = m.hp / m.maxHp; m.maxHp = hpT; m.hp = Mathf.Max(1, Mathf.Round(hpT * frac)); }
            if (m.atk > atkT) m.atk = atkT;
        }
        float MoveSpeed(Mon m) { return book.baseSpeed * (m.spd > 0 ? m.spd : m.def.spd > 0 ? m.def.spd : 0.55f) * (m.boss ? book.bossSpeedMult : book.speedMult); }
        float MvMax(Mon m, MoveDef mv)
        {
            if (mv.max >= 0) return mv.max + (m.boss ? m.r * 0.5f : 0);
            if (mv.reach >= 0 && mv.reach > 0) return m.r + mv.reach + 10;
            if (mv.rad >= 0) return Mathf.Max(0, mv.fwd) + mv.rad * 0.85f;
            return m.r + 40;
        }
        static float Dmg(Mon m, MoveDef mv, float mult = 1) { return Mathf.Max(1, Mathf.Round(m.atk * (mv.dmg > 0 ? mv.dmg : 1) * mult * (0.85f + Random.value * 0.3f) * (m.enrT > 0 ? 1.2f : 1))); }

        // ---- the per-frame tick (update() in the browser) ----
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f); var H = AldaraHero.I; var P = AldaraPlayer.I; if (!P || !H || !AldaraSave.Ready) return;
            float px = P.x, py = P.y;
            TrackPlayerVelocity(dt, px, py);
            UpdateEnemyFx(dt); UpdateCorpses(dt);
            int busy = 0; foreach (var o in all) if (!o.dead && o.act != null && o.act.mv.cd > 0 && !o.boss) busy++;
            foreach (var m in all)
            {
                if (!m.lvs) LvsNorm(m);
                if (m.dead)
                {
                    m.respawnT -= dt;
                    if (m.respawnT <= 0) { m.dead = false; m.hp = m.maxHp; m.x = m.homeX; m.y = m.homeY; m.act = null; m.lift = 0; m.enrT = 0; }
                    continue;
                }
                bool near = Mathf.Abs(m.x - px) < viewRangePx && Mathf.Abs(m.y - py) < viewRangePx * 0.8f;
                if (!m.dun && !(m.aggroT > 0) && !near) { if (m.view) Hide(m); continue; }
                if (m.flash > 0) m.flash -= dt; if (m.hurtT > 0) m.hurtT -= dt; if (m.engT > 0) m.engT -= dt; if (m.enrT > 0) m.enrT -= dt;
                if (m.aggroT > 0) m.aggroT -= dt; if (m.slowT > 0) m.slowT -= dt;
                if (m.dotT > 0) { m.dotT -= dt; m.dotTick -= dt; if (m.dotTick <= 0) { m.dotTick = 1; HitMonster(m, m.dotDmg, AldaraRules.Hex("#9aff6a")); if (m.dead) continue; } }
                if (m.mcd == null) m.mcd = new float[16];
                float mox = m.x, moy = m.y;
                if (m.stunT > 0) { m.stunT -= dt; m.act = null; m.lift = 0; }
                else
                {
                    float sf = m.slowT > 0 ? 0.5f : 1, d = Mathf.Sqrt((px - m.x) * (px - m.x) + (py - m.y) * (py - m.y));
                    float aggro = m.aggroT > 0 ? 650 : m.dun ? 420 : m.boss ? 320 : book.aggro[Mathf.Clamp(m.tier - 1, 0, book.aggro.Length - 1)];
                    if (H.alive && AldaraDungeon.SameRoom(m) && d < aggro && (!m.dun || AldaraDungeon.Aggro(m, aggro))) Combat(m, dt * sf, d, Mathf.Atan2(py - m.y, px - m.x), ref busy, !m.dun || AldaraDungeon.Sees(m));
                    else if (m.act != null) DoAct(m, dt * sf);
                    else
                    {
                        m.wanderT -= dt;
                        if (m.wanderT <= 0) { m.wanderT = 1.5f + Random.value * 2; float a = Random.value * Mathf.PI * 2; m.wx = Mathf.Cos(a); m.wy = Mathf.Sin(a); }
                        float ws = m.boss ? 18 : 28; m.x += m.wx * ws * dt; m.y += m.wy * ws * dt;
                        m.x = Mathf.Clamp(m.x, m.homeX - 120, m.homeX + 120); m.y = Mathf.Clamp(m.y, m.homeY - 120, m.homeY + 120);
                        if (m.dun && AldaraDungeon.Active) AldaraDungeon.PushMon(m, mox, moy);
                    }
                    if (!m.dun) { SlideMove(m, mox, moy); if (AldaraWorld.LiquidAt(m.x, m.y) != 0 && AldaraWorld.LiquidAt(mox, moy) == 0) { m.x = mox; m.y = moy; } }
                }
                if (near) Show(m, m.x - mox, m.y - moy, dt); else if (m.view) Hide(m);
            }
        }
        static void SlideMove(Mon o, float ox, float oy)
        {
            if (!AldaraWorld.BlockedAt(o.x, o.y) || AldaraWorld.BlockedAt(ox, oy)) return;
            float nx = o.x, ny = o.y;
            if (!AldaraWorld.BlockedAt(nx, oy)) o.y = oy; else if (!AldaraWorld.BlockedAt(ox, ny)) o.x = ox; else { o.x = ox; o.y = oy; }
        }

        // ---- monCombat / pickMove / startAct / monAct ----
        void Combat(Mon m, float dt, float d, float ang, ref int busy, bool see)
        {
            m.engT = 0.35f; m.faceA = ang; m.hasFaceA = true;
            for (int k = 0; k < m.mcd.Length; k++) if (m.mcd[k] > 0) m.mcd[k] -= dt;
            if (m.act != null) { DoAct(m, dt); return; }
            m.atkCd -= dt * (m.enrT > 0 ? 1.6f : 1);
            if (m.atkCd <= 0 && see && AldaraHero.I.alive)
            {
                var mv = PickMove(m, d, busy >= 2 && !m.boss);
                if (mv != null) { StartAct(m, mv, ang); if (mv.cd > 0 && !m.boss) busy++; return; }
            }
            bool kite = m.def.kite != 0; float want = kite ? 210 : m.r + 24, ms = MoveSpeed(m);
            if (d > want || !see) { if (m.dun && AldaraDungeon.Active) AldaraDungeon.MonStep(m, ms * dt); else { m.x += Mathf.Cos(ang) * ms * dt; m.y += Mathf.Sin(ang) * ms * dt; } }
            else if (kite && d < 120) Step(m, ang + Mathf.PI, ms * 0.75f * dt);
            else if (!m.boss)
            {
                m.strT -= dt; if (m.strT <= 0) { m.strT = 0.7f + Random.value * 1.3f; m.str = Random.value < 0.4f ? 0 : Random.value < 0.5f ? -1 : 1; }
                if (m.str != 0) Step(m, ang + Mathf.PI / 2 * m.str, ms * 0.32f * dt);
            }
        }
        MoveDef PickMove(Mon m, float d, bool busy)
        {
            float tot = 0; var c = new List<KeyValuePair<MoveDef, float>>();
            foreach (var mv in m.def.moves)
            {
                if (m.mcd[mv.i] > 0) continue; if (busy && mv.cd > 0) continue;
                if (d < Mathf.Max(0, mv.min) || d > MvMax(m, mv)) continue;
                float w = mv.w * (mv.cd > 0 ? 1.25f : 1); c.Add(new KeyValuePair<MoveDef, float>(mv, w)); tot += w;
            }
            if (c.Count == 0) return null; float r = Random.value * tot;
            foreach (var kv in c) { r -= kv.Value; if (r <= 0) return kv.Key; }
            return c[c.Count - 1].Key;
        }
        void StartAct(Mon m, MoveDef mv, float ang)
        {
            var P = AldaraPlayer.I;
            m.act = new Act { mv = mv, a = ang, tx = P.x, ty = P.y, sx = m.x, sy = m.y };
            m.mcd[mv.i] = mv.cd;
            if (mv.cast > 0) { var E = Elc(mv.el); for (int k = 0; k < 6; k++) AldaraVfx.Mpush(new AldaraVfx.Mp { x = m.x + (Random.value - 0.5f) * m.r * 3, y = m.y + (Random.value - 0.5f) * m.r * 2, z = Random.value * 40, vz = 30, life = 0.5f, max = 0.5f, c = E[0], s = 2.5f, glow = true }); }
        }
        void LockTarget(Mon m, Act A, MoveDef mv)
        {
            var P = AldaraPlayer.I; float mx = MvMax(m, mv), tx = P.x + pvx * 0.18f, ty = P.y + pvy * 0.18f, dd = Mathf.Sqrt((tx - m.x) * (tx - m.x) + (ty - m.y) * (ty - m.y));
            if (dd > mx) { tx = m.x + (tx - m.x) / dd * mx; ty = m.y + (ty - m.y) / dd * mx; }
            if (mv.move == "leap") for (int k = 0; k < 6 && !SpotOk(m, tx, ty); k++) { tx = m.x + (tx - m.x) * 0.8f; ty = m.y + (ty - m.y) * 0.8f; }
            A.tx = tx; A.ty = ty;
        }
        static bool SpotOk(Mon m, float x, float y) { if (m.dun && AldaraDungeon.Active) return AldaraDungeon.SpotOk(m, x, y); return !AldaraWorld.BlockedAt(x, y) && AldaraWorld.LiquidAt(x, y) == 0; }
        static void Step(Mon m, float a, float d) { float ox = m.x, oy = m.y; m.x += Mathf.Cos(a) * d; m.y += Mathf.Sin(a) * d; if (m.dun && AldaraDungeon.Active) AldaraDungeon.PushMon(m, ox, oy); }
        static float EIO(float x) { return x < 0.5f ? 2 * x * x : 1 - Mathf.Pow(-2 * x + 2, 2) / 2; }
        void DoAct(Mon m, float dt)
        {
            var A = m.act; if (A == null) return; var mv = A.mv; var P = AldaraPlayer.I;
            if (A.hs > 0) { A.hs -= dt; return; }
            A.t += dt; float W = mv.wind, S = mv.act, R = mv.rec, t = A.t, px = P.x, py = P.y;
            float lockT = W * (mv.@lock >= 0 ? mv.@lock : 0.7f);
            if (t < lockT || (mv.chan > 0 && t >= W && t < W + S))
            {
                float want = Mathf.Atan2(py - m.y, px - m.x), tr = (t < lockT ? 7 : (mv.turn > 0 ? mv.turn : 1)) * dt;
                A.a += Mathf.Clamp(AldaraRules.AngD(want, A.a), -tr, tr); if (t < lockT) LockTarget(m, A, mv);
            }
            if (t >= W && t < W + S)
            {
                float f = (t - W) / S;
                if (!A.st) { A.st = true; ActBegin(m, A); }
                switch (mv.move)
                {
                    case "step": Step(m, A.a, mv.dist * mv.hits.Length / S * dt); break;
                    case "drift": Step(m, Mathf.Atan2(py - m.y, px - m.x), mv.dist / S * dt); break;
                    case "dash":
                        {
                            float ox = m.x, oy = m.y, st = mv.spd * dt; Step(m, A.a, st); if (!m.dun) SlideMove(m, ox, oy);
                            if (!m.dun && AldaraWorld.LiquidAt(m.x, m.y) != 0 && AldaraWorld.LiquidAt(ox, oy) == 0) { m.x = ox; m.y = oy; }
                            if (f > 0.08f && Mathf.Sqrt((m.x - ox) * (m.x - ox) + (m.y - oy) * (m.y - oy)) < st * 0.3f) { A.t = W + S; Mfx(new AldaraVfx.Mf { k = "dust", x = m.x + Mathf.Cos(A.a) * m.r, y = m.y + Mathf.Sin(A.a) * m.r, r = m.r * 1.2f, T = 0.6f }); Shake(3, 0.2f); }
                            A.dd -= dt; if (A.dd <= 0) { A.dd = 0.05f; Mfx(new AldaraVfx.Mf { k = "dust", x = m.x - Mathf.Cos(A.a) * m.r * 0.6f, y = m.y + m.r * 0.8f, r = m.r * 0.6f, T = 0.5f }); Mfx(new AldaraVfx.Mf { k = "speed", x = m.x, y = m.y, a = A.a, r = m.r, T = 0.2f, c = Elc(mv.el)[1] }); }
                            if (!A.hit && AldaraHero.I.alive && Mathf.Sqrt((px - m.x) * (px - m.x) + (py - m.y) * (py - m.y)) < m.r + 16 * 0.8f + 4) { A.hit = true; HurtPlayer(m, mv, Mathf.Atan2(py - m.y, px - m.x)); }
                            break;
                        }
                    case "leap": { float e = EIO(f), lox = m.x, loy = m.y; m.x = A.sx + (A.tx - A.sx) * e; m.y = A.sy + (A.ty - A.sy) * e; if (m.dun && AldaraDungeon.Active) AldaraDungeon.PushMon(m, lox, loy); m.lift = Mathf.Sin(f * Mathf.PI) * (mv.lift > 0 ? mv.lift : 60) * (m.boss ? 1.3f : 1); break; }
                }
            }
            else if (mv.move == "leap") m.lift = 0;
            while (A.hi < mv.hits.Length && t >= W + mv.hits[A.hi] * S) { DoHit(m, A, A.hi); A.hi++; }
            if (mv.chan > 0 && t >= W && t < W + S) { A.tk -= dt; if (A.tk <= 0) { A.tk = mv.tick; if (HitTest(m, mv, A.a)) HurtPlayer(m, mv, A.a); } ChanFx(m, A, dt); }
            if (mv.back > 0 && t >= W + S && t < W + S + 0.25f) Step(m, A.a + Mathf.PI, 240 * dt);
            if (t >= W + S + R) { m.act = null; m.lift = 0; m.atkCd = (mv.gap >= 0 ? mv.gap : (0.18f + Random.value * 0.5f)) * (m.boss ? 0.7f : 1); }
        }
        void ActBegin(Mon m, Act A)
        {
            var mv = A.mv; var P = AldaraPlayer.I;
            if (mv.move == "blink" || mv.move == "away")
            {
                float bse = mv.move == "blink" ? Mathf.Atan2(P.y - m.y, P.x - m.x) : Mathf.Atan2(m.y - P.y, m.x - P.x) + (Random.value - 0.5f) * 1.6f;
                foreach (var off in new[] { 0f, 0.7f, -0.7f, 1.4f, -1.4f })
                {
                    float a = bse + off, d = mv.move == "blink" ? 16 + m.r + 8 : 180;
                    float x = (mv.move == "blink" ? P.x : m.x) + Mathf.Cos(a) * d, y = (mv.move == "blink" ? P.y : m.y) + Mathf.Sin(a) * d;
                    if (SpotOk(m, x, y))
                    {
                        var E = Elc(mv.el); float ox = m.x, oy = m.y; m.x = x; m.y = y;
                        Mfx(new AldaraVfx.Mf { k = "blink", x = ox, y = oy, h = Chest(m), c = E[0], c2 = E[1], T = 0.4f }); Mfx(new AldaraVfx.Mf { k = "blink", x = m.x, y = m.y, h = Chest(m), c = E[0], c2 = E[1], T = 0.45f });
                        AldaraVfx.Burst(ox, oy, E[0], 10, 160); AldaraVfx.Burst(m.x, m.y, E[0], 10, 160); A.a = Mathf.Atan2(P.y - m.y, P.x - m.x); break;
                    }
                }
            }
            if (mv.shape == "dash") Shake(2, 0.15f);
        }
        bool HitTest(Mon m, MoveDef mv, float a)
        {
            var P = AldaraPlayer.I; float pr = 16 * 0.6f, dx = P.x - m.x, dy = P.y - m.y, d = Mathf.Sqrt(dx * dx + dy * dy), c = Mathf.Cos(a), s = Mathf.Sin(a), al = dx * c + dy * s, ac = -dx * s + dy * c;
            switch (mv.shape)
            {
                case "arc": return d < m.r + mv.reach + pr && (d < m.r + pr || Mathf.Abs(AldaraRules.AngD(Mathf.Atan2(dy, dx), a)) < mv.arc);
                case "circle": { float fwd = Mathf.Max(0, mv.fwd), cx = m.x + c * fwd, cy = m.y + s * fwd, R = mv.rad >= 0 ? mv.rad : m.r + mv.reach; return Mathf.Sqrt((P.x - cx) * (P.x - cx) + (P.y - cy) * (P.y - cy)) < R + pr; }
                case "line": return al > -pr && al < m.r + mv.reach + pr && Mathf.Abs(ac) < (mv.wid > 0 ? mv.wid : 20) + pr;
                case "cone": return d < mv.len + pr && al > 0 && Mathf.Abs(AldaraRules.AngD(Mathf.Atan2(dy, dx), a)) < mv.arc;
                case "beam": return al > 0 && al < BeamLen(m, a, mv.len) + pr && Mathf.Abs(ac) < mv.wid + pr;
            }
            return false;
        }
        static void Knock(float a, float dist)
        {
            var P = AldaraPlayer.I; if (AldaraDungeon.Active) { AldaraDungeon.Shove(a, dist); return; }
            for (int k = 0; k < 6; k++) { float ox = P.x, oy = P.y; P.x += Mathf.Cos(a) * dist / 6; P.y += Mathf.Sin(a) * dist / 6; if (AldaraWorld.BlockedAt(P.x, P.y)) { P.x = ox; P.y = oy; break; } }
        }

        float pvx, pvy, plx = float.NaN, ply;
        void TrackPlayerVelocity(float dt, float x, float y)
        {
            if (!float.IsNaN(plx) && dt > 0) { float vx = (x - plx) / dt, vy = (y - ply) / dt; bool ok = Mathf.Abs(vx) < 900 && Mathf.Abs(vy) < 900; pvx = ok ? pvx * 0.7f + vx * 0.3f : 0; pvy = ok ? pvy * 0.7f + vy * 0.3f : 0; }
            plx = x; ply = y;
        }
        float AimLead(Mon m, float spd) { var P = AldaraPlayer.I; float dx = P.x - m.x, dy = P.y - m.y, tt = Mathf.Sqrt(dx * dx + dy * dy) / spd * 0.5f; return Mathf.Atan2(dy + pvy * tt, dx + pvx * tt); }
        // ---- the hero hits a monster (hitMonster / monHurt / killMonster) ----
        public void HitMonster(Mon m, float dmg, Color col)
        {
            if (m.dummy || m.dead) return; if (AldaraRaid.Immune(m)) return; if (AldaraRaid.On && m.rVuln) dmg = Mathf.Round(dmg * 1.3f);
            var H = AldaraHero.I; int g = m.lvl - H.lvl; bool mine = HitSrc == "player"; AldaraPlayer.lastCombat = Time.time; string src = HitSrc;
            if (mine && AldaraAuto.on && dmg > 0) dmg = Mathf.Max(1, Mathf.Round(dmg * AldaraAuto.DMG));   // auto combat deals 25% less
            dmg = AldaraSubclass.PreHit(m, dmg, src); float hp0 = m.hp;
            HitCore(m, dmg, col, g, mine);
            AldaraSubclass.PostHit(m, Mathf.Max(0, hp0 - Mathf.Max(0, m.hp)), src);
        }
        void HitCore(Mon m, float dmg, Color col, int g, bool mine)
        {
            var H = AldaraHero.I;
            if (g > 0 && Random.value < AldaraRules.LvMiss(g)) { m.aggroT = 6; AldaraFx.Text(m.x, m.y - m.r - 10, "Glance", new Color(0.6f, 0.6f, 0.6f)); dmg = Mathf.Max(1, Mathf.Round(dmg * 0.15f)); }
            dmg = Mathf.Max(1, Mathf.Round(dmg * AldaraRules.LvOut(g)));
            if (mine && AldaraRelics.busy == 0) dmg = Mathf.Max(1, Mathf.Round(dmg * AldaraRelics.DmgMult()));
            DStat.Hit(m, dmg); AldaraSkillFx.PfxHit(m, col); m.hp -= dmg; m.flash = 0.15f; m.aggroT = 6;
            m.hurtT = 0.22f; if (m.anim) m.anim.Hurt();
            if (m.act != null && !m.boss && m.act.t < m.act.mv.wind && dmg >= m.maxHp * 0.12f) { m.act = null; m.lift = 0; m.atkCd = 0.6f; AldaraFx.Text(m.x, m.y - m.r - 44, "Interrupted", AldaraRules.Hex("#ffd35a")); }
            if (mine && H.critFrame == Time.frameCount) { H.critFrame = -1; AldaraFx.Text(m.x + 8, m.y - m.r - 22, "CRIT", AldaraRules.Hex("#ffe08a")); }
            AldaraFx.Text(m.x, m.y - m.r - 10, "-" + dmg, col);
            float ls = AldaraTree.T("ls"); if (mine && ls > 0 && AldaraRelics.busy == 0) { float h = Mathf.Round(dmg * ls); if (h > 0) { float hb = H.hp; H.hp = Mathf.Min(H.maxHp, H.hp + h); DStat.Heal(H.hp - hb); } }
            if (mine) AldaraRelics.OnHit(m, dmg);
            if (m.hp <= 0) KillMonster(m);
        }
        void KillMonster(Mon t)
        {
            var H = AldaraHero.I;
            t.act = null; t.lift = 0; t.enrT = 0; t.dead = true; t.respawnT = t.dun ? 1e9f : t.boss ? 420 : 28 + Random.value * 18; if (t.ambush != null) t.respawnT = 1e12f; else if (!t.dun && !t.boss && t.tier == 11) t.respawnT = 150 + Random.value * 90; if (t.dun) DStat.Kill();
            int g = t.lvl - H.lvl; float xm = g >= 0 ? Mathf.Min(1.6f, 1 + 0.04f * g) : g >= -2 ? 1 : Mathf.Max(0.05f, 1 + 0.12f * (g + 2));
            H.GainXp(Mathf.Max(1, Mathf.Round(t.xp * xm)));
            if (AldaraLoot.I) AldaraLoot.I.OnKill(t); else H.gold += t.gold;
            AldaraQuests.OnKill(t); AldaraRelics.OnKill(t); AldaraSubclass.OnKill(t);
            if (t.boss) H.bossKills++;
            AldaraFx.Text(t.x, t.y - t.r - 26, "+" + t.xp + "xp", AldaraRules.Hex("#7ec8ff"));
            if (t.boss) { AldaraHud.Banner(t.name + " defeated!"); AldaraChat.Sys(H.heroName + " defeated " + t.name + (AldaraDungeon.Active && AldaraDungeon.def != null ? " in " + AldaraDungeon.def.name : "") + "!", "boss"); }
            if (H.target == t) H.target = null;
            H.kills++;
            SpawnCorpse(t);
            if (t.dun && AldaraDungeon.Active) AldaraDungeon.OnKill(t);
        }
        /// into a dungeon: the world's monsters wait (hidden) while the dungeon's run
        public void EnterDungeon(out List<Mon> saved)
        {
            saved = new List<Mon>(all); foreach (var m in all) { if (m.view) Destroy(m.view); m.view = null; m.anim = null; m.model = null; m.sculpt = null; m.rends = null; }
            all.Clear(); ClearFx();
        }
        public void LeaveDungeon(List<Mon> saved)
        {
            foreach (var m in all) if (m.view) Destroy(m.view);
            all.Clear(); ClearFx(); if (saved != null) all.AddRange(saved);
        }
        public Mon FindNearest(float x, float y, float range)
        {
            Mon best = null; float bd = range;
            foreach (var m in all)
            {
                if (m.dead || !AldaraDungeon.Reachable(m)) continue; float d = Mathf.Sqrt((m.x - x) * (m.x - x) + (m.y - y) * (m.y - y));
                if (m.dun && AldaraDungeon.Active) { int f = AldaraDungeon.FlowAt(m.x, m.y); if (f < 0) continue; d = Mathf.Max(d, f * AldaraDungeon.NCS); }
                if (d < bd) { best = m; bd = d; }
            }
            return best;
        }

        /// who is hitting: "player" (relics, life steal and crits apply) or "pet"
        public static string HitSrc = "player";
        public static GameObject PrefabOf(string name) { if (!I) return null; var d = I.book.Get(name); return d == null ? null : I.Prefab(d); }
        // ---- visuals ----
        GameObject Prefab(MonDef d)
        {
            GameObject p; if (prefabs.TryGetValue(d.id, out p)) return p;
            p = Resources.Load<GameObject>("MonPrefabs/Mon_" + d.id); prefabs[d.id] = p; return p;
        }
        void Show(Mon m, float dx, float dy, float dt)
        {
            if (!m.view)
            {
                var pf = Prefab(m.def); if (!pf) return;
                m.view = new GameObject(m.name); m.view.transform.SetParent(transform, false); m.view.transform.localScale = AldaraView.Squash;
                var inst = Instantiate(pf, m.view.transform, false); m.model = inst.transform; m.anim = inst.GetComponent<AldaraMonsterAnimator>();
                bool sc = m.boss && AldaraBossSculpt.Has(m.name);
                if (!(m.chest > 0) || sc) { var rs = inst.GetComponentsInChildren<Renderer>(); if (rs.Length > 0) { m.view.transform.position = AldaraWorld.ToUnity(m.x, m.y); float top = float.MinValue, bot = float.MaxValue; foreach (var r in rs) { top = Mathf.Max(top, r.bounds.max.y); bot = Mathf.Min(bot, r.bounds.min.y); } if (!(m.chest > 0)) m.chest = (top - bot) * AldaraWorld.PX * 0.55f;
                        if (sc) { float oldH = top - Mathf.Max(bot, m.view.transform.position.y); var s = AldaraBossSculpt.Make(m, m.view.transform, oldH); if (s) { inst.SetActive(false); Destroy(inst); m.model = s.transform; m.anim = null; m.sculpt = s; } } } }
            }
            if (!m.view.activeSelf) m.view.SetActive(true);
            // facing, as drawMonster3: the move's aim, else the hero while engaged, else the way it walks
            bool mvd = dx * dx + dy * dy > 0.02f * dt * 60;
            if (m.act != null) m.face = m.act.a; else if (m.engT > 0 && m.hasFaceA && (!mvd || m.strT > 0)) m.face = m.faceA; else if (mvd) m.face = Mathf.Atan2(dy, dx);
            float mv = Mathf.Sqrt(dx * dx + dy * dy); m.walkT += mv * 0.11f;
            // feet are drawn 15 r / 16 below the monster's point, as in the browser
            float fy = m.y + 15 * m.r / 16;
            float hov = 0; if (m.def.winged != 0) { float cyc = Time.time * (m.def.flyer != 0 ? 5.2f : 4.4f); float bse = m.def.flyer != 0 ? 30 : 6, k = Mathf.Min(m.r / 16, m.def.flyer != 0 ? 1.5f : 2.2f); hov = Mathf.Max(2, (bse - Mathf.Cos(cyc * 2) * 4) * k * (m.act != null ? 0.45f : 1)); }
            var pos = AldaraWorld.ToUnity(m.x, fy); pos.y += (m.lift + hov) / AldaraWorld.PX;
            if (m.hurtT > 0 && m.act == null) pos.x += Mathf.Cos(Mathf.Atan2(m.y - AldaraPlayer.I.y, m.x - AldaraPlayer.I.x)) * m.hurtT / 0.22f * 5 / AldaraWorld.PX;
            m.view.transform.position = pos;
            if (m.model) m.model.localRotation = Quaternion.Euler(0, 90 + m.face * Mathf.Rad2Deg, 0);
            // the hit flash: the browser adds a 55% copy of the frame on top
            bool fo = m.flash > 0; if (fo != m.flashOn)
            {
                m.flashOn = fo; if (m.rends == null) m.rends = m.view.GetComponentsInChildren<Renderer>();
                foreach (var r in m.rends) { if (!r) continue; if (fo) { var mp = new MaterialPropertyBlock(); r.GetPropertyBlock(mp); var mat = r.sharedMaterial; mp.SetFloat("_Emit", (mat && mat.HasProperty("_Emit") ? mat.GetFloat("_Emit") : 0) + 0.55f); r.SetPropertyBlock(mp); } else r.SetPropertyBlock(null); }
            }
            if (m.sculpt) m.sculpt.Tick(mvd && m.act == null, dt);
            if (m.anim)
            {
                bool moving = mvd && m.act == null;
                if (m.act != null) m.anim.SetAction(m.act.mv.i, m.act.t); else m.anim.ClearAction();
                if (moving && m.def.floater == 0 && m.anim.Has("walk")) m.anim.SetBase("walk", mv / Mathf.Max(dt, 1e-4f) * 0.11f / (Mathf.PI * 2));
                else if (moving && m.anim.Has("fly")) m.anim.SetBase("fly", 0);
                else m.anim.SetBase("idle", 0);
            }
        }
        void Hide(Mon m) { if (m.view && m.view.activeSelf) m.view.SetActive(false); }
    }
}
