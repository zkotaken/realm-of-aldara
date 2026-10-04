using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // The browser's dungeons (dunBuild / startDungeon / exitDungeon / updateDungeon / dunPlayerPost / dunMonStep /
    // updateTraps / bossMech / dungeonOnKill / dungeonCleared). Each map is the browser's own, exported as it builds it:
    // its kinds and distance fields at 8 px, its 32 px nav grid with sections, gates and rune links. A dungeon is its own
    // pixel space (0..W, 0..H), shown far west of the world (AldaraWorld.Dun), so all the world rules switch to the map.
    public class AldaraDungeon : MonoBehaviour
    {
        public static AldaraDungeon I;
        public const int KW = 0, KF = 1, KP = 2, KB = 3, KI = 4, KS = 5, KT = 6, KX = 7, DCS = 8, NCS = 32;
        public static int DUN_LEVEL = 20;

        // ---------- the list (DUNGEONS) ----------
        public class MobDef { public string name, color, bolt; public float r, spd = -1; public bool ranged, tank, slowHit; public string mech; }
        public class Def { public int i; public string id, name, accent, desc; public int rec, sections; public bool deep, hidden, raid, layout; public MobDef[] mobs; public MobDef boss; }
        static List<Def> list;
        public static List<Def> List { get { if (list == null) Load(); return list; } }
        static void Load()
        {
            var j = JObject.Parse(Resources.Load<TextAsset>("Dungeons/dungeons").text); DUN_LEVEL = (int)j["DUN_LEVEL"];
            list = new List<Def>();
            foreach (var d in j["list"])
            {
                var D = new Def { i = (int)d["i"], id = (string)d["id"], name = (string)d["name"], accent = (string)d["accent"], desc = (string)d["desc"], rec = (int)d["rec"], deep = (bool)d["deep"], hidden = (bool)d["hidden"], raid = (bool)d["raid"], layout = (bool)d["layout"], sections = (int)d["sections"] };
                D.mobs = d["mobs"] is JArray ma ? ma.Select(Mob).ToArray() : new MobDef[0]; D.boss = d["boss"] is JObject bo ? Mob(bo) : null; list.Add(D);
            }
        }
        static MobDef Mob(JToken t) { return new MobDef { name = (string)t["name"], color = (string)t["color"] ?? "#ffffff", bolt = (string)t["bolt"], r = (float)t["r"], spd = t["spd"] != null ? (float)t["spd"] : -1, ranged = t["ranged"] != null && (bool)t["ranged"], tank = t["tank"] != null && (bool)t["tank"], slowHit = t["slowHit"] != null && (bool)t["slowHit"], mech = (string)t["mech"] }; }
        public static bool Ready(Def d) { return d.layout && Resources.Load<TextAsset>("Dungeons/" + d.id + "/meta") != null; }

        // ---------- the map (DM) ----------
        public class Gate { public float x1, y1, x2, y2, openT; public bool open; public int[] cells; public Vector2[] pts; }
        public class Trap
        {
            public string type; public float x, y, w, h, ang, per = 1, off, ax, ay, bx, by, r, spd, amp, len, on, wid, n, a0, br;
            public Vector2[] pts; public float bw;   // phase bridge
            public float cd, ph, a, rot, acc, crack, broken, cbx, cby, vel; public int st; public bool rolling, warn, was, hit, safe, solid;
        }
        public class Rune { public Vector2 a, b; public float r; }
        public class Torch { public float x, y, yb, ph; }
        public class LightDef { public float x, y, r, i, ph; public string col; public bool fl; }
        public class Prop { public string type, m, col; public float x, y, blk, s; }
        public class Map
        {
            public string id; public int W, H, gw, gh, nw, nh; public string[] names; public Vector2 start, bossAt; public List<Vector3>[] spawns;
            public byte[] K, free, gblk; public sbyte[] sec; public short[] C, Wsd, Psd; public int[] link;
            public List<Gate> gates = new List<Gate>(); public List<Trap> traps = new List<Trap>(); public List<Rune> runes = new List<Rune>(); public List<Torch> torches = new List<Torch>();
            public List<LightDef> lights = new List<LightDef>(); public List<Prop> props = new List<Prop>(); public JObject theme, models; public int[] tiles;
            public Color torchCol; public string gateStyle, bridgeStyle; public bool lava; public int baseKind; public float[] amb, grade; public string mm, wx;
            public short[] flow; public int[] q;
            public float t, vx, vy, fallT, flowT; public int psec, runeLock = -1; public bool[] seen; public Vector2 safe;
            public float stT, stTx, stTy; public Vector2 stWp; public bool hasSteer;
        }
        public static Map DM;
        public static Def def; public static int idx, open; public static bool done; public static float exitT; static Vector2 ret;
        public static bool Active { get { return DM != null; } }
        public static Vector2 Ret { get { return ret; } }
        static List<AldaraMonsters.Mon> worldMons;

        static T[] Arr<T>(string res, System.Func<byte[], T[]> f) { var ta = Resources.Load<TextAsset>(res); return ta ? f(ta.bytes) : null; }
        static short[] S16(byte[] b) { var o = new short[b.Length / 2]; System.Buffer.BlockCopy(b, 0, o, 0, b.Length); return o; }
        static Map Build(Def d)
        {
            string R = "Dungeons/" + d.id + "/"; var j = JObject.Parse(Resources.Load<TextAsset>(R + "meta").text);
            var M = new Map { id = d.id, W = (int)j["W"], H = (int)j["H"], gw = (int)j["gw"], gh = (int)j["gh"], nw = (int)j["nw"], nh = (int)j["nh"] };
            M.names = j["names"].ToObject<string[]>(); M.start = V(j["start"]); M.bossAt = V(j["bossAt"]);
            M.spawns = j["spawns"].Select(a => a.Select(s => new Vector3((float)s["x"], (float)s["y"], (float)s["r"])).ToList()).ToArray();
            M.K = Resources.Load<TextAsset>(R + "K").bytes; M.free = Resources.Load<TextAsset>(R + "free").bytes; M.gblk = (byte[])Resources.Load<TextAsset>(R + "gblk").bytes.Clone();
            var sb = Resources.Load<TextAsset>(R + "sec").bytes; M.sec = new sbyte[sb.Length]; System.Buffer.BlockCopy(sb, 0, M.sec, 0, sb.Length);
            M.C = S16(Resources.Load<TextAsset>(R + "C").bytes); M.Wsd = S16(Resources.Load<TextAsset>(R + "Wsd").bytes); M.Psd = S16(Resources.Load<TextAsset>(R + "Psd").bytes);
            M.link = Enumerable.Repeat(-1, M.nw * M.nh).ToArray(); foreach (var l in j["links"]) M.link[(int)l[0]] = (int)l[1];
            foreach (var g in j["gates"]) M.gates.Add(new Gate { x1 = (float)g["x1"], y1 = (float)g["y1"], x2 = (float)g["x2"], y2 = (float)g["y2"], cells = g["cells"].ToObject<int[]>(), pts = g["pts"].Select(V2).ToArray() });
            foreach (var t in j["traps"])
            {
                var tj = (JObject)t.DeepClone(); tj.Remove("pts"); var T = tj.ToObject<Trap>(); if (t["pts"] is JArray pa) T.pts = pa.Select(V2).ToArray(); if (t["w"] != null) T.bw = (float)t["w"];
                if (T.type == "phase") T.w = T.bw; M.traps.Add(T);
            }
            foreach (var r in j["runes"]) M.runes.Add(new Rune { a = V(r["a"]), b = V(r["b"]), r = (float)r["r"] });
            foreach (var t in j["torches"]) M.torches.Add(new Torch { x = (float)t["x"], y = (float)t["y"], yb = (float)t["yb"], ph = (float)t["ph"] });
            foreach (var l in j["lights"]) M.lights.Add(new LightDef { x = (float)l["x"], y = (float)l["y"], r = (float)l["r"], i = (float)l["i"], col = (string)l["col"], fl = l["fl"] != null && l["fl"].Type != JTokenType.Null && (int)l["fl"] != 0, ph = l["ph"] != null && l["ph"].Type != JTokenType.Null ? (float)l["ph"] : 0 });
            foreach (var p in j["props"]) M.props.Add(new Prop { type = (string)p["type"], m = (string)p["m"], col = (string)p["col"], x = (float)p["x"], y = (float)p["y"], blk = (float)p["blk"], s = (float)p["s"] });
            M.theme = (JObject)j["theme"]; M.models = (JObject)j["models"]; M.tiles = j["tiles"].ToObject<int[]>();
            M.torchCol = AldaraRules.Hex((string)M.theme["torch"]); M.gateStyle = (string)M.theme["gate"]; M.bridgeStyle = (string)M.theme["bridge"]; M.lava = (bool)M.theme["lava"]; M.baseKind = (int)M.theme["base"];
            M.amb = M.theme["amb"].ToObject<float[]>(); M.grade = M.theme["grade"].ToObject<float[]>(); M.mm = (string)M.theme["mm"]; M.wx = (string)M.theme["wx"];
            M.flow = new short[M.nw * M.nh]; M.q = new int[M.nw * M.nh]; M.seen = new bool[M.names.Length]; M.seen[0] = true;
            return M;
        }
        static Vector2 V(JToken t) { return new Vector2((float)t["x"], (float)t["y"]); }
        static Vector2 V2(JToken t) { return new Vector2((float)t[0], (float)t[1]); }

        // ---------- fields ----------
        static float SdAt(short[] F, float x, float y)
        {
            var D = DM; float fx = x / DCS - 0.5f, fy = y / DCS - 0.5f; int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fy); float u = fx - i, v = fy - j;
            if (i < 0) i = 0; if (j < 0) j = 0; if (i > D.gw - 2) i = D.gw - 2; if (j > D.gh - 2) j = D.gh - 2; int k = j * D.gw + i;
            float a = F[k], b = F[k + 1], c = F[k + D.gw], d = F[k + D.gw + 1]; return (a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v) / 8f;
        }
        public static float Clr(float x, float y) { return SdAt(DM.C, x, y); }
        public static float WallD(float x, float y) { return SdAt(DM.Wsd, x, y); }
        public static float DropD(float x, float y) { return SdAt(DM.Psd, x, y); }
        public static int Kind(float x, float y) { int i = Mathf.FloorToInt(x / DCS), j = Mathf.FloorToInt(y / DCS); if (i < 0 || j < 0 || i >= DM.gw || j >= DM.gh) return DM.baseKind; return DM.K[j * DM.gw + i]; }
        /// a point inside a wall, a drop or a prop (AldaraWorld.BlockedAt in a dungeon)
        public static bool Blocked(float x, float y) { return Clr(x, y) < 0; }
        public static bool ShotWall(float x, float y, float ox, float oy) { return WallD(x, y) < 0 || SegHitsGate(ox, oy, x, y); }
        public static int NavIdx(float x, float y) { var D = DM; return Mathf.Clamp(Mathf.FloorToInt(y / NCS), 0, D.nh - 1) * D.nw + Mathf.Clamp(Mathf.FloorToInt(x / NCS), 0, D.nw - 1); }
        public static int NearFree(float x, float y)
        {
            var D = DM; int k = NavIdx(x, y); if (D.free[k] != 0) return k; int best = -1; float bd = 1e9f; int i0 = Mathf.FloorToInt(x / NCS), j0 = Mathf.FloorToInt(y / NCS);
            for (int dj = -3; dj <= 3; dj++) for (int di = -3; di <= 3; di++)
                {
                    int i = i0 + di, j = j0 + dj; if (i < 0 || j < 0 || i >= D.nw || j >= D.nh) continue; int kk = j * D.nw + i; if (D.free[kk] == 0) continue;
                    float d = Vector2.Distance(new Vector2((i + 0.5f) * NCS, (j + 0.5f) * NCS), new Vector2(x, y)); if (d < bd) { bd = d; best = kk; }
                }
            return best;
        }
        static Vector2 NavC(int k) { int i = k % DM.nw; return new Vector2((i + 0.5f) * NCS, ((k - i) / DM.nw + 0.5f) * NCS); }
        public static int Section(float x, float y)
        {
            var D = DM; int k = NavIdx(x, y); if (D.sec[k] >= 0) return D.sec[k];
            int i0 = Mathf.FloorToInt(x / NCS), j0 = Mathf.FloorToInt(y / NCS);
            for (int r = 1; r <= 3; r++) for (int dj = -r; dj <= r; dj++) for (int di = -r; di <= r; di++)
                    { int i = i0 + di, j = j0 + dj; if (i < 0 || j < 0 || i >= D.nw || j >= D.nh) continue; int s = D.sec[j * D.nw + i]; if (s >= 0) return s; }
            return -1;
        }
        public static bool SegHitsGate(float ax, float ay, float bx, float by)
        {
            foreach (var g in DM.gates)
            {
                if (g.open) continue; float d = (bx - ax) * (g.y2 - g.y1) - (by - ay) * (g.x2 - g.x1); if (Mathf.Abs(d) < 1e-6f) continue;
                float t = ((g.x1 - ax) * (g.y2 - g.y1) - (g.y1 - ay) * (g.x2 - g.x1)) / d, u = ((g.x1 - ax) * (by - ay) - (g.y1 - ay) * (bx - ax)) / d; if (t >= 0 && t <= 1 && u >= 0 && u <= 1) return true;
            }
            return false;
        }
        /// clear line? walk needs room to walk (no walls, drops or props), a shot only needs no walls
        public static bool LOS(float ax, float ay, float bx, float by, bool walk, float clr = 10)
        {
            float L = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)); int n = Mathf.CeilToInt(L / 12); var F = walk ? DM.C : DM.Wsd; float c = walk ? clr : -2;
            for (int k = 1; k < n; k++) { float u = k / (float)n; if (SdAt(F, ax + (bx - ax) * u, ay + (by - ay) * u) < c) return false; }
            return !SegHitsGate(ax, ay, bx, by);
        }
        // ---------- the flow field from the player ----------
        static void FlowUpdate(bool force)
        {
            var D = DM; if (!force && D.t - D.flowT < 0.2f) return; D.flowT = D.t; var P = AldaraPlayer.I;
            int nw = D.nw, n = nw * D.nh; var F = D.flow; var Q = D.q; for (int i = 0; i < n; i++) F[i] = -1; int s = NearFree(P.x, P.y); if (s < 0) return;
            int qh = 0, qt = 0; F[s] = 0; Q[qt++] = s;
            while (qh < qt)
            {
                int k = Q[qh++], i = k % nw; short d = (short)(F[k] + 1);
                for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                    {
                        if (di == 0 && dj == 0) continue; int ii = i + di; if (ii < 0 || ii >= nw) continue; int nk = k + dj * nw + di; if (nk < 0 || nk >= n) continue;
                        if (F[nk] >= 0 || D.free[nk] == 0 || D.gblk[nk] != 0) continue; if (di != 0 && dj != 0 && (D.free[k + di] == 0 || D.free[k + dj * nw] == 0)) continue; F[nk] = d; Q[qt++] = nk;
                    }
                int l = D.link[k]; if (l >= 0 && F[l] < 0 && D.gblk[l] == 0) { F[l] = d; Q[qt++] = l; }
            }
        }
        public static int FlowAt(float x, float y) { int k = NearFree(x, y); return k < 0 ? -1 : DM.flow[k]; }
        static int Downhill(int k)
        {
            var D = DM; int nw = D.nw, n = nw * D.nh, i = k % nw, best = -1; float bd = D.flow[k] < 0 ? 1e9f : D.flow[k];
            for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue; int ii = i + di; if (ii < 0 || ii >= nw) continue; int nk = k + dj * nw + di; if (nk < 0 || nk >= n) continue;
                    int f = D.flow[nk]; if (f < 0 || f >= bd) continue; if (di != 0 && dj != 0 && (D.free[k + di] == 0 || D.free[k + dj * nw] == 0)) continue; bd = f; best = nk;
                }
            int l = D.link[k]; if (l >= 0 && D.flow[l] >= 0 && D.flow[l] < bd) best = l; return best;
        }
        /// dunSteer: which way the player walks to reach (tx, ty)
        public static float Steer(float tx, float ty)
        {
            var D = DM; var P = AldaraPlayer.I; if (LOS(P.x, P.y, tx, ty, true, 13)) return Mathf.Atan2(ty - P.y, tx - P.x);
            if (D.hasSteer && D.t - D.stT < 0.15f && Vector2.Distance(new Vector2(D.stTx, D.stTy), new Vector2(tx, ty)) < 40) return Mathf.Atan2(D.stWp.y - P.y, D.stWp.x - P.x);
            int k = NearFree(tx, ty); if (k < 0 || D.flow[k] < 0) return Mathf.Atan2(ty - P.y, tx - P.x);
            var path = new List<int> { k }; for (int s = 0; s < 2000 && D.flow[k] > 0; s++) { int nk = Downhill(k); if (nk < 0) break; k = nk; path.Add(k); }
            path.Reverse(); var wp = NavC(path[Mathf.Min(1, path.Count - 1)]);
            for (int s = Mathf.Min(path.Count - 1, 14); s >= 1; s--) { var c = NavC(path[s]); if (Vector2.Distance(c, NavC(path[s - 1])) > NCS * 2) continue; if (LOS(P.x, P.y, c.x, c.y, true, 13)) { wp = c; break; } }
            for (int s = 0; s < Mathf.Min(path.Count - 1, 20); s++) { Vector2 a = NavC(path[s]), b = NavC(path[s + 1]); if (Vector2.Distance(a, b) > NCS * 2) { if (LOS(P.x, P.y, a.x, a.y, true, 13)) wp = a; break; } }
            D.hasSteer = true; D.stT = D.t; D.stTx = tx; D.stTy = ty; D.stWp = wp; return Mathf.Atan2(wp.y - P.y, wp.x - P.x);
        }
        /// dunPush: out of walls, drops, props and shut gates
        public static void Push(ref float x, ref float y, float r, float ox, float oy, bool hasO = true)
        {
            var D = DM;
            for (int it = 0; it < 3; it++)
            {
                float c = Clr(x, y); if (c >= r) break;
                float gx = Clr(x + 3, y) - Clr(x - 3, y), gy = Clr(x, y + 3) - Clr(x, y - 3), gl = Mathf.Sqrt(gx * gx + gy * gy);
                if (gl < 1e-4f) break; x += gx / gl * (r - c + 0.5f); y += gy / gl * (r - c + 0.5f);
            }
            foreach (var g in D.gates)
            {
                if (g.open) continue; float dx = g.x2 - g.x1, dy = g.y2 - g.y1, l2 = dx * dx + dy * dy; float t = ((x - g.x1) * dx + (y - g.y1) * dy) / l2; t = Mathf.Clamp01(t);
                float px = g.x1 + dx * t, py = g.y1 + dy * t, d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py)), need = r + 10; if (d >= need) continue;
                float nx = -dy, ny = dx, nl = Mathf.Sqrt(nx * nx + ny * ny); nx /= nl; ny /= nl; float rf = hasO ? (ox - g.x1) * nx + (oy - g.y1) * ny : (x - g.x1) * nx + (y - g.y1) * ny, sg = rf >= 0 ? 1 : -1;
                x = px + nx * sg * need; y = py + ny * sg * need;
            }
            x = Mathf.Clamp(x, DCS * 3, D.W - DCS * 3); y = Mathf.Clamp(y, DCS * 3, D.H - DCS * 3);
            if (hasO && Clr(x, y) < r * 0.35f && Clr(ox, oy) > Clr(x, y)) { x = ox; y = oy; }
        }
        public static void PushMon(AldaraMonsters.Mon m, float ox, float oy) { Push(ref m.x, ref m.y, Mathf.Min(m.r, 22) * 0.8f, ox, oy); }
        public static void Place(AldaraMonsters.Mon m) { float r = Mathf.Min(m.r, 22); if (Clr(m.x, m.y) >= r) return; int k = NearFree(m.x, m.y); if (k >= 0) { var c = NavC(k); m.x = c.x; m.y = c.y; } }
        public static bool SpotOk(AldaraMonsters.Mon m, float x, float y) { return Clr(x, y) > Mathf.Min(m.r, 22) * 0.7f; }
        /// dunShove: the player knocked along, stopping at walls
        public static void Shove(float ang, float dist)
        {
            var P = AldaraPlayer.I; float ox = P.x, oy = P.y;
            for (int s = 0; s < 6; s++) { float px = P.x, py = P.y; P.x += Mathf.Cos(ang) * dist / 6; P.y += Mathf.Sin(ang) * dist / 6; Push(ref P.x, ref P.y, 14, px, py); }
            if (!(Clr(P.x, P.y) > 0)) { P.x = ox; P.y = oy; }
        }
        /// monsters: straight at you when the way is clear, otherwise down the flow field
        public static void MonStep(AldaraMonsters.Mon m, float step)
        {
            var P = AldaraPlayer.I; float r = Mathf.Min(m.r, 22) * 0.8f, ox = m.x, oy = m.y;
            if (m.losT <= 0 || DM.t - m.losT > 0.25f) { m.losT = DM.t; m.los = LOS(m.x, m.y, P.x, P.y, true, r); }
            float tx = P.x, ty = P.y;
            if (!m.los)
            {
                int k = NearFree(m.x, m.y); if (k >= 0) { int nk = Downhill(k); if (nk >= 0) { var c = NavC(nk); if (DM.link[k] == nk) { AldaraFx.Burst(m.x, m.y, 10, AldaraRules.Hex("#c07aff")); m.x = c.x; m.y = c.y; return; } tx = c.x; ty = c.y; } }
            }
            float a = Mathf.Atan2(ty - m.y, tx - m.x); m.x += Mathf.Cos(a) * step; m.y += Mathf.Sin(a) * step; PushMon(m, ox, oy);
        }
        public static bool Sees(AldaraMonsters.Mon m) { var P = AldaraPlayer.I; if (m.seeT <= 0 || DM.t - m.seeT > 0.3f) { m.seeT = DM.t; m.sees = LOS(m.x, m.y, P.x, P.y, false); } return m.sees; }
        public static bool Aggro(AldaraMonsters.Mon m, float aggro) { if (Sees(m)) return true; int f = FlowAt(m.x, m.y); return f >= 0 && f * NCS < aggro * 1.4f; }
        public static bool SameRoom(AldaraMonsters.Mon m) { return !m.dun || (DM != null && DM.psec == m.room); }
        public static bool Reachable(AldaraMonsters.Mon m) { return !m.dun || m.room <= open; }

        // ---------- entering and leaving ----------
        public struct Scale { public float hp, atk, xp, gold, bossHp, bossAtk; }
        public static Scale DunScale(int i)
        {
            if (i >= 5) { var b = DunScale(4); int k = i - 5; return new Scale { hp = b.hp * 1.5f * Mathf.Pow(1.35f, k), atk = b.atk * 1.25f * Mathf.Pow(1.18f, k), xp = b.xp * 1.5f * Mathf.Pow(1.4f, k), gold = b.gold * 1.5f * Mathf.Pow(1.4f, k), bossHp = b.bossHp * 1.5f * Mathf.Pow(1.35f, k), bossAtk = b.bossAtk * 1.25f * Mathf.Pow(1.18f, k) }; }
            return new Scale { hp = 1600 * Mathf.Pow(1.85f, i), atk = 60 * Mathf.Pow(1.55f, i), xp = 350 * Mathf.Pow(1.5f, i), gold = 80 * Mathf.Pow(1.5f, i), bossHp = 22000 * Mathf.Pow(1.95f, i), bossAtk = 105 * Mathf.Pow(1.55f, i) };
        }
        public static AldaraMonsters.Mon MakeMob(MobDef md, int i, int room, float x, float y, bool summoned)
        {
            var sc = DunScale(i); float hp = Mathf.Round(sc.hp * (md.tank ? 1.6f : 1) * (summoned ? 0.6f : 1)); var book = MonsterBook.Load();
            var mdef = book.Get(md.name);
            return new AldaraMonsters.Mon
            {
                def = mdef, name = md.name, r = md.r, hp = hp, maxHp = hp, atk = Mathf.Round(sc.atk * (md.tank ? 0.85f : 1)), xp = Mathf.Round(sc.xp * (summoned ? 0.4f : 1)), gold = Mathf.Round(sc.gold * (summoned ? 0.4f : 1)),
                tier = Mathf.Min(6, 4 + i / 2), dun = true, room = room, x = x, y = y, homeX = x, homeY = y, spd = md.spd > 0 ? md.spd : 0.55f, lvl = DUN_LEVEL + i * 5 + room, atkCd = 1, color = md.color, bolt = md.bolt, slowHit = md.slowHit
            };
        }
        static bool CanUse(Def d, out string why)
        {
            var H = AldaraHero.I; why = null;
            if (H.lvl < DUN_LEVEL) { why = "Dungeons require level " + DUN_LEVEL; return false; }
            if (d.deep && H.lvl < d.rec - 3) { why = d.name + " requires level " + (d.rec - 3); return false; }
            if (!d.hidden && !Unlocked(d.i)) { why = "Clear " + List[d.i - 1].name + " first"; return false; }
            return true;
        }
        public static int Best { get { var j = DunSave(); return j["best"] != null ? (int)j["best"] : 0; } }
        public static int Clears(string id) { var c = DunSave()["clears"] as JObject; return c != null && c[id] != null ? (int)c[id] : 0; }
        public static bool Unlocked(int i) { return i <= Best; }
        static JObject DunSave()
        {
            var r = AldaraSave.Raw; if (r == null) return new JObject();
            if (!(r["dun"] is JObject d)) { d = new JObject { ["best"] = 0, ["clears"] = new JObject() }; r["dun"] = d; }
            if (!(d["clears"] is JObject)) d["clears"] = new JObject(); return d;
        }
        static float Rnd() { return Random.value; }
        static Vector2 Spot(int s, Vector3 a, float rad)
        {
            for (int t = 0; t < 40; t++) { float an = Rnd() * 6.283f, d = Mathf.Sqrt(Rnd()) * a.z, x = a.x + Mathf.Cos(an) * d, y = a.y + Mathf.Sin(an) * d; if (Clr(x, y) >= Mathf.Min(rad, 22) + 4 && Section(x, y) == s) return new Vector2(x, y); }
            return new Vector2(a.x, a.y);
        }
        public static void Start(int i)
        {
            var H = AldaraHero.I; var P = AldaraPlayer.I; if (!AldaraSave.Ready || DM != null || !H.alive) return;
            var d = List[i]; if (!CanUse(d, out string why)) { AldaraHud.Banner(why); return; }
            if (!Ready(d)) { AldaraHud.Banner(d.name + " is not open yet"); return; }
            Map M; try { M = Build(d); } catch (System.Exception e) { Debug.LogException(e); AldaraHud.Banner("The dungeon failed to load"); return; }
            def = d; idx = i; open = 0; done = false; exitT = 0; ret = new Vector2(P.x, P.y);
            DM = M; AldaraWorld.Dun = true;
            var MS = AldaraMonsters.I; MS.EnterDungeon(out worldMons);
            int NRM = M.names.Length;
            for (int r = 0; r < NRM - 1; r++)
            {
                int n = d.deep ? 7 + r * 2 : 6 + r * 2; var A = M.spawns[r]; if (A.Count == 0) continue;
                for (int k = 0; k < n; k++) { var md = d.mobs[k % d.mobs.Length]; var p = Spot(r, A[k % A.Count], md.r); var mob = MakeMob(md, i, r, p.x, p.y, false); if (mob.def != null) MS.all.Add(mob); }
            }
            var sc = DunScale(i); var b = d.boss;
            var boss = new AldaraMonsters.Mon
            {
                def = MonsterBook.Load().Get(b.name), name = b.name, r = b.r, hp = Mathf.Round(sc.bossHp), maxHp = Mathf.Round(sc.bossHp), atk = Mathf.Round(sc.bossAtk), xp = Mathf.Round(sc.xp * 20), gold = Mathf.Round(sc.gold * 15),
                tier = Mathf.Min(6, 4 + i), lvl = DUN_LEVEL + i * 5 + 5, boss = true, dun = true, room = NRM - 1, x = M.bossAt.x, y = M.bossAt.y, homeX = M.bossAt.x, homeY = M.bossAt.y, spd = 0.5f, mech = b.mech, mt = 4, mt2 = 8, atkCd = 1, color = b.color, bolt = b.bolt
            };
            if (boss.def != null) MS.all.Add(boss);
            DStat.Start(d);
            P.x = M.start.x; P.y = M.start.y; H.target = null; if (AldaraSkills.I) AldaraSkills.I.queued = null; M.safe = new Vector2(P.x, P.y);
            AldaraAuto.questHunt = false; AldaraAuto.questWalk = null; AldaraQuests.Render(); if (AldaraPet.I) AldaraPet.I.placed = false;
            if (AldaraWindows.I) { AldaraWindows.I.Toggle("dun", false); }
            FlowUpdate(true);
            if (AldaraDungeonView.I) AldaraDungeonView.I.Enter(M);
            AldaraHud.Banner(d.name + ": " + M.names[0]);
        }
        public static void Exit(string reason)
        {
            if (DM == null) return; var H = AldaraHero.I; var P = AldaraPlayer.I;
            if (AldaraDungeonView.I) AldaraDungeonView.I.Leave();
            AldaraMonsters.I.LeaveDungeon(worldMons); worldMons = null; DM = null; AldaraWorld.Dun = false;
            P.x = ret.x; P.y = ret.y; H.target = null; if (AldaraSkills.I) AldaraSkills.I.queued = null; H.slowT = 0; if (AldaraPet.I) AldaraPet.I.placed = false;
            if (reason == "fail") { AldaraHud.Banner("Dungeon failed"); DStat.Show("fail"); }
            else if (reason == "left") { AldaraHud.Banner("You left the dungeon"); DStat.Show("left"); }
            AldaraQuests.Render(); AldaraSave.Dirty();
        }
        static bool RoomCleared(int r) { return !AldaraMonsters.I.all.Any(m => !m.dead && m.room == r); }
        /// dungeonOnKill
        public static void OnKill(AldaraMonsters.Mon t)
        {
            t.respawnT = 1e9f; int NRK = DM.names.Length;
            while (open < NRK - 1 && RoomCleared(open))
            {
                var g = open < DM.gates.Count ? DM.gates[open] : null;
                if (g != null && !g.open) { g.open = true; foreach (var k in g.cells) DM.gblk[k] = (byte)Mathf.Max(0, DM.gblk[k] - 1); if (AldaraDungeonView.I) AldaraDungeonView.I.Shake(3, 0.4f); AldaraFx.Burst((g.x1 + g.x2) / 2, (g.y1 + g.y2) / 2, 24, DM.torchCol); }
                open++;
                AldaraHud.Banner(open == NRK - 1 ? "The way to " + DM.names[NRK - 1] + " is open" : "The gate to " + DM.names[open] + " opens");
                FlowUpdate(true);
            }
            if (t.boss) Cleared();
        }
        static void Cleared()
        {
            var H = AldaraHero.I; var P = AldaraPlayer.I; int i = idx; done = true; exitT = 20;
            float xp = Mathf.Round(5000 * Mathf.Pow(1.8f, i)), gold = Mathf.Round(2000 * Mathf.Pow(1.7f, i)); H.gold += gold;
            int lv = DUN_LEVEL + 5 * i + 5;
            for (int k = 0; k < 3; k++)
            {
                var it = AldaraItems.RollItem(lv, AldaraItems.PickRarity(lv, 40, true), 8);
                if (H.AddLoot(it)) { DStat.loot.Add(it); AldaraFx.Text(P.x, P.y - 16 - 60 - k * 16, it.name + "!", it.Col); }
            }
            H.vialHp = Mathf.Min(AldaraItems.VIAL_MAX, H.vialHp + 2); H.vialMp = Mathf.Min(AldaraItems.VIAL_MAX, H.vialMp + 2);
            int sp = Clears(def.id) > 0 ? 1 : 2; AldaraTree.sp += sp; AldaraTree.spTotal += sp; AldaraHud.Banner("+" + sp + " Skill Point" + (sp > 1 ? "s" : "") + " (" + def.name + "): press K");
            if (Random.value < 0.012f) { var w = AldaraItems.RollWings(lv); if (H.AddLoot(w)) AldaraHud.Banner("WINGS DROP: " + w.name + "!"); }
            if (Random.value < 0.015f) { var a = AldaraItems.RollMythicArmor(lv); if (H.AddLoot(a)) AldaraHud.Banner("MYTHICAL ARMOR: " + a.name + "!"); }
            bool first = Best <= i; var ds = DunSave();
            if (!def.hidden) ds["best"] = Mathf.Max(Best, i + 1);
            ((JObject)ds["clears"])[def.id] = Clears(def.id) + 1;
            AldaraQuests.OnDungeon(def.id);
            H.GainXp(xp);
            AldaraHud.Banner(def.name + " cleared!" + (first && i + 1 < List.Count && !List[i + 1].hidden ? " " + List[i + 1].name + " unlocked" : ""));
            AldaraFx.Text(P.x, P.y - 16 - 40, "+" + xp + " XP  +" + gold + " Gold", AldaraRules.Hex("#e0b64b"));
            DStat.xp = xp; DStat.gold = gold; DStat.Show("done"); AldaraSave.Dirty();
        }

        // ---------- per frame ----------
        void Awake() { I = this; }
        void Update()
        {
            if (DM == null) return; var H = AldaraHero.I; var P = AldaraPlayer.I; if (!AldaraSave.Ready) { Exit("left"); return; }
            float dt = Mathf.Min(Time.deltaTime, 0.1f); var D = DM; D.t += dt;
            foreach (var g in D.gates) if (g.open && g.openT < 1) g.openT = Mathf.Min(1, g.openT + dt / 1.2f);
            FlowUpdate(false); UpdateTraps(dt);
            if (done) { exitT -= dt; if (exitT <= 0) { Exit("done"); return; } }
            // the boss's abilities, only while you are in its room
            foreach (var m in AldaraMonsters.I.all.ToArray()) if (m.dun && m.boss && !m.dead && m.mech != null && SameRoom(m) && H.alive && !done) BossMech(m, dt, Vector2.Distance(new Vector2(P.x, P.y), new Vector2(m.x, m.y)));
            if (!H.alive) failT += dt; else failT = 0;
        }
        static float failT;
        public static string Status
        {
            get
            {
                if (DM == null) return ""; int room = DM.psec; int left = AldaraMonsters.I.all.Count(m => !m.dead && m.room == room);
                return done ? "Cleared! Returning in " + Mathf.CeilToInt(exitT) + "s" : room == DM.names.Length - 1 ? DM.names[DM.names.Length - 1] : (room >= 0 && room < DM.names.Length ? DM.names[room] : "") + "  ·  " + left + " left";
            }
        }
        /// the hero died: back to where you entered
        public static void OnDeath() { if (DM != null) Exit("fail"); }

        // ---------- after the player moved this frame: ice, walls, drops, runes, section banners ----------
        public static void PlayerPost(float lx, float ly, float dt, float pspeed)
        {
            var D = DM; var P = AldaraPlayer.I; if (dt <= 0) return;
            int k = Kind(P.x, P.y); bool ice = k == KI || k == KT;
            float vx = (P.x - lx) / dt, vy = (P.y - ly) / dt;
            if (ice) { float a = 1 - Mathf.Exp(-2.4f * dt); D.vx += (vx - D.vx) * a; D.vy += (vy - D.vy) * a; float sp = Mathf.Sqrt(D.vx * D.vx + D.vy * D.vy), cap = pspeed * 1.25f; if (sp > cap) { D.vx *= cap / sp; D.vy *= cap / sp; } P.x = lx + D.vx * dt; P.y = ly + D.vy * dt; }
            Push(ref P.x, ref P.y, 14, lx, ly);
            if (!ice) { D.vx = (P.x - lx) / dt; D.vy = (P.y - ly) / dt; } else { if (Mathf.Abs(P.x - (lx + D.vx * dt)) > 0.5f) D.vx = (P.x - lx) / dt; if (Mathf.Abs(P.y - (ly + D.vy * dt)) > 0.5f) D.vy = (P.y - ly) / dt; }
            if (D.fallT > 0) D.fallT -= dt;
            else
            {
                int kk = Kind(P.x, P.y);
                if (kk == KS) { foreach (var t in D.traps) if (t.type == "stone" && Vector2.Distance(new Vector2(P.x, P.y), new Vector2(t.x, t.y)) < t.r + 4 && !t.safe) { Fall("lava"); break; } }
                else if (kk == KT) { foreach (var t in D.traps) if (t.type == "thin" && t.broken > 0 && Vector2.Distance(new Vector2(P.x, P.y), new Vector2(t.x, t.y)) < t.r * 0.85f) { Fall("water"); break; } }
                else if (kk == KX) { bool onSolid = false, onAny = false; foreach (var t in D.traps) if (t.type == "phase" && OnBridge(t, P.x, P.y)) { onAny = true; if (t.solid) onSolid = true; } if (onAny && !onSolid) Fall("void"); }
            }
            if (D.fallT <= 0 && (k == KF || k == KB || k == KI) && Clr(P.x, P.y) >= 12) D.safe = new Vector2(P.x, P.y);
            // runes
            int on = -1; for (int i = 0; i < D.runes.Count; i++) { var r = D.runes[i]; if (Vector2.Distance(new Vector2(P.x, P.y), r.a) < r.r * 0.7f) on = i * 2; else if (Vector2.Distance(new Vector2(P.x, P.y), r.b) < r.r * 0.7f) on = i * 2 + 1; }
            if (on < 0) D.runeLock = -1;
            else if (on != D.runeLock)
            {
                var r = D.runes[on >> 1]; var to = (on & 1) != 0 ? r.a : r.b; AldaraFx.Burst(P.x, P.y, 20, AldaraRules.Hex("#c07aff")); P.x = to.x; P.y = to.y; D.runeLock = on ^ 1; D.vx = D.vy = 0;
                AldaraFx.Burst(P.x, P.y, 24, AldaraRules.Hex("#e0c0ff")); AldaraFx.Ring(P.x, P.y, 60, AldaraRules.Hex("#c07aff"), 0.6f); FlowUpdate(true);
            }
            int s = Section(P.x, P.y); if (s >= 0) { D.psec = s; if (!D.seen[s]) { D.seen[s] = true; AldaraHud.Banner(D.names[s]); } }
        }
        static bool OnBridge(Trap t, float x, float y)
        {
            var Pp = t.pts; for (int s = 0; s < Pp.Length - 1; s++) { var a = Pp[s]; var b = Pp[s + 1]; float dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy, u = Mathf.Clamp01(((x - a.x) * dx + (y - a.y) * dy) / l2); if (Vector2.Distance(new Vector2(x, y), new Vector2(a.x + dx * u, a.y + dy * u)) < t.w / 2 + 2) return true; }
            return false;
        }
        static void Fall(string kind)
        {
            var D = DM; var P = AldaraPlayer.I; var H = AldaraHero.I; if (D.fallT > 0 || !H.alive) return; D.fallT = 1.4f;
            var col = AldaraRules.Hex(kind == "lava" ? "#ff7a2a" : kind == "water" ? "#8fdfff" : "#b07aff");
            AldaraFx.Burst(P.x, P.y, 26, col); if (AldaraDungeonView.I) AldaraDungeonView.I.Shake(4, 0.35f);
            AldaraFx.Text(P.x, P.y - 16 - 40, kind == "lava" ? "Into the lava!" : kind == "water" ? "Through the ice!" : "Lost to the void!", col);
            P.x = D.safe.x; P.y = D.safe.y; D.vx = D.vy = 0; H.target = null;
            H.Damage(Mathf.Round(H.maxHp * (kind == "lava" ? 0.18f : 0.12f)), 0, 0);
            if (kind == "water") H.slowT = Mathf.Max(H.slowT, 2.5f);
            AldaraFx.Burst(P.x, P.y, 14, col);
        }

        // ---------- traps ----------
        static float TrapDmg(float k) { return Mathf.Round(DunScale(idx).atk * k); }
        static float Ph(Trap t) { return ((((DM.t + t.off) % t.per) + t.per) % t.per) / t.per; }
        static bool InRot(Trap t, float x, float y, float pad) { float c = Mathf.Cos(-t.ang), s = Mathf.Sin(-t.ang), dx = x - t.x, dy = y - t.y, lx = dx * c - dy * s, ly = dx * s + dy * c; return Mathf.Abs(lx) < t.w / 2 + pad && Mathf.Abs(ly) < t.h / 2 + pad; }
        static float SegDist(float x, float y, float ax, float ay, float bx, float by) { float dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy; if (l2 == 0) l2 = 1; float u = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / l2); return Mathf.Sqrt((x - ax - dx * u) * (x - ax - dx * u) + (y - ay - dy * u) * (y - ay - dy * u)); }
        static void Hurt(float dmg) { AldaraHero.I.Damage(dmg, 0, DUN_LEVEL + idx * 5 + Mathf.Max(0, DM.psec)); }
        static void UpdateTraps(float dt)
        {
            var D = DM; var P = AldaraPlayer.I; float px = P.x, py = P.y; bool alive = AldaraHero.I.alive;
            foreach (var t in D.traps)
            {
                if (t.cd > 0) t.cd -= dt;
                switch (t.type)
                {
                    case "spikes": { float ph = Ph(t); bool up = ph >= 0.7f && ph < 0.92f; t.st = ph < 0.52f ? 0 : ph < 0.7f ? 1 : up ? 2 : 3; t.ph = ph; if (up && alive && !(t.cd > 0) && InRot(t, px, py, 4)) { t.cd = 0.8f; Hurt(TrapDmg(1.2f)); AldaraFx.Burst(px, py, 8, AldaraRules.Hex("#d8d8e0")); } break; }
                    case "blade":
                        {
                            float w = 2 * Mathf.PI / t.per, s = Mathf.Sin(w * D.t + t.off * w); t.cbx = t.x + t.ax * t.amp * s; t.cby = t.y + t.ay * t.amp * s; t.vel = Mathf.Cos(w * D.t + t.off * w);
                            float nx = t.ay, ny = -t.ax, hl = t.len / 2;
                            if (alive && !(t.cd > 0) && SegDist(px, py, t.cbx - nx * hl, t.cby - ny * hl, t.cbx + nx * hl, t.cby + ny * hl) < 30)
                            { t.cd = 0.8f; Hurt(TrapDmg(1.6f)); AldaraFx.Burst(px, py, 10, AldaraRules.Hex("#ff6a6a")); float sg = Mathf.Sign(t.vel == 0 ? 1 : t.vel); Shove(Mathf.Atan2(t.ay * sg, t.ax * sg), 60); }
                            break;
                        }
                    case "fire":
                        {
                            float ph = Ph(t); t.ph = ph; t.st = ph < 1 - t.on - 0.14f ? 0 : ph < 1 - t.on ? 1 : 2;
                            if (t.st == 2 && alive && !(t.cd > 0)) { float c = Mathf.Cos(t.ang), s = Mathf.Sin(t.ang), dx = px - t.x, dy = py - t.y, al = dx * c + dy * s, ac = -dx * s + dy * c; if (al > -10 && al < t.len && Mathf.Abs(ac) < t.wid / 2 + 8) { t.cd = 0.35f; Hurt(TrapDmg(0.55f)); } }
                            break;
                        }
                    case "boulder":
                        {
                            float L = Mathf.Sqrt((t.bx - t.ax) * (t.bx - t.ax) + (t.by - t.ay) * (t.by - t.ay)), T = L / t.spd, c = ((D.t + t.off) % t.per + t.per) % t.per;
                            t.rolling = c < T; t.warn = c > t.per - 1.2f; if (t.rolling) { float u = c / T; t.x = t.ax + (t.bx - t.ax) * u; t.y = t.ay + (t.by - t.ay) * u; t.rot = c * t.spd / t.r; }
                            if (t.warn && Vector2.Distance(new Vector2(px, py), new Vector2(t.ax, t.ay)) < 900 && Random.value < dt * 6 && AldaraDungeonView.I) AldaraDungeonView.I.Shake(1.5f, 0.15f);
                            if (t.rolling && !t.was && Vector2.Distance(new Vector2(px, py), new Vector2(t.ax, t.ay)) < 1400 && AldaraDungeonView.I) AldaraDungeonView.I.Shake(3, 0.3f);
                            if (!t.rolling && t.was) AldaraFx.Burst(t.bx, t.by, 26, AldaraRules.Hex("#8a7a5a"));
                            t.was = t.rolling;
                            if (t.rolling && alive && !(t.cd > 0) && Vector2.Distance(new Vector2(px, py), new Vector2(t.x, t.y)) < t.r + 12)
                            { t.cd = 1; Hurt(TrapDmg(2)); float ux = (t.bx - t.ax) / L, uy = (t.by - t.ay) / L, side = ((px - t.x) * -uy + (py - t.y) * ux) >= 0 ? 1 : -1; Shove(Mathf.Atan2(ux * side, -uy * side), 90); }
                            break;
                        }
                    case "icicles":
                        {
                            if (!alive || Vector2.Distance(new Vector2(px, py), new Vector2(t.x, t.y)) > t.r) break; t.acc += dt;
                            if (t.acc > t.per)
                            {
                                t.acc = 0; int n = 1 + (Random.value < 0.4f ? 1 : 0);
                                for (int k = 0; k < n; k++) { float a = Random.value * 6.28f, d = k > 0 ? 60 + Random.value * 80 : Random.value * 40; AldaraMonsters.I.AddHazard(px + Mathf.Cos(a) * d + D.vx * 0.5f, py + Mathf.Sin(a) * d + D.vy * 0.5f, 52, 1.0f, TrapDmg(1.1f), AldaraRules.Hex("#bfefff"), 2, true, 0, 0); }
                            }
                            break;
                        }
                    case "geyser":
                        {
                            float ph = Ph(t); int st = ph < 0.62f ? 0 : ph < 0.8f ? 1 : ph < 0.94f ? 2 : 3; if (st == 2 && t.st != 2) { t.hit = false; if (AldaraDungeonView.I) AldaraDungeonView.I.Shake(2, 0.2f); }
                            t.st = st; if (st == 2 && alive && !t.hit && Vector2.Distance(new Vector2(px, py), new Vector2(t.x, t.y)) < t.r + 8) { t.hit = true; Hurt(TrapDmg(1.8f)); AldaraFx.Burst(px, py, 12, AldaraRules.Hex("#ffb04a")); }
                            break;
                        }
                    case "hammer":
                        {
                            float ph = Ph(t); t.ph = ph; int st = ph < 0.66f ? 0 : ph < 0.7f ? 1 : ph < 0.84f ? 2 : 3;
                            if (st == 2 && t.st != 2)
                            {
                                if (AldaraDungeonView.I) AldaraDungeonView.I.Shake(Vector2.Distance(new Vector2(px, py), new Vector2(t.x, t.y)) < 600 ? 5 : 1.5f, 0.3f); AldaraFx.Burst(t.x, t.y, 22, AldaraRules.Hex("#8a7a6a"));
                                if (alive && InRot(t, px, py, 6)) { Hurt(TrapDmg(2.6f)); Shove(Mathf.Atan2(py - t.y, px - t.x), 70); }
                            }
                            t.st = st; break;
                        }
                    case "stone": { float ph = Ph(t); t.st = ph < 0.58f ? 0 : ph < 0.72f ? 1 : ph < 0.8f ? 2 : ph < 0.94f ? 3 : 4; t.safe = t.st <= 1; t.ph = ph; break; }
                    case "thin":
                        {
                            if (t.broken > 0) { t.broken -= dt; if (t.broken <= 0) { t.broken = 0; t.crack = 0; } break; }
                            if (alive && Vector2.Distance(new Vector2(px, py), new Vector2(t.x, t.y)) < t.r) { t.crack += dt; if (t.crack > 0.9f) { t.broken = 6; AldaraFx.Burst(t.x, t.y, 30, AldaraRules.Hex("#e8f8ff")); } } else t.crack = Mathf.Max(0, t.crack - dt * 0.3f);
                            break;
                        }
                    case "phase": { float ph = Ph(t); t.solid = ph < 0.72f; t.warn = ph > 0.56f && ph < 0.72f; t.ph = ph; break; }
                    case "beam":
                        {
                            t.a = t.a0 + D.t * t.spd;
                            if (alive && !(t.cd > 0))
                            {
                                float dc = Vector2.Distance(new Vector2(px, py), new Vector2(t.x, t.y));
                                if (dc > 50 && dc < t.len + 10) for (int k = 0; k < t.n; k++)
                                    {
                                        float a = t.a + k * 2 * Mathf.PI / t.n;
                                        if (SegDist(px, py, t.x, t.y, t.x + Mathf.Cos(a) * t.len, t.y + Mathf.Sin(a) * t.len) < 24) { t.cd = 0.5f; Hurt(TrapDmg(0.9f)); AldaraFx.Burst(px, py, 10, AldaraRules.Hex("#d8a0ff")); Shove(a + Mathf.PI / 2 * (Mathf.Sin(t.spd) > 0 ? 1 : -1), 40); break; }
                                    }
                            }
                            break;
                        }
                }
            }
        }

        // ---------- the bosses' abilities ----------
        static void Summon(MobDef md, int n, AldaraMonsters.Mon near)
        {
            for (int k = 0; k < n; k++)
            {
                float a = Random.value * Mathf.PI * 2; var mob = MakeMob(md, idx, DM.names.Length - 1, near.x + Mathf.Cos(a) * 90, near.y + Mathf.Sin(a) * 90, true); if (mob.def == null) continue;
                mob.aggroT = 10; Place(mob); AldaraMonsters.I.all.Add(mob); AldaraFx.Burst(mob.x, mob.y, 10, AldaraRules.Hex(md.color));
            }
        }
        static void BossMech(AldaraMonsters.Mon m, float dt, float dTo)
        {
            m.mt -= dt; m.mt2 -= dt; var P = AldaraPlayer.I; var H = AldaraHero.I; var MS = AldaraMonsters.I;
            switch (m.mech)
            {
                case "summon":
                    if (m.mt <= 0) { m.mt = m.enraged ? 8 : 12; Summon(def.mobs[0], 3, m); AldaraHud.Banner(m.name + " calls for help!"); }
                    if (!m.enraged && m.hp < m.maxHp * 0.3f) { m.enraged = true; m.atk = Mathf.Round(m.atk * 1.5f); m.spd = 0.7f; m.color = "#ff5a3a"; AldaraHud.Banner(m.name + " is enraged!"); }
                    break;
                case "nova":
                    if (m.mt <= 0) { m.mt = 8; MS.AddHazard(m.x, m.y, 230, 1.4f, Mathf.Round(m.atk * 1.6f), AldaraRules.Hex("#b89aff"), 0, false, 0, 0); }
                    break;
                case "spikes":
                    if (dTo < 270) H.slowT = Mathf.Max(H.slowT, 0.3f);
                    if (m.mt <= 0) { m.mt = 5.5f; for (int k = 0; k < 4; k++) { float a = Random.value * 7, d = k == 0 ? 0 : 60 + Random.value * 110; MS.AddHazard(P.x + Mathf.Cos(a) * d, P.y + Mathf.Sin(a) * d, 70, 1.1f, Mathf.Round(m.atk * 1.3f), AldaraRules.Hex("#8fdfff"), 0, true, 0, 0); } }
                    break;
                case "lava":
                    if (m.mt <= 0) { m.mt = 6.5f; for (int k = 0; k < 3; k++) { float a = Random.value * 7, d = k == 0 ? 0 : 90 + Random.value * 120; MS.AddHazard(P.x + Mathf.Cos(a) * d, P.y + Mathf.Sin(a) * d, 85, 1.0f, 0, AldaraRules.Hex("#ff6a2a"), 0, false, 6, Mathf.Round(m.atk * 0.6f)); } }
                    break;
                case "void":
                    if (m.mt <= 0) { m.mt = 6; for (int k = 0; k < 12; k++) MS.EnemyShoot(m, k * Mathf.PI / 6 + m.mt2, Mathf.Round(m.atk * 0.7f), AldaraRules.Hex("#c07aff"), 260); }
                    if (m.mt2 <= 0)
                    {
                        m.mt2 = 10; AldaraFx.Burst(m.x, m.y, 16, AldaraRules.Hex("#9a5aff"));
                        float a = P.facing + Mathf.PI; m.x = P.x + Mathf.Cos(a) * 80; m.y = P.y + Mathf.Sin(a) * 80; Place(m); AldaraFx.Burst(m.x, m.y, 16, AldaraRules.Hex("#9a5aff"));
                    }
                    if (!m.summoned && m.hp < m.maxHp * 0.5f) { m.summoned = true; Summon(def.mobs[0], 3, m); AldaraHud.Banner(m.name + " calls the void!"); }
                    break;
            }
        }
    }

    // DSTAT: the run's numbers for the results window
    public static class DStat
    {
        public static bool on; public static float t0, xp, gold, dmg, boss, taken, big; public static int kills, hits; public static AldaraDungeon.Def def; public static List<Item> loot = new List<Item>(); public static string pet;
        public static string verdict; public static float secs; public static int roomsOpen, rooms;
        public static void Start(AldaraDungeon.Def d) { on = true; t0 = Time.time; dmg = boss = taken = big = 0; kills = hits = 0; xp = gold = 0; loot.Clear(); def = d; var p = AldaraHero.I.Eq("pet"); pet = p != null ? p.name : null; }
        public static void Hit(AldaraMonsters.Mon m, float d) { if (!on || !AldaraDungeon.Active) return; d = Mathf.Max(0, Mathf.Min(d, m.hp)); dmg += d; hits++; if (d > big) big = d; if (m.boss) boss += d; }
        public static void Kill() { if (on && AldaraDungeon.Active) kills++; }
        public static void Taken(float d) { if (on && AldaraDungeon.Active && d > 0) taken += d; }
        public static void Show(string v)
        {
            if (!on) return; on = false; verdict = v; secs = Time.time - t0;
            roomsOpen = AldaraDungeon.open + 1; rooms = AldaraDungeon.DM != null ? AldaraDungeon.DM.names.Length : 4;
            if (AldaraWindows.I) { AldaraWindows.I.Toggle("dres", true); AldaraWindows.Refresh("dres"); }
        }
    }
}
