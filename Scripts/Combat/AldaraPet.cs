using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // The pet (the browser's pet / updatePet / drawPet3): it follows behind you, joins your fights (melee pets run in and
    // bite, ranged pets shoot bolts or fire from beside you, healers mend you), hits for a share of your attack by
    // rarity, and mythical beasts hit an area. Drawn with the browser's own models; glow and sparkles are drawn by
    // AldaraHeroFx.
    public class AldaraPet : MonoBehaviour
    {
        public static AldaraPet I;
        public float x, y, t, cd, healCd = 2, lunge, lungeA, face = Mathf.PI / 2; public bool placed;
        public AldaraMonsters.Mon target;
        public JObject info; public JObject meta; public string petName;
        GameObject view; Transform model; AldaraMonsterAnimator anim; string built; float lx, ly;
        static JObject book; public static JObject Book { get { if (book == null) book = JObject.Parse(Resources.Load<TextAsset>("pets_meta").text); return book; } }
        class Shot { public GameObject go; public float x, y, tx, ty; public AldaraMonsters.Mon t; public float mult; public bool fire; }
        readonly List<Shot> shots = new List<Shot>();
        void Awake() { I = this; }
        static AldaraHero H { get { return AldaraHero.I; } }
        static AldaraPlayer P { get { return AldaraPlayer.I; } }
        public bool Myth { get { return info != null && info["myth"] != null && (bool)info["myth"]; } }
        string Role { get { return info != null && info["role"] != null ? (string)info["role"] : "melee"; } }
        float Power(Item it) { var p = Book["power"][it.rarity]; return p != null ? (float)p : 0.25f; }

        AldaraMonsters.Mon PickTarget()
        {
            var tg = H.target; if (tg != null && !tg.dead && Dist(tg.x, tg.y, P.x, P.y) < 500) return tg;
            AldaraMonsters.Mon best = null; float bd = 320;
            foreach (var m in AldaraMonsters.I.all) { if (m.dead) continue; float d = Dist(m.x, m.y, P.x, P.y); if (!(m.aggroT > 0 || d < 160)) continue; if (d < bd) { bd = d; best = m; } }
            return best;
        }
        void Hit(AldaraMonsters.Mon m, float mult, Item it)
        {
            float dmg = Mathf.Max(1, Mathf.Round(H.atk * Power(it) * mult * (0.8f + Random.value * 0.4f))); var col = AldaraRules.Hex("#ffb0e0");
            float aoe = info != null && info["aoe"] != null ? (float)info["aoe"] : 0;
            if (aoe > 0) { AldaraFx.Ring(m.x, m.y, aoe, it.Col, 0.35f); foreach (var o in AldaraMonsters.I.all.ToArray()) if (!o.dead && Dist(o.x, o.y, m.x, m.y) < aoe) AldaraMonsters.I.HitMonster(o, dmg, col); }
            else AldaraMonsters.I.HitMonster(m, dmg, col);
            if (info != null && info["slow"] != null && !m.dead) m.slowT = 1.5f;
        }
        static float Dist(float ax, float ay, float bx, float by) { return Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)); }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            var it = AldaraSave.Ready && H && P ? H.Eq("pet") : null;
            if (it == null) { placed = false; Hide(); shots.ForEach(s => { if (s.go) Destroy(s.go); }); shots.Clear(); return; }
            if (petName != it.name) { petName = it.name; meta = Book["pets"][it.name] as JObject; info = meta != null ? meta["info"] as JObject : null; }
            t += dt; if (lunge > 0) lunge -= dt;
            float back = P.facing + Mathf.PI * 0.75f, dist = Myth ? 60 : 34;
            float tx = P.x + Mathf.Cos(back) * dist, ty = P.y + Mathf.Sin(back) * dist;
            if (!placed || Dist(tx, ty, x, y) > 700) { x = tx; y = ty; placed = true; lx = x; ly = y; }
            bool follow = true;
            if (!H.alive) target = null;
            else
            {
                cd -= dt; healCd -= dt;
                if (target == null || target.dead || Dist(target.x, target.y, P.x, P.y) > 520) target = PickTarget();
                // healers keep you alive
                if (Role == "heal" && healCd <= 0 && H.hp < H.maxHp * 0.9f)
                {
                    healCd = Myth ? 2.5f : 3.5f; float amt = Mathf.Round(H.maxHp * Mathf.Min(0.12f, 0.02f * Power(it) * 4));
                    H.hp = Mathf.Min(H.maxHp, H.hp + amt); AldaraFx.Text(P.x + 10, P.y - 16 - 30, "+" + amt + " HP", AldaraRules.Hex("#7fe07f")); AldaraFx.Burst(P.x, P.y - 6, 30, AldaraRules.Hex("#9fffa0"));
                }
                var tg = target;
                if (tg != null)
                {
                    float d = Dist(tg.x, tg.y, x, y);
                    if (Role == "melee")
                    {
                        float reach = tg.r + (Myth ? 40 : 16);
                        if (d > reach) { float a = Mathf.Atan2(tg.y - y, tg.x - x), sp = (Myth ? 300 : 330) * dt; x += Mathf.Cos(a) * sp; y += Mathf.Sin(a) * sp; }
                        else if (cd <= 0) { cd = Myth ? 1.1f : 0.8f; lunge = 0.2f; lungeA = Mathf.Atan2(tg.y - y, tg.x - x); Hit(tg, 1, it); AldaraFx.Burst(tg.x, tg.y, 20, AldaraRules.Hex("#ffb0e0")); }
                        follow = false; if (AldaraDungeon.Active) AldaraDungeon.Push(ref x, ref y, 8, 0, 0, false);
                    }
                    else if (d < 400 && cd <= 0)
                    {   // ranged and healer pets shoot from beside you
                        cd = Role == "heal" ? (Myth ? 1.4f : 1.8f) : (Myth ? 1.2f : 1.1f);
                        bool fire = info != null && (string)info["shot"] == "fire";
                        var go = AldaraFx.Orb(fire ? AldaraRules.Hex("#ffb14a") : it.Col, 0.28f);
                        shots.Add(new Shot { go = go, x = x, y = y - (Myth ? 30 : 12), t = tg, tx = tg.x, ty = tg.y, mult = Role == "heal" ? 0.5f : 1, fire = fire });
                    }
                }
            }
            if (follow) { float k = Mathf.Min(1, dt * 4); x += (tx - x) * k; y += (ty - y) * k; }
            // pet shots fly at 520 px/s
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i]; if (s.t != null && !s.t.dead) { s.tx = s.t.x; s.ty = s.t.y; }
                float dx = s.tx - s.x, dy = s.ty - s.y, dd = Mathf.Sqrt(dx * dx + dy * dy), st = 520 * dt;
                if (dd <= st + 8) { if (s.t != null && !s.t.dead) { Hit(s.t, s.mult, it); AldaraFx.Burst(s.t.x, s.t.y, 22, s.fire ? AldaraRules.Hex("#ffb14a") : AldaraRules.Hex("#cfe0ff")); } if (s.go) Destroy(s.go); shots.RemoveAt(i); continue; }
                s.x += dx / dd * st; s.y += dy / dd * st; if (s.go) s.go.transform.position = AldaraWorld.ToUnity(s.x, s.y) + Vector3.up * 0.9f;
            }
            Show(dt);
        }

        // ---- the body ----
        public float Hover { get; private set; }
        void Show(float dt)
        {
            if (meta == null) { Hide(); return; }
            string id = (string)meta["id"];
            if (built != id || !view)
            {
                if (view) Destroy(view); built = id;
                var pf = Resources.Load<GameObject>("PetPrefabs/Pet_" + id); if (!pf) return;
                view = new GameObject("Pet"); view.transform.localScale = AldaraView.Squash;
                var g = Instantiate(pf, view.transform, false); model = g.transform; anim = g.GetComponent<AldaraMonsterAnimator>();
            }
            if (!view.activeSelf) view.SetActive(true);
            float dx = x - lx, dy = y - ly, d = Mathf.Sqrt(dx * dx + dy * dy); lx = x; ly = y;
            bool moving = d > 0.4f * dt * 60; if (moving) face = Mathf.Atan2(dy, dx);
            if (!moving && target != null && !target.dead) face = Mathf.Atan2(target.y - y, target.x - x);
            if (lunge > 0) face = lungeA;
            float lk = lunge > 0 ? Mathf.Sin(Mathf.PI * (1 - lunge / 0.2f)) * 8 : 0;
            float px = x + Mathf.Cos(lungeA) * lk, py = y + Mathf.Sin(lungeA) * lk, pv = (float)meta["pv"];
            bool PW = (int)meta["winged"] != 0, fl = (int)meta["floater"] != 0;
            float tt = Time.time; Hover = PW ? Mathf.Max(2, (8 - Mathf.Cos(tt * 4.4f) * 3.5f) * pv * (Myth ? 1.6f : 1)) : 0;
            var pos = AldaraWorld.ToUnity(px, py + 12); pos.y += Hover / AldaraWorld.PX; view.transform.position = pos;
            if (model) model.localRotation = Quaternion.Euler(0, 90 + face * Mathf.Rad2Deg, 0);
            if (anim)
            {
                if (lunge > 0) anim.SetAction(0, 0.2f - lunge); else anim.ClearAction();
                if (anim.Has("move")) anim.SetBase(moving ? "move" : "idle", 0);
                else if (moving && !fl && anim.Has("walk")) anim.SetBase("walk", Mathf.Min(d, 12 * dt * 60) / Mathf.Max(dt, 1e-4f) * 0.28f / 60f * 60f / (Mathf.PI * 2));
                else anim.SetBase("idle", 0);
            }
        }
        void Hide() { if (view && view.activeSelf) view.SetActive(false); }
        public Vector3 Ground { get { return AldaraWorld.ToUnity(x, y + 12) + Vector3.up * Hover / AldaraWorld.PX; } }
        public bool Shown { get { return view && view.activeSelf; } }
    }
}
