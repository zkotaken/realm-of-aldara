using System.IO;
using UnityEditor;
using UnityEngine;
using Aldara;

// Monster prefabs from the browser's baked monsters: one node per joint with its mesh, plus the animation data.
public static class AldaraMonsterBuilder
{
    [MenuItem("Aldara/Build Monsters")]
    public static void BuildAll()
    {
        var book = MonsterBook.Load(); int n = 0;
        Directory.CreateDirectory("Assets/_Aldara/Resources/MonPrefabs"); Directory.CreateDirectory("Assets/_Aldara/Models/Mons/Meshes");
        var mat = AldaraMeshIO.VertexLit("Monsters", 0);
        foreach (var d in book.monsters) if (Build(d.id, mat)) n++;
        AssetDatabase.SaveAssets(); Debug.Log("Aldara monsters built: " + n);
    }
    /// the people of Lorenmar and Valcrest, the quest givers and the bounty board
    [MenuItem("Aldara/Build Folk")]
    public static void BuildFolk()
    {
        Directory.CreateDirectory("Assets/_Aldara/Resources/MonPrefabs"); Directory.CreateDirectory("Assets/_Aldara/Models/Mons/Meshes");
        var mat = AldaraMeshIO.VertexLit("Monsters", 0); int n = 0;
        var list = Newtonsoft.Json.Linq.JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Aldara/Resources/folk.json").text);
        foreach (var kv in list) if (Build((string)kv.Value["id"], mat)) n++;
        AldaraMeshIO.MeshPrefab("Assets/_Aldara/Models/Mons/qboard.amesh.bytes", "Assets/_Aldara/Resources/MonPrefabs/QBoard.prefab", mat, false);
        AssetDatabase.SaveAssets(); Debug.Log("Aldara folk built: " + n);
    }
    static bool Build(string id, Material mat)
    {
        var d = new { id = id };
        var data = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Aldara/Resources/Mons/" + d.id + ".bytes"); if (!data) return false;
        int N; int[] par;
        using (var r = new BinaryReader(new MemoryStream(data.bytes))) { r.ReadChars(4); N = r.ReadInt32(); par = new int[N]; for (int i = 0; i < N; i++) par[i] = r.ReadInt32(); }
        var root = new GameObject("Mon_" + d.id);
        var rig = new GameObject("Rig"); rig.transform.SetParent(root.transform, false); rig.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var T = new Transform[N];
        for (int i = 0; i < N; i++)
        {
            var go = new GameObject("n" + i); T[i] = go.transform; go.transform.SetParent(par[i] < 0 ? rig.transform : T[par[i]], false);
            var mb = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Aldara/Models/Mons/" + d.id + "_n" + i + ".amesh.bytes");
            if (!mb) continue;
            string mp = "Assets/_Aldara/Models/Mons/Meshes/" + d.id + "_n" + i + ".asset";
            var mesh = AldaraMeshIO.Read(mb.bytes, d.id + "_n" + i);
            var ex = AssetDatabase.LoadAssetAtPath<Mesh>(mp); if (ex) { EditorUtility.CopySerialized(mesh, ex); mesh = ex; } else AssetDatabase.CreateAsset(mesh, mp);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        root.AddComponent<AldaraMonsterAnimator>().data = data;
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Aldara/Resources/MonPrefabs/Mon_" + d.id + ".prefab");
        Object.DestroyImmediate(root);
        return true;
    }
}
