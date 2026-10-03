using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The browser's Skill Tree window (renderSkills): the class constellation with its root, three branches and apex,
    // lines that light up as you unlock along them (dashed where you can unlock next), node badges for key slot, cost and
    // level, and the card for the selected star (Unlock, Equip, Up, Down, Unequip), the passive bonus totals and Reforge.
    public class AldaraSkillWin : AldaraWindows.Win
    {
        public AldaraSkillWin() { id = "skills"; title = "Skill Tree"; }
        public override float Y => 78; public override float W => 900;
        static AldaraHero Hr { get { return AldaraHero.I; } }
        static AldaraSkills Sk { get { return AldaraSkills.I; } }
        string sel; TreeView view;

        public override void Render(VisualElement body)
        {
            var tree = AldaraTree.For(Hr.cls); var e = Sk.equipped;
            if (sel == null || !tree.byId.ContainsKey(sel)) sel = tree.root;
            // ---- top row ----
            var top = Row(body, 14); top.style.flexWrap = Wrap.Wrap; top.style.marginBottom = 4;
            T(top, Cap(Hr.cls) + " Path", 15, C("#e8c46a"), true, true, false, 1, false, false);
            var S = AldaraTree.SubCur(); int SL = AldaraTree.B.SUB_LEVEL;
            var sb = B(top, S != null ? "Subclass: " + S.name : Hr.lvl >= SL ? "Choose a subclass" : "Subclass at level " + SL, () => AldaraWindows.I.Toggle("sub", true), "btn", 11); sb.Padding(5, 14);
            var spl = T(top, "<size=18><b>" + AldaraTree.sp + "</b></size> skill points", 13, C("#8ae8ff"), false, false, false, 0, false, false);
            T(top, "Equipped <b>" + e.Count + "</b> / " + AldaraTree.B.MAX_EQUIPPED, 12, C("#c8c0a8"), false, false, false, 0, false, false);
            var gap = E(top); gap.style.flexGrow = 1;
            var rs = B(top, "Reforge (" + AldaraTree.RespecCost() + " g)", AldaraTree.Respec, "btn", 10); rs.tooltip = "Refund every point for gold";
            ApplyGapLater(top);
            var note = T(body, "You earn one skill point each level, and more for clearing a dungeon the first time. Click a star to see it; unlock along the lines. Equipped skills sit on keys 1 to 9 and 0.", 10.5f, C("#8f8a7c")); note.style.marginBottom = 6;
            // ---- tree and side ----
            var wrap = Row(body, 10, Justify.FlexStart, Align.Stretch);
            var left = E(wrap); left.style.flexGrow = 1; left.style.flexBasis = 0; left.style.minWidth = 0; Border(left, 1, C("#3a3f52"), 8); left.style.overflow = Overflow.Hidden;
            view = new TreeView(this, tree); left.Add(view);
            var right = Col(wrap, 8); right.style.width = 240; right.style.flexShrink = 0;
            Card(right, tree);
            var eff = Effects();
            if (eff.Length > 0)
            {
                var te = E(right); Border(te, 1, C("#2e3446"), 8); te.style.backgroundColor = C("#0d1018", 0.8f); Pad(te, 8, 10);
                var h = T(te, "Passive bonuses", 11, C("#e8c46a"), true); h.style.marginBottom = 3;
                T(te, eff, 11, C("#c8e8c8"));
            }
            ApplyGapLater(right); ApplyGapLater(wrap);
        }
        static void ApplyGapLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }
        static string Cap(string s) { return char.ToUpper(s[0]) + s.Substring(1); }
        static readonly Dictionary<string, string> EFF_NAME = new Dictionary<string, string> { { "atk", "Attack" }, { "hp", "Health" }, { "mana", "Mana" }, { "cdr", "Cooldowns" }, { "crit", "Crit" }, { "spd", "Speed" }, { "ls", "Lifesteal" }, { "dmg", "Damage" }, { "block", "Block" }, { "mreg", "Mana regen" }, { "xp", "Experience" }, { "gold", "Gold" }, { "mcost", "Mana cost" } };
        static string Effects()
        {
            var p = new List<string>();
            foreach (var k in AldaraTree.TE_KEYS) { float v = AldaraTree.T(k); if (v <= 0) continue; p.Add(EFF_NAME[k] + " " + (k == "cdr" || k == "block" || k == "mcost" ? "-" : "+") + Mathf.RoundToInt(v * 100) + "%"); }
            return string.Join(" · ", p);
        }
        static readonly Dictionary<string, string> KIND = new Dictionary<string, string> { { "target", "Attack" }, { "self_aoe", "Area" }, { "buff", "Buff" }, { "heal", "Heal" }, { "shield", "Barrier" }, { "blink", "Movement" } };
        void Card(VisualElement right, AldaraTree.Tree tree)
        {
            var n = tree.byId[sel]; bool un = AldaraTree.Unlocked(n.id), can = AldaraTree.CanUnlock(n); var sk = n.kind == "active" ? Sk.Get(n.id) : null; int slot = sk != null ? Sk.equipped.IndexOf(n.id) : -1;
            var col = C(n.col);
            var c = E(right); Border(c, 1, col, 8); c.style.backgroundColor = C("#10131f"); Pad(c, 10, 10);
            var head = Row(c, 8); head.style.marginBottom = 6;
            var ico = new Octagon(col) { pickingMode = PickingMode.Ignore }; ico.style.width = ico.style.height = 26; ico.style.flexShrink = 0; head.Add(ico);
            var g = E(ico); g.style.position = Position.Absolute; g.style.left = g.style.top = 4; g.style.width = g.style.height = 18; Glyph(g, n.glyph, C("#1a1208"));
            var hn = E(head);
            T(hn, AldaraTree.NodeName(n), 14, Color.white, true, true);
            string kind = sk != null ? (KIND.ContainsKey(sk.kind ?? "") ? KIND[sk.kind] : "Skill") : "Passive";
            T(hn, (n.bname ?? "Foundation") + " · " + kind + (n.tier == 6 ? " · Apex" : ""), 10, col, false, false, false, 0.5f, true);
            ApplyGapLater(head);
            T(c, sk != null ? sk.desc : n.desc, 12, C("#d8d4c8"));
            if (sk != null) { var m = T(c, sk.cost + " mana · " + Num(sk.cd) + "s cooldown", 10, C("#88aa88")); m.style.marginTop = 2; }
            if (n.req.Length > 0)
            {
                var parts = n.req.Select(id => { var rn = tree.byId[id]; return Span(AldaraTree.NodeName(rn), AldaraTree.Unlocked(id) ? "#7fd07f" : "#c05a5a"); });
                var r = T(c, "Requires " + string.Join(n.anyReq ? " or " : ", ", parts), 10.5f, C("#9a948a")); r.style.marginTop = 6;
            }
            var st = T(c, un ? (slot >= 0 ? "Equipped in slot " + ((slot + 1) % 10) : sk != null ? "Learned, not equipped" : "Active") : can ? "Ready to unlock" : "Locked", 10.5f, C("#8ae8ff"), true); st.style.marginTop = 6;
            var btns = Row(c, 5, Justify.FlexEnd); btns.style.marginTop = 6;
            if (!un)
            {
                string label = n.lvl > 0 && Hr.lvl < n.lvl ? "Requires level " + n.lvl : n.cost > 0 ? "Unlock for " + n.cost + " point" + (n.cost > 1 ? "s" : "") : "Learned";
                var b = B(btns, label, () => AldaraTree.Unlock(n.id), "btn_buy"); b.Disabled = !(can && AldaraTree.sp >= n.cost);
            }
            else if (sk != null)
            {
                var e = Sk.equipped; string id = sk.id;
                if (slot >= 0)
                {
                    B(btns, "Up", () => Move(id, -1)).Disabled = slot == 0;
                    B(btns, "Down", () => Move(id, 1)).Disabled = slot == e.Count - 1;
                    B(btns, "Unequip", () => { Sk.equipped.Remove(id); if (Sk.queued == id) Sk.queued = null; AldaraSave.Dirty(); });
                }
                else
                {
                    var b = B(btns, "Equip", () => { if (Sk.equipped.Count >= AldaraTree.B.MAX_EQUIPPED) { AldaraHud.Banner("All " + AldaraTree.B.MAX_EQUIPPED + " skill slots are full"); return; } Sk.equipped.Add(id); AldaraSave.Dirty(); }, "btn_equip");
                    b.Disabled = e.Count >= AldaraTree.B.MAX_EQUIPPED; if (b.Disabled) b.tooltip = "All slots full";
                }
            }
            ApplyGapLater(btns);
        }
        static string Num(float v) { return v == Mathf.Floor(v) ? ((int)v).ToString() : v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }
        static void Move(string id, int dir)
        {
            var e = Sk.equipped; int i = e.IndexOf(id), j = i + dir; if (i < 0 || j < 0 || j >= e.Count) return;
            var t = e[i]; e[i] = e[j]; e[j] = t; AldaraSave.Dirty();
        }
        public static void Glyph(VisualElement e, string glyph, Color tint)
        {
            var t = Resources.Load<Texture2D>("UI/tree/g_" + glyph) ?? Resources.Load<Texture2D>("UI/tree/g__star");
            if (t) e.style.backgroundImage = Background.FromTexture2D(t); e.style.unityBackgroundImageTintColor = tint; e.pickingMode = PickingMode.Ignore;
        }
        public void Select(string id) { sel = id; dirty = true; }
        public override void Tick() { if (view != null) view.MarkDirtyRepaint(); }

        // ---------- the constellation ----------
        class TreeView : VisualElement
        {
            readonly AldaraSkillWin win; readonly AldaraTree.Tree tr; float s = 1; string hover;
            readonly Dictionary<string, VisualElement> icons = new Dictionary<string, VisualElement>();
            public TreeView(AldaraSkillWin w, AldaraTree.Tree t)
            {
                win = w; tr = t; style.width = Length.Percent(100);
                var bg = Resources.Load<Texture2D>("UI/tree/treebg_" + AldaraHero.I.cls); if (bg) style.backgroundImage = Background.FromTexture2D(bg);
                generateVisualContent += Draw;
                RegisterCallback<GeometryChangedEvent>(e => Layout());
            }
            static float R(AldaraTree.Node n) { return n.kind == "active" ? (n.tier == 6 ? 26 : n.tier == 0 ? 24 : 21) : 15; }
            void Layout()
            {
                float w = resolvedStyle.width; if (w <= 0) return; float ns = w / tr.W; float h = tr.H * ns;
                if (Mathf.Abs(resolvedStyle.height - h) > 0.5f) style.height = h;
                if (Mathf.Abs(ns - s) < 0.0001f && icons.Count > 0) return; s = ns;
                Clear(); icons.Clear();
                var e = AldaraSkills.I.equipped; int lvl = AldaraHero.I.lvl;
                foreach (var n in tr.nodes)
                {
                    float r = R(n); bool un = AldaraTree.Unlocked(n.id), can = AldaraTree.CanUnlock(n); string id = n.id;
                    var hit = new VisualElement(); hit.style.position = Position.Absolute; hit.style.left = (n.x - r) * s; hit.style.top = (n.y - r) * s; hit.style.width = hit.style.height = 2 * r * s; Add(hit);
                    hit.RegisterCallback<ClickEvent>(ev => win.Select(id));
                    hit.RegisterCallback<MouseEnterEvent>(ev => hover = id); hit.RegisterCallback<MouseLeaveEvent>(ev => { if (hover == id) hover = null; });
                    float isz = r * 1.24f * s; var ic = new VisualElement(); ic.style.position = Position.Absolute; ic.style.left = (n.x - r * 0.62f) * s; ic.style.top = (n.y - r * 0.62f) * s; ic.style.width = ic.style.height = isz;
                    Glyph(ic, n.glyph, un ? Color.white : can ? C("#c8c8d8") : C("#565b70")); Add(ic); icons[id] = ic;
                    int slot = n.kind == "active" ? e.IndexOf(id) : -1;
                    if (slot >= 0) Badge(n.x + r * 0.72f, n.y - r * 0.72f, ((slot + 1) % 10).ToString(), 10, C("#2a1a08"), 16, 16);
                    if (!un && n.cost > 0) Badge(n.x, n.y + r + 11 - 0.5f, n.cost.ToString(), 9, can ? C("#8ae8ff") : C("#6a6f82"), 22, 13);
                    if (!un && n.lvl > 0 && lvl < n.lvl) Badge(n.x, n.y - r - 9 - 0.5f, "Lv " + n.lvl, 9, C("#ffb0a0"), 34, 13);
                }
            }
            void Badge(float cx, float cy, string text, float size, Color col, float w, float h)
            {
                var l = T(this, text, size * s, col, true, true, false, 0, false, false); l.style.position = Position.Absolute;
                l.style.left = (cx - w / 2) * s; l.style.top = (cy - h / 2) * s; l.style.width = w * s; l.style.height = h * s; l.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            void Draw(MeshGenerationContext ctx)
            {
                if (s <= 0) return; var p = ctx.painter2D; float t = Time.unscaledTime;
                Vector2 P(float x, float y) { return new Vector2(x * s, y * s); }
                // edges
                foreach (var n in tr.nodes) foreach (var rid in n.req)
                    {
                        AldaraTree.Node q; if (!tr.byId.TryGetValue(rid, out q)) continue;
                        bool lit = AldaraTree.Unlocked(n.id), open = !lit && AldaraTree.CanUnlock(n) && AldaraTree.Unlocked(rid); var col = C(n.col);
                        Vector2 a = P(q.x, q.y), b = P(n.x, n.y);
                        if (lit) { Line(p, a, b, 9 * s, new Color(col.r, col.g, col.b, 0.18f)); Line(p, a, b, 5.5f * s, new Color(col.r, col.g, col.b, 0.3f)); Line(p, a, b, 3 * s, col); }
                        else if (open) Dashed(p, a, b, 2.5f * s, new Color(col.r, col.g, col.b, 0.45f), 4 * s, 5 * s, -(t / 1.4f) * 9 * s);
                        else Line(p, a, b, 2.5f * s, C("#2e3446"));
                    }
                // nodes
                foreach (var n in tr.nodes)
                {
                    float r = R(n); bool un = AldaraTree.Unlocked(n.id), can = AldaraTree.CanUnlock(n), isSel = win.sel == n.id; var col = C(n.col); var c = P(n.x, n.y);
                    if (un) for (int k = 0; k < 6; k++) Disc(p, c, (r + 9 + 6 - k * 2.2f) * s, new Color(col.r, col.g, col.b, 0.22f / 6 * 1.6f));
                    if (can) { float ph = (t % 1.6f) / 1.6f; Ring(p, c, (r + 6) * s * (1 + ph * 0.35f), 1.5f * s, new Color(col.r, col.g, col.b, 0.6f * (1 - ph))); }
                    Color fill = un ? col : C("#0e1120"), stroke = un ? C("#fff8e0") : can ? col : C("#3a3f52"); float sw = un ? 2 : can ? 2.2f : n.kind == "active" ? 2.4f : 2;
                    if (hover == n.id) stroke = C("#ffe8a0"); if (isSel) { stroke = Color.white; sw = 3; }
                    if (un) Disc(p, c, (r + 2.5f) * s, new Color(col.r, col.g, col.b, 0.35f));
                    Disc(p, c, r * s, fill); Ring(p, c, r * s, sw * s, stroke);
                    Disc(p, c, (r - 3) * s, un ? new Color(0, 0, 0, 0.28f) : C("#161a2c"));
                    int slot = n.kind == "active" ? AldaraSkills.I.equipped.IndexOf(n.id) : -1;
                    if (slot >= 0) { var b = P(n.x + r * 0.72f, n.y - r * 0.72f); Disc(p, b, 8 * s, C("#ffd35a")); Ring(p, b, 8 * s, 1.2f * s, C("#3a2a10")); }
                    if (!un && n.cost > 0) RRect(p, P(n.x - 11, n.y + r + 11 - 7), 22 * s, 13 * s, 4 * s, C("#0d1018", 0.9f), can ? C("#8ae8ff") : C("#4a4f62"));
                    if (!un && n.lvl > 0 && AldaraHero.I.lvl < n.lvl) RRect(p, P(n.x - 17, n.y - r - 9 - 7), 34 * s, 13 * s, 4 * s, C("#2a1212"), C("#a04030"));
                }
            }
            static void Line(Painter2D p, Vector2 a, Vector2 b, float w, Color c) { p.strokeColor = c; p.lineWidth = w; p.lineCap = LineCap.Round; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }
            static void Dashed(Painter2D p, Vector2 a, Vector2 b, float w, Color c, float on, float off, float phase)
            {
                float L = Vector2.Distance(a, b); if (L <= 0) return; var d = (b - a) / L; float per = on + off; float st = ((phase % per) + per) % per - per;
                p.strokeColor = c; p.lineWidth = w; p.lineCap = LineCap.Round;
                for (float x = st; x < L; x += per) { float x0 = Mathf.Max(0, x), x1 = Mathf.Min(L, x + on); if (x1 <= x0) continue; p.BeginPath(); p.MoveTo(a + d * x0); p.LineTo(a + d * x1); p.Stroke(); }
            }
            static void Disc(Painter2D p, Vector2 c, float r, Color col) { p.fillColor = col; p.BeginPath(); p.Arc(c, r, 0, 360); p.ClosePath(); p.Fill(); }
            static void Ring(Painter2D p, Vector2 c, float r, float w, Color col) { p.strokeColor = col; p.lineWidth = w; p.BeginPath(); p.Arc(c, r, 0, 360); p.ClosePath(); p.Stroke(); }
            static void RRect(Painter2D p, Vector2 o, float w, float h, float r, Color fill, Color stroke)
            {
                p.BeginPath(); p.MoveTo(o + new Vector2(r, 0)); p.ArcTo(o + new Vector2(w, 0), o + new Vector2(w, h), r); p.ArcTo(o + new Vector2(w, h), o + new Vector2(0, h), r);
                p.ArcTo(o + new Vector2(0, h), o, r); p.ArcTo(o, o + new Vector2(w, 0), r); p.ClosePath(); p.fillColor = fill; p.Fill(); p.strokeColor = stroke; p.lineWidth = 1; p.Stroke();
            }
        }
        /// the card's octagon icon plate: the node colour, lighter at the top
        class Octagon : VisualElement
        {
            readonly Color c; public Octagon(Color col) { c = col; generateVisualContent += Draw; }
            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect; float w = r.width, h = r.height; var p = ctx.painter2D;
                p.fillColor = Color.Lerp(c, Color.black, 0.25f);
                p.BeginPath(); p.MoveTo(new Vector2(w * 0.3f, 0)); p.LineTo(new Vector2(w * 0.7f, 0)); p.LineTo(new Vector2(w, h * 0.3f)); p.LineTo(new Vector2(w, h * 0.7f)); p.LineTo(new Vector2(w * 0.7f, h)); p.LineTo(new Vector2(w * 0.3f, h)); p.LineTo(new Vector2(0, h * 0.7f)); p.LineTo(new Vector2(0, h * 0.3f)); p.ClosePath(); p.Fill();
                p.fillColor = Color.Lerp(c, Color.white, 0.25f); p.BeginPath(); p.Arc(new Vector2(w / 2, h * 0.4f), w * 0.3f, 0, 360); p.ClosePath(); p.Fill();
                p.fillColor = c; p.BeginPath(); p.Arc(new Vector2(w / 2, h * 0.5f), w * 0.36f, 0, 360); p.ClosePath(); p.Fill();
            }
        }
    }
}
