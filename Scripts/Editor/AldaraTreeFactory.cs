using System.IO;
using UnityEditor;
using UnityEngine;

// Meshes exported from the browser game's procedural models (trees, props, buildings, characters).
// .amesh.bytes layout: "AMSH", int nv, int ni, nv*3 float pos, nv*3 float normal, nv*4 byte rgba, nv byte emission, ni int index.
public static class AldaraMeshIO
{
    public static Mesh Read(byte[] b, string name)
    {
        using (var r = new BinaryReader(new MemoryStream(b)))
        {
            if (new string(r.ReadChars(4)) != "AMSH") throw new System.Exception("not an AMSH mesh: " + name);
            int nv = r.ReadInt32(), ni = r.ReadInt32();
            var p = new Vector3[nv]; var n = new Vector3[nv]; var c = new Color32[nv]; var e = new Vector2[nv]; var ix = new int[ni];
            for (int i = 0; i < nv; i++) p[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            for (int i = 0; i < nv; i++) n[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            for (int i = 0; i < nv; i++) c[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
            for (int i = 0; i < nv; i++) e[i] = new Vector2(r.ReadByte() / 255f, 0);
            for (int i = 0; i < ni; i++) ix[i] = r.ReadInt32();
            var m = new Mesh { name = name, indexFormat = nv > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.vertices = p; m.normals = n; m.colors32 = c; m.uv2 = e; m.triangles = ix; m.RecalculateBounds();
            return m;
        }
    }
    public static Material VertexLit(string name, float wind)
    {
        string path = "Assets/_Aldara/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Aldara/VertexLit")); m.enableInstancing = true; AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_Wind", wind); return m;
    }
    // A prefab holding one exported mesh. Meshes are kept in a sibling .asset so prefabs stay small.
    public static GameObject MeshPrefab(string meshBytesPath, string prefabPath, Material mat, bool collider)
    {
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(meshBytesPath);
        if (!ta) return null;
        string meshPath = Path.ChangeExtension(prefabPath, null) + "_mesh.asset";
        var mesh = Read(ta.bytes, Path.GetFileNameWithoutExtension(prefabPath));
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing) { EditorUtility.CopySerialized(mesh, existing); mesh = existing; } else AssetDatabase.CreateAsset(mesh, meshPath);
        var go = new GameObject(Path.GetFileNameWithoutExtension(prefabPath), typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) { var cc = go.AddComponent<CapsuleCollider>(); cc.radius = 0.25f; cc.height = mesh.bounds.size.y; cc.center = new Vector3(0, mesh.bounds.size.y / 2, 0); }
        Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
        var pf = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return pf;
    }
}

public static class AldaraTreeFactory
{
    // kind is "oak:2" etc. (tree type and variant, as in the browser's treeModel(type, v))
    public static GameObject Get(string kind)
    {
        string file = kind.Replace(':', '_');
        string prefab = "Assets/_Aldara/Models/Trees/" + file + ".prefab";
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        if (pf) return pf;
        var mat = AldaraMeshIO.VertexLit("Tree_" + kind.Split(':')[0], kind.StartsWith("willow") ? 0.8f : 0.35f);
        return AldaraMeshIO.MeshPrefab("Assets/_Aldara/Models/Trees/" + file + ".amesh.bytes", prefab, mat, true);
    }
}
