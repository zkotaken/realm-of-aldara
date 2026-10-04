using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // The glows the browser paints over the hero: light behind the wings and on their tips with falling sparks
    // (drawWingAura / drawWingGlow), and the aura of a mythic set worn four or more pieces deep (drawMythAuraBack /
    // drawMythAuraFront): a soft light, a turning sigil on the ground and motes rising.
    public static class AldaraHeroFx
    {
        static JObject fx; static JObject Fx { get { if (fx == null) { var ta = Resources.Load<TextAsset>("Gear/wingfx_meta"); fx = ta ? JObject.Parse(ta.text) : new JObject(); } return fx; } }
        class Spark { public float x, y, vx, vy, life, max; public bool w; }
        static readonly List<Spark> sparks = new List<Spark>(), motes = new List<Spark>();
        static float last = -1;
        public static void Draw(AldaraQuestWorld.Painter paint, System.Func<Vector3, Vector2> W2P, float k)
        {
            var P = AldaraPlayer.I; if (!P || !P.model) return; var g = P.model.GetComponent<AldaraHeroGear>(); if (!g || !g.gameObject.activeInHierarchy) return;
            float t = Time.time, dt = last < 0 ? 0 : Mathf.Min(0.1f, Time.time - last); last = Time.time;
            float scale = 26 * k; var feet = W2P(g.transform.position); float x = feet.x, gy = feet.y;
            // mythic aura, behind
            if (g.mythGlow != null)
            {
                var col = AldaraRules.Hex(g.mythGlow); float p = 0.85f + 0.15f * Mathf.Sin(t * 2.5f), R = scale * (1.3f + g.mythN * 0.08f) * p;
                paint.Glow(new Vector2(x, gy - scale * 1.1f), R, new Color(col.r, col.g, col.b, 0.3f));
                paint.Sigil(new Vector2(x, gy), scale * 0.75f, 0.38f, t * 0.8f, new Color(col.r, col.g, col.b, 0.55f * p));
            }
            // wings
            JObject W = g.wingName != null && Fx["wings"] != null ? Fx["wings"][g.wingName] as JObject : null;
            var tips = new List<Vector2>();
            if (W != null && g.wingNodes != null)
            {
                float pw = Fx["power"][g.wingRarity] != null ? (float)Fx["power"][g.wingRarity] : 0.8f;
                var glow = AldaraRules.Hex((string)W["glow"] ?? "#ffffff"); float pulse = 0.85f + 0.15f * Mathf.Sin(t * 3);
                foreach (var a in (JArray)W["aura"]) { var n = Node(g, (int)a["n"]); if (!n) continue; var s = W2P(n.TransformPoint(V3(a["p"]))); paint.Glow(s, scale * 1.25f * pw * pulse, new Color(glow.r, glow.g, glow.b, 0.32f * pw)); }
                foreach (var tp in (JArray)W["tips"])
                {
                    var n = Node(g, (int)tp["n"]); if (!n) continue; var s = W2P(n.TransformPoint(V3(tp["p"]))); tips.Add(s);
                    float R = scale * (0.16f + 0.05f * Mathf.Sin(t * 4 + s.x)) * pw;
                    paint.Glow(s, R, new Color(glow.r, glow.g, glow.b, 0.55f)); paint.Glow(s, R * 0.35f, new Color(1, 1, 1, 0.7f * Mathf.Min(1, pw)));
                }
                float rate = (pw > 1 ? 26 : 14) * pw;
                if (tips.Count > 0) for (int n = Mathf.FloorToInt(rate * dt + Random.value); n > 0; n--)
                    {
                        var s = tips[Random.Range(0, tips.Count)];
                        if (sparks.Count < 60) sparks.Add(new Spark { x = (s.x - x) / scale, y = (s.y - gy) / scale, vx = (Random.value - 0.5f) * 0.4f, vy = 0.25f + Random.value * 0.35f, life = 0.9f + Random.value * 0.7f, max = 1.6f });
                    }
                var sc = AldaraRules.Hex((string)W["spark"] ?? "#ffffff");
                for (int i = sparks.Count - 1; i >= 0; i--)
                {
                    var q = sparks[i]; q.life -= dt; if (q.life <= 0) { sparks.RemoveAt(i); continue; } q.x += q.vx * dt; q.y += q.vy * dt;
                    float r = Mathf.Max(0.6f, scale * 0.035f * (q.life / q.max + 0.3f));
                    paint.Diamond(new Vector2(x + q.x * scale, gy + q.y * scale), r, r * 1.8f, new Color(sc.r, sc.g, sc.b, Mathf.Min(1, q.life * 1.4f)), Color.clear, 0);
                }
            }
            else sparks.Clear();
            // mythic aura, in front: motes rising
            if (g.mythGlow != null)
            {
                var col = AldaraRules.Hex(g.mythGlow);
                for (int n = Mathf.FloorToInt((g.mythN >= 6 ? 30 : 18) * dt + Random.value); n > 0; n--) if (motes.Count < 50)
                        motes.Add(new Spark { x = (Random.value - 0.5f) * 1.1f, y = -Random.value * 0.4f, vy = -(0.9f + Random.value * 0.8f), life = 0.7f + Random.value * 0.6f, max = 1.3f, w = Random.value < 0.3f });
                for (int i = motes.Count - 1; i >= 0; i--)
                {
                    var q = motes[i]; q.life -= dt; if (q.life <= 0) { motes.RemoveAt(i); continue; } q.y += q.vy * dt;
                    float a = Mathf.Min(1, q.life * 1.5f) * 0.9f, X = x + q.x * scale, Y = gy + q.y * scale;
                    if (q.w) paint.Bar(new Rect(X - 0.6f, Y - 3.6f, 1.2f, 7.2f), new Color(1, 1, 1, a));
                    else paint.Diamond(new Vector2(X, Y), 1.8f, 3.6f, new Color(col.r, col.g, col.b, a), Color.clear, 0);
                }
            }
            else motes.Clear();
        }
        static Transform Node(AldaraHeroGear g, int i) { return g.wingNodes != null && i >= 0 && i < g.wingNodes.Length ? g.wingNodes[i] : null; }
        static Vector3 V3(JToken a) { return new Vector3((float)a[0], (float)a[1], (float)a[2]); }
    }
}
