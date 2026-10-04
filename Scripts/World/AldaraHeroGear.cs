using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // What the hero wears, drawn the way the browser does it (buildP3 with its armour wrapper, GPU detail):
    // the weapon's own model, the armour pieces (the browser's ARM_DATA models) hung on the body's joints, the body
    // tucked in under the plates, the tunic skirt taken off under a chest piece, a bald head or a dark core under a
    // helm, fists in the gauntlets' colour, wings on the back, and cloth (skirt panels and capes) that swings with
    // the legs and the walk (plSecondary). A knight with a two-handed weapon uses the greatsword body.
    [DefaultExecutionOrder(100)]
    public class AldaraHeroGear : MonoBehaviour
    {
        public string baseId = "knight", cls = "knight";
        const float UK = 26f / 32f;
        static readonly string[] ARM_SLOTS = { "helmet", "chest", "gauntlets", "leggings", "boots", "back" };

        // ---- data ----
        class Base { public JObject meta; public AldaraGearPack pack; public Dictionary<string, int> rig = new Dictionary<string, int>(); public HashSet<string> great = new HashSet<string>(); }
        static readonly Dictionary<string, Base> bases = new Dictionary<string, Base>();
        static readonly Dictionary<string, JObject> pieces = new Dictionary<string, JObject>();
        static Base B(string id)
        {
            Base b; if (bases.TryGetValue(id, out b)) return b;
            var ta = Resources.Load<TextAsset>("Gear/hero_" + id + "_meta"); if (!ta) { bases[id] = null; return null; }
            b = new Base { meta = JObject.Parse(ta.text), pack = AldaraGearPack.Load("Gear/gear_" + id) };
            foreach (var kv in (JObject)b.meta["info"]["rig"]) b.rig[kv.Key] = (int)kv.Value;
            foreach (var kv in (JObject)b.meta["V"]["weapons"]) b.great.Add(kv.Key.Split('|')[0]);
            bases[id] = b; return b;
        }
        static JObject Pieces(string cls)
        {
            JObject p; if (pieces.TryGetValue(cls, out p)) return p;
            var ta = Resources.Load<TextAsset>("Gear/pieces_" + cls + "_meta"); p = ta ? JObject.Parse(ta.text) : new JObject(); pieces[cls] = p; return p;
        }
        static AldaraGearPack PiecePack(string cls) { return AldaraGearPack.Load("Gear/pieces_" + cls); }
        /// which body a hero needs: the knight swaps to the greatsword body for two-handed weapons
        public static string BaseFor(string cls, Item weapon)
        {
            if (cls != "knight") return cls;
            var g = B("knight_great"); return weapon != null && g != null && g.great.Contains(weapon.name) ? "knight_great" : "knight";
        }
        public static GameObject Prefab(string baseId) { return Resources.Load<GameObject>("Heroes/Hero_" + baseId); }

        // ---- the instance ----
        Transform[] T; Mesh[] baseMesh; Material mat; string sig;
        readonly List<GameObject> added = new List<GameObject>();
        readonly List<Transform> hidden = new List<Transform>();
        class Panel { public Transform n; public float a, len; }
        readonly List<Panel> panels = new List<Panel>(); readonly List<Transform> cape = new List<Transform>();
        class Wing { public Transform n, f; public int sd; public bool fairy; }
        readonly List<Wing> wings = new List<Wing>();
        AldaraCharacterAnimator anim;
        /// the browser's facing (ry) for the wings' turn
        public float ry;

        void Init()
        {
            if (T != null) return; var b = B(baseId); if (b == null) return;
            anim = GetComponent<AldaraCharacterAnimator>();
            var list = new Dictionary<int, Transform>(); int max = 0;
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.Length > 1 && t.name[0] == 'n' && int.TryParse(t.name.Substring(1), out int i)) { list[i] = t; max = Mathf.Max(max, i); }
            T = new Transform[max + 1]; baseMesh = new Mesh[max + 1];
            foreach (var kv in list) { T[kv.Key] = kv.Value; var mf = kv.Value.GetComponent<MeshFilter>(); if (mf) baseMesh[kv.Key] = mf.sharedMesh; var mr = kv.Value.GetComponent<MeshRenderer>(); if (mr && !mat) mat = mr.sharedMaterial; }
        }
        Transform Rig(int k) { var b = B(baseId); int i; return b != null && b.rig.TryGetValue(k.ToString(), out i) && i < T.Length ? T[i] : null; }
        void SetMesh(int i, Mesh m)
        {
            if (i < 0 || i >= T.Length || !T[i]) return; var t = T[i];
            var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
            if (!mf) { mf = t.gameObject.AddComponent<MeshFilter>(); mr = t.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; }
            mf.sharedMesh = m; if (mr) mr.enabled = m != null;
        }
        GameObject Child(Transform parent, string name, Mesh m, Vector3 pos)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = pos; added.Add(g);
            if (m) { g.AddComponent<MeshFilter>().sharedMesh = m; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; }
            return g;
        }

        static string Sig(Dictionary<string, Item> eq)
        {
            var s = new System.Text.StringBuilder();
            foreach (var k in new[] { "helmet", "chest", "gauntlets", "leggings", "boots", "back", "wings", "weapon" }) { Item it; if (eq != null && eq.TryGetValue(k, out it) && it != null) s.Append(it.name).Append(it.rarity); s.Append('|'); }
            return s.ToString();
        }
        /// dress the model in this equipment (call again whenever it changes; nothing happens if it did not)
        public bool Apply(Dictionary<string, Item> eq)
        {
            Init(); var b = B(baseId); if (b == null || T == null) return false;
            string sg = Sig(eq); if (sg == sig) return false; sig = sg;
            Item Eq(string k) { Item it; return eq != null && eq.TryGetValue(k, out it) ? it : null; }
            // back to the plain body
            foreach (var g in added) if (g) Destroy(g); added.Clear(); panels.Clear(); cape.Clear(); wings.Clear();
            foreach (var t in hidden) if (t) t.gameObject.SetActive(true); hidden.Clear();
            for (int i = 0; i < T.Length; i++) if (T[i]) { var mf = T[i].GetComponent<MeshFilter>(); if (mf || baseMesh[i]) SetMesh(i, baseMesh[i]); }
            var V = (JObject)b.meta["V"]; var I = (JObject)b.meta["info"];
            // the weapon
            var w = Eq("weapon");
            if (w != null && V["weapons"][w.name + "|" + w.rarity] is JArray wl) foreach (var i in wl) SetMesh((int)i, b.pack.Get("w_" + w.name + "|" + w.rarity + ":" + (int)i));
            // armour pieces
            var P = Pieces(cls); var PP = PiecePack(cls); var W = new Dictionary<string, JObject>();
            foreach (var sl in ARM_SLOTS) { var it = Eq(sl); if (it != null && P[it.name] is JObject pj && pj["missing"] == null) W[sl] = pj; }
            System.Func<string, int[], bool> has = (sl, ns) => { JObject pj; if (!W.TryGetValue(sl, out pj)) return false; foreach (var n in pj["nodes"]) foreach (var q in ns) if ((int)n == q) return true; return false; };
            bool helmCovers = W.ContainsKey("helmet") && (int?)W["helmet"]["covers"] == 1, closed = helmCovers && (int?)W["helmet"]["closed"] == 1;
            if (W.Count > 0)
            {
                // tuck the body in under the plates (armTuck)
                var shrink = new HashSet<int>();
                if (has("chest", new[] { 1 })) shrink.Add(1);
                if (has("chest", new[] { 0 }) || has("leggings", new[] { 0, 1 })) shrink.Add(0);
                for (int s = 0; s < 2; s++)
                {
                    int o = s * 3;
                    if (has("chest", new[] { 3 + o }) || has("gauntlets", new[] { 3 + o })) shrink.Add(3 + o);
                    if (has("chest", new[] { 4 + o }) || has("gauntlets", new[] { 4 + o })) shrink.Add(4 + o);
                    if (has("leggings", new[] { 9 + o })) shrink.Add(9 + o);
                    if (has("leggings", new[] { 10 + o }) || has("boots", new[] { 10 + o })) shrink.Add(10 + o);
                    if (has("boots", new[] { 11 + o })) shrink.Add(11 + o);
                }
                var neck = new HashSet<int>(); if (closed) foreach (var i in V["neck"]) neck.Add((int)i);
                var tucked = new HashSet<int>();
                foreach (var i in V["tuck"]) { int n = (int)i; var of = I["tuckOf"][n.ToString()]; if (of != null && shrink.Contains((int)of)) tucked.Add(n); }
                foreach (var n in tucked) SetMesh(n, b.pack.Get((neck.Contains(n) ? "tuckneck:" : "tuck:") + n));
                foreach (var n in neck) if (!tucked.Contains(n)) SetMesh(n, b.pack.Get("neck:" + n));
                // no tunic or robe skirt under a chest piece or new skirt panels
                bool usesPanels = false; foreach (var pj in W.Values) foreach (var n in pj["nodes"]) if ((int)n >= 16 && (int)n < 32) usesPanels = true;
                if (usesPanels || W.ContainsKey("chest")) foreach (var i in I["skirt"]) Hide(T[(int)i]);
                // a helm that covers the head hides the hair: a bald head, or a dark core inside a closed helm
                if (helmCovers)
                {
                    foreach (var i in I["headCh"]) Hide(T[(int)i]);
                    Child(Rig(2), "head_under", b.pack.Get(closed ? "head_shade" : "head_bald"), Vector3.zero);
                }
                // the pieces, on their joints; skirt panels and cape rows on their own swinging nodes
                float capeTop = (float)I["capeTop"]; int rows = (int)I["capeRows"], np = (int)I["np"]; float h = (capeTop - 0.2f) / rows;
                var panelOf = new Dictionary<int, Panel>();
                foreach (var sl in ARM_SLOTS)
                {
                    JObject pj; if (!W.TryGetValue(sl, out pj)) continue; string nm = Eq(sl).name;
                    foreach (var nn in pj["nodes"])
                    {
                        int n = (int)nn; var m = PP.Get(nm + ":" + n); if (!m) continue;
                        if (n >= 16 && n < 32)
                        {
                            int k = n - 16; Panel pn;
                            if (!panelOf.TryGetValue(k, out pn))
                            {
                                float lo = pj["lo"] != null && pj["lo"][n.ToString()] != null ? (float)pj["lo"][n.ToString()] : -0.3f;
                                var g = Child(Rig(0), "panel" + k, null, new Vector3(0, 1.02f * UK, 0));
                                pn = new Panel { n = g.transform, a = Mathf.PI / 2 + k / (float)np * Mathf.PI * 2, len = Mathf.Max(0.3f, -lo) }; panelOf[k] = pn; panels.Add(pn);
                            }
                            Child(pn.n, nm, m, Vector3.zero); continue;
                        }
                        if (n >= 32)
                        {
                            if (cape.Count == 0)
                            {
                                Transform par = Child(Rig(1), "cape", null, new Vector3(0, capeTop * UK, 0.2f * UK)).transform;
                                for (int r = 0; r < rows; r++) { var sgo = Child(par, "seg" + r, null, Vector3.zero).transform; cape.Add(sgo); par = Child(sgo, "segEnd" + r, null, new Vector3(0, -h * UK, 0)).transform; }
                            }
                            if (n - 32 < cape.Count) Child(cape[n - 32], nm, m, Vector3.zero); continue;
                        }
                        var tgt = Rig(n); if (tgt) Child(tgt, nm, m, Vector3.zero);
                    }
                }
                // bare fists take the gauntlets' colour
                var gt = Eq("gauntlets"); if (gt != null && W.ContainsKey("gauntlets") && V["hands"][gt.name] is JArray hl) foreach (var i in hl) SetMesh((int)i, b.pack.Get("hand_" + gt.name + ":" + (int)i));
            }
            // wings
            var wg = Eq("wings");
            if (wg != null && V["wings"][wg.name] is JObject wj)
            {
                float S = wg.rarity == "Mythical" ? 1.18f : wg.rarity == "Legendary" ? 1.08f : 1;
                var nodes = (JArray)wj["nodes"]; var made = new Transform[nodes.Count];
                for (int i = 0; i < nodes.Count; i++)
                {
                    int par = (int)nodes[i]["par"]; var t = (JArray)nodes[i]["t"];
                    var g = Child(par < 0 ? Rig(1) : made[par], "wing" + i, (int)nodes[i]["mesh"] != 0 ? b.pack.Get("wing_" + wg.name + ":" + i) : null, new Vector3((float)t[0], (float)t[1], (float)t[2]));
                    g.transform.localRotation = new Quaternion((float)t[3], (float)t[4], (float)t[5], (float)t[6]); g.transform.localScale = Vector3.one * (float)t[7] * (par < 0 ? S : 1); made[i] = g.transform;
                }
                foreach (var r in (JArray)wj["roles"]) wings.Add(new Wing { n = made[(int)r["n"]], f = (int)r["f"] >= 0 ? made[(int)r["f"]] : null, sd = (int)r["sd"], fairy = (int)r["fairy"] != 0 });
            }
            return true;
        }
        void Hide(Transform t) { if (!t || !t.gameObject.activeSelf) return; t.gameObject.SetActive(false); hidden.Add(t); }

        // ---- cloth and wings (plSecondary) ----
        // ---- flying with wings (updateFly, FLY_STYLE): a bobbing hover, legs trailing, wings beating ----
        public bool flying; public float flyPh, flyM, flyH;
        static readonly Dictionary<string, float[]> FLY_STYLE = new Dictionary<string, float[]> {   // rate, rateM, base, climb, bob, amp
            { "knight", new[] { 2.0f, 1.6f, 13, 6, 4.2f, 0.5f } }, { "mage", new[] { 1.5f, 1.2f, 26, 5, 2.6f, 0.2f } }, { "archer", new[] { 3.3f, 2.4f, 16, 10, 4.5f, 0.36f } } };
        float lastX, lastY; bool haveLast;
        void LateUpdate()
        {
            if (T == null) return;
            var PL = AldaraPlayer.I; bool isPlayer = PL && transform.parent == PL.transform;
            flying = isPlayer && wings.Count > 0;
            if (isPlayer) ry = Mathf.Atan2(Mathf.Cos(PL.facing), Mathf.Sin(PL.facing));
            float dt = Mathf.Min(0.05f, Time.deltaTime), t = Time.time;
            float[] FS = null;
            if (flying)
            {
                FS = FLY_STYLE.ContainsKey(cls) ? FLY_STYLE[cls] : FLY_STYLE["knight"];
                float mv = haveLast ? Mathf.Sqrt((PL.x - lastX) * (PL.x - lastX) + (PL.y - lastY) * (PL.y - lastY)) / Mathf.Max(dt, 1e-4f) / 60f : 0; lastX = PL.x; lastY = PL.y; haveLast = true;
                float target = Mathf.Min(1, mv / 2.6f); flyM += (target - flyM) * Mathf.Min(1, dt * (target > flyM ? 4 : 2.5f));
                flyPh += dt * (FS[0] + FS[1] * flyM); flyH = FS[2] + flyM * FS[3] - FS[4] * Mathf.Sin(flyPh);
                transform.localPosition = new Vector3(0, flyH / AldaraWorld.PX / AldaraView.TCP, 0);
            }
            else if (isPlayer && transform.localPosition.y != 0) { transform.localPosition = Vector3.zero; flyM = 0; }
            if (panels.Count == 0 && cape.Count == 0 && wings.Count == 0) return;
            var ax = (float[])(anim ? anim.aux : new float[13]).Clone();
            // legs trail and the body leans into the flight (poseP3, o.hover > 0)
            if (flying)
            {
                bool busy = anim && anim.Busy; float ph = flyPh, m = flyM; float add = 0, rzA = float.NaN;
                if (cls == "knight") { if (!busy) for (int k = 0; k < 2; k++) { ax[k * 4] = 0.05f + 0.35f * m + Mathf.Sin(ph + k * 0.3f) * 0.03f; ax[k * 4 + 3] = 0.12f + 0.3f * m; ax[k * 4 + 1] = (k == 1 ? 1 : -1) * 0.02f; } add = 0.18f * m + Mathf.Sin(ph) * 0.025f; }
                else if (cls == "mage") { if (!busy) for (int k = 0; k < 2; k++) { ax[k * 4] = -1.15f + 0.5f * m; ax[k * 4 + 3] = 1.75f - 0.4f * m; ax[k * 4 + 1] = (k == 1 ? -1 : 1) * 0.42f; } add = -0.06f + 0.2f * m + Mathf.Sin(ph * 0.5f) * 0.03f; }
                else { if (!busy) { ax[0] = -0.55f + 0.2f * m; ax[3] = 1.25f - 0.3f * m; ax[4] = 0.3f + 0.45f * m + Mathf.Sin(ph) * 0.08f; ax[7] = 0.35f + 0.3f * m; } add = 0.42f * m + Mathf.Sin(ph) * 0.02f; rzA = Mathf.Sin(ph * 0.5f) * 0.12f * (0.4f + m); }
                if (!busy) for (int k = 0; k < 2; k++)
                    {
                        var hip = Rig(9 + k * 3); var knee = Rig(10 + k * 3); var ankle = Rig(11 + k * 3);
                        if (hip) hip.localRotation = Rot(ax[k * 4], 0, ax[k * 4 + 1]); if (knee) knee.localRotation = Rot(ax[k * 4 + 3], 0, 0); if (ankle) ankle.localRotation = Rot(0.5f, 0, 0);
                    }
                var body = Rig(0); if (body) { body.localRotation = Rot(add, 0, 0) * body.localRotation; if (!float.IsNaN(rzA)) body.localRotation = body.localRotation * Rot(0, 0, rzA); }
                ax[8] += add;
            }
            foreach (var s in panels)
            {
                float ca = Mathf.Cos(s.a), sa = Mathf.Sin(s.a), push = 0;
                for (int k = 0; k < 2; k++)
                {
                    float hrx = ax[k * 4], hrz = ax[k * 4 + 1], hpx = ax[k * 4 + 2], krx = ax[k * 4 + 3];
                    float dx = 0.51f * Mathf.Sin(hrz), dz = -0.51f * Mathf.Sin(hrx) - 0.2f * Mathf.Sin(Mathf.Max(0, krx) * 0.5f) * (hrx < 0 ? 1 : 0);
                    float lat = (hpx * ca) > 0 ? 1 : 0.35f; push = Mathf.Max(push, (dx * ca + dz * sa) * lat);
                }
                float th = Mathf.Atan2(Mathf.Max(0, push) * 1.05f, s.len) + 0.03f + ax[10] * 0.25f * (sa < -0.5f ? 1 : 0.4f);
                s.n.localRotation = Rot(-th * sa, 0, th * ca);
            }
            if (cape.Count > 0)
            {
                float lean = ax[8] + ax[9], sp = ax[11], fm = flying ? flyM : 0, flow = 0.08f + sp * 0.28f + (flying ? 0.3f + 0.6f * fm : 0); int n = cape.Count;
                for (int i = 0; i < n; i++)
                {
                    float u = i / (float)n;
                    if (i == 0) { cape[i].localRotation = Rot(Mathf.Max(0.02f, lean + flow + ax[12]), 0, Mathf.Sin(t * 1.3f) * 0.02f); continue; }
                    cape[i].localRotation = Rot(0.05f + Mathf.Sin(t * (2.4f + sp * 4) - i * 0.9f) * (0.045f + sp * 0.09f) * (0.5f + u) + sp * 0.04f + fm * 0.18f, Mathf.Sin(t * 1.1f + i * 0.6f) * 0.02f * u, Mathf.Sin(t * 1.7f + i * 0.8f) * (0.03f + sp * 0.06f) * u);
                }
            }
            if (wings.Count > 0)
            {
                bool moving = anim && anim.Moving; float fast = moving ? 1.35f : 1;
                float r0 = Mathf.Atan2(Mathf.Sin(ry), Mathf.Cos(ry)); if (Mathf.Abs(r0) > Mathf.PI / 2) r0 -= Mathf.Sign(r0) * Mathf.PI; float turn = -r0 * 0.75f;
                foreach (var wg in wings)
                {
                    int d = wg.sd;
                    if (wg.fairy) { float fl = Mathf.Sin(t * 11); wg.n.localRotation = Rot(0, turn + d * (0.55f + fl * 0.3f), d * 0.05f * fl); continue; }
                    if (flying)
                    {
                        float ph = flyPh, m = flyM, fl = Mathf.Sin(ph), A = FS[5];
                        wg.n.localRotation = Rot(-0.08f - 0.12f * m, turn + d * (0.36f + 0.16f * Mathf.Cos(ph) * A / 0.42f + 0.28f * m), d * (0.12f + A * fl + (A < 0.25f ? 0.12f : 0)));
                        if (wg.f) wg.f.localRotation = Rot(0, d * (0.14f + 0.16f * Mathf.Sin(ph - 0.9f) + 0.1f * m), d * (0.08f + A * 0.76f * Mathf.Sin(ph - 0.75f)));
                        continue;
                    }
                    float ph2 = t * 2.3f * fast, fl2 = Mathf.Sin(ph2);
                    wg.n.localRotation = Rot(-0.08f, turn + d * (0.42f + 0.3f * fl2), d * (0.06f + 0.12f * Mathf.Sin(ph2 + 0.5f)));
                    if (wg.f) wg.f.localRotation = Rot(0, d * (0.18f + 0.22f * Mathf.Sin(ph2 - 0.8f)), d * (0.05f + 0.14f * Mathf.Sin(ph2 - 0.5f)));
                }
            }
        }
        /// the browser's rotation (m3TRS: R = Ry Rx Rz) in Unity's frame (S R S, S = diag(1, 1, -1))
        public static Quaternion Rot(float rx, float ry, float rz)
        {
            float cx = Mathf.Cos(rx), sx = Mathf.Sin(rx), cy = Mathf.Cos(ry), sy = Mathf.Sin(ry), cz = Mathf.Cos(rz), sz = Mathf.Sin(rz);
            float a = cy * cz + sy * sx * sz, b = -cy * sz + sy * sx * cz, c = sy * cx, d = cx * sz, e = cx * cz, f = -sx, g = -sy * cz + cy * sx * sz, h = sy * sz + cy * sx * cz, i = cy * cx;
            c = -c; f = -f; g = -g; h = -h;
            float qw, qx, qy, qz, tr = a + e + i;
            if (tr > 0) { float s = Mathf.Sqrt(tr + 1) * 2; qw = 0.25f * s; qx = (h - f) / s; qy = (c - g) / s; qz = (d - b) / s; }
            else if (a > e && a > i) { float s = Mathf.Sqrt(1 + a - e - i) * 2; qw = (h - f) / s; qx = 0.25f * s; qy = (b + d) / s; qz = (c + g) / s; }
            else if (e > i) { float s = Mathf.Sqrt(1 + e - a - i) * 2; qw = (c - g) / s; qx = (b + d) / s; qy = 0.25f * s; qz = (f + h) / s; }
            else { float s = Mathf.Sqrt(1 + i - a - e) * 2; qw = (d - b) / s; qx = (c + g) / s; qy = (f + h) / s; qz = 0.25f * s; }
            return new Quaternion(qx, qy, qz, qw);
        }

        // ---- the player ----
        /// keep the player's model in the right body and dressed (called whenever equipment changes)
        public static void SyncPlayer()
        {
            var H = AldaraHero.I; var P = AldaraPlayer.I; if (!H || !P) return;
            string want = BaseFor(H.cls, H.Eq("weapon"));
            var cur = P.model ? P.model.GetComponent<AldaraHeroGear>() : null;
            if (cur == null || cur.baseId != want)
            {
                var pf = Prefab(want); if (!pf) return;
                if (P.model) { P.model.gameObject.SetActive(false); Object.Destroy(P.model.gameObject); }
                var g = Object.Instantiate(pf, P.transform, false); P.model = g.transform; P.anim = g.GetComponent<AldaraCharacterAnimator>(); cur = g.GetComponent<AldaraHeroGear>();
            }
            if (cur) cur.Apply(H.equip);
        }
    }
}
