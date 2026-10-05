using System.Collections.Generic;
using UnityEngine;

// Hand-built geometry for the structures (Graphics, HD structures), made before the surface pass
// (AldaraStructureRemaster.Process). Each keeps the browser model's size, place and colours:
// - caves: a craggy, layered rock outcrop with a real tunnel that runs back into the dark, a rocky lip around the
//   mouth, timber props with a lintel, two burning torches, rock teeth hanging over the entrance and rubble below;
// - camp tents: hide stretched over A-frame poles and a ridge pole, sagging between them, with a short wall, sewn
//   seams and patches, the door flaps tied back on a dark inside, guy ropes to stakes and a fur at the door;
// - lair pillars: rough-hewn blocks stacked on a stepped plinth under a capstone, chipped and worn, carved with
//   glowing runes, rubble at the foot (the fire bowl on top kept as it was);
// - rocks, boulders, stone circles, ruins, graves and the dungeons' columns, pillars, obelisks and stalagmites:
//   cut finer and sculpted (craggy rock, chipped and broken masonry).
// Each triangle can carry a surface for the remaster pass (Forced) and a smooth flag (Soft: no crease shading).
public static class AldaraSculpt
{
    public const int NONE = -1, STONE = 1, FIELD = 2, ROCK = 3, PLANK = 4, BEAM = 5, CLAY = 6, SLATE = 7, THATCH = 8, PLASTER = 9, METAL = 10, LEATHER = 11, CLOTH = 12, COBBLE = 13, MARBLE = 14, SAND = 15;
    /// per triangle of the last mesh Apply returned (null when it changed nothing)
    public static int[] Forced; public static bool[] Soft;

    static bool Starts(string n, params string[] p) { foreach (var s in p) if (n.StartsWith(s)) return true; return false; }

    public static Mesh Apply(Mesh src, string name)
    {
        Forced = null; Soft = null; B b = null;
        if (name.StartsWith("cave")) b = Cave(src);
        else if (name.StartsWith("tent_")) b = Tent(src, name);
        else if (name.StartsWith("lairPillar")) b = LairPillar(src, name.StartsWith("lairPillartrue"));
        else if (name.StartsWith("campfire")) b = Campfire(src);
        else if (Starts(name, "boulder", "lore_stones", "dun_stalag", "dun_stal", "dun_rock", "dun_icespire")) b = Generic(src, false, name.StartsWith("dun_ice") ? 0.5f : 1f);
        else if (Starts(name, "ruin", "grave", "lore_graves", "dun_column", "dun_brokenpillar", "dun_obelisk", "lore_well")) b = Generic(src, true, 1f, name.StartsWith("ruin") && !name.StartsWith("ruinSlab") || name.StartsWith("dun_brokenpillar"));
        else if (IsBuilding(name)) b = Building(src, name);
        if (b == null) return src;
        var m = b.ToMesh(src.name); Forced = b.F.ToArray(); Soft = b.S.ToArray(); return m;
    }

    // ------------------------------------------------------------------ noise
    static readonly int[] PM = MakePerm();
    static int[] MakePerm() { var r = new System.Random(9137); var q = new int[256]; for (int i = 0; i < 256; i++) q[i] = i; for (int i = 255; i > 0; i--) { int j = r.Next(i + 1); int t = q[i]; q[i] = q[j]; q[j] = t; } var p = new int[512]; for (int i = 0; i < 512; i++) p[i] = q[i & 255]; return p; }
    static float Fd(float t) { return t * t * t * (t * (t * 6 - 15) + 10); }
    static float Gr(int h, float x, float y, float z) { h &= 15; float u = h < 8 ? x : y, v = h < 4 ? y : (h == 12 || h == 14 ? x : z); return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v); }
    public static float Noise(Vector3 q)
    {
        int X = Mathf.FloorToInt(q.x), Y = Mathf.FloorToInt(q.y), Z = Mathf.FloorToInt(q.z);
        float x = q.x - X, y = q.y - Y, z = q.z - Z; X &= 255; Y &= 255; Z &= 255;
        float u = Fd(x), v = Fd(y), w = Fd(z);
        int A = PM[X] + Y, AA = PM[A] + Z, AB = PM[A + 1] + Z, B1 = PM[X + 1] + Y, BA = PM[B1] + Z, BB = PM[B1 + 1] + Z;
        return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(Gr(PM[AA], x, y, z), Gr(PM[BA], x - 1, y, z), u), Mathf.Lerp(Gr(PM[AB], x, y - 1, z), Gr(PM[BB], x - 1, y - 1, z), u), v),
                          Mathf.Lerp(Mathf.Lerp(Gr(PM[AA + 1], x, y, z - 1), Gr(PM[BA + 1], x - 1, y, z - 1), u), Mathf.Lerp(Gr(PM[AB + 1], x, y - 1, z - 1), Gr(PM[BB + 1], x - 1, y - 1, z - 1), u), v), w);
    }
    public static float Fbm(Vector3 p, int oct) { float s = 0, a = 0.5f; for (int i = 0; i < oct; i++) { s += a * Noise(p); p = p * 2.03f + new Vector3(1.7f, 9.2f, 3.1f); a *= 0.5f; } return s; }
    /// 0 .. ~1, sharp creases
    public static float Ridged(Vector3 p, int oct) { float s = 0, a = 0.55f; for (int i = 0; i < oct; i++) { float n = 1 - Mathf.Abs(Noise(p)); s += a * n * n; p = p * 2.07f + new Vector3(5.3f, 1.1f, 7.7f); a *= 0.5f; } return s; }

    // ------------------------------------------------------------------ building geometry
    public class B
    {
        public List<Vector3> P = new List<Vector3>(), N = new List<Vector3>(); public List<Color32> C = new List<Color32>(); public List<float> E = new List<float>();
        public List<int> I = new List<int>(), F = new List<int>(), G = new List<int>(); public List<bool> S = new List<bool>(); public List<float> K = new List<float>();
        /// group and sculpt scale for what is added next (K > 0 is sculpted by Sculpt)
        public int group; public float k; public bool soft;
        public int V(Vector3 p, Vector3 n, Color32 c, float e) { P.Add(p); N.Add(n); C.Add(c); E.Add(e); return P.Count - 1; }
        public void T(int a, int b, int c, int f, bool s) { I.Add(a); I.Add(b); I.Add(c); F.Add(f); S.Add(s || soft); G.Add(group); K.Add(k); }
        public int Tris { get { return I.Count / 3; } }
        /// a flat triangle; its normal faces 'outDir'
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 col, int f, Vector3 outDir, float e = 0)
        {
            var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < 1e-14f) return; n.Normalize(); if (Vector3.Dot(n, outDir) < 0) n = -n;
            int i = V(a, n, col, e); V(b, n, col, e); V(c, n, col, e); T(i, i + 1, i + 2, f, false);
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 col, int f, Vector3 outDir, float e = 0) { Tri(a, b, c, col, f, outDir, e); Tri(a, c, d, col, f, outDir, e); }
        public void Box(Vector3 c, Vector3 size, Quaternion r, Color32 col, int f, float e = 0)
        {
            var h = size * 0.5f;
            for (int ax = 0; ax < 3; ax++) for (int sg = -1; sg <= 1; sg += 2)
                {
                    Vector3 n = Vector3.zero; n[ax] = sg; Vector3 u = Vector3.zero, v = Vector3.zero; u[(ax + 1) % 3] = 1; v[(ax + 2) % 3] = 1;
                    Vector3 o = Vector3.Scale(n, h), du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
                    var wn = r * n; int i0 = P.Count;
                    V(c + r * (o - du - dv), wn, col, e); V(c + r * (o + du - dv), wn, col, e); V(c + r * (o + du + dv), wn, col, e); V(c + r * (o - du + dv), wn, col, e);
                    T(i0, i0 + 1, i0 + 2, f, false); T(i0, i0 + 2, i0 + 3, f, false);
                }
        }
        /// a tapered tube from a to b, smooth sides
        public void Cyl(Vector3 a, Vector3 b, float r0, float r1, int seg, Color32 col, int f, bool capA, bool capB, float e = 0)
        {
            var ax = (b - a); float len = ax.magnitude; if (len < 1e-6f) return; ax /= len;
            var u = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var v = Vector3.Cross(ax, u);
            int i0 = P.Count; float slope = (r0 - r1) / len;
            for (int s = 0; s <= seg; s++)
            {
                float t = s * Mathf.PI * 2 / seg; var d = u * Mathf.Cos(t) + v * Mathf.Sin(t); var n = (d + ax * slope).normalized;
                V(a + d * r0, n, col, e); V(b + d * r1, n, col, e);
            }
            for (int s = 0; s < seg; s++) { int q = i0 + s * 2; T(q, q + 2, q + 1, f, true); T(q + 1, q + 2, q + 3, f, true); }
            if (capA && r0 > 1e-4f) { int c0 = V(a, -ax, col, e); int st = P.Count; for (int s = 0; s <= seg; s++) { float t = s * Mathf.PI * 2 / seg; V(a + (u * Mathf.Cos(t) + v * Mathf.Sin(t)) * r0, -ax, col, e); } for (int s = 0; s < seg; s++) T(c0, st + s + 1, st + s, f, false); }
            if (capB && r1 > 1e-4f) { int c0 = V(b, ax, col, e); int st = P.Count; for (int s = 0; s <= seg; s++) { float t = s * Mathf.PI * 2 / seg; V(b + (u * Mathf.Cos(t) + v * Mathf.Sin(t)) * r1, ax, col, e); } for (int s = 0; s < seg; s++) T(c0, st + s, st + s + 1, f, false); }
        }
        /// an icosahedron cut once (80 faces), stretched to radii and turned; sculpt it for a rock
        public void Rock(Vector3 c, Vector3 rad, Quaternion r, Color32 col, int f)
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var vs = new List<Vector3> { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            int[] fs = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            for (int i = 0; i < vs.Count; i++) vs[i] = vs[i].normalized;
            var mid = new Dictionary<long, int>(); var f2 = new List<int>();
            System.Func<int, int, int> M = (a, b) => { long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; int id; if (!mid.TryGetValue(key, out id)) { id = vs.Count; vs.Add((vs[a] + vs[b]).normalized); mid[key] = id; } return id; };
            for (int i = 0; i < fs.Length; i += 3) { int a = fs[i], b = fs[i + 1], d = fs[i + 2], ab = M(a, b), bd = M(b, d), da = M(d, a); f2.AddRange(new[] { a, ab, da, b, bd, ab, d, da, bd, ab, bd, da }); }
            int i0 = P.Count;
            foreach (var p in vs) { var q = Vector3.Scale(p, rad); var n = r * new Vector3(p.x / rad.x, p.y / rad.y, p.z / rad.z).normalized; V(c + r * q, n, col, 0); }
            for (int i = 0; i < f2.Count; i += 3) T(i0 + f2[i], i0 + f2[i + 1], i0 + f2[i + 2], f, true);
        }
        /// a smooth sheet p(u, v) for u, v in 0..1; normals face away from 'inside'
        public void Sheet(System.Func<float, float, Vector3> p, int nu, int nv, Vector3 inside, System.Func<float, float, Color32> col, int f)
        {
            int i0 = P.Count;
            for (int j = 0; j <= nv; j++) for (int i = 0; i <= nu; i++)
                {
                    float u = i / (float)nu, v = j / (float)nv, e = 1e-3f;
                    var q = p(u, v); var n = Vector3.Cross(p(Mathf.Min(1, u + e), v) - p(Mathf.Max(0, u - e), v), p(u, Mathf.Min(1, v + e)) - p(u, Mathf.Max(0, v - e))).normalized;
                    if (Vector3.Dot(n, q - inside) < 0) n = -n;
                    V(q, n, col(u, v), 0);
                }
            for (int j = 0; j < nv; j++) for (int i = 0; i < nu; i++) { int a = i0 + j * (nu + 1) + i; T(a, a + 1, a + nu + 2, f, true); T(a, a + nu + 2, a + nu + 1, f, true); }
        }
        public void Fan(List<Vector3> loop, Vector3 center, Color32 col, Color32 ccol, int f, Vector3 outDir)
        {
            int c0 = V(center, outDir.normalized, ccol, 0); int st = P.Count;
            foreach (var q in loop) V(q, outDir.normalized, col, 0);
            for (int i = 0; i < loop.Count; i++) T(c0, st + i, st + (i + 1) % loop.Count, f, true);
        }
        /// copy triangles of a mesh that pass 'keep', with the surface 'f(colour)' and the current group and scale
        public void Add(Mesh m, System.Func<Color32, Vector3, bool> keep, System.Func<Color32, int> f, Vector3 offset = default(Vector3))
        {
            var p = m.vertices; var n = m.normals; var c = m.colors32; var e = m.uv2; var ix = m.triangles;
            for (int t = 0; t < ix.Length; t += 3)
            {
                var cen = (p[ix[t]] + p[ix[t + 1]] + p[ix[t + 2]]) / 3; if (!keep(c[ix[t]], cen)) continue;
                int i0 = P.Count; for (int j = 0; j < 3; j++) { int s = ix[t + j]; V(p[s] + offset, n[s], c[s], e != null && e.Length > s ? e[s].x : 0); }
                T(i0, i0 + 1, i0 + 2, f(c[ix[t]]), false);
            }
        }
        public void Drop(System.Func<int, Vector3, bool> drop)
        {
            var I2 = new List<int>(); var F2 = new List<int>(); var G2 = new List<int>(); var S2 = new List<bool>(); var K2 = new List<float>();
            for (int t = 0; t < Tris; t++)
            {
                var cen = (P[I[t * 3]] + P[I[t * 3 + 1]] + P[I[t * 3 + 2]]) / 3; if (drop(t, cen)) continue;
                I2.Add(I[t * 3]); I2.Add(I[t * 3 + 1]); I2.Add(I[t * 3 + 2]); F2.Add(F[t]); G2.Add(G[t]); S2.Add(S[t]); K2.Add(K[t]);
            }
            I = I2; F = F2; G = G2; S = S2; K = K2;
        }
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = P.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            var e = new Vector2[E.Count]; for (int i = 0; i < e.Length; i++) e[i] = new Vector2(E[i], 0);
            m.SetVertices(P); m.SetNormals(N); m.SetColors(C); m.uv2 = e; m.SetTriangles(I, 0); m.RecalculateBounds(); return m;
        }
    }

    // ------------------------------------------------------------------ sculpting: cut finer, then move along the normal
    public delegate Vector3 DispFn(Vector3 p, Vector3 n, float k);
    struct Cn { public int w; public Color32 c; public float e; public Vector3 n; }
    struct Tr { public Cn a, b, c; public int f, g; public float k; public bool s; }
    static Cn Mid(Cn x, Cn y, int w) { return new Cn { w = w, c = Color32.Lerp(x.c, y.c, 0.5f), e = (x.e + y.e) * 0.5f, n = (x.n + y.n).normalized }; }
    static long EK(int a, int b) { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }

    /// every triangle with K > 0 is cut until no edge is longer than L (its neighbours are cut along shared edges so
    /// nothing cracks), then its corners are moved by disp; sculpted triangles get smooth normals
    public static B Sculpt(B src, float L, DispFn disp)
    {
        var W = new List<Vector3>(); var map = new Dictionary<Vector3Int, int>(); var wid = new int[src.P.Count];
        for (int i = 0; i < src.P.Count; i++)
        {
            var p = src.P[i]; var key = new Vector3Int(Mathf.RoundToInt(p.x * 10000), Mathf.RoundToInt(p.y * 10000), Mathf.RoundToInt(p.z * 10000));
            int id; if (!map.TryGetValue(key, out id)) { id = W.Count; W.Add(p); map[key] = id; }
            wid[i] = id;
        }
        var tris = new List<Tr>(src.Tris);
        System.Func<int, Cn> CN = i => new Cn { w = wid[i], c = src.C[i], e = src.E[i], n = src.N[i] };
        for (int t = 0; t < src.Tris; t++) tris.Add(new Tr { a = CN(src.I[t * 3]), b = CN(src.I[t * 3 + 1]), c = CN(src.I[t * 3 + 2]), f = src.F[t], g = src.G[t], k = src.K[t], s = src.S[t] || src.K[t] > 0 });
        for (int it = 0; it < 9; it++)
        {
            var split = new Dictionary<long, int>();
            foreach (var t in tris)
            {
                if (t.k <= 0) continue;
                float lt = Mathf.Max(0.035f, Mathf.Min(L, t.k * 0.4f)), l2 = lt * lt;   // small rocks are cut finer
                if ((W[t.a.w] - W[t.b.w]).sqrMagnitude > l2) split[EK(t.a.w, t.b.w)] = -1;
                if ((W[t.b.w] - W[t.c.w]).sqrMagnitude > l2) split[EK(t.b.w, t.c.w)] = -1;
                if ((W[t.c.w] - W[t.a.w]).sqrMagnitude > l2) split[EK(t.c.w, t.a.w)] = -1;
            }
            if (split.Count == 0) break;
            var keys = new List<long>(split.Keys);
            foreach (var key in keys) { int a = (int)(key >> 32), b = (int)(key & 0xffffffff); split[key] = W.Count; W.Add((W[a] + W[b]) * 0.5f); }
            var nt = new List<Tr>(tris.Count * 3);
            foreach (var t in tris)
            {
                var cs = new[] { t.a, t.b, t.c }; var m = new int[3]; int cnt = 0;
                for (int j = 0; j < 3; j++) { int id; m[j] = split.TryGetValue(EK(cs[j].w, cs[(j + 1) % 3].w), out id) ? id : -1; if (m[j] >= 0) cnt++; }
                if (cnt == 0) { nt.Add(t); continue; }
                System.Action<Cn, Cn, Cn> Add = (x, y, z) => { var q = t; q.a = x; q.b = y; q.c = z; nt.Add(q); };
                if (cnt == 3)
                {
                    Cn m0 = Mid(cs[0], cs[1], m[0]), m1 = Mid(cs[1], cs[2], m[1]), m2 = Mid(cs[2], cs[0], m[2]);
                    Add(cs[0], m0, m2); Add(m0, cs[1], m1); Add(m2, m1, cs[2]); Add(m0, m1, m2);
                }
                else if (cnt == 1)
                {
                    int j = m[0] >= 0 ? 0 : (m[1] >= 0 ? 1 : 2); Cn c0 = cs[j], c1 = cs[(j + 1) % 3], c2 = cs[(j + 2) % 3], mm = Mid(c0, c1, m[j]);
                    Add(c0, mm, c2); Add(mm, c1, c2);
                }
                else
                {
                    int j = m[0] < 0 ? 0 : (m[1] < 0 ? 1 : 2); Cn c0 = cs[j], c1 = cs[(j + 1) % 3], c2 = cs[(j + 2) % 3];
                    Cn mA = Mid(c1, c2, m[(j + 1) % 3]), mB = Mid(c2, c0, m[(j + 2) % 3]);
                    Add(c0, c1, mA); Add(c0, mA, mB); Add(mB, mA, c2);
                }
            }
            tris = nt;
        }
        // move the corners that belong only to sculpted triangles
        var nrm = new Vector3[W.Count]; var ks = new float[W.Count]; var kc = new int[W.Count]; var fixd = new bool[W.Count];
        foreach (var t in tris)
        {
            var fn = Vector3.Cross(W[t.b.w] - W[t.a.w], W[t.c.w] - W[t.a.w]); if (Vector3.Dot(fn, t.a.n + t.b.n + t.c.n) < 0) fn = -fn;
            foreach (var w in new[] { t.a.w, t.b.w, t.c.w }) { if (t.k > 0) { nrm[w] += fn; ks[w] += t.k; kc[w]++; } else fixd[w] = true; }
        }
        var W2 = new List<Vector3>(W);
        for (int i = 0; i < W.Count; i++) if (kc[i] > 0 && !fixd[i] && nrm[i].sqrMagnitude > 1e-16f) { var n = nrm[i].normalized; W2[i] = W[i] + disp(W[i], n, ks[i] / kc[i]); }
        var n2 = new Vector3[W.Count];
        foreach (var t in tris)
        {
            if (t.k <= 0) continue;
            var fn = Vector3.Cross(W2[t.b.w] - W2[t.a.w], W2[t.c.w] - W2[t.a.w]); if (Vector3.Dot(fn, t.a.n + t.b.n + t.c.n) < 0) fn = -fn;
            n2[t.a.w] += fn; n2[t.b.w] += fn; n2[t.c.w] += fn;
        }
        var o = new B();
        foreach (var t in tris)
        {
            int i0 = o.P.Count;
            bool facet = t.k > 0 && t.k < 0.45f;   // small stones: chiselled facets
            var ff = Vector3.Cross(W2[t.b.w] - W2[t.a.w], W2[t.c.w] - W2[t.a.w]).normalized; if (Vector3.Dot(ff, t.a.n + t.b.n + t.c.n) < 0) ff = -ff;
            foreach (var c in new[] { t.a, t.b, t.c }) o.V(W2[c.w], facet ? ff : (t.k > 0 && n2[c.w].sqrMagnitude > 1e-16f ? n2[c.w].normalized : c.n), c.c, c.e);
            o.group = t.g; o.k = t.k; o.T(i0, i0 + 1, i0 + 2, t.f, t.s && !facet);
        }
        return o;
    }

    /// craggy rock: lumps, sharp ridges and a few strata ledges, scaled by k (about the rock's radius)
    public static Vector3 RockDisp(Vector3 p, Vector3 n, float k)
    {
        var q = p / Mathf.Max(0.05f, k);
        float d = Fbm(q * 0.55f + new Vector3(3.1f, 0, 7.3f), 3) * 0.42f + (Ridged(q * 1.3f, 3) - 0.4f) * 0.24f + Noise(q * 5f) * 0.03f;
        if (k > 0.7f)
        {   // layered rock: on steep faces each stratum juts out at its foot and leans back above, so ledges catch the light
            float steep = 1 - Mathf.Abs(n.y), band = Mathf.Repeat(p.y / (k * 0.2f) + Fbm(q * 0.8f, 2) * 0.7f, 1f);
            d += steep * (0.5f - band) * 0.15f;
        }
        return n * d * k;
    }
    /// worked stone gone old: shallow chips and pits
    public static Vector3 ChipDisp(Vector3 p, Vector3 n, float k)
    {
        float d = (Ridged(p * 6f, 2) - 0.45f) * 0.03f + Fbm(p * 3f, 2) * 0.025f; float pit = Noise(p * 13f + new Vector3(4, 4, 4));
        if (pit > 0.45f) d -= (pit - 0.45f) * 0.12f;
        return n * d * k;
    }

    // ------------------------------------------------------------------ generic: rocks, ruins, graves, dungeon pieces
    static B Generic(Mesh src, bool chip, float amp, bool broken = false)
    {
        var b = new B(); var bd = src.bounds; float size = Mathf.Max(bd.size.x, Mathf.Max(bd.size.y, bd.size.z));
        b.k = chip ? amp : size * 0.42f * amp; b.Add(src, (c, p) => true, c => 0);
        float L = Mathf.Clamp(size / (chip ? 6.5f : 10f), 0.08f, 0.24f); float top = bd.max.y;
        DispFn D = chip ? (DispFn)ChipDisp : RockDisp;
        if (broken)
        {   // the top broken off: a jagged rim and a cracked top
            DispFn baseD = D;
            D = (p, n, k) =>
            {
                var r = baseD(p, n, k); float h = Mathf.Clamp01((p.y - (top - size * 0.3f)) / (size * 0.3f));
                float bite = Mathf.Clamp01(0.55f + Fbm(new Vector3(p.x * 3.1f, 0.3f, p.z * 3.1f) + new Vector3(2, 0, 5), 3) * 1.6f);
                return r + Vector3.down * h * h * bite * size * 0.32f;
            };
        }
        return Sculpt(b, L, D);
    }

    // ------------------------------------------------------------------ buildings
    static bool IsBuilding(string n)
    {
        if (!Starts(n, "kd_", "tw_")) return false;
        return !Starts(n, "kd_fence", "kd_rail", "kd_hedge", "kd_flower", "kd_hay", "kd_banner", "kd_hstatue", "kd_kstatue", "kd_kingfountain", "kd_brazier", "kd_stand", "tw_bench", "tw_cart", "tw_planter", "tw_notice", "tw_fountain", "tw_wall");
    }
    /// old roofs settle between their beams: a gentle, uneven sag (thatch bulges and dips more)
    static Vector3 RoofDisp(Vector3 p, Vector3 n, float k)
    {
        float d = -0.03f * (0.55f + 0.45f * Fbm(p * 0.6f + new Vector3(7, 1, 3), 2)) + 0.01f * Noise(p * 2.7f) * k;
        return n * d * k;
    }
    /// simple houses and halls: ridge tiles along every ridge, sills under the windows and a lintel over them (models already built tile by tile are left as they are)
    static B Building(Mesh src, string name)
    {
        var ix = src.triangles; int T = ix.Length / 3; if (T > 12000 || T == 0) return null;
        Vector3[] fn; var mat = AldaraStructureRemaster.Surfaces(src, name, out fn);
        var p = src.vertices; var n = src.normals; var c = src.colors32; var e = src.uv2;
        var b = new B(); bool any = false; var roofT = new List<int>();
        for (int t = 0; t < T; t++)
        {
            bool roof = (mat[t] == CLAY || mat[t] == SLATE || mat[t] == THATCH) && fn[t].y > 0.12f && e[ix[t * 3]].x <= 0;
            if (roof) { roofT.Add(t); any = true; }
            b.k = roof ? (mat[t] == THATCH ? 1.8f : 1f) : 0;
            int i0 = b.P.Count; for (int j = 0; j < 3; j++) { int q = ix[t * 3 + j]; b.V(p[q], n[q], c[q], e[q].x); }
            b.T(i0, i0 + 1, i0 + 2, mat[t] > 0 ? mat[t] : 0, false);
        }
        // windows: glowing boxes or panes on the walls
        var bc = src.bounds.center; var wins = new List<Bounds>(); var winN = new List<Vector3>();
        {
            var comp = new int[T]; for (int t = 0; t < T; t++) comp[t] = -1;
            var byPos = new Dictionary<Vector3Int, List<int>>();
            System.Func<int, bool> Lit = t => { var k = c[ix[t * 3]]; return e[ix[t * 3]].x > 0 && k.r > 200 && k.g > 150 && k.r >= k.g && k.g >= k.b && k.b < 200; };   // warm lamplight, not stained glass
            for (int t = 0; t < T; t++)
            {
                if (!Lit(t)) continue;
                for (int j = 0; j < 3; j++) { var q = p[ix[t * 3 + j]]; var key = new Vector3Int(Mathf.RoundToInt(q.x * 500), Mathf.RoundToInt(q.y * 500), Mathf.RoundToInt(q.z * 500)); List<int> l; if (!byPos.TryGetValue(key, out l)) byPos[key] = l = new List<int>(); l.Add(t); }
            }
            int nc = 0;
            for (int t = 0; t < T; t++)
            {
                if (!Lit(t) || comp[t] >= 0) continue;
                var st = new Stack<int>(); st.Push(t); comp[t] = nc; var bb = new Bounds(p[ix[t * 3]], Vector3.zero); 
                while (st.Count > 0)
                {
                    int u = st.Pop(); for (int j = 0; j < 3; j++) bb.Encapsulate(p[ix[u * 3 + j]]);
                    
                    for (int j = 0; j < 3; j++)
                    {
                        var q = p[ix[u * 3 + j]]; var key = new Vector3Int(Mathf.RoundToInt(q.x * 500), Mathf.RoundToInt(q.y * 500), Mathf.RoundToInt(q.z * 500));
                        foreach (var v in byPos[key]) if (comp[v] < 0) { comp[v] = nc; st.Push(v); }
                    }
                }
                nc++;
                // the face that looks out of the building: across the window's thin side, away from the middle
                var outward = bb.center - bc; bool alongX = bb.size.x < bb.size.z;
                var ax = alongX ? new Vector3(Mathf.Sign(outward.x + 1e-5f), 0, 0) : new Vector3(0, 0, Mathf.Sign(outward.z + 1e-5f));
                float w = alongX ? bb.size.z : bb.size.x, h = bb.size.y;
                if (bb.min.y < 0.15f || h < 0.12f || h > 1.4f || w < 0.08f || w > 1.6f) continue;
                wins.Add(bb); winN.Add(ax);
            }
        }
        if (!any && wins.Count == 0) return null;
        var s = b;   // (a sagging roof cost too much geometry for what it showed at the game's distance)
        // ridge tiles: the level top edge of each roof slope that no higher roof face continues
        var caps = new List<KeyValuePair<Vector3, Vector3>>(); var capT = new List<int>();
        foreach (var t in roofT)
        {
            for (int j = 0; j < 3; j++)
            {
                var a = p[ix[t * 3 + (j + 1) % 3]]; var d = p[ix[t * 3 + (j + 2) % 3]]; var o = p[ix[t * 3 + j]];
                float len = (d - a).magnitude; if (len < 0.4f || Mathf.Abs(a.y - d.y) > 0.03f * len || o.y > a.y - 0.05f) continue;
                var mid = (a + d) * 0.5f; bool dup = false;
                for (int k = 0; k < caps.Count && !dup; k++)
                {
                    var ca = caps[k].Key; var cd = caps[k].Value; var cm = (ca + cd) * 0.5f;
                    if ((new Vector2(cm.x - mid.x, cm.z - mid.z)).magnitude < 0.16f && Mathf.Abs(cm.y - mid.y) < 0.2f && Mathf.Abs(Vector3.Dot((cd - ca).normalized, (d - a).normalized)) > 0.97f)
                    { dup = true; if (mid.y > cm.y) { caps[k] = new KeyValuePair<Vector3, Vector3>(a, d); capT[k] = t; } }
                }
                if (!dup) { caps.Add(new KeyValuePair<Vector3, Vector3>(a, d)); capT.Add(t); }
            }
        }
        for (int k = 0; k < caps.Count; k++)
        {
            int t = capT[k]; var a = caps[k].Key; var d = caps[k].Value; var dir = (d - a).normalized; int m = mat[t];
            float r = m == THATCH ? 0.085f : (m == CLAY ? 0.055f : 0.045f); var col = Mul(c[ix[t * 3]], m == THATCH ? 0.85f : 0.78f);
            s.Cyl(a - dir * 0.04f + Vector3.up * r * 0.35f, d + dir * 0.04f + Vector3.up * r * 0.35f, r, r, 8, col, m, true, true);
        }
        // sills and lintels
        for (int k = 0; k < wins.Count; k++)
        {
            var bb = wins[k]; var ax = winN[k]; var right = Vector3.Cross(Vector3.up, ax); float w = ax.x != 0 ? bb.size.z : bb.size.x;
            float face = Vector3.Dot(bb.center, ax) + Vector3.Dot(bb.extents, new Vector3(Mathf.Abs(ax.x), 0, Mathf.Abs(ax.z)));
            var cc = bb.center - ax * Vector3.Dot(bb.center, ax) + ax * face; var q = Quaternion.LookRotation(ax, Vector3.up);
            s.Box(new Vector3(cc.x, bb.min.y - 0.022f, cc.z) + ax * 0.03f, new Vector3(w + 0.09f, 0.045f, 0.085f), q, new Color32(0x8a, 0x84, 0x7a, 255), STONE);
            s.Box(new Vector3(cc.x, bb.max.y + 0.018f, cc.z) + ax * 0.012f, new Vector3(w + 0.06f, 0.035f, 0.045f), q, WOOD2, BEAM);
        }
        return s;
    }

    // ------------------------------------------------------------------ fire
    /// tongues of flame: curved, tapering, leaning out from a hot white-yellow core
    public static void Flame(B b, Vector3 at, float r, float h, int seed)
    {
        var rng = new System.Random(seed); System.Func<float> R = () => (float)rng.NextDouble();
        for (int i = 0; i < 7; i++)
        {
            bool inner = i >= 5; float a = R() * Mathf.PI * 2, d = inner ? r * 0.15f : r * (0.25f + R() * 0.45f);
            var p0 = at + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d); float hh = h * (inner ? 0.55f + R() * 0.15f : 0.55f + R() * 0.45f), rr = (inner ? 0.45f : 0.5f + R() * 0.35f) * r;
            var lean = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * hh * 0.25f + new Vector3(R() - 0.5f, 0, R() - 0.5f) * hh * 0.2f;
            Vector3 prev = p0; float pr = rr;
            for (int k = 1; k <= 3; k++)
            {
                float t = k / 3f; var q = p0 + Vector3.up * hh * t + lean * t * t; float qr = rr * (1 - t) * (1 - t * 0.2f);
                b.Cyl(prev, q, pr, Mathf.Max(0.002f, qr), 6, inner ? FIRE2 : (k == 1 ? FIRE2 : FIRE), NONE, k == 1, false, 1);
                prev = q; pr = qr;
            }
        }
    }
    static B Campfire(Mesh src)
    {
        var rng = new System.Random(18); System.Func<float> R = () => (float)rng.NextDouble(); var b = new B();
        // a ring of fire-blackened stones
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2 / 10 + R() * 0.2f, rr = 0.075f + R() * 0.03f, d = 0.34f + R() * 0.03f; b.k = rr;
            b.Rock(new Vector3(Mathf.Cos(a) * d, rr * 0.45f, Mathf.Sin(a) * d), new Vector3(rr * 1.3f, rr * 0.8f, rr), Quaternion.Euler(0, a * Mathf.Rad2Deg + R() * 30, R() * 20), Mul(new Color32(0x5a, 0x5a, 0x5a, 255), 0.75f + R() * 0.35f), ROCK);
        }
        b.k = 0; var s = Sculpt(b, 0.1f, RockDisp);
        // ash and embers
        var ash = new List<Vector3>(); var emb = new List<Vector3>();
        for (int i = 0; i < 14; i++) { float a = i * Mathf.PI * 2 / 14, j = 1 + (R() - 0.5f) * 0.2f; ash.Add(new Vector3(Mathf.Cos(a) * 0.3f * j, 0.012f, Mathf.Sin(a) * 0.3f * j)); emb.Add(new Vector3(Mathf.Cos(a) * 0.17f * j, 0.02f, Mathf.Sin(a) * 0.17f * j)); }
        s.Fan(ash, new Vector3(0, 0.015f, 0), new Color32(0x2a, 0x26, 0x22, 255), new Color32(0x1e, 0x1a, 0x16, 255), NONE, Vector3.up);
        int st = s.P.Count; s.Fan(emb, new Vector3(0, 0.025f, 0), new Color32(0x5a, 0x1a, 0x06, 255), new Color32(0xff, 0x6a, 0x1a, 255), NONE, Vector3.up); for (int i = st; i < s.P.Count; i++) s.E[i] = 0.8f;
        // logs leaning together, charred where they burn
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2 / 5 + 0.3f; var foot = new Vector3(Mathf.Cos(a) * 0.27f, 0.03f, Mathf.Sin(a) * 0.27f); var head = new Vector3(Mathf.Cos(a) * 0.04f, 0.26f, Mathf.Sin(a) * 0.04f);
            var mid = Vector3.Lerp(foot, head, 0.5f);
            s.Cyl(foot, mid, 0.034f, 0.031f, 6, new Color32(0x4a, 0x2e, 0x1a, 255), BEAM, true, false); s.Cyl(mid, head, 0.031f, 0.02f, 6, new Color32(0x22, 0x16, 0x10, 255), BEAM, false, true);
        }
        Flame(s, new Vector3(0, 0.04f, 0), 0.14f, 0.5f, 3);
        return s;
    }

    // ------------------------------------------------------------------ caves
    static Color32 Mul(Color32 c, float k) { return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255); }
    static Color32 Hex(string h) { return new Color32(System.Convert.ToByte(h.Substring(0, 2), 16), System.Convert.ToByte(h.Substring(2, 2), 16), System.Convert.ToByte(h.Substring(4, 2), 16), 255); }
    static readonly Color32 WOOD = new Color32(0x5a, 0x3a, 0x1e, 255), WOOD2 = new Color32(0x4a, 0x2e, 0x18, 255), FIRE = new Color32(0xff, 0x7a, 0x2a, 255), FIRE2 = new Color32(0xff, 0xd3, 0x5a, 255), ROPE = new Color32(0xb8, 0xa4, 0x80, 255), IRON = new Color32(0x3a, 0x3a, 0x3e, 255);
    static void Torch(B b, Vector3 at, Vector3 lean)
    {
        var top = at + lean.normalized * 0.26f;
        b.Cyl(at, top, 0.024f, 0.03f, 6, WOOD2, BEAM, true, false);
        b.Cyl(top - lean.normalized * 0.05f, top + lean.normalized * 0.01f, 0.038f, 0.04f, 6, IRON, METAL, false, true);
        b.Cyl(top, top + Vector3.up * 0.2f, 0.06f, 0.0f, 6, FIRE, NONE, true, false, 1);
        b.Cyl(top + Vector3.up * 0.01f, top + Vector3.up * 0.13f, 0.036f, 0.0f, 5, FIRE2, NONE, true, false, 1);
    }
    static B Cave(Mesh src)
    {
        var dark = new Color32(0x0a, 0x08, 0x06, 255); var rng = new System.Random(57);
        System.Func<float> R = () => (float)rng.NextDouble();
        // the entrance: where the browser put its dark mouth
        float w = 2.0f, h = 1.55f, hs = 0.5f, depth = 2.4f, z0 = -1.12f;
        System.Func<float, float> HalfW = y => y < hs ? w / 2 : (y >= h ? 0 : w / 2 * Mathf.Pow(Mathf.Max(0, 1 - Mathf.Pow((y - hs) / (h - hs), 3)), 1 / 3f));
        System.Func<float, float> ArchY = x => { float r = Mathf.Abs(x) / (w / 2); return r >= 1 ? 0 : hs + (h - hs) * Mathf.Pow(1 - r * r * r, 1 / 3f); };
        var b = new B();
        // 1: the outcrop
        b.group = 1; b.k = 1.5f; b.Add(src, (c, p) => !(c.r == dark.r && c.g == dark.g && c.b == dark.b), c => ROCK);
        // 2: the lip around the mouth, rubble below
        b.group = 2;
        for (int i = 0; i <= 12; i++)
        {
            float t = i / 12f, x = Mathf.Lerp(w / 2 + 0.12f, -(w / 2 + 0.12f), t); float y = Mathf.Max(0.05f, ArchY(Mathf.Clamp(x, -w / 2 + 0.01f, w / 2 - 0.01f)) + 0.1f);
            if (Mathf.Abs(x) > w / 2) y = 0.12f + R() * 0.25f;
            float r = 0.2f + R() * 0.16f; b.k = r; b.Rock(new Vector3(x, y, z0 + 0.02f + R() * 0.08f), new Vector3(r * (1.1f + R() * 0.4f), r * (0.75f + R() * 0.3f), r * 0.8f), Quaternion.Euler(R() * 40, R() * 360, R() * 40), Mul(new Color32(0x4a, 0x42, 0x38, 255), 0.72f + R() * 0.2f), ROCK);
        }
        for (int i = 0; i < 16; i++)
        {
            float x = (R() * 2 - 1) * 3.1f; if (Mathf.Abs(x) < 0.85f) x = Mathf.Sign(x + 1e-3f) * (0.85f + R() * 0.4f);
            float r = 0.07f + R() * 0.2f; b.k = r;
            b.Rock(new Vector3(x, r * 0.25f, z0 - 0.15f - R() * 0.7f), new Vector3(r * (1 + R() * 0.5f), r * 0.6f, r), Quaternion.Euler(0, R() * 360, R() * 25), Mul(new Color32(0x52, 0x4a, 0x40, 255), 0.7f + R() * 0.25f), ROCK);
        }
        b.k = 0;
        var s = Sculpt(b, 0.2f, RockDisp);
        // carve the mouth out of the outcrop
        s.Drop((t, c) => s.G[t] == 1 && c.z < z0 + depth * 0.85f && c.y < h + 0.05f && c.y > -0.4f && Mathf.Abs(c.x) < HalfW(Mathf.Max(0, c.y)) * 1.04f);
        // 3: the tunnel, going down into the dark
        s.group = 3; s.k = 0; int K = 8, M = 18; var rock = new Color32(0x3e, 0x37, 0x2f, 255);
        var ring = new List<int>[K + 1];
        for (int k = 0; k <= K; k++)
        {
            float t = k / (float)K, sc = 1 - 0.32f * t, yb = -0.35f * t * t, z = z0 - 0.05f + depth * t; ring[k] = new List<int>();
            var col = Color32.Lerp(Mul(rock, 1.15f), dark, Mathf.Pow(t, 1.1f));
            for (int j = 0; j <= M; j++)
            {
                float a = j / (float)M; float x = Mathf.Lerp(w / 2, -w / 2, a) * 0.98f; float y = ArchY(Mathf.Clamp(x, -w / 2 + 0.001f, w / 2 - 0.001f)) * 0.99f; if (j == 0 || j == M) y = 0;
                float jit = Noise(new Vector3(x * 2.1f, y * 2.1f, z * 1.7f)) * 0.07f;
                var p = new Vector3(x * sc, yb + y * sc, z); var ctr = new Vector3(0, yb + h * 0.4f * sc, z); var nIn = (ctr - p); nIn.z = 0; nIn.Normalize();
                p -= nIn * jit; ring[k].Add(s.V(p, nIn, col, 0));
            }
        }
        for (int k = 0; k < K; k++) for (int j = 0; j < M; j++) { int a = ring[k][j], b2 = ring[k][j + 1], c = ring[k + 1][j + 1], d = ring[k + 1][j]; s.T(a, b2, c, ROCK, true); s.T(a, c, d, ROCK, true); }
        // its floor and the dark at the end
        for (int k = 0; k < K; k++)
        {
            int a = ring[k][0], b2 = ring[k][M], c = ring[k + 1][M], d = ring[k + 1][0];
            var cA = s.C[a]; var cB = s.C[c];
            int i0 = s.V(s.P[a], Vector3.up, Mul(cA, 0.8f), 0); s.V(s.P[b2], Vector3.up, Mul(cA, 0.8f), 0); s.V(s.P[c], Vector3.up, Mul(cB, 0.8f), 0); s.V(s.P[d], Vector3.up, Mul(cB, 0.8f), 0);
            s.T(i0, i0 + 1, i0 + 2, ROCK, true); s.T(i0, i0 + 2, i0 + 3, ROCK, true);
        }
        var endLoop = new List<Vector3>(); foreach (var i in ring[K]) endLoop.Add(s.P[i]);
        var endC = Vector3.zero; foreach (var q in endLoop) endC += q; endC /= endLoop.Count; s.Fan(endLoop, endC, dark, dark, NONE, Vector3.back);
        { // a faint firelight far inside
            var gl = new List<Vector3>(); foreach (var q in endLoop) gl.Add(Vector3.Lerp(endC, q, 0.55f) + Vector3.back * 0.02f);
            int i0 = s.Tris; s.Fan(gl, endC + Vector3.back * 0.02f, new Color32(0x14, 0x08, 0x03, 255), new Color32(0x6a, 0x2a, 0x0a, 255), NONE, Vector3.back);
            for (int i = s.P.Count - gl.Count - 1; i < s.P.Count; i++) s.E[i] = 0.55f;
        }
        // 4: timber props, lintel and braces, two torches
        s.group = 4;
        float zp = z0 + 0.3f;
        s.Box(new Vector3(-0.78f, 0.6f, zp), new Vector3(0.13f, 1.22f, 0.13f), Quaternion.Euler(0, 4, 3), WOOD, BEAM);
        s.Box(new Vector3(0.78f, 0.6f, zp), new Vector3(0.13f, 1.22f, 0.13f), Quaternion.Euler(0, -3, -2.5f), WOOD, BEAM);
        s.Box(new Vector3(0, 1.26f, zp - 0.01f), new Vector3(1.9f, 0.14f, 0.16f), Quaternion.Euler(0, 1, 1.5f), WOOD2, BEAM);
        s.Box(new Vector3(-0.6f, 1.08f, zp), new Vector3(0.36f, 0.08f, 0.08f), Quaternion.Euler(0, 0, 45), WOOD2, BEAM);
        s.Box(new Vector3(0.6f, 1.08f, zp), new Vector3(0.36f, 0.08f, 0.08f), Quaternion.Euler(0, 0, -45), WOOD2, BEAM);
        // torches on iron brackets in the rock either side of the mouth
        foreach (var sx in new[] { -1, 1 })
        {
            float fz = z0 - 0.12f; for (int t = 0; t < s.Tris; t++) { if (s.G[t] != 1 && s.G[t] != 2) continue; var q = s.P[s.I[t * 3]]; if (Mathf.Abs(q.x - sx * 1.32f) < 0.14f && Mathf.Abs(q.y - 0.95f) < 0.14f) fz = Mathf.Min(fz, q.z); }
            var mount = new Vector3(sx * 1.32f, 0.95f, fz - 0.02f);
            s.Box(mount + new Vector3(0, 0, 0.06f), new Vector3(0.05f, 0.16f, 0.14f), Quaternion.identity, IRON, METAL);
            Torch(s, mount + new Vector3(0, -0.06f, -0.04f), new Vector3(sx * 0.25f, 1, -0.45f));
        }
        // 5: rock teeth hanging over the mouth
        for (int i = 0; i < 7; i++)
        {
            float x = Mathf.Lerp(-0.62f, 0.62f, i / 6f) + (R() - 0.5f) * 0.12f, y = ArchY(x) * 0.97f, len = 0.1f + R() * 0.22f;
            s.Cyl(new Vector3(x, y + 0.03f, z0 + 0.12f + R() * 0.25f), new Vector3(x + (R() - 0.5f) * 0.05f, y - len, z0 + 0.15f + R() * 0.2f), 0.045f + R() * 0.035f, 0.004f, 6, Mul(rock, 0.9f + R() * 0.3f), ROCK, true, false);
        }
        return s;
    }

    // ------------------------------------------------------------------ tents
    static B Tent(Mesh src, string name)
    {
        var canvas = name.Length >= 11 ? Hex(name.Substring(5, 6)) : new Color32(0x8a, 0x3a, 0x2a, 255);
        var rng = new System.Random(name.GetHashCode()); System.Func<float> R = () => (float)rng.NextDouble();
        float W = 1.14f, Hr = 1.72f, D = 1.02f, wall = 0.17f; var b = new B();
        var sideIn = new Vector3(0, 0.55f, 0);
        // the slopes: sagging between the poles, a few wrinkles
        for (int sd = -1; sd <= 1; sd += 2)
        {
            int side = sd; var outN = new Vector3(side * (Hr - wall), W, 0).normalized;
            System.Func<float, float, Vector3> P = (u, v) =>
            {
                float z = Mathf.Lerp(-D, D, u); var q = new Vector3(side * W * (1 - v), wall + (Hr - wall) * v, z);
                float sag = 0.12f * Mathf.Sin(Mathf.PI * v) * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.7f) + 0.024f * Noise(new Vector3(u * 7, v * 5, side * 3.3f)) * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(Mathf.PI * v);
                return q - outN * sag;
            };
            b.Sheet(P, 14, 8, sideIn, (u, v) => Mul(canvas, 0.9f + 0.12f * v), LEATHER);
            // the short wall below
            b.Sheet((u, v) => new Vector3(side * (W + 0.025f * (1 - v)), wall * v, Mathf.Lerp(-D, D, u)), 14, 1, sideIn, (u, v) => Mul(canvas, 0.72f + 0.15f * v), LEATHER);
            // sewn seams and a patch
            b.soft = true;
            for (int si = 1; si <= 2; si++)
            {
                float u0 = si / 3f; for (int j = 0; j < 8; j++)
                {
                    float v0 = j / 8f, v1 = (j + 1) / 8f; var lift = outN * 0.006f;
                    b.Quad(P(u0 - 0.006f, v0) + lift, P(u0 + 0.006f, v0) + lift, P(u0 + 0.006f, v1) + lift, P(u0 - 0.006f, v1) + lift, Mul(canvas, 0.68f), LEATHER, outN);
                }
            }
            float pu = 0.25f + R() * 0.5f, pv = 0.2f + R() * 0.45f, du = 0.05f + R() * 0.03f, dv = 0.07f + R() * 0.04f; var pl = outN * 0.008f;
            b.Quad(P(pu - du, pv - dv) + pl, P(pu + du, pv - dv) + pl, P(pu + du, pv + dv) + pl, P(pu - du, pv + dv) + pl, Mul(Color32.Lerp(canvas, new Color32(0x6a, 0x5a, 0x48, 255), 0.5f), 0.9f), LEATHER, outN);
            b.soft = false;
        }
        // back gable, bowed in a little
        var back = new List<Vector3> { new Vector3(-W, 0, D), new Vector3(W, 0, D), new Vector3(W, wall, D), new Vector3(0, Hr, D), new Vector3(-W, wall, D) };
        b.Fan(back, new Vector3(0, 0.6f, D - 0.05f), Mul(canvas, 0.85f), Mul(canvas, 0.8f), LEATHER, Vector3.forward);
        // front gable round the door
        var fz = -D; Vector3 apex = new Vector3(0, 1.12f, fz), dl = new Vector3(-0.46f, 0, fz), dr = new Vector3(0.46f, 0, fz);
        for (int sd = -1; sd <= 1; sd += 2)
        {
            var c0 = new Vector3(sd * W, 0, fz); var c1 = new Vector3(sd * W, wall, fz); var top = new Vector3(0, Hr, fz); var dd = sd < 0 ? dl : dr;
            b.Tri(c0, c1, dd, Mul(canvas, 0.86f), LEATHER, Vector3.back); b.Tri(c1, top, apex, Mul(canvas, 0.86f), LEATHER, Vector3.back); b.Tri(c1, apex, dd, Mul(canvas, 0.86f), LEATHER, Vector3.back);
            // the door flap tied back, its edge rolled
            var fl = new Vector3(sd * 0.86f, 0.1f, fz - 0.035f);
            b.Tri(apex + new Vector3(0, -0.02f, -0.02f), dd + new Vector3(0, 0, -0.025f), fl, Mul(canvas, 0.78f), LEATHER, Vector3.back);
            b.Cyl(apex + new Vector3(sd * 0.03f, -0.05f, -0.04f), fl + new Vector3(0, 0.03f, -0.01f), 0.03f, 0.036f, 6, Mul(canvas, 0.7f), LEATHER, true, true);
            var tie = Vector3.Lerp(apex, fl, 0.55f) + new Vector3(0, 0, -0.045f); b.Cyl(tie + new Vector3(-0.025f, 0.03f, 0), tie + new Vector3(0.025f, -0.03f, 0), 0.044f, 0.044f, 6, ROPE, CLOTH, false, false);
        }
        // inside: dark, going back
        var inside = new Color32(0x1a, 0x10, 0x08, 255); int K = 3;
        var rings = new List<int>[K + 1];
        for (int k = 0; k <= K; k++)
        {
            float t = k / (float)K, sc = 1 - 0.15f * t, z = fz + 0.02f + t * 0.75f; rings[k] = new List<int>();
            var col = Color32.Lerp(Mul(canvas, 0.35f), inside, t);
            foreach (var q in new[] { dl, apex, dr }) rings[k].Add(b.V(new Vector3(q.x * sc, q.y * sc, z), Vector3.back, col, 0));
        }
        for (int k = 0; k < K; k++) for (int j = 0; j < 2; j++) { int a = rings[k][j], c2 = rings[k][j + 1], d = rings[k + 1][j + 1], e = rings[k + 1][j]; b.T(a, c2, d, NONE, true); b.T(a, d, e, NONE, true); }
        { int a = rings[0][0], c2 = rings[0][2], d = rings[K][2], e = rings[K][0]; int i0 = b.V(b.P[a] + Vector3.up * 0.01f, Vector3.up, Mul(inside, 1.4f), 0); b.V(b.P[c2] + Vector3.up * 0.01f, Vector3.up, Mul(inside, 1.4f), 0); b.V(b.P[d] + Vector3.up * 0.01f, Vector3.up, inside, 0); b.V(b.P[e] + Vector3.up * 0.01f, Vector3.up, inside, 0); b.T(i0, i0 + 1, i0 + 2, NONE, true); b.T(i0, i0 + 2, i0 + 3, NONE, true); }
        b.Tri(b.P[rings[K][0]], b.P[rings[K][1]], b.P[rings[K][2]], inside, NONE, Vector3.back);
        // A-frame poles at both ends and the ridge pole
        foreach (var zz in new[] { -D - 0.06f, D + 0.06f })
        {
            b.Cyl(new Vector3(-0.45f, 0, zz), new Vector3(0.1f, Hr + 0.36f, zz), 0.034f, 0.026f, 6, WOOD, BEAM, false, true);
            b.Cyl(new Vector3(0.45f, 0, zz), new Vector3(-0.1f, Hr + 0.36f, zz), 0.034f, 0.026f, 6, WOOD2, BEAM, false, true);
        }
        b.Cyl(new Vector3(0, Hr + 0.03f, -D - 0.24f), new Vector3(0, Hr + 0.03f, D + 0.24f), 0.03f, 0.03f, 6, WOOD, BEAM, true, true);
        // guy ropes and stakes
        System.Action<Vector3, Vector3> Rope = (a, c) =>
        {
            Vector3 prev = a; for (int i = 1; i <= 4; i++) { float t = i / 4f; var q = Vector3.Lerp(a, c, t) + Vector3.down * Mathf.Sin(Mathf.PI * t) * 0.04f; b.Cyl(prev, q, 0.011f, 0.011f, 4, ROPE, CLOTH, false, false); prev = q; }
            b.Box(c + new Vector3(0, 0.04f, 0), new Vector3(0.045f, 0.2f, 0.045f), Quaternion.LookRotation((a - c).normalized) * Quaternion.Euler(70, 0, 0), WOOD2, BEAM);
        };
        foreach (var sz in new[] { -1, 1 }) foreach (var sx in new[] { -1, 1 }) Rope(new Vector3(0, Hr + 0.25f, sz * (D + 0.06f)), new Vector3(sx * 0.55f, 0.02f, sz * (D + 0.75f)));
        foreach (var sx in new[] { -1, 1 }) foreach (var zz in new[] { -0.5f, 0.5f }) Rope(new Vector3(sx * W * 0.97f, wall + 0.12f, zz), new Vector3(sx * (W + 0.5f), 0.02f, zz * 1.1f));
        // a fur at the door
        var fur = new List<Vector3>(); for (int i = 0; i < 12; i++) { float a = i * Mathf.PI * 2 / 12; float r = 1 + (R() - 0.5f) * 0.35f; fur.Add(new Vector3(Mathf.Cos(a) * 0.36f * r, 0.012f, fz - 0.32f + Mathf.Sin(a) * 0.2f * r)); }
        b.Fan(fur, new Vector3(0, 0.016f, fz - 0.32f), new Color32(0x4a, 0x34, 0x26, 255), new Color32(0x5a, 0x40, 0x2c, 255), LEATHER, Vector3.up);
        return b;
    }

    // ------------------------------------------------------------------ lair pillars
    static readonly float[][] RUNES = {
        new float[] { 0, -.5f, 0, .5f, 0, .5f, .35f, .2f, 0, .15f, .35f, -.15f },
        new float[] { -.2f, -.5f, -.2f, .5f, -.2f, .5f, .25f, .2f, .25f, .2f, -.2f, 0, -.2f, 0, .25f, -.5f },
        new float[] { 0, -.5f, 0, .5f, 0, .15f, -.35f, .5f, 0, .15f, .35f, .5f },
        new float[] { -.3f, -.5f, .3f, .5f, .3f, -.5f, -.3f, .5f, -.3f, -.5f, -.3f, .5f, .3f, -.5f, .3f, .5f },
        new float[] { 0, .5f, .3f, .1f, .3f, .1f, 0, -.25f, 0, -.25f, -.3f, .1f, -.3f, .1f, 0, .5f, 0, -.25f, -.25f, -.5f, 0, -.25f, .25f, -.5f },
        new float[] { 0, -.5f, 0, .5f, 0, .5f, -.3f, .2f, 0, .5f, .3f, .2f },
        new float[] { -.25f, -.5f, -.25f, .5f, .25f, -.5f, .25f, .5f, -.25f, .2f, .25f, -.2f },
    };
    static B LairPillar(Mesh src, bool fire)
    {
        var rng = new System.Random(fire ? 31 : 17); System.Func<float> R = () => (float)rng.NextDouble();
        var b = new B(); var plinth = new Color32(0x3a, 0x34, 0x40, 255); var body = new Color32(0x4a, 0x44, 0x50, 255); var cap = new Color32(0x5a, 0x54, 0x62, 255); var rune = new Color32(0x8a, 0x6a, 0xff, 255);
        b.k = 0.6f;
        b.Box(new Vector3(0, 0.06f, 0), new Vector3(0.8f, 0.12f, 0.8f), Quaternion.Euler(0, 3, 0), plinth, ROCK);
        b.Box(new Vector3(0.01f, 0.165f, -0.005f), new Vector3(0.64f, 0.11f, 0.64f), Quaternion.Euler(0, -4, 0), Mul(plinth, 1.08f), ROCK);
        float[] hs = { 0.78f, 0.72f, 0.68f }, ws = { 0.5f, 0.47f, 0.45f }, ry = { 2, -5, 4 }, tint = { 1f, 0.93f, 1.07f };
        float y = 0.22f; var blocks = new List<KeyValuePair<Vector3, Quaternion>>(); var bsz = new List<Vector3>();
        for (int i = 0; i < 3; i++)
        {
            var c = new Vector3((R() - 0.5f) * 0.03f, y + hs[i] / 2, (R() - 0.5f) * 0.03f); var q = Quaternion.Euler((R() - 0.5f) * 2, ry[i], (R() - 0.5f) * 2); var sz = new Vector3(ws[i], hs[i] - 0.012f, ws[i]);
            b.Box(c, sz, q, Mul(body, tint[i]), ROCK); blocks.Add(new KeyValuePair<Vector3, Quaternion>(c, q)); bsz.Add(sz); y += hs[i];
        }
        b.Box(new Vector3(0, 2.49f, 0), new Vector3(0.8f, 0.18f, 0.8f), Quaternion.Euler(0, 1.5f, 0), cap, ROCK);
        for (int i = 0; i < 6; i++)
        {
            float a = R() * Mathf.PI * 2, d = 0.48f + R() * 0.2f, r = 0.05f + R() * 0.08f;
            b.Rock(new Vector3(Mathf.Cos(a) * d, r * 0.3f, Mathf.Sin(a) * d), new Vector3(r * 1.3f, r * 0.7f, r), Quaternion.Euler(0, R() * 360, R() * 30), Mul(plinth, 0.9f + R() * 0.3f), ROCK);
        }
        b.k = 0;
        var s = Sculpt(b, 0.18f, ChipDisp);
        // carved runes, glowing, two to a face of each block
        for (int i = 0; i < 3; i++)
        {
            var c = blocks[i].Key; var q = blocks[i].Value; var sz = bsz[i];
            for (int f = 0; f < 4; f++)
            {
                var fq = q * Quaternion.Euler(0, f * 90, 0); var nrm = fq * Vector3.back; var right = fq * Vector3.right; var up = fq * Vector3.up;
                var fc = c + nrm * (sz.x / 2 + 0.022f);
                for (int g = 0; g < 2; g++)
                {
                    var gc = fc + up * ((g - 0.5f) * sz.y * 0.48f); var st = RUNES[rng.Next(RUNES.Length)]; float cell = 0.15f, wd = 0.016f;
                    for (int k = 0; k + 3 < st.Length; k += 4)
                    {
                        var a = gc + (right * st[k] + up * st[k + 1]) * cell; var e = gc + (right * st[k + 2] + up * st[k + 3]) * cell;
                        var along = (e - a).normalized; var side = Vector3.Cross(nrm, along).normalized * wd * 0.5f; a -= along * wd * 0.4f; e += along * wd * 0.4f;
                        s.Quad(a - side, e - side, e + side, a + side, rune, NONE, nrm, 1);
                    }
                }
            }
        }
        // the fire bowl and its flames, as they were
        if (fire)
        {
            s.k = 0; s.Add(src, (c, p) => p.y > 2.58f && c.r == 0x2a && c.g == 0x2a, c => METAL);
            Flame(s, new Vector3(0, 2.66f, 0), 0.13f, 0.42f, 7);
        }
        return s;
    }
}
