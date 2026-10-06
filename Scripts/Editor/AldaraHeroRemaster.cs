using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Aldara.EditorTools
{
    // The heroes remastered (bodies, every armour piece, weapons, shields and wings): each vertex is given its material
    // from its colour and what the piece is (steel, gold, dark iron, painted metal, chain mail, dragon scale, leather,
    // cloth, skin, hair, gems, wood, feathers), read by the hero shading (Aldara/VertexLit, _HeroHD) for brushed and
    // hammered steel, engraved gold, links, scales, leather grain, woven cloth, skin and hair; and the faceted normals
    // are smoothed within each material while plate edges stay crisp. The packs become AGQ2 (a surface byte per vertex).
    public static class AldaraHeroRemaster
    {
        public const byte NONE = 0, STEEL = 1, GOLD = 2, LEATHER = 3, CLOTH = 4, SKIN = 5, HAIR = 6, GEM = 7, IRON = 8, ENAMEL = 9, CHAIN = 10, SCALE = 11, WOOD = 12, FEATHER = 13;
        const string G = "Assets/_Aldara/Resources/Gear/";
        static readonly string[] BASES = { "knight", "knight_great", "mage", "archer" }, CLASSES = { "knight", "mage", "archer" };

        public static string Run()
        {
            var log = new StringBuilder(); int meshes = 0;
            foreach (var id in BASES)
            {
                var meta = JObject.Parse(File.ReadAllText(G + "hero_" + id + "_meta.json")); var head = new HashSet<int>();
                if (meta["info"]["headCh"] != null) foreach (var i in meta["info"]["headCh"]) head.Add((int)i);
                // the weapon and shield the body carries by default (the nodes under its weapon and shield sockets)
                var wpn = new Dictionary<int, string>(); var hp = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Aldara/Resources/Heroes/Hero_" + id + ".prefab");
                var so = meta["sockets"] as JObject; string wname = meta["wname"] != null ? (string)meta["wname"] : "";
                if (hp && so != null)
                    foreach (var sk in new[] { "weapon", "shield" })
                    {
                        if (so[sk] == null) continue; string nn = "n" + (int)so[sk];
                        foreach (var t in hp.GetComponentsInChildren<Transform>(true))
                            if (t.name == nn) foreach (var d in t.GetComponentsInChildren<Transform>(true)) { int q; if (d.name.StartsWith("n") && int.TryParse(d.name.Substring(1), out q)) wpn[q] = sk == "shield" ? "Shield" : wname; }
                    }
                int n = RewritePack(G + "gear_" + id + ".bytes", k => Ctx(k, head, id, wpn)); meshes += n; log.Append(id + " gear " + n + "; ");
                // the bodies' own meshes, kept as assets for the prefabs: rebuilt from the remastered pack in place
                var pack = new AldaraGearPack(File.ReadAllBytes(G + "gear_" + id + ".bytes")); int b = 0;
                for (int i = 0; i < 200; i++)
                {
                    string mp = "Assets/_Aldara/Models/Chars/Meshes/" + id + "_n" + i + ".asset"; var ex = AssetDatabase.LoadAssetAtPath<Mesh>(mp); if (!ex) continue;
                    var src = pack.Get("n" + i); if (!src) continue; Into(src, ex); b++;
                }
                log.Append(id + " body " + b + "; ");
            }
            foreach (var cls in CLASSES) { var pmeta = JObject.Parse(File.ReadAllText(G + "pieces_" + cls + "_meta.json")); int n = RewritePack(G + "pieces_" + cls + ".bytes", k => PieceCtx(k, cls, pmeta)); meshes += n; log.Append(cls + " pieces " + n + "; "); }
            // the heroes' material switches the detail on
            foreach (var id in BASES)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Aldara/Resources/Heroes/Hero_" + id + ".prefab"); if (!pf) continue;
                foreach (var r in pf.GetComponentsInChildren<MeshRenderer>(true)) if (r.sharedMaterial && r.sharedMaterial.HasProperty("_HeroHD")) { r.sharedMaterial.SetFloat("_HeroHD", 1); EditorUtility.SetDirty(r.sharedMaterial); }
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            return meshes + " meshes; " + log;
        }

        static void Into(Mesh src, Mesh dst)
        {
            dst.Clear(); dst.indexFormat = src.indexFormat; dst.vertices = src.vertices; dst.normals = src.normals; dst.colors32 = src.colors32; dst.uv2 = src.uv2; dst.triangles = src.triangles; dst.RecalculateBounds();
            EditorUtility.SetDirty(dst);
        }

        // ---------- what each mesh is ----------
        public class C { public string kind, name, cls, shape; public bool hair; }
        static C Ctx(string key, HashSet<int> head, string id, Dictionary<int, string> wpn)
        {
            if (key.StartsWith("w_")) { int c = key.IndexOf(':'); string nm = key.Substring(2, (c > 0 ? c : key.Length) - 2); int bar = nm.IndexOf('|'); return new C { kind = "weapon", name = bar > 0 ? nm.Substring(0, bar) : nm }; }
            if (key.StartsWith("wing_")) return new C { kind = "wing", name = key.Substring(5) };
            if (key.StartsWith("head_")) return new C { kind = "body", name = key, shape = "head" };
            int node = -1; int k2 = key.LastIndexOf(':'); string num = key.StartsWith("n") ? key.Substring(1) : k2 >= 0 ? key.Substring(k2 + 1) : "";
            int.TryParse(num, out node);
            string wn; if (node >= 0 && key.StartsWith("n") && wpn.TryGetValue(node, out wn)) return new C { kind = "weapon", name = wn };
            bool hd = node >= 0 && head.Contains(node);
            // the head itself (the node with the face on it) is given a longer jaw like a real head
            return new C { kind = "body", name = key, hair = hd, shape = hd && key.StartsWith("n") && node == HeadNode(head) ? "head" : null };
        }
        static int HeadNode(HashSet<int> head) { int m = int.MaxValue; foreach (var h in head) m = Mathf.Min(m, h); return m; }
        static C PieceCtx(string key, string cls, JObject meta)
        {
            int c = key.LastIndexOf(':'); string nm = c > 0 ? key.Substring(0, c) : key, node = c > 0 ? key.Substring(c + 1) : "";
            var ctx = new C { kind = "piece", name = nm, cls = cls };
            // the head shells of the helmets that cover the head are reshaped: a hood for the mage and the archer (and the
            // knight's cloth and leather caps), a proper helm for the knight's metal ones
            var pm = meta[nm] as JObject;
            if (pm != null && (string)pm["slot"] == "helmet" && node == "2" && (int)(pm["covers"] ?? 0) == 1)
                ctx.shape = cls == "mage" || Any(nm, "Cloth Cap", "Leather Cap") || (cls == "archer" && !Any(nm, "Helm", "Visor")) ? "hood" : "helm";
            return ctx;
        }
        static bool Any(string s, params string[] w) { foreach (var x in w) if (s.IndexOf(x, System.StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }

        /// a vertex's material from its colour (sRGB as stored) and the piece it is on
        public static byte Classify(Color32 c32, float em, C ctx)
        {
            Color c = c32; float h, s, v; Color.RGBToHSV(c, out h, out s, out v); h *= 360;
            bool gold = h >= 28 && h <= 62 && s > 0.38f && v > 0.42f, brown = h >= 8 && h <= 48 && s > 0.28f && v < 0.55f && !gold, grey = s < 0.2f;
            bool skinTone = h >= 8 && h <= 42 && s > 0.18f && s < 0.62f && v > 0.5f;
            if (em > 0.05f) return GEM;
            string n = ctx.name ?? "";
            switch (ctx.kind)
            {
                case "body":
                    if (n.StartsWith("head_")) return n.Contains("shade") ? IRON : SKIN;
                    if (ctx.hair) return skinTone ? SKIN : HAIR;
                    if (skinTone) return SKIN; if (gold) return GOLD; if (brown) return LEATHER; if (grey && v > 0.55f) return STEEL;
                    return CLOTH;
                case "weapon":
                    {
                        bool staffish = Any(n, "Staff", "Wand", "Rod", "Scepter", "Bow", "Shield", "Oak");
                        if (gold) return GOLD; if (brown) return staffish ? WOOD : LEATHER;
                        if (staffish && skinTone) return WOOD;   // pale planks and shafts
                        if (grey) return v > 0.3f ? STEEL : IRON;
                        if (Any(n, "Crystal", "Runed", "Dragon", "Celestial", "Void", "Frost", "Hellfire") && s > 0.45f && v > 0.5f) return GEM;
                        return staffish && s < 0.5f ? WOOD : ENAMEL;
                    }
                case "wing":
                    if (gold) return GOLD; if (grey && v > 0.5f) return FEATHER; if (grey) return IRON; return s > 0.5f && v > 0.6f ? GEM : FEATHER;
                default:
                    {
                        bool clothy = Any(n, "Rags", "Cloak", "Cape", "Mantle", "Raiment", "Legwraps", "Wraps", "Trousers", "Sandals", "Cloth", "Wool", "Shroud", "Robe", "Frostweave");
                        bool leathery = Any(n, "Leather", "Vest", "hide", "Traveler");
                        bool chain = Any(n, "Chainmail", "Mithril Hauberk"), scale = Any(n, "Scale", "Wyrm", "Dragonhide", "Dragonclaw");
                        bool winged = Any(n, "Wings"), halo = Any(n, "Halo", "Circlet", "Crown");
                        bool helm = Any(n, "Helm", "Visor");
                        if (winged) return gold ? GOLD : grey && v < 0.35f ? IRON : FEATHER;
                        if (clothy && !(gold && s > 0.6f)) return brown ? LEATHER : CLOTH;   // gold-coloured cloth stays cloth; bright trim is gold
                        if (gold) return GOLD;
                        // a mage's robes and hoods are cloth, an archer's jerkins and hoods leather, whatever their colour
                        if (ctx.cls == "mage" && !grey && !scale) return brown && !clothy ? LEATHER : CLOTH;
                        if (ctx.cls == "archer" && !grey && !scale) return clothy ? CLOTH : LEATHER;
                        if (skinTone && !clothy && !leathery && !scale) return GOLD;   // pale warm metal (the deities' gilt), never skin on armour
                        if (helm && !grey && s > 0.45f) return FEATHER;      // plumes and crests
                        if (leathery) return grey && v > 0.5f ? STEEL : LEATHER;
                        if (brown) return LEATHER;
                        if (chain && grey) return CHAIN;
                        if (scale && !grey) return SCALE;
                        if (halo && !grey && s > 0.55f && v > 0.75f) return GEM;
                        if (grey) return v > 0.33f ? STEEL : IRON;
                        return scale ? SCALE : ENAMEL;
                    }
            }
        }

        // ---------- the packs ----------
        static int RewritePack(string path, System.Func<string, C> ctx)
        {
            var b = File.ReadAllBytes(path); var outS = new MemoryStream(); int count = 0;
            using (var r = new BinaryReader(new MemoryStream(b))) using (var w = new BinaryWriter(outS))
            {
                bool v2 = new string(r.ReadChars(4)) == "AGQ2"; int n = r.ReadInt32();
                w.Write("AGQ2".ToCharArray()); w.Write(n);
                for (int i = 0; i < n; i++)
                {
                    string k = r.ReadString(); int len = r.ReadInt32(); w.Write(k);
                    if (len < 0) { w.Write(len); w.Write(r.ReadString()); continue; }
                    var mesh = r.ReadBytes(len); var nb = Remaster(mesh, ctx(k), v2); w.Write(nb.Length); w.Write(nb); count++;
                }
                w.Flush(); File.WriteAllBytes(path, outS.ToArray());
            }
            return count;
        }
        static byte[] Remaster(byte[] m, C ctx, bool v2)
        {
            var o = new MemoryStream(); var w = new BinaryWriter(o);
            using (var r = new BinaryReader(new MemoryStream(m)))
            {
                int nv = r.ReadInt32(), ni = r.ReadInt32(); w.Write(nv); w.Write(ni); if (nv == 0) { w.Flush(); return o.ToArray(); }
                var mn = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); var ex = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var qp = new ushort[nv * 3]; for (int i = 0; i < nv * 3; i++) qp[i] = r.ReadUInt16();
                var nrm = new Vector3[nv]; for (int i = 0; i < nv; i++) nrm[i] = new Vector3(r.ReadSByte() / 127f, r.ReadSByte() / 127f, r.ReadSByte() / 127f).normalized;
                var col = new Color32[nv]; for (int i = 0; i < nv; i++) col[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                var em = new byte[nv]; for (int i = 0; i < nv; i++) em[i] = r.ReadByte();
                if (v2) for (int i = 0; i < nv; i++) r.ReadByte();
                var ix = new int[ni]; if (nv < 65536) for (int i = 0; i < ni; i++) ix[i] = r.ReadUInt16(); else for (int i = 0; i < ni; i++) ix[i] = r.ReadInt32();
                var pos = new Vector3[nv]; for (int i = 0; i < nv; i++) pos[i] = new Vector3(mn.x + qp[i * 3] / 65535f * ex.x, mn.y + qp[i * 3 + 1] / 65535f * ex.y, mn.z + qp[i * 3 + 2] / 65535f * ex.z);
                var surf = new byte[nv]; for (int i = 0; i < nv; i++) surf[i] = Classify(col[i], em[i] / 255f, ctx);
                if (ctx.shape != null)
                {   // reshaped: the positions are quantized again over their new bounds
                    Reshape(pos, ctx.shape, surf);
                    Vector3 lo = pos[0], hi = pos[0]; foreach (var q in pos) { lo = Vector3.Min(lo, q); hi = Vector3.Max(hi, q); }
                    mn = lo; ex = Vector3.Max(hi - lo, Vector3.one * 1e-5f);
                    for (int i = 0; i < nv; i++) for (int k = 0; k < 3; k++) qp[i * 3 + k] = (ushort)Mathf.Clamp(Mathf.RoundToInt((pos[i][k] - mn[k]) / ex[k] * 65535f), 0, 65535);
                }
                for (int k = 0; k < 3; k++) w.Write(mn[k]); for (int k = 0; k < 3; k++) w.Write(ex[k]);
                var sn = Smooth(pos, nrm, ix, surf);
                foreach (var q in qp) w.Write(q);
                foreach (var v in sn) { w.Write((sbyte)Mathf.Clamp(Mathf.RoundToInt(v.x * 127), -127, 127)); w.Write((sbyte)Mathf.Clamp(Mathf.RoundToInt(v.y * 127), -127, 127)); w.Write((sbyte)Mathf.Clamp(Mathf.RoundToInt(v.z * 127), -127, 127)); }
                foreach (var c in col) { w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a); }
                w.Write(em); w.Write(surf);
                if (nv < 65536) foreach (var i in ix) w.Write((ushort)i); else foreach (var i in ix) w.Write(i);
            }
            w.Flush(); return o.ToArray();
        }

        static float Median(List<float> l) { l.Sort(); return l[l.Count / 2]; }
        /// a helmet's head shell, reshaped about its centre in units of its radius (x to the side, y up, z to the back):
        /// the decorations standing off the shell (horns, plumes, halos, spikes) ride along at their roots and keep their tips
        static void Reshape(Vector3[] p, string shape, byte[] surf)
        {
            // a head is measured by its skin alone (the hair would pull the centre off the face)
            int nSkin = 0; if (shape == "head") foreach (var s in surf) if (s == SKIN) nSkin++;
            bool bySkin = nSkin >= 30;
            var xs = new List<float>(); var ys = new List<float>(); var zs = new List<float>();
            for (int i = 0; i < p.Length; i++) { if (bySkin && surf[i] != SKIN) continue; xs.Add(p[i].x); ys.Add(p[i].y); zs.Add(p[i].z); }
            var C = new Vector3(Median(xs), Median(ys), Median(zs)); var ds = new List<float>();
            for (int i = 0; i < p.Length; i++) { if (bySkin && surf[i] != SKIN) continue; ds.Add((p[i] - C).magnitude); }
            float R = Mathf.Max(0.02f, Median(ds));
            for (int i = 0; i < p.Length; i++)
            {
                var q = (p[i] - C) / R; float r = q.magnitude; if (r < 1e-4f) continue;
                float wgt = Mathf.Clamp01((1.85f - r) / 0.6f); if (wgt <= 0) continue;
                var d = q / r; var nq = shape == "helm" ? Helm(q, d, r) : shape == "head" ? (d.y < 0 ? Jaw(q, d, r, 1.1f) : q) : Hood(q, d, r);
                p[i] = C + Vector3.Lerp(q, nq, wgt) * R;
            }
        }
        static float G2(float x, float s) { return Mathf.Exp(-(x / s) * (x / s)); }
        // a great helm (after the user's references): a round dome with no point, a squarer body, a flat faceplate with
        // a ridge down its middle and a brow over the eyes, a long jaw drawn down to a chin and a flared guard behind
        static Vector3 Helm(Vector3 q, Vector3 d, float r)
        {
            // the old comb along the crown pressed down to a low ridge, so the top reads as a dome, not a cone
            if (d.y > 0.6f && r > 1.03f && r < 1.32f) { float rr = 1.03f + (r - 1.03f) * 0.3f; q = d * Mathf.Lerp(r, rr, Mathf.Clamp01((d.y - 0.6f) / 0.2f)); }
            if (d.y > 0) q.y *= 1 - 0.05f * d.y;   // the dome stays round, a touch lower
            float hx = Mathf.Abs(d.x), hz = Mathf.Abs(d.z), hm = Mathf.Max(hx, hz);
            float sq = hm > 1e-3f ? Mathf.Sqrt(hx * hx + hz * hz) / hm : 1; float k = Mathf.Lerp(1, sq, 0.32f * (1 - Mathf.Clamp01((d.y - 0.15f) / 0.5f)));
            q.x *= k; q.z *= k;
            if (d.z < 0)
            {
                float face = Mathf.Clamp01(-d.z * 1.4f - 0.2f) * (1 - Mathf.Clamp01((d.y - 0.45f) / 0.35f));
                const float zf = -0.9f; if (q.z < zf) q.z = Mathf.Lerp(q.z, zf, 0.75f * face);   // the flat faceplate
                q.z -= 0.07f * face * G2(d.x, 0.11f) * r;                                     // its middle ridge
                q.z -= 0.07f * face * G2(d.y - 0.3f, 0.09f) * r;                              // the brow over the eye slit
            }
            if (d.y < 0)
            {
                float t = Mathf.Clamp01(-d.y), front = Mathf.Clamp01(-d.z * 1.2f + 0.25f);
                q = Jaw(q, d, r);
                q.y -= 0.12f * t * front * G2(d.x, 0.35f) * r;          // the faceplate comes to a chin
                q.z -= 0.05f * t * front * G2(d.x, 0.35f) * r;
                if (d.z > 0) { q.z += 0.2f * t * t * d.z * r; q.y -= 0.04f * t * d.z * r; }   // the neck guard flares out
            }
            return q;
        }
        // the lower half of the head lengthened, most at the front (the jaw), narrowing and a little forward toward the chin
        static Vector3 Jaw(Vector3 q, Vector3 d, float r, float amt = 1)
        {
            float t = Mathf.Clamp01(-d.y), front = Mathf.Clamp01(-d.z * 1.2f + 0.25f), s = t * (0.6f + 0.4f * t) * amt;
            q.y -= (0.2f + 0.3f * front) * s * r;
            q.x *= 1 - 0.2f * s * front;
            q.z -= 0.07f * s * front * r;
            return q;
        }
        // a hood: soft and round over the crown, falling back a little, with a face opening as long as a face
        static Vector3 Hood(Vector3 q, Vector3 d, float r)
        {
            if (d.y > 0) { q.x *= 1 - 0.08f * d.y * d.y; q.z += 0.12f * d.y * d.y * Mathf.Clamp01(d.z + 0.6f) * r; }
            if (d.y < 0) q = Jaw(q, d, r);
            if (d.z < 0) q.z -= 0.05f * (-d.z) * Mathf.Clamp01(d.y * 2) * r;   // the brim over the face
            return q;
        }

        /// smooth shading within each material: a vertex takes the area-weighted normals of the faces meeting at its
        /// position that turn less than the crease angle from its own face (hard plates keep their edges)
        static Vector3[] Smooth(Vector3[] p, Vector3[] n0, int[] ix, byte[] surf)
        {
            int nv = p.Length, nt = ix.Length / 3; var fn = new Vector3[nt]; var fa = new float[nt]; var fs = new byte[nt];
            for (int t = 0; t < nt; t++) { var c = Vector3.Cross(p[ix[t * 3 + 1]] - p[ix[t * 3]], p[ix[t * 3 + 2]] - p[ix[t * 3]]); fa[t] = c.magnitude; fn[t] = fa[t] > 1e-12f ? c / fa[t] : Vector3.up; fs[t] = surf[ix[t * 3]]; }
            // faces at each welded position
            var at = new Dictionary<long, List<int>>(); var key = new long[nv];
            for (int i = 0; i < nv; i++) { long k = ((long)Mathf.RoundToInt(p[i].x * 4000) * 73856093L) ^ ((long)Mathf.RoundToInt(p[i].y * 4000) * 19349663L) ^ ((long)Mathf.RoundToInt(p[i].z * 4000) * 83492791L); key[i] = k; }
            var vf = new List<int>[nv]; for (int i = 0; i < nv; i++) vf[i] = new List<int>();
            for (int t = 0; t < nt; t++) for (int j = 0; j < 3; j++) vf[ix[t * 3 + j]].Add(t);
            for (int i = 0; i < nv; i++) { List<int> l; if (!at.TryGetValue(key[i], out l)) at[key[i]] = l = new List<int>(); l.AddRange(vf[i]); }
            var outN = new Vector3[nv];
            for (int i = 0; i < nv; i++)
            {
                // the vertex's own face direction: its faces, or its stored normal
                Vector3 own = Vector3.zero; foreach (var t in vf[i]) own += fn[t] * fa[t]; own = own.sqrMagnitude > 1e-12f ? own.normalized : n0[i];
                byte s = surf[i]; bool soft = s == CLOTH || s == SKIN || s == HAIR || s == LEATHER || s == FEATHER;
                float cosT = Mathf.Cos((soft ? 68 : s == GEM ? 25 : 42) * Mathf.Deg2Rad);
                Vector3 acc = Vector3.zero; var seen = new HashSet<int>();
                foreach (var t in at[key[i]]) { if (!seen.Add(t)) continue; if (Vector3.Dot(fn[t], own) < cosT) continue; if (fs[t] != s && Vector3.Dot(fn[t], own) < 0.95f) continue; acc += fn[t] * fa[t]; }
                var nn = acc.sqrMagnitude > 1e-12f ? acc.normalized : own;
                // keep the export's side: where the stored normal disagreed with the winding, follow the stored normal
                if (Vector3.Dot(nn, n0[i]) < 0) nn = -nn;
                outN[i] = nn;
            }
            return outN;
        }
    }
}
