using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aldara
{
    // Development helpers used while porting: apply a zip from the import inbox (scripts, art, data), fix texture import
    // settings for the UI art, and drive play mode a frame at a time (the editor pauses play when it is not focused).
    public static class AldaraDev
    {
        public static string Apply(string zip)
        {
            Time.captureDeltaTime = 0; EditorApplication.isPaused = false; EditorApplication.isPlaying = false;
            AldaraImport.Unzip(AldaraImport.INBOX + "/" + zip + ".zip"); AssetDatabase.Refresh();
            int n = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Aldara/Resources/UI" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g); var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti == null) continue;
                if (!ti.mipmapEnabled && ti.textureCompression == TextureImporterCompression.Uncompressed && ti.npotScale == TextureImporterNPOTScale.None) continue;
                ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.alphaIsTransparency = true; ti.npotScale = TextureImporterNPOTScale.None; ti.SaveAndReimport(); n++;
            }
            return "applied " + zip + ", textures fixed " + n;
        }
        /// in play mode: load a save slot and step a few frames
        public static string Load(int slot)
        {
            AldaraSave.Load(slot); EditorApplication.isPaused = true; Time.captureDeltaTime = 1f / 30f; Step(5); return "loaded " + AldaraHero.I.heroName;
        }
        public static void Step(int n) { for (int i = 0; i < n; i++) EditorApplication.Step(); }
        public static string Open(string win) { if (AldaraWindows.I) AldaraWindows.I.Toggle(win, true); Step(8); return "open " + win; }
        /// copy the newest screenshot to the inbox so it can be fetched
        public static string Shot(string name)
        {
            var dir = new DirectoryInfo("Assets/Screenshots"); FileInfo best = null;
            foreach (var f in dir.GetFiles("*.png")) if (best == null || f.LastWriteTimeUtc > best.LastWriteTimeUtc) best = f;
            if (best == null) return "none"; File.Copy(best.FullName, AldaraImport.INBOX + "/" + name + ".png", true); return best.Name;
        }
    
        /// in play mode: a hero in the given outfit ("slot=Name;slot=Name", rarity Epic), front view, saved to the inbox
        public static string Outfit(string cls, string items, string file, string rarity = "Epic")
        {
            var eq = new System.Collections.Generic.Dictionary<string, Item>();
            foreach (var kv in items.Split(';')) { var p = kv.Split('='); if (p.Length == 2) eq[p[0]] = new Item { type = p[0], name = p[1], rarity = rarity, cls = cls }; }
            Item w; eq.TryGetValue("weapon", out w); string id = AldaraHeroGear.BaseFor(cls, w);
            var home = new Vector3(-8000, 0, -8000); var rig = new GameObject("OutfitRig"); rig.transform.position = home;
            var hero = Object.Instantiate(AldaraHeroGear.Prefab(id), rig.transform, false); hero.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var g = hero.GetComponent<AldaraHeroGear>(); g.Apply(eq);
            var cg = new GameObject("cam"); cg.transform.SetParent(rig.transform, false); var cam = cg.AddComponent<Camera>(); cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.235f, 0.235f, 0.275f, 1);
            cam.orthographicSize = 6 * 0.5f / 1f * 0.8125f; cam.transform.localPosition = new Vector3(0, 1.5f * 0.8125f, -12); cam.nearClipPlane = 0.1f; cam.farClipPlane = 50;
            var rt = new RenderTexture(260, 600, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; cam.targetTexture = rt;
            Step(6); cam.Render(); RenderTexture.active = rt; var t = new Texture2D(260, 600, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, 260, 600), 0, 0); t.Apply(); RenderTexture.active = null;
            File.WriteAllBytes(AldaraImport.INBOX + "/" + file + ".png", t.EncodeToPNG()); cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(rig);
            return id;
        }

        /// in play mode: a pet at rest, three-quarter view, saved to the inbox
        public static string PetShot(string id, string file, float size = 1.5f)
        {
            var home = new Vector3(-8200, 0, -8000); var rig = new GameObject("PetRig"); rig.transform.position = home;
            var pf = Resources.Load<GameObject>("PetPrefabs/Pet_" + id); if (!pf) return "no prefab";
            var g = Object.Instantiate(pf, rig.transform, false); g.transform.localRotation = Quaternion.Euler(0, 90 + (Mathf.PI / 2 + 0.6f) * Mathf.Rad2Deg, 0);
            var cg = new GameObject("cam"); cg.transform.SetParent(rig.transform, false); var cam = cg.AddComponent<Camera>(); cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.235f, 0.235f, 0.275f, 1);
            cam.orthographicSize = size; cam.transform.localPosition = new Vector3(0, size * 0.6f, -12); cam.transform.localRotation = Quaternion.Euler(8, 0, 0); cam.nearClipPlane = 0.1f; cam.farClipPlane = 50;
            var rt = new RenderTexture(300, 300, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; cam.targetTexture = rt;
            Step(6); cam.Render(); RenderTexture.active = rt; var t = new Texture2D(300, 300, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, 300, 300), 0, 0); t.Apply(); RenderTexture.active = null;
            File.WriteAllBytes(AldaraImport.INBOX + "/" + file + ".png", t.EncodeToPNG()); cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(rig);
            return "ok";
        }
}
}
