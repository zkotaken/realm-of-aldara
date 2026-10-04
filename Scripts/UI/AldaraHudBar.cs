using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aldara
{
    // The pieces of the browser's bottom HUD that sit above the hotbar: the status strip (#buffs: every buff, debuff and
    // relic cooldown with its time left), the ability pops that rise off it (#abPops), the subclass bar (#subBar: the
    // signature, the two path abilities and the capstone on their own keys) and the walk / run tag (#runTag).
    public class AldaraHudBar : MonoBehaviour
    {
        public static AldaraHudBar I;
        VisualElement strip, pops, subBar, runTag; Label runLbl; bool built;
        void Awake() { I = this; AldaraRelics.Popped += OnRelicPop; }
        void OnDestroy() { AldaraRelics.Popped -= OnRelicPop; }
        static Color Hx(string h) { return AldaraRules.Hex(h); }
        static Font Cz(bool bold = true) { return AldaraUI.Font(true, bold); }
        static Texture2D Tex(string n) { return Resources.Load<Texture2D>(n); }
        static void Border(VisualElement e, float w, Color c, float r)
        {
            e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = w;
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = c;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }
        static Label L(VisualElement p, string t, float size, Color c, bool bold = true)
        {
            var l = new Label(t) { pickingMode = PickingMode.Ignore }; l.style.unityFontDefinition = FontDefinition.FromFont(Cz(bold)); l.style.fontSize = size; l.style.color = c;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0; l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0; l.style.whiteSpace = WhiteSpace.NoWrap; p.Add(l); return l;
        }
        static VisualElement Icon(VisualElement p, Texture2D t, Color tint, float size)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore }; e.style.width = e.style.height = size; e.style.flexShrink = 0; if (t) e.style.backgroundImage = Background.FromTexture2D(t); e.style.unityBackgroundImageTintColor = tint; p.Add(e); return e;
        }
        void Build()
        {
            var ui = AldaraHudUI.I; if (ui == null || ui.Root == null) return; built = true;
            strip = new VisualElement { name = "buffs", pickingMode = PickingMode.Ignore }; strip.style.flexDirection = FlexDirection.Row; strip.style.flexWrap = Wrap.Wrap; strip.style.justifyContent = Justify.Center; strip.style.alignItems = Align.FlexEnd;
            ui.PlaceAt(strip, new Rect(558, 880, 804, 22));
            pops = new VisualElement { name = "abPops", pickingMode = PickingMode.Ignore }; pops.style.flexDirection = FlexDirection.Row; pops.style.justifyContent = Justify.Center; pops.style.alignItems = Align.FlexEnd;
            ui.PlaceAt(pops, new Rect(560, 820, 800, 40));
            subBar = new VisualElement { name = "subBar" }; subBar.style.flexDirection = FlexDirection.Row; subBar.style.justifyContent = Justify.Center; subBar.style.alignItems = Align.FlexStart;
            ui.PlaceAt(subBar, new Rect(560, 868, 800, 58)); subBar.pickingMode = PickingMode.Ignore;
            runTag = new VisualElement { name = "runTag" }; Border(runTag, 1, new Color(214 / 255f, 178 / 255f, 94 / 255f, 0.55f), 9); runTag.style.backgroundColor = new Color(20 / 255f, 16 / 255f, 10 / 255f, 0.72f);
            runTag.style.paddingLeft = runTag.style.paddingRight = 9; runTag.style.paddingTop = runTag.style.paddingBottom = 3; runTag.tooltip = "Press Ctrl to switch between walking and running";
            runLbl = L(runTag, "", 12, Hx("#f2e6c8")); runTag.RegisterCallback<ClickEvent>(e => AldaraPlayer.ToggleRun());
            ui.PlaceAt(runTag, new Rect(12, 1080 - 12 - 22, 140, 22)); runTag.style.width = StyleKeyword.Auto; runTag.style.height = StyleKeyword.Auto;
        }

        // ---------- ability pops (stPop / stAbility) ----------
        class Pop { public VisualElement e; public float t, T; }
        readonly List<Pop> live = new List<Pop>();
        public static void PopAbility(string id)
        {
            if (!I || id == null) return; var H = AldaraHero.I;
            if (id.StartsWith("a") && id.Length == 2 && char.IsDigit(id[1])) { I.Add("Slash " + new[] { "I", "II", "III", "IV" }[Mathf.Clamp(id[1] - '1', 0, 3)], Tex("UI/tree/g_st_sword"), Hx("#dfe8ff"), true); return; }
            if (id.StartsWith("basic")) { bool ar = H.cls == "archer"; I.Add(ar ? "Shot" : "Bolt", Tex(ar ? "UI/tree/g_st_arrow" : "UI/tree/g_st_staff"), Hx("#cfe0ff"), true); return; }
            var sk = AldaraSkills.I ? AldaraSkills.I.Get(id) : null;
            if (sk != null) { var c = SkCol(sk.sub != null ? sk.baseId : sk.id); I.Add(sk.name, Tex("UI/icons/icon_" + (sk.sub != null ? sk.baseId : sk.id)) ?? Tex("UI/tree/g_" + id), c, false, sk.sub != null || sk.custom != null ? false : true); return; }
            I.Add(id.Replace('_', ' '), Tex("UI/tree/g_st_sword"), Hx("#ffd88a"), true);
        }
        static Color SkCol(string id)
        {
            switch (id) { case "fireball": return Hx("#ff8a3a"); case "ice_shard": case "frost_nova": return Hx("#8fdfff"); case "chain_lightning": return Hx("#cfe0ff"); case "meteor": return Hx("#ff6a1a"); case "arcane_missiles": case "arcane_cataclysm": return Hx("#c07aff"); case "blizzard": return Hx("#bfefff"); case "rejuvenate": return Hx("#7fffb0"); case "arcane_barrier": return Hx("#8ab4ff"); case "blink": return Hx("#b07aff"); }
            return Hx("#ffd88a");
        }
        void OnRelicPop(string name, string relic, string col)
        {
            Item it = AldaraGear.RelicWorn(relic); Add(name, it != null ? AldaraGear.RelicIcon(it) : null, Hx(col), false, false);
        }
        void Add(string name, Texture2D ico, Color c, bool small, bool tintIcon = true)
        {
            if (!built) return;
            var d = new VisualElement { pickingMode = PickingMode.Ignore }; d.style.flexDirection = FlexDirection.Row; d.style.alignItems = Align.Center; d.style.marginLeft = d.style.marginRight = 3;
            Border(d, 1, c, 8); d.style.backgroundColor = new Color(13 / 255f, 16 / 255f, 24 / 255f, 0.9f);
            d.style.paddingLeft = d.style.paddingRight = small ? 7 : 10; d.style.paddingTop = d.style.paddingBottom = small ? 2 : 4;
            if (ico) { var ie = Icon(d, ico, tintIcon ? c : Color.white, 16); ie.style.marginRight = 5; }
            L(d, name, small ? 10 : 12, c);
            pops.Add(d); live.Add(new Pop { e = d, T = small ? 0.9f : 1.5f });
            while (live.Count > 4) { live[0].e.RemoveFromHierarchy(); live.RemoveAt(0); }
        }

        // ---------- the status strip (stCollect) ----------
        struct Chip { public string key, name, t, cls; public Texture2D ico; public Color col; public bool tint; }
        string sig = "";
        List<Chip> Collect()
        {
            var B = new List<Chip>(); var H = AldaraHero.I; var P = AldaraPlayer.I;
            System.Func<float, string> sec = v => Mathf.CeilToInt(v) + "s";
            System.Action<string, string, Texture2D, Color, string, string, bool> chip = (k, n, i, c, t, cl, tn) => B.Add(new Chip { key = k, name = n, ico = i, col = c, t = t, cls = cl ?? "buff", tint = tn });
            System.Func<string, Texture2D> st = k => Tex("UI/tree/g_st_" + k);
            // the subclass's timed buffs first
            var S = AldaraTree.SubCur();
            foreach (var kv in AldaraSubclass.SUBB) { var b = kv.Value; if (b == null || b.name == null || !(b.t > 0)) continue; B.Insert(0, new Chip { key = "sub_" + kv.Key, name = b.name, ico = S != null ? Tex("UI/tree/g_sub_" + S.id) : st("sword"), col = Hx(b.col ?? "#ffffff"), t = sec(b.t), cls = "buff", tint = true }); }
            if (H.atkBuff > 0) chip("atk", "War Cry", Tex("UI/tree/g_war_cry"), Hx("#ff8a6a"), sec(H.atkBuff), null, true);
            if (H.hasteBuff > 0) chip("haste", "Eagle Eye", Tex("UI/tree/g_eagle_eye"), Hx("#9fe07f"), sec(H.hasteBuff), null, true);
            if (H.shield > 0) chip("shield", H.shieldName ?? "Barrier", st("barrier"), Hx("#8ab4ff"), Mathf.CeilToInt(H.shield) + (H.shieldT > 0 ? " · " + sec(H.shieldT) : ""), null, true);
            if (AldaraRelics.HasteCount > 0) chip("rhaste", "Haste x" + AldaraRelics.HasteCount, RelicIco("hourglass"), Hx("#ffe08a"), sec(AldaraRelics.HasteLeft), null, false);
            if (AldaraRelics.skel.Count > 0) { float mx = 0; foreach (var s in AldaraRelics.skel) mx = Mathf.Max(mx, s.life); chip("skel", "Skeletons " + AldaraRelics.skel.Count, st("skull"), Hx("#e8e4d0"), sec(mx), null, true); }
            float bs = AldaraGear.RelicV("berserkskull"); if (bs > 0 && H.hp < H.maxHp * 0.9f) { int k = Mathf.RoundToInt(bs * Mathf.Min(1, (1 - Mathf.Max(0, H.hp) / H.maxHp) / 0.9f)); if (k > 0) chip("rage", "Berserk", RelicIco("berserkskull"), Hx("#ff7a5a"), "+" + k + "%", null, false); }
            float wl = AldaraGear.RelicV("wisplantern"); if (wl > 0) chip("wisp", "Wisp Regen", RelicIco("wisplantern"), Hx("#a0ffd0"), wl + "%/s", null, false);
            if (!AldaraDungeon.Active && AldaraWorld.RoadDist(P.x, P.y) < 30) chip("road", "Traveller's Pace", st("haste"), Hx("#d8c8a0"), "+12%", null, true);
            var pet = H.Eq("pet"); if (pet != null) chip("pet", pet.name, st("pet"), pet.Col, "+" + Mathf.RoundToInt(AldaraItems.PetPct(pet) * 100) + "%", null, true);
            if (H.slowT > 0) chip("slow", "Slowed", st("slow"), Hx("#8fdfff"), sec(H.slowT), "debuff", true);
            if (P.liq == 2) chip("lava", "Burning", st("burn"), Hx("#ff7a2a"), "", "debuff", true);
            else if (P.liq != 0) chip("wade", "Wading", st("water"), Hx("#6ab0ff"), "", "debuff", true);
            if (H.VialCdHp > 0) chip("vial", "Vial", st("heal"), Hx("#ff8a7a"), sec(H.VialCdHp), "cd", true);
            foreach (var r in new[] { new[] { "phoenix", "Phoenix" }, new[] { "bonecrown", "Bone King" }, new[] { "frostheart", "Frost Nova" }, new[] { "seraphtear", "Seraph" } })
                if (AldaraGear.RelicWorn(r[0]) != null) { float cd = AldaraRelics.CdLeft(r[0]); if (cd > 0) chip("cd_" + r[0], r[1], RelicIco(r[0]), Hx("#9a9ab0"), sec(cd), "cd", false); }
            if (AldaraSubclass.undyingCd > 0) chip("sub_undying", "Undying Rage", st("heal"), Hx("#ff6a4a"), sec(AldaraSubclass.undyingCd), "cd", true);
            return B;
        }
        /// kbKeyName: the bound key, upper case
        static string KN(string a) { var k = AldaraKeys.KeyOf(a); return k == " " ? "Space" : string.IsNullOrEmpty(k) ? "-" : k.ToUpper(); }
        static Texture2D RelicIco(string id) { var it = AldaraGear.RelicWorn(id); return it != null ? AldaraGear.RelicIcon(it) : null; }
        void Strip()
        {
            var L0 = Collect(); var sb = new System.Text.StringBuilder(); foreach (var c in L0) sb.Append(c.key).Append(c.t).Append(c.name).Append('|');
            string s = sb.ToString(); if (s == sig) return; sig = s; strip.Clear();
            foreach (var c in L0)
            {
                var d = new VisualElement { pickingMode = PickingMode.Ignore }; d.style.flexDirection = FlexDirection.Row; d.style.alignItems = Align.Center; d.style.marginLeft = d.style.marginRight = 2; d.style.marginTop = 2;
                Border(d, 1, c.col, 6); d.style.backgroundColor = c.cls == "debuff" ? new Color(42 / 255f, 13 / 255f, 16 / 255f, 0.85f) : new Color(13 / 255f, 16 / 255f, 24 / 255f, 0.85f);
                if (c.cls == "cd") d.style.opacity = 0.7f; d.style.paddingLeft = 5; d.style.paddingRight = 7; d.style.paddingTop = d.style.paddingBottom = 2;
                if (c.ico) { var ie = Icon(d, c.ico, c.tint ? c.col : Color.white, 14); ie.style.marginRight = 4; }
                L(d, c.name, 10, Hx("#e8e6da")); if (!string.IsNullOrEmpty(c.t)) { var tl = L(d, c.t, 10, c.col); tl.style.marginLeft = 4; }
                strip.Add(d);
            }
        }

        // ---------- the subclass bar ----------
        string barKey = "";
        void Bar()
        {
            var L0 = AldaraSubclass.Slots(); var S = AldaraTree.SubCur(); var SK = AldaraSkills.I;
            var sb = new System.Text.StringBuilder(L0.Count > 0 ? S.id : "none"); foreach (var e in L0) sb.Append(e.sk != null ? e.sk.id + ":" + Mathf.CeilToInt(Mathf.Max(0, SK.Cd(e.sk.id)) * 4) : "x").Append(',');
            for (int i = 1; i <= 4; i++) sb.Append(KN("sub" + i));
            string key = sb.ToString(); if (key == barKey) return; barKey = key; subBar.Clear();
            subBar.style.display = L0.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None; if (L0.Count == 0) return;
            var sc = Hx(S.col);
            for (int i = 0; i < L0.Count; i++)
            {
                var e = L0[i]; bool cap = e.key == "c", lockd = e.sk == null; float sz = cap ? 58 : 50; string k = KN("sub" + (i + 1));
                var slot = new VisualElement(); slot.style.width = slot.style.height = sz; slot.style.marginLeft = slot.style.marginRight = 4.5f; Border(slot, cap ? 3 : 2, lockd ? Hx("#3a3f52") : sc, sz / 2);
                slot.style.backgroundColor = Hx("#10121c"); if (lockd) slot.style.opacity = 0.7f;
                var ico = new VisualElement { pickingMode = PickingMode.Ignore }; ico.style.position = Position.Absolute; ico.style.left = ico.style.top = ico.style.right = ico.style.bottom = 5 - (cap ? 3 : 2); Border(ico, 0, sc, sz);
                var ic = lockd ? new Color(0.12f, 0.12f, 0.12f) : Color.Lerp(sc, Color.black, 0.45f); float cd = e.sk != null ? Mathf.Max(0, SK.Cd(e.sk.id)) : 0; if (cd > 0) ic = Color.Lerp(ic, Color.black, 0.45f);
                ico.style.backgroundColor = ic; ico.style.alignItems = Align.Center; ico.style.justifyContent = Justify.Center; slot.Add(ico);
                var g = new VisualElement { pickingMode = PickingMode.Ignore }; g.style.width = g.style.height = (sz - 10) * 0.6f; AldaraSkillWin.Glyph(g, "sub_" + S.id, lockd ? new Color(0.5f, 0.5f, 0.5f) : Color.white); ico.Add(g);
                if (cd > 0 && e.sk.cd > 0)
                {
                    var pie = new CdPie { pickingMode = PickingMode.Ignore }; pie.style.position = Position.Absolute; pie.style.left = pie.style.top = pie.style.right = pie.style.bottom = 1; pie.Set(Mathf.Clamp01(cd / Mathf.Max(0.1f, e.sk.cd))); slot.Add(pie);
                    var cl = L(slot, Mathf.CeilToInt(cd).ToString(), 15, Color.white); cl.style.position = Position.Absolute; cl.style.left = cl.style.right = 0; cl.style.top = 0; cl.style.bottom = 0; cl.style.unityTextAlign = TextAnchor.MiddleCenter;
                    cl.style.textShadow = new TextShadow { color = Color.black, offset = new Vector2(0, 1), blurRadius = 3 };
                }
                var kb = new VisualElement { pickingMode = PickingMode.Ignore }; kb.style.position = Position.Absolute; kb.style.top = -7 - (cap ? 3 : 2); kb.style.left = sz / 2 - 11; kb.style.minWidth = 16; kb.style.height = 15; kb.style.paddingLeft = kb.style.paddingRight = 3;
                Border(kb, 1, lockd ? Hx("#3a3f52") : sc, 4); kb.style.backgroundColor = Hx("#0c0d14"); var kl = L(kb, k, 10, Color.white); kl.style.unityTextAlign = TextAnchor.MiddleCenter; slot.Add(kb);
                if (lockd)
                {
                    var lk = new VisualElement { pickingMode = PickingMode.Ignore }; lk.style.position = Position.Absolute; lk.style.bottom = -6 - 2; lk.style.left = sz / 2 - 16; lk.style.paddingLeft = lk.style.paddingRight = 3; lk.style.backgroundColor = Hx("#0c0d14");
                    lk.style.borderTopLeftRadius = lk.style.borderTopRightRadius = lk.style.borderBottomLeftRadius = lk.style.borderBottomRightRadius = 3; L(lk, "Lv " + e.n.lvl, 9, Hx("#c8c0a8"), false); slot.Add(lk);
                    slot.tooltip = e.n.n + ": learn it in your subclass path (level " + e.n.lvl + ", " + e.n.cost + " skill points)";
                }
                else slot.tooltip = e.sk.name + " (" + k + "): " + (e.sk.desc ?? "") + " No mana, " + e.sk.cd + "s cooldown.";
                int idx = i; slot.RegisterCallback<ClickEvent>(ev => AldaraSubclass.UseSlot(idx));
                subBar.Add(slot);
            }
        }

        void Update()
        {
            if (!built) { Build(); if (!built) return; }
            var H = AldaraHero.I; if (!AldaraSave.Ready || !H) return;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i]; p.t += Time.deltaTime; float u = p.t / p.T;
                if (u >= 1) { p.e.RemoveFromHierarchy(); live.RemoveAt(i); continue; }
                float y, s = 1, a; if (u < 0.15f) { float k = u / 0.15f; y = 8 * (1 - k); s = 0.6f + 0.5f * k; a = k; } else if (u < 0.3f) { float k = (u - 0.15f) / 0.15f; y = 0; s = 1.1f - 0.1f * k; a = 1; } else if (u < 0.7f) { float k = (u - 0.3f) / 0.4f; y = -12 * k; a = 1; } else { float k = (u - 0.7f) / 0.3f; y = -12 - 18 * k; a = 1 - k; }
                p.e.style.translate = new Translate(0, y); p.e.style.scale = new Scale(new Vector2(s, s)); p.e.style.opacity = a;
            }
            // the browser's bottom column: pops over the strip, the strip over the subclass bar (when it shows) over the hotbar
            bool barOn = AldaraSubclass.Slots().Count > 0; float stripTop = (barOn ? 868 - 2 - 4 : 934 - 4) - 22;
            strip.style.top = stripTop - 1080; pops.style.top = stripTop - 4 - 40 - 1080;
            Strip(); Bar();
            runLbl.text = (AldaraPlayer.Run ? "Running" : "Walking") + "  (Ctrl)";
        }
    }
}
