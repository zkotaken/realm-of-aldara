using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The browser's Subclass window (renderSub, renderSubTree): from level 30 a hero picks one of four paths of their class;
    // the path tree beneath it (buffs, trait upgrades, two abilities and a capstone) is bought with skill points.
    public class AldaraSubWin : AldaraWindows.Win
    {
        public AldaraSubWin() { id = "sub"; title = "Subclass"; }
        public override float Y => 78; public override float W => 1180; public override float MaxH => 800;
        static AldaraHero Hr { get { return AldaraHero.I; } }
        public string view = "tree"; string sel;
        public override void OnOpen() { }

        public override void Render(VisualElement body)
        {
            var S = AldaraTree.SubCur(); int SL = AldaraTree.B.SUB_LEVEL;
            if (S != null && Hr.lvl >= SL)
            {
                var tabs = Row(body, 6, Justify.Center); tabs.style.marginBottom = 10;
                Tab(tabs, S.name + " Path", view == "tree", () => { view = "tree"; dirty = true; });
                Tab(tabs, "Change subclass", view != "tree", () => { view = "cards"; dirty = true; });
                ApplyGapLater(tabs);
                if (view == "tree") { Tree(body, S); return; }
            }
            Cards(body);
        }
        static void ApplyGapLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }
        static void Tab(VisualElement p, string text, bool on, System.Action a)
        {
            var b = B(p, text, a, on ? "btn_gold" : "btn", 12); b.Padding(6, 14); if (on) b.label.style.color = C("#ffe8a0");
        }
        /// a subclass-coloured button (.sub-card button / .st-card button)
        static VisualElement ColBtn(VisualElement p, string text, Color sc, bool disabled, System.Action a)
        {
            var b = E(p); Border(b, 1, sc, 6); b.style.backgroundColor = Color.Lerp(C("#1a1a22"), sc, 0.3f); Pad(b, 8, 8);
            var l = T(b, text, 12, Color.white, true); l.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (disabled) b.style.opacity = 0.55f;
            else { b.RegisterCallback<ClickEvent>(e => a()); b.RegisterCallback<MouseEnterEvent>(e => b.style.backgroundColor = Color.Lerp(C("#1a1a22"), sc, 0.42f)); b.RegisterCallback<MouseLeaveEvent>(e => b.style.backgroundColor = Color.Lerp(C("#1a1a22"), sc, 0.3f)); }
            return b;
        }

        // ---------- the four paths ----------
        void Cards(VisualElement body)
        {
            var L = AldaraTree.SubList(); string cur = AldaraTree.sub; int SL = AldaraTree.B.SUB_LEVEL; bool locked = Hr.lvl < SL;
            var curS = AldaraTree.SubCur(); string cn = char.ToUpper(Hr.cls[0]) + Hr.cls.Substring(1);
            string head = locked ? "Subclasses open at level " + SL + ". You are level " + Hr.lvl + "."
                : curS != null ? "You walk the path of the " + Span("<b>" + curS.name + "</b>", "#ffe8a0") + ". Changing it costs " + AldaraTree.SubCost().ToString("N0") + " gold and refunds the points in your path tree."
                : "Choose the path your " + cn + " will walk. You can change it later for gold.";
            var h = T(body, head, 13, C("#c8b890")); h.style.unityTextAlign = TextAnchor.MiddleCenter; h.style.marginBottom = 12;
            var row = Row(body, 12, Justify.FlexStart, Align.Stretch);
            foreach (var S in L)
            {
                bool on = cur == S.id; var sc = C(S.col);
                var c = Col(row, 8); c.style.flexGrow = 1; c.style.flexBasis = 0; c.style.minWidth = 0; Pad(c, 16, 14); Border(c, 1, on ? sc : C("#2e3446"), 12);
                c.style.backgroundColor = Color.Lerp(C("#0e1120"), sc, 0.06f); if (locked) c.style.opacity = 0.75f;
                var ico = E(c); ico.style.width = ico.style.height = 64; ico.style.alignSelf = Align.Center; Border(ico, 2, sc, 32); ico.style.backgroundColor = C("#0a0c14");
                ico.style.alignItems = Align.Center; ico.style.justifyContent = Justify.Center;
                var g = E(ico); g.style.width = g.style.height = 36; AldaraSkillWin.Glyph(g, "sub_" + S.id, sc);
                var nm = T(c, S.name, 20, sc, true, true, false, 1, true); nm.style.unityTextAlign = TextAnchor.MiddleCenter; Shadow(nm, new Color(sc.r, sc.g, sc.b, 0.5f), 12, 0);
                var tg = T(c, S.tag, 12, C("#a8a090"), false, false, true); tg.style.unityTextAlign = TextAnchor.MiddleCenter; tg.style.marginTop = -4;
                Sec(c, sc, "Bonuses", S.effTxt); Sec(c, sc, "Trait", S.trait);
                Sec(c, sc, "Signature ability (no mana)", "<b><color=#ffffff>" + (string)S.skill["name"] + "</color></b>: " + (string)S.skill["desc"]);
                var sp = E(c); sp.style.flexGrow = 1;
                string sid = S.id;
                ColBtn(c, on ? "Your path" : locked ? "Level " + SL : cur != null ? "Change path" : "Choose " + S.name, sc, locked || on, () => AldaraTree.SubChoose(sid));
                ApplyGapLater(c);
            }
            ApplyGapLater(row);
        }
        static void Sec(VisualElement c, Color sc, string h, string text)
        {
            var s = E(c); var hl = T(s, h, 10, sc, true, false, false, 1.5f, true); hl.style.opacity = 0.9f; hl.style.marginBottom = 2;
            T(s, text, 12, C("#d8d0bc"));
        }

        // ---------- the path tree ----------
        static readonly string[][] LAYOUT = { new[] { "root" }, new[] { "p1", "p2" }, new[] { "a2" }, new[] { "p3", "p4" }, new[] { "a3" }, new[] { "p5", "p6" }, new[] { "c" } };
        static readonly string[] BAR = { "root", "a2", "a3", "c" }, BAR_KEYS = { "Z", "X", "R", "T" };
        void Tree(VisualElement body, AldaraTree.Sub S)
        {
            var N = S.nodes; var sc = C(S.col);
            if (sel == null || !N.Any(n => n.id == sel)) sel = N[0].id;
            var wrap = Row(body, 14, Justify.FlexStart, Align.Stretch);
            var box = E(wrap); box.style.flexGrow = 1; box.style.flexBasis = 0; box.style.minWidth = 0; box.style.height = 660; Border(box, 1, C("#2e3446"), 10); box.style.overflow = Overflow.Hidden;
            box.style.backgroundColor = C("#0b0d16");
            var tv = new PathView(this, S); tv.style.flexGrow = 1; box.Add(tv);
            var side = Col(wrap, 10); side.style.width = 250; side.style.flexShrink = 0;
            var spl = T(side, "<size=20><b>" + AldaraTree.sp + "</b></size> skill points", 13, C("#8ae8ff"), true); spl.style.unityTextAlign = TextAnchor.MiddleCenter;
            var n = N.First(x => x.id == sel); bool own = n.root || AldaraTree.SubHas(n.id);
            bool can = !own && AldaraTree.SubNodeReady(S, n) && Hr.lvl >= n.lvl && AldaraTree.sp >= n.cost;
            string kind = n.root ? "Signature ability" : n.key == "c" ? (n.pass != null ? "Capstone passive" : "Capstone ability") : n.sk != null ? "Ability" : n.mod != null ? "Trait upgrade" : "Buff";
            var card = Col(side, 6); Pad(card, 12, 12); Border(card, 1, sc, 10); card.style.backgroundColor = Color.Lerp(C("#10131e"), sc, 0.08f);
            T(card, kind, 10, sc, true, false, false, 1.5f, true);
            T(card, n.n, 17, Color.white, true, true);
            T(card, n.d, 13, C("#d8d0bc"));
            int bi = System.Array.IndexOf(BAR, n.key);
            if (n.sk != null) T(card, "No mana · " + (float)n.sk["cd"] + "s cooldown · subclass bar key " + (bi >= 0 ? BAR_KEYS[bi] : ""), 11, C("#8ab4ff"));
            else if (n.root) T(card, "No mana · " + AldaraTree.SubSkillCd(S) + "s cooldown · subclass bar key " + BAR_KEYS[0], 11, C("#8ab4ff"));
            T(card, n.root ? "Yours with the subclass" : own ? "Learned" : "Level " + n.lvl + (n.cost > 0 ? " · " + n.cost + " skill points" : ""), 11, C("#a8a090"));
            if (!own)
            {
                string label = Hr.lvl < n.lvl ? "Requires level " + n.lvl : !AldaraTree.SubNodeReady(S, n) ? "Unlock the step below first" : "Learn for " + n.cost + " points";
                string nid = n.id; var b = ColBtn(card, label, sc, !can, () => AldaraTree.SubUnlock(nid)); b.style.marginTop = 4;
            }
            ApplyGapLater(card);
            var note = T(side, "Subclass abilities sit on their own bar above your hotbar (Z, X, R, T). They cost no mana and never take a hotbar slot. Changing subclass refunds every point spent here.", 11, C("#7a7464"));
            note.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyGapLater(side); ApplyGapLater(wrap);
        }
        public void Select(string id) { sel = id; dirty = true; }
        public override void Tick() { foreach (var v in panel.Query<PathView>().ToList()) v.MarkDirtyRepaint(); }

        class PathView : VisualElement
        {
            readonly AldaraSubWin win; readonly AldaraTree.Sub S; const float W = 720, RowH = 92; float H => LAYOUT.Length * RowH + 20; float s = 1, ox, oy; bool built;
            public PathView(AldaraSubWin w, AldaraTree.Sub sub) { win = w; S = sub; generateVisualContent += Draw; RegisterCallback<GeometryChangedEvent>(e => Build()); }
            Vector2 Pos(AldaraTree.SubNode n) { int k = LAYOUT[n.tier].Length; return new Vector2(W / 2 + (n.col - (k - 1) / 2f) * 200, H - 40 - n.tier * RowH); }
            Vector2 Px(Vector2 v) { return new Vector2(ox + v.x * s, oy + v.y * s); }
            static float R(AldaraTree.SubNode n) { bool big = n.sk != null || n.root || n.key == "c"; return big ? (n.key == "c" ? 30 : 25) : 19; }
            void Build()
            {
                var r = contentRect; if (r.width <= 0) return; float ns = Mathf.Min(r.width / W, r.height / H);
                if (built && Mathf.Abs(ns - s) < 0.0001f) return; built = true; s = ns; ox = (r.width - W * s) / 2; oy = (r.height - H * s) / 2;
                Clear(); var sc = C(S.col);
                foreach (var n in S.nodes)
                {
                    var p = Px(Pos(n)); float rr = R(n) * s; bool own = n.root || AldaraTree.SubHas(n.id); bool ready = !own && AldaraTree.SubNodeReady(S, n) && AldaraHero.I.lvl >= n.lvl;
                    bool big = n.sk != null || n.root || n.key == "c"; string id = n.id;
                    var hit = new VisualElement(); hit.style.position = Position.Absolute; hit.style.left = p.x - rr; hit.style.top = p.y - rr; hit.style.width = hit.style.height = rr * 2; Add(hit);
                    hit.RegisterCallback<ClickEvent>(e => win.Select(id));
                    if (big)
                    {
                        var g = new VisualElement(); g.style.position = Position.Absolute; g.style.left = p.x - 12 * s; g.style.top = p.y - 12 * s; g.style.width = g.style.height = 24 * s;
                        AldaraSkillWin.Glyph(g, n.sk != null || n.root ? "sub_" + S.id : "_star", own ? Color.white : ready ? sc : C("#8a8474")); Add(g);
                    }
                    else Lbl(n.mod != null ? "T" : "+", 13, own ? Color.white : C("#8a8474"), p.x, p.y, true);
                    Lbl(n.n, 11, own ? sc : C("#d8d0bc"), p.x, p.y + (R(n) + 16 - 4) * s, false);
                    if (!own && n.cost > 0) Lbl(AldaraHero.I.lvl < n.lvl ? "Lv " + n.lvl : n.cost + " pt", 10, C("#8a8474"), p.x, p.y + (-R(n) - 8 - 4) * s, false);
                }
            }
            void Lbl(string t, float size, Color c, float cx, float cy, bool bold)
            {
                var l = T(this, t, size * s, c, true, bold, false, 0, true, false); l.style.position = Position.Absolute; l.style.width = 200 * s; l.style.left = cx - 100 * s; l.style.top = cy - size * s * 0.65f; l.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            void Draw(MeshGenerationContext ctx)
            {
                if (!built) return; var p = ctx.painter2D; var sc = C(S.col); float t = Time.unscaledTime;
                // the soft glow of the subclass colour from below
                for (int k = 0; k < 24; k++) { p.fillColor = new Color(sc.r, sc.g, sc.b, 0.007f); p.BeginPath(); p.Arc(new Vector2(contentRect.width / 2, contentRect.height), contentRect.height * (0.75f - k * 0.03f), 180, 360); p.ClosePath(); p.Fill(); }
                foreach (var n in S.nodes)
                {
                    if (n.tier == 0) continue; var a = Px(Pos(n));
                    foreach (var q in S.nodes.Where(x => x.tier == n.tier - 1))
                    {
                        var b = Px(Pos(q)); bool on = (q.root || AldaraTree.SubHas(q.id)) && AldaraTree.SubHas(n.id);
                        if (on) { Line(p, a, b, 8 * s, new Color(sc.r, sc.g, sc.b, 0.2f)); Line(p, a, b, 3 * s, sc); } else Line(p, a, b, 3 * s, C("#2e3446"));
                    }
                }
                foreach (var n in S.nodes)
                {
                    var c = Px(Pos(n)); float r = R(n) * s; bool own = n.root || AldaraTree.SubHas(n.id); bool ready = !own && AldaraTree.SubNodeReady(S, n) && AldaraHero.I.lvl >= n.lvl; bool isSel = win.sel == n.id;
                    if (own) for (int k = 0; k < 5; k++) Disc(p, c, r + (8 - k * 1.6f) * s, new Color(sc.r, sc.g, sc.b, 0.06f));
                    if (ready) { float ph = 0.5f + 0.5f * Mathf.Sin(t / 1.4f * Mathf.PI * 2); Ring(p, c, r + 6 * s, 1 * s, new Color(sc.r, sc.g, sc.b, 0.25f + 0.25f * ph)); }
                    Disc(p, c, r, own ? Color.Lerp(C("#0e1120"), sc, 0.3f) : C("#0e1120"));
                    float sw = (n.key == "c" ? 3.5f : own ? 3 : 2.5f) * s; Color st = own || ready ? sc : C("#3a3f52"); if (isSel) st = Color.white;
                    if (ready && !isSel) DashRing(p, c, r, sw, st, 4 * s, 3 * s); else Ring(p, c, r, sw, st);
                }
            }
            static void Line(Painter2D p, Vector2 a, Vector2 b, float w, Color c) { p.strokeColor = c; p.lineWidth = w; p.lineCap = LineCap.Round; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }
            static void Disc(Painter2D p, Vector2 c, float r, Color col) { p.fillColor = col; p.BeginPath(); p.Arc(c, r, 0, 360); p.ClosePath(); p.Fill(); }
            static void Ring(Painter2D p, Vector2 c, float r, float w, Color col) { p.strokeColor = col; p.lineWidth = w; p.BeginPath(); p.Arc(c, r, 0, 360); p.ClosePath(); p.Stroke(); }
            static void DashRing(Painter2D p, Vector2 c, float r, float w, Color col, float on, float off)
            {
                float circ = 2 * Mathf.PI * r, per = on + off; p.strokeColor = col; p.lineWidth = w;
                for (float d = 0; d < circ; d += per) { float a0 = d / r * Mathf.Rad2Deg, a1 = Mathf.Min(circ, d + on) / r * Mathf.Rad2Deg; p.BeginPath(); p.Arc(c, r, a0, a1); p.Stroke(); }
            }
        }
    }
}
