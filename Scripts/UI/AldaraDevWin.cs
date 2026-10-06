using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The developer menu (F10), behind a password: spawn any item, pet, wings, relic, mythic set or mount, spawn any
    // monster or boss, set levels, gold and points, teleport anywhere, and switch on god mode, one-hit kills or a
    // faster stride. Unlocked once per session.
    public class AldaraDevWin : AldaraWindows.Win
    {
        public const string PASSWORD = "lorb";
        public static bool Unlocked, God, OneShot; public static float SpeedK = 1;
        static string tab = "hero", itemType = "helmet", wcls = null; static int rar = 3, lvlPick = 0;
        static AldaraHero H { get { return AldaraHero.I; } }
        static AldaraPlayer P { get { return AldaraPlayer.I; } }

        public AldaraDevWin() { id = "dev"; title = "Developer"; }
        public override float Y => 50; public override float W => 820; public override float MaxH => 820;

        /// F10: the password first, then the menu
        public static void Open()
        {
            if (!AldaraWindows.I || !AldaraSave.Ready) return;
            if (Unlocked) AldaraWindows.I.Toggle("dev"); else AldaraWindows.I.Toggle("devpw");
        }
        public override void OnClose() { AldaraHud.Typing = false; }
        static void Msg(string s) { AldaraHud.Banner(s); }
        static void Give(Item it) { if (it == null) return; H.inventory.Add(it); AldaraSave.Dirty(); AldaraWindows.Refresh("inv"); Msg("Added " + it.name + " (" + it.rarity + ")"); }
        static int Lvl { get { return lvlPick > 0 ? lvlPick : Mathf.Max(1, H.lvl); } }
        static AldaraItems.Rar Rar { get { return AldaraItems.RARITY[rar]; } }

        public override void Render(VisualElement body)
        {
            var tabs = Row(body, 4); tabs.style.marginBottom = 10; tabs.style.flexWrap = Wrap.Wrap; tabs.style.flexShrink = 0;
            foreach (var t in new[] { new[] { "hero", "Hero" }, new[] { "items", "Items" }, new[] { "mounts", "Mounts" }, new[] { "monsters", "Monsters" }, new[] { "world", "World" } })
            { string k = t[0]; var b = B(tabs, t[1], () => { tab = k; dirty = true; }, tab == k ? "btn_gold" : "btn", 11); b.Padding(5, 16); if (tab == k) { Border(b, 2, C("#ffd35a"), 6); b.label.style.color = C("#fff2c0"); } }
            tabs.schedule.Execute(() => ApplyGap(tabs));
            var sv = new ScrollView(ScrollViewMode.Vertical); body.Add(sv); Thin(sv); sv.style.flexShrink = 1; var c = sv.contentContainer;
            switch (tab)
            {
                case "hero": HeroTab(c); break;
                case "items": ItemsTab(c); break;
                case "mounts": MountsTab(c); break;
                case "monsters": MonstersTab(c); break;
                case "world": WorldTab(c); break;
            }
        }

        // ---------- pieces ----------
        static VisualElement Section(VisualElement c, string name)
        {
            var h = T(c, name, 12, C("#efcf78"), true, true, false, 1, true, false); h.style.marginTop = 10; h.style.marginBottom = 6;
            var r = Row(c, 6); r.style.flexWrap = Wrap.Wrap; return r;
        }
        void Btn(VisualElement r, string text, System.Action fn, bool on = false) { var b = B(r, text, () => { fn(); dirty = true; }, on ? "btn_gold" : "btn", 10); b.Padding(4, 12); b.style.marginBottom = 6; b.style.marginRight = 6; if (on) { Border(b, 2, C("#ffd35a"), 6); b.label.style.color = C("#fff2c0"); } }
        static TextField Search(VisualElement c, string ph, System.Action<string> changed)
        {
            var f = new TextField(); c.Add(f); f.style.height = 28; f.style.marginBottom = 8; f.style.marginLeft = f.style.marginRight = 0;
            var inp = f.Q(className: "unity-text-field__input") ?? f.Q(TextField.textInputUssName);
            if (inp != null) { inp.style.backgroundColor = C("#0a0c12"); Border(inp, 1, C("#3a3f52"), 5); Pad(inp, 6, 9); inp.style.color = C("#e8e6da"); inp.style.unityFontDefinition = FontDefinition.FromFont(Font(false, false)); inp.style.fontSize = 13; }
            f.textEdition.placeholder = ph; f.textEdition.hidePlaceholderOnFocus = false;
            f.RegisterCallback<FocusInEvent>(e => AldaraHud.Typing = true); f.RegisterCallback<FocusOutEvent>(e => AldaraHud.Typing = false);
            f.RegisterValueChangedCallback(e => changed(e.newValue ?? "")); return f;
        }
        /// a grid of named buttons that a search box filters in place
        void Grid(VisualElement c, string ph, IEnumerable<KeyValuePair<string, System.Action>> items, string colHex = null)
        {
            var grid = Row(c, 6); grid.style.flexWrap = Wrap.Wrap; var all = new List<KeyValuePair<string, VisualElement>>();
            Search(c, ph, q => { q = q.Trim().ToLower(); foreach (var kv in all) kv.Value.style.display = q.Length == 0 || kv.Key.ToLower().Contains(q) ? DisplayStyle.Flex : DisplayStyle.None; });
            grid.BringToFront(); c.Remove(grid); c.Add(grid);
            foreach (var kv in items)
            {
                var fn = kv.Value; var b = B(grid, kv.Key, () => { fn(); }, "btn", 10); b.Padding(4, 10); b.style.marginBottom = 6; b.style.marginRight = 6;
                if (colHex != null) b.label.style.color = C(colHex); all.Add(new KeyValuePair<string, VisualElement>(kv.Key, b));
            }
        }
        static KeyValuePair<string, System.Action> KV(string k, System.Action a) { return new KeyValuePair<string, System.Action>(k, a); }

        // ---------- the hero ----------
        void HeroTab(VisualElement c)
        {
            T(c, H.heroName + "  -  level " + H.lvl + " " + H.cls + "  -  " + Mathf.RoundToInt(H.gold) + " gold  -  " + H.statPoints + " stat points  -  " + AldaraTree.sp + " skill points", 12, C("#d8d0bc"));
            var r = Section(c, "Level");
            foreach (int n in new[] { 1, 5, 10, 25, 50 }) { int k = n; Btn(r, "+" + k + (k == 1 ? " level" : " levels"), () => { for (int i = 0; i < k; i++) H.GainXp(Mathf.Max(1, H.xpNeed - H.xp) / (1 + AldaraTree.T("xp")) + 1); }); }
            r = Section(c, "Gold and points");
            Btn(r, "+10,000 gold", () => { H.gold += 10000; AldaraSave.Dirty(); }); Btn(r, "+1,000,000 gold", () => { H.gold += 1000000; AldaraSave.Dirty(); });
            Btn(r, "+100 stat points", () => { H.statPoints += 100; AldaraSave.Dirty(); }); Btn(r, "+10 skill points", () => { AldaraTree.sp += 10; AldaraTree.spTotal += 10; AldaraSave.Dirty(); });
            Btn(r, "Fill vials", () => { H.vialHp = AldaraItems.VIAL_MAX; H.vialMp = AldaraItems.VIAL_MAX; AldaraSave.Dirty(); });
            Btn(r, "Learn every class skill", () => { var S = AldaraSkills.I; foreach (var s in S.classSkills) if (!S.owned.Contains(s.id)) S.owned.Add(s.id); AldaraSave.Dirty(); Msg("Every skill learned"); });
            r = Section(c, "Health");
            Btn(r, "Full heal", () => { H.hp = H.maxHp; H.mana = H.maxMana; });
            Btn(r, God ? "God mode: on" : "God mode: off", () => { God = !God; Msg(God ? "God mode on: you take no damage" : "God mode off"); }, God);
            Btn(r, OneShot ? "One-hit kills: on" : "One-hit kills: off", () => { OneShot = !OneShot; Msg(OneShot ? "One-hit kills on" : "One-hit kills off"); }, OneShot);
            Btn(r, SpeedK > 1 ? "Fast travel speed: on" : "Fast travel speed: off", () => { SpeedK = SpeedK > 1 ? 1 : 2.5f; }, SpeedK > 1);
        }

        // ---------- items ----------
        void ItemsTab(VisualElement c)
        {
            var r = Section(c, "Rarity");
            for (int i = 0; i < AldaraItems.RARITY.Length; i++) { int k = i; Btn(r, AldaraItems.RARITY[k].k, () => rar = k, rar == k); }
            r = Section(c, "Item level");
            foreach (int L in new[] { 0, 10, 30, 50, 80, 100, 120 }) { int k = L; Btn(r, k == 0 ? "Your level (" + H.lvl + ")" : "Level " + k, () => lvlPick = k, lvlPick == k); }
            r = Section(c, "Kind");
            foreach (var t in new[] { "helmet", "chest", "gauntlets", "leggings", "boots", "weapon", "back", "accessory", "pet", "wings", "relic", "mythic" })
            { string k = t; string lab = k == "back" ? "Cape" : k == "mythic" ? "Mythic sets" : char.ToUpper(k[0]) + k.Substring(1); Btn(r, lab, () => itemType = k, itemType == k); }
            r = Section(c, "Quick");
            Btn(r, "10 random items", () => { for (int i = 0; i < 10; i++) Give(AldaraItems.RollItem(Lvl, Rar)); });
            Btn(r, "Best gear for every slot", () => { foreach (var t in new[] { "helmet", "chest", "gauntlets", "leggings", "boots", "back", "accessory" }) { var ns = AldaraItems.NamesOf(t); if (ns.Length > 0) Give(AldaraItems.Build(t, ns[Mathf.Min(ns.Length - 1, AldaraItems.BASE_NAME_COUNT - 1)], Rar, Lvl)); } var w = AldaraItems.WeaponNamesOf(H.cls); if (w.Length > 0) Give(AldaraItems.Build("weapon", w[w.Length - 1], Rar, Lvl, H.cls)); });
            Btn(r, "Equip the best I own", () => H.AutoEquipBest());
            Btn(r, "Empty the backpack", () => { H.inventory.Clear(); AldaraSave.Dirty(); AldaraWindows.Refresh("inv"); Msg("Backpack emptied"); });

            T(c, "Click a name to add it to your backpack (" + H.inventory.Count + " of " + AldaraItems.BACKPACK_MAX + " used; the developer menu ignores the limit).", 11, C("#8a8474")).style.marginTop = 10;
            var list = new List<KeyValuePair<string, System.Action>>();
            if (itemType == "weapon")
            {
                var cr = Row(c, 6); cr.style.marginTop = 6; cr.style.flexWrap = Wrap.Wrap;
                foreach (var cl in new[] { "knight", "mage", "archer" }) { string k = cl; Btn(cr, "For the " + k, () => wcls = k, (wcls ?? H.cls) == k); }
                string wc = wcls ?? H.cls; foreach (var n in AldaraItems.WeaponNamesOf(wc)) { string nm = n; list.Add(KV(nm, () => Give(AldaraItems.Build("weapon", nm, Rar, Lvl, wc)))); }
            }
            else if (itemType == "wings") foreach (var n in AldaraItems.NamesOf("wings")) { string nm = n; list.Add(KV(nm, () => Give(AldaraItems.BuildWings(nm, Rar, Lvl)))); }
            else if (itemType == "relic") foreach (var d in AldaraGear.AllRelics) { var dd = d; list.Add(KV(dd.name, () => Give(AldaraGear.BuildRelic(dd, Rar, Lvl)))); }
            else if (itemType == "mythic")
                foreach (var S in AldaraItems.MythicSets)
                {
                    var SS = S; list.Add(KV(SS.name + " (full set)", () => { for (int i = 0; i < SS.pieces.Length && i < AldaraItems.MythicSlots.Length; i++) Give(AldaraItems.BuildMythic(SS, i, Lvl)); }));
                    for (int i = 0; i < SS.pieces.Length && i < AldaraItems.MythicSlots.Length; i++) { int k = i; list.Add(KV(SS.pieces[k], () => Give(AldaraItems.BuildMythic(SS, k, Lvl)))); }
                }
            else if (itemType == "pet")
            {
                foreach (var n in AldaraItems.NamesOf("pet")) { string nm = n; list.Add(KV(nm, () => Give(AldaraItems.Build("pet", nm, Rar, Lvl)))); }
                foreach (var n in AldaraItems.NamesOf("mythpet")) { string nm = n; list.Add(KV(nm + " (mythical)", () => Give(AldaraItems.Build("pet", nm, AldaraItems.RARITY[4], Lvl)))); }
            }
            else foreach (var n in AldaraItems.NamesOf(itemType)) { string nm = n, ty = itemType; list.Add(KV(nm, () => Give(AldaraItems.Build(ty, nm, Rar, Lvl)))); }
            Grid(c, "Search names", list, AldaraItems.RARITY[rar].c);
        }

        // ---------- mounts ----------
        void MountsTab(VisualElement c)
        {
            T(c, "Mounts are not dropped anywhere yet. Give yourself one here, then press " + AldaraKeys.Name(AldaraKeys.KeyOf("mount")) + " to ride.", 12, C("#d8d0bc"));
            foreach (var d in AldaraMount.ALL)
            {
                var dd = d; bool own = AldaraMount.owned.Contains(d.id);
                var b = Row(c, 10); b.style.marginTop = 8; Pad(b, 8, 12); Border(b, 1, own ? C("#5a7a3a") : C("#2e3446"), 7); b.style.backgroundColor = C("#10131c");
                var col = Col(b, 2); col.style.flexGrow = 1;
                T(col, d.name + (AldaraMount.active == d.id ? "  (active)" : ""), 14, C("#efcf78"), true);
                T(col, d.desc + "  Speed: " + Mathf.RoundToInt(d.speed * 138) + "% of your running pace when galloping.", 11, C("#a8a090"));
                if (!own) Btn(b, "Give", () => AldaraMount.Give(dd.id));
                else
                {
                    Btn(b, "Ride", () => { AldaraMount.Choose(dd.id); if (!AldaraMount.Riding && AldaraMount.I) AldaraMount.I.Mount(); });
                    Btn(b, "Remove", () => AldaraMount.Take(dd.id));
                }
            }
        }

        // ---------- monsters ----------
        void MonstersTab(VisualElement c)
        {
            var r = Section(c, "Here");
            Btn(r, "Kill everything near me", () => AldaraMonsters.I.DevKillNear(P.x, P.y, 600));
            T(c, "Click a monster to spawn it a little in front of you, at its own level. Spawned monsters do not come back once killed.", 11, C("#8a8474"));
            var list = new List<KeyValuePair<string, System.Action>>();
            foreach (var d in AldaraMonsters.I.Book.Where(m => m.dun == 0).OrderBy(m => m.boss).ThenBy(m => m.tier).ThenBy(m => m.lv0))
            {
                var dd = d; string label = d.name + (d.boss != 0 ? "  (boss)" : "  Lv " + d.lv0);
                list.Add(KV(label, () => { float a = P.facing, dist = 140 + dd.r; var m = AldaraMonsters.I.DevSpawn(dd.name, P.x + Mathf.Cos(a) * dist, P.y + Mathf.Sin(a) * dist); if (m != null) Msg("Spawned " + dd.name); }));
            }
            foreach (var d in AldaraMonsters.I.Book.Where(m => m.dun != 0).OrderBy(m => m.tier))
            {
                var dd = d; list.Add(KV(d.name + "  (dungeon)", () => { float a = P.facing; var m = AldaraMonsters.I.DevSpawn(dd.name, P.x + Mathf.Cos(a) * (140 + dd.r), P.y + Mathf.Sin(a) * (140 + dd.r)); if (m != null) Msg("Spawned " + dd.name); }));
            }
            Grid(c, "Search monsters", list);
        }

        // ---------- the world ----------
        void WorldTab(VisualElement c)
        {
            var r = Section(c, "Places");
            Btn(r, "Lorenmar (town)", () => Go(AldaraWorld.TOWN_SPAWN.x, AldaraWorld.TOWN_SPAWN.y));
            Btn(r, "Attune every waystone", () => { AldaraWaystones.Load(); var a = new JArray(); foreach (var w in AldaraWaystones.ALL) a.Add(w.id); AldaraSave.Raw["waystones"] = a; AldaraSave.Dirty(); Msg("Every waystone attuned"); });
            Btn(r, "Save now", () => { AldaraSave.Save(); Msg("Saved"); });
            T(c, "Teleport to any waystone:", 11, C("#8a8474")).style.marginTop = 8;
            AldaraWaystones.Load(); var list = new List<KeyValuePair<string, System.Action>>();
            foreach (var w in AldaraWaystones.ALL.OrderBy(w => w.z)) { var ww = w; list.Add(KV(w.n, () => Go(ww.x + 70, ww.y + 30))); }
            Grid(c, "Search places", list);
        }
        static void Go(float x, float y)
        {
            if (AldaraDungeon.Active) { Msg("Leave the dungeon first"); return; }
            var f = AldaraWorld.BlockedAt(x, y) ? AldaraPlayer.FreeSpotNear(x, y) : new Vector2(x, y);
            P.x = f.x; P.y = f.y; H.target = null; if (AldaraPet.I) AldaraPet.I.placed = false; AldaraSave.Dirty();
        }
    }

    /// the developer menu's password
    public class AldaraDevPw : AldaraWindows.Win
    {
        TextField field; Label note;
        public AldaraDevPw() { id = "devpw"; title = "Developer"; }
        public override float Y => 200; public override float W => 380;
        public override void OnOpen() { dirty = true; }
        public override void OnClose() { AldaraHud.Typing = false; }
        public override void Render(VisualElement body)
        {
            T(body, "Enter the developer password.", 12, C("#d8d0bc")).style.marginBottom = 10;
            field = new TextField { isPasswordField = true, maskChar = '*' }; body.Add(field); field.style.height = 30; field.style.marginLeft = field.style.marginRight = 0;
            var inp = field.Q(className: "unity-text-field__input") ?? field.Q(TextField.textInputUssName);
            if (inp != null) { inp.style.backgroundColor = C("#0a0c12"); Border(inp, 1, C("#3a3f52"), 5); Pad(inp, 6, 9); inp.style.color = C("#e8e6da"); inp.style.fontSize = 14; }
            field.RegisterCallback<FocusInEvent>(e => AldaraHud.Typing = true); field.RegisterCallback<FocusOutEvent>(e => AldaraHud.Typing = false);
            field.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Try(); e.StopPropagation(); } }, TrickleDown.TrickleDown);
            var row = Row(body, 8, Justify.FlexEnd); row.style.marginTop = 12;
            note = T(row, "", 11, C("#ff8a6a")); note.style.flexGrow = 1;
            var b = B(row, "Unlock", Try, "btn_gold", 11); b.Padding(5, 18);
            field.schedule.Execute(() => field.Focus()).StartingIn(50);
        }
        void Try()
        {
            if (field == null) return;
            if ((field.value ?? "").Trim() == AldaraDevWin.PASSWORD)
            {
                AldaraDevWin.Unlocked = true; field.value = ""; AldaraHud.Typing = false;
                AldaraWindows.I.Toggle("devpw", false); AldaraWindows.I.Toggle("dev", true);
            }
            else { field.value = ""; if (note != null) note.text = "Wrong password"; field.Focus(); }
        }
    }
}
