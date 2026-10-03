using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The browser's title screen and start panels: the title menu (Log In, Settings, Quit) over drifting light shafts and
    // embers with a hero turning on a plinth, then Choose your character (Play, Delete with a second click to confirm,
    // Create New Character) and Enter your name and choose a class. A browser character in a slot the Unity copy has
    // not used yet can be brought over from that slot.
    public class AldaraTitle : MonoBehaviour
    {
        public static AldaraTitle I;
        UIDocument doc; VisualElement root, titleV, startV; string screen = "title"; int sel; readonly List<VisualElement> tmBtns = new List<VisualElement>();
        string newName = "", newCls; int armed = -1;
        const int MAX_CHARS = 3;

        void Awake() { I = this; }
        void Start()
        {
            var go = new GameObject("TitleUI"); go.SetActive(false); doc = go.AddComponent<UIDocument>(); doc.panelSettings = Resources.Load<PanelSettings>("UI/AldaraPanel"); doc.sortingOrder = 3; go.SetActive(true);
            root = doc.rootVisualElement; root.style.position = Position.Absolute; root.style.left = root.style.top = root.style.right = root.style.bottom = 0; root.pickingMode = PickingMode.Ignore;
            Show("title");
        }
        static Texture2D Tx(string n) { return Resources.Load<Texture2D>("UI/title/" + n); }
        static VisualElement Img(VisualElement p, string tex, float w, float h)
        {
            var e = E(p); e.style.width = w; e.style.height = h; var t = Tx(tex); if (t) e.style.backgroundImage = Background.FromTexture2D(t); e.style.flexShrink = 0; e.pickingMode = PickingMode.Ignore; return e;
        }
        public void Show(string s)
        {
            screen = s; armed = -1; root.Clear(); root.style.display = DisplayStyle.Flex;
            if (s == "title") BuildTitle(); else if (s == "select") BuildSelect(); else BuildNew();
        }
        public void Hide() { root.Clear(); root.style.display = DisplayStyle.None; if (hero) hero.SetActive(false); }

        // ---------- the title menu ----------
        void BuildTitle()
        {
            var bg = Img(root, "title_bg", 0, 0); bg.style.position = Position.Absolute; bg.style.left = bg.style.top = bg.style.right = bg.style.bottom = 0; bg.style.width = StyleKeyword.Auto; bg.style.height = StyleKeyword.Auto; bg.pickingMode = PickingMode.Position;
            var fx = new Fx(); fx.style.position = Position.Absolute; fx.style.left = fx.style.top = fx.style.right = fx.style.bottom = 0; fx.pickingMode = PickingMode.Ignore; root.Add(fx);
            // the hero on the plinth
            var right = E(root); right.style.position = Position.Absolute; right.style.right = 120; right.style.top = Length.Percent(50); right.style.translate = new Translate(0, Length.Percent(-50)); right.style.width = 380; right.style.height = 560; right.pickingMode = PickingMode.Ignore;
            var glow = Img(right, "tm_glow", 456, 224); glow.style.position = Position.Absolute; glow.style.left = -38; glow.style.bottom = Length.Percent(6); glow.style.opacity = 0.7f;
            var pl = Img(right, "tm_plinth", 266, 34); pl.style.position = Position.Absolute; pl.style.left = 57; pl.style.bottom = Length.Percent(5);
            var hv = E(right); hv.style.position = Position.Absolute; hv.style.left = hv.style.top = hv.style.right = hv.style.bottom = 0; hv.pickingMode = PickingMode.Ignore;
            var rt = HeroRT(); if (rt) hv.style.backgroundImage = Background.FromRenderTexture(rt);
            // the menu
            var left = Col(root); left.style.position = Position.Absolute; left.style.left = 140; left.style.top = Length.Percent(50); left.style.translate = new Translate(0, Length.Percent(-50)); left.style.width = 440;
            var crest = Img(left, "title_crest", 106, 106); crest.style.marginLeft = -25; crest.style.marginTop = -16; crest.style.marginBottom = -16;
            var logo = Img(left, "title_logo", 456, 126); logo.style.marginLeft = -8; logo.style.marginTop = 2; logo.style.marginBottom = -8;
            var rule = Row(left, 10); rule.style.marginTop = 12; rule.style.marginBottom = 26;
            Line(rule); T(rule, "An endless adventure awaits", 14, C("#b8a888"), false, false, true, 1, false, false); Line(rule);
            ApplyGapLater(rule);
            var btns = Col(left, 8); tmBtns.Clear();
            TmBtn(btns, "Log In", "Choose or create a hero", () => Show(AnySlot() ? "select" : "new"));
            TmBtn(btns, "Settings", "Graphics, sound and controls", () => { if (AldaraWindows.I) AldaraWindows.I.Toggle("set", true); });
            TmBtn(btns, "Quit", "Leave the realm", () => { Application.Quit(); });
            ApplyGapLater(btns); Focus(0);
            var foot = Row(root, 0, Justify.SpaceBetween); foot.style.position = Position.Absolute; foot.style.left = foot.style.right = 0; foot.style.bottom = 14; Pad(foot, 0, 24); foot.pickingMode = PickingMode.Ignore;
            T(foot, "Realm of Aldara", 10, C("#6a6050"), true, false, false, 2, true, false); T(foot, "Arrow keys and Enter, or click", 10, C("#6a6050"), true, false, false, 2, true, false);
        }
        static void ApplyGapLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }
        static void Line(VisualElement p) { var i = E(p); i.style.flexGrow = 1; i.style.height = 1; i.style.backgroundColor = C("#c8a860", 0.55f); }
        void TmBtn(VisualElement p, string text, string small, System.Action a)
        {
            int idx = tmBtns.Count; var b = Row(p, 12, Justify.FlexStart, Align.FlexEnd); Pad(b, 10, 14, 10, 26); b.style.borderLeftWidth = 1; b.style.borderLeftColor = C("#e8c46a", 0.15f);
            var dia = E(b); dia.name = "dia"; dia.style.position = Position.Absolute; dia.style.left = 8; dia.style.top = Length.Percent(50); dia.style.width = dia.style.height = 9; dia.style.marginTop = -4.5f;
            Border(dia, 1.5f, C("#e8c46a")); dia.style.rotate = new Rotate(45); dia.style.opacity = 0;
            var s = T(b, text, 22, C("#d8d0bc"), true, true, false, 3, true, false); s.name = "t";
            var sm = T(b, small, 12, C("#8a8070"), false, false, false, 0.5f, false, false); sm.name = "s"; sm.style.marginBottom = 3; sm.style.marginLeft = 12;
            b.RegisterCallback<MouseEnterEvent>(e => Focus(idx)); b.RegisterCallback<ClickEvent>(e => a()); b.userData = a;
            tmBtns.Add(b);
        }
        void Focus(int i)
        {
            sel = i;
            for (int k = 0; k < tmBtns.Count; k++)
            {
                var b = tmBtns[k]; bool on = k == i;
                b.style.borderLeftColor = on ? C("#e8c46a") : C("#e8c46a", 0.15f); b.style.paddingLeft = on ? 34 : 26;
                b.style.backgroundImage = on ? Background.FromTexture2D(Tx("tm_focus")) : new StyleBackground(StyleKeyword.None);
                b.Q("dia").style.opacity = on ? 1 : 0;
                var t = b.Q<Label>("t"); t.style.color = on ? C("#fff2c8") : C("#d8d0bc"); t.style.textShadow = on ? new TextShadow { color = new Color(1, 220 / 255f, 140 / 255f, 0.7f), blurRadius = 14 } : new TextShadow();
                b.Q<Label>("s").style.color = on ? C("#c8b890") : C("#8a8070");
            }
        }

        // ---------- the start panels ----------
        VisualElement StartPanel(float w, string sub)
        {
            var bg = Img(root, "start_bg", 0, 0); bg.style.position = Position.Absolute; bg.style.left = bg.style.top = bg.style.right = bg.style.bottom = 0; bg.style.width = StyleKeyword.Auto; bg.style.height = StyleKeyword.Auto; bg.pickingMode = PickingMode.Position;
            var wrap = E(root); wrap.style.position = Position.Absolute; wrap.style.left = wrap.style.top = wrap.style.right = wrap.style.bottom = 0; wrap.style.alignItems = Align.Center; wrap.style.justifyContent = Justify.Center; wrap.pickingMode = PickingMode.Ignore;
            var p = Col(wrap); p.style.width = w; Skin(p, "panel"); Pad(p, 40, 38, 40, 38); p.style.alignItems = Align.Stretch;
            var cr = Img(p, "start_crest", 88, 88); cr.style.alignSelf = Align.Center; cr.style.marginTop = -18; cr.style.marginBottom = -6;
            var h1 = Img(p, "start_h1", 396, 50); h1.style.alignSelf = Align.Center; h1.style.marginTop = -8; h1.style.marginBottom = -4;
            var s = T(p, sub, 12, C("#c8a860"), true, false, false, 1, true); s.style.unityTextAlign = TextAnchor.MiddleCenter; s.style.marginBottom = 20;
            return p;
        }
        bool AnySlot() { for (int i = 0; i < MAX_CHARS; i++) if (SlotInfo(i) != null) return true; return false; }
        class Slot { public string name, cls; public int lvl; public bool browser; }
        Slot SlotInfo(int i)
        {
            foreach (var dir in new[] { AldaraSave.UnityDir, AldaraSave.BrowserDir })
            {
                var p = AldaraSave.PathOf(dir, i); if (!File.Exists(p)) continue;
                try { var j = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(p)); var d = (j["data"] as Newtonsoft.Json.Linq.JObject) ?? j; return new Slot { name = (string)d["name"] ?? "?", cls = (string)d["cls"] ?? "knight", lvl = d["lvl"] == null ? 1 : (int)d["lvl"], browser = dir == AldaraSave.BrowserDir }; } catch { }
            }
            return null;
        }
        void BuildSelect()
        {
            var p = StartPanel(436, "Choose your character");
            var list = Col(p, 8); list.style.marginBottom = 14; int count = 0;
            for (int i = 0; i < MAX_CHARS; i++)
            {
                var s = SlotInfo(i); int slot = i;
                if (s == null) { var e = E(list); Border(e, 1, C("#2f3446"), 6); Pad(e, 12, 12); var l = T(e, "Empty slot", 11, C("#666666")); l.style.unityTextAlign = TextAnchor.MiddleCenter; continue; }
                count++;
                var card = Row(list, 10); Skin(card, "bp_bg"); Pad(card, 10, 12); Border(card, 1, C("#2e3444"), 6);
                var ci = E(card); ci.style.flexGrow = 1; ci.style.minWidth = 0;
                T(ci, s.name, 14, C("#e8c46a"), true, true, false, 0, true, false);
                var m = T(ci, "Level " + s.lvl + " " + char.ToUpper(s.cls[0]) + s.cls.Substring(1) + (s.browser ? " (from the browser game)" : ""), 11, C("#999999")); m.style.marginTop = 2;
                var play = B(card, s.browser ? "Import" : "Play", () => Play(slot), "btn", 11); play.Padding(7, 14);
                if (!s.browser)
                {
                    Btn del = null; del = B(card, armed == slot ? "Confirm delete" : "Delete", () => { if (armed != slot) { armed = slot; Show("select"); return; } Delete(slot); }, "btn_discard", 10); del.Padding(7, 8); del.label.style.color = C("#ff9a8a");
                }
                ApplyGapLater(card);
            }
            ApplyGapLater(list);
            bool full = count >= MAX_CHARS;
            if (!full) { var c = B(p, "Create New Character", () => Show("new"), "btn", 13); c.Padding(10, 14); c.style.height = 35; }
            var n = T(p, full ? "All " + MAX_CHARS + " character slots are used. Delete one to make room." : count + " of " + MAX_CHARS + " character slots used", 11, C("#888888")); n.style.marginTop = 8; n.style.unityTextAlign = TextAnchor.MiddleCenter;
            var back = B(p, "Back to Menu", () => Show("title"), "btn", 11); back.Padding(4, 4); back.style.marginTop = 10; back.style.height = 20; back.label.style.color = C("#888888"); back.style.unityBackgroundImageTintColor = new Color(1, 1, 1, 0.5f);
        }
        void Play(int slot)
        {
            if (!File.Exists(AldaraSave.PathOf(AldaraSave.UnityDir, slot))) AldaraSave.Import(slot);
            Hide(); AldaraSave.Load(slot);
        }
        void Delete(int slot)
        {
            var p = AldaraSave.PathOf(AldaraSave.UnityDir, slot);
            if (File.Exists(p)) { File.Move(p, p + ".deleted-" + System.DateTime.Now.ToString("yyyyMMddHHmmss")); }   // kept aside, never destroyed
            armed = -1; Show(AnySlot() ? "select" : "new");
        }
        void BuildNew()
        {
            var p = StartPanel(456, "Enter your name and choose a class");
            var tf = new TextField { value = newName, maxLength = 18 }; p.Add(tf); tf.style.marginBottom = 12; tf.style.marginLeft = tf.style.marginRight = tf.style.marginTop = 0; tf.style.height = 37;
            var inp = tf.Q(className: "unity-text-field__input");
            if (inp != null) { inp.style.backgroundColor = C("#0a0b10"); Border(inp, 1, C("#6a4a1a"), 6); Pad(inp, 9, 10); inp.style.color = C("#f3e6c4"); inp.style.unityFontDefinition = FontDefinition.FromFont(Font(true, true)); inp.style.fontSize = 15; inp.style.unityTextAlign = TextAnchor.MiddleCenter; }
            tf.textEdition.placeholder = "Your name";
            tf.RegisterCallback<FocusInEvent>(e => AldaraHud.Typing = true); tf.RegisterCallback<FocusOutEvent>(e => AldaraHud.Typing = false);
            Btn begin = null;
            tf.RegisterValueChangedCallback(e => { newName = e.newValue ?? ""; if (begin != null) begin.Disabled = !Valid(); });
            tf.schedule.Execute(() => tf.Focus());
            var cl = T(p, "Class", 11, C("#888888"), false, false, false, 0.5f, true); cl.style.marginBottom = 6;
            var grid = Row(p, 8, Justify.FlexStart, Align.Stretch); grid.style.marginBottom = 16;
            foreach (var c in new[] { new[] { "knight", "Knight", "Heavy melee fighter. High Strength and Vitality." }, new[] { "mage", "Mage", "Spellcaster. High Energy fuels powerful attacks." }, new[] { "archer", "Archer", "Fast ranged fighter. High Agility." } })
            {
                string id = c[0]; bool on = newCls == id;
                var card = Col(grid); card.style.flexGrow = 1; card.style.flexBasis = 0; Pad(card, 12, 6); Border(card, 1, on ? C("#e8c46a") : C("#2e3444"), 8); card.style.backgroundColor = on ? C("#3a2a12") : C("#141720"); card.style.alignItems = Align.Center;
                var ic = Img(card, "class_" + id, 38, 38); ic.style.marginBottom = 4;
                var b = T(card, c[1], 13, C("#e8c46a"), true, true, false, 0.5f, true); b.style.marginBottom = 5;
                var d = T(card, c[2], 10, on ? C("#cccccc") : C("#999999")); d.style.unityTextAlign = TextAnchor.UpperCenter;
                card.RegisterCallback<ClickEvent>(e => { newCls = id; newName = tf.value; Show("new"); });
                if (!on) { card.RegisterCallback<MouseEnterEvent>(e => Border(card, 1, C("#5a6078"), 8)); card.RegisterCallback<MouseLeaveEvent>(e => Border(card, 1, C("#2e3444"), 8)); }
            }
            ApplyGapLater(grid);
            begin = B(p, "Begin Adventure", Begin, "btn", 13); begin.Padding(10, 14); begin.style.height = 35; begin.Disabled = !Valid();
            if (AnySlot()) { var bk = B(p, "Back to Characters", () => Show("select"), "btn", 11); bk.Padding(4, 4); bk.style.marginTop = 10; bk.style.height = 20; bk.label.style.color = C("#888888"); bk.style.unityBackgroundImageTintColor = new Color(1, 1, 1, 0.5f); }
            var back = B(p, "Back to Menu", () => Show("title"), "btn", 11); back.Padding(4, 4); back.style.marginTop = 10; back.style.height = 20; back.label.style.color = C("#888888"); back.style.unityBackgroundImageTintColor = new Color(1, 1, 1, 0.5f);
        }
        bool Valid() { return newCls != null && newName.Trim().Length > 0; }
        void Begin()
        {
            if (!Valid()) return; int slot = -1; for (int i = 0; i < MAX_CHARS; i++) if (!File.Exists(AldaraSave.PathOf(AldaraSave.UnityDir, i))) { slot = i; break; }
            if (slot < 0) return; AldaraHud.Typing = false; Hide(); AldaraSave.NewCharacter(slot, newName.Trim(), newCls); newName = ""; newCls = null;
        }

        void Update()
        {
            if (AldaraSave.Ready) { if (root != null && root.style.display != DisplayStyle.None) Hide(); return; }
            if (root != null && root.style.display == DisplayStyle.None) Show("title");
            if (screen == "title")
            {
                var kb = Keyboard.current; bool menu = AldaraWindows.I && AldaraWindows.I.AnyOpen();
                if (kb != null && !menu)
                {
                    if (kb.downArrowKey.wasPressedThisFrame) Focus((sel + 1) % 3);
                    if (kb.upArrowKey.wasPressedThisFrame) Focus((sel + 2) % 3);
                    if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) ((System.Action)tmBtns[sel].userData)();
                }
                TickHero();
            }
        }

        // ---------- the hero on the plinth ----------
        GameObject hero, model; Camera cam; RenderTexture rt; float heroA;
        RenderTexture HeroRT()
        {
            if (!hero)
            {
                hero = new GameObject("TitleHeroRig"); hero.transform.position = new Vector3(-6000, 0, -6000);
                var pf = Resources.Load<GameObject>("Heroes/Hero_knight"); if (!pf) return null;
                model = Instantiate(pf, hero.transform, false); var an = model.GetComponent<AldaraCharacterAnimator>(); if (an) an.ApplyRest();
                var cg = new GameObject("TitleCam"); cg.transform.SetParent(hero.transform, false); cam = cg.AddComponent<Camera>(); cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
                rt = new RenderTexture(760, 1120, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; cam.targetTexture = rt;
                var rs = model.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float h = b.size.y, feet = b.min.y - hero.transform.position.y; float size = h / 2 / 0.62f;   // the browser fits the hero to about the plinth at 90% height
                cam.orthographicSize = size; cam.transform.localPosition = new Vector3(0, feet + size * (1 - 2 * 0.1f) + 12 * Mathf.Tan(4 * Mathf.Deg2Rad), -12); cam.transform.localRotation = Quaternion.Euler(4, 0, 0);
            }
            hero.SetActive(true); return rt;
        }
        void TickHero() { if (!model) return; heroA += 0.0035f * 60 * Time.unscaledDeltaTime; float f = Mathf.PI / 2 + Mathf.Sin(heroA) * 0.9f; model.transform.localRotation = Quaternion.Euler(0, 90 + f * Mathf.Rad2Deg, 0); }

        /// light shafts sweeping slowly, embers and dust (titleFrame)
        class Fx : VisualElement
        {
            class Ember { public float x, y, s, v, w, a; public Color c; public bool dust; }
            readonly List<Ember> embers = new List<Ember>(); float t0;
            public Fx()
            {
                var R = new System.Random(9); System.Func<float> r = () => (float)R.NextDouble();
                for (int k = 0; k < 90; k++) embers.Add(new Ember { x = r(), y = r(), s = 0.6f + r() * 1.6f, v = 0.02f + r() * 0.05f, w = r() * 6.28f, a = 0.3f + r() * 0.7f, c = k % 3 != 0 ? C("#ffb04a") : C("#ff6a2a") });
                for (int k = 0; k < 40; k++) embers.Add(new Ember { x = r(), y = r(), s = 0.4f + r() * 0.9f, v = 0.004f + r() * 0.01f, w = r() * 6.28f, a = 0.2f + r() * 0.5f, c = C("#fff0c0"), dust = true });
                t0 = Time.unscaledTime; generateVisualContent += Draw; schedule.Execute(MarkDirtyRepaint).Every(16);
            }
            void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D; float W = contentRect.width, H = contentRect.height, t = Time.unscaledTime - t0; if (W <= 0) return;
                for (int k = 0; k < 4; k++)
                {
                    float x = W * (0.2f + k * 0.2f) + Mathf.Sin(t * 0.13f + k) * W * 0.08f, peak = 0.035f + 0.02f * Mathf.Sin(t * 0.4f + k);
                    const int N = 12;
                    for (int i = 0; i < N; i++)
                    {
                        float a0 = i / (float)N, a1 = (i + 1) / (float)N, am = (a0 + a1) / 2; float al = am < 0.35f ? peak * am / 0.35f : peak * (1 - (am - 0.35f) / 0.65f);
                        float xl0 = Mathf.Lerp(x - W * 0.03f, x + W * 0.08f, a0), xr0 = Mathf.Lerp(x + W * 0.05f, x + W * 0.28f, a0), xl1 = Mathf.Lerp(x - W * 0.03f, x + W * 0.08f, a1), xr1 = Mathf.Lerp(x + W * 0.05f, x + W * 0.28f, a1);
                        p.fillColor = new Color(1, 190 / 255f, 110 / 255f, al); p.BeginPath(); p.MoveTo(new Vector2(xl0, a0 * H)); p.LineTo(new Vector2(xr0, a0 * H)); p.LineTo(new Vector2(xr1, a1 * H)); p.LineTo(new Vector2(xl1, a1 * H)); p.ClosePath(); p.Fill();
                    }
                }
                float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime) * 60;
                foreach (var e in embers)
                {
                    e.y -= e.v * 0.016f * (e.dust ? 0.4f : 1) * dt; e.w += 0.016f * (e.dust ? 0.6f : 1.2f) * dt; if (e.y < -0.05f) { e.y = 1.05f; e.x = Random.value; }
                    float x = (e.x + Mathf.Sin(e.w) * 0.012f) * W, y = e.y * H, a = e.a * (0.6f + 0.4f * Mathf.Sin(t * 3 + e.w)); var c = new Vector2(x, y);
                    if (e.dust) { p.fillColor = new Color(1, 240 / 255f, 200 / 255f, a * 0.5f); p.BeginPath(); p.Arc(c, e.s, 0, 360); p.Fill(); continue; }
                    for (int k = 3; k >= 1; k--) { p.fillColor = new Color(e.c.r, e.c.g, e.c.b, a * 0.18f * (4 - k) / 3); p.BeginPath(); p.Arc(c, e.s * 5 * k / 3, 0, 360); p.Fill(); }
                    p.fillColor = C("#fff8e0"); p.BeginPath(); p.Arc(c, e.s * 0.6f, 0, 360); p.Fill();
                }
            }
        }
    }
}
