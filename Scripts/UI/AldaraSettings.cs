using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // The browser's settings (SET, SET_BIND, SET_PRESETS): the same keys and defaults, remembered on this device
    // ("aldara_settings"). The pages of the Game Menu are built from the browser's own page list (settings.json), and the
    // settings that mean something in Unity are applied here: display mode, quality, shadows, the frame rate cap, the
    // background throttle, window and HUD size and opacity, which HUD pieces show, the FPS counter and gear comparison.
    public static class AldaraSettings
    {
        public class Item { public string k, t, hint, id, c, cls; public JToken v; public float min, max, step; public string[][] opts; public BtnSpec[] b; }
        public class BtnSpec { public string t, cls, on; }
        public class Page { public string id, name; public Item[] items; }
        public class Spec { public Page[] pages; public JObject SET; public Dictionary<string, JObject> PRESETS; public string[][] BIND; public string kbNote; }
        static Spec spec; public static Spec S { get { if (spec == null) spec = JsonConvert.DeserializeObject<Spec>(Resources.Load<TextAsset>("settings").text); return spec; } }
        static JObject set;
        public static JObject SET
        {
            get
            {
                if (set == null)
                {
                    set = (JObject)S.SET.DeepClone(); set["disp"] = "windowed"; set["sndOn"] = true; set["sndMaster"] = 0.8; set["sndMusic"] = 0.6; foreach (var k in new[] { "sndSelf", "sndEnemy", "sndOthers", "sndUi" }) set[k] = 1; set["sndVoice"] = "HumanM";
                    try { var saved = JObject.Parse(PlayerPrefs.GetString("aldara_settings", "{}")); foreach (var kv in saved) set[kv.Key] = kv.Value; } catch { }
                }
                return set;
            }
        }
        /// the SET key behind a control id (SET_BIND, else the id itself, as the browser's sound and display rows do)
        public static string KeyOf(string id)
        {
            if (id == null) return null; foreach (var b in S.BIND) if (b[0] == id) return b[1];
            switch (id) { case "optPreset": return "preset"; case "optDisp": return "disp"; case "optSndBg": return "sndBg"; default: return id; }
        }
        public static JToken Get(string key) { return SET[key]; }
        public static bool On(string key, bool d = true) { var t = SET[key]; if (t == null || t.Type == JTokenType.Null) return d; if (t.Type == JTokenType.Boolean) return (bool)t; if (t.Type == JTokenType.String) return (string)t != "" && (string)t != "0" && (string)t != "false"; return (float)t != 0; }
        public static float F(string key, float d) { var t = SET[key]; if (t == null || t.Type == JTokenType.Null) return d; float v; return float.TryParse(t.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : d; }
        public static void Set(string key, JToken v)
        {
            SET[key] = v;
            if (key == "preset" && S.PRESETS.TryGetValue((string)v, out JObject p)) foreach (var kv in p) SET[kv.Key] = kv.Value;
            else if (S.PRESETS.ContainsKey("high") && S.PRESETS["high"][key] != null) SET["preset"] = "custom";
            Save(); Apply();
        }
        public static void Defaults() { var keep = SET["disp"]; set = null; PlayerPrefs.DeleteKey("aldara_settings"); SET["disp"] = keep; Save(); Apply(); }
        static void Save() { PlayerPrefs.SetString("aldara_settings", SET.ToString(Formatting.None)); PlayerPrefs.Save(); }

        public static void Apply()
        {
            // display mode
            string disp = (string)SET["disp"] ?? "windowed";
            var mode = disp == "fullscreen" ? FullScreenMode.ExclusiveFullScreen : disp == "borderless" ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            // resolution: native is the screen's own size (a window keeps whatever size it has); a size sets it
            string res = (string)SET["res"] ?? "native";
            if (!Application.isEditor && (Screen.fullScreenMode != mode || res != lastRes))
            {
                int rw = 0, rh = 0; var parts = res.Split('x');
                if (parts.Length == 2) { int.TryParse(parts[0], out rw); int.TryParse(parts[1], out rh); }
                if (rw <= 0 || rh <= 0) { if (mode != FullScreenMode.Windowed) { rw = Display.main.systemWidth; rh = Display.main.systemHeight; } else { rw = Screen.width; rh = Screen.height; } }
                Screen.SetResolution(rw, rh, mode); lastRes = res;
            }
            // quality preset onto Unity's quality levels by name
            string pr = (string)SET["preset"] ?? "high"; var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++) if (names[i].ToLower().Replace(" ", "") == pr) { if (QualitySettings.GetQualityLevel() != i) QualitySettings.SetQualityLevel(i, true); break; }
            QualitySettings.shadows = On("shadows") ? ShadowQuality.All : ShadowQuality.Disable;
            int cap = (int)F("fpsCap", 0); Application.targetFrameRate = cap > 0 ? cap : -1; QualitySettings.vSyncCount = 0;
            if (AldaraHudUI.I) AldaraHudUI.I.ApplySettings();
            if (AldaraWindows.I) AldaraWindows.I.ApplySettings();
        }
        static string lastRes;
        static bool focused = true;
        /// called every frame: the background throttle (30 fps while another window has focus)
        public static void Tick()
        {
            bool f = Application.isFocused; if (f == focused) return; focused = f;
            int cap = (int)F("fpsCap", 0); Application.targetFrameRate = !f && On("bgThrottle") ? 30 : cap > 0 ? cap : -1;
        }
    }
}
