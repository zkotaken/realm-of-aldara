using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // The browser's music (music pack v0.1): each zone, town and dungeon has its own tracks. Changing zone crossfades over
    // 5 seconds; quiet places leave a stretch of silence between plays. A boss coming into view plays an intro stinger,
    // pulling it crossfades into its battle theme over 2 seconds, and killing it (or falling) fades the battle music out
    // over 3 seconds before the zone returns. The tracks are the browser's own (Resources/Music).
    public class AldaraMusic : MonoBehaviour
    {
        class V { public string t, role; public AudioSource a; public float g, to = 1, rate; public bool kill, ended, started, played; }
        readonly List<V> voices = new List<V>();
        static readonly Dictionary<string, string[]> ZONE = new Dictionary<string, string[]> {
            { "Lorenmar", new[] { "Village_TheBritons", "Tavern_DancingAtTheInn" } }, { "Valcrest", new[] { "Capital_KingsTrailer", "Alchemist_BrewingPotions" } },
            { "Green Fields", new[] { "Roads_TravelersNotebook", "Wilds_ManaTwo" } }, { "Ashen Highlands", new[] { "Mountains_LonelyMountain", "Ruins_ManaTwo" } },
            { "The Shadowfen", new[] { "Swamp_WitchWaltz", "Forest_ForestNight" } }, { "Frostpeak Tundra", new[] { "Snow_ColdJourney", "Snow_Winter" } },
            { "Emberwaste", new[] { "Mountains_LonelyMountain", "Haunted_HiddenTruth" } }, { "The Void Reach", new[] { "Haunted_HiddenTruth", "Graveyard_NightVigil" } },
            { "Elysian Expanse", new[] { "Temple_AncientRite", "Ruins_ManaTwo" } }, { "The Brigand Marches", new[] { "Roads_TravelersNotebook", "Mountains_LonelyMountain" } },
            { "Gloomwood", new[] { "Forest_ForestNight", "Haunted_HiddenTruth" } }, { "The Crownlands", new[] { "Wilds_ManaTwo", "Roads_TravelersNotebook" } },
            { "The Bloodmoor", new[] { "Graveyard_NightVigil", "Crypt_LandOfTheDead" } } };
        static readonly Dictionary<string, string[]> DUN = new Dictionary<string, string[]> {
            { "warrens", new[] { "Dungeon_TheAbyss" } }, { "crypt", new[] { "Crypt_LandOfTheDead" } }, { "frozen", new[] { "Snow_Winter", "Dungeon_TheAbyss" } }, { "forge", new[] { "Dungeon_TheAbyss" } }, { "void", new[] { "Haunted_HiddenTruth" } },
            { "astral", new[] { "Temple_AncientRite" } }, { "blood", new[] { "Graveyard_NightVigil" } }, { "clock", new[] { "Alchemist_BrewingPotions", "Dungeon_TheAbyss" } }, { "nest", new[] { "Dungeon_TheAbyss" } }, { "sewer", new[] { "Crypt_LandOfTheDead" } },
            { "sunken", new[] { "Ruins_ManaTwo" } }, { "eclipse", new[] { "Temple_AncientRite", "Dungeon_TheAbyss" } }, { "fort", new[] { "Combat_BattleReady" } } };
        static readonly Dictionary<string, float[]> GAP = new Dictionary<string, float[]> { { "Lorenmar", new[] { 4f, 10f } }, { "Valcrest", new[] { 4f, 10f } } };
        static bool Rx(string s, string p) { return System.Text.RegularExpressions.Regex.IsMatch(s ?? "", p); }
        static string BossTrack(string n)
        {
            if (Rx(n, "Frost Giant|Glacia")) return "Boss_NordicWist"; if (Rx(n, "Goblin|Grak|Bone Tyrant|Ignar")) return "Boss_BloodEagle"; if (Rx(n, "Maelthar")) return "Boss_NightAttack";
            if (Rx(n, "Lich|Void Emperor|Hollow King|Countess|Infernal|Vesper|Solace|Behemoth|Revenant")) return "Boss_EvilIncoming"; if (Rx(n, "Nerissa|Arachna|Artificer|Astraeus")) return "Boss_HonorBound";
            return "Boss_EpicBossBattle";
        }
        static float Vol() { if (!AldaraSettings.On("sndOn", true)) return 0; return AldaraSettings.F("sndMaster", 0.8f) * AldaraSettings.F("sndMusic", 0.6f); }
        static string File(string t) { return "Music/MUS_" + (Rx(t, "^(Boss|Stinger|Combat)_") ? t : "Zone_" + t); }
        V Voice(string t, float fadeIn, string role, bool loop = false)
        {
            var clip = Resources.Load<AudioClip>(File(t)); var go = new GameObject("mus " + t); go.transform.SetParent(transform, false); var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false; a.spatialBlend = 0; a.loop = loop; a.clip = clip; a.volume = 0; if (clip) a.Play();
            var v = new V { t = t, role = role, a = a, rate = 1 / Mathf.Max(0.05f, fadeIn), ended = clip == null, started = clip != null }; voices.Add(v); return v;
        }
        void Fade(V v, float to, float secs) { if (v == null) return; v.to = to; v.rate = 1 / Mathf.Max(0.05f, secs); if (to <= 0) v.kill = true; }
        void FadeAll(string role, float secs) { foreach (var v in voices) if (role == null || v.role == role) Fade(v, 0, secs); }
        V Cur(string role) { for (int i = voices.Count - 1; i >= 0; i--) { var v = voices[i]; if (v.role == role && !v.kill) return v; } return null; }
        static string PlaceKey()
        {
            if (AldaraDungeon.Active) { string id = AldaraDungeon.Fort ? "fort" : AldaraRaid.On && AldaraRaid.def != null ? AldaraRaid.def.id : AldaraDungeon.def != null ? AldaraDungeon.def.id : ""; return "d:" + id; }
            if (AldaraWilds.InMoor) return "pvp"; var P = AldaraPlayer.I; return "z:" + AldaraWorld.ZoneName(P.x, P.y);
        }
        static string[] Playlist(string key)
        {
            if (key == "pvp") return new[] { "Combat_BattleReady" }; string[] L;
            if (key.StartsWith("d:")) return DUN.TryGetValue(key.Substring(2), out L) ? L : new[] { "Dungeon_TheAbyss" };
            return ZONE.TryGetValue(key.Substring(2), out L) ? L : new[] { "Wilds_ManaTwo" };
        }
        readonly Dictionary<string, string> lastTrack = new Dictionary<string, string>();
        string NextZone(string key) { var L = Playlist(key); string t = L[0], last; if (L.Length > 1 && lastTrack.TryGetValue(key, out last)) { int i = System.Array.IndexOf(L, last); t = L[(i + 1) % L.Length]; } lastTrack[key] = t; return t; }
        AldaraMonsters.Mon boss; string bossState = "", zoneKey = ""; float gapUntil, introT; bool pending, started;
        AldaraMonsters.Mon FindBoss()
        {
            var M = AldaraMonsters.I; var P = AldaraPlayer.I; if (M == null || !P) return null; AldaraMonsters.Mon best = null; float bd = 1e9f;
            foreach (var m in M.all) { if (!m.boss || m.dead || m.dummy) continue; float d = Mathf.Sqrt((m.x - P.x) * (m.x - P.x) + (m.y - P.y) * (m.y - P.y)); if (d < bd) { bd = d; best = m; } }
            return best != null && bd < (boss == best ? 1300 : 720) ? best : null;
        }
        void Update()
        {
            float dt = Mathf.Min(1, Time.unscaledDeltaTime), now = Time.unscaledTime; var H = AldaraHero.I;
            if (!AldaraSave.Ready || !H || !AldaraPlayer.I) { if (voices.Count > 0) FadeAll(null, 1.5f); zoneKey = ""; boss = null; bossState = ""; }
            else
            {
                var b = H.alive ? FindBoss() : null;
                if (b != boss)
                {
                    if (boss != null && bossState != "") { FadeAll("boss", 3); gapUntil = now + 3.5f; }
                    boss = b; bossState = "";
                    if (b != null) { bossState = "intro"; introT = now; FadeAll("zone", 2); Voice(Rx(b.name, "Frost Giant|Glacia") ? "Stinger_FrostBoss_TheIceGiants" : "Stinger_BossIntro_StrengthOfTheTitans", 0.6f, "boss"); }
                }
                else if (b != null && (bossState == "intro" || bossState == "wait") && (b.engT > 0 || b.act != null || b.hp < b.maxHp) && now - introT > 4.5f) { bossState = "fight"; FadeAll("boss", 2); Voice(BossTrack(b.name), 2, "boss", true); }
                else if (b != null && bossState == "intro") { var s = Cur("boss"); if (s == null || s.ended) bossState = "wait"; }
                if (boss == null)
                {
                    string key = PlaceKey(); var cur = Cur("zone");
                    if (key != zoneKey) { bool first = zoneKey == ""; zoneKey = key; FadeAll("zone", first ? 1.5f : 5); gapUntil = Mathf.Max(gapUntil, now + (first ? 0.3f : 1.5f)); pending = true; }
                    else if (cur != null && cur.ended) { cur.kill = true; float[] g; if (!GAP.TryGetValue(key.Length > 2 ? key.Substring(2) : key, out g)) g = new[] { 20f, 50f }; gapUntil = now + g[0] + Random.value * (g[1] - g[0]); pending = true; }
                    else if (cur == null) pending = true;
                    if (pending && now >= gapUntil) { pending = false; Voice(NextZone(key), started ? 5 : 2.5f, "zone"); started = true; }
                }
            }
            float mv = Vol();
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                var v = voices[i]; if (v.a && v.a.isPlaying) v.played = true; if (v.a && v.played && !v.a.loop && !v.a.isPlaying && !AudioListener.pause && v.a.time <= 0.01f) v.ended = true;
                if (v.g < v.to) v.g = Mathf.Min(v.to, v.g + v.rate * dt); else if (v.g > v.to) v.g = Mathf.Max(v.to, v.g - v.rate * dt);
                if (v.a) v.a.volume = Mathf.Clamp01(v.g * mv);
                if (v.kill && v.g <= 0) { if (v.a) Destroy(v.a.gameObject); voices.RemoveAt(i); }
            }
        }
    }
}
