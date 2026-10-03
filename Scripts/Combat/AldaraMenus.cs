using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldara
{
    // Character select (continue, import a browser character, or make a new one) and the in-game windows:
    // Inventory (I), Attributes (C) and Skills (K), with the browser's rules. Esc closes them.
    public class AldaraMenus : MonoBehaviour
    {
        enum Win { None, Inventory, Attributes, Skills }
        Win open = Win.None;
        string newName = "Adventurer", newCls = "knight"; Vector2 scroll; bool creating;
        List<AldaraSave.Summary> mine, browser;
        GUIStyle h1, h2, body, btn, itemSt; Texture2D white;
        static readonly string[] CLS = { "knight", "mage", "archer" };

        static AldaraMenus inst;
        void Start() { inst = this; Refresh(); }
        public static void Open(string w)
        {
            if (AldaraWindows.I && AldaraWindows.I.Has(w)) { AldaraWindows.I.Toggle(w); return; }
            if (!inst) return; Win x = w == "inv" ? Win.Inventory : w == "att" ? Win.Attributes : w == "skills" ? Win.Skills : Win.None;
            if (x == Win.None) { AldaraHud.Banner("Coming soon: " + w); return; }
            inst.Toggle(x);
        }
        void Refresh() { mine = AldaraSave.List(AldaraSave.UnityDir, false); browser = AldaraSave.List(AldaraSave.BrowserDir, true); }
        void Update()
        {
            AldaraSave.Tick(); var kb = Keyboard.current; if (kb == null || !AldaraSave.Ready || AldaraHud.Typing) return;
            if (kb.escapeKey.wasPressedThisFrame) open = Win.None;
        }
        void OnApplicationQuit() { AldaraSave.Save(); }
        void Toggle(Win w) { open = open == w ? Win.None : w; scroll = Vector2.zero; }
        void Init()
        {
            if (white) return; white = Texture2D.whiteTexture;
            h1 = new GUIStyle { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; h1.normal.textColor = AldaraRules.Hex("#e8c86a");
            h2 = new GUIStyle { fontSize = 17, fontStyle = FontStyle.Bold }; h2.normal.textColor = AldaraRules.Hex("#e8c86a");
            body = new GUIStyle { fontSize = 13, wordWrap = true }; body.normal.textColor = new Color(0.9f, 0.88f, 0.82f);
            itemSt = new GUIStyle(body) { fontStyle = FontStyle.Bold };
            btn = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        }
        void Panel(Rect r) { GUI.color = new Color(0.06f, 0.06f, 0.09f, 0.94f); GUI.DrawTexture(r, white); GUI.color = AldaraRules.Hex("#8a6a2a"); GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2), white); GUI.DrawTexture(new Rect(r.x, r.yMax - 2, r.width, 2), white); GUI.color = Color.white; }
        void OnGUI()
        {
            Init(); btn = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            if (!AldaraSave.Ready) { if (!AldaraTitle.I) CharacterSelect(); return; }
            AldaraHud.Typing = false;
            var r = new Rect(Screen.width / 2 - 360, 70, 720, Screen.height - 200);
            switch (open)
            {
                case Win.Inventory: Inventory(r); break;
                case Win.Attributes: Attributes(new Rect(Screen.width / 2 - 220, 110, 440, 360)); break;
                case Win.Skills: Skills(r); break;
            }
            if (open != Win.None) AldaraHud.Block(r);
        }

        // ---- character select ----
        void CharacterSelect()
        {
            GUI.color = new Color(0, 0, 0, 0.72f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white); GUI.color = Color.white;
            var r = new Rect(Screen.width / 2 - 300, 80, 600, Screen.height - 160); Panel(r);
            GUI.Label(new Rect(r.x, r.y + 18, r.width, 44), "Realm of Aldara", h1);
            float y = r.y + 84;
            if (!creating)
            {
                GUI.Label(new Rect(r.x + 30, y, 400, 24), "Your characters", h2); y += 30;
                if (mine.Count == 0) { GUI.Label(new Rect(r.x + 30, y, 540, 22), "No characters in the Unity version yet.", body); y += 26; }
                foreach (var s in mine)
                {
                    if (GUI.Button(new Rect(r.x + 30, y, 540, 34), s.name + "   -   Lv " + s.lvl + " " + Cap(s.cls) + "   (slot " + (s.slot + 1) + ")", btn)) { AldaraSave.Load(s.slot); }
                    y += 40;
                }
                y += 10;
                if (browser.Count > 0)
                {
                    GUI.Label(new Rect(r.x + 30, y, 540, 24), "Bring over from the browser game", h2); y += 30;
                    foreach (var s in browser)
                    {
                        bool taken = mine.Exists(m => m.slot == s.slot);
                        if (GUI.Button(new Rect(r.x + 30, y, 540, 34), "Import " + s.name + "   -   Lv " + s.lvl + " " + Cap(s.cls) + (taken ? "   (replaces slot " + (s.slot + 1) + ")" : ""), btn)) { AldaraSave.Import(s.slot); Refresh(); }
                        y += 40;
                    }
                    GUI.Label(new Rect(r.x + 30, y, 540, 36), "Importing copies the character. The browser game's own save is left as it is.", body); y += 44;
                }
                if (mine.Count < 3 && GUI.Button(new Rect(r.x + 30, y, 540, 38), "Create a new character", btn)) creating = true;
            }
            else
            {
                GUI.Label(new Rect(r.x + 30, y, 400, 24), "New character", h2); y += 34;
                GUI.Label(new Rect(r.x + 30, y + 6, 80, 22), "Name", body);
                GUI.SetNextControlName("nm"); newName = GUI.TextField(new Rect(r.x + 110, y, 300, 30), newName, 16); AldaraHud.Typing = GUI.GetNameOfFocusedControl() == "nm"; y += 44;
                for (int i = 0; i < 3; i++) { var c = CLS[i]; if (GUI.Toggle(new Rect(r.x + 30 + i * 180, y, 170, 40), newCls == c, Cap(c), btn)) newCls = c; }
                y += 52;
                GUI.Label(new Rect(r.x + 30, y, 540, 60), newCls == "knight" ? "Sword and shield. Fights up close with a three-hit combo, tough and strong." : newCls == "mage" ? "A staff of arcane fire and frost. Casts from range; Energy powers everything." : "A bow. Shoots from long range; Agility makes every arrow hit harder.", body); y += 64;
                if (GUI.Button(new Rect(r.x + 30, y, 260, 38), "Begin", btn) && newName.Trim().Length > 0)
                {
                    int s = 0; while (mine.Exists(m => m.slot == s) && s < 3) s++;
                    AldaraSave.NewCharacter(s, newName.Trim(), newCls); creating = false;
                }
                if (GUI.Button(new Rect(r.x + 310, y, 260, 38), "Back", btn)) creating = false;
            }
        }
        static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1); }

        // ---- inventory ----
        void Inventory(Rect r)
        {
            var H = AldaraHero.I; Panel(r);
            GUI.Label(new Rect(r.x + 20, r.y + 12, 300, 26), "Inventory", h2);
            GUI.Label(new Rect(r.x + 200, r.y + 14, 500, 22), "Gold " + H.gold + "    Health Vials " + H.vialHp + " (H)    Mana Vials " + H.vialMp + " (M)    Bag " + H.inventory.Count + "/" + AldaraItems.BACKPACK_MAX, body);
            float y = r.y + 46, colW = (r.width - 60) / 2;
            for (int c = 0; c < 2; c++)
            {
                var slots = c == 0 ? AldaraItems.LEFT_SLOTS : AldaraItems.RIGHT_SLOTS;
                for (int i = 0; i < slots.Length; i++)
                {
                    var sl = slots[i]; var it = H.Eq(sl); var rr = new Rect(r.x + 20 + c * (colW + 20), y + i * 46, colW, 42);
                    GUI.color = new Color(1, 1, 1, 0.06f); GUI.DrawTexture(rr, white); GUI.color = Color.white;
                    GUI.Label(new Rect(rr.x + 8, rr.y + 2, 200, 16), AldaraItems.SLOT_LABEL[sl] + (it != null ? "  -  Lv " + it.lvl : ""), new GUIStyle(body) { fontSize = 11 });
                    if (it != null)
                    {
                        var st = new GUIStyle(itemSt); st.normal.textColor = it.Col; GUI.Label(new Rect(rr.x + 8, rr.y + 16, rr.width - 90, 20), it.name, st);
                        GUI.Label(new Rect(rr.x + 8, rr.y + 28, rr.width - 90, 16), AldaraItems.Desc(it), new GUIStyle(body) { fontSize = 10 });
                        if (!sl.StartsWith("relic") && GUI.Button(new Rect(rr.xMax - 80, rr.y + 9, 72, 24), "Remove", btn)) H.Unequip(sl);
                    }
                    else GUI.Label(new Rect(rr.x + 8, rr.y + 18, 200, 18), sl.StartsWith("relic") ? "No relic" : "Empty", body);
                }
            }
            y += 6 * 46 + 8;
            if (GUI.Button(new Rect(r.x + 20, y, 200, 28), "Equip best", btn)) H.AutoEquipBest();
            y += 36;
            var list = new Rect(r.x + 20, y, r.width - 40, r.yMax - y - 14);
            float rowH = 40; var view = new Rect(0, 0, list.width - 20, Mathf.Max(list.height, H.inventory.Count * rowH));
            scroll = GUI.BeginScrollView(list, scroll, view);
            Item toEquip = null, toDrop = null;
            for (int i = 0; i < H.inventory.Count; i++)
            {
                var it = H.inventory[i]; var rr = new Rect(0, i * rowH, view.width, rowH - 4);
                GUI.color = new Color(1, 1, 1, i % 2 == 0 ? 0.05f : 0.02f); GUI.DrawTexture(rr, white); GUI.color = Color.white;
                var st = new GUIStyle(itemSt); st.normal.textColor = it.Col; bool usable = AldaraItems.CanUse(it, H.cls);
                GUI.Label(new Rect(8, rr.y + 2, 340, 20), it.name + "   (" + it.rarity + " " + (AldaraItems.SLOT_LABEL.ContainsKey(it.type) ? AldaraItems.SLOT_LABEL[it.type] : it.type) + ", Lv " + it.lvl + ")", st);
                var cur = H.Eq(it.type); string cmp = cur == null ? "" : AldaraItems.Score(it) > AldaraItems.Score(cur) ? "   upgrade" : "";
                GUI.Label(new Rect(8, rr.y + 20, 420, 16), AldaraItems.Desc(it) + cmp, new GUIStyle(body) { fontSize = 11 });
                if (usable && GUI.Button(new Rect(rr.xMax - 150, rr.y + 6, 70, 24), "Equip", btn)) toEquip = it;
                if (GUI.Button(new Rect(rr.xMax - 74, rr.y + 6, 70, 24), "Drop", btn)) toDrop = it;
            }
            GUI.EndScrollView();
            if (toEquip != null) H.EquipItem(toEquip);
            if (toDrop != null) { H.inventory.Remove(toDrop); AldaraSave.Dirty(); }
        }

        // ---- attributes ----
        void Attributes(Rect r)
        {
            var H = AldaraHero.I; Panel(r);
            GUI.Label(new Rect(r.x + 20, r.y + 12, 300, 26), "Attributes", h2);
            GUI.Label(new Rect(r.x + 20, r.y + 44, 400, 22), "Points to spend: " + H.statPoints, body);
            string[] k = { "str", "agi", "vit", "ene" }; string[] n = { "Strength", "Agility", "Vitality", "Energy" };
            string[] d = { "Attack (1 per 4)", "Speed, and Attack for archers", "Health (3 per point)", "Mana, and Attack for mages" };
            int[] v = { H.str, H.agi, H.vit, H.ene };
            for (int i = 0; i < 4; i++)
            {
                float y = r.y + 76 + i * 50;
                GUI.Label(new Rect(r.x + 20, y, 120, 22), n[i], itemSt); GUI.Label(new Rect(r.x + 140, y, 60, 22), v[i].ToString(), itemSt);
                GUI.Label(new Rect(r.x + 20, y + 20, 300, 18), d[i], new GUIStyle(body) { fontSize = 11 });
                GUI.enabled = H.statPoints > 0; if (GUI.Button(new Rect(r.xMax - 70, y, 50, 30), "+", btn)) H.SpendPoint(k[i]); GUI.enabled = true;
            }
            GUI.Label(new Rect(r.x + 20, r.yMax - 66, 400, 50), "Attack " + H.atk + "    Health " + H.maxHp + "    Mana " + H.maxMana + "    Speed " + H.speed, body);
        }

        // ---- skills ----
        void Skills(Rect r)
        {
            var H = AldaraHero.I; var S = AldaraSkills.I; Panel(r);
            GUI.Label(new Rect(r.x + 20, r.y + 12, 300, 26), "Skills", h2);
            GUI.Label(new Rect(r.x + 160, r.y + 14, 540, 22), "Gold " + H.gold + "    Equipped " + S.equipped.Count + "/10 (keys 1-9, 0)", body);
            float y = r.y + 48;
            foreach (var sk in S.classSkills)
            {
                var rr = new Rect(r.x + 20, y, r.width - 40, 54); GUI.color = new Color(1, 1, 1, 0.05f); GUI.DrawTexture(rr, white); GUI.color = Color.white;
                bool own = S.owned.Contains(sk.id), eq = S.equipped.Contains(sk.id);
                GUI.Label(new Rect(rr.x + 8, rr.y + 4, 400, 20), sk.name + "   (" + sk.cost + " mana, " + sk.cd + "s)", itemSt);
                GUI.Label(new Rect(rr.x + 8, rr.y + 24, rr.width - 200, 30), sk.desc, new GUIStyle(body) { fontSize = 11 });
                if (!own)
                {
                    GUI.enabled = H.gold >= sk.price;
                    if (GUI.Button(new Rect(rr.xMax - 170, rr.y + 12, 160, 30), "Learn (" + sk.price + " gold)", btn)) { H.gold -= sk.price; S.owned.Add(sk.id); if (S.equipped.Count < 10) S.equipped.Add(sk.id); AldaraSave.Dirty(); }
                    GUI.enabled = true;
                }
                else if (GUI.Button(new Rect(rr.xMax - 170, rr.y + 12, 160, 30), eq ? "Unequip" : "Equip", btn))
                {
                    if (eq) S.equipped.Remove(sk.id); else if (S.equipped.Count < 10) S.equipped.Add(sk.id); AldaraSave.Dirty();
                }
                y += 60;
            }
        }
    }
}
