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
        VisualElement zoneImg; float zoneW0, zoneL0, zoneTW0, zoneTL0; string zoneLast;
        Label ufName, ufLvl, ufHpT, ufMpT, stats, tfName, tfLvl, tfHpT, zone, orbHpT, orbMpT, xpT, banner, hpVialN, mpVialN;
        VisualElement hpVial, mpVial;
        readonly List<Slot> slots = new List<Slot>();
        class Slot { public VisualElement root, bgFilled, bgEmpty, icon; public CdPie cd; public Label key; public VisualElement badge; public string skill; }
        float ufHpW, ufMpW, tfW, xpW; Rect hpOrbR, mpOrbR; LivingOrb orbHp, orbMp;
        string portraitCls;

        void Awake() { I = this; }
        public VisualElement Root { get { return root; } }
        public VisualElement MinimapView { get { return mmView; } }
        /// a piece added from outside (the quest tracker), placed in the browser's 1920 x 1080 frame and shown with a HUD group
        /// an element some other part of the HUD placed itself, counted into one of the pieces (for showing, hiding and moving)
        public void RegisterPiece(string group, VisualElement e) { List<VisualElement> l; if (!pieces.TryGetValue(group, out l)) pieces[group] = l = new List<VisualElement>(); if (!l.Contains(e)) l.Add(e); AldaraHudLayout.Apply(); }
        public List<VisualElement> Piece(string k) { List<VisualElement> l; return pieces.TryGetValue(k, out l) ? l : null; }
        public void AddPiece(string group, VisualElement e)
        {
            var r = new Rect(e.style.left.value.value, e.style.top.value.value, e.style.width.value.value, 10); e.RemoveFromHierarchy();
            var g = Group(r, out var o); g.Add(e); e.style.left = r.x - o.x; e.style.top = r.y - o.y;
            List<VisualElement> l; if (!pieces.TryGetValue(group, out l)) pieces[group] = l = new List<VisualElement>(); l.Add(e);
        }
        void Start() { Build(); }
        /// place a piece from outside in the browser's 1920 x 1080 frame, hanging off the nearest corner or edge
        public VisualElement PlaceAt(VisualElement e, Rect r) { return Place(e, r); }
        VisualElement chatClip;
        /// the chat box folded to its header (#chatBox.hidden)
        public void ChatHidden(bool h) { if (chatClip != null) chatClip.style.height = h ? 31 : ShotR("chat").height; }

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
            root.style.opacity = AldaraSettings.F("hudA", 1); zoneLast = null;
            Show("player", AldaraSettings.On("showPlayer")); Show("minimap", AldaraSettings.On("minimap")); Show("zone", AldaraSettings.On("showZone")); Show("chat", AldaraSettings.On("showChat"));
            showTarget = AldaraSettings.On("showTarget"); Show("quest", AldaraSettings.On("showQuest"));
            // text size (--txt: the chat, the quest box, the frames' names and the zone)
            float tx = Mathf.Clamp(AldaraSettings.F("txtS", 1), 0.5f, 2);
            foreach (var l in new VisualElement[] { ufName, tfName, zone }) if (l != null) { l.style.scale = new Scale(new Vector2(tx, tx)); l.style.transformOrigin = new TransformOrigin(Length.Percent(l == zone ? 50 : 0), Length.Percent(50)); }
            List<VisualElement> q; if (pieces.TryGetValue("quest", out q)) foreach (var e in q) { e.style.scale = new Scale(new Vector2(tx, tx)); e.style.transformOrigin = new TransformOrigin(Length.Percent(100), 0); }
            AldaraChat.TextScale(tx);
            // the colour themes: the browser's filters over the frames, the hotbar, the chat and the windows
            Theme(root);
            AldaraHudBar.ApplySettings(); AldaraHudLayout.Apply();
            // render resolution: Auto draws at the screen's own size; a number scales it
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (rp) { var rr = AldaraSettings.Get("renderRes"); string r = rr != null ? (string)rr : "auto"; float sc = r == "auto" ? 1 : r == "low" ? 0.5f : Mathf.Clamp(AldaraSettings.F("renderRes", 1), 0.25f, 2); rp.renderScale = sc; }
        }
        public static void Theme(VisualElement e)
        {
            if (e == null) return; var th = AldaraSettings.Get("theme"); string t = th != null ? (string)th : "gold"; var L = new List<FilterFunction>();
            System.Action<FilterFunctionType, float> F = (k, v) => { var f = new FilterFunction(k); f.AddParameter(new FilterParameter(v)); L.Add(f); };
            switch (t)
            {
                case "silver": F(FilterFunctionType.Grayscale, 0.88f); F(FilterFunctionType.Contrast, 1.05f); break;
                case "crimson": F(FilterFunctionType.HueRotate, -38); break;
                case "emerald": F(FilterFunctionType.HueRotate, 85); F(FilterFunctionType.Grayscale, 0.1f); break;
                case "void": F(FilterFunctionType.HueRotate, 225); break;
                case "frost": F(FilterFunctionType.HueRotate, 170); F(FilterFunctionType.Grayscale, 0.3f); break;
            }
            if (L.Count == 0) e.style.filter = StyleKeyword.Null; else e.style.filter = L;
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
            Begin("target"); var tfr = ShotR("targetframe"); tfRoot = new VisualElement { pickingMode = PickingMode.Ignore }; Place(tfRoot, tfr); collect = null;
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
            Begin("zone"); zoneImg = Img("zone", ShotR("zone")); zone = Txt("zone");
            // the banner grows with the name, as the browser's does (its pointed ends kept, the middle stretched)
            zoneImg.style.unitySliceLeft = 24; zoneImg.style.unitySliceRight = 24; zoneImg.style.unitySliceTop = 0; zoneImg.style.unitySliceBottom = 0;
            zoneW0 = zoneImg.style.width.value.value; zoneL0 = zoneImg.style.left.value.value; zoneTW0 = zone.style.width.value.value; zoneTL0 = zone.style.left.value.value;
            // ---- chat (bottom left) ----
            Begin("chat"); { var cr = ShotR("chat"); chatClip = new VisualElement { pickingMode = PickingMode.Ignore }; chatClip.style.overflow = Overflow.Hidden; Place(chatClip, cr); Img("chat", cr, chatClip, cr); } collect = null;
            // ---- bottom bar: base, orb liquid, gloss, claws, slots, vials ----
            Begin("bottom"); Img("ab", ShotR("ab"));
            hpOrbR = RectR("orb_hp"); mpOrbR = RectR("orb_mp");
            orbHp = new LivingOrb(true); orbMp = new LivingOrb(false); Place(orbHp.Element(), LivingOrb.Grow(hpOrbR)); Place(orbMp.Element(), LivingOrb.Grow(mpOrbR));
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
            collect = null;
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
        /// F8: the interface hidden for a clean view (Esc brings it back)
        public static bool Hidden;
        /// the zone name glows red on the Bloodmoor (#zone.pvp)
        public static void ZonePvp(bool on) { if (!I || I.zone == null) return; I.zonePvp = on; }
        bool zonePvp, zoneShown; Color zoneCol0; bool zoneCol0Set;
        void Update()
        {
            if (root == null) return; var H = AldaraHero.I; var P = AldaraPlayer.I;
            root.style.display = AldaraSave.Ready && H && P && !Hidden && !(AldaraWindows.I && AldaraWindows.I.IsOpen("map")) ? DisplayStyle.Flex : DisplayStyle.None;
            if (!AldaraSave.Ready || !H) return;
            if (portraitCls != H.cls) { portraitCls = H.cls; var t = Tex("portrait_" + H.cls); if (t) portraitEl.style.backgroundImage = Background.FromTexture2D(t); }
            var Sc = AldaraSubclass.Cur; ufName.text = (H.heroName + " the " + (Sc != null ? Sc.name : char.ToUpper(H.cls[0]) + H.cls.Substring(1))).ToUpper(); ufLvl.text = H.lvl.ToString();
            ufHpClip.style.width = ufHpW * Mathf.Clamp01(H.hp / H.maxHp); ufMpClip.style.width = ufMpW * Mathf.Clamp01(H.mana / H.maxMana);
            ufHpT.text = Mathf.CeilToInt(Mathf.Max(0, H.hp)) + " / " + H.maxHp; ufMpT.text = Mathf.FloorToInt(H.mana) + " / " + H.maxMana;
            stats.text = "ATK " + H.atk + "   SPD " + Mathf.RoundToInt(H.speed) + "\nSTR " + H.str + "  AGI " + H.agi + "  VIT " + H.vit + "  ENE " + H.ene + "\nGold " + H.gold.ToString("N0");
            var t0 = H.target; bool show = t0 != null && !t0.dead;
            tfRoot.style.display = show && showTarget ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) { tfName.text = (t0.boss ? "Boss: " : "") + t0.name; tfLvl.text = "Lv " + t0.lvl; tfClip.style.width = tfW * Mathf.Clamp01(t0.hp / t0.maxHp); tfHpT.text = Mathf.CeilToInt(Mathf.Max(0, t0.hp)) + " / " + t0.maxHp; tfLvl.style.color = LvColor(t0.lvl - H.lvl); }
            var zn = AldaraWorld.ZoneName(P.x, P.y);
            if (zn != zoneLast)
            {
                zoneLast = zn; zone.text = zn; float tx = Mathf.Clamp(AldaraSettings.F("txtS", 1), 0.5f, 2);
                float tw = zone.MeasureTextSize(zone.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x * tx;
                float w = Mathf.Max(zoneW0, tw + 46); zoneImg.style.width = w; zoneImg.style.left = zoneL0 + (zoneW0 - w) / 2;
                float lw = Mathf.Max(zoneTW0, tw / tx + 4); zone.style.width = lw; zone.style.left = zoneTL0 + (zoneTW0 - lw) / 2;
            }
            if (!zoneCol0Set) { zoneCol0 = zone.style.color.keyword == StyleKeyword.Undefined ? zone.style.color.value : zone.resolvedStyle.color; zoneCol0Set = true; }
            if (zonePvp != zoneShown) { zoneShown = zonePvp; zone.style.color = zonePvp ? AldaraRules.Hex("#ff8a7a") : zoneCol0; zone.style.textShadow = zonePvp ? new TextShadow { color = new Color(1, 60 / 255f, 40 / 255f, 0.7f), offset = Vector2.zero, blurRadius = 8 } : new StyleTextShadow(StyleKeyword.Null); }
            float odt = Mathf.Min(0.05f, Time.unscaledDeltaTime); orbHp.Tick(H.maxHp > 0 ? H.hp / H.maxHp : 0, odt); orbMp.Tick(H.maxMana > 0 ? H.mana / H.maxMana : 0, odt);
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
        /// levelColor: grey far below you, white even, yellow a bit above, orange tough, red very dangerous
        public static Color LvColor(int d) { return AldaraRules.Hex(d <= -6 ? "#8a8a8a" : d <= 2 ? "#e8e8e8" : d <= 4 ? "#ffe05a" : d <= 7 ? "#ff9a3a" : "#ff4a4a"); }

        // ---------- minimap (drawMinimap): the browser's 1/40 picture of the realm, 4200 world units across ----------
        public const float MM_VIEW = 4200f;
        VisualElement mmImg; MiniWorld mmDots; float mmWorldW;
        void SetupMinimapCamera()
        {
            mmView.style.backgroundColor = new Color(5 / 255f, 5 / 255f, 10 / 255f);
            var tex = Resources.Load<Texture2D>("map/minimap");
            mmImg = new VisualElement { pickingMode = PickingMode.Ignore }; mmImg.style.position = Position.Absolute; if (tex) { mmImg.style.backgroundImage = Background.FromTexture2D(tex); var md = AldaraMapWin.Map; mmWorldW = Mathf.Round(md.WORLD_W / 40f) * 40f; mmImg.userData = Mathf.Round(md.WORLD / 40f) * 40f; }
            mmView.Insert(0, mmImg); mmDots = new MiniWorld(); mmView.Insert(1, mmDots);
            mmArrow.style.width = mmArrow.style.height = 8; mmArrow.style.left = mmArrow.style.top = StyleKeyword.Null; mmArrow.style.borderLeftWidth = mmArrow.style.borderRightWidth = mmArrow.style.borderTopWidth = mmArrow.style.borderBottomWidth = 1;
            mmArrow.style.backgroundColor = AldaraRules.Hex("#6fc0ff");
        }
        void UpdateMinimap()
        {
            var P = AldaraPlayer.I; if (mmImg == null || !P) return; float S = mmView.contentRect.width; if (!(S > 1)) return; float s = S / MM_VIEW, ox = P.x - MM_VIEW / 2, oy = P.y - MM_VIEW / 2;
            mmImg.style.left = -ox * s; mmImg.style.top = -oy * s; mmImg.style.width = mmWorldW * s; mmImg.style.height = (mmImg.userData is float h ? h : 0) * s;
            mmArrow.style.left = S / 2 - 4; mmArrow.style.top = mmView.contentRect.height / 2 - 4; if (mmView.IndexOf(mmArrow) != mmView.childCount - 1) mmArrow.BringToFront();
            mmDots.MarkDirtyRepaint();
        }
        // the monsters (red dots, gold when your quest wants them), bosses (diamonds, pinned to the edge when away),
        // the way to Lorenmar when it is off the map, the screen's frame and the map waypoint
        class MiniWorld : VisualElement
        {
            public MiniWorld() { pickingMode = PickingMode.Ignore; style.position = Position.Absolute; style.left = style.top = style.right = style.bottom = 0; generateVisualContent += Gen; }
            static string qKind, qTarget; static int qZone;
            static void CurQuest()
            {
                qKind = null; qTarget = null; var inst = AldaraQuests.FocusInst(); if (inst == null || AldaraQuests.IsReady(inst)) return; var d = AldaraQuests.Def(inst);
                for (int k = 0; k < d.obj.Length; k++) if (!AldaraQuests.ObjDone(inst, k)) { var o = d.obj[k]; if (o.k == "kill" || o.k == "boss" || o.k == "collect") { qKind = "kill"; qTarget = o.t; } else if (o.k == "zone") { qKind = "zone"; qZone = o.z; } return; }
            }
            static bool QM(AldaraMonsters.Mon m) { return qKind == "kill" ? m.name == qTarget : qKind == "zone" && AldaraQuests.MonZone(m) == qZone; }
            void Gen(MeshGenerationContext ctx)
            {
                var P = AldaraPlayer.I; var M = AldaraMonsters.I; if (!P || AldaraWorld.Dun) return; float S = contentRect.width; if (!(S > 1)) return;
                float s = S / MM_VIEW, ox = P.x - MM_VIEW / 2, oy = P.y - MM_VIEW / 2; var g = ctx.painter2D;
                try { CurQuest(); } catch { qKind = null; }
                System.Func<float, float, bool> inView = (x, y) => x > ox && x < ox + MM_VIEW && y > oy && y < oy + MM_VIEW;
                if (M != null)
                {
                    var red = new Color(1, 138 / 255f, 138 / 255f, 0.6f); var gold = AldaraRules.Hex("#ffe05a");
                    foreach (var m in M.all) if (!m.dead && !m.boss && inView(m.x, m.y)) { bool q = QM(m); float r = q ? 1.5f : 1; Sq(g, (m.x - ox) * s, (m.y - oy) * s, r, q ? gold : red); }
                    foreach (var m in M.all)
                    {
                        if (!m.boss) continue; float x = (m.x - ox) * s, y = (m.y - oy) * s; bool edge = !inView(m.x, m.y);
                        if (edge) { x = Mathf.Clamp(x, 5, S - 5); y = Mathf.Clamp(y, 5, S - 5); } float r = edge ? 3.5f : 4.5f;
                        g.BeginPath(); g.MoveTo(new Vector2(x, y - r)); g.LineTo(new Vector2(x + r, y)); g.LineTo(new Vector2(x, y + r)); g.LineTo(new Vector2(x - r, y)); g.ClosePath(); g.lineWidth = 1;
                        if (m.dead) { g.strokeColor = AldaraRules.Hex("#777777"); g.Stroke(); } else { g.fillColor = QM(m) ? gold : AldaraRules.Hex("#ff3a3a"); g.Fill(); g.strokeColor = AldaraRules.Hex("#e0b64b"); g.Stroke(); }
                    }
                }
                float tx = (AldaraWorld.CENTER.x - ox) * s, ty = (AldaraWorld.CENTER.y - oy) * s;
                if (tx < 0 || tx > S || ty < 0 || ty > S) { g.fillColor = AldaraRules.Hex("#e0b64b"); g.BeginPath(); g.Arc(new Vector2(Mathf.Clamp(tx, 4, S - 4), Mathf.Clamp(ty, 4, S - 4)), 2.5f, 0, 360); g.Fill(); }
                // the screen's frame: the browser's 1920 x 1080 view
                float fw = 1920 * s, fh = 1080 * s; g.strokeColor = new Color(1, 1, 1, 0.25f); g.lineWidth = 1; g.BeginPath(); g.MoveTo(new Vector2(S / 2 - fw / 2, S / 2 - fh / 2)); g.LineTo(new Vector2(S / 2 + fw / 2, S / 2 - fh / 2)); g.LineTo(new Vector2(S / 2 + fw / 2, S / 2 + fh / 2)); g.LineTo(new Vector2(S / 2 - fw / 2, S / 2 + fh / 2)); g.ClosePath(); g.Stroke();
                var w = AldaraMapWin.wp;
                if (w != null)
                {
                    float x = (w.x - ox) * s, y = (w.y - oy) * s; bool edge = x < 6 || y < 6 || x > S - 6 || y > S - 6; x = Mathf.Clamp(x, 6, S - 6); y = Mathf.Clamp(y, 6, S - 6);
                    var c = AldaraRules.Hex("#8ff0a0"); g.strokeColor = c; g.lineWidth = 1.5f; g.BeginPath(); g.Arc(new Vector2(x, y), edge ? 3 : 4.5f, 0, 360); g.Stroke(); if (!edge) { g.fillColor = c; g.BeginPath(); g.Arc(new Vector2(x, y), 1.5f, 0, 360); g.Fill(); }
                }
            }
            static void Sq(Painter2D g, float x, float y, float r, Color c) { g.fillColor = c; g.BeginPath(); g.MoveTo(new Vector2(x - r, y - r)); g.LineTo(new Vector2(x + r, y - r)); g.LineTo(new Vector2(x + r, y + r)); g.LineTo(new Vector2(x - r, y + r)); g.ClosePath(); g.Fill(); }
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
