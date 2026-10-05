using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Aldara;

// Trees and bushes, enhanced (Graphics, enhanced rendering). Each keeps its browser shape and colours, and gets:
// - leaf cards: tufts of leaves (or needles, willow strands, blossom, by kind) cut from a drawn leaf atlas, scattered
//   over every crown so the edges break up into leaves, shaded round the crown's middle, darker deep inside it;
// - the crowns themselves rounded (normals from their middle) and drawn in a little so the leaves stand proud;
// - bark: trunks and branches smoothed and furrowed;
// all drawn by Aldara/Foliage (and Aldara/FoliageFade when a tree stands in front of the hero).
public static class AldaraTreeRemaster
{
    const string ROOT = "Assets/_Aldara";
    static bool Starts(string n, params string[] p) { foreach (var s in p) if (n.StartsWith(s)) return true; return false; }
    public static bool IsBush(string n) { return Starts(n, "prop_bush", "prop_goldBush", "prop_snowBush", "prop_thornBush", "prop_ashBush", "prop_frostShrub"); }

    // atlas cells: 0 broadleaf, 1 needles, 2 willow, 3 blossom (uv cell origin)
    static readonly Vector2[] CELL = { new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(0.5f, 0) };

    public static Mesh Foliate(Mesh src, string name)
    {
        var P = src.vertices; var N = src.normals; var C = src.colors32; var E = src.uv2; var I = src.triangles; int T = I.Length / 3;
        bool crystal = name.Contains("crystal"), elysian = name.Contains("elysian"), pine = name.Contains("pine"), willow = name.Contains("willow"), bush = IsBush(name);
        bool snowy = name.Contains("snow") || name.Contains("frost");
        var kind = new int[T];   // 0 other, 1 foliage, 2 wood
        for (int t = 0; t < T; t++)
        {
            var c = C[I[t * 3]]; float h, s, vl; Color.RGBToHSV(c, out h, out s, out vl); h *= 360;
            bool em = E != null && E.Length > I[t * 3] && E[I[t * 3]].x > 0;
            if (em || crystal) { kind[t] = 0; continue; }
            bool wood = (h >= 8 && h <= 52 && s > 0.2f && s < 0.8f && vl < 0.5f) || (s < 0.25f && vl < 0.36f) || (elysian && s < 0.16f && vl > 0.6f && vl < 0.95f && c.r > c.b);
            bool snow = s < 0.14f && vl > 0.82f && !elysian;
            if (wood && !bush) kind[t] = 2; else if (snow && !snowy) kind[t] = 0; else kind[t] = 1;
            if (name.Contains("dead") || name.Contains("charred")) kind[t] = em ? 0 : 2;
        }
        // weld positions, components of foliage (each crown blob)
        var weld = new Dictionary<Vector3Int, int>(); var wid = new int[P.Length];
        for (int i = 0; i < P.Length; i++) { var k = new Vector3Int(Mathf.RoundToInt(P[i].x * 500), Mathf.RoundToInt(P[i].y * 500), Mathf.RoundToInt(P[i].z * 500)); int id; if (!weld.TryGetValue(k, out id)) { id = weld.Count; weld[k] = id; } wid[i] = id; }
        var par = new int[weld.Count]; for (int i = 0; i < par.Length; i++) par[i] = i;
        System.Func<int, int> F = null; F = x => { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; };
        for (int t = 0; t < T; t++) if (kind[t] == 1) { int a = F(wid[I[t * 3]]), b = F(wid[I[t * 3 + 1]]), c = F(wid[I[t * 3 + 2]]); par[b] = a; par[F(c)] = a; }
        var comp = new Dictionary<int, List<int>>();
        for (int t = 0; t < T; t++) if (kind[t] == 1) { int r = F(wid[I[t * 3]]); List<int> l; if (!comp.TryGetValue(r, out l)) comp[r] = l = new List<int>(); l.Add(t); }
        float fy0 = float.MaxValue, fy1 = float.MinValue; foreach (var kv in comp) foreach (var t in kv.Value) for (int j = 0; j < 3; j++) { fy0 = Mathf.Min(fy0, P[I[t * 3 + j]].y); fy1 = Mathf.Max(fy1, P[I[t * 3 + j]].y); }
        if (fy1 < fy0) { fy0 = 0; fy1 = 1; }
        var center = new Dictionary<int, Vector3>(); var radius = new Dictionary<int, float>();
        foreach (var kv in comp)
        {
            Vector3 s = Vector3.zero; float A = 0; foreach (var t in kv.Value) { var a = P[I[t * 3]]; var b = P[I[t * 3 + 1]]; var c = P[I[t * 3 + 2]]; float ar = Vector3.Cross(b - a, c - a).magnitude * 0.5f; s += (a + b + c) / 3 * ar; A += ar; }
            var cc = A > 0 ? s / A : P[I[kv.Value[0] * 3]]; float rr = 0; foreach (var t in kv.Value) rr += (P[I[t * 3]] - cc).magnitude; rr /= kv.Value.Count;
            center[kv.Key] = cc; radius[kv.Key] = Mathf.Max(0.05f, rr);
        }
        // smooth wood normals (welded, within 60 degrees)
        var wn = new Dictionary<int, Vector3>();
        for (int t = 0; t < T; t++) if (kind[t] == 2) { var a = P[I[t * 3]]; var fn = Vector3.Cross(P[I[t * 3 + 1]] - a, P[I[t * 3 + 2]] - a); for (int j = 0; j < 3; j++) { int w = wid[I[t * 3 + j]]; Vector3 x; wn.TryGetValue(w, out x); wn[w] = x + fn; } }

        var v = new List<Vector3>(); var n = new List<Vector3>(); var col = new List<Color32>(); var uv = new List<Vector4>(); var e2 = new List<Vector2>(); var ix = new List<int>();
        System.Func<float, float> AOh = y => Mathf.Lerp(0.6f, 1f, Mathf.Clamp01((y - fy0) / Mathf.Max(0.01f, fy1 - fy0)));
        for (int t = 0; t < T; t++)
        {
            int root = kind[t] == 1 ? F(wid[I[t * 3]]) : -1; var fn = Vector3.Cross(P[I[t * 3 + 1]] - P[I[t * 3]], P[I[t * 3 + 2]] - P[I[t * 3]]).normalized;
            for (int j = 0; j < 3; j++)
            {
                int si = I[t * 3 + j]; var p = P[si]; var nn = N != null && N.Length == P.Length ? N[si] : fn; float ao = 1;
                if (kind[t] == 1)
                {
                    var cc = center[root]; var d = p - cc; p = cc + d * 0.93f; nn = Vector3.Lerp(nn, d.normalized, 0.75f).normalized; ao = AOh(p.y) * Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(d.normalized.y * 0.5f + 0.5f));
                }
                else if (kind[t] == 2) { Vector3 x; if (wn.TryGetValue(wid[si], out x) && Vector3.Dot(x.normalized, fn) > 0.5f) nn = x.normalized; ao = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(1 - (p.y - fy0) / Mathf.Max(0.01f, fy1 - fy0))) ; }
                v.Add(p); n.Add(nn); col.Add(C[si]); uv.Add(new Vector4(0, 0, kind[t] == 2 ? 2 : 0, ao)); e2.Add(new Vector2(E != null && E.Length > si ? E[si].x : 0, 0)); ix.Add(v.Count - 1);
            }
        }
        // leaf cards over every crown
        var rng = new System.Random(name.GetHashCode());
        System.Func<float> R = () => (float)rng.NextDouble();
        foreach (var kv in comp)
        {
            var tris = kv.Value; var cc = center[kv.Key]; float rr = radius[kv.Key];
            var cum = new float[tris.Count]; float A = 0;
            for (int q = 0; q < tris.Count; q++) { int t = tris[q]; A += Vector3.Cross(P[I[t * 3 + 1]] - P[I[t * 3]], P[I[t * 3 + 2]] - P[I[t * 3]]).magnitude * 0.5f; cum[q] = A; }
            var c0 = C[I[tris[0] * 3]]; float hh, ss, vv; Color.RGBToHSV(c0, out hh, out ss, out vv); hh *= 360;
            int cell = pine ? 1 : willow ? 2 : (elysian && (hh > 280 || hh < 70) && ss > 0.12f) ? 3 : 0;
            float cs = pine ? Mathf.Clamp(rr * 0.5f, 0.28f, 0.7f) : willow ? Mathf.Clamp(rr * 0.45f, 0.3f, 0.7f) : Mathf.Clamp(rr * 0.6f, 0.22f, 0.95f);
            if (bush) cs = Mathf.Clamp(rr * 0.75f, 0.12f, 0.5f);
            int count = Mathf.Clamp(Mathf.RoundToInt(A * 1.6f / (cs * cs)), 3, 420);
            for (int k = 0; k < count; k++)
            {
                float pick = R() * A; int lo = 0, hi = tris.Count - 1; while (lo < hi) { int mid = (lo + hi) / 2; if (cum[mid] < pick) lo = mid + 1; else hi = mid; }
                int t = tris[lo]; float u1 = R(), u2 = R(); if (u1 + u2 > 1) { u1 = 1 - u1; u2 = 1 - u2; }
                var a = P[I[t * 3]]; var b = P[I[t * 3 + 1]]; var c = P[I[t * 3 + 2]]; var q = a + (b - a) * u1 + (c - a) * u2;
                var outw = (q - cc).normalized; var pos = cc + (q - cc) * 0.93f + outw * cs * 0.18f;
                var rnd = new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f);
                var fnrm = (outw + rnd * 0.9f + Vector3.up * (pine ? 0.6f : 0.35f)).normalized;
                if (willow) fnrm = new Vector3(outw.x, 0, outw.z).normalized + rnd * 0.3f;
                var t1 = Vector3.Cross(fnrm, Mathf.Abs(fnrm.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var t2 = Vector3.Cross(fnrm, t1);
                float rot = R() * Mathf.PI * 2; if (willow) rot = 0; var r1 = t1 * Mathf.Cos(rot) + t2 * Mathf.Sin(rot); var r2 = Vector3.Cross(fnrm, r1);
                if (willow) { r2 = Vector3.down; r1 = Vector3.Cross(r2, fnrm).normalized; }
                float sz = cs * (0.8f + R() * 0.45f); var hx = r1 * sz * 0.5f; var hy = r2 * sz * (willow ? 0.8f : 0.5f);
                var cv = C[I[t * 3]]; float vr = 0.85f + R() * 0.3f; var ccol = new Color32((byte)Mathf.Clamp(cv.r * vr * (0.97f + R() * 0.06f), 0, 255), (byte)Mathf.Clamp(cv.g * vr, 0, 255), (byte)Mathf.Clamp(cv.b * vr * (0.95f + R() * 0.08f), 0, 255), 255);
                float ao = AOh(pos.y) * Mathf.Lerp(0.8f, 1.05f, Mathf.Clamp01(outw.y * 0.5f + 0.5f));
                var o = CELL[cell]; int i0 = v.Count;
                var corners = new[] { pos - hx - hy, pos + hx - hy, pos + hx + hy, pos - hx + hy }; var uvs = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                for (int m = 0; m < 4; m++)
                {
                    v.Add(corners[m]); n.Add((corners[m] - cc).normalized * 0.6f + fnrm * 0.4f); col.Add(ccol);
                    uv.Add(new Vector4(o.x + uvs[m].x * 0.5f, o.y + uvs[m].y * 0.5f, 1, ao)); e2.Add(Vector2.zero);
                }
                ix.Add(i0); ix.Add(i0 + 1); ix.Add(i0 + 2); ix.Add(i0); ix.Add(i0 + 2); ix.Add(i0 + 3);
            }
        }
        for (int i = 0; i < n.Count; i++) n[i] = n[i].normalized;
        var mesh = new Mesh { name = src.name, indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetColors(col); mesh.SetUVs(0, uv); mesh.SetUVs(1, e2); mesh.SetTriangles(ix, 0); mesh.RecalculateBounds();
        return mesh;
    }

    static void Into(Mesh m, Mesh ex)
    {
        string nm = ex.name; ex.Clear(); ex.indexFormat = m.indexFormat;
        ex.vertices = m.vertices; ex.normals = m.normals; ex.colors32 = m.colors32;
        var u0 = new List<Vector4>(); m.GetUVs(0, u0); ex.SetUVs(0, u0); ex.uv2 = m.uv2; ex.triangles = m.triangles; ex.RecalculateBounds(); ex.name = nm;
        EditorUtility.SetDirty(ex); ex.UploadMeshData(false); Object.DestroyImmediate(m);
    }
    static Material Foliage(string path, Texture leaves, float wind)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path); var sh = Shader.Find("Aldara/Foliage");
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh; m.SetTexture("_LeafTex", leaves); if (wind >= 0) m.SetFloat("_Wind", wind); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
    }

    [MenuItem("Aldara/Remaster Trees")]
    public static string Run()
    {
        // the leaf atlas
        string LP = ROOT + "/World/Leaves.png"; AssetDatabase.ImportAsset(LP);
        var ti = AssetImporter.GetAtPath(LP) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Default; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.5f; ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 1024; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.SaveAndReimport(); }
        var leaves = AssetDatabase.LoadAssetAtPath<Texture2D>(LP);
        // always include the see-through variant (found by name at run time)
        var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset"); var so = new SerializedObject(gs); var arr = so.FindProperty("m_AlwaysIncludedShaders");
        var fade = Shader.Find("Aldara/FoliageFade"); bool has = false; for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == fade) has = true;
        if (!has && fade) { arr.InsertArrayElementAtIndex(arr.arraySize); arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = fade; so.ApplyModifiedProperties(); }

        int nt = 0, no = 0;
        // the wild trees (instanced) and their materials
        foreach (var f in Directory.GetFiles(ROOT + "/Models/Trees", "*.amesh.bytes"))
        {
            var p = f.Replace('\\', '/'); string file = Path.GetFileName(p).Replace(".amesh.bytes", "");
            var ex = AssetDatabase.LoadAssetAtPath<Mesh>(ROOT + "/Models/Trees/" + file + "_mesh.asset"); var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(p); if (!ex || !ta) continue;
            Into(Foliate(AldaraMeshIO.Read(ta.bytes, file), file), ex); nt++;
        }
        Foliage(ROOT + "/Materials/Trees.mat", leaves, -1);
        foreach (var mf in Directory.GetFiles(ROOT + "/Materials", "Tree_*.mat")) Foliage(mf.Replace('\\', '/'), leaves, -1);
        var sway = Foliage(ROOT + "/Materials/Props_Sway.mat", leaves, -1);
        var town = Foliage(ROOT + "/Materials/TreesTown.mat", leaves, 0.3f);
        // town trees, bushes (object models)
        var names = AssetDatabase.LoadAssetAtPath<TextAsset>(ROOT + "/World/object_models.txt").text.Trim().Split('\n');
        var treeSet = new HashSet<string>();
        foreach (var raw in names)
        {
            string nm = raw.Trim(); bool tree = Starts(nm, "tree_", "etree"); if (!tree && !IsBush(nm)) continue;
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(ROOT + "/Models/Objects/" + nm + ".amesh.bytes"); var ex = AssetDatabase.LoadAssetAtPath<Mesh>(ROOT + "/Models/Objects/Meshes/" + nm + ".asset");
            if (!ta || !ex) continue; Into(Foliate(AldaraMeshIO.Read(ta.bytes, nm), nm), ex); no++; if (tree) treeSet.Add(nm);
        }
        // scene: placed trees drawn with the foliage material; bush props drawn swaying (with the foliage shader)
        int sr = 0, sb = 0; var objs = GameObject.Find("Objects");
        if (objs)
        {
            foreach (Transform g in objs.transform) foreach (Transform a in g) if (treeSet.Contains(a.name)) foreach (var r in a.GetComponentsInChildren<MeshRenderer>(true)) { r.sharedMaterial = town; sr++; }
            var pf = objs.GetComponentInChildren<AldaraPropField>(true);
            if (pf) { for (int i = 0; i < pf.batches.Count; i++) { var b = pf.batches[i]; var m = b.mesh < pf.meshes.Length ? pf.meshes[b.mesh] : null; if (m && (IsBush(m.name) || Starts(m.name, "tree_", "etree")) && !b.sway) { b.sway = true; sb++; } } EditorUtility.SetDirty(pf); }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(objs.scene);
        }
        AssetDatabase.SaveAssets();
        return "tree meshes " + nt + ", object trees and bushes " + no + ", town tree renderers " + sr + ", prop batches made swaying " + sb;
    }
}
