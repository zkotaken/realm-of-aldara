using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aldara
{
    // Two pieces of the browser's late-game overworld:
    // Kingsroad ambushes (kambTick): on the roads of the Brigand Marches the Red Hand springs from the roadside, and in
    // Gloomwood a warg pack closes in from the trees, a few at a time, worth more experience and gold than a camp.
    // The Bloodmoor (pvpTick, pvpZoneNote): the notice on entering and leaving the free-for-all moor, the zone name in
    // red while you are on it, auto-combat switched off there, and its monsters slow to come back.
    public class AldaraWilds : MonoBehaviour
    {
        public static AldaraWilds I; public const int PVP_Z = 10;
        public static bool InMoor;
        readonly List<AldaraMonsters.Mon> pool = new List<AldaraMonsters.Mon>(); bool made; float cd = 30, t;
        VisualElement note; Label nTag, nT, nS, nS2; float noteT, noteMax; bool noteOn;
        void Awake() { I = this; }
        static Color Hx(string h) { return AldaraRules.Hex(h); }

        void MakePool()
        {
            var M = AldaraMonsters.I; if (made || M == null) return; made = true; var book = MonsterBook.Load();
            string[] bandits = { "Bandit Cutthroat", "Bandit Cutthroat", "Bandit Crossbowman", "Brigand Brute", "Bandit Cutthroat", "Bandit Crossbowman" }, wargs = { "Dire Warg", "Dire Warg", "Dire Warg", "Marauder Warg" };
            for (int k = 0; k < 12; k++)
            {
                string nm = k < 6 ? bandits[k] : wargs[(k - 6) % 4]; var d = book.Get(nm); if (d == null) continue; var st = AldaraMonsters.MonStats(d, d.lv0);
                var m = new AldaraMonsters.Mon { def = d, name = d.name, r = d.r, tier = d.tier, lvl = d.lv0, hp = st.hp, maxHp = st.hp, atk = st.atk, xp = st.xp, gold = st.gold, dead = true, respawnT = 1e12f, ambush = k < 6 ? "bandit" : "warg" };
                M.all.Add(m); pool.Add(m);
            }
        }
        void Ambush(float dt)
        {
            var P = AldaraPlayer.I; t -= dt; if (cd > 0) cd -= dt;
            foreach (var m in pool) if (!m.dead && (Mathf.Abs(m.x - P.x) > 1900 || Mathf.Abs(m.y - P.y) > 1500)) { m.dead = true; m.respawnT = 1e12f; }
            if (t > 0) return; t = 1;
            int z = AldaraWorld.ZoneAt(P.x, P.y); if (z != 7 && z != 8) return; if (AldaraWorld.InCity(P.x, P.y)) return;
            if (AldaraWorld.RoadDist(P.x, P.y) > 110 || cd > 0 || Random.value > 0.2f) return;
            string kind = z == 7 ? "bandit" : "warg"; var free = pool.Where(m => m.dead && m.ambush == kind).ToList(); if (free.Count < 3) return;
            int n = Mathf.Min(free.Count, 3 + Mathf.FloorToInt(Random.value * 3)), madeN = 0; float bas = Random.value * Mathf.PI * 2;
            for (int k = 0; k < n; k++)
            {
                var m = free[k]; var d = m.def; Vector2? sp = null;
                for (int tr = 0; tr < 12 && sp == null; tr++) { float a = bas + k * (Mathf.PI * 2 / n) + (Random.value - 0.5f) * 0.6f, dd = 380 + Random.value * 120, x = P.x + Mathf.Cos(a) * dd, y = P.y + Mathf.Sin(a) * dd; if (!AldaraWorld.BlockedAt(x, y) && AldaraWorld.LiquidAt(x, y) == 0) sp = new Vector2(x, y); }
                if (sp == null) continue; int lvl = Mathf.Max(d.lv0, Mathf.Min(d.lv1, d.lv0 + Mathf.FloorToInt(Random.value * (d.lv1 - d.lv0 + 1)))); var st = AldaraMonsters.MonStats(d, lvl);
                m.x = m.homeX = sp.Value.x; m.y = m.homeY = sp.Value.y; m.lvl = lvl; m.hp = m.maxHp = st.hp; m.atk = st.atk; m.xp = Mathf.Round(st.xp * 1.3f); m.gold = Mathf.Round(st.gold * 1.5f);
                m.dead = false; m.respawnT = 1e12f; m.aggroT = 14; m.act = null; m.flash = 0; m.stunT = 0; m.slowT = 0; m.lvs = false;
                AldaraVfx.Burst(m.x, m.y - 20, Hx(kind == "bandit" ? "#8a1a1a" : "#3a3a40"), 16, 160); madeN++;
            }
            if (madeN > 0) { cd = 45 + Random.value * 40; AldaraHud.Banner(kind == "bandit" ? "Ambush! The Red Hand springs from the roadside" : "A warg pack closes in from the trees"); AldaraVfx.Shake(4, 0.35f); }
        }

        void BuildNote()
        {
            var ui = AldaraHudUI.I; if (ui == null || ui.Root == null) return;
            note = new VisualElement { pickingMode = PickingMode.Ignore }; note.style.position = Position.Absolute; note.style.left = Length.Percent(50); note.style.top = 92; note.style.minWidth = 380; note.style.alignItems = Align.Center;
            note.style.paddingLeft = note.style.paddingRight = 30; note.style.paddingTop = 12; note.style.paddingBottom = 13; note.style.opacity = 0;
            note.style.borderTopLeftRadius = note.style.borderTopRightRadius = note.style.borderBottomLeftRadius = note.style.borderBottomRightRadius = 6; note.style.borderLeftWidth = note.style.borderRightWidth = note.style.borderTopWidth = note.style.borderBottomWidth = 1;
            System.Func<string, float, bool, Label> L = (s, size, head) => { var l = new Label(s) { pickingMode = PickingMode.Ignore }; l.style.unityFontDefinition = FontDefinition.FromFont(AldaraUI.Font(head, head)); l.style.fontSize = size; l.style.unityTextAlign = TextAnchor.MiddleCenter; l.style.whiteSpace = WhiteSpace.Normal; l.style.maxWidth = 640; l.style.marginTop = 3; note.Add(l); return l; };
            nTag = L("", 11, true); nTag.style.letterSpacing = 2; nTag.style.paddingLeft = nTag.style.paddingRight = 12; nTag.style.paddingTop = nTag.style.paddingBottom = 2; nTag.style.marginTop = 0; nTag.style.marginBottom = 5;
            nTag.style.borderTopLeftRadius = nTag.style.borderTopRightRadius = nTag.style.borderBottomLeftRadius = nTag.style.borderBottomRightRadius = 10;
            nT = L("", 24, true); nT.style.letterSpacing = 1; nT.style.color = Hx("#ffe8e0"); nT.style.marginTop = 0;
            nS = L("", 14, false); nS.style.color = Hx("#f0d8d0"); nS2 = L("", 12, false); nS2.style.color = Hx("#ffb0a0"); nS2.style.unityFontStyleAndWeight = FontStyle.Italic;
            ui.Root.Add(note);
        }
        void Note(bool on)
        {
            if (note == null) BuildNote(); if (note == null) return;
            int lv = 999; foreach (var d in MonsterBook.Load().monsters) if (d.tier == PVP_Z + 1 && d.lv0 > 0 && d.lv0 < lv) lv = d.lv0; if (lv == 999) lv = 1; bool low = AldaraHero.I.lvl < lv;
            nTag.text = on ? "PVP ZONE" : "SAFE"; nT.text = on ? "Entering the Bloodmoor" : "Leaving the Bloodmoor";
            nS.text = on ? "Anyone outside your party or guild can attack you here, and you them. The victor steals a quarter of the loser's gold." : "PvP is off. Other players can no longer attack you.";
            nS2.text = on ? "Auto-combat is off" + (low ? " · Monsters here are level " + lv + " and above" : "") : ""; nS2.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            var bc = on ? Hx("#ff4a3a") : Hx("#6ad08a"); note.style.borderLeftColor = note.style.borderRightColor = note.style.borderTopColor = note.style.borderBottomColor = bc;
            note.style.backgroundColor = on ? new Color(65 / 255f, 8 / 255f, 7 / 255f, 0.94f) : new Color(14 / 255f, 34 / 255f, 24 / 255f, 0.94f);
            nTag.style.backgroundColor = bc; nTag.style.color = on ? Hx("#1a0404") : Hx("#04160a");
            nT.style.textShadow = new TextShadow { color = on ? new Color(1, 80 / 255f, 50 / 255f, 0.8f) : new Color(80 / 255f, 220 / 255f, 130 / 255f, 0.6f), offset = Vector2.zero, blurRadius = on ? 12 : 10 };
            noteOn = on; noteMax = on ? 6.5f : 3.5f; noteT = 0; AldaraHudUI.ZonePvp(on);
        }
        void Update()
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I; if (!AldaraSave.Ready || !P || !H) return;
            MakePool();
            if (note != null)
            {
                noteT += Time.deltaTime; float a = noteT < noteMax ? Mathf.Clamp01(noteT / 0.45f) : Mathf.Clamp01(1 - (noteT - noteMax) / 0.45f);
                note.style.opacity = a; note.style.translate = new Translate(Length.Percent(-50), -30 * (1 - a));
            }
            bool inZ = !AldaraWorld.Dun && AldaraWorld.ZoneAt(P.x, P.y) == PVP_Z; if (inZ != InMoor) { InMoor = inZ; Note(inZ); }
            if (InMoor && AldaraAuto.on) { AldaraAuto.Set(false); AldaraHud.Banner("Auto-combat does not work in the Bloodmoor"); }
            if (!AldaraWorld.Dun && H.alive) Ambush(Mathf.Min(0.1f, Time.deltaTime));
        }
    }
}
