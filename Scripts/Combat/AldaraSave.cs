using System.Linq;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // Characters are saved in exactly the browser game's save format (one JSON per slot), so a browser character can be
    // imported and keeps every field, including ones the Unity port does not use yet (quests, dungeons, titles, the
    // skill tree, guild...). Unity keeps its own copies beside the browser's: %APPDATA%/RealmOfAldara/saves_unity.
    public static class AldaraSave
    {
        public static int slot = -1;
        public static bool Ready { get { return slot >= 0; } }
        static JObject raw; static float dirtyAt = -1;
        public static JObject Raw { get { return raw; } }
        public static string BrowserDir { get { return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "RealmOfAldara", "saves"); } }
        public static string UnityDir { get { return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "RealmOfAldara", "saves_unity"); } }
        public static string PathOf(string dir, int s) { return Path.Combine(dir, "saves_slot" + s + ".json"); }

        public class Summary { public int slot; public string name, cls; public int lvl; public bool browser; }
        public static List<Summary> List(string dir, bool browser)
        {
            var o = new List<Summary>();
            for (int s = 0; s < 3; s++)
            {
                var p = PathOf(dir, s); if (!File.Exists(p)) continue;
                try { var j = JObject.Parse(File.ReadAllText(p)); o.Add(new Summary { slot = s, name = (string)j["name"] ?? "?", cls = (string)j["cls"] ?? "knight", lvl = (int?)j["lvl"] ?? 1, browser = browser }); } catch { }
            }
            return o;
        }
        public static void Import(int s)
        {
            Directory.CreateDirectory(UnityDir); File.Copy(PathOf(BrowserDir, s), PathOf(UnityDir, s), true);
        }
        public static void Dirty() { if (Ready && dirtyAt < 0) dirtyAt = Time.unscaledTime; AldaraWindows.Refresh(); }
        public static void Tick() { if (dirtyAt >= 0 && Time.unscaledTime - dirtyAt > 2f) { dirtyAt = -1; Save(); } }

        static float F(JToken t, float d = 0) { return t == null || t.Type == JTokenType.Null ? d : (float)t; }
        public static Item ItemOf(JToken t) { if (t == null || t.Type != JTokenType.Object) return null; var it = t.ToObject<Item>(); it.raw = (JObject)t; return it; }
        public static JObject ItemJ(Item it) { var o = Clean(JObject.FromObject(it)); if (it.raw == null) return o; var m = (JObject)it.raw.DeepClone(); foreach (var kv in o) m[kv.Key] = kv.Value; return m; }

        public static void NewCharacter(int s, string name, string cls)
        {
            raw = new JObject(); slot = s; var H = AldaraHero.I; var P = AldaraPlayer.I;
            H.heroName = name; ApplyClass(cls); H.lvl = 1; H.xp = 0; H.gold = 0; H.statPoints = 0; H.equip.Clear(); H.inventory.Clear(); H.vialHp = 0; H.vialMp = 0;
            H.SetClass(cls); P.x = AldaraWorld.TOWN_SPAWN.x; P.y = AldaraWorld.TOWN_SPAWN.y;
            Save();
        }
        public static void Load(int s)
        {
            var j = JObject.Parse(File.ReadAllText(PathOf(UnityDir, s))); raw = j; slot = s;
            var H = AldaraHero.I; var P = AldaraPlayer.I;
            string cls = (string)j["cls"] ?? "knight"; ApplyClass(cls);
            H.heroName = (string)j["name"] ?? "Adventurer";
            H.lvl = (int)F(j["lvl"], 1); H.xp = F(j["xp"]); H.xpNeed = F(j["xpNeed"], AldaraRules.XpNeedFor(H.lvl)); H.gold = F(j["gold"]);
            H.baseAtk = F(j["baseAtk"], H.baseAtk); H.baseHp = F(j["baseHp"], H.baseHp);
            H.str = (int)F(j["str"], H.str); H.agi = (int)F(j["agi"], H.agi); H.vit = (int)F(j["vit"], H.vit); H.ene = (int)F(j["ene"], H.ene); H.statPoints = (int)F(j["statPoints"]);
            H.equip.Clear(); if (j["equip"] is JObject eq) foreach (var kv in eq) { var it = ItemOf(kv.Value); if (it != null) H.equip[kv.Key] = it; }
            H.inventory.Clear(); if (j["inventory"] is JArray inv) foreach (var t in inv) { var it = ItemOf(t); if (it != null) H.inventory.Add(it); }
            AldaraItems.itemSeq = (int)F(j["itemSeq"], 1000);
            if (j["potions"] is JObject po) { H.vialHp = (int)F(po["hp"]); H.vialMp = (int)F(po["mp"]); }
            if (j["stats"] is JObject st) { H.kills = (int)F(st["kills"]); H.bossKills = (int)F(st["bosses"]); H.deaths = (int)F(st["deaths"]); }
            H.title = (string)j["title"]; H.sub = (string)j["sub"]; AldaraGear.VaultLoad();
            var S = AldaraSkills.I; S.Load(cls);
            if (j["skills"] is JObject sk)
            {
                S.owned.Clear(); S.equipped.Clear();
                if (sk["owned"] is JArray ow) foreach (var t in ow) if (S.Get((string)t) != null) S.owned.Add((string)t);
                if (sk["equipped"] is JArray eqd) foreach (var t in eqd) if (S.Get((string)t) != null && S.equipped.Count < 10) S.equipped.Add((string)t);
                if (S.owned.Count == 0 && S.classSkills.Count > 0) { S.owned.Add(S.classSkills[0].id); S.equipped.Add(S.classSkills[0].id); }
            }
            H.maxHp = 0; H.maxMana = 0; H.Recompute(); H.hp = H.maxHp; H.mana = H.maxMana;
            P.x = F(j["x"], AldaraWorld.TOWN_SPAWN.x); P.y = F(j["y"], AldaraWorld.TOWN_SPAWN.y);
            if (AldaraWorld.BlockedAt(P.x, P.y)) { var f = AldaraPlayer.FreeSpotNear(P.x, P.y); P.x = f.x; P.y = f.y; }
        }
        public static void Save()
        {
            if (!Ready) return; var H = AldaraHero.I; var P = AldaraPlayer.I; if (!H || !P) return;
            var j = raw ?? new JObject();
            j["name"] = H.heroName; j["cls"] = H.cls;
            if (j["x"] == null || Mathf.Abs((float)j["x"] - P.x) > 0.01f || Mathf.Abs((float)j["y"] - P.y) > 0.01f) { j["x"] = P.x; j["y"] = P.y; } j["lvl"] = H.lvl; j["xp"] = H.xp; j["xpNeed"] = H.xpNeed; j["gold"] = H.gold;
            j["baseAtk"] = H.baseAtk; j["baseHp"] = H.baseHp; j["str"] = H.str; j["agi"] = H.agi; j["vit"] = H.vit; j["ene"] = H.ene; j["statPoints"] = H.statPoints;
            var eq = new JObject(); foreach (var sl in new[] { "helmet", "chest", "gauntlets", "leggings", "boots", "weapon", "back", "wings", "accessory", "pet", "relic1", "relic2" }) { var it = H.Eq(sl); eq[sl] = it == null ? null : ItemJ(it); }
            j["equip"] = eq;
            var inv = new JArray(); foreach (var it in H.inventory) inv.Add(ItemJ(it)); j["inventory"] = inv;
            j["itemSeq"] = AldaraItems.itemSeq;
            var S = AldaraSkills.I; var sk = (j["skills"] as JObject) ?? new JObject();
            // keep skill ids the port does not know yet (the browser's skill tree), in their places
            var own = new JArray(S.owned.ToArray()); if (sk["owned"] is JArray o0) foreach (var t in o0) if (S.Get((string)t) == null && !own.Any(q => (string)q == (string)t)) own.Add(t);
            var eqd = new JArray(); var mine = new Queue<string>(S.equipped);
            if (sk["equipped"] is JArray e0) foreach (var t in e0) { if (S.Get((string)t) == null) eqd.Add(t); else if (mine.Count > 0) eqd.Add(mine.Dequeue()); }
            while (mine.Count > 0) eqd.Add(mine.Dequeue());
            sk["owned"] = own; sk["equipped"] = eqd; j["skills"] = sk;
            j["potions"] = new JObject { ["hp"] = H.vialHp, ["mp"] = H.vialMp };
            var st = (j["stats"] as JObject) ?? new JObject(); st["kills"] = H.kills; st["bosses"] = H.bossKills; st["deaths"] = H.deaths; j["stats"] = st; j["title"] = H.title;
            Ints(j); raw = j; Directory.CreateDirectory(UnityDir); var p = PathOf(UnityDir, slot);
            File.WriteAllText(p + ".tmp", j.ToString(Newtonsoft.Json.Formatting.None)); if (File.Exists(p)) File.Delete(p); File.Move(p + ".tmp", p);
        }
        // whole numbers are written as integers, as JavaScript does
        static void Ints(JToken t)
        {
            if (t is JObject o) { foreach (var p in new List<JProperty>(o.Properties())) { if (p.Value.Type == JTokenType.Float) { double d = (double)p.Value; if (d == System.Math.Floor(d) && System.Math.Abs(d) < 9e15) p.Value = new JValue((long)d); } else Ints(p.Value); } }
            else if (t is JArray a) for (int i = 0; i < a.Count; i++) { if (a[i].Type == JTokenType.Float) { double d = (double)a[i]; if (d == System.Math.Floor(d) && System.Math.Abs(d) < 9e15) a[i] = new JValue((long)d); } else Ints(a[i]); }
        }
        // the browser leaves cls / myth / agi out when unused
        static JObject Clean(JObject o) { foreach (var k in new[] { "cls", "myth", "relic" }) if (o[k] == null || o[k].Type == JTokenType.Null || (string)o[k] == "") o.Remove(k); if (o["agi"] != null && (float)o["agi"] == 0) o.Remove("agi"); return o; }

        /// swap the hero's body to the class
        public static void ApplyClass(string cls)
        {
            var H = AldaraHero.I; var P = AldaraPlayer.I; H.cls = cls;
            var pf = Resources.Load<GameObject>("Heroes/Hero_" + cls); if (!pf) return;
            if (P.model) Object.Destroy(P.model.gameObject);
            var g = Object.Instantiate(pf, P.transform, false); P.model = g.transform; P.anim = g.GetComponent<AldaraCharacterAnimator>();
        }
    }
}
