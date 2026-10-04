using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // A canvas for the browser's 2D drawing, rebuilt every frame: shapes in world px (x east, y south) at a height h px,
    // with the browser's screen mapping (a point at height h is drawn h px higher). Ground layers sit on the floor and
    // hide behind anything standing in front of them; the 'air' layers draw over everything, as dunDrawAir does.
    public class AldaraSketch
    {
        readonly List<Vector3> v = new List<Vector3>(); readonly List<Color> c = new List<Color>(); readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> ix = new List<int>();
        readonly Mesh mesh; readonly Material mat; public float h0;
        public static Shader Sh { get { return Shader.Find("Aldara/DunFx"); } }
        public static Material Mat(UnityEngine.Rendering.BlendMode src, UnityEngine.Rendering.BlendMode dst, bool always, int queue, Texture tex = null)
        {
            var m = new Material(Sh); m.SetFloat("_Src", (float)src); m.SetFloat("_Dst", (float)dst); m.SetFloat("_ZT", always ? 8 : 4); m.renderQueue = queue; if (tex) m.mainTexture = tex; return m;
        }
        /// alpha 'normal', 'add' (lighter) or 'mul' (multiply)
        public AldaraSketch(string blend, bool air, int queue, float h0 = 1)
        {
            this.h0 = h0; mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.MarkDynamic();
            var B = UnityEngine.Rendering.BlendMode.SrcAlpha; var D = UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
            if (blend == "add") { D = UnityEngine.Rendering.BlendMode.One; }
            else if (blend == "mul") { B = UnityEngine.Rendering.BlendMode.DstColor; D = UnityEngine.Rendering.BlendMode.Zero; }
            else if (blend == "screen") { B = UnityEngine.Rendering.BlendMode.OneMinusDstColor; D = UnityEngine.Rendering.BlendMode.One; }   // colours premultiplied by the caller
            mat = Mat(B, D, air, queue);
        }
        public void Clear() { v.Clear(); c.Clear(); uv.Clear(); ix.Clear(); }
        public static Vector3 W(float x, float y, float h) { return new Vector3(x / AldaraWorld.PX + AldaraWorld.OX, h / AldaraWorld.PX, (AldaraWorld.YTOP - y) / AldaraWorld.PX); }
        int Add(float x, float y, float h, Color col) { v.Add(W(x, y, h)); c.Add(col); uv.Add(Vector2.zero); return v.Count - 1; }
        public void Draw(Camera cam)
        {
            if (v.Count == 0) return; mesh.Clear(); mesh.SetVertices(v); mesh.SetColors(c); mesh.SetUVs(0, uv); mesh.SetTriangles(ix, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, cam);
        }
        // ---------- shapes on the floor (offsets in the ground plane) ----------
        public void Tri(Vector2 a, Vector2 b, Vector2 d, Color col, float h = -1) { if (h < 0) h = h0; int i = Add(a.x, a.y, h, col); Add(b.x, b.y, h, col); Add(d.x, d.y, h, col); ix.Add(i); ix.Add(i + 1); ix.Add(i + 2); }
        public void Poly(IList<Vector2> p, Color col, float h = -1)
        {
            if (h < 0) h = h0; if (p.Count < 3) return; int b = v.Count; foreach (var q in p) Add(q.x, q.y, h, col);
            for (int k = 1; k < p.Count - 1; k++) { ix.Add(b); ix.Add(b + k); ix.Add(b + k + 1); }
        }
        public void Rect(float x, float y, float w, float hh, Color col, float h = -1) { Poly(new[] { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + hh), new Vector2(x, y + hh) }, col, h); }
        /// a rectangle in a rotated frame (centre cx, cy; local x along ang)
        public void RotRect(float cx, float cy, float ang, float x0, float y0, float x1, float y1, Color col, float h = -1)
        {
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang); System.Func<float, float, Vector2> R = (x, y) => new Vector2(cx + x * ca - y * sa, cy + x * sa + y * ca);
            Poly(new[] { R(x0, y0), R(x1, y0), R(x1, y1), R(x0, y1) }, col, h);
        }
        public void Ellipse(float x, float y, float rx, float ry, Color col, float h = -1, float rot = 0, int n = 32)
        {
            if (h < 0) h = h0; int b = Add(x, y, h, col); float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            for (int k = 0; k <= n; k++) { float a = k / (float)n * Mathf.PI * 2, ex = Mathf.Cos(a) * rx, ey = Mathf.Sin(a) * ry; Add(x + ex * cr - ey * sr, y + ex * sr + ey * cr, h, col); if (k > 0) { ix.Add(b); ix.Add(b + k); ix.Add(b + k + 1); } }
        }
        /// a radial gradient (createRadialGradient): colour stops from the centre out
        public void Glow(float x, float y, float rx, float ry, float[] t, Color[] cs, float h = -1, int n = 28)
        {
            if (h < 0) h = h0; int prev = -1;
            for (int s = 0; s < t.Length; s++)
            {
                int start = v.Count;
                if (t[s] <= 0) { Add(x, y, h, cs[s]); }
                else for (int k = 0; k <= n; k++) { float a = k / (float)n * Mathf.PI * 2; Add(x + Mathf.Cos(a) * rx * t[s], y + Mathf.Sin(a) * ry * t[s], h, cs[s]); }
                if (prev >= 0)
                {
                    bool fan = t[s - 1] <= 0;
                    for (int k = 0; k < n; k++)
                    {
                        if (fan) { ix.Add(prev); ix.Add(start + k); ix.Add(start + k + 1); }
                        else { int a0 = prev + k, a1 = prev + k + 1, b0 = start + k, b1 = start + k + 1; ix.Add(a0); ix.Add(b0); ix.Add(b1); ix.Add(a0); ix.Add(b1); ix.Add(a1); }
                    }
                }
                prev = start;
            }
        }
        public void Glow(float x, float y, float r, Color col, float a0, float h = -1) { Glow(x, y, r, r, new[] { 0f, 1f }, new[] { new Color(col.r, col.g, col.b, a0), new Color(col.r, col.g, col.b, 0) }, h); }
        public void Line(float ax, float ay, float bx, float by, float w, Color col, float h = -1)
        {
            if (h < 0) h = h0; float dx = bx - ax, dy = by - ay, L = Mathf.Sqrt(dx * dx + dy * dy); if (L < 1e-4f) return; float nx = -dy / L * w / 2, ny = dx / L * w / 2;
            Poly(new[] { new Vector2(ax + nx, ay + ny), new Vector2(bx + nx, by + ny), new Vector2(bx - nx, by - ny), new Vector2(ax - nx, ay - ny) }, col, h);
        }
        public void Polyline(IList<Vector2> p, float w, Color col, bool closed = false, float h = -1) { for (int k = 0; k < p.Count - 1; k++) Line(p[k].x, p[k].y, p[k + 1].x, p[k + 1].y, w, col, h); if (closed && p.Count > 2) Line(p[p.Count - 1].x, p[p.Count - 1].y, p[0].x, p[0].y, w, col, h); }
        public void Arc(float x, float y, float rx, float ry, float a0, float a1, float w, Color col, float h = -1, float rot = 0)
        {
            int n = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 0.15f)); var p = new List<Vector2>(); float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            for (int k = 0; k <= n; k++) { float a = a0 + (a1 - a0) * k / n, ex = Mathf.Cos(a) * rx, ey = Mathf.Sin(a) * ry; p.Add(new Vector2(x + ex * cr - ey * sr, y + ex * sr + ey * cr)); }
            Polyline(p, w, col, false, h);
        }
        // ---------- shapes standing up (screen offsets go into the height, so they keep their depth) ----------
        public void VLine(float ax, float ay, float ah, float bx, float by, float bh, float w, Color col)
        {
            float sx = bx - ax, sy = (by - bh) - (ay - ah), L = Mathf.Sqrt(sx * sx + sy * sy); if (L < 1e-4f) return; float nx = -sy / L * w / 2, ny = sx / L * w / 2;
            int b = v.Count; Add(ax + nx, ay, ah - ny, col); Add(bx + nx, by, bh - ny, col); Add(bx - nx, by, bh + ny, col); Add(ax - nx, ay, ah + ny, col);
            ix.Add(b); ix.Add(b + 1); ix.Add(b + 2); ix.Add(b); ix.Add(b + 2); ix.Add(b + 3);
        }
        public void VQuad(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color ca, Color cb)
        {   // corners (x, y, h): a-b along the ground, d-e above them
            int i = v.Count; Add(a.x, a.y, a.z, ca); Add(b.x, b.y, b.z, ca); Add(d.x, d.y, d.z, cb); Add(e.x, e.y, e.z, cb);
            ix.Add(i); ix.Add(i + 1); ix.Add(i + 2); ix.Add(i); ix.Add(i + 2); ix.Add(i + 3);
        }
        /// a filled shape in the screen plane at a point standing at height h (like a canvas path drawn there)
        public void ScreenPoly(float x, float y, float h, IList<Vector2> p, Color col)
        {
            if (p.Count < 3) return; int b = v.Count; foreach (var q in p) Add(x + q.x, y, h - q.y, col);
            for (int k = 1; k < p.Count - 1; k++) { ix.Add(b); ix.Add(b + k); ix.Add(b + k + 1); }
        }
        public void ScreenGlow(float x, float y, float h, float r, Color col, float a0, float a1 = 0, int n = 20)
        {
            int b = Add(x, y, h, new Color(col.r, col.g, col.b, a0));
            for (int k = 0; k <= n; k++) { float a = k / (float)n * Mathf.PI * 2; Add(x + Mathf.Cos(a) * r, y, h - Mathf.Sin(a) * r, new Color(col.r, col.g, col.b, a1)); if (k > 0) { ix.Add(b); ix.Add(b + k); ix.Add(b + k + 1); } }
        }
    }
}
