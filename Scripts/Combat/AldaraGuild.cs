using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // Guilds from the browser (guildCreate, guildJoin, guildLeave, guildDisband, guildAnswer, guildKick, guildSetMode,
    // guildSync): found one for 10 000 gold with a name, tag, description and an emblem; anyone may join at once or must
    // ask first. Without the browser's shared store they are kept on this computer, like its local mode; the realm's three
    // orders are always open. Every guild has its Guild Tavern, a quiet two-storey interior (fortEnter / fortUpdate).
    public static class AldaraGuild
    {
        public const int COST = 10000;
        public class Icon { public string shape = "shield", sym = "sword", bg = "#12304a", fg = "#e8c46a", rim = "#e8c46a"; public Icon Copy() { return (Icon)MemberwiseClone(); } }
        public class Member { public string uid, name, cls; public int lvl; public double t; }
        public class Leader { public string uid, name; }
        public class G { public string id, name, tag, desc, mode = "open"; public Icon icon = new Icon(); public bool npc; public Leader leader; public List<Member> members = new List<Member>(), requests = new List<Member>(); public double created, updated; }
        public static readonly string[] COLS = { "#1a1a2a", "#3a1418", "#12304a", "#1e3a1e", "#3a2a10", "#2a1a40", "#8a1a1a", "#1a4a8a", "#2a7a3a", "#c07a1a", "#6a2aa0", "#e0e0e8", "#e8c46a", "#ff6a3a", "#6ae0ff", "#9aff6a", "#ff7ab0", "#ffffff" };
        public static readonly Dictionary<string, string> SHAPES = new Dictionary<string, string> {
            { "shield", "M32 4l24 8v18c0 16-10 26-24 32C18 56 8 46 8 30V12z" }, { "round", "M32 4a28 28 0 1 0 0 56 28 28 0 0 0 0-56z" }, { "hex", "M32 3l25 14.5v29L32 61 7 46.5v-29z" },
            { "diamond", "M32 2l30 30-30 30L2 32z" }, { "banner", "M10 4h44v48L32 42 10 52z" }, { "star", "M32 2l8 20 21 1-16 13 6 21-19-12-19 12 6-21L3 23l21-1z" },
            { "crest", "M32 3c10 6 20 6 26 4v22c0 17-12 27-26 33C18 56 6 46 6 29V7c6 2 16 2 26-4z" }, { "square", "M8 8h48v48H8z" } };
        public static readonly Dictionary<string, string> SYMS = new Dictionary<string, string> {
            { "sword", "M32 12v30M26 36h12M32 42v8M29 50h6" }, { "crown", "M18 42h28l3-18-10 8-7-12-7 12-10-8z" }, { "skull", "M32 16a12 12 0 0 0-12 12c0 5 3 7 3 10v5h18v-5c0-3 3-5 3-10a12 12 0 0 0-12-12zM27 29h.1M37 29h.1M29 43v4M35 43v4" },
            { "flame", "M32 14c3 7 10 10 10 19a10 10 0 0 1-20 0c0-6 4-8 4-13 2 4 4 6 6 6-2-5 0-8 0-12z" }, { "moon", "M38 16a16 16 0 1 0 0 32 13 13 0 0 1 0-32z" }, { "star", "M32 16l4 10 11 1-8 7 3 11-10-6-10 6 3-11-8-7 11-1z" },
            { "eye", "M16 32s6-10 16-10 16 10 16 10-6 10-16 10-16-10-16-10zM32 27a5 5 0 1 0 0 10 5 5 0 0 0 0-10z" }, { "tree", "M32 14l12 16h-6l8 10H18l8-10h-6zM32 40v10" },
            { "axe", "M24 16l20 32M38 18c6 2 10 8 8 14l-10-6z" }, { "tower", "M22 48V24h4v-6h4v6h4v-6h4v6h4v24zM30 48v-8h4v8" }, { "wing", "M16 40c8-2 14-10 16-22 2 12 8 20 16 22-6 2-12 0-16-4-4 4-10 6-16 4z" },
            { "wolf", "M18 20l8 8h12l8-8-2 16-6 8h-12l-6-8zM27 33h.1M37 33h.1" }, { "rune", "M26 14v36M26 24l12-10M26 32l12 12" }, { "anchor", "M32 16v30M24 22h16M20 36c2 8 7 12 12 12s10-4 12-12M32 16a3 3 0 1 0 0-.1" },
            { "heart", "M32 46s-14-8-14-18a7 7 0 0 1 14-3 7 7 0 0 1 14 3c0 10-14 18-14 18z" }, { "bolt", "M35 12l-12 22h9l-3 18 12-22h-9z" } };
        static readonly G[] NPC = {
            new G { id = "npc_dawn", name = "Order of the Dawn", tag = "DAWN", desc = "Knights sworn to guard the roads of the Green Fields. New recruits are always welcome.", icon = new Icon { shape = "shield", sym = "sword", bg = "#12304a", fg = "#e8c46a", rim = "#e8c46a" }, mode = "open", npc = true, leader = new Leader { uid = "npc", name = "Captain Maren" } },
            new G { id = "npc_ember", name = "The Ember Pact", tag = "EMBR", desc = "Hunters of the Emberwaste. We fight fire with fire.", icon = new Icon { shape = "crest", sym = "flame", bg = "#3a1418", fg = "#ff6a3a", rim = "#ffb04a" }, mode = "open", npc = true, leader = new Leader { uid = "npc", name = "Kael Ashborn" } },
            new G { id = "npc_veil", name = "Keepers of the Veil", tag = "VEIL", desc = "Scholars and mages who study the Void Reach. Curious minds only.", icon = new Icon { shape = "hex", sym = "eye", bg = "#2a1a40", fg = "#c8a0ff", rim = "#9a7aff" }, mode = "open", npc = true, leader = new Leader { uid = "npc", name = "Archmage Vhal" } } };
        static Dictionary<string, G> list;
        static AldaraHero H { get { return AldaraHero.I; } }
        static JObject Raw { get { return AldaraSave.Raw; } }
        static double Now { get { return (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds; } }

        // ---- storage: this computer (the browser's local mode) ----
        static void Load() { if (list != null) return; try { list = JsonConvert.DeserializeObject<Dictionary<string, G>>(PlayerPrefs.GetString("aldara/guilds", "{}")) ?? new Dictionary<string, G>(); } catch { list = new Dictionary<string, G>(); } }
        static void Store() { PlayerPrefs.SetString("aldara/guilds", JsonConvert.SerializeObject(list)); PlayerPrefs.Save(); }
        static void Write(G g) { if (g.npc) return; Load(); g.updated = Now; list[g.id] = g; Store(); }
        public static Dictionary<string, G> All() { Load(); var o = new Dictionary<string, G>(); foreach (var g in NPC) o[g.id] = g; foreach (var kv in list) o[kv.Key] = kv.Value; return o; }
        public static string Uid()
        {
            var r = Raw; if (r == null) return "x"; var u = (string)r["uid"];
            if (string.IsNullOrEmpty(u)) { u = ((long)Now).ToString("x") + Random.Range(0, 0xffffff).ToString("x6"); r["uid"] = u; AldaraSave.Dirty(); }
            return u;
        }
        public static G Mine()
        {
            var all = All(); string me = Uid();
            foreach (var g in all.Values) { if (g.leader != null && g.leader.uid == me) return g; if (g.members != null && g.members.Any(m => m.uid == me)) return g; }
            string gid = Raw != null ? (string)Raw["guildId"] : null; G n; if (gid != null && all.TryGetValue(gid, out n) && n.npc) return n;
            return null;
        }
        static Member MeEntry() { return new Member { uid = Uid(), name = H.heroName, lvl = H.lvl, cls = H.cls, t = Now }; }
        /// guildSync: the character's guild record follows the list
        public static void Sync(bool quiet = false)
        {
            var r = Raw; if (r == null) return; var g = Mine(); string prev = (string)r["guildId"];
            r["guildId"] = g != null ? g.id : null; r["guildTag"] = g != null ? g.tag : null; r["guildCol"] = g != null ? (g.icon != null ? g.icon.fg : "#e8c46a") : null;
            if (!quiet && g != null && prev != g.id) AldaraHud.Banner("You are now a member of " + g.name);
            if (!quiet && g == null && prev != null) AldaraHud.Banner("You are no longer in a guild");
            if (g == null && AldaraFort.On) AldaraFort.Leave();
        }
        public static string Tag { get { return Raw != null ? (string)Raw["guildTag"] : null; } }
        public static Color TagCol { get { var c = Raw != null ? (string)Raw["guildCol"] : null; return AldaraRules.Hex(c ?? "#e8c46a"); } }

        // ---- actions ----
        public static bool Create(string name, string tag, string desc, string mode, Icon icon)
        {
            name = (name ?? "").Trim(); tag = new string((tag ?? "").Trim().ToUpperInvariant().Where(ch => (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')).ToArray()); desc = (desc ?? "").Trim();
            if (Mine() != null) { AldaraHud.Banner("Leave your current guild first"); return false; }
            if (name.Length < 3 || name.Length > 24) { AldaraHud.Banner("Guild name: 3 to 24 characters"); return false; }
            if (tag.Length < 2 || tag.Length > 4) { AldaraHud.Banner("Guild tag: 2 to 4 letters"); return false; }
            var all = All(); if (all.Values.Any(g => g.name.ToLowerInvariant() == name.ToLowerInvariant())) { AldaraHud.Banner("That guild name is taken"); return false; }
            if (all.Values.Any(g => g.tag == tag)) { AldaraHud.Banner("That tag is taken"); return false; }
            if (H.gold < COST) { AldaraHud.Banner("Founding a guild costs " + COST.ToString("N0") + " gold"); return false; }
            H.gold -= COST;
            var ng = new G { id = "g_" + ((long)Now).ToString("x") + Random.Range(0, 0xffff).ToString("x4"), name = name, tag = tag, desc = desc.Length > 220 ? desc.Substring(0, 220) : desc, icon = icon.Copy(), mode = mode == "request" ? "request" : "open", leader = new Leader { uid = Uid(), name = H.heroName }, created = Now };
            ng.members.Add(MeEntry()); Write(ng); Sync(true); AldaraSave.Dirty();
            AldaraHud.Banner("The guild " + name + " is founded!"); AldaraChat.Sys(H.heroName + " founded the guild " + name + " [" + tag + "]!", "myth"); return true;
        }
        public static void Join(string id)
        {
            if (Mine() != null) { AldaraHud.Banner("Leave your current guild first"); return; }
            G g; if (!All().TryGetValue(id, out g)) return;
            if (g.npc) { Raw["guildId"] = g.id; Sync(true); AldaraHud.Banner("You joined " + g.name); AldaraSave.Dirty(); return; }
            var me = MeEntry();
            if (g.mode == "open") { g.members.RemoveAll(m => m.uid == me.uid); g.members.Add(me); g.requests.RemoveAll(r => r.uid == me.uid); Write(g); Sync(true); AldaraHud.Banner("You joined " + g.name); AldaraSave.Dirty(); }
            else { if (g.requests.Any(r => r.uid == me.uid)) { AldaraHud.Banner("Your request is waiting for the leader"); return; } g.requests.Add(me); Write(g); AldaraHud.Banner("Request sent to " + g.name); }
        }
        public static void CancelRequest(string id) { G g; if (!All().TryGetValue(id, out g)) return; g.requests.RemoveAll(r => r.uid == Uid()); Write(g); }
        public static bool Requested(G g) { return g.requests != null && g.requests.Any(r => r.uid == Uid()); }
        public static void Leave()
        {
            var g = Mine(); if (g == null) return; string me = Uid();
            if (g.npc) { Raw["guildId"] = null; Sync(true); AldaraSave.Dirty(); return; }
            if (g.leader.uid == me) { var rest = g.members.Where(m => m.uid != me).ToList(); if (rest.Count == 0) { AldaraHud.Banner("You are the last member: disband the guild instead"); return; } rest.Sort((a, b) => b.lvl.CompareTo(a.lvl)); g.leader = new Leader { uid = rest[0].uid, name = rest[0].name }; }
            g.members.RemoveAll(m => m.uid == me); Write(g); Raw["guildId"] = null; Sync(true); AldaraHud.Banner("You left " + g.name); AldaraSave.Dirty();
        }
        public static void Disband() { var g = Mine(); if (g == null || g.leader.uid != Uid()) return; Load(); list.Remove(g.id); Store(); Raw["guildId"] = null; Sync(true); AldaraHud.Banner(g.name + " has been disbanded"); AldaraSave.Dirty(); }
        public static void Answer(string uid, bool yes)
        {
            var g = Mine(); if (g == null || g.leader.uid != Uid()) return; var r = g.requests.FirstOrDefault(x => x.uid == uid); if (r == null) return;
            g.requests.RemoveAll(x => x.uid == uid); if (yes && !g.members.Any(m => m.uid == uid)) { r.t = Now; g.members.Add(r); }
            Write(g); AldaraHud.Banner(yes ? r.name + " joined the guild" : "Request declined");
        }
        public static void Kick(string uid) { var g = Mine(); if (g == null || g.leader.uid != Uid() || uid == g.leader.uid) return; g.members.RemoveAll(m => m.uid == uid); Write(g); }
        public static void SetMode(string m) { var g = Mine(); if (g == null || g.leader.uid != Uid()) return; g.mode = m; if (m == "open" && g.requests.Count > 0) { foreach (var r in g.requests) { r.t = Now; g.members.Add(r); } g.requests.Clear(); } Write(g); }
        public static void SaveDesc(string d) { var g = Mine(); if (g == null || g.leader.uid != Uid()) return; d = (d ?? "").Trim(); g.desc = d.Length > 220 ? d.Substring(0, 220) : d; Write(g); AldaraHud.Banner("Description saved"); }
        /// guildTick: keep your own level fresh in the member list
        static float beat;
        public static void Tick(float dt)
        {
            beat += dt; if (beat < 60) return; beat = 0; var g = Mine(); if (g == null || g.npc) return; var me = g.members.FirstOrDefault(m => m.uid == Uid());
            if (me != null && (me.lvl != H.lvl || me.name != H.heroName)) { me.lvl = H.lvl; me.name = H.heroName; Write(g); }
        }
        public static Icon RandomIcon()
        {
            var sh = SHAPES.Keys.ToArray(); var sy = SYMS.Keys.ToArray();
            return new Icon { shape = sh[Random.Range(0, sh.Length)], sym = sy[Random.Range(0, sy.Length)], bg = COLS[Random.Range(0, 6)], fg = COLS[Random.Range(0, COLS.Length)], rim = COLS[Random.Range(0, COLS.Length)] };
        }
        static Color Lighter(Color c, float k) { return new Color(Mathf.Min(1, c.r * k), Mathf.Min(1, c.g * k), Mathf.Min(1, c.b * k), 1); }
        /// guildIconSvg: the field (lightened at the top), its rim, a dark inner line, the symbol glowing faintly
        public static void Paint(UnityEngine.UIElements.Painter2D p, Icon ic, Vector2 o, float size)
        {
            ic = ic ?? new Icon(); string sh, sy; if (!SHAPES.TryGetValue(ic.shape ?? "", out sh)) sh = SHAPES["shield"]; if (!SYMS.TryGetValue(ic.sym ?? "", out sy)) sy = SYMS["sword"];
            float k = size / 64f; Color bg = AldaraRules.Hex(ic.bg ?? "#12304a"), fg = AldaraRules.Hex(ic.fg ?? "#e8c46a"), rim = AldaraRules.Hex(ic.rim ?? "#e8c46a");
            AldaraSvg.Draw(p, sh, 64, o, k, Color.Lerp(Lighter(bg, 1.45f), bg, 0.5f), rim, 3);
            AldaraSvg.Draw(p, sh, 64, o + new Vector2(32, 32) * k * 0.14f, k * 0.86f, null, new Color(0, 0, 0, 0.35f), 1 / 0.86f);
            var gl = fg; gl.a = 0.35f; AldaraSvg.Draw(p, sy, 64, o, k, null, gl, 5.5f);
            AldaraSvg.Draw(p, sy, 64, o, k, null, fg, 3.2f);
        }
    }

    // the Guild Tavern (fortEnter / fortUpdate / fortLeave): built on the dungeon engine with no monsters
    public static class AldaraFort
    {
        public static bool On; static float stairCd; public static int floor;
        public static void Enter()
        {
            var g = AldaraGuild.Mine(); if (g == null) { AldaraHud.Banner("Only guild members can enter the guild tavern"); return; }
            var H = AldaraHero.I; if (!AldaraSave.Ready || !H.alive) return; if (AldaraDungeon.Active) { AldaraHud.Banner(On ? "You are already in the tavern" : "Finish the dungeon first"); return; }
            if (AldaraWaystones.I) AldaraWaystones.I.FadeNow();
            if (AldaraWindows.I) AldaraWindows.I.Toggle("guild", false);
            AldaraDungeon.StartFort(g.name + " Tavern", g.icon != null ? g.icon.rim : "#e8c46a");
        }
        public static void OnEnter(string name) { On = true; floor = 0; stairCd = 1; AldaraHud.Banner("Welcome to " + name); }
        public static void Leave() { if (!On) return; if (AldaraWaystones.I) AldaraWaystones.I.FadeNow(); AldaraDungeon.Exit("fort"); AldaraHud.Banner("You step out of the guild tavern"); }
        public static void OnExit() { On = false; }
        public static void Tick(float dt)
        {
            if (!On) return; var D = AldaraDungeon.DM; var P = AldaraPlayer.I; stairCd -= dt;
            AldaraDungeon.Prop up = null, down = null, door = null;
            foreach (var pr in D.props) { if (pr.type == "fstairs") { if (pr.x < 1700) up = pr; else down = pr; } if (pr.type == "fdoor") door = pr; }
            if (stairCd <= 0 && up != null && down != null && Mathf.Abs(P.x - up.x) < 60 && Mathf.Abs(P.y - (up.y + 10)) < 50) { stairCd = 1.2f; Fade(); P.x = down.x + 90; P.y = down.y + 30; AldaraHero.I.target = null; floor = 1; AldaraHud.Banner(D.names[1]); }
            else if (stairCd <= 0 && up != null && down != null && Mathf.Abs(P.x - down.x) < 60 && Mathf.Abs(P.y - (down.y + 10)) < 50) { stairCd = 1.2f; Fade(); P.x = up.x - 90; P.y = up.y + 40; AldaraHero.I.target = null; floor = 0; AldaraHud.Banner(D.names[0]); }
            if (door != null && stairCd <= 0 && P.y > door.y - 30 && Mathf.Abs(P.x - door.x) < 80) Leave();
        }
        static void Fade() { if (AldaraWaystones.I) AldaraWaystones.I.FadeNow(); }
        public static string Status { get { var D = AldaraDungeon.DM; return (D != null && floor < D.names.Length ? D.names[floor] : "") + "  ·  stairs in the corner, the door leads out"; } }
    }
}
