using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // The browser draws a tree, building or set piece that stands in front of you at 45% (the 'hide' test in
    // drawWorldObjects and dunDrawProps): when you are inside the middle 70% of its sprite, below the top tenth, and its
    // foot is no more than 44 px south of you. Here the same test runs on each model's screen box, and a hidden model is
    // drawn with a depth pre-pass and then 45% colour, so it reads as one flat see-through picture like the sprite.
    public class AldaraOccluders : MonoBehaviour
    {
        public static AldaraOccluders I;
        public class Occ { public Renderer[] rs; public Material[][] orig; public float x, y, gy; public Bounds b; public bool hid; }
        const float CELL = 512;
        readonly Dictionary<long, List<Occ>> world = new Dictionary<long, List<Occ>>();
        readonly List<Occ> dun = new List<Occ>();
        readonly List<Occ> hidden = new List<Occ>();
        bool built;
        static Material prime; static readonly Dictionary<Material, Material> fades = new Dictionary<Material, Material>();

        void Awake() { I = this; }
        public static Material Prime { get { if (!prime) { prime = new Material(Shader.Find("Aldara/DepthPrime")) { renderQueue = 2990 }; } return prime; } }
        public static Material FadeOf(Material m)
        {
            if (m == null) return null; Material f; if (fades.TryGetValue(m, out f) && f) return f;
            f = new Material(Shader.Find("Aldara/VertexLitFade")) { renderQueue = 2991 };
            foreach (var p in new[] { "_Tint", "_Wind", "_Emit" }) if (m.HasProperty(p)) { if (p == "_Tint") f.SetColor(p, m.GetColor(p)); else f.SetFloat(p, m.GetFloat(p)); }
            f.SetFloat("_Alpha", 0.45f); f.enableInstancing = true; fades[m] = f; return f;
        }
        static long Key(int cx, int cy) { return ((long)cy << 20) ^ (cx & 0xfffff); }
        static Occ Make(Transform anchor)
        {
            var rs = anchor.GetComponentsInChildren<MeshRenderer>(true); if (rs.Length == 0) return null;
            var o = new Occ { rs = rs, orig = new Material[rs.Length][] }; var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            for (int i = 0; i < rs.Length; i++) o.orig[i] = rs[i].sharedMaterials;
            o.b = b; var px = AldaraWorld.ToPx(anchor.position); o.x = px.x; o.y = px.y; o.gy = anchor.position.y; return o;
        }
        void BuildWorld()
        {
            built = true; foreach (var pf in FindObjectsOfType<AldaraPropField>()) if (pf.name == "Trees") pf.occlude = true;
            var root = GameObject.Find("Objects"); if (!root) return;
            foreach (Transform grp in root.transform) foreach (Transform a in grp)
                {
                    var o = Make(a); if (o == null) continue; int cx = Mathf.FloorToInt(o.x / CELL), cy = Mathf.FloorToInt(o.y / CELL);
                    List<Occ> l; if (!world.TryGetValue(Key(cx, cy), out l)) world[Key(cx, cy)] = l = new List<Occ>(); l.Add(o);
                }
        }
        /// the set pieces of the dungeon now shown (null when leaving)
        public void SetDungeon(Transform props)
        {
            foreach (var o in dun) Show(o, false); dun.Clear(); hidden.Clear();
            if (props != null) foreach (Transform a in props) { var o = Make(a); if (o != null) dun.Add(o); }
        }
        /// the browser's test, on the model's screen box (canvas y = world y - height)
        public static bool Hide(float px, float py, float ox, float oy, float L, float W, float T, float H)
        {
            return oy > py && px > L + W * 0.15f && px < L + W * 0.85f && py > T + H * 0.1f && py - 50 < oy - 6;
        }
        static bool Test(Occ o, float px, float py)
        {
            float L = (o.b.min.x - AldaraWorld.OX) * AldaraWorld.PX, W = o.b.size.x * AldaraWorld.PX;
            float T = AldaraWorld.YTOP - o.b.max.z * AldaraWorld.PX - (o.b.max.y - o.gy) * AldaraWorld.PX, B = AldaraWorld.YTOP - o.b.min.z * AldaraWorld.PX - (o.b.min.y - o.gy) * AldaraWorld.PX;
            return Hide(px, py, o.x, o.y, L, W, T, B - T);
        }
        static void Show(Occ o, bool hide)
        {
            if (o.hid == hide) return; o.hid = hide;
            for (int i = 0; i < o.rs.Length; i++)
            {
                var r = o.rs[i]; if (!r) continue;
                if (!hide) { r.sharedMaterials = o.orig[i]; continue; }
                var m = o.orig[i].Length > 0 ? o.orig[i][0] : null; r.sharedMaterials = new[] { Prime, FadeOf(m) };
            }
        }
        void LateUpdate()
        {
            var P = AldaraPlayer.I; if (!P || !AldaraSave.Ready) return; if (!built) BuildWorld();
            float px = P.x, py = P.y;
            var now = new List<Occ>();
            if (AldaraWorld.Dun) { foreach (var o in dun) if (Mathf.Abs(o.x - px) < 600 && o.y > py - 40 && o.y < py + 400 && Test(o, px, py)) now.Add(o); }
            else
            {
                int cx = Mathf.FloorToInt(px / CELL), cy = Mathf.FloorToInt(py / CELL);
                for (int j = cy - 1; j <= cy + 1; j++) for (int i = cx - 1; i <= cx + 1; i++)
                    {
                        List<Occ> l; if (!world.TryGetValue(Key(i, j), out l)) continue;
                        foreach (var o in l) if (o.y > py && o.y < py + 600 && Mathf.Abs(o.x - px) < 700 && Test(o, px, py)) now.Add(o);
                    }
            }
            foreach (var o in hidden) if (!now.Contains(o)) Show(o, false);
            foreach (var o in now) Show(o, true);
            hidden.Clear(); hidden.AddRange(now);
        }
    }
}
