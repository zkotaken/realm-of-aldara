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
        // for the glows (drawWingAura, drawWingGlow, drawMythAura)
        public Transform[] wingNodes; public string wingName, wingRarity, mythGlow; public int mythN;
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
            // a mythic aura when four or more pieces of one set are worn
            mythGlow = null; mythN = 0; var cnt = new Dictionary<string, int>(); MythicSet bestSet = null;
            foreach (var sl in ARM_SLOTS) { var ms = AldaraItems.MythSetOf(Eq(sl)); if (ms == null) continue; int c0; cnt.TryGetValue(ms.id, out c0); cnt[ms.id] = c0 + 1; if (cnt[ms.id] >= 4 && (bestSet == null || cnt[ms.id] > cnt[bestSet.id])) bestSet = ms; }
            if (bestSet != null) { mythGlow = bestSet.glow; mythN = cnt[bestSet.id]; }
            wingNodes = null; wingName = null;
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
                wingNodes = made; wingName = wg.name; wingRarity = wg.rarity;
                foreach (var r in (JArray)wj["roles"]) wings.Add(new Wing { n = made[(int)r["n"]], f = (int)r["f"] >= 0 ? made[(int)r["f"]] : null, sd = (int)r["sd"], fairy = (int)r["fairy"] != 0 });
            }
            Holsters(b, w);
            return true;
        }
        void Hide(Transform t) { if (!t || !t.gameObject.activeSelf) return; t.gameObject.SetActive(false); hidden.Add(t); }

        // ---- cloth and wings (plSecondary) ----
        // ---- flying with wings (updateFly, FLY_STYLE): a bobbing hover, legs trailing, wings beating ----
        public bool flying; public float flyPh, flyM, flyH;
        static readonly Dictionary<string, float[]> FLY_STYLE = new Dictionary<string, float[]> {   // rate, rateM, base, climb, bob, amp
            { "knight", new[] { 2.0f, 1.6f, 13, 6, 4.2f, 0.5f } }, { "mage", new[] { 1.5f, 1.2f, 26, 5, 2.6f, 0.2f } }, { "archer", new[] { 3.3f, 2.4f, 16, 10, 4.5f, 0.36f } } };
        float lastX, lastY; bool haveLast;

        // ---- weapons put away out of combat: the sword and shield go on the back, a staff or bow across it ----
        class Holster { public Renderer hand; public GameObject back; }
        readonly List<Holster> holsters = new List<Holster>(); bool sheathed;
        void Holsters(Base b, Item w)
        {
            holsters.Clear(); sheathed = false; if (w == null) return;
            var wl = b.meta["V"]["weapons"][w.name + "|" + w.rarity] as JArray; var torso = Rig(1); if (wl == null || !torso) return;
            float top = b.meta["info"]["capeTop"] != null ? (float)b.meta["info"]["capeTop"] * UK : 0.6f;
            var items = new List<KeyValuePair<Transform, Mesh>>();
            foreach (var i in wl) { int n = (int)i; if (n < 0 || n >= T.Length || !T[n]) continue; var mf = T[n].GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) items.Add(new KeyValuePair<Transform, Mesh>(T[n], mf.sharedMesh)); }
            int longN = 0;
            foreach (var kv in items)
            {
                var node = kv.Key; var mb = kv.Value.bounds; var e = mb.extents;
                // the item's axes, longest to thinnest
                var ax = new List<KeyValuePair<float, Vector3>> { new KeyValuePair<float, Vector3>(e.x, Vector3.right), new KeyValuePair<float, Vector3>(e.y, Vector3.up), new KeyValuePair<float, Vector3>(e.z, Vector3.forward) };
                ax.Sort((a, c) => c.Key.CompareTo(a.Key));
                Vector3 La = ax[0].Value, Th = ax[2].Value; bool isLong = ax[0].Key > ax[1].Key * 2.2f;
                if (Vector3.Dot(mb.center, La) < 0) La = -La;   // grip to tip (or bottom to top)
                Quaternion q; Vector3 at;
                if (isLong) { var tip = new Vector3(longN % 2 == 0 ? -0.26f : 0.26f, -0.97f, 0).normalized; q = Quaternion.LookRotation(tip, Vector3.forward) * Quaternion.Inverse(Quaternion.LookRotation(La, Th)); at = new Vector3(longN % 2 == 0 ? 0.04f : -0.04f, top * 0.64f, 0.34f); longN++; }   // grip over the shoulder, tip down the back
                else { q = Quaternion.LookRotation(Vector3.up, Vector3.back) * Quaternion.Inverse(Quaternion.LookRotation(La, Th)); at = new Vector3(0, top * 0.7f, 0.4f); }   // a shield flat on the back, over the blade
                var rel = torso.worldToLocalMatrix * node.localToWorldMatrix; float sc = new Vector3(rel.m00, rel.m10, rel.m20).magnitude;
                var g = new GameObject("holster"); g.transform.SetParent(torso, false); g.transform.localRotation = q; g.transform.localScale = Vector3.one * sc;
                g.transform.localPosition = at - q * (mb.center * sc);
                g.AddComponent<MeshFilter>().sharedMesh = kv.Value; var hr = node.GetComponent<MeshRenderer>(); var r = g.AddComponent<MeshRenderer>(); r.sharedMaterials = hr ? hr.sharedMaterials : new[] { mat };
                g.SetActive(false); added.Add(g); holsters.Add(new Holster { hand = hr, back = g });
            }
        }
        /// out of a fight for a few seconds the hero puts the weapon away (the same moment the sheathing sound plays)
        void Sheath(bool isPlayer)
        {
            if (holsters.Count == 0) return; var H = AldaraHero.I;
            bool want = isPlayer && H && H.alive && !H.InFight;
            if (want == sheathed) return; sheathed = want;
            foreach (var h in holsters) { if (h.hand) h.hand.enabled = !want; if (h.back) h.back.SetActive(want); }
        }
        void LateUpdate()
        {
            if (T == null) return;
            var PL = AldaraPlayer.I; bool isPlayer = PL && transform.parent == PL.transform;
            Sheath(isPlayer);
            bool riding = isPlayer && AldaraMount.Riding;
            flying = isPlayer && wings.Count > 0 && !riding;
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
            else if (isPlayer && !riding && transform.localPosition.y != 0 && crouch == 0) { transform.localPosition = Vector3.zero; flyM = 0; }
            if (riding && !(anim && anim.Busy))
            {   // in the saddle (AldaraMount): thighs forward along the horse's sides, knees bent, heels down in the stirrups;
                // hands forward on the reins; the rider leans into the gallop, rocks with the stride, and at rest sits easy,
                // breathing and looking about
                float ph = AldaraMount.Phase, g = AldaraMount.Gallop, mv = AldaraMount.Move, s1 = Mathf.Sin(ph), s2 = Mathf.Sin(ph * 2 + 0.4f), rest = 1 - mv;
                for (int k = 0; k < 2; k++)
                {
                    var hip = Rig(9 + k * 3); var knee = Rig(10 + k * 3); var ankle = Rig(11 + k * 3); float sd = k == 1 ? -1 : 1;
                    float grip = mv * (0.05f + 0.08f * g) * s2;   // knees squeeze and the heels sink with each stride
                    if (hip) hip.localRotation = Rot(-1.3f + grip * 0.5f, 0, sd * (0.34f - 0.04f * g * mv)); if (knee) knee.localRotation = Rot(1.45f - grip, 0, 0); if (ankle) ankle.localRotation = Rot(0.25f + grip * 0.6f, 0, 0);
                }
                var r1 = Rig(1); var body = r1 ? r1.parent : null;   // the waist: the upper body leans, the legs stay in the stirrups
                if (body)
                {
                    float lean = 0.05f + mv * (0.06f + 0.24f * g) + mv * s2 * (0.025f + 0.04f * g) + rest * Mathf.Sin(t * 1.6f) * 0.012f;
                    float sway = rest * Mathf.Sin(t * 0.55f) * 0.04f + mv * Mathf.Sin(ph) * 0.03f * (1 - g);
                    body.localRotation = Rot(lean, 0, sway) * body.localRotation;
                }
                for (int k = 0; k < 2; k++)
                {   // the reins: upper arms forward and in, elbows bent, hands low over the pommel
                    var sh = Rig(3 + k * 3); var el = Rig(4 + k * 3); float sd = k == 1 ? -1 : 1;
                    float pull = mv * (0.08f + 0.1f * g) * s2;
                    if (sh) sh.localRotation = Rot(-0.5f - 0.18f * g * mv + pull, 0, -sd * 0.16f);
                    if (el) el.localRotation = Rot(-1.0f - pull * 0.6f, 0, sd * 0.12f);
                }
                var head = Rig(2);
                if (head) head.localRotation = head.localRotation * Rot(-0.12f * g * mv, rest * Mathf.Sin(t * 0.33f) * 0.35f, 0);   // eyes up the road; at rest, a look round
            }
            IdleLayer(isPlayer, riding, dt, t);
            if (panels.Count == 0 && cape.Count == 0 && wings.Count == 0) return;
            var ax = (float[])(anim ? anim.aux : new float[13]).Clone();
            if (riding) for (int k = 0; k < 2; k++) { ax[k * 4] = -1.3f; ax[k * 4 + 1] = (k == 1 ? -1 : 1) * 0.34f; ax[k * 4 + 3] = 1.45f; }
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
        // ---------- the idle poses, layered over the browser's clips ----------
        // Standing with the weapon put away (or with none), the hero stands easy: arms hanging relaxed a little off the
        // body with the elbows soft, breathing, weight shifting from foot to foot, head looking about. Standing in a
        // fight with the weapon out, each class takes a ready stance: the knight's sword up and shield across the body,
        // the mage's staff forward and the free hand raised with a spell, the archer's bow held low in front with the
        // other hand at the string; knees bent, a slow breathing bob.
        float relaxW, readyW;
        void IdleLayer(bool isPlayer, bool riding, float dt, float t)
        {
            if (!anim || T == null) return;
            bool still = isPlayer && !riding && !anim.Moving && !anim.Busy && AldaraHero.I && AldaraHero.I.alive;
            // a weapon with a holster is out until it is sheathed; one that never goes on the back (the great blades) counts as
            // out while the fight lasts; bare hands never take the ready stance
            var HH = AldaraHero.I;
            bool armed = holsters.Count > 0 ? !sheathed : (HH && HH.Eq("weapon") != null && HH.InFight);
            relaxW = Mathf.MoveTowards(relaxW, still && !armed ? 1 : 0, dt * 4);
            readyW = Mathf.MoveTowards(readyW, still && armed ? 1 : 0, dt * 5);
            if (relaxW <= 0 && readyW <= 0) { if (crouch != 0 && !flying) { crouch = 0; transform.localPosition = Vector3.zero; } return; }
            float br = Mathf.Sin(t * 1.9f), sh = Mathf.Sin(t * 0.42f), look = Mathf.Sin(t * 0.23f) * Mathf.Max(0, Mathf.Sin(t * 0.11f + 1));
            int wk = cls == "archer" ? 0 : 1;   // the side that holds the weapon (the bow is in the other hand)
            if (relaxW > 0) Pose(relaxW, br, sh, look, -1, t);
            if (readyW > 0) Pose(readyW, br, sh, look * 0.4f, wk, t);
        }
        float crouch;
        void Blend(Transform x, Quaternion q, float w) { if (x) x.localRotation = Quaternion.Slerp(x.localRotation, q, w); }
        /// wk < 0: the easy stance; otherwise the class's ready stance with the weapon in side wk
        void Pose(float w, float br, float sh, float look, int wk, float t)
        {
            bool ready = wk >= 0;
            for (int k = 0; k < 2; k++)
            {
                float sd = k == 1 ? -1 : 1;   // +z swings this side's limb outward
                Transform s0 = Rig(3 + k * 3), e0 = Rig(4 + k * 3), h0 = Rig(5 + k * 3);
                float srx, srz, sry = 0, erx, hrx = 0, hry = 0, hrz = 0;
                if (!ready) { srx = 0.06f + br * 0.015f; srz = sd * (0.14f + br * 0.01f); erx = -0.28f - br * 0.03f; hrx = 0.1f; }
                else if (cls == "archer")
                {
                    if (k == wk) { srx = -0.75f + br * 0.02f; srz = -sd * 0.05f; erx = -0.25f; }      // the bow, low and forward
                    else { srx = -0.3f + br * 0.02f; sry = sd * 0.8f; srz = 0; erx = -1.4f; }          // fingers on the string, by the grip
                }
                else if (cls == "mage")
                {
                    if (k == wk) { srx = -0.4f + br * 0.02f; srz = sd * 0.06f; erx = -0.95f; hrx = -0.8f; }   // the staff forward, its head up
                    else { srx = -0.8f + br * 0.03f + Mathf.Sin(t * 2.6f) * 0.03f; srz = -sd * 0.12f; erx = -1.25f; hrx = -0.4f; }   // the casting hand up
                }
                else if (baseId == "knight_great")
                {
                    if (k == wk) { srx = -0.5f + br * 0.025f; srz = sd * 0.4f; erx = -1.3f; hrx = -0.6f; hrz = 0.8f * sd; }   // the great blade held up before you in both hands
                    else { srx = -0.6f + br * 0.025f; srz = -sd * 0.6f; erx = -1.1f; }                                   // the other hand on the grip
                }
                else
                {
                    if (k == wk) { srx = -0.35f + br * 0.025f; srz = sd * 0.1f; erx = -1.25f; hrx = -0.6f; hry = sd * 0.5f; }   // the sword up before you, point high
                    else { srx = -0.62f + br * 0.02f; srz = -sd * 0.3f; erx = -1.35f; }                                         // the shield across the body
                }
                Blend(s0, Rot(srx, sry, srz), w); Blend(e0, Rot(erx, 0, 0), w); if (hrx != 0 || hry != 0 || hrz != 0 || !ready) Blend(h0, Rot(hrx, hry, hrz), ready ? w : w * 0.7f);
            }
            // legs: the easy stance shifts its weight from foot to foot; the ready stance bends the knees and spreads the feet
            if (!flying)
            {
                for (int k = 0; k < 2; k++)
                {
                    float sd = k == 1 ? -1 : 1; Transform hp = Rig(9 + k * 3), kn = Rig(10 + k * 3), an = Rig(11 + k * 3);
                    float bend = ready ? 0.34f + br * 0.02f : Mathf.Max(0, (k == 0 ? sh : -sh)) * 0.16f;
                    float spread = ready ? 0.1f : 0.04f;
                    Blend(hp, Rot(-bend * 0.5f + (ready && k == 0 ? -0.12f : 0), 0, sd * spread), w); Blend(kn, Rot(bend, 0, 0), w); Blend(an, Rot(-bend * 0.5f, 0, 0), w);
                }
                float c = ready ? 0.045f + br * 0.006f : Mathf.Abs(sh) * 0.012f; crouch = c * w; transform.localPosition = new Vector3(0, -crouch, 0);
            }
            // the body breathes and sways; the ready stance leans in a little; the head looks round now and then
            var r1 = Rig(1); var waist = r1 ? r1.parent : null;
            if (waist) waist.localRotation = Rot((ready ? 0.12f : 0.02f) + br * 0.012f, ready ? (cls == "archer" ? 0.25f : 0.12f) * w : 0, sh * (ready ? 0.015f : 0.03f)) * Quaternion.Slerp(Quaternion.identity, waist.localRotation, 1 - w);
            var head = Rig(2); if (head) Blend(head, Rot(ready ? -0.06f : 0.04f + br * 0.01f, look * (ready ? 0.15f : 0.45f) - (ready && cls == "archer" ? 0.2f : 0), 0), w * 0.9f);
        }
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
