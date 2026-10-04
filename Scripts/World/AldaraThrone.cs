using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // The Throne Room of Valcrest (ktEnter / ktSpawn / ktUpdate / ktLeave): through the great doors of the palace, a hall
    // of black and white marble, a crimson carpet between gilded pillars, stained glass behind the dais, the statues of
    // the old kings, the court, the Queen and King Oswin IV. Built on the dungeon engine like the browser's; no monsters.
    public static class AldaraThrone
    {
        public static bool On; static float cd;
        public static readonly Vector2 DOOR = new Vector2(1500, 1735);
        public static readonly Vector2 PALACE_DOOR = new Vector2(25500, 18095.1f);
        public static readonly List<AldaraFolk.Folk> court = new List<AldaraFolk.Folk>();
        public static AldaraFolk.Folk king, queen;

        /// F at the palace doors
        public static void Enter()
        {
            var H = AldaraHero.I; if (!AldaraSave.Ready || !H.alive) return; if (AldaraDungeon.Active) { AldaraHud.Banner("Finish what you are doing first"); return; }
            if (AldaraWaystones.I) AldaraWaystones.I.FadeNow();
            AldaraDungeon.StartThrone();
        }
        /// called once the room is built
        public static void OnEnter()
        {
            On = true; cd = 1.2f; Spawn();
            AldaraHud.Banner("The Throne Room of Valcrest");
        }
        public static void Leave()
        {
            if (!On) return; if (AldaraWaystones.I) AldaraWaystones.I.FadeNow(); if (AldaraQuestDlg.IsOpen) AldaraQuestDlg.Close();
            AldaraDungeon.Exit("fort"); AldaraHud.Banner("You step out onto the palace stair");
        }
        public static void OnExit() { if (!On) return; On = false; foreach (var n in court) if (n.view) Object.Destroy(n.view); court.Clear(); king = queen = null; }
        public static void Tick(float dt)
        {
            if (!On) return; var P = AldaraPlayer.I; cd -= dt;
            foreach (var n in court) { if (n.npc != null && Vector2.Distance(new Vector2(n.x, n.y), new Vector2(P.x, P.y)) < 300) n.face = Mathf.Atan2(P.y - n.y, P.x - n.x); n.dx = n.dy = 0; }
            if (cd <= 0 && P.y > DOOR.y - 40 && Mathf.Abs(P.x - DOOR.x) < 110) Leave();
        }
        public const string STATUS = "King Oswin IV holds court  ·  the doors lead back out";

        // ---------- the court (ktSpawn) ----------
        static AldaraFolk.Folk Mk(string name, float x, float y, float face, string npc = null)
        {
            var f = AldaraFolk.I.NewFolk(name, x, y); f.role = "stand"; f.face = face; f.face3 = face; f.npc = npc; court.Add(f); return f;
        }
        static void Spawn()
        {
            court.Clear(); float down = Mathf.PI / 2;
            king = Mk("KN King", 1500, 650, down, "king"); queen = Mk("KN Queen", 1640, 670, down, "queen"); Mk("KN Chamberlain", 1340, 700, down);
            for (int k = 0; k < 5; k++) foreach (var sd in new[] { -1, 1 }) Mk("Royal Knight", 1500 + sd * 150, 800 + k * 170, sd < 0 ? 0 : Mathf.PI);
            string[] courtN = { "Noble Lord", "Noble Lady", "Elf Sage", "Dwarf Smith", "Noble Lady", "Elf Lady", "Noble Lord", "Scholar" };
            for (int i = 0; i < courtN.Length; i++) { int sd = i < 4 ? -1 : 1; float x = 1500 + sd * (320 + (i % 2) * 60), y = 820 + (i % 4) * 170; Mk(courtN[i], x, y, sd < 0 ? 0 : Mathf.PI); }
            Mk("Crown Guard", 1380, 1700, -Mathf.PI / 2); Mk("Crown Guard", 1620, 1700, -Mathf.PI / 2); Mk("Royal Herald", 1500, 760, down);
        }
        public static Vector2? Pos(string npc)
        {
            if (!On) return null; if (npc == "king" && king != null) return new Vector2(king.x, king.y); if (npc == "queen" && queen != null) return new Vector2(queen.x, queen.y); return null;
        }
        /// ktNear: the King or the Queen within reach
        public static AldaraFolk.Folk Near()
        {
            if (!On) return null; var P = AldaraPlayer.I; AldaraFolk.Folk best = null; float bd = 120;
            foreach (var n in new[] { king, queen }) { if (n == null) continue; float d = Vector2.Distance(new Vector2(n.x, n.y), new Vector2(P.x, P.y)); if (d < bd) { bd = d; best = n; } }
            return best;
        }
        public static bool Interact() { var n = Near(); if (n == null) return false; AldaraQuestDlg.Open(n.npc); return true; }
    }
}
