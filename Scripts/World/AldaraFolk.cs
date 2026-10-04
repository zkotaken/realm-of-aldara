using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // The people of Lorenmar and Valcrest (the browser's townFolk / kdFolk with updateTown / kdUpdate): walkers and dogs
    // wander the street graph, guards patrol and stand at the gates, children chase round the fountain, smiths hammer,
    // criers shout the news. Also the quest givers at the outposts and in the cities, and the bounty boards.
    // Each one is the browser's own model, baked with its walk and idle like the monsters.
    public class AldaraFolk : MonoBehaviour
    {
        public static AldaraFolk I;
        public class Node { public float x, y; public int[] nb; }
        public class Squad { public int[] ids; public bool loop; public int i, dir; public float spd, hx, hy = 1; public List<Folk> m = new List<Folk>(); }
        public class Folk
        {
            public string name, role, id; public float x, y, spd, wait, face = float.NaN, face3 = Mathf.PI / 2, walkT, ang, rad, w, cx, cy, shout, hit, lunge, spark, dx, dy, top;
            public int at, to, prev = -1, ri, dir = 1, k; public bool guard, hidden, kd, guide, board; public Squad sq; public string npc;
            public GameObject view; public Transform model; public AldaraMonsterAnimator anim;
        }
        public readonly List<Folk> town = new List<Folk>(), kd = new List<Folk>(), guides = new List<Folk>();
        Node[] tG, kG; int[] P2; Vector2 C, KC; float TR, KEXT;
        readonly List<Squad> squads = new List<Squad>();
        static JObject meta;
        static readonly string[] TOWN_CRY = { "Hear ye! Monsters stir in the Shadowfen!", "Fresh bread at the bakery!", "Dungeons await the brave, level 20 and up!", "The Gilded Tankard pours the finest ale!", "Keep to the roads after dark!" };
        string[] KD_CRY;

        void Awake() { I = this; }
        void Start()
        {
            AldaraQuests.Load(); var J = AldaraQuests.J; meta = JObject.Parse(Resources.Load<TextAsset>("folk").text);
            tG = J["town"]["graph"].ToObject<Node[]>(); kG = J["kd"]["graph"].ToObject<Node[]>(); P2 = J["town"]["P2"].ToObject<int[]>();
            C = new Vector2((float)J["town"]["C"]["x"], (float)J["town"]["C"]["y"]); TR = (float)J["town"]["R"];
            KC = new Vector2((float)J["kd"]["C"]["x"], (float)J["kd"]["C"]["y"]); KEXT = (float)J["kd"]["EXT"]; KD_CRY = J["kd"]["cry"].ToObject<string[]>();
            foreach (var s in J["kd"]["squads"]) squads.Add(new Squad { ids = s["ids"].ToObject<int[]>(), loop = (int)s["loop"] != 0, i = (int)s["i"], dir = (int)s["dir"], spd = (float)s["spd"] });
            foreach (var o in J["town"]["folk"]) town.Add(Make((JObject)o, false));
            foreach (var o in J["kd"]["folk"]) { var f = Make((JObject)o, true); kd.Add(f); if (o["sq"] != null) { var S = squads[(int)o["sq"]]; f.sq = S; S.m.Add(f); } }
            // quest givers who are not townsfolk, and the boards
            foreach (var kv in AldaraQuests.QOBJ)
            {
                var q = kv.Value; if (q.folk >= 0) { town[q.folk].npc = kv.Key; continue; }
                if (q.far != 0) continue;
                var f = new Folk { name = q.look, role = "stand", x = q.x, y = q.y, face = q.face ?? Mathf.PI / 2, guide = q.q != 0, board = q.board != 0, npc = kv.Key };
                f.id = f.board ? null : Id(f.name); f.top = Top(f.name); guides.Add(f);
            }
            ApplyCrowd();
        }
        /// someone standing in an interior (the throne room's court)
        public Folk NewFolk(string name, float x, float y) { var f = new Folk { name = name, role = "stand", x = x, y = y, walkT = Random.value * 6 }; f.id = Id(f.name); f.top = Top(f.name); return f; }
        static string Id(string name) { if (name == null) return null; var m = meta[name]; return m != null ? (string)m["id"] : null; }
        static float Top(string name) { if (name == null) return 48; var m = meta[name]; return m != null ? (float)m["top"] * (float)m["unitW"] : 60; }
        Folk Make(JObject o, bool isKd)
        {
            var f = new Folk { name = (string)o["name"], role = (string)o["role"], x = (float)o["x"], y = (float)o["y"], kd = isKd };
            f.at = (int?)o["at"] ?? 0; f.to = (int?)o["to"] ?? 0; f.spd = (float?)o["spd"] ?? 34; f.wait = (float?)o["wait"] ?? 0; if (o["face"] != null) f.face = (float)o["face"];
            f.ang = (float?)o["ang"] ?? 0; f.rad = (float?)o["rad"] ?? 0; f.w = (float?)o["w"] ?? 1; f.cx = (float?)o["cx"] ?? 0; f.cy = (float?)o["cy"] ?? 0;
            f.ri = (int?)o["ri"] ?? 0; f.dir = (int?)o["dir"] ?? 1; f.k = (int?)o["k"] ?? 0; f.guard = o["guard"] != null; f.shout = (float?)o["shout"] ?? 5; f.hit = (float?)o["hit"] ?? 0;
            f.walkT = Random.value * 6; f.id = Id(f.name); f.top = Top(f.name); return f;
        }
        /// SET.crowd: all, half or none (the tagged people always stay)
        public void ApplyCrowd()
        {
            string crowd = (string)AldaraSettings.Get("crowd") ?? "all";
            for (int i = 0; i < town.Count; i++) { var n = town[i]; bool tag = AldaraQuests.FOLK_TAG.ContainsKey(n.name); n.hidden = crowd == "none" ? !tag : crowd == "half" ? (i % 2 == 1 && !tag) : false; }
            for (int i = 0; i < kd.Count; i++) { var n = kd[i]; bool am = n.role == "walk" || n.role == "dog" || n.role == "kid"; n.hidden = crowd == "none" ? am : crowd == "half" ? ((n.role == "walk" || n.role == "dog") && i % 2 == 1) : false; }
        }
        /// where a quest giver stands now (townsfolk move)
        public static Vector2? Pos(string npc)
        {
            var tp = AldaraThrone.Pos(npc); if (tp.HasValue) return tp;
            var o = AldaraQuests.NpcPos(npc); if (o == null) return null;
            if (I != null && o.folk >= 0 && o.folk < I.town.Count) { var f = I.town[o.folk]; return new Vector2(f.x, f.y); }
            return new Vector2(o.x, o.y);
        }
        public bool TownNear(float x, float y) { return Mathf.Abs(x - C.x) < TR + 900 && Mathf.Abs(y - C.y) < TR + 700; }
        public bool KdNear(float x, float y) { return Mathf.Abs(x - KC.x) < KEXT + 900 && Mathf.Abs(y - KC.y) < KEXT + 700; }

        void Update()
        {
            var P = AldaraPlayer.I; if (!AldaraSave.Ready || P == null || AldaraWorld.Dun)
            {
                HideAll();
                if (P != null && AldaraThrone.On) { float dt0 = Mathf.Min(Time.deltaTime, 0.1f); foreach (var n in AldaraThrone.court) Show(n, dt0); }
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (TownNear(P.x, P.y)) Sim(town, tG, dt, false); else HideList(town);
            if (KdNear(P.x, P.y)) Sim(kd, kG, dt, true); else HideList(kd);
            foreach (var g in guides)
            {
                float d = Dist(g, P.x, P.y);
                if (g.guide && d < 260) g.face = Mathf.Atan2(P.y - g.y, P.x - g.x);
                g.dx = g.dy = 0; if (d < 1600) Show(g, dt); else Hide(g);
            }
            AldaraQuests.Update(dt);
        }
        static float Dist(Folk f, float x, float y) { return Mathf.Sqrt((f.x - x) * (f.x - x) + (f.y - y) * (f.y - y)); }
        void Sim(List<Folk> L, Node[] N, float dt, bool isKd)
        {
            var P = AldaraPlayer.I; float now = Time.time;
            foreach (var n in L)
            {
                float ox = n.x, oy = n.y;
                switch (n.role)
                {
                    case "walk": case "dog":
                        {
                            if (n.wait > 0) { n.wait -= dt; break; }
                            var tg = N[n.to]; float dx = tg.x - n.x, dy = tg.y - n.y, d = Mathf.Sqrt(dx * dx + dy * dy), sp = n.spd * dt;
                            if (d <= sp)
                            {
                                n.x = tg.x; n.y = tg.y; var nb = new List<int>(); foreach (var k in tg.nb) if (k != n.prev) nb.Add(k); if (nb.Count == 0) nb.AddRange(tg.nb);
                                n.prev = n.to; n.at = n.to; n.to = nb.Count > 0 ? nb[Random.Range(0, nb.Count)] : n.to;
                                if (Random.value < (n.role == "dog" ? 0.1f : isKd ? 0.2f : 0.22f)) n.wait = 1 + Random.value * (n.role == "dog" ? 2 : isKd ? 4 : 4.5f);
                            }
                            else { n.x += dx / d * sp; n.y += dy / d * sp; }
                            break;
                        }
                    case "patrol":
                        {
                            int L2 = P2.Length; var tg = N[P2[((n.ri % L2) + L2) % L2]]; float dx = tg.x - n.x, dy = tg.y - n.y, d = Mathf.Sqrt(dx * dx + dy * dy), sp = n.spd * dt;
                            if (d <= sp) n.ri = (n.ri + n.dir + L2) % L2; else { n.x += dx / d * sp; n.y += dy / d * sp; }
                            break;
                        }
                    case "plead":
                        {
                            var S = n.sq; var tg = N[S.ids[S.i]]; float dx = tg.x - n.x, dy = tg.y - n.y, d = Mathf.Sqrt(dx * dx + dy * dy), sp = S.spd * dt;
                            if (d <= sp) { n.x = tg.x; n.y = tg.y; int j = S.i + S.dir; if (S.loop) j = (j + S.ids.Length) % S.ids.Length; else if (j < 0 || j >= S.ids.Length) { S.dir = -S.dir; j = S.i + S.dir; } S.i = j; }
                            else { n.x += dx / d * sp; n.y += dy / d * sp; S.hx = dx / d; S.hy = dy / d; }
                            break;
                        }
                    case "pfol":
                        {
                            var S = n.sq; var Ld = S.m[0]; float hx = S.hx, hy = S.hy; int side = n.k % 2 == 1 ? 1 : -1; float back = 30 * Mathf.Ceil(n.k / 2f) * 1.2f;
                            float tx = Ld.x - hx * back - hy * side * 16, ty = Ld.y - hy * back + hx * side * 16, dx = tx - n.x, dy = ty - n.y, d = Mathf.Sqrt(dx * dx + dy * dy), sp = S.spd * 1.5f * dt;
                            if (d > 400) { n.x = tx; n.y = ty; } else if (d > 2) { float m = Mathf.Min(d, sp); n.x += dx / d * m; n.y += dy / d * m; }
                            break;
                        }
                    case "kid":
                        {
                            n.ang += n.w * dt * (1 + 0.3f * Mathf.Sin(now * 0.7f + n.rad));
                            if (isKd) { n.x = n.cx + Mathf.Cos(n.ang) * n.rad; n.y = n.cy + Mathf.Sin(n.ang) * n.rad * 0.9f; } else { n.x = C.x + Mathf.Cos(n.ang) * n.rad; n.y = C.y + Mathf.Sin(n.ang) * n.rad * 0.92f; }
                            break;
                        }
                    case "smith":
                        n.hit -= dt; if (n.hit <= 0) { n.hit = 1.15f; n.lunge = 0.2f; n.spark = 0.001f; }
                        if (n.lunge > 0) n.lunge -= dt; if (n.spark > 0) { n.spark += dt * 2.5f; if (n.spark > 1) n.spark = 0; }
                        break;
                    case "crier":
                        n.shout -= dt;
                        if (n.shout <= 0)
                        {
                            n.shout = (isKd ? 9 + Random.value * 7 : 9 + Random.value * 6);
                            if (Dist(n, P.x, P.y) < (isKd ? 460 : 420)) { var cry = isKd ? KD_CRY : TOWN_CRY; AldaraFx.Text(n.x, n.y - (isKd ? 72 : 70), cry[Random.Range(0, cry.Length)], AldaraRules.Hex("#ffe8a0")); }
                        }
                        break;
                    case "stand":
                        if (n.guard && Dist(n, P.x, P.y) < 120) n.face = Mathf.Atan2(P.y - n.y, P.x - n.x);
                        break;
                }
                n.dx = n.x - ox; n.dy = n.y - oy;
                if (!n.hidden && Dist(n, P.x, P.y) < 1500) Show(n, dt); else Hide(n);
            }
        }

        // ---- the bodies ----
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static GameObject Prefab(string key)
        {
            if (key == null) return null; GameObject p; if (prefabs.TryGetValue(key, out p)) return p;
            p = Resources.Load<GameObject>("MonPrefabs/" + key); prefabs[key] = p; return p;
        }
        void Show(Folk n, float dt)
        {
            if (!n.view)
            {
                var pf = Prefab(n.board ? "QBoard" : "Mon_" + n.id); if (!pf) return;
                n.view = new GameObject(n.npc ?? n.name); n.view.transform.SetParent(transform, false); n.view.transform.localScale = AldaraView.Squash;
                var inst = Instantiate(pf, n.view.transform, false); n.model = inst.transform; n.anim = inst.GetComponent<AldaraMonsterAnimator>();
                if (n.board) n.model.localRotation = Quaternion.Euler(0, 0.25f * Mathf.Rad2Deg, 0);
            }
            if (!n.view.activeSelf) n.view.SetActive(true);
            bool moving = n.dx * n.dx + n.dy * n.dy > 0.001f;
            if (moving) n.face3 = Mathf.Atan2(n.dy, n.dx); else if (!float.IsNaN(n.face) && n.role != "walk") n.face3 = n.face;
            float mv = Mathf.Sqrt(n.dx * n.dx + n.dy * n.dy);
            n.view.transform.position = AldaraWorld.ToUnity(n.x, n.board ? n.y : n.y + 15);
            if (n.board) return;
            if (n.model) n.model.localRotation = Quaternion.Euler(0, 90 + n.face3 * Mathf.Rad2Deg, 0);
            if (n.anim)
            {
                if (n.lunge > 0) n.anim.SetAction(0, 0.2f - n.lunge); else n.anim.ClearAction();
                if (moving && n.anim.Has("walk")) n.anim.SetBase("walk", mv / Mathf.Max(dt, 1e-4f) * 0.11f * (n.role == "kid" ? 1.4f : 1) / (Mathf.PI * 2));
                else n.anim.SetBase("idle", 0);
            }
        }
        static void Hide(Folk n) { if (n.view && n.view.activeSelf) n.view.SetActive(false); }
        static void HideList(List<Folk> L) { foreach (var n in L) Hide(n); }
        void HideAll() { HideList(town); HideList(kd); HideList(guides); }
        /// everyone with a name tag or a quest, for labels and clicks
        public IEnumerable<Folk> Visible()
        {
            foreach (var n in town) if (n.view && n.view.activeSelf) yield return n;
            foreach (var n in kd) if (n.view && n.view.activeSelf) yield return n;
            foreach (var n in guides) if (n.view && n.view.activeSelf) yield return n;
            foreach (var n in AldaraThrone.court) if (n.view && n.view.activeSelf) yield return n;
        }
        public Folk Of(string npc) { foreach (var g in guides) if (g.npc == npc) return g; foreach (var t in town) if (t.npc == npc) return t; return null; }
    }
}
