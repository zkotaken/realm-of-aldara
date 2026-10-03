using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The browser's Attributes window (renderAttributes): spend points one at a time, or Auto-Assign toward the class's
    // recommended split (autoAssignPoints hands each point to the stat furthest behind its share of the total).
    public class AldaraAttWin : AldaraWindows.Win
    {
        public AldaraAttWin() { id = "att"; title = "Attributes"; }
        public override float X => 14; public override float Y => 150; public override float W => 300;
        static AldaraHero Hr { get { return AldaraHero.I; } }
        public static readonly Dictionary<string, KeyValuePair<string, float>[]> WEIGHTS = new Dictionary<string, KeyValuePair<string, float>[]> {
            { "knight", new[] { KV("str", 0.40f), KV("vit", 0.35f), KV("agi", 0.15f), KV("ene", 0.10f) } },
            { "mage", new[] { KV("ene", 0.45f), KV("vit", 0.25f), KV("agi", 0.20f), KV("str", 0.10f) } },
            { "archer", new[] { KV("agi", 0.40f), KV("str", 0.25f), KV("vit", 0.25f), KV("ene", 0.10f) } } };
        static KeyValuePair<string, float> KV(string k, float v) { return new KeyValuePair<string, float>(k, v); }
        static string Cap(string s) { return char.ToUpper(s[0]) + s.Substring(1); }

        public override void Render(VisualElement body)
        {
            var gold = C("#e8c46a");
            var a = T(body, "Class: " + Cap(Hr.cls), 12, gold); a.style.marginBottom = 2;
            var b = T(body, "Points available: " + Hr.statPoints, 12, gold); b.style.marginBottom = 8;
            Row("Strength (ATK)", "str", Hr.str, Hr.effStr); Row("Agility (SPD)", "agi", Hr.agi, Hr.effAgi); Row("Vitality (HP)", "vit", Hr.vit, Hr.effVit); Row("Energy (ATK, MP)", "ene", Hr.ene, Hr.effEne);
            var au = B(body, "Auto-Assign (Recommended Build)", AutoAssign, "btn_blue", 12); au.style.height = 32; au.style.marginTop = 10; au.label.style.color = C("#e8e6da"); au.Disabled = Hr.statPoints <= 0;
            var w = WEIGHTS[Hr.cls]; var parts = new List<string>(); foreach (var kv in w) parts.Add(Mathf.RoundToInt(kv.Value * 100) + "% " + kv.Key.ToUpper());
            var n = T(body, Cap(Hr.cls) + " split: " + string.Join(" · ", parts), 10, C("#888888")); n.style.marginTop = 6;
            var s = T(body, "ATK " + Hr.atk + " · Max HP " + Hr.maxHp + " · Max MP " + Hr.maxMana + " · Speed " + Hr.speed, 11, C("#999999")); s.style.marginTop = 12;

            void Row(string label, string key, int v, int eff)
            {
                var r = AldaraUI.Row(body, 0, Justify.SpaceBetween); Pad(r, 5, 0); BorderBottom(r, 1, C("#2a3040")); r.style.height = 35;
                int bonus = eff - v;
                T(r, label + ": <b>" + v + "</b>" + (bonus > 0 ? " " + "<size=11><color=#88aa88>+" + bonus + " bonus</color></size>" : ""), 13, C("#f3e6c4"), false, false, false, 0, false, false);
                var pb = B(r, "+", () => { Hr.SpendPoint(key); }, "btn", 14); pb.style.width = 28; pb.style.height = 24; pb.Padding(1, 0); pb.Disabled = Hr.statPoints <= 0;
            }
        }
        static void AutoAssign()
        {
            if (Hr.statPoints <= 0) return; var w = WEIGHTS[Hr.cls]; int pool = Hr.statPoints;
            while (pool > 0)
            {
                float total = Hr.str + Hr.agi + Hr.vit + Hr.ene + pool; string best = null; float bd = float.NegativeInfinity;
                foreach (var kv in w) { float def = total * kv.Value - Get(kv.Key); if (def > bd) { bd = def; best = kv.Key; } }
                Add(best); pool--;
            }
            Hr.statPoints = 0; Hr.Recompute(); AldaraSave.Dirty(); AldaraHud.Banner("Attributes auto-assigned");
        }
        static int Get(string k) { return k == "str" ? Hr.str : k == "agi" ? Hr.agi : k == "vit" ? Hr.vit : Hr.ene; }
        static void Add(string k) { if (k == "str") Hr.str++; else if (k == "agi") Hr.agi++; else if (k == "vit") Hr.vit++; else Hr.ene++; }
    }
}
