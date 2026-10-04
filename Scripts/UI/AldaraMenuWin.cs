using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The browser's Game Menu (Esc with nothing open): Game (Resume, Log Out, Fullscreen, Save and Exit), Settings with its
    // Graphics, Interface and Sound pages built from the browser's own rows, and Controls with every key binding.
    public class AldaraMenuWin : AldaraWindows.Win
    {
        public AldaraMenuWin() { id = "set"; title = "Game Menu"; }
        public override float Y => 120; public override float W => 430; public override float MaxH => 930;
        string tab = "game", page = "graphics"; float scroll;
        public override void OnClose() { AldaraKeys.listening = null; }

        public override void Render(VisualElement body)
        {
            var tabs = Row(body, 6); tabs.style.marginBottom = 10; tabs.style.flexShrink = 0;
            foreach (var t in new[] { new[] { "game", "Game" }, new[] { "settings", "Settings" }, new[] { "controls", "Controls" } }) { string k = t[0]; Tab(tabs, t[1], tab == k, 12, 27, () => { tab = k; scroll = 0; AldaraKeys.listening = null; dirty = true; }); }
            ApplyGapLater(tabs);
            if (tab == "game") { Game(body); return; }
            var sv = new ScrollView(ScrollViewMode.Vertical); body.Add(sv); sv.style.flexGrow = 1; sv.style.flexShrink = 1; sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden; sv.mouseWheelScrollSize = 60;
            sv.contentContainer.style.paddingRight = 6; Thin(sv);
            float s0 = scroll; sv.schedule.Execute(() => sv.scrollOffset = new Vector2(0, s0)); sv.verticalScroller.valueChanged += v => scroll = v;
            var c = sv.contentContainer;
            if (tab == "settings") Settings(c); else Controls(c);
        }
        static void ApplyGapLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }
        static void Tab(VisualElement p, string text, bool on, float size, float h, System.Action a)
        {
            var b = new Btn(text, a, on ? "tab_on" : "tab_off", size); b.Padding(0, 4); b.style.height = h; b.style.flexGrow = 1; b.style.flexBasis = 0; p.Add(b);
            if (on) { b.label.style.color = C("#1a1008"); b.label.style.textShadow = new TextShadow(); }
        }
        static Btn SetBtn(VisualElement p, string text, System.Action a, string skin = "btn")
        {
            var b = B(p, text, a, skin, 12); b.Padding(9, 14); b.style.marginTop = 16; b.style.height = 31; return b;
        }
        static void Note(VisualElement p, string t) { var n = T(p, t, 10, C("#888888")); n.style.marginTop = 6; n.style.unityTextAlign = TextAnchor.MiddleCenter; }

        void Game(VisualElement body)
        {
            SetBtn(body, "Resume", () => AldaraWindows.I.Toggle("set", false));
            SetBtn(body, "Log Out to Character Select", () => { AldaraWindows.I.CloseAll(); AldaraSave.Logout(); if (AldaraTitle.I) AldaraTitle.I.Show("select"); });
            if (!Application.isEditor)
            {
                SetBtn(body, "Toggle Fullscreen (F11)", ToggleFullscreen);
                SetBtn(body, "Save and Exit to Desktop", () => { AldaraSave.Save(); Application.Quit(); });
            }
            Note(body, "Your progress is saved before logging out.");
        }
        public static void ToggleFullscreen()
        {
            var cur = (string)AldaraSettings.Get("disp") ?? "windowed";
            AldaraSettings.Set("disp", cur == "windowed" ? "borderless" : "windowed");
        }

        // ---------- settings pages ----------
        void Settings(VisualElement c)
        {
            var st = Row(c, 6); st.style.marginBottom = 10;
            foreach (var p in AldaraSettings.S.pages) { string k = p.id; Tab(st, p.name, page == k, 11, 22, () => { page = k; scroll = 0; dirty = true; }); }
            ApplyGapLater(st);
            var pg = AldaraSettings.S.pages.FirstOrDefault(p => p.id == page) ?? AldaraSettings.S.pages[0];
            foreach (var it in pg.items)
            {
                switch (it.k)
                {
                    case "sec":
                        bool h = page == "interface";   // the Interface page uses the ruled .set-h headings
                        var s = T(c, it.t, 11, C("#e8c46a"), true, false, false, h ? 2 : 1, true);
                        s.style.marginTop = 12; s.style.marginBottom = h ? 4 : 2; if (h) { s.style.paddingBottom = 3; BorderBottom(s, 1, C("#6a4a1a")); }
                        break;
                    case "row": SetRow(c, it); break;
                    case "btns":
                        var bp = Row(c, 6); bp.style.flexWrap = Wrap.Wrap; bp.style.marginTop = 6; bp.style.marginBottom = 8;
                        foreach (var b in it.b) { string on = b.on, bt = b.t; var x = B(bp, b.t, () => { if (bt == "Play a test sound") AldaraSound.Test(); else Preset(on); }, b.cls == "gold" ? "btn_gold" : b.cls == "warn" ? "btn_buy" : "btn", 11); x.Padding(5, 10); x.style.marginBottom = 4; }
                        ApplyGapLater(bp); break;
                    case "btn": if (it.id == "gpuApply") { var g = SetBtn(c, it.t, null); g.Disabled = true; } break;
                    case "note": if (!string.IsNullOrEmpty(it.t)) Note(c, it.t); break;
                    case "other":
                        if (it.cls == "gpuinfo")
                        {
                            var gi = E(c); Pad(gi, 9, 0); BorderBottom(gi, 1, C("#2a3040"));
                            var l = T(gi, "Graphics card in use", 11, C("#a08a60")); l.style.marginBottom = 3; T(gi, SystemInfo.graphicsDeviceName, 12, C("#fff2c4"));
                        }
                        break;
                }
            }
            SetBtn(c, "Restore Defaults", () => { AldaraSettings.Defaults(); dirty = true; });
        }
        /// the preset and layout buttons on the Interface and Sound pages (hudPreset, hudResetAll, sound test)
        void Preset(string js)
        {
            if (js == null) return;
            if (js.Contains("hudPreset"))
            {
                string p = js.Split('\'')[1];
                var P = new System.Collections.Generic.Dictionary<string, string[]> { { "compact", new[] { "0.85", "0.85", "0.85", "0.85", "1" } }, { "normal", new[] { "1", "1", "1", "1", "1" } },
                    { "large", new[] { "1.25", "1.2", "1.2", "1.15", "1.1" } }, { "huge", new[] { "1.5", "1.45", "1.4", "1.3", "1.2" } }, { "tv", new[] { "1.8", "1.7", "1.6", "1.5", "1.3" } } };
                if (!P.ContainsKey(p)) return; var ks = new[] { "ui", "hudS", "chatS", "mmS", "txtS" };
                for (int i = 0; i < 5; i++) AldaraSettings.SET[ks[i]] = P[p][i]; AldaraSettings.Set("ui", P[p][0]); AldaraHud.Banner("Interface: " + p);
            }
            else if (js.Contains("hudResetAll"))
            {
                foreach (var k in new[] { "ui", "hudS", "chatS", "mmS", "txtS", "hudA", "winA" }) AldaraSettings.SET[k] = "1"; AldaraSettings.SET["theme"] = "gold";
                foreach (var k in new[] { "showChat", "showQuest", "showParty", "showTarget", "showBuffs", "showPops", "showPlayer", "showZone" }) AldaraSettings.SET[k] = true;
                AldaraSettings.Set("ui", "1"); AldaraHud.Banner("Interface restored");
            }
            else if (js.Contains("hudEdit") || js.Contains("hudResetLayout")) AldaraHud.Banner("Moving the interface pieces comes with a later update");
            dirty = true;
        }
        void SetRow(VisualElement c, AldaraSettings.Item it)
        {
            string key = AldaraSettings.KeyOf(it.id); var cur = AldaraSettings.Get(key) ?? it.v;
            var r = Row(c, 8, Justify.SpaceBetween); Pad(r, 9, 0); BorderBottom(r, 1, C("#2a3040"));
            var lab = E(r); lab.style.flexShrink = 1; lab.style.flexGrow = 1;
            T(lab, it.t, 13, C("#f3e6c4"));
            if (!string.IsNullOrEmpty(it.hint)) { var hl = T(lab, it.hint, 11, C("#8a8474")); hl.style.marginTop = 2; }
            switch (it.c)
            {
                case "check":
                    bool on = cur != null && cur.Type == JTokenType.Boolean ? (bool)cur : AldaraSettings.On(key);
                    var cb = new Check(on); r.Add(cb); cb.RegisterCallback<ClickEvent>(e => { AldaraSettings.Set(key, !on); dirty = true; }); break;
                case "select":
                    string v = cur == null ? "" : cur.ToString(); var opt = it.opts.FirstOrDefault(o => o[0] == v) ?? it.opts[0];
                    var sel = E(r); sel.style.flexShrink = 0; sel.style.maxWidth = 210; Border(sel, 1, C("#6a4a1a"), 5); sel.style.backgroundColor = C("#120c06"); Pad(sel, 3, 6); sel.style.flexDirection = FlexDirection.Row; sel.style.alignItems = Align.Center;
                    T(sel, opt[1], 12, C("#f3e6c4"), false, false, false, 0, false, false); var ar = T(sel, "  ▾", 11, C("#c8b890"), false, false, false, 0, false, false);
                    sel.RegisterCallback<ClickEvent>(e =>
                    {
                        var m = new GenericDropdownMenu();
                        foreach (var o in it.opts) { string ov = o[0]; m.AddItem(o[1], ov == v, () => { AldaraSettings.Set(key, ov); dirty = true; }); }
                        m.DropDown(sel.worldBound, sel, DropdownMenuSizeMode.Auto);
                    });
                    break;
                case "range":
                    float val = AldaraSettings.F(key, cur == null ? 1 : (float)cur);
                    var sl = AldaraUI.Row(r, 8); sl.style.flexShrink = 0;
                    var rg = new Range(it.min, it.max, it.step, val); sl.Add(rg);
                    var vl = T(sl, Mathf.RoundToInt(val * 100) + "%", 11, C("#c8b890"), true, true, false, 0, false, false); vl.style.minWidth = 38; vl.style.unityTextAlign = TextAnchor.MiddleRight;
                    rg.changed = x => vl.text = Mathf.RoundToInt(x * 100) + "%";
                    rg.committed = x => AldaraSettings.Set(key, x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                    ApplyGapLater(sl); break;
            }
            ApplyGapLater(r);
        }

        // ---------- controls ----------
        void Controls(VisualElement c)
        {
            var top = Col(c, 6); top.style.marginBottom = 8;
            T(top, "Controls", 13, C("#e8c46a"), true, true, false, 1);
            T(top, AldaraSettings.S.kbNote ?? "", 12, C("#a8a090"));
            var rb = B(top, "Restore default keys", () => { AldaraKeys.ResetAll(); dirty = true; }, "btn", 12); rb.Padding(6, 14);
            ApplyGapLater(top);
            foreach (var A in AldaraKeys.ACTIONS)
            {
                string k = AldaraKeys.KeyOf(A.a); bool changed = k != A.d, listen = AldaraKeys.listening == A.a; string a = A.a;
                var r = Row(c, 8); Pad(r, 5, 4); BorderBottom(r, 1, C("#3a2a14")); if (listen) r.style.backgroundColor = C("#e8c46a", 0.08f);
                var n = T(r, A.n, 13, C("#e8dcc0")); n.style.flexGrow = 1;
                var kb = E(r); kb.style.minWidth = 96; Border(kb, 1, listen ? C("#ffe8a0") : changed ? C("#7fd8ff") : C("#6a4a1a"), 5); kb.style.backgroundColor = C("#120c06"); Pad(kb, 5, 10);
                var kl = T(kb, listen ? "Press a key" : AldaraKeys.Name(k), 12, changed ? C("#bfe8ff") : C("#ffe8a0"), true, true, false, 0, false, false); kl.style.unityTextAlign = TextAnchor.MiddleCenter;
                kb.RegisterCallback<ClickEvent>(e => { AldaraKeys.listening = listen ? null : a; dirty = true; });
                kb.RegisterCallback<MouseEnterEvent>(e => { if (!listen) kb.style.borderTopColor = kb.style.borderBottomColor = kb.style.borderLeftColor = kb.style.borderRightColor = C("#e8c46a"); });
                var rs = E(r); rs.style.width = 52;
                if (changed) { Border(rs, 1, C("#4a4f62"), 4); rs.style.backgroundColor = C("#161a26"); Pad(rs, 4, 6); var rl = T(rs, "Reset", 10, C("#a8a090"), true); rl.style.unityTextAlign = TextAnchor.MiddleCenter; rs.tooltip = "Back to " + AldaraKeys.Name(A.d); rs.RegisterCallback<ClickEvent>(e => { AldaraKeys.Set(a, AldaraKeys.Default(a)); dirty = true; }); }
                ApplyGapLater(r);
            }
        }
        public override void Tick()
        {
            if (AldaraKeys.listening == null) return; var kb = Keyboard.current; if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { AldaraKeys.listening = null; dirty = true; return; }
            if (kb.backspaceKey.wasPressedThisFrame) { AldaraKeys.Set(AldaraKeys.listening, ""); AldaraKeys.listening = null; dirty = true; return; }
            foreach (var c in kb.allKeys) if (c.wasPressedThisFrame) { var n = AldaraKeys.NameOfControl(c); if (n == null) continue; AldaraKeys.Set(AldaraKeys.listening, n); AldaraKeys.listening = null; dirty = true; return; }
        }

        /// the browser's checkbox with the gold accent colour
        class Check : VisualElement
        {
            readonly bool on; public Check(bool v) { on = v; style.width = style.height = 18; style.marginLeft = 4; style.marginRight = 3; style.flexShrink = 0; generateVisualContent += Draw; }
            void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D; var r = contentRect; float w = r.width, h = r.height;
                RR(p, 0.5f, 0.5f, w - 1, h - 1, 3); p.fillColor = on ? C("#e8c46a") : C("#f5f3ee"); p.Fill(); p.strokeColor = on ? C("#e8c46a") : C("#767676"); p.lineWidth = 1; p.Stroke();
                if (on) { p.strokeColor = C("#1a1208"); p.lineWidth = 2.2f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round; p.BeginPath(); p.MoveTo(new Vector2(w * 0.24f, h * 0.52f)); p.LineTo(new Vector2(w * 0.43f, h * 0.7f)); p.LineTo(new Vector2(w * 0.77f, h * 0.31f)); p.Stroke(); }
            }
        }
        static void RR(Painter2D p, float x, float y, float w, float h, float r)
        {
            p.BeginPath(); p.MoveTo(new Vector2(x + r, y)); p.ArcTo(new Vector2(x + w, y), new Vector2(x + w, y + h), r); p.ArcTo(new Vector2(x + w, y + h), new Vector2(x, y + h), r);
            p.ArcTo(new Vector2(x, y + h), new Vector2(x, y), r); p.ArcTo(new Vector2(x, y), new Vector2(x + w, y), r); p.ClosePath();
        }
        /// the browser's range slider with the gold accent colour
        class Range : VisualElement
        {
            readonly float min, max, step; float v; public System.Action<float> changed, committed; bool drag;
            public Range(float a, float b, float s, float val)
            {
                min = a; max = b; step = s > 0 ? s : 0.01f; v = val; style.width = 130; style.height = 18; generateVisualContent += Draw;
                RegisterCallback<PointerDownEvent>(e => { drag = true; this.CapturePointer(e.pointerId); Move(e.localPosition.x); });
                RegisterCallback<PointerMoveEvent>(e => { if (drag) Move(e.localPosition.x); });
                RegisterCallback<PointerUpEvent>(e => { if (!drag) return; drag = false; this.ReleasePointer(e.pointerId); if (committed != null) committed(v); });
            }
            void Move(float x)
            {
                float t = Mathf.Clamp01((x - 8) / Mathf.Max(1, contentRect.width - 16)); float nv = Mathf.Round((min + t * (max - min)) / step) * step; nv = Mathf.Clamp(nv, min, max);
                if (Mathf.Abs(nv - v) > 1e-5f) { v = nv; MarkDirtyRepaint(); if (changed != null) changed(v); }
            }
            void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D; var r = contentRect; float w = r.width, cy = r.height / 2, t = (v - min) / (max - min), x = 8 + t * (w - 16);
                p.lineCap = LineCap.Round; p.lineWidth = 4;
                p.strokeColor = C("#efefef"); p.BeginPath(); p.MoveTo(new Vector2(8, cy)); p.LineTo(new Vector2(w - 8, cy)); p.Stroke();
                p.strokeColor = C("#e8c46a"); p.BeginPath(); p.MoveTo(new Vector2(8, cy)); p.LineTo(new Vector2(x, cy)); p.Stroke();
                p.fillColor = C("#e8c46a"); p.BeginPath(); p.Arc(new Vector2(x, cy), 8, 0, 360); p.ClosePath(); p.Fill();
            }
        }
    }
}
