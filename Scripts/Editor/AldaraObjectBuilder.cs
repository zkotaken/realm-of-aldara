using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Aldara;

// Places every object the browser game puts in the world (Lorenmar's buildings and walls, Valcrest, camps, lairs,
// ruins, lore scenes, waystones, outposts, lamps, props) from the exported placement list and models.
public static class AldaraObjectBuilder
{
    const string ROOT = "Assets/_Aldara";
    [MenuItem("Aldara/Build Objects")]
    public static void Build()
    {
        AldaraWorld.Load();
        var names = AssetDatabase.LoadAssetAtPath<TextAsset>(ROOT + "/World/object_models.txt").text.Trim().Split('\n');
        var bytes = AssetDatabase.LoadAssetAtPath<TextAsset>(ROOT + "/World/objects.bytes").bytes;
        Directory.CreateDirectory(ROOT + "/Models/Objects/Meshes");
        var meshes = new Mesh[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i].Trim(), mp = ROOT + "/Models/Objects/Meshes/" + n + ".asset";
            var ex = AssetDatabase.LoadAssetAtPath<Mesh>(mp);
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(ROOT + "/Models/Objects/" + n + ".amesh.bytes");
            if (!ta) continue;
            var m = AldaraMeshIO.Read(ta.bytes, n);
            if (!AldaraStructureRemaster.Skip(n)) m = AldaraStructureRemaster.Process(AldaraSculpt.Apply(m, n), n);   // HD structures: surfaces and creases
            if (ex) { EditorUtility.CopySerialized(m, ex); m = ex; } else AssetDatabase.CreateAsset(m, mp);
            meshes[i] = m;
        }
        var still = AldaraMeshIO.VertexLit("Objects", 0);
        var sway = AldaraMeshIO.VertexLit("Props_Sway", 0.6f);
        var propStill = AldaraMeshIO.VertexLit("Props", 0);
        still.shader = Shader.Find("Aldara/Structure"); propStill.shader = still.shader; EditorUtility.SetDirty(still); EditorUtility.SetDirty(propStill);

        var old = GameObject.Find("Objects"); if (old) Object.DestroyImmediate(old);
        var root = new GameObject("Objects");
        var groups = new Dictionary<string, Transform>();
        var field = new GameObject("Props").AddComponent<AldaraPropField>(); field.transform.SetParent(root.transform);
        field.meshes = meshes; field.still = propStill; field.sway = sway;
        var cells = new Dictionary<long, List<Matrix4x4>>();
        int n0 = System.BitConverter.ToInt32(bytes, 0), placed = 0;
        for (int i = 0; i < n0; i++)
        {
            int o = 4 + i * 15;
            float x = System.BitConverter.ToSingle(bytes, o), y = System.BitConverter.ToSingle(bytes, o + 4);
            int mi = System.BitConverter.ToUInt16(bytes, o + 8); float ry = System.BitConverter.ToSingle(bytes, o + 10); int fl = bytes[o + 14];
            if (mi >= meshes.Length || !meshes[mi]) continue;
            var pos = AldaraWorld.ToUnity(x, y); var rot = Quaternion.Euler(0, -ry * Mathf.Rad2Deg, 0);
            if ((fl & 2) != 0)
            {
                int cx = Mathf.Clamp((int)(pos.x / field.cellSize), 0, field.cellsX - 1), cz = Mathf.Max(0, (int)(pos.z / field.cellSize));
                long key = ((long)(cz * field.cellsX + cx) << 16) | (long)(mi << 1) | (long)(fl & 1);
                List<Matrix4x4> L; if (!cells.TryGetValue(key, out L)) cells[key] = L = new List<Matrix4x4>();
                L.Add(Matrix4x4.TRS(pos, Quaternion.identity, AldaraView.Squash) * Matrix4x4.Rotate(rot));
            }
            else
            {
                string nm = names[mi].Trim(), cat = Category(nm);
                Transform g; if (!groups.TryGetValue(cat, out g)) { g = new GameObject(cat).transform; g.SetParent(root.transform); groups[cat] = g; }
                var anchor = new GameObject(nm); anchor.transform.SetParent(g); anchor.transform.position = pos; anchor.transform.localScale = AldaraView.Squash; anchor.isStatic = true;
                var go = new GameObject("model", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(anchor.transform, false); go.transform.localRotation = rot;
                go.GetComponent<MeshFilter>().sharedMesh = meshes[mi];
                go.GetComponent<MeshRenderer>().sharedMaterial = (fl & 1) != 0 ? sway : still;
                go.isStatic = true;
            }
            placed++;
        }
        foreach (var kv in cells)
            field.batches.Add(new AldaraPropField.Batch { cell = (int)(kv.Key >> 16), mesh = (int)((kv.Key >> 1) & 0x7fff), sway = (kv.Key & 1) != 0, m = kv.Value.ToArray() });
        AssetDatabase.SaveAssets();
        Debug.Log("Aldara objects placed: " + placed + " (" + field.batches.Count + " prop batches)");
    }
    static string Category(string n)
    {
        if (n.StartsWith("tw_") || n.StartsWith("lamp") || n.StartsWith("dp_")) return "Lorenmar";
        if (n.StartsWith("kd")) return "Valcrest";
        if (n.StartsWith("lair")) return "Lairs";
        if (n.StartsWith("lore") || n.StartsWith("ruin")) return "Lore and ruins";
        if (n.StartsWith("waystone")) return "Waystones";
        if (n.StartsWith("tent") || n.StartsWith("campfire") || n.StartsWith("stake") || n.StartsWith("banner") || n.StartsWith("totem") || n.StartsWith("spears")) return "Camps";
        return "Other";
    }
}
