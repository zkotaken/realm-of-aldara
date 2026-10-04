using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using A = Aldara.AldaraDungeon;

namespace Aldara
{
    // The browser's raids (RAIDS / RENC / raidInit / raidTickEnc / raidApply ...): the Vault of the Eclipse, built on the
    // dungeon engine. Eight sections with an encounter in each (plates, a cleared descent, the Oracles' glyph sequence,
    // levers, the Twin Regents, a hold-out on the bridge, Maelthar and the Sunwell, the Treasury), waves of enemies,
    // checkpoints that are saved, and wipes that send you back to the last one. Played alone, you are always the leader.
    public class AldaraRaid : MonoBehaviour
    {
        public static AldaraRaid I;
        public const int RAID_LVL_MIN = 100;

        // ---------- the enemies (hits: how many of your hits they take at the raid's level; die: how many of theirs you take) ----------
        public class RMob { public string name, color, bolt, mech; public float r, spd, hits, die; public bool ranged, tank, elite, boss; }
        public static readonly Dictionary<string, RMob> RMOB = new Dictionary<string, RMob> {
            {"thrall", new RMob{name="Gloam Wretch",color="#6a5a8a",r=16,spd=0.95f,hits=4,die=13}},
            {"legion", new RMob{name="Duskguard Spearman",color="#8a7a5a",r=21,spd=0.6f,hits=9,die=9}},
            {"acolyte", new RMob{name="Shade Chanter",color="#7a4ad0",r=17,spd=0.55f,hits=6,die=12,ranged=true,bolt="#c07aff"}},
            {"knight", new RMob{name="Emberhelm Templar",color="#f2c46a",r=27,spd=0.55f,hits=38,die=7,tank=true,elite=true}},
            {"owarden", new RMob{name="Glyphbound Sentinel",color="#9ad8ff",r=28,spd=0.6f,hits=40,die=7,ranged=true,bolt="#bfe8ff",elite=true}},
            {"dusk", new RMob{name="Duskbound Herald",color="#9a5aff",r=24,spd=0.6f,hits=26,die=8,elite=true}},
            {"dawn", new RMob{name="Dawnbound Herald",color="#ffd35a",r=24,spd=0.6f,hits=26,die=8,elite=true,ranged=true,bolt="#ffe08a"}},
            {"eward", new RMob{name="Veilrender",color="#c07aff",r=30,spd=0.55f,hits=60,die=6,elite=true}},
            {"colossus", new RMob{name="Sealwarden Behemoth",color="#d9a93a",r=50,spd=0.42f,hits=150,die=5,boss=true,mech="nova"}},
            {"bwarden", new RMob{name="Spanwatch Revenant",color="#8ab4ff",r=44,spd=0.5f,hits=130,die=5,boss=true,mech="spikes"}},
            {"vesper", new RMob{name="Vesper, Regent of Dusk",color="#9a5aff",r=46,spd=0.5f,hits=170,die=4,boss=true,mech="nova",ranged=true,bolt="#c07aff"}},
            {"solace", new RMob{name="Solace, Regent of Dawn",color="#ffd35a",r=46,spd=0.5f,hits=170,die=4,boss=true,mech="spikes"}},
            {"sovereign", new RMob{name="Maelthar, the Sunless King",color="#f2c46a",r=62,spd=0.45f,hits=420,die=4,boss=true,mech="nova",ranged=true,bolt="#ffd35a"}},
        };
        public static float LvsAtk(float L) { return 14 + 5.5f * (Mathf.Max(1, L) - 1); }
        public static float LvsHp(float L) { return 100 + 33 * (Mathf.Max(1, L) - 1); }

        // ---------- the encounters, one per section (entry: where you appear when you start at that section's checkpoint) ----------
        public class Enc
        {
            public string type; public Vector2 entry; public Vector3 trig; public bool hasTrig; public Vector2[] plates, pads, levers, spawns, pylons, bpos;
            public float pr, need, tl, dur, wr; public int[] rounds; public int cpAfter = -1; public Vector2 boss, center, chest, secret, warden; public bool hasSecret;
            public object[][] mobs;
        }
        static Vector2 P2(float x, float y) { return new Vector2(x, y); }
        static Vector2[] Ring(int n, float cx, float cy, float r, float a0) { var o = new Vector2[n]; for (int k = 0; k < n; k++) o[k] = P2(cx + Mathf.Cos(k * 2 * Mathf.PI / n + a0) * r, cy + Mathf.Sin(k * 2 * Mathf.PI / n + a0) * r); return o; }
        static object[] M(string k, float x, float y) { return new object[] { k, x, y }; }
        public static readonly Enc[] RENC = {
            new Enc{type="plates",entry=P2(430,2800),trig=new Vector3(1300,2800,470),hasTrig=true,plates=new[]{P2(1300,2430),P2(1620,2985),P2(980,2985)},pr=95,need=24,tl=480,
                spawns=new[]{P2(1300,3180),P2(1700,2600),P2(900,2600),P2(1300,2800)},boss=P2(1300,2800),cpAfter=1},
            new Enc{type="clear",entry=P2(2330,2800),secret=P2(3080,1360),hasSecret=true,mobs=new[]{M("thrall",2330,2760),M("thrall",2380,2860),M("thrall",2290,2850),M("legion",3080,2760),M("legion",3140,2860),M("acolyte",3000,2880),M("acolyte",3160,2720),
                M("thrall",3080,1960),M("thrall",3020,2060),M("thrall",3150,2050),M("acolyte",3080,2140),M("legion",4060,1650),M("legion",4250,1850),M("acolyte",4040,1860),M("acolyte",4260,1640),M("knight",4150,1890),
                M("thrall",4100,2760),M("thrall",4200,2840),M("thrall",4150,2700),M("legion",3080,1330)}},
            new Enc{type="oracle",entry=P2(4900,2800),trig=new Vector3(5350,2800,430),hasTrig=true,center=P2(5350,2800),pr=62,rounds=new[]{4,6,8},cpAfter=3,
                pads=Ring(6,5350,2800,360,0),spawns=new[]{P2(5350,2330),P2(5350,3270),P2(5820,2800),P2(4880,2800)}},
            new Enc{type="levers",entry=P2(6340,2800),levers=new[]{P2(7250,2130),P2(7250,3470),P2(8030,2290)},
                mobs=new[]{M("legion",6520,2800),M("thrall",6560,2760),M("thrall",6560,2840),M("acolyte",6850,2510),M("acolyte",7650,2510),M("legion",7000,2500),M("legion",7500,2500),
                M("thrall",7000,3100),M("thrall",7100,3120),M("legion",7450,3100),M("acolyte",6850,3090),M("acolyte",7650,3090),M("knight",7250,2170),M("knight",7250,3440),M("legion",7990,2300)}},
            new Enc{type="twins",entry=P2(8330,2800),trig=new Vector3(8800,2800,480),hasTrig=true,pads=new[]{P2(8520,2560),P2(9080,3040)},bpos=new[]{P2(8650,2800),P2(8950,2800)},tl=600,
                spawns=new[]{P2(8800,2300),P2(8800,3300),P2(9300,2800),P2(8350,2800)},cpAfter=5},
            new Enc{type="survive",entry=P2(8800,1520),trig=new Vector3(8800,1420,110),hasTrig=true,dur=100,spawns=new[]{P2(8800,1300),P2(8800,1600),P2(8580,1450),P2(9020,1450)},warden=P2(7300,1450),cpAfter=6},
            new Enc{type="sov",entry=P2(6640,1450),trig=new Vector3(6100,1450,520),hasTrig=true,center=P2(6100,1450),tl=900,boss=P2(6100,1250),wr=105,
                pylons=Ring(4,6100,1450,430,Mathf.PI/4),spawns=new[]{P2(6100,900),P2(6100,2000),P2(5550,1450),P2(6650,1450)}},
            new Enc{type="treasury",entry=P2(6100,560),chest=P2(6100,380)},
        };
        static readonly HashSet<string> SEALS = new HashSet<string> { "plates", "oracle", "twins", "survive", "sov" };

        // ---------- the encounter running now ----------
        public class EState
        {
            public string type; public int s; public float t, wt; public bool wipe;
            public float[] pl, lv, pad, vul2, dtt; public int[] occ, con, ok, ch, bs; public int ph, wn; public float tl; public AldaraMonsters.Mon cb, wb, boss; public AldaraMonsters.Mon[] b;
            public int round, step, lit = -1, bad = -1, si; public string oph; public float pt; public List<int> seq = new List<int>(), good = new List<int>();
            public float jt, rt, away, ecl, kt, lt, vul, fs = -1, depT; public int need, dep, carry; public bool w50; public List<Vector2> sp = new List<Vector2>(), wells = new List<Vector2>();
        }

        // ---------- state ----------
        public static bool On; public static A.Def def; public static int idx, L, cp, seed; public static EState E; public static bool[] done;
        public static bool won, chest, secret, wiping; static float wipeT, deadT, t; static int lastPad = -1;
        class Later { public float at; public System.Action f; }
        static readonly List<Later> later = new List<Later>();
        static void After(float s, System.Action f) { later.Add(new Later { at = Time.time + s, f = f }); }

        // ---------- the save (player.dun.raids) ----------
        static JObject Prog(string id)
        {
            var r = AldaraSave.Raw; if (r == null) return new JObject { ["cp"] = 0, ["clears"] = 0, ["best"] = 0 };
            if (!(r["dun"] is JObject d)) { d = new JObject { ["best"] = 0, ["clears"] = new JObject() }; r["dun"] = d; }
            if (!(d["raids"] is JObject rs)) { rs = new JObject(); d["raids"] = rs; }
            if (!(rs[id] is JObject p)) { p = new JObject { ["cp"] = 0, ["clears"] = 0, ["best"] = 0 }; rs[id] = p; }
            return p;
        }
        public static int Cp(string id) { return (int)Prog(id)["cp"]; }
        public static int Clears(string id) { var r = AldaraSave.Raw?["dun"]?["raids"]?[id]?["clears"]; return r == null ? 0 : (int)r; }
        public static int BestTime(string id) { return (int)Prog(id)["best"]; }
        public static string Fmt(float s) { int k = Mathf.Max(0, Mathf.CeilToInt(s)); return k / 60 + ":" + (k % 60).ToString("00"); }

        static int Seed(int L, int cp) { return L * 1000000 + cp * 100000 + 1 + Random.Range(0, 99990); }

        // ---------- entering ----------
        public static void Enter(string id, bool fromCp)
        {
            int i = A.List.FindIndex(d => d.id == id); if (i < 0) return; var d0 = A.List[i]; var H = AldaraHero.I;
            if (!AldaraSave.Ready || !H.alive) return; if (A.Active) { AldaraHud.Banner("Leave this place first"); return; }
            if (H.lvl < RAID_LVL_MIN) { AldaraHud.Banner(d0.name + " requires level " + RAID_LVL_MIN); return; }
            int c = fromCp ? Cp(id) : 0; if (!fromCp) Prog(id)["cp"] = 0;
            int lv = Mathf.Max(d0.rec, H.lvl); A.Start(i, Seed(lv, c));
        }
        /// raidInit: called by AldaraDungeon.Start in place of the usual monsters
        public static void Init(A.Def d, int i, int sd)
        {
            int Ld = sd / 1000000, c = sd / 100000 % 10; if (!(Ld >= 1 && Ld <= 999) || c >= RENC.Length) { Ld = Mathf.Max(d.rec, AldaraHero.I.lvl); c = 0; }
            On = true; def = d; idx = i; L = Ld; cp = c; seed = sd; E = null; done = new bool[RENC.Length]; for (int s = 0; s < RENC.Length; s++) done[s] = s < c;
            wipeT = deadT = t = 0; won = chest = secret = wiping = false; lastPad = -1; later.Clear();
            var R = new AldaraRng(sd % 2147483647 + 913);
            for (int s = c; s < RENC.Length; s++) { var e = RENC[s]; if (e.mobs == null) continue; foreach (var m in e.mobs) { var st = Stats((string)m[0]); float a = R.Next() * 6.283f, dd = R.Next() * 30; SpawnLocal((string)m[0], (float)m[1] + Mathf.Cos(a) * dd, (float)m[2] + Mathf.Sin(a) * dd, s, st, null, false, false, false); } }
            for (int g = 0; g < c; g++) OpenGate(g);
            A.open = c; for (int s = 0; s <= c && s < A.DM.seen.Length; s++) A.DM.seen[s] = true;
            AldaraChat.Sys(c > 0 ? "Raid checkpoint: " + d.encNames[c] + ". Raid level " + L + "." : "You enter " + d.name + ". Raid level " + L + ". Wipes return the party to the last checkpoint.", "dun");
            var en = RENC[c].entry; var P = AldaraPlayer.I; P.x = en.x; P.y = en.y; A.DM.safe = en; A.DM.psec = c;
            A.FlowNow();
            Prog(d.id)["cp"] = c; AldaraSave.Dirty();
            AldaraHud.Banner(d.name + (c > 0 ? ": checkpoint, " + d.encNames[c] : ": " + d.encNames[0]));
        }
        public static void OnExit()
        {
            if (!On) return; On = false; E = null; later.Clear(); var H = AldaraHero.I;
            if (!H.alive) H.Rise(1);
        }

        // ---------- enemies ----------
        public struct St { public float hp, atk; }
        static St Stats(string k) { var D = RMOB[k]; int n = 1; float pm = D.boss ? 1 + 0.8f * (n - 1) : D.elite ? 1 + 0.5f * (n - 1) : 1 + 0.3f * (n - 1); return new St { hp = Mathf.Round(LvsAtk(L) * D.hits * pm), atk = Mathf.Max(1, Mathf.Round(LvsHp(L) / D.die)) }; }
        static AldaraMonsters.Mon SpawnLocal(string k, float x, float y, int s, St st, string tag, bool imm, bool ag, bool fx)
        {
            var D = RMOB[k]; var sc = A.DunScale(9); float mul = D.boss ? 40 : D.elite ? 3 : 0.6f;
            var m = new AldaraMonsters.Mon
            {
                def = MonsterBook.Load().Get(D.name), name = D.name, color = D.color, r = D.r, hp = st.hp, maxHp = st.hp, atk = st.atk, xp = Mathf.Round(sc.xp * mul), gold = Mathf.Round(sc.gold * mul), tier = D.boss ? 6 : 5,
                dun = true, room = s, x = x, y = y, homeX = x, homeY = y, spd = D.spd > 0 ? D.spd : 0.55f, lvl = L, bolt = D.bolt, boss = D.boss, mech = D.mech, mt = 5, mt2 = 9, atkCd = 1, rk = k, rTag = tag, rImm = imm, aggroT = ag ? 40 : 0, lvs = true
            };
            if (m.def == null) return null;
            A.Place(m); AldaraMonsters.I.all.Add(m); if (fx) AldaraFx.Burst(m.x, m.y, D.boss ? 30 : 12, AldaraRules.Hex(D.color));
            return m;
        }
        static AldaraMonsters.Mon Spawn(string k, float x, float y, int s, string tag = null, bool imm = false) { return SpawnLocal(k, Mathf.Round(x), Mathf.Round(y), s, Stats(k), tag, imm, true, true); }
        static Vector2 Jit(Vector2 p, float r) { float a = Random.value * 6.283f, d = Random.value * r; return new Vector2(p.x + Mathf.Cos(a) * d, p.y + Mathf.Sin(a) * d); }
        static void Wave(EState e, params string[] list)
        {
            var C = RENC[e.s]; var sp = C.spawns ?? new[] { C.entry }; int i = Random.Range(0, sp.Length);
            if (Alive(e.s, m => !m.boss && m.rTag == null) > 12) return;
            foreach (var k in list) { var p = Jit(sp[i++ % sp.Length], 70); Spawn(k, p.x, p.y, e.s); }
        }
        public static int Alive(int s, System.Func<AldaraMonsters.Mon, bool> pred = null) { int n = 0; foreach (var m in AldaraMonsters.I.all) if (!m.dead && m.room == s && (pred == null || pred(m))) n++; return n; }

        // ---------- gates ----------
        static void OpenGate(int g)
        {
            var D = A.DM; if (D == null || g < 0 || g >= D.gates.Count) return; var G = D.gates[g];
            if (!G.open) { G.open = true; foreach (var k in G.cells) D.gblk[k] = (byte)Mathf.Max(0, D.gblk[k] - 1); if (AldaraDungeonView.I) AldaraDungeonView.I.Shake(3, 0.4f); AldaraFx.Burst((G.x1 + G.x2) / 2, (G.y1 + G.y2) / 2, 24, D.torchCol); }
            if (A.open < g + 1) A.open = g + 1; A.FlowNow();
        }
        static void CloseGate(int g)
        {
            var D = A.DM; if (D == null || g < 0 || g >= D.gates.Count) return; var G = D.gates[g]; if (!G.open) return;
            G.open = false; G.openT = 0; foreach (var k in G.cells) D.gblk[k]++; A.FlowNow();
        }

        // ---------- messages, hazards ----------
        public static string lastMsg; static void Msg(string m, string col = "#f2c46a") { AldaraHud.Banner(m); AldaraChat.Sys(m, "dun"); lastMsg = m; }
        static void Haz(float x, float y, float r, float delay, float dmgF, string col) { AldaraMonsters.I.AddHazard(Mathf.Round(x), Mathf.Round(y), r, delay, Mathf.Round(LvsHp(L) * dmgF), AldaraRules.Hex(col), 0, false, 0, 0); }
        static void RingHaz(float x, float y, float r1, float spd, float w, float dmgF, string col) { AldaraMonsters.I.AddRing(Mathf.Round(x), Mathf.Round(y), 20, r1, spd, w, Mathf.Round(LvsHp(L) * dmgF), AldaraRules.Hex(col)); }
        static void OnPlayers(System.Action<Vector2> f) { var P = AldaraPlayer.I; if (AldaraHero.I.alive && A.Section(P.x, P.y) == E.s) f(new Vector2(P.x, P.y)); }

        // ---------- starting and ending encounters ----------
        static void StartEnc(int s)
        {
            var c = RENC[s]; var e = new EState { type = c.type, s = s, wt = 6 };
            if (s > 0 && SEALS.Contains(c.type)) { CloseGate(s - 1); Pull(s, c.entry); }
            switch (c.type)
            {
                case "plates": e.wipe = true; e.pl = new float[3]; e.occ = new int[3]; e.con = new int[3]; e.tl = c.tl; Msg("The Eclipsed Gate: charge the three plates while the Gloam pour in."); break;
                case "oracle": e.wipe = true; e.oph = "pause"; e.pt = 4; e.wt = 10; Msg("Hall of the Oracles: watch the Oracle Eye, then step on the glyphs in the same order."); break;
                case "levers": e.lv = new float[3]; e.ok = new int[3]; Msg("The Sunken Gallery: pull the three levers hidden in the alcoves to open the way."); break;
                case "twins":
                    e.wipe = true; e.tl = c.tl; e.ch = new int[2]; e.pad = new float[2]; e.vul2 = new float[2]; e.dtt = new float[2]; e.bs = new int[2]; e.wt = 8; e.jt = 9; e.rt = 20;
                    e.b = new[] { Spawn("vesper", c.bpos[0].x, c.bpos[0].y, s, "twin0", true), Spawn("solace", c.bpos[1].x, c.bpos[1].y, s, "twin1", true) };
                    Msg("The Twin Regents: slay each regent's heralds, then stand on its sigil to break its shield. They must fall together."); break;
                case "survive": e.wipe = true; e.tl = c.dur; e.wt = 3; Msg("The Eclipse Bridge: hold the platform until the bridge is yours."); break;
                case "sov":
                    e.wipe = true; e.tl = c.tl; e.need = 6; e.ecl = 40; e.wt = 10; e.kt = 4; e.lt = 11; e.rt = 24;
                    e.boss = Spawn("sovereign", c.boss.x, c.boss.y, s, "sov", true);
                    Msg("Maelthar, the Sunless King awakens. Slay the Emberhelm Templars, carry their Sun Embers to the Sunwell and ignite it."); break;
            }
            E = e;
        }
        static void Pull(int s, Vector2 p)
        {
            var P = AldaraPlayer.I; if (!AldaraHero.I.alive || A.Section(P.x, P.y) == s) return;
            P.x = p.x; P.y = p.y; A.DM.safe = p; AldaraHero.I.target = null; AldaraFx.Burst(p.x, p.y, 20, AldaraRules.Hex("#f2c46a")); AldaraHud.Banner("You are drawn into the encounter");
        }
        static void EncDone(EState e, string msg)
        {
            int s = e.s; var c = RENC[s]; done[s] = true; if (msg != null) AldaraHud.Banner(msg);
            if (c.cpAfter >= 0) { cp = c.cpAfter; Prog(def.id)["cp"] = cp; AldaraSave.Dirty(); AldaraHud.Banner("Checkpoint: " + def.encNames[cp]); }
            OpenGate(s); if (s > 0 && SEALS.Contains(c.type)) OpenGate(s - 1);
            E = null;
        }
        static void Fail(string msg)
        {
            if (wiping) return; Msg(msg, "#ff6a5a"); wiping = true; After(2.2f, () => { if (On) Wipe(Seed(L, cp)); });
        }
        static void Wipe(int sd)
        {
            int i = idx; wiping = true; if (AldaraDungeonView.I) AldaraDungeonView.I.Shake(5, 0.8f); AldaraHud.Banner("Wipe. Returning to the last checkpoint...");
            After(1.4f, () => { if (A.Active) A.Exit("wipe"); var H = AldaraHero.I; if (!H.alive) H.Rise(1); else { H.hp = H.maxHp; H.mana = H.maxMana; } A.Start(i, sd); });
        }

        // ---------- encounters (raidTickEnc) ----------
        static void TickEnc(EState e, float dt)
        {
            var c = RENC[e.s]; var P = AldaraPlayer.I; var H = AldaraHero.I; e.t += dt;
            bool inRoom = H.alive && A.Section(P.x, P.y) == e.s; Vector2 pp = new Vector2(P.x, P.y);
            switch (e.type)
            {
                case "clear": if (Alive(e.s) == 0) EncDone(e, "The way ahead opens."); break;
                case "plates":
                    {
                        if (e.t > e.tl && !wiping) { Fail("The Vault seals itself. The Eclipse takes you."); break; }
                        if (e.ph == 0)
                        {
                            int full = 0;
                            for (int k = 0; k < 3; k++)
                            {
                                var q = c.plates[k]; int np = inRoom && Vector2.Distance(pp, q) < c.pr ? 1 : 0;
                                int ne = AldaraMonsters.I.all.Count(m => !m.dead && m.room == e.s && Vector2.Distance(new Vector2(m.x, m.y), q) < c.pr + m.r * 0.5f); e.occ[k] = np; e.con[k] = ne;
                                if (e.pl[k] < 1)
                                {
                                    if (np > 0 && ne == 0) { e.pl[k] = Mathf.Min(1, e.pl[k] + dt / c.need); if (e.pl[k] >= 1) Msg("A plate is charged. " + e.pl.Count(v => v >= 1) + " of 3."); }
                                    else if (ne > 0) e.pl[k] = Mathf.Max(0, e.pl[k] - dt * 0.025f * ne);
                                }
                                if (e.pl[k] >= 1) full++;
                            }
                            e.wt -= dt; if (e.wt <= 0) { e.wn++; e.wt = 13; var Ls = new List<string> { "thrall", "thrall", "acolyte", "thrall" }; if (e.wn % 2 == 0) Ls.Add("legion"); if (e.wn % 4 == 0) Ls.Add("knight"); Wave(e, Ls.ToArray()); }
                            if (full == 3) { e.ph = 1; e.wt = 16; e.cb = Spawn("colossus", c.boss.x, c.boss.y, e.s, "colossus"); Msg("The plates blaze. The Sealwarden Behemoth rises to seal the vault!"); }
                        }
                        else { e.wt -= dt; if (e.wt <= 0) { e.wt = 18; Wave(e, "thrall", "thrall", "acolyte"); } if (e.cb == null || e.cb.dead) EncDone(e, "The Eclipsed Gate opens. Checkpoint reached."); }
                        break;
                    }
                case "oracle":
                    {
                        e.wt -= dt; if (e.wt <= 0) { e.wt = 15; var Ls = new List<string> { "thrall", "thrall", "acolyte" }; if (e.round > 0) Ls.Add("legion"); if (e.round > 1) Ls.Add("acolyte"); Wave(e, Ls.ToArray()); }
                        e.pt -= dt;
                        if (e.oph == "pause")
                        {
                            if (e.pt <= 0) { int len = c.rounds[e.round]; e.seq.Clear(); for (int k = 0; k < len; k++) { int g; do { g = Random.Range(0, 6); } while (k > 0 && g == e.seq[k - 1]); e.seq.Add(g); } e.step = 0; e.good.Clear(); e.bad = -1; e.oph = "show"; e.pt = 1; e.si = -1; Msg("Round " + (e.round + 1) + " of 3: the Oracle Eye shows " + len + " glyphs."); }
                        }
                        else if (e.oph == "show")
                        {
                            if (e.pt <= 0) { e.si++; if (e.si >= e.seq.Count * 2) { e.oph = "input"; e.pt = 30 + e.seq.Count * 4; e.lit = -1; lastPad = -1; Msg("Now: step on the glyphs in the order you saw."); } else { e.lit = e.si % 2 == 0 ? e.seq[e.si >> 1] : -1; e.pt = e.si % 2 == 0 ? 1.1f : 0.35f; } }
                        }
                        else if (e.oph == "input")
                        {
                            if (inRoom)
                            {
                                int pad = -1; for (int k = 0; k < c.pads.Length; k++) if (Vector2.Distance(pp, c.pads[k]) < c.pr) pad = k;
                                int last = lastPad; lastPad = pad;
                                if (pad >= 0 && pad != last)
                                {
                                    if (pad == e.seq[e.step])
                                    {
                                        e.good.Add(pad); e.step++; e.lit = pad;
                                        if (e.step >= e.seq.Count) { e.round++; if (e.round >= c.rounds.Length) { EncDone(e, "The Oracles fall silent. Checkpoint reached."); return; } Msg("The Oracles accept the offering. Round " + e.round + " complete."); e.oph = "pause"; e.pt = 5; e.lit = -1; Wave(e, "owarden"); }
                                    }
                                    else { e.bad = pad; Msg("Wrong glyph! The Oracles punish you.", "#ff6a5a"); Wave(e, "owarden", "acolyte"); OnPlayers(q => Haz(q.x, q.y, 95, 1.3f, 0.35f, "#9ad8ff")); e.oph = "pause"; e.pt = 5; e.lit = -1; }
                                }
                            }
                            if (e.oph == "input" && e.pt <= 0) { Msg("Too slow. The Oracles grow restless.", "#ff6a5a"); Wave(e, "owarden"); e.oph = "pause"; e.pt = 5; }
                        }
                        break;
                    }
                case "levers":
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            if (e.ok[k] != 0) continue; var q = c.levers[k]; bool on = H.alive && Vector2.Distance(pp, q) < 60;
                            e.lv[k] = on ? Mathf.Min(1, e.lv[k] + dt / 2.5f) : Mathf.Max(0, e.lv[k] - dt * 0.5f);
                            if (e.lv[k] >= 1) { e.ok[k] = 1; Msg("A lever grinds down. " + e.ok.Count(v => v != 0) + " of 3."); foreach (var kk in new[] { "legion", "thrall", "thrall" }) { var p = Jit(q, 110); Spawn(kk, p.x, p.y, e.s); } }
                        }
                        if (e.ok.All(v => v != 0) && Alive(e.s) == 0) EncDone(e, "The Sunken Gallery gate rises.");
                        break;
                    }
                case "twins":
                    {
                        if (e.t > e.tl && !wiping) { Fail("The Twin Regents join as one. Their judgment is absolute."); break; }
                        for (int k = 0; k < 2; k++)
                        {
                            var b = e.b[k]; if (b == null) continue; string tag = "bear" + k; int alive = Alive(e.s, m => m.rTag == tag);
                            if (!b.dead && e.ch[k] == 0 && e.vul2[k] <= 0)
                            {
                                if (e.bs[k] != 0 && alive == 0) { e.ch[k] = 1; e.bs[k] = 0; Msg((k == 1 ? "The dawn" : "The dusk") + " sigil is charged. Stand on it!"); }
                                else if (e.bs[k] == 0 && alive == 0) { e.bs[k] = 1; int n = 2; for (int j = 0; j < n; j++) { var p = Jit(new Vector2(b.x, b.y), 160); Spawn(k == 1 ? "dawn" : "dusk", p.x, p.y, e.s, tag); } }
                            }
                            if (e.ch[k] == 1)
                            {
                                var q = c.pads[k]; bool on = inRoom && Vector2.Distance(pp, q) < 80; e.pad[k] = on ? Mathf.Min(1, e.pad[k] + dt / 3) : Mathf.Max(0, e.pad[k] - dt * 0.4f);
                                if (e.pad[k] >= 1) { e.pad[k] = 0; e.ch[k] = 0; e.vul2[k] = 22; Imm(b, false, true, 3); Msg(Short(b.name) + "'s shield shatters! Damage it now!"); }
                            }
                            if (e.vul2[k] > 0) { e.vul2[k] -= dt; if (e.vul2[k] <= 0 && !b.dead) { Imm(b, true, false, 0); Msg(Short(b.name) + " is shielded again."); } }
                            if (b.dead) { e.dtt[k] += dt; var o = e.b[1 - k]; if (o != null && !o.dead && e.dtt[k] > 12) { e.dtt[k] = 0; Restore(b, 0.5f, true); Msg(Short(o.name) + " restores " + Short(b.name) + "! They must fall together.", "#ff6a5a"); } }
                            else e.dtt[k] = 0;
                        }
                        if (e.b.All(b => b != null && b.dead)) { EncDone(e, "The Twin Regents fall together. Checkpoint reached."); break; }
                        e.jt -= dt; if (e.jt <= 0) { e.jt = 9; OnPlayers(q => Haz(q.x, q.y, 90, 1.3f, 0.4f, e.t % 18 < 9 ? "#c07aff" : "#ffd35a")); }
                        e.rt -= dt; if (e.rt <= 0) { e.rt = 20; foreach (var b in e.b) if (b != null && !b.dead) RingHaz(b.x, b.y, 720, 250, 44, 0.3f, b.color); }
                        e.wt -= dt; if (e.wt <= 0) { e.wt = 17; Wave(e, "thrall", "thrall", "acolyte"); }
                        break;
                    }
                case "survive":
                    {
                        if (e.ph == 0)
                        {
                            e.tl -= dt; e.wt -= dt;
                            if (e.wt <= 0) { e.wt = 12; e.wn++; var Ls = new List<string> { "thrall", "thrall", "thrall", "acolyte" }; if (e.wn % 2 == 0) { Ls.Add("legion"); Ls.Add("legion"); } if (e.wn % 3 == 0) Ls.Add("knight"); Wave(e, Ls.ToArray()); }
                            if (!inRoom && !wiping) { e.away += dt; if (e.away > 6) { Fail("The bridge is lost to the Eclipse."); break; } } else e.away = 0;
                            if (e.tl <= 0) { e.ph = 1; e.wb = Spawn("bwarden", c.warden.x, c.warden.y, e.s, "bw"); Msg("The bridge holds. The Spanwatch Revenant bars the far side!"); }
                        }
                        else if (e.wb == null || e.wb.dead) EncDone(e, "The Eclipse Bridge is yours. Checkpoint reached.");
                        break;
                    }
                case "sov":
                    {
                        if (e.t > e.tl && !wiping) { Fail("Oblivion. Maelthar, the Sunless King swallows the last light."); break; }
                        var b = e.boss; if (b == null) break;
                        if (b.dead) { Won(); E = null; break; }
                        // knights and their embers
                        e.kt -= dt; if (e.kt <= 0) { e.kt = 16; int n = Alive(e.s, m => m.rTag == "knight"); int want = 2 - n; for (int j = 0; j < Mathf.Min(2, want); j++) { var py = c.pylons[Random.Range(0, 4)]; var p = Jit(py, 60); Spawn("knight", p.x, p.y, e.s, "knight"); } }
                        if (inRoom)
                        {
                            int cr = e.carry;
                            for (int j = e.sp.Count - 1; j >= 0; j--) { if (cr < 5 && Vector2.Distance(pp, e.sp[j]) < 46) { cr++; e.sp.RemoveAt(j); } }
                            bool inWell = Vector2.Distance(pp, c.center) < 95, sealedW = Alive(e.s, m => m.rTag == "eward") > 0;
                            if (inWell && cr > 0 && e.vul <= 0 && !sealedW) { e.depT += dt; if (e.depT > 0.25f) { e.depT = 0; cr--; e.dep++; } }
                            e.carry = cr;
                        }
                        if (e.dep >= e.need && e.vul <= 0) { e.dep = 0; e.vul = 26; Imm(b, false, true, 4); Msg("The Sunwell ignites! Maelthar, the Sunless King is exposed!"); }
                        if (e.vul > 0) { e.vul -= dt; if (e.vul <= 0 && e.fs < 0) { Imm(b, true, false, 0); Msg("The Sunwell gutters out. Maelthar is shielded again."); } }
                        if (!e.w50 && b.hp < b.maxHp * 0.5f) { e.w50 = true; for (int j = 0; j < 2; j++) { var p = Jit(c.pylons[j * 2], 40); Spawn("eward", p.x, p.y, e.s, "eward"); } Msg("Maelthar tears the veil! Slay the Veilrenders to unseal the Sunwell.", "#ff6a5a"); }
                        if (e.fs < 0 && b.hp < b.maxHp * 0.1f) { e.fs = 45; Imm(b, false, true, 0); Msg("FINAL STAND! Kill Maelthar before it consumes the vault!", "#ff6a5a"); }
                        if (e.fs > 0) { e.fs -= dt; if (e.fs <= 0 && !wiping) { Fail("The Final Stand fails. Oblivion."); break; } }
                        // the Total Eclipse: stand in a Light Well or be consumed
                        e.ecl -= dt;
                        if (e.ecl < 8 && e.wells.Count == 0) { for (int j = 0; j < 3; j++) { float a = Random.value * 6.283f, d = 180 + Random.value * 260; e.wells.Add(new Vector2(Mathf.Round(c.center.x + Mathf.Cos(a) * d), Mathf.Round(c.center.y + Mathf.Sin(a) * d))); } Msg("Total Eclipse approaches. Get into a Light Well!", "#ff6a5a"); }
                        if (e.ecl <= 0) { Blast(e.wells, c.wr); e.ecl = 46; e.wells.Clear(); }
                        e.lt -= dt; if (e.lt <= 0) { e.lt = 11; OnPlayers(q => Haz(q.x, q.y, 85, 1.4f, 0.38f, "#ffd35a")); }
                        e.rt -= dt; if (e.rt <= 0) { e.rt = 24; RingHaz(b.x, b.y, 760, 240, 46, 0.32f, "#f2c46a"); }
                        e.wt -= dt; if (e.wt <= 0) { e.wt = 20; Wave(e, "thrall", "thrall", "acolyte", "legion"); }
                        break;
                    }
            }
        }
        static string Short(string n) { int i = n.IndexOf(','); return i < 0 ? n : n.Substring(0, i); }
        static void Imm(AldaraMonsters.Mon m, bool v, bool vu, float st) { if (m == null) return; m.rImm = v; m.rVuln = vu; if (st > 0) m.stunT = Mathf.Max(m.stunT, st); AldaraFx.Burst(m.x, m.y, 30, AldaraRules.Hex(v ? "#c07aff" : "#fff0b0")); }
        static void Restore(AldaraMonsters.Mon m, float hp, bool imm) { m.dead = false; m.respawnT = 0; m.hp = Mathf.Round(m.maxHp * hp); m.rImm = imm; m.rVuln = false; m.act = null; AldaraFx.Burst(m.x, m.y, 40, AldaraRules.Hex(m.color ?? "#ffffff")); }
        static void Blast(List<Vector2> wells, float r)
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I; AldaraFx.Burst(P.x, P.y, 30, AldaraRules.Hex("#1a0a2a")); if (AldaraDungeonView.I) AldaraDungeonView.I.Shake(6, 0.6f);
            if (!H.alive) return; bool safe = wells.Any(q => Vector2.Distance(new Vector2(P.x, P.y), q) < r);
            if (safe) { AldaraFx.Text(P.x, P.y - 16 - 30, "Sheltered", AldaraRules.Hex("#fff0b0")); return; }
            H.Damage(Mathf.Round(H.maxHp * 0.95f), 0, -1); AldaraFx.Text(P.x, P.y - 16 - 30, "Consumed by the Eclipse", AldaraRules.Hex("#c07aff"));
        }
        /// embers fall where knights die
        public static void OnKill(AldaraMonsters.Mon m) { m.respawnT = 1e9f; if (E != null && E.type == "sov" && m.rTag == "knight") E.sp.Add(new Vector2(Mathf.Round(m.x), Mathf.Round(m.y))); }

        // ---------- death ----------
        public static void OnDeath()
        {
            deadT = 0; bool inEnc = E != null && E.wipe && E.s == A.DM.psec;
            AldaraHud.Banner(inEnc ? "You have fallen. Wipe incoming..." : "You have fallen. You will rise at the checkpoint.");
        }
        static void Respawn()
        {
            int s = Mathf.Clamp(A.DM.psec, 0, RENC.Length - 1); Vector2 e = RENC[s].entry; bool allOpen = true;
            for (int gi = 0; gi < A.DM.gates.Count; gi++) if (gi < A.DM.psec && !A.DM.gates[gi].open) allOpen = false;
            if (!allOpen) e = A.DM.safe;
            var P = AldaraPlayer.I; P.x = e.x; P.y = e.y; A.DM.safe = e; AldaraHero.I.Rise(1); deadT = 0;
            A.FlowNow(); AldaraFx.Burst(P.x, P.y, 24, AldaraRules.Hex("#f2c46a")); AldaraHud.Banner("You rise again");
        }

        // ---------- victory and the treasury ----------
        static void Won()
        {
            if (won) return; won = true; int s = System.Array.FindIndex(RENC, c => c.type == "sov"); done[s] = true; OpenGate(s);
            var p = Prog(def.id); p["cp"] = 0; p["clears"] = (int)p["clears"] + 1; int secs = Mathf.RoundToInt(Time.time - DStat.t0); if ((int)p["best"] == 0 || secs < (int)p["best"]) p["best"] = secs; AldaraSave.Dirty();
            if (AldaraDungeonView.I) AldaraDungeonView.I.Shake(6, 1); AldaraHud.Banner(def.name + " conquered! Claim your reward in the Treasury."); AldaraChat.Sys(AldaraHero.I.heroName + " and party conquered " + def.name + "!", "dun");
        }
        static void Claim()
        {
            if (chest) return; chest = true; var H = AldaraHero.I; var P = AldaraPlayer.I;
            float xp = Mathf.Round((H.xpNeed > 0 ? H.xpNeed : 10000) * 0.6f), gold = 20000 + L * 420; H.gold += gold;
            for (int k = 0; k < 4; k++) { var it = AldaraItems.RollItem(L, AldaraItems.PickRarity(L, 70, true), 10); if (H.AddLoot(it)) { DStat.loot.Add(it); AldaraFx.Text(P.x, P.y - 16 - 60 - k * 16, it.name + "!", it.Col); } }
            if (Random.value < 0.18f) { var a = AldaraItems.RollMythicArmor(L); if (H.AddLoot(a)) { AldaraHud.Banner("MYTHICAL ARMOR: " + a.name + "!"); AldaraFx.Text(P.x, P.y - 110, a.name + "!", AldaraRules.Hex(AldaraItems.MythSetOf(a).glow)); } }
            if (Random.value < 0.06f) { var w = AldaraItems.RollWings(L, 40); if (H.AddLoot(w)) { AldaraHud.Banner("WINGS DROP: " + w.name + "!"); DStat.loot.Add(w); } }
            var rl = AldaraGear.MaybeDropRelic(9, L, P.x, P.y); if (rl != null) DStat.loot.Add(rl);
            H.vialHp = AldaraItems.VIAL_MAX; H.vialMp = AldaraItems.VIAL_MAX;
            AldaraTree.sp += 3; AldaraTree.spTotal += 3; AldaraHud.Banner("+3 Skill Points (" + def.name + "): press K");
            H.GainXp(xp); DStat.xp = xp; DStat.gold = gold;
            AldaraFx.Text(P.x, P.y - 16 - 40, "+" + xp + " XP  +" + gold + " Gold", AldaraRules.Hex("#e0b64b")); AldaraFx.Burst(P.x, P.y, 50, AldaraRules.Hex("#f2c46a"));
            A.done = true; A.exitT = 40; DStat.Show("done"); AldaraSave.Dirty();
        }
        static void SecretClaim()
        {
            if (secret) return; secret = true; var H = AldaraHero.I; var P = AldaraPlayer.I; float gold = 5000 + L * 80; H.gold += gold;
            var it = AldaraItems.RollItem(L, AldaraItems.PickRarity(L, 55, true), 8); if (H.AddLoot(it)) { DStat.loot.Add(it); AldaraFx.Text(P.x, P.y - 16 - 60, it.name + "!", it.Col); }
            AldaraFx.Text(P.x, P.y - 16 - 40, "Hidden cache! +" + gold + " Gold", AldaraRules.Hex("#e0b64b")); AldaraFx.Burst(P.x, P.y, 30, AldaraRules.Hex("#f2c46a")); AldaraHud.Banner("You found the hidden Eclipse cache"); AldaraSave.Dirty();
        }

        // ---------- every frame (raidTick / raidLeaderTick / raidLocalTick) ----------
        void Awake() { I = this; }
        /// called by AldaraDungeon.Update
        public static void Tick(float dt)
        {
            for (int i = 0; i < later.Count; i++) if (Time.time >= later[i].at) { var f = later[i].f; later.RemoveAt(i); f(); return; }
            if (!On || !A.Active) return; t += dt; var H = AldaraHero.I; var P = AldaraPlayer.I;
            if (!H.alive)
            {
                deadT += dt; bool inEnc = E != null && E.wipe && E.s == A.DM.psec;
                if (!inEnc && deadT > 5) Respawn();
            }
            // a wipe: everyone in the encounter has fallen
            if (E != null && E.wipe && !H.alive) { wipeT += dt; if (wipeT > 2.5f && !wiping) { wiping = true; Msg("Wipe. The party returns to the last checkpoint.", "#ff6a5a"); After(1.5f, () => { if (On) Wipe(Seed(L, cp)); }); } }
            else wipeT = 0;
            // the encounter of the frontier section starts when you walk into it
            if (E == null && !won)
            {
                int s = A.open; if (s < RENC.Length && !done[s] && H.alive && A.Section(P.x, P.y) == s) { var c = RENC[s]; if (!c.hasTrig || Vector2.Distance(new Vector2(P.x, P.y), new Vector2(c.trig.x, c.trig.y)) < c.trig.z) StartEnc(s); }
            }
            if (E != null && !wiping) TickEnc(E, dt);
            // the treasury chest and the hidden cache
            var tr = RENC[RENC.Length - 1]; if (won && !chest && H.alive && Vector2.Distance(new Vector2(P.x, P.y), tr.chest) < 95) Claim();
            var cs = RENC[1]; if (cs.hasSecret && !secret && H.alive && Vector2.Distance(new Vector2(P.x, P.y), cs.secret) < 70) SecretClaim();
        }
        /// hitMonster: immune bosses take nothing, exposed ones take 30% more
        public static bool Immune(AldaraMonsters.Mon m) { if (!On || !m.rImm) return false; if (Time.time > m.immT) { m.immT = Time.time + 0.6f; AldaraFx.Text(m.x, m.y - m.r - 18, "Immune", AldaraRules.Hex("#c07aff")); } return true; }
        public static float TrapDmg(float k) { return Mathf.Round(LvsHp(L) * 0.12f * k); }

        // ---------- the HUD text (raidHudUpdate) ----------
        public class Bar { public string label, val, cls; public float f; }
        public static void Hud(out string title, out string sub, out string obj, List<Bar> bars, out string warn, out string foot, out List<int> seqDots)
        {
            title = sub = obj = warn = ""; seqDots = null; var C = RENC; var H = AldaraHero.I; int sec = Mathf.Clamp(A.DM.psec, 0, C.Length - 1);
            System.Action<string, float, string, string> bar = (l, f, v, c) => bars.Add(new Bar { label = l, f = Mathf.Clamp01(f), val = v, cls = c });
            string[] ROM = { "I", "II", "III" };
            if (won) { title = def.encNames[C.Length - 1]; obj = chest ? "The vault is plundered. Returning soon." : "Maelthar is slain. Claim your reward from the chest in the Treasury."; }
            else if (E != null)
            {
                var c = C[E.s]; title = def.encNames[E.s]; sub = "Encounter " + (E.s + 1) + " of " + C.Length;
                switch (E.type)
                {
                    case "clear": obj = "Clear the Gloam from the islands. Enemies left: " + Alive(E.s); break;
                    case "plates":
                        if (E.ph == 0) { obj = "Stand on the plates to charge them. Enemies on a plate drain it."; for (int k = 0; k < 3; k++) bar("Plate " + ROM[k] + (E.occ[k] != 0 ? " (held)" : ""), E.pl[k], Mathf.RoundToInt(E.pl[k] * 100) + "%", E.con[k] != 0 ? "red" : ""); }
                        else { obj = "Destroy the Sealwarden Behemoth!"; if (E.cb != null) bar("Behemoth", E.cb.hp / E.cb.maxHp, Mathf.RoundToInt(100 * E.cb.hp / E.cb.maxHp) + "%", ""); }
                        bar("Vault seals", 1 - E.t / E.tl, Fmt(E.tl - E.t), "red"); break;
                    case "oracle":
                        obj = E.oph == "show" ? "Watch the Oracle Eye: remember the glyphs." : E.oph == "input" ? "Step on the glyphs in order: " + (E.step + 1) + " of " + E.seq.Count : "The Oracles gather...";
                        bar("Round", E.round / (float)c.rounds.Length, (E.round + 1) + " / " + c.rounds.Length, "");
                        if (E.oph == "input") { seqDots = new List<int>(); for (int k = 0; k < E.seq.Count; k++) seqDots.Add(k < E.step ? 1 : 0); bar("Time", E.pt / (30 + E.seq.Count * 4), Fmt(E.pt), "red"); }
                        break;
                    case "levers":
                        obj = "Pull the three levers in the alcoves (stand at each one), then clear the gallery.";
                        for (int k = 0; k < 3; k++) bar("Lever " + ROM[k], E.ok[k] != 0 ? 1 : E.lv[k], E.ok[k] != 0 ? "Pulled" : Mathf.RoundToInt(E.lv[k] * 100) + "%", "");
                        if (E.ok.All(v => v != 0)) obj = "Clear the gallery. Enemies left: " + Alive(E.s); break;
                    case "twins":
                        obj = "Kill a regent's heralds, stand on its sigil, then damage it. Both must fall within 12 seconds.";
                        for (int k = 0; k < 2; k++) { var m = E.b[k]; if (m == null) continue; bar(Short(m.name), m.dead ? 0 : m.hp / m.maxHp, m.dead ? "Fallen" : E.vul2[k] > 0 ? "Open " + Mathf.CeilToInt(E.vul2[k]) + "s" : E.ch[k] != 0 ? "Sigil " + Mathf.RoundToInt(E.pad[k] * 100) + "%" : "Shielded", m.rImm ? "imm" : ""); }
                        bar("Enrage", 1 - E.t / E.tl, Fmt(E.tl - E.t), "red"); break;
                    case "survive":
                        if (E.ph == 0) { obj = "Hold the platform against the Gloam!"; bar("Hold", 1 - E.tl / c.dur, Fmt(E.tl), ""); }
                        else { obj = "Destroy the Spanwatch Revenant!"; if (E.wb != null) bar("Revenant", E.wb.hp / E.wb.maxHp, Mathf.RoundToInt(100 * E.wb.hp / E.wb.maxHp) + "%", ""); }
                        break;
                    case "sov":
                        {
                            var m = E.boss; int my = E.carry;
                            obj = E.fs > 0 ? "FINAL STAND: kill Maelthar!" : E.vul > 0 ? "Maelthar is exposed. Damage it!" : "Kill Emberhelm Templars, carry their Sun Embers (up to 5) to the Sunwell.";
                            if (m != null) bar("Maelthar", m.hp / m.maxHp, Mathf.RoundToInt(100 * m.hp / m.maxHp) + "%", m.rImm ? "imm" : "");
                            if (E.vul > 0) bar("Exposed", E.vul / 26, Mathf.CeilToInt(E.vul) + "s", ""); else bar("Sunwell", E.dep / (float)E.need, E.dep + " / " + E.need, "");
                            bar("Your embers", my / 5f, my + " / 5", "");
                            if (E.fs > 0) bar("Final Stand", E.fs / 45, Mathf.CeilToInt(E.fs) + "s", "red"); else bar("Total Eclipse", 1 - Mathf.Max(0, E.ecl) / 46, Mathf.CeilToInt(Mathf.Max(0, E.ecl)) + "s", "red");
                            if (E.ecl < 8 && E.ecl > 0) warn = "TOTAL ECLIPSE: GET INTO A LIGHT WELL!";
                            if (E.w50 && Alive(E.s, x => x.rTag == "eward") > 0 && warn == "") warn = "The Sunwell is sealed: slay the Veilrenders!";
                            break;
                        }
                    case "treasury": obj = "Claim the treasure."; break;
                }
            }
            else { title = def.encNames[sec]; sub = done[sec] ? "Cleared" : "Section " + (sec + 1) + " of " + C.Length; obj = done[sec] ? "Press on to " + def.encNames[Mathf.Min(C.Length - 1, sec + 1)] + "." : "Move forward to begin."; }
            if (!H.alive) { bool inEnc = E != null && E.wipe && E.s == A.DM.psec; warn = inEnc ? "You have fallen..." : "You have fallen. Rising in " + Mathf.Max(0, Mathf.CeilToInt(5 - deadT)) + "s"; }
            foot = "Checkpoint: " + def.encNames[cp] + "  ·  Raid level " + L;
        }
        public static string Title { get { if (!On) return ""; if (won) return def.encNames[RENC.Length - 1]; if (E != null) return def.encNames[E.s]; return def.encNames[Mathf.Clamp(A.DM.psec, 0, RENC.Length - 1)]; } }
    }
}
