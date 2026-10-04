using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Aldara
{
    // The browser's scene lighting and post-processing (drawPostFX), for the world and the dungeons alike:
    // the time of day (an 18 minute day and night), each region's mood blended across its borders, drifting cloud
    // shadows, the light map (ambient with every lamp, campfire, glowing tree, lava field, the hero, pet and bosses
    // added on top, multiplied over the view), hot light cores, depth haze and sun shafts, the region's weather,
    // the vignette and the low health heartbeat.
    public class AldaraPost : MonoBehaviour
    {
        public static AldaraPost I;
        public const float DAY_LEN = 18 * 60;
        public struct Lt { public float x, y, r, i; public Color c; }

        // ---- data from the browser ----
        static float[][] ZAMB, ZGRADE; static float[] ZGA; static string[] ZWX; static float[] NIGHT = { 0.42f, 0.48f, 0.76f };
        static int[][] SEEDS; static Lt[] WL; static bool[] WLsp; static float[] WLfl; static Dictionary<long, List<int>> WLB;
        const float LB = 1024;
        static void LoadData()
        {
            if (WL != null) return; var ta = Resources.Load<TextAsset>("world_lights"); if (!ta) { WL = new Lt[0]; WLB = new Dictionary<long, List<int>>(); return; }
            var J = JObject.Parse(ta.text); var cols = new List<Color>(); foreach (var c in J["C"]) cols.Add(AldaraRules.Hex((string)c));
            var L = (JArray)J["L"]; WL = new Lt[L.Count]; WLsp = new bool[L.Count]; WLfl = new float[L.Count]; WLB = new Dictionary<long, List<int>>();
            for (int k = 0; k < L.Count; k++)
            {
                var a = L[k]; WL[k] = new Lt { x = (float)a[0], y = (float)a[1], r = (float)a[2], i = (float)a[3], c = cols[(int)a[5]] }; int fl = (int)a[4]; WLfl[k] = fl; WLsp[k] = fl == 2;
                float R = WL[k].r; for (int by = Mathf.FloorToInt((WL[k].y - R) / LB); by <= Mathf.FloorToInt((WL[k].y + R) / LB); by++) for (int bx = Mathf.FloorToInt((WL[k].x - R) / LB); bx <= Mathf.FloorToInt((WL[k].x + R) / LB); bx++)
                    { long key = ((long)by << 20) ^ (bx & 0xfffff); List<int> li; if (!WLB.TryGetValue(key, out li)) WLB[key] = li = new List<int>(); li.Add(k); }
            }
            var S = (JArray)J["S"]; SEEDS = new int[S.Count][]; for (int k = 0; k < S.Count; k++) SEEDS[k] = new[] { (int)S[k][0], (int)S[k][1], (int)S[k][2] };
            var Z = (JArray)J["Z"]; int n = Z.Count; ZAMB = new float[n][]; ZGRADE = new float[n][]; ZGA = new float[n]; ZWX = new string[n];
            for (int k = 0; k < n; k++) { ZAMB[k] = Z[k]["amb"].ToObject<float[]>(); ZGRADE[k] = Z[k]["grade"].ToObject<float[]>(); ZGA[k] = (float)Z[k]["ga"]; ZWX[k] = (string)Z[k]["wx"]; }
            if (J["N"] != null) NIGHT = J["N"].ToObject<float[]>();
        }

        // ---- noise, exactly the browser's ----
        static float Hash2(int ix, int iy, int seed) { unchecked { int h = ix * 374761393 + iy * 668265263 + seed * 982451653; h = (h ^ (int)((uint)h >> 13)) * 1274126177; h ^= (int)((uint)h >> 16); return (uint)h / 4294967296f; } }
        static float VNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y); float fx = x - ix, fy = y - iy, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
            float a = Hash2(ix, iy, seed), b = Hash2(ix + 1, iy, seed), c = Hash2(ix, iy + 1, seed), d = Hash2(ix + 1, iy + 1, seed);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }
        static float Fbm(float x, float y, int seed) { return VNoise(x, y, seed) * 0.57f + VNoise(x * 2.1f, y * 2.1f, seed + 7) * 0.29f + VNoise(x * 4.3f, y * 4.3f, seed + 13) * 0.14f; }
        static float SStep(float a, float b, float v) { float t = Mathf.Clamp01((v - a) / (b - a)); return t * t * (3 - 2 * t); }
        /// zoneCalc: the region here, the nearest other region and the distance to the border between them
        public static int ZoneCalc(float x, float y, out int z2, out float edge)
        {
            LoadData(); var D = new double[11]; for (int i = 0; i < 11; i++) D[i] = 1e18;
            double wx = x + (Fbm(x * 0.00045f, y * 0.00045f, 11) - 0.5) * 2400, wy = y + (Fbm(x * 0.00045f, y * 0.00045f, 29) - 0.5) * 2400;
            foreach (var s in SEEDS) { double dx = wx - s[1], dy = wy - s[2], d = dx * dx + dy * dy; if (d < D[s[0]]) D[s[0]] = d; }
            int z1 = 0; for (int i = 1; i < 11; i++) if (D[i] < D[z1]) z1 = i;
            z2 = z1 == 0 ? 1 : 0; for (int i = 0; i < 11; i++) if (i != z1 && D[i] < D[z2]) z2 = i;
            edge = (float)(System.Math.Sqrt(D[z2]) - System.Math.Sqrt(D[z1])); return z1;
        }

        // ---- time of day ----
        static float t0Offset;
        /// 0..1 through the day, starting mid-morning like the browser
        public static float DayP { get { return ((Time.time - t0Offset) / DAY_LEN + 0.3f) % 1f; } }
        public static void SetTime(float p) { t0Offset = Time.time - (((p - 0.3f) % 1 + 1) % 1) * DAY_LEN; }
        static void DayLight(out float k, out float dusk)
        {
            float p = DayP;
            if (p < 0.62f && p > 0.05f) { k = 1; dusk = 0; return; }
            if (p >= 0.62f && p < 0.72f) { float u = (p - 0.62f) / 0.1f; k = 1 - u; dusk = Mathf.Sin(u * Mathf.PI); return; }
            if (p >= 0.72f && p < 0.93f) { k = 0; dusk = 0; return; }
            float w = p >= 0.93f ? (p - 0.93f) / 0.12f : (p + 0.07f) / 0.12f; k = w; dusk = Mathf.Sin(w * Mathf.PI) * 0.7f;
        }
        public static float Night { get { if (AldaraWorld.Dun) return 0; float k, d; DayLight(out k, out d); return 1 - k; } }

        // ---- state ----
        readonly float[] amb = { 1, 1, 1 }, grade = { 0, 0, 0 }; float gradeA;
        AldaraSketch cores, wxA, wxAdd, haze, vig; Material lightMat, raysMat; Texture2D lightTex, raysTex; Mesh lightMesh, raysMesh; Color32[] lpx; float[] lacc;
        static float[] cloud; const int CN = 256; const float LS = 0.25f;

        void Awake() { I = this; }
        void Init()
        {
            if (cores != null) return; LoadData();
            cores = new AldaraSketch("add", true, 3301); wxA = new AldaraSketch("normal", true, 3302); wxAdd = new AldaraSketch("add", true, 3303);
            haze = new AldaraSketch("screen", true, 3304); vig = new AldaraSketch("normal", true, 3306);
            lightMat = AldaraSketch.Mat(UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.Zero, true, 3300);
            Bloom();
            BuildRays(); raysMat = AldaraSketch.Mat(UnityEngine.Rendering.BlendMode.OneMinusDstColor, UnityEngine.Rendering.BlendMode.One, true, 3305, raysTex);
        }

        // the browser's bloom pass (bright pixels blurred and added back at 0.45), done with the render pipeline's bloom
        UnityEngine.Rendering.Volume vol; UnityEngine.Rendering.Universal.Bloom bloom;
        void Bloom()
        {
            var go = new GameObject("AldaraBloom"); go.transform.SetParent(transform, false); vol = go.AddComponent<UnityEngine.Rendering.Volume>(); vol.isGlobal = true; vol.priority = 10;
            var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); bloom = prof.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.threshold.Override(0.85f); bloom.intensity.Override(0.6f); bloom.scatter.Override(0.6f); bloom.highQualityFiltering.Override(false); vol.sharedProfile = prof;
        }
        void LateUpdate()
        {
            var cam = Camera.main; var P = AldaraPlayer.I; var Hh = AldaraHero.I; if (!cam || !P || Hh == null || !AldaraWorld.Loaded) return; Init();
            bool dun = AldaraWorld.Dun; var D = AldaraDungeon.DM; if (dun && D == null) return;
            if (vol) vol.enabled = AldaraSettings.On("bloom"); var cd = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam); if (cd && !cd.renderPostProcessing) cd.renderPostProcessing = true;
            float t = Time.time, dt = Time.deltaTime;
            float vh = cam.orthographicSize * 2 * AldaraWorld.PX, vw = vh * cam.aspect;
            Vector2 c = AldaraCamera.I ? AldaraCamera.I.ScreenToWorldPx(new Vector2(Screen.width / 2f, Screen.height / 2f)) : new Vector2(P.x, P.y);
            float x0 = c.x - vw / 2, y0 = c.y - vh / 2, gh = dun ? 0 : AldaraWorld.HeightPx(P.x, P.y);
            cores.Clear(); wxA.Clear(); wxAdd.Clear(); haze.Clear(); vig.Clear();
            cores.h0 = wxA.h0 = wxAdd.h0 = haze.h0 = vig.h0 = gh;

            // region mood, day and night
            float dk, dusk; DayLight(out dk, out dusk); float night = dun ? 0 : 1 - dk;
            float[] A, G; float ga; string wxKind;
            if (dun) { A = D.amb; G = D.grade; ga = 0.22f; wxKind = D.wx; }
            else
            {
                int z2; float e; int z = ZoneCalc(P.x, P.y, out z2, out e); float k = e < 500 ? 0.5f * (1 - e / 500) : 0;
                A = new float[3]; G = new float[3]; for (int i = 0; i < 3; i++) { A[i] = ZAMB[z][i] + (ZAMB[z2][i] - ZAMB[z][i]) * k; G[i] = ZGRADE[z][i] + (ZGRADE[z2][i] - ZGRADE[z][i]) * k; }
                ga = ZGA[z] + (ZGA[z2] - ZGA[z]) * k; wxKind = k > 0.45f ? ZWX[z2] : ZWX[z];
                for (int i = 0; i < 3; i++) A[i] = A[i] * (1 - night) + A[i] * NIGHT[i] * night;
                if (dusk > 0) { A[0] *= 1 + 0.06f * dusk; A[1] *= 1 - 0.08f * dusk; A[2] *= 1 - 0.2f * dusk; float[] warm = { 255, 120, 60 }; for (int i = 0; i < 3; i++) G[i] += (warm[i] - G[i]) * dusk * 0.7f; ga += 0.08f * dusk; }
            }
            float ease = 1 - Mathf.Pow(0.96f, dt * 60);
            for (int i = 0; i < 3; i++) { amb[i] += (A[i] - amb[i]) * ease; grade[i] += (G[i] - grade[i]) * ease; } gradeA += (ga - gradeA) * ease;

            // the light map
            lightOn = false; if (AldaraSettings.On("light")) LightMap(cam, dun, D, x0, y0, vw, vh, gh, t, night);
            // atmosphere: haze at the top of the view, sun shafts by day
            if (!dun && AldaraSettings.On("atmos") && AldaraSettings.On("light")) Atmos(cam, x0, y0, vw, vh, gh, t, dk, dusk);
            // weather
            if (!string.IsNullOrEmpty(wxKind) && AldaraSettings.On("weather")) Weather(wxKind, vw, vh, t, Mathf.Min(0.05f, dt), night, P.x, P.y);
            // vignette and the low health heartbeat
            float cx = x0 + vw / 2, cy = y0 + vh / 2, diag = Mathf.Sqrt(vw * vw + vh * vh), mn = Mathf.Min(vw, vh);
            if (AldaraSettings.On("vignette")) Radial(cx, cy, mn * 0.38f, diag * 0.58f, new[] { 0f, 1f }, new[] { new Color(4 / 255f, 2 / 255f, 10 / 255f, 0), new Color(4 / 255f, 2 / 255f, 10 / 255f, 0.55f) }, 1);
            if (AldaraSettings.On("lowHpFx") && Hh.alive && Hh.maxHp > 0)
            {
                float f = Hh.hp / Hh.maxHp;
                if (f < 0.3f)
                {
                    float k = 1 - f / 0.3f, beat = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * (4.2f + k * 3))), 6), al = Mathf.Min(1, 0.35f + k * 0.3f + beat * 0.35f);
                    Radial(cx, cy, mn * 0.34f, diag * 0.52f, new[] { 0f, 0.55f, 1f }, new[] { new Color(160 / 255f, 0, 0, 0), new Color(190 / 255f, 16 / 255f, 16 / 255f, 0.16f), new Color(130 / 255f, 0, 0, 0.85f) }, al);
                }
            }
            cores.Draw(cam); wxA.Draw(cam); wxAdd.Draw(cam); haze.Draw(cam); vig.Draw(cam);
        }

        /// a canvas radial gradient between two radii (createRadialGradient(c, r0, c, r1)), drawn over the view
        void Radial(float cx, float cy, float r0, float r1, float[] st, Color[] cs, float alpha)
        {
            var ts = new float[st.Length + 1]; var cc = new Color[st.Length + 1]; ts[0] = 0; cc[0] = cs[0]; cc[0].a *= alpha;
            for (int i = 0; i < st.Length; i++) { ts[i + 1] = (r0 + st[i] * (r1 - r0)) / r1; cc[i + 1] = cs[i]; cc[i + 1].a *= alpha; }
            if (ts[1] <= 0) ts[1] = 0.001f;
            vig.Glow(cx, cy, r1, r1, ts, cc, -1, 64);
        }

        // ---------- the light map ----------
        readonly List<Lt> lights = new List<Lt>();
        /// the lights gathered for this frame's light map, and how strongly they show (for the enhanced local lights)
        public List<Lt> FrameLights { get { return lightOn ? lights : null; } }
        public float FrameBoost; bool lightOn;
        void Add(float x, float y, Color c, float r, float i, float vx0, float vy0, float vx1, float vy1) { if (x + r < vx0 || x - r > vx1 || y + r < vy0 || y - r > vy1) return; lights.Add(new Lt { x = x, y = y, c = c, r = r, i = i }); }
        void LightMap(Camera cam, bool dun, AldaraDungeon.Map D, float x0, float y0, float vw, float vh, float gh, float t, float night)
        {
            int LW = Mathf.CeilToInt(vw * LS), LH = Mathf.CeilToInt(vh * LS); if (LW < 2 || LH < 2) return;
            if (!lightTex || lightTex.width != LW || lightTex.height != LH) { if (lightTex) Destroy(lightTex); lightTex = new Texture2D(LW, LH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear }; lpx = new Color32[LW * LH]; lacc = new float[LW * LH * 3]; }
            // ambient = region light x time of day x a soft colour grade
            float gk = gradeA * 0.7f, mx = Mathf.Max(grade[0], Mathf.Max(grade[1], grade[2])); if (mx <= 0) mx = 1;
            var a3 = new float[3]; for (int i = 0; i < 3; i++) a3[i] = Mathf.Min(1, amb[i] * (1 - gk + gk * grade[i] / mx) * (1 + gk * 0.25f));
            for (int i = 0; i < LW * LH; i++) { lacc[i * 3] = a3[0]; lacc[i * 3 + 1] = a3[1]; lacc[i * 3 + 2] = a3[2]; }
            float inv = 1 / LS;
            if (!dun)
            {
                // drifting cloud shadows over the land by day
                if (AldaraSettings.On("clouds"))
                {
                    if (cloud == null) BuildCloud(); float ca = 0.3f * (1 - night * 0.8f), ox = t * 14, oy = t * 6;
                    for (int j = 0; j < LH; j++)
                    {
                        float wy = y0 + (j + 0.5f) * inv + oy; float fv = ((wy % 1536) + 1536) % 1536 / 1536f * CN; int v0 = (int)fv % CN, v1 = (v0 + 1) % CN; float fy = fv - Mathf.Floor(fv);
                        int row = (LH - 1 - j) * LW;
                        for (int i = 0; i < LW; i++)
                        {
                            float wx = x0 + (i + 0.5f) * inv + ox; float fu = ((wx % 1536) + 1536) % 1536 / 1536f * CN; int u0 = (int)fu % CN, u1 = (u0 + 1) % CN; float fx = fu - Mathf.Floor(fu);
                            float a = (cloud[v0 * CN + u0] * (1 - fx) + cloud[v0 * CN + u1] * fx) * (1 - fy) + (cloud[v1 * CN + u0] * (1 - fx) + cloud[v1 * CN + u1] * fx) * fy; a *= ca; if (a <= 0) continue;
                            int q = (row + i) * 3; lacc[q] += (8 / 255f - lacc[q]) * a; lacc[q + 1] += (12 / 255f - lacc[q + 1]) * a; lacc[q + 2] += (24 / 255f - lacc[q + 2]) * a;
                        }
                    }
                }
                // lava fields glow (the chunk's 32 px emit map)
                LavaEmit(x0, y0, LW, LH, t);
            }
            float vx1 = x0 + vw, vy1 = y0 + vh; lights.Clear();
            if (!dun)
            {
                for (int by = Mathf.FloorToInt(y0 / LB); by <= Mathf.FloorToInt(vy1 / LB); by++) for (int bx = Mathf.FloorToInt(x0 / LB); bx <= Mathf.FloorToInt(vx1 / LB); bx++)
                    {
                        List<int> li; if (!WLB.TryGetValue(((long)by << 20) ^ (bx & 0xfffff), out li)) continue;
                        foreach (int k in li)
                        {
                            var L = WL[k]; // a light spanning several buckets is listed in each; keep the first
                            if (Mathf.FloorToInt(Mathf.Max(L.y - L.r, y0) / LB) != by || Mathf.FloorToInt(Mathf.Max(L.x - L.r, x0) / LB) != bx) continue;
                            float f = WLfl[k] == 0 ? 1 : WLsp[k] ? 0.86f + 0.14f * Mathf.Sin(t * 9 + L.x) * Mathf.Sin(t * 5.3f + L.y) : 0.84f + 0.16f * Mathf.Sin(t * 9 + L.x) * Mathf.Sin(t * 5.3f + L.y);
                            Add(L.x, L.y, L.c, L.r * f, L.i * f, x0, y0, vx1, vy1);
                        }
                    }
            }
            else if (AldaraDungeonView.I) foreach (var L in AldaraDungeonView.I.Gather(D, D.t)) Add(L.x, L.y, L.c, L.r, L.i, x0, y0, vx1, vy1);
            // the hero, mythic gear, pet and bosses
            var P = AldaraPlayer.I; var H = AldaraHero.I; Add(P.x, P.y - 30, AldaraRules.Hex("#ffe6b8"), 200, 0.28f, x0, y0, vx1, vy1);
            foreach (var sl in new[] { "chest", "boots" }) { var ms = AldaraItems.MythSetOf(H.Eq(sl)); if (ms != null) { Add(P.x, P.y - 40, AldaraRules.Hex(ms.glow), 230, 0.5f, x0, y0, vx1, vy1); break; } }
            var pet = AldaraPet.I; var pit = H.Eq("pet");
            if (pet && pet.Shown && pit != null) { string pg; var pc = PET_GLOW.TryGetValue(pit.name, out pg) ? AldaraRules.Hex(pg) : pit.Col; bool ph = pit.name == "Celestial Phoenix"; Add(pet.x, pet.y - 20, pc, ph ? 300 : 150, ph ? 0.8f : 0.35f, x0, y0, vx1, vy1); }
            foreach (var m in AldaraMonsters.I.all) { if (m.dead || !m.boss) continue; Add(m.x, m.y - 30, m.color != null ? AldaraRules.Hex(m.color) : AldaraRules.Hex("#ffe07a"), 240, 0.45f, x0, y0, vx1, vy1); }

            float boost = dun ? 1.4f : 0.45f + night * 0.9f; FrameBoost = boost; lightOn = true;
            foreach (var L in lights)
            {
                float X = (L.x - x0) * LS, Y = (L.y - y0) * LS, R = L.r * LS, a = Mathf.Min(1, L.i * boost); if (a < 0.02f || R < 0.5f) continue;
                int i0 = Mathf.Max(0, Mathf.FloorToInt(X - R)), i1 = Mathf.Min(LW - 1, Mathf.CeilToInt(X + R)), j0 = Mathf.Max(0, Mathf.FloorToInt(Y - R)), j1 = Mathf.Min(LH - 1, Mathf.CeilToInt(Y + R));
                float cr = L.c.r * a, cg = L.c.g * a, cb = L.c.b * a, ir = 1 / R;
                for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++)
                    {
                        float dx = i + 0.5f - X, dy = j + 0.5f - Y, u = Mathf.Sqrt(dx * dx + dy * dy) * ir; if (u >= 1) continue;
                        float p = u < 0.45f ? 1 - 0.6f * u / 0.45f : 0.4f * (1 - (u - 0.45f) / 0.55f); int k = ((LH - 1 - j) * LW + i) * 3;
                        lacc[k] += cr * p; lacc[k + 1] += cg * p; lacc[k + 2] += cb * p;
                    }
                // hot cores glow a little past full brightness
                if (L.i >= 0.45f) { float ca = Mathf.Min(0.3f, (L.i - 0.35f) * 0.35f * boost); if (ca > 0.004f) cores.Glow(L.x, L.y, L.r * 0.45f, L.c, ca); }
            }
            for (int i = 0; i < LW * LH; i++) lpx[i] = new Color32((byte)Mathf.Min(255, lacc[i * 3] * 255), (byte)Mathf.Min(255, lacc[i * 3 + 1] * 255), (byte)Mathf.Min(255, lacc[i * 3 + 2] * 255), 255);
            lightTex.SetPixels32(lpx); lightTex.Apply(false);
            if (!lightMesh) { lightMesh = new Mesh(); lightMesh.MarkDynamic(); }
            Quad(lightMesh, AldaraSketch.W(x0, y0 + vh, gh), AldaraSketch.W(x0 + vw, y0 + vh, gh), AldaraSketch.W(x0 + vw, y0, gh), AldaraSketch.W(x0, y0, gh), Color.white);
            lightMat.mainTexture = lightTex; Graphics.DrawMesh(lightMesh, Matrix4x4.identity, lightMat, 0, cam);
        }
        static readonly Dictionary<string, string> PET_GLOW = new Dictionary<string, string> {
            {"Starfur Kitten","#8ab4ff"},{"Runic Warhound","#5ab0ff"},{"Bloom Sprite","#9fffc0"},{"Crystal Golemling","#bff4ff"},{"Arcane Owl","#c07aff"},{"Ember Wyrmling","#ff9a3a"},
            {"Glacier Wolfling","#8fdfff"},{"Lumen Wisp","#9fffc0"},{"Ninetail Flamefox","#ffb14a"},{"Umbral Panther","#9a5aff"},{"Grove Tortoise","#b0ff8a"},{"Storm Griffon","#8fdfff"},
            {"Prism Scarab","#ff8ae0"},{"Infernal Boar","#ff6a1a"},{"Moonlit Stag","#bfefff"},{"Runeforged Sentinel","#7fd8ff"},{"Elder Dragon","#ffe07a"},{"Celestial Phoenix","#ffb14a"},{"Void Behemoth","#b07aff"} };
        static void Quad(Mesh m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            m.Clear(); m.vertices = new[] { a, b, c, d }; m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }; m.colors = new[] { col, col, col, col };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 }; m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
        }
        // lava cells (32 px) light the map orange, softly stretched like the browser's 16 x 16 emit canvas
        float[] lavaG; int lgW, lgH;
        void LavaEmit(float x0, float y0, int LW, int LH, float t)
        {
            float inv = 1 / LS; int ci0 = Mathf.FloorToInt((x0 - 16) / 32), cj0 = Mathf.FloorToInt((y0 - 16) / 32), ci1 = Mathf.FloorToInt((x0 + LW * inv - 16) / 32) + 1, cj1 = Mathf.FloorToInt((y0 + LH * inv - 16) / 32) + 1;
            lgW = ci1 - ci0 + 1; lgH = cj1 - cj0 + 1; if (lavaG == null || lavaG.Length < lgW * lgH) lavaG = new float[lgW * lgH]; bool any = false;
            for (int j = 0; j < lgH; j++) for (int i = 0; i < lgW; i++) { bool lv = AldaraWorld.LiquidAt((ci0 + i) * 32 + 16, (cj0 + j) * 32 + 16) == 2; lavaG[j * lgW + i] = lv ? 1 : 0; any |= lv; }
            if (!any) return; float al = 0.32f + 0.06f * Mathf.Sin(t * 1.5f), er = 1 * al, eg = 90 / 255f * al, eb = 20 / 255f * al;
            for (int j = 0; j < LH; j++)
            {
                float gy = (y0 + (j + 0.5f) * inv - 16) / 32 - cj0; int b0 = Mathf.Clamp(Mathf.FloorToInt(gy), 0, lgH - 2); float fy = Mathf.Clamp01(gy - b0); int row = (LH - 1 - j) * LW;
                for (int i = 0; i < LW; i++)
                {
                    float gx = (x0 + (i + 0.5f) * inv - 16) / 32 - ci0; int a0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, lgW - 2); float fx = Mathf.Clamp01(gx - a0);
                    float v = (lavaG[b0 * lgW + a0] * (1 - fx) + lavaG[b0 * lgW + a0 + 1] * fx) * (1 - fy) + (lavaG[(b0 + 1) * lgW + a0] * (1 - fx) + lavaG[(b0 + 1) * lgW + a0 + 1] * fx) * fy; if (v <= 0) continue;
                    int q = (row + i) * 3; lacc[q] += er * v; lacc[q + 1] += eg * v; lacc[q + 2] += eb * v;
                }
            }
        }
        static void BuildCloud()
        {
            cloud = new float[CN * CN];
            System.Func<float, float, int, int, float> pv = (x, y, P, seed) =>
            {
                int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y); float fx = x - ix, fy = y - iy, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
                System.Func<int, int, float> h = (i, j) => Hash2(((i % P) + P) % P, ((j % P) + P) % P, seed);
                float a = h(ix, iy), b = h(ix + 1, iy), c = h(ix, iy + 1), d = h(ix + 1, iy + 1); return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
            };
            for (int j = 0; j < CN; j++) for (int i = 0; i < CN; i++) { float v = pv(i / 64f, j / 64f, 4, 501) * 0.6f + pv(i / 32f, j / 32f, 8, 502) * 0.28f + pv(i / 16f, j / 16f, 16, 503) * 0.12f; cloud[j * CN + i] = SStep(0.5f, 0.72f, v); }
        }

        // ---------- depth haze and sun shafts ----------
        void Atmos(Camera cam, float x0, float y0, float vw, float vh, float gh, float t, float dk, float dusk)
        {
            float day = Mathf.Clamp01(dk), al = 0.55f + 0.45f * day;
            var col = new Color(Mathf.Min(255, amb[0] * 190 + 40) / 255f, Mathf.Min(255, amb[1] * 200 + 40) / 255f, Mathf.Min(255, amb[2] * 220 + 50) / 255f);
            System.Func<float, Color> pm = a => new Color(col.r * a * al, col.g * a * al, col.b * a * al, 1);
            float H5 = vh * 0.55f;
            haze.VQuad(new Vector3(x0, y0 + H5 * 0.5f, gh), new Vector3(x0 + vw, y0 + H5 * 0.5f, gh), new Vector3(x0 + vw, y0, gh), new Vector3(x0, y0, gh), pm(0.04f), pm(0.12f));
            haze.VQuad(new Vector3(x0, y0 + H5, gh), new Vector3(x0 + vw, y0 + H5, gh), new Vector3(x0 + vw, y0 + H5 * 0.5f, gh), new Vector3(x0, y0 + H5 * 0.5f, gh), pm(0), pm(0.04f));
            if (day > 0.35f)
            {
                float sc = Mathf.Max(vw, vh) / 512f * 1.6f, drift = (t * 6) % (512 * sc * 0.5f), a = Mathf.Min(1, (day - 0.35f) / 0.4f) * 0.15f * (1 - dusk * 0.3f);
                float ca = Mathf.Cos(0.42f), sa = Mathf.Sin(0.42f), lx0 = -512 * sc * 0.5f - drift * 0.3f, lx1 = lx0 + 512 * sc, ly1 = 512 * sc;
                System.Func<float, float, Vector3> S = (lx, ly) => AldaraSketch.W(x0 + vw * 0.5f + lx * ca - ly * sa, y0 - vh * 0.2f + lx * sa + ly * ca, gh);
                if (!raysMesh) { raysMesh = new Mesh(); raysMesh.MarkDynamic(); }
                // texture v = 1 at the top of the shafts (canvas y = 0)
                raysMesh.Clear(); raysMesh.vertices = new[] { S(lx0, ly1), S(lx1, ly1), S(lx1, 0), S(lx0, 0) }; raysMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                var c4 = new Color(a, a, a, 1); raysMesh.colors = new[] { c4, c4, c4, c4 }; raysMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; raysMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
                Graphics.DrawMesh(raysMesh, Matrix4x4.identity, raysMat, 0, cam);
            }
        }
        void BuildRays()
        {
            const int N = 512; var R = new AldaraRng(911); var acc = new float[N];
            for (int b = 0; b < 16; b++)
            {
                float x = R.Next() * N, bw = 8 + R.Next() * 46, al = Mathf.Round((0.25f + R.Next() * 0.55f) * 100) / 100f;
                for (int i = Mathf.Max(0, Mathf.FloorToInt(x - bw)); i < Mathf.Min(N, Mathf.CeilToInt(x + bw)); i++) { float u = (i + 0.5f - (x - bw)) / (bw * 2); if (u < 0 || u > 1) continue; float a = al * (1 - Mathf.Abs(u * 2 - 1)); acc[i] = a + acc[i] * (1 - a); }
            }
            raysTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color32[N * N];
            for (int j = 0; j < N; j++)
            {
                float cy = 1 - (j + 0.5f) / N; float f = cy < 0.55f ? 1 - 0.5f * cy / 0.55f : 0.5f * (1 - (cy - 0.55f) / 0.45f);   // canvas y from the top
                for (int i = 0; i < N; i++) { float a = acc[i] * f; px[j * N + i] = new Color32((byte)(255 * a), (byte)(240 * a), (byte)(200 * a), 255); }
            }
            raysTex.SetPixels32(px); raysTex.Apply(false);
        }

        // ---------- weather (updateWeather / drawWeather), kept in world space so it drifts past as you move ----------
        class Wp { public float x, y, s, ph; public string k; }
        readonly List<Wp> wx = new List<Wp>();
        void Weather(string kind, float W, float Hh, float t, float dt, float night, float cx, float cy)
        {
            int want = kind == "snow" ? 140 : kind == "fireflies" ? 60 : 90; float hw = W / 2 + 60, hh = Hh / 2 + 60;
            System.Action<Wp, bool> spawn = (p, any) => { p.x = cx + (Random.value * 2 - 1) * hw; p.y = any ? cy + (Random.value * 2 - 1) * hh : (kind == "embers" || kind == "motes" ? cy + hh : cy - hh); p.k = kind; p.s = Random.value; p.ph = Random.value * 7; };
            while (wx.Count < want) { var p = new Wp(); spawn(p, true); wx.Add(p); }
            if (wx.Count > want) wx.RemoveRange(want, wx.Count - want);
            bool glow = kind == "fireflies" || kind == "embers" || kind == "motes" || (kind == "pollen" && night > 0.5f); var G = glow ? wxAdd : wxA;
            foreach (var p in wx)
            {
                if (p.k != kind) spawn(p, true); float s = p.s;
                switch (kind)
                {
                    case "snow": p.y += (28 + s * 46) * dt; p.x += (Mathf.Sin(t * 0.8f + p.ph) * 14 + 10) * dt; break;
                    case "ash": p.y += (12 + s * 16) * dt; p.x += (Mathf.Sin(t * 0.6f + p.ph) * 18 + 14) * dt; break;
                    case "pollen": case "petals": p.y += (Mathf.Sin(t * 0.9f + p.ph) * 8 + 6) * dt; p.x += (16 + s * 14) * dt; break;
                    case "fireflies": p.x += Mathf.Sin(t * 0.7f + p.ph) * 22 * dt; p.y += Mathf.Cos(t * 0.9f + p.ph * 1.3f) * 18 * dt; break;
                    case "embers": case "motes": p.y -= (22 + s * 40) * dt; p.x += Mathf.Sin(t * 1.3f + p.ph) * 16 * dt; break;
                }
                if (p.x < cx - hw || p.x > cx + hw || p.y < cy - hh || p.y > cy + hh) spawn(p, false);
                float x = p.x, y = p.y;
                switch (kind)
                {
                    case "snow": { float r = 0.8f + s * 2.2f; G.Ellipse(x, y, r, r, new Color(1, 1, 1, 0.55f + s * 0.4f), -1, 0, 8); break; }
                    case "ash": G.Rect(x, y, 1.5f + s * 2, 1 + s * 1.4f, new Color(70 / 255f, 64 / 255f, 60 / 255f, 0.35f + s * 0.3f)); break;
                    case "pollen":
                        if (night > 0.5f) { float b = Mathf.Max(0, Mathf.Sin(t * 2 + p.ph * 3)), r = 1.5f + s; G.Ellipse(x, y, r, r, new Color(220 / 255f, 1, 120 / 255f, b * 0.9f), -1, 0, 8); G.Ellipse(x, y, 7, 7, new Color(200 / 255f, 1, 80 / 255f, b * 0.18f), -1, 0, 12); }
                        else { float r = 0.8f + s * 1.3f; G.Ellipse(x, y, r, r, new Color(1, 252 / 255f, 230 / 255f, 0.5f + s * 0.4f), -1, 0, 8); }
                        break;
                    case "petals": G.Ellipse(x, y, 2.6f + s * 1.6f, 1.3f, s < 0.5f ? new Color(1, 190 / 255f, 220 / 255f, 0.8f) : new Color(1, 236 / 255f, 160 / 255f, 0.75f), -1, t * 1.5f + p.ph, 10); break;
                    case "fireflies": { float b = Mathf.Max(0, Mathf.Sin(t * 2.2f + p.ph * 3)), r = 1.4f + s * 0.8f; G.Ellipse(x, y, r, r, new Color(210 / 255f, 1, 110 / 255f, 0.2f + b * 0.8f), -1, 0, 8); G.Ellipse(x, y, 8, 8, new Color(170 / 255f, 1, 90 / 255f, b * 0.16f), -1, 0, 12); break; }
                    case "embers": { float k = 0.5f + 0.5f * Mathf.Sin(t * 6 + p.ph * 5); G.Rect(x, y, 1.4f + s * 1.6f, 2 + s * 2.4f, new Color(1, (int)(120 + k * 100) / 255f, 40 / 255f, 0.55f + k * 0.4f)); break; }
                    case "motes": { float k = 0.5f + 0.5f * Mathf.Sin(t * 3 + p.ph * 4), r = 1 + s * 1.8f; G.Ellipse(x, y, r, r, new Color(190 / 255f, 150 / 255f, 1, 0.35f + k * 0.55f), -1, 0, 8); break; }
                }
            }
        }
    }
}
