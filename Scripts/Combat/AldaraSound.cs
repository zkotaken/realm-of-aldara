using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Aldara
{
    // The browser's sound (combat sound pack v0.2): 505 sounds, mixed the way its Audio Bible describes. Every sound goes
    // through a bus (your own actions, enemies aimed at you, other players, the interface) with random pitch and volume,
    // never one of the last two variants played, voice limits, distance and panning for things away from you, a short
    // duck on crits, and a heartbeat when you drop below a quarter of your health. Hooks follow the browser's wrappers:
    // swings, skills, hits (weapon layer + what was hit), deaths, monster attacks and cries, hits on you, enemy shots,
    // parries, vials and levelling. The sounds are the browser's own (Resources/Sound, converted from its WebM data).
    public class AldaraSound : MonoBehaviour
    {
        public static AldaraSound I;
        public class Opt { public string bus = "self"; public float vol, delay, rate = 1, dur, fadeIn = 0.2f, fadeOut = 0.3f, pitch = -1, p = -1; public bool loop, drop, hasXY, force; public float x, y; public int max; }
        public class Voice { public AudioSource src; public string grp, bus; public float lv, t0, end, gain, fadeTo, fadeRate; public float stopAt = -1, fadeRateOut = 3; }
        static readonly Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>();
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, List<string>> last = new Dictionary<string, List<string>>();
        readonly List<Voice> active = new List<Voice>(); readonly Stack<AudioSource> pool = new Stack<AudioSource>();
        static readonly Dictionary<string, float> BUS_DB = new Dictionary<string, float> { { "self", -3 }, { "enemy", -6 }, { "others", -10 }, { "ui", -6 } };
        static float Db(float d) { return Mathf.Pow(10, d / 20); }
        static bool loaded;
        void Awake() { I = this; if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>(); }
        static void Load()
        {
            if (loaded) return; loaded = true;
            foreach (var c in Resources.LoadAll<AudioClip>("Sound")) { clips[c.name] = c; string g = Regex.Replace(c.name, @"_\d+$", ""); List<string> L; if (!groups.TryGetValue(g, out L)) groups[g] = L = new List<string>(); L.Add(c.name); }
        }
        static float SetF(string k, float d) { return AldaraSettings.F(k, d); }
        static bool On { get { return AldaraSettings.On("sndOn", true); } }
        static float BusGain(string bus) { if (!On) return 0; float u = SetF(bus == "self" ? "sndSelf" : bus == "enemy" ? "sndEnemy" : bus == "others" ? "sndOthers" : "sndUi", 1); float b; BUS_DB.TryGetValue(bus, out b); return SetF("sndMaster", 0.8f) * Db(-4) * Db(b) * u; }
        static string Pick(string g)
        {
            List<string> L; if (!groups.TryGetValue(g, out L) || L.Count == 0) return null; if (L.Count < 3) return L[Random.Range(0, L.Count)];
            List<string> ls; if (!last.TryGetValue(g, out ls)) last[g] = ls = new List<string>(); string k; int n = 0;
            do { k = L[Random.Range(0, L.Count)]; } while (ls.Contains(k) && ++n < 12); ls.Add(k); if (ls.Count > 2) ls.RemoveAt(0); return k;
        }
        static void Dist(float x, float y, out float g, out float p)
        {
            var P = AldaraPlayer.I; g = 1; p = 0; if (!P) return; float dx = x - P.x, dy = y - P.y, d = Mathf.Sqrt(dx * dx + dy * dy), mn = 70, mx = 1000;
            float q = d <= mn ? 1 : Mathf.Max(0, 1 - Mathf.Log(d / mn) / Mathf.Log(mx / mn)); g = q * q * 0.7f + q * 0.3f; p = Mathf.Clamp(dx / 650, -0.75f, 0.75f);
        }
        /// snd(): play one sound or a random variant of a group
        public static Voice Play(string g, Opt o = null)
        {
            if (!I || !On) return null; Load(); o = o ?? new Opt();
            float pg = 1, pan = 0; if (o.bus != "self" && o.bus != "ui" && o.hasXY) { Dist(o.x, o.y, out pg, out pan); if (pg < 0.02f) return null; }
            string k = clips.ContainsKey(g) ? g : Pick(g); if (k == null) return null; string grp = Regex.Replace(k, @"_\d+$", "");
            float now = Time.unscaledTime; I.active.RemoveAll(a => { if (a.end <= now || a.src == null || (!a.src.isPlaying && now > a.t0 + 0.05f)) { I.Free(a); return true; } return false; });
            int max = o.max > 0 ? o.max : (grp.StartsWith("IMP_") || grp.StartsWith("HIT_") ? 4 : 6);
            var same = I.active.FindAll(a => a.grp == grp);
            if (same.Count >= max) { if (o.drop) return null; same.Sort((a, b) => a.t0.CompareTo(b.t0)); I.StopV(same[0], 0.02f); }
            if (I.active.Count >= 64) { var pl = I.active.FindAll(a => a.bus != "self"); if (pl.Count == 0) return null; pl.Sort((a, b) => a.lv.CompareTo(b.lv)); I.StopV(pl[0], 0); }
            var src = I.Get(); var clip = clips[k]; src.clip = clip;
            float sp = o.pitch >= 0 ? o.pitch : (grp.StartsWith("IMP_") ? 0.05f : 0.03f); src.pitch = o.rate * (1 + (Random.value * 2 - 1) * sp);
            float lv = Db(o.vol + (Random.value * 3 - 1.5f)) * pg; src.panStereo = pan; src.loop = o.loop;
            var V = new Voice { src = src, grp = grp, bus = o.bus, lv = lv, t0 = now + o.delay };
            if (o.loop) { V.gain = 0; V.fadeTo = 1; V.fadeRate = 1 / Mathf.Max(0.01f, o.fadeIn); if (o.dur > 0) { V.end = V.t0 + o.dur; V.stopAt = V.end - o.fadeOut; V.fadeRateOut = 1 / Mathf.Max(0.01f, o.fadeOut); } else V.end = 1e12f; }
            else { V.gain = 1; V.fadeTo = 1; V.end = V.t0 + clip.length / Mathf.Max(0.05f, Mathf.Abs(src.pitch)) + 0.05f; }
            src.volume = lv * V.gain * BusGain(o.bus);
            if (o.delay > 0) src.PlayDelayed(o.delay); else src.Play();
            I.active.Add(V); return V;
        }
        public static void Stop(Voice v, float fade = 0.3f) { if (I && v != null) I.StopV(v, fade); }
        void StopV(Voice v, float fade) { if (v == null) return; if (fade <= 0) { if (v.src) v.src.Stop(); v.end = 0; return; } v.fadeTo = 0; v.fadeRate = 1 / fade; v.end = Time.unscaledTime + fade + 0.02f; v.stopAt = -1; }
        AudioSource Get() { if (pool.Count > 0) { var s = pool.Pop(); if (s) return s; } var go = new GameObject("snd"); go.transform.SetParent(transform, false); var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 0; a.dopplerLevel = 0; return a; }
        void Free(Voice v) { if (v.src) { v.src.Stop(); v.src.clip = null; pool.Push(v.src); v.src = null; } }

        void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var v = active[i]; if (v.src == null) { active.RemoveAt(i); continue; }
                if (v.end <= now) { Free(v); active.RemoveAt(i); continue; }
                if (v.stopAt > 0 && now >= v.stopAt) { v.fadeTo = 0; v.fadeRate = v.fadeRateOut; v.stopAt = -1; }
                if (now >= v.t0) { if (v.gain < v.fadeTo) v.gain = Mathf.Min(v.fadeTo, v.gain + v.fadeRate * dt); else if (v.gain > v.fadeTo) v.gain = Mathf.Max(v.fadeTo, v.gain - v.fadeRate * dt); }
                float duck = v.bus == "others" && now < duckUntil ? Db(-3) : 1;
                v.src.volume = v.lv * v.gain * BusGain(v.bus) * duck;
            }
            Watch();
        }
        static float duckUntil;
        /// sndDuck: crits and big slams duck other players' combat by 3 dB for 250 ms
        public static void Duck() { duckUntil = Time.unscaledTime + 0.45f; }

        // ---- what things are made of and what they hit with ----
        static bool Rx(string s, string p) { return Regex.IsMatch(s ?? "", p, RegexOptions.IgnoreCase); }
        public static string Mat(AldaraMonsters.Mon m)
        {
            if (m == null) return "Flesh"; string n = (m.name ?? "") + " " + (m.def != null ? m.def.kind : "");
            if (Rx(n, "skelet|bone|lich|ghoul|crypt|skull|undead|tyrant")) return "Bone";
            if (Rx(n, "wraith|ghost|wisp|spirit|shade|specter|spectre|phantom|void|seraph|horror|star eater|archon|deity|ascended|emperor|seer")) return "Ethereal";
            if (Rx(n, "golem|elemental|automaton|sentinel|sentry|brass|clockwork|magma|titan|gargoyle|construct|artificer")) return "Stone";
            if (Rx(n, "knight|oathbreaker|champion|guard|warden|marshal|captain|sergeant|paladin|vanguard|king oswin")) return "Plate";
            if (Rx(n, "bandit|brigand|deserter|raider|orc|soldier|cutthroat|arbalest|crossbow|warlord|reaver|marauder|goblin warchief|ogre")) return "Mail";
            if (Rx(n, "wolf|warg|hound|bear|troll|wyvern|dragon|wyrm|serpent|salamander|rat|bat|crawler|weaver|harpy|centaur|treant|heartwood|spider|brood|boar|beast|naga|imp")) return "Leather";
            return "Flesh";
        }
        public static string Size(AldaraMonsters.Mon m) { if (m == null) return "Medium"; if (m.boss) return "Huge"; float r = m.r > 0 ? m.r : 16; return r < 13 ? "Small" : r < 22 ? "Medium" : r < 34 ? "Large" : "Huge"; }
        static string MonWpn(AldaraMonsters.Mon m)
        {
            string n = m != null ? m.name ?? "" : "";
            if (Rx(n, "archer|crossbow|arbalest|bow|marksman|ranger")) return "Pierce";
            if (Rx(n, "golem|troll|ogre|brute|giant|titan|automaton|treant|bonebreaker|forge|tyrant")) return "Blunt";
            if (Rx(n, "wolf|warg|hound|rat|bat|crawler|spider|dragon|wyrm|wyvern|serpent|salamander|harpy|slime|imp|beast|bear|boar|naga")) return "Unarmed";
            return "Blade";
        }
        static string PlayerMat()
        {
            var H = AldaraHero.I; string kind = null; var ch = H ? H.Eq("chest") : null; if (ch != null) { string n = ch.name ?? ""; kind = Rx(n, "plate|cuirass|aegis|bulwark") ? "plate" : Rx(n, "mail|hauberk|chain|scale") ? "mail" : Rx(n, "leather|jerkin|hide|vest") ? "leather" : null; }
            return kind == "plate" ? "Plate" : kind == "mail" ? "Mail" : kind == "leather" ? "Leather" : H && H.cls == "knight" ? "Mail" : H && H.cls == "archer" ? "Leather" : "Flesh";
        }
        static readonly Dictionary<string, string> COL = new Dictionary<string, string> { { "#ffb14a", "Fire" }, { "#ff8a3a", "Fire" }, { "#ff6a1a", "Fire" }, { "#bfefff", "Frost" }, { "#cfefff", "Frost" }, { "#8fdfff", "Frost" }, { "#cfe0ff", "Arcane" }, { "#d0a0ff", "Arcane" }, { "#c07aff", "Arcane" }, { "#9fc0ff", "Arcane" }, { "#9aff6a", "Nature" }, { "#fff4b0", "Holy" } };
        static readonly Dictionary<string, string> EL = new Dictionary<string, string> { { "fire", "Fire" }, { "frost", "Frost" }, { "shadow", "Shadow" }, { "void", "Shadow" }, { "holy", "Holy" }, { "nature", "Nature" }, { "poison", "Nature" }, { "arcane", "Arcane" }, { "water", "Arcane" }, { "blood", "Blood" }, { "bone", "Blood" } };
        static readonly string[] GREAT = { "Runed Greatsword", "Hellfire Cleaver", "Celestial Claymore", "Voidreaver" };
        static string KnightSwing()
        {
            var w = AldaraHero.I.Eq("weapon"); string n = w != null ? w.name : ""; if (w == null) return "MEL_Fist_Swing";
            if (System.Array.IndexOf(GREAT, n) >= 0) return "MEL_Greatsword2H_Swing"; if (Rx(n, "dagger")) return "MEL_Dagger_Swing"; if (Rx(n, "cleaver|axe")) return "MEL_Axe1H_Swing"; return "MEL_Sword1H_Swing";
        }
        static string SheathClass()
        {
            var H = AldaraHero.I; var w = H.Eq("weapon"); string n = w != null ? w.name : "";
            if (H.cls == "mage") return "Polearm"; if (H.cls != "knight" || w == null) return null;
            if (System.Array.IndexOf(GREAT, n) >= 0) return "Blade2H"; if (Rx(n, "dagger")) return "Dagger"; return "Blade1H";
        }
        // which school and weapon each skill sounds like
        public class SK { public object sw; public int heavy, fin, big, slam, travel, n = 1, bow, draw, thr, dodge, block; public string wpn, sch, rel, rel2, buff, heal, linger, debuff; public float ld; }
        static readonly Dictionary<string, SK> SND_SK = new Dictionary<string, SK> {
            { "power_strike", new SK { sw = 1, heavy = 1 } }, { "whirlwind", new SK { sw = 2, heavy = 1 } }, { "shield_bash", new SK { sw = "MEL_Fist_Swing", wpn = "Blunt", block = 1 } }, { "war_cry", new SK { buff = "Blood", rel = "Blood" } }, { "second_wind", new SK { heal = "Nature" } },
            { "ground_slam", new SK { sw = "MEL_Maul2H_Swing", heavy = 1, wpn = "Blunt", slam = 1 } }, { "cleave", new SK { sw = 1, fin = 1 } }, { "battle_roar", new SK { rel = "Blood", debuff = "Blood", heavy = 1 } }, { "iron_skin", new SK { buff = "Holy" } },
            { "leap_strike", new SK { sw = "MEL_Greatsword2H_Swing", heavy = 1, wpn = "Blunt", slam = 1 } }, { "judgement", new SK { sw = 1, heavy = 1, sch = "Holy", rel = "Holy", big = 1 } },
            { "fireball", new SK { sch = "Fire", rel = "Fire", travel = 1 } }, { "ice_shard", new SK { sch = "Frost", rel = "Frost", travel = 1 } }, { "chain_lightning", new SK { sch = "Arcane", rel = "Arcane" } }, { "arcane_barrier", new SK { buff = "Arcane" } }, { "rejuvenate", new SK { heal = "Nature" } },
            { "meteor", new SK { sch = "Fire", rel = "Fire", big = 1, slam = 1, travel = 1 } }, { "frost_nova", new SK { sch = "Frost", rel = "Frost" } }, { "arcane_missiles", new SK { sch = "Arcane", rel = "Arcane", n = 5 } }, { "blink", new SK { rel = "Arcane", dodge = 1 } },
            { "blizzard", new SK { sch = "Frost", rel = "Frost", linger = "Frost", ld = 2.6f } }, { "arcane_cataclysm", new SK { sch = "Shadow", rel = "Arcane", rel2 = "Shadow", big = 1, slam = 1 } },
            { "multi_shot", new SK { bow = 3 } }, { "piercing_arrow", new SK { bow = 1, fin = 1 } }, { "frost_arrow", new SK { bow = 1, sch = "Frost" } }, { "eagle_eye", new SK { buff = "Nature" } }, { "rain_of_arrows", new SK { bow = 3, draw = 1 } }, { "explosive_arrow", new SK { bow = 1, sch = "Fire" } },
            { "poison_arrow", new SK { bow = 1, sch = "Nature", debuff = "Nature", linger = "Nature", ld = 4 } }, { "explosive_trap", new SK { sch = "Fire", thr = 1 } }, { "evasive_roll", new SK { dodge = 1 } }, { "barrage", new SK { bow = 8, draw = 1 } }, { "starfall_arrow", new SK { bow = 1, sch = "Holy", rel = "Holy", big = 1 } } };
        static SK SkillOf(SkillDef sk)
        {
            if (sk == null) return new SK(); SK S0; bool own = SND_SK.TryGetValue(sk.id ?? "", out S0); if (!own && sk.baseId != null) SND_SK.TryGetValue(sk.baseId, out S0);
            var S = S0 != null ? S0.Copy() : new SK(); string n = (sk.id ?? "") + " " + (sk.name ?? "");
            string kw = Rx(n, "fire|flame|burn|inferno|meteor|ember|ash") ? "Fire" : Rx(n, "frost|ice|blizz|snow|glacia|winter") ? "Frost" : Rx(n, "shadow|void|dark|curse|soul|death|dead|night") ? "Shadow" :
                Rx(n, "holy|light|judg|divine|sacred|smite|star|celest|dawn|seraph") ? "Holy" : Rx(n, "poison|nature|thorn|vine|root|venom|wild") ? "Nature" : Rx(n, "blood|rage|roar|cry|berserk|undying") ? "Blood" : Rx(n, "arcane|thunder|storm|lightning|burst") ? "Arcane" : null;
            if (kw != null && !own) { if (S.sch != null) S.sch = kw; if (S.rel != null) S.rel = kw; if (S.buff != null) S.buff = kw; if (S.heal != null) S.heal = kw; if (S.sch == null && S.buff == null && S.heal == null && S.dodge == 0 && S.bow == 0 && S.sw == null) S.rel = kw; }
            return S;
        }

        // ---- voices ----
        static string VoiceSet() { string v = AldaraSettings.Get("sndVoice") != null ? (string)AldaraSettings.Get("sndVoice") : "HumanM"; if (string.IsNullOrEmpty(v)) v = "HumanM"; return v == "off" ? null : v; }
        static readonly Dictionary<string, float> VO_GAP = new Dictionary<string, float> { { "AttackLight", 0.9f }, { "AttackHeavy", 0.7f }, { "Cast", 1f }, { "PainLight", 1.1f }, { "PainHeavy", 0.9f }, { "Jump", 0.4f }, { "Land", 0.4f }, { "Death", 0 } };
        static float voT = -9;
        public static Voice Vo(string act, Opt o = null)
        {
            var v = VoiceSet(); if (v == null) return null; o = o ?? new Opt(); float now = Time.unscaledTime; float gap; if (!VO_GAP.TryGetValue(act, out gap)) gap = 0.6f;
            if (!o.force && now - voT < gap) return null; if (o.p >= 0 && Random.value > o.p) return null; voT = now;
            if (o.vol == 0) o.vol = -3; o.max = 1; o.pitch = 0.02f; return Play("VO_EFF_" + v + "_" + act, o);
        }
        static readonly Dictionary<AldaraMonsters.Mon, string> cre = new Dictionary<AldaraMonsters.Mon, string>(); static readonly Dictionary<AldaraMonsters.Mon, float> creRate = new Dictionary<AldaraMonsters.Mon, float>(), creT = new Dictionary<AldaraMonsters.Mon, float>(), aggT = new Dictionary<AldaraMonsters.Mon, float>();
        static string Cre(AldaraMonsters.Mon m)
        {
            if (m == null || m.dummy) return null; string c; if (cre.TryGetValue(m, out c)) return c; string kind = m.def != null ? m.def.kind : null; string n = (m.name ?? "") + " " + (kind ?? "");
            if (Rx(n, "spider|brood|weaver|crawler|scarab|beetle|arachna")) c = "Spider";
            else if (Rx(n, "wraith|ghost|spirit|shade|spect|phantom|lich|banshee|wisp|void|horror|glacia|emperor|hollow|countess|seraph|astraeus|star eater|siren|nerissa|naga|jelly")) c = "Wraith";
            else if (Rx(n, @"ghoul|zombie|undead|skelet|bone|crypt|corpse|rat\b|imp\b|slime|blob|bog")) c = "Ghoul";
            else if (Rx(n, @"wolf|warg|hound|fox|cat\b|panther|bear|boar|dragon|wyrm|wyvern|drake|salamander|beast|stalker|lion|harpy|bats?\b")) c = "DireWolf";
            else if (Rx(n, "troll|ogre|giant|golem|brute|orc|titan|treant|behemoth|warchief|goblin|grak|ignar|infernal|artificer|minotaur|yeti|elemental|centaur")) c = "Troll";
            else if (kind == "hum" || Rx(n, "bandit|knight|soldier|guard|cultist|raider|archer|mage|warden|captain")) c = "Human";
            float r = m.r > 0 ? m.r : 16, rate = m.boss ? 0.8f : r > 30 ? 0.9f : r < 13 ? 1.15f : 1; if (Rx(n, "dragon|wyrm|behemoth|tyrant")) rate *= 0.75f; creRate[m] = rate;
            if (cre.Count > 600) { cre.Clear(); creRate.Clear(); creT.Clear(); aggT.Clear(); }
            return cre[m] = c;
        }
        static readonly Dictionary<string, string> HUM = new Dictionary<string, string> { { "Aggro", "AttackHeavy" }, { "Attack", "AttackLight" }, { "Pain", "PainLight" }, { "Death", "Death" } };
        static float creN, creLast;
        public static Voice CreVo(AldaraMonsters.Mon m, string act, float p = -1, float vol = 999)
        {
            var c = Cre(m); if (c == null) return null; float now = Time.unscaledTime; float t0; creT.TryGetValue(m, out t0);
            if (act != "Death" && now - t0 < (act == "Pain" ? 1.4f : 0.9f) && t0 > 0) return null; if (p >= 0 && Random.value > p) return null;
            creN = creLast > now - 0.5f ? creN + 1 : 0; creLast = now; if (creN > 2 && act != "Death" && !m.boss) return null; creT[m] = now;
            string g; if (c == "Human") { string a; if (!HUM.TryGetValue(act, out a)) return null; g = "VO_EFF_" + (Rx(m.name, "orc|goblin") ? "OrcM" : "HumanM") + "_" + a; } else g = "VO_CRE_" + c + "_" + act;
            float rt; creRate.TryGetValue(m, out rt);
            return Play(g, new Opt { bus = "enemy", hasXY = true, x = m.x, y = m.y, vol = vol != 999 ? vol : (m.boss ? -1 : -5), rate = rt > 0 ? rt : 1, max = 3 });
        }

        // ---- hooks into the game ----
        static bool arm; static float lastFight = -99;
        static void Fight() { float now = Time.unscaledTime; if (!arm && now - lastFight > 8) { var c = SheathClass(); if (c != null) Play("MEL_" + c + "_Unsheathe", new Opt { vol = -6 }); arm = true; } lastFight = now; }
        class Ctx { public string sch, wpn; public bool slam; public float until; }
        static Ctx ctxS;
        /// basicAttack (and a free swing that connects)
        public static void Basic()
        {
            var H = AldaraHero.I; if (!H) return; Fight();
            if (H.cls == "knight") { Play(KnightSwing()); ctxS = null; Vo("AttackLight", new Opt { p = 0.3f }); }
            else if (H.cls == "mage") { Play("SPL_Arcane_Release", new Opt { vol = -7 }); ctxS = new Ctx { sch = "Arcane", until = Time.unscaledTime + 1.5f }; }
            else { Play("MEL_Bow_Release", new Opt { vol = -2 }); ctxS = null; Vo("AttackLight", new Opt { p = 0.12f, vol = -6 }); }
        }
        public static void FreeSwing() { var H = AldaraHero.I; if (!H || H.cls != "knight") return; Fight(); Play(KnightSwing()); ctxS = null; Vo("AttackLight", new Opt { p = 0.3f }); }
        static Voice linger;
        /// castSkill
        public static void Skill(SkillDef sk)
        {
            var H = AldaraHero.I; if (!H || sk == null) return; var S = SkillOf(sk);
            if (S.sch != null || S.wpn != null || S.slam != 0) ctxS = new Ctx { sch = S.sch, wpn = S.wpn, slam = S.slam != 0, until = Time.unscaledTime + (S.sch != null ? 2.2f : 0.6f) }; else if (S.sw != null || S.bow != 0) ctxS = null;
            Fight();
            if (S.sw != null) { bool basic = S.sw is int; Play(basic ? KnightSwing() : (string)S.sw); if (basic && (int)S.sw == 2) Play(KnightSwing(), new Opt { delay = 0.22f }); }
            if (S.heavy != 0) Play("MEL_Special_Heavy", new Opt { vol = -2 }); if (S.fin != 0) Play("MEL_Special_Finesse", new Opt { vol = -3 });
            if (S.big != 0 && S.rel != null) Play("SPL_" + S.rel + "_PrecastLoop", new Opt { loop = true, dur = 0.55f, fadeIn = 0.12f, fadeOut = 0.3f, vol = -4 });
            if (S.rel != null) for (int i = 0; i < Mathf.Max(1, S.n); i++) Play("SPL_" + S.rel + "_Release", new Opt { delay = i * 0.12f + (S.big != 0 ? 0.1f : 0), vol = i > 0 ? -4 : 0, max = 6 });
            if (S.rel2 != null) Play("SPL_" + S.rel2 + "_Release", new Opt { delay = 0.18f, vol = -3 });
            if (S.travel != 0 && S.sch != null) Play("SPL_" + S.sch + "_TravelLoop", new Opt { loop = true, dur = 0.5f, fadeIn = 0.05f, fadeOut = 0.25f, vol = -6, delay = 0.05f });
            if (S.draw != 0) Play("MEL_Bow_Draw", new Opt { vol = -4 });
            if (S.bow != 0) for (int i = 0; i < Mathf.Min(S.bow, 8); i++) Play("MEL_Bow_Release", new Opt { delay = (S.draw != 0 ? 0.25f : 0) + i * (S.bow > 3 ? 0.13f : 0.07f), vol = i > 0 ? -4 : -1, max = 4 });
            if (S.thr != 0) Play("MEL_Fist_Swing", new Opt { vol = -4 });
            if (S.buff != null) Play("SPL_" + S.buff + "_Buff");
            if (S.heal != null) Play("SPL_" + S.heal + "_Heal");
            if (S.dodge != 0) Play("HIT_Dodge"); if (S.block != 0) Play("HIT_Block_Metal", new Opt { delay = 0.08f, vol = -2 });
            if (S.linger != null) linger = Play("SPL_" + S.linger + "_LingerLoop", new Opt { loop = true, dur = S.ld > 0 ? S.ld : 3, vol = -8, delay = 0.2f });
            if (S.debuff != null) Play("SPL_" + S.debuff + "_Debuff", new Opt { delay = 0.25f, vol = -4 });
            if (S.dodge != 0) Vo("Jump", new Opt { p = 0.7f }); else if ((sk.id ?? "").Contains("leap")) { Vo("Jump", new Opt { force = true }); Vo("Land", new Opt { force = true, delay = 0.55f }); }
            else if (S.heavy != 0 || S.big != 0) Vo(H.cls == "mage" ? "Cast" : "AttackHeavy", new Opt { force = true });
            else if (H.cls == "mage" && (S.rel != null || S.sch != null)) Vo("Cast", new Opt { p = 0.55f });
            else if (S.sw != null || S.bow != 0) Vo("AttackLight", new Opt { p = 0.5f });
        }
        static float lastImp = -9; static int impN;
        /// hitMonster: weapon layer + target layer (or the school's impact + target at -6 dB), crit sweetener on top
        public static void Hit(AldaraMonsters.Mon m, Color col, bool crit, string src, bool dead0)
        {
            if (m == null || m.dummy || dead0) return; var H = AldaraHero.I; float now = Time.unscaledTime; string mat = Mat(m);
            if (lastImp > now - 0.045f) { impN++; if (impN > 2) return; } else impN = 0; lastImp = now; Fight();
            var C = ctxS != null && ctxS.until > now ? ctxS : null; string hex = "#" + ColorUtility.ToHtmlStringRGB(col).ToLowerInvariant(); string sch; COL.TryGetValue(hex, out sch);
            if (src == "pet") { Play("IMP_Wpn_Unarmed", new Opt { vol = -6 }); Play("IMP_Tgt_" + mat, new Opt { vol = -8 }); }
            else if (sch != null || (C != null && C.sch != null)) { var s = sch ?? C.sch; Play("SPL_" + s + "_Impact", new Opt { vol = (H.cls == "mage" && C == null ? -4 : 0) - (impN > 0 ? 4 : 0), max = 3 }); Play("IMP_Tgt_" + mat, new Opt { vol = -6 }); }
            else { string w = C != null && C.wpn != null ? C.wpn : H.cls == "archer" ? "Pierce" : H.cls == "mage" ? "Unarmed" : H.Eq("weapon") != null ? "Blade" : "Unarmed"; float d = H.cls == "knight" ? 0.07f : 0; Play("IMP_Wpn_" + w, new Opt { delay = d }); Play("IMP_Tgt_" + mat, new Opt { delay = d, vol = -1 }); }
            if (C != null && C.slam && impN == 0) Play("HIT_BodyFall_Large", new Opt { vol = -3, delay = 0.02f });
            if (crit) { Play("HIT_Crit", new Opt { delay = 0.02f }); Duck(); }
            if (!m.dead) CreVo(m, "Pain", m.boss ? 0.25f : 0.4f);
        }
        public static void Kill(AldaraMonsters.Mon t) { if (t == null || t.dummy) return; Play("HIT_BodyFall_" + Size(t), new Opt { bus = "enemy", hasXY = true, x = t.x, y = t.y, delay = 0.15f, vol = -2 }); CreVo(t, "Death"); }
        public static void Attack(AldaraMonsters.Mon m) { if (m != null) CreVo(m, "Attack", m.boss ? 0.8f : 0.45f); }
        /// monCombat: a call when a monster first takes you on (again after 12 s apart)
        public static void Engage(AldaraMonsters.Mon m) { float now = Time.unscaledTime, t; if (!aggT.TryGetValue(m, out t) || now - t > 12) CreVo(m, "Aggro", 0.85f); aggT[m] = now; }
        static bool absorbed, blocked, fromMon;
        /// hurtPlayer: a monster's blow or spell lands on you
        public static void HurtBegin() { absorbed = blocked = false; fromMon = true; }
        public static void HurtEnd(AldaraMonsters.Mon m, MoveDef mv)
        {
            fromMon = false; var H = AldaraHero.I; if (!H) return; string sch = null; if (mv != null && mv.el != null) EL.TryGetValue(mv.el, out sch); bool big = (mv != null && mv.dmg > 0 ? mv.dmg : 1) >= 1.4f;
            if (sch != null) Play("SPL_" + sch + "_Impact", new Opt { bus = "enemy", vol = big ? 0 : -3, max = 3 });
            else { Play("IMP_Wpn_" + MonWpn(m), new Opt { bus = "enemy", vol = big ? 0 : -2 }); if (!absorbed && !blocked) Play("IMP_Tgt_" + PlayerMat(), new Opt { bus = "enemy", vol = -3 }); }
            if (blocked) { var w = H.Eq("weapon"); Play(w == null ? "HIT_Block_Wood" : "HIT_Block_Metal", new Opt { bus = "enemy", vol = -2 }); }
            if (big) Play("HIT_Crit", new Opt { bus = "enemy", vol = -6 });
            if (mv != null && mv.slow > 0) Play("SPL_Frost_Debuff", new Opt { bus = "enemy", vol = -6 });
            absorbed = blocked = false;
        }
        /// damagePlayer: the shield soaking it, a knight's block, pain cries and death
        public static void Damaged(float sh0, float hp0)
        {
            var H = AldaraHero.I; if (!H) return; Fight();
            bool blk = AldaraTree.T("block") > 0 && H.cls == "knight" && !(H.Eq("weapon") != null && System.Array.IndexOf(GREAT, H.Eq("weapon").name) >= 0);
            if (sh0 > 0 && H.shield < sh0 && H.hp >= hp0) { absorbed = true; Play("HIT_Absorb", new Opt { bus = "enemy", vol = -2 }); }
            else if (blk && fromMon && Random.value < 0.5f) blocked = true;
            if (!fromMon && H.hp < hp0) Play("IMP_Tgt_" + PlayerMat(), new Opt { bus = "enemy", vol = -4 });
            if (hp0 > 0 && H.hp <= 0) { Play("HIT_BodyFall_Medium", new Opt { vol = -1, delay = 0.2f }); Vo("Death", new Opt { force = true, vol = -1 }); }
            else if (H.hp < hp0) { float lost = (hp0 - H.hp) / Mathf.Max(1, H.maxHp); if (lost >= 0.12f) Vo("PainHeavy", new Opt { force = lost >= 0.25f }); else Vo("PainLight", new Opt { p = 0.45f, vol = -5 }); }
        }
        public static void Shot(AldaraMonsters.Mon m, MoveDef mv) { if (m == null) return; string sch = null; if (mv != null && mv.el != null) EL.TryGetValue(mv.el, out sch); Play(sch != null ? "SPL_" + sch + "_Release" : (Rx(m.name, "crossbow|arbalest") ? "MEL_Crossbow_Release" : "MEL_Bow_Release"), new Opt { bus = "enemy", hasXY = true, x = m.x, y = m.y, vol = -3, max = 3, drop = true }); }
        public static void Text(string t) { if (t == "Parry") Play("HIT_Parry"); else if (t == "Glance") Play("HIT_Parry", new Opt { vol = -5 }); }
        public static void Vial(bool hp) { Play(hp ? "SPL_Nature_Heal" : "SPL_Arcane_Heal", new Opt { bus = "ui", vol = -2 }); }
        public static void LevelUp() { Play("SPL_Holy_Buff", new Opt { bus = "ui" }); Play("SPL_Holy_Release", new Opt { bus = "ui", vol = -6, delay = 0.1f }); }
        public static void Test() { var H = AldaraHero.I; Play(H && H.cls == "mage" ? "SPL_Fire_Release" : H && H.cls == "archer" ? "MEL_Bow_Release" : "MEL_Sword1H_Swing"); Play("IMP_Wpn_Blade", new Opt { delay = 0.12f }); Play("IMP_Tgt_Plate", new Opt { delay = 0.12f }); }

        // ---- every quarter second: the low-health heartbeat, sheathing after a fight, buffs and shields running out, idle cries ----
        float watchT; bool lowHp, atkWas, hasteWas, shieldWas; Voice hb;
        void Watch()
        {
            watchT -= Time.unscaledDeltaTime; if (watchT > 0) return; watchT = 0.25f; var H = AldaraHero.I; var P = AldaraPlayer.I; if (!H || !P || !AldaraSave.Ready) return;
            bool low = H.alive && H.hp > 0 && H.hp < H.maxHp * 0.25f;
            if (low != lowHp) { lowHp = low; if (low) { var v = VoiceSet(); hb = Play(v != null ? "VO_EFF_" + v + "_LowHealthBreath_Loop" : "SPL_Blood_LingerLoop", new Opt { loop = true, vol = v != null ? -6 : -9, fadeIn = 0.8f }); } else { Stop(hb, 0.8f); hb = null; } }
            if (arm && Time.unscaledTime - lastFight > 8) { arm = false; var c = SheathClass(); if (c != null && H.alive) Play("MEL_" + c + "_Sheathe", new Opt { vol = -7 }); }
            bool atk = H.atkBuff > 0, haste = H.hasteBuff > 0; if (atkWas && !atk) Play("SPL_Blood_Fade", new Opt { vol = -5 }); if (hasteWas && !haste) Play("SPL_Nature_Fade", new Opt { vol = -5 }); atkWas = atk; hasteWas = haste;
            var M = AldaraMonsters.I;
            if (M != null && M.all.Count > 0 && Random.value < 0.06f) { var m = M.all[Random.Range(0, M.all.Count)]; if (m != null && !m.dead && !(m.engT > 0) && Mathf.Sqrt((m.x - P.x) * (m.x - P.x) + (m.y - P.y) * (m.y - P.y)) < 520) { var c = Cre(m); if (c != null && c != "Human") CreVo(m, "Idle", -1, -9); } }
            bool sh = H.shield > 0; if (shieldWas && !sh && H.alive) Play("SPL_" + (H.cls == "mage" ? "Arcane" : "Holy") + "_Fade", new Opt { vol = -5 }); shieldWas = sh;
        }
        void OnApplicationFocus(bool f) { AudioListener.pause = !f && !AldaraSettings.On("sndBg", false); }
    }
    static class SKExt
    {
        public static AldaraSound.SK Copy(this AldaraSound.SK s) { return new AldaraSound.SK { sw = s.sw, heavy = s.heavy, fin = s.fin, big = s.big, slam = s.slam, travel = s.travel, n = s.n, bow = s.bow, draw = s.draw, thr = s.thr, dodge = s.dodge, block = s.block, wpn = s.wpn, sch = s.sch, rel = s.rel, rel2 = s.rel2, buff = s.buff, heal = s.heal, linger = s.linger, debuff = s.debuff, ld = s.ld }; }
    }
}
