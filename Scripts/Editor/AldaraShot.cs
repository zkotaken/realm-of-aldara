using UnityEngine;

namespace Aldara.EditorTools
{
    // Close-up pictures of the world's models for checking the art (works in edit and play mode): a camera looking
    // the way the game's camera does, framed on one model, rendered to a PNG in the import folder.
    public static class AldaraShot
    {
        public const string OUT = "C:/Users/seanm/Downloads/AldaraClassicImport/";
        static Transform Find(string prefix, int nth)
        {
            var root = GameObject.Find("Objects"); if (!root) return null; int k = 0;
            foreach (Transform g in root.transform) foreach (Transform a in g) if (a.name.StartsWith(prefix)) { if (k++ == nth) return a; }
            return null;
        }
        /// edit mode: switch the enhanced look on so the structure shader can be seen without playing
        public static void EditLook(float dbg)
        {
            Shader.SetGlobalFloat("_AldaraHQ", 1); Shader.SetGlobalFloat("_StructOn", 1); Shader.SetGlobalFloat("_StructDbg", dbg);
            var sa = Resources.Load<Texture2DArray>("World/StructAlb"); var sn = Resources.Load<Texture2DArray>("World/StructNra");
            if (sa) Shader.SetGlobalTexture("_StructAlb", sa); if (sn) Shader.SetGlobalTexture("_StructNra", sn);
        }
        public static string Close(string prefix, string file, float size = 0, int nth = 0, float up = 0, int px = 1000)
        {
            var a = Find(prefix, nth); if (!a) return "no " + prefix;
            var rs = a.GetComponentsInChildren<MeshRenderer>(); if (rs.Length == 0) return "no renderer";
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return CloseAt(b.center + Vector3.up * up, size > 0 ? size : Mathf.Max(b.extents.y, b.extents.x) + 0.5f, file, px) + " " + a.name;
        }
        public static string CloseAt(Vector3 c, float size, string file, int px = 1000)
        {
            var main = Camera.main; if (!main) { var cg = GameObject.Find("Main Camera"); if (cg) main = cg.GetComponent<Camera>(); }
            if (!main) return "no camera";
            var go = new GameObject("AldaraShotCam"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(main); cam.rect = new Rect(0, 0, 1, 1);
            cam.ResetWorldToCameraMatrix(); cam.ResetProjectionMatrix();
            cam.transform.rotation = main.transform.rotation; cam.transform.position = c - main.transform.forward * 80f; cam.orthographic = true; cam.orthographicSize = size;
            var rt = new RenderTexture(px, px, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; cam.targetTexture = rt; cam.aspect = 1; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(px, px, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, px, px), 0, 0); tex.Apply(); RenderTexture.active = null;
            System.IO.File.WriteAllBytes(OUT + file + ".png", tex.EncodeToPNG());
            cam.targetTexture = null; Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(tex);
            return file;
        }
    }
}
