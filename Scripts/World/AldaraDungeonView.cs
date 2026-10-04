using System.Collections.Generic;
using UnityEngine;
using A = Aldara.AldaraDungeon;

namespace Aldara
{
    // How a dungeon looks, drawn the browser's way (dunDrawGround / dunDrawProps / dunDrawAir / drawGate / dunLights and
    // the light map of drawPostFX): the baked floor tiles and the set pieces come from the dungeon's prefab; the rest is
    // drawn every frame like the canvas: wall torches, the exit portal, runes, traps, gates, glows, and the light map
    // (the theme's dim ambient with every torch, brazier, lava glow and trap light added) multiplied over the view.
    public class AldaraDungeonView : MonoBehaviour
    {
        public static AldaraDungeonView I;
        GameObject inst; AldaraSketch gA, gAdd, aA, aAdd;
        float shakeAmp, shakeT, shakeMax; readonly List<KeyValuePair<Vector3, Color>> propGlows = new List<KeyValuePair<Vector3, Color>>();
        void Awake() { I = this; }
        public void Shake(float amp, float dur) { AldaraVfx.Shake(amp, dur); }
        public Vector2 ShakeOffset { get { return AldaraVfx.ShakeOffset; } }

        public void Enter(A.Map M)
        {
            Leave();
            var pf = Resources.Load<GameObject>("DunPrefabs/Dun_" + M.id);
            if (pf) { inst = Instantiate(pf); inst.transform.position = new Vector3(AldaraWorld.DOX, 0, 0); }
            if (AldaraOccluders.I) AldaraOccluders.I.SetDungeon(inst ? inst.transform.Find("Props") : null);
            gA = new AldaraSketch("normal", false, 3001); gAdd = new AldaraSketch("add", false, 3002); aA = new AldaraSketch("normal", true, 3110); aAdd = new AldaraSketch("add", true, 3111);
            // the glows painted onto set pieces (glowDot in their sprites)
            propGlows.Clear();
            foreach (var p in M.props)
            {
                if (p.m == null || M.models[p.m] == null) continue; var md = M.models[p.m]; float unit = (float)md["unit"];
                foreach (var g in md["glows"]) { var col = AldaraRules.Hex((string)g[3]); col.a = (float)g[4]; propGlows.Add(new KeyValuePair<Vector3, Color>(new Vector3(p.x + (float)g[0] * unit, p.y - (float)g[1] * unit, (float)g[2] * unit), col)); }
            }
            if (Camera.main) { camFlags = Camera.main.clearFlags; Camera.main.clearFlags = CameraClearFlags.SolidColor; camBg = Camera.main.backgroundColor; Camera.main.backgroundColor = M.baseKind == A.KW ? new Color(M.theme["top"][0].ToObject<float>() / 255f, M.theme["top"][1].ToObject<float>() / 255f, M.theme["top"][2].ToObject<float>() / 255f) : new Color(5 / 255f, 3 / 255f, 10 / 255f); }
        }
        Color camBg; CameraClearFlags camFlags = CameraClearFlags.Skybox;
        public void Leave()
        {
            if (AldaraOccluders.I) AldaraOccluders.I.SetDungeon(null);
            if (inst) Destroy(inst); inst = null;
            if (gA != null && Camera.main) { Camera.main.clearFlags = camFlags; Camera.main.backgroundColor = camBg; }
            gA = gAdd = aA = aAdd = null;
        }

        void LateUpdate()
        {
            var D = A.DM; if (D == null || gA == null) return; var cam = Camera.main; if (!cam) return; var P = AldaraPlayer.I;
            if (shakeT > 0) shakeT -= Time.deltaTime;
            float t = D.t; gA.Clear(); gAdd.Clear(); aA.Clear(); aAdd.Clear();
            float vh = cam.orthographicSize * 2 * AldaraWorld.PX, vw = vh * cam.aspect, x0 = P.x - vw / 2, y0 = P.y - vh / 2;
            System.Func<float, float, bool> vis = (x, y) => x > x0 - 200 && x < x0 + vw + 200 && y > y0 - 200 && y < y0 + vh + 260;
            // the exit portal where you came in
            if (vis(D.start.x, D.start.y)) { var s = D.start; float k = 0.7f + 0.3f * Mathf.Sin(t * 2.5f); gAdd.Glow(s.x - 40, s.y, 70, 44, new[] { 0f, 1f }, new[] { new Color(160 / 255f, 200 / 255f, 1, 0.35f * k), new Color(80 / 255f, 120 / 255f, 1, 0) }); gAdd.Arc(s.x - 40, s.y, 46, 28, 0, 5, 2, new Color(190 / 255f, 220 / 255f, 1, 0.6f * k), -1, t); }
            // lava bubbles
            if (D.lava) foreach (var L in D.lights)
                {
                    if (L.col != "#ff6a1a" || !vis(L.x, L.y)) continue; float ph = (t * 0.7f + H(Mathf.Round(L.x), Mathf.Round(L.y)) * 5) % 1;
                    float bx = L.x + (H(Mathf.Round(L.y), Mathf.Floor(t * 0.7f + L.x)) - 0.5f) * 120, by = L.y + (H(Mathf.Floor(t * 0.7f + L.y), Mathf.Round(L.x)) - 0.5f) * 90;
                    if (A.Kind(bx, by) != A.KP) continue; float r = 4 + ph * 16; gA.Arc(bx, by, r, r * 0.6f, 0, 6.3f, 2, new Color(1, 230 / 255f, 140 / 255f, 0.9f * (1 - ph)));
                    if (ph < 0.4f) gA.Ellipse(bx, by - ph * 10, 5 * (1 - ph * 2), 5 * (1 - ph * 2), new Color(1, 200 / 255f, 90 / 255f, 0.6f * (1 - ph)));
                }
            // wall torches
            foreach (var tc in D.torches)
            {
                if (!vis(tc.x, tc.y)) continue; var dark = Hx("#1c1814"); gA.Rect(tc.x - 2, tc.y, 4, 14, dark); gA.Rect(tc.x - 6, tc.y - 2, 12, 4, dark);
                float f = 0.8f + 0.2f * Mathf.Sin(t * 11 + tc.ph) * Mathf.Sin(t * 7.3f + tc.ph * 2); var tcol = D.torchCol;
                gAdd.Glow(tc.x, tc.y - 10, 34 * f, tcol, 0.55f);
                var fl = new List<Vector2>(); for (int k = 0; k <= 10; k++) { float u = k / 10f; fl.Add(Quad(new Vector2(tc.x - 5, tc.y - 2), new Vector2(tc.x - 6, tc.y - 12 * f), new Vector2(tc.x + Mathf.Sin(t * 9 + tc.ph) * 2, tc.y - 22 * f), u)); }
                for (int k = 1; k <= 10; k++) { float u = k / 10f; fl.Add(Quad(new Vector2(tc.x + Mathf.Sin(t * 9 + tc.ph) * 2, tc.y - 22 * f), new Vector2(tc.x + 6, tc.y - 12 * f), new Vector2(tc.x + 5, tc.y - 2), u)); }
                gAdd.Poly(fl, new Color(tcol.r, tcol.g, tcol.b, 0.9f)); gAdd.Ellipse(tc.x, tc.y - 6, 2.5f, 5 * f, new Color(1, 250 / 255f, 220 / 255f, 0.9f));
            }
            // runes
            foreach (var r in D.runes) foreach (var e in new[] { r.a, r.b })
                {
                    if (!vis(e.x, e.y)) continue; float k = 0.7f + 0.3f * Mathf.Sin(t * 3 + e.x);
                    gAdd.Glow(e.x, e.y, r.r * 1.3f, r.r * 1.3f * 0.62f, new[] { 0f, 1f }, new[] { new Color(200 / 255f, 140 / 255f, 1, 0.4f * k), new Color(120 / 255f, 60 / 255f, 1, 0) });
                    gAdd.Arc(e.x, e.y, r.r, r.r * 0.62f, 0, 6.3f, 2.5f, new Color(220 / 255f, 180 / 255f, 1, 0.85f)); gAdd.Arc(e.x, e.y, r.r * 0.66f, r.r * 0.66f * 0.62f, 0, 6.3f, 1.5f, new Color(220 / 255f, 180 / 255f, 1, 0.85f));
                    for (int q = 0; q < 8; q++) { float a = t * 0.8f + (q + 1) * Mathf.PI / 4, ca = Mathf.Cos(a), sa = Mathf.Sin(a); var pts = new List<Vector2>(); foreach (var c in new[] { new Vector2(r.r * 0.7f, -2), new Vector2(r.r * 0.94f, -2), new Vector2(r.r * 0.94f, 2), new Vector2(r.r * 0.7f, 2) }) pts.Add(new Vector2(e.x + c.x * ca - c.y * sa, e.y + (c.x * sa + c.y * ca) * 0.62f)); gAdd.Poly(pts, new Color(230 / 255f, 200 / 255f, 1, 0.9f)); }
                }
            // the gold ring under a boss, the frost ring under a slowed monster
            foreach (var m in AldaraMonsters.I.all)
            {
                if (m.dead || !vis(m.x, m.y)) continue;
                if (m.boss) gA.Arc(m.x, m.y + m.r * 0.95f, m.r * 1.2f, m.r * 0.35f, 0, 6.3f, 2.5f, Hx("#e0b64b", 0.8f));
                if (m.slowT > 0) gA.Arc(m.x, m.y + m.r * 0.95f, m.r * 0.9f, m.r * 0.3f, 0, 6.3f, 2, Hx("#8fdfff"));
            }
            DrawTrapsGround(D, t, vis); DrawGates(D, t); DrawAir(D, t, vis); DrawDeep(D, t, vis, x0, y0, vw, vh);
            foreach (var g in propGlows) if (vis(g.Key.x, g.Key.y)) aAdd.Glow(g.Key.x, g.Key.y, g.Key.z, g.Value, g.Value.a);
            gA.Draw(cam); gAdd.Draw(cam); aA.Draw(cam); aAdd.Draw(cam);
        }
        static Vector2 Quad(Vector2 a, Vector2 c, Vector2 b, float u) { float m = 1 - u; return m * m * a + 2 * m * u * c + u * u * b; }
        static Color Hx(string h) { return AldaraRules.Hex(h); }
        static Color Hx(string h, float a) { var c = AldaraRules.Hex(h); c.a = a; return c; }
        static float H(float i, float j) { unchecked { int h = (int)i * 374761393 + (int)j * 668265263; h = (h ^ (int)((uint)h >> 13)) * 1274126177; return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296f; } }

        // ---------- traps on the floor ----------
        void DrawTrapsGround(A.Map D, float t, System.Func<float, float, bool> vis)
        {
            foreach (var tr in D.traps)
            {
                switch (tr.type)
                {
                    case "phase": DrawPhase(tr, t); break;
                    case "stone":
                        {
                            if (!vis(tr.x, tr.y)) break; float h = tr.st <= 1 ? 1 : tr.st == 2 ? 1 - (tr.ph - 0.72f) / 0.08f : tr.st == 3 ? 0 : (tr.ph - 0.94f) / 0.06f, dx = tr.st == 1 ? Mathf.Sin(t * 40) * 2 : 0;
                            if (h > 0.02f)
                            {
                                float r = tr.r * (0.85f + 0.15f * h); gA.Ellipse(tr.x + dx, tr.y + 8, r, r * 0.7f, new Color(0, 0, 0, 0.35f)); gA.Ellipse(tr.x + dx, tr.y + 4 * h, r, r * 0.8f, Hx("#2a1e18"));
                                gA.Glow(tr.x + dx, tr.y - 6 * h, r, r * 0.8f, new[] { 0f, 0.7f, 1f }, new[] { Hx("#7a6a5e"), Hx("#4a3c32"), Hx("#2a2019") });
                                gA.Arc(tr.x + dx, tr.y - 6 * h + 2, r - 1, r * 0.8f - 1, 0.2f, Mathf.PI - 0.2f, 2, new Color(1, 140 / 255f, 60 / 255f, 0.35f)); gA.Arc(tr.x + dx, tr.y - 6 * h, r - 3, r * 0.8f - 3, Mathf.PI + 0.3f, 2 * Mathf.PI - 0.3f, 2, new Color(160 / 255f, 140 / 255f, 120 / 255f, 0.5f));
                                if (tr.st >= 1) { var R = new Rng((int)Mathf.Round(tr.x)); var cc = new Color(1, 120 / 255f, 30 / 255f, tr.st == 1 ? 0.5f + 0.4f * Mathf.Sin(t * 20) : 0.8f); for (int q = 0; q < 5; q++) { float a = R.N() * 6.28f, x = tr.x + dx, y = tr.y - 6 * h; var pl = new List<Vector2> { new Vector2(x, y) }; for (int s = 0; s < 3; s++) { a += R.N() - 0.5f; x += Mathf.Cos(a) * r * 0.3f; y += Mathf.Sin(a) * r * 0.25f; pl.Add(new Vector2(x, y)); } gAdd.Polyline(pl, 2, cc); } }
                            }
                            else for (int q = 0; q < 4; q++) { float a = t * 2 + q * 1.6f, rr = tr.r * 0.5f * (0.5f + 0.5f * Mathf.Sin(t * 3 + q)); float s = 4 + 2 * Mathf.Sin(t * 5 + q); gAdd.Ellipse(tr.x + Mathf.Cos(a) * rr, tr.y + Mathf.Sin(a) * rr * 0.7f, s, s, new Color(1, 220 / 255f, 120 / 255f, 0.4f)); }
                            break;
                        }
                    case "thin":
                        {
                            if (!vis(tr.x, tr.y)) break;
                            if (tr.broken > 0)
                            {
                                float k = Mathf.Min(1, tr.broken / 0.5f), re = tr.broken < 1.2f ? 1 - tr.broken / 1.2f : 0, al = k * (1 - re * 0.7f);
                                gA.Glow(tr.x, tr.y, tr.r, tr.r * 0.85f, new[] { 0f, 0.8f, 1f }, new[] { Hx("#08243c", al), Hx("#15507a", al), Hx("#a8d8f0", al) });
                                var R = new Rng((int)Mathf.Round(tr.y)); for (int q = 0; q < 7; q++) { float a = R.N() * 6.28f + t * 0.2f, d = R.N() * tr.r * 0.7f, x = tr.x + Mathf.Cos(a) * d, y = tr.y + Mathf.Sin(a) * d * 0.8f; gA.Poly(new[] { new Vector2(x, y - 5), new Vector2(x + 7, y), new Vector2(x + 1, y + 5), new Vector2(x - 6, y + 1) }, new Color(230 / 255f, 248 / 255f, 1, 0.85f * al)); }
                            }
                            else if (tr.crack > 0)
                            {
                                float k = Mathf.Min(1, tr.crack / 0.9f); var R = new Rng((int)Mathf.Round(tr.x + tr.y));
                                for (int q = 0; q < 9; q++) { float a = q / 9f * 6.28f + R.N(), x = tr.x, y = tr.y; var pl = new List<Vector2> { new Vector2(x, y) }; for (int s = 0; s < 4 * k + 1; s++) { a += (R.N() - 0.5f) * 0.9f; x += Mathf.Cos(a) * tr.r * 0.22f; y += Mathf.Sin(a) * tr.r * 0.2f; pl.Add(new Vector2(x, y)); } gA.Polyline(pl, 1.5f + k, new Color(1, 1, 1, 0.4f + 0.6f * k)); }
                            }
                            break;
                        }
                    case "spikes":
                        {
                            if (!vis(tr.x, tr.y)) break; float cw = tr.w / Mathf.Max(1, Mathf.Round(tr.w / 44)), ch = tr.h / Mathf.Max(1, Mathf.Round(tr.h / 44));
                            for (float px = -tr.w / 2; px < tr.w / 2 - 1; px += cw) for (float py = -tr.h / 2; py < tr.h / 2 - 1; py += ch)
                                {
                                    gA.RotRect(tr.x, tr.y, tr.ang, px + 2, py + 2, px + cw - 2, py + ch - 2, Hx("#2e2c30")); gA.RotRect(tr.x, tr.y, tr.ang, px + 2, py + 2, px + cw - 2, py + 5, Hx("#48464c"));
                                    for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) { float hx = px + cw * (a + 0.5f) / 3, hy = py + ch * (b + 0.5f) / 3; var p = Rot(tr, hx, hy); gA.Ellipse(p.x, p.y, 2.2f, 2.2f, tr.st == 1 && Mathf.Sin(t * 30 + a + b) > 0 ? Hx("#ff9a6a") : Hx("#141216"), -1, 0, 8); }
                                }
                            break;
                        }
                    case "geyser":
                        {
                            if (!vis(tr.x, tr.y)) break; gA.Ellipse(tr.x, tr.y, tr.r * 0.45f, tr.r * 0.3f, Hx("#2a1610"));
                            float k = tr.st == 1 ? 0.5f + 0.5f * Mathf.Sin(t * 25) : tr.st == 2 ? 1 : 0.25f; gAdd.Ellipse(tr.x, tr.y, tr.r * 0.3f, tr.r * 0.2f, new Color(1, 170 / 255f, 60 / 255f, 0.5f * k));
                            if (tr.st == 1) { float q = (t * 2) % 1; gAdd.Arc(tr.x, tr.y, tr.r * q, tr.r * q * 0.7f, 0, 6.3f, 3, new Color(1, 140 / 255f, 40 / 255f, 0.7f)); gAdd.Arc(tr.x, tr.y, tr.r, tr.r * 0.7f, 0, 6.3f, 3, new Color(1, 90 / 255f, 20 / 255f, 0.5f)); }
                            break;
                        }
                    case "hammer":
                        {
                            if (!vis(tr.x, tr.y)) break; gA.Rect(tr.x - tr.w / 2, tr.y - tr.h / 2, tr.w, tr.h, Hx("#2e2a28"));
                            gA.Polyline(new[] { new Vector2(tr.x - tr.w / 2 + 3, tr.y - tr.h / 2 + 3), new Vector2(tr.x + tr.w / 2 - 3, tr.y - tr.h / 2 + 3), new Vector2(tr.x + tr.w / 2 - 3, tr.y + tr.h / 2 - 3), new Vector2(tr.x - tr.w / 2 + 3, tr.y + tr.h / 2 - 3) }, 3, Hx("#4a4440"), true);
                            foreach (var ab in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) }) gA.Ellipse(tr.x + ab.x * (tr.w / 2 - 10), tr.y + ab.y * (tr.h / 2 - 10), 3, 3, Hx("#5a5450"), -1, 0, 8);
                            float lift = HammerLift(tr), sh = 0.15f + 0.6f * (1 - lift), sw = tr.w * (0.7f + 0.3f * (1 - lift)), shh = tr.h * (0.7f + 0.3f * (1 - lift)); gA.Rect(tr.x - sw / 2, tr.y - shh / 2, sw, shh, new Color(0, 0, 0, sh));
                            if (tr.ph > 0.4f && tr.ph < 0.7f) gA.Polyline(new[] { new Vector2(tr.x - tr.w / 2, tr.y - tr.h / 2), new Vector2(tr.x + tr.w / 2, tr.y - tr.h / 2), new Vector2(tr.x + tr.w / 2, tr.y + tr.h / 2), new Vector2(tr.x - tr.w / 2, tr.y + tr.h / 2) }, 3, new Color(1, 60 / 255f, 40 / 255f, 0.4f + 0.4f * Mathf.Sin(t * 18)), true);
                            break;
                        }
                    case "blade":
                        {
                            if (!vis(tr.x, tr.y)) break; float nx = tr.ay, ny = -tr.ax; gA.Ellipse(tr.cbx, tr.cby, tr.len / 2 + 6, 9, new Color(0, 0, 0, 0.3f), -1, Mathf.Atan2(ny, nx)); break;
                        }
                    case "boulder":
                        {
                            if (tr.rolling && vis(tr.x, tr.y)) gA.Ellipse(tr.x, tr.y + tr.r * 0.25f, tr.r * 1.05f, tr.r * 0.45f, new Color(0, 0, 0, 0.4f));
                            if (tr.warn && vis(tr.ax, tr.ay)) for (int q = 0; q < 6; q++) { float a = Random.value * 6.28f, d = Random.value * 60, s = 4 + Random.value * 8; gA.Ellipse(tr.ax + Mathf.Cos(a) * d, tr.ay + Mathf.Sin(a) * d * 0.6f, s, s, new Color(160 / 255f, 140 / 255f, 110 / 255f, 0.35f), -1, 0, 10); }
                            break;
                        }
                }
            }
        }
        static Vector2 Rot(A.Trap t, float lx, float ly) { float ca = Mathf.Cos(t.ang), sa = Mathf.Sin(t.ang); return new Vector2(t.x + lx * ca - ly * sa, t.y + lx * sa + ly * ca); }
        static float HammerLift(A.Trap t) { float ph = t.ph; return ph < 0.66f ? 1 : ph < 0.7f ? 1 - (ph - 0.66f) / 0.04f : ph < 0.84f ? 0 : (ph - 0.84f) / 0.16f; }
        void DrawPhase(A.Trap tr, float t)
        {
            var Pp = tr.pts; float hw = tr.w / 2, a = tr.solid ? (tr.warn ? (Mathf.Sin(t * 30) > 0 ? 0.95f : 0.3f) : 1) : 0;
            for (int s = 0; s < Pp.Length - 1; s++)
            {
                Vector2 pa = Pp[s], pb = Pp[s + 1]; float L = Vector2.Distance(pa, pb), ux = (pb.x - pa.x) / L, uy = (pb.y - pa.y) / L, nx = -uy, ny = ux;
                System.Func<float, Vector2[]> quad = o => new[] { new Vector2(pa.x + nx * hw, pa.y + ny * hw + o), new Vector2(pb.x + nx * hw, pb.y + ny * hw + o), new Vector2(pb.x - nx * hw, pb.y - ny * hw + o), new Vector2(pa.x - nx * hw, pa.y - ny * hw + o) };
                if (a > 0)
                {
                    gA.Poly(quad(24), new Color(0, 0, 0, a * 0.45f));
                    // the gradient across the bridge: edges #5a38a0, middle #2c1a52
                    var q = quad(0); var mid0 = (q[0] + q[3]) / 2; var mid1 = (q[1] + q[2]) / 2; var ce = Hx("#5a38a0", a * 0.85f); var cm = Hx("#2c1a52", a * 0.85f);
                    gA.Poly(new[] { q[0], q[1], mid1, mid0 }, ce); gA.Poly(new[] { mid0, mid1, q[2], q[3] }, cm);
                    foreach (var o in new[] { hw - 2, -hw + 2 }) gAdd.Line(pa.x + nx * o, pa.y + ny * o, pb.x + nx * o, pb.y + ny * o, 2.5f, new Color(200 / 255f, 150 / 255f, 1, 0.8f * a));
                    for (float d = 18; d < L - 8; d += 36) { float x = pa.x + ux * d, y = pa.y + uy * d, k = 0.35f + 0.65f * Mathf.Max(0, Mathf.Sin(t * 5 - d * 0.06f)); gAdd.Poly(new[] { new Vector2(x + ux * 9, y + uy * 9), new Vector2(x + nx * 6, y + ny * 6), new Vector2(x - ux * 9, y - uy * 9), new Vector2(x - nx * 6, y - ny * 6) }, new Color(220 / 255f, 180 / 255f, 1, (0.25f + 0.6f * k) * a)); }
                }
                else
                {   // dashed outline while it is gone
                    foreach (var o in new[] { hw, -hw }) for (float d = (t * 20) % 18; d < L; d += 18) { float d1 = Mathf.Min(L, d + 6); gA.Line(pa.x + ux * d + nx * o, pa.y + uy * d + ny * o, pa.x + ux * d1 + nx * o, pa.y + uy * d1 + ny * o, 1.5f, Hx("#b07aff", 0.3f)); }
                }
            }
        }
        // ---------- gates: bars that lift into the ceiling when the section is cleared ----------
        void DrawGates(A.Map D, float t)
        {
            foreach (var g in D.gates)
            {
                if (g.openT >= 1 || g.pts.Length == 0) continue; string st = D.gateStyle; float k = g.openT, L = Mathf.Sqrt((g.x2 - g.x1) * (g.x2 - g.x1) + (g.y2 - g.y1) * (g.y2 - g.y1)), uy = (g.y2 - g.y1) / L, Hh = 100;
                float sk = Mathf.Abs(uy) * 0.42f, al = 1 - k * 0.7f; var pts = g.pts;
                // P(x, y, h) = (x - sk h, y - h): a point h up, leaning a little
                gAdd.Line(pts[0].x, pts[0].y, pts[pts.Length - 1].x, pts[pts.Length - 1].y, 5, new Color(D.torchCol.r, D.torchCol.g, D.torchCol.b, 0.5f * (1 - k) * al));
                float lift = k * Hh * 0.95f, hh = Hh * (1 - k * 0.95f);
                if (st == "ice" || st == "void")
                {
                    for (int i = 0; i < pts.Length - 1; i++)
                    {
                        Vector2 a0 = pts[i], a1 = pts[i + 1]; Color cb, ct;
                        if (st == "ice") { cb = new Color(150 / 255f, 210 / 255f, 245 / 255f, 0.85f * al); ct = new Color(230 / 255f, 248 / 255f, 1, 0.5f * al); }
                        else { float w = 0.5f + 0.5f * Mathf.Sin(t * 4 + a0.x * 0.05f + a0.y * 0.05f); cb = new Color(120 / 255f, 50 / 255f, 220 / 255f, (0.55f + 0.2f * w) * al); ct = new Color(200 / 255f, 140 / 255f, 1, 0.08f * al); }
                        gA.VQuad(new Vector3(a0.x, a0.y, 0), new Vector3(a1.x, a1.y, 0), new Vector3(a1.x - sk * hh, a1.y, hh), new Vector3(a0.x - sk * hh, a0.y, hh), cb, ct);
                    }
                    var lc = st == "ice" ? new Color(1, 1, 1, 0.85f * al) : new Color(220 / 255f, 180 / 255f, 1, 0.85f * al);
                    for (int i = 0; i < pts.Length - 1; i++) gA.VLine(pts[i].x - sk * hh, pts[i].y, hh, pts[i + 1].x - sk * hh, pts[i + 1].y, hh, 2, lc);
                }
                else
                {
                    foreach (var p in pts)
                    {
                        gA.VLine(p.x - sk * lift, p.y, lift, p.x - sk * (Hh + lift * 0.2f), p.y, Hh + lift * 0.2f, 6, Hx("#1e1e24", al));
                        gA.VLine(p.x - sk * lift - 1.5f, p.y, lift, p.x - sk * (Hh + lift * 0.2f) - 1.5f, p.y, Hh + lift * 0.2f, 1.5f, Hx("#707080", al));
                        if (st == "hot") gAdd.VLine(p.x - sk * lift, p.y, lift, p.x - sk * (lift + 26), p.y, lift + 26, 3, new Color(1, 110 / 255f, 30 / 255f, 0.45f * al));
                    }
                    foreach (var f in new[] { 0.22f, 0.62f, 0.98f }) { float h = lift + (Hh - lift * 0.8f) * f; for (int i = 0; i < pts.Length - 1; i++) gA.VLine(pts[i].x - sk * h, pts[i].y, h, pts[i + 1].x - sk * h, pts[i + 1].y, h, 6, Hx("#26262c", al)); }
                }
                foreach (var p in new[] { pts[0], pts[pts.Length - 1] })
                {
                    float h = Hh + 14; gA.VQuad(new Vector3(p.x - 9, p.y, 0), new Vector3(p.x + 9, p.y, 0), new Vector3(p.x - sk * h + 9, p.y, h), new Vector3(p.x - sk * h - 9, p.y, h), Hx("#2e2a28", al), Hx("#2e2a28", al));
                    gA.VQuad(new Vector3(p.x - sk * h - 11, p.y, h - 2), new Vector3(p.x - sk * h + 11, p.y, h - 2), new Vector3(p.x - sk * h + 11, p.y, h + 6), new Vector3(p.x - sk * h - 11, p.y, h + 6), Hx("#4a4440", al), Hx("#4a4440", al));
                }
            }
        }
        // ---------- things above the floor (over everything): blades, flames, boulders, hammer heads, geysers, beams ----------
        void DrawAir(A.Map D, float t, System.Func<float, float, bool> vis)
        {
            foreach (var tr in D.traps)
            {
                switch (tr.type)
                {
                    case "spikes":
                        {
                            if (!vis(tr.x, tr.y) || !(tr.st == 2 || tr.st == 3)) break; float ph = tr.ph, e = tr.st == 2 ? Mathf.Min(1, (ph - 0.7f) / 0.03f) : Mathf.Max(0, 1 - (ph - 0.92f) / 0.08f);
                            float cw = tr.w / Mathf.Max(1, Mathf.Round(tr.w / 44)), ch = tr.h / Mathf.Max(1, Mathf.Round(tr.h / 44));
                            for (float px = -tr.w / 2; px < tr.w / 2 - 1; px += cw) for (float py = -tr.h / 2; py < tr.h / 2 - 1; py += ch) for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++)
                                        {
                                            var p = Rot(tr, px + cw * (a + 0.5f) / 3, py + ch * (b + 0.5f) / 3); float h = 16 * e;
                                            aA.Tri(new Vector2(p.x - 3, p.y), new Vector2(p.x, p.y - h), new Vector2(p.x + 3, p.y), Hx("#9a9aa6")); aA.Tri(new Vector2(p.x - 3, p.y), new Vector2(p.x, p.y - h), new Vector2(p.x - 0.5f, p.y), Hx("#e0e0ea"));
                                        }
                            break;
                        }
                    case "blade":
                        {
                            if (!vis(tr.x, tr.y)) break; float nx = tr.ay, ny = -tr.ax, hl = tr.len / 2, bx = tr.cbx, by = tr.cby - 48;
                            aA.Line(tr.x, tr.y - 330, bx, by - 24, 4, Hx("#3a3a42")); aA.Line(tr.x, tr.y - 330, bx, by - 24, 1.2f, Hx("#6a6a74"));
                            for (int g = 2; g >= 0; g--)
                            {
                                float back = g * 0.05f * Mathf.Sign(tr.vel == 0 ? 1 : tr.vel), s2 = Mathf.Sin(2 * Mathf.PI / tr.per * (t - back) + tr.off * 2 * Mathf.PI / tr.per), gx = tr.x + tr.ax * tr.amp * s2, gy = tr.y + tr.ay * tr.amp * s2 - 48;
                                float al = g > 0 ? 0.18f : 1, rot = Mathf.Atan2(ny, nx), ca = Mathf.Cos(rot), sa = Mathf.Sin(rot);
                                var shape = new List<Vector2>(); System.Action<Vector2, Vector2, Vector2> qd = (p0, c0, p1) => { for (int k = 1; k <= 8; k++) { var q = Quad(p0, c0, p1, k / 8f); shape.Add(new Vector2(gx + q.x * ca - q.y * sa, gy + q.x * sa + q.y * ca)); } };
                                shape.Add(new Vector2(gx + -hl * ca - (-6) * sa, gy + -hl * sa + (-6) * ca));
                                qd(new Vector2(-hl, -6), new Vector2(0, -26), new Vector2(hl, -6)); qd(new Vector2(hl, -6), new Vector2(hl * 0.6f, 26), new Vector2(0, 30)); qd(new Vector2(0, 30), new Vector2(-hl * 0.6f, 26), new Vector2(-hl, -6));
                                aA.Poly(shape, new Color(200 / 255f, 204 / 255f, 214 / 255f, al));
                                if (g == 0) { var ed = new List<Vector2>(); for (int k = 0; k <= 10; k++) { var q = Quad(new Vector2(-hl * 0.8f, 8), new Vector2(0, 40), new Vector2(hl * 0.8f, 8), k / 10f); ed.Add(new Vector2(gx + q.x * ca - q.y * sa, gy + q.x * sa + q.y * ca)); } aA.Polyline(ed, 1.5f, Color.white); aA.Ellipse(gx + 10 * sa, gy - 10 * ca, 6, 6, Hx("#2a2a30"), -1, 0, 10); }
                            }
                            break;
                        }
                    case "fire":
                        {
                            if (!vis(tr.x, tr.y)) break; float ca = Mathf.Cos(tr.ang), sa = Mathf.Sin(tr.ang);
                            if (tr.st == 1) for (int q = 0; q < 6; q++) { float d = Random.value * 24, sx = tr.x + ca * d + (Random.value - 0.5f) * 14, sy = tr.y + sa * d - 24 + (Random.value - 0.5f) * 14; aAdd.Rect(sx, sy, 2, 2, new Color(1, 200 / 255f, 90 / 255f, 0.9f)); }
                            if (tr.st == 2) for (float s = 0; s < tr.len; s += 12)
                                {
                                    float u = s / tr.len, r = tr.wid * (0.25f + 0.4f * u) * (0.8f + 0.3f * Random.value), x = tr.x + ca * s + (Random.value - 0.5f) * 8, y = tr.y + sa * s - 24 + (Random.value - 0.5f) * 8 - u * 10;
                                    aAdd.Glow(x, y, r, r, new[] { 0f, 0.4f, 1f }, new[] { new Color(1, 240 / 255f, 170 / 255f, 0.55f * (1 - u * 0.6f)), new Color(1, 140 / 255f, 40 / 255f, 0.4f * (1 - u * 0.5f)), new Color(200 / 255f, 40 / 255f, 10 / 255f, 0) });
                                }
                            break;
                        }
                    case "boulder":
                        {
                            if (!tr.rolling || !vis(tr.x, tr.y)) break; float x = tr.x, y = tr.y - tr.r * 0.85f;
                            aA.Glow(x - tr.r * 0.35f * 0, y, tr.r, tr.r, new[] { 0f, 0.7f, 1f }, new[] { Hx("#9a8a70"), Hx("#5a4a38"), Hx("#2a2018") });
                            for (int q = 0; q < 5; q++) { float a = tr.rot + q * 1.26f; var pl = new List<Vector2>(); for (int k = 0; k <= 8; k++) pl.Add(Quad(new Vector2(x + Mathf.Cos(a) * tr.r * 0.2f, y + Mathf.Sin(a) * tr.r * 0.9f), new Vector2(x + Mathf.Cos(a + 0.6f) * tr.r * 0.6f, y), new Vector2(x + Mathf.Cos(a + 1) * tr.r, y + Mathf.Sin(a + 1) * tr.r * 0.4f), k / 8f)); aA.Polyline(pl, 3, new Color(30 / 255f, 22 / 255f, 14 / 255f, 0.7f)); }
                            break;
                        }
                    case "hammer":
                        {
                            if (!vis(tr.x, tr.y)) break; float lift = HammerLift(tr), hy = tr.y - lift * 230, fh = tr.h * 0.55f;
                            aA.Rect(tr.x - 12, hy - fh - 700, 24, 700, Hx("#1e1c1c")); aA.Rect(tr.x - 12, hy - fh - 700, 5, 700, Hx("#3a3634"));
                            aA.Rect(tr.x - tr.w / 2, hy - fh, tr.w, fh, Hx("#2a2826")); aA.Rect(tr.x - tr.w / 2, hy - fh - tr.h * 0.35f, tr.w, tr.h * 0.35f, Hx("#46423e"));
                            for (int q = 0; q < 5; q++) aA.Ellipse(tr.x - tr.w / 2 + 14 + q * (tr.w - 28) / 4, hy - fh + 12, 3.5f, 3.5f, Hx("#5a5652"), -1, 0, 8);
                            aAdd.Rect(tr.x - tr.w / 2, hy - 5, tr.w, 5, new Color(1, 90 / 255f, 20 / 255f, 0.55f));
                            break;
                        }
                    case "geyser":
                        {
                            if (!vis(tr.x, tr.y) || tr.st != 2) break;
                            for (int q = 0; q < 22; q++)
                            {
                                float u = Random.value, x = tr.x + (Random.value - 0.5f) * tr.r * 0.7f * (1 - u * 0.4f), y = tr.y - u * 190, r = 10 + Random.value * 16 * (1 - u * 0.5f);
                                aAdd.Glow(x, y, r, r, new[] { 0f, 0.5f, 1f }, new[] { new Color(1, 230 / 255f, 140 / 255f, 0.7f), new Color(1, 120 / 255f, 30 / 255f, 0.45f), new Color(180 / 255f, 30 / 255f, 0, 0) });
                            }
                            break;
                        }
                    case "beam":
                        {
                            if (!vis(tr.x, tr.y)) break;
                            for (int k = 0; k < tr.n; k++)
                            {
                                float a = tr.a + k * 2 * Mathf.PI / tr.n, ex = tr.x + Mathf.Cos(a) * tr.len, ey = tr.y + Mathf.Sin(a) * tr.len, sx = tr.x + Mathf.Cos(a) * 30, sy = tr.y + Mathf.Sin(a) * 30 - 40, wob = 0.9f + 0.1f * Mathf.Sin(t * 20);
                                aAdd.Line(sx, sy, ex, ey - 6, 34 * wob, new Color(150 / 255f, 60 / 255f, 1, 0.18f)); aAdd.Line(sx, sy, ex, ey - 6, 16 * wob, new Color(190 / 255f, 120 / 255f, 1, 0.4f)); aAdd.Line(sx, sy, ex, ey - 6, 5 * wob, new Color(250 / 255f, 230 / 255f, 1, 0.95f));
                                for (int q = 0; q < 3; q++) aAdd.Rect(ex + (Random.value - 0.5f) * 20, ey - 6 + (Random.value - 0.5f) * 20, 2, 2, new Color(230 / 255f, 200 / 255f, 1, 0.8f));
                            }
                            break;
                        }
                }
            }
        }

        // ---------- what each deep dungeon adds (dunxDraw, ksewDraw) ----------
        void DrawDeep(A.Map D, float t, System.Func<float, float, bool> vis, float x0, float y0, float vw, float vh)
        {
            string id = D.id;
            if (id == "sunken" && D.tide > 0)
            {
                aA.Rect(x0 - 100, y0 - 100, vw + 200, vh + 200, new Color(30 / 255f, 120 / 255f, 140 / 255f, 0.28f * D.tide));
                foreach (var d in D.decals) if (d.kind == "dais" && vis(d.x, d.y)) { float r = d.r + Mathf.Sin(t * 3) * 4; aA.Arc(d.x, d.y, r, r * 0.9f, 0, 6.3f, 2, new Color(180 / 255f, 240 / 255f, 1, 0.4f * D.tide)); }
            }
            if (id == "nest")
            {
                var wc = new Color(230 / 255f, 240 / 255f, 220 / 255f, 0.55f);
                foreach (var w in D.traps)
                {
                    if (w.type != "web" || !vis(w.x, w.y)) continue;
                    for (int k = 0; k < 8; k++) { float a = k / 8f * Mathf.PI * 2; aA.Line(w.x, w.y, w.x + Mathf.Cos(a) * w.r, w.y + Mathf.Sin(a) * w.r * 0.8f, 1.2f, wc); }
                    for (int ring = 1; ring <= 3; ring++) { var pl = new List<Vector2>(); for (int k = 0; k <= 8; k++) { float a = k / 8f * Mathf.PI * 2, rr = w.r * ring / 3.3f; pl.Add(new Vector2(w.x + Mathf.Cos(a) * rr, w.y + Mathf.Sin(a) * rr * 0.8f)); } aA.Polyline(pl, 1.2f, wc); }
                }
            }
            if (id == "clock")
                foreach (var tr in D.traps)
                {
                    if (tr.type != "turret" || !vis(tr.x, tr.y)) continue;
                    for (int k = 0; k < tr.n; k++)
                    {
                        float a = tr.a + k * Mathf.PI * 2 / tr.n, ex = tr.x + Mathf.Cos(a) * tr.len, ey = tr.y + Mathf.Sin(a) * tr.len;
                        aAdd.Line(tr.x, tr.y - 18, ex, ey - 4, 14, new Color(106 / 255f, 224 / 255f, 1, 0.25f)); aAdd.Line(tr.x, tr.y - 18, ex, ey - 4, 3, new Color(220 / 255f, 250 / 255f, 1, 0.95f));
                        float s = 5 + Mathf.Sin(t * 20) * 1.5f; aA.Ellipse(ex, ey - 4, s, s, new Color(106 / 255f, 224 / 255f, 1, 0.8f), -1, 0, 12);
                    }
                    aA.Ellipse(tr.x, tr.y, 26, 12, Hx("#3a3228")); aA.Ellipse(tr.x, tr.y - 18, 14, 14, Hx("#c8a050"), -1, 0, 18); aA.Ellipse(tr.x, tr.y - 18, 6, 6, Hx("#6ae0ff"), -1, 0, 12);
                }
            if (id == "blood")
                foreach (var d in D.decals) { if (d.kind != "circle" || d.col != "#ffe07a" || !vis(d.x, d.y)) continue; aAdd.Glow(d.x, d.y - 20, d.r * 1.2f, Hx("#ffe07a"), 0.22f + 0.08f * Mathf.Sin(t * 2)); }
            if (id == "sewer")
                foreach (var g in D.gas)
                {
                    float k = g.life / g.max, a = (g.life < 1.2f ? g.life / 1.2f : 1 - Mathf.Max(0, (k - 0.75f) / 0.25f)) * 0.34f, rx = g.r * (1 + Mathf.Sin(t + g.x) * 0.05f);
                    aA.Glow(g.x, g.y - 20, rx, g.r * 0.62f, new[] { 0f, 1f }, new[] { new Color(140 / 255f, 200 / 255f, 60 / 255f, a), new Color(90 / 255f, 140 / 255f, 40 / 255f, 0) });
                }
        }
        /// the dungeon's own lights this frame (dunLights); the post pass adds the hero, pet and bosses
        public List<AldaraPost.Lt> Gather(A.Map D, float t)
        {
            var L = new List<AldaraPost.Lt>();
            System.Action<float, float, Color, float, float> add = (x, y, c, r, i) => L.Add(new AldaraPost.Lt { x = x, y = y, c = c, r = r, i = i });
            foreach (var l in D.lights) { float f = l.fl ? 0.84f + 0.16f * Mathf.Sin(t * 9 + (l.ph != 0 ? l.ph : l.x)) * Mathf.Sin(t * 5.3f + l.y) : 1; add(l.x, l.y, l.col != null ? AldaraRules.Hex(l.col) : D.torchCol, l.r * f, l.i * f); }
            foreach (var tr in D.traps)
            {
                if (tr.type == "fire" && tr.st == 2) add(tr.x + Mathf.Cos(tr.ang) * tr.len / 2, tr.y + Mathf.Sin(tr.ang) * tr.len / 2, Hx("#ff8a30"), tr.len * 0.9f, 0.9f);
                else if (tr.type == "geyser" && tr.st >= 1) add(tr.x, tr.y - 60, Hx("#ff8a30"), tr.st == 2 ? 300 : 160, tr.st == 2 ? 0.9f : 0.4f);
                else if (tr.type == "beam") for (int k = 0; k < tr.n; k++) { float a = tr.a + k * 2 * Mathf.PI / tr.n; add(tr.x + Mathf.Cos(a) * tr.len * 0.6f, tr.y + Mathf.Sin(a) * tr.len * 0.6f, Hx("#c07aff"), 260, 0.8f); }
                else if (tr.type == "phase" && tr.solid) { var Pp = tr.pts; add((Pp[0].x + Pp[Pp.Length - 1].x) / 2, (Pp[0].y + Pp[Pp.Length - 1].y) / 2, Hx("#b07aff"), 260, 0.5f); }
            }
            return L;
        }

        /// labels drawn by the quest overlay (none yet)
        public void Labels(System.Func<float, float, Vector2> w2p, float k, AldaraWaystones.LabFn lab) { }

        // the browser's seeded random (mulberry32)
        class Rng { readonly AldaraRng r; public Rng(int s) { r = new AldaraRng(s); } public float N() { return r.Next(); } }
    }
}
