using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Aldara
{
    // The browser's key bindings (KBIND_ACTIONS, kbKeyOf, kbName): every action has a default key, any of them can be
    // rebound in the Controls tab, Backspace unbinds, and the choice is remembered on this device ("aldara/keys").
    public static class AldaraKeys
    {
        public class Action { public string a, n, d; }
        public static readonly Action[] ACTIONS = {
            A("up","Move up","w"), A("down","Move down","s"), A("left","Move left","a"), A("right","Move right","d"), A("attack","Attack toward the cursor"," "),
            A("interact","Talk, gather, use a waystone","f"), A("auto","Auto-combat","e"), A("hp","Health vial","h"), A("mp","Mana vial","m"), A("quick","Quick skill (first slot)","q"),
            A("slot1","Skill slot 1","1"), A("slot2","Skill slot 2","2"), A("slot3","Skill slot 3","3"), A("slot4","Skill slot 4","4"), A("slot5","Skill slot 5","5"),
            A("slot6","Skill slot 6","6"), A("slot7","Skill slot 7","7"), A("slot8","Skill slot 8","8"), A("slot9","Skill slot 9","9"), A("slot10","Skill slot 10","0"),
            A("inv","Inventory","i"), A("att","Attributes","c"), A("skills","Skill tree","k"), A("quest","Quest log","l"), A("dun","Dungeons","j"), A("guild","Guilds","g"),
            A("coop","Play Together","o"), A("map","World map","n"), A("vault","Vault","b"), A("sub1","Subclass ability 1","z"), A("sub2","Subclass ability 2","x"),
            A("sub3","Subclass ability 3","r"), A("sub4","Subclass ability 4","t"), A("zoomin","Zoom in","="), A("zoomout","Zoom out","-"), A("hideui","Hide the interface","f8"), A("mount","Ride or dismiss your mount","v") };
        static Action A(string a, string n, string d) { return new Action { a = a, n = n, d = d }; }
        static Dictionary<string, string> bind;
        static Dictionary<string, string> B { get { if (bind == null) { try { bind = JsonConvert.DeserializeObject<Dictionary<string, string>>(PlayerPrefs.GetString("aldara/keys", "{}")); } catch { } if (bind == null) bind = new Dictionary<string, string>(); } return bind; } }
        public static string KeyOf(string a) { string k; if (B.TryGetValue(a, out k)) return k; foreach (var x in ACTIONS) if (x.a == a) return x.d; return ""; }
        public static string Default(string a) { foreach (var x in ACTIONS) if (x.a == a) return x.d; return ""; }
        public static void Set(string a, string k)
        {
            // a key can only do one thing: whatever had it loses it
            if (k != "") foreach (var x in ACTIONS) if (x.a != a && KeyOf(x.a) == k) B[x.a] = "";
            if (k == Default(a)) B.Remove(a); else B[a] = k; Save();
        }
        public static void ResetAll() { B.Clear(); Save(); }
        static void Save() { PlayerPrefs.SetString("aldara/keys", JsonConvert.SerializeObject(B)); PlayerPrefs.Save(); }
        public static string Name(string k)
        {
            switch (k) { case " ": return "Space"; case "arrowup": return "Up arrow"; case "arrowdown": return "Down arrow"; case "arrowleft": return "Left arrow"; case "arrowright": return "Right arrow"; case "escape": return "Esc"; case "": return "Unbound"; }
            return k.Length == 1 ? k.ToUpper() : char.ToUpper(k[0]) + k.Substring(1);
        }
        // ---- the browser's key names to the Input System ----
        public static KeyControl Control(string k)
        {
            var kb = Keyboard.current; if (kb == null || string.IsNullOrEmpty(k)) return null;
            switch (k)
            {
                case " ": return kb.spaceKey; case "=": return kb.equalsKey; case "-": return kb.minusKey; case "tab": return kb.tabKey; case "enter": return kb.enterKey;
                case "shift": return kb.leftShiftKey; case "control": return kb.leftCtrlKey; case "alt": return kb.leftAltKey; case "`": return kb.backquoteKey;
                case "[": return kb.leftBracketKey; case "]": return kb.rightBracketKey; case ";": return kb.semicolonKey; case "'": return kb.quoteKey; case ",": return kb.commaKey; case ".": return kb.periodKey; case "/": return kb.slashKey; case "\\": return kb.backslashKey;
                case "arrowup": return kb.upArrowKey; case "arrowdown": return kb.downArrowKey; case "arrowleft": return kb.leftArrowKey; case "arrowright": return kb.rightArrowKey;
            }
            if (k.Length == 1 && k[0] >= 'a' && k[0] <= 'z') return kb[(Key)((int)Key.A + (k[0] - 'a'))];
            if (k.Length == 1 && k[0] >= '0' && k[0] <= '9') return k[0] == '0' ? kb.digit0Key : kb[(Key)((int)Key.Digit1 + (k[0] - '1'))];
            if (k.Length >= 2 && k[0] == 'f' && int.TryParse(k.Substring(1), out int f) && f >= 1 && f <= 12) return kb[(Key)((int)Key.F1 + f - 1)];
            return null;
        }
        public static string NameOfControl(KeyControl c)
        {
            var kb = Keyboard.current; if (c == null) return null;
            if (c == kb.spaceKey) return " "; if (c == kb.equalsKey) return "="; if (c == kb.minusKey) return "-"; if (c == kb.tabKey) return "tab"; if (c == kb.enterKey) return "enter";
            if (c == kb.leftShiftKey || c == kb.rightShiftKey) return "shift"; if (c == kb.leftCtrlKey || c == kb.rightCtrlKey) return "control"; if (c == kb.leftAltKey || c == kb.rightAltKey) return "alt";
            if (c == kb.backquoteKey) return "`"; if (c == kb.leftBracketKey) return "["; if (c == kb.rightBracketKey) return "]"; if (c == kb.semicolonKey) return ";"; if (c == kb.quoteKey) return "'";
            if (c == kb.commaKey) return ","; if (c == kb.periodKey) return "."; if (c == kb.slashKey) return "/"; if (c == kb.backslashKey) return "\\";
            var key = c.keyCode;
            if (key >= Key.A && key <= Key.Z) return ((char)('a' + (key - Key.A))).ToString();
            if (key >= Key.Digit1 && key <= Key.Digit9) return ((char)('1' + (key - Key.Digit1))).ToString(); if (key == Key.Digit0) return "0";
            if (key >= Key.F1 && key <= Key.F12) return "f" + (1 + key - Key.F1);
            return null;
        }
        static bool Blocked { get { return AldaraHud.Typing || listening != null; } }
        public static string listening;   // the action waiting for a new key in the Controls tab
        public static bool Pressed(string a) { if (Blocked) return false; var c = Control(KeyOf(a)); return c != null && c.wasPressedThisFrame; }
        public static bool Held(string a) { if (Blocked) return false; var c = Control(KeyOf(a)); return c != null && c.isPressed; }
    }
}
