using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Aldara
{
    // Plays the hero animations baked from the browser game's procedural animation (poseP3), sampled at 30 fps.
    // Node i of the hierarchy is the child named "n<i>". Base loops: idle, combatIdle, run, runCombat, runBack;
    // one-shot clips (attacks, casts, skills, hurt, death, fidgets) play over the base and cross-fade back.
    // .bytes layout: "ACHR", int N, N int parents, int C, then per clip: string name, byte loop, float dur, int F, F*N*8 floats.
    public class AldaraCharacterAnimator : MonoBehaviour
    {
        public TextAsset data;
        public string cls = "knight";
        public bool combat;
        public float strideK = 0.078f;
        /// in the saddle (AldaraMount): the thighs forward and apart, the knees bent; seatNodes = thigh L, knee L, thigh R, knee R
        [System.NonSerialized] public bool seated; [System.NonSerialized] public int[] seatNodes; float seatW;  // browser walkT per px (knight .078, mage .082, archer .074)

        class Clip { public string name; public bool loop; public float dur; public int F; public float[] d; public int A; public float[] ax; }
        int N; Transform[] T; readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        Clip baseClip, prevBase, oneShot; float baseT, prevBaseT, baseFade = 1, oneT, oneFadeIn, oneFadeOut;
        bool moving, back; float pxSpeed; float idleFor;
        float[] bufA, bufB;
        /// the browser's pose values the cloth follows (hips and knees, lean, flow, speed), blended like the pose
        public readonly float[] aux = new float[13]; readonly float[] auxB = new float[13];
        public bool Moving { get { return moving; } }
        public bool Busy { get { return oneShot != null; } }

        void Awake() { Load(); }
        public void Load()
        {
            if (data == null) return;
            oneShot = null; oneT = 0; oneFadeIn = 0; oneFadeOut = 0; prevBase = null; baseClip = null; baseFade = 1;   // rising again: drop the death pose
            clips.Clear();
            using (var r = new BinaryReader(new MemoryStream(data.bytes)))
            {
                bool v2 = new string(r.ReadChars(4)) == "ACH2"; N = r.ReadInt32(); for (int i = 0; i < N; i++) r.ReadInt32();
                int C = r.ReadInt32();
                for (int c = 0; c < C; c++)
                {
                    var k = new Clip { name = r.ReadString(), loop = r.ReadByte() != 0, dur = r.ReadSingle() }; k.F = r.ReadInt32();
                    k.d = new float[k.F * N * 8]; for (int i = 0; i < k.d.Length; i++) k.d[i] = r.ReadSingle();
                    if (v2) { k.A = r.ReadInt32(); k.ax = new float[k.F * k.A]; for (int i = 0; i < k.ax.Length; i++) k.ax[i] = r.ReadSingle(); }
                    clips[k.name] = k;
                }
            }
            T = new Transform[N];
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.Length > 1 && t.name[0] == 'n' && int.TryParse(t.name.Substring(1), out int i) && i < N) T[i] = t;
            bufA = new float[N * 8]; bufB = new float[N * 8];
            baseClip = Get("idle");
        }
        /// Pose the hierarchy in the browser's rest pose (used when building the prefab, and in the editor).
        public void ApplyRest() { if (T == null) Load(); var c = Get("idle") ?? Get("rest"); if (c == null) return; Sample(c, 0, bufA); Apply(bufA); }
        Clip Get(string n) { Clip c; return clips.TryGetValue(n, out c) ? c : null; }
        public bool Has(string n) { return clips.ContainsKey("clip_" + n); }
        public float ClipLength(string n) { var c = Get("clip_" + n); return c != null ? c.dur : 0; }

        public void SetMoving(bool m, bool b, float speedPx) { moving = m; back = b; pxSpeed = speedPx; }
        /// Play a one-shot clip by its browser name (e.g. "a1", "a2", "cast", "hurt", "death", "whirlwind").
        public bool Play(string name, float speedMul = 1)
        {
            var c = Get("clip_" + name); if (c == null) return false;
            oneShot = c; oneT = 0; oneFadeIn = 0; oneFadeOut = 0; oneSpeed = speedMul; return true;
        }
        float oneSpeed = 1;

        void SetBase(Clip c)
        {
            if (c == null || c == baseClip) return;
            prevBase = baseClip; prevBaseT = baseT; baseClip = c; baseFade = 0;
            if (prevBase != null && (prevBase.name.StartsWith("run") == c.name.StartsWith("run"))) baseT = prevBaseT * c.dur / Mathf.Max(0.01f, prevBase.dur); else baseT = 0;
        }

        void LateUpdate()
        {
            if (T == null || baseClip == null) return;
            float dt = Time.deltaTime;
            Clip want = moving ? (back ? Get("runBack") : combat ? Get("runCombat") : Get("run")) : (combat ? Get("combatIdle") : Get("idle"));
            SetBase(want ?? baseClip);
            // run cycle speed follows the ground covered (the browser advances walkT by distance)
            float rate = 1;
            if (baseClip.name.StartsWith("run")) rate = Mathf.Max(0.2f, pxSpeed * strideK / (Mathf.PI * 2) * baseClip.dur);
            baseT += dt * rate; if (prevBase != null) prevBaseT += dt * rate;
            baseFade = Mathf.Min(1, baseFade + dt * 7);

            // idle fidgets after a while standing still
            idleFor = (!moving && !combat && oneShot == null) ? idleFor + dt : 0;
            if (idleFor > 9f) { idleFor = 0; Play(Random.value < 0.5f ? "fidget1" : "fidget2"); }

            Sample(baseClip, baseT, bufA); SampleAux(baseClip, baseT, aux);
            if (prevBase != null && baseFade < 1) { Sample(prevBase, prevBaseT, bufB); Mix(bufB, bufA, baseFade, bufA); SampleAux(prevBase, prevBaseT, auxB); MixAux(auxB, aux, baseFade, aux); }
            if (oneShot != null)
            {
                oneT += dt * oneSpeed;
                if (moving && oneShot.name.StartsWith("clip_fidget")) oneShot = null;
                else
                {
                    float u = oneT / oneShot.dur;
                    oneFadeIn = Mathf.Min(1, oneFadeIn + dt * 12);
                    float w = oneFadeIn;
                    if (!oneShot.loop && u >= 1)
                    {
                        if (oneShot.name == "clip_death") { u = 1; }
                        else { oneFadeOut += dt * 8; w *= Mathf.Max(0, 1 - oneFadeOut); if (w <= 0) oneShot = null; u = 1; }
                    }
                    if (oneShot != null) { Sample(oneShot, Mathf.Min(u, 1) * oneShot.dur, bufB); Mix(bufA, bufB, w, bufA); SampleAux(oneShot, Mathf.Min(u, 1) * oneShot.dur, auxB); MixAux(aux, auxB, w, aux); }
                }
            }
            Apply(bufA);
            seatW = Mathf.MoveTowards(seatW, seated ? 1 : 0, dt * 6);
            if (false && seatW > 0 && seatNodes != null && seatNodes.Length == 4)   // the saddle pose is set by AldaraHeroGear
            {
                for (int s = 0; s < 2; s++)
                {
                    int th = seatNodes[s * 2], kn = seatNodes[s * 2 + 1]; if (th >= N || kn >= N || !T[th] || !T[kn]) continue;
                    float side = T[th].localPosition.x < 0 ? -1 : 1;
                    var sit = Quaternion.AngleAxis(side * 16, Vector3.forward) * Quaternion.AngleAxis(-80, Vector3.right);
                    T[th].localRotation = Quaternion.Slerp(T[th].localRotation, T[th].localRotation * sit, seatW);
                    T[kn].localRotation = Quaternion.Slerp(T[kn].localRotation, T[kn].localRotation * Quaternion.AngleAxis(78, Vector3.right), seatW);
                }
            }
        }

        void Sample(Clip c, float t, float[] o)
        {
            float f; if (c.loop) { f = Mathf.Repeat(t / c.dur, 1f) * (c.F - 1); } else f = Mathf.Clamp01(t / c.dur) * (c.F - 1);
            int a = Mathf.Min(c.F - 1, (int)f), b = Mathf.Min(c.F - 1, a + 1); float u = f - a;
            int sa = a * N * 8, sb = b * N * 8;
            for (int i = 0; i < N * 8; i += 8)
            {
                for (int k = 0; k < 3; k++) o[i + k] = Mathf.Lerp(c.d[sa + i + k], c.d[sb + i + k], u);
                var q = Quaternion.Slerp(Q(c.d, sa + i + 3), Q(c.d, sb + i + 3), u);
                o[i + 3] = q.x; o[i + 4] = q.y; o[i + 5] = q.z; o[i + 6] = q.w;
                o[i + 7] = Mathf.Lerp(c.d[sa + i + 7], c.d[sb + i + 7], u);
            }
        }
        void SampleAux(Clip c, float t, float[] o)
        {
            if (c.ax == null || c.A == 0) { for (int i = 0; i < o.Length; i++) o[i] = 0; return; }
            float f; if (c.loop) { f = Mathf.Repeat(t / c.dur, 1f) * (c.F - 1); } else f = Mathf.Clamp01(t / c.dur) * (c.F - 1);
            int a = Mathf.Min(c.F - 1, (int)f), b = Mathf.Min(c.F - 1, a + 1); float u = f - a;
            for (int i = 0; i < o.Length && i < c.A; i++) o[i] = Mathf.Lerp(c.ax[a * c.A + i], c.ax[b * c.A + i], u);
        }
        static void MixAux(float[] a, float[] b, float w, float[] o) { for (int i = 0; i < o.Length; i++) o[i] = Mathf.Lerp(a[i], b[i], w); }
        static Quaternion Q(float[] d, int o) { return new Quaternion(d[o], d[o + 1], d[o + 2], d[o + 3]); }
        void Mix(float[] a, float[] b, float w, float[] o)
        {
            for (int i = 0; i < N * 8; i += 8)
            {
                for (int k = 0; k < 3; k++) o[i + k] = Mathf.Lerp(a[i + k], b[i + k], w);
                var q = Quaternion.Slerp(Q(a, i + 3), Q(b, i + 3), w);
                o[i + 3] = q.x; o[i + 4] = q.y; o[i + 5] = q.z; o[i + 6] = q.w;
                o[i + 7] = Mathf.Lerp(a[i + 7], b[i + 7], w);
            }
        }
        void Apply(float[] o)
        {
            for (int i = 0; i < N; i++)
            {
                var t = T[i]; if (!t) continue; int k = i * 8;
                t.localPosition = new Vector3(o[k], o[k + 1], o[k + 2]);
                t.localRotation = Q(o, k + 3);
                t.localScale = Vector3.one * o[k + 7];
            }
        }
    }
}
