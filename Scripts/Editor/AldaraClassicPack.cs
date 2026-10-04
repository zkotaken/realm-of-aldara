using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Aldara.EditorTools
{
    // Packs a Windows build for the Realm of Aldara Launcher: every file is cut into 48 MB pieces, each piece gzipped
    // and stored as parts/<sha256 of the gzip>.gz (identical pieces are shared between versions), and
    // manifests/<version>.json lists every file with its checksum and parts. The launcher (classic.go) downloads only
    // the files whose checksum changed. Writes <out>/pack_result.json when done.
    public static class AldaraClassicPack
    {
        const int CHUNK = 48 << 20;
        static string Hex(byte[] b) { var sb = new StringBuilder(b.Length * 2); foreach (var x in b) sb.Append(x.ToString("x2")); return sb.ToString(); }
        static string Esc(string s) { return s.Replace("\\", "\\\\").Replace("\"", "\\\""); }

        public static string Pack(string build, string outDir, string version)
        {
            var parts = Path.Combine(outDir, "parts"); Directory.CreateDirectory(parts); Directory.CreateDirectory(Path.Combine(outDir, "manifests"));
            var files = new List<string>(); foreach (var f in Directory.GetFiles(build, "*", SearchOption.AllDirectories)) if (!f.Contains("_BackUpThisFolder_ButDontShipItWithYourGame")) files.Add(f);
            files.Sort(System.StringComparer.Ordinal);
            var sb = new StringBuilder(); sb.Append("{\"version\":\"").Append(Esc(version)).Append("\",\"exe\":\"");
            string exe = null; foreach (var f in files) { var rel = f.Substring(build.Length).TrimStart('/', '\\').Replace('\\', '/'); if (!rel.Contains("/") && rel.EndsWith(".exe") && !rel.ToLower().Contains("crashhandler")) exe = rel; }
            sb.Append(Esc(exe ?? "")).Append("\",\"files\":[");
            long total = 0, fresh = 0; var buf = new byte[CHUNK]; bool firstF = true;
            foreach (var f in files)
            {
                var rel = f.Substring(build.Length).TrimStart('/', '\\').Replace('\\', '/');
                using (var whole = SHA256.Create())
                using (var fs = File.OpenRead(f))
                {
                    var plist = new StringBuilder(); long size = 0; bool firstP = true;
                    while (true)
                    {
                        int n = 0, r; while (n < CHUNK && (r = fs.Read(buf, n, CHUNK - n)) > 0) n += r;
                        if (n == 0 && size > 0) break;
                        whole.TransformBlock(buf, 0, n, null, 0); size += n;
                        byte[] gz; using (var ms = new MemoryStream()) { using (var g = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) g.Write(buf, 0, n); gz = ms.ToArray(); }
                        string h; using (var s = SHA256.Create()) h = Hex(s.ComputeHash(gz));
                        var pp = Path.Combine(parts, h + ".gz"); if (!File.Exists(pp)) { File.WriteAllBytes(pp, gz); fresh += gz.Length; }
                        if (!firstP) plist.Append(','); firstP = false;
                        plist.Append("{\"h\":\"").Append(h).Append("\",\"n\":").Append(gz.Length).Append(",\"r\":").Append(n).Append('}');
                        total += gz.Length;
                        if (n < CHUNK) break;
                    }
                    whole.TransformFinalBlock(new byte[0], 0, 0);
                    if (!firstF) sb.Append(','); firstF = false;
                    sb.Append("{\"p\":\"").Append(Esc(rel)).Append("\",\"s\":\"").Append(Hex(whole.Hash)).Append("\",\"n\":").Append(size).Append(",\"parts\":[").Append(plist).Append("]}");
                }
            }
            sb.Append("]}");
            var man = Encoding.UTF8.GetBytes(sb.ToString()); var mp = Path.Combine(outDir, "manifests", version + ".json"); File.WriteAllBytes(mp, man);
            string ms2; using (var s = SHA256.Create()) ms2 = Hex(s.ComputeHash(man));
            var res = "{\"version\":\"" + Esc(version) + "\",\"exe\":\"" + Esc(exe ?? "") + "\",\"files\":" + files.Count + ",\"download\":" + total + ",\"new_part_bytes\":" + fresh + ",\"manifest\":\"manifests/" + Esc(version) + ".json\",\"manifest_sha\":\"" + ms2 + "\"}";
            File.WriteAllText(Path.Combine(outDir, "pack_result.json"), res);
            return res;
        }
    }
}
