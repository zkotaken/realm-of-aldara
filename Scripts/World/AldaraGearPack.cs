using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Aldara
{
    // A pack of meshes exported from the browser game (hero bodies, weapons, wings, armour pieces), quantised:
    // "AGQ1", int count, then per entry: string key, int len (-1 = same mesh as the key that follows), mesh bytes:
    // int nv, int ni, float min[3], float ext[3], ushort pos[nv*3], sbyte normal[nv*3], rgba[nv*4], emission[nv],
    // indices (ushort when nv < 65536, else int). Meshes are built on first use and shared.
    public class AldaraGearPack
    {
        readonly byte[] data; readonly Dictionary<string, int> at = new Dictionary<string, int>(); readonly Dictionary<string, string> alias = new Dictionary<string, string>();
        readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, AldaraGearPack> packs = new Dictionary<string, AldaraGearPack>();
        public static AldaraGearPack Load(string res)
        {
            AldaraGearPack p; if (packs.TryGetValue(res, out p)) return p;
            var ta = Resources.Load<TextAsset>(res); p = ta ? new AldaraGearPack(ta.bytes) : null; packs[res] = p; return p;
        }
        public AldaraGearPack(byte[] b)
        {
            data = b; using (var r = new BinaryReader(new MemoryStream(b)))
            {
                r.ReadChars(4); int n = r.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    string k = r.ReadString(); int len = r.ReadInt32();
                    if (len < 0) { alias[k] = r.ReadString(); continue; }
                    at[k] = (int)r.BaseStream.Position; r.BaseStream.Position += len;
                }
            }
        }
        public bool Has(string k) { return at.ContainsKey(k) || alias.ContainsKey(k); }
        /// the mesh for a key (null when the key is missing or the mesh is empty)
        public Mesh Get(string k)
        {
            string a; while (alias.TryGetValue(k, out a)) k = a;
            Mesh m; if (cache.TryGetValue(k, out m)) return m;
            int o; if (!at.TryGetValue(k, out o)) return null;
            m = Read(data, o, k); cache[k] = m; return m;
        }
        public static Mesh Read(byte[] d, int o, string name)
        {
            using (var r = new BinaryReader(new MemoryStream(d, o, d.Length - o)))
            {
                int nv = r.ReadInt32(), ni = r.ReadInt32(); if (nv == 0) return null;
                var mn = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); var ex = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var p = new Vector3[nv]; var n = new Vector3[nv]; var c = new Color32[nv]; var e = new Vector2[nv]; var ix = new int[ni];
                for (int i = 0; i < nv; i++) p[i] = new Vector3(mn.x + r.ReadUInt16() / 65535f * ex.x, mn.y + r.ReadUInt16() / 65535f * ex.y, mn.z + r.ReadUInt16() / 65535f * ex.z);
                for (int i = 0; i < nv; i++) n[i] = new Vector3(r.ReadSByte() / 127f, r.ReadSByte() / 127f, r.ReadSByte() / 127f).normalized;
                for (int i = 0; i < nv; i++) c[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                for (int i = 0; i < nv; i++) e[i] = new Vector2(r.ReadByte() / 255f, 0);
                if (nv < 65536) for (int i = 0; i < ni; i++) ix[i] = r.ReadUInt16(); else for (int i = 0; i < ni; i++) ix[i] = r.ReadInt32();
                var m = new Mesh { name = name, indexFormat = nv > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                m.vertices = p; m.normals = n; m.colors32 = c; m.uv2 = e; m.triangles = ix; m.RecalculateBounds();
                return m;
            }
        }
    }
}
