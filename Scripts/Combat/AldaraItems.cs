using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // Items, exactly as the browser makes them (rollItem, buildItem, pickRarity, rarityWeights, petChance, mythChance,
    // rollMythicArmor, wings, itemScore). Fields match the browser save format so characters carry over unchanged.
    [System.Serializable] public class Item
    {
        public int id; public string type, name, rarity, color, cls, myth, relic; public float atk, hp, speed, agi; public int lvl;
        [System.NonSerialized, Newtonsoft.Json.JsonIgnore] public Newtonsoft.Json.Linq.JObject raw;   // every field the browser stored, kept on save
        [Newtonsoft.Json.JsonIgnore] public Color Col { get { return AldaraRules.Hex(string.IsNullOrEmpty(color) ? "#b8b8c8" : color); } }
    }
    [System.Serializable] class SlotNames { public string type; public string[] names; }
    [System.Serializable] class WeaponNames { public string cls; public string[] names; }
    [System.Serializable] public class MythicSet { public string id, name, glow; public string[] pieces; }
    [System.Serializable] class ItemBook { public SlotNames[] slots; public WeaponNames[] weapons; public MythicSet[] mythicSets; public string[] mythicSlots, wings, mythicPets; }

    public static class AldaraItems
    {
        public struct Rar { public string k, c; public float m; }
        public static readonly Rar[] RARITY = {
            new Rar { k = "Common", c = "#b8b8c8", m = 1 }, new Rar { k = "Rare", c = "#4a90e2", m = 1.7f }, new Rar { k = "Epic", c = "#c04ad0", m = 2.6f },
            new Rar { k = "Legendary", c = "#ff9a2a", m = 3.6f }, new Rar { k = "Mythical", c = "#ff3a7a", m = 5 } };
        public static readonly string[] GEAR_TYPES = { "helmet", "chest", "gauntlets", "leggings", "boots", "weapon", "back", "accessory" };
        public static readonly string[] LEFT_SLOTS = { "helmet", "chest", "gauntlets", "leggings", "boots", "relic1" }, RIGHT_SLOTS = { "weapon", "back", "wings", "accessory", "pet", "mount", "relic2" };
        public static readonly Dictionary<string, string> SLOT_LABEL = new Dictionary<string, string> {
            {"helmet","Helmet"},{"chest","Chest Armor"},{"gauntlets","Gauntlets"},{"leggings","Leggings"},{"boots","Boots"},{"weapon","Weapon"},{"back","Cape"},
            {"wings","Back"},{"accessory","Accessory"},{"pet","Pet"},{"mount","Mount"},{"relic1","Relic"},{"relic2","Relic"} };
        public static readonly Dictionary<string, float> PET_PCT = new Dictionary<string, float> { { "Common", 0.05f }, { "Rare", 0.10f }, { "Epic", 0.15f }, { "Legendary", 0.20f }, { "Mythical", 0.35f } };
        public const int BACKPACK_MAX = 100, BASE_NAME_COUNT = 11, WINGS_LEVEL = 40, VIAL_MAX = 30;
        public static int itemSeq = 1;
        static ItemBook book;
        static ItemBook B { get { if (book == null) book = JsonUtility.FromJson<ItemBook>(Resources.Load<TextAsset>("items").text); return book; } }
        static string[] Names(string type) { foreach (var s in B.slots) if (s.type == type) return s.names; return new string[0]; }
        static string[] WNames(string cls) { foreach (var w in B.weapons) if (w.cls == cls) return w.names; return new string[0]; }
        // the developer menu (AldaraDevWin): every name the item book knows
        public static string[] NamesOf(string type) { return type == "wings" ? B.wings : type == "mythpet" ? B.mythicPets : Names(type); }
        public static string[] WeaponNamesOf(string cls) { return WNames(cls); }
        public static MythicSet[] MythicSets { get { return B.mythicSets; } }
        public static string[] MythicSlots { get { return B.mythicSlots; } }
        public static Item BuildWings(string name, Rar r, int level)
        {
            level = Mathf.Max(10, level);
            return new Item { id = itemSeq++, type = "wings", name = name, rarity = r.k, color = r.c, lvl = level, hp = Mathf.Round((6 + level * 0.8f) * r.m * 0.5f), speed = Mathf.Round((10 + level * 0.3f) * r.m * 0.5f), agi = Mathf.Round((8 + level * 0.8f) * r.m * 0.6f) };
        }
        public static Item BuildMythic(MythicSet S, int si, int level)
        {
            string sl = B.mythicSlots[si]; var it = Build(sl, S.pieces[si], RARITY[4], Mathf.Max(40, level));
            it.hp = Mathf.Round(it.hp * 1.8f); it.speed = Mathf.Round(it.speed * 1.5f);
            it.atk = Mathf.Round(it.atk * 1.8f + (4 + it.lvl / 4f) * (sl == "chest" ? 2.2f : 1.2f)); it.myth = S.id; return it;
        }
        static bool IsMythSlot(string t) { return System.Array.IndexOf(B.mythicSlots, t) >= 0; }
        public static MythicSet MythSetById(string id) { foreach (var s in B.mythicSets) if (s.id == id) return s; return null; }
        public static MythicSet MythSetOf(Item it)
        {
            if (it == null) return null; foreach (var s in B.mythicSets) if (System.Array.IndexOf(s.pieces, it.name) >= 0) return s; return null;
        }
        public static bool OwnsFullMythSet()
        {
            var H = AldaraHero.I; var all = new List<Item>(H.inventory); foreach (var kv in H.equip) if (kv.Value != null) all.Add(kv.Value);
            foreach (var S in B.mythicSets) { bool full = true; foreach (var p in S.pieces) if (!all.Exists(it => it.name == p)) { full = false; break; } if (full) return true; }
            return false;
        }
        public static float PetPct(Item it) { float v; return it != null && it.type == "pet" && PET_PCT.TryGetValue(it.rarity, out v) ? v : 0; }
        public static float Score(Item it) { return it.atk * 3 + it.speed * 5 + it.hp + it.agi * 4 + PetPct(it) * 400; }
        public static bool CanUse(Item it, string cls) { return it.type != "weapon" || (string.IsNullOrEmpty(it.cls) ? "knight" : it.cls) == cls; }

        static float[] RarityWeights(int level, float bonus)
        {
            return new[] { Mathf.Max(30, 80 - level * 0.6f - bonus * 0.5f), 6 + level * 0.16f + bonus * 0.25f, 0.6f + level * 0.05f + bonus * 0.12f, level >= 25 ? (level - 25) * 0.03f + bonus * 0.05f : bonus * 0.015f };
        }
        public static Rar PickRarity(int level, float bonus = 0, bool noCommon = false)
        {
            var w = RarityWeights(Mathf.Max(1, level), bonus); if (noCommon) w[0] = 0;
            float tot = 0; foreach (var x in w) tot += x; float roll = Random.value * tot;
            for (int i = 0; i < w.Length; i++) { if (roll < w[i]) return RARITY[i]; roll -= w[i]; }
            return RARITY[0];
        }
        static float PetChance(int level, float bonus) { return Mathf.Min(0.02f, 0.0025f * Mathf.Min(4, bonus > 0 ? bonus : 1) * (1 + Mathf.Min(1, level / 60f))); }
        static float MythChance(int level, float bonus) { return level < 20 ? 0 : (0.002f + (level - 20) * 0.0003f) * (bonus > 0 ? bonus : 1); }
        public static Item RollItem(int level, Rar? force = null, float mythBonus = 0)
        {
            level = Mathf.Max(1, level);
            string type = Random.value < PetChance(level, mythBonus) ? "pet" : GEAR_TYPES[Random.Range(0, GEAR_TYPES.Length)];
            if (type == "back" && Random.value < 0.66f) { var g = new List<string>(GEAR_TYPES); g.Remove("back"); type = g[Random.Range(0, g.Count)]; }
            var rarity = force ?? PickRarity(level);
            string wcls = type == "weapon" ? new[] { "knight", "mage", "archer" }[Random.Range(0, 3)] : null;
            string[] list = wcls != null ? WNames(wcls) : IsMythSlot(type) ? Sub(Names(type), BASE_NAME_COUNT) : Names(type);
            int nameIdx = Mathf.Min(list.Length - 1, Mathf.FloorToInt((level - 1) * list.Length / 50f) + (Random.value < 0.3f ? 1 : 0));
            string name = list[nameIdx];
            if (type == "pet" && Random.value < MythChance(level, mythBonus) * 0.4f) { rarity = RARITY[4]; name = B.mythicPets[Random.Range(0, B.mythicPets.Length)]; }
            return Build(type, name, rarity, level, wcls);
        }
        static string[] Sub(string[] a, int n) { var o = new string[Mathf.Min(n, a.Length)]; System.Array.Copy(a, o, o.Length); return o; }
        public static Item Build(string type, string name, Rar rarity, int level, string wcls = null)
        {
            float tier = 0.5f + level / 6f, m = rarity.m;
            var it = new Item { id = itemSeq++, type = type, name = name, rarity = rarity.k, color = rarity.c, lvl = level, cls = wcls };
            switch (type)
            {
                case "weapon": it.atk = Mathf.Round((2 + tier * 2) * m); break;
                case "chest": it.hp = Mathf.Round((8 + tier * 6) * m); break;
                case "leggings": it.hp = Mathf.Round((5 + tier * 4) * m); break;
                case "gauntlets": it.atk = Mathf.Round((1 + tier) * m * 0.7f); it.hp = Mathf.Round((2 + tier) * m); break;
                case "boots": it.hp = Mathf.Round((3 + tier * 2) * m); it.speed = Mathf.Round(3 * m); break;
                case "helmet": it.hp = Mathf.Round((4 + tier * 3) * m); break;
                case "back": it.hp = Mathf.Round((3 + tier * 2) * m); it.speed = Mathf.Round(2 * m); break;
                case "pet": it.atk = Mathf.Round((1 + tier) * m * 0.5f); it.hp = Mathf.Round((3 + tier * 2) * m * 0.7f); it.speed = Mathf.Round(m); break;
                default: it.atk = Mathf.Round((1 + tier) * m * 0.6f); it.hp = Mathf.Round((4 + tier * 3) * m * 0.6f); it.speed = Mathf.Round(2 * m); break;
            }
            return it;
        }
        // ---- the ultra rare drops ----
        public static float MythArmorChance(int level, bool boss) { return boss ? 0.006f : (level < 30 ? 0 : 0.00011f + (level - 30) * 0.0000025f); }
        public static Item RollMythicArmor(int level)
        {
            var S = B.mythicSets[Random.Range(0, B.mythicSets.Length)]; int si = Random.Range(0, B.mythicSlots.Length); string sl = B.mythicSlots[si];
            var it = Build(sl, S.pieces[si], RARITY[4], Mathf.Max(40, level));
            it.hp = Mathf.Round(it.hp * 1.8f); it.speed = Mathf.Round(it.speed * 1.5f);
            it.atk = Mathf.Round(it.atk * 1.8f + (4 + it.lvl / 4f) * (sl == "chest" ? 2.2f : 1.2f)); it.myth = S.id; return it;
        }
        public static float WingsChance(int level, bool boss) { return boss ? 0.004f : (level < 30 ? 0 : 0.00008f + level * 0.000004f); }
        public static Item RollWings(int level, float bonus = 0)
        {   // rollWings + buildWings
            level = Mathf.Max(10, level); var r = PickRarity(level, 20 + bonus, true);
            return new Item { id = itemSeq++, type = "wings", name = B.wings[Random.Range(0, B.wings.Length)], rarity = r.k, color = r.c, lvl = level,
                hp = Mathf.Round((6 + level * 0.8f) * r.m * 0.5f), speed = Mathf.Round((10 + level * 0.3f) * r.m * 0.5f), agi = Mathf.Round((8 + level * 0.8f) * r.m * 0.6f) };
        }
        public static string Desc(Item it)
        {
            var p = new List<string>();
            if (it.atk > 0) p.Add("+" + it.atk + " ATK"); if (it.hp > 0) p.Add("+" + it.hp + " HP"); if (it.agi > 0) p.Add("+" + it.agi + " AGI"); if (it.speed > 0) p.Add("+" + it.speed + " SPD");
            float pp = PetPct(it); if (pp > 0) p.Add("+" + Mathf.RoundToInt(pp * 100) + "% ALL STATS");
            var ms = MythSetOf(it); if (ms != null) p.Add(ms.name + " Set");
            if (it.type == "wings") p.Add("Level " + WINGS_LEVEL + "+");
            if (it.type == "mount") p.Add("Ride with " + AldaraKeys.Name(AldaraKeys.KeyOf("mount")) + "  Gallops 1.5x faster than running");
            return string.Join("  ", p);
        }
    }
}
