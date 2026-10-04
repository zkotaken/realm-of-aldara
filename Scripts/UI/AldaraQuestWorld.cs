using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;
using Q = Aldara.AldaraQuests;

namespace Aldara
{
    // Quests in the world, as the browser draws them: names and "!" / "?" over quest givers, the name tags of the
    // townsfolk, what F does here, the gold waypoint (a diamond on screen, an arrow at the edge with the distance),
    // glowing gathering spots with the gather ring, light columns over places to scout, and the quest marks on the
    // minimap. F talks, gathers or reads; clicking a person talks to them.
    public class AldaraQuestWorld : MonoBehaviour
    {
        public static AldaraQuestWorld I;
        UIDocument doc; VisualElement root; Painter paint; AldaraQuestUI.Tracker tracker; MiniMarks mini;
        readonly List<Label> pool = new List<Label>(); int used;
        void Awake() { I = this; }
        void Start()
        {
            Q.Load();
            var go = new GameObject("QuestWorldUI"); go.SetActive(false); doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("UI/AldaraPanel"); doc.sortingOrder = -1; go.SetActive(true);
            root = doc.rootVisualElement; root.pickingMode = PickingMode.Ignore; root.style.position = Position.Absolute; root.style.left = root.style.top = root.style.right = root.style.bottom = 0;
            paint = new Painter(); root.Add(paint);
            Q.Changed += () => { if (tracker != null) tracker.Mark(); };
        }
        void EnsureHud()
        {
            if (tracker != null || AldaraHudUI.I == null || AldaraHudUI.I.Root == null) return;
            tracker = new AldaraQuestUI.Tracker(AldaraHudUI.I.Root); AldaraHudUI.I.AddPiece("minimap", tracker.root);
            var mv = AldaraHudUI.I.MinimapView; if (mv != null) { mini = new MiniMarks(); mv.Add(mini); }
        }

        // ---------- labels ----------
        Label Lab(string s, float size, Color c, bool cinzel, bool bold, float x, float y, float alpha = 1, TextAnchor anchor = TextAnchor.LowerCenter, float outline = 0.18f)
        {
            Label l; if (used < pool.Count) l = pool[used]; else { l = new Label { pickingMode = PickingMode.Ignore }; l.style.position = Position.Absolute; l.style.whiteSpace = WhiteSpace.NoWrap; Pad(l, 0, 0); l.style.marginLeft = l.style.marginTop = l.style.marginRight = l.style.marginBottom = 0; l.enableRichText = false; root.Add(l); pool.Add(l); }
            used++; l.style.display = DisplayStyle.Flex; l.enableRichText = false; l.text = s; l.style.fontSize = size; l.style.color = c; l.style.opacity = alpha;
            l.style.unityFontDefinition = FontDefinition.FromFont(Font(cinzel, bold)); l.style.unityTextOutlineWidth = outline; l.style.unityTextOutlineColor = Color.black;
            l.style.textShadow = new TextShadow { color = new Color(0, 0, 0, 0.9f), offset = Vector2.zero, blurRadius = 2 };
            l.style.unityTextAlign = anchor; float w = 600, h = size * 1.6f; l.style.width = w; l.style.height = h;
            l.style.left = anchor == TextAnchor.MiddleLeft ? x : x - w / 2; l.style.top = anchor == TextAnchor.LowerCenter ? y - h : y - h / 2;
            return l;
        }

        // ---------- world to panel ----------
        Camera cam; float k = 1;
        Vector2 W2P(float x, float y)
        {
            var sp = cam.WorldToScreenPoint(AldaraWorld.ToUnity(x, y));
            return RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, Screen.height - sp.y));
        }
        Vector2 W2P3(Vector3 w)
        {
            var sp = cam.WorldToScreenPoint(w);
            return RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, Screen.height - sp.y));
        }
        bool OnView(Vector2 p, float m = 0) { var r = root.layout; return p.x > -m && p.x < r.width + m && p.y > -m && p.y < r.height + m; }

        // ---------- interaction ----------
        public class It { public string npc; public Q.GNode node; public float x, y; public int lore = -1; public string label; }
        public It Interactable()
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I; if (!H.alive || AldaraFolk.I == null) return null; It best = null; float bd = 80;
            foreach (var kv in Q.QOBJ)
            {
                if (kv.Value.far != 0) continue; var p = AldaraFolk.Pos(kv.Key); if (!p.HasValue) continue;
                float d = Vector2.Distance(p.Value, new Vector2(P.x, P.y)); if (d < bd) { bd = d; best = new It { npc = kv.Key, x = p.Value.x, y = p.Value.y }; }
            }
            var nd = Q.NearestNode(P.x, P.y, 52); if (nd != null && (best == null || Vector2.Distance(new Vector2(nd.x, nd.y), new Vector2(P.x, P.y)) < bd)) best = new It { node = nd, x = nd.x, y = nd.y };
            // Valcrest lore you can read
            var lore = Q.J["kd"]["lore"]; float ld = best != null ? Vector2.Distance(new Vector2(best.x, best.y), new Vector2(P.x, P.y)) : 1e9f;
            for (int i = 0; i < lore.Count(); i++)
            {
                var L = lore[i]; if (L["px"] == null || L["px"].Type == Newtonsoft.Json.Linq.JTokenType.Null) continue; float lx = (float)L["px"], ly = (float)L["py"];
                float d = Vector2.Distance(new Vector2(lx, ly), new Vector2(P.x, P.y)); if (d < 75 && d < ld) { ld = d; best = new It { lore = i, x = lx, y = ly, label = (AldaraLore.Read(i) ? "Read again: " : "Read: ") + (string)L["t"] }; }
            }
            return best;
        }
        bool Interact()
        {
            if (AldaraLore.IsOpen) { AldaraLore.Close(); return true; }
            var it = Interactable(); if (it == null) return false;
            if (it.lore >= 0) AldaraLore.Open(it.lore); else if (it.npc != null) AldaraQuestDlg.Open(it.npc); else if (it.node != null) Q.StartGather(it.node);
            return true;
        }
        /// a click in the world on a person or a gathering spot (before the hero attacks there)
        public bool Click(float wx, float wy)
        {
            if (AldaraFolk.I == null) return false; var P = AldaraPlayer.I;
            foreach (var n in AldaraFolk.I.Visible())
            {
                if (n.npc == null || !Q.QOBJ.ContainsKey(n.npc)) continue;
                float top = n.board ? n.y - 60 : n.y - 40;
                if (Mathf.Abs(wx - n.x) < 26 && wy > top - 30 && wy < n.y + 14)
                {
                    if (Vector2.Distance(new Vector2(n.x, n.y), new Vector2(P.x, P.y)) < 150) AldaraQuestDlg.Open(n.npc); else AldaraHud.Banner("Walk closer to talk");
                    return true;
                }
            }
            var nd = Q.NearestNode(wx, wy + 10, 30);
            if (nd != null) { if (Vector2.Distance(new Vector2(nd.x, nd.y), new Vector2(P.x, P.y)) < 60) Q.StartGather(nd); else AldaraHud.Banner("Walk closer to gather"); return true; }
            return false;
        }

        // ---------- per frame ----------
        void Update()
        {
            EnsureHud(); if (tracker != null) tracker.Update();
            var P = AldaraPlayer.I; var H = AldaraHero.I;
            bool live = AldaraSave.Ready && P && H && root != null && root.panel != null && !(AldaraWindows.I && AldaraWindows.I.IsOpen("map"));
            root.style.display = live ? DisplayStyle.Flex : DisplayStyle.None;
            if (!live) { Sync3D(false); return; }
            cam = Camera.main; if (!cam) return;
            if (AldaraKeys.Pressed("interact")) Interact();
            Draw(); Sync3D(true);
            if (mini != null) mini.MarkDirtyRepaint();
        }
        static float Ttime { get { return Time.time; } }
        void Draw()
        {
            used = 0; paint.Clear(); var P = AldaraPlayer.I; float t = Ttime;
            var a = W2P(P.x, P.y); var b = W2P(P.x + 100, P.y); k = Mathf.Max(0.2f, Mathf.Abs(b.x - a.x) / 100f);
            AldaraHeroFx.DrawPet(paint, W2P3, k);
            AldaraHeroFx.Draw(paint, W2P3, k);
            // a mythical pet carries its name over its head
            { var A = AldaraPet.I; var pit = AldaraHero.I.Eq("pet"); if (A && A.Shown && A.Myth && pit != null && A.meta != null) { var gp = W2P3(A.Ground); Lab(pit.name, 12, pit.Col, true, true, gp.x, gp.y - ((float)A.meta["top"] * (float)A.meta["unit"] + 12) * k + 4, 1, TextAnchor.LowerCenter, 0.15f); } }
            // the hero's name tag (drawNametag): level and name, the title under it (glowing titles pulse)
            if (AldaraHero.I.alive)
            {
                var gr = P.model ? P.model.GetComponent<AldaraHeroGear>() : null; float hov = gr && gr.flying ? gr.flyH : 0;
                var g0 = W2P(P.x, P.y); float top = g0.y - (hov + 42) * k; var tt = AldaraGear.TitleById(AldaraHero.I.title);
                var nl = Lab("", 12, C("#f4f0e6"), true, true, g0.x, (tt != null ? top - 13 * k : top) + 4, 1, TextAnchor.LowerCenter, 0.2f);
                nl.enableRichText = true; nl.text = "<color=#ffd35a>Lv " + AldaraHero.I.lvl + "</color> " + AldaraQuestUI.Esc(AldaraHero.I.heroName);
                if (tt != null)
                {
                    var tc = C(tt.col); var tl = Lab(tt.name, 11, tc, true, true, g0.x, top + 4, 1, TextAnchor.LowerCenter, 0.15f);
                    if (tt.glow) { float pu = 0.5f + 0.5f * Mathf.Sin(t * 2.6f); tl.style.textShadow = new TextShadow { color = new Color(tc.r, tc.g, tc.b, 0.7f + 0.3f * pu), offset = Vector2.zero, blurRadius = 6 + 6 * pu }; }
                }
            }
            bool names = AldaraSettings.On("names");
            // townsfolk name tags
            foreach (var n in AldaraFolk.I.Visible())
            {
                if (n.guide || n.board || !AldaraQuests.FOLK_TAG.TryGetValue(n.name, out string tag) || !names) continue;
                float d = Dist(n.x, n.y, P.x, P.y); if (d > 300 || (n.name == "Merchant" && d > 150)) continue;
                var g = W2P(n.x, n.y + 15); Lab(tag, 11, C("#9fe0ff"), false, true, g.x, g.y - (n.top + 6) * k, Mathf.Min(1, (300 - d) / 80));
            }
            // quest givers and boards: names, titles and markers
            foreach (var kv in Q.QOBJ)
            {
                string id = kv.Key; var o = kv.Value; if (o.far != 0) continue; var pos = AldaraFolk.Pos(id); if (!pos.HasValue) continue;
                float d = Dist(pos.Value.x, pos.Value.y, P.x, P.y); if (d > 1400) continue; var N = Q.QNPC[id];
                var f = AldaraFolk.I.Of(id); float topPx = o.board != 0 ? 48 : (f != null ? f.top + 15 : 70);
                var g = W2P(pos.Value.x, o.board != 0 ? pos.Value.y : pos.Value.y + 15); float top = g.y - topPx * k;
                string mk = Q.Marker(id);
                if ((o.q != 0 || o.board != 0) && d < 420 && names)
                {
                    float al = Mathf.Min(1, (420 - d) / 100);
                    Lab(N.name, 12, C("#ffe9a8"), false, true, g.x, top - 6 * k + 4, al);
                    if (!string.IsNullOrEmpty(N.title) && N.board == 0) Lab("<" + N.title + ">", 10, C("#c8b890"), false, false, g.x, top + 7 * k + 3, al);
                }
                if (mk != null)
                {
                    bool low = mk.EndsWith("low"); float y = top - (o.q != 0 || o.board != 0 ? 30 : 22) * k + Mathf.Sin(t * 3 + pos.Value.x) * 3;
                    if (!low) paint.Glow(new Vector2(g.x, y - 9), 22, C("#ffd35a", 0.35f));
                    var l = Lab(mk.Substring(0, 1), 26, low ? C("#9a9a9a") : C("#ffd35a"), true, true, g.x, y + 6, 1, TextAnchor.LowerCenter, 0.12f); l.style.unityTextOutlineColor = C("#1a1008");
                }
            }
            // gathering spots (glow) and the scouting beacons are in 3D; the gather ring is drawn here
            if (Q.gather != null)
            {
                var g = W2P(P.x, P.y); float y = g.y - 72 * k, kk = Mathf.Min(1, Q.gatherT / 1.3f);
                paint.Arc(new Vector2(g.x, y), 14, 0, 360, 5, new Color(0, 0, 0, 0.5f)); paint.Arc(new Vector2(g.x, y), 14, -90, -90 + kk * 360, 5, C(Q.QNODE[Q.gather.type].col));
            }
            // what F does here
            var it = Interactable();
            if (it != null && !AldaraQuestDlg.IsOpen && Q.gather == null && !AldaraLore.IsOpen)
            {
                float y; var g = W2P(it.x, it.npc != null ? it.y + 15 : it.y);
                if (it.npc != null) { var f = AldaraFolk.I.Of(it.npc); float tp = Q.QOBJ[it.npc].board != 0 ? 48 : f != null ? f.top + 15 : 70; y = g.y - tp * k - 58; }
                else y = g.y - (it.lore >= 0 ? 70 : 40) * k;
                string txt = it.label ?? (it.npc != null ? (Q.QNPC[it.npc].board != 0 ? "Read the bounties" : "Talk to " + Q.QNPC[it.npc].name) : "Gather " + Q.QNODE[it.node.type].name);
                float w = txt.Length * 6.1f + 34; string key = AldaraKeys.Name(AldaraKeys.KeyOf("interact"));
                paint.Box(new Rect(g.x - w / 2, y - 12, w, 22), 5, new Color(10 / 255f, 8 / 255f, 16 / 255f, 0.8f), C("#e0b64b"));
                paint.Box(new Rect(g.x - w / 2 + 4, y - 9, 16, 16), 3, C("#e0b64b"), Color.clear);
                Lab(key, 11, C("#1a1008"), true, true, g.x - w / 2 + 12, y - 1, 1, TextAnchor.MiddleCenter, 0);
                Lab(txt, 12, C("#f0e6d0"), false, true, g.x - w / 2 + 25, y - 1, 1, TextAnchor.MiddleLeft, 0.1f);
            }
            // unread Valcrest lore: a soft "?"
            var lore = Q.J["kd"]["lore"];
            if (AldaraFolk.I.KdNear(P.x, P.y)) for (int i = 0; i < lore.Count(); i++)
                {
                    var L = lore[i]; if (L["px"] == null || L["px"].Type == Newtonsoft.Json.Linq.JTokenType.Null || AldaraLore.Read(i)) continue; float lx = (float)L["px"], ly = (float)L["py"];
                    var g = W2P(lx, ly); if (!OnView(g, 40)) continue; float y = g.y - 34 * k + Mathf.Sin(t * 2.4f + i) * 4;
                    paint.Glow(new Vector2(g.x, y), 16, C("#9fe8ff", 0.5f)); Lab("?", 16, C("#e8f8ff"), true, true, g.x, y + 9, 1, TextAnchor.LowerCenter, 0.1f);
                }
            // where to go next
            var wp = Q.Waypoint();
            if (wp != null)
            {
                var g = W2P(wp.x, wp.y); var r = root.layout; float m = 46;
                bool inside = g.x > m && g.x < r.width - m && g.y > m + 40 && g.y < r.height - 150;
                if (inside) { if (wp.npc == null && wp.node == null && wp.mon == null) { float y = g.y - 60 * k + Mathf.Sin(t * 3) * 5; paint.Diamond(new Vector2(g.x, y), 7, 9, C("#ffd35a"), C("#1a1008"), 2); } }
                else
                {
                    float cx = r.width / 2, cy = r.height / 2, ang = Mathf.Atan2(g.y - cy, g.x - cx);
                    float kx = (r.width / 2 - m) / Mathf.Max(1e-6f, Mathf.Abs(Mathf.Cos(ang))), ky = (r.height / 2 - m - 60) / Mathf.Max(1e-6f, Mathf.Abs(Mathf.Sin(ang))), kk = Mathf.Min(kx, ky);
                    float ex = cx + Mathf.Cos(ang) * kk, ey = cy + Mathf.Sin(ang) * kk; int dist = Mathf.RoundToInt(Dist(wp.x, wp.y, P.x, P.y) / 10);
                    paint.Arrow(new Vector2(ex, ey), ang, C("#ffd35a"), C("#1a1008"));
                    Lab(wp.label + "  " + dist + " m", 11, C("#ffe9a8"), false, true, ex - Mathf.Cos(ang) * 30, ey - Mathf.Sin(ang) * 30 + 8, 1, TextAnchor.LowerCenter, 0.15f);
                }
            }
            // floating combat text (floaters), under the windows like the browser's canvas
            foreach (var f in AldaraFx.texts)
            {
                var g = W2P(f.x, f.y); if (!OnView(g, 100)) continue;
                Lab(f.text, 15, f.col, true, true, g.x, g.y + 5, Mathf.Clamp01(f.life * 1.6f), TextAnchor.LowerCenter, 0.2f);
            }
            for (int i = used; i < pool.Count; i++) if (pool[i].style.display != DisplayStyle.None) pool[i].style.display = DisplayStyle.None;
            paint.MarkDirtyRepaint();
        }
        static float Dist(float ax, float ay, float bx, float by) { return Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)); }

        // ---------- 3D: gathering spots and scouting beacons ----------
        readonly Dictionary<Q.GNode, GameObject> nodeViews = new Dictionary<Q.GNode, GameObject>();
        readonly Dictionary<string, GameObject> beacons = new Dictionary<string, GameObject>();
        static Sprite glowSpr, colSpr; static readonly Dictionary<string, Sprite> nodeSpr = new Dictionary<string, Sprite>();
        static Material ringMat;
        void Sync3D(bool on)
        {
            var P = AldaraPlayer.I; float t = Ttime;
            // nodes
            var seen = new HashSet<Q.GNode>();
            if (on) foreach (var n in Q.NODES)
                {
                    seen.Add(n); GameObject v; if (!nodeViews.TryGetValue(n, out v)) { v = MakeNode(n); nodeViews[n] = v; }
                    bool show = n.alive && Mathf.Abs(n.x - P.x) < 1400 && Mathf.Abs(n.y - P.y) < 1000; if (v.activeSelf != show) v.SetActive(show); if (!show) continue;
                    v.transform.position = AldaraWorld.ToUnity(n.x, n.y); v.transform.rotation = cam.transform.rotation;
                    float pul = 0.75f + 0.25f * Mathf.Sin(t * 3 + n.ph); var gl = v.transform.GetChild(0); gl.localScale = Vector3.one * (34 * pul * 2 / AldaraWorld.PX * 1.4f);
                }
            foreach (var kv in new List<KeyValuePair<Q.GNode, GameObject>>(nodeViews)) if (!seen.Contains(kv.Key)) { Destroy(kv.Value); nodeViews.Remove(kv.Key); }
            // beacons over places to scout
            var want = new HashSet<string>();
            if (on) foreach (var inst in Q.S.act)
                {
                    var d = Q.Def(inst);
                    for (int i = 0; i < d.obj.Length; i++) { var o = d.obj[i]; if (o.k != "explore" || Q.ObjDone(inst, i)) continue; var p = Q.Place(o.at); if (p == null) continue; want.Add(o.at); }
                }
            foreach (var at in want)
            {
                GameObject v; var p = Q.Place(at); if (!beacons.TryGetValue(at, out v)) { v = MakeBeacon(); beacons[at] = v; }
                bool show = Mathf.Abs(p.x - P.x) < 1600 && Mathf.Abs(p.y - P.y) < 1200; if (v.activeSelf != show) v.SetActive(show); if (!show) continue;
                v.transform.position = AldaraWorld.ToUnity(p.x, p.y); float pul = 0.7f + 0.3f * Mathf.Sin(t * 2);
                var ring = v.transform.GetChild(0); ring.localRotation = Quaternion.Euler(0, t * 30 / 190 * Mathf.Rad2Deg, 0);
                var rm = ring.GetComponent<MeshFilter>().mesh; var rc = new Color(1, 0.83f, 0.35f, 0.5f * pul); var cols = rm.colors; if (cols.Length > 0 && Mathf.Abs(cols[0].a - rc.a) > 0.02f) { for (int ci = 0; ci < cols.Length; ci++) cols[ci] = rc; rm.colors = cols; }
                var col = v.transform.GetChild(1); col.rotation = cam.transform.rotation; col.GetComponent<SpriteRenderer>().color = new Color(1, 0.83f, 0.35f, 0.45f * pul / 0.45f * 0.6f);
                var gl = v.transform.GetChild(2); gl.rotation = cam.transform.rotation; gl.GetComponent<SpriteRenderer>().color = new Color(1, 0.83f, 0.35f, 0.4f * pul);
            }
            foreach (var at in new List<string>(beacons.Keys)) if (!want.Contains(at)) { Destroy(beacons[at]); beacons.Remove(at); }
        }
        static Sprite Glow()
        {
            if (glowSpr) return glowSpr; int S = 64; var tx = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f)) / (S / 2f); float a = Mathf.Clamp01(1 - d); tx.SetPixel(x, y, new Color(1, 1, 1, a * a)); }
            tx.Apply(); glowSpr = Sprite.Create(tx, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S); return glowSpr;
        }
        static Sprite Column()
        {
            if (colSpr) return colSpr; var tx = new Texture2D(8, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 8; x++) tx.SetPixel(x, y, new Color(1, 1, 1, 1 - y / 63f));
            tx.Apply(); colSpr = Sprite.Create(tx, new Rect(0, 0, 8, 64), new Vector2(0.5f, 0), 8); return colSpr;
        }
        GameObject MakeNode(Q.GNode n)
        {
            var v = new GameObject("node_" + n.type); v.transform.SetParent(transform, false);
            var col = C(Q.QNODE[n.type].col);
            var gl = new GameObject("glow"); gl.transform.SetParent(v.transform, false); gl.transform.localPosition = new Vector3(0, 10 / AldaraWorld.PX, 0);
            var gs = gl.AddComponent<SpriteRenderer>(); gs.sprite = Glow(); gs.color = new Color(col.r, col.g, col.b, 0.35f); gs.sortingOrder = 1;
            Sprite sp; if (!nodeSpr.TryGetValue(n.type, out sp)) { var tx = Resources.Load<Texture2D>("UI/quest/node_" + n.type); sp = tx ? Sprite.Create(tx, new Rect(0, 0, tx.width, tx.height), new Vector2(0.5f, 18f / 80f), 2 * AldaraWorld.PX) : null; nodeSpr[n.type] = sp; }
            var pic = new GameObject("pic"); pic.transform.SetParent(v.transform, false); var ps = pic.AddComponent<SpriteRenderer>(); ps.sprite = sp; ps.sortingOrder = 2;
            return v;
        }
        GameObject MakeBeacon()
        {
            var v = new GameObject("beacon"); v.transform.SetParent(transform, false);
            if (!ringMat) { ringMat = new Material(Shader.Find("Aldara/Water")); ringMat.SetFloat("_Glint", 0); ringMat.SetFloat("_Speed", 0); ringMat.renderQueue = 3100; }
            var ring = new GameObject("ring", typeof(MeshFilter), typeof(MeshRenderer)); ring.transform.SetParent(v.transform, false); ring.transform.localPosition = Vector3.up * 0.1f;
            ring.GetComponent<MeshFilter>().sharedMesh = DashRing(190 / AldaraWorld.PX, 3 / AldaraWorld.PX); var mr = ring.GetComponent<MeshRenderer>(); mr.material = ringMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = new GameObject("column"); col.transform.SetParent(v.transform, false); var cs = col.AddComponent<SpriteRenderer>(); cs.sprite = Column(); col.transform.localScale = new Vector3(32 / 8f * 8 / AldaraWorld.PX, 320 / 64f * 8 / AldaraWorld.PX, 1);
            var gl = new GameObject("glow"); gl.transform.SetParent(v.transform, false); var gs = gl.AddComponent<SpriteRenderer>(); gs.sprite = Glow(); gl.transform.localScale = Vector3.one * (120 / AldaraWorld.PX);
            return v;
        }
        /// the browser's dashed circle (dash 18, gap 12) as a flat ring of quads
        static Mesh DashRing(float R, float w)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var ix = new List<int>(); float circ = 2 * Mathf.PI * R * AldaraWorld.PX; int n = Mathf.RoundToInt(circ / 30);
            for (int i = 0; i < n; i++)
            {
                float a0 = i / (float)n * Mathf.PI * 2, a1 = a0 + 18f / 30f / n * Mathf.PI * 2; int seg = 4;
                for (int s = 0; s <= seg; s++)
                {
                    float a = Mathf.Lerp(a0, a1, s / (float)seg); var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a) / AldaraView.TSP * AldaraView.TSP);
                    v.Add(dir * (R - w / 2)); v.Add(dir * (R + w / 2)); c.Add(Color.white); c.Add(Color.white);
                    if (s < seg) { int b = v.Count - 2; ix.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 }); }
                }
            }
            var m = new Mesh(); m.SetVertices(v); m.SetColors(c); m.SetTriangles(ix, 0); m.RecalculateBounds(); return m;
        }

        // ---------- the painter for marks, boxes and arrows ----------
        public class Painter : VisualElement
        {
            abstract class Cmd { }
            class GlowC : Cmd { public Vector2 p; public float r; public Color c; }
            class ArcC : Cmd { public Vector2 p; public float r, a0, a1, w; public Color c; }
            class BoxC : Cmd { public Rect r; public float rad; public Color fill, line; }
            class DiaC : Cmd { public Vector2 p; public float w, h, lw; public Color fill, line; }
            class ArrC : Cmd { public Vector2 p; public float a; public Color fill, line; }
            class SigC : Cmd { public Vector2 p; public float r, ky, rot; public Color c; }
            class BarC : Cmd { public Rect r; public Color c; }
            readonly List<Cmd> cmds = new List<Cmd>();
            public Painter() { pickingMode = PickingMode.Ignore; style.position = Position.Absolute; style.left = style.top = style.right = style.bottom = 0; generateVisualContent += Gen; }
            public void Clear() { cmds.Clear(); }
            public void Glow(Vector2 p, float r, Color c) { cmds.Add(new GlowC { p = p, r = r, c = c }); }
            public void Arc(Vector2 p, float r, float a0, float a1, float w, Color c) { cmds.Add(new ArcC { p = p, r = r, a0 = a0, a1 = a1, w = w, c = c }); }
            public void Box(Rect r, float rad, Color fill, Color line) { cmds.Add(new BoxC { r = r, rad = rad, fill = fill, line = line }); }
            public void Diamond(Vector2 p, float w, float h, Color fill, Color line, float lw) { cmds.Add(new DiaC { p = p, w = w, h = h, fill = fill, line = line, lw = lw }); }
            public void Arrow(Vector2 p, float a, Color fill, Color line) { cmds.Add(new ArrC { p = p, a = a, fill = fill, line = line }); }
            /// the mythic ground sigil: a flattened rotating ring with eight spokes
            public void Sigil(Vector2 p, float r, float ky, float rot, Color c) { cmds.Add(new SigC { p = p, r = r, ky = ky, rot = rot, c = c }); }
            public void Bar(Rect r, Color c) { cmds.Add(new BarC { r = r, c = c }); }
            void Gen(MeshGenerationContext ctx)
            {
                var g = ctx.painter2D;
                foreach (var c in cmds)
                {
                    if (c is GlowC gc) { for (int i = 6; i >= 1; i--) { g.fillColor = new Color(gc.c.r, gc.c.g, gc.c.b, gc.c.a / 6f); g.BeginPath(); g.Arc(gc.p, gc.r * i / 6f, 0, 360); g.Fill(); } }
                    else if (c is ArcC ac) { g.strokeColor = ac.c; g.lineWidth = ac.w; g.BeginPath(); g.Arc(ac.p, ac.r, ac.a0, ac.a1); g.Stroke(); }
                    else if (c is BoxC bc)
                    {
                        RoundRect(g, bc.r, bc.rad); g.fillColor = bc.fill; g.Fill();
                        if (bc.line.a > 0) { RoundRect(g, bc.r, bc.rad); g.strokeColor = bc.line; g.lineWidth = 1; g.Stroke(); }
                    }
                    else if (c is DiaC dc)
                    {
                        g.BeginPath(); g.MoveTo(dc.p + new Vector2(0, -dc.h)); g.LineTo(dc.p + new Vector2(dc.w, 0)); g.LineTo(dc.p + new Vector2(0, dc.h)); g.LineTo(dc.p + new Vector2(-dc.w, 0)); g.ClosePath();
                        g.fillColor = dc.fill; g.Fill(); g.strokeColor = dc.line; g.lineWidth = dc.lw; g.Stroke();
                    }
                    else if (c is SigC sc)
                    {
                        System.Func<float, float, Vector2> P = (a, rr) => sc.p + new Vector2(Mathf.Cos(a + sc.rot) * rr, Mathf.Sin(a + sc.rot) * rr * sc.ky);
                        g.strokeColor = sc.c; g.lineWidth = 2; g.BeginPath(); for (int i = 0; i <= 40; i++) { var q = P(i / 40f * Mathf.PI * 2, sc.r); if (i == 0) g.MoveTo(q); else g.LineTo(q); } g.Stroke();
                        g.lineWidth = 1; for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2; g.BeginPath(); g.MoveTo(P(a, sc.r * 0.5f / 0.75f)); g.LineTo(P(a, sc.r)); g.Stroke(); }
                    }
                    else if (c is BarC bc2) { g.fillColor = bc2.c; g.BeginPath(); g.MoveTo(new Vector2(bc2.r.xMin, bc2.r.yMin)); g.LineTo(new Vector2(bc2.r.xMax, bc2.r.yMin)); g.LineTo(new Vector2(bc2.r.xMax, bc2.r.yMax)); g.LineTo(new Vector2(bc2.r.xMin, bc2.r.yMax)); g.ClosePath(); g.Fill(); }
                    else if (c is ArrC rc)
                    {
                        float ca = Mathf.Cos(rc.a), sa = Mathf.Sin(rc.a); System.Func<float, float, Vector2> R = (x, y) => rc.p + new Vector2(x * ca - y * sa, x * sa + y * ca);
                        g.BeginPath(); g.MoveTo(R(16, 0)); g.LineTo(R(-8, -11)); g.LineTo(R(-3, 0)); g.LineTo(R(-8, 11)); g.ClosePath();
                        g.fillColor = rc.fill; g.Fill(); g.strokeColor = rc.line; g.lineWidth = 2.5f; g.Stroke();
                    }
                }
            }
            static void RoundRect(Painter2D g, Rect r, float rad)
            {
                g.BeginPath(); g.MoveTo(new Vector2(r.xMin + rad, r.yMin)); g.LineTo(new Vector2(r.xMax - rad, r.yMin)); g.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMin + rad), rad);
                g.LineTo(new Vector2(r.xMax, r.yMax - rad)); g.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMax - rad, r.yMax), rad);
                g.LineTo(new Vector2(r.xMin + rad, r.yMax)); g.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMax - rad), rad);
                g.LineTo(new Vector2(r.xMin, r.yMin + rad)); g.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMin + rad, r.yMin), rad); g.ClosePath();
            }
        }
        // the quest marks and the waypoint on the minimap (qMinimap): 1400 px of world across the map
        class MiniMarks : VisualElement
        {
            public MiniMarks() { pickingMode = PickingMode.Ignore; style.position = Position.Absolute; style.left = style.top = style.right = style.bottom = 0; generateVisualContent += Gen; }
            readonly List<Label> labs = new List<Label>();
            void Gen(MeshGenerationContext ctx)
            {
                var P = AldaraPlayer.I; if (!P || AldaraFolk.I == null) return; float S = contentRect.width; if (!(S > 1)) return; float s = S / 1400f, ox = P.x - 700, oy = P.y - 700;
                var g = ctx.painter2D; int li = 0;
                foreach (var kv in Q.QOBJ)
                {
                    string mk = Q.Marker(kv.Key); if (mk == null) continue; var p = AldaraFolk.Pos(kv.Key); if (!p.HasValue) continue;
                    float x = (p.Value.x - ox) * s, y = (p.Value.y - oy) * s; if (x < 3 || y < 3 || x > S - 3 || y > S - 3) continue;
                    if (li >= labs.Count) { var l = new Label { pickingMode = PickingMode.Ignore }; l.style.position = Position.Absolute; l.style.width = 16; l.style.height = 16; l.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(l, 0, 0); l.style.fontSize = 11; l.style.unityFontDefinition = FontDefinition.FromFont(Font(true, true)); l.style.unityTextOutlineWidth = 0.3f; l.style.unityTextOutlineColor = Color.black; Add(l); labs.Add(l); }
                    var lb = labs[li++]; lb.style.display = DisplayStyle.Flex; lb.text = mk.Substring(0, 1); lb.style.color = mk.EndsWith("low") ? C("#aaaaaa") : C("#ffd35a"); lb.style.left = x - 8; lb.style.top = y - 8;
                }
                for (int i = li; i < labs.Count; i++) labs[i].style.display = DisplayStyle.None;
                var w = Q.Waypoint();
                if (w != null)
                {
                    float x = (w.x - ox) * s, y = (w.y - oy) * s; bool edge = x < 6 || y < 6 || x > S - 6 || y > S - 6;
                    // the map is round: keep the mark inside the circle
                    var c = new Vector2(S / 2, S / 2); var d = new Vector2(x, y) - c; if (d.magnitude > S / 2 - 6) { d = d.normalized * (S / 2 - 6); edge = true; }
                    var q = c + d;
                    g.BeginPath(); g.MoveTo(q + new Vector2(0, -6)); g.LineTo(q + new Vector2(5, 0)); g.LineTo(q + new Vector2(0, 6)); g.LineTo(q + new Vector2(-5, 0)); g.ClosePath();
                    g.fillColor = C("#ffd35a"); g.Fill(); g.strokeColor = Color.black; g.lineWidth = 1.5f; g.Stroke();
                    if (edge && Mathf.FloorToInt(Time.time * 1000 / 400) % 2 == 1) { g.strokeColor = C("#ffd35a"); g.BeginPath(); g.Arc(q, 8, 0, 360); g.Stroke(); }
                }
            }
        }
    }

    // ---------- Valcrest lore you can read (kdOpenLore) ----------
    public static class AldaraLore
    {
        public static int open = -1;
        public static bool IsOpen { get { return AldaraWindows.I && AldaraWindows.I.IsOpen("lore"); } }
        static Newtonsoft.Json.Linq.JObject Kd() { var j = AldaraSave.Raw; if (j == null) return new Newtonsoft.Json.Linq.JObject(); if (!(j["kd"] is Newtonsoft.Json.Linq.JObject k)) { k = new Newtonsoft.Json.Linq.JObject(); j["kd"] = k; } return k; }
        public static bool Read(int i) { return Kd()["lore"] is Newtonsoft.Json.Linq.JArray a && a.Any(x => (int)x == i); }
        public static int Count() { return Kd()["lore"] is Newtonsoft.Json.Linq.JArray a ? a.Count : 0; }
        public static void Open(int i)
        {
            open = i; bool was = Read(i); int total = Q.J["kd"]["lore"].Count();
            if (!was)
            {
                var kd = Kd(); if (!(kd["lore"] is Newtonsoft.Json.Linq.JArray a)) { a = new Newtonsoft.Json.Linq.JArray(); kd["lore"] = a; } a.Add(i);
                var H = AldaraHero.I; var P = AldaraPlayer.I; float xp = Mathf.Max(800, Mathf.Round(H.xpNeed * 0.012f)); H.GainXp(xp); AldaraFx.Text(P.x, P.y - 16 - 60, "+" + xp + " XP  (Lore)", C("#bfe8ff"));
                if (a.Count == total) { AldaraHud.Banner("You have read every tale of Valcrest"); H.GainXp(Mathf.Round(xp * 6)); }
                AldaraSave.Dirty();
            }
            AldaraWindows.I.Toggle("lore", true); AldaraWindows.Refresh("lore");
        }
        public static void Close() { if (AldaraWindows.I) AldaraWindows.I.Toggle("lore", false); }
    }
    public class AldaraLoreWin : AldaraWindows.Win
    {
        public AldaraLoreWin() { id = "lore"; title = "Lore"; }
        public override bool NoTitle => true;
        public override float Y => 120; public override float W => 460;
        public override void Render(VisualElement v)
        {
            int i = AldaraLore.open; var L = Q.J["kd"]["lore"]; if (i < 0 || i >= L.Count()) return;
            var t = T(v, ((string)L[i]["t"]).ToUpper(), 18, C("#efcf78"), true, true, false, 3); t.style.unityTextAlign = TextAnchor.MiddleCenter; t.style.marginBottom = 12; t.style.paddingBottom = 8; t.style.marginTop = -4; t.style.paddingRight = 30; t.style.paddingLeft = 30;
            t.style.backgroundImage = Background.FromTexture2D(Tex("h3line")); t.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            var x = T(v, AldaraQuestUI.Esc((string)L[i]["x"]), 13, C("#d8d2c0"), false, false, true); x.style.marginBottom = 10;
            T(v, "Valcrest lore: " + AldaraLore.Count() + " of " + L.Count() + " read", 11, C("#9a9a9a"));
        }
    }
}
