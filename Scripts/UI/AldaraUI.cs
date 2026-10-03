using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aldara
{
    // The browser's window kit in UI Toolkit. The boxes (panel, win, slots, item cards, buttons) are the browser's own
    // rendering captured at 2x with their contents hidden and drawn here as 9-slices, so gradients, inset shadows, bevels
    // and cut corners match; text uses the browser's fonts (Cinzel for headings and buttons, Alegreya for body text) and
    // the sizes and colours measured from the browser at 1920 x 1080.
    public static class AldaraUI
    {
        // ---------- fonts ----------
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        public static Font Font(bool cinzel, bool bold, bool italic = false)
        {
            string n = cinzel ? (bold ? "Cinzel-Bold" : "Cinzel-Regular") : italic ? "Alegreya-Italic" : bold ? "Alegreya-Bold" : "Alegreya-Regular";
            if (!fonts.TryGetValue(n, out Font f)) { f = Resources.Load<Font>("Fonts/" + n); fonts[n] = f; }
            return f;
        }
        // ---------- textures ----------
        static readonly Dictionary<string, Texture2D> texs = new Dictionary<string, Texture2D>();
        public static Texture2D Tex(string n)
        {
            if (!texs.TryGetValue(n, out Texture2D t) || !t) { t = Resources.Load<Texture2D>("UI/win/" + n) ?? Resources.Load<Texture2D>("UI/" + n); texs[n] = t; }
            return t;
        }
        public static Color C(string hex) { return AldaraRules.Hex(hex); }
        public static Color C(string hex, float a) { var c = AldaraRules.Hex(hex); c.a = a; return c; }

        // ---------- 9-slice skins (slice in CSS px; the art is 2x) ----------
        static readonly Dictionary<string, float> SLICE = new Dictionary<string, float> {
            { "panel", 14 }, { "win", 14 }, { "win_head", 6 }, { "eslot_bg", 8 }, { "eslot_line", 8 }, { "eslot_relic", 8 }, { "bp_bg", 10 }, { "bp_line", 8 },
            { "sup_row", 10 }, { "btn", 8 }, { "btn_discard", 8 }, { "btn_blue", 8 }, { "btn_buy", 8 }, { "btn_gold", 8 }, { "btn_tab", 12 }, { "btn_tab_on", 12 },
            { "select", 6 }, { "doll_btn", 7 }, { "worse_tag", 4 }, { "input", 6 }, { "tree_card", 10 }, { "dtab_on", 8 } };
        public static void Skin(VisualElement e, string tex, Color? tint = null)
        {
            var t = Tex(tex); if (!t) return; e.style.backgroundImage = Background.FromTexture2D(t);
            float s; if (!SLICE.TryGetValue(tex, out s)) s = 0;
            float scale = 2;
            int px = Mathf.RoundToInt(s * scale);
            e.style.unitySliceLeft = e.style.unitySliceRight = e.style.unitySliceTop = e.style.unitySliceBottom = px;
            e.style.unitySliceScale = 1f / scale;
            if (tint.HasValue) e.style.unityBackgroundImageTintColor = tint.Value;
        }
        /// the panel's gold frame: the browser's border-image SVG (160 units, slice 40, drawn 10px wide)
        public static void Frame(VisualElement e)
        {
            var t = Tex("frame"); if (!t) return; e.style.backgroundImage = Background.FromTexture2D(t);
            e.style.unitySliceLeft = e.style.unitySliceRight = e.style.unitySliceTop = e.style.unitySliceBottom = 160;
            e.style.unitySliceScale = 10f / 160f;
        }

        // ---------- elements ----------
        public static VisualElement E(VisualElement parent, string name = null)
        {
            var e = new VisualElement(); if (name != null) e.name = name; if (parent != null) parent.Add(e); return e;
        }
        public static VisualElement Row(VisualElement parent, float gap = 0, Justify j = Justify.FlexStart, Align a = Align.Center)
        {
            var e = E(parent); e.style.flexDirection = FlexDirection.Row; e.style.justifyContent = j; e.style.alignItems = a; if (gap > 0) Gap(e, gap); return e;
        }
        public static VisualElement Col(VisualElement parent, float gap = 0)
        {
            var e = E(parent); e.style.flexDirection = FlexDirection.Column; if (gap > 0) Gap(e, gap); return e;
        }
        /// CSS gap: a margin on every child after the first (applied as children are added and laid out)
        public static void Gap(VisualElement e, float g)
        {
            e.userData = g;
            e.RegisterCallback<GeometryChangedEvent>(ev => ApplyGap(e));
        }
        public static void ApplyGap(VisualElement e)
        {
            if (!(e.userData is float)) return; float g = (float)e.userData; bool row = e.resolvedStyle.flexDirection == FlexDirection.Row;
            bool first = true;
            foreach (var c in e.Children())
            {
                if (c.resolvedStyle.display == DisplayStyle.None || c.resolvedStyle.position == Position.Absolute) continue;
                float want = first ? 0 : g; first = false;
                if (row) { if (c.style.marginLeft.value.value != want) c.style.marginLeft = want; }
                else { if (c.style.marginTop.value.value != want) c.style.marginTop = want; }
            }
        }
        public static void Pad(VisualElement e, float t, float r, float b, float l) { e.style.paddingTop = t; e.style.paddingRight = r; e.style.paddingBottom = b; e.style.paddingLeft = l; }
        public static void Pad(VisualElement e, float v, float h) { Pad(e, v, h, v, h); }
        public static void Border(VisualElement e, float w, Color c, float radius = 0)
        {
            e.style.borderTopWidth = e.style.borderRightWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = w;
            e.style.borderTopColor = e.style.borderRightColor = e.style.borderBottomColor = e.style.borderLeftColor = c;
            if (radius > 0) Radius(e, radius);
        }
        public static void Radius(VisualElement e, float r) { e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r; }
        public static void BorderBottom(VisualElement e, float w, Color c) { e.style.borderBottomWidth = w; e.style.borderBottomColor = c; }

        public class TextStyle { public float size = 12; public Color col = C("#f3e6c4"); public bool cinzel, bold, italic, upper, wrap = true; public float ls; public TextAnchor align = TextAnchor.UpperLeft; public bool shadow; }
        public static Label T(VisualElement parent, string text, float size, Color col, bool cinzel = false, bool bold = false, bool italic = false, float ls = 0, bool upper = false, bool wrap = true)
        {
            var l = new Label(upper && text != null ? text.ToUpper() : text);
            l.style.unityFontDefinition = FontDefinition.FromFont(Font(cinzel, bold, italic));
            l.style.fontSize = size; l.style.color = col; if (ls != 0) l.style.letterSpacing = ls;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.whiteSpace = wrap ? WhiteSpace.Normal : WhiteSpace.NoWrap; l.enableRichText = true;
            l.pickingMode = PickingMode.Ignore;
            if (parent != null) parent.Add(l); return l;
        }
        public static void Shadow(Label l, Color c, float blur = 2, float dy = 1) { l.style.textShadow = new TextShadow { color = c, offset = new Vector2(0, dy), blurRadius = blur }; }
        public static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }
        public static string Span(string s, string hex) { return "<color=" + hex + ">" + s + "</color>"; }

        // ---------- buttons: the browser's gold-bevelled button (and its variants) ----------
        public class Btn : VisualElement
        {
            public Label label; public Action onClick; bool disabled; readonly string skin;
            public Btn(string text, Action click, string skin = "btn", float size = 10, bool cinzel = true, Color? col = null)
            {
                this.skin = skin; onClick = click; Skin(this, skin);
                style.flexDirection = FlexDirection.Row; style.justifyContent = Justify.Center; style.alignItems = Align.Center; style.flexShrink = 0;
                Pad(this, 4, 14, 4, 14);
                label = T(this, text, size, col ?? C("#f7e7bd"), cinzel, cinzel, false, cinzel ? 0.5f : 0, false, false);
                if (cinzel) Shadow(label, Color.black, 1, 1);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                RegisterCallback<ClickEvent>(e => { if (!disabled && onClick != null) { onClick(); e.StopPropagation(); } });
                RegisterCallback<MouseEnterEvent>(e => { if (!disabled) style.unityBackgroundImageTintColor = new Color(1.25f, 1.25f, 1.25f, 1); });
                RegisterCallback<MouseLeaveEvent>(e => { if (!disabled) style.unityBackgroundImageTintColor = Color.white; });
            }
            public bool Disabled
            {
                get { return disabled; }
                set { disabled = value; style.opacity = value ? 0.6f : 1; style.unityBackgroundImageTintColor = value ? new Color(0.62f, 0.6f, 0.58f, 1) : Color.white; label.style.color = value ? C("#a8a090") : C("#f7e7bd"); }
            }
            public Btn Size(float w, float h) { if (w > 0) style.width = w; if (h > 0) style.height = h; return this; }
            public Btn Padding(float v, float h) { Pad(this, v, h); return this; }
        }
        public static Btn B(VisualElement parent, string text, Action click, string skin = "btn", float size = 10)
        {
            var b = new Btn(text, click, skin, size); if (parent != null) parent.Add(b); return b;
        }

        /// is the pointer over a window or a clickable HUD piece (so a click there does not attack in the world)
        public static bool PointerOverUi(Vector2 screenPos)
        {
            foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var p = doc.rootVisualElement != null ? doc.rootVisualElement.panel : null; if (p == null) continue;
                var pos = RuntimePanelUtils.ScreenToPanel(p, new Vector2(screenPos.x, Screen.height - screenPos.y));
                var hit = p.Pick(pos);
                if (hit != null && hit != doc.rootVisualElement && hit.pickingMode == PickingMode.Position) return true;
            }
            return false;
        }
    }
}
