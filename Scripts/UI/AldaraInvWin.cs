using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The browser's Inventory window (renderInventory and the backpack tabs, gear comparison and dismantling added later
    // in the browser): equipment doll with the twelve slots around a turning preview of the hero, Auto-Equip, Character
    // and Records tables, Titles, Vials, and the Backpack with tabs, sorting, Equip / Vault / Discard and Dismantle.
    public class AldaraInvWin : AldaraWindows.Win
    {
        public AldaraInvWin() { id = "inv"; title = "Inventory"; }
        public override float Y => 30; public override float W => 1180; public override float H => 880;
        static AldaraHero Hr { get { return AldaraHero.I; } }

        bool dismantleMode, autoConfirm; readonly HashSet<Item> sel = new HashSet<Item>();
        string tab = "all", sort = "new";
        float leftScroll, rightScroll; ScrollView leftSv, rightSv;
        public override void OnOpen() { dismantleMode = false; autoConfirm = false; sel.Clear(); tab = PlayerPrefs.GetString("aldara/invtab", "all"); sort = PlayerPrefs.GetString("aldara/invsort", "new"); AldaraDoll.Show(true); }
        public override void OnClose() { AldaraDoll.Show(false); }

        static readonly Color GOLD = C("#e8c46a"), INK = C("#f3e6c4");

        // ---------- shared boxes ----------
        static VisualElement Win(VisualElement parent, out VisualElement head)
        {
            var w = E(parent, "win"); Skin(w, "win"); w.style.marginBottom = 12; w.style.overflow = Overflow.Hidden; w.style.flexShrink = 0;
            head = E(w, "win-head"); Skin(head, "win_head"); Pad(head, 6, 10); head.style.flexShrink = 0;
            return w;
        }
        static Label HeadText(VisualElement head, string s) { return T(head, s, 12, GOLD, true, true, false, 1, true, false); }
        static VisualElement BpHead(VisualElement head) { head.style.flexDirection = FlexDirection.Row; head.style.justifyContent = Justify.SpaceBetween; head.style.alignItems = Align.Center; head.style.height = 30; return head; }
        static VisualElement Tools(VisualElement head) { var t = Row(head, 5); return t; }
        static Btn ToolBtn(VisualElement parent, string text, Action a, string skin = "btn") { var b = B(parent, text, a, skin, 10); b.Padding(3, 14); return b; }

        public override void Render(VisualElement body)
        {
            var grid = Row(body, 14, Justify.FlexStart, Align.Stretch); grid.style.flexGrow = 1; grid.style.minHeight = 0;
            leftSv = new ScrollView(ScrollViewMode.Vertical); grid.Add(leftSv); leftSv.style.width = 460; leftSv.contentContainer.style.paddingRight = 4; Thin(leftSv);
            rightSv = new ScrollView(ScrollViewMode.Vertical); grid.Add(rightSv); rightSv.style.flexGrow = 1; rightSv.style.flexShrink = 1; rightSv.contentContainer.style.paddingRight = 4; Thin(rightSv);
            rightSv.style.marginLeft = 14;
            BuildLeft(leftSv.contentContainer); BuildRight(rightSv.contentContainer);
            float ls = leftScroll, rs = rightScroll;
            leftSv.schedule.Execute(() => { leftSv.scrollOffset = new Vector2(0, ls); rightSv.scrollOffset = new Vector2(0, rs); });
            leftSv.verticalScroller.valueChanged += v => leftScroll = v; rightSv.verticalScroller.valueChanged += v => rightScroll = v;
        }
        // ---------- left column ----------
        void BuildLeft(VisualElement col)
        {
            VisualElement head;
            var eqw = Win(col, out head); HeadText(head, "Equipment");
            var doll = Row(eqw, 12, Justify.Center, Align.FlexStart); Pad(doll, 14, 8);
            var lc = Col(doll, 9); lc.style.flexGrow = 1; lc.style.flexBasis = 0; lc.style.minWidth = 0;
            foreach (var s in AldaraItems.LEFT_SLOTS) ESlot(lc, s);
            var center = Col(doll, 4); center.style.width = 130; center.style.alignItems = Align.Center; center.style.paddingTop = 4; center.style.flexShrink = 0;
            AldaraDoll.Attach(center);
            var turn = Row(center, 3);
            DollBtn(turn, 0, () => AldaraDoll.Turn(-1)); var spin = DollBtn(turn, 1, () => { AldaraDoll.spin = !AldaraDoll.spin; dirty = true; }); DollBtn(turn, 2, () => AldaraDoll.Turn(1));
            if (AldaraDoll.spin) spin.style.unityBackgroundImageTintColor = new Color(0.75f, 1.2f, 0.7f, 1);
            var rc = Col(doll, 9); rc.style.flexGrow = 1; rc.style.flexBasis = 0; rc.style.minWidth = 0;
            foreach (var s in AldaraItems.RIGHT_SLOTS) ESlot(rc, s);
            var ae = E(eqw); Pad(ae, 0, 10, 10, 10);
            var aeb = B(ae, "Auto-Equip Best Loot", () => Hr.AutoEquipBest(), "btn_blue", 12); aeb.style.height = 32; aeb.label.style.color = C("#e8e6da");
            var note = T(ae, "Scores every item you own by attack, speed, and HP bonuses, and equips the strongest option for each slot.", 10, C("#888888")); note.style.marginTop = 6;

            var chw = Win(col, out head); HeadText(head, "Character");
            var st = Stats(chw);
            StatCell(st, "Attack", Hr.atk.ToString()); StatCell(st, "Max HP", Hr.maxHp.ToString()); StatCell(st, "Max MP", Hr.maxMana.ToString()); StatCell(st, "Speed", Hr.speed.ToString());
            StatCell(st, "Strength", Hr.effStr.ToString()); StatCell(st, "Agility", Hr.effAgi.ToString()); StatCell(st, "Vitality", Hr.effVit.ToString()); StatCell(st, "Energy", Hr.effEne.ToString());
            StatCell(st, "Gold", Mathf.Floor(Hr.gold).ToString()); StatCell(st, "Level", Hr.lvl.ToString());
            if (Hr.setBonusSet != null)
            {
                var g = C(Hr.setBonusSet.glow);
                var sb = T(chw, Hr.setBonusSet.name + " Set (" + Hr.setBonusCount + " / 6): +" + Mathf.RoundToInt(Hr.setBonusPct * 100) + "% Attack and HP", 12, g, true, true, false, 0.3f, true);
                sb.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(sb, 8, 6, 4, 6); Shadow(sb, new Color(g.r, g.g, g.b, 0.9f), 8, 0);
            }
            var rw = Win(col, out head); HeadText(head, "Records");
            st = Stats(rw);
            StatCell(st, "Enemies Slain", Hr.kills.ToString("N0")); StatCell(st, "Bosses Slain", Hr.bossKills.ToString("N0"));
            StatCell(st, "Deaths", Hr.deaths.ToString("N0")); StatCell(st, "Dungeons Cleared", AldaraGear.DunClearsTotal().ToString());

            var tw = Win(col, out head); BpHead(head);
            T(head, "Titles (" + AldaraGear.UnlockedTitles() + " / " + AldaraGear.TITLES.Length + ")", 12, GOLD, true, true, false, 1, true, false);
            var tools = Tools(head); if (Hr.title != null) ToolBtn(tools, "Remove Title", () => AldaraGear.EquipTitle(null));
            var tl = Col(tw, 5); Pad(tl, 10, 10);
            foreach (var t in AldaraGear.TITLES)
            {
                bool ok = t.ok(), on = Hr.title == t.id;
                var r = Row(tl, 8, Justify.SpaceBetween); Pad(r, 6, 10); Border(r, 1, on ? GOLD : C("#2a2f42"), 6); r.style.backgroundColor = on ? C("#2a2412", 0.8f) : C("#0d1018", 0.8f);
                if (!ok) r.style.opacity = 0.6f;
                var tx = E(r); tx.style.flexShrink = 1;
                var tn = T(tx, t.name, 12, ok ? C(t.col) : C("#6a6a72"), true, true, false, 0.3f); if (t.glow && ok) Shadow(tn, C(t.col, 0.55f), 6, 0);
                var td = T(tx, t.req, 10, C("#999999")); td.style.marginTop = 1;
                if (on) T(r, "Equipped", 10, GOLD, false, true);
                else if (ok) { string tid = t.id; ToolBtn(r, "Equip", () => AldaraGear.EquipTitle(tid)).Padding(4, 14); }
                else T(r, "Locked", 10, C("#777777"));
                ApplyGapLater(r);
            }
            ApplyGapLater(tl);
            foreach (var c in new[] { doll, lc, rc, center, turn }) ApplyGapLater(c);
        }
        static void ApplyGapLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }
        static VisualElement Stats(VisualElement win)
        {
            var g = Row(win, 0, Justify.SpaceBetween, Align.FlexStart); g.style.flexWrap = Wrap.Wrap; Pad(g, 10, 14); return g;
        }
        static void StatCell(VisualElement grid, string k, string v)
        {
            var c = Row(grid, 0, Justify.SpaceBetween); c.style.width = Length.Percent(47.4f); Pad(c, 2, 0); BorderBottom(c, 1, C("#2a3040")); c.style.marginBottom = 4;
            T(c, k, 11, C("#b8a070"), true, false, false, 0, true, false);
            T(c, v, 12, C("#fff2c4"), false, false, false, 0, false, false);
        }
        VisualElement DollBtn(VisualElement parent, int kind, Action a)
        {
            var b = E(parent); b.style.width = 28; b.style.height = 20; Skin(b, "doll_btn");
            var g = new Glyph(kind) { pickingMode = PickingMode.Ignore }; g.style.flexGrow = 1; b.Add(g);
            b.RegisterCallback<ClickEvent>(e => a()); b.RegisterCallback<MouseEnterEvent>(e => b.style.unityBackgroundImageTintColor = new Color(1.25f, 1.25f, 1.25f)); b.RegisterCallback<MouseLeaveEvent>(e => b.style.unityBackgroundImageTintColor = Color.white);
            return b;
        }
        /// the small turn-left / spin / turn-right marks on the doll buttons
        class Glyph : VisualElement
        {
            readonly int kind; public Glyph(int k) { kind = k; generateVisualContent += Draw; }
            void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D; var r = contentRect; var c = r.center; p.fillColor = C("#f7e7bd"); p.strokeColor = C("#f7e7bd"); p.lineWidth = 1.3f;
                if (kind == 1) { p.BeginPath(); p.Arc(c, 3.6f, 40, 330); p.Stroke(); p.BeginPath(); p.MoveTo(c + new Vector2(3.2f, -4.6f)); p.LineTo(c + new Vector2(4.2f, -1.4f)); p.LineTo(c + new Vector2(1.0f, -1.8f)); p.ClosePath(); p.Fill(); return; }
                float d = kind == 0 ? -1 : 1; p.BeginPath(); p.MoveTo(c + new Vector2(3.5f * d, 0)); p.LineTo(c + new Vector2(-2.5f * d, -3.5f)); p.LineTo(c + new Vector2(-2.5f * d, 3.5f)); p.ClosePath(); p.Fill();
            }
        }
        void ESlot(VisualElement col, string slot)
        {
            var it = Hr.Eq(slot); bool rl = slot.StartsWith("relic"); string label = AldaraItems.SLOT_LABEL[slot];
            var e = E(col, "eslot"); Pad(e, 6, 8); e.style.minHeight = 46;
            if (it == null && rl) Skin(e, "eslot_relic");
            else
            {
                Skin(e, "eslot_bg");
                var line = E(e); line.pickingMode = PickingMode.Ignore; line.style.position = Position.Absolute; line.style.left = line.style.top = line.style.right = line.style.bottom = 0;
                Skin(line, "eslot_line", it != null ? it.Col : C("#3a3f52"));
                if (it != null) { e.RegisterCallback<MouseEnterEvent>(ev => line.style.unityBackgroundImageTintColor = Color.Lerp(it.Col, Color.white, 0.25f)); e.RegisterCallback<MouseLeaveEvent>(ev => line.style.unityBackgroundImageTintColor = it.Col); }
                else { e.RegisterCallback<MouseEnterEvent>(ev => line.style.unityBackgroundImageTintColor = C("#5a6078")); e.RegisterCallback<MouseLeaveEvent>(ev => line.style.unityBackgroundImageTintColor = C("#3a3f52")); }
            }
            if (it == null)
            {
                T(e, label, 9, rl ? C("#a89ac8") : C("#a08a60"), true, false, false, 0.5f, true);
                var v = T(e, rl ? "No relic" : "Empty", 12, C("#666666"), false, false, true); v.style.marginTop = 2;
                return;
            }
            T(e, label + (rl ? " · " + it.rarity : (it.lvl > 0 ? " · Lv " + it.lvl : "")), 9, rl ? C("#a89ac8") : C("#a08a60"), true, false, false, 0.5f, true);
            var vr = Row(e, 4); vr.style.marginTop = 2; vr.style.alignItems = Align.FlexStart;
            if (rl) { var ic = E(vr); ic.style.width = ic.style.height = 18; ic.style.flexShrink = 0; var tx = AldaraGear.RelicIcon(it); if (tx) ic.style.backgroundImage = Background.FromTexture2D(tx); }
            var val = T(vr, it.name, 12, it.Col); val.style.flexShrink = 1;
            string d = rl ? AldaraGear.RelicDesc(it) : AldaraItems.Desc(it);
            if (!string.IsNullOrEmpty(d)) { var s = T(e, d, 10, rl ? C("#d8d0e8") : C("#88aa88")); s.style.marginTop = 1; }
            e.RegisterCallback<ClickEvent>(ev => Hr.Unequip(slot));
            e.tooltip = "Click to unequip";
            ApplyGapLater(vr);
        }

        // ---------- right column ----------
        static readonly string[][] TABS = { new[] { "all", "All" }, new[] { "weapon", "Weapons" }, new[] { "armor", "Armour" }, new[] { "back", "Back & Wings" }, new[] { "jewel", "Jewellery & Relics" }, new[] { "pet", "Pets" }, new[] { "other", "Other" } };
        static readonly string[] ARMOR = { "helmet", "chest", "gauntlets", "leggings", "boots" };
        static readonly string[] KNOWN = { "weapon", "helmet", "chest", "gauntlets", "leggings", "boots", "back", "wings", "accessory", "relic", "pet" };
        static bool InTab(string t, Item it)
        {
            switch (t)
            {
                case "weapon": return it.type == "weapon";
                case "armor": return Array.IndexOf(ARMOR, it.type) >= 0;
                case "back": return it.type == "back" || it.type == "wings";
                case "jewel": return it.type == "accessory" || it.type == "relic";
                case "pet": return it.type == "pet";
                case "other": return Array.IndexOf(KNOWN, it.type) < 0;
                default: return true;
            }
        }
        static readonly Dictionary<string, int> RANK = new Dictionary<string, int> { { "Common", 0 }, { "Rare", 1 }, { "Epic", 2 }, { "Legendary", 3 }, { "Mythical", 4 } };
        static int Rk(Item it) { int r; return it.rarity != null && RANK.TryGetValue(it.rarity, out r) ? r : 0; }
        static readonly string[][] SORTS = { new[] { "new", "Newest first" }, new[] { "old", "Oldest first" }, new[] { "rarity", "Rarity" }, new[] { "level", "Level" }, new[] { "name", "Name" }, new[] { "slot", "Slot" } };

        void BuildRight(VisualElement col)
        {
            VisualElement head;
            if (Hr.vialHp > 0 || Hr.vialMp > 0)
            {
                var vw = Win(col, out head); HeadText(head, "Vials");
                var sup = Col(vw, 6); Pad(sup, 10, 10);
                if (Hr.vialHp > 0) Supply(sup, "Health Vial x" + Hr.vialHp, "#ff8a7a", "Restores 40% HP. Key H.", () => Hr.DrinkVial(true));
                if (Hr.vialMp > 0) Supply(sup, "Mana Vial x" + Hr.vialMp, "#8aa8ff", "Restores 50% MP. Key M.", () => Hr.DrinkVial(false));
                ApplyGapLater(sup);
            }
            var inv = Hr.inventory; var worse = inv.Where(AldaraGear.IsWorse).ToList();
            var bw = Win(col, out head); BpHead(head);
            T(head, "Backpack (" + inv.Count + " / " + AldaraItems.BACKPACK_MAX + ")", 12, GOLD, true, true, false, 1, true, false);
            var tools = Tools(head);
            if (!dismantleMode) ToolBtn(tools, "Dismantle", () => { dismantleMode = true; autoConfirm = false; sel.Clear(); dirty = true; });
            var ad = ToolBtn(tools, autoConfirm ? "Confirm: dismantle " + worse.Count + " for " + worse.Sum(AldaraGear.ScrapValue) + " Gold" : "Auto Dismantle (" + worse.Count + ")", () =>
            {
                var w2 = Hr.inventory.Where(AldaraGear.IsWorse).ToList();
                if (w2.Count == 0) { autoConfirm = false; dirty = true; return; }
                if (!autoConfirm) { autoConfirm = true; dirty = true; return; }
                autoConfirm = false; AldaraGear.Dismantle(w2); dismantleMode = false; dirty = true;
            }, autoConfirm ? "btn_buy" : "btn");
            ad.Disabled = worse.Count == 0; ApplyGapLater(tools);
            if (dismantleMode)
            {
                var picked = inv.Where(sel.Contains).ToList();
                var bar = E(bw); Pad(bar, 8, 10); BorderBottom(bar, 1, C("#2a2f42")); bar.style.backgroundColor = C("#1a1420");
                T(bar, "Click gear and weapons to select them. " + Span(picked.Count.ToString(), "#e8c46a") + " selected, worth " + Span(picked.Sum(AldaraGear.ScrapValue) + " Gold", "#e8c46a") + ".", 11, C("#cccccc"));
                var db = Row(bar, 5); db.style.marginTop = 6; db.style.flexWrap = Wrap.Wrap;
                var ds = ToolBtn(db, "Dismantle Selected", () => { AldaraGear.Dismantle(Hr.inventory.Where(it => sel.Contains(it) && AldaraGear.CanDismantle(it)).ToList()); sel.Clear(); dismantleMode = false; dirty = true; }, "btn_buy"); ds.Disabled = picked.Count == 0;
                ToolBtn(db, "Select Worse Than Equipped", () => { foreach (var it in Hr.inventory) if (AldaraGear.IsWorse(it)) sel.Add(it); dirty = true; });
                ToolBtn(db, "Cancel", () => { dismantleMode = false; sel.Clear(); dirty = true; });
                ApplyGapLater(db);
            }
            // tabs with counts (empty tabs hidden except All and the open one), and the sort
            var tabs = Row(bw, 4); tabs.style.flexWrap = Wrap.Wrap; Pad(tabs, 8, 10, 0, 10);
            foreach (var t in TABS)
            {
                int c = inv.Count(it => InTab(t[0], it)); if (c == 0 && t[0] != "all" && t[0] != tab) continue;
                string k = t[0]; bool on = k == tab;
                var b = new Btn(t[1] + " " + Span(c.ToString(), on ? "#ffe2a0a6" : "#b8c0d0a6"), () => { tab = k; PlayerPrefs.SetString("aldara/invtab", k); dirty = true; }, on ? "btn_tab_on" : "btn_tab", 12, false, on ? C("#ffe2a0") : C("#b8c0d0"));
                b.Padding(3, 10); b.style.height = 24; tabs.Add(b);
            }
            var sp = E(tabs); sp.style.flexGrow = 1;
            var sortBtn = new Btn(SORTS.First(s => s[0] == sort)[1] + "  ▾", null, "select", 12, false, C("#d0d6e2")); sortBtn.Padding(3, 8); sortBtn.style.height = 26; tabs.Add(sortBtn);
            sortBtn.onClick = () => SortMenu(sortBtn);
            ApplyGapLater(tabs);
            // the list: two columns
            var list = Row(bw, 0, Justify.SpaceBetween, Align.Stretch); list.style.flexWrap = Wrap.Wrap; list.style.alignContent = Align.FlexStart; Pad(list, 10, 10);
            if (inv.Count == 0) { Empty(list, "No items yet. Defeat monsters to find equipment."); return; }
            var order = inv.Select((it, i) => new KeyValuePair<Item, int>(it, i)).ToList();
            Comparison<KeyValuePair<Item, int>> cmp;
            switch (sort)
            {
                case "old": cmp = (a, b) => a.Value.CompareTo(b.Value); break;
                case "rarity": cmp = (a, b) => Tie(-(Rk(a.Key) * 1e4 + a.Key.lvl), -(Rk(b.Key) * 1e4 + b.Key.lvl), a, b); break;
                case "level": cmp = (a, b) => Tie(-(a.Key.lvl * 10 + Rk(a.Key)), -(b.Key.lvl * 10 + Rk(b.Key)), a, b); break;
                case "name": cmp = (a, b) => { int r = string.CompareOrdinal(a.Key.name ?? "", b.Key.name ?? ""); return r != 0 ? r : a.Value.CompareTo(b.Value); }; break;
                case "slot": cmp = (a, b) => { int r = string.CompareOrdinal(SlotKey(a.Key), SlotKey(b.Key)); return r != 0 ? r : a.Value.CompareTo(b.Value); }; break;
                default: cmp = (a, b) => b.Value.CompareTo(a.Value); break;
            }
            order.Sort(cmp);
            int shown = 0;
            foreach (var kv in order) { if (!InTab(tab, kv.Key)) continue; Card(list, kv.Key); shown++; }
            if (shown == 0) Empty(list, "Nothing in this tab.");
        }
        static int Tie(double x, double y, KeyValuePair<Item, int> a, KeyValuePair<Item, int> b) { return x < y ? -1 : x > y ? 1 : a.Value.CompareTo(b.Value); }
        static string SlotKey(Item it) { return (it.type ?? "") + "|" + (9 - Rk(it)) + "|" + (999 - it.lvl); }
        static void Empty(VisualElement list, string s) { var l = T(list, s, 12, C("#888888")); l.style.width = Length.Percent(100); l.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(l, 14, 0); }
        void SortMenu(VisualElement anchor)
        {
            var menu = new GenericDropdownMenu();
            foreach (var s in SORTS) { string k = s[0]; menu.AddItem(s[1], k == sort, () => { sort = k; PlayerPrefs.SetString("aldara/invsort", k); dirty = true; }); }
            menu.DropDown(anchor.worldBound, anchor, DropdownMenuSizeMode.Auto);
        }
        static void Supply(VisualElement parent, string name, string col, string desc, Action use)
        {
            var r = Row(parent, 0, Justify.SpaceBetween); Skin(r, "sup_row"); Pad(r, 7, 10);
            var tx = E(r); T(tx, name, 12, C(col), false, true); var d = T(tx, desc, 10, C("#999999")); d.style.marginTop = 1;
            B(r, "Use", use, "btn", 10);
        }
        void Card(VisualElement list, Item it)
        {
            bool can = AldaraGear.CanDismantle(it), picked = sel.Contains(it);
            var c = Row(list, 8, Justify.SpaceBetween); c.style.width = Length.Percent(49.5f); c.style.marginBottom = 6; Skin(c, "bp_bg"); Pad(c, 8, 10);
            if (picked) c.style.unityBackgroundImageTintColor = new Color(1.6f, 0.75f, 0.7f);
            var line = E(c); line.pickingMode = PickingMode.Ignore; line.style.position = Position.Absolute; line.style.left = line.style.top = line.style.right = line.style.bottom = 0;
            Skin(line, "bp_line", picked ? C("#ff6a4a") : it.Col);
            if (dismantleMode)
            {
                if (!can) c.style.opacity = 0.45f;
                var chk = E(c); chk.style.width = chk.style.height = 16; chk.style.flexShrink = 0; Border(chk, 1, C("#888888"), 3);
                if (picked) { var x = T(chk, "X", 10, C("#ff8a7a")); x.style.unityTextAlign = TextAnchor.MiddleCenter; x.style.flexGrow = 1; }
                if (can) { c.RegisterCallback<ClickEvent>(e => { if (!sel.Remove(it)) sel.Add(it); dirty = true; }); c.RegisterCallback<MouseEnterEvent>(e => { if (!picked) c.style.unityBackgroundImageTintColor = new Color(1.15f, 1.15f, 1.2f); }); c.RegisterCallback<MouseLeaveEvent>(e => { if (!picked) c.style.unityBackgroundImageTintColor = Color.white; }); }
            }
            var info = E(c); info.style.flexGrow = 1; info.style.flexShrink = 1; info.style.minWidth = 0;
            var nr = Row(info, 4, Justify.FlexStart, Align.FlexEnd); nr.style.flexWrap = Wrap.Wrap;
            if (it.type == "relic") { var ic = E(nr); ic.style.width = ic.style.height = 20; var tx = AldaraGear.RelicIcon(it); if (tx) ic.style.backgroundImage = Background.FromTexture2D(tx); }
            T(nr, it.name, 12, it.Col, false, true);
            if (!dismantleMode && AldaraGear.IsWorse(it))
            {
                var wt = E(nr); Skin(wt, "worse_tag"); Pad(wt, 0, 4); wt.style.marginLeft = 4; wt.style.marginBottom = 1;
                T(wt, "Worse", 9, C("#ff9a8a"), false, false, false, 0, false, false);
            }
            string cls = it.type == "weapon" ? (string.IsNullOrEmpty(it.cls) ? "knight" : it.cls) : null;
            string meta = (it.lvl > 0 ? "Lv " + it.lvl + " · " : "") + (AldaraItems.SLOT_LABEL.ContainsKey(it.type) ? AldaraItems.SLOT_LABEL[it.type] : it.type == "relic" ? "Relic" : it.type) + " · " + it.rarity;
            if (cls != null) meta += " · " + (AldaraItems.CanUse(it, Hr.cls) ? Cap(cls) + " only" : Span(Cap(cls) + " only", "#ff8a7a"));
            var m = T(info, meta, 10, C("#999999")); m.style.marginTop = 1;
            if (!dismantleMode && AldaraSettings.On("compare")) { var cm = AldaraGear.Compare(it); if (cm != null) { var cl = T(info, cm, 11, C("#9aa3b5")); cl.style.marginTop = 2; } }
            string d = it.type == "relic" ? AldaraGear.RelicDesc(it) : AldaraItems.Desc(it);
            if (!string.IsNullOrEmpty(d)) { var s = T(info, d, 10, C("#88aa88")); s.style.marginTop = 1; }
            if (dismantleMode) { var dm = T(info, can ? "Dismantle for " + AldaraGear.ScrapValue(it) + " Gold" : "Pets, accessories and relics cannot be dismantled", 10, C("#999999")); dm.style.marginTop = 1; }
            else
            {
                var btns = Row(c, 5); btns.style.flexShrink = 0;
                var eb = B(btns, "Equip", () => Hr.EquipItem(it)); eb.Disabled = !AldaraItems.CanUse(it, Hr.cls); if (eb.Disabled) eb.tooltip = "Wrong class";
                var vb = B(btns, "Vault", () => AldaraGear.VaultStore(it)); vb.tooltip = "Store in the vault shared by all your characters";
                B(btns, "Discard", () => AldaraGear.Drop(it), "btn_discard");
                ApplyGapLater(btns);
            }
            ApplyGapLater(c); ApplyGapLater(nr);
        }
        static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1); }
        public override void Tick() { AldaraDoll.Tick(); }
    }
}
