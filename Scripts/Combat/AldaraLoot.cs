using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // What a kill drops, as in the browser's killMonster: gold scattered as coins that bounce and fly to you
    // (pickupSpawn / pickupUpdate), health and mana vials (dropVials), an item 12% + 5% per tier (bosses: two, never
    // Common), and the ultra rare wings and Mythical armor.
    public class AldaraLoot : MonoBehaviour
    {
        public static AldaraLoot I;
        class Coin { public float x, y, z, vx, vy, vz, val, t, rot; public GameObject go; }
        readonly List<Coin> coins = new List<Coin>();
        void Awake() { I = this; }

        public void OnKill(AldaraMonsters.Mon t)
        {
            var H = AldaraHero.I;
            SpawnCoins(t);
            // vials
            int hp = t.boss ? 3 : (Random.value < 0.16f ? 1 : 0), mp = t.boss ? 2 : (Random.value < 0.06f ? 1 : 0); float y = t.y - t.r - 58;
            if (hp > 0) { H.vialHp = Mathf.Min(AldaraItems.VIAL_MAX, H.vialHp + hp); AldaraFx.Text(t.x, y, "Health Vial" + (hp > 1 ? " x" + hp : ""), AldaraRules.Hex("#ff8a7a")); y -= 16; }
            if (mp > 0) { H.vialMp = Mathf.Min(AldaraItems.VIAL_MAX, H.vialMp + mp); AldaraFx.Text(t.x, y, "Mana Vial" + (mp > 1 ? " x" + mp : ""), AldaraRules.Hex("#8aa8ff")); }
            // wings and mythical armor
            if (Random.value < AldaraItems.WingsChance(t.lvl, t.boss)) { var w = AldaraItems.RollWings(t.lvl); if (H.AddLoot(w)) { AldaraHud.Banner("WINGS DROP: " + w.name + "!"); AldaraFx.Text(t.x, t.y - t.r - 90, w.name + "!", w.Col); } }
            if (Random.value < AldaraItems.MythArmorChance(t.lvl, t.boss)) { var a = AldaraItems.RollMythicArmor(t.lvl); if (H.AddLoot(a)) { AldaraHud.Banner("MYTHICAL ARMOR: " + a.name + "!"); AldaraFx.Text(t.x, t.y - 110, a.name + "!", AldaraRules.Hex(AldaraItems.MythSetOf(a).glow)); } }
            if (t.boss)
            {
                for (int k = 0; k < 2; k++) { var it = AldaraItems.RollItem(t.lvl, AldaraItems.PickRarity(t.lvl, 30, true), 5); if (H.AddLoot(it)) AldaraFx.Text(t.x, t.y - t.r - 42 - k * 16, it.name + "!", it.Col); }
            }
            else if (Random.value < 0.12f + t.tier * 0.05f)
            {
                var it = AldaraItems.RollItem(t.lvl);
                if (H.AddLoot(it))
                {
                    AldaraFx.Text(t.x, t.y - t.r - 42, it.name + "!", it.Col);
                    if (it.type == "pet") AldaraHud.Banner("PET FOUND: " + it.name + "!");
                    else if (it.rarity == "Epic" || it.rarity == "Legendary" || it.rarity == "Mythical") AldaraHud.Banner(it.rarity + " drop: " + it.name + "!");
                }
            }
            AldaraSave.Dirty();
        }
        void SpawnCoins(AldaraMonsters.Mon t)
        {
            float gold = Mathf.Max(1, Mathf.Round(t.gold * (1 + AldaraTree.T("gold")))); int n = Mathf.Min(7, 2 + Mathf.FloorToInt(gold / 12) + (t.boss ? 3 : 0)); float per = Mathf.Max(1, Mathf.Floor(gold / n)), left = gold;
            for (int k = 0; k < n; k++)
            {
                float v = k == n - 1 ? left : per; left -= v; float a = Random.value * Mathf.PI * 2, sp = 40 + Random.value * 90;
                var c = new Coin { x = t.x + (Random.value - 0.5f) * 10, y = t.y + (Random.value - 0.5f) * 6, z = 14, vx = Mathf.Cos(a) * sp, vy = Mathf.Sin(a) * sp * 0.6f, vz = 150 + Random.value * 140, val = v, rot = Random.value * 6 };
                c.go = AldaraFx.Orb(AldaraRules.Hex("#f0c040"), 0.28f); c.go.transform.localScale = new Vector3(0.3f, 0.3f, 0.08f); coins.Add(c);
            }
            while (coins.Count > 160) { Destroy(coins[0].go); AldaraHero.I.gold += coins[0].val; coins.RemoveAt(0); }
        }
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f); var P = AldaraPlayer.I; var H = AldaraHero.I; if (!P || !H) return;
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                var p = coins[i]; p.t += dt; p.rot += dt * 4;
                float d = Mathf.Sqrt((P.x - p.x) * (P.x - p.x) + (P.y - p.y) * (P.y - p.y));
                bool pull = H.alive && p.t > 0.55f && (d < 150 || p.t > 10);
                if (pull)
                {
                    float k = Mathf.Min(1, dt * (6 + p.t)), ang = Mathf.Atan2(P.y - p.y, P.x - p.x), sp = (260 + p.t * 80) * dt;
                    p.x += Mathf.Cos(ang) * Mathf.Min(sp, d); p.y += Mathf.Sin(ang) * Mathf.Min(sp, d); p.z += (20 - p.z) * k; p.vx = p.vy = p.vz = 0;
                    if (d < 16) { H.gold += p.val; AldaraFx.Text(P.x + 10, P.y - 30, "+" + p.val, AldaraRules.Hex("#ffd84a")); Destroy(p.go); coins.RemoveAt(i); continue; }
                }
                else
                {
                    p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt; p.vz -= 520 * dt;
                    if (p.z < 0) { p.z = 0; p.vz = -p.vz * 0.42f; p.vx *= 0.6f; p.vy *= 0.6f; if (Mathf.Abs(p.vz) < 25) p.vz = 0; }
                }
                if (p.t > 40) { H.gold += p.val; Destroy(p.go); coins.RemoveAt(i); continue; }
                if (p.go) { p.go.transform.position = AldaraWorld.ToUnity(p.x, p.y) + Vector3.up * (p.z + 4) / AldaraWorld.PX; p.go.transform.rotation = Quaternion.Euler(0, p.rot * Mathf.Rad2Deg, 0); }
            }
        }
    }
}
