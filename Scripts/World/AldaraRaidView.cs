using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Aldara.AldaraUI;
using R = Aldara.AldaraRaid;

namespace Aldara
{
    // How a raid looks (raidDrawGround / raidDrawAir): the plates, the Oracles' glyph pads and their Eye, the levers, the
    // regents' sigils, the Sunwell and its Light Wells, embers on the floor and over your head, the darkness of a Total
    // Eclipse and shields round immune bosses. And the raid box under the dungeon box (#raidHud).
    public class AldaraRaidView : MonoBehaviour
    {
        AldaraSketch gA, gAdd, aA, aAdd;
        void LateUpdate()
        {
            if (!R.On || !AldaraDungeon.Active) { Hud(false); return; }
            var cam = Camera.main; if (!cam) return;
            if (gA == null) { gA = new AldaraSketch("normal", false, 3003); gAdd = new AldaraSketch("add", false, 3004); aA = new AldaraSketch("normal", true, 3112); aAdd = new AldaraSketch("add", true, 3113); }
            gA.Clear(); gAdd.Clear(); aA.Clear(); aAdd.Clear();
            float t = Time.time; Ground(t); Air(t, cam);
            gA.Draw(cam); gAdd.Draw(cam); aA.Draw(cam); aAdd.Draw(cam);
            Hud(true);
        }
        static Color Hx(string h, float a = 1) { var c = AldaraRules.Hex(h); c.a = a; return c; }
        void Disc(AldaraSketch g, float x, float y, float r, string col, float a, bool fill)
        {
            g.Arc(x, y, r, r * 0.55f, 0, 6.3f, 3, Hx(col, a)); if (fill) g.Ellipse(x, y, r, r * 0.55f, Hx(col, a * 0.35f));
        }
        void ArcF(AldaraSketch g, float x, float y, float r, float f, string col) { if (f <= 0) return; g.Arc(x, y, r, r * 0.55f, -Mathf.PI / 2, -Mathf.PI / 2 + f * Mathf.PI * 2, 5, Hx(col)); }
        void Glow(AldaraSketch g, float x, float y, float r, string col, float a) { var c = Hx(col); g.Glow(x, y, r, r * 0.6f, new[] { 0f, 1f }, new[] { new Color(c.r, c.g, c.b, a), new Color(c.r, c.g, c.b, 0) }); }
        void Glyph(AldaraSketch g, int k, float x, float y, float s, string col)
        {
            var c = Hx(col); System.Func<float, float, Vector2> P = (a, b) => new Vector2(x + a, y + b * 0.55f);
            System.Action<float, float, float, float> L = (a, b, cc, d) => g.Line(P(a, b).x, P(a, b).y, P(cc, d).x, P(cc, d).y, 3, c);
            System.Action<float, float, float, float, float> Ac = (cx, cy, r, a0, a1) => { int n = 24; for (int i = 0; i < n; i++) { float u0 = a0 + (a1 - a0) * i / n, u1 = a0 + (a1 - a0) * (i + 1) / n; L(cx + Mathf.Cos(u0) * r, cy + Mathf.Sin(u0) * r, cx + Mathf.Cos(u1) * r, cy + Mathf.Sin(u1) * r); } };
            switch (k)
            {
                case 0: Ac(0, 0, s, 0, 6.283f); L(-s, 0, s, 0); break;
                case 1: L(0, -s, s * 0.9f, s * 0.7f); L(s * 0.9f, s * 0.7f, -s * 0.9f, s * 0.7f); L(-s * 0.9f, s * 0.7f, 0, -s); break;
                case 2: L(0, -s, s, 0); L(s, 0, 0, s); L(0, s, -s, 0); L(-s, 0, 0, -s); L(0, -s * 0.4f, 0, s * 0.4f); break;
                case 3: Ac(0, 0, s, 0.6f, 5.7f); Ac(0, 0, s * 0.35f, 0, 6.283f); break;
                case 4: L(-s, -s, s, s); L(s, -s, -s, s); L(0, -s, 0, s); break;
                case 5: { var pts = new Vector2[5]; for (int j = 0; j < 5; j++) { float a = -Mathf.PI / 2 + j * Mathf.PI * 4 / 5; pts[j] = new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s); } for (int j = 0; j < 5; j++) L(pts[j].x, pts[j].y, pts[(j + 1) % 5].x, pts[(j + 1) % 5].y); break; }
            }
        }
        void Ground(float t)
        {
            var E = R.E; var C = R.RENC;
            var c0 = C[0];
            for (int k = 0; k < 3; k++)
            {
                var q = c0.plates[k]; float f = E != null && E.type == "plates" ? E.pl[k] : (R.done[0] ? 1 : 0);
                Disc(gA, q.x, q.y, c0.pr, f >= 1 ? "#fff0b0" : "#d9a93a", 0.85f, f >= 1); ArcF(gA, q.x, q.y, c0.pr - 8, f, "#f2c46a");
                if (E != null && E.type == "plates" && E.con[k] != 0) Disc(gA, q.x, q.y, c0.pr + 6, "#ff5a4a", 0.5f + 0.3f * Mathf.Sin(t * 8), false);
                if (f >= 1) Glow(gAdd, q.x, q.y, c0.pr * 1.2f, "#f2c46a", 0.3f);
            }
            for (int s = 0; s < C.Length; s++)
            {
                var cc = C[s];
                if (cc.type == "oracle")
                {
                    bool on = E != null && E.type == "oracle";
                    for (int k = 0; k < cc.pads.Length; k++)
                    {
                        var q = cc.pads[k]; bool lit = on && E.lit == k, good = on && E.oph == "input" && E.good.Contains(k), bad = on && E.bad == k && E.oph == "pause";
                        string col = lit ? "#bfe8ff" : bad ? "#ff5a4a" : good ? "#7adf8a" : "#6a8ab0"; Disc(gA, q.x, q.y, cc.pr, col, lit ? 1 : 0.75f, lit);
                        Glyph(gA, k, q.x, q.y, cc.pr * 0.45f, col); if (lit) Glow(gAdd, q.x, q.y, cc.pr * 1.6f, "#bfe8ff", 0.45f);
                    }
                }
                if (cc.type == "levers")
                {
                    bool on = E != null && E.type == "levers";
                    for (int k = 0; k < 3; k++) { var q = cc.levers[k]; bool ok = R.done[s] || (on && E.ok[k] != 0); float f = on ? E.lv[k] : 0; Disc(gA, q.x, q.y, 56, ok ? "#7adf8a" : "#d9a93a", 0.8f, ok); ArcF(gA, q.x, q.y, 48, ok ? 1 : f, "#f2c46a"); }
                }
                if (cc.type == "twins")
                {
                    bool on = E != null && E.type == "twins";
                    for (int k = 0; k < 2; k++) { var q = cc.pads[k]; bool ch = on && E.ch[k] != 0; string col = k == 1 ? "#ffd35a" : "#9a5aff"; Disc(gA, q.x, q.y, 80, col, ch ? 1 : 0.5f, ch); if (ch) { ArcF(gA, q.x, q.y, 70, E.pad[k], "#ffffff"); Glow(gAdd, q.x, q.y, 120, col, 0.4f + 0.15f * Mathf.Sin(t * 5)); } }
                }
                if (cc.type == "sov")
                {
                    bool on = E != null && E.type == "sov"; var q = cc.center; float f = on ? (E.vul > 0 ? 1 : E.dep / (float)E.need) : 0;
                    Disc(gA, q.x, q.y, 95, "#f2c46a", 0.9f, f > 0); ArcF(gA, q.x, q.y, 86, f, "#fff0b0"); Glow(gAdd, q.x, q.y, 140, "#f2c46a", 0.12f + 0.5f * f * (on && E.vul > 0 ? 0.8f + 0.2f * Mathf.Sin(t * 6) : 0.6f));
                    if (on) foreach (var w in E.wells) { Disc(gA, w.x, w.y, cc.wr, "#fff4d0", 0.9f, true); Glow(gAdd, w.x, w.y, cc.wr * 1.4f, "#fff4d0", 0.5f); }
                }
            }
        }
        void Air(float t, Camera cam)
        {
            var E = R.E; var C = R.RENC; var P = AldaraPlayer.I;
            // the Oracle Eye
            var oc = C[2]; { var q = oc.center; bool on = E != null && E.type == "oracle"; float ey = q.y - 150 + Mathf.Sin(t * 1.3f) * 6; bool lit = on && E.lit >= 0;
                Glow(aAdd, q.x, ey, 70, lit ? "#bfe8ff" : "#6a8ab0", 0.5f); aA.Ellipse(q.x, ey, 34, 22, Hx("#0a0e1a")); aA.Arc(q.x, ey, 34, 22, 0, 6.3f, 2, Hx("#9ad8ff"));
                float rr = 10 + (lit ? 4 : 0); aA.Ellipse(q.x, ey, rr, rr, Hx(lit ? "#e8f8ff" : "#4a7ab0"));
                if (lit && E.oph == "show") { var p = oc.pads[E.lit]; aAdd.Line(q.x, ey, p.x, p.y, 6, new Color(191 / 255f, 232 / 255f, 1, 0.7f)); Glyph(aA, E.lit, q.x, ey - 58, 20, "#e8f8ff"); } }
            // levers
            var lc = C[3]; { bool on = E != null && E.type == "levers";
                for (int k = 0; k < 3; k++) { var q = lc.levers[k]; bool ok = R.done[3] || (on && E.ok[k] != 0); aA.Rect(q.x - 10, q.y - 18, 20, 18, Hx("#2a2230")); float a = ok ? 0.9f : -0.9f; float tx = q.x + Mathf.Sin(a) * 38, ty = q.y - 12 - Mathf.Cos(a) * 38;
                    aA.Line(q.x, q.y - 12, tx, ty, 5, Hx("#d9a93a")); aA.Ellipse(tx, ty, 6, 6, Hx(ok ? "#7adf8a" : "#f2c46a")); } }
            if (E != null && E.type == "sov")
            {
                foreach (var q in E.sp) { float y = q.y - 18 - Mathf.Sin(t * 3 + q.x) * 5; Glow(aAdd, q.x, y, 26, "#ffd35a", 0.9f); aA.Ellipse(q.x, y, 5, 5, Hx("#fff8d8")); }
                foreach (var w in E.wells) aAdd.VQuad(new Vector3(w.x - 40, w.y, 1), new Vector3(w.x + 40, w.y, 1), new Vector3(w.x + 40, w.y - 320, 1), new Vector3(w.x - 40, w.y - 320, 1), new Color(1, 244 / 255f, 208 / 255f, 0.45f), new Color(1, 244 / 255f, 208 / 255f, 0));
                if (E.ecl < 8 && E.ecl > 0)
                {
                    float a = Mathf.Min(0.55f, (8 - E.ecl) / 8 * 0.55f), vh = cam.orthographicSize * 2 * AldaraWorld.PX, vw = vh * cam.aspect;
                    aA.Rect(P.x - vw, P.y - vh, vw * 2, vh * 2, new Color(8 / 255f, 2 / 255f, 16 / 255f, a));
                    foreach (var w in E.wells) Glow(aAdd, w.x, w.y, C[E.s].wr * 1.5f, "#fff4d0", 0.55f);
                }
            }
            // shields round immune bosses
            foreach (var m in AldaraMonsters.I.all)
            {
                if (m.dead || !m.rImm) continue; float y = m.y - m.r * 1.1f, a = 0.18f + 0.06f * Mathf.Sin(t * 4); var c = new Color(154 / 255f, 90 / 255f, 1, 0);
                aAdd.Glow(m.x, y, m.r * 1.9f, m.r * 2.1f, new[] { 0.316f, 0.863f, 1f }, new[] { c, new Color(c.r, c.g, c.b, a), c });
            }
            // embers over your head
            int n = E != null && E.type == "sov" ? E.carry : 0;
            for (int j = 0; j < n; j++) { float x = P.x - (n - 1) * 6 + j * 12, y = P.y - 92 + Mathf.Sin(t * 4 + j) * 2; Glow(aAdd, x, y, 10, "#ffd35a", 0.9f); aA.Ellipse(x, y, 3, 3, Hx("#fff8d8")); }
        }

        // ---------- the raid box ----------
        VisualElement box, bars, seq; Label title, sub, obj, warn, foot; string sig;
        void Hud(bool on)
        {
            var hud = AldaraHudUI.I; if (hud == null || hud.Root == null) return;
            if (box == null)
            {
                if (!on) return;
                box = new VisualElement { pickingMode = PickingMode.Ignore }; hud.Root.Add(box); box.style.position = Position.Absolute; box.style.left = Length.Percent(50); box.style.translate = new Translate(Length.Percent(-50), 0); box.style.top = 172; box.style.width = 380;
                box.style.backgroundColor = new Color(14 / 255f, 10 / 255f, 19 / 255f, 0.84f); Border(box, 1, C("#d9a93a", 0.55f), 10); Pad(box, 8, 12, 9, 12); box.style.alignItems = Align.Center;
                title = T(box, "", 15, C("#f2c46a"), true, true); title.style.unityTextAlign = TextAnchor.MiddleCenter; Shadow(title, C("#f2c46a", 0.35f), 8, 0);
                sub = T(box, "", 11, C("#b8a888"), true, true, false, 0.8f, true); sub.style.unityTextAlign = TextAnchor.MiddleCenter;
                obj = T(box, "", 13, C("#efe4cc")); obj.style.unityTextAlign = TextAnchor.MiddleCenter; obj.style.marginTop = 4;
                bars = new VisualElement { pickingMode = PickingMode.Ignore }; bars.style.alignSelf = Align.Stretch; box.Add(bars);
                seq = Row(box, 6, Justify.Center); seq.style.marginTop = 6;
                warn = T(box, "", 13, C("#ff8a7a"), false, true); warn.style.marginTop = 5; warn.style.unityTextAlign = TextAnchor.MiddleCenter;
                foot = T(box, "", 11.5f, C("#9a8e76")); foot.style.marginTop = 5; foot.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            bool show = on && !(AldaraWindows.I && AldaraWindows.I.IsOpen("map"));
            box.style.display = show ? DisplayStyle.Flex : DisplayStyle.None; if (!show || Time.frameCount % 6 != 0) return;
            var B = new List<R.Bar>(); string t, s, o, w, f; List<int> dots; R.Hud(out t, out s, out o, B, out w, out f, out dots);
            string k = t + "|" + s + "|" + o + "|" + w + "|" + f + "|" + (dots == null ? "" : string.Join(",", dots)); foreach (var b in B) k += "|" + b.label + b.val + b.cls + Mathf.Round(b.f * 200);
            if (k == sig) return; sig = k;
            title.text = t; sub.text = s; sub.style.display = s == "" ? DisplayStyle.None : DisplayStyle.Flex; obj.text = o; warn.text = w; warn.style.display = w == "" ? DisplayStyle.None : DisplayStyle.Flex; foot.text = f;
            bars.Clear();
            foreach (var b in B)
            {
                var r = Row(bars, 8); r.style.marginTop = 5;
                var l = T(r, b.label, 12, C("#cdbf9f"), false, false, false, 0, false, false); l.style.minWidth = 88; l.style.unityTextAlign = TextAnchor.MiddleRight;
                var tr = E(r); tr.style.flexGrow = 1; tr.style.height = 8; tr.style.backgroundColor = new Color(1, 1, 1, 0.08f); Border(tr, 1, new Color(1, 1, 1, 0.08f), 5); tr.style.overflow = Overflow.Hidden;
                var fl = E(tr); fl.style.position = Position.Absolute; fl.style.left = 0; fl.style.top = 0; fl.style.bottom = 0; fl.style.width = Length.Percent(b.f * 100);
                fl.style.backgroundColor = b.cls == "imm" ? C("#7a4ad0") : b.cls == "red" ? C("#c0443a") : C("#d9a93a"); fl.style.borderTopLeftRadius = fl.style.borderBottomLeftRadius = fl.style.borderTopRightRadius = fl.style.borderBottomRightRadius = 5;
                var v = T(r, b.val, 12, C("#f2e6c8"), false, false, false, 0, false, false); v.style.minWidth = 40;
            }
            seq.Clear(); seq.style.display = dots == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (dots != null) foreach (var d in dots) { var u = E(seq); u.style.width = u.style.height = 14; Border(u, 1, d != 0 ? C("#7adf8a") : C("#f2c46a", 0.6f), 7); u.style.backgroundColor = d != 0 ? C("#7adf8a") : new Color(1, 1, 1, 0.05f); u.style.marginLeft = u.style.marginRight = 3; }
        }
    }
}
