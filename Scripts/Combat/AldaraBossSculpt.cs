using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Aldara
{
    // The browser's new boss models (BOSSM): skinned sculpts with their own hand-made clips. Every boss keeps its name
    // and fight; the sculpt replaces the old built-from-code body. Idle and Walk loop, each attack move plays its clip
    // timed to the move's wind-up, strike and recovery, Hit flinches on top when the boss is struck, and Death plays out
    // on the corpse. The models are the browser's own (Resources/BossSculpts, imported glTF), sized to the old model.
    public class AldaraBossSculpt : MonoBehaviour
    {
        // size next to the old model (the old ones were measured with crowns and raised weapons)
        static readonly Dictionary<string, float> ADJ = new Dictionary<string, float> {
            { "Goblin Warchief", 1.25f }, { "Grak the Goblin King", 1.25f }, { "Bone Tyrant", 1.3f }, { "Frost Giant King", 1.15f }, { "Void Emperor", 1.1f }, { "Lich Morvain", 1.1f }, { "Ignar the Forgemaster", 1.2f },
            { "The Hollow King", 1.1f }, { "Nerissa the Tide Queen", 1.1f }, { "Arachna, Mother of Webs", 1.2f }, { "The Grand Artificer", 1.35f }, { "Astraeus, the Fallen Star", 1.1f } };
        // which clip each move plays (several: one at random); moves not listed cycle through the attack clips
        static readonly Dictionary<string, Dictionary<string, string[]>> MOVES = new Dictionary<string, Dictionary<string, string[]>>();
        static void Mv(string boss, params string[] kv) { var d = new Dictionary<string, string[]>(); for (int i = 0; i + 1 < kv.Length; i += 2) d[kv[i]] = kv[i + 1].Split('/'); MOVES[boss] = d; }
        static AldaraBossSculpt()
        {
            Mv("Goblin Warchief", "combo", "Frenzy", "chop", "Attack_Chop", "charge", "Attack_Sweep", "roar", "Warcry", "leap", "Attack_Chop");
            Mv("Grak the Goblin King", "combo", "Attack_Sweep", "chop", "Attack_Chop", "charge", "Attack_Sweep", "leap", "Kingsguard_Stomp", "roar", "Royal_Decree");
            Mv("Bone Tyrant", "sweep", "Tail_Sweep", "chop", "Claw_Rake", "spin", "Tail_Sweep", "rain", "Soulfire_Roar", "leap", "Double_Slam");
            Mv("Shadowfen Dragon", "bite", "Bite/Claw_Swipe", "breath", "Fire_Breath", "buffet", "Wing_Storm", "rain", "Wing_Storm", "tail", "Tail_Swipe");
            Mv("Frost Giant King", "slam", "Club_Slam", "spikeline", "Club_Slam", "hurl", "Club_Sweep", "stomp", "Glacial_Stomp", "roar", "Bellow");
            Mv("Infernal Lord", "combo", "Cleaver_Sweep", "wave", "Cleaver_Chop", "rain", "Hellfire_Roar", "leap", "Cleaver_Chop", "nova", "Wing_Buffet");
            Mv("Void Emperor", "barrage", "Scepter_Strike", "nova", "Gravity_Crush", "rain", "Void_Channel", "beam", "Void_Channel", "blinkaway", "Scepter_Strike");
            Mv("Lich Morvain", "barrage", "Bone_Spear", "rain", "Summon_Dead", "nova", "Soul_Drain", "blinkaway", "Bone_Spear");
            Mv("Queen Glacia", "volley", "Ice_Shard", "spikeline", "Ice_Shard", "rain", "Blizzard", "nova", "Frost_Nova");
            Mv("Ignar the Forgemaster", "slam", "Hammer_Slam", "chop", "Hammer_Slam", "hurl", "Molten_Roar", "spin", "Hammer_Sweep", "stomp", "Forge_Quake");
            Mv("The Hollow King", "beam", "Hollow_Roar", "barrage", "Void_Impale", "blinkcut", "Greatsword_Cleave/Greatsword_Sweep", "nova", "Hollow_Roar", "sweep", "Greatsword_Sweep");
            Mv("Nerissa the Tide Queen", "thrust", "Trident_Thrust", "lash", "Tail_Lash", "wave", "Tidal_Surge", "barrage", "Siren_Song");
            Mv("Arachna, Mother of Webs", "bite", "Fang_Strike", "strike", "Leg_Impale", "spit", "Web_Spit", "screech", "Venom_Screech");
            Mv("Countess Sanguine", "claws", "Claw_Slash", "barrage", "Blood_Bolt", "screech", "Blood_Shriek", "swoop", "Wing_Lunge");
            Mv("The Grand Artificer", "smash", "Hammer_Strike", "punch", "Piston_Smash", "eruption", "Seismic_Slam", "breath", "Steam_Vent");
            Mv("Astraeus, the Fallen Star", "sweep", "Comet", "barrage", "Comet", "nova", "Nova", "rain", "Starfall");
        }
        class Data { public GameObject pf; public Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>(); public List<string> atk = new List<string>(); public Dictionary<string, float> hit = new Dictionary<string, float>(); public Material[][] mats; public Mesh[] meshes; }
        static readonly Dictionary<string, Data> cache = new Dictionary<string, Data>(); static Newtonsoft.Json.Linq.JObject meta;
        static string File(string name) { return name.Replace(",", "").Replace(" ", "_"); }
        static Data Get(string name)
        {
            Data d; if (cache.TryGetValue(name, out d)) return d;
            var pf = Resources.Load<GameObject>("BossSculpts/" + File(name)); if (!pf) { cache[name] = null; return null; }
            d = new Data { pf = pf };
            foreach (var c in Resources.LoadAll<AnimationClip>("BossSculpts/" + File(name))) { string k = c.name.Substring(c.name.LastIndexOf('|') + 1); d.clips[k] = c; }
            foreach (var k in new List<string>(d.clips.Keys)) if (k != "Idle" && k != "Walk" && k != "Hit" && k != "Death") d.atk.Add(k);
            d.atk.Sort(System.StringComparer.Ordinal);
            if (!d.clips.ContainsKey("Idle")) { cache[name] = null; return null; }
            // the strike moments the browser stores with each clip
            if (meta == null) { var ta = Resources.Load<TextAsset>("bossm_meta"); meta = ta ? Newtonsoft.Json.Linq.JObject.Parse(ta.text) : new Newtonsoft.Json.Linq.JObject(); }
            var mj = meta[name] as Newtonsoft.Json.Linq.JObject; if (mj != null && mj["clips"] is Newtonsoft.Json.Linq.JObject cj) foreach (var kv in cj) { var hv = kv.Value["hit"]; if (hv != null && hv.Type != Newtonsoft.Json.Linq.JTokenType.Null) d.hit[kv.Key] = (float)hv; }
            return cache[name] = d;
        }
        public static bool Has(string name) { return Get(name) != null; }

        Data D; AldaraMonsters.Mon mon; Animator an; PlayableGraph g; AnimationMixerPlayable mix; readonly Dictionary<string, int> slot = new Dictionary<string, int>(); readonly List<AnimationClipPlayable> ps = new List<AnimationClipPlayable>();
        string cur, prev, actClip; float curT, prevT, fade = 1, hitT = 9, lastHurt; AldaraMonsters.Act act; public float Height;

        /// the sculpt for this boss under its view, sized to the old model's height (oldH, in world units)
        public static AldaraBossSculpt Make(AldaraMonsters.Mon m, Transform parent, float oldH)
        {
            var d = Get(m.name); if (d == null) return null;
            var go = Instantiate(d.pf, parent, false); go.name = "sculpt"; var s = go.AddComponent<AldaraBossSculpt>(); s.D = d; s.mon = m;
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true)) mr.gameObject.SetActive(false);   // the exporter's stray cube
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr)
            {
                // the game's own lit, unlit-ambient look: the glTF colours as tints of the vertex-lit material
                if (d.mats == null)
                {
                    var src = smr.sharedMaterials; var mats = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++)
                    {
                        var sm = src[i]; var nm = new Material(Shader.Find("Aldara/VertexLit")) { name = sm ? sm.name : "m" }; Color c = Color.white; Color e = Color.black;
                        if (sm) { if (sm.HasProperty("baseColorFactor")) c = sm.GetColor("baseColorFactor"); else if (sm.HasProperty("_BaseColor")) c = sm.GetColor("_BaseColor"); else c = sm.color; if (sm.HasProperty("emissiveFactor")) e = sm.GetColor("emissiveFactor"); else if (sm.HasProperty("_EmissionColor")) e = sm.GetColor("_EmissionColor"); }
                        nm.SetColor("_Tint", c); nm.SetFloat("_Emit", e.maxColorComponent > 0.05f ? 0.9f : 0); mats[i] = nm;
                    }
                    d.mats = new[] { mats };
                    var mesh = Instantiate(smr.sharedMesh); var cols = new Color32[mesh.vertexCount]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255); mesh.colors32 = cols; d.meshes = new[] { mesh };
                }
                smr.sharedMaterials = d.mats[0]; smr.sharedMesh = d.meshes[0]; smr.updateWhenOffscreen = true;
            }
            s.an = go.GetComponent<Animator>() ?? go.GetComponentInChildren<Animator>(); if (s.an) { s.an.applyRootMotion = false; s.an.runtimeAnimatorController = null; }
            s.Build(); s.Pose("Idle", 0, null, 0, 0, 0); s.g.Evaluate(0);
            // match the old model's height
            float h = 1; if (smr) { var bk = new Mesh(); smr.BakeMesh(bk, true); float t0 = float.MinValue, b0 = float.MaxValue; foreach (var v in bk.vertices) { float y = (smr.transform.position + smr.transform.rotation * v).y; t0 = Mathf.Max(t0, y); b0 = Mathf.Min(b0, y); } Destroy(bk); h = t0 - Mathf.Max(b0, parent.position.y); }
            float adj; ADJ.TryGetValue(m.name, out adj); if (adj <= 0) adj = 1;
            float k = oldH > 0 && h > 0 ? oldH / h * adj : adj; go.transform.localScale = go.transform.localScale * k; s.Height = h * k;
            return s;
        }
        void Build()
        {
            g = PlayableGraph.Create("boss " + mon.name); g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(g, "out", an); mix = AnimationMixerPlayable.Create(g, D.clips.Count); output.SetSourcePlayable(mix);
            int i = 0; foreach (var kv in D.clips) { var p = AnimationClipPlayable.Create(g, kv.Value); p.SetApplyFootIK(false); p.Pause(); g.Connect(p, 0, mix, i); mix.SetInputWeight(i, 0); slot[kv.Key] = i; ps.Add(p); i++; }
        }
        void OnDestroy() { if (g.IsValid()) g.Destroy(); }
        float Len(string c) { return D.clips[c].length; }
        float Hit(string c)
        {   // the moment of the strike: the browser stores it with each clip; here it is where the pose moves fastest
            float h; if (D.hit.TryGetValue(c, out h)) return h;
            var clip = D.clips[c]; var bones = GetComponentsInChildren<Transform>(); int N = 30; var prevP = new Vector3[bones.Length]; float best = -1; h = clip.length * 0.5f;
            for (int s = 0; s <= N; s++)
            {
                float t = clip.length * s / N; clip.SampleAnimation(gameObject, t); float sp = 0;
                for (int b = 0; b < bones.Length; b++) { var p = transform.InverseTransformPoint(bones[b].position); if (s > 0) sp += (p - prevP[b]).sqrMagnitude; prevP[b] = p; }
                if (s > 0 && t > clip.length * 0.15f && t < clip.length * 0.85f && sp > best) { best = sp; h = t; }
            }
            return D.hit[c] = h;
        }
        static float[] Phase(AldaraMonsters.Act A)
        {
            var mv = A.mv; float W = mv.wind, S = mv.act, t = A.t;
            if (t < W) return new[] { 0, W > 0 ? t / W : 1 }; if (t < W + S) return new[] { 1, S > 0 ? (t - W) / S : 1 }; return new[] { 2, mv.rec > 0 ? Mathf.Min(1, (t - W - S) / mv.rec) : 1 };
        }
        float ClipTime(string c, AldaraMonsters.Act A)
        {
            var mv = A.mv; var ph = Phase(A); float d = Len(c), hit = Mathf.Max(0.1f, Mathf.Min(d * 0.85f, Hit(c)));
            bool chan = mv.chan > 0; float span = chan ? Mathf.Max(0.12f * d, (d - hit) * 0.45f) : Mathf.Min(0.12f * d, Mathf.Max(0.02f, d - hit));
            if (ph[0] == 0) return ph[1] * hit;
            if (ph[0] == 1) { if (chan) { float p = (A.t - mv.wind) * 0.9f, q = p % (2 * span); return hit + (q < span ? p % span : span - p % span); } return hit + ph[1] * span; }
            return Mathf.Min(d, hit + span + ph[1] * (d - hit - span) * 0.9f);
        }
        string ClipFor(MoveDef mv)
        {
            Dictionary<string, string[]> tab; string c = null; if (MOVES.TryGetValue(mon.name, out tab)) { string[] o; if (mv.id != null && tab.TryGetValue(mv.id, out o)) c = o[Random.Range(0, o.Length)]; }
            if (c == null || !D.clips.ContainsKey(c)) c = D.atk.Count > 0 ? D.atk[Mathf.Abs(mv.i) % D.atk.Count] : "Idle";
            return c;
        }
        void Pose(string c, float t, string p, float pt, float fw, float hw)
        {
            for (int i = 0; i < ps.Count; i++) mix.SetInputWeight(i, 0);
            float wc = (p != null && p != c ? fw : 1) * (1 - hw); Set(c, t, wc, false);
            if (p != null && p != c) Set(p, pt, (1 - fw) * (1 - hw), true);
            if (hw > 0 && D.clips.ContainsKey("Hit")) Set("Hit", hitT, hw, true);
        }
        void Set(string c, float t, float w, bool add) { int i = slot[c]; ps[i].SetTime(t); mix.SetInputWeight(i, (add ? mix.GetInputWeight(i) : 0) + w); }

        /// bossmAnimate: one frame of the live boss
        public void Tick(bool moving, float dt)
        {
            if (!g.IsValid()) return; var A = mon.act; string C; float t;
            if (A != null) { if (act != A) { act = A; actClip = ClipFor(A.mv); } C = actClip; t = ClipTime(C, A); }
            else
            {
                act = null;
                if (moving) { C = D.clips.ContainsKey("Walk") ? "Walk" : "Idle"; float d = Len(C); t = ((mon.walkT / (Mathf.PI * 2) * d * 0.9f) % d + d) % d; }
                else { C = "Idle"; float d = Len(C); t = (Time.time + mon.homeX * 0.013f) % d; }
            }
            if (cur != C) { if (cur != null) { prev = cur; prevT = curT; fade = 0; } cur = C; }
            curT = t;
            if (fade < 1 && prev != null)
            {
                fade = Mathf.Min(1, fade + dt / (A != null ? 0.12f : 0.25f)); float pd = Len(prev);
                prevT = prev == "Idle" || prev == "Walk" ? (prevT + dt) % pd : Mathf.Min(pd, prevT + dt);
            }
            // flinch: a new hit restarts the Hit clip unless it is already playing; softly while walking, not at all mid-attack
            if (mon.hurtT > 0 && mon.hurtT > lastHurt + 0.01f && hitT > 0.4f) hitT = 0; lastHurt = mon.hurtT;
            float hw = 0; if (D.clips.ContainsKey("Hit") && hitT < Len("Hit")) { hitT += dt; float u = hitT / Len("Hit"); hw = (A != null ? 0 : moving ? 0.45f : 0.85f) * Mathf.Min(1, u * 6) * Mathf.Min(1, (1 - u) * 3); }
            Pose(cur, curT, fade < 1 ? prev : null, prevT, fade, Mathf.Max(0, hw));
            g.Evaluate(0);
        }
        public bool HasDeath { get { return D.clips.ContainsKey("Death"); } }
        public float DeathLen { get { return HasDeath ? Len("Death") : 0; } }
        public float DeathHit { get { return HasDeath ? Hit("Death") : 0; } }
        /// the corpse: the Death clip, eased in from the idle pose over the first moment
        public void DeathPose(float t)
        {
            if (!g.IsValid() || !HasDeath) return; float d = Len("Death"); for (int i = 0; i < ps.Count; i++) mix.SetInputWeight(i, 0);
            float e = t < 0.15f ? 1 - t / 0.15f : 0; Set("Death", Mathf.Min(d - 0.001f, t), 1 - e, false); if (e > 0) Set("Idle", 0, e, true);
            g.Evaluate(0);
        }
    }
}
