using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Aldara;

// Dungeon prefabs from the browser's dungeons: the baked floor tiles (512 px each, unlit, exactly as baked) and the
// set pieces (the browser's own models, placed and turned as dpProp draws them). Built in the map's own pixel space;
// the game shows the prefab west of the world (AldaraWorld.DOX).
public static class AldaraDungeonBuilder
{
    const string SRC = "Assets/_Aldara/Dungeons";
    [MenuItem("Aldara/Build Dungeons")]
    public static string BuildAll()
    {
        Directory.CreateDirectory("Assets/_Aldara/Resources/DunPrefabs"); int n = 0; string log = "";
        foreach (var dir in Directory.GetDirectories("Assets/_Aldara/Resources/Dungeons"))
        {
            string id = Path.GetFileName(dir); var meta = AssetDatabase.LoadAssetAtPath<TextAsset>(dir + "/meta.json"); if (!meta) continue;
            log += Build(id, JObject.Parse(meta.text)) + " "; n++;
        }
        AssetDatabase.SaveAssets(); Debug.Log("Aldara dungeons built: " + n + " " + log); return log;
    }
    static string Build(string id, JObject M)
    {
        string D = SRC + "/" + id; Directory.CreateDirectory(D + "/Meshes"); Directory.CreateDirectory(D + "/Materials");
        var root = new GameObject("Dun_" + id); int tiles = 0, props = 0;
        // floor tiles
        var floor = new GameObject("Floor"); floor.transform.SetParent(root.transform, false);
        var quad = QuadMesh(D + "/Meshes/tile.asset");
        var sh = Shader.Find("Aldara/DunFx");
        int nx = (int)M["tiles"][0], ny = (int)M["tiles"][1];
        for (int cy = 0; cy < ny; cy++) for (int cx = 0; cx < nx; cx++)
            {
                string tp = D + "/chunks/" + cx + "_" + cy + ".jpg"; var ti = AssetImporter.GetAtPath(tp) as TextureImporter; if (ti == null) continue;
                if (ti.mipmapEnabled || ti.wrapMode != TextureWrapMode.Clamp || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.maxTextureSize < 512) { ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 512; ti.npotScale = TextureImporterNPOTScale.None; ti.SaveAndReimport(); }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                string mp = D + "/Materials/t" + cx + "_" + cy + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (!mat) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, mp); }
                mat.shader = sh; mat.mainTexture = tex; mat.SetFloat("_Src", 1); mat.SetFloat("_Dst", 0); mat.SetFloat("_ZT", 4); mat.renderQueue = 2000; EditorUtility.SetDirty(mat);
                var go = new GameObject("t" + cx + "_" + cy, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(floor.transform, false);
                go.transform.localPosition = new Vector3(cx * 512 / AldaraWorld.PX, 0, (AldaraWorld.YTOP - cy * 512) / AldaraWorld.PX);
                go.GetComponent<MeshFilter>().sharedMesh = quad; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; tiles++;
            }
        // the set pieces
        var pr = new GameObject("Props"); pr.transform.SetParent(root.transform, false);
        var vmat = AldaraMeshIO.VertexLit("Dungeon_" + id, 0); vmat.shader = Shader.Find("Aldara/Structure"); EditorUtility.SetDirty(vmat);
        var meshes = new System.Collections.Generic.Dictionary<string, Mesh>();
        foreach (var p in M["props"])
        {
            string m = (string)p["m"]; if (m == null) continue; var md = M["models"][m]; if (md == null || (int)md["nv"] == 0) continue;
            Mesh mesh; if (!meshes.TryGetValue(m, out mesh))
            {
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(D + "/props/" + m + ".amesh.bytes"); if (!ta) continue;
                mesh = AldaraMeshIO.Read(ta.bytes, id + "_" + m); mesh = AldaraStructureRemaster.Process(mesh, "dun_" + (string)p["type"]); string mp = D + "/Meshes/" + m + ".asset"; var ex = AssetDatabase.LoadAssetAtPath<Mesh>(mp);
                if (ex) { EditorUtility.CopySerialized(mesh, ex); mesh = ex; } else AssetDatabase.CreateAsset(mesh, mp); meshes[m] = mesh;
            }
            float x = (float)p["x"], y = (float)p["y"], ry = (float)md["ry"];
            var anchor = new GameObject((string)p["type"]); anchor.transform.SetParent(pr.transform, false);
            anchor.transform.localPosition = new Vector3(x / AldaraWorld.PX, 0, (AldaraWorld.YTOP - y) / AldaraWorld.PX); anchor.transform.localScale = AldaraView.Squash;
            var go = new GameObject("model", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(anchor.transform, false); go.transform.localRotation = Quaternion.Euler(0, -ry * Mathf.Rad2Deg, 0);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = vmat; props++;
        }
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Aldara/Resources/DunPrefabs/Dun_" + id + ".prefab"); Object.DestroyImmediate(root);
        return id + ":" + tiles + "t/" + props + "p";
    }
    /// a 512 px square on the ground, its top-left corner at the origin (x east, z north)
    static Mesh QuadMesh(string path)
    {
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (ex) return ex;
        float s = 512 / AldaraWorld.PX; var m = new Mesh { name = "tile" };
        m.vertices = new[] { new Vector3(0, 0, -s), new Vector3(s, 0, -s), new Vector3(s, 0, 0), new Vector3(0, 0, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 }; m.RecalculateNormals(); m.RecalculateBounds(); AssetDatabase.CreateAsset(m, path); return m;
    }
}
