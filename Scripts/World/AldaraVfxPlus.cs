using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // Enhanced effects (Graphics, enhanced rendering, Enhanced effects): on top of the browser's canvas effects
    // (AldaraVfx), every hit, spell and blast also throws real light on the world around it and real particles in 3D:
    // fire blasts flash orange, scatter sparks and embers and leave smoke; frost bursts into shards and cold mist;
    // lightning flares blue-white and spits sparks where it strikes; healing light and holy pillars lift golden motes;
    // blinks leave violet motes; slams, quakes and shockwaves kick up dust; claws, bites and impacts strike sparks; and
    // the campfires and braziers near you breathe embers and smoke. Sparks, embers and motes are bright enough to bloom.
    public class AldaraVfxPlus : MonoBehaviour
    {
        public static AldaraVfxPlus I;
        struct P { public Vector3 p, v; public float life, max, size, grow, drag, g, bright, flick; public Color c; public int kind; }   // kind 0 spark, 1 glow, 2 smoke, 3 shard
        struct Fl { public Vector3 p; public Color c; public float i, r, life, max; }
        readonly List<P> parts = new List<P>(); readonly List<Fl> flashes = new List<Fl>(); readonly List<Light> lights = new List<Light>();
        readonly List<AldaraVfx.Fx> pendingFx = new List<AldaraVfx.Fx>();
        Mesh addMesh, alphaMesh; Material addMat, alphaMat; Texture2D tex;
        readonly List<Vector3> vA = new List<Vector3>(), vB = new List<Vector3>(); readonly List<Color> cA = new List<Color>(), cB = new List<Color>(); readonly List<Vector4> uA = new List<Vector4>(), uB = new List<Vector4>(); readonly List<int> iA = new List<int>(), iB = new List<int>();
        float fireT;

        public static bool On { get { return AldaraSettings.On("hq") && AldaraSettings.On("vfxHd"); } }
        static AldaraVfxPlus V { get { if (!I) { var go = new GameObject("AldaraVfxPlus"); I = go.AddComponent<AldaraVfxPlus>(); } return I; } }
        void Awake() { I = this; }
        int Cap { get { var p = AldaraSettings.Get("particles"); string s = p != null ? (string)p : "high"; return s == "low" ? 300 : s == "medium" ? 700 : s == "ultra" ? 2400 : 1400; } }

        // ---------- world positions from the browser's px ----------
        static Vector3 W(float x, float y, float h) { var u = AldaraWorld.ToUnity(x, y); if (AldaraWorld.Dun) u.y = 0; return u + Vector3.up * (h / AldaraWorld.PX); }
        static float R() { return Random.value; }
        static Color Hot(Color c, float k) { return new Color(Mathf.Lerp(c.r, 1, k), Mathf.Lerp(c.g, 1, k), Mathf.Lerp(c.b, 1, k), 1); }
        void Add(P p) { if (parts.Count < Cap) parts.Add(p); }
        public static void Flash(Vector3 at, Color c, float intensity, float range, float life) { if (!On) return; V.flashes.Add(new Fl { p = at, c = c, i = intensity, r = range, life = life, max = life }); if (V.flashes.Count > 40) V.flashes.RemoveAt(0); }

        public static void SparkBurst(Vector3 at, Color c, int n, float spd, float up = 0.4f, float g = 9f)
        {
            if (!On) return; var hot = Hot(c, 0.45f);
            for (int k = 0; k < n; k++) { var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * up + up * 0.5f; V.Add(new P { p = at, v = d.normalized * spd * (0.4f + R() * 0.8f), life = 0.3f + R() * 0.35f, max = 0.65f, size = 0.05f + R() * 0.05f, drag = 2.5f, g = g, bright = 2.6f, c = hot, kind = 0 }); }
        }
        public static void Embers(Vector3 at, Color c, int n, float spread, float rise = 1.2f)
        {
            if (!On) return;
            for (int k = 0; k < n; k++) { var o = Random.insideUnitSphere * spread; o.y = Mathf.Abs(o.y) * 0.3f; V.Add(new P { p = at + o, v = new Vector3((R() - 0.5f) * 0.6f, rise * (0.5f + R()), (R() - 0.5f) * 0.6f), life = 0.8f + R() * 1.2f, max = 2f, size = 0.04f + R() * 0.05f, drag = 0.6f, g = -0.4f, bright = 2.2f, flick = 9 + R() * 6, c = Hot(c, 0.3f), kind = 1 }); }
        }
        public static void Motes(Vector3 at, Color c, int n, float spread, float rise = 1f)
        {
            if (!On) return;
            for (int k = 0; k < n; k++) { var o = Random.insideUnitSphere * spread; o.y = Mathf.Abs(o.y) * 0.5f; V.Add(new P { p = at + o, v = new Vector3((R() - 0.5f) * 0.3f, rise * (0.4f + R() * 0.8f), (R() - 0.5f) * 0.3f), life = 0.9f + R() * 0.9f, max = 1.8f, size = 0.07f + R() * 0.07f, drag = 0.4f, g = 0, bright = 1.8f, flick = 3 + R() * 3, c = Hot(c, 0.2f), kind = 1 }); }
        }
        public static void Smoke(Vector3 at, Color c, int n, float spread, float rise = 0.6f, float size = 0.35f)
        {
            if (!On) return;
            for (int k = 0; k < n; k++) { var o = Random.insideUnitSphere * spread; o.y = Mathf.Abs(o.y) * 0.4f; V.Add(new P { p = at + o, v = new Vector3((R() - 0.5f) * 0.5f, rise * (0.6f + R() * 0.6f), (R() - 0.5f) * 0.5f), life = 1.2f + R() * 1.2f, max = 2.4f, size = size * (0.7f + R() * 0.6f), grow = size * 0.9f, drag = 0.8f, g = -0.1f, c = c, kind = 2 }); }
        }
        public static void Dust(Vector3 at, float radius, int n, Color c)
        {
            if (!On) return;
            for (int k = 0; k < n; k++) { float a = R() * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); V.Add(new P { p = at + d * radius * 0.3f + Vector3.up * 0.1f, v = d * (1.5f + R() * 2.5f) + Vector3.up * (0.3f + R() * 0.5f), life = 0.9f + R() * 0.7f, max = 1.6f, size = 0.3f + R() * 0.3f, grow = 0.6f, drag = 2.2f, g = 0, c = c, kind = 2 }); }
        }
        public static void Shards(Vector3 at, Color c, int n, float spd)
        {
            if (!On) return; var hot = Hot(c, 0.3f);
            for (int k = 0; k < n; k++) { var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.8f + 0.2f; V.Add(new P { p = at, v = d * spd * (0.5f + R() * 0.7f), life = 0.5f + R() * 0.5f, max = 1f, size = 0.06f + R() * 0.06f, drag = 1.2f, g = 9, bright = 1.6f, c = hot, kind = 3 }); }
        }

        // ---------- hooks from the browser's effects (AldaraVfx) ----------
        public static void OnEffect(AldaraVfx.Fx f) { if (On) V.pendingFx.Add(f); }   // read next frame, once every field is set
        void DoEffect(AldaraVfx.Fx f)
        {
            float h = AldaraVfx.LZ(f.x, f.y); var at = W(f.x, f.y, 10); float rr = Mathf.Max(0.6f, f.r / AldaraWorld.PX);
            switch (f.kind)
            {
                case "explode": case "meteor3": case "cataBoom":
                    { var c = f.color.a > 0 ? f.color : new Color(1, 0.5f, 0.2f); Flash(at + Vector3.up, Hot(c, 0.2f), 3.4f, rr * 3.2f + 3, 0.5f); SparkBurst(at, c, 26, 6 + rr * 2); Embers(at, c, 16, rr * 0.6f, 1.6f); Smoke(at, new Color(0.18f, 0.16f, 0.15f, 0.55f), 6, rr * 0.5f, 0.8f, 0.5f + rr * 0.15f); Dust(at, rr, 8, new Color(0.35f, 0.3f, 0.25f, 0.5f)); break; }
                case "frost": case "blizzard":
                    { var c = new Color(0.6f, 0.85f, 1f); Flash(at + Vector3.up, c, 2.2f, rr * 2.6f + 2, 0.4f); Shards(at, c, 18, 4 + rr); Smoke(at, new Color(0.8f, 0.92f, 1f, 0.35f), 5, rr * 0.6f, 0.3f, 0.6f); break; }
                case "lightning": case "zap":
                    {
                        var end = f.kind == "zap" && (f.x2 != 0 || f.y2 != 0) ? W(f.x2, f.y2, 14) : at; var c = new Color(0.72f, 0.82f, 1f);
                        Flash(end + Vector3.up * 1.5f, c, 4.2f, 7, 0.16f); SparkBurst(end, c, 16, 7, 0.6f); break;
                    }
                case "heal": case "pulse": { var c = new Color(1f, 0.88f, 0.55f); Flash(at + Vector3.up, c, 1.6f, 4, 0.6f); Motes(at, c, 16, 0.6f, 1.4f); break; }
                case "blink": { var c = new Color(0.75f, 0.5f, 1f); Flash(at + Vector3.up, c, 1.8f, 4, 0.3f); Motes(at, c, 14, 0.5f, 1f); break; }
                case "slash": SparkBurst(at, f.color.a > 0 ? f.color : Color.white, 8, 4); break;
                case "slam": case "ring": if (f.kind == "slam" || f.r > 40) { Dust(at, rr, 7, new Color(0.4f, 0.36f, 0.3f, 0.45f)); Flash(at + Vector3.up, f.color.a > 0 ? f.color : Color.white, 1.0f, rr * 2 + 2, 0.2f); } break;
                case "cataCharge": Motes(at, f.color.a > 0 ? f.color : new Color(1, 0.4f, 0.2f), 20, rr, 2f); break;
            }
        }
        public static void OnMfx(AldaraVfx.Mf f)
        {
            if (!On) return; var at = W(f.x, f.y, f.h + 8); float rr = Mathf.Max(0.5f, f.r / AldaraWorld.PX); var c = f.c;
            switch (f.k)
            {
                case "impact": case "bite": case "claw": case "bash": SparkBurst(at, c, 10, 4.5f); Flash(at + Vector3.up * 0.6f, Hot(c, 0.3f), 1.0f, 3, 0.14f); break;
                case "flash": case "nova": case "roar": Flash(at + Vector3.up, Hot(c, 0.25f), 2.4f, rr * 2.5f + 2.5f, f.T > 0 ? Mathf.Min(0.6f, f.T) : 0.3f); SparkBurst(at, c, 12, 5); break;
                case "pillar": Flash(at + Vector3.up * 2, Hot(c, 0.25f), 2.6f, 5, Mathf.Clamp(f.T, 0.3f, 1f)); Motes(at, c, 16, 0.4f, 3f); break;
                case "shock": Dust(at, rr, 8, new Color(0.42f, 0.38f, 0.32f, 0.4f)); break;
                case "spikes": Shards(at, new Color(0.65f, 0.88f, 1f), 12, 4); break;
                case "scorch": Embers(at, new Color(1, 0.45f, 0.15f), 10, rr * 0.5f, 1.1f); Smoke(at, new Color(0.15f, 0.13f, 0.12f, 0.5f), 3, rr * 0.4f); break;
                case "crack": Dust(at, rr * 0.5f, 4, new Color(0.4f, 0.34f, 0.28f, 0.4f)); break;
                case "blink": Motes(at, new Color(0.75f, 0.5f, 1f), 8, 0.4f); break;
            }
        }
        public static void OnBurst(float x, float y, Color c, int n, float speed) { if (On) SparkBurst(W(x, y, 12), c, Mathf.Clamp(n / 2, 2, 20), 2 + speed / 60f); }
        public static void OnSparks(float x, float y, float h, int n, Color c, float spd) { if (On) SparkBurst(W(x, y, h), c, Mathf.Clamp(n, 1, 24), 1.5f + spd / 70f); }
        public static void OnDebris(float x, float y, int n, string el) { if (!On) return; var at = W(x, y, 4); Dust(at, 0.6f, Mathf.Clamp(n / 2, 1, 10), new Color(0.38f, 0.34f, 0.3f, 0.42f)); if (el == "fire") Embers(at, new Color(1, 0.5f, 0.2f), n, 0.5f); }
        public static void OnMotes(float x, float y, float r, int n, Color c) { if (On) Motes(W(x, y, 6), c, Mathf.Clamp(n, 1, 24), Mathf.Max(0.2f, r / AldaraWorld.PX)); }

        // ---------- simulate and draw ----------
        void LateUpdate()
        {
            var cam = Camera.main; float dt = Mathf.Min(0.05f, Time.deltaTime);
            bool on = On && cam && AldaraWorld.Loaded;
            if (!on) { parts.Clear(); flashes.Clear(); pendingFx.Clear(); foreach (var l in lights) if (l.enabled) l.enabled = false; return; }
            for (int i = 0; i < pendingFx.Count; i++) DoEffect(pendingFx[i]); pendingFx.Clear();
            Ambient(dt);
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i]; p.life -= dt; if (p.life <= 0) { parts.RemoveAt(i); continue; }
                p.v *= Mathf.Exp(-p.drag * dt); p.v.y -= p.g * dt; p.p += p.v * dt; p.size += p.grow * dt;
                if (p.kind != 2 && p.p.y < GroundAt(p.p) && p.v.y < 0) { p.v.y *= -0.3f; p.v.x *= 0.5f; p.v.z *= 0.5f; p.p.y = GroundAt(p.p); }
                parts[i] = p;
            }
            Lights(dt); Draw(cam);
        }
        static float GroundAt(Vector3 u) { if (AldaraWorld.Dun) return 0; var px = AldaraWorld.ToPx(u); return AldaraWorld.GroundY(px.x, px.y); }
        // embers and smoke off the fires near the hero
        void Ambient(float dt)
        {
            fireT -= dt; if (fireT > 0) return; fireT = 0.25f;
            var Pl = AldaraPlayer.I; var post = AldaraPost.I; if (!Pl || !post) return; var L = post.FrameLights; if (L == null) return;
            int n = 0;
            foreach (var l in L)
            {
                if (l.c.r < 0.9f || l.c.g > 0.7f || l.c.b > 0.45f || l.r < 60) continue;   // orange fire, not lamps
                float dx = l.x - Pl.x, dy = l.y - Pl.y; if (dx * dx + dy * dy > 1100 * 1100) continue;
                var at = W(l.x, l.y, 14); if (R() < 0.8f) Embers(at, new Color(1, 0.55f, 0.2f), 1, 0.15f, 1.3f); if (R() < 0.35f) Smoke(at + Vector3.up * 0.4f, new Color(0.2f, 0.19f, 0.18f, 0.28f), 1, 0.1f, 0.7f, 0.25f);
                if (++n > 12) break;
            }
        }
        void Lights(float dt)
        {
            while (lights.Count < 6) { var go = new GameObject("AldaraVfxLight"); go.transform.SetParent(transform, false); var l = go.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel; l.enabled = false; lights.Add(l); }
            for (int i = flashes.Count - 1; i >= 0; i--) { var f = flashes[i]; f.life -= dt; if (f.life <= 0) flashes.RemoveAt(i); else flashes[i] = f; }
            flashes.Sort((a, b) => (b.i * b.life / b.max).CompareTo(a.i * a.life / a.max));
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i]; if (i >= flashes.Count) { if (l.enabled) l.enabled = false; continue; }
                var f = flashes[i]; float k = f.life / f.max; l.enabled = true; l.color = f.c; l.range = f.r; l.intensity = f.i * k * k * (0.85f + 0.15f * Mathf.Sin(Time.time * 40 + i)); l.transform.position = f.p;
            }
        }
        void Draw(Camera cam)
        {
            if (!tex) MakeTex();
            if (!addMat) { var sh = Resources.Load<Shader>("Shaders/AldaraVfxPart"); if (!sh) return; addMat = new Material(sh) { renderQueue = 3100 }; addMat.SetTexture("_MainTex", tex); addMat.SetFloat("_Src", 1); addMat.SetFloat("_Dst", 1); alphaMat = new Material(sh) { renderQueue = 3090 }; alphaMat.SetTexture("_MainTex", tex); alphaMat.SetFloat("_Src", 5); alphaMat.SetFloat("_Dst", 10); }
            if (!addMesh) { addMesh = new Mesh { name = "VfxAdd" }; addMesh.MarkDynamic(); alphaMesh = new Mesh { name = "VfxAlpha" }; alphaMesh.MarkDynamic(); }
            vA.Clear(); cA.Clear(); uA.Clear(); iA.Clear(); vB.Clear(); cB.Clear(); uB.Clear(); iB.Clear();
            var right = cam.transform.right; var up = cam.transform.up; float t = Time.time;
            foreach (var p in parts)
            {
                float k = p.life / p.max; bool smoke = p.kind == 2;
                var vl = smoke ? vB : vA; var cl = smoke ? cB : cA; var ul = smoke ? uB : uA; var il = smoke ? iB : iA;
                Vector3 ax = right * p.size, ay = up * p.size;
                if (p.kind == 0)
                {   // sparks stretch along their motion on screen
                    var sv = Vector3.ProjectOnPlane(p.v, cam.transform.forward); float sp = sv.magnitude;
                    if (sp > 0.01f) { var dir = sv / sp; ay = dir * Mathf.Max(p.size, sp * 0.045f); ax = Vector3.Cross(cam.transform.forward, dir).normalized * p.size * 0.5f; }
                }
                float a = smoke ? p.c.a * Mathf.Sin(Mathf.PI * Mathf.Clamp01(1 - k)) * Mathf.Min(1, k * 3) : Mathf.Min(1, k * 2.2f);
                if (p.flick > 0) a *= 0.7f + 0.3f * Mathf.Sin(t * p.flick + p.p.x * 7);
                int i0 = vl.Count; var col = new Color(p.c.r, p.c.g, p.c.b, a);
                vl.Add(p.p - ax - ay); vl.Add(p.p + ax - ay); vl.Add(p.p + ax + ay); vl.Add(p.p - ax + ay);
                for (int q = 0; q < 4; q++) cl.Add(col);
                float br = smoke ? 1 : p.bright; float soft = smoke ? 1 : 0;
                ul.Add(new Vector4(0, 0, br, soft)); ul.Add(new Vector4(1, 0, br, soft)); ul.Add(new Vector4(1, 1, br, soft)); ul.Add(new Vector4(0, 1, br, soft));
                il.Add(i0); il.Add(i0 + 1); il.Add(i0 + 2); il.Add(i0); il.Add(i0 + 2); il.Add(i0 + 3);
            }
            Fill(addMesh, vA, cA, uA, iA); Fill(alphaMesh, vB, cB, uB, iB);
            if (iA.Count > 0) Graphics.DrawMesh(addMesh, Matrix4x4.identity, addMat, 0, cam);
            if (iB.Count > 0) Graphics.DrawMesh(alphaMesh, Matrix4x4.identity, alphaMat, 0, cam);
        }
        static void Fill(Mesh m, List<Vector3> v, List<Color> c, List<Vector4> u, List<int> ix)
        {
            m.Clear(); if (ix.Count == 0) return; m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(v); m.SetColors(c); m.SetUVs(0, u); m.SetTriangles(ix, 0); m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
        }
        void MakeTex()
        {   // red: a soft round puff (smoke); alpha: a bright core in a glow (sparks, embers)
            const int N = 64; tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "VfxDot" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2 - 1, dy = (y + 0.5f) / N * 2 - 1, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float soft = Mathf.Clamp01(1 - d); soft = soft * soft * (3 - 2 * soft);
                    float core = Mathf.Exp(-d * d * 18) + 0.35f * Mathf.Exp(-d * d * 3.5f); core = Mathf.Clamp01(core) * Mathf.Clamp01((1 - d) * 4);
                    px[y * N + x] = new Color32((byte)(soft * 255), 255, 255, (byte)(core * 255));
                }
            tex.SetPixels32(px); tex.Apply(true);
        }
    }
}
