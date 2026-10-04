using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The browser's World Map (N, or click the minimap): the whole realm charted as the browser charts it, dragged to pan,
    // wheel or buttons to zoom, region names with their levels, bosses, towns, attuned waystones, monster camps and you,
    // a tooltip for the marker under the cursor, the region under the cursor, a scale bar, and a waypoint you set by clicking.
    public class AldaraMapWin : AldaraWindows.Win
    {
        public AldaraMapWin() { id = "map"; title = "Map of Aldara"; }
        public override bool Full => true; public override float W => 1920;
        public class Zone { public int z; public float x, y; public string name; public int lv; }
        public class Lair { public string name; public float x, y; public int? lvl; }
        public class Town { public string name, sub; public float x, y, r; }
        public class Ws { public int id, z; public string n; public float x, y; public bool known; }
        public class Camp { public string type; public float x, y; public int? lvl; }
        public class Data { public int W, H, PAD, SW, SH; public float SCALE, WORLD_W, WORLD; public Zone[] zones; public Lair[] lairs; public Town[] towns; public Ws[] waystones; public Camp[] camps; }
        static Data D; static byte[] sea;
        public static Data Map { get { if (D == null) { D = JsonConvert.DeserializeObject<Data>(Resources.Load<TextAsset>("map/map").text); sea = Resources.Load<TextAsset>("map/sea").bytes; } return D; } }
        public static bool Sea(float x, float y) { var d = Map; int i = (int)(x / 50), j = (int)(y / 50); if (i < 0 || j < 0 || i >= d.SW || j >= d.SH) return true; return sea[j * d.SW + i] == 1; }

        // the waypoint (shown on the minimap and in the world too)
        public class Wp { public float x, y; public string name; }
        public static Wp wp;
        static readonly Dictionary<string, bool> layers = new Dictionary<string, bool> { { "names", true }, { "camps", true }, { "lore", true } };
        float cx = -1, cy, z, fit; MapView view; Label where, zoomL; VisualElement wpBtn;

        public override void Render(VisualElement body)
        {
            body.style.backgroundColor = C("#07080f");
            // ---- the bar across the top ----
            var top = Row(body, 14); Pad(top, 8, 14); top.style.backgroundColor = C("#140f07"); BorderBottom(top, 1, C("#6a5a30")); top.style.flexShrink = 0; top.style.flexWrap = Wrap.Wrap;
            var tl = T(top, title, 20, C("#ffe8a0"), true, true, false, 3, true, false); Shadow(tl, C("#e8c46a", 0.5f), 14, 0);
            where = T(top, "", 14, C("#c8b890"), false, false, true, 0, false, false); where.style.flexGrow = 1; where.style.minWidth = 120;
            var tools = Row(top, 6); tools.style.flexWrap = Wrap.Wrap;
            Tog(tools, "names", "Names", "Region names"); Tog(tools, "camps", "Camps", "Monster camps"); Tog(tools, "lore", "Explored", "Places you have explored");
            var sep = E(tools); sep.style.width = 1; sep.style.height = 22; sep.style.backgroundColor = C("#4a4f62"); sep.style.marginLeft = sep.style.marginRight = 4;
            Tool(tools, "-", () => ZoomAt(1 / 1.5f, -1, -1)).tooltip = "Zoom out";
            zoomL = T(tools, "100%", 12, C("#a8a090"), true, false, false, 0, false, false); zoomL.style.minWidth = 44; zoomL.style.unityTextAlign = TextAnchor.MiddleCenter;
            Tool(tools, "+", () => ZoomAt(1.5f, -1, -1)).tooltip = "Zoom in";
            Tool(tools, "Find me", Center);
            wpBtn = Tool(tools, "Clear waypoint", () => { wp = null; Sync(); });
            var cl = Tool(tools, "Close", () => AldaraWindows.I.Toggle("map", false)); Border(cl, 1, C("#a04030"), 5); cl.style.backgroundColor = C("#2a1210"); ((Label)cl[0]).style.color = C("#ffb0a0");
            ApplyGapLater(tools); ApplyGapLater(top);
            view = new MapView(this); view.style.flexGrow = 1; body.Add(view);
            Sync();
        }
        static void ApplyGapLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }
        static VisualElement Tool(VisualElement p, string text, System.Action a)
        {
            var b = E(p); Border(b, 1, C("#4a4f62"), 5); b.style.backgroundColor = C("#161a26"); Pad(b, 5, 8); b.style.minWidth = 34; b.style.alignItems = Align.Center;
            T(b, text, 11, C("#d8d0bc"), true, false, false, 0, true, false);
            b.RegisterCallback<ClickEvent>(e => { a(); e.StopPropagation(); });
            b.RegisterCallback<MouseEnterEvent>(e => b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = C("#e8c46a"));
            b.RegisterCallback<MouseLeaveEvent>(e => b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = (b.userData as string) == "on" ? C("#8a7a50") : C("#4a4f62"));
            return b;
        }
        void Tog(VisualElement p, string k, string text, string tip)
        {
            VisualElement b = null; b = Tool(p, text, () => { layers[k] = !layers[k]; Style(); }); b.tooltip = tip;
            void Style() { bool on = layers[k]; b.userData = on ? "on" : "off"; b.style.opacity = on ? 1 : 0.55f; Border(b, 1, on ? C("#8a7a50") : C("#4a4f62"), 5); b.style.backgroundColor = on ? C("#221c10") : C("#161a26"); }
            Style();
        }
        void Sync() { if (wpBtn != null) wpBtn.style.display = wp != null ? DisplayStyle.Flex : DisplayStyle.None; if (zoomL != null && fit > 0) zoomL.text = Mathf.RoundToInt(z / fit * 100) + "%"; }
        public override void OnOpen() { cx = -1; }

        // ---------- the browser's map camera (wmFitZoom, wmClamp, wmZoomAt, wmCenter) ----------
        float VW => view.contentRect.width; float VH => view.contentRect.height;
        float Fit() { var d = Map; return Mathf.Min(VW / (d.WORLD_W + 2 * d.PAD), VH / (d.WORLD + 2 * d.PAD)) * 0.98f; }
        void Clamp()
        {
            var d = Map; float hw = VW / 2 / z, hh = VH / 2 / z;
            cx = Mathf.Max(Mathf.Min(hw, d.WORLD_W / 2), Mathf.Min(Mathf.Max(d.WORLD_W / 2, d.WORLD_W - hw), cx)); cy = Mathf.Max(Mathf.Min(hh, d.WORLD / 2), Mathf.Min(Mathf.Max(d.WORLD / 2, d.WORLD - hh), cy));
            if (hw > d.WORLD_W / 2) cx = d.WORLD_W / 2; if (hh > d.WORLD / 2) cy = d.WORLD / 2;
        }
        public Vector2 ToScreen(float x, float y) { return new Vector2((x - cx) * z + VW / 2, (y - cy) * z + VH / 2); }
        public Vector2 ToWorld(float sx, float sy) { return new Vector2((sx - VW / 2) / z + cx, (sy - VH / 2) / z + cy); }
        void ZoomAt(float f, float sx, float sy)
        {
            float z1 = Mathf.Max(fit * 0.9f, Mathf.Min(fit * 6, z * f)); if (sx < 0 || sy < 0) { sx = VW / 2; sy = VH / 2; }
            var w = ToWorld(sx, sy); z = z1; cx = w.x - (sx - VW / 2) / z1; cy = w.y - (sy - VH / 2) / z1; Clamp(); Sync();
        }
        void Center() { var P = AldaraPlayer.I; cx = P.x; cy = P.y; if (z < fit * 3) z = fit * 3; Clamp(); Sync(); }
        void SetWp(float x, float y, string name)
        {
            if (wp != null && Vector2.Distance(new Vector2(wp.x, wp.y), new Vector2(x, y)) < 40 / z) { wp = null; AldaraHud.Banner("Waypoint cleared"); }
            else { wp = new Wp { x = Mathf.Round(x), y = Mathf.Round(y), name = name }; AldaraHud.Banner(name != null ? "Waypoint: " + name : "Waypoint set"); }
            Sync();
        }

        public class Marker { public string k, name, sub, mk; public float x, y, r; public bool noWp; public Vector2 s; }
        List<Marker> Markers()
        {
            var d = Map; var M = new List<Marker>(); var P = AldaraPlayer.I; var H = AldaraHero.I;
            foreach (var L in d.lairs) M.Add(new Marker { k = "boss", x = L.x, y = L.y, name = L.name, sub = "Boss, level " + (L.lvl.HasValue ? L.lvl.Value.ToString() : "?"), r = 9 });
            foreach (var t in d.towns) M.Add(new Marker { k = "town", x = t.x, y = t.y, name = t.name, sub = t.sub, r = t.r });
            var known = new HashSet<int>(); if (AldaraSave.Raw?["waystones"] is JArray ja) foreach (var v in ja) known.Add((int)v);
            foreach (var w in d.waystones) if (w.known || known.Contains(w.id)) M.Add(new Marker { k = "ws", x = w.x, y = w.y, name = w.n, sub = "Waystone. Travel here from any other waystone.", r = 8 });
            if (layers["camps"]) foreach (var c in d.camps) M.Add(new Marker { k = "camp", x = c.x, y = c.y, name = c.type + " camp", sub = "Level " + (c.lvl.HasValue ? c.lvl.Value.ToString() : "?") + " camp", r = 5 });
            if (layers["lore"]) { var lm = AldaraLoreScenes.Mine; if (lm != null && lm.Count > 0) { AldaraLoreScenes.Load(); foreach (var v in lm) { if (v.Type != JTokenType.Integer) continue; int id = (int)v; if (id < 0 || id >= AldaraLoreScenes.ALL.Count) continue; var L = AldaraLoreScenes.ALL[id]; M.Add(new Marker { k = "lore", x = L.x, y = L.y, name = AldaraLoreScenes.MapName(L.k), sub = "A place you have explored", r = 5 }); } } }
            try
            {
                if (AldaraQuests.QOBJ != null && !AldaraWorld.Dun)
                {
                    foreach (var kv in AldaraQuests.QOBJ) { string mk = AldaraQuests.Marker(kv.Key); if (mk == null) continue; var n = kv.Value; M.Add(new Marker { k = "quest", x = n.x, y = n.y, name = AldaraQuests.QNPC.ContainsKey(kv.Key) ? AldaraQuests.QNPC[kv.Key].name : kv.Key, sub = mk.EndsWith("low") ? "Quest giver (too low level yet)" : "Quest available", r = 8, mk = mk.Substring(0, 1) }); }
                    var qw = AldaraQuests.Waypoint(); if (qw != null) M.Add(new Marker { k = "qwp", x = qw.x, y = qw.y, name = qw.label ?? "Quest objective", sub = "Your current quest leads here", r = 8 });
                }
            }
            catch { }
            if (wp != null) M.Add(new Marker { k = "wp", x = wp.x, y = wp.y, name = wp.name ?? "Waypoint", sub = Mathf.RoundToInt(Vector2.Distance(new Vector2(wp.x, wp.y), new Vector2(P.x, P.y)) / 60) + "m from you. Click to clear.", r = 9, noWp = true });
            M.Add(new Marker { k = "me", x = P.x, y = P.y, name = H.heroName, sub = "You, level " + H.lvl, r = 9, noWp = true });
            return M;
        }

        class MapView : VisualElement
        {
            readonly AldaraMapWin w; readonly VisualElement chart, labels, tip; readonly Label tipN, tipS; Marker hover; Vector2 mouse = new Vector2(-1, -1);
            Vector2 dragFrom; float dcx, dcy, moved; bool drag; readonly List<Label> pool = new List<Label>(); int used;
            public MapView(AldaraMapWin win)
            {
                w = win; style.overflow = Overflow.Hidden;
                chart = new VisualElement { pickingMode = PickingMode.Ignore }; chart.style.position = Position.Absolute; var t = Resources.Load<Texture2D>("map/chart"); if (t) chart.style.backgroundImage = Background.FromTexture2D(t); Add(chart);
                var over = new VisualElement { pickingMode = PickingMode.Ignore }; over.style.position = Position.Absolute; over.style.left = over.style.top = over.style.right = over.style.bottom = 0; over.generateVisualContent += Draw; Add(over);
                labels = new VisualElement { pickingMode = PickingMode.Ignore }; labels.style.position = Position.Absolute; labels.style.left = labels.style.top = labels.style.right = labels.style.bottom = 0; Add(labels);
                tip = new VisualElement { pickingMode = PickingMode.Ignore }; tip.style.position = Position.Absolute; Border(tip, 1, C("#8a7a50"), 6); tip.style.backgroundColor = new Color(10 / 255f, 12 / 255f, 20 / 255f, 0.92f); Pad(tip, 6, 12); Add(tip);
                tipN = T(tip, "", 13, C("#ffe8a0"), true, true, false, 0, false, false); tipS = T(tip, "", 12, C("#d8d0bc"), false, false, false, 0, false, false); tipS.style.marginTop = 2; tip.style.display = DisplayStyle.None;
                RegisterCallback<PointerDownEvent>(e => { drag = true; dragFrom = e.localPosition; dcx = w.cx; dcy = w.cy; moved = 0; this.CapturePointer(e.pointerId); });
                RegisterCallback<PointerMoveEvent>(e => { mouse = e.localPosition; if (drag) { var d = (Vector2)e.localPosition - dragFrom; moved += Mathf.Abs(e.deltaPosition.x) + Mathf.Abs(e.deltaPosition.y); w.cx = dcx - d.x / w.z; w.cy = dcy - d.y / w.z; w.Clamp(); } });
                RegisterCallback<PointerUpEvent>(e =>
                {
                    if (!drag) return; drag = false; this.ReleasePointer(e.pointerId);
                    if (moved < 6) { if (hover != null && !hover.noWp) w.SetWp(hover.x, hover.y, hover.name); else { var p = w.ToWorld(mouse.x, mouse.y); w.SetWp(p.x, p.y, null); } }
                });
                RegisterCallback<WheelEvent>(e => { w.ZoomAt(Mathf.Exp(-e.delta.y * 0.0016f * 40), mouse.x, mouse.y); e.StopPropagation(); });
                RegisterCallback<ClickEvent>(e => { if (e.clickCount == 2) w.ZoomAt(1.8f, mouse.x, mouse.y); });
                RegisterCallback<PointerLeaveEvent>(e => { mouse = new Vector2(-1, -1); hover = null; });
                schedule.Execute(Frame).Every(0);
            }
            Label L(string text, float size, Color c, bool bold, TextAnchor a, float x, float y, float wdt)
            {
                Label l; if (used < pool.Count) l = pool[used]; else { l = T(labels, "", 12, Color.white, true, true, false, 0, false, false); l.style.position = Position.Absolute; pool.Add(l); var sh = new TextShadow { color = new Color(0, 0, 0, 0.85f), offset = Vector2.zero, blurRadius = 3 }; l.style.textShadow = sh; }
                used++; l.style.unityTextOutlineWidth = 0; l.style.display = DisplayStyle.Flex; l.text = text; l.style.fontSize = size; l.style.color = c; l.style.unityFontDefinition = FontDefinition.FromFont(Font(true, bold));
                l.style.unityTextAlign = a; l.style.width = wdt; l.style.left = a == TextAnchor.MiddleLeft ? x : x - wdt / 2; l.style.top = y - size * 0.7f; return l;
            }
            void Frame()
            {
                var cr = contentRect; if (!w.open || !(cr.width > 1) || !(cr.height > 1)) return; var d = Map;
                float nf = w.Fit(); if (w.cx < 0 || !(w.fit > 0) || float.IsNaN(w.z)) { w.fit = nf; w.z = nf; w.cx = d.WORLD_W / 2; w.cy = d.WORLD / 2; w.Clamp(); w.Sync(); } else if (Mathf.Abs(nf - w.fit) > 1e-6f) { w.fit = nf; w.z = Mathf.Max(w.z, nf * 0.9f); w.Clamp(); w.Sync(); }
                var o = w.ToScreen(-d.PAD, -d.PAD); float ss = w.z / d.SCALE;
                chart.style.left = o.x; chart.style.top = o.y; chart.style.width = d.W * ss; chart.style.height = d.H * ss;
                used = 0; float z = w.z;
                if (layers["names"])
                    foreach (var Lz in d.zones)
                    {
                        var s = w.ToScreen(Lz.x, Lz.y); if (s.x < -200 || s.x > contentRect.width + 200 || s.y < -100 || s.y > contentRect.height + 100) continue;
                        float fs = Mathf.Max(15, Mathf.Min(34, z * 160));
                        L(Lz.name.ToUpper(), fs, new Color(1, 236 / 255f, 190 / 255f, 0.92f), true, TextAnchor.MiddleCenter, s.x, s.y, 600);
                        bool danger = Lz.lv - AldaraHero.I.lvl >= 10; string lv = Lz.z == 10 ? "PvP: free for all" : "Level " + Lz.lv + (danger ? "  (dangerous)" : "");
                        L(lv.ToUpper(), Mathf.Round(fs * 0.55f), danger ? C("#ff8a7a") : new Color(220 / 255f, 210 / 255f, 180 / 255f, 0.9f), true, TextAnchor.MiddleCenter, s.x, s.y + fs * 0.8f, 600);
                    }
                var M = w.Markers(); hover = null; float hd = 1e9f;
                foreach (var m in M) { m.s = w.ToScreen(m.x, m.y); if (mouse.x < 0) continue; float dd = Vector2.Distance(mouse, m.s); if (dd < Mathf.Max(10, m.r + 4) && dd < hd) { hd = dd; hover = m; } }
                foreach (var m in M)
                {
                    if ((m.k == "boss" || m.k == "town" || m.k == "me" || m.k == "qwp" || m.k == "ws") && (z > w.fit * 1.6f || m.k == "me" || m.k == "town"))
                        L(m.name.ToUpper(), 11, m.k == "boss" ? C("#ffb0a0") : m.k == "me" ? C("#bfe4ff") : m.k == "ws" ? C("#bfeeff") : C("#ffe8a0"), true, TextAnchor.MiddleLeft, m.s.x + 14, m.s.y, 300);
                    if (m.k == "quest") { var ql = L(m.mk, 15, m.sub.Contains("too low") ? C("#aaaaaa") : C("#ffd35a"), true, TextAnchor.MiddleCenter, m.s.x, m.s.y, 30); ql.style.unityTextOutlineWidth = 0.3f; ql.style.unityTextOutlineColor = Color.black; }
                    if (m.k == "boss") L("B", 9, Color.white, true, TextAnchor.MiddleCenter, m.s.x, m.s.y + 0.5f, 20);
                }
                for (int i = used; i < pool.Count; i++) pool[i].style.display = DisplayStyle.None;
                markers = M;
                // the tooltip
                if (hover != null)
                {
                    tip.style.display = DisplayStyle.Flex; tipN.text = hover.name; tipS.text = hover.sub;
                    float bw = Mathf.Max(tip.resolvedStyle.width, 60), bh = 44; float bx = hover.s.x + 16, by = hover.s.y - bh / 2; if (bx + bw > contentRect.width - 8) bx = hover.s.x - 16 - bw; by = Mathf.Max(8, Mathf.Min(contentRect.height - bh - 8, by));
                    tip.style.left = bx; tip.style.top = by;
                }
                else tip.style.display = DisplayStyle.None;
                // the region under the cursor
                if (mouse.x >= 0 && w.where != null) { var p = w.ToWorld(mouse.x, mouse.y); string zn = Sea(p.x, p.y) ? "The sea" : AldaraWorld.ZoneName(p.x, p.y); if (w.where.text != zn) w.where.text = zn; }
                // scale bar label
                int sm = 5; foreach (var c in new[] { 5, 10, 25, 50, 100, 250, 500, 1000, 2000 }) { sm = c; if (c * 60 * z >= 70) break; }
                L(sm + "m", 11, C("#e8dcc0"), false, TextAnchor.MiddleLeft, 26, contentRect.height - 30, 100);
                for (int i = used; i < pool.Count; i++) pool[i].style.display = DisplayStyle.None;
                foreach (var c in Children()) c.MarkDirtyRepaint();
            }
            List<Marker> markers;
            void Draw(MeshGenerationContext ctx)
            {
                if (markers == null) return; var p = ctx.painter2D; float t = Time.unscaledTime, W = contentRect.width, H = contentRect.height, z = w.z;
                // the soft vignette
                for (int k = 0; k < 10; k++) { p.strokeColor = new Color(0, 0, 0, 0.05f); p.lineWidth = Mathf.Max(W, H) * 0.03f; float rr = Mathf.Max(W, H) * (0.46f + k * 0.03f); p.BeginPath(); p.Arc(new Vector2(W / 2, H / 2), rr, 0, 360); p.Stroke(); }
                // a grid of leagues when zoomed in
                if (z > w.fit * 3)
                {
                    p.strokeColor = new Color(1, 1, 1, 0.06f); p.lineWidth = 1; var w0 = w.ToWorld(0, 0); var w1 = w.ToWorld(W, H);
                    for (float x = Mathf.Floor(w0.x / 1000) * 1000; x < w1.x; x += 1000) { var s = w.ToScreen(x, 0); p.BeginPath(); p.MoveTo(new Vector2(s.x, 0)); p.LineTo(new Vector2(s.x, H)); p.Stroke(); }
                    for (float y = Mathf.Floor(w0.y / 1000) * 1000; y < w1.y; y += 1000) { var s = w.ToScreen(0, y); p.BeginPath(); p.MoveTo(new Vector2(0, s.y)); p.LineTo(new Vector2(W, s.y)); p.Stroke(); }
                }
                // the waypoint line from you
                if (wp != null) { var a = w.ToScreen(wp.x, wp.y); var b = w.ToScreen(AldaraPlayer.I.x, AldaraPlayer.I.y); Dash(p, b, a, new Color(143 / 255f, 240 / 255f, 160 / 255f, 0.7f), 1.5f, 6, 6, t * 20); }
                foreach (var m in markers)
                {
                    float x = m.s.x, y = m.s.y; if (x < -30 || x > W + 30 || y < -30 || y > H + 30) continue; bool hv = m == hover; var c = new Vector2(x, y);
                    switch (m.k)
                    {
                        case "lore": Poly(p, new[] { c + new Vector2(-4, -4), c + new Vector2(4, -4), c + new Vector2(4, 4), c + new Vector2(-4, 4) }, C("#e8d8a0"), C("#5a4a20"), 1); break;
                        case "qwp": { float r = 8 + Mathf.Sin(t * 4) * 1.5f; Poly(p, new[] { c + new Vector2(0, -r), c + new Vector2(r * 0.8f, 0), c + new Vector2(0, r), c + new Vector2(-r * 0.8f, 0) }, C("#ffd35a"), Color.black, 1); break; }
                        case "camp": Disc(p, c, hv ? 4.5f : 3, new Color(1, 120 / 255f, 100 / 255f, 0.75f)); Ring(p, c, hv ? 4.5f : 3, 1.5f, Color.black); break;
                        case "boss": { float r = hv ? 11 : 8; Poly(p, new[] { c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0) }, C("#ff3a3a"), C("#ffd35a"), 2); break; }
                        case "town": Ring(p, c, hv ? 14 : 11, 2, C("#ffe8a0")); Disc(p, c, 4, C("#ffe8a0")); break;
                        case "ws": { float r = hv ? 9 : 7; Poly(p, new[] { c + new Vector2(0, -r), c + new Vector2(r * 0.8f, 0), c + new Vector2(0, r), c + new Vector2(-r * 0.8f, 0) }, C("#7fd8ff"), C("#e8fbff"), 1.5f); break; }
                        case "wp": Ring(p, c, 7 + Mathf.Sin(t * 3) * 2, 2, C("#8ff0a0")); Poly(p, new[] { c + new Vector2(0, -14), c + new Vector2(5, -4), c + new Vector2(-5, -4) }, C("#8ff0a0"), C("#8ff0a0"), 0); Disc(p, c, 2.5f, C("#8ff0a0")); break;
                        case "me":
                            {
                                float a = AldaraPlayer.I.facing; Vector2 R(float px, float py) { return c + new Vector2(px * Mathf.Cos(a) - py * Mathf.Sin(a), px * Mathf.Sin(a) + py * Mathf.Cos(a)); }
                                Poly(p, new[] { R(10, 0), R(-7, -7), R(-4, 0), R(-7, 7) }, C("#6fc0ff"), Color.white, 2);
                                Ring(p, c, 14 + Mathf.Sin(t * 3) * 3, 1.5f, new Color(111 / 255f, 192 / 255f, 1, 0.5f + 0.4f * Mathf.Sin(t * 3))); break;
                            }
                    }
                }
                // the scale bar
                int sm = 5; foreach (var cc in new[] { 5, 10, 25, 50, 100, 250, 500, 1000, 2000 }) { sm = cc; if (cc * 60 * z >= 70) break; }
                float len = sm * 60 * z; Rect(p, 16, H - 34, len + 16, 22, new Color(0, 0, 0, 0.5f)); var col = C("#e8dcc0"); Rect(p, 24, H - 20, len, 2, col); Rect(p, 24, H - 24, 2, 6, col); Rect(p, 22 + len, H - 24, 2, 6, col);
            }
            static void Disc(Painter2D p, Vector2 c, float r, Color col) { p.fillColor = col; p.BeginPath(); p.Arc(c, r, 0, 360); p.ClosePath(); p.Fill(); }
            static void Ring(Painter2D p, Vector2 c, float r, float lw, Color col) { p.strokeColor = col; p.lineWidth = lw; p.BeginPath(); p.Arc(c, r, 0, 360); p.ClosePath(); p.Stroke(); }
            static void Rect(Painter2D p, float x, float y, float w, float h, Color c) { p.fillColor = c; p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y)); p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h)); p.ClosePath(); p.Fill(); }
            static void Poly(Painter2D p, Vector2[] v, Color fill, Color stroke, float lw)
            {
                p.BeginPath(); p.MoveTo(v[0]); for (int i = 1; i < v.Length; i++) p.LineTo(v[i]); p.ClosePath(); p.fillColor = fill; p.Fill();
                if (lw > 0) { p.strokeColor = stroke; p.lineWidth = lw; p.Stroke(); }
            }
            static void Dash(Painter2D p, Vector2 a, Vector2 b, Color c, float lw, float on, float off, float phase)
            {
                float L = Vector2.Distance(a, b); if (L <= 0) return; var d = (b - a) / L; float per = on + off, st = ((phase % per) + per) % per - per;
                p.strokeColor = c; p.lineWidth = lw; for (float x = st; x < L; x += per) { float x0 = Mathf.Max(0, x), x1 = Mathf.Min(L, x + on); if (x1 <= x0) continue; p.BeginPath(); p.MoveTo(a + d * x0); p.LineTo(a + d * x1); p.Stroke(); }
            }
        }
    }
}
