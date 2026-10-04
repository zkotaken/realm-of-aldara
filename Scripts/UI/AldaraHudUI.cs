using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json.Linq;

namespace Aldara
{
    // The browser game's HUD, rebuilt in UI Toolkit from art captured out of the browser (each piece's frame with its
    // moving parts hidden, the moving parts at full) and the browser's own layout: every rect and text style was measured
    // in the browser at 1920 x 1080, so pieces sit exactly where they do there and scale with the screen like it.
    public class AldaraHudUI : MonoBehaviour
    {
        public static AldaraHudUI I;
        UIDocument doc; VisualElement root; JObject L;
        readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        const float RW = 1920, RH = 1080;

        // live pieces
        VisualElement ufHpClip, ufMpClip, tfRoot, tfClip, xpClip, hpOrbClip, mpOrbClip, hpOrbImg, mpOrbImg, portraitEl, mmView, mmArrow, bannerWrap;
        Label ufName, ufLvl, ufHpT, ufMpT, stats, tfName, tfLvl, tfHpT, zone, orbHpT, orbMpT, xpT, banner, hpVialN, mpVialN;
        VisualElement hpVial, mpVial;
        readonly List<Slot> slots = new List<Slot>();
        class Slot { public VisualElement root, bgFilled, bgEmpty, icon; public CdPie cd; public Label key; public VisualElement badge; public string skill; }
        float ufHpW, ufMpW, tfW, xpW; Rect hpOrbR, mpOrbR;
        string portraitCls;

        void Awake() { I = this; }
        public VisualElement Root { get { return root; } }
        public VisualElement MinimapView { get { return mmView; } }
        /// a piece added from outside (the quest tracker), placed in the browser's 1920 x 1080 frame and shown with a HUD group
        public void AddPiece(string group, VisualElement e)
        {
            var r = new Rect(e.style.left.value.value, e.style.top.value.value, e.style.width.value.value, 10); e.RemoveFromHierarchy();
            var g = Group(r, out var o); g.Add(e); e.style.left = r.x - o.x; e.style.top = r.y - o.y;
            List<VisualElement> l; if (pieces.TryGetValue(group, out l)) l.Add(e);
        }
        void Start() { Build(); }
        /// place a piece from outside in the browser's 1920 x 1080 frame, hanging off the nearest corner or edge
        public VisualElement PlaceAt(VisualElement e, Rect r) { return Place(e, r); }

        // ---------- helpers ----------
        Font F(string css, string weight, string style)
        {
            bool bold = weight != null && int.TryParse(weight, out int w) && w >= 600;
            string fam = css.Contains("Alegreya") ? "Alegreya" : css.Contains("Decorative") ? "CinzelDecorative" : "Cinzel";
            string n = fam + (style == "italic" && fam == "Alegreya" ? "-Italic" : "") + (fam == "CinzelDecorative" ? "-Bold" : bold ? "-Bold" : "-Regular");
            if (!fonts.TryGetValue(n, out Font f)) { f = Resources.Load<Font>("Fonts/" + n); fonts[n] = f; }
            return f;
        }
        static Color Css(string c)
        {
            if (string.IsNullOrEmpty(c)) return Color.white;
            c = c.Trim(); if (c.StartsWith("#")) return AldaraRules.Hex(c);
            int a = c.IndexOf('('), b = c.IndexOf(')'); if (a < 0 || b < 0) return Color.white;
            var p = c.Substring(a + 1, b - a - 1).Split(',');
            float r = float.Parse(p[0]) / 255f, g = float.Parse(p[1]) / 255f, bl = float.Parse(p[2]) / 255f, al = p.Length > 3 ? float.Parse(p[3]) : 1;
            return new Color(r, g, bl, al);
        }
        static Rect R(JToken t) { return new Rect((float)t[0], (float)t[1], (float)t[2], (float)t[3]); }
        Rect ShotR(string n) { return R(L["shots"][n]["rect"]); }
        Rect RectR(string n) { return R(L["rects"][n]); }
        bool HasShot(string n) { return L["shots"][n] != null; }
        static Texture2D Tex(string n) { return Resources.Load<Texture2D>("UI/" + n); }

        // anchor groups: each piece hangs off the corner or edge it sits by in the browser, so it stays put on any screen
        readonly Dictionary<string, VisualElement> groups = new Dictionary<string, VisualElement>();
        List<VisualElement> collect; readonly Dictionary<string, List<VisualElement>> pieces = new Dictionary<string, List<VisualElement>>();
        void Begin(string k) { collect = new List<VisualElement>(); pieces[k] = collect; }
        /// HUD size, opacity and which pieces show, from the Interface settings
        public void ApplySettings()
        {
            if (root == null) return; var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var kv in groups)
            {
                string k = kv.Key; float s = AldaraSettings.F(k == "tr" ? "mmS" : k == "bl" ? "chatS" : "hudS", 1);
                kv.Value.style.scale = new Scale(new Vector2(s, s)); kv.Value.style.transformOrigin = new TransformOrigin(0, 0);
            }
            root.style.opacity = AldaraSettings.F("hudA", 1);
            Show("player", AldaraSettings.On("showPlayer")); Show("minimap", AldaraSettings.On("minimap")); Show("zone", AldaraSettings.On("showZone")); Show("chat", AldaraSettings.On("showChat"));
            showTarget = AldaraSettings.On("showTarget");
        }
        bool showTarget = true;
        void Show(string k, bool on) { List<VisualElement> l; if (pieces.TryGetValue(k, out l)) foreach (var e in l) e.style.visibility = on ? Visibility.Visible : Visibility.Hidden; }
        VisualElement Group(Rect r, out Vector2 origin)
        {
            float cx = r.center.x, cy = r.center.y;
            string h = cx < RW * 0.25f ? "l" : cx > RW * 0.75f ? "r" : "c", v = cy < RH * 0.5f ? "t" : "b";
            string k = v + h;
            if (!groups.TryGetValue(k, out var g))
            {
                g = new VisualElement { name = "grp_" + k, pickingMode = PickingMode.Ignore }; g.style.position = Position.Absolute; g.style.width = 0; g.style.height = 0;
                if (h == "l") g.style.left = 0; else if (h == "r") g.style.right = 0; else g.style.left = Length.Percent(50);
                if (v == "t") g.style.top = 0; else g.style.bottom = 0;
                root.Add(g); groups[k] = g;
            }
            origin = new Vector2(h == "l" ? 0 : h == "r" ? RW : RW / 2, v == "t" ? 0 : RH);
            return g;
        }
        VisualElement Place(VisualElement e, Rect r, VisualElement parent = null, Rect? parentRect = null)
        {
            e.style.position = Position.Absolute;
            if (parent == null) { var g = Group(r, out var o); g.Add(e); if (collect != null) collect.Add(e); e.style.left = r.x - o.x; e.style.top = r.y - o.y; }
            else { parent.Add(e); e.style.left = r.x - parentRect.Value.x; e.style.top = r.y - parentRect.Value.y; }
            e.style.width = r.width; e.style.height = r.height; return e;
        }
        VisualElement Img(string tex, Rect r, VisualElement parent = null, Rect? pr = null)
        {
            var e = new VisualElement { name = tex, pickingMode = PickingMode.Ignore }; var t = Tex(tex);
            if (t) e.style.backgroundImage = Background.FromTexture2D(t);
            return Place(e, r, parent, pr);
        }
        Label Txt(string key, VisualElement parent = null, Rect? pr = null, Color? overrideCol = null)
        {
            var t = L["texts"][key]; var l = new Label((string)t["text"]) { name = key, pickingMode = PickingMode.Ignore };
            l.style.unityFontDefinition = FontDefinition.FromFont(F((string)t["font"], (string)t["weight"], (string)t["style"]));
            l.style.fontSize = (float)t["size"];
            var col = Css((string)t["color"]); if (col.a < 0.05f) col = AldaraRules.Hex("#f0d68a"); l.style.color = overrideCol ?? col;
            string ls = (string)t["ls"]; if (ls != null && ls.EndsWith("px")) l.style.letterSpacing = float.Parse(ls.Replace("px", ""));
            string al = (string)t["align"]; l.style.unityTextAlign = al == "center" ? TextAnchor.MiddleCenter : al == "right" || al == "end" ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            string sh = (string)t["shadow"]; if (!string.IsNullOrEmpty(sh) && sh != "none") l.style.textShadow = new TextShadow { color = new Color(0, 0, 0, 0.9f), offset = new Vector2(0, 1), blurRadius = 3 };
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0; l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.whiteSpace = WhiteSpace.NoWrap; l.style.overflow = Overflow.Visible;
            if ((string)t["tt"] == "uppercase") l.text = l.text.ToUpper();
            Place(l, R(t["rect"]), parent, pr); return l;
        }
        VisualElement Clip(string fillTex, Rect full, out float w)
        {   // a bar fill: a clipped box whose width follows the value, holding the full fill image
            var clip = new VisualElement { pickingMode = PickingMode.Ignore }; clip.style.overflow = Overflow.Hidden; Place(clip, full);
            var img = new VisualElement { pickingMode = PickingMode.Ignore }; var t = Tex(fillTex); if (t) img.style.backgroundImage = Background.FromTexture2D(t);
            img.style.position = Position.Absolute; img.style.left = 0; img.style.top = 0; img.style.width = full.width; img.style.height = full.height; clip.Add(img);
            w = full.width; return clip;
        }

        // ---------- build ----------
        public void Build()
        {
            var ps = Resources.Load<PanelSettings>("UI/AldaraPanel");
            doc = gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>(); doc.panelSettings = ps;
            root = doc.rootVisualElement; root.Clear(); root.pickingMode = PickingMode.Ignore; root.style.flexGrow = 1;
            L = JObject.Parse(Resources.Load<TextAsset>("UI/layout").text);

            // ---- unit frame (top left) ----
            Begin("player");
            Img("unitframe", ShotR("unitframe"));
            portraitEl = Img("portrait_knight", RectR("portrait"));
            ufHpClip = Clip("uf_hp_fill", RectR("uf_hp_fillr"), out ufHpW); ufMpClip = Clip("uf_mp_fill", RectR("uf_mp_fillr"), out ufMpW);
            ufName = Txt("uf_name"); Diamond(R(L["texts"]["uf_lvl"]["rect"])); ufLvl = Txt("uf_lvl"); ufLvl.style.unityTextAlign = TextAnchor.MiddleCenter; ufLvl.style.textShadow = new TextShadow(); ufHpT = Txt("uf_hpt"); ufMpT = Txt("uf_mpt"); stats = Txt("uf_stats");
            ufHpT.style.unityTextAlign = ufMpT.style.unityTextAlign = TextAnchor.MiddleCenter; stats.style.whiteSpace = WhiteSpace.Normal; stats.style.unityTextAlign = TextAnchor.UpperLeft;
            collect = null;
            // ---- target frame (top centre) ----
            var tfr = ShotR("targetframe"); tfRoot = new VisualElement { pickingMode = PickingMode.Ignore }; Place(tfRoot, tfr);
            Img("targetframe", tfr, tfRoot, tfr);
            var fr = RectR("tf_fillr"); tfClip = new VisualElement { pickingMode = PickingMode.Ignore }; tfClip.style.overflow = Overflow.Hidden; Place(tfClip, fr, tfRoot, tfr);
            var tfi = new VisualElement { pickingMode = PickingMode.Ignore }; tfi.style.backgroundImage = Background.FromTexture2D(Tex("tf_fill")); tfi.style.position = Position.Absolute; tfi.style.width = fr.width; tfi.style.height = fr.height; tfClip.Add(tfi); tfW = fr.width;
            tfName = Txt("tf_name", tfRoot, tfr); tfLvl = Txt("tf_lvl", tfRoot, tfr); tfHpT = Txt("tf_hpt", tfRoot, tfr); tfHpT.style.unityTextAlign = TextAnchor.MiddleCenter;
            // ---- minimap and zone (top right) ----
            Begin("minimap");
            var mmr = RectR("minimap"); mmView = new VisualElement { pickingMode = PickingMode.Ignore }; Place(mmView, mmr);
            mmView.style.borderTopLeftRadius = mmView.style.borderTopRightRadius = mmView.style.borderBottomLeftRadius = mmView.style.borderBottomRightRadius = mmr.width / 2; mmView.style.overflow = Overflow.Hidden;
            mmArrow = new VisualElement { pickingMode = PickingMode.Ignore }; mmArrow.style.position = Position.Absolute; mmArrow.style.width = 10; mmArrow.style.height = 10; mmArrow.style.left = mmr.width / 2 - 5; mmArrow.style.top = mmr.height / 2 - 5;
            mmArrow.style.backgroundColor = new Color(0.55f, 0.85f, 1f); mmArrow.style.borderTopLeftRadius = mmArrow.style.borderTopRightRadius = mmArrow.style.borderBottomLeftRadius = mmArrow.style.borderBottomRightRadius = 5;
            mmArrow.style.borderLeftWidth = mmArrow.style.borderRightWidth = mmArrow.style.borderTopWidth = mmArrow.style.borderBottomWidth = 2; mmArrow.style.borderLeftColor = mmArrow.style.borderRightColor = mmArrow.style.borderTopColor = mmArrow.style.borderBottomColor = Color.white;
            mmView.Add(mmArrow); mmView.pickingMode = PickingMode.Position; mmView.RegisterCallback<ClickEvent>(e => { if (AldaraWindows.I) AldaraWindows.I.Toggle("map", true); });
            Img("minimap_ring", ShotR("minimap_ring"));
            Begin("zone"); Img("zone", ShotR("zone")); zone = Txt("zone");
            // ---- chat (bottom left) ----
            Begin("chat"); Img("chat", ShotR("chat")); collect = null;
            // ---- bottom bar: base, orb liquid, gloss, claws, slots, vials ----
            Img("ab", ShotR("ab"));
            hpOrbR = RectR("orb_hp"); mpOrbR = RectR("orb_mp");
            hpOrbClip = OrbClip("orb_hp_fill", hpOrbR, out hpOrbImg); mpOrbClip = OrbClip("orb_mp_fill", mpOrbR, out mpOrbImg);
            if (HasShot("orb_gloss")) Img("orb_gloss", ShotR("orb_gloss")); if (HasShot("orb_gloss_mp")) Img("orb_gloss_mp", ShotR("orb_gloss_mp"));
            if (HasShot("orbmount_hp")) Img("orbmount_hp", ShotR("orbmount_hp")); if (HasShot("orbmount_mp")) Img("orbmount_mp", ShotR("orbmount_mp"));
            orbHpT = Txt("orb_hpt"); orbMpT = Txt("orb_mpt");
            for (int i = 0; i < 10; i++)
            {
                var sr = RectR("slot" + i); var s = new Slot { root = new VisualElement { name = "slot" + i } }; Place(s.root, sr);
                var fsr = ShotR("slot_filled"); var esr = ShotR("slot_empty");
                s.bgEmpty = Img("slot_empty", new Rect(sr.x, sr.y, esr.width, esr.height), s.root, sr);
                s.bgFilled = Img("slot_filled", new Rect(sr.x, sr.y, fsr.width, fsr.height), s.root, sr);
                var ir = RectR("slot_ico"); var s0 = RectR("slot0");
                s.icon = new VisualElement { pickingMode = PickingMode.Ignore }; Place(s.icon, new Rect(sr.x + (ir.x - s0.x), sr.y + (ir.y - s0.y), ir.width, ir.height), s.root, sr);
                s.cd = new CdPie(); Place(s.cd, new Rect(sr.x + (ir.x - s0.x), sr.y + (ir.y - s0.y), ir.width, ir.height), s.root, sr);
                var kt = L["texts"]["slot_key"]; var kr = R(kt["rect"]); var k0 = RectR("slot0");
                s.badge = new VisualElement { pickingMode = PickingMode.Ignore }; Place(s.badge, new Rect(sr.x + kr.x - k0.x, kr.y, kr.width, kr.height), s.root, sr); Badge(s.badge); s.key = Txt("slot_key", s.root, sr); s.key.style.left = kr.x - k0.x; s.key.text = ((i + 1) % 10).ToString();
                int idx = i; s.root.RegisterCallback<ClickEvent>(e => { if (AldaraSkills.I) AldaraSkills.I.UseSlot(idx); });
                slots.Add(s);
            }
            autoBtn = Img("autobtn_off", RectR("autoBtn")); autoBtn.pickingMode = PickingMode.Position; autoBtn.RegisterCallback<ClickEvent>(e => AldaraAuto.Set(!AldaraAuto.on));
            hpVial = Img("vial_hp", RectR("hpVial")); mpVial = Img("vial_mp", RectR("mpVial"));
            var vn = L["texts"]["vial_n"]; var vr = R(vn["rect"]); var hvr = RectR("hpVial");
            hpVialN = Txt("vial_n", hpVial, hvr); mpVialN = Txt("vial_n", mpVial, hvr);
            hpVial.pickingMode = mpVial.pickingMode = PickingMode.Position;
            hpVial.RegisterCallback<ClickEvent>(e => AldaraHero.I.DrinkVial(true)); mpVial.RegisterCallback<ClickEvent>(e => AldaraHero.I.DrinkVial(false));
            // ---- experience ----
            Img("xp_bg", ShotR("xp_bg"));
            xpClip = Clip("xp_fill", HasShot("xp_fill") ? ShotR("xp_fill") : InnerXp(), out xpW);
            xpT = Txt("xp_txt");
            // ---- micro menu (bottom right): the browser's buttons open the windows ----
            Img("micromenu", ShotR("micromenu"));
            foreach (var kv in new Dictionary<string, string> { { "attBtn", "att" }, { "invBtn", "inv" }, { "skillsBtn", "skills" }, { "questBtn", "quest" }, { "mapBtn", "map" }, { "dunBtn", "dun" }, { "vaultBtn", "vault" }, { "guildBtn", "guild" }, { "coopBtn", "coop" }, { "setBtn", "settings" } })
            {
                var hit = new VisualElement { name = kv.Key }; Place(hit, RectR(kv.Key)); string w = kv.Value;
                hit.RegisterCallback<ClickEvent>(e => AldaraMenus.Open(w));
                hit.RegisterCallback<MouseEnterEvent>(e => hit.style.backgroundColor = new Color(1, 0.85f, 0.4f, 0.12f));
                hit.RegisterCallback<MouseLeaveEvent>(e => hit.style.backgroundColor = Color.clear);
            }
            // ---- banner ----
            banner = Txt("banner"); var bt = L["texts"]["banner"]; banner.style.left = 0; banner.style.width = RW; banner.style.left = -RW / 2; banner.style.opacity = 0;
            banner.style.textShadow = new TextShadow { color = AldaraRules.Hex("#3a2a0a"), offset = new Vector2(0, 2), blurRadius = 0 };
            SetupMinimapCamera(); collect = null;
            ApplySettings();
        }
        void Diamond(Rect r)
        {   // the gold level diamond under the portrait
            float d = r.width * 0.74f; var e = new VisualElement { pickingMode = PickingMode.Ignore }; Place(e, new Rect(r.center.x - d / 2, r.center.y - d / 2, d, d));
            Badge(e); e.style.rotate = new Rotate(new Angle(45));
        }
        static void Badge(VisualElement e)
        {
            e.style.backgroundColor = AldaraRules.Hex("#dcb25a"); var bc = AldaraRules.Hex("#4a3010");
            e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = 1.5f;
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = bc;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 3;
        }
        Rect InnerXp() { var r = ShotR("xp_bg"); return new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4); }
        VisualElement OrbClip(string tex, Rect r, out VisualElement img)
        {   // the liquid drains from the top: a box anchored at the orb's bottom holding the full liquid image
            var clip = new VisualElement { pickingMode = PickingMode.Ignore }; clip.style.overflow = Overflow.Hidden; Place(clip, r);
            img = new VisualElement { pickingMode = PickingMode.Ignore }; img.style.backgroundImage = Background.FromTexture2D(Tex(tex));
            img.style.position = Position.Absolute; img.style.left = 0; img.style.top = 0; img.style.width = r.width; img.style.height = r.height; clip.Add(img);
            clip.userData = clip.style.top.value.value;
            return clip;
        }
        static void SetOrb(VisualElement clip, VisualElement img, Rect r, float f) { f = Mathf.Clamp01(f); float top0 = (float)clip.userData; clip.style.top = top0 + r.height * (1 - f); clip.style.height = r.height * f; img.style.top = -r.height * (1 - f); }

        // ---------- per frame ----------
        float bannerT; string bannerText;
        public static void ShowBanner(string s) { if (I) { I.bannerText = s; I.bannerT = 1.4f; } }
        VisualElement autoBtn; bool autoShown;
        void Update()
        {
            if (root == null) return; var H = AldaraHero.I; var P = AldaraPlayer.I;
            root.style.display = AldaraSave.Ready && H && P && !(AldaraWindows.I && AldaraWindows.I.IsOpen("map")) ? DisplayStyle.Flex : DisplayStyle.None;
            if (!AldaraSave.Ready || !H) return;
            if (portraitCls != H.cls) { portraitCls = H.cls; var t = Tex("portrait_" + H.cls); if (t) portraitEl.style.backgroundImage = Background.FromTexture2D(t); }
            var Sc = AldaraSubclass.Cur; ufName.text = (H.heroName + " the " + (Sc != null ? Sc.name : char.ToUpper(H.cls[0]) + H.cls.Substring(1))).ToUpper(); ufLvl.text = H.lvl.ToString();
            ufHpClip.style.width = ufHpW * Mathf.Clamp01(H.hp / H.maxHp); ufMpClip.style.width = ufMpW * Mathf.Clamp01(H.mana / H.maxMana);
            ufHpT.text = Mathf.CeilToInt(Mathf.Max(0, H.hp)) + " / " + H.maxHp; ufMpT.text = Mathf.FloorToInt(H.mana) + " / " + H.maxMana;
            stats.text = "ATK " + H.atk + "   SPD " + Mathf.RoundToInt(H.speed) + "\nSTR " + H.str + "  AGI " + H.agi + "  VIT " + H.vit + "  ENE " + H.ene + "\nGold " + H.gold.ToString("N0");
            var t0 = H.target; bool show = t0 != null && !t0.dead;
            tfRoot.style.display = show && showTarget ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) { tfName.text = (t0.boss ? "Boss: " : "") + t0.name; tfLvl.text = "Lv " + t0.lvl; tfClip.style.width = tfW * Mathf.Clamp01(t0.hp / t0.maxHp); tfHpT.text = Mathf.CeilToInt(Mathf.Max(0, t0.hp)) + " / " + t0.maxHp; tfLvl.style.color = LvColor(t0.lvl - H.lvl); }
            zone.text = AldaraWorld.ZoneName(P.x, P.y);
            SetOrb(hpOrbClip, hpOrbImg, hpOrbR, H.hp / H.maxHp); SetOrb(mpOrbClip, mpOrbImg, mpOrbR, H.mana / H.maxMana);
            orbHpT.text = Mathf.CeilToInt(Mathf.Max(0, H.hp)).ToString(); orbMpT.text = Mathf.FloorToInt(H.mana).ToString();
            xpClip.style.width = xpW * Mathf.Clamp01(H.xp / H.xpNeed); xpT.text = H.xp.ToString("N0") + " / " + H.xpNeed.ToString("N0") + " XP";
            hpVial.style.display = H.vialHp > 0 ? DisplayStyle.Flex : DisplayStyle.None; mpVial.style.display = H.vialMp > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            hpVialN.text = H.vialHp.ToString(); mpVialN.text = H.vialMp.ToString();
            if (autoBtn != null && autoShown != AldaraAuto.on) { autoShown = AldaraAuto.on; var at = Tex(autoShown ? "autobtn_on" : "autobtn_off"); if (at) autoBtn.style.backgroundImage = Background.FromTexture2D(at); }
            var S = AldaraSkills.I;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i]; string id = S && i < S.equipped.Count ? S.equipped[i] : null; var sk = id != null ? S.Get(id) : null;
                s.bgFilled.style.display = sk != null ? DisplayStyle.Flex : DisplayStyle.None; s.bgEmpty.style.display = sk == null ? DisplayStyle.Flex : DisplayStyle.None;
                if (s.skill != id) { s.skill = id; var it = id != null ? Tex("icons/icon_" + id) : null; s.icon.style.backgroundImage = it ? Background.FromTexture2D(it) : null; }
                float cd = sk != null && sk.cd > 0 ? Mathf.Clamp01(S.Cd(sk.id) / sk.cd) : 0; s.cd.Set(cd);
                s.icon.style.opacity = sk != null && H.mana < sk.cost ? 0.45f : 1f; s.badge.style.display = sk != null ? DisplayStyle.Flex : DisplayStyle.None; s.key.style.color = sk != null ? new Color(0.1f, 0.063f, 0.03f) : new Color(0.56f, 0.52f, 0.44f);
            }
            if (bannerT > 0) { bannerT -= Time.deltaTime; banner.text = bannerText; banner.style.opacity = Mathf.Clamp01(bannerT * 2); } else banner.style.opacity = 0;
            UpdateMinimap();
        }
        static Color LvColor(int g) { return g >= 5 ? new Color(1, 0.3f, 0.3f) : g >= 3 ? new Color(1, 0.6f, 0.2f) : g >= -2 ? new Color(1, 0.9f, 0.4f) : g >= -8 ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.6f, 0.6f, 0.6f); }

        // ---------- minimap: a small top-down camera over the ground ----------
        Camera mmCam; RenderTexture mmRT;
        void SetupMinimapCamera()
        {
            mmRT = new RenderTexture(320, 320, 16) { name = "Minimap" };
            var go = new GameObject("MinimapCamera"); go.transform.SetParent(transform, false); mmCam = go.AddComponent<Camera>();
            mmCam.orthographic = true; mmCam.orthographicSize = 1400f / 2f / AldaraWorld.PX; mmCam.targetTexture = mmRT; mmCam.clearFlags = CameraClearFlags.SolidColor; mmCam.backgroundColor = new Color(0.12f, 0.3f, 0.48f);
            mmCam.cullingMask = 1 << 0; mmCam.nearClipPlane = 1; mmCam.farClipPlane = 400; mmCam.transform.rotation = Quaternion.Euler(90, 0, 0);
            mmView.style.backgroundImage = Background.FromRenderTexture(mmRT);
        }
        void UpdateMinimap()
        {
            var P = AldaraPlayer.I; if (!mmCam || !P) return;
            var p = AldaraWorld.ToUnityFlat(P.x, P.y); mmCam.transform.position = new Vector3(p.x, 200, p.z);
            mmArrow.style.rotate = new Rotate(new Angle(P.facing * Mathf.Rad2Deg + 90));
        }
    }

    // a cooldown sweep over a skill icon, drawn like the browser's (dark wedge shrinking clockwise)
    public class CdPie : VisualElement
    {
        float v;
        public CdPie() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
        public void Set(float f) { if (Mathf.Abs(f - v) > 0.002f || (f == 0) != (v == 0)) { v = f; MarkDirtyRepaint(); } }
        void Draw(MeshGenerationContext ctx)
        {
            if (v <= 0) return; var r = contentRect; var c = r.center; float rad = Mathf.Max(r.width, r.height) * 0.75f;
            var p = ctx.painter2D; p.fillColor = new Color(0, 0, 0, 0.62f);
            p.BeginPath(); p.MoveTo(c); p.Arc(c, rad, -90, -90 + 360 * v); p.ClosePath(); p.Fill();
        }
    }
}
