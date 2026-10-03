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
    }
}
