using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // The browser's quest system (QDB, QNPC, qS ...): the story campaign in seven chapters, side quests, the Valcrest
    // quests and bounties from the boards. Quests have several objectives (defeat, collect, clear a region, slay a boss,
    // clear a dungeon, scout a place, gather, speak with someone); several run at once, the tracker shows the ones you
    // follow, the log shows them all. Quest data, where people stand and the gathering spots come from the browser
    // (quests.json); the state is saved in the same "quests" field (v3) so saves move between the two games.
    public static class AldaraQuests
    {
        public class QObj { public string k, t, item, npc, at, label, node, d; public int n, z; }
        public class QRw { public float xp, gold, xpF; public int vials, choice; }
        public class QDef
        {
            public string id, title, text, fin, giver, turn, key; public int main, side, bounty, ch, lvl;
            public string[] pre; public QObj[] obj; public QRw rw;
        }
        public class Npc { public string name, look, title, hello, folk, kd; public int town, board; public int zone = -1; public Topic[] topics; }
        public class Topic { public string t, x; }
        public class NodeDef { public string name, col, kind; public int zone; }
        public class Spot { public float x, y; }
        public class Obj { public float x, y; public string look; public float? face; public int q, board, far, folk; }
        public class Area { public float x, y, r; }
        public class NodePos { public float x, y, ph; }
        public class Inst { public string id; public int[] p; public int tr; public QDef def; public List<Item> choices; }
        public class State { public List<Inst> act = new List<Inst>(); public Dictionary<string, int> done = new Dictionary<string, int>(); public List<QDef> offers = new List<QDef>(); public int bnLvl, seq = 1; public string focus, pendingHint; }
        public class GNode { public string type; public float x, y, ph, resp; public bool alive = true; }

        // ---- the book ----
        public static Dictionary<string, QDef> QDB; public static List<string> QMAIN; public static Dictionary<string, Npc> QNPC; public static Dictionary<string, NodeDef> QNODE;
        public static Dictionary<string, Obj> QOBJ; public static Dictionary<string, Spot> places; public static Dictionary<string, Area> areas; public static Dictionary<string, NodePos[]> nodeSpots;
        public static Dictionary<string, string> BOUNTY_ITEM, FOLK_TAG; public static string[] ZONES; public static Dictionary<string, string> DUN_NAME = new Dictionary<string, string>();
        public static JObject J;
        static bool loaded;
        public static void Load()
        {
            if (loaded) return; loaded = true;
            J = JObject.Parse(Resources.Load<TextAsset>("quests").text);
            QDB = J["QDB"].ToObject<Dictionary<string, QDef>>(); QMAIN = J["QMAIN"].ToObject<List<string>>(); QNPC = J["QNPC"].ToObject<Dictionary<string, Npc>>();
            QNODE = J["QNODE"].ToObject<Dictionary<string, NodeDef>>(); QOBJ = J["QOBJ"].ToObject<Dictionary<string, Obj>>(); places = J["places"].ToObject<Dictionary<string, Spot>>();
            areas = J["areas"].ToObject<Dictionary<string, Area>>(); nodeSpots = J["nodes"].ToObject<Dictionary<string, NodePos[]>>();
            BOUNTY_ITEM = J["BOUNTY_ITEM"].ToObject<Dictionary<string, string>>(); FOLK_TAG = J["FOLK_TAG"].ToObject<Dictionary<string, string>>(); ZONES = J["ZONES"].ToObject<string[]>();
            foreach (var d in J["DUNGEONS"]) DUN_NAME[(string)d["id"]] = (string)d["name"];
            foreach (var kv in QDB) { kv.Value.id = kv.Key; if (kv.Value.turn == null && kv.Value.bounty == 0) kv.Value.turn = kv.Value.giver; }
        }

        // ---- state ----
        public static State S = new State();
        public static readonly List<GNode> NODES = new List<GNode>();
        public static Inst G_inst; public static GNode gather; public static float gatherT; static float gsx, gsy;
        static AldaraHero H { get { return AldaraHero.I; } }
        static AldaraPlayer P { get { return AldaraPlayer.I; } }
        public static event System.Action Changed;
        public static void Render() { if (Changed != null) Changed(); }

        public static QDef Def(Inst i) { return i.def ?? (QDB.TryGetValue(i.id, out QDef d) ? d : null); }
        public static int Need(QObj o) { return o.k == "boss" || o.k == "explore" || o.k == "talk" || o.k == "dungeon" ? 1 : Mathf.Max(1, o.n); }
        public static bool ObjDone(Inst i, int k) { return i.p[k] >= Need(Def(i).obj[k]); }
        public static bool IsReady(Inst i) { var d = Def(i); for (int k = 0; k < d.obj.Length; k++) if (!ObjDone(i, k)) return false; return true; }
        public static Inst InstOf(string id) { return S.act.FirstOrDefault(a => a.id == id); }
        public static bool Done(string id) { return S.done.ContainsKey(id); }
        static bool PreOk(QDef d) { return d.pre == null || d.pre.All(Done); }
        public static bool CanOffer(QDef d) { return !Done(d.id) && InstOf(d.id) == null && PreOk(d); }
        public static bool LevelOk(QDef d) { return d.main != 0 || H.lvl >= Mathf.Max(1, d.lvl) - 3; }
        public static List<QDef> AvailFrom(string npc) { return QDB.Values.Where(d => d.giver == npc && CanOffer(d)).ToList(); }
        public static List<Inst> ReadyFor(string npc) { return S.act.Where(a => { var d = Def(a); return d.bounty == 0 && d.turn == npc && IsReady(a); }).ToList(); }
        public static List<Inst> ActiveFor(string npc) { return S.act.Where(a => { var d = Def(a); return d.bounty == 0 && d.turn == npc && !IsReady(a); }).ToList(); }
        public static float RwXp(QRw r) { if (r == null) return 0; if (r.xpF > 0) return Mathf.Max(2000, Mathf.Round(H.xpNeed * r.xpF)); return r.xp; }
        public static string Zone(int z) { return ZONES != null && z >= 0 && z < ZONES.Length ? ZONES[z] : "the wilds"; }
        public static string Pl(string w)
        {
            if (w.StartsWith("Pinch of ")) return "Pinches" + w.Substring(5); if (w.StartsWith("Handful of ")) return "Handfuls" + w.Substring(7);
            var lw = w.ToLower(); if (lw.EndsWith("dust") || lw.EndsWith("heartwood") || lw.EndsWith("essence")) return w; return w.EndsWith("s") ? w : w + "s";
        }
        public static string ObjText(QObj o, bool shortT = false)
        {
            switch (o.k)
            {
                case "kill": return shortT ? o.t : "Defeat " + (Mathf.Max(1, o.n) > 1 ? Pl(o.t) : o.t);
                case "collect": return "Collect " + (Mathf.Max(1, o.n) > 1 ? Pl(o.item) : o.item) + (shortT ? "" : " from " + Pl(o.t));
                case "zone": return "Defeat monsters in " + Zone(o.z);
                case "boss": return "Slay the " + o.t;
                case "dungeon": return "Clear the " + (DUN_NAME.TryGetValue(o.d ?? "", out string dn) ? dn : o.d) + (shortT ? "" : " (Dungeons, J)");
                case "explore": return "Scout " + (o.label ?? "the area");
                case "gather": return "Gather " + QNODE[o.node].name + (shortT ? "" : " in " + Zone(QNODE[o.node].zone));
                case "talk": return "Speak with " + QNPC[o.npc].name;
            }
            return "";
        }

        public static void Accept(string id, QDef def = null, bool quiet = false)
        {
            if (def == null && (InstOf(id) != null || Done(id))) return;
            var d = def ?? (QDB.TryGetValue(id, out QDef q) ? q : null); if (d == null) return;
            var inst = new Inst { id = id, p = new int[d.obj.Length], tr = 1, def = def };
            for (int i = 0; i < d.obj.Length; i++) if (d.obj[i].k == "dungeon" && AldaraGear.DunClears(d.obj[i].d) > 0) inst.p[i] = 1;
            S.act.Add(inst);
            var tr = S.act.Where(a => a.tr != 0).ToList(); if (tr.Count > 4) { var old = tr.FirstOrDefault(a => Def(a).main == 0 && a != inst); if (old != null) old.tr = 0; }
            if (d.main != 0 || S.focus == null || InstOf(S.focus) == null) S.focus = id;
            if (quiet) return;
            if (d.bounty == 0) AldaraHud.Banner("Quest accepted: " + d.title);
            EnsureNodes(); Render(); AldaraSave.Dirty();
        }
        public static void Abandon(string id)
        {
            int i = S.act.FindIndex(a => a.id == id); if (i < 0 || Def(S.act[i]).main != 0) return; S.act.RemoveAt(i);
            if (S.focus == id) S.focus = S.act.Count > 0 ? S.act[0].id : null; EnsureNodes(); Render(); AldaraSave.Dirty();
        }
        public static List<Item> Choices(Inst inst)
        {
            var d = Def(inst); if (d.rw == null || d.rw.choice == 0) return null;
            if (inst.choices == null)
            {
                int L = Mathf.Max(H.lvl, Mathf.Max(1, d.lvl)); var o = new List<Item>();
                for (int k = 0; k < 3; k++)
                {
                    Item it = null; for (int t = 0; t < 30; t++) { it = AldaraItems.RollItem(L, AldaraItems.PickRarity(L, d.main != 0 ? 45 : 30, true), d.main != 0 ? 5 : 2); if (AldaraItems.CanUse(it, H.cls) && !o.Any(x => x.type == it.type)) break; }
                    o.Add(it);
                }
                inst.choices = o;
            }
            return inst.choices;
        }
        public static bool Complete(Inst inst, int choice = -1)
        {
            var d = Def(inst); var rw = d.rw ?? new QRw();
            if (rw.choice != 0 && d.bounty == 0)
            {
                var ch = Choices(inst); if (choice < 0 || choice >= ch.Count) { AldaraHud.Banner("Choose a reward first"); return false; }
                if (H.inventory.Count >= AldaraItems.BACKPACK_MAX) { AldaraHud.Banner("Your backpack is full"); return false; }
                H.AddLoot(ch[choice]); AldaraWindows.Refresh("inv");
            }
            S.act.Remove(inst);
            if (d.bounty == 0) S.done[inst.id] = 1;
            H.gold += rw.gold;
            if (rw.vials > 0) { H.vialHp = Mathf.Min(AldaraItems.VIAL_MAX, H.vialHp + rw.vials); H.vialMp = Mathf.Min(AldaraItems.VIAL_MAX, H.vialMp + rw.vials); }
            float xp = RwXp(rw); H.GainXp(xp);
            AldaraHud.Banner((d.bounty != 0 ? "Bounty complete: " : "Quest complete: ") + d.title);
            AldaraFx.Text(P.x, P.y - 16 - 58, "+" + xp + " XP  +" + rw.gold + " Gold", AldaraRules.Hex("#e0b64b"));
            try { AldaraFx.Ring(P.x, P.y, 90, AldaraRules.Hex("#ffd35a"), 0.7f); AldaraFx.Disc(P.x, P.y, 60, new Color(1, 0.83f, 0.35f, 0.35f), 1.2f); } catch { }
            if (S.focus == inst.id) S.focus = null;
            var nxt = d.main != 0 ? QMAIN.Select(id => QDB[id]).FirstOrDefault(CanOffer) : null;
            if (S.focus == null) { var m = S.act.FirstOrDefault(a => Def(a).main != 0) ?? S.act.FirstOrDefault(a => a.tr != 0) ?? S.act.FirstOrDefault(); S.focus = m != null ? m.id : null; }
            EnsureNodes(); Render(); AldaraSave.Dirty();
            if (nxt != null && nxt.giver == null) Accept(nxt.id);
            return true;
        }

        // ---- progress from play ----
        public static int MonZone(AldaraMonsters.Mon m)
        {
            var camps = MonsterBook.Load().camps;
            if (m.camp >= 0 && m.camp < camps.Length) return camps[m.camp].z; return m.boss ? m.tier - 1 : -1;
        }
        public static void Bump(Inst inst, int i, int amt, float x = float.NaN, float y = 0, string label = null)
        {
            var d = Def(inst); var o = d.obj[i]; int need = Need(o); bool was = IsReady(inst);
            inst.p[i] = Mathf.Min(need, inst.p[i] + amt);
            if (!float.IsNaN(x)) AldaraFx.Text(x, y, label ?? (ObjText(o, true) + "  " + inst.p[i] + " / " + need), AldaraRules.Hex("#e0b64b"));
            if (!was && IsReady(inst))
            {
                if (d.bounty != 0) Complete(inst);
                else { var t = d.turn != null && QNPC.TryGetValue(d.turn, out Npc n) ? n : null; AldaraHud.Banner(d.title + ": return to " + (t != null ? t.name : "the quest giver")); if (S.focus != inst.id && d.main != 0) S.focus = inst.id; }
            }
            AldaraSave.Dirty();
        }
        public static void OnKill(AldaraMonsters.Mon m)
        {
            if (S.act.Count == 0) return; int z = MonZone(m); bool any = false;
            foreach (var inst in S.act.ToList())
            {
                var d = Def(inst);
                for (int i = 0; i < d.obj.Length; i++)
                {
                    if (!S.act.Contains(inst) || ObjDone(inst, i)) continue; var o = d.obj[i];
                    if ((o.k == "kill" || o.k == "boss") && m.name == o.t) { Bump(inst, i, 1, m.x, m.y - m.r - 74); any = true; }
                    else if (o.k == "zone" && z == o.z) { Bump(inst, i, 1, m.x, m.y - m.r - 74); any = true; }
                    else if (o.k == "collect" && m.name == o.t && Random.value < 0.62f) { Bump(inst, i, 1, m.x, m.y - m.r - 74, "+1 " + o.item + "  " + Mathf.Min(Need(o), inst.p[i] + 1) + " / " + Need(o)); any = true; }
                }
            }
            if (any) Render();
        }
        public static void OnDungeon(string id)
        {
            foreach (var inst in S.act.ToList()) { var d = Def(inst); for (int i = 0; i < d.obj.Length; i++) if (d.obj[i].k == "dungeon" && d.obj[i].d == id && !ObjDone(inst, i)) Bump(inst, i, 1, P.x, P.y - 16 - 90, "Dungeon cleared"); }
            Render();
        }
        public static void TalkTo(string npc)
        {
            bool any = false;
            foreach (var inst in S.act.ToList()) { var d = Def(inst); for (int i = 0; i < d.obj.Length; i++) if (d.obj[i].k == "talk" && d.obj[i].npc == npc && !ObjDone(inst, i)) { Bump(inst, i, 1); any = true; } }
            if (any) Render();
        }

        // ---- gathering ----
        static HashSet<string> NodesNeeded() { var s = new HashSet<string>(); foreach (var inst in S.act) { var d = Def(inst); for (int i = 0; i < d.obj.Length; i++) if (d.obj[i].k == "gather" && !ObjDone(inst, i)) s.Add(d.obj[i].node); } return s; }
        public static void EnsureNodes()
        {
            var need = NodesNeeded();
            NODES.RemoveAll(n => !need.Contains(n.type));
            foreach (var type in need)
            {
                if (NODES.Any(n => n.type == type)) continue;
                if (nodeSpots.TryGetValue(type, out NodePos[] ps)) foreach (var p in ps) NODES.Add(new GNode { type = type, x = p.x, y = p.y, ph = p.ph });
            }
        }
        public static GNode NearestNode(float x, float y, float r, string type = null)
        {
            GNode best = null; float bd = r;
            foreach (var n in NODES) { if (!n.alive || (type != null && n.type != type)) continue; float d = Dist(n.x, n.y, x, y); if (d < bd) { bd = d; best = n; } }
            return best;
        }
        public static void StartGather(GNode n) { if (n == null || !n.alive || gather != null) return; gather = n; gatherT = 0; gsx = P.x; gsy = P.y; }
        static void FinishGather(GNode n)
        {
            n.alive = false; n.resp = 22; var def = QNODE[n.type];
            foreach (var inst in S.act.ToList()) { var d = Def(inst); for (int i = 0; i < d.obj.Length; i++) { var o = d.obj[i]; if (o.k == "gather" && o.node == n.type && !ObjDone(inst, i)) Bump(inst, i, 1, n.x, n.y - 40, "+1 " + def.name + "  " + Mathf.Min(Need(o), inst.p[i] + 1) + " / " + Need(o)); } }
            try { AldaraFx.Ring(n.x, n.y, 24, AldaraRules.Hex(def.col), 0.3f); } catch { }
            EnsureNodes(); Render(); AldaraSave.Dirty();
        }
        public static Area GatherArea(string type) { return areas[type]; }

        // ---- bounties ----
        static QDef BountyGen(List<string> avoid)
        {
            int L = H.lvl; var all = MonsterBook.Load().monsters.Where(t => t.boss == 0).ToList();
            var pool = all.Where(t => t.lv0 <= L + 2 && t.lv1 >= L - 4).ToList();
            if (avoid.Count > 0) { var p2 = pool.Where(t => !avoid.Contains(t.name)).ToList(); if (p2.Count > 0) pool = p2; else { var wide = all.Where(t => t.lv0 <= L + 5 && t.lv1 >= L - 8 && !avoid.Contains(t.name)).ToList(); if (wide.Count > 0) pool = wide; } }
            if (pool.Count == 0) pool = all.OrderBy(a => Mathf.Abs((a.lv0 + a.lv1) / 2f - L)).Take(3).ToList();
            var tt = pool[Random.Range(0, pool.Count)]; int lv = Mathf.Max(tt.lv0, Mathf.Min(tt.lv1, L)); var st = AldaraMonsters.MonStats(tt, lv);
            bool collect = Random.value < 0.4f; int n = collect ? 5 + Random.Range(0, 4) : 8 + Random.Range(0, 7);
            var o = collect ? new QObj { k = "collect", t = tt.name, item = BOUNTY_ITEM.TryGetValue(tt.name, out string bi) ? bi : tt.name + " Trophy", n = n } : new QObj { k = "kill", t = tt.name, n = n };
            string zn = tt.tier - 1 >= 0 && tt.tier - 1 < ZONES.Length ? ZONES[tt.tier - 1] : "the wilds";
            return new QDef
            {
                bounty = 1, lvl = lv, title = "Bounty: " + (collect ? o.item : tt.name), giver = "board", turn = null, obj = new[] { o },
                rw = new QRw { xp = Mathf.Round(st.xp * n * (collect ? 2.4f : 1.7f)), gold = Mathf.Round(st.gold * n * (collect ? 3 : 2.2f)) },
                text = collect ? "Wanted: " + n + " " + Pl(o.item) + " from " + Pl(tt.name) + " in " + zn + ". Paid on delivery." : "Wanted: " + n + " " + Pl(tt.name) + " dead in " + zn + ". The town pays for every one.",
                key = "b" + (S.seq++)
            };
        }
        public static List<QDef> BountyOffers()
        {
            if (Mathf.Abs(S.bnLvl - H.lvl) >= 4) S.offers.Clear();
            S.offers.RemoveAll(o => o == null);
            while (S.offers.Count < 3) S.offers.Add(BountyGen(S.offers.Select(o => o.obj[0].t).Concat(S.act.Where(x => Def(x).bounty != 0).Select(x => Def(x).obj[0].t)).ToList()));
            S.bnLvl = S.offers.Count > 0 && S.bnLvl != 0 && Mathf.Abs(S.bnLvl - H.lvl) < 4 ? S.bnLvl : H.lvl;
            return S.offers;
        }
        public static void TakeBounty(string k)
        {
            int i = S.offers.FindIndex(o => o.key == k); if (i < 0) return;
            if (S.act.Count(a => Def(a).bounty != 0) >= 3) { AldaraHud.Banner("You can hold 3 bounties at once"); return; }
            var def = S.offers[i]; S.offers.RemoveAt(i); Accept(def.key, def); AldaraHud.Banner("Bounty taken: " + def.title.Replace("Bounty: ", ""));
        }

        // ---- save and load (old saves pick up the story in the region they had reached) ----
        static readonly JsonSerializer JS = JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        static JObject DefJ(QDef d) { return JObject.FromObject(d, JS); }
        public static void Init(JObject s)
        {
            Load(); NODES.Clear(); gather = null; S = new State();
            var q = s != null ? s["quests"] as JObject : null;
            if (q != null && (int?)q["v"] == 3)
            {
                if (q["act"] is JArray act) foreach (var a in act)
                    {
                        if (!(a is JObject ao)) continue; string id = (string)ao["id"]; QDef def = ao["def"] is JObject dj ? dj.ToObject<QDef>() : null;
                        if (def == null && (id == null || !QDB.ContainsKey(id))) continue; if (def != null) def.id = id;
                        var d = def ?? QDB[id]; var p = new int[d.obj.Length]; if (ao["p"] is JArray pa) for (int i = 0; i < p.Length && i < pa.Count; i++) p[i] = (int)(float)pa[i];
                        S.act.Add(new Inst { id = id, p = p, tr = (ao["tr"] != null && (float)ao["tr"] != 0) ? 1 : 0, def = def });
                    }
                if (q["done"] is JObject dn) foreach (var kv in dn) S.done[kv.Key] = 1;
                if (q["bn"] is JObject bn)
                {
                    if (bn["offers"] is JArray of) foreach (var o in of) if (o is JObject oj) S.offers.Add(oj.ToObject<QDef>());
                    S.bnLvl = (int?)bn["lvl"] ?? 0; S.seq = (int?)bn["seq"] ?? 1;
                }
                S.focus = (string)q["focus"]; S.pendingHint = (string)q["pendingHint"];
            }
            else
            {
                var old = s != null ? s["quest"] as JObject : null; int idx = old != null ? ((int?)old["idx"] ?? 0) : 0;
                int[] bounds = { 5, 11, 18, 26, 34, 42, 51 }; int c = System.Array.FindIndex(bounds, b => idx < b); if (c < 0) c = 7;
                int lvl = s != null ? ((int?)s["lvl"] ?? 1) : 1;
                bool fresh = old == null || (idx == 0 && !(((float?)old["count"] ?? 0) > 0) && lvl <= 1);
                if (!fresh)
                {
                    foreach (var id in QMAIN) if (QDB[id].ch < c + 1) S.done[id] = 1; S.done["m0"] = 1;
                    foreach (var d in QDB.Values) if (d.side != 0 && (d.pre == null || d.pre.All(p => S.done.ContainsKey(p))) && Mathf.Max(1, d.lvl) < H.lvl - 8) S.done[d.id] = 1;
                }
                var first = QMAIN.Select(id => QDB[id]).FirstOrDefault(d => !S.done.ContainsKey(d.id));
                if (first != null && (first.giver == null || fresh)) Accept(first.id, null, true); else if (first != null && first.id == "m1") Accept("m1", null, true);
                else if (first != null) { var g = first.giver != null && QNPC.TryGetValue(first.giver, out Npc n) ? n : null; S.pendingHint = "Next: speak with " + (g != null ? g.name : "your guide") + " (" + first.title + ")"; }
            }
            EnsureNodes(); Render();
        }
        public static void Write(JObject j)
        {
            var act = new JArray();
            foreach (var a in S.act) { var o = new JObject { ["id"] = a.id, ["p"] = new JArray(a.p), ["tr"] = a.tr }; if (a.def != null) o["def"] = DefJ(a.def); act.Add(o); }
            var done = new JObject(); foreach (var kv in S.done) done[kv.Key] = 1;
            var offers = new JArray(); foreach (var o in S.offers) offers.Add(DefJ(o));
            var q = new JObject { ["v"] = 3, ["act"] = act, ["done"] = done, ["bn"] = new JObject { ["offers"] = offers, ["lvl"] = S.bnLvl, ["seq"] = S.seq }, ["focus"] = S.focus };
            if (S.pendingHint != null) q["pendingHint"] = S.pendingHint;
            j["quests"] = q; j["qv"] = 2;
        }

        // ---- what to chase next ----
        public static Inst FocusInst() { return (S.focus != null ? InstOf(S.focus) : null) ?? S.act.FirstOrDefault(a => Def(a).main != 0) ?? S.act.FirstOrDefault(); }
        public static QDef NextMain() { return QMAIN.Select(id => QDB[id]).FirstOrDefault(CanOffer); }
        public class Way { public float x, y; public string label, npc; public GNode node; public bool explore, hunt; public AldaraMonsters.Mon mon; }
        public static Obj NpcPos(string id) { return QOBJ.TryGetValue(id, out Obj o) ? o : null; }
        public static Spot Place(string at) { return at != null && places.TryGetValue(at, out Spot p) ? p : null; }
        static float Dist(float ax, float ay, float bx, float by) { return Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)); }
        public static Way Waypoint(Inst inst = null)
        {
            inst = inst ?? FocusInst();
            if (inst == null) { var nx = NextMain(); if (nx != null && nx.giver != null) { var n = AldaraFolk.Pos(nx.giver); if (n.HasValue) return new Way { x = n.Value.x, y = n.Value.y, label = QNPC[nx.giver].name, npc = nx.giver }; } return null; }
            var d = Def(inst);
            if (IsReady(inst)) { if (d.turn == null) return null; var n = AldaraFolk.Pos(d.turn); return n.HasValue ? new Way { x = n.Value.x, y = n.Value.y, label = QNPC[d.turn].name, npc = d.turn } : null; }
            int i = 0; while (i < d.obj.Length && ObjDone(inst, i)) i++; if (i >= d.obj.Length) return null; var o = d.obj[i];
            switch (o.k)
            {
                case "talk": { var n = AldaraFolk.Pos(o.npc); return n.HasValue ? new Way { x = n.Value.x, y = n.Value.y, label = QNPC[o.npc].name, npc = o.npc } : null; }
                case "explore": { var p = Place(o.at); return p != null ? new Way { x = p.x, y = p.y, label = o.label, explore = true } : null; }
                case "gather": { var n = NearestNode(P.x, P.y, 1e9f, o.node); if (n != null) return new Way { x = n.x, y = n.y, label = QNODE[o.node].name, node = n }; var A = GatherArea(o.node); return new Way { x = A.x, y = A.y, label = QNODE[o.node].name }; }
                case "dungeon": return null;
                case "zone":
                    {
                        MonsterBook.Load(); CampDef best = null; float bd = float.MaxValue;
                        foreach (var c in MonsterBook.Load().camps) { if (c.z != o.z) continue; float dd = Dist(c.x, c.y, P.x, P.y); if (dd < bd) { bd = dd; best = c; } }
                        return best != null ? new Way { x = best.x, y = best.y, label = Zone(o.z), hunt = true } : null;
                    }
                default:
                    {
                        AldaraMonsters.Mon best = null; float bd = float.MaxValue;
                        foreach (var m in AldaraMonsters.I.all) { if (m.dead || m.name != o.t) continue; float dd = Dist(m.x, m.y, P.x, P.y); if (dd < bd) { bd = dd; best = m; } }
                        if (best != null && bd < 900) return new Way { x = best.x, y = best.y, label = o.t, hunt = true, mon = best };
                        AldaraMonsters.Mon home = null; bd = float.MaxValue;
                        foreach (var m in AldaraMonsters.I.all) { if (m.name != o.t) continue; float dd = Dist(m.homeX, m.homeY, P.x, P.y); if (dd < bd) { bd = dd; home = m; } }
                        return home != null ? new Way { x = home.homeX, y = home.homeY, label = o.t, hunt = true } : null;
                    }
            }
        }
        /// "!" (has work), "?" (ready to turn in), "!low" / "?low" (grey) over a quest giver
        public static string Marker(string id)
        {
            if (!QNPC.TryGetValue(id, out Npc N)) return null;
            if (N.board != 0) return S.act.Count(a => Def(a).bounty != 0) < 3 ? "!" : null;
            if (ReadyFor(id).Count > 0) return "?";
            var av = AvailFrom(id); if (av.Any(LevelOk)) return "!"; if (av.Count > 0) return "!low";
            if (ActiveFor(id).Count > 0) return "?low"; return null;
        }
        public static List<Inst> TrackedList()
        {
            var foc = FocusInst();
            System.Func<Inst, int> rank = a => a == foc ? 0 : Def(a).main != 0 ? 1 : Def(a).bounty != 0 ? 3 : 2;
            return S.act.Where(a => a.tr != 0 || a == foc).OrderBy(rank).Take(4).ToList();
        }

        // ---- per frame ----
        static float exploreT;
        public static void Update(float dt)
        {
            if (!AldaraSave.Ready || P == null) return;
            exploreT -= dt;
            if (exploreT <= 0)
            {
                exploreT = 0.25f;
                foreach (var inst in S.act.ToList())
                {
                    var d = Def(inst);
                    for (int i = 0; i < d.obj.Length; i++)
                    {
                        var o = d.obj[i]; if (o.k != "explore" || ObjDone(inst, i)) continue; var p = Place(o.at);
                        if (p != null && Dist(p.x, p.y, P.x, P.y) < 190) { Bump(inst, i, 1, P.x, P.y - 16 - 70, "Discovered: " + o.label); Render(); }
                    }
                }
            }
            foreach (var n in NODES) if (!n.alive) { n.resp -= dt; if (n.resp <= 0) n.alive = true; }
            if (gather != null)
            {
                if (!H.alive || Dist(P.x, P.y, gsx, gsy) > 8 || !gather.alive) gather = null;
                else { gatherT += dt; P.facing = Mathf.Atan2(gather.y - P.y, gather.x - P.x); if (gatherT >= 1.3f) { var n = gather; gather = null; FinishGather(n); } }
            }
        }
    }
}
