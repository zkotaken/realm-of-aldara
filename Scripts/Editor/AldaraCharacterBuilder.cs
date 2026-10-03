using System.IO;
using UnityEditor;
using UnityEngine;
using Aldara;

// Builds hero prefabs from the hierarchy and meshes exported out of the browser game (one mesh per joint),
// with the baked animation data on an AldaraCharacterAnimator.
public static class AldaraCharacterBuilder
{
    const string SRC = "Assets/_Aldara/Models/Chars";
    [MenuItem("Aldara/Build Heroes")]
    public static void BuildAll() { foreach (var c in new[] { "knight", "mage", "archer" }) Build(c); AssetDatabase.SaveAssets(); }

    public static GameObject Build(string cls)
    {
        var data = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Aldara/Resources/Chars/" + cls + ".bytes");
        if (!data) { Debug.LogWarning("no hero data for " + cls); return null; }
        int N; int[] par;
        using (var r = new BinaryReader(new MemoryStream(data.bytes))) { r.ReadChars(4); N = r.ReadInt32(); par = new int[N]; for (int i = 0; i < N; i++) par[i] = r.ReadInt32(); }
        var mat = AldaraMeshIO.VertexLit("Hero", 0);
        var root = new GameObject("Hero_" + cls);
        var rig = new GameObject("Rig"); rig.transform.SetParent(root.transform, false); rig.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var T = new Transform[N];
        Directory.CreateDirectory(SRC + "/Meshes");
        for (int i = 0; i < N; i++)
        {
            var go = new GameObject("n" + i); T[i] = go.transform;
            go.transform.SetParent(par[i] < 0 ? rig.transform : T[par[i]], false);
            var mb = AssetDatabase.LoadAssetAtPath<TextAsset>(SRC + "/" + cls + "_n" + i + ".amesh.bytes");
            if (mb)
            {
                string mp = SRC + "/Meshes/" + cls + "_n" + i + ".asset";
                var mesh = AldaraMeshIO.Read(mb.bytes, cls + "_n" + i);
                var ex = AssetDatabase.LoadAssetAtPath<Mesh>(mp); if (ex) { EditorUtility.CopySerialized(mesh, ex); mesh = ex; } else AssetDatabase.CreateAsset(mesh, mp);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            }
        }
        var an = root.AddComponent<AldaraCharacterAnimator>(); an.data = data; an.cls = cls;
        an.strideK = cls == "mage" ? 0.082f : cls == "archer" ? 0.074f : 0.078f;
        an.Load(); an.ApplyRest();
        Directory.CreateDirectory("Assets/_Aldara/Prefabs");
        var pf = PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Aldara/Prefabs/Hero_" + cls + ".prefab");
        Directory.CreateDirectory("Assets/_Aldara/Resources/Heroes"); PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Aldara/Resources/Heroes/Hero_" + cls + ".prefab");
        Object.DestroyImmediate(root);
        return pf;
    }

    [MenuItem("Aldara/Set Up Player")]
    public static void SetUpPlayer() { SetUpPlayer("knight"); }
    public static void SetUpPlayer(string cls)
    {
        var old = GameObject.Find("Player"); if (old) Object.DestroyImmediate(old);
        var p = new GameObject("Player"); var pl = p.AddComponent<AldaraPlayer>();
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Aldara/Prefabs/Hero_" + cls + ".prefab");
        if (pf) { var h = (GameObject)PrefabUtility.InstantiatePrefab(pf); h.transform.SetParent(p.transform, false); pl.model = h.transform; pl.anim = h.GetComponent<AldaraCharacterAnimator>(); }
        p.transform.position = AldaraWorld.ToUnityFlat(AldaraWorld.TOWN_SPAWN.x, AldaraWorld.TOWN_SPAWN.y);
        var cam = Camera.main; if (cam)
        {
            var c = cam.GetComponent<AldaraCamera>() ?? cam.gameObject.AddComponent<AldaraCamera>(); c.target = p.transform;
            cam.fieldOfView = 34; cam.nearClipPlane = 0.3f; cam.farClipPlane = 600;
        }
    }
}
