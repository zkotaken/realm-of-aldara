using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The browser's windows (".panel"): the dark panel with its gold frame, the title with its two sigils and gold rule,
    // the red octagon close button, opened from the micro menu or their keys and closed with Esc. Each window builds its
    // contents from the game state whenever it opens or the state changes, like the browser's render functions.
    public class AldaraWindows : MonoBehaviour
    {
        public static AldaraWindows I;
        UIDocument doc; VisualElement root;
        public abstract class Win
        {
            public string id, title; public VisualElement panel, body; public bool open; public bool dirty = true;
            public virtual float X => -1; public virtual float Y => 60; public abstract float W { get; } public virtual float H => -1; public virtual float MaxH => 790;
            public abstract void Render(VisualElement body);
            public virtual void Tick() { }
            public virtual void OnOpen() { }
            public virtual void OnClose() { }
        }
        readonly Dictionary<string, Win> wins = new Dictionary<string, Win>();

        void Awake() { I = this; }
        void Start()
        {
            var go = new GameObject("WindowsUI"); go.transform.SetParent(transform, false); doc = go.AddComponent<UIDocument>(); doc.panelSettings = Resources.Load<PanelSettings>("UI/AldaraPanel"); doc.sortingOrder = 5;
            root = doc.rootVisualElement; root.pickingMode = PickingMode.Ignore; root.style.flexGrow = 1;
            Register(new AldaraInvWin()); Register(new AldaraAttWin()); Register(new AldaraSkillWin());
        }
        public void Register(Win w) { wins[w.id] = w; }
        public bool Has(string id) { return wins.ContainsKey(id); }
        public bool IsOpen(string id) { Win w; return wins.TryGetValue(id, out w) && w.open; }
        public static void Refresh(string id = null) { if (!I) return; foreach (var w in I.wins.Values) if (w.open && (id == null || w.id == id)) w.dirty = true; }

        public void Toggle(string id, bool? force = null)
        {
            Win w; if (!wins.TryGetValue(id, out w)) return;
            bool on = force ?? !w.open;
            // the browser: opening inventory closes attributes and the other way round; any of them closes skills and dungeons
            if (on) foreach (var o in wins.Values) if (o != w && o.open && (Exclusive(w.id, o.id))) Close(o);
            if (on) Open(w); else Close(w);
        }
        static bool Exclusive(string a, string b)
        {
            if (a == "inv" && b == "att" || a == "att" && b == "inv") return true;
            if (b == "skills" || b == "dun" || a == "skills" || a == "dun") return true;
            return false;
        }
        void Open(Win w)
        {
            if (w.panel == null) Build(w);
            w.open = true; w.dirty = true; w.panel.style.display = DisplayStyle.Flex; w.panel.BringToFront(); w.OnOpen();
        }
        void Close(Win w) { if (!w.open) return; w.open = false; if (w.panel != null) w.panel.style.display = DisplayStyle.None; w.OnClose(); }
        public void CloseAll() { foreach (var w in wins.Values) Close(w); }
        public bool AnyOpen() { foreach (var w in wins.Values) if (w.open) return true; return false; }

        void Build(Win w)
        {
            var p = E(root, "win_" + w.id); w.panel = p;
            p.style.position = Position.Absolute; p.style.width = w.W; p.style.top = w.Y;
            if (w.X < 0) { p.style.left = Length.Percent(50); p.style.translate = new Translate(Length.Percent(-50), 0); } else p.style.left = w.X;
            if (w.H > 0) p.style.height = w.H; else p.style.maxHeight = w.MaxH;
            Skin(p, "panel"); Pad(p, 28, 28, 28, 28); p.style.flexDirection = FlexDirection.Column;
            // title: sigil, name, sigil over the gold rule
            var h3 = Row(p, 12, Justify.Center); h3.style.height = 30; h3.style.marginTop = -4; h3.style.marginBottom = 12; h3.style.paddingBottom = 8; h3.style.flexShrink = 0;
            var line = E(h3); line.pickingMode = PickingMode.Ignore; line.style.position = Position.Absolute; line.style.left = 0; line.style.right = 0; line.style.top = 0; line.style.bottom = 0;
            line.style.backgroundImage = Background.FromTexture2D(Tex("h3line")); line.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            var s1 = E(h3); s1.style.width = s1.style.height = 20; s1.style.backgroundImage = Background.FromTexture2D(Tex("sig")); s1.style.opacity = 0.9f;
            var tl = T(h3, w.title, 18, C("#efcf78"), true, true, false, 3, true, false); Shadow(tl, new Color(0, 0, 0, 0.9f), 2, 2);
            var s2 = E(h3); s2.style.width = s2.style.height = 20; s2.style.backgroundImage = Background.FromTexture2D(Tex("sig")); s2.style.opacity = 0.9f;
            ApplyGapLater(h3);
            var close = E(p, "close"); close.style.position = Position.Absolute; close.style.right = 16; close.style.top = 16; close.style.width = close.style.height = 26;
            close.style.backgroundImage = Background.FromTexture2D(Tex("close"));
            close.RegisterCallback<ClickEvent>(e => Close(w));
            close.RegisterCallback<MouseEnterEvent>(e => close.style.unityBackgroundImageTintColor = new Color(1.3f, 1.3f, 1.3f, 1));
            close.RegisterCallback<MouseLeaveEvent>(e => close.style.unityBackgroundImageTintColor = Color.white);
            w.body = E(p, "body"); w.body.style.flexGrow = 1; w.body.style.flexShrink = 1; w.body.style.minHeight = 0;
            p.style.display = DisplayStyle.None;
        }
        static void ApplyGapLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && AldaraSave.Ready && !AldaraHud.Typing)
            {
                if (kb.iKey.wasPressedThisFrame) Toggle("inv");
                if (kb.cKey.wasPressedThisFrame) Toggle("att");
                if (kb.kKey.wasPressedThisFrame) Toggle("skills");
                if (kb.escapeKey.wasPressedThisFrame) CloseAll();
            }
            if (!AldaraSave.Ready) { CloseAll(); return; }
            foreach (var w in wins.Values)
            {
                if (!w.open) continue;
                if (w.dirty) { w.dirty = false; w.body.Clear(); try { w.Render(w.body); } catch (Exception ex) { Debug.LogException(ex); } }
                w.Tick();
            }
        }
    }
}
