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
            public virtual bool Full => false; public virtual bool NoTitle => false; public virtual float X => -1; public virtual float Y => 60; public abstract float W { get; } public virtual float H => -1; public virtual float MaxH => 790;
            public abstract void Render(VisualElement body);
            public virtual void Tick() { }
            public virtual void OnOpen() { }
            public virtual void OnClose() { }
        }
        readonly Dictionary<string, Win> wins = new Dictionary<string, Win>();

        void Awake() { I = this; }
        void Start()
        {
            // its own GameObject: a UIDocument under another one nests inside that one
            var go = new GameObject("WindowsUI"); go.SetActive(false); doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("UI/AldaraPanel"); doc.sortingOrder = 5; go.SetActive(true);
            root = doc.rootVisualElement; root.pickingMode = PickingMode.Ignore; root.style.position = Position.Absolute; root.style.left = root.style.top = root.style.right = root.style.bottom = 0;
            Register(new AldaraInvWin()); Register(new AldaraAttWin()); Register(new AldaraSkillWin()); Register(new AldaraSubWin()); Register(new AldaraVaultWin()); Register(new AldaraMenuWin()); Register(new AldaraMapWin()); Register(new AldaraQuestLog()); Register(new AldaraQuestDlg()); Register(new AldaraLoreWin()); Register(new AldaraWsWin()); Register(new AldaraDunWin()); Register(new AldaraDunResWin()); Register(new AldaraGuildWin());
            AldaraSettings.Apply();
            if (!GetComponent<AldaraTitle>()) gameObject.AddComponent<AldaraTitle>();
            if (!GetComponent<AldaraFolk>()) gameObject.AddComponent<AldaraFolk>();
            if (!GetComponent<AldaraQuestWorld>()) gameObject.AddComponent<AldaraQuestWorld>();
            if (!GetComponent<AldaraPet>()) gameObject.AddComponent<AldaraPet>();
            if (!GetComponent<AldaraWaystones>()) gameObject.AddComponent<AldaraWaystones>();
            if (!GetComponent<AldaraDungeon>()) gameObject.AddComponent<AldaraDungeon>();
            if (!GetComponent<AldaraDungeonView>()) gameObject.AddComponent<AldaraDungeonView>();
            if (!GetComponent<AldaraPost>()) gameObject.AddComponent<AldaraPost>();
            if (!GetComponent<AldaraRaid>()) gameObject.AddComponent<AldaraRaid>();
            if (!GetComponent<AldaraOccluders>()) gameObject.AddComponent<AldaraOccluders>();
            if (!GetComponent<AldaraRaidView>()) gameObject.AddComponent<AldaraRaidView>();
            if (!GetComponent<AldaraVfx>()) gameObject.AddComponent<AldaraVfx>();
            if (!GetComponent<AldaraRelics>()) gameObject.AddComponent<AldaraRelics>();
            if (!GetComponent<AldaraSubclass>()) gameObject.AddComponent<AldaraSubclass>();
            if (!GetComponent<AldaraHudBar>()) gameObject.AddComponent<AldaraHudBar>();
            if (!GetComponent<AldaraChat>()) gameObject.AddComponent<AldaraChat>();
            if (!GetComponent<AldaraLoreScenes>()) gameObject.AddComponent<AldaraLoreScenes>();
            if (!GetComponent<AldaraWilds>()) gameObject.AddComponent<AldaraWilds>();
            if (!GetComponent<AldaraTownFx>()) gameObject.AddComponent<AldaraTownFx>();
            if (!GetComponent<AldaraFlightFx>()) gameObject.AddComponent<AldaraFlightFx>();
            if (!GetComponent<AldaraSound>()) gameObject.AddComponent<AldaraSound>();
            if (!GetComponent<AldaraMusic>()) gameObject.AddComponent<AldaraMusic>();
            if (!GetComponent<AldaraDunHud>()) gameObject.AddComponent<AldaraDunHud>();
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
            if (a == "quest" && b == "qdlg" || a == "qdlg" && b == "quest") return true;
            if (b == "skills" || b == "dun" || a == "skills" || a == "dun") return true;
            return false;
        }
        void Open(Win w)
        {
            if (w.panel == null) Build(w);
            w.open = true; w.dirty = true; w.panel.style.display = DisplayStyle.Flex; w.panel.BringToFront(); w.OnOpen();
        }
        void Close(Win w) { if (!w.open) return; w.open = false; if (w.panel != null) w.panel.style.display = DisplayStyle.None; w.OnClose(); }
        /// window size and opacity from the Interface settings
        public void ApplySettings()
        {
            if (root == null) return; float s = AldaraSettings.F("ui", 1), a = AldaraSettings.F("winA", 1);
            foreach (var w in wins.Values) if (w.panel != null) { w.panel.style.scale = new Scale(new Vector2(s, s)); w.panel.style.transformOrigin = new TransformOrigin(Length.Percent(w.X < 0 ? 50 : 0), 0); w.panel.style.opacity = a; }
            Refresh();
        }
        public void CloseAll() { foreach (var w in wins.Values) Close(w); }
        public bool AnyOpen() { foreach (var w in wins.Values) if (w.open) return true; return false; }

        void Build(Win w)
        {
            var p = E(root, "win_" + w.id); w.panel = p;
            if (w.Full)
            {   // a full-screen view (the world map)
                p.style.position = Position.Absolute; p.style.left = p.style.top = p.style.right = p.style.bottom = 0;
                w.body = E(p, "body"); w.body.style.flexGrow = 1; p.style.display = DisplayStyle.None; return;
            }
            p.style.position = Position.Absolute; p.style.width = w.W; p.style.top = w.Y;
            if (w.X < 0) { p.style.left = Length.Percent(50); p.style.translate = new Translate(Length.Percent(-50), 0); } else p.style.left = w.X;
            if (w.H > 0) p.style.height = w.H; else p.style.maxHeight = w.MaxH;
            Skin(p, "panel"); Pad(p, 28, 28, 28, 28); p.style.flexDirection = FlexDirection.Column;
            // title: sigil, name, sigil over the gold rule
            if (!w.NoTitle) {
            var h3 = Row(p, 12, Justify.Center); h3.style.height = 30; h3.style.marginTop = -4; h3.style.marginBottom = 12; h3.style.paddingBottom = 8; h3.style.flexShrink = 0;
            var line = E(h3); line.pickingMode = PickingMode.Ignore; line.style.position = Position.Absolute; line.style.left = 0; line.style.right = 0; line.style.top = 0; line.style.bottom = 0;
            line.style.backgroundImage = Background.FromTexture2D(Tex("h3line")); line.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            var s1 = E(h3); s1.style.width = s1.style.height = 20; s1.style.backgroundImage = Background.FromTexture2D(Tex("sig")); s1.style.opacity = 0.9f;
            var tl = T(h3, w.title, 18, C("#efcf78"), true, true, false, 3, true, false); Shadow(tl, new Color(0, 0, 0, 0.9f), 2, 2);
            var s2 = E(h3); s2.style.width = s2.style.height = 20; s2.style.backgroundImage = Background.FromTexture2D(Tex("sig")); s2.style.opacity = 0.9f;
            ApplyGapLater(h3);
            }
            var close = E(p, "close"); close.style.position = Position.Absolute; close.style.right = 16; close.style.top = 16; close.style.width = close.style.height = 26;
            close.style.backgroundImage = Background.FromTexture2D(Tex("close"));
            close.RegisterCallback<ClickEvent>(e => Close(w));
            close.RegisterCallback<MouseEnterEvent>(e => close.style.unityBackgroundImageTintColor = new Color(1.3f, 1.3f, 1.3f, 1));
            close.RegisterCallback<MouseLeaveEvent>(e => close.style.unityBackgroundImageTintColor = Color.white);
            p.style.opacity = AldaraSettings.F("winA", 1); float us = AldaraSettings.F("ui", 1); p.style.scale = new Scale(new Vector2(us, us)); p.style.transformOrigin = new TransformOrigin(Length.Percent(w.X < 0 ? 50 : 0), 0);
            w.body = E(p, "body"); w.body.style.flexGrow = 1; w.body.style.flexShrink = 1; w.body.style.minHeight = 0;
            p.style.display = DisplayStyle.None;
        }
        static void ApplyGapLater(VisualElement e) { e.schedule.Execute(() => ApplyGap(e)); }

        void Update()
        {
            AldaraSettings.Tick();
            var kb = Keyboard.current;
            if (kb != null && AldaraSave.Ready && !AldaraHud.Typing && AldaraKeys.listening == null)
            {
                foreach (var w in new[] { "inv", "att", "skills", "vault", "map", "quest", "dun", "guild", "coop" }) if (AldaraKeys.Pressed(w)) { if (Has(w)) Toggle(w); else AldaraMenus.Open(w); }
                // Esc closes whatever is open; with nothing open it opens the Game Menu
                if (kb.escapeKey.wasPressedThisFrame) { if (IsOpen("map")) Toggle("map", false); else if (AnyOpen()) CloseAll(); else Toggle("set", true); }
                if (kb.f11Key.wasPressedThisFrame && !Application.isEditor) AldaraMenuWin.ToggleFullscreen();
            }
            if (!AldaraSave.Ready)
            {   // on the title screens only the Game Menu (its settings) can be open
                foreach (var w in wins.Values) if (w.open && w.id != "set") Close(w);
                if (kb != null && kb.escapeKey.wasPressedThisFrame) Close(wins["set"]);
            }
            foreach (var w in wins.Values)
            {
                if (!w.open) continue;
                if (w.dirty) { w.dirty = false; w.body.Clear(); try { w.Render(w.body); } catch (Exception ex) { Debug.LogException(ex); } }
                w.Tick();
            }
        }
    }
}
