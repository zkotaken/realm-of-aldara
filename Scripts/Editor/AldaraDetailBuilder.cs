using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aldara.EditorTools
{
    // Builds the ground's high-resolution surface layers (enhanced rendering) from the Poly Haven CC0 photo textures in
    // World/Detail: an albedo array and a normal array, one layer per surface (grass, dirt, rock, sand, snow, ash, mud,
    // gravel), saved to Resources/World so the ground shader can blend them into the painted map.
    public static class AldaraDetailBuilder
    {
        public static readonly string[] IDS = { "leafy_grass", "forest_ground_04", "rock_face_03", "sand_01", "sand_01", "burned_ground_01", "brown_mud_leaves_01", "ground_grey" };
        const string SRC = "Assets/_Aldara/World/Detail/", DST = "Assets/_Aldara/Resources/World/";

        [MenuItem("Aldara/Build Ground Detail Layers")]
        public static string Build()
        {
            AssetDatabase.Refresh();
            foreach (var kind in new[] { "diff", "nor" })
                foreach (var id in IDS)
                {
                    var p = SRC + id + "_" + kind + ".jpg"; var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti == null) return "missing " + p;
                    bool dirty = false;
                    if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
                    if (ti.sRGBTexture != (kind == "diff")) { ti.sRGBTexture = kind == "diff"; dirty = true; }
                    if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; dirty = true; }
                    if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; dirty = true; }
                    if (!ti.isReadable) { ti.isReadable = true; dirty = true; }
                    if (ti.textureCompression != TextureImporterCompression.CompressedHQ) { ti.textureCompression = TextureImporterCompression.CompressedHQ; dirty = true; }
                    if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                    var ps = ti.GetPlatformTextureSettings("Standalone"); if (!ps.overridden || ps.format != TextureImporterFormat.BC7) { ps.overridden = true; ps.format = TextureImporterFormat.BC7; ps.maxTextureSize = 1024; ti.SetPlatformTextureSettings(ps); dirty = true; }
                    if (dirty) ti.SaveAndReimport();
                }
            Directory.CreateDirectory(DST);
            string r = "";
            foreach (var kind in new[] { "diff", "nor" })
            {
                var first = AssetDatabase.LoadAssetAtPath<Texture2D>(SRC + IDS[0] + "_" + kind + ".jpg");
                var arr = new Texture2DArray(first.width, first.height, IDS.Length, first.format, true, kind == "nor") { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
                for (int i = 0; i < IDS.Length; i++)
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(SRC + IDS[i] + "_" + kind + ".jpg");
                    if (t.width != first.width || t.format != first.format) return "size or format differs: " + IDS[i] + " " + t.width + " " + t.format;
                    for (int m = 0; m < t.mipmapCount; m++) arr.SetPixelData(t.GetPixelData<byte>(m), m, i);
                }
                arr.Apply(false, false);
                var path = DST + (kind == "diff" ? "DetailAlb.asset" : "DetailNor.asset");
                var old = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path); if (old) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(arr, path); r += path + " " + arr.width + " " + arr.format + " mips " + arr.mipmapCount + "; ";
            }
            AssetDatabase.SaveAssets();
            return r;
        }
    }
}
