using System.Linq;
using UnityEngine;
using Q = Aldara.AldaraQuests;

namespace Aldara
{
    // The browser's auto-combat (E, setAuto / AUTO_NERF / pickAutoSkill / autoSupport / autoVials) and auto-questing
    // (click a quest in the tracker while auto-combat is on: questAutoTarget). While on, the hero finds the nearest
    // monster within 420 px, walks to it and fights it, uses one ready skill every 3 seconds in slot order, heals,
    // shields and buffs itself, and drinks vials when low, at the cost of 25% less damage and 25% slower attacks.
    // Auto-questing hunts the followed quest's monsters, walks to its places and people and gathers its herbs.
    public static class AldaraAuto
    {
        public const float DMG = 0.75f, ATK = 1.25f, SK_GAP = 3, FIND = 420, HP_VIAL = 0.25f, MP_VIAL = 0.1f;
        public static bool on, questHunt; public static Q.Way questWalk; public static float skT; static float retargetT; static string asked;
        static AldaraHero H { get { return AldaraHero.I; } }
        static AldaraPlayer P { get { return AldaraPlayer.I; } }

        public static void Set(bool v)
        {
            on = v;
            if (v && AldaraSave.Ready) AldaraHud.Banner("Auto-combat on: 25% less damage and slower attacks");
            if (!v) { questHunt = false; questWalk = null; }
            Q.Render();
        }
        /// a click on a quest in the tracker
        public static void ClickQuest(string id)
        {
            var S = Q.S; var foc = Q.FocusInst(); bool was = S.focus == id || foc != null && foc.id == id; S.focus = id;
            if (!on) { questHunt = false; AldaraHud.Banner("Following: " + Q.Def(Q.InstOf(id)).title); Q.Render(); return; }
            questHunt = was ? !questHunt : true; questWalk = null; retargetT = 0; asked = null;
            if (questHunt) { H.target = null; AldaraHud.Banner("Auto-questing: " + Q.Def(Q.InstOf(id)).title); }
            Q.Render();
        }
        static float Dist(float ax, float ay, float bx, float by) { return Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)); }

        // currentQuest / questMatch: the followed quest's next step in the old shape
        class Cur { public string kind, target; public int zone; }
        static Cur Current()
        {
            var inst = Q.FocusInst(); if (inst == null || Q.IsReady(inst)) return new Cur { kind = "none" };
            var d = Q.Def(inst); Q.QObj o = null; for (int k = 0; k < d.obj.Length; k++) if (!Q.ObjDone(inst, k)) { o = d.obj[k]; break; }
            if (o == null) return new Cur { kind = "none" };
            if (o.k == "kill" || o.k == "boss") return new Cur { kind = "kill", target = o.t };
            if (o.k == "collect") return new Cur { kind = "collect", target = o.t };
            if (o.k == "zone") return new Cur { kind = "zone", zone = o.z };
            return new Cur { kind = o.k };
        }
        static bool Match(AldaraMonsters.Mon m, Cur q)
        {
            if (m == null || q == null) return false;
            if (q.kind == "zone") return Q.MonZone(m) == q.zone;
            if (q.kind == "kill" || q.kind == "collect") return m.name == q.target;
            return false;
        }
        /// called when no movement key is held and auto-combat is on
        public static void FindTarget(float dt)
        {
            if (questHunt) QuestTarget(dt);
            else if (H.target == null || H.target.dead) H.target = AldaraMonsters.I.FindNearest(P.x, P.y, FIND);
        }
        static void QuestTarget(float dt)
        {
            var q = Current(); var t = H.target;
            if (q.kind == "kill" || q.kind == "collect" || q.kind == "zone")
            {
                if (t != null && !t.dead && Match(t, q)) { questWalk = null; return; }
                retargetT -= dt; if (retargetT > 0 && t != null && !t.dead) return; retargetT = 0.5f;
                AldaraMonsters.Mon best = null; float bd = float.MaxValue;
                foreach (var m in AldaraMonsters.I.all) { if (m.dead || !Match(m, q) || m.navIgnore > Time.time) continue; float d = Dist(m.x, m.y, P.x, P.y); if (d < bd) { bd = d; best = m; } }
                if (best != null && bd < 2400) { H.target = best; questWalk = null; return; }
                var w0 = Q.Waypoint(); if (w0 != null && Dist(w0.x, w0.y, P.x, P.y) > 170) { if (t == null || t.dead) { H.target = null; questWalk = w0; } }
                else { questWalk = null; if (t == null || t.dead) H.target = AldaraMonsters.I.FindNearest(P.x, P.y, 400); }
                return;
            }
            if (q.kind == "dungeon") { questWalk = null; if (t == null || t.dead) H.target = AldaraMonsters.I.FindNearest(P.x, P.y, 400); return; }
            // walk to the next place, person or herb; fight whatever gets in the way
            if (t != null && !t.dead && Dist(t.x, t.y, P.x, P.y) < 260) { questWalk = null; return; }
            var w = Q.Waypoint(); if (w == null) { questWalk = null; return; }
            float dd = Dist(w.x, w.y, P.x, P.y);
            if (w.node != null && dd < 46) { questWalk = null; H.target = null; if (Q.gather == null) Q.StartGather(w.node); return; }
            if (w.npc != null && dd < 70) { questWalk = null; H.target = null; if (!(AldaraWindows.I && AldaraWindows.I.IsOpen("qdlg")) && asked == null) { asked = w.npc; AldaraQuestDlg.Open(w.npc); } return; }
            asked = null; H.target = null; questWalk = dd > (w.explore ? 60 : 30) ? w : null;
        }

        /// the hero's per-frame upkeep: the skill gap, support skills and vials
        public static void Tick(float dt)
        {
            if (skT > 0) skT -= dt;
            if (!on) return;
            if (!(skT > 0)) { float m0 = H.mana; Support(); if (H.mana < m0) skT = SK_GAP; }
            Vials();
        }
        static void Support()
        {
            var S = AldaraSkills.I; if (!S) return; bool fighting = H.aiming && H.target != null && !H.target.dead;
            foreach (var id in S.equipped)
            {
                var sk = S.Get(id); if (!S.Ready(sk)) continue;
                if (sk.kind == "heal" && H.hp < H.maxHp * 0.5f) { S.CastSelfPublic(sk); return; }
                if (!fighting) continue;
                if (sk.kind == "shield" && H.shield <= 0) { S.CastSelfPublic(sk); return; }
                if (sk.kind == "buff" && !((sk.buff == "atk" ? H.atkBuff : H.hasteBuff) > 0)) { S.CastSelfPublic(sk); return; }
            }
        }
        static void Vials()
        {
            if (H.hp < H.maxHp * HP_VIAL) H.DrinkVial(true, true);
            bool fighting = H.target != null && !H.target.dead; if (!fighting) return;
            var S = AldaraSkills.I; var costs = S ? S.equipped.Select(id => S.Get(id)).Where(s => s != null).Select(s => s.cost).ToList() : new System.Collections.Generic.List<float>();
            float need = costs.Count > 0 ? costs.Min() : 0;
            if (H.mana < need * 0.5f || H.mana < H.maxMana * MP_VIAL) H.DrinkVial(false, true);
        }
        /// pickAutoSkill: the first ready attack skill in slot order
        public static SkillDef PickSkill(AldaraMonsters.Mon t)
        {
            var S = AldaraSkills.I; if (!S) return null;
            foreach (var id in S.equipped)
            {
                var sk = S.Get(id); if (!S.Ready(sk)) continue;
                if (sk.kind == "target") return sk;
                if (sk.kind == "self_aoe" && AldaraMonsters.I.all.Any(m => !m.dead && Dist(m.x, m.y, P.x, P.y) <= sk.radius + m.r)) return sk;
            }
            return null;
        }
    }
}
