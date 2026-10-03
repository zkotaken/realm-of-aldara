using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // Combat visuals: ground telegraphs, shockwave rings, slash flashes, bursts, glowing orbs (projectiles) and floating
    // combat text. Positions are given in the browser's pixel space.
    public class AldaraFx : MonoBehaviour
    {
        static AldaraFx inst;
        static Material discMat, orbMat;
        static Mesh disc, ring;
        class Timed { public GameObject go; public float t, dur; public Color c; public bool grow, fade; public float r0, r1; }
        readonly List<Timed> live = new List<Timed>();
        public class FloatText { public float x, y, life = 1; public string text; public Color col; }
        public static readonly List<FloatText> texts = new List<FloatText>();

        static AldaraFx I { get { if (!inst) { inst = new GameObject("AldaraFx").AddComponent<AldaraFx>(); } return inst; } }
        static void Init()
        {
            if (discMat) return;
            discMat = new Material(Shader.Find("Aldara/Water")); // vertex-coloured, transparent, double sided
            discMat.SetFloat("_Glint", 0); discMat.SetFloat("_Speed", 0); discMat.renderQueue = 3100;
            orbMat = new Material(Shader.Find("Aldara/VertexLit")); orbMat.SetFloat("_Emit", 1.4f);
            disc = MakeDisc(1f, 0f, 40); ring = MakeDisc(1f, 0.86f, 48);
        }
        static Mesh MakeDisc(float r, float inner, int n)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var ix = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2, x = Mathf.Cos(a) * 0.5f, z = Mathf.Sin(a) * 0.5f;
                v.Add(new Vector3(x, 0, z)); c.Add(Color.white); v.Add(new Vector3(x * inner, 0, z * inner)); c.Add(new Color(1, 1, 1, inner > 0 ? 1 : 0.35f));
                if (i < n) { int b = i * 2; ix.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 }); }
            }
            var m = new Mesh(); m.SetVertices(v); m.SetColors(c); m.SetTriangles(ix, 0); m.RecalculateBounds(); return m;
        }
        static GameObject Flat(Mesh mesh, Color col, string name)
        {
            Init(); var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(I.transform, false);
            var m = Object.Instantiate(mesh); var cs = m.colors; for (int i = 0; i < cs.Length; i++) cs[i] = new Color(col.r, col.g, col.b, col.a * cs[i].a); m.colors = cs;
            go.GetComponent<MeshFilter>().sharedMesh = m; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = discMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
        static void Place(GameObject go, float x, float y, float rPx, float lift)
        {
            go.transform.position = AldaraWorld.ToUnity(x, y) + Vector3.up * lift;
            float s = rPx * 2 / AldaraWorld.PX; go.transform.localScale = new Vector3(s, 1, s * AldaraView.TSP / AldaraView.TSP);
        }
        /// a telegraph that fills in over 'dur' seconds, then vanishes
        public static void Disc(float x, float y, float rPx, Color col, float dur)
        {
            var g = Flat(disc, col, "telegraph"); Place(g, x, y, rPx, 0.12f);
            I.live.Add(new Timed { go = g, dur = Mathf.Max(0.1f, dur), grow = true, r0 = rPx * 0.25f, r1 = rPx, c = col });
        }
        public static void Ring(float x, float y, float rPx, Color col, float dur)
        {
            var g = Flat(ring, col, "ring"); Place(g, x, y, rPx * 0.3f, 0.15f);
            I.live.Add(new Timed { go = g, dur = dur, grow = true, fade = true, r0 = rPx * 0.3f, r1 = rPx, c = col });
        }
        public static GameObject RingObj(Color col) { return Flat(ring, new Color(col.r, col.g, col.b, 0.7f), "wave"); }
        public static void Burst(float x, float y, float rPx, Color col) { Ring(x, y, Mathf.Max(24, rPx), new Color(col.r, col.g, col.b, 0.7f), 0.35f); }
        public static void Slash(float x, float y, float a, Color col, float rPx, bool round)
        {
            var g = Flat(ring, new Color(col.r, col.g, col.b, 0.45f), "slash"); Place(g, x, y, Mathf.Max(14, rPx * (round ? 1 : 0.35f)), 0.3f);
            I.live.Add(new Timed { go = g, dur = 0.2f, fade = true, r0 = rPx * 0.2f, r1 = rPx * (round ? 1 : 0.4f), grow = true, c = col });
        }
        public static GameObject Orb(Color col, float size)
        {
            Init(); var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(g.GetComponent<Collider>()); g.transform.SetParent(I.transform, false);
            g.transform.localScale = Vector3.one * size; var m = g.GetComponent<MeshFilter>().mesh; var cs = new Color[m.vertexCount]; for (int i = 0; i < cs.Length; i++) cs[i] = col; m.colors = cs;
            g.GetComponent<MeshRenderer>().sharedMaterial = orbMat; return g;
        }
        public static void Text(float x, float y, string s, Color c) { texts.Add(new FloatText { x = x, y = y, text = s, col = c }); if (texts.Count > 80) texts.RemoveAt(0); }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var t = live[i]; t.t += dt; float u = Mathf.Clamp01(t.t / t.dur);
                if (!t.go) { live.RemoveAt(i); continue; }
                if (t.grow) { float r = Mathf.Lerp(t.r0, t.r1, u) * 2 / AldaraWorld.PX; t.go.transform.localScale = new Vector3(r, 1, r); }
                if (u >= 1) { Destroy(t.go); live.RemoveAt(i); }
            }
            for (int i = texts.Count - 1; i >= 0; i--) { var f = texts[i]; f.y -= 30 * dt; f.life -= dt * 1.1f; if (f.life <= 0) texts.RemoveAt(i); }
        }
    }
}
