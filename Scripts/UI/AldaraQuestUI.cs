using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;
using Q = Aldara.AldaraQuests;

namespace Aldara
{
    // The browser's quest UI: the tracker under the minimap (renderQuest), conversations with quest givers and the
    // bounty boards (qDlgPanel / qRenderDlg, with Valcrest's lore topics and the King's Blessing), and the Quest Log (L).
    public static class AldaraQuestUI
    {
        public static readonly Color GOLD = C("#e0b64b");
        static Texture2D grad; static readonly Dictionary<Color, Texture2D> grads = new Dictionary<Color, Texture2D>();
        /// a horizontal fade from clear (left) to the colour (right), like linear-gradient(270deg, c, transparent)
        public static Texture2D GradR(Color c)
        {
            if (grads.TryGetValue(c, out Texture2D t) && t) return t;
            t = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int i = 0; i < 64; i++) t.SetPixel(i, 0, new Color(c.r, c.g, c.b, c.a * i / 63f)); t.Apply(); grads[c] = t; return t;
        }
        public static string Esc(string s) { return (s ?? "").Replace("<", "&lt;").Replace(">", "&gt;"); }
        static string N(float v) { return v.ToString("N0"); }

        // ---------- tracker (top right, under the zone name) ----------
        public class Tracker
        {
            public VisualElement root; bool dirty = true;
            public Tracker(VisualElement parent)
            {
                root = new VisualElement { name = "questBox" }; parent.Add(root);
                root.style.position = Position.Absolute; root.style.left = 1636; root.style.top = 223; root.style.width = 274; Pad(root, 4, 6); Radius(root, 4);
                root.style.alignItems = Align.FlexEnd; root.pickingMode = PickingMode.Ignore;
                Q.Changed += () => dirty = true;
            }
            public void Mark() { dirty = true; }
            public void Update() { if (dirty) { dirty = false; Render(); } }
            static Label Tx(VisualElement p, string s, float size, Color c, bool cinzel = false, bool bold = false)
            {
                var l = T(p, s, size, c, cinzel, bold); l.style.unityTextAlign = TextAnchor.UpperRight; Shadow(l, new Color(0, 0, 0, 0.95f), 3, 1); return l;
            }
            void Render()
            {
                root.Clear(); if (!AldaraSave.Ready) return;
                var foc = Q.FocusInst(); var list = Q.TrackedList();
                var qh = Row(root, 0, Justify.SpaceBetween, Align.FlexEnd); qh.style.width = 262; qh.pickingMode = PickingMode.Position;
                var h = T(qh, "QUESTS", 11, C("#e8c46a"), true, true, false, 2); Shadow(h, new Color(0, 0, 0, 0.95f), 3, 1);
                var k = T(qh, AldaraKeys.Name(AldaraKeys.KeyOf("quest")) + ": Quest Log", 10, C("#9a8a6a")); Shadow(k, new Color(0, 0, 0, 0.95f), 3, 1);
                qh.RegisterCallback<ClickEvent>(e => { AldaraWindows.I.Toggle("quest"); e.StopPropagation(); });
                qh.RegisterCallback<MouseEnterEvent>(e => h.style.color = C("#ffe9a8")); qh.RegisterCallback<MouseLeaveEvent>(e => h.style.color = C("#e8c46a"));
                if (list.Count == 0)
                {
                    var nx = Q.NextMain();
                    var e1 = Tx(root, nx != null && nx.giver != null ? "Next: speak with " + Q.QNPC[nx.giver].name + " (" + nx.title + ")" : "No quests running. The bounty board in Lorenmar always has work.", 11.5f, C("#dddddd"));
                    e1.style.marginTop = 5; e1.style.width = 262;
                }
                foreach (var inst in list)
                {
                    var d = Q.Def(inst); bool ready = Q.IsReady(inst), f = inst == foc; string cls = d.main != 0 ? "main" : d.bounty != 0 ? "bounty" : "side";
                    var qq = Col(root); qq.style.marginTop = 7; Pad(qq, 3, 8, 4, 6); qq.style.alignItems = Align.FlexEnd; qq.style.width = 262; qq.pickingMode = PickingMode.Position;
                    Color edge = cls == "main" ? (f ? C("#ffd24a") : C("#e0b64b", 0.27f)) : cls == "side" ? (f ? C("#8fc4f4") : C("#7fb0e0", 0.33f)) : (f ? C("#f0a070") : C("#d0805a", 0.33f));
                    qq.style.borderRightWidth = 2; qq.style.borderRightColor = edge; qq.style.borderTopLeftRadius = qq.style.borderBottomLeftRadius = 3;
                    if (f) { qq.style.backgroundImage = Background.FromTexture2D(GradR(cls == "main" ? C("#e0b64b", 0.15f) : cls == "side" ? C("#7fb0e0", 0.13f) : C("#d0805a", 0.13f))); }
                    var bgHover = GradR(new Color(0, 0, 0, 0.53f));
                    qq.RegisterCallback<MouseEnterEvent>(e => { if (!f) qq.style.backgroundImage = Background.FromTexture2D(bgHover); });
                    qq.RegisterCallback<MouseLeaveEvent>(e => { if (!f) qq.style.backgroundImage = StyleKeyword.None; });
                    string id = inst.id;
                    qq.RegisterCallback<ClickEvent>(e => { AldaraAuto.ClickQuest(id); e.StopPropagation(); });
                    Tx(qq, d.title.ToUpper(), 12.5f, cls == "main" ? C("#ffd24a") : cls == "side" ? C("#bcdcf8") : C("#f4c8a4"), true, true);
                    if (d.main != 0) Tx(qq, "Chapter " + d.ch, 9.5f, C("#a89a70")).style.letterSpacing = 0.5f;
                    if (ready) { var t = Q.QNPC.TryGetValue(d.turn ?? "", out Q.Npc tn) ? tn.name : "the quest giver"; Tx(qq, "Return to " + t, 11.5f, C("#9fe09f")).style.marginTop = 1; }
                    else for (int i = 0; i < d.obj.Length; i++)
                        {
                            bool dn = Q.ObjDone(inst, i); int need = Q.Need(d.obj[i]);
                            string s = Esc(Q.ObjText(d.obj[i], true)) + (need > 1 ? " " + Span(Mathf.Min(inst.p[i], need) + " / " + need, dn ? "#80b080" : "#ffe9a8") : "");
                            Tx(qq, s, 11.5f, dn ? C("#80b080") : C("#e8e2d0")).style.marginTop = 1;
                        }
                }
                if (list.Count > 0)
                {
                    bool hunt = AldaraAuto.questHunt && AldaraAuto.on;
                    string txt = hunt ? "Auto-questing. Click the quest to stop." : AldaraAuto.on ? "Click a quest to auto-quest it" : "Follow the gold marker, or turn on Auto-Combat (" + AldaraKeys.Name(AldaraKeys.KeyOf("auto")) + ") and click a quest";
                    var s = Tx(root, txt, 10, hunt ? C("#9fe09f") : AldaraAuto.on ? C("#e0b64b") : C("#999999")); s.style.marginTop = 6; s.style.width = 262;
                }
                // #questBox.hunting: a green fade from the right
                root.style.backgroundImage = AldaraAuto.questHunt && AldaraAuto.on ? new StyleBackground(Background.FromTexture2D(GradR(C("#2a5a1a", 0.33f)))) : new StyleBackground(StyleKeyword.None);
            }
        }

        // ---------- shared pieces of the conversation and log ----------
        static void Skin2(VisualElement e, string tex, float slice) { var t = Tex(tex); if (!t) return; e.style.backgroundImage = Background.FromTexture2D(t); int px = Mathf.RoundToInt(slice * 2); e.style.unitySliceLeft = e.style.unitySliceRight = e.style.unitySliceTop = e.style.unitySliceBottom = px; e.style.unitySliceScale = 0.5f; }
        public static void Head(VisualElement v, Q.Npc N)
        {
            var h = Col(v); h.style.paddingRight = 50;
            var l = T(h, N.name.ToUpper(), 18, GOLD, true, true); Shadow(l, new Color(0, 0, 0, 0.8f), 2, 1);
            var s = T(h, (N.title ?? "").ToUpper(), 11, C("#a89a78"), true, true, false, 0.5f); s.style.marginTop = -2;
        }
        public static VisualElement QHead(VisualElement v, QDef_ d, string tag, string tagCls)
        {
            var r = Row(v, 8, Justify.SpaceBetween, Align.FlexEnd); r.style.marginTop = 10; r.style.marginBottom = 4;
            var t = T(r, d.title.ToUpper(), 15, C("#ffe9a8"), true, true); t.style.flexShrink = 1;
            if (tag != null)
            {
                var c = E(r); Skin2(c, "q_tag", 4); Pad(c, 1, 6); c.style.flexShrink = 0;
                Color tc = tagCls == "side" ? C("#9fc8f0") : tagCls == "bounty" ? C("#f0b890") : C("#c8b080");
                if (tagCls == "side") c.style.unityBackgroundImageTintColor = C("#6aa0d8"); if (tagCls == "bounty") c.style.unityBackgroundImageTintColor = C("#d88060");
                T(c, tag.ToUpper(), 10, tc, true, true, false, 0, false, false);
            }
            return r;
        }
        public class QDef_ { public string title; public QDef_(string t) { title = t; } }
        public static Label Text(VisualElement v, string s, float mt = 6)
        {
            var l = T(v, Esc(s), 13, C("#d8d2c0")); l.style.marginTop = mt; l.style.marginBottom = 8; return l;
        }
        public static void Sec(VisualElement v, string s)
        {
            var l = T(v, s.ToUpper(), 10, C("#9a9a9a"), false, false, false, 1); l.style.marginTop = 12; l.style.marginBottom = 4; l.style.paddingBottom = 3; BorderBottom(l, 1, C("#2a2f42"));
        }
        public static void Note(VisualElement v, string s) { var l = T(v, s, 11.5f, C("#999999")); l.style.marginTop = 8; }
        public static void Objectives(VisualElement v, Q.QDef d, Q.Inst inst)
        {
            Sec(v, "Objectives");
            for (int i = 0; i < d.obj.Length; i++)
            {
                var o = d.obj[i]; int need = Q.Need(o), have = inst != null ? Mathf.Min(inst.p[i], need) : 0; bool dn = inst != null && Q.ObjDone(inst, i);
                string s = Esc(Q.ObjText(o)) + (need > 1 || inst != null ? " " + Span(inst != null ? have + " / " + need : need.ToString(), dn ? "#7fa87f" : "#e0b64b") : "");
                var l = T(v, s, 12.5f, dn ? C("#7fa87f") : C("#eeeeee")); l.style.paddingTop = l.style.paddingBottom = 2;
            }
        }
        public static void Rewards(VisualElement v, Q.QDef d, Q.Inst inst, int pick, System.Action<int> onPick)
        {
            var r = d.rw ?? new Q.QRw(); var parts = new List<string>();
            float xp = Q.RwXp(r);
            if (xp > 0) parts.Add(Span(N(xp) + " XP", "#9fd0ff")); if (r.gold > 0) parts.Add(Span(N(r.gold) + " Gold", "#ffd35a"));
            if (r.vials > 0) parts.Add(r.vials + " Health and Mana Vial" + (r.vials > 1 ? "s" : ""));
            Sec(v, "Rewards");
            var row = Row(v, 14); row.style.flexWrap = Wrap.Wrap; foreach (var p in parts) T(row, p, 12.5f, C("#dddddd"), false, false, false, 0, false, false);
            if (r.choice != 0 && d.bounty == 0)
            {
                if (inst != null)
                {
                    var ch = Q.Choices(inst); Sec(v, "Choose one");
                    var col = Col(v, 6);
                    for (int i = 0; i < ch.Count; i++)
                    {
                        var it = ch[i]; int idx = i; bool sel = pick == i;
                        var b = Col(col); Skin2(b, "q_item", 6); Pad(b, 7, 9); b.pickingMode = PickingMode.Position;
                        if (sel) { Border(b, 1, GOLD, 6); b.style.unityBackgroundImageTintColor = C("#e8d090"); }
                        T(b, "<b>" + Esc(it.name) + "</b>", 12.5f, it.Col, false, true);
                        T(b, it.rarity + " " + (it.type == "weapon" ? (it.cls ?? "") + " weapon" : it.type), 10, C("#999999"));
                        T(b, Esc(AldaraItems.Desc(it)), 10.5f, C("#bbbbbb"));
                        b.RegisterCallback<ClickEvent>(e => onPick(idx));
                        b.RegisterCallback<MouseEnterEvent>(e => { if (!sel) Border(b, 1, C("#e0b64b", 0.53f), 6); }); b.RegisterCallback<MouseLeaveEvent>(e => { if (!sel) Border(b, 0, Color.clear, 6); });
                    }
                }
                else Note(v, "Plus one item of your choice from three.");
            }
        }
        public static Btn Button(VisualElement p, string s, System.Action a, bool gold)
        {
            var b = new Btn(s.ToUpper(), a, "btn", 12); Skin2(b, gold ? "q_btn_gold" : "q_btn", 6); p.Add(b);
            b.style.flexGrow = 1; b.style.flexBasis = 0; b.style.height = 31; b.label.style.letterSpacing = 1;
            if (gold) { b.label.style.color = C("#2a1a08"); b.label.style.textShadow = new TextShadow { color = new Color(1, 1, 1, 0.3f), offset = new Vector2(0, 1), blurRadius = 0 }; }
            return b;
        }
        public static VisualElement Btns(VisualElement v) { var r = Row(v, 8); r.style.marginTop = 16; return r; }
        /// a choice row: the browser's .qd-li (gold-bevelled, Cinzel, an "!" or "?" glyph and a note at the right)
        public static VisualElement Li(VisualElement v, string glyph, string title, string note, System.Action a, string kind = null, bool disabled = false)
        {
            var b = Row(v, 9); b.style.marginTop = 6; Pad(b, 9, 14); b.pickingMode = PickingMode.Position;
            Skin2(b, kind == "bless" ? "q_bless" : "q_li", 6);
            var i = E(b); i.style.width = 12; i.style.alignItems = Align.Center; i.style.justifyContent = Justify.Center;
            Color gc = kind == "low" || kind == "ac" ? C("#8a8a8a") : C("#ffd35a");
            if (kind == "topic" || kind == "bless") { i.style.width = 12; i.style.height = 18; i.style.backgroundColor = kind == "topic" ? C("#3a5a7a") : GOLD; gc = kind == "topic" ? C("#dff4ff") : C("#1a1008"); }
            var gl = T(i, glyph, kind == "topic" || kind == "bless" ? 13 : 16, gc, true, true, false, 0, false, false); gl.style.unityTextAlign = TextAnchor.MiddleCenter;
            var t = T(b, title.ToUpper(), 13, C("#f7e7bd"), true, true, false, 0.5f, false, false); Shadow(t, Color.black, 1, 1); t.style.flexShrink = 1; t.style.flexGrow = 1;
            var n = T(b, note.ToUpper(), 10, kind == "rd" ? C("#9fe09f") : kind == "topic" ? C("#9fc8e8") : C("#999999"), true, true, false, 0.5f, false, false); n.style.flexShrink = 0;
            if (disabled) b.style.opacity = 0.6f;
            else
            {
                b.RegisterCallback<ClickEvent>(e => { a(); e.StopPropagation(); });
                b.RegisterCallback<MouseEnterEvent>(e => b.style.unityBackgroundImageTintColor = new Color(1.25f, 1.2f, 1.1f)); b.RegisterCallback<MouseLeaveEvent>(e => b.style.unityBackgroundImageTintColor = Color.white);
            }
            b.schedule.Execute(() => ApplyGap(b));
            return b;
        }
    }

    // ---------- talking to people (qDlgPanel) ----------
    public class AldaraQuestDlg : AldaraWindows.Win
    {
        public AldaraQuestDlg() { id = "qdlg"; title = ""; }
        public override bool NoTitle => true;
        public override float Y => 96; public override float W => 450; public override float MaxH => 880;
        public static string npc, view = "home", qid; public static int pick = -1, topic = -1;
        static AldaraHero Hr { get { return AldaraHero.I; } }
        public static void Open(string n)
        {
            if (!Q.QNPC.ContainsKey(n)) return; Q.TalkTo(n);
            npc = n; pick = -1; qid = null; view = "home";
            var N = Q.QNPC[n];
            if (N.board == 0)
            {
                var rd = Q.ReadyFor(n); var av = Q.AvailFrom(n).Where(Q.LevelOk).ToList(); var ac = Q.ActiveFor(n);
                if (rd.Count == 1 && av.Count == 0) { view = "turn"; qid = rd[0].id; }
                else if (rd.Count == 0 && av.Count == 1 && ac.Count == 0) { view = "offer"; qid = av[0].id; }
            }
            AldaraWindows.I.Toggle("quest", false); AldaraWindows.I.Toggle("qdlg", true); AldaraWindows.Refresh("qdlg");
        }
        public static bool IsOpen { get { return AldaraWindows.I && AldaraWindows.I.IsOpen("qdlg"); } }
        public static void Close() { if (AldaraWindows.I) AldaraWindows.I.Toggle("qdlg", false); }
        public override void OnClose() { npc = null; }
        public override void OnOpen() { Q.Changed -= Redraw; Q.Changed += Redraw; }
        void Redraw() { dirty = true; }
        public override void Tick()
        {   // walk away and the conversation ends
            var P = AldaraPlayer.I; var n = npc != null ? AldaraFolk.Pos(npc) : null;
            if (P && n.HasValue && Vector2.Distance(n.Value, new Vector2(P.x, P.y)) > 260) Close();
        }
        void Go(string v, string q = null) { view = v; qid = q; pick = -1; dirty = true; }
        public override void Render(VisualElement v)
        {
            if (npc == null || !Q.QNPC.TryGetValue(npc, out Q.Npc N)) return;
            AldaraQuestUI.Head(v, N);
            if (N.board != 0)
            {
                var mine = Q.S.act.Where(a => Q.Def(a).bounty != 0).ToList();
                AldaraQuestUI.Text(v, "Bounties pay out the moment the job is done. You can hold three at once.");
                AldaraQuestUI.Sec(v, "Posted");
                foreach (var o in Q.BountyOffers()) { var k = o.key; BRow(v, o.title.Replace("Bounty: ", ""), o.lvl, o.text, o.rw, "Take", () => { Q.TakeBounty(k); dirty = true; }); }
                if (mine.Count > 0)
                {
                    AldaraQuestUI.Sec(v, "Your bounties");
                    foreach (var a in mine) { var d = Q.Def(a); var o = d.obj[0]; string id = a.id; BRow(v, d.title.Replace("Bounty: ", ""), -1, Q.ObjText(o) + " " + AldaraUI.Span(Mathf.Min(a.p[0], Q.Need(o)) + " / " + Q.Need(o), "#e0b64b"), null, "Drop", () => { Q.Abandon(id); dirty = true; }); }
                }
                return;
            }
            if (view == "ktopic" && N.topics != null && topic >= 0 && topic < N.topics.Length)
            {
                var T0 = N.topics[topic]; AldaraQuestUI.QHead(v, new AldaraQuestUI.QDef_(T0.t), null, null);
                var lt = T(v, AldaraQuestUI.Esc(T0.x), 13, C("#d8d2c0"), false, false, true); lt.style.marginTop = 6; lt.style.marginBottom = 8;
                Heard(npc + ":" + topic);
                var bb = AldaraQuestUI.Btns(v); AldaraQuestUI.Button(bb, "Back", () => Go("home"), false); ApplyLater(bb); return;
            }
            Q.QDef q = null; Q.Inst inst = qid != null ? Q.InstOf(qid) : null;
            if (qid != null) q = Q.QDB.TryGetValue(qid, out Q.QDef qq) ? qq : inst != null ? Q.Def(inst) : null;
            if (view == "offer" && q != null)
            {
                AldaraQuestUI.QHead(v, new AldaraQuestUI.QDef_(q.title), q.main != 0 ? "Chapter " + q.ch : "Side quest", q.main != 0 ? null : "side");
                AldaraQuestUI.Text(v, q.text); AldaraQuestUI.Objectives(v, q, null); AldaraQuestUI.Rewards(v, q, null, -1, null);
                if (Mathf.Max(1, q.lvl) > Hr.lvl + 1) { var w = T(v, "Recommended level " + q.lvl + ". You are level " + Hr.lvl + ".", 11, C("#ff9a8a")); Border(w, 1, C("#c0392b", 0.53f), 6); Pad(w, 6, 8); w.style.marginTop = 10; }
                var bb = AldaraQuestUI.Btns(v);
                AldaraQuestUI.Button(bb, "Accept", () => { Q.Accept(qid); Go("home"); if (Q.AvailFrom(npc).Count == 0 && Q.ReadyFor(npc).Count == 0) Close(); }, true);
                AldaraQuestUI.Button(bb, "Back", () => Go("home"), false); ApplyLater(bb);
            }
            else if (view == "turn" && inst != null)
            {
                var d = Q.Def(inst);
                AldaraQuestUI.QHead(v, new AldaraQuestUI.QDef_(d.title), null, null); AldaraQuestUI.Text(v, d.fin ?? "Well done.");
                AldaraQuestUI.Rewards(v, d, inst, pick, i => { pick = i; dirty = true; });
                bool need = d.rw != null && d.rw.choice != 0; var bb = AldaraQuestUI.Btns(v);
                var cb = AldaraQuestUI.Button(bb, need && pick < 0 ? "Choose a reward" : "Complete Quest", () =>
                {
                    if (Q.Complete(inst, pick))
                    {
                        Go("home"); Q.TalkTo(npc);
                        var rd = Q.ReadyFor(npc); var av = Q.AvailFrom(npc).Where(Q.LevelOk).ToList(); var mn = av.FirstOrDefault(x => x.main != 0);
                        if (rd.Count == 0 && mn != null) Go("offer", mn.id); else if (rd.Count == 0 && av.Count == 0) Close();
                    }
                }, true);
                if (need && pick < 0) cb.Disabled = true;
                AldaraQuestUI.Button(bb, "Back", () => Go("home"), false); ApplyLater(bb);
            }
            else if (view == "progress" && inst != null)
            {
                var d = Q.Def(inst); AldaraQuestUI.QHead(v, new AldaraQuestUI.QDef_(d.title), null, null); AldaraQuestUI.Text(v, d.text); AldaraQuestUI.Objectives(v, d, inst);
                var bb = AldaraQuestUI.Btns(v); AldaraQuestUI.Button(bb, "Back", () => Go("home"), false); ApplyLater(bb);
            }
            else
            {
                var rd = Q.ReadyFor(npc); var av = Q.AvailFrom(npc); var ac = Q.ActiveFor(npc);
                AldaraQuestUI.Text(v, N.hello ?? "");
                if (rd.Count == 0 && av.Count == 0 && ac.Count == 0) { var nx = Q.NextMain(); if (!(Q.S.pendingHint != null && nx != null && nx.giver == npc)) AldaraQuestUI.Note(v, "Nothing for you right now. Come back later."); }
                foreach (var a in rd) { string id = a.id; AldaraQuestUI.Li(v, "?", Q.Def(a).title, "Complete", () => Go("turn", id), "rd"); }
                foreach (var d in av) { string id = d.id; bool low = !Q.LevelOk(d); AldaraQuestUI.Li(v, "!", d.title, d.main != 0 ? "Story" : "Lv " + d.lvl, () => Go("offer", id), low ? "low" : null); }
                foreach (var a in ac) { string id = a.id; AldaraQuestUI.Li(v, "?", Q.Def(a).title, "In progress", () => Go("progress", id), "ac"); }
                // Valcrest: lore topics, and kneeling before the King
                if (N.topics != null || N.kd == "king")
                {
                    AldaraQuestUI.Sec(v, "Talk");
                    if (N.kd == "king") { bool got = KdFlag("blessed"); AldaraQuestUI.Li(v, "+", got ? "You carry the King's Blessing" : "Kneel and receive the King's Blessing", got ? "Received" : "Artifact", () => { GrantBlessing(); dirty = true; }, "bless", got); }
                    if (N.topics != null) for (int i = 0; i < N.topics.Length; i++) { int ti = i; AldaraQuestUI.Li(v, "?", N.topics[i].t, HeardYet(npc + ":" + i) ? "Heard" : "Ask", () => { topic = ti; Go("ktopic"); topic = ti; }, "topic"); }
                }
            }
        }
        static void ApplyLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }
        void BRow(VisualElement v, string name, int lvl, string text, Q.QRw rw, string btn, System.Action a)
        {
            var r = Row(v, 10); r.style.marginTop = 6; Pad(r, 8, 10); var t = Tex("q_brow"); r.style.backgroundImage = Background.FromTexture2D(t);
            r.style.unitySliceLeft = r.style.unitySliceRight = r.style.unitySliceTop = r.style.unitySliceBottom = 16; r.style.unitySliceScale = 0.5f;
            var c = Col(r); c.style.flexGrow = 1; c.style.flexShrink = 1;
            T(c, "<b>" + AldaraQuestUI.Esc(name) + "</b>" + (lvl > 0 ? " " + Span("Lv " + lvl, "#999999") : ""), 12.5f, C("#eeeeee"), false, false);
            var bt = T(c, text, 11, C("#cccccc")); bt.style.marginTop = 2;
            if (rw != null) { var br = T(c, Span(rw.xp.ToString("N0") + " XP", "#9fd0ff") + " " + Span(rw.gold.ToString("N0") + " Gold", "#ffd35a"), 11, C("#dddddd")); br.style.marginTop = 2; }
            var b = new Btn(btn.ToUpper(), a, "btn", 11); var bt2 = Tex("q_bbtn"); b.style.backgroundImage = Background.FromTexture2D(bt2);
            b.style.unitySliceLeft = b.style.unitySliceRight = b.style.unitySliceTop = b.style.unitySliceBottom = 12; b.style.unitySliceScale = 0.5f; b.Padding(6, 14); r.Add(b);
            ApplyLater(r);
        }
        // ---- the Valcrest extras (player.kd) ----
        static Newtonsoft.Json.Linq.JObject Kd() { var j = AldaraSave.Raw; if (j == null) return new Newtonsoft.Json.Linq.JObject(); if (!(j["kd"] is Newtonsoft.Json.Linq.JObject k)) { k = new Newtonsoft.Json.Linq.JObject(); j["kd"] = k; } return k; }
        static bool KdFlag(string f) { var t = Kd()[f]; return t != null && t.Type != Newtonsoft.Json.Linq.JTokenType.Null && (float)t != 0; }
        static bool HeardYet(string k) { return Kd()["heard"] is Newtonsoft.Json.Linq.JArray a && a.Any(x => (string)x == k); }
        static void Heard(string k)
        {
            var kd = Kd(); if (!(kd["heard"] is Newtonsoft.Json.Linq.JArray a)) { a = new Newtonsoft.Json.Linq.JArray(); kd["heard"] = a; }
            if (a.Any(x => (string)x == k)) return; a.Add(k);
            float xp = Mathf.Max(500, Mathf.Round(Hr.xpNeed * 0.004f)); Hr.GainXp(xp); var P = AldaraPlayer.I; AldaraFx.Text(P.x, P.y - 16 - 60, "+" + xp + " XP  (Lore)", C("#bfe8ff")); AldaraSave.Dirty();
        }
        static void GrantBlessing()
        {
            var kd = Kd(); if (KdFlag("blessed")) { AldaraHud.Banner("You already carry the King's Blessing"); return; }
            var it = new Item { id = AldaraItems.itemSeq++, type = "relic", relic = "kingsblessing", name = "The King's Blessing", rarity = "Common", color = AldaraItems.RARITY[0].c, lvl = Hr.lvl };
            if (Hr.inventory.Count >= AldaraItems.BACKPACK_MAX || !Hr.AddLoot(it)) { AldaraHud.Banner("Your backpack is full: make room for the blessing"); return; }
            kd["blessed"] = 1; AldaraHud.Banner("The King's Blessing: equip it from your backpack (I)"); var P = AldaraPlayer.I; AldaraFx.Text(P.x, P.y - 16 - 70, "The King's Blessing", C("#ffd35a"));
            AldaraWindows.Refresh("inv"); AldaraSave.Dirty();
        }
    }

    // ---------- the Quest Log (L) ----------
    public class AldaraQuestLog : AldaraWindows.Win
    {
        public AldaraQuestLog() { id = "quest"; title = "Quest Log"; }
        public override float Y => 78; public override float W => 680; public override float MaxH => 880;
        string sel; string sure;
        public override void OnOpen() { AldaraQuestDlg.Close(); Q.Changed -= Redraw; Q.Changed += Redraw; sure = null; }
        void Redraw() { dirty = true; }
        public override void Render(VisualElement v)
        {
            var S = Q.S; if (sel == null || Q.InstOf(sel) == null) { var f = Q.FocusInst(); sel = f != null ? f.id : null; }
            var ql = Row(v, 16, Justify.FlexStart, Align.Stretch); ql.style.minHeight = 320;
            var L = Col(ql); L.style.width = 210; L.style.flexShrink = 0; L.style.paddingRight = 10; L.style.borderRightWidth = 1; L.style.borderRightColor = C("#2a2f42");
            var R = Col(ql); R.style.flexGrow = 1; R.style.flexShrink = 1; R.style.flexBasis = 0;
            bool firstG = true;
            foreach (var g in new[] { "Story", "Side Quests", "Bounties" })
            {
                var list = S.act.Where(a => { var d = Q.Def(a); return g == "Story" ? d.main != 0 : g == "Bounties" ? d.bounty != 0 : d.main == 0 && d.bounty == 0; }).ToList(); if (list.Count == 0) continue;
                var gl = T(L, g.ToUpper(), 10, C("#9a9a9a"), false, false, false, 1); gl.style.marginTop = firstG ? 0 : 10; gl.style.marginBottom = 3; firstG = false;
                foreach (var a in list)
                {
                    string id = a.id; bool on = a.id == sel, rd = Q.IsReady(a);
                    var b = Row(L, 7); Pad(b, 5, 10); b.style.marginTop = 2; b.pickingMode = PickingMode.Position; var t = Tex("q_li"); b.style.backgroundImage = Background.FromTexture2D(t);
                    b.style.unitySliceLeft = b.style.unitySliceRight = b.style.unitySliceTop = b.style.unitySliceBottom = 12; b.style.unitySliceScale = 0.5f;
                    if (on) b.style.unityBackgroundImageTintColor = C("#f0d890");
                    var dot = E(b); dot.style.width = dot.style.height = 7; Border(dot, 1, a.tr != 0 ? AldaraQuestUI.GOLD : C("#5a5a5a"), 3.5f); if (a.tr != 0) dot.style.backgroundColor = AldaraQuestUI.GOLD; dot.style.flexShrink = 0;
                    var tl = T(b, Q.Def(a).title.ToUpper(), 12, rd ? C("#9fe09f") : on ? C("#ffe9a8") : C("#f7e7bd"), true, true, false, 0.5f); Shadow(tl, Color.black, 1, 1); tl.style.flexShrink = 1;
                    b.RegisterCallback<ClickEvent>(e => { sel = id; sure = null; dirty = true; });
                    b.schedule.Execute(() => ApplyGap(b));
                }
            }
            if (S.act.Count == 0) AldaraQuestUI.Note(L, "No quests running.");
            int doneN = S.done.Count, mainN = Q.QMAIN.Count(id => S.done.ContainsKey(id));
            var st = T(L, "Story: " + mainN + " / " + Q.QMAIN.Count + "\nQuests completed: " + doneN, 10.5f, C("#888888")); st.style.marginTop = 16;
            var inst = sel != null ? Q.InstOf(sel) : null;
            if (inst == null) { AldaraQuestUI.Note(R, "Select a quest."); R.style.marginLeft = 16; return; }
            var d = Q.Def(inst); Q.Npc gN = d.giver != null && Q.QNPC.TryGetValue(d.giver, out Q.Npc gg) ? gg : null, tN = d.turn != null && Q.QNPC.TryGetValue(d.turn, out Q.Npc tt) ? tt : null;
            var hd = AldaraQuestUI.QHead(R, new AldaraQuestUI.QDef_(d.title), d.main != 0 ? "Chapter " + d.ch : d.bounty != 0 ? "Bounty" : "Side quest", d.main != 0 ? null : d.bounty != 0 ? "bounty" : "side"); hd.style.marginTop = 0;
            string who = (gN != null && gN.board == 0 ? "From " + gN.name : "") + (tN != null && tN != gN ? (gN != null ? ". " : "") + "Turn in to " + tN.name : "");
            var wl = T(R, who, 11, C("#a89a78")); wl.style.marginTop = 2;
            AldaraQuestUI.Text(R, d.text); AldaraQuestUI.Objectives(R, d, inst); AldaraQuestUI.Rewards(R, d, null, -1, null);
            if (Q.IsReady(inst) && tN != null) { var ok = T(R, "Complete. Return to " + tN.name + ".", 12, C("#9fe09f")); ok.style.marginTop = 10; }
            var bb = AldaraQuestUI.Btns(R); string iid = inst.id;
            AldaraQuestUI.Button(bb, Q.FocusInst() == inst ? "Following" : "Follow", () => { Q.S.focus = iid; inst.tr = 1; Q.Render(); dirty = true; }, true);
            AldaraQuestUI.Button(bb, inst.tr != 0 ? "Untrack" : "Track", () => { inst.tr = inst.tr != 0 ? 0 : 1; Q.Render(); AldaraSave.Dirty(); dirty = true; }, false);
            if (d.main == 0) AldaraQuestUI.Button(bb, sure == iid ? "Click again to abandon" : "Abandon", () => { if (sure == iid) { Q.Abandon(iid); sel = null; sure = null; } else sure = iid; dirty = true; }, false);
            bb.schedule.Execute(() => ApplyGap(bb)); ql.schedule.Execute(() => ApplyGap(ql));
        }
    }
}
