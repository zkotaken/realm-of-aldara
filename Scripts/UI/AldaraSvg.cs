using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aldara
{
    // A small SVG path reader for the browser's vector icons (guild emblems): M L H V C S Q A Z, absolute and relative,
    // curves and arcs flattened to points, drawn with UI Toolkit's Painter2D as a fill and a round-capped stroke.
    public static class AldaraSvg
    {
        public class Sub { public List<Vector2> p = new List<Vector2>(); public bool closed; }
        static readonly Dictionary<string, List<Sub>> cache = new Dictionary<string, List<Sub>>();
        public static List<Sub> Parse(string d)
        {
            List<Sub> o; if (cache.TryGetValue(d, out o)) return o; o = new List<Sub>(); cache[d] = o;
            int i = 0; char cmd = ' '; Vector2 cur = Vector2.zero, start = Vector2.zero, lastC = Vector2.zero; Sub s = null; char prev = ' ';
            System.Func<float> num = () =>
            {
                while (i < d.Length && (d[i] == ' ' || d[i] == ',' || d[i] == '\n' || d[i] == '\t')) i++;
                int b = i; if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++; bool dot = false;
                while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot))) { if (d[i] == '.') dot = true; i++; }
                if (i < d.Length && (d[i] == 'e' || d[i] == 'E')) { i++; if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++; while (i < d.Length && char.IsDigit(d[i])) i++; }
                return float.Parse(d.Substring(b, i - b), CultureInfo.InvariantCulture);
            };
            System.Func<bool> more = () => { while (i < d.Length && (d[i] == ' ' || d[i] == ',')) i++; return i < d.Length && (char.IsDigit(d[i]) || d[i] == '-' || d[i] == '.' || d[i] == '+'); };
            System.Action<Vector2> lineTo = v => { if (s == null) { s = new Sub(); o.Add(s); s.p.Add(cur); } s.p.Add(v); cur = v; };
            while (i < d.Length)
            {
                int i0 = i; char c = d[i];
                if (char.IsLetter(c) && c != 'e' && c != 'E') { cmd = c; i++; }
                else if (c == ' ' || c == ',') { i++; continue; }
                bool rel = char.IsLower(cmd); char C = char.ToUpper(cmd);
                switch (C)
                {
                    case 'M': { var v = new Vector2(num(), num()); if (rel) v += cur; s = new Sub(); o.Add(s); s.p.Add(v); cur = start = v; cmd = rel ? 'l' : 'L'; break; }
                    case 'L': { var v = new Vector2(num(), num()); if (rel) v += cur; lineTo(v); break; }
                    case 'H': { float x = num(); lineTo(new Vector2(rel ? cur.x + x : x, cur.y)); break; }
                    case 'V': { float y = num(); lineTo(new Vector2(cur.x, rel ? cur.y + y : y)); break; }
                    case 'C': case 'S':
                        {
                            Vector2 c1; if (C == 'C') { c1 = new Vector2(num(), num()); if (rel) c1 += cur; } else c1 = (prev == 'C' || prev == 'S') ? cur * 2 - lastC : cur;
                            var c2 = new Vector2(num(), num()); var e = new Vector2(num(), num()); if (rel) { c2 += cur; e += cur; }
                            var p0 = cur; for (int k = 1; k <= 10; k++) { float t = k / 10f, u = 1 - t; lineTo(u * u * u * p0 + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * e); }
                            lastC = c2; break;
                        }
                    case 'Q':
                        {
                            var c1 = new Vector2(num(), num()); var e = new Vector2(num(), num()); if (rel) { c1 += cur; e += cur; }
                            var p0 = cur; for (int k = 1; k <= 8; k++) { float t = k / 8f, u = 1 - t; lineTo(u * u * p0 + 2 * u * t * c1 + t * t * e); }
                            lastC = c1; break;
                        }
                    case 'A':
                        {
                            float rx = num(), ry = num(), phi = num() * Mathf.Deg2Rad; int fa = (int)num(), fs = (int)num(); var e = new Vector2(num(), num()); if (rel) e += cur;
                            Arc(cur, e, rx, ry, phi, fa != 0, fs != 0, lineTo); break;
                        }
                    case 'Z': if (s != null) { s.closed = true; cur = start; s = null; } break;
                    default: i++; break;
                }
                prev = C; more();
                if (i == i0) i++;
            }
            return o;
        }
        static void Arc(Vector2 p0, Vector2 p1, float rx, float ry, float phi, bool fa, bool fs, System.Action<Vector2> lineTo)
        {
            if (rx == 0 || ry == 0) { lineTo(p1); return; }
            float c = Mathf.Cos(phi), s = Mathf.Sin(phi); var d = (p0 - p1) / 2; float x1 = c * d.x + s * d.y, y1 = -s * d.x + c * d.y;
            rx = Mathf.Abs(rx); ry = Mathf.Abs(ry); float l = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry); if (l > 1) { rx *= Mathf.Sqrt(l); ry *= Mathf.Sqrt(l); }
            float num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1, den = rx * rx * y1 * y1 + ry * ry * x1 * x1; float k = Mathf.Sqrt(Mathf.Max(0, num / den)) * (fa == fs ? -1 : 1);
            float cx1 = k * rx * y1 / ry, cy1 = -k * ry * x1 / rx; var m = (p0 + p1) / 2; float cx = c * cx1 - s * cy1 + m.x, cy = s * cx1 + c * cy1 + m.y;
            float a1 = Mathf.Atan2((y1 - cy1) / ry, (x1 - cx1) / rx), a2 = Mathf.Atan2((-y1 - cy1) / ry, (-x1 - cx1) / rx), da = a2 - a1;
            if (fs && da < 0) da += Mathf.PI * 2; else if (!fs && da > 0) da -= Mathf.PI * 2;
            int n = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(da) / 0.2f));
            for (int q = 1; q <= n; q++) { float a = a1 + da * q / n, ex = rx * Mathf.Cos(a), ey = ry * Mathf.Sin(a); lineTo(new Vector2(c * ex - s * ey + cx, s * ex + c * ey + cy)); }
        }
        /// draw a path in a box: viewBox size vb, at offset o with scale k
        public static void Draw(Painter2D g, string d, float vb, Vector2 o, float k, Color? fill, Color? stroke, float sw)
        {
            foreach (var sp in Parse(d))
            {
                if (sp.p.Count < 2) continue; g.BeginPath(); g.MoveTo(o + sp.p[0] * k); for (int q = 1; q < sp.p.Count; q++) g.LineTo(o + sp.p[q] * k); if (sp.closed) g.ClosePath();
                if (fill.HasValue && sp.p.Count > 2) { g.fillColor = fill.Value; g.Fill(); }
                if (stroke.HasValue) { g.strokeColor = stroke.Value; g.lineWidth = sw * k; g.lineCap = LineCap.Round; g.lineJoin = LineJoin.Round; g.Stroke(); }
            }
        }
    }
}
