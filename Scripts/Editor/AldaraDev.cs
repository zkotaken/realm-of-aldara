using System.Collections.Generic;
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
        /// in play mode: a close look at a spot (unity position), at the game's own camera angle, saved to the inbox as JPG
        public static string Close(string file, float x, float y, float z, float size, float time = 0.42f, int px = 1000)
        {
            var P = AldaraPlayer.I; if (P) { P.x = x * 32; P.y = 28672 - (z - 6) * 32; }
            AldaraPost.SetTime(time); Step(10);
            var main = Camera.main; var go = new GameObject("zcam"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(main); cam.rect = new Rect(0, 0, 1, 1); cam.ResetWorldToCameraMatrix(); cam.ResetProjectionMatrix();
            var tgt = new Vector3(x, y, z); cam.transform.rotation = main.transform.rotation; cam.transform.position = tgt - main.transform.forward * 80f; cam.orthographic = true; cam.orthographicSize = size;
            var rt = new RenderTexture(px, px, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; cam.targetTexture = rt; cam.aspect = 1; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(px, px, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, px, px), 0, 0); tex.Apply(); RenderTexture.active = null;
            File.WriteAllBytes(AldaraImport.INBOX + "/" + file + ".jpg", tex.EncodeToJPG(90));
            cam.targetTexture = null; Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(tex); return file;
        }
        /// in play mode: a hero of any class in an outfit ("slot=Name;..."), from the front three-quarter, the side and the
        /// back, lit by the world's sun, side by side in one picture in the inbox
        public static string Lookbook(string cls, string items, string file, string rarity = "Epic", int px = 420)
        {
            var eq = new Dictionary<string, Item>();
            foreach (var kv in items.Split(';')) { var p = kv.Split('='); if (p.Length == 2) eq[p[0]] = new Item { type = p[0], name = p[1], rarity = rarity, cls = cls }; }
            Item w; eq.TryGetValue("weapon", out w); string id = AldaraHeroGear.BaseFor(cls, w);
            var P = AldaraPlayer.I; var home = P ? P.transform.position + new Vector3(0, 0, -30) : new Vector3(-8000, 0, -8000);
            var rig = new GameObject("LookRig"); rig.transform.position = home;
            var hero = Object.Instantiate(AldaraHeroGear.Prefab(id), rig.transform, false);
            var g = hero.GetComponent<AldaraHeroGear>(); g.Apply(eq);
            var cg = new GameObject("cam"); var cam = cg.AddComponent<Camera>(); cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.19f, 0.2f, 0.23f, 1);
            cam.orthographicSize = 1.25f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 60; cam.aspect = 0.62f;
            int W = Mathf.RoundToInt(px * 0.62f), H = px; var sheet = new Texture2D(W * 3, H, TextureFormat.RGB24, false);
            float[] yaw = { 145, 90, 0 };
            foreach (var tr in hero.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 31; cam.cullingMask = 1 << 31;
            for (int v = 0; v < 3; v++)
            {
                hero.transform.localRotation = Quaternion.Euler(0, yaw[v], 0); Step(4); foreach (var tr in hero.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 31;
                var tgt = home + Vector3.up * 1.05f; cg.transform.rotation = Quaternion.Euler(18, 0, 0); cg.transform.position = tgt - cg.transform.forward * 20;
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 }; cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; var t = new Texture2D(W, H, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, W, H), 0, 0); t.Apply(); RenderTexture.active = null;
                sheet.SetPixels(v * W, 0, W, H, t.GetPixels()); cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(t);
            }
            sheet.Apply(); File.WriteAllBytes(AldaraImport.INBOX + "/" + file + ".jpg", sheet.EncodeToJPG(92)); Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(cg); Object.DestroyImmediate(rig); return id;
        }
        /// in play mode: every helmet of a class up close (front three-quarter and side), in a 7 x 4 sheet
        public static string Helmets(string cls, string file, string rarity = "Epic", int px = 220)
        {
            var meta = AldaraHeroGear_Pieces(cls); var names = new List<string>();
            foreach (var kv in meta) if ((string)kv.Value["slot"] == "helmet") names.Add(kv.Key);
            var P = AldaraPlayer.I; var home = P ? P.transform.position + new Vector3(0, 0, -30) : new Vector3(-8000, 0, -8000);
            int cols = 7, rows = (names.Count + cols - 1) / cols; var sheet = new Texture2D(px * 2 * cols, px * rows, TextureFormat.RGB24, false);
            var cg = new GameObject("cam"); var cam = cg.AddComponent<Camera>(); cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.19f, 0.2f, 0.23f, 1);
            cam.orthographicSize = 0.26f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 60; cam.aspect = 1; cam.cullingMask = 1 << 31;
            for (int i = 0; i < names.Count; i++)
            {
                var eq = new Dictionary<string, Item>(); eq["helmet"] = new Item { type = "helmet", name = names[i], rarity = rarity, cls = cls };
                var rig = new GameObject("HelmRig"); rig.transform.position = home; var hero = Object.Instantiate(AldaraHeroGear.Prefab(AldaraHeroGear.BaseFor(cls, null)), rig.transform, false);
                hero.GetComponent<AldaraHeroGear>().Apply(eq);
                float[] yaw = { 150, 90 };
                for (int v = 0; v < 2; v++)
                {
                    hero.transform.localRotation = Quaternion.Euler(0, yaw[v], 0); Step(3); foreach (var tr in hero.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 31;
                    bool any = false; var b = new Bounds(home + Vector3.up * 1.45f, Vector3.zero);
                    foreach (var mf in hero.GetComponentsInChildren<MeshFilter>()) { var r = mf.GetComponent<Renderer>(); if (!r || !r.enabled || !mf.sharedMesh || !mf.sharedMesh.name.StartsWith(names[i] + ":")) continue; if (!any) b = r.bounds; else b.Encapsulate(r.bounds); any = true; }
                    var tgt = b.center; cg.transform.rotation = Quaternion.Euler(12, 0, 0); cg.transform.position = tgt - cg.transform.forward * 20;
                    var rt = new RenderTexture(px, px, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 }; cam.targetTexture = rt; cam.Render();
                    RenderTexture.active = rt; var t = new Texture2D(px, px, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, px, px), 0, 0); t.Apply(); RenderTexture.active = null;
                    sheet.SetPixels(((i % cols) * 2 + v) * px, (rows - 1 - i / cols) * px, px, px, t.GetPixels()); cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(t);
                }
                Object.DestroyImmediate(rig);
            }
            sheet.Apply(); File.WriteAllBytes(AldaraImport.INBOX + "/" + file + ".jpg", sheet.EncodeToJPG(92)); Object.DestroyImmediate(sheet); Object.DestroyImmediate(cg);
            return string.Join(",", names.ToArray());
        }
        static Newtonsoft.Json.Linq.JObject AldaraHeroGear_Pieces(string cls) { var ta = Resources.Load<TextAsset>("Gear/pieces_" + cls + "_meta"); return Newtonsoft.Json.Linq.JObject.Parse(ta.text); }
        /// in play mode: the hero up close at several facings (wings and pet hidden for the shot only), out of a fight and in one
        /// in play mode: the hero standing in the given class and weapon (wings off), in its ready stance; each candidate
        /// ("rig,rx,ry,rz,rig,rx,ry,rz|...") overrides those rig joints, shot front and side to t{i}f / t{i}s; ready=false shoots the easy stance
        public static string PoseTest(string cls, string weapon, string cands, bool ready = true, float size = 1.3f)
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I; if (!P || !H) return "no hero"; H.target = null;
            var lf = typeof(AldaraHero).GetField("lastFight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var rigM = typeof(AldaraHeroGear).GetMethod("Rig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var origCls = H.cls; Item origW; H.equip.TryGetValue("weapon", out origW); Item origWings; H.equip.TryGetValue("wings", out origWings); H.equip.Remove("wings");
            System.Action<string, float> Shot = (file, yaw) =>
            {
                var main = Camera.main; var go = new GameObject("zcam"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(main); cam.rect = new Rect(0, 0, 1, 1); cam.ResetWorldToCameraMatrix(); cam.ResetProjectionMatrix();
                var tgt = P.transform.position + Vector3.up * 0.8f; var rot = Quaternion.Euler(0, yaw, 0) * main.transform.rotation; cam.transform.rotation = rot; cam.transform.position = tgt - (rot * Vector3.forward) * 80f; cam.orthographic = true; cam.orthographicSize = size;
                var rt = new RenderTexture(300, 300, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; cam.targetTexture = rt; cam.aspect = 1; cam.Render();
                RenderTexture.active = rt; var tex = new Texture2D(300, 300, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 300, 300), 0, 0); tex.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(AldaraImport.INBOX + "/" + file + ".jpg", tex.EncodeToJPG(88)); cam.targetTexture = null; Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(tex);
            };
            var off = new List<Renderer>(); int n = 0;
            try
            {
                H.cls = cls; if (!string.IsNullOrEmpty(weapon)) H.equip["weapon"] = new Item { type = "weapon", name = weapon, cls = cls, rarity = "Epic" }; AldaraHeroGear.SyncPlayer();
                var pet = GameObject.Find("Pet"); if (pet) foreach (var r in pet.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; off.Add(r); }
                P.facing = 1.57f; lf.SetValue(H, -99f);
                for (int f = 0; f < 40; f++) { if (ready) H.MarkFight(); Step(1); }
                var g = P.model.GetComponent<AldaraHeroGear>();
                foreach (var c in cands.Split('|'))
                {
                    if (ready) H.MarkFight(); Step(1);
                    var v = new List<float>(); foreach (var x in c.Split(',')) { float q; if (float.TryParse(x.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out q)) v.Add(q); }
                    for (int j = 0; j + 3 < v.Count; j += 4) { var tr = (Transform)rigM.Invoke(g, new object[] { (int)v[j] }); if (tr) tr.localRotation = AldaraHeroGear.Rot(v[j + 1], v[j + 2], v[j + 3]); }
                    Shot("t" + n + "f", 0); Shot("t" + n + "s", 70); n++;
                }
            }
            finally
            {
                foreach (var r in off) if (r) r.enabled = true;
                lf.SetValue(H, -99f); H.cls = origCls; if (origW != null) H.equip["weapon"] = origW; if (origWings != null) H.equip["wings"] = origWings; AldaraHeroGear.SyncPlayer();
            }
            return "shots " + n;
        }
        public static string HeroShots(string prefix, float size = 1.3f)
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I; if (!P || !H) return "no hero"; H.target = null;
            var off = new List<Renderer>();
            foreach (var r in P.GetComponentsInChildren<Renderer>()) { var t = r.transform; bool wing = false; while (t != null && t != P.transform) { if (t.name.StartsWith("wing")) wing = true; t = t.parent; } if (wing && r.enabled) { r.enabled = false; off.Add(r); } }
            var pet = GameObject.Find("Pet"); if (pet) foreach (var r in pet.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; off.Add(r); }
            var lf = typeof(AldaraHero).GetField("lastFight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            System.Action<string> Shot = file =>
            {
                Step(3); var main = Camera.main; var go = new GameObject("zcam"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(main); cam.rect = new Rect(0, 0, 1, 1); cam.ResetWorldToCameraMatrix(); cam.ResetProjectionMatrix();
                var tgt = P.transform.position + Vector3.up * 0.8f; cam.transform.rotation = main.transform.rotation; cam.transform.position = tgt - main.transform.forward * 80f; cam.orthographic = true; cam.orthographicSize = size;
                var rt = new RenderTexture(600, 600, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; cam.targetTexture = rt; cam.aspect = 1; cam.Render();
                RenderTexture.active = rt; var tex = new Texture2D(600, 600, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 600, 600), 0, 0); tex.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(AldaraImport.INBOX + "/" + file + ".jpg", tex.EncodeToJPG(90)); cam.targetTexture = null; Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(tex);
            };
            lf.SetValue(H, -99f); string[] nm = { "n", "s", "e", "w" }; float[] fa = { -1.57f, 1.57f, 0, 3.14f };
            for (int i = 0; i < 4; i++) { P.facing = fa[i]; Shot(prefix + "_" + nm[i]); }
            H.MarkFight(); P.facing = 1.57f; Shot(prefix + "_fight"); lf.SetValue(H, -99f);
            foreach (var r in off) r.enabled = true; return "ok";
        }
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
