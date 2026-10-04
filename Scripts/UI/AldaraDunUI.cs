using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;
using A = Aldara.AldaraDungeon;

namespace Aldara
{
    // The Dungeons window (renderDungeons): the note, a card for each dungeon with its state, description, boss,
    // level and an Enter button; raids on their own tab.
    public class AldaraDunWin : AldaraWindows.Win
    {
        public AldaraDunWin() { id = "dun"; title = "Dungeons"; }
        public override float Y => 56; public override float W => 440; public override float MaxH => 940;
        string tab = "dun";
        public override void Render(VisualElement body)
        {
            var H = AldaraHero.I; var sv = new ScrollView(ScrollViewMode.Vertical); body.Add(sv); Thin(sv); sv.style.flexShrink = 1; var v = sv.contentContainer;
            var tabs = Row(v, 6); tabs.style.marginBottom = 8;
            foreach (var t in new[] { new[] { "dun", "Dungeons" }, new[] { "raid", "Raids" } })
            { string k = t[0]; bool on = tab == k; var b = new Btn(t[1], () => { tab = k; dirty = true; }, on ? "btn_tab_on" : "btn_tab", 12, true, on ? C("#ffe2a0") : C("#b8c0d0")); tabs.Add(b); b.style.flexGrow = 1; b.style.height = 30; }
            ApplyGapLater(tabs);
            if (tab == "raid") { Raids(v); return; }
            T(v, "Dungeons are private runs: only you (and your party) can enter while your run lasts. Fight through every section, past traps and obstacles, and defeat the boss for big rewards. Each dungeon unlocks after you clear the one before it.", 10, C("#8f8a7c")).style.marginBottom = 8;
            bool lvlOk = H.lvl >= A.DUN_LEVEL;
            if (!lvlOk) Warn(v, "Requires level " + A.DUN_LEVEL + ". You are level " + H.lvl + ".");
            if (A.Active) Warn(v, "You are in " + A.def.name + ". Leave or finish it first.");
            var L = A.List;
            for (int i = 0; i < L.Count; i++)
            {
                var d = L[i]; if (d.hidden || d.raid) continue; bool un = A.Unlocked(i); int cl = A.Clears(d.id);
                string tag = !un ? "Locked" : cl > 0 ? "Cleared x" + cl : "Available";
                bool can = lvlOk && un && !A.Active && H.alive && !(d.deep && H.lvl < d.rec - 3);
                var c = E(v); Skin(c, "sup_row"); Pad(c, 8, 10); c.style.marginBottom = 7; if (!un) c.style.opacity = 0.55f;
                if (cl > 0 && un) { c.style.borderLeftWidth = 3; c.style.borderLeftColor = C("#e8c46a"); }
                var hd = Row(c, 8); T(hd, d.name.ToUpper(), 13, C("#e8c46a"), true, true); var sp = E(hd); sp.style.flexGrow = 1; T(hd, tag.ToUpper(), 10, C("#aaaaaa"), false, false, false, 0.5f, false, false);
                var de = T(c, d.desc, 11, C("#cccccc")); de.style.marginTop = 3;
                var me = T(c, "Boss: " + d.boss.name + " · " + (d.deep ? "Requires level " + (d.rec - 3) + " · " + d.sections + " sections" : "Recommended level " + d.rec + "+") + " · Guards a relic", 10, C("#88aa88")); me.style.marginTop = 2;
                var bt = Row(c, 5, Justify.FlexEnd); bt.style.marginTop = 6;
                if (un) { int ii = i; var b = B(bt, "Enter", () => A.Start(ii), "btn_buy", 10); b.Padding(4, 9); b.Disabled = !can || !A.Ready(d); if (!A.Ready(d)) b.tooltip = "Not open in this version yet"; }
                else T(bt, "Clear " + L[i - 1].name + " to unlock", 10, C("#88aa88"));
            }
        }
        static void ApplyGapLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }
        static void Warn(VisualElement v, string s) { var w = T(v, s, 11, C("#ff9a7a")); w.style.marginBottom = 8; }
        // the Raids tab
        void Raids(VisualElement v)
        {
            var H = AldaraHero.I;
            T(v, "Raids are long instances for up to 4 players (or one brave soul). Every encounter has mechanics and waves of enemies. Clearing an encounter saves a checkpoint: leave and you can continue from it later, and if the whole party falls (a wipe) everyone returns to it. Fallen players can be revived by an ally standing over them.", 10, C("#8f8a7c")).style.marginBottom = 8;
            if (H.lvl < AldaraRaid.RAID_LVL_MIN) Warn(v, "Raids require level " + AldaraRaid.RAID_LVL_MIN + ". You are level " + H.lvl + ".");
            if (A.Active) Warn(v, "You are in " + A.def.name + ". Leave or finish it first.");
            foreach (var d in A.List)
            {
                if (!d.raid) continue; int cp = AldaraRaid.Cp(d.id), cl = AldaraRaid.Clears(d.id), best = AldaraRaid.BestTime(d.id);
                bool can = H.lvl >= AldaraRaid.RAID_LVL_MIN && !A.Active && H.alive && A.Ready(d);
                var c = E(v); Skin(c, "sup_row"); Pad(c, 8, 10); c.style.marginBottom = 7; if (cl > 0) { c.style.borderLeftWidth = 3; c.style.borderLeftColor = C("#e8c46a"); }
                var hd = Row(c, 8); T(hd, d.name.ToUpper(), 13, C("#e8c46a"), true, true); var sp = E(hd); sp.style.flexGrow = 1;
                T(hd, (cl > 0 ? "Conquered x" + cl : cp > 0 ? "In progress" : "Available").ToUpper(), 10, C("#aaaaaa"), false, false, false, 0.5f, false, false);
                var de = T(c, d.desc, 11, C("#cccccc")); de.style.marginTop = 3;
                var cps = new HashSet<int>(); foreach (var e in AldaraRaid.RENC) if (e.cpAfter >= 0) cps.Add(e.cpAfter);
                var ul = E(c); ul.style.marginTop = 6; ul.style.marginBottom = 2;
                for (int k = 0; k < d.encNames.Length; k++)
                {
                    var li = Row(ul, 6); li.style.paddingTop = li.style.paddingBottom = 1;
                    var nb = T(li, (k + 1).ToString(), 12, C("#f2c46a"), false, true, false, 0, false, false); nb.style.minWidth = 18;
                    T(li, d.encNames[k] + (cp > 0 && k < cp ? " (cleared)" : ""), 12, cp > 0 && cp == k ? C("#f7e3b0") : C("#cdbf9f"), false, false, false, 0, false, false);
                    if (cps.Contains(k)) { var g = E(li); g.style.flexGrow = 1; T(li, "CHECKPOINT", 10.5f, C("#9a8e76"), false, false, false, 0.4f, false, false); }
                }
                var me = T(c, "Requires level " + AldaraRaid.RAID_LVL_MIN + " · Enemies match your level (at least " + d.rec + ") · Final boss: " + d.boss.name + (best > 0 ? " · Best time " + AldaraRaid.Fmt(best) : ""), 10, C("#88aa88")); me.style.marginTop = 2;
                var bt = Row(c, 5, Justify.FlexEnd); bt.style.marginTop = 6; string id = d.id;
                if (cp > 0)
                {
                    var b1 = B(bt, "Continue: " + d.encNames[cp], () => AldaraRaid.Enter(id, true), "btn_buy", 10); b1.Padding(4, 9); b1.Disabled = !can;
                    var b2 = B(bt, "Start over", () => AldaraRaid.Enter(id, false), "btn", 10); b2.Padding(4, 9); b2.Disabled = !can;
                }
                else { var b = B(bt, "Enter raid", () => AldaraRaid.Enter(id, false), "btn_buy", 10); b.Padding(4, 9); b.Disabled = !can; }
                ApplyGapLater(bt);
            }
        }
    }

    // The results window (dsShow): the run's damage, healing, kills, boss damage, damage taken and biggest hit, the
    // party DPS, enemies slain, and XP and gold or rooms opened, any loot, and Return to town after a clear.
    public class AldaraDunResWin : AldaraWindows.Win
    {
        public AldaraDunResWin() { id = "dres"; title = "Dungeon Results"; }
        public override float Y => 70; public override float W => 760;
        static string F(float n) { return n >= 1e6f ? (n / 1e6f).ToString("0.0") + "M" : n >= 1e4f ? Mathf.Round(n / 1e3f) + "K" : n >= 1e3f ? (n / 1e3f).ToString("0.0") + "K" : Mathf.Round(n).ToString(); }
        public override void OnOpen() { title = DStat.verdict == "done" ? "Dungeon Cleared" : "Dungeon Results"; }
        public override void Render(VisualElement v)
        {
            var H = AldaraHero.I; int secs = Mathf.RoundToInt(DStat.secs); string v1 = DStat.verdict;
            var hd = Row(v, 10, Justify.SpaceBetween); hd.style.marginBottom = 10;
            T(hd, (DStat.def != null ? DStat.def.name : "Dungeon").ToUpper(), 16, C("#e8c46a"), true, true);
            T(hd, "Time " + secs / 60 + ":" + (secs % 60).ToString("00"), 12, C("#c8b890"), true);
            var vd = T(hd, v1 == "done" ? "CLEARED" : v1 == "fail" ? "FAILED" : "ABANDONED", 12, v1 == "done" ? C("#d8ffc8") : v1 == "fail" ? C("#ffd0c0") : C("#e8e6da"), true, true); Pad(vd, 2, 10); vd.style.letterSpacing = 1; vd.style.backgroundColor = v1 == "done" ? C("#2e5a26") : v1 == "fail" ? C("#6a1a10") : C("#3a4050");
            string[] heads = { "Member", "Damage", "Healing", "Kills", "Boss damage", "Damage taken", "Biggest hit" }; float[] wd = { 150, 80, 80, 50, 100, 100, 90 };
            var hr = Row(v); BorderBottom(hr, 1, C("#6a4a1a")); Pad(hr, 4, 0);
            for (int i = 0; i < heads.Length; i++) { var l = T(hr, heads[i].ToUpper(), 10.5f, C("#c8a860"), true, true, false, 1, false, false); l.style.width = wd[i]; l.style.unityTextAlign = i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight; }
            var row = Row(v, 0, Justify.FlexStart, Align.Center); Pad(row, 6, 0); BorderBottom(row, 1, C("#2a3040"));
            var who = Col(row); who.style.width = wd[0]; T(who, H.heroName, 12.5f, C("#fff2c4"), true, true); T(who, "Level " + H.lvl + " " + char.ToUpper(H.cls[0]) + H.cls.Substring(1) + (DStat.pet != null ? " with " + DStat.pet : ""), 10.5f, C("#a89a70"));
            Cell(row, F(DStat.dmg), wd[1], DStat.dmg, null); Cell(row, F(DStat.heal), wd[2], DStat.heal, "h"); Cell(row, DStat.kills.ToString(), wd[3]); Cell(row, F(DStat.boss), wd[4], DStat.boss, "b"); Cell(row, F(DStat.taken), wd[5]); Cell(row, F(DStat.big), wd[6]);
            var tot = Row(v, 0, Justify.FlexStart, Align.Center); Pad(tot, 6, 0); tot.style.borderTopWidth = 1; tot.style.borderTopColor = C("#6a4a1a");
            var tl = T(tot, "Party total", 12.5f, C("#fff2c4"), true, true); tl.style.width = wd[0];
            Cell(tot, F(DStat.dmg), wd[1], -1, null, true); Cell(tot, F(DStat.heal), wd[2], -1, null, true); Cell(tot, DStat.kills.ToString(), wd[3], -1, null, true); Cell(tot, F(DStat.boss), wd[4], -1, null, true); Cell(tot, F(DStat.taken), wd[5], -1, null, true);
            var sm = Row(v, 8); sm.style.marginTop = 10;
            Sum(sm, "Party DPS", F(secs > 0 ? DStat.dmg / secs : 0)); Sum(sm, "Enemies slain", DStat.kills.ToString());
            if (v1 == "done") { Sum(sm, "XP earned", F(DStat.xp)); Sum(sm, "Gold earned", F(DStat.gold)); } else Sum(sm, "Rooms opened", DStat.roomsOpen + " / " + DStat.rooms);
            ApplyGapLater(sm);
            if (DStat.loot.Count > 0) { var lt = T(v, "Loot: " + string.Join("   ", DStat.loot.Select(it => Span(it.name, Hex(it.Col)))), 12, C("#c8b890")); lt.style.marginTop = 10; }
            var bb = Row(v, 8); bb.style.marginTop = 12;
            if (A.Active && A.done) { var r = B(bb, "Return to town", () => { AldaraWindows.I.Toggle("dres", false); if (A.Active) A.Exit("done"); }, "btn_gold", 12); r.style.flexGrow = 1; r.style.height = 32; }
            var cb = B(bb, A.Active && A.done ? "Stay a moment" : "Close", () => AldaraWindows.I.Toggle("dres", false), "btn", 12); cb.style.flexGrow = 1; cb.style.height = 32;
            ApplyGapLater(bb);
        }
        /// a number, with the browser's thin bar under it (.ds-bar: gold for damage, green for healing, red for boss damage) scaled to the best in the party
        static void Cell(VisualElement r, string s, float w, float bar = -1, string kind = null, bool tot = false)
        {
            var c = Col(r); c.style.width = w; c.style.alignItems = Align.FlexEnd;
            var l = T(c, s, 12.5f, tot ? C("#fff2c4") : C("#e8e6da"), false, tot, false, 0, false, false); l.style.unityTextAlign = TextAnchor.MiddleRight;
            if (bar < 0) return; var b = new VisualElement { pickingMode = PickingMode.Ignore }; b.style.height = 4; b.style.marginTop = 4; b.style.width = Length.Percent(100); b.style.backgroundColor = C("#0a0b10"); c.Add(b);
            var f = new VisualElement { pickingMode = PickingMode.Ignore }; f.style.height = Length.Percent(100); f.style.width = Length.Percent(Mathf.Round(100 * bar / Mathf.Max(1, bar))); f.style.backgroundColor = kind == "h" ? C("#6cc56d") : kind == "b" ? C("#c45a3a") : C("#d8b25a"); b.Add(f);
        }
        static void Sum(VisualElement r, string a, string b) { var c = Col(r); Skin(c, "sup_row"); Pad(c, 7, 10); c.style.flexGrow = 1; c.style.flexBasis = 0; var h = T(c, a.ToUpper(), 9.5f, C("#c8a860"), true); h.style.letterSpacing = 1; T(c, b, 15, C("#fff2c4"), false, true); }
        static void ApplyGapLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }
    }

    // The dungeon box at the top of the screen (#dunHud): the dungeon's name, where you are and what is left, Leave.
    // And the minimap in a dungeon (drawDungeonMinimap): the whole map fitted in, sections you can reach lit, shut gates red.
    public class AldaraDunHud : MonoBehaviour
    {
        VisualElement box, mini; Label nameL, status; Btn leave; MiniMarks marks; Texture2D miniTex; int miniOpen = -1; string miniId;
        void Update()
        {
            var hud = AldaraHudUI.I; if (hud == null || hud.Root == null) return;
            if (box == null)
            {
                box = new VisualElement { pickingMode = PickingMode.Position }; hud.Root.Add(box); box.style.position = Position.Absolute; box.style.left = Length.Percent(50); box.style.translate = new Translate(Length.Percent(-50), 0); box.style.top = 84; box.style.width = 220;
                Skin(box, "panel"); Pad(box, 12, 14); box.style.alignItems = Align.Center;
                nameL = T(box, "", 15, C("#efcf78"), true, true, false, 1, true, false); Shadow(nameL, Color.black, 2, 1); nameL.style.unityTextAlign = TextAnchor.MiddleCenter;
                status = T(box, "", 11, C("#d8d0bc")); status.style.unityTextAlign = TextAnchor.MiddleCenter; status.style.marginTop = 2;
                leave = B(box, "Leave Dungeon", () => { if (A.Throne) AldaraThrone.Leave(); else if (A.Fort) AldaraFort.Leave(); else if (A.Active) A.Exit(A.done ? "done" : "left"); }, "btn_buy", 10); leave.style.marginTop = 6; leave.Padding(3, 14);
            }
            bool on = A.Active && AldaraSave.Ready && !(AldaraWindows.I && AldaraWindows.I.IsOpen("map"));
            box.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on) { nameL.text = A.def.name.ToUpper(); status.text = A.Status; leave.label.text = A.Throne ? "Leave the Palace" : A.Fort ? "Leave Tavern" : A.done ? "Return to Town" : "Leave Dungeon"; }
            // the minimap
            var mv = hud.MinimapView; if (mv == null) return;
            if (mini == null) { mini = new VisualElement { pickingMode = PickingMode.Ignore }; mini.style.position = Position.Absolute; mini.style.left = mini.style.top = mini.style.right = mini.style.bottom = 0; mini.style.backgroundColor = C("#05050a"); mv.Add(mini); img = new VisualElement { pickingMode = PickingMode.Ignore }; img.style.position = Position.Absolute; mini.Add(img); marks = new MiniMarks(); mini.Add(marks); }
            mini.style.display = A.Active ? DisplayStyle.Flex : DisplayStyle.None;
            if (!A.Active) return;
            var D = A.DM; if (miniId != D.id || miniOpen != A.open || !miniTex) { miniId = D.id; miniOpen = A.open; BuildMini(D); }
            float S = mini.contentRect.width; if (S > 1) { float k = S / 220f; img.style.left = (S - marks.w * k) / 2; img.style.top = (S - marks.h * k) / 2; img.style.width = marks.w * k; img.style.height = marks.h * k; }
            marks.MarkDirtyRepaint();
        }
        VisualElement img;
        void BuildMini(A.Map D)
        {
            int S = 220; float s = S / (float)Mathf.Max(D.W, D.H); int w = Mathf.CeilToInt(D.W * s), h = Mathf.CeilToInt(D.H * s);
            if (miniTex) Destroy(miniTex); miniTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var mm = C(D.mm); var px = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    float wx = x / s, wy = y / s; int k = A.Kind(wx, wy); Color32 col = new Color32(0, 0, 0, 0);
                    if (k == A.KF || k == A.KB || k == A.KI || k == A.KS || k == A.KT || k == A.KX) { int sc = D.sec[A.NavIdx(wx, wy)]; bool lit = sc < 0 || sc <= A.open; col = lit ? (k == A.KI || k == A.KT ? new Color32(150, 200, 230, 255) : (Color32)mm) : new Color32(30, 30, 38, 255); }
                    else if (k == A.KP) col = D.lava ? new Color32(150, 50, 14, 255) : new Color32(8, 6, 14, 255);
                    px[(h - 1 - y) * w + x] = col;
                }
            miniTex.SetPixels32(px); miniTex.Apply(); marks.tex = miniTex; marks.s = s; marks.w = w; marks.h = h; img.style.backgroundImage = Background.FromTexture2D(miniTex);
        }
        class MiniMarks : VisualElement
        {
            public Texture2D tex; public float s, w, h;
            public MiniMarks() { pickingMode = PickingMode.Ignore; style.position = Position.Absolute; style.left = style.top = style.right = style.bottom = 0; generateVisualContent += Gen; }
            void Gen(MeshGenerationContext ctx)
            {
                var D = A.DM; if (D == null || tex == null) return; float S = contentRect.width; if (!(S > 1)) return; float k = S / 220f;
                float ox = (S - w * k) / 2, oy = (S - h * k) / 2;
                var g = ctx.painter2D; float sc = s * k;
                g.strokeColor = C("#ff5a4a"); g.lineWidth = 2; foreach (var gt in D.gates) { if (gt.open) continue; g.BeginPath(); g.MoveTo(new Vector2(ox + gt.x1 * sc, oy + gt.y1 * sc)); g.LineTo(new Vector2(ox + gt.x2 * sc, oy + gt.y2 * sc)); g.Stroke(); }
                foreach (var m in AldaraMonsters.I.all)
                {
                    if (m.dead) continue; var c = new Vector2(ox + m.x * sc, oy + m.y * sc);
                    if (m.boss) { g.fillColor = C("#ff3a3a"); Sq(g, c, 3); } else { g.fillColor = C("#ff8a8a"); Sq(g, c, 1); }
                }
                var P = AldaraPlayer.I; g.fillColor = C("#6fc0ff"); g.BeginPath(); g.Arc(new Vector2(ox + P.x * sc, oy + P.y * sc), 3, 0, 360); g.Fill();
            }
            static void Sq(Painter2D g, Vector2 c, float r) { g.BeginPath(); g.MoveTo(c + new Vector2(-r, -r)); g.LineTo(c + new Vector2(r, -r)); g.LineTo(c + new Vector2(r, r)); g.LineTo(c + new Vector2(-r, r)); g.ClosePath(); g.Fill(); }
        }
    }
}
