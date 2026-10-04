using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // World monsters, simulated in the browser's pixel space with the browser's rules (camps from makeCamps / spawnCamp,
    // bosses in their lairs, monStats, aggro ranges, monCombat / startAct / monAct / doHit / hitTest, projectiles, lobs,
    // ground hazards and shockwaves, respawn timers). Models and animations are the browser's own, baked per monster.
    public class AldaraMonsters : MonoBehaviour
    {
        public static AldaraMonsters I;
        public float viewRangePx = 1500;     // models exist only near the hero
        public readonly List<Mon> all = new List<Mon>();
        MonsterBook book;
        readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        public class Act { public MoveDef mv; public float t, a, tx, ty, sx, sy, hs, tk, dd; public int hi; public bool st, hit; }
        public class Mon
        {
            public MonDef def; public string name; public bool boss; public float r, x, y, homeX, homeY, hp, maxHp, atk, xp, gold; public int lvl, tier, camp = -1;
            public float wanderT, wx, wy, atkCd, respawnT, flash, hurtT, engT, enrT, aggroT, slowT, stunT, strT, walkT, lift, face = Mathf.PI / 2, faceA; public int str;
            public bool dead, hasFaceA; public Act act; public float[] mcd;
            public GameObject view; public AldaraMonsterAnimator anim; public Transform model;
            public float lx, ly, dotT, dotTick, dotDmg, navIgnore;
            // dungeons: its section, its own speed, the boss's ability timers, line-of-sight caches
            public bool dun, enraged, summoned, slowHit, los, sees; public int room; public float spd = -1, mt, mt2, losT, seeT; public string mech, color, bolt;
        }

        void Awake()
        {
            I = this; AldaraWorld.Load(); book = MonsterBook.Load();
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
            UpdateEnemyFx(dt);
            int busy = 0; foreach (var o in all) if (!o.dead && o.act != null && o.act.mv.cd > 0 && !o.boss) busy++;
            foreach (var m in all)
            {
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
            if (mv.tele > 0 && (mv.shape == "circle" || mv.shape == "line" || mv.shape == "arc")) Telegraph(m, mv, ang);
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
                            if (f > 0.08f && Mathf.Sqrt((m.x - ox) * (m.x - ox) + (m.y - oy) * (m.y - oy)) < st * 0.3f) A.t = W + S;
                            if (!A.hit && AldaraHero.I.alive && Mathf.Sqrt((px - m.x) * (px - m.x) + (py - m.y) * (py - m.y)) < m.r + 16 * 0.8f + 4) { A.hit = true; HurtPlayer(m, mv, Mathf.Atan2(py - m.y, px - m.x)); }
                            break;
                        }
                    case "leap": { float e = EIO(f), lox = m.x, loy = m.y; m.x = A.sx + (A.tx - A.sx) * e; m.y = A.sy + (A.ty - A.sy) * e; if (m.dun && AldaraDungeon.Active) AldaraDungeon.PushMon(m, lox, loy); m.lift = Mathf.Sin(f * Mathf.PI) * (mv.lift > 0 ? mv.lift : 60) * (m.boss ? 1.3f : 1); break; }
                }
            }
            else if (mv.move == "leap") m.lift = 0;
            while (A.hi < mv.hits.Length && t >= W + mv.hits[A.hi] * S) { DoHit(m, A, A.hi); A.hi++; }
            if (mv.chan > 0 && t >= W && t < W + S) { A.tk -= dt; if (A.tk <= 0) { A.tk = mv.tick; if (HitTest(m, mv, A.a)) HurtPlayer(m, mv, A.a); } }
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
                    if (SpotOk(m, x, y)) { AldaraFx.Burst(m.x, m.y, 30, ElCol(mv.el)); m.x = x; m.y = y; AldaraFx.Burst(m.x, m.y, 30, ElCol(mv.el)); A.a = Mathf.Atan2(P.y - m.y, P.x - m.x); break; }
                }
            }
        }
        public static Color ElCol(string el)
        {
            switch (el)
            {
                case "fire": return AldaraRules.Hex("#ff6a1a"); case "frost": return AldaraRules.Hex("#6ac8ff"); case "shadow": return AldaraRules.Hex("#8a4aff");
                case "void": return AldaraRules.Hex("#a86aff"); case "poison": return AldaraRules.Hex("#8ad040"); case "holy": return AldaraRules.Hex("#ffc84a");
                case "nature": return AldaraRules.Hex("#6ad06a"); case "arcane": return AldaraRules.Hex("#c07aff"); case "water": return AldaraRules.Hex("#3ac0e0");
                case "bone": return AldaraRules.Hex("#d8d0b8"); case "blood": return AldaraRules.Hex("#e0304a"); case "earth": return AldaraRules.Hex("#b08a5a");
                default: return AldaraRules.Hex("#e6dcc8");
            }
        }
        static Color TeleCol(MoveDef mv) { return string.IsNullOrEmpty(mv.el) || mv.el == "phys" || mv.el == "bone" || mv.el == "earth" ? AldaraRules.Hex("#ff3a24") : ElCol(mv.el); }
        void Telegraph(Mon m, MoveDef mv, float a)
        {
            var c = TeleCol(mv); c.a = 0.32f;
            if (mv.shape == "circle") { float fwd = Mathf.Max(0, mv.fwd); AldaraFx.Disc(m.x + Mathf.Cos(a) * fwd, m.y + Mathf.Sin(a) * fwd, mv.rad >= 0 ? mv.rad : m.r + mv.reach, c, mv.wind); }
            else AldaraFx.Disc(m.x + Mathf.Cos(a) * (m.r + mv.reach) * 0.5f, m.y + Mathf.Sin(a) * (m.r + mv.reach) * 0.5f, (m.r + mv.reach) * 0.55f, c, mv.wind);
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
                case "beam": return al > 0 && al < mv.len + pr && Mathf.Abs(ac) < mv.wid + pr;
            }
            return false;
        }
        void HurtPlayer(Mon m, MoveDef mv, float a)
        {
            var H = AldaraHero.I; if (!H.alive) return;
            H.Damage(Dmg(m, mv), a, m.lvl);
            if (mv.slow > 0) H.slowT = Mathf.Max(H.slowT, mv.slow);
            if (mv.kb > 0) Knock(a, mv.kb);
            if (m.act != null && mv.chan <= 0) m.act.hs = (mv.dmg > 0 ? mv.dmg : 1) >= 1.4f ? 0.09f : 0.05f;
        }
        static void Knock(float a, float dist)
        {
            var P = AldaraPlayer.I; if (AldaraDungeon.Active) { AldaraDungeon.Shove(a, dist); return; }
            for (int k = 0; k < 6; k++) { float ox = P.x, oy = P.y; P.x += Mathf.Cos(a) * dist / 6; P.y += Mathf.Sin(a) * dist / 6; if (AldaraWorld.BlockedAt(P.x, P.y)) { P.x = ox; P.y = oy; break; } }
        }

        // ---- doHit: what each move shape does when it lands ----
        class Proj { public float x, y, vx, vy, life, dmg, rad, slow, kb; public Color col; public bool pierce, hitP, ground; public GameObject go; }
        class Lob { public float sx, sy, tx, ty, t, T, H, aoe, dmg, slow; public Color col; public GameObject go; }
        class Hz { public float x, y, r, delay, max, dmg, slow, dur, dps, tick; public Color col; public bool ring, icicle, pool; public float rr, r1, spd, w, kb; public bool hit; public GameObject go, ic; }
        readonly List<Proj> eproj = new List<Proj>(); readonly List<Lob> lobs = new List<Lob>(); readonly List<Hz> hazards = new List<Hz>();
        void DoHit(Mon m, Act A, int i)
        {
            var mv = A.mv; float a = A.a; var P = AldaraPlayer.I; var E = ElCol(mv.el); float c = Mathf.Cos(a), s = Mathf.Sin(a), pd = Mathf.Sqrt((P.x - m.x) * (P.x - m.x) + (P.y - m.y) * (P.y - m.y));
            switch (mv.shape)
            {
                case "arc": case "line": case "circle":
                    AldaraFx.Slash(m.x + c * (m.r + Mathf.Max(20, mv.reach)) * 0.6f, m.y + s * (m.r + Mathf.Max(20, mv.reach)) * 0.6f, a, E, mv.shape == "circle" ? (mv.rad >= 0 ? mv.rad : m.r + mv.reach) : m.r + mv.reach, mv.shape == "circle");
                    if (HitTest(m, mv, a)) HurtPlayer(m, mv, Mathf.Atan2(P.y - m.y, P.x - m.x));
                    break;
                case "roar":
                    AldaraFx.Ring(m.x, m.y, mv.rad, E, 0.8f);
                    if (mv.buff > 0) { m.enrT = mv.buff; AldaraFx.Text(m.x, m.y - m.r - 40, "Enraged", AldaraRules.Hex("#ff8a5a")); }
                    if (pd < mv.rad + 16) { if (mv.slow > 0) AldaraHero.I.slowT = Mathf.Max(AldaraHero.I.slowT, mv.slow); if (mv.dmg > 0) AldaraHero.I.Damage(Dmg(m, mv), Mathf.Atan2(P.y - m.y, P.x - m.x), m.lvl); }
                    break;
                case "proj":
                    {
                        int n = Mathf.Max(1, (int)mv.n); float sp = mv.spd > 0 ? mv.spd : 400, lead = AimLead(m, sp);
                        float bse = mv.hits.Length > 1 ? lead : (Mathf.Abs(AldaraRules.AngD(lead, a)) < 0.5f ? lead : a);
                        for (int k = 0; k < n; k++) SpawnProj(m, mv, bse + (k - (n - 1) / 2f) * mv.spread + (mv.hits.Length > 1 ? (Random.value - 0.5f) * (mv.spread > 0 ? mv.spread : 0.1f) : 0), false);
                        break;
                    }
                case "gwave": { int n = Mathf.Max(1, (int)mv.n); for (int k = 0; k < n; k++) SpawnProj(m, mv, a + (k - (n - 1) / 2f) * mv.spread, true); break; }
                case "lob":
                    {
                        var L = new Lob { sx = m.x + c * m.r * 0.6f, sy = m.y + s * m.r * 0.6f, tx = A.tx, ty = A.ty, T = mv.T > 0 ? mv.T : 0.9f, H = mv.H > 0 ? mv.H : 120, aoe = mv.rad >= 0 ? mv.rad : 60, dmg = Dmg(m, mv), slow = mv.slow, col = E };
                        L.go = AldaraFx.Orb(E, 0.45f); lobs.Add(L); AldaraFx.Disc(L.tx, L.ty, L.aoe, new Color(E.r, E.g, E.b, 0.25f), L.T); break;
                    }
                case "rain":
                    {
                        int n = Mathf.Max(1, (int)(mv.n > 0 ? mv.n : 4));
                        for (int k = 0; k < n; k++)
                        {
                            float x, y; if (k == 0) { x = P.x + pvx * 0.35f; y = P.y + pvy * 0.35f; } else { float aa = Random.value * Mathf.PI * 2, dd = 50 + Random.value * 110; x = A.tx + Mathf.Cos(aa) * dd; y = A.ty + Mathf.Sin(aa) * dd; }
                            AddHz(x, y, mv.rad, mv.delay + k * 0.13f, Dmg(m, mv), E, mv.slow);
                        }
                        break;
                    }
                case "target": AddHz(P.x + pvx * 0.25f, P.y + pvy * 0.25f, mv.rad, mv.delay, Dmg(m, mv), E, mv.slow); break;
                case "spikeline": for (int k = 1; k <= (mv.n > 0 ? mv.n : 7); k++) { float d = m.r + k * (mv.gap > 0 ? mv.gap : 40); AddHz(m.x + c * d, m.y + s * d, mv.rad, mv.delay + k * 0.075f, Dmg(m, mv), E, mv.slow); } break;
                case "ringz":
                    {
                        int n = (int)(mv.n > 0 ? mv.n : 6);
                        for (int k = 0; k < n; k++) { float aa = k / (float)n * Mathf.PI * 2 + Random.value * 0.3f, d = (mv.dist > 0 ? mv.dist : 110) * (0.8f + Random.value * 0.4f); AddHz(m.x + Mathf.Cos(aa) * d, m.y + Mathf.Sin(aa) * d, mv.rad, mv.delay + k * 0.06f, Dmg(m, mv), E, 0); }
                        AddHz(P.x, P.y, mv.rad, mv.delay + 0.25f, Dmg(m, mv), E, 0); break;
                    }
                case "wave":
                    {
                        var h = new Hz { ring = true, x = m.x, y = m.y, rr = m.r, r1 = mv.rad, spd = mv.spd, w = 30, dmg = Dmg(m, mv), col = E, kb = mv.kb, slow = mv.slow };
                        h.go = AldaraFx.RingObj(E); hazards.Add(h); break;
                    }
            }
        }
        float pvx, pvy, plx = float.NaN, ply;
        void TrackPlayerVelocity(float dt, float x, float y)
        {
            if (!float.IsNaN(plx) && dt > 0) { float vx = (x - plx) / dt, vy = (y - ply) / dt; bool ok = Mathf.Abs(vx) < 900 && Mathf.Abs(vy) < 900; pvx = ok ? pvx * 0.7f + vx * 0.3f : 0; pvy = ok ? pvy * 0.7f + vy * 0.3f : 0; }
            plx = x; ply = y;
        }
        float AimLead(Mon m, float spd) { var P = AldaraPlayer.I; float dx = P.x - m.x, dy = P.y - m.y, tt = Mathf.Sqrt(dx * dx + dy * dy) / spd * 0.5f; return Mathf.Atan2(dy + pvy * tt, dx + pvx * tt); }
        void SpawnProj(Mon m, MoveDef mv, float ang, bool ground)
        {
            float sp = mv.spd > 0 ? mv.spd : 400; var E = ElCol(mv.el);
            var p = new Proj { x = m.x + Mathf.Cos(ang) * m.r * 0.8f, y = m.y + Mathf.Sin(ang) * m.r * 0.8f, vx = Mathf.Cos(ang) * sp, vy = Mathf.Sin(ang) * sp, dmg = Dmg(m, mv), col = E,
                life = (MvMax(m, mv) + 160) / sp, rad = mv.look == "flamewave" ? 26 : mv.look == "lance" || mv.look == "spear" ? 9 : 7, slow = mv.slow, kb = mv.kb, pierce = mv.look == "flamewave", ground = ground };
            p.go = AldaraFx.Orb(E, mv.look == "arrow" || mv.look == "dagger" ? 0.22f : 0.32f); eproj.Add(p);
        }
        /// a ground hazard from a dungeon (a boss's nova, falling icicles, a lava pool that burns while you stand in it)
        public void AddHazard(float x, float y, float r, float delay, float dmg, Color col, float slow, bool icicle, float poolDur, float dps)
        {
            var h = new Hz { x = x, y = y, r = r, delay = delay, max = delay, dmg = dmg, col = col, slow = slow, icicle = icicle, pool = poolDur > 0, dur = poolDur, dps = dps };
            AldaraFx.Disc(x, y, r, new Color(col.r, col.g, col.b, 0.3f), delay); if (icicle) h.ic = AldaraFx.Icicle(); hazards.Add(h);
        }
        /// enemyShoot: a bolt from a dungeon boss
        public void EnemyShoot(Mon m, float ang, float dmg, Color col, float spd)
        {
            var p = new Proj { x = m.x + Mathf.Cos(ang) * m.r, y = m.y + Mathf.Sin(ang) * m.r, vx = Mathf.Cos(ang) * spd, vy = Mathf.Sin(ang) * spd, dmg = dmg, col = col, life = 2.2f, rad = 7, slow = m.slowHit ? 1.5f : 0 };
            p.go = AldaraFx.Orb(col, 0.32f); eproj.Add(p);
        }
        void AddHz(float x, float y, float r, float delay, float dmg, Color col, float slow)
        {
            var h = new Hz { x = x, y = y, r = r, delay = delay, dmg = dmg, col = col, slow = slow };
            AldaraFx.Disc(x, y, r, new Color(col.r, col.g, col.b, 0.3f), delay); hazards.Add(h);
        }
        void UpdateEnemyFx(float dt)
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I;
            for (int i = eproj.Count - 1; i >= 0; i--)
            {
                var e = eproj[i]; e.x += e.vx * dt; e.y += e.vy * dt; e.life -= dt;
                if (e.go) e.go.transform.position = AldaraWorld.ToUnity(e.x, e.y) + Vector3.up * (e.ground ? 0.2f : 0.9f);
                if (H.alive && !e.hitP && Mathf.Sqrt((e.x - P.x) * (e.x - P.x) + (e.y - P.y) * (e.y - P.y)) < 16 * 0.7f + e.rad)
                {
                    H.Damage(e.dmg, Mathf.Atan2(e.vy, e.vx), 0); if (e.slow > 0) H.slowT = Mathf.Max(H.slowT, e.slow); if (e.kb > 0) Knock(Mathf.Atan2(e.vy, e.vx), e.kb);
                    if (!e.pierce) { Kill(e.go); eproj.RemoveAt(i); continue; }
                    e.hitP = true;
                }
                if (e.life <= 0 || (!e.ground && (AldaraDungeon.Active ? AldaraDungeon.ShotWall(e.x, e.y, e.x - e.vx * dt, e.y - e.vy * dt) : AldaraWorld.BlockedAt(e.x, e.y)))) { Kill(e.go); eproj.RemoveAt(i); }
            }
            for (int i = lobs.Count - 1; i >= 0; i--)
            {
                var L = lobs[i]; L.t += dt; float u = Mathf.Min(1, L.t / L.T), x = L.sx + (L.tx - L.sx) * u, y = L.sy + (L.ty - L.sy) * u, h = 4 * L.H * u * (1 - u);
                if (L.go) L.go.transform.position = AldaraWorld.ToUnity(x, y) + Vector3.up * (h / AldaraWorld.PX + 0.6f);
                if (u >= 1)
                {
                    if (H.alive && Mathf.Sqrt((P.x - L.tx) * (P.x - L.tx) + (P.y - L.ty) * (P.y - L.ty)) < L.aoe + 16 * 0.6f) { H.Damage(L.dmg, 0, 0); if (L.slow > 0) H.slowT = Mathf.Max(H.slowT, L.slow); }
                    AldaraFx.Burst(L.tx, L.ty, L.aoe, L.col); Kill(L.go); lobs.RemoveAt(i);
                }
            }
            for (int i = hazards.Count - 1; i >= 0; i--)
            {
                var h = hazards[i];
                if (h.ring)
                {
                    h.rr += h.spd * dt; float d = Mathf.Sqrt((P.x - h.x) * (P.x - h.x) + (P.y - h.y) * (P.y - h.y));
                    if (h.go) { h.go.transform.position = AldaraWorld.ToUnity(h.x, h.y) + Vector3.up * 0.15f; h.go.transform.localScale = new Vector3(h.rr * 2 / AldaraWorld.PX, 1, h.rr * 2 / AldaraWorld.PX); }
                    if (!h.hit && H.alive && Mathf.Abs(d - h.rr) < h.w * 0.5f + 16 * 0.6f) { h.hit = true; float a = Mathf.Atan2(P.y - h.y, P.x - h.x); H.Damage(h.dmg, a, 0); if (h.slow > 0) H.slowT = Mathf.Max(H.slowT, h.slow); if (h.kb > 0) Knock(a, h.kb); }
                    if (h.rr >= h.r1) { Kill(h.go); hazards.RemoveAt(i); }
                    continue;
                }
                if (h.pool && h.delay <= 0)
                {   // a burning pool: hurts every 0.3 s while you stand in it
                    h.dur -= dt; h.tick -= dt;
                    if (h.tick <= 0 && H.alive && Mathf.Sqrt((P.x - h.x) * (P.x - h.x) + (P.y - h.y) * (P.y - h.y)) < h.r) { h.tick = 0.3f; H.Damage(Mathf.Round(h.dps * 0.3f), 0, 0); }
                    if (h.dur <= 0) { Kill(h.go); hazards.RemoveAt(i); }
                    continue;
                }
                h.delay -= dt;
                if (h.icicle && h.ic) { float k = 1 - h.delay / Mathf.Max(0.01f, h.max); h.ic.SetActive(k > 0.45f); if (k > 0.45f) { float f = (k - 0.45f) / 0.55f; h.ic.transform.position = AldaraWorld.ToUnity(h.x, h.y) + Vector3.up * ((1 - f * f) * 420 / AldaraWorld.PX); } }
                if (h.delay <= 0)
                {
                    if (h.pool) { if (h.ic) Kill(h.ic); h.go = AldaraFx.Pool(h.x, h.y, h.r, h.col); h.delay = 0; continue; }
                    if (h.ic) Kill(h.ic);
                    if (h.dmg > 0 && H.alive && Mathf.Sqrt((P.x - h.x) * (P.x - h.x) + (P.y - h.y) * (P.y - h.y)) < h.r + 16 * 0.4f) { H.Damage(h.dmg, 0, 0); if (h.slow > 0) H.slowT = Mathf.Max(H.slowT, h.slow); }
                    AldaraFx.Burst(h.x, h.y, h.r, h.col); hazards.RemoveAt(i);
                }
            }
        }
        static void Kill(GameObject g) { if (g) Destroy(g); }

        // ---- the hero hits a monster (hitMonster / monHurt / killMonster) ----
        public void HitMonster(Mon m, float dmg, Color col)
        {
            if (m.dead) return; var H = AldaraHero.I; int g = m.lvl - H.lvl;
            if (g > 0 && Random.value < AldaraRules.LvMiss(g)) { m.aggroT = 6; AldaraFx.Text(m.x, m.y - m.r - 10, "Glance", new Color(0.6f, 0.6f, 0.6f)); dmg = Mathf.Max(1, Mathf.Round(dmg * 0.15f)); }
            dmg = Mathf.Max(1, Mathf.Round(dmg * AldaraRules.LvOut(g)));
            DStat.Hit(m, dmg); m.hp -= dmg; m.flash = 0.15f; m.aggroT = 6;
            m.hurtT = 0.22f; if (m.anim) m.anim.Hurt();
            if (m.act != null && !m.boss && m.act.t < m.act.mv.wind && dmg >= m.maxHp * 0.12f) { m.act = null; m.lift = 0; m.atkCd = 0.6f; AldaraFx.Text(m.x, m.y - m.r - 44, "Interrupted", AldaraRules.Hex("#ffd35a")); }
            if (H.critFrame == Time.frameCount) { H.critFrame = -1; AldaraFx.Text(m.x + 8, m.y - m.r - 22, "CRIT", AldaraRules.Hex("#ffe08a")); }
            AldaraFx.Text(m.x, m.y - m.r - 10, "-" + dmg, col);
            float ls = AldaraTree.T("ls"); if (ls > 0) { float h = Mathf.Round(dmg * ls); if (h > 0) H.hp = Mathf.Min(H.maxHp, H.hp + h); }
            if (m.hp <= 0) KillMonster(m);
        }
        void KillMonster(Mon t)
        {
            var H = AldaraHero.I;
            t.act = null; t.lift = 0; t.enrT = 0; t.dead = true; t.respawnT = t.dun ? 1e9f : t.boss ? 420 : 28 + Random.value * 18; if (t.dun) DStat.Kill();
            int g = t.lvl - H.lvl; float xm = g >= 0 ? Mathf.Min(1.6f, 1 + 0.04f * g) : g >= -2 ? 1 : Mathf.Max(0.05f, 1 + 0.12f * (g + 2));
            H.GainXp(Mathf.Max(1, Mathf.Round(t.xp * xm)));
            if (AldaraLoot.I) AldaraLoot.I.OnKill(t); else H.gold += t.gold;
            AldaraQuests.OnKill(t);
            if (t.boss) H.bossKills++;
            AldaraFx.Text(t.x, t.y - t.r - 26, "+" + t.xp + "xp", AldaraRules.Hex("#7ec8ff"));
            if (t.boss) AldaraHud.Banner(t.name + " defeated!");
            if (H.target == t) H.target = null;
            H.kills++;
            Hide(t);
            if (t.dun && AldaraDungeon.Active) AldaraDungeon.OnKill(t);
        }
        /// into a dungeon: the world's monsters wait (hidden) while the dungeon's run
        public void EnterDungeon(out List<Mon> saved)
        {
            saved = new List<Mon>(all); foreach (var m in all) { if (m.view) Destroy(m.view); m.view = null; m.anim = null; m.model = null; }
            all.Clear(); ClearFx();
        }
        public void LeaveDungeon(List<Mon> saved)
        {
            foreach (var m in all) if (m.view) Destroy(m.view);
            all.Clear(); ClearFx(); if (saved != null) all.AddRange(saved);
        }
        void ClearFx()
        {
            foreach (var e in eproj) Kill(e.go); foreach (var l in lobs) Kill(l.go); foreach (var h in hazards) { Kill(h.go); Kill(h.ic); }
            eproj.Clear(); lobs.Clear(); hazards.Clear();
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
