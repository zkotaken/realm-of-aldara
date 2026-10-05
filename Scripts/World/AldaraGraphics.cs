using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aldara
{
    // Enhanced rendering on top of the browser's look (Settings, Graphics, "Enhanced rendering"):
    // - shading: ambient occlusion, torch and fire light on the models, a soft sun highlight and sky rim (AldaraLighting.hlsl),
    //   rippled water that catches the sun with depth and foaming shores, fine grain in the ground paint;
    // - a cinematic grade: filmic tone mapping, gentle contrast and colour balance, high-quality bloom, fine film grain, and a
    //   shallow depth of field focused on the hero;
    // - a moving sun: it rises, climbs and sets over the day with warm low light and long shadows, and a cool moon at night;
    // - local lights: the nearest lamps, campfires, lava and glows light the models around them;
    // - living air: fireflies on summer nights, embers over the Emberwaste, motes in the Void, pollen by day, ash on the moor.
    // Every part has its own setting, and turning "Enhanced rendering" off gives back the original look exactly.
    public class AldaraGraphics : MonoBehaviour
    {
        public static AldaraGraphics I;
        static bool On(string k, bool d = true) { return AldaraSettings.On(k, d); }
        public static bool HQ { get { return On("hq"); } }
        Volume vol; ColorAdjustments ca; ShadowsMidtonesHighlights smh; Bloom bl; DepthOfField dof; FilmGrain fg; ChromaticAberration chr;
        ScriptableRendererFeature ssao; Light sun; Quaternion sunRot0; Color sunCol0; float sunInt0; bool sunSaved;
        readonly List<Light> pool = new List<Light>(); float applyT; bool structSet, structOk; bool detailSet, detailOk;
        void Awake() { I = this; AldaraVfx.DrawAir += DrawAir; }
        // the ambient occlusion feature is left on in the renderer asset: a build strips its shaders when it is saved off
        void OnDestroy() { AldaraVfx.DrawAir -= DrawAir; Shader.SetGlobalFloat("_AldaraHQ", 0); RestoreSun(); if (ssao) ssao.SetActive(true); }

        void Build()
        {
            if (vol) return;
            var go = new GameObject("AldaraCinema"); go.transform.SetParent(transform, false); vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 20;
            var p = ScriptableObject.CreateInstance<VolumeProfile>(); vol.sharedProfile = p;
            ca = p.Add<ColorAdjustments>(true); ca.postExposure.Override(0f); ca.contrast.Override(14f); ca.saturation.Override(14f);
            var lgg = p.Add<LiftGammaGain>(true); lgg.lift.Override(new Vector4(1, 1, 1.01f, -0.02f)); lgg.gain.Override(new Vector4(1.01f, 1, 0.98f, 0.02f));
            smh = p.Add<ShadowsMidtonesHighlights>(true); smh.shadows.Override(new Vector4(0.97f, 0.99f, 1.06f, 0)); smh.highlights.Override(new Vector4(1.04f, 1.01f, 0.95f, 0));
            bl = p.Add<Bloom>(true); bl.threshold.Override(0.85f); bl.intensity.Override(0.6f); bl.scatter.Override(0.6f); bl.highQualityFiltering.Override(true);
            dof = p.Add<DepthOfField>(true); dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianMaxRadius.Override(1.2f); dof.highQualitySampling.Override(true);
            fg = p.Add<FilmGrain>(true); fg.type.Override(FilmGrainLookup.Thin1); fg.intensity.Override(0.1f); fg.response.Override(0.85f);
            chr = p.Add<ChromaticAberration>(true); chr.intensity.Override(0.05f);
            // the ambient occlusion feature on the renderer
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (rp != null) foreach (var rd in rp.rendererDataList) if (rd != null) foreach (var f in rd.rendererFeatures) if (f is ScreenSpaceAmbientOcclusion) ssao = f;
            for (int i = 0; i < 8; i++) { var lg = new GameObject("AldaraLocalLight" + i); lg.transform.SetParent(transform, false); var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel; l.enabled = false; pool.Add(l); }
        }
        void Apply()
        {
            // only what changed: switching a renderer feature rebuilds the renderer
            bool hq = HQ; Shader.SetGlobalFloat("_AldaraHQ", hq ? 1 : 0);
            // the ground's high-resolution surface layers (sizes in world units per repeat: grass, dirt, rock, sand, snow, ash, mud, gravel)
            if (!detailSet) { detailSet = true; var da = Resources.Load<Texture2DArray>("World/DetailAlb"); var dn = Resources.Load<Texture2DArray>("World/DetailNor");
                if (da && dn) { Shader.SetGlobalTexture("_DetailAlb", da); Shader.SetGlobalTexture("_DetailNor", dn); detailOk = true; }
                Shader.SetGlobalVector("_DetailSize0", new Vector4(2.7f, 3.8f, 3.6f, 2.1f)); Shader.SetGlobalVector("_DetailSize1", new Vector4(3.4f, 1.6f, 1.8f, 1.6f)); }
            Shader.SetGlobalFloat("_DetailOn", hq && detailOk && On("groundHd") ? 1 : 0);
            // the structures' photographed surfaces (AldaraStructureRemaster)
            if (!structSet) { structSet = true; var sa = Resources.Load<Texture2DArray>("World/StructAlb"); var sn = Resources.Load<Texture2DArray>("World/StructNra");
                if (sa && sn) { Shader.SetGlobalTexture("_StructAlb", sa); Shader.SetGlobalTexture("_StructNra", sn); structOk = true; } }
            Shader.SetGlobalFloat("_StructOn", hq && structOk && On("structHd") ? 1 : 0);
            if (vol.enabled != hq) vol.enabled = hq; bool ao = hq && On("ssao"); if (ssao && ssao.isActive != ao) ssao.SetActive(ao);
            bool b = On("bloom"), d = On("dofOn", false), g = On("grain", true);
            if (bl.active != b) bl.active = b; if (dof.active != d) dof.active = d; if (fg.active != g) { fg.active = g; chr.active = g; }
            // edge smoothing on top of the multisampling: SMAA catches shader edges (rock fissures, lava seams, glints)
            var cam = Camera.main; var cd = cam ? cam.GetComponent<UniversalAdditionalCameraData>() : null;
            if (cd && cd.requiresColorTexture != hq) cd.requiresColorTexture = hq;   // the water looks through to the bed
            if (cd) { var want = hq ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None; if (cd.antialiasing != want) { cd.antialiasing = want; cd.antialiasingQuality = AntialiasingQuality.High; } }
            if (!GetComponent<AldaraGrass>()) gameObject.AddComponent<AldaraGrass>();
            if (!GetComponent<AldaraNpcFx>()) gameObject.AddComponent<AldaraNpcFx>();
            if (!GetComponent<AldaraFoeFx>()) gameObject.AddComponent<AldaraFoeFx>();
        }
        void Update()
        {
            var cam = Camera.main; var P = AldaraPlayer.I; if (!cam || !AldaraWorld.Loaded) return; Build();
            applyT -= Time.unscaledDeltaTime; if (applyT <= 0) { applyT = 0.5f; Apply(); }
            bool hq = HQ;
            // depth of field: the focus sits on the hero
            if (hq && dof.active && P) { var hp = AldaraWorld.ToUnity(P.x, P.y); float d = Vector3.Dot(hp - cam.transform.position, cam.transform.forward); dof.gaussianStart.Override(Mathf.Max(0.1f, d + 9)); dof.gaussianEnd.Override(d + 30); }
            Sun(hq && On("sunCycle"));
            Locals(cam, hq && On("localLights"));
            Air(Mathf.Min(0.05f, Time.deltaTime), hq && On("ambFx"));
        }

        // ---- the sun and the moon ----
        void SaveSun() { if (sunSaved || !sun) return; sunSaved = true; sunRot0 = sun.transform.rotation; sunCol0 = sun.color; sunInt0 = sun.intensity; }
        void RestoreSun() { if (!sunSaved || !sun) return; sun.transform.rotation = sunRot0; sun.color = sunCol0; sun.intensity = sunInt0; }
        void Sun(bool on)
        {
            if (!sun) { sun = RenderSettings.sun; if (!sun) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; } if (!sun) return; }
            SaveSun(); if (!on || AldaraWorld.Dun) { RestoreSun(); return; }
            var e0 = sunRot0.eulerAngles; float p = AldaraPost.DayP, night = AldaraPost.Night;
            // daylight runs from about p 0.95 (dawn) to 0.72 (dusk)
            float u = Mathf.Clamp01((((p + 0.05f) % 1f + 1f) % 1f) / 0.77f), arc = Mathf.Sin(u * Mathf.PI);
            var sunQ = Quaternion.Euler(Mathf.Lerp(18, e0.x, arc), e0.y + Mathf.Lerp(-58, 58, u), 0);
            var moonQ = Quaternion.Euler(52, e0.y + 150, 0);
            sun.transform.rotation = Quaternion.Slerp(sunQ, moonQ, Mathf.SmoothStep(0, 1, night));
            float low = Mathf.Pow(1 - arc, 2.2f);
            var day = Color.Lerp(sunCol0, new Color(1f, 0.70f, 0.48f), low * 0.85f); var moon = new Color(0.60f, 0.70f, 1.0f);
            sun.color = Color.Lerp(day, moon, night); sun.intensity = Mathf.Lerp(sunInt0 * Mathf.Lerp(1f, 0.8f, low), sunInt0 * 0.62f, night);
        }

        // ---- torch and fire light on the models ----
        readonly List<AldaraPost.Lt> pick = new List<AldaraPost.Lt>();
        void Locals(Camera cam, bool on)
        {
            if (!on || AldaraPost.I == null) { foreach (var l in pool) if (l.enabled) l.enabled = false; return; }
            var src = AldaraPost.I.FrameLights; var P = AldaraPlayer.I; float boost = AldaraPost.I.FrameBoost; pick.Clear();
            if (src != null && P) { pick.AddRange(src); pick.Sort((a, b) => Score(b, P).CompareTo(Score(a, P))); }
            for (int i = 0; i < pool.Count; i++)
            {
                var l = pool[i]; if (i >= pick.Count) { if (l.enabled) l.enabled = false; continue; }
                var L = pick[i]; float a = Mathf.Min(1, L.i * boost) * (AldaraWorld.Dun ? 1 : Mathf.Lerp(0.12f, 1, AldaraPost.Night)); if (a < 0.05f) { l.enabled = false; continue; }
                l.enabled = true; l.color = L.c; l.range = L.r / AldaraWorld.PX * 0.9f; l.intensity = a * 2.4f;
                l.transform.position = AldaraWorld.ToUnity(L.x, L.y + 40) + Vector3.up * (60 / AldaraWorld.PX);
            }
        }
        static float Score(AldaraPost.Lt L, AldaraPlayer P) { float dx = L.x - P.x, dy = L.y - P.y; return L.i * Mathf.Sqrt(L.r) / (1 + Mathf.Sqrt(dx * dx + dy * dy) / 500); }

        // ---- living air ----
        class Mote { public float x, y, h, vx, vy, vh, t, life, r, ph; public Color c; public int kind; }
        readonly List<Mote> motes = new List<Mote>();
        float spawnAcc;
        void Air(float dt, bool on)
        {
            var P = AldaraPlayer.I; if (!on || !P) { motes.Clear(); return; }
            float night = AldaraPost.Night; string z = AldaraWorld.Dun ? "dun" : AldaraWorld.ZoneName(P.x, P.y);
            int kind; float rate; Kind(z, night, out kind, out rate);
            spawnAcc += dt * rate;
            int cap = 70; var ps = AldaraSettings.Get("particles"); string pq = ps != null ? (string)ps : "high"; if (pq == "low") cap = 20; else if (pq == "medium") cap = 40; else if (pq == "ultra") cap = 110;
            while (spawnAcc >= 1 && kind >= 0) { spawnAcc -= 1; if (motes.Count < cap) motes.Add(Make(kind, P)); }
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var m = motes[i]; m.t += dt; if (m.t >= m.life || Mathf.Abs(m.x - P.x) > 1300 || Mathf.Abs(m.y - P.y) > 900) { motes.RemoveAt(i); continue; }
                if (m.kind == 0) { m.vx += (Random.value - 0.5f) * 60 * dt; m.vy += (Random.value - 0.5f) * 60 * dt; m.vx *= 0.98f; m.vy *= 0.98f; m.vh = Mathf.Sin(m.t * 1.3f + m.ph) * 8; }
                m.x += m.vx * dt; m.y += m.vy * dt; m.h += m.vh * dt;
            }
        }
        static void Kind(string z, float night, out int kind, out float rate)
        {
            kind = -1; rate = 0;
            if (z == "dun") { kind = 2; rate = 6; return; }
            if (z == "Emberwaste") { kind = 1; rate = 14; return; }
            if (z == "The Void Reach") { kind = 3; rate = 10; return; }
            if (z == "The Bloodmoor") { kind = 4; rate = 9; return; }
            if (z == "Frostpeak Tundra") { kind = 5; rate = 7; return; }
            bool green = z == "Green Fields" || z == "The Shadowfen" || z == "Gloomwood" || z == "The Crownlands" || z == "Elysian Expanse" || z == "Lorenmar";
            if (green && night > 0.35f) { kind = 0; rate = 10 * night; return; }
            if (green || z == "The Brigand Marches" || z == "Valcrest" || z == "Ashen Highlands") { kind = 2; rate = 5; }
        }
        static Mote Make(int kind, AldaraPlayer P)
        {
            var m = new Mote { kind = kind, x = P.x + (Random.value - 0.5f) * 2200, y = P.y + (Random.value - 0.5f) * 1400, ph = Random.value * 7 };
            switch (kind)
            {
                case 0: m.h = 10 + Random.value * 50; m.life = 4 + Random.value * 5; m.r = 7; m.c = new Color(0.78f, 1f, 0.45f); break;                                       // fireflies
                case 1: m.h = Random.value * 20; m.vh = 30 + Random.value * 50; m.vx = 10 + Random.value * 20; m.vy = -6; m.life = 2.5f + Random.value * 2.5f; m.r = 5; m.c = new Color(1f, 0.55f, 0.18f); break;   // embers
                case 2: m.h = 20 + Random.value * 120; m.vx = 6 + Random.value * 8; m.vy = 2; m.vh = (Random.value - 0.5f) * 6; m.life = 6 + Random.value * 6; m.r = 3.2f; m.c = new Color(1f, 0.97f, 0.85f); break;   // dust and pollen
                case 3: m.h = Random.value * 40; m.vh = 18 + Random.value * 22; m.vx = (Random.value - 0.5f) * 10; m.life = 3 + Random.value * 3; m.r = 5; m.c = new Color(0.75f, 0.5f, 1f); break;    // void motes
                case 4: m.h = 60 + Random.value * 120; m.vh = -14 - Random.value * 10; m.vx = 18 + Random.value * 12; m.life = 5 + Random.value * 4; m.r = 3; m.c = new Color(0.55f, 0.18f, 0.16f); break; // ash
                default: m.h = 5 + Random.value * 60; m.vx = 4; m.life = 2 + Random.value * 2; m.r = 3.5f; m.c = new Color(0.85f, 0.95f, 1f); break;                                     // frost glints
            }
            return m;
        }
        void DrawAir(AldaraVfx V)
        {
            if (motes.Count == 0) return; var A = V.AirA; var N = V.AirN;
            foreach (var m in motes)
            {
                float u = m.t / m.life, fade = Mathf.Clamp01(Mathf.Min(u / 0.15f, (1 - u) / 0.25f)), h = AldaraVfx.LZ(m.x, m.y), y = m.y - m.h;
                switch (m.kind)
                {
                    case 0: { float bl = Mathf.Pow(Mathf.Max(0, Mathf.Sin(m.t * 2.2f + m.ph)), 3); float a = fade * (0.15f + 0.85f * bl); AldaraVfx.Glow(A, m.x, y, m.r * 2.4f, m.c, 0.5f * a, h); AldaraVfx.Circle(A, m.x, y, 1.4f, new Color(1, 1, 0.8f, a), h); break; }
                    case 4: N.Rect(m.x - 1.2f, y - 1.2f, 2.4f, 2.4f, new Color(m.c.r, m.c.g, m.c.b, 0.6f * fade), h); break;
                    case 5: { float tw = Mathf.Pow(Mathf.Max(0, Mathf.Sin(m.t * 6 + m.ph)), 6) * fade; AldaraVfx.Glow(A, m.x, y, m.r * 2, m.c, 0.5f * tw, h); A.Line(m.x - 4 * tw, y, m.x + 4 * tw, y, 1, new Color(1, 1, 1, tw), h); A.Line(m.x, y - 4 * tw, m.x, y + 4 * tw, 1, new Color(1, 1, 1, tw), h); break; }
                    default: { float a = fade * (m.kind == 2 ? 0.28f : 0.6f); AldaraVfx.Glow(A, m.x, y, m.r * 2, m.c, a * 0.6f, h); AldaraVfx.Circle(A, m.x, y, m.kind == 2 ? 0.9f : 1.2f, new Color(m.c.r, m.c.g, m.c.b, a), h); break; }
                }
            }
        }
    }
}
