using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The browser's Vault window (renderVault): your backpack beside the vault shared by every character on this computer,
    // filter tabs and a search, Store / Take, and gold in and out.
    public class AldaraVaultWin : AldaraWindows.Win
    {
        public AldaraVaultWin() { id = "vault"; title = "Vault"; }
        public override float Y => 78; public override float W => 880; public override float MaxH => 920;
        static AldaraHero Hr { get { return AldaraHero.I; } }
        string filter = "all", q = ""; float sl, sr; bool focusSearch;
        public override void OnOpen() { AldaraGear.VaultLoad(); }

        static readonly string[][] TABS = { new[] { "all", "All" }, new[] { "gear", "Gear" }, new[] { "pet", "Pets" }, new[] { "relic", "Relics" }, new[] { "wings", "Wings" }, new[] { "accessory", "Accessories" } };
        bool Match(Item it)
        {
            if (filter != "all") { if (filter == "gear" && new[] { "pet", "relic", "wings", "accessory" }.Contains(it.type)) return false; if (filter != "gear" && it.type != filter) return false; }
            string s = q.Trim().ToLower(); return s.Length == 0 || (it.name ?? "").ToLower().Contains(s) || (it.rarity ?? "").ToLower().Contains(s);
        }
        static TextField Input(VisualElement parent, string placeholder, string value, float w)
        {
            var f = new TextField { value = value }; parent.Add(f); f.style.width = w; f.style.height = 28; f.style.marginLeft = f.style.marginRight = f.style.marginTop = f.style.marginBottom = 0;
            var inp = f.Q(className: "unity-text-field__input") ?? f.Q(TextField.textInputUssName);
            if (inp != null) { inp.style.backgroundColor = C("#0a0c12"); Border(inp, 1, C("#3a3f52"), 5); Pad(inp, 6, 9); inp.style.color = C("#e8e6da"); inp.style.unityFontDefinition = FontDefinition.FromFont(Font(false, false)); inp.style.fontSize = 13; }
            f.textEdition.placeholder = placeholder; f.textEdition.hidePlaceholderOnFocus = false;
            f.RegisterCallback<FocusInEvent>(e => AldaraHud.Typing = true); f.RegisterCallback<FocusOutEvent>(e => AldaraHud.Typing = false);
            return f;
        }
        public override void OnClose() { AldaraHud.Typing = false; }

        public override void Render(VisualElement body)
        {
            var top = Row(body, 10, Justify.SpaceBetween); top.style.marginBottom = 10; top.style.flexWrap = Wrap.Wrap;
            var tabs = Row(top, 4); tabs.style.flexWrap = Wrap.Wrap;
            foreach (var t in TABS) { string k = t[0]; var b = B(tabs, t[1], () => { filter = k; dirty = true; }, filter == k ? "btn_gold" : "btn", 11); b.Padding(5, 14); if (filter == k) b.label.style.color = C("#ffe8a0"); }
            ApplyGapLater(tabs);
            var search = Input(top, "Search", q, 190);
            search.RegisterValueChangedCallback(e => { q = e.newValue ?? ""; focusSearch = true; dirty = true; });
            if (focusSearch) { focusSearch = false; search.schedule.Execute(() => { search.Focus(); search.SelectRange(q.Length, q.Length); }); }
            var cols = Row(body, 12, Justify.FlexStart, Align.Stretch);
            // backpack
            var a = Column(cols, Hr.heroName + "'s backpack", Hr.inventory.Count + " / " + AldaraItems.BACKPACK_MAX, false, out ScrollView la);
            var inv = Hr.inventory.Where(Match).ToList();
            if (inv.Count == 0) Empty(la, "Nothing here.");
            foreach (var it in inv) { var row = ItemRow(la, it); var item = it; Gold(row, "Store", () => AldaraGear.VaultStore(item)); }
            // vault
            var b2 = Column(cols, "Vault", AldaraGear.vaultItems.Count + " / " + AldaraGear.VAULT_MAX, true, out ScrollView lb);
            int shown = 0;
            for (int i = 0; i < AldaraGear.vaultItems.Count; i++)
            {
                var it = AldaraSave.ItemOf(AldaraGear.vaultItems[i]); if (it == null || !Match(it)) continue; shown++;
                int idx = i; var row = ItemRow(lb, it); Gold(row, "Take", () => AldaraGear.VaultTake(idx));
            }
            if (shown == 0) Empty(lb, "The vault is empty. Store items here, then log in with another character to take them.");
            ApplyGapLater(cols);
            la.verticalScroller.valueChanged += v => sl = v; lb.verticalScroller.valueChanged += v => sr = v;
            float s1 = sl, s2 = sr; la.schedule.Execute(() => { la.scrollOffset = new Vector2(0, s1); lb.scrollOffset = new Vector2(0, s2); });
            // gold
            var g = Row(body, 8); g.style.marginTop = 10; g.style.flexWrap = Wrap.Wrap;
            var gl = T(g, "Gold: you " + Span("<b>" + Mathf.Floor(Hr.gold).ToString("N0") + "</b>", "#ffd84a") + " · vault " + Span("<b>" + AldaraGear.vaultGold.ToString("N0") + "</b>", "#ffd84a"), 12, C("#c8b890"), false, false, false, 0, false, false);
            gl.style.flexGrow = 1;
            var amt = Input(g, "Amount", "", 120);
            B(g, "Store gold", () => GoldMove(amt.value, 1), "btn", 11).Padding(5, 14);
            B(g, "Take gold", () => GoldMove(amt.value, -1), "btn", 11).Padding(5, 14);
            ApplyGapLater(g);
            var ft = T(body, "Shared by every character on this computer. Link with a friend to send them items.", 11, C("#7a7464")); ft.style.marginTop = 8; ft.style.unityTextAlign = TextAnchor.MiddleCenter;
        }
        static void ApplyGapLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }
        static VisualElement Column(VisualElement parent, string title, string count, bool vault, out ScrollView list)
        {
            var c = E(parent); c.style.flexGrow = 1; c.style.flexBasis = 0; c.style.minWidth = 0; Border(c, 1, vault ? C("#6a5a30") : C("#2e3446"), 9); c.style.backgroundColor = C("#0e111c");
            var h = Row(c, 0, Justify.SpaceBetween); Pad(h, 8, 10); BorderBottom(h, 1, C("#2e3446"));
            T(h, title, 13, C("#e8c46a"), true, false, false, 0, false, false); T(h, count, 11, C("#8a8474"), true, false, false, 0, false, false);
            list = new ScrollView(ScrollViewMode.Vertical); c.Add(list); list.style.height = 712; list.contentContainer.style.paddingTop = list.contentContainer.style.paddingBottom = 6;
            list.contentContainer.style.paddingLeft = list.contentContainer.style.paddingRight = 6; list.horizontalScrollerVisibility = ScrollerVisibility.Hidden; list.mouseWheelScrollSize = 60; Thin(list);
            return c;
        }
        static void Empty(VisualElement list, string s) { var l = T(list, s, 12, C("#7a7464"), false, false, true); Pad(l, 18, 18); l.style.unityTextAlign = TextAnchor.MiddleCenter; }
        static VisualElement ItemRow(ScrollView list, Item it)
        {
            var r = Row(list.contentContainer, 8); Pad(r, 6, 8); Border(r, 1, C("#2a2e3c"), 6); r.style.borderLeftWidth = 3; r.style.borderLeftColor = it.Col; r.style.backgroundColor = C("#0e111a"); r.style.marginBottom = 5;
            var info = E(r); info.style.flexGrow = 1; info.style.minWidth = 0;
            var nr = Row(info, 4);
            if (it.type == "relic") { var ic = E(nr); ic.style.width = ic.style.height = 18; var tx = AldaraGear.RelicIcon(it); if (tx) ic.style.backgroundImage = Background.FromTexture2D(tx); }
            T(nr, it.name, 13, it.Col, false, true);
            string cls = it.type == "weapon" ? (string.IsNullOrEmpty(it.cls) ? "knight" : it.cls) : null;
            string lab = AldaraItems.SLOT_LABEL.ContainsKey(it.type) ? AldaraItems.SLOT_LABEL[it.type] : it.type == "relic" ? "Relic" : it.type;
            T(info, (it.lvl > 0 ? "Lv " + it.lvl + " · " : "") + lab + " · " + it.rarity + (cls != null ? " · " + char.ToUpper(cls[0]) + cls.Substring(1) + " only" : ""), 11, C("#8a8474"));
            string d = it.type == "relic" ? AldaraGear.RelicDesc(it) : AldaraItems.Desc(it); if (!string.IsNullOrEmpty(d)) T(info, d, 11, C("#bfe0a0"));
            ApplyGapLater(r);
            return r;
        }
        static void Gold(VisualElement row, string text, System.Action a) { var b = B(row, text, a, "btn_gold", 11); b.Padding(5, 14); b.label.style.color = C("#ffe8a0"); }
        static void GoldMove(string s, int dir)
        {
            long n; if (!long.TryParse(s, out n) || n <= 0) return; AldaraGear.VaultLoad();
            if (dir > 0) { n = System.Math.Min(n, (long)Hr.gold); if (n <= 0) return; AldaraGear.vaultGold += n; Hr.gold -= n; }
            else { n = System.Math.Min(n, AldaraGear.vaultGold); if (n <= 0) return; AldaraGear.vaultGold -= n; Hr.gold += n; }
            AldaraGear.VaultSave(); AldaraSave.Dirty(); AldaraHud.Banner((dir > 0 ? "Stored " : "Took ") + n.ToString("N0") + " gold");
        }
    }
}
