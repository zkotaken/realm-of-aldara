using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // Mounts: creatures you own and ride. Press the mount key (V) to call yours and climb into the saddle, press it
    // again to get down. Riding is faster than running on foot; attacking, casting or being hurt puts you back on your
    // feet, and there is no riding in dungeons or in lava. The first mount is the Bay Horse. Owned mounts and the one you
    // ride are kept in the save ("mounts", "mount").
    public class AldaraMount : MonoBehaviour
    {
        public static AldaraMount I;
        public class Def { public string id, name, desc; public float speed; public Color coat, mane, legs, blanket, trim; }
        public static readonly List<Def> ALL = new List<Def> {
            new Def { id = "horse", name = "Bay Horse", desc = "A steady bay horse from the stables of Lorenmar.", speed = 1.0f,
                coat = new Color(0.45f, 0.27f, 0.14f), mane = new Color(0.09f, 0.075f, 0.07f), legs = new Color(0.12f, 0.1f, 0.09f), blanket = new Color(0.55f, 0.12f, 0.12f), trim = new Color(0.88f, 0.68f, 0.28f) } };
        public static Def Get(string id) { foreach (var d in ALL) if (d.id == id) return d; return null; }

        public static readonly List<string> owned = new List<string>();
        public static string active;
        public static bool Riding { get { return I && I.riding; } }
        /// how fast a rider goes, as a share of the hero's base speed (on foot running is 0.88)
        public static float SpeedK { get { if (!Riding) return 1; var d = Get(I.riding ? I.ridingId : active); return d != null ? d.speed : 1; } }

        // ---------- the save ----------
        public static void Load(JObject j)
        {
            owned.Clear(); active = null; if (I) I.Dismount(false);
            if (j != null && j["mounts"] is JArray a) foreach (var t in a) { var id = (string)t; if (Get(id) != null && !owned.Contains(id)) owned.Add(id); }
            if (j != null && j["mount"] != null && j["mount"].Type == JTokenType.String) { var id = (string)j["mount"]; if (owned.Contains(id)) active = id; }
            if (active == null && owned.Count > 0) active = owned[0];
        }
        public static void Write(JObject j) { j["mounts"] = new JArray(owned.ToArray()); j["mount"] = active; }
        public static void Give(string id)
        {
            var d = Get(id); if (d == null || owned.Contains(id)) return; owned.Add(id); if (active == null) active = id;
            AldaraHud.Banner("New mount: " + d.name + ". Press " + AldaraKeys.Name(AldaraKeys.KeyOf("mount")) + " to ride"); AldaraSave.Dirty();
        }
        public static void Take(string id) { if (I && I.riding && I.ridingId == id) I.Dismount(true); owned.Remove(id); if (active == id) active = owned.Count > 0 ? owned[0] : null; AldaraSave.Dirty(); }
        public static void Choose(string id) { if (!owned.Contains(id)) return; active = id; if (I && I.riding && I.ridingId != id) { I.Dismount(false); I.Mount(); } AldaraSave.Dirty(); }

        // ---------- riding ----------
        bool riding; string ridingId; GameObject horse; Horse rig; float phase, idleT, appear, lx, ly, spdPx, gallop;
        static AldaraPlayer P { get { return AldaraPlayer.I; } }
        static AldaraHero H { get { return AldaraHero.I; } }

        void Awake() { I = this; }
        public void Toggle() { if (riding) Dismount(true); else Mount(); }
        public void Mount()
        {
            if (riding || !P || !H) return;
            if (active == null) { AldaraHud.Banner("You have no mount"); return; }
            if (!H.alive) return;
            if (AldaraDungeon.Active) { AldaraHud.Banner("You cannot ride in here"); return; }
            if (P.liq == 2) { AldaraHud.Banner("Your mount will not go into lava"); return; }
            if (Fighting()) { AldaraHud.Banner("You cannot mount while fighting"); return; }
            ridingId = active; riding = true; appear = 0; lx = P.x; ly = P.y; spdPx = 0; Build(Get(ridingId));
            Puff();
        }
        public void Dismount(bool fx)
        {
            if (!riding) { if (horse) Destroy(horse); horse = null; return; }
            riding = false; if (fx) Puff();
            if (horse) Destroy(horse); horse = null; rig = null;
            if (P && P.model) P.model.localPosition = Vector3.zero;
            if (P && P.anim) P.anim.seated = false;
        }
        /// the hero model's thigh and knee nodes (hero_<id>_meta info.rig 9, 10, 12, 13)
        static int[] SeatNodes()
        {
            try
            {
                string id = P.model.name.Replace("(Clone)", "").Replace("Hero_", "").Trim(); var ta = Resources.Load<TextAsset>("Gear/hero_" + id + "_meta"); if (!ta) return null;
                var rg = JObject.Parse(ta.text)["info"]["rig"]; return new[] { (int)rg["9"], (int)rg["10"], (int)rg["12"], (int)rg["13"] };
            }
            catch { return null; }
        }
        bool Fighting() { return H.swing != null || H.cast > 0 || H.release > 0 || Time.time - AldaraPlayer.lastCombat < 0.5f; }
        void Puff()
        {
            if (!P) return; var at = AldaraWorld.ToUnity(P.x, P.y + 18);
            if (AldaraVfxPlus.On) { AldaraVfxPlus.Dust(at, 0.9f, 14, new Color(0.5f, 0.45f, 0.36f, 0.5f)); AldaraVfxPlus.Motes(at + Vector3.up * 0.6f, new Color(1f, 0.92f, 0.7f), 10, 0.6f, 1f); }
            AldaraVfx.Burst(P.x, P.y - 10, AldaraRules.Hex("#e8d8b0"), 14, 140);
        }

        void Update()
        {
            if (!AldaraSave.Ready || !P || !H) { if (riding) Dismount(false); return; }
            if (AldaraKeys.Pressed("mount")) Toggle();
            if (!riding) return;
            if (!H.alive || AldaraDungeon.Active || P.liq == 2 || Fighting() || !owned.Contains(ridingId)) { Dismount(true); return; }
        }
        void LateUpdate()
        {
            if (!riding || !horse || !P) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            // the horse turns with the rider and is drawn like the other creatures (squashed in depth)
            horse.transform.localPosition = new Vector3(0, P.sink > 0.6f ? -P.sink * 0.5f / AldaraWorld.PX : 0, 0);
            if (P.model) horse.transform.localRotation = P.model.localRotation;
            appear = Mathf.Min(1, appear + dt * 5); horse.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, 1 - (1 - appear) * (1 - appear));
            // ground covered, smoothed
            float mv = Mathf.Sqrt((P.x - lx) * (P.x - lx) + (P.y - ly) * (P.y - ly)) / Mathf.Max(dt, 1e-4f); lx = P.x; ly = P.y; if (mv > 2000) mv = 0;
            spdPx = Mathf.Lerp(spdPx, P.Moving ? mv : 0, 1 - Mathf.Exp(-8 * dt));
            Animate(dt);
            // the rider in the saddle
            if (P.model)
            {   // the seat, in the hero's own space (the model's hips stand 0.83 above its feet)
                var seat = P.transform.InverseTransformPoint(rig.seat.position);
                P.model.localPosition = seat - new Vector3(0, 0.72f, 0);
            }
            if (P.anim) { if (P.anim.seatNodes == null || P.anim.seatNodes.Length != 4) P.anim.seatNodes = SeatNodes(); P.anim.seated = true; }
        }

        // ---------- the horse: built from rounded parts, its legs moved in walk, trot and gallop ----------
        class Horse { public Transform root, body, neck, head, tail, tail2, seat; public Transform[] hip = new Transform[4], knee = new Transform[4]; public Vector3 bodyRest; }
        const int LEATHER = 3, CLOTH = 4, HAIR = 6, IRON = 8, GOLD = 2, STEEL = 1;

        void Build(Def d)
        {
            if (horse) Destroy(horse); if (d == null) return;
            horse = new GameObject("Mount_" + d.id); horse.transform.SetParent(P.transform, false); horse.layer = P.gameObject.layer;
            Material mat = null; if (P.model) { var r = P.model.GetComponentInChildren<MeshRenderer>(); if (r) mat = r.sharedMaterial; }
            if (!mat) mat = new Material(Shader.Find("Aldara/VertexLit"));
            rig = new Horse(); var R = rig; R.root = horse.transform;
            var coat = d.coat; var dark = new Color(coat.r * 0.62f, coat.g * 0.58f, coat.b * 0.55f); var muzzle = new Color(coat.r * 0.55f, coat.g * 0.48f, coat.b * 0.44f);
            var saddle = new Color(0.34f, 0.19f, 0.09f); var steel = new Color(0.66f, 0.68f, 0.72f); var hoof = new Color(0.17f, 0.15f, 0.13f); var eye = new Color(0.04f, 0.03f, 0.03f);

            // body: barrel, chest and rump, the saddle and its blanket
            R.body = Node("body", R.root, new Vector3(0, 1.0f, 0)); R.bodyRest = R.body.localPosition;
            var m = new MB();
            m.Ellip(new Vector3(0, 0, 0), new Vector3(0.27f, 0.29f, 0.6f), coat, LEATHER);
            m.Ellip(new Vector3(0, 0.03f, 0.4f), new Vector3(0.26f, 0.3f, 0.3f), coat, LEATHER);
            m.Ellip(new Vector3(0, 0.05f, -0.42f), new Vector3(0.28f, 0.3f, 0.33f), coat, LEATHER);
            m.Ellip(new Vector3(0, -0.12f, 0.02f), new Vector3(0.22f, 0.2f, 0.5f), dark, LEATHER);   // the belly, shaded
            for (int s = -1; s <= 1; s += 2)
            {   // shoulders and haunches, where the legs join
                m.Ellip(new Vector3(s * 0.13f, -0.06f, 0.44f), new Vector3(0.14f, 0.24f, 0.17f), coat, LEATHER);
                m.Ellip(new Vector3(s * 0.14f, -0.02f, -0.46f), new Vector3(0.15f, 0.27f, 0.22f), coat, LEATHER);
            }
            m.Ellip(new Vector3(0, 0.27f, 0.0f), new Vector3(0.3f, 0.035f, 0.36f), d.blanket, CLOTH);
            m.Ellip(new Vector3(0, 0.255f, 0.0f), new Vector3(0.315f, 0.03f, 0.375f), d.trim, GOLD);
            m.Ellip(new Vector3(0, 0.32f, -0.02f), new Vector3(0.21f, 0.06f, 0.25f), saddle, LEATHER);
            m.Ellip(new Vector3(0, 0.37f, 0.18f), new Vector3(0.09f, 0.07f, 0.06f), saddle, LEATHER);   // the pommel
            m.Ellip(new Vector3(0, 0.37f, -0.22f), new Vector3(0.16f, 0.07f, 0.05f), saddle, LEATHER);  // the cantle
            for (int s = -1; s <= 1; s += 2)
            {   // the girth strap and the stirrups
                m.Tube(new Vector3(s * 0.25f, 0.24f, 0.05f), new Vector3(s * 0.25f, -0.24f, 0.08f), 0.025f, 0.025f, saddle, LEATHER);
                m.Tube(new Vector3(s * 0.24f, 0.28f, -0.02f), new Vector3(s * 0.31f, -0.08f, 0.0f), 0.012f, 0.012f, saddle, LEATHER);
                m.Ellip(new Vector3(s * 0.31f, -0.11f, 0.0f), new Vector3(0.02f, 0.035f, 0.06f), steel, STEEL);
            }
            Part("barrel", R.body, Vector3.zero, m, mat);
            R.seat = Node("seat", R.body, new Vector3(0, 0.33f, -0.03f));

            // neck and head
            R.neck = Node("neck", R.body, new Vector3(0, 0.16f, 0.55f));
            m = new MB();
            m.Tube(new Vector3(0, -0.05f, 0), new Vector3(0, 0.46f, 0.3f), 0.2f, 0.12f, coat, LEATHER, 12);
            for (int i = 0; i < 9; i++) { float u = i / 8f; m.Ellip(new Vector3(0, Mathf.Lerp(0.02f, 0.5f, u) + 0.06f, Mathf.Lerp(-0.12f, 0.24f, u)), new Vector3(0.045f, 0.09f, 0.07f), d.mane, HAIR); }
            Part("neckM", R.neck, Vector3.zero, m, mat);
            R.head = Node("head", R.neck, new Vector3(0, 0.5f, 0.3f));
            m = new MB();
            m.Ellip(new Vector3(0, 0, 0.02f), new Vector3(0.115f, 0.13f, 0.15f), coat, LEATHER);   // the poll and cheeks
            m.Tube(new Vector3(0, -0.02f, 0.06f), new Vector3(0, -0.2f, 0.36f), 0.105f, 0.075f, coat, LEATHER, 12);
            m.Ellip(new Vector3(0, -0.22f, 0.38f), new Vector3(0.08f, 0.075f, 0.075f), muzzle, LEATHER);
            m.Ellip(new Vector3(0, -0.02f, 0.26f), new Vector3(0.04f, 0.1f, 0.03f), new Color(0.92f, 0.9f, 0.86f), LEATHER);   // the white blaze
            for (int s = -1; s <= 1; s += 2)
            {
                m.Tube(new Vector3(s * 0.06f, 0.1f, -0.02f), new Vector3(s * 0.075f, 0.24f, -0.04f), 0.03f, 0.008f, coat, LEATHER, 8);   // ears
                m.Ellip(new Vector3(s * 0.098f, 0.0f, 0.12f), new Vector3(0.02f, 0.024f, 0.028f), eye, LEATHER);
                m.Ellip(new Vector3(s * 0.04f, -0.24f, 0.44f), new Vector3(0.016f, 0.014f, 0.012f), eye, LEATHER);   // nostrils
                m.Tube(new Vector3(s * 0.105f, 0.04f, 0.0f), new Vector3(s * 0.085f, -0.19f, 0.33f), 0.012f, 0.012f, saddle, LEATHER, 6);   // the bridle
                m.Ellip(new Vector3(s * 0.085f, -0.2f, 0.34f), new Vector3(0.015f, 0.025f, 0.025f), steel, STEEL);
            }
            m.Tube(new Vector3(-0.1f, -0.06f, 0.18f), new Vector3(0.1f, -0.06f, 0.18f), 0.012f, 0.012f, saddle, LEATHER, 6);
            m.Ellip(new Vector3(0, 0.12f, 0.1f), new Vector3(0.05f, 0.05f, 0.08f), d.mane, HAIR);   // the forelock
            Part("headM", R.head, Vector3.zero, m, mat);

            // the tail
            R.tail = Node("tail", R.body, new Vector3(0, 0.16f, -0.9f));
            m = new MB(); m.Tube(new Vector3(0, 0, 0), new Vector3(0, -0.22f, -0.12f), 0.055f, 0.07f, d.mane, HAIR, 10); Part("tailA", R.tail, Vector3.zero, m, mat);
            R.tail2 = Node("tail2", R.tail, new Vector3(0, -0.22f, -0.12f));
            m = new MB(); m.Tube(new Vector3(0, 0, 0), new Vector3(0, -0.42f, -0.06f), 0.07f, 0.03f, d.mane, HAIR, 10); Part("tailB", R.tail2, Vector3.zero, m, mat);

            // legs: front left, front right, hind left, hind right
            var hips = new[] { new Vector3(-0.13f, -0.08f, 0.45f), new Vector3(0.13f, -0.08f, 0.45f), new Vector3(-0.14f, -0.06f, -0.46f), new Vector3(0.14f, -0.06f, -0.46f) };
            for (int i = 0; i < 4; i++)
            {
                bool hind = i >= 2; R.hip[i] = Node("hip" + i, R.body, hips[i]);
                m = new MB(); float up = hind ? 0.46f : 0.42f;
                m.Tube(Vector3.zero, new Vector3(0, -up, hind ? -0.03f : 0.01f), hind ? 0.11f : 0.095f, 0.06f, coat, LEATHER, 10);
                Part("upper" + i, R.hip[i], Vector3.zero, m, mat);
                R.knee[i] = Node("knee" + i, R.hip[i], new Vector3(0, -up, hind ? -0.03f : 0.01f));
                m = new MB(); float lo = hind ? 0.44f : 0.46f;
                m.Tube(Vector3.zero, new Vector3(0, -lo, 0.02f), 0.05f, 0.042f, d.legs, LEATHER, 10);
                m.Ellip(new Vector3(0, -lo + 0.04f, 0.035f), new Vector3(0.055f, 0.045f, 0.06f), d.legs, LEATHER);   // the fetlock
                m.Tube(new Vector3(0, -lo + 0.01f, 0.04f), new Vector3(0, -lo - 0.06f, 0.06f), 0.05f, 0.062f, hoof, LEATHER, 10);   // the hoof
                Part("lower" + i, R.knee[i], Vector3.zero, m, mat);
            }
            foreach (var t in horse.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = P.gameObject.layer;
        }
        static Transform Node(string n, Transform parent, Vector3 at) { var g = new GameObject(n); g.transform.SetParent(parent, false); g.transform.localPosition = at; return g.transform; }
        void Part(string n, Transform parent, Vector3 at, MB m, Material mat)
        {
            var g = new GameObject(n); g.transform.SetParent(parent, false); g.transform.localPosition = at;
            g.AddComponent<MeshFilter>().sharedMesh = m.Done(n); var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        void Animate(float dt)
        {
            var R = rig; if (R == null) return; float t = Time.time;
            // gait from the speed: walk, trot, then a gallop
            float sp = spdPx, moving = Mathf.Clamp01(sp / 30f); gallop = Mathf.Lerp(gallop, Mathf.Clamp01((sp - 200) / 60f), 1 - Mathf.Exp(-4 * dt));
            float stride = Mathf.Lerp(70, 140, gallop); phase += dt * Mathf.PI * 2 * (sp / stride);
            float[] walkO = { 0, Mathf.PI, Mathf.PI * 0.5f, Mathf.PI * 1.5f }, galO = { 1.7f, 2.2f, 0, 0.5f };
            float amp = Mathf.Lerp(0.38f, 0.62f, gallop) * moving;
            for (int i = 0; i < 4; i++)
            {
                float o = Mathf.LerpAngle(walkO[i] * Mathf.Rad2Deg, galO[i] * Mathf.Rad2Deg, gallop) * Mathf.Deg2Rad, s = Mathf.Sin(phase + o), c = Mathf.Cos(phase + o);
                float swing = -amp * s;                                   // forward is negative about x
                float lift = Mathf.Max(0, c) * Mathf.Lerp(0.7f, 1.2f, gallop) * moving;   // the lower leg folds as it comes forward
                bool hind = i >= 2;
                R.hip[i].localRotation = Quaternion.Euler(swing * Mathf.Rad2Deg, 0, 0);
                R.knee[i].localRotation = Quaternion.Euler((hind ? -lift * 0.7f : lift) * Mathf.Rad2Deg, 0, 0);
            }
            // the body rides up and down, rocking in a gallop, breathing when it stands
            idleT += dt; float breathe = (1 - moving) * Mathf.Sin(t * 1.7f) * 0.006f;
            float bob = moving * (Mathf.Lerp(0.018f, 0.05f, gallop) * Mathf.Abs(Mathf.Sin(phase)) - 0.01f);
            R.body.localPosition = R.bodyRest + new Vector3(0, bob + breathe, 0);
            R.body.localRotation = Quaternion.Euler(gallop * moving * 4.5f * Mathf.Sin(phase + 0.6f), 0, 0);
            float graze = (1 - moving) * Mathf.Max(0, Mathf.Sin(t * 0.21f) - 0.7f) * 3.3f;   // now and then the head goes down a little
            R.neck.localRotation = Quaternion.Euler(-8 + moving * Mathf.Lerp(3f, 7f, gallop) * Mathf.Sin(phase * 2 + 0.5f) + graze * 22 + gallop * 10, 0, 0);
            R.head.localRotation = Quaternion.Euler(10 + (1 - moving) * Mathf.Sin(t * 0.9f) * 2 - graze * 6, (1 - moving) * Mathf.Sin(t * 0.37f) * 6, 0);
            R.tail.localRotation = Quaternion.Euler(-gallop * moving * 35 + 6, 0, Mathf.Sin(t * 1.3f) * (moving > 0.5f ? 6 : 12));
            R.tail2.localRotation = Quaternion.Euler(-gallop * moving * 20, 0, Mathf.Sin(t * 1.3f - 0.8f) * 10);
            if (gallop > 0.5f && AldaraVfxPlus.On && Random.value < dt * 6) AldaraVfxPlus.Dust(AldaraWorld.ToUnity(P.x, P.y + 18), 0.4f, 2, new Color(0.5f, 0.45f, 0.36f, 0.4f));
        }

        // ---------- smooth rounded parts with the hero shading's material codes ----------
        class MB
        {
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); readonly List<Color32> c = new List<Color32>(); readonly List<Vector2> e = new List<Vector2>(); readonly List<int> t = new List<int>();
            public void Ellip(Vector3 ctr, Vector3 r, Color col, int code, int seg = 14, int rings = 10)
            {
                int b = v.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float th = Mathf.PI * i / rings, sy = Mathf.Cos(th), sr = Mathf.Sin(th);
                    for (int j = 0; j <= seg; j++)
                    {
                        float ph = Mathf.PI * 2 * j / seg; var u = new Vector3(Mathf.Cos(ph) * sr, sy, Mathf.Sin(ph) * sr);
                        v.Add(ctr + Vector3.Scale(u, r)); n.Add(new Vector3(u.x / r.x, u.y / r.y, u.z / r.z).normalized); c.Add(col); e.Add(new Vector2(0, code));
                    }
                }
                for (int i = 0; i < rings; i++) for (int j = 0; j < seg; j++) { int a = b + i * (seg + 1) + j, d = a + seg + 1; t.Add(a); t.Add(d); t.Add(a + 1); t.Add(a + 1); t.Add(d); t.Add(d + 1); }
            }
            /// a tapered round limb from a to b with rounded ends
            public void Tube(Vector3 a, Vector3 bp, float ra, float rb, Color col, int code, int seg = 10)
            {
                var ax = bp - a; float L = ax.magnitude; if (L < 1e-4f) return; var dir = ax / L;
                var side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var side2 = Vector3.Cross(dir, side);
                int rings = 6, b = v.Count; float slope = (ra - rb) / L;
                for (int i = 0; i <= rings; i++)
                {
                    float u = i / (float)rings, rr = Mathf.Lerp(ra, rb, u); var p = a + ax * u;
                    for (int j = 0; j <= seg; j++)
                    {
                        float ph = Mathf.PI * 2 * j / seg; var o = side * Mathf.Cos(ph) + side2 * Mathf.Sin(ph);
                        v.Add(p + o * rr); n.Add((o + dir * slope).normalized); c.Add(col); e.Add(new Vector2(0, code));
                    }
                }
                for (int i = 0; i < rings; i++) for (int j = 0; j < seg; j++) { int q = b + i * (seg + 1) + j, d = q + seg + 1; t.Add(q); t.Add(q + 1); t.Add(d); t.Add(q + 1); t.Add(d + 1); t.Add(d); }
                Ellip(a, Vector3.one * ra, col, code, seg, 6); Ellip(bp, Vector3.one * rb, col, code, seg, 6);
            }
            public Mesh Done(string name)
            {
                var m = new Mesh { name = name }; if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetUVs(1, e); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
            }
        }
    }
}
