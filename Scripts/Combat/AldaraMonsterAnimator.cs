using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Aldara
{
    // Plays a monster's animations baked from the browser (same ACHR format as the heroes): base loop idle / walk / fly,
    // one-shots hurt and move<i> (an attack, sampled over its wind / strike / recover time) driven by the simulation.
    public class AldaraMonsterAnimator : MonoBehaviour
    {
        public TextAsset data;
        class Clip { public string name; public bool loop; public float dur; public int F; public float[] d; }
        static readonly Dictionary<TextAsset, Dictionary<string, Clip>> cache = new Dictionary<TextAsset, Dictionary<string, Clip>>();
        static readonly Dictionary<TextAsset, int> nodeCount = new Dictionary<TextAsset, int>();
        Dictionary<string, Clip> clips; int N; Transform[] T; float[] a, b;
        Clip baseClip; float baseT; string baseName = "idle";
        Clip one; float oneT, oneW;   // one-shot: either externally timed (attack) or self-timed (hurt)
        bool external; float extT;

        void Awake() { Load(); }
        void Load()
        {
            if (!data) return;
            if (!cache.TryGetValue(data, out clips))
            {
                clips = new Dictionary<string, Clip>();
                using (var r = new BinaryReader(new MemoryStream(data.bytes)))
                {
                    r.ReadChars(4); int n = r.ReadInt32(); for (int i = 0; i < n; i++) r.ReadInt32(); int C = r.ReadInt32();
                    for (int c = 0; c < C; c++)
                    {
                        var k = new Clip { name = r.ReadString(), loop = r.ReadByte() != 0, dur = r.ReadSingle() }; k.F = r.ReadInt32();
                        k.d = new float[k.F * n * 8]; for (int i = 0; i < k.d.Length; i++) k.d[i] = r.ReadSingle(); clips[k.name] = k;
                    }
                    nodeCount[data] = n;
                }
                cache[data] = clips;
            }
            N = nodeCount[data];
            T = new Transform[N];
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.Length > 1 && t.name[0] == 'n' && int.TryParse(t.name.Substring(1), out int i) && i < N) T[i] = t;
            a = new float[N * 8]; b = new float[N * 8];
            clips.TryGetValue("idle", out baseClip);
            Pose(baseClip, 0);
        }
        public bool Has(string n) { return clips != null && clips.ContainsKey(n); }
        /// base loop: "idle", "walk" (rate = cycles per second) or "fly"
        public void SetBase(string n, float rate)
        {
            if (clips == null) return;
            if (n != baseName) { Clip c; if (clips.TryGetValue(n, out c)) { baseClip = c; baseName = n; } }
            baseRate = rate;
        }
        float baseRate = 1;
        /// an attack, positioned by the simulation's own clock (seconds since the move started)
        public void SetAction(int moveIndex, float t)
        {
            Clip c; if (clips == null || !clips.TryGetValue("move" + moveIndex, out c)) { external = false; return; }
            if (one != c || !external) { one = c; oneW = 0; }
            external = true; extT = t;
        }
        public void ClearAction() { if (external) { external = false; one = null; } }
        public void Hurt() { Clip c; if (!external && clips != null && clips.TryGetValue("hurt", out c)) { one = c; oneT = 0; oneW = 1; } }

        void LateUpdate()
        {
            if (T == null || baseClip == null) return;
            float dt = Time.deltaTime;
            baseT += dt * (baseRate > 0 ? baseRate : 1f / Mathf.Max(0.01f, baseClip.dur));
            Sample(baseClip, baseT * baseClip.dur, a, baseName == "walk");
            if (one != null)
            {
                float t; if (external) { t = extT; oneW = Mathf.Min(1, oneW + dt * 14); } else { oneT += dt; t = oneT; if (t >= one.dur) { one = null; } }
                if (one != null) { Sample(one, t, b, false); Mix(a, b, external ? oneW : 1, a); }
            }
            Apply(a);
        }
        void Pose(Clip c, float t) { if (c == null) return; Sample(c, t, a, false); Apply(a); }
        // walk clips are stored over one cycle normalised to 1 s; others in seconds
        void Sample(Clip c, float t, float[] o, bool cycle)
        {
            float f = c.loop ? Mathf.Repeat(t / c.dur, 1f) * (c.F - 1) : Mathf.Clamp01(t / c.dur) * (c.F - 1);
            int ia = Mathf.Min(c.F - 1, (int)f), ib = Mathf.Min(c.F - 1, ia + 1); float u = f - ia; int sa = ia * N * 8, sb = ib * N * 8;
            for (int i = 0; i < N * 8; i += 8)
            {
                for (int k = 0; k < 3; k++) o[i + k] = Mathf.Lerp(c.d[sa + i + k], c.d[sb + i + k], u);
                var q = Quaternion.Slerp(Q(c.d, sa + i + 3), Q(c.d, sb + i + 3), u); o[i + 3] = q.x; o[i + 4] = q.y; o[i + 5] = q.z; o[i + 6] = q.w;
                o[i + 7] = Mathf.Lerp(c.d[sa + i + 7], c.d[sb + i + 7], u);
            }
        }
        static Quaternion Q(float[] d, int o) { return new Quaternion(d[o], d[o + 1], d[o + 2], d[o + 3]); }
        void Mix(float[] x, float[] y, float w, float[] o)
        {
            for (int i = 0; i < N * 8; i += 8)
            {
                for (int k = 0; k < 3; k++) o[i + k] = Mathf.Lerp(x[i + k], y[i + k], w);
                var q = Quaternion.Slerp(Q(x, i + 3), Q(y, i + 3), w); o[i + 3] = q.x; o[i + 4] = q.y; o[i + 5] = q.z; o[i + 6] = q.w;
                o[i + 7] = Mathf.Lerp(x[i + 7], y[i + 7], w);
            }
        }
        void Apply(float[] o)
        {
            for (int i = 0; i < N; i++) { var t = T[i]; if (!t) continue; int k = i * 8; t.localPosition = new Vector3(o[k], o[k + 1], o[k + 2]); t.localRotation = Q(o, k + 3); t.localScale = Vector3.one * o[k + 7]; }
        }
    }
}
