using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The browser's Guild window (renderGuild): My Guild (the tavern, the emblem and description, joining rules, requests,
    // members, leave or disband), the Guild Browser (search, join or ask) and Found a Guild (the emblem maker and the form).
    public class AldaraGuildWin : AldaraWindows.Win
    {
        public AldaraGuildWin() { id = "guild"; title = "Guilds"; }
        public override float W => 820; public override float MaxH => 760;
        string tab = "browse", q = ""; bool userTab, confirmDisband, typing;
        string dName = "", dTag = "", dDesc = "", dMode = "open"; AldaraGuild.Icon dIcon = new AldaraGuild.Icon();
        public override void OnOpen() { if (AldaraGuild.Mine() != null && tab == "browse" && !userTab) tab = "mine"; confirmDisband = false; }

        /// an emblem drawn from the guild's icon
        public class Emblem : VisualElement
        {
            public AldaraGuild.Icon icon; float size;
            public Emblem(AldaraGuild.Icon ic, float s) { icon = ic; size = s; style.width = style.height = s; style.flexShrink = 0; pickingMode = PickingMode.Ignore; generateVisualContent += ctx => AldaraGuild.Paint(ctx.painter2D, icon, Vector2.zero, size); }
        }
        static void Gb(VisualElement p, string text, System.Action a, bool gold = false, bool warn = false, bool disabled = false, float size = 11)
        {
            var b = E(p); Pad(b, 6, 11); Border(b, 1, gold ? C("#c8a050") : warn ? C("#a04030") : C("#4a4f62"), 5); b.style.backgroundColor = gold ? C("#3a3016") : warn ? C("#2a1210") : C("#161a26");
            var l = T(b, text, size, gold ? C("#ffe8a0") : warn ? C("#ffb0a0") : C("#d8d0bc"), true); l.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (disabled) b.style.opacity = 0.45f;
            else { b.RegisterCallback<ClickEvent>(e => { a(); e.StopPropagation(); }); b.RegisterCallback<MouseEnterEvent>(e => Border(b, 1, C("#e8c46a"), 5)); b.RegisterCallback<MouseLeaveEvent>(e => Border(b, 1, gold ? C("#c8a050") : warn ? C("#a04030") : C("#4a4f62"), 5)); }
        }
        static TextField Field(VisualElement p, string val, int max, bool multi, System.Action<string> set)
        {
            var f = new TextField { value = val, maxLength = max, multiline = multi }; p.Add(f); f.style.marginLeft = f.style.marginRight = 0;
            var ti = f.Q(TextField.textInputUssName); if (ti != null) { ti.style.backgroundColor = C("#0a0c12"); Border(ti, 1, C("#3a3f52"), 6); ti.style.color = C("#e8e4d8"); ti.style.unityFontDefinition = FontDefinition.FromFont(Font(false, false)); ti.style.fontSize = 13; Pad(ti, 6, 9); if (multi) ti.style.minHeight = 70; }
            f.RegisterCallback<FocusInEvent>(e => AldaraHud.Typing = true); f.RegisterCallback<FocusOutEvent>(e => AldaraHud.Typing = false);
            f.RegisterValueChangedCallback(e => set(e.newValue)); return f;
        }
        static Label H2(VisualElement p, string t) { var l = T(p, t.ToUpper(), 11, C("#e8c46a"), true, false, false, 1.5f); l.style.marginTop = 8; l.style.marginBottom = 5; return l; }
        void Go(string t) { tab = t; userTab = true; confirmDisband = false; dirty = true; }

        public override void Render(VisualElement body)
        {
            var mine = AldaraGuild.Mine();
            var tabs = Row(body, 6); BorderBottom(tabs, 1, C("#3a3f52")); tabs.style.paddingBottom = 8; tabs.style.marginBottom = 10;
            foreach (var t in new[] { new[] { "mine", "My Guild" }, new[] { "browse", "Guild Browser" }, new[] { "create", "Found a Guild" } })
            {
                bool on = tab == t[0]; var b = E(tabs); Pad(b, 7, 14); Border(b, 1, on ? C("#8a7440") : C("#3a3f52"), 0); b.style.borderBottomWidth = 0; Radius(b, 0); b.style.borderTopLeftRadius = b.style.borderTopRightRadius = 6;
                b.style.backgroundColor = on ? C("#2a2618") : C("#0d1018"); T(b, t[1], 12, on ? C("#ffe8a0") : C("#a8a090"), true, true, false, 1); string k = t[0]; b.RegisterCallback<ClickEvent>(e => Go(k));
            }
            var sp = E(tabs); sp.style.flexGrow = 1; var md = E(tabs); Border(md, 1, C("#3a4050"), 0); Pad(md, 1, 7); T(md, "THIS DEVICE", 9, C("#8a9ab0"), true, false, false, 1);
            ApplyGap(tabs);
            if (tab == "mine") Mine(body, mine); else if (tab == "create") Create(body, mine); else Browse(body, mine);
        }
        void Card(VisualElement p, AldaraGuild.G g, AldaraGuild.G mine)
        {
            var gc = C(g.icon != null ? g.icon.rim : "#e8c46a"); var c = Row(p, 12); Pad(c, 10, 12); Border(c, 1, C("#2e3446"), 8); c.style.backgroundColor = C("#11141f"); c.style.marginBottom = 6;
            c.RegisterCallback<MouseEnterEvent>(e => Border(c, 1, gc, 8)); c.RegisterCallback<MouseLeaveEvent>(e => Border(c, 1, C("#2e3446"), 8));
            c.Add(new Emblem(g.icon, 54));
            var info = Col(c); info.style.flexGrow = 1; info.style.minWidth = 0;
            var nm = T(info, g.name + " " + Span("[" + g.tag + "]", Hex(gc)), 15, C("#f4ecd8"), true, true); nm.enableRichText = true;
            int n = g.members != null ? g.members.Count : 0; string ns = g.npc ? "∞" : n.ToString();
            T(info, ns + " member" + (n == 1 && !g.npc ? "" : "s") + " · " + (g.mode == "open" ? "Open to all" : "Request to join") + " · Led by " + (g.leader != null ? g.leader.name : "") + (g.npc ? " · Realm order" : ""), 10.5f, C("#8a8474")).style.marginTop = 2;
            T(info, string.IsNullOrEmpty(g.desc) ? "No description" : g.desc, 12, C("#c8c0ac"), false, false, string.IsNullOrEmpty(g.desc)).style.marginTop = 4;
            var act = E(c); act.style.flexShrink = 0;
            bool inIt = mine != null && mine.id == g.id, req = AldaraGuild.Requested(g); string gid = g.id;
            if (inIt) { var bd = E(act); Border(bd, 1, C("#4a9a5a"), 10); Pad(bd, 3, 9); T(bd, "Your guild", 10, C("#8aff9a"), true); }
            else if (mine != null) Gb(act, g.mode == "open" ? "Join" : "Request", null, false, false, true);
            else if (req) Gb(act, "Cancel request", () => { AldaraGuild.CancelRequest(gid); dirty = true; });
            else Gb(act, g.mode == "open" ? "Join" : "Request to join", () => { AldaraGuild.Join(gid); if (AldaraGuild.Mine() != null) tab = "mine"; dirty = true; }, true);
        }
        void Browse(VisualElement body, AldaraGuild.G mine)
        {
            var sf = Field(body, q, 60, false, v => { q = v; }); sf.style.marginBottom = 10; sf.RegisterCallback<KeyUpEvent>(e => { typing = true; dirty = true; });
            if (typing) { typing = false; sf.schedule.Execute(() => { sf.Focus(); sf.SelectRange(q.Length, q.Length); }); }
            string ql = (q ?? "").ToLowerInvariant();
            var L = AldaraGuild.All().Values.Where(g => ql == "" || g.name.ToLowerInvariant().Contains(ql) || (g.tag ?? "").ToLowerInvariant().Contains(ql) || (g.desc ?? "").ToLowerInvariant().Contains(ql)).ToList();
            L.Sort((a, b) => { int c = (a.npc ? 1 : 0) - (b.npc ? 1 : 0); return c != 0 ? c : (b.members?.Count ?? 0).CompareTo(a.members?.Count ?? 0); });
            if (L.Count == 0) { var e = T(body, "No guilds match.", 13, C("#8a8474")); e.style.unityTextAlign = TextAnchor.MiddleCenter; e.style.marginTop = 20; }
            foreach (var g in L) Card(body, g, mine);
        }
        void Mine(VisualElement body, AldaraGuild.G g)
        {
            if (g == null)
            {
                var e = Col(body, 10); e.style.alignItems = Align.Center; e.style.marginTop = 24; T(e, "You are not in a guild yet.", 13, C("#a8a090"));
                var r = Row(e, 8); Gb(r, "Browse guilds", () => Go("browse"), true); Gb(r, "Found your own", () => Go("create")); ApplyGap(r); ApplyGap(e); return;
            }
            string me = AldaraGuild.Uid(); bool lead = g.leader != null && g.leader.uid == me; var gc = C(g.icon != null ? g.icon.rim : "#e8c46a");
            var mem = (g.members ?? new System.Collections.Generic.List<AldaraGuild.Member>()).OrderByDescending(m => m.uid == g.leader.uid).ThenByDescending(m => m.lvl).ToList();
            bool inT = AldaraFort.On, busy = AldaraDungeon.Active && !AldaraFort.On;
            var tv = Row(body, 12, Justify.SpaceBetween); Pad(tv, 12, 14); Border(tv, 1, C("#8a6a30"), 10); tv.style.backgroundColor = C("#1e1810"); tv.style.marginBottom = 10;
            var tvi = Col(tv); T(tvi, "Guild Tavern", 15, C("#ffe8a0"), true, true); T(tvi, "A two-storey tavern for " + g.name + ". Members only.", 12, C("#c8b890"));
            if (inT) Gb(tv, "Leave the tavern", AldaraFort.Leave); else Gb(tv, "Enter the tavern", AldaraFort.Enter, true, false, busy);
            var hero = Row(body, 16, Justify.FlexStart, Align.FlexStart); Pad(hero, 14, 14); Border(hero, 1, gc, 10); hero.style.backgroundColor = C("#0d1018"); hero.style.marginBottom = 10;
            hero.Add(new Emblem(g.icon, 96));
            var hc = Col(hero); hc.style.flexGrow = 1; T(hc, g.name, 22, C("#f4ecd8"), true, true);
            T(hc, "[" + g.tag + "] · " + (g.npc ? "Realm order" : mem.Count + " member" + (mem.Count == 1 ? "" : "s")) + " · " + (g.mode == "open" ? "Open to all" : "Request to join") + " · Led by " + g.leader.name, 11, gc);
            if (lead) { string d = g.desc; var df = Field(hc, d ?? "", 220, true, v => d = v); df.style.marginTop = 6; df.style.marginBottom = 6; Gb(hc, "Save description", () => { AldaraGuild.SaveDesc(d); dirty = true; }); }
            else T(hc, string.IsNullOrEmpty(g.desc) ? "No description" : g.desc, 13, C("#c8c0ac"), false, false, string.IsNullOrEmpty(g.desc)).style.marginTop = 6;
            if (lead)
            {
                var s1 = Sec(body, "Joining"); var seg = Row(s1, 0); Border(seg, 1, C("#3a3f52"), 6);
                foreach (var m in new[] { new[] { "open", "Auto accept" }, new[] { "request", "Request to join" } }) { bool on = g.mode == m[0]; var b = E(seg); Pad(b, 6, 11); b.style.backgroundColor = on ? C("#3a3016") : C("#161a26"); T(b, m[1], 11, on ? C("#ffe8a0") : C("#d8d0bc"), true); string k = m[0]; b.RegisterCallback<ClickEvent>(e => { AldaraGuild.SetMode(k); dirty = true; }); }
                var R = g.requests ?? new System.Collections.Generic.List<AldaraGuild.Member>(); var s2 = Sec(body, "Requests (" + R.Count + ")");
                if (R.Count == 0) T(s2, "No one is waiting.", 12, C("#8a8474"));
                foreach (var r in R) { var row = MRow(s2, r, g, me); string u = r.uid; Gb(row, "Accept", () => { AldaraGuild.Answer(u, true); dirty = true; }, true); Gb(row, "Decline", () => { AldaraGuild.Answer(u, false); dirty = true; }); ApplyGap(row); }
            }
            if (!g.npc)
            {
                var s3 = Sec(body, "Members");
                foreach (var m in mem) { var row = MRow(s3, m, g, me); string u = m.uid; if (lead && m.uid != g.leader.uid) Gb(row, "Remove", () => { AldaraGuild.Kick(u); dirty = true; }); ApplyGap(row); }
            }
            var foot = Row(body, 8, Justify.FlexEnd); foot.style.marginTop = 6;
            Gb(foot, "Leave guild", () => { AldaraGuild.Leave(); if (AldaraGuild.Mine() == null) tab = "browse"; dirty = true; });
            if (lead) Gb(foot, confirmDisband ? "Confirm: disband forever" : "Disband guild", () => { if (!confirmDisband) { confirmDisband = true; dirty = true; return; } confirmDisband = false; AldaraGuild.Disband(); tab = "browse"; dirty = true; }, false, true);
            ApplyGap(foot); ApplyGap(hero); ApplyGap(tv);
        }
        static VisualElement Sec(VisualElement p, string h) { var s = Col(p); Border(s, 1, C("#2e3446"), 8); Pad(s, 10, 10); s.style.marginBottom = 8; s.style.backgroundColor = C("#0d1018", 0.8f); var l = H2(s, h); l.style.marginTop = 0; return s; }
        static VisualElement MRow(VisualElement p, AldaraGuild.Member m, AldaraGuild.G g, string me)
        {
            var row = Row(p, 8); Pad(row, 5, 0); BorderBottom(row, 1, C("#1e2230"));
            var n = T(row, m.name + (m.uid == g.leader.uid ? "  " + Span("Leader", "#e8c46a") : "") + (m.uid == me ? "  " + Span("You", "#8aff9a") : ""), 13, C("#f4ecd8"), true, true); n.enableRichText = true; n.style.flexGrow = 1;
            string cn = string.IsNullOrEmpty(m.cls) ? "" : char.ToUpper(m.cls[0]) + m.cls.Substring(1); T(row, "Level " + m.lvl + " " + cn, 11, C("#8a8474")); return row;
        }
        void Create(VisualElement body, AldaraGuild.G mine)
        {
            if (mine != null) { var e = T(body, "You already belong to " + mine.name + ". Leave it to found your own.", 13, C("#a8a090")); e.style.unityTextAlign = TextAnchor.MiddleCenter; e.style.marginTop = 20; return; }
            var H = AldaraHero.I; bool can = H.gold >= AldaraGuild.COST;
            var wrap = Row(body, 16, Justify.FlexStart, Align.FlexStart);
            var mk = Col(wrap); mk.style.width = 340; mk.style.flexShrink = 0; Border(mk, 1, C("#2e3446"), 10); Pad(mk, 12, 12); mk.style.backgroundColor = C("#0d1018");
            var pv = Col(mk, 6); pv.style.alignItems = Align.Center; var em = new Emblem(dIcon, 150); pv.Add(em);
            var pn = T(pv, (dName == "" ? "Your Guild" : dName) + " " + Span("[" + (dTag == "" ? "TAG" : dTag.ToUpper()) + "]", "#e8c46a"), 15, C("#f4ecd8"), true, true); pn.enableRichText = true; ApplyGap(pv);
            System.Action refresh = () => { em.MarkDirtyRepaint(); pn.text = (dName == "" ? "Your Guild" : dName) + " " + Span("[" + (dTag == "" ? "TAG" : dTag.ToUpper()) + "]", "#e8c46a"); };
            H2(mk, "Shape"); Picks(mk, AldaraGuild.SHAPES, "shape", true, refresh);
            H2(mk, "Symbol"); Picks(mk, AldaraGuild.SYMS, "sym", false, refresh);
            H2(mk, "Field colour"); Swatches(mk, "bg", refresh); H2(mk, "Symbol colour"); Swatches(mk, "fg", refresh); H2(mk, "Rim colour"); Swatches(mk, "rim", refresh);
            var rb = E(mk); rb.style.marginTop = 8; Gb(rb, "Random emblem", () => { dIcon = AldaraGuild.RandomIcon(); dirty = true; });
            var form = Col(wrap, 8); form.style.flexGrow = 1;
            H2(form, "Guild name"); Field(form, dName, 24, false, v => { dName = v; refresh(); });
            H2(form, "Tag"); Field(form, dTag, 4, false, v => { dTag = v; refresh(); });
            H2(form, "Description"); Field(form, dDesc, 220, true, v => dDesc = v);
            H2(form, "Who can join"); var seg = Row(form, 0); Border(seg, 1, C("#3a3f52"), 6); seg.style.alignSelf = Align.FlexStart;
            foreach (var m in new[] { new[] { "open", "Auto accept" }, new[] { "request", "Request to join" } }) { bool on = dMode == m[0]; var b = E(seg); Pad(b, 6, 11); b.style.backgroundColor = on ? C("#3a3016") : C("#161a26"); T(b, m[1], 11, on ? C("#ffe8a0") : C("#d8d0bc"), true); string k = m[0]; b.RegisterCallback<ClickEvent>(e => { dMode = k; dirty = true; }); }
            var cl = T(form, "Founding cost " + Span(AldaraGuild.COST.ToString("N0") + " gold", "#ffe8a0") + " · you have " + Span(H.gold.ToString("N0"), can ? "#ffe8a0" : "#ff7a6a"), 12, C("#c8b890")); cl.enableRichText = true; cl.style.marginTop = 6;
            var go = E(form); Gb(go, "Found the guild", () => { if (AldaraGuild.Create(dName, dTag, dDesc, dMode, dIcon)) { tab = "mine"; dName = dTag = dDesc = ""; dIcon = new AldaraGuild.Icon(); } dirty = true; }, true, false, !can, 14);
            ApplyGap(form); ApplyGap(wrap);
        }
        void Picks(VisualElement p, System.Collections.Generic.Dictionary<string, string> set, string key, bool fill, System.Action refresh)
        {
            var grid = Row(p, 4); grid.style.flexWrap = Wrap.Wrap;
            foreach (var kv in set)
            {
                string k = kv.Key, d = kv.Value; bool on = (key == "shape" ? dIcon.shape : dIcon.sym) == k;
                var b = E(grid); b.style.width = b.style.height = 34; b.style.marginRight = 4; b.style.marginBottom = 4; Border(b, 1, on ? C("#e8c46a") : C("#4a4f62"), 5); b.style.backgroundColor = C("#161a26"); b.tooltip = k;
                var col = on ? C("#ffe8a0") : C("#a8a090"); var ic = E(b); ic.style.flexGrow = 1; ic.pickingMode = PickingMode.Ignore;
                ic.generateVisualContent += ctx => AldaraSvg.Draw(ctx.painter2D, d, 64, new Vector2(3, 3), 26 / 64f, fill ? C("#2a3040") : (Color?)null, col, fill ? 2.5f : 3.5f);
                b.RegisterCallback<ClickEvent>(e => { if (key == "shape") dIcon.shape = k; else dIcon.sym = k; dirty = true; });
            }
        }
        void Swatches(VisualElement p, string key, System.Action refresh)
        {
            var row = Row(p, 4); row.style.flexWrap = Wrap.Wrap;
            foreach (var c in AldaraGuild.COLS)
            {
                string cc = c; string cur = key == "bg" ? dIcon.bg : key == "fg" ? dIcon.fg : dIcon.rim; bool on = cur == c;
                var s = E(row); s.style.width = s.style.height = 20; s.style.marginRight = 4; s.style.marginBottom = 4; Border(s, 2, on ? Color.white : C("#2a2e3a"), 10); s.style.backgroundColor = C(c); s.tooltip = c;
                s.RegisterCallback<ClickEvent>(e => { if (key == "bg") dIcon.bg = cc; else if (key == "fg") dIcon.fg = cc; else dIcon.rim = cc; dirty = true; });
            }
        }
    }
}
