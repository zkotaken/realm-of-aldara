using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Aldara;

// Pet prefabs from the browser's pets: the skinned models (a mesh with bone weights on the bone hierarchy, posed by the
// baked clips) and the older node model (Celestial Phoenix, one mesh per joint like the monsters).
public static class AldaraPetBuilder
{
    const string SRC = "Assets/_Aldara/Models/Pets";
    [MenuItem("Aldara/Build Pets")]
    public static void BuildAll()
    {
        var meta = JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Aldara/Resources/pets_meta.json").text);
        Directory.CreateDirectory("Assets/_Aldara/Resources/PetPrefabs"); Directory.CreateDirectory(SRC + "/Meshes");
        var mat = AldaraMeshIO.VertexLit("Monsters", 0); int n = 0;
        foreach (var kv in (JObject)meta["pets"]) if (Build((JObject)kv.Value, mat)) n++;
        AssetDatabase.SaveAssets(); Debug.Log("Aldara pets built: " + n);
    }
    static bool Build(JObject m, Material mat)
    {
        string id = (string)m["id"];
        var data = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Aldara/Resources/Pets/" + id + ".bytes"); if (!data) return false;
        int N; int[] par;
        using (var r = new BinaryReader(new MemoryStream(data.bytes))) { r.ReadChars(4); N = r.ReadInt32(); par = new int[N]; for (int i = 0; i < N; i++) par[i] = r.ReadInt32(); }
        var root = new GameObject("Pet_" + id);
        var rig = new GameObject("Rig"); rig.transform.SetParent(root.transform, false); rig.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var T = new Transform[N];
        if ((int)m["skinned"] == 1)
        {
            float s = (float)m["s"] * (float)m["UK"];
            var sc = new GameObject("Scale"); sc.transform.SetParent(rig.transform, false); sc.transform.localScale = Vector3.one * s;
            for (int i = 0; i < N; i++) { var go = new GameObject("n" + i); T[i] = go.transform; }
            for (int i = 0; i < N; i++) T[i].SetParent(par[i] < 0 ? sc.transform : T[par[i]], false);
            var an0 = root.AddComponent<AldaraMonsterAnimator>(); an0.data = data;
            // rest pose from the "rest" clip
            float[] rest = RestFrame(data.bytes, N);
            for (int i = 0; i < N; i++) { int k = i * 8; T[i].localPosition = new Vector3(rest[k], rest[k + 1], rest[k + 2]); T[i].localRotation = new Quaternion(rest[k + 3], rest[k + 4], rest[k + 5], rest[k + 6]); T[i].localScale = Vector3.one * rest[k + 7]; }
            var skinGo = new GameObject("skin"); skinGo.transform.SetParent(sc.transform, false);
            var sk = AssetDatabase.LoadAssetAtPath<TextAsset>(SRC + "/" + id + ".skin.bytes"); if (!sk) { Object.DestroyImmediate(root); return false; }
            var mesh = ReadSkin(sk.bytes, id);
            var bind = new Matrix4x4[N]; for (int i = 0; i < N; i++) bind[i] = T[i].worldToLocalMatrix * skinGo.transform.localToWorldMatrix; mesh.bindposes = bind;
            string mp = SRC + "/Meshes/" + id + ".asset"; var ex = AssetDatabase.LoadAssetAtPath<Mesh>(mp); if (ex) { EditorUtility.CopySerialized(mesh, ex); mesh = ex; } else AssetDatabase.CreateAsset(mesh, mp);
            var smr = skinGo.AddComponent<SkinnedMeshRenderer>(); smr.sharedMesh = mesh; smr.bones = T; smr.rootBone = sc.transform; smr.sharedMaterial = mat; smr.updateWhenOffscreen = true;
        }
        else
        {
            for (int i = 0; i < N; i++)
            {
                var go = new GameObject("n" + i); T[i] = go.transform; go.transform.SetParent(par[i] < 0 ? rig.transform : T[par[i]], false);
                var mb = AssetDatabase.LoadAssetAtPath<TextAsset>(SRC + "/" + id + "_n" + i + ".amesh.bytes"); if (!mb) continue;
                string mp = SRC + "/Meshes/" + id + "_n" + i + ".asset"; var mesh = AldaraMeshIO.Read(mb.bytes, id + "_n" + i);
                var ex = AssetDatabase.LoadAssetAtPath<Mesh>(mp); if (ex) { EditorUtility.CopySerialized(mesh, ex); mesh = ex; } else AssetDatabase.CreateAsset(mesh, mp);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            root.AddComponent<AldaraMonsterAnimator>().data = data;
        }
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Aldara/Resources/PetPrefabs/Pet_" + id + ".prefab");
        Object.DestroyImmediate(root); return true;
    }
    static float[] RestFrame(byte[] b, int N)
    {
        using (var r = new BinaryReader(new MemoryStream(b)))
        {
            r.ReadChars(4); r.ReadInt32(); for (int i = 0; i < N; i++) r.ReadInt32(); int C = r.ReadInt32(); float[] first = null;
            for (int c = 0; c < C; c++)
            {
                string name = r.ReadString(); r.ReadByte(); r.ReadSingle(); int F = r.ReadInt32(); var f = new float[N * 8]; for (int i = 0; i < f.Length; i++) f[i] = r.ReadSingle();
                r.BaseStream.Position += (long)(F - 1) * N * 8 * 4; if (first == null) first = f; if (name == "rest") return f;
            }
            return first;
        }
    }
    // "APSK", int nv, int nb, int 0, pos, normal, rgba, emission, 4 bone bytes and 4 float weights per vertex
    static Mesh ReadSkin(byte[] b, string name)
    {
        using (var r = new BinaryReader(new MemoryStream(b)))
        {
            r.ReadChars(4); int nv = r.ReadInt32(); r.ReadInt32(); r.ReadInt32();
            var p = new Vector3[nv]; var n = new Vector3[nv]; var c = new Color32[nv]; var e = new Vector2[nv]; var bw = new BoneWeight[nv]; var ix = new int[nv];
            for (int i = 0; i < nv; i++) p[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            for (int i = 0; i < nv; i++) n[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            for (int i = 0; i < nv; i++) c[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
            for (int i = 0; i < nv; i++) e[i] = new Vector2(r.ReadByte() / 255f, 0);
            var J = new int[nv * 4]; for (int i = 0; i < nv * 4; i++) J[i] = r.ReadByte();
            var W = new float[nv * 4]; for (int i = 0; i < nv * 4; i++) W[i] = r.ReadSingle();
            for (int i = 0; i < nv; i++) bw[i] = new BoneWeight { boneIndex0 = J[i * 4], weight0 = W[i * 4], boneIndex1 = J[i * 4 + 1], weight1 = W[i * 4 + 1], boneIndex2 = J[i * 4 + 2], weight2 = W[i * 4 + 2], boneIndex3 = J[i * 4 + 3], weight3 = W[i * 4 + 3] };
            for (int t = 0; t < nv; t += 3) { ix[t] = t; ix[t + 1] = t + 2; ix[t + 2] = t + 1; }
            var m = new Mesh { name = name, indexFormat = nv > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.vertices = p; m.normals = n; m.colors32 = c; m.uv2 = e; m.boneWeights = bw; m.triangles = ix; m.RecalculateBounds();
            return m;
        }
    }
}
