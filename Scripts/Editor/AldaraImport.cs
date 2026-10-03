using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

// Unpacks data exported from the browser game (sent as zips) into Assets/_Aldara.
public static class AldaraImport
{
    public const string INBOX = "C:/Users/seanm/Downloads/AldaraClassicImport";
    public static int Unzip(string zipPath)
    {
        string dst = Application.dataPath + "/_Aldara"; int n = 0;
        using (var fs = File.OpenRead(zipPath))
        using (var z = new ZipArchive(fs))
            foreach (var e in z.Entries)
            {
                if (e.Name == "") continue;
                var to = Path.Combine(dst, e.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                using (var s = e.Open()) using (var f = File.Create(to)) s.CopyTo(f);
                n++;
            }
        AssetDatabase.Refresh();
        return n;
    }
}
