using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Aldara;

// Builds the browser game's island from the data exported out of the browser build: 8 x 7 ground tiles of 128 units
// (meshes from the 16 px height grid, lakes and sea carved below the water line, the browser's painted ground as the
// texture), water and lava meshes, and the trees (drawn with instancing, squashed in depth like every browser model).
public static class AldaraWorldBuilder
{
    const string ROOT = "Assets/_Aldara/World";
    static readonly Color32[] LAKE = {
        new Color32(40,92,140,170), new Color32(40,92,140,170), new Color32(36,64,56,200), new Color32(196,224,242,150),
        new Color32(210,74,24,255), new Color32(10,4,22,215), new Color32(64,176,196,160), new Color32(70,96,90,180),
        new Color32(30,52,48,200), new Color32(50,110,150,170), new Color32(98,26,24,210) };
    static readonly Color32 SEA = new Color32(30,78,122,200), LAVA = new Color32(255,92,22,255);

    [MenuItem("Aldara/Build World")]
    public static void Build()
    {
        AldaraWorld.Load();
        int W = AldaraWorld.W, H = AldaraWorld.H;
        var hb = Resources.Load<TextAsset>("World/height").bytes; var height = new float[W * H]; System.Buffer.BlockCopy(hb, 0, height, 0, hb.Length);
        var liquid = Resources.Load<TextAsset>("World/liquid").bytes;
        var zone = Resources.Load<TextAsset>("World/zone").bytes;
        System.Func<int, int, float> Hg = (i, j) => (i < 0 || j < 0 || i >= W || j >= H) ? 0f : height[j * W + i];
        System.Func<int, int, int> Lg = (i, j) => (i < 0 || j < 0 || i >= W || j >= H) ? 3 : liquid[j * W + i];

        Directory.CreateDirectory(ROOT + "/Ground");
        var old = GameObject.Find("World"); if (old) Object.DestroyImmediate(old);
        var world = new GameObject("World");
        var groundRoot = new GameObject("Ground"); groundRoot.transform.SetParent(world.transform);
        var waterRoot = new GameObject("Water"); waterRoot.transform.SetParent(world.transform);
        var waterMat = AssetDatabase.LoadAssetAtPath<Material>(ROOT + "/Water.mat");
        if (!waterMat) { waterMat = new Material(Shader.Find("Aldara/Water")); AssetDatabase.CreateAsset(waterMat, ROOT + "/Water.mat"); }

        int N = 257, G = 256; float step = 0.5f;
        for (int tj = 0; tj < AldaraWorld.TILES_Y; tj++)
            for (int ti = 0; ti < AldaraWorld.TILES_X; ti++)
            {
                EditorUtility.DisplayProgressBar("Aldara", "Ground " + ti + "," + tj, (tj * AldaraWorld.TILES_X + ti) / 56f);
                var v = new Vector3[N * N]; var uv = new Vector2[N * N];
                for (int r = 0; r < N; r++)
                    for (int c = 0; c < N; c++)
                    {
                        int gx = ti * G + c, gy = (int)(AldaraWorld.YTOP / 16) - tj * G - r;
                        float hu = Hg(gx, gy) / AldaraWorld.PX * AldaraWorld.HS;
                        float wet = 0, sea = 0;
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int l = Lg(gx + dx, gy + dy); if (l != 0) wet += 1f / 9; if (l == 3) sea += 1f / 9; }
                        hu -= wet * 1.3f + sea * 1.4f;
                        v[r * N + c] = new Vector3(c * step, hu, r * step); uv[r * N + c] = new Vector2(c / (float)G, r / (float)G);
                    }
                var idx = new int[G * G * 6]; int k = 0;
                for (int r = 0; r < G; r++) for (int c = 0; c < G; c++)
                    { int a = r * N + c; idx[k++] = a; idx[k++] = a + N; idx[k++] = a + 1; idx[k++] = a + 1; idx[k++] = a + N; idx[k++] = a + N + 1; }
                var mesh = new Mesh { name = "Ground_" + ti + "_" + tj, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.vertices = v; mesh.uv = uv; mesh.triangles = idx; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string mp = ROOT + "/Ground/Ground_" + ti + "_" + tj + ".asset";
                var ex = AssetDatabase.LoadAssetAtPath<Mesh>(mp); if (ex) { EditorUtility.CopySerialized(mesh, ex); mesh = ex; } else AssetDatabase.CreateAsset(mesh, mp);
                string matp = ROOT + "/Ground/Ground_" + ti + "_" + tj + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matp);
                if (!mat) { mat = new Material(Shader.Find("Aldara/Ground")); AssetDatabase.CreateAsset(mat, matp); }
                mat.shader = Shader.Find("Aldara/Ground");
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ROOT + "/Tiles/tile_" + ti + "_" + tj + ".jpg"));
                var go = new GameObject("Ground_" + ti + "_" + tj, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(groundRoot.transform); go.transform.position = new Vector3(ti * 128, 0, tj * 128);
                go.GetComponent<MeshFilter>().sharedMesh = mesh; var gr = go.GetComponent<MeshRenderer>(); gr.sharedMaterial = mat;
                gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; go.isStatic = true;

                var wm = WaterMesh(ti, tj, Lg, zone, W, H);
                string wp = ROOT + "/Ground/Water_" + ti + "_" + tj + ".asset";
                if (wm != null)
                {
                    var wex = AssetDatabase.LoadAssetAtPath<Mesh>(wp); if (wex) { EditorUtility.CopySerialized(wm, wex); wm = wex; } else AssetDatabase.CreateAsset(wm, wp);
                    var w = new GameObject("Water_" + ti + "_" + tj, typeof(MeshFilter), typeof(MeshRenderer));
                    w.transform.SetParent(waterRoot.transform); w.transform.position = new Vector3(ti * 128, AldaraWorld.WATER_Y, tj * 128);
                    w.GetComponent<MeshFilter>().sharedMesh = wm; var mr = w.GetComponent<MeshRenderer>(); mr.sharedMaterial = waterMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; w.isStatic = true;
                }
            }
        BuildTrees(world.transform);
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
    }

    static float Hash(float a, float b) { float h = Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f; return h - Mathf.Floor(h); }

    // ---- trees: instanced, in 48-unit cells ----
    struct TreeRec { public float x, y; public int kind; }
    static void BuildTrees(Transform parent)
    {
        var kj = AssetDatabase.LoadAssetAtPath<TextAsset>(ROOT + "/tree_kinds.txt"); if (!kj) return;
        var kinds = kj.text.Trim().Split('\n');
        var meshes = new Mesh[kinds.Length];
        for (int i = 0; i < kinds.Length; i++) { var pf = AldaraTreeFactory.Get(kinds[i].Trim()); if (pf) meshes[i] = pf.GetComponent<MeshFilter>().sharedMesh; }
        var field = new GameObject("Trees").AddComponent<AldaraPropField>(); field.transform.SetParent(parent);
        field.meshes = meshes; field.still = AldaraMeshIO.VertexLit("Trees", 0.35f); field.sway = field.still;
        var cells = new Dictionary<long, List<Matrix4x4>>();
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(ROOT + "/trees.bytes"); var b = ta.bytes; int n = System.BitConverter.ToInt32(b, 0);
        for (int i = 0; i < n; i++)
        {
            int o = 4 + i * 9; float x = System.BitConverter.ToSingle(b, o), y = System.BitConverter.ToSingle(b, o + 4); int kind = b[o + 8];
            if (kind >= meshes.Length || !meshes[kind]) continue;
            var pos = AldaraWorld.ToUnity(x, y);
            float s = 0.85f + Hash(x, y) * 0.4f, hs = s * (0.9f + Hash(y, x) * 0.25f);
            var m = Matrix4x4.TRS(pos, Quaternion.identity, AldaraView.Squash) * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, Hash(x * 3, y) * 360, 0), new Vector3(s, hs, s));
            int cx = Mathf.Clamp((int)(pos.x / field.cellSize), 0, field.cellsX - 1), cz = Mathf.Max(0, (int)(pos.z / field.cellSize));
            long key = ((long)(cz * field.cellsX + cx) << 16) | (long)(kind << 1);
            List<Matrix4x4> L; if (!cells.TryGetValue(key, out L)) cells[key] = L = new List<Matrix4x4>(); L.Add(m);
        }
        foreach (var kv in cells) field.batches.Add(new AldaraPropField.Batch { cell = (int)(kv.Key >> 16), mesh = (int)((kv.Key >> 1) & 0x7fff), sway = false, m = kv.Value.ToArray() });
        Debug.Log("Aldara trees: " + n + " in " + field.batches.Count + " batches");
    }

    static Mesh WaterMesh(int ti, int tj, System.Func<int, int, int> Lg, byte[] zone, int W, int H)
    {
        var v = new List<Vector3>(); var col = new List<Color32>(); var idx = new List<int>();
        const int N = 128; const float S = 1f;
        for (int r = 0; r < N; r++)
            for (int c = 0; c < N; c++)
            {
                int gx = ti * 256 + c * 2, gy = (int)(AldaraWorld.YTOP / 16) - tj * 256 - r * 2;
                int best = 0, cnt = 0;
                for (int dy = -1; dy <= 2; dy++) for (int dx = -1; dx <= 2; dx++) { int l = Lg(gx + dx, gy - dy); if (l != 0) { cnt++; if (l > best || best == 0) best = l; } }
                if (cnt == 0) continue;
                Color32 k;
                if (best == 3) k = SEA; else if (best == 2) k = LAVA;
                else { int zi = (gx >= 0 && gy >= 0 && gx < W && gy < H) ? zone[gy * W + gx] : 0; k = LAKE[Mathf.Clamp(zi, 0, 10)]; }
                float yy = best == 2 ? 0.12f : 0; int b = v.Count;
                v.Add(new Vector3(c * S, yy, r * S)); v.Add(new Vector3((c + 1) * S, yy, r * S)); v.Add(new Vector3((c + 1) * S, yy, (r + 1) * S)); v.Add(new Vector3(c * S, yy, (r + 1) * S));
                for (int q = 0; q < 4; q++) col.Add(k);
                idx.Add(b); idx.Add(b + 2); idx.Add(b + 1); idx.Add(b); idx.Add(b + 3); idx.Add(b + 2);
            }
        if (v.Count == 0) return null;
        var m = new Mesh { name = "Water_" + ti + "_" + tj, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(v); m.SetColors(col); m.SetTriangles(idx, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
}
