using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // The monsters of Aldara, enhanced (Graphics, Enhanced rendering, Menacing monsters):
    // - every monster model gets a darker, harsher hide with glowing veins crawling over it, a glow from below and the richer shading the townsfolk have, with an aura in its land's colour (sickly green in
    //   the fields and the fen, ember orange in the waste, frost blue on the peaks, violet in the void and Gloomwood, blood
    //   red on the Bloodmoor), stronger the higher its tier, flaring while it is fighting you;
    // - bosses stand on a slowly turning ring of jagged runes with embers or motes rising off them, and a light of their
    //   colour on them.
    public class AldaraFoeFx : MonoBehaviour
    {
        public static AldaraFoeFx I;
        static readonly Color[] ZONE = {
            new Color(0.78f, 0.95f, 0.45f), new Color(1f, 0.55f, 0.3f), new Color(0.5f, 1f, 0.62f), new Color(0.55f, 0.85f, 1f),
            new Color(1f, 0.42f, 0.15f), new Color(0.76f, 0.42f, 1f), new Color(1f, 0.9f, 0.6f), new Color(1f, 0.5f, 0.4f),
            new Color(0.62f, 0.42f, 1f), new Color(1f, 0.82f, 0.45f), new Color(1f, 0.16f, 0.14f) };
        readonly Dictionary<AldaraMonsters.Mon, float> applied = new Dictionary<AldaraMonsters.Mon, float>();
        readonly List<Light> lights = new List<Light>();
        MaterialPropertyBlock mpb; static readonly int RimId = Shader.PropertyToID("_NpcRim"), FoeId = Shader.PropertyToID("_FoeFx");
        readonly List<AldaraMonsters.Mon> drop = new List<AldaraMonsters.Mon>();

        void Awake() { I = this; AldaraVfx.DrawGround += DrawGround; AldaraVfx.DrawAir += DrawAir; mpb = new MaterialPropertyBlock(); }
        void OnDestroy() { AldaraVfx.DrawGround -= DrawGround; AldaraVfx.DrawAir -= DrawAir; }
        static bool On { get { return AldaraSettings.On("hq") && AldaraSettings.On("foeFx"); } }

        public static Color ColorOf(AldaraMonsters.Mon m)
        {
            if (!string.IsNullOrEmpty(m.color) && m.dun) { var c = AldaraRules.Hex(m.color); return Color.Lerp(c, Color.white, 0.15f); }
            int z = Mathf.Clamp(m.tier - 1, 0, ZONE.Length - 1); if (m.def != null && m.def.tier > 0) z = Mathf.Clamp(m.def.tier - 1, 0, ZONE.Length - 1);
            return ZONE[z];
        }
        static float StrengthOf(AldaraMonsters.Mon m)
        {
            if (m.dead) return 0;
            float s = m.boss ? 1.1f : 0.62f + Mathf.Clamp(m.tier, 1, 11) * 0.04f;
            if (m.aggroT > 0 || m.engT > 0) s *= 1.35f;
            return Mathf.Round(s * 20) / 20;   // in steps, so the blocks change rarely
        }
        void Update()
        {
            var M = AldaraMonsters.I; if (!M || !AldaraSave.Ready) return; bool on = On;
            foreach (var m in M.all)
            {
                if (!m.view || !m.view.activeInHierarchy) continue;
                float s = on ? StrengthOf(m) : 0; float was;
                if (applied.TryGetValue(m, out was) && Mathf.Abs(was - s) < 0.001f) continue;
                applied[m] = s; var c = ColorOf(m);
                if (m.rends == null) m.rends = m.view.GetComponentsInChildren<Renderer>();
                foreach (var r in m.rends) { if (!r) continue; r.GetPropertyBlock(mpb); mpb.SetColor(RimId, new Color(c.r, c.g, c.b, s)); mpb.SetFloat(FoeId, s <= 0 ? 0 : (m.boss ? 1.1f : 0.35f + Mathf.Clamp(m.tier, 1, 11) * 0.05f) * (m.aggroT > 0 || m.engT > 0 ? 1.4f : 1f)); r.SetPropertyBlock(mpb); }
            }
            // forget monsters that are gone
            drop.Clear(); foreach (var kv in applied) if (kv.Key.view == null) drop.Add(kv.Key); foreach (var d in drop) applied.Remove(d);
            Lights(on);
        }
        void Lights(bool on)
        {
            var M = AldaraMonsters.I; var P = AldaraPlayer.I; if (!P) return;
            while (lights.Count < 3) { var go = new GameObject("AldaraFoeLight"); go.transform.SetParent(transform, false); var l = go.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel; l.range = 4.5f; l.enabled = false; lights.Add(l); }
            var best = new List<AldaraMonsters.Mon>();
            if (on) foreach (var m in M.all) if (m.boss && !m.dead && m.view && m.view.activeInHierarchy && (m.x - P.x) * (m.x - P.x) + (m.y - P.y) * (m.y - P.y) < 1500 * 1500) best.Add(m);
            best.Sort((a, b) => ((a.x - P.x) * (a.x - P.x) + (a.y - P.y) * (a.y - P.y)).CompareTo((b.x - P.x) * (b.x - P.x) + (b.y - P.y) * (b.y - P.y)));
            float night = AldaraPost.Night;
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i]; if (i >= best.Count) { if (l.enabled) l.enabled = false; continue; }
                var m = best[i]; l.enabled = true; l.color = ColorOf(m); l.intensity = (0.8f + 1.6f * night) * (0.8f + 0.2f * Mathf.Sin(Time.time * 2.3f + i));
                l.transform.position = AldaraWorld.ToUnity(m.x, m.y) + Vector3.up * (m.r * 1.6f / AldaraWorld.PX + 0.6f);
            }
        }
        void DrawGround(AldaraVfx V)
        {
            if (!On || AldaraMonsters.I == null) return; var A = V.GroundA; float t = Time.time;
            foreach (var m in AldaraMonsters.I.all)
            {
                if (m.dead || !m.view || !m.view.activeInHierarchy) continue;
                var c = ColorOf(m); float h = AldaraVfx.LZ(m.x, m.y), ky = 0.42f;
                if (!m.boss)
                {   // a faint pool of its colour under it while it fights
                    AldaraVfx.Glow(A, m.x, m.y, m.r * 1.6f, c, (m.aggroT > 0 || m.engT > 0) ? 0.2f : 0.07f, h, m.r * 1.6f * ky);
                    continue;
                }
                float r = Mathf.Max(40, m.r * 1.5f), pul = 0.75f + 0.25f * Mathf.Sin(t * 2.1f), a = 0.5f * pul, rot = -t * 0.25f;
                AldaraVfx.Glow(A, m.x, m.y, r * 1.4f, c, 0.3f * pul, h, r * 1.4f * ky);
                A.Arc(m.x, m.y, r, r * ky, 0, Mathf.PI * 2, 2.2f, AldaraVfx.A(c, a), h);
                A.Arc(m.x, m.y, r * 0.8f, r * 0.8f * ky, 0, Mathf.PI * 2, 1.2f, AldaraVfx.A(c, a * 0.6f), h);
                // jagged teeth around the ring
                for (int k = 0; k < 14; k++)
                {
                    float an = rot + k * Mathf.PI * 2 / 14, an2 = an + Mathf.PI / 14;
                    float x1 = m.x + Mathf.Cos(an) * r, y1 = m.y + Mathf.Sin(an) * r * ky, x2 = m.x + Mathf.Cos(an + 0.11f) * r * 1.16f, y2 = m.y + Mathf.Sin(an + 0.11f) * r * 1.16f * ky, x3 = m.x + Mathf.Cos(an2) * r, y3 = m.y + Mathf.Sin(an2) * r * ky;
                    A.Line(x1, y1, x2, y2, 1.6f, AldaraVfx.A(c, a * 0.9f), h); A.Line(x2, y2, x3, y3, 1.6f, AldaraVfx.A(c, a * 0.9f), h);
                }
            }
        }
        void DrawAir(AldaraVfx V)
        {
            if (!On || AldaraMonsters.I == null) return; var A = V.AirA; float t = Time.time;
            foreach (var m in AldaraMonsters.I.all)
            {
                if (m.dead || !m.boss || !m.view || !m.view.activeInHierarchy) continue;
                var c = ColorOf(m); float h = AldaraVfx.LZ(m.x, m.y), seed = (m.x * 0.011f + m.y * 0.017f) % 1, r = Mathf.Max(40, m.r * 1.4f);
                for (int k = 0; k < 12; k++)
                {
                    float ph = Mathf.Repeat(t * 0.3f + k / 12f + seed, 1), ang = k * 2.39f + seed * 6;
                    float x = m.x + Mathf.Cos(ang) * r * (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(ang * 2))), y = m.y + Mathf.Sin(ang) * r * 0.4f, up = ph * (m.r * 2.6f + 40);
                    float a = Mathf.Sin(ph * Mathf.PI) * 0.8f;
                    AldaraVfx.Glow(A, x, y - up, 6, c, a * 0.5f, h);
                    AldaraVfx.Circle(A, x, y - up, 1.2f, AldaraVfx.A(Color.Lerp(c, Color.white, 0.4f), a), h);
                }
            }
        }
    }
}
