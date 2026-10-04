using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aldara
{
    // The browser's living orbs (orbTick, orbDraw): the health and mana orbs painted every frame as a liquid with two
    // rolling wave layers, a pale ghost of the level just lost, rising bubbles, embers over the health and a turning
    // rune ring over the mana, caustic light, a ripple when hit, and a heartbeat glow when health runs low.
    // Painted by a shader (Aldara/LivingOrb) into a render texture: the 200 px canvas of the browser, with a margin for the low-health glow.
    public class LivingOrb
    {
        const int S = 200, M = 60, T = S + M * 2; const float CX = 100, CY = 100;
        public readonly bool hp; public readonly RenderTexture tex; readonly Material mat;
        float v = 1, lag = 1, lagT, t = Random.value * 9, prev = 1;
        class Bub { public float x, y, r, s, w; } class Emb { public float x, y, vy, life, w; }
        readonly List<Bub> bub = new List<Bub>(); readonly List<Emb> emb = new List<Emb>(); readonly List<float> rip = new List<float>();
        readonly Vector4[] vb = new Vector4[24], ve = new Vector4[24], vr = new Vector4[6];
        static Shader sh;
        public LivingOrb(bool isHp)
        {
            hp = isHp; tex = new RenderTexture(T, T, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = isHp ? "OrbHp" : "OrbMp", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; tex.Create();
            if (!sh) sh = Shader.Find("Aldara/LivingOrb"); mat = new Material(sh);
            System.Func<string, Vector4> G = h => { var c = AldaraRules.Hex(h); return new Vector4(c.r, c.g, c.b, 1); };
            mat.SetVector("_Bg0", G(hp ? "#1a0806" : "#080c22")); mat.SetVector("_Bg1", G(hp ? "#050101" : "#010206"));
            mat.SetVector("_L0", G(hp ? "#ff9a78" : "#a8d0ff")); mat.SetVector("_L1", G(hp ? "#e0321e" : "#3a6ae8")); mat.SetVector("_L2", G(hp ? "#4a0604" : "#0a1a5a"));
            mat.SetVector("_Ghost", hp ? new Vector4(1, 214 / 255f, 180 / 255f, 0.42f) : new Vector4(214 / 255f, 232 / 255f, 1, 0.4f));
            mat.SetVector("_Lay2", hp ? new Vector4(1, 120 / 255f, 90 / 255f, 1) : new Vector4(140 / 255f, 190 / 255f, 1, 1));
            mat.SetVector("_Caus", hp ? new Vector4(1, 160 / 255f, 120 / 255f, 0.22f) : new Vector4(160 / 255f, 210 / 255f, 1, 0.25f));
            mat.SetFloat("_Hp", hp ? 1 : 0); mat.SetFloat("_M", M); mat.SetFloat("_TS", T); mat.SetFloat("_Lin", QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0);
        }
        /// the element showing the orb, and the orb's rect grown by the margin it is placed in
        public VisualElement Element() { var e = new VisualElement { pickingMode = PickingMode.Ignore }; e.style.backgroundImage = Background.FromRenderTexture(tex); return e; }
        public static Rect Grow(Rect r) { float k = r.width / S; return new Rect(r.x - M * k, r.y - M * k, T * k, T * k); }

        public void Tick(float val, float dt)
        {
            float tgt = Mathf.Clamp01(val); t += dt;
            if (tgt < prev - 0.02f) rip.Add(0); prev = tgt;
            v += (tgt - v) * Mathf.Min(1, dt * 9);
            if (tgt >= lag) { lag = tgt; lagT = 0; } else { lagT += dt; if (lagT > 0.5f) lag = Mathf.Max(tgt, lag - dt * 0.9f); }
            float lvl = S - v * S; bool low = hp && v < 0.3f;
            float beat = low ? Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 4.2f)), 8) + 0.6f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 4.2f - 0.9f)), 12) : 0;
            if (Random.value < dt * (hp ? 5 : 8) && v > 0.05f && bub.Count < 24) bub.Add(new Bub { x = 30 + Random.value * 140, y = S + 4, r = 1.5f + Random.value * 2.5f, s = 18 + Random.value * 26, w = Random.value * 6 });
            for (int i = bub.Count - 1; i >= 0; i--) { var b = bub[i]; b.y -= b.s * dt; b.x += Mathf.Sin(t * 3 + b.w) * 0.3f; if (b.y < lvl + 4) bub.RemoveAt(i); }
            if (hp)
            {
                if (Random.value < dt * 6 && emb.Count < 24) emb.Add(new Emb { x = 40 + Random.value * 120, y = lvl, vy = -(14 + Random.value * 20), life = 1.2f, w = Random.value * 6 });
                for (int i = emb.Count - 1; i >= 0; i--) { var e = emb[i]; e.life -= dt; e.y += e.vy * dt; e.x += Mathf.Sin(t * 4 + e.w) * 0.4f; if (e.life <= 0 || e.y > lvl + 6) emb.RemoveAt(i); }
            }
            for (int i = rip.Count - 1; i >= 0; i--) { float u = rip[i] + dt * 2.2f; if (u > 1) rip.RemoveAt(i); else rip[i] = u; }
            while (rip.Count > 6) rip.RemoveAt(0);
            for (int i = 0; i < 24; i++) { vb[i] = i < bub.Count ? new Vector4(bub[i].x, bub[i].y, bub[i].r, 0) : Vector4.zero; ve[i] = i < emb.Count ? new Vector4(emb[i].x, emb[i].y, 0.8f * Mathf.Min(1, emb[i].life), (120 + 80 * emb[i].life) / 255f) : Vector4.zero; }
            for (int i = 0; i < 6; i++) vr[i] = i < rip.Count ? new Vector4(0, 0, rip[i], 1) : Vector4.zero;
            mat.SetFloat("_T", t); mat.SetFloat("_Lvl", lvl); mat.SetFloat("_Lag", S - lag * S); mat.SetFloat("_Beat", beat); mat.SetFloat("_Low", low ? 1 : 0);
            mat.SetVectorArray("_Bub", vb); mat.SetVectorArray("_Emb", ve); mat.SetVectorArray("_Rip", vr);
            if (!tex.IsCreated()) tex.Create();
            Graphics.Blit(null, tex, mat);
        }
    }
}
