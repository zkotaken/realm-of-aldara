using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;

namespace Aldara
{
    // The browser's waystones (wsInit / wsUpdate / wsDraw / wsTravel): walk within 110 px of a stone to attune it (a
    // banner, XP by zone, saved in player.waystones), stand within 95 px and press F (or click it) to open the Waystones
    // window, pick an attuned stone and the screen fades while the stone carries you there. Known stones pulse with a
    // blue glow, a rune ring and five orbiting motes; unknown ones only glow faintly. The stones themselves and their
    // ground shadow are part of the world.
    public class AldaraWaystones : MonoBehaviour
    {
        public static AldaraWaystones I;
        public class Ws { public int id, z; public string n; public float x, y; public bool known; }
        public class Zone { public string name, mm; public int lv; }
        public static List<Ws> ALL; public static List<Zone> ZONES;
        public static Ws from; static float fxT; static bool busy;
        static readonly Color BLUE = new Color(127 / 255f, 216 / 255f, 1f);

        void Awake() { I = this; Load(); }
        public static void Load()
        {
            if (ALL != null) return; var j = JObject.Parse(Resources.Load<TextAsset>("waystones").text);
            ALL = j["ws"].Select(t => new Ws { id = (int)t["id"], z = (int)t["z"], n = (string)t["n"], x = (float)t["x"], y = (float)t["y"], known = (int)t["known"] != 0 }).ToList();
            ZONES = j["zones"].Select(t => new Zone { name = (string)t["name"], mm = (string)t["mm"], lv = (int)t["lv"] }).ToList();
        }
        static JArray Mine { get { var r = AldaraSave.Raw; if (r == null) return null; return r["waystones"] as JArray; } }
        public static bool Known(Ws w) { if (w.known) return true; var m = Mine; return m != null && m.Any(v => v.Type == JTokenType.Integer && (int)v == w.id); }
        public static List<Ws> KnownList() { Load(); return ALL.Where(Known).ToList(); }
        static float Dist(float ax, float ay, float bx, float by) { return Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)); }
        static AldaraPlayer P { get { return AldaraPlayer.I; } }
        static AldaraHero H { get { return AldaraHero.I; } }

        /// the attuned stone within 95 px (F travels from it)
        public static Ws Near()
        {
            if (!AldaraSave.Ready || !P) return null; Load(); Ws best = null; float bd = 95;
            foreach (var w in ALL) { float d = Dist(w.x, w.y, P.x, P.y); if (d < bd && Known(w)) { bd = d; best = w; } }
            return best;
        }
        /// a click in the world on a stone
        public static bool Click(float wx, float wy)
        {
            Load();
            foreach (var w in ALL)
            {
                if (Dist(wx, wy, w.x, w.y - 40) < 40 || Dist(wx, wy, w.x, w.y) < 40)
                {
                    if (!Known(w)) { AldaraHud.Banner("Walk up to the waystone to attune it"); return true; }
                    if (Dist(w.x, w.y, P.x, P.y) > 140) { AldaraHud.Banner("Come closer to the waystone"); return true; }
                    Open(w); return true;
                }
            }
            return false;
        }
        public static void Open(Ws w) { from = w ?? Near(); if (AldaraWindows.I) AldaraWindows.I.Toggle("ws", true); }

        void Update()
        {
            if (fade != null) Fade();
            if (!AldaraSave.Ready || !P || !H || !H.alive) { Sync3D(false); return; }
            fxT -= Time.deltaTime;
            if (fxT <= 0)
            {
                fxT = 0.3f;
                foreach (var w in ALL)
                {
                    if (Known(w) || Dist(w.x, w.y, P.x, P.y) >= 110) continue;
                    var r = AldaraSave.Raw; var m = Mine; if (m == null) { m = new JArray(); r["waystones"] = m; } m.Add(w.id);
                    AldaraHud.Banner("Waystone attuned: " + w.n);
                    AldaraFx.Burst(w.x, w.y - 40, 36, C("#9fe8ff")); AldaraFx.Ring(w.x, w.y, 120, C("#9fe8ff"), 0.8f);
                    H.GainXp(Mathf.Round(40 * Mathf.Pow(1.35f, w.z))); AldaraSave.Dirty(); AldaraWindows.Refresh("ws");
                }
            }
            bool live = !(AldaraWindows.I && AldaraWindows.I.IsOpen("map"));
            Sync3D(live);
        }

        // ---------- travel ----------
        public static void Travel(int id)
        {
            Load(); var w = ALL.FirstOrDefault(x => x.id == id); if (w == null || !Known(w) || !H.alive || busy) return; busy = true;
            if (AldaraWindows.I) AldaraWindows.I.Toggle("ws", false);
            if (!I) { busy = false; return; } I.StartFade(); I.StartCoroutine(I.Arrive(w));
        }
        System.Collections.IEnumerator Arrive(Ws w)
        {
            yield return new WaitForSecondsRealtime(0.26f);
            float tx = w.x + 70, ty = w.y + 30; Vector2 f = AldaraWorld.BlockedAt(tx, ty) ? AldaraPlayer.FreeSpotNear(tx, ty) : new Vector2(tx, ty);
            P.x = f.x; P.y = f.y; H.target = null; if (AldaraSkills.I) AldaraSkills.I.queued = null; if (AldaraPet.I) AldaraPet.I.placed = false;
            AldaraFx.Burst(P.x, P.y - 20, 30, C("#9fe8ff")); AldaraFx.Ring(P.x, P.y, 60, C("#9fe8ff"), 0.8f);
            AldaraHud.Banner("The waystone carries you to " + w.n); busy = false; AldaraSave.Dirty();
        }
        // #fortFade: black over everything, 0 -> 1 at 25% -> 0 over 0.9 s
        VisualElement fade; float fadeT;
        void StartFade()
        {
            if (fade == null)
            {
                var go = new GameObject("FadeUI"); go.SetActive(false); var doc = go.AddComponent<UIDocument>();
                doc.panelSettings = Resources.Load<PanelSettings>("UI/AldaraPanel"); doc.sortingOrder = 70; go.SetActive(true);
                var root = doc.rootVisualElement; root.pickingMode = PickingMode.Ignore; root.style.position = Position.Absolute; root.style.left = root.style.top = root.style.right = root.style.bottom = 0;
                fade = new VisualElement { pickingMode = PickingMode.Ignore }; fade.style.position = Position.Absolute; fade.style.left = fade.style.top = fade.style.right = fade.style.bottom = 0;
                fade.style.backgroundColor = Color.black; fade.style.opacity = 0; root.Add(fade);
            }
            fadeT = 0;
        }
        void Fade()
        {
            fadeT += Time.unscaledDeltaTime; float u = fadeT / 0.9f;
            // ease-out over the whole animation
            float e = 1 - (1 - Mathf.Clamp01(u)) * (1 - Mathf.Clamp01(u));
            fade.style.opacity = u >= 1 ? 0 : e < 0.25f ? e / 0.25f : 1 - (e - 0.25f) / 0.75f;
        }

        // ---------- in the world ----------
        readonly Dictionary<int, GameObject> views = new Dictionary<int, GameObject>();
        static Sprite glowSpr; static Material ringMat; static Mesh ringMesh;
        void Sync3D(bool on)
        {
            var cam = Camera.main;
            foreach (var w in ALL)
            {
                GameObject v; views.TryGetValue(w.id, out v);
                bool show = on && cam && Mathf.Abs(w.x - P.x) < 1400 && Mathf.Abs(w.y - P.y) < 1000;
                if (!show) { if (v && v.activeSelf) v.SetActive(false); continue; }
                if (!v) { v = Make(); views[w.id] = v; }
                if (!v.activeSelf) v.SetActive(true);
                bool known = Known(w); float t = Time.time, pul = 0.55f + 0.25f * Mathf.Sin(t * 2.2f);
                var g = AldaraWorld.ToUnity(w.x, w.y); v.transform.position = g; var back = cam.transform.forward * 1.5f;
                // the glow behind the stone
                var gl = v.transform.GetChild(0); gl.rotation = cam.transform.rotation; gl.position = g + Vector3.up * ((known ? 52 : 40) / AldaraWorld.PX) + back;
                gl.localScale = Vector3.one * ((known ? 70 : 40) * 2 / AldaraWorld.PX);
                gl.GetComponent<SpriteRenderer>().color = new Color(BLUE.r, BLUE.g, BLUE.b, known ? 0.28f * pul * 1.6f : 0.07f * 1.6f);
                // the rune ring on the ground
                var ring = v.transform.GetChild(1); ring.gameObject.SetActive(known);
                if (known)
                {
                    ring.localPosition = new Vector3(0, 0.06f, -4 / AldaraWorld.PX); ring.localScale = new Vector3((36 + Mathf.Sin(t * 2.2f) * 2) / 36f, 1, 1);
                    ring.GetComponent<MeshRenderer>().material.color = new Color(160 / 255f, 230 / 255f, 1, 0.5f * pul);
                }
                // five motes circling the top of the stone
                for (int k = 0; k < 5; k++)
                {
                    var m = v.transform.GetChild(2 + k); m.gameObject.SetActive(known); if (!known) continue;
                    float a = t * 0.9f + k * 1.2566f, rr = 30, px = Mathf.Cos(a) * rr, py = -46 + Mathf.Sin(a) * rr * 0.35f - Mathf.Sin(t * 1.7f + k) * 8;
                    m.rotation = cam.transform.rotation; m.position = g + new Vector3(px / AldaraWorld.PX, -py / AldaraWorld.PX, 0) + back * 0.6f; m.localScale = Vector3.one * (8 / AldaraWorld.PX);
                    m.GetComponent<SpriteRenderer>().color = new Color(200 / 255f, 240 / 255f, 1, 0.5f + 0.4f * Mathf.Sin(t * 3 + k));
                }
            }
        }
        GameObject Make()
        {
            var v = new GameObject("waystone_fx"); v.transform.SetParent(transform, false);
            var gl = new GameObject("glow"); gl.transform.SetParent(v.transform, false); var gs = gl.AddComponent<SpriteRenderer>(); gs.sprite = Glow(); gs.sortingOrder = 1;
            if (!ringMat) { ringMat = new Material(Shader.Find("Sprites/Default")); ringMat.renderQueue = 3100; }
            if (!ringMesh) ringMesh = Ellipse(36 / AldaraWorld.PX, 15 / AldaraWorld.PX, 2 / AldaraWorld.PX);
            var ring = new GameObject("ring", typeof(MeshFilter), typeof(MeshRenderer)); ring.transform.SetParent(v.transform, false);
            ring.GetComponent<MeshFilter>().sharedMesh = ringMesh; var mr = ring.GetComponent<MeshRenderer>(); mr.material = new Material(ringMat); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int k = 0; k < 5; k++) { var m = new GameObject("mote"); m.transform.SetParent(v.transform, false); var ms = m.AddComponent<SpriteRenderer>(); ms.sprite = Glow(); ms.sortingOrder = 2; }
            return v;
        }
        static Sprite Glow()
        {
            if (glowSpr) return glowSpr; int S = 64; var tx = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f)) / (S / 2f); tx.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1 - d))); }
            tx.Apply(); glowSpr = Sprite.Create(tx, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S); return glowSpr;
        }
        static Mesh Ellipse(float rx, float ry, float w)
        {
            var v = new List<Vector3>(); var ix = new List<int>(); int n = 48;
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v.Add(new Vector3(c * (rx - w / 2), 0, s * (ry - w / 2))); v.Add(new Vector3(c * (rx + w / 2), 0, s * (ry + w / 2)));
                if (i < n) { int b = i * 2; ix.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 }); }
            }
            var m = new Mesh(); m.SetVertices(v); m.SetColors(Enumerable.Repeat(Color.white, v.Count).ToList()); m.SetTriangles(ix, 0); m.RecalculateBounds(); return m;
        }

        /// the names over the stones and the F prompt (drawn by the quest overlay)
        public delegate void LabFn(string s, float size, Color c, bool cinzel, bool bold, float x, float y);
        public static void Labels(System.Func<float, float, Vector2> w2p, float k, LabFn lab)
        {
            if (!AldaraSave.Ready || !P) return; Load(); string key = AldaraKeys.Name(AldaraKeys.KeyOf("interact"));
            foreach (var w in ALL)
            {
                float d = Dist(w.x, w.y, P.x, P.y); if (d >= 260) continue; bool known = Known(w); var g = w2p(w.x, w.y);
                lab(known ? w.n : "Waystone", 12, known ? C("#bfe8ff") : C("#8a98a8"), true, true, g.x, g.y - 104 * k + 6);
                if (d < 95 && known) lab("Press " + key + " to travel", 11, C("#ffe8a0"), false, false, g.x, g.y - 90 * k + 6);
            }
        }
        /// the blue diamonds on the minimap (wsMinimap)
        public static void Minimap(Painter2D g, float ox, float oy, float s, float S)
        {
            if (!AldaraSave.Ready) return; Load();
            foreach (var w in ALL)
            {
                if (!Known(w)) continue; float x = (w.x - ox) * s, y = (w.y - oy) * s; if (x < 4 || y < 4 || x > S - 4 || y > S - 4) continue;
                if (Vector2.Distance(new Vector2(x, y), new Vector2(S / 2, S / 2)) > S / 2 - 4) continue;
                g.BeginPath(); g.MoveTo(new Vector2(x, y - 4)); g.LineTo(new Vector2(x + 3.5f, y)); g.LineTo(new Vector2(x, y + 4)); g.LineTo(new Vector2(x - 3.5f, y)); g.ClosePath();
                g.fillColor = BLUE; g.Fill(); g.strokeColor = Color.black; g.lineWidth = 1; g.Stroke();
            }
        }
    }

    // The Waystones window (renderWaystones): where you stand, how many you have attuned, the attuned stones grouped by
    // zone with its level, and a button for each with its distance.
    public class AldaraWsWin : AldaraWindows.Win
    {
        public AldaraWsWin() { id = "ws"; title = "Waystones"; }
        public override float Y => 56; public override float W => 520;
        static Texture2D itemBg;
        public override void Render(VisualElement v)
        {
            var list = AldaraWaystones.KnownList(); var from = AldaraWaystones.from; int total = AldaraWaystones.ALL.Count; var P = AldaraPlayer.I; var H = AldaraHero.I;
            var sv = new ScrollView(ScrollViewMode.Vertical); v.Add(sv); Thin(sv); sv.style.flexShrink = 1; var c = sv.contentContainer;
            var head = Row(c, 10, Justify.SpaceBetween, Align.FlexEnd); head.style.marginBottom = 10;
            T(head, from != null ? "Standing at <color=#ffffff>" + AldaraQuestUI.Esc(from.n) + "</color>" : "No waystone near you", 13, C("#bfe8ff"), true);
            T(head, list.Count + " of " + total + " waystones attuned", 11, C("#8a8474"), true, false, false, 0, false, false);
            foreach (var g in list.GroupBy(w => w.z).OrderBy(g => g.Key))
            {
                var Z = AldaraWaystones.ZONES[g.Key]; int lv = Z.lv;
                var zr = Row(c, 0, Justify.SpaceBetween, Align.FlexEnd); zr.style.marginTop = 8; zr.style.marginBottom = 3; Pad(zr, 3, 8); zr.style.borderLeftWidth = 3; zr.style.borderLeftColor = C(Z.mm ?? "#888888");
                T(zr, g.Key == 0 ? "Green Fields and Lorenmar" : Z.name, 12, C("#e8dcc0"), true, false, false, 1, true, false);
                T(zr, "Level " + lv + (lv - H.lvl >= 10 ? " (dangerous)" : ""), 11, C("#a8a090"), false, false, false, 0, false, false);
                foreach (var w in g)
                {
                    bool here = from != null && from.id == w.id; int d = Mathf.RoundToInt(Vector2.Distance(new Vector2(w.x, w.y), new Vector2(P.x, P.y)) / 60); int wid = w.id;
                    var b = Row(c, 10); b.style.marginTop = 4; Pad(b, 8, 12); Border(b, 1, here ? C("#3a5a78") : C("#2e3446"), 7); b.style.backgroundImage = Background.FromTexture2D(ItemBg()); b.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
                    b.pickingMode = PickingMode.Position; if (here) b.style.opacity = 0.6f;
                    var rune = E(b); rune.style.width = rune.style.height = 12; rune.style.rotate = new Rotate(45); rune.style.backgroundColor = C("#7fd8ff"); Border(rune, 1, C("#e8fbff")); rune.style.flexShrink = 0;
                    var n = T(b, AldaraQuestUI.Esc(w.n), 13, C("#d8d0bc"), true); n.style.flexGrow = 1;
                    T(b, here ? "You are here" : d + "m away", 12, C("#8a98a8"), false, false, false, 0, false, false);
                    if (!here)
                    {
                        b.RegisterCallback<ClickEvent>(e => AldaraWaystones.Travel(wid));
                        b.RegisterCallback<MouseEnterEvent>(e => b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = C("#7fd8ff"));
                        b.RegisterCallback<MouseLeaveEvent>(e => b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = C("#2e3446"));
                    }
                    b.schedule.Execute(() => ApplyGap(b));
                }
            }
            var ft = T(c, "Undiscovered waystones show as dim stones in the world. Walk up to one to attune it.", 12, C("#8a8474")); ft.style.marginTop = 10; ft.style.unityTextAlign = TextAnchor.UpperCenter;
            head.schedule.Execute(() => ApplyGap(head));
        }
        static Texture2D ItemBg()
        {
            if (itemBg) return itemBg; itemBg = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color a = C("#141828"), b = C("#0b0d16"); for (int i = 0; i < 32; i++) itemBg.SetPixel(0, i, Color.Lerp(b, a, i / 31f)); itemBg.Apply(); return itemBg;
        }
        public override void OnClose() { }
    }
}
