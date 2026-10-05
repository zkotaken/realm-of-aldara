using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Aldara;

// High-definition structures (Graphics, HD structures). Every building, wall, gate, camp, lair, ruin, cave, shrine,
// waystone and dungeon set piece keeps its browser shape and colours, and gets:
// - a surface for each face (stone masonry, fieldstone, natural rock, planks, timber, clay roof tiles, slate, thatch,
//   plaster, iron, hide, cloth, cobbles, marble or sandstone), worked out from its colour, its slope and where on the
//   model it sits, so the shader can lay a photographed material (Poly Haven, CC0) under the model's own colour;
// - its real edges: every crease between faces is found and baked as a distance to that edge, signed for outside
//   corners (rounded off, worn and catching the light) and inside corners (shadowed, dirt gathering).
// Layout written into the mesh: each triangle gets its own three vertices; uv2.y = edge rounding width, uv3 = the
// distances to the triangle's three edges (9 when that side is not a crease, negative for an inside corner) and,
// in w, the surface + 1 (0 = not processed).
public static class AldaraStructureRemaster
{
    public const int STONE = 1, FIELD = 2, ROCK = 3, PLANK = 4, BEAM = 5, CLAY = 6, SLATE = 7, THATCH = 8, PLASTER = 9, METAL = 10, LEATHER = 11, CLOTH = 12, COBBLE = 13, MARBLE = 14, SAND = 15;
    public static readonly string[] LAYERS = { "castle_wall_slates", "rustic_stone_wall", "rock_face_03", "weathered_planks", "rough_wood", "clay_roof_tiles_02", "roof_slates_02", "thatch_roof_angled", "clay_plaster", "metal_plate", "fabric_leather_02", "quatrefoil_jacquard_fabric", "cobblestone_floor_08", "marble_01", "white_sandstone_bricks" };

    static bool Starts(string n, params string[] p) { foreach (var s in p) if (n.StartsWith(s)) return true; return false; }
    static bool Has(string n, params string[] p) { foreach (var s in p) if (n.Contains(s)) return true; return false; }
    /// vegetation and the like stay as they are
    public static bool Skip(string n) { return Starts(n, "prop_", "tree_", "etree", "ecol", "coral", "shell", "cryst", "kd_flower", "kd_hedge", "kd_hay", "dun_ice", "dun_mush", "dun_bones", "dun_cryst"); }

    static readonly string[] WHITE = { "palace", "statue", "fountain", "cathedral", "heroes", "moonwell", "observatory", "academy", "kstatue", "hstatue", "elfspire", "chapel", "treasury", "shrine" };

    /// the surface the colour guess gives each triangle of a model (0 = none), and each triangle's outward normal
    public static int[] Surfaces(Mesh src, string name, out Vector3[] nn)
    {
        var P0 = src.vertices; var N0 = src.normals; var C0 = src.colors32; var E0 = src.uv2; var I0 = src.triangles;
        int T = I0.Length / 3; nn = new Vector3[T]; var ar = new float[T]; var cen = new Vector3[T]; var col = new Color32[T]; var em = new float[T];
        float y0 = float.MaxValue, y1 = float.MinValue; foreach (var p in P0) { y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); }
        for (int t = 0; t < T; t++)
        {
            int a = I0[t * 3], b = I0[t * 3 + 1], c = I0[t * 3 + 2];
            var n = Vector3.Cross(P0[b] - P0[a], P0[c] - P0[a]); ar[t] = n.magnitude * 0.5f; n = n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up;
            if (N0 != null && N0.Length == P0.Length && Vector3.Dot(n, N0[a] + N0[b] + N0[c]) < 0) n = -n;
            nn[t] = n; cen[t] = (P0[a] + P0[b] + P0[c]) / 3; col[t] = C0 != null && C0.Length == P0.Length ? C0[a] : new Color32(200, 200, 200, 255);
            em[t] = E0 != null && E0.Length == P0.Length ? E0[a].x : 0;
        }
        return Classify(name, T, nn, ar, cen, col, em, y0, y1);
    }
    public static Mesh Process(Mesh src, string name)
    {
        var P0 = src.vertices; var N0 = src.normals; var C0 = src.colors32; var E0 = src.uv2; var I0 = src.triangles;
        int T = I0.Length / 3; if (T == 0) return src;
        var nn = new Vector3[T]; var ar = new float[T]; var cen = new Vector3[T]; var col = new Color32[T]; var em = new float[T];
        float y0 = float.MaxValue, y1 = float.MinValue; foreach (var p in P0) { y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); }
        for (int t = 0; t < T; t++)
        {
            int a = I0[t * 3], b = I0[t * 3 + 1], c = I0[t * 3 + 2];
            var n = Vector3.Cross(P0[b] - P0[a], P0[c] - P0[a]); ar[t] = n.magnitude * 0.5f; n = n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up;
            if (N0 != null && N0.Length == P0.Length && Vector3.Dot(n, N0[a] + N0[b] + N0[c]) < 0) n = -n;
            nn[t] = n; cen[t] = (P0[a] + P0[b] + P0[c]) / 3; col[t] = C0 != null && C0.Length == P0.Length ? C0[a] : new Color32(200, 200, 200, 255);
            em[t] = E0 != null && E0.Length == P0.Length ? E0[a].x : 0;
        }
        var mat = Classify(name, T, nn, ar, cen, col, em, y0, y1);
        // hand-built geometry (AldaraSculpt) names its own surfaces and its smooth, sculpted faces
        var forced = AldaraSculpt.Forced != null && AldaraSculpt.Forced.Length == T ? AldaraSculpt.Forced : null;
        var soft = AldaraSculpt.Soft != null && AldaraSculpt.Soft.Length == T ? AldaraSculpt.Soft : new bool[T];
        AldaraSculpt.Forced = null; AldaraSculpt.Soft = null;
        if (forced != null) for (int t = 0; t < T; t++) { if (forced[t] > 0) mat[t] = forced[t]; else if (forced[t] < 0) mat[t] = 0; }

        // creases: weld positions, find each edge's neighbour
        var key = new int[P0.Length]; var weld = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < P0.Length; i++) { var k = new Vector3Int(Mathf.RoundToInt(P0[i].x * 1000), Mathf.RoundToInt(P0[i].y * 1000), Mathf.RoundToInt(P0[i].z * 1000)); int id; if (!weld.TryGetValue(k, out id)) { id = weld.Count; weld[k] = id; } key[i] = id; }
        var edges = new Dictionary<long, List<int>>();   // edge -> list of tri*3+side (side j = the edge opposite corner j)
        for (int t = 0; t < T; t++) for (int j = 0; j < 3; j++)
            {
                int u = key[I0[t * 3 + (j + 1) % 3]], v = key[I0[t * 3 + (j + 2) % 3]]; if (u == v) continue;
                long ek = u < v ? ((long)u << 32) | (uint)v : ((long)v << 32) | (uint)u;
                List<int> l; if (!edges.TryGetValue(ek, out l)) edges[ek] = l = new List<int>(2); l.Add(t * 3 + j);
            }
        var sideV = new float[T * 3];  // per tri side: 0 = no crease, +1 outside corner, -1 inside corner
        foreach (var kv in edges)
        {
            var l = kv.Value;
            foreach (var ts in l)
            {
                int t = ts / 3; float s = soft[t] ? 0 : 1;   // an open edge is an outside corner (not on smooth, sculpted or sewn-on faces)
                int best = -1; float bd = 2;
                foreach (var os in l) { int o = os / 3; if (o == t) continue; float d = Vector3.Dot(nn[t], nn[o]); if (d < bd) { bd = d; best = o; } if (d > (soft[t] && soft[o] ? 0.5f : 0.94f)) { best = -2; break; } }
                if (best == -2) s = 0;
                else if (best >= 0)
                {
                    int j = ts % 3; var mid = (P0[I0[t * 3 + (j + 1) % 3]] + P0[I0[t * 3 + (j + 2) % 3]]) * 0.5f;
                    s = Vector3.Dot(nn[t], cen[best] - mid) > 0 ? -1 : 1;
                }
                sideV[ts] = s;
            }
        }
        // the new mesh: three vertices per triangle
        int NV = T * 3;
        var P = new Vector3[NV]; var N = new Vector3[NV]; var C = new Color32[NV]; var E = new Vector2[NV]; var U3 = new Vector4[NV]; var I = new int[NV];
        for (int t = 0; t < T; t++)
        {
            float hmin = 9; var h = new float[3];
            for (int j = 0; j < 3; j++)
            {
                var a = P0[I0[t * 3 + j]]; var b = P0[I0[t * 3 + (j + 1) % 3]]; var c = P0[I0[t * 3 + (j + 2) % 3]];
                float len = (c - b).magnitude; h[j] = len > 1e-6f ? 2 * ar[t] / len : 0; hmin = Mathf.Min(hmin, h[j]);
            }
            float bw = Mathf.Clamp(Mathf.Min(0.07f, hmin * 0.3f), 0.004f, 0.07f);
            for (int j = 0; j < 3; j++)
            {
                int vi = t * 3 + j, si = I0[t * 3 + j];
                P[vi] = P0[si]; N[vi] = N0 != null && N0.Length == P0.Length ? N0[si] : nn[t]; C[vi] = C0 != null && C0.Length == P0.Length ? C0[si] : col[t];
                E[vi] = new Vector2(E0 != null && E0.Length == P0.Length ? E0[si].x : 0, bw);
                var d = new float[3];
                for (int k = 0; k < 3; k++) { float s = sideV[t * 3 + k]; d[k] = s == 0 ? 9 : (k == j ? s * Mathf.Max(h[k], 1e-4f) : 0); }
                U3[vi] = new Vector4(d[0], d[1], d[2], mat[t] + 1); I[vi] = vi;
            }
        }
        var m = new Mesh { name = src.name, indexFormat = NV > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        m.vertices = P; m.normals = N; m.colors32 = C; m.uv2 = E; m.SetUVs(3, new List<Vector4>(U3)); m.triangles = I; m.RecalculateBounds();
        return m;
    }

    static int[] Classify(string name, int T, Vector3[] nn, float[] ar, Vector3[] cen, Color32[] col, float[] em, float y0, float y1)
    {
        var mat = new int[T]; if (Skip(name)) return mat;
        float span = Mathf.Max(1e-3f, y1 - y0);
        var hf = new float[T]; for (int t = 0; t < T; t++) hf[t] = (cen[t].y - y0) / span;
        // colour groups: a colour that is mostly sloped and high on the model is a roof
        var groups = new Dictionary<int, List<int>>();
        for (int t = 0; t < T; t++) { int k = (col[t].r << 16) | (col[t].g << 8) | col[t].b; List<int> l; if (!groups.TryGetValue(k, out l)) groups[k] = l = new List<int>(); l.Add(t); }
        var roofish = new bool[T]; var garea = new float[T];
        foreach (var kv in groups)
        {
            float A = 0, As = 0, Ah = 0; foreach (var t in kv.Value) { A += ar[t]; float ay = Mathf.Abs(nn[t].y); if (ay > 0.22f && ay < 0.985f) As += ar[t]; Ah += ar[t] * hf[t]; }
            bool roof = A > 0.5f && As / A > 0.5f && Ah / A > 0.35f;
            foreach (var t in kv.Value) { garea[t] = A; roofish[t] = roof && Mathf.Abs(nn[t].y) > 0.15f; }
        }
        bool white = Has(name, WHITE);
        bool rocky = Starts(name, "cave", "boulder", "stone", "lairPillar", "altar", "glowstone", "lore_stones", "dun_rock", "dun_stal");
        bool ruin = Starts(name, "ruin", "grave", "lore_graves", "lore_battle", "lore_well");
        bool woody = Starts(name, "dp_barrel", "dp_crate", "lore_ship", "lore_wreck", "tw_cart", "tw_bench", "totem", "stake", "spears", "kd_fence", "kd_rail", "tw_notice", "kd_stand", "kd_stall", "tw_stall", "lore_camp", "lore_chest", "campfire");
        bool fort = Starts(name, "kdw", "tw_wall", "tw_gate", "kdg", "kd_sentry", "tw_tower", "kd_barracks", "tw_barracks");
        bool tent = name.StartsWith("tent"), banner = Starts(name, "banner", "qbanner", "kd_banner");
        for (int t = 0; t < T; t++)
        {
            if (em[t] > 0) continue;
            float H, S, V; Color.RGBToHSV(col[t], out H, out S, out V); H *= 360;
            bool grey = S < 0.13f, brown = (H < 48 || H > 345) && S >= 0.25f && V < 0.85f;
            bool tan = H >= 20 && H <= 55 && S >= 0.14f && S < 0.32f && V >= 0.55f;
            bool green = H > 70 && H < 170 && S > 0.25f, sat = S > 0.45f && V > 0.45f;
            float ny = nn[t].y; int r = 0;
            if (green) continue;
            if (tent) r = V < 0.13f ? 0 : (brown && V < 0.25f ? BEAM : LEATHER);
            else if (banner) r = brown && V < 0.45f && !sat ? BEAM : (grey && V < 0.35f ? METAL : CLOTH);
            else if (rocky) r = (grey || brown || tan || V < 0.5f) && !sat ? (grey && V > 0.8f ? MARBLE : ROCK) : 0;
            else if (ruin) r = grey || tan || (brown && S < 0.3f) ? FIELD : 0;
            else if (woody)
            {
                if (roofish[t] && name.Contains("stall")) r = CLOTH;
                else if (grey) r = Starts(name, "campfire", "lore_camp") ? ROCK : (V < 0.4f ? METAL : STONE);
                else if (brown || tan) r = V < 0.3f || Starts(name, "totem", "stake", "spears") ? BEAM : PLANK;
                else if (sat) r = CLOTH;
            }
            else if (roofish[t])
            {
                if ((H < 28 || H > 340) && S > 0.35f) r = CLAY;
                else if (H >= 28 && H <= 58 && S > 0.3f && V > 0.45f) r = white ? METAL : THATCH;
                else r = SLATE;
            }
            else if (fort && (grey || tan || (S < 0.3f && V > 0.3f))) r = ny > 0.97f && hf[t] < 0.06f ? COBBLE : STONE;
            else if (grey)
            {
                if (V > 0.84f) r = white ? MARBLE : PLASTER;
                else if (V > 0.28f) r = ny > 0.97f && hf[t] < 0.06f ? COBBLE : STONE;
                else r = garea[t] > 8 ? STONE : METAL;
            }
            else if (tan) r = V > 0.8f ? PLASTER : SAND;
            else if (brown) r = H >= 35 && H <= 60 && S > 0.5f && V > 0.65f ? METAL : (V < 0.3f ? BEAM : PLANK);
            else if (H >= 35 && H <= 60 && S > 0.45f && V > 0.65f) r = METAL;
            else if (sat) r = banner || fort || Has(name, "banner", "palace", "cathedral") ? CLOTH : PLANK;
            else if (S < 0.3f) r = V > 0.75f ? PLASTER : STONE;
            else r = V < 0.6f ? STONE : PLASTER;
            mat[t] = r;
        }
        return mat;
    }


    /// remaster the meshes the world and dungeons already use, in place (no scene changes): every structure in
    /// Models/Objects/Meshes and every dungeon set piece, from the exported .amesh sources, and their materials
    /// put a rebuilt mesh into the existing asset (so scenes keep their references) and onto the GPU
    static void Into(Mesh m, Mesh ex)
    {
        string nm = ex.name; ex.Clear(); ex.indexFormat = m.indexFormat;
        ex.vertices = m.vertices; ex.normals = m.normals; ex.colors32 = m.colors32; ex.uv2 = m.uv2;
        var u3 = new List<Vector4>(); m.GetUVs(3, u3); ex.SetUVs(3, u3); ex.triangles = m.triangles; ex.RecalculateBounds(); ex.name = nm;
        EditorUtility.SetDirty(ex); ex.UploadMeshData(false); Object.DestroyImmediate(m);
    }
    /// only the structures whose names start with one of the given prefixes (comma separated), for trying changes
    public static string RemasterSome(string prefixes)
    {
        const string ROOT = "Assets/_Aldara"; int n = 0; var pre = prefixes.Split(',');
        var names = AssetDatabase.LoadAssetAtPath<TextAsset>(ROOT + "/World/object_models.txt").text.Trim().Split('\n');
        foreach (var raw in names)
        {
            string nm = raw.Trim(); bool hit = false; foreach (var p in pre) if (p.Length > 0 && nm.StartsWith(p)) hit = true; if (!hit || Skip(nm)) continue;
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(ROOT + "/Models/Objects/" + nm + ".amesh.bytes"); var ex = AssetDatabase.LoadAssetAtPath<Mesh>(ROOT + "/Models/Objects/Meshes/" + nm + ".asset");
            if (!ta || !ex) continue;
            Into(Process(AldaraSculpt.Apply(AldaraMeshIO.Read(ta.bytes, nm), nm), nm), ex); n++;
        }
        AssetDatabase.SaveAssets(); return "remastered " + n;
    }
    [MenuItem("Aldara/Remaster Structures")]
    public static string RemasterAll()
    {
        const string ROOT = "Assets/_Aldara"; int n = 0, d = 0; var sh = Shader.Find("Aldara/Structure"); if (!sh) return "no Aldara/Structure shader";
        var names = AssetDatabase.LoadAssetAtPath<TextAsset>(ROOT + "/World/object_models.txt").text.Trim().Split('\n');
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                string nm = names[i].Trim(); if (Skip(nm)) continue;
                EditorUtility.DisplayProgressBar("Aldara", "Structure " + nm, i / (float)names.Length);
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(ROOT + "/Models/Objects/" + nm + ".amesh.bytes"); var ex = AssetDatabase.LoadAssetAtPath<Mesh>(ROOT + "/Models/Objects/Meshes/" + nm + ".asset");
                if (!ta || !ex) continue;
                Into(Process(AldaraSculpt.Apply(AldaraMeshIO.Read(ta.bytes, nm), nm), nm), ex); n++;
            }
            foreach (var dir in Directory.GetDirectories(ROOT + "/Resources/Dungeons"))
            {
                string id = Path.GetFileName(dir); var meta = AssetDatabase.LoadAssetAtPath<TextAsset>(dir + "/meta.json"); if (!meta) continue;
                var M = Newtonsoft.Json.Linq.JObject.Parse(meta.text); var type = new Dictionary<string, string>();
                foreach (var p in M["props"]) { var mk = (string)p["m"]; if (mk != null && !type.ContainsKey(mk)) type[mk] = (string)p["type"]; }
                string D = ROOT + "/Dungeons/" + id;
                foreach (var kv in type)
                {
                    var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(D + "/props/" + kv.Key + ".amesh.bytes"); var ex = AssetDatabase.LoadAssetAtPath<Mesh>(D + "/Meshes/" + kv.Key + ".asset");
                    if (!ta || !ex) continue;
                    Into(Process(AldaraSculpt.Apply(AldaraMeshIO.Read(ta.bytes, id + "_" + kv.Key), "dun_" + kv.Value), "dun_" + kv.Value), ex); d++;
                }
                var vm = AssetDatabase.LoadAssetAtPath<Material>(ROOT + "/Materials/Dungeon_" + id + ".mat"); if (vm) { vm.shader = sh; EditorUtility.SetDirty(vm); }
                // the floor: relief and a surface of the dungeon's kind
                var fl = Shader.Find("Aldara/DunFloor"); int layer; float tile, amt; FloorOf(id, out layer, out tile, out amt);
                if (fl && Directory.Exists(D + "/Materials"))
                    foreach (var f in Directory.GetFiles(D + "/Materials", "t*.mat"))
                    {
                        var fm = AssetDatabase.LoadAssetAtPath<Material>(f.Replace('\\', '/')); if (!fm) continue;
                        fm.shader = fl; fm.SetFloat("_Layer", layer); fm.SetFloat("_Tile", tile); fm.SetFloat("_Amt", amt); fm.renderQueue = 2000; EditorUtility.SetDirty(fm);
                    }
            }
            foreach (var mn in new[] { "Objects", "Props" }) { var mt = AssetDatabase.LoadAssetAtPath<Material>(ROOT + "/Materials/" + mn + ".mat"); if (mt) { mt.shader = sh; mt.enableInstancing = true; EditorUtility.SetDirty(mt); } }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.SaveAssets();
        return "structures " + n + ", dungeon pieces " + d;
    }

    // ---- the material layers: Assets/_Aldara/World/Struct/NN_alb.png (colour normalised to a mean of 0.5, height in
    // alpha) and NN_nra.png (normal x, y, roughness, occlusion), packed into two texture arrays ----
    const string SRC = "Assets/_Aldara/World/Struct/", DST = "Assets/_Aldara/Resources/World/";
    [MenuItem("Aldara/Build Structure Materials")]
    public static string BuildArrays()
    {
        AssetDatabase.Refresh(); string r = "";
        foreach (var kind in new[] { "alb", "nra" })
        {
            var tex = new List<Texture2D>();
            for (int i = 0; i < LAYERS.Length; i++)
            {
                var p = SRC + i.ToString("00") + "_" + kind + ".png"; var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti == null) return "missing " + p;
                bool dirty = false;
                if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
                if (ti.sRGBTexture != (kind == "alb")) { ti.sRGBTexture = kind == "alb"; dirty = true; }
                if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; dirty = true; }
                if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; dirty = true; }
                if (ti.alphaSource != TextureImporterAlphaSource.FromInput) { ti.alphaSource = TextureImporterAlphaSource.FromInput; dirty = true; }
                if (ti.alphaIsTransparency) { ti.alphaIsTransparency = false; dirty = true; }
                if (!ti.isReadable) { ti.isReadable = true; dirty = true; }
                if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                var ps = ti.GetPlatformTextureSettings("Standalone"); if (!ps.overridden || ps.format != TextureImporterFormat.BC7 || ps.maxTextureSize != 1024) { ps.overridden = true; ps.format = TextureImporterFormat.BC7; ps.maxTextureSize = 1024; ti.SetPlatformTextureSettings(ps); dirty = true; }
                if (dirty) ti.SaveAndReimport();
                tex.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(p));
            }
            var f = tex[0]; var arr = new Texture2DArray(f.width, f.height, tex.Count, f.format, true, kind == "nra") { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            for (int i = 0; i < tex.Count; i++)
            {
                if (tex[i].width != f.width || tex[i].format != f.format) return "size or format differs: " + i + " " + tex[i].format;
                for (int mi = 0; mi < tex[i].mipmapCount && mi < arr.mipmapCount; mi++) arr.SetPixelData(tex[i].GetPixelData<byte>(mi), mi, i);
            }
            arr.Apply(false, false);
            var path = DST + (kind == "alb" ? "StructAlb.asset" : "StructNra.asset");
            if (AssetDatabase.LoadAssetAtPath<Texture2DArray>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(arr, path); r += path + " " + arr.width + " " + arr.format + " x" + arr.depth + "; ";
        }
        AssetDatabase.SaveAssets(); return r;
    }

    // each dungeon's floor surface: layer (STONE-1 etc.), size in units, strength
    static void FloorOf(string id, out int layer, out float tile, out float amt)
    {
        layer = STONE - 1; tile = 2.6f; amt = 0.7f;
        switch (id)
        {
            case "nest": case "warrens": case "void": case "eclipse": layer = ROCK - 1; tile = 4f; break;
            case "frozen": layer = ROCK - 1; tile = 4.5f; amt = 0.45f; break;
            case "forge": case "sewer": layer = COBBLE - 1; tile = 2.4f; break;
            case "sunken": case "blood": layer = FIELD - 1; tile = 2.4f; break;
            case "throne": case "astral": layer = MARBLE - 1; tile = 3f; amt = 0.5f; break;
        }
    }
    /// how many triangles of each surface a model got (for checking)
    public static string Report(Mesh m)
    {
        var u = new List<Vector4>(); m.GetUVs(3, u); var cnt = new int[17]; foreach (var v in u) cnt[Mathf.Clamp((int)v.w, 0, 16)]++;
        var s = ""; for (int i = 0; i < 17; i++) if (cnt[i] > 0) s += i + ":" + cnt[i] / 3 + " "; return s;
    }
}
