using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // The browser's lore scenes (loreUpdate): the wrecks, camps, battlefields, shrines and the rest placed across the
    // world. Walking up to one tells its story once per session; the searchable ones (wagons, camps, battlefields,
    // caches and shipwrecks) give up gold and sometimes an item, once per character (saved as "lore").
    public class AldaraLoreScenes : MonoBehaviour
    {
        public class Scene { public int id, z; public string k, t; public float x, y; public bool loot; }
        public static List<Scene> ALL;
        static readonly HashSet<int> seen = new HashSet<int>();
        float t;
        void Awake() { AldaraVfx.DrawAir += DrawWp; }
        void OnDestroy() { AldaraVfx.DrawAir -= DrawWp; }
        // wmDrawWorld: the map waypoint as a beam of light where it stands; walking up to it clears it
        void DrawWp(AldaraVfx V)
        {
            var w = AldaraMapWin.wp; var P = AldaraPlayer.I; if (w == null || !P || AldaraWorld.Dun) return; float dx = w.x - P.x, dy = w.y - P.y; if (Mathf.Abs(dx) > 1920 || Mathf.Abs(dy) > 1080) return;
            float tt = Time.time, h = AldaraVfx.LZ(w.x, w.y), pul = 0.7f + 0.3f * Mathf.Sin(tt * 2.5f); var c = AldaraRules.Hex("#8ff0a0");
            V.AirA.VQuad(new Vector3(w.x - 10, w.y, h), new Vector3(w.x + 10, w.y, h), new Vector3(w.x + 10, w.y, h + 260), new Vector3(w.x - 10, w.y, h + 260), new Color(c.r, c.g, c.b, 0.35f * pul), new Color(c.r, c.g, c.b, 0));
            AldaraVfx.Ring(V.AirN, w.x, w.y, 22, 8, 2, new Color(c.r, c.g, c.b, 0.8f * pul), h + 1);
            if (Mathf.Sqrt(dx * dx + dy * dy) < 60) { AldaraMapWin.wp = null; AldaraHud.Banner("You reached your waypoint"); }
        }
        public static void Load()
        {
            if (ALL != null) return; ALL = new List<Scene>(); var ta = Resources.Load<TextAsset>("lore_scenes"); if (!ta) return;
            foreach (JObject o in JArray.Parse(ta.text)) ALL.Add(new Scene { id = (int)o["id"], k = (string)o["k"], x = (float)o["x"], y = (float)o["y"], z = (int)o["z"], t = (string)o["t"], loot = (int)o["loot"] != 0 });
        }
        public static JArray Mine { get { var r = AldaraSave.Raw; return r == null ? null : r["lore"] as JArray; } }
        public static bool Searched(int id) { var m = Mine; return m != null && m.Any(v => v.Type == JTokenType.Integer && (int)v == id); }
        public static string MapName(string k) { return k == "wreck" ? "Crashed wagon" : k == "camp" ? "Abandoned camp" : k == "battle" ? "Old battlefield" : k == "chest" ? "Hidden cache" : k == "ship" ? "Shipwreck" : "Lore scene"; }
        void Update()
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I;
            if (!AldaraSave.Ready || !P || !H || !H.alive || AldaraWorld.Dun) return;
            Load(); t += Time.deltaTime; if (t < 0.4f) return; t = 0;
            foreach (var L in ALL)
            {
                float d = Mathf.Sqrt((L.x - P.x) * (L.x - P.x) + (L.y - P.y) * (L.y - P.y)); if (d > 150) continue;
                if (seen.Add(L.id)) AldaraHud.Banner(L.t);
                if (!L.loot || d >= 100 || Searched(L.id)) continue;
                var r = AldaraSave.Raw; var m = Mine; if (m == null) { m = new JArray(); r["lore"] = m; } m.Add(L.id);
                int lvl = Mathf.Max(1, L.z * 8 + 4), gold = Mathf.RoundToInt((40 + lvl * 14) * (0.8f + Random.value * 0.5f)); H.gold += gold;
                AldaraFx.Text(P.x, P.y - 60, "+" + gold + " Gold", AldaraRules.Hex("#e0b64b")); AldaraVfx.Burst(L.x, L.y - 10, AldaraRules.Hex("#e0b64b"), 14, 120);
                string what = L.k == "battle" ? "fallen" : L.k == "chest" ? "remains" : L.k == "camp" ? "camp" : "wreck";
                string msg = "You search the " + what + " and find " + gold + " gold";
                if (Random.value < 0.4f) { var it = AldaraItems.RollItem(lvl, null, 3); if (H.AddLoot(it)) { msg += " and " + it.name; AldaraFx.Text(P.x, P.y - 78, it.name + "!", it.Col); } }
                AldaraHud.Banner(msg + "."); AldaraSave.Save();
            }
        }
    }
}
