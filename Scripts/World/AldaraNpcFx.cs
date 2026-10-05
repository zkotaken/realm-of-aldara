using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // The people of Aldara, enhanced (Graphics, Enhanced rendering, Mystical townsfolk):
    // - every NPC model gets richer cloth, gilded trim that glows, grounded feet and a soft coloured aura in the
    //   lit shader (_NpcRim on its renderers);
    // - quest givers stand on a slowly turning golden sigil with motes of light rising around them and a warm light
    //   on them; priests the same in holy blue; merchants, innkeepers, smiths and criers carry a faint amber glow;
    //   guards a steel-blue edge; everyone else a whisper of light at the rim.
    public class AldaraNpcFx : MonoBehaviour
    {
        public static AldaraNpcFx I;
        struct Look { public int kind; public Color rim; public float a; }
        readonly HashSet<GameObject> tagged = new HashSet<GameObject>();
        readonly List<Light> lights = new List<Light>();
        MaterialPropertyBlock mpb; static readonly int RimId = Shader.PropertyToID("_NpcRim");
        bool lastOn = true;

        void Awake() { I = this; AldaraVfx.DrawGround += DrawGround; AldaraVfx.DrawAir += DrawAir; mpb = new MaterialPropertyBlock(); }
        void OnDestroy() { AldaraVfx.DrawGround -= DrawGround; AldaraVfx.DrawAir -= DrawAir; }
        static bool On { get { return AldaraSettings.On("hq") && AldaraSettings.On("npcFx"); } }

        static Look LookOf(AldaraFolk.Folk n)
        {
            string nm = n.name ?? "";
            if (n.board || nm.Contains("Dog")) return new Look { kind = -1 };
            if (n.npc != null && !n.board) return new Look { kind = 1, rim = new Color(1f, 0.78f, 0.38f), a = 0.95f };
            if (nm.Contains("Priest")) return new Look { kind = 2, rim = new Color(0.62f, 0.82f, 1f), a = 0.85f };
            if (nm.Contains("Merchant") || nm.Contains("Innkeeper") || nm.Contains("Blacksmith") || nm.Contains("Crier")) return new Look { kind = 3, rim = new Color(1f, 0.62f, 0.32f), a = 0.6f };
            if (n.guard || nm.Contains("Guard")) return new Look { kind = 4, rim = new Color(0.6f, 0.76f, 1f), a = 0.5f };
            return new Look { kind = 5, rim = new Color(0.92f, 0.86f, 1f), a = 0.3f };
        }
        void Tag(AldaraFolk.Folk n, bool on)
        {
            var L = LookOf(n); var c = on && L.kind >= 0 ? new Color(L.rim.r, L.rim.g, L.rim.b, L.a) : new Color(0, 0, 0, 0);
            foreach (var r in n.view.GetComponentsInChildren<Renderer>(true)) { r.GetPropertyBlock(mpb); mpb.SetColor(RimId, c); r.SetPropertyBlock(mpb); }
        }
        void Update()
        {
            var F = AldaraFolk.I; if (!F || !AldaraSave.Ready) return;
            bool on = On;
            if (on != lastOn) { tagged.Clear(); lastOn = on; }
            foreach (var n in F.Visible()) if (n.view && !tagged.Contains(n.view)) { Tag(n, on); tagged.Add(n.view); }
            Lights(on);
        }
        // a warm light on the nearest quest givers and priests, brightest at night
        void Lights(bool on)
        {
            var F = AldaraFolk.I; var P = AldaraPlayer.I; if (!P) return;
            var best = new List<KeyValuePair<float, AldaraFolk.Folk>>();
            if (on && !AldaraWorld.Dun) foreach (var n in F.Visible()) { var L = LookOf(n); if (L.kind != 1 && L.kind != 2) continue; float d = (n.x - P.x) * (n.x - P.x) + (n.y - P.y) * (n.y - P.y); if (d < 1400 * 1400) best.Add(new KeyValuePair<float, AldaraFolk.Folk>(d, n)); }
            best.Sort((a, b) => a.Key.CompareTo(b.Key));
            while (lights.Count < 4) { var go = new GameObject("AldaraNpcLight"); go.transform.SetParent(transform, false); var l = go.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel; l.range = 3.2f; l.enabled = false; lights.Add(l); }
            float night = AldaraPost.Night;
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i]; if (i >= best.Count) { if (l.enabled) l.enabled = false; continue; }
                var n = best[i].Value; var L = LookOf(n);
                l.enabled = true; l.color = L.rim; l.intensity = (0.5f + 1.4f * night) * (0.85f + 0.15f * Mathf.Sin(Time.time * 1.7f + i));
                l.transform.position = AldaraWorld.ToUnity(n.x, n.y + 15) + Vector3.up * 1.4f;
            }
        }
        void DrawGround(AldaraVfx V)
        {
            if (!On || AldaraFolk.I == null) return; var A = V.GroundA; float t = Time.time;
            foreach (var n in AldaraFolk.I.Visible())
            {
                var L = LookOf(n); if (L.kind != 1 && L.kind != 2 && L.kind != 3) continue;
                float x = n.x, y = n.y + 15, h = AldaraVfx.LZ(x, y), r = L.kind == 3 ? 20 : 30, ky = 0.42f, pul = 0.8f + 0.2f * Mathf.Sin(t * 1.6f + x * 0.01f);
                var c = L.rim; float a = (L.kind == 3 ? 0.28f : 0.55f) * pul;
                AldaraVfx.Glow(A, x, y, r * 1.5f, c, a * 0.45f, h, r * 1.5f * ky);
                if (L.kind == 3) continue;
                A.Arc(x, y, r, r * ky, 0, Mathf.PI * 2, 1.6f, AldaraVfx.A(c, a), h);
                A.Arc(x, y, r * 0.72f, r * 0.72f * ky, 0, Mathf.PI * 2, 1.0f, AldaraVfx.A(c, a * 0.7f), h);
                // eight rune strokes turning between the rings, and a slow star in the middle
                float rot = t * 0.35f;
                for (int k = 0; k < 8; k++)
                {
                    float an = rot + k * Mathf.PI / 4;
                    A.Arc(x, y, r * 0.86f, r * 0.86f * ky, an, an + 0.32f, 2.2f, AldaraVfx.A(c, a * 0.9f), h);
                }
                for (int k = 0; k < 5; k++)
                {
                    float a0 = -rot * 0.6f + k * Mathf.PI * 2 / 5, a1 = a0 + Mathf.PI * 4 / 5;
                    A.Line(x + Mathf.Cos(a0) * r * 0.62f, y + Mathf.Sin(a0) * r * 0.62f * ky, x + Mathf.Cos(a1) * r * 0.62f, y + Mathf.Sin(a1) * r * 0.62f * ky, 0.9f, AldaraVfx.A(c, a * 0.45f), h);
                }
            }
        }
        void DrawAir(AldaraVfx V)
        {
            if (!On || AldaraFolk.I == null) return; var A = V.AirA; float t = Time.time;
            foreach (var n in AldaraFolk.I.Visible())
            {
                var L = LookOf(n); if (L.kind != 1 && L.kind != 2) continue;
                float x0 = n.x, y0 = n.y + 15, h = AldaraVfx.LZ(x0, y0), seed = (x0 * 0.013f + y0 * 0.007f) % 1;
                for (int k = 0; k < 7; k++)
                {
                    float ph = Mathf.Repeat(t * 0.22f + k / 7f + seed, 1), ang = k * 2.39f + seed * 6;
                    float x = x0 + Mathf.Cos(ang) * 22 * (0.5f + 0.5f * Mathf.Sin(ang * 3)), y = y0 + Mathf.Sin(ang) * 9, up = ph * 78 + 6;
                    float a = Mathf.Sin(ph * Mathf.PI) * 0.75f;
                    AldaraVfx.Glow(A, x + Mathf.Sin(t * 1.3f + k) * 3, y - up, 5.5f, L.rim, a * 0.55f, h);
                    AldaraVfx.Circle(A, x + Mathf.Sin(t * 1.3f + k) * 3, y - up, 1.1f, AldaraVfx.A(Color.Lerp(L.rim, Color.white, 0.6f), a), h);
                }
                // a soft halo behind the head
                AldaraVfx.Glow(A, x0, y0 - 52, 26, L.rim, 0.12f + 0.05f * Mathf.Sin(t * 1.2f + seed * 9), h);
            }
        }
    }
}
