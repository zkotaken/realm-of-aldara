using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aldara
{
    // The browser's world chat box (chatRender, chatPost, chatSys, chatSend, chatToggle) in its local form: your own
    // messages and the game's announcements (boss kills, mythical drops, dungeon clears and the like), with the chat
    // filter's phrase checks and its pause after repeated blocked messages. Enter opens the input, Escape leaves it.
    public class AldaraChat : MonoBehaviour
    {
        public static AldaraChat I;
        ScrollView log; TextField input; VisualElement sendBtn, toggle; Label toggleL; bool built, hidden; float last; float muteUntil; readonly List<float> blocks = new List<float>();
        void Awake() { I = this; }
        static Color Hx(string h) { return AldaraRules.Hex(h); }
        static readonly List<KeyValuePair<string, string>> pending = new List<KeyValuePair<string, string>>();
        /// chatSys: an announcement in the chat ('boss', 'myth', 'dun', 'note')
        public static void Sys(string text, string style = "") { if (I && I.built) I.Render(true, null, text, style); else pending.Add(new KeyValuePair<string, string>(text, style)); }

        void Build()
        {
            var ui = AldaraHudUI.I; if (ui == null || ui.Root == null) return; built = true;
            log = new ScrollView(ScrollViewMode.Vertical) { name = "chatLog" }; log.horizontalScrollerVisibility = ScrollerVisibility.Hidden; log.verticalScrollerVisibility = ScrollerVisibility.Auto;
            log.style.paddingLeft = log.style.paddingRight = 10; log.style.paddingTop = log.style.paddingBottom = 6; ui.PlaceAt(log, new Rect(11, 705, 320, 182));
            input = new TextField { name = "chatInput", maxLength = 140 }; ui.PlaceAt(input, new Rect(19, 894, 246, 29));
            input.style.marginLeft = input.style.marginRight = input.style.marginTop = input.style.marginBottom = 0;
            var ti = input.Q(TextField.textInputUssName); if (ti != null) { ti.style.backgroundColor = new Color(0, 0, 0, 0); ti.style.borderLeftWidth = ti.style.borderRightWidth = ti.style.borderTopWidth = ti.style.borderBottomWidth = 0; ti.style.color = Hx("#f3e6c4"); ti.style.unityFontDefinition = FontDefinition.FromFont(AldaraUI.Font(false, false)); ti.style.fontSize = 12.5f; ti.style.paddingLeft = 8; }
            input.RegisterCallback<FocusInEvent>(e => AldaraHud.Typing = true); input.RegisterCallback<FocusOutEvent>(e => AldaraHud.Typing = false);
            input.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Send(); e.StopPropagation(); }
                else if (e.keyCode == KeyCode.Escape) { input.Blur(); e.StopPropagation(); }
            }, TrickleDown.TrickleDown);
            sendBtn = new VisualElement { name = "chatSend" }; ui.PlaceAt(sendBtn, new Rect(269, 894, 54, 29)); sendBtn.RegisterCallback<ClickEvent>(e => Send());
            sendBtn.RegisterCallback<MouseEnterEvent>(e => sendBtn.style.backgroundColor = new Color(1, 1, 1, 0.08f)); sendBtn.RegisterCallback<MouseLeaveEvent>(e => sendBtn.style.backgroundColor = Color.clear);
            toggle = new VisualElement { name = "chatToggle" }; ui.PlaceAt(toggle, new Rect(304, 678, 20, 20)); toggle.tooltip = "Hide chat"; toggle.RegisterCallback<ClickEvent>(e => Toggle());
            hidden = PlayerPrefs.GetString("aldara_chat", "1") == "0"; Apply();
            Render(true, null, "Announcements for boss kills, mythical drops and dungeon clears appear here. Press Enter to chat. Keep it friendly: slurs, sexual talk, harassment and politics are blocked.", "note");
            foreach (var p in pending) Render(true, null, p.Key, p.Value); pending.Clear();
        }
        void Toggle() { hidden = !hidden; PlayerPrefs.SetString("aldara_chat", hidden ? "0" : "1"); Apply(); }
        void Apply() { log.style.display = input.style.display = sendBtn.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex; toggle.tooltip = hidden ? "Show chat" : "Hide chat"; if (AldaraHudUI.I) AldaraHudUI.I.ChatHidden(hidden); }
        void Focus() { if (hidden) Toggle(); input.Focus(); }

        void Render(bool sys, string name, string text, string style)
        {
            var d = new Label { enableRichText = true, pickingMode = PickingMode.Ignore }; d.style.whiteSpace = WhiteSpace.Normal; d.style.marginBottom = 3; d.style.unityFontDefinition = FontDefinition.FromFont(AldaraUI.Font(false, false, sys));
            d.style.fontSize = 12; d.style.textShadow = new TextShadow { color = Color.black, offset = new Vector2(0, 1), blurRadius = 2 };
            var t = System.DateTime.Now; string time = "<size=9><color=#6a7080>" + t.Hour.ToString("00") + ":" + t.Minute.ToString("00") + "</color></size> ";
            string esc = Esc(text);
            if (sys)
            {
                string col = style == "boss" ? "#ff9a6a" : style == "myth" ? "#ff7ae0" : style == "dun" ? "#9fe09f" : style == "note" ? "#8a9ab0" : "#ffd88a";
                if (style != "note") { int sp = esc.IndexOf(' '); var m = System.Text.RegularExpressions.Regex.Match(esc, @"^([^ ]+( the [A-Za-z]+)?)"); if (m.Success) esc = "<b><color=#fff2c4>" + m.Value + "</color></b>" + esc.Substring(m.Length); }
                d.text = time + "<i><color=" + col + ">" + (style == "note" ? "<size=11>" + esc + "</size>" : esc) + "</color></i>";
            }
            else d.text = time + "<b><color=#fff2c4><size=11>" + Esc(name) + ":</size></color></b> <color=#e8e6da>" + esc + "</color>";
            log.Add(d); while (log.contentContainer.childCount > 120) log.contentContainer.RemoveAt(0);
            log.schedule.Execute(() => log.scrollOffset = new Vector2(0, float.MaxValue));
        }
        static string Esc(string s) { return (s ?? "").Replace("<", "&lt;").Replace(">", "&gt;"); }

        // ---- the chat filter's phrase checks (cfCheck) ----
        static List<string[]> phr; static Dictionary<char, char> look;
        static void LoadFilter()
        {
            if (phr != null) return; phr = new List<string[]>(); look = new Dictionary<char, char>();
            var ta = Resources.Load<TextAsset>("chatfilter"); if (!ta) return; var j = JObject.Parse(ta.text);
            foreach (var kv in (JObject)j["look"]) look[kv.Key[0]] = ((string)kv.Value)[0];
            foreach (JArray p in (JArray)j["phr"]) phr.Add(new[] { (string)p[0], Rot13((string)p[1]) });
        }
        static string Rot13(string s) { var b = new StringBuilder(s.Length); foreach (var c in s) b.Append(c >= 'a' && c <= 'z' ? (char)('a' + (c - 'a' + 13) % 26) : c); return b.ToString(); }
        static string Norm(string t)
        {
            t = (t ?? "").Normalize(NormalizationForm.FormKD); var b = new StringBuilder();
            foreach (var ch in t) { if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue; var c = char.ToLowerInvariant(ch); char m; b.Append(look.TryGetValue(c, out m) ? m : c); }
            return b.ToString();
        }
        static string Check(string text)
        {
            LoadFilter(); var toks = new List<string>();
            foreach (var w in text.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries)) foreach (var t in System.Text.RegularExpressions.Regex.Split(Norm(w), "[^a-z0-9]+")) if (t.Length > 0) toks.Add(t);
            string flat = " " + string.Join(" ", toks) + " ", squash = string.Join("", toks);
            foreach (var p in phr) { string ps = Norm(p[1]), pz = ps.Replace(" ", ""); if (flat.Contains(" " + ps + " ") || (pz.Length >= 8 && squash.Contains(pz))) return p[0]; }
            return null;
        }
        static string Why(string w) { return w == "pol" ? "political topics are not allowed in chat" : w == "harm" ? "harassment and personal questions are not allowed" : w == "sex" ? "sexual content is not allowed" : "slurs and hateful words are not allowed"; }
        void Send()
        {
            string t = (input.value ?? "").Trim(); if (t.Length == 0) return; float now = Time.unscaledTime; if (now - last < 1.2f) return; last = now;
            if (now < muteUntil) { Render(true, null, "Chat is paused for you for " + Mathf.CeilToInt(muteUntil - now) + " more seconds.", "note"); return; }
            if (t.Length > 140) t = t.Substring(0, 140); var why = Check(t);
            if (why != null)
            {
                input.value = ""; blocks.RemoveAll(x => now - x > 60); blocks.Add(now); string m = "Message not sent: " + Why(why) + ".";
                if (blocks.Count >= 3) { muteUntil = now + 60; blocks.Clear(); m += " Chat is paused for you for 60 seconds."; }
                Render(true, null, m, "note"); return;
            }
            Render(false, AldaraHero.I ? AldaraHero.I.heroName : "Adventurer", t, null); input.value = "";
        }
        void Update()
        {
            if (!built) { Build(); if (!built) return; }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && AldaraSave.Ready && !AldaraHud.Typing && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Focus();
        }
    }
}
