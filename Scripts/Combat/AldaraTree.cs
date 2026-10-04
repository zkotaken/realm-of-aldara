using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // The skill trees and subclasses from the browser (treeFor, treeEffects, treeSync, treeUnlock, treeRespec, SUBCLASSES,
    // subNodes): the tree layouts, node costs and levels and the subclass path trees were exported from the browser as
    // they are built at run time. TE is the browser's table of passive totals; recompute and combat read it.
    public static class AldaraTree
    {
        public class Node { public string id, kind, col, bname, name, desc, glyph; public int cost, lvl, tier, branch; public string[] req; public bool anyReq; public float x, y; public Dictionary<string, float> eff; }
        public class Branch { public string name, col; }
        public class Tree { public float W, H; public string root, apex; public Branch[] branches; public Node[] nodes; [JsonIgnore] public Dictionary<string, Node> byId; }
        public class SubNode { public string id, key, n, d, skillId, pass; public int tier, col, cost, lvl; public bool root; public Dictionary<string, float> eff, mod; public JObject sk; }
        public class Sub { public string id, name, col, tag, effTxt, trait; public Dictionary<string, float> eff; public JObject skill; public SubNode[] nodes; }
        public class Book { public Dictionary<string, Tree> trees; public float[][] stars; public Dictionary<string, Sub[]> subs; public int SUB_LEVEL, SUB_CD_X, MAX_EQUIPPED; }
        static Book book;
        public static Book B { get { if (book == null) { book = JsonConvert.DeserializeObject<Book>(Resources.Load<TextAsset>("tree").text); foreach (var t in book.trees.Values) { t.byId = new Dictionary<string, Node>(); foreach (var n in t.nodes) t.byId[n.id] = n; } } return book; } }
        static AldaraHero H { get { return AldaraHero.I; } }

        // ---- state (saved as tree.unlocked, sp, spTotal, sub, subTree, subPaid) ----
        public static List<string> unlocked = new List<string>(), subTree = new List<string>();
        public static int sp, spTotal; public static string sub; public static Dictionary<string, int> subPaid = new Dictionary<string, int>();
        public static readonly string[] TE_KEYS = { "atk", "hp", "mana", "cdr", "crit", "spd", "ls", "dmg", "block", "mreg", "xp", "gold", "mcost" };
        public static Dictionary<string, float> TE = Zero();
        static Dictionary<string, float> Zero() { var e = new Dictionary<string, float>(); foreach (var k in TE_KEYS) e[k] = 0; return e; }
        public static float T(string k) { float v; return TE.TryGetValue(k, out v) ? v : 0; }

        public static Tree For(string cls) { Tree t; return B.trees.TryGetValue(cls, out t) ? t : B.trees["knight"]; }
        public static Node NodeOf(string id) { Node n; return For(H.cls).byId.TryGetValue(id, out n) ? n : null; }
        public static bool Unlocked(string id) { return unlocked.Contains(id); }
        public static bool CanUnlock(Node n)
        {
            if (Unlocked(n.id)) return false; if (n.lvl > 0 && H.lvl < n.lvl) return false; if (n.req.Length == 0) return true;
            return n.anyReq ? n.req.Any(Unlocked) : n.req.All(Unlocked);
        }
        public static int Spent() { int s = 0; foreach (var id in unlocked) { var n = NodeOf(id); if (n != null) s += n.cost; } return s; }
        public static string NodeName(Node n) { if (n.kind == "active") { var sk = AldaraSkills.I ? AldaraSkills.I.Get(n.id) : null; return sk != null ? sk.name : n.id; } return n.name; }

        // ---- subclasses ----
        public static Sub[] SubList() { Sub[] l; return B.subs.TryGetValue(H.cls, out l) ? l : new Sub[0]; }
        public static Sub SubCur() { if (sub == null) return null; foreach (var s in SubList()) if (s.id == sub) return s; return null; }
        public static bool SubHas(string id) { return subTree.Contains(id); }
        static readonly int[] SUBT_OLDCOST = { 0, 2, 4, 3, 5, 3, 8 };
        public static int SubCost() { return Mathf.RoundToInt(3000 + H.lvl * 600); }
        public static bool SubNodeReady(Sub S, SubNode n) { if (n.root) return true; return S.nodes.Any(x => x.tier == n.tier - 1 && (x.root || SubHas(x.id))); }
        /// the signature skill's cooldown: its own (or its base skill's) times SUB_CD_X
        public static float SubSkillCd(Sub S)
        {
            float cd = S.skill["over"]?["cd"] != null ? (float)S.skill["over"]["cd"] : 0;
            if (cd <= 0) { var b = AldaraSkills.I.Get((string)S.skill["base"]); cd = b != null ? b.cd : 0; }
            return Mathf.Round(cd * B.SUB_CD_X);
        }
        static int SubRefund()
        {
            var S = SubCur(); int back = 0;
            if (S != null) foreach (var n in S.nodes) if (!n.root && SubHas(n.id)) { int pd; back += subPaid.TryGetValue(n.id, out pd) ? pd : SUBT_OLDCOST[n.tier]; }
            var ids = S != null ? S.nodes.Where(n => n.sk != null).Select(n => n.skillId).ToList() : new List<string>();
            AldaraSkills.I.equipped.RemoveAll(ids.Contains); AldaraSkills.I.owned.RemoveAll(ids.Contains);
            subTree.Clear(); subPaid.Clear(); sp += back; return back;
        }
        public static void SubChoose(string id)
        {
            var S = SubList().FirstOrDefault(s => s.id == id); if (S == null) return;
            if (H.lvl < B.SUB_LEVEL) { AldaraHud.Banner("Subclasses open at level " + B.SUB_LEVEL); return; }
            if (sub == id) return;
            if (sub != null) { int c = SubCost(); if (H.gold < c) { AldaraHud.Banner("Changing your path costs " + c.ToString("N0") + " gold"); return; } H.gold -= c; }
            int back = sub != null ? SubRefund() : 0; if (back > 0) AldaraHud.Banner(back + " skill points refunded");
            var old = SubCur(); if (old != null) { string oid = (string)old.skill["id"]; AldaraSkills.I.equipped.Remove(oid); AldaraSkills.I.owned.Remove(oid); }
            sub = id; subTree.Clear(); AldaraSubclass.Reset(); Sync(); StripSubFromBar();
            AldaraHud.Banner("You are now a " + S.name); AldaraChat.Sys(H.heroName + " became a " + S.name, "note"); AldaraSave.Dirty();
            var P = AldaraPlayer.I; AldaraVfx.Burst(P.x, P.y - 20, AldaraRules.Hex(S.col), 40, 240); AldaraVfx.Mfx(new AldaraVfx.Mf { k = "pillar", x = P.x, y = P.y, r = 30, c = AldaraRules.Hex(S.col), c2 = Color.white, H = 420, T = 1 });
        }
        public static void SubUnlock(string id)
        {
            var S = SubCur(); if (S == null) return; var n = S.nodes.FirstOrDefault(x => x.id == id); if (n == null || n.root || SubHas(id)) return;
            if (H.lvl < n.lvl) { AldaraHud.Banner("Reach level " + n.lvl + " first"); return; }
            if (!SubNodeReady(S, n)) { AldaraHud.Banner("Unlock the step before it first"); return; }
            if (sp < n.cost) { AldaraHud.Banner("Not enough skill points (" + n.cost + " needed)"); return; }
            sp -= n.cost; subTree.Add(id); subPaid[id] = n.cost; Sync(); StripSubFromBar();
            AldaraHud.Banner("Learned " + n.n); AldaraSave.Dirty(); var P = AldaraPlayer.I; AldaraVfx.Burst(P.x, P.y - 20, AldaraRules.Hex(S.col), 24, 200);
        }
        /// subclass abilities live on their own bar, never on the hotbar
        static void StripSubFromBar()
        {
            var all = new List<string>(); foreach (var s in SubList()) { all.Add((string)s.skill["id"]); foreach (var n in s.nodes) if (n.skillId != null) all.Add(n.skillId); }
            AldaraSkills.I.equipped.RemoveAll(all.Contains); if (all.Contains(AldaraSkills.I.queued)) AldaraSkills.I.queued = null;
        }

        /// treeEffects with the subclass layers: tree passives, the subclass bonuses and its path tree nodes (from level 30)
        public static void Effects()
        {
            var e = Zero(); var T0 = For(H.cls);
            foreach (var id in unlocked) { Node n; if (T0.byId.TryGetValue(id, out n) && n.eff != null) foreach (var kv in n.eff) e[kv.Key] = (e.ContainsKey(kv.Key) ? e[kv.Key] : 0) + kv.Value; }
            var S = SubCur();
            if (S != null && H.lvl >= B.SUB_LEVEL)
            {
                if (S.eff != null) foreach (var kv in S.eff) e[kv.Key] = (e.ContainsKey(kv.Key) ? e[kv.Key] : 0) + kv.Value;
                foreach (var n in S.nodes) if (!n.root && SubHas(n.id) && n.eff != null) foreach (var kv in n.eff) e[kv.Key] = (e.ContainsKey(kv.Key) ? e[kv.Key] : 0) + kv.Value;
            }
            TE = e;
        }
        /// treeInit: read the save, repair it the way the browser does, then sync
        public static void Init(JObject raw)
        {
            var T0 = For(H.cls); unlocked.Clear(); subTree.Clear();
            sub = raw != null ? (string)raw["sub"] : null; if (sub != null && SubCur() == null) sub = null;
            if (raw != null && raw["subTree"] is JArray st) foreach (var t in st) subTree.Add((string)t);
            subPaid.Clear(); if (raw != null && raw["subPaid"] is JObject sp0) foreach (var kv in sp0) subPaid[kv.Key] = (int)kv.Value;
            if (raw != null && raw["tree"] is JObject tr && tr["unlocked"] is JArray ua) { foreach (var t in ua) unlocked.Add((string)t); sp = raw["sp"] == null ? 0 : (int)raw["sp"]; spTotal = raw["spTotal"] == null ? 0 : (int)raw["spTotal"]; }
            else
            {   // older saves: what was bought stays learned; the points for your levels are yours to spend
                foreach (var id in AldaraSkills.I.owned) if (T0.byId.ContainsKey(id)) unlocked.Add(id);
                if (!unlocked.Contains(T0.root)) unlocked.Insert(0, T0.root);
                sp = (H.lvl - 1) + 1; spTotal = sp + unlocked.Sum(id => T0.byId[id].cost);
            }
            unlocked = unlocked.Where(id => T0.byId.ContainsKey(id)).ToList(); if (!unlocked.Contains(T0.root)) unlocked.Insert(0, T0.root);
            sp = Mathf.Max(0, sp); spTotal = Mathf.Max(sp + Spent(), spTotal);
            Sync(false);
        }
        /// treeSync: the skills you own are the active nodes you have unlocked; equipped skills you no longer own drop off
        public static void Sync(bool recompute = true)
        {
            var S0 = AldaraSkills.I; var T0 = For(H.cls);
            var keep = S0.owned.Where(id => S0.Get(id) == null).ToList();   // subclass skills the port cannot cast yet stay owned
            S0.owned.Clear(); foreach (var n in T0.nodes) if (n.kind == "active" && Unlocked(n.id)) S0.owned.Add(n.id);
            S0.owned.AddRange(keep);
            var Sc = SubCur(); if (Sc != null) { string sid = (string)Sc.skill["id"]; if (!S0.owned.Contains(sid)) S0.owned.Add(sid); foreach (var n in Sc.nodes) if (n.sk != null && SubHas(n.id) && !S0.owned.Contains(n.skillId)) S0.owned.Add(n.skillId); }
            S0.equipped.RemoveAll(id => !S0.owned.Contains(id));
            Effects(); if (recompute) H.Recompute();
        }
        public static void Unlock(string id)
        {
            var n = NodeOf(id); if (n == null || Unlocked(id)) return;
            if (!CanUnlock(n)) { AldaraHud.Banner(n.lvl > 0 && H.lvl < n.lvl ? "Reach level " + n.lvl + " first" : "Unlock the path to it first"); return; }
            if (sp < n.cost) { AldaraHud.Banner("Not enough skill points (" + n.cost + " needed)"); return; }
            sp -= n.cost; unlocked.Add(id); Sync();
            AldaraHud.Banner("Learned " + NodeName(n));
            var S0 = AldaraSkills.I; if (n.kind == "active" && S0.equipped.Count < B.MAX_EQUIPPED) S0.equipped.Add(id);
            AldaraSave.Dirty();
        }
        public static int RespecCost() { return Mathf.RoundToInt(1500 + H.lvl * 400); }
        public static void Respec()
        {
            int cost = RespecCost(); if (H.gold < cost) { AldaraHud.Banner("Reforging your path costs " + cost + " gold"); return; }
            H.gold -= cost; var T0 = For(H.cls); sp += Spent(); unlocked = new List<string> { T0.root }; Sync();
            AldaraSkills.I.equipped.Clear(); AldaraSkills.I.equipped.Add(T0.root);
            AldaraHud.Banner("Your path is reforged: " + sp + " skill points to spend"); AldaraSave.Dirty();
        }
        public static void GainPoints(int n, string why)
        {
            sp += n; spTotal += n; AldaraHud.Banner("+" + n + " Skill Point" + (n > 1 ? "s" : "") + (why != null ? " (" + why + ")" : "") + ": press K"); AldaraSave.Dirty();
        }
        public static void Write(JObject j)
        {
            j["tree"] = new JObject { ["unlocked"] = new JArray(unlocked.ToArray()) }; j["sp"] = sp; j["spTotal"] = spTotal;
            j["sub"] = sub; j["subTree"] = new JArray(subTree.ToArray()); var pd = new JObject(); foreach (var kv in subPaid) pd[kv.Key] = kv.Value; j["subPaid"] = pd;
        }
    }
}
