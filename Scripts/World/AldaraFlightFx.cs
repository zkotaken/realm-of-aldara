using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // What flying on wings does to the ground under you (updateFly, drawFlightGround): every downstroke pushes a gust
    // ring across the ground (with a puff of dust for the heavier fliers and a thump for a hovering knight), speed
    // streaks trail you when you fly fast, the wings' colour glows on the ground, and a mage levitates over a turning
    // rune circle with motes rising from it.
    public class AldaraFlightFx : MonoBehaviour
    {
        class Gust { public float x, y, t; public Color col; public bool big, heavy; }
        class Streak { public float x, y, a, t, len; }
        class Mote { public float x, y, vy, t; }
        readonly List<Gust> gusts = new List<Gust>(); readonly List<Streak> streaks = new List<Streak>(); readonly List<Mote> motes = new List<Mote>();
        float prevPh, lastX, lastY, dir; bool have;
        static readonly Dictionary<string, float[]> ST = new Dictionary<string, float[]> {   // dust, streak
            { "knight", new[] { 10f, 0.7f } }, { "mage", new[] { 0f, 0.5f } }, { "archer", new[] { 4f, 1.3f } } };
        static readonly Dictionary<string, string> GUST = new Dictionary<string, string> { { "knight", "#ffd35a" }, { "mage", "#8ab4ff" }, { "archer", "#9fe07a" } };
        static JObject fx; static JObject Fx { get { if (fx == null) { var ta = Resources.Load<TextAsset>("Gear/wingfx_meta"); fx = ta ? JObject.Parse(ta.text) : new JObject(); } return fx; } }
        void Awake() { AldaraVfx.DrawGround += Draw; }
        void OnDestroy() { AldaraVfx.DrawGround -= Draw; }
        AldaraHeroGear Gear() { var P = AldaraPlayer.I; return P && P.model ? P.model.GetComponent<AldaraHeroGear>() : null; }
        string Glow(AldaraHeroGear g) { var W = g.wingName != null && Fx["wings"] != null ? Fx["wings"][g.wingName] as JObject : null; return W != null ? (string)W["glow"] : null; }
        void Update()
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I; var g = Gear(); float dt = Mathf.Min(0.05f, Time.deltaTime);
            for (int i = gusts.Count - 1; i >= 0; i--) { gusts[i].t += dt; if (gusts[i].t > 0.7f) gusts.RemoveAt(i); }
            for (int i = streaks.Count - 1; i >= 0; i--) { streaks[i].t += dt; if (streaks[i].t > 0.35f) streaks.RemoveAt(i); }
            for (int i = motes.Count - 1; i >= 0; i--) { var q = motes[i]; q.t += dt; q.y += q.vy * dt; if (q.t > 1.4f) motes.RemoveAt(i); }
            if (!P || !H || !g || !g.flying) { have = false; return; }
            string cls = H.cls; var S = ST.ContainsKey(cls) ? ST[cls] : ST["knight"];
            if (have && (Mathf.Abs(P.x - lastX) + Mathf.Abs(P.y - lastY)) > 0.3f) dir = Mathf.Atan2(P.y - lastY, P.x - lastX); lastX = P.x; lastY = P.y;
            float C = Mathf.PI * 2, a = prevPh % C, b = g.flyPh % C, mark = Mathf.PI * 1.5f;
            if (have && ((a < mark && b >= mark) || (b < a && a < mark)))
            {
                string gl = Glow(g); var col = AldaraRules.Hex(cls == "mage" ? (gl ?? GUST["mage"]) : GUST.ContainsKey(cls) ? GUST[cls] : GUST["knight"]);
                gusts.Add(new Gust { x = P.x, y = P.y + 18, col = col, big = g.flyM < 0.4f, heavy = cls == "knight" });
                if (cls == "knight" && g.flyM < 0.4f) AldaraVfx.Shake(0.8f, 0.1f);
                for (int i = 0; i < (int)S[0]; i++) { float an = Random.value * Mathf.PI * 2; AldaraVfx.Particle(P.x + Mathf.Cos(an) * 14, P.y + 18 + Mathf.Sin(an) * 5, Mathf.Cos(an) * 90, Mathf.Sin(an) * 30, 0.35f, AldaraRules.Hex("#d8d0c0"), 2); }
            }
            prevPh = g.flyPh; have = true;
            if (g.flyM > 0.35f && Random.value < g.flyM * 0.9f * S[1])
            {
                float back = dir + Mathf.PI, off = (Random.value - 0.5f) * 50;
                streaks.Add(new Streak { x = P.x + Mathf.Cos(back) * 20 - Mathf.Sin(back) * off, y = P.y - 30 + Mathf.Sin(back) * 20 + Mathf.Cos(back) * off * 0.5f - Random.value * 30, a = dir, len = 24 + Random.value * 30 });
            }
            if (cls == "mage" && Random.value < dt * 14 && motes.Count < 30) motes.Add(new Mote { x = P.x + (Random.value - 0.5f) * 40, y = P.y + 14, vy = -(20 + Random.value * 25) });
        }
        static Color A(Color c, float a) { c.a = a; return c; }
        void Draw(AldaraVfx V)
        {
            var P = AldaraPlayer.I; var H = AldaraHero.I; var g = Gear(); var N = V.GroundN; var Ad = V.GroundA;
            bool fly = P && H && g && g.flying; string gl = g ? Glow(g) : null; var glow = AldaraRules.Hex(gl ?? "#ffffff");
            if (fly)
            {
                float x = P.x, gy = P.y + 15;
                AldaraVfx.Glow(Ad, x, gy, 34, glow, 0.22f, 1, 12);
                if (H.cls == "mage") Rune(Ad, x, gy, 30 + Mathf.Sin(g.flyPh) * 2, AldaraRules.Hex(gl ?? "#8ab4ff"));
            }
            foreach (var q in motes) { float u = q.t / 1.4f; AldaraVfx.Circle(Ad, q.x + Mathf.Sin(q.t * 4 + q.x) * 4, q.y, 1.6f, A(u < 0.3f ? Color.white : AldaraRules.Hex(gl ?? "#8ab4ff"), 0.8f * (1 - u)), 1); }
            foreach (var q in gusts)
            {
                float u = q.t / 0.7f, R = ((q.big ? 16 : 12) + u * (q.big ? 70 : 50)) * (q.heavy ? 1.3f : 1);
                AldaraVfx.Ring(Ad, q.x, q.y, R, R * 0.32f, 2.5f * (1 - u) + 0.5f, A(q.col, 0.55f * (1 - u)), 1);
                AldaraVfx.Ring(Ad, q.x, q.y, R * 0.7f, R * 0.22f, 1, A(Color.white, 0.35f * (1 - u)), 1);
            }
            foreach (var st in streaks)
            {
                float u = st.t / 0.35f; Ad.Line(st.x, st.y, st.x - Mathf.Cos(st.a) * st.len, st.y - Mathf.Sin(st.a) * st.len, 1.6f * (1 - u) + 0.4f, A(u < 0.5f ? Color.white : glow, 0.45f * (1 - u)), 1);
            }
        }
        // drawRuneCircle at full strength, squashed onto the ground
        static void Rune(AldaraSketch s, float x, float y, float R, Color col)
        {
            const float K = 0.42f; float t = Time.time, rot = t * 2.4f;
            AldaraVfx.Glow(s, x, y, R * 1.1f, col, 0.35f, 1, R * 1.1f * K);
            AldaraVfx.Ring(s, x, y, R, R * K, 2.2f, col, 1); AldaraVfx.Ring(s, x, y, R * 0.8f, R * 0.8f * K, 1.2f, col, 1);
            System.Func<float, float, Vector2> E = (an, r) => new Vector2(x + Mathf.Cos(an) * r, y + Mathf.Sin(an) * r * K);
            for (int k = 0; k < 2; k++) { var pts = new List<Vector2>(); for (int i = 0; i <= 3; i++) pts.Add(E(rot + k * Mathf.PI / 3 + i * Mathf.PI * 2 / 3, R * 0.8f)); s.Polyline(pts, 1.2f, col, false, 1); }
            for (int i = 0; i < 12; i++) { float an = rot + i / 12f * Mathf.PI * 2; var c = E(an, R * 0.9f); var d = E(an, R * 0.9f + 3); var e = E(an, R * 0.9f - 3); s.Line(e.x, e.y, d.x, d.y, 3, Color.white, 1); }
            AldaraVfx.Ring(s, x, y, R * 0.45f, R * 0.45f * K, 1, col, 1);
            for (int i = 0; i < 6; i++) { var c = E(-rot * 1.3f + i / 6f * Mathf.PI * 2, R * 0.45f); AldaraVfx.Ring(s, c.x, c.y, R * 0.08f, R * 0.08f * K, 1, col, 1); }
        }
    }
}
