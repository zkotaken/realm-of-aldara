using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // The browser's walking around mountains (navFree / losClear / findPath / navAngle): A* on a 32 px grid that keeps
    // paths a little away from edges and avoids deep water unless you fly. Used when chasing a target and when
    // auto-questing walks somewhere: a straight line when the way is clear, else the path, skipping ahead and
    // sidestepping when stuck. Angle returns null when the place cannot be reached.
    public static class AldaraNav
    {
        const int NC = 32; static int NW, NH;
        static byte[] lqC, freeC; static float[] G; static int[] from, stamp, shut; static int gen;
        static void Init()
        {
            if (freeC != null) return; NW = Mathf.CeilToInt(AldaraWorld.WORLD_W / NC); NH = Mathf.CeilToInt(AldaraWorld.WORLD_H / NC); int n = NW * NH;
            lqC = new byte[n]; freeC = new byte[n]; G = new float[n]; from = new int[n]; stamp = new int[n]; shut = new int[n];
        }
        static bool Wings { get { return AldaraPlayer.I && AldaraPlayer.I.wings; } }
        static bool Free(int i, int j)
        {
            if (i < 0 || j < 0 || i >= NW || j >= NH) return false;
            int k = j * NW + i; byte v = freeC[k];
            if (v == 0)
            {
                float x = i * NC + NC / 2, y = j * NC + NC / 2;
                v = freeC[k] = (byte)(AldaraWorld.BlockedAt(x, y) || AldaraWorld.BlockedAt(x - 13, y - 13) || AldaraWorld.BlockedAt(x + 13, y - 13) || AldaraWorld.BlockedAt(x - 13, y + 13) || AldaraWorld.BlockedAt(x + 13, y + 13) ? 2 : 1);
            }
            if (v != 1) return false;
            return !(Liq(i, j) == 2 && !Wings);
        }
        static int Liq(int i, int j) { int k = j * NW + i; int v = lqC[k]; if (v == 0) v = lqC[k] = (byte)(AldaraWorld.LiquidAt(i * NC + NC / 2, j * NC + NC / 2) + 1); return v - 1; }
        public static bool LosClear(float ax, float ay, float bx, float by)
        {
            int n = Mathf.CeilToInt(Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)) / 18); bool wet = !Wings;
            for (int k = 1; k <= n; k++) { float u = k / (float)n, x = ax + (bx - ax) * u, y = ay + (by - ay) * u; if (AldaraWorld.BlockedAt(x, y) || (wet && AldaraWorld.LiquidAt(x, y) != 0)) return false; }
            return true;
        }
        // a binary heap of (cell, f)
        static readonly List<int> heap = new List<int>(); static readonly List<float> hf = new List<float>();
        static void Push(int k, float f)
        {
            heap.Add(k); hf.Add(f); int c = heap.Count - 1;
            while (c > 0) { int p = (c - 1) >> 1; if (hf[p] <= hf[c]) break; Swap(p, c); c = p; }
        }
        static int Pop()
        {
            int top = heap[0], last = heap.Count - 1; int lk = heap[last]; float lf = hf[last]; heap.RemoveAt(last); hf.RemoveAt(last);
            if (heap.Count > 0)
            {
                heap[0] = lk; hf[0] = lf; int c = 0;
                for (; ; ) { int l = c * 2 + 1, r = l + 1, m = c; if (l < heap.Count && hf[l] < hf[m]) m = l; if (r < heap.Count && hf[r] < hf[m]) m = r; if (m == c) break; Swap(m, c); c = m; }
            }
            return top;
        }
        static void Swap(int a, int b) { int t = heap[a]; heap[a] = heap[b]; heap[b] = t; float f = hf[a]; hf[a] = hf[b]; hf[b] = f; }

        public static List<Vector2> FindPath(float sx, float sy, float tx, float ty, int maxN = 60000)
        {
            Init(); gen++; heap.Clear(); hf.Clear();
            int si = Mathf.FloorToInt(sx / NC), sj = Mathf.FloorToInt(sy / NC), ti = Mathf.FloorToInt(tx / NC), tj = Mathf.FloorToInt(ty / NC);
            if (si < 0 || sj < 0 || si >= NW || sj >= NH) return null;
            if (!Free(ti, tj))
            {
                bool f = false;
                for (int r = 1; r <= 5 && !f; r++) for (int dj = -r; dj <= r && !f; dj++) for (int di = -r; di <= r; di++) if (Free(ti + di, tj + dj)) { ti += di; tj += dj; f = true; break; }
                if (!f) return null;
            }
            int sk = sj * NW + si, tk = tj * NW + ti;
            System.Func<int, int, float> Hh = (i, j) => { int dx = Mathf.Abs(i - ti), dy = Mathf.Abs(j - tj); return dx + dy + (1.414f - 2) * Mathf.Min(dx, dy); };
            stamp[sk] = gen; G[sk] = 0; from[sk] = -1; Push(sk, Hh(si, sj));
            int n = 0; bool found = false; bool wings = Wings;
            while (heap.Count > 0 && n++ < maxN)
            {
                int k = Pop(); if (shut[k] == gen) continue; shut[k] = gen;
                if (k == tk) { found = true; break; }
                int i = k % NW, j = (k - i) / NW; float g = G[k];
                for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                    {
                        if (di == 0 && dj == 0) continue; int ni = i + di, nj = j + dj;
                        if (!Free(ni, nj)) continue;
                        if (di != 0 && dj != 0 && (!Free(i + di, j) || !Free(i, j + dj))) continue;   // no corner cutting
                        int nk = nj * NW + ni; if (shut[nk] == gen) continue;
                        float ng = g + (di != 0 && dj != 0 ? 1.414f : 1) * (Liq(ni, nj) != 0 && !wings ? 4 : 1);
                        if (stamp[nk] != gen || ng < G[nk]) { stamp[nk] = gen; G[nk] = ng; from[nk] = k; Push(nk, ng + Hh(ni, nj)); }
                    }
            }
            if (!found) return null;
            var pts = new List<Vector2>(); for (int k = tk; k != -1 && k != sk; k = from[k]) { int i = k % NW, j = (k - i) / NW; pts.Add(new Vector2(i * NC + NC / 2, j * NC + NC / 2)); }
            pts.Reverse(); pts.Add(new Vector2(tx, ty)); return pts;
        }

        // NAV: the path being followed toward (tx, ty)
        static float ntx = -1e9f, nty = -1e9f, nt, losT, scT, st, sx, sy; static bool los = true, none; static List<Vector2> path; static int ni, stuck;
        static float Now { get { return Time.time * 1000; } }
        /// the direction to walk toward (tx, ty) around mountains, or null if it cannot be reached
        public static float? Angle(float tx, float ty)
        {
            var P = AldaraPlayer.I; float now = Now;
            if (Mathf.Sqrt((tx - ntx) * (tx - ntx) + (ty - nty) * (ty - nty)) > 90 || now - nt > 5000) { ntx = tx; nty = ty; path = null; none = false; nt = now; losT = -1e9f; }
            if (now - losT > 250) { losT = now; los = LosClear(P.x, P.y, tx, ty); }
            if (los) return Mathf.Atan2(ty - P.y, tx - P.x);
            if (path == null && !none) { path = FindPath(P.x, P.y, tx, ty); none = path == null; ni = 0; }
            if (none) return null;
            var p = path;
            // stuck on an edge? skip ahead, then sidestep
            if (Mathf.Sqrt((P.x - sx) * (P.x - sx) + (P.y - sy) * (P.y - sy)) > 6) { sx = P.x; sy = P.y; st = now; stuck = 0; }
            else if (now - st > 450) { st = now; stuck++; if (ni < p.Count - 1) ni++; if (stuck > 3) { path = null; nt = now; } }
            if (path == null) { path = FindPath(P.x, P.y, tx, ty); none = path == null; ni = 0; if (none) return null; p = path; }
            if (stuck > 1 && now - st < 250) return Mathf.Atan2(p[ni].y - P.y, p[ni].x - P.x) + (stuck % 2 == 1 ? 1.4f : -1.4f);
            while (ni < p.Count - 1 && Vector2.Distance(p[ni], new Vector2(P.x, P.y)) < 22) ni++;
            if (ni < p.Count - 1 && now - scT > 150) { scT = now; if (LosClear(P.x, P.y, p[ni + 1].x, p[ni + 1].y)) ni++; }
            var w = p[ni]; return Mathf.Atan2(w.y - P.y, w.x - P.x);
        }
    }
}
