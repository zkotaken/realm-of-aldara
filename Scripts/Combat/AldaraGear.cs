using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // Gear rules from the browser that the inventory window needs: titles, relics, dismantling ("worse" items and their
    // scrap value), discarding, the comparison against what you wear, and the vault shared by every character.
    public static class AldaraGear
    {
        // ---------- titles ----------
        public class Title { public string id, name, col, req; public bool glow; public System.Func<bool> ok; }
        static AldaraHero H { get { return AldaraHero.I; } }
        public static int DunClears(string id) { var d = AldaraSave.Raw?["dun"]?["clears"]?[id]; return d == null ? 0 : (int)d; }
        public static int DunClearsTotal() { var c = AldaraSave.Raw?["dun"]?["clears"] as JObject; int n = 0; if (c != null) foreach (var kv in c) n += (int)kv.Value; return n; }
        public static readonly Title[] TITLES = {
            new Title { id = "wanderer", name = "Wanderer", col = "#c8ccd4", req = "Reach level 5", ok = () => H.lvl >= 5 },
            new Title { id = "seasoned", name = "Seasoned Warrior", col = "#9fe07a", req = "Reach level 10", ok = () => H.lvl >= 10 },
            new Title { id = "veteran", name = "Veteran of Aldara", col = "#7ec8ff", req = "Reach level 20", ok = () => H.lvl >= 20 },
            new Title { id = "champion", name = "Champion", col = "#c07aff", req = "Reach level 30", ok = () => H.lvl >= 30 },
            new Title { id = "warlord", name = "Warlord", col = "#ff9a3a", req = "Reach level 40", ok = () => H.lvl >= 40 },
            new Title { id = "legend", name = "Legend of Aldara", col = "#ffd35a", req = "Reach level 50", ok = () => H.lvl >= 50, glow = true },
            new Title { id = "ascended", name = "The Ascended", col = "#ff7ae0", req = "Reach level 60", ok = () => H.lvl >= 60, glow = true },
            new Title { id = "goblinbane", name = "Goblin Bane", col = "#a8e05a", req = "Clear the Goblin Warrens", ok = () => DunClears("warrens") > 0 },
            new Title { id = "bonebreaker", name = "Bonebreaker", col = "#e0e0ee", req = "Clear the Crypt of Bones", ok = () => DunClears("crypt") > 0 },
            new Title { id = "frostbreaker", name = "Frostbreaker", col = "#8fdfff", req = "Clear the Frozen Depths", ok = () => DunClears("frozen") > 0 },
            new Title { id = "forgeborn", name = "Forgeborn", col = "#ff7a2a", req = "Clear the Molten Forge", ok = () => DunClears("forge") > 0 },
            new Title { id = "voidwalker", name = "Voidwalker", col = "#b07aff", req = "Clear the Void Sanctum", ok = () => DunClears("void") > 0, glow = true },
            new Title { id = "conqueror", name = "Dungeon Conqueror", col = "#ffe07a", req = "Clear every dungeon", ok = () => new[] { "warrens", "crypt", "frozen", "forge", "void" }.All(d => DunClears(d) > 0), glow = true },
            new Title { id = "godforged", name = "Godforged", col = "#ff5ad8", req = "Own a complete Mythical armor set", ok = () => AldaraItems.OwnsFullMythSet(), glow = true },
            new Title { id = "relentless", name = "The Relentless", col = "#ff5a5a", req = "Clear dungeons 25 times in total", ok = () => DunClearsTotal() >= 25, glow = true },
        };
        public static Title TitleById(string id) { foreach (var t in TITLES) if (t.id == id) return t; return null; }
        public static int UnlockedTitles() { int n = 0; foreach (var t in TITLES) if (t.ok()) n++; return n; }
        public static void EquipTitle(string id) { var t = TitleById(id); if (id != null && (t == null || !t.ok())) return; H.title = id; AldaraSave.Dirty(); AldaraWindows.Refresh("inv"); }

        // ---------- relics ----------
        public class RelicDef { public string id, name, col, glyph; public float[] v; public bool noDrop; public string[] desc; }
        class RelicBook { public RelicDef[] relics; }
        static Dictionary<string, RelicDef> relics;
        public static readonly string[] RELIC_SLOTS = { "relic1", "relic2" };
        public static readonly Dictionary<string, int> RELIC_T = new Dictionary<string, int> { { "Common", 0 }, { "Rare", 1 }, { "Epic", 2 }, { "Legendary", 3 }, { "Mythical", 4 } };
        public static RelicDef RelicDefOf(Item it)
        {
            if (relics == null) { relics = new Dictionary<string, RelicDef>(); var ta = Resources.Load<TextAsset>("relics"); if (ta) foreach (var r in Newtonsoft.Json.JsonConvert.DeserializeObject<RelicBook>(ta.text).relics) relics[r.id] = r; }
            if (it == null || it.type != "relic" || it.relic == null) return null; RelicDef d; return relics.TryGetValue(it.relic, out d) ? d : null;
        }
        public static int RelicT(Item it) { int t; return it != null && it.rarity != null && RELIC_T.TryGetValue(it.rarity, out t) ? t : 0; }
        public static float RelicVal(Item it) { var d = RelicDefOf(it); return d == null ? 0 : d.v[RelicT(it)]; }
        public static string RelicDesc(Item it) { var d = RelicDefOf(it); return d == null ? "" : d.desc[RelicT(it)]; }
        public static Item RelicWorn(string id) { foreach (var s in RELIC_SLOTS) { var it = H.Eq(s); if (it != null && it.type == "relic" && it.relic == id) return it; } return null; }
        public static float RelicV(string id) { return RelicVal(RelicWorn(id)); }
        public static Texture2D RelicIcon(Item it) { return it == null ? null : Resources.Load<Texture2D>("UI/relics/relic_" + it.relic + "_" + it.rarity); }
        public static void EquipRelic(Item item)
        {
            int idx = H.inventory.IndexOf(item); if (idx < 0) return;
            foreach (var s in RELIC_SLOTS) { var w = H.Eq(s); if (w != null && w.relic == item.relic) { H.equip[s] = item; H.inventory.RemoveAt(idx); H.inventory.Add(w); return; } }
            string slot = RELIC_SLOTS.FirstOrDefault(s => H.Eq(s) == null);
            if (slot == null) { slot = RELIC_SLOTS[0]; H.inventory.Add(H.Eq(slot)); }
            H.equip[slot] = item; H.inventory.Remove(item);
        }

        // ---------- dismantling ----------
        public static bool CanDismantle(Item it) { return it.type != "pet" && it.type != "relic"; }
        public static bool IsWorse(Item it)
        {
            if (!CanDismantle(it)) return false; if (AldaraItems.MythSetOf(it) != null) return false;
            if (!AldaraItems.CanUse(it, H.cls)) return true;
            var cur = H.Eq(it.type); return cur != null && AldaraItems.Score(cur) >= AldaraItems.Score(it);
        }
        public static int ScrapValue(Item it)
        {
            int lvl = it.lvl > 0 ? it.lvl : 8; float mult; switch (it.rarity) { case "Rare": mult = 3; break; case "Epic": mult = 8; break; case "Legendary": mult = 20; break; default: mult = 1; break; }
            return Mathf.RoundToInt((4 + lvl * 2) * mult);
        }
        public static void Dismantle(List<Item> list)
        {
            if (list.Count == 0) return; int gold = 0; var ids = new HashSet<Item>(list);
            foreach (var it in list) gold += ScrapValue(it);
            H.inventory.RemoveAll(it => ids.Contains(it)); H.gold += gold;
            AldaraHud.Banner("Dismantled " + list.Count + " item" + (list.Count > 1 ? "s" : "") + ": +" + gold + " Gold");
            var P = AldaraPlayer.I; AldaraFx.Text(P.x, P.y - 56, "+" + gold + " Gold", AldaraRules.Hex("#e0b64b"));
            AldaraSave.Dirty();
        }
        public static void Drop(Item it) { H.inventory.Remove(it); AldaraSave.Dirty(); }

        // ---------- "vs equipped" ----------
        static readonly string[][] CMP = { new[] { "atk", "ATK" }, new[] { "hp", "HP" }, new[] { "agi", "AGI" }, new[] { "speed", "SPD" } };
        static float Stat(Item it, string k) { if (it == null) return 0; switch (k) { case "atk": return it.atk; case "hp": return it.hp; case "agi": return it.agi; default: return it.speed; } }
        /// rich text for the comparison line, or null (relics, wrong class)
        public static string Compare(Item it)
        {
            if (it == null || it.type == "relic" || !AldaraItems.SLOT_LABEL.ContainsKey(it.type) || !AldaraItems.CanUse(it, H.cls)) return null;
            var cur = H.Eq(it.type);
            const string up = "#7fe08a", dn = "#ff7a6a", nw = "#ffd76a";
            if (it.type == "pet")
            {
                if (cur == null) return AldaraUI.Span("Fills an empty slot", nw);
                int d = Mathf.RoundToInt((AldaraItems.PetPct(it) - AldaraItems.PetPct(cur)) * 100);
                return "vs equipped: " + (d != 0 ? AldaraUI.Span((d > 0 ? "+" : "−") + Mathf.Abs(d) + "% all stats", d > 0 ? up : dn) : "same bonus");
            }
            var parts = new List<string>();
            foreach (var c in CMP) { float d = Stat(it, c[0]) - Stat(cur, c[0]); if (d != 0) parts.Add(AldaraUI.Span((d > 0 ? "+" : "−") + Mathf.Abs(d) + " " + c[1], d > 0 ? up : dn)); }
            if (cur == null) return AldaraUI.Span("Fills an empty slot", nw);
            if (parts.Count == 0) return "Same stats as what you wear";
            return "vs equipped: " + string.Join(", ", parts);
        }

        // ---------- the vault (one store for every character on this machine) ----------
        public const int VAULT_MAX = 200;
        public static List<JObject> vaultItems = new List<JObject>(); public static long vaultGold; static bool vaultLoaded;
        static string VaultPath { get { return Path.Combine(AldaraSave.UnityDir, "vault.json"); } }
        public static void VaultLoad()
        {
            vaultItems.Clear(); vaultGold = 0;
            try
            {
                var p = VaultPath; if (!File.Exists(p)) { var b = Path.Combine(AldaraSave.BrowserDir, "vault.json"); if (File.Exists(b)) { Directory.CreateDirectory(AldaraSave.UnityDir); File.Copy(b, p); } }
                if (File.Exists(p)) { var j = JObject.Parse(File.ReadAllText(p)); var d = (j["data"] as JObject) ?? j; if (d["items"] is JArray a) foreach (var t in a) if (t is JObject o) vaultItems.Add(o); vaultGold = d["gold"] == null ? 0 : (long)d["gold"]; }
            }
            catch (System.Exception e) { Debug.LogException(e); }
            vaultLoaded = true;
        }
        static void VaultWrite()
        {
            Directory.CreateDirectory(AldaraSave.UnityDir);
            var j = new JObject { ["items"] = new JArray(vaultItems), ["gold"] = vaultGold, ["t"] = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            File.WriteAllText(VaultPath, j.ToString(Newtonsoft.Json.Formatting.None));
        }
        public static void VaultStore(Item it)
        {
            if (!vaultLoaded) VaultLoad();
            if (vaultItems.Count >= VAULT_MAX) { AldaraHud.Banner("The vault is full (" + VAULT_MAX + ")"); return; }
            var o = AldaraSave.ItemJ(it); o.Remove("id"); vaultItems.Add(o); VaultWrite();
            H.inventory.Remove(it); AldaraSave.Dirty(); AldaraHud.Banner(it.name + " stored in the vault");
        }
        public static void VaultTake(int idx)
        {
            if (!vaultLoaded) VaultLoad();
            if (H.inventory.Count >= AldaraItems.BACKPACK_MAX) { AldaraHud.Banner("Your backpack is full"); return; }
            if (idx < 0 || idx >= vaultItems.Count) return; var o = vaultItems[idx]; vaultItems.RemoveAt(idx); VaultWrite();
            o["id"] = AldaraItems.itemSeq++; var it = AldaraSave.ItemOf(o); H.inventory.Add(it); AldaraSave.Dirty(); AldaraHud.Banner(it.name + " taken from the vault");
        }
    }
}
