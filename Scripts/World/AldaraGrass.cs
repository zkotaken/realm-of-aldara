using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aldara
{
    // Wind-blown grass (enhanced rendering): small tufts of blades over the painted ground around the hero, drawn with
    // GPU instancing. Each tuft takes its colour from the ground paint under it, and only grows where that paint is green
    // (so roads, sand, snow, ash and rock stay bare). Gusts roll across the fields and the blades part around the hero.
    // Nothing grows in water, lava, on roads, in towns or dungeons.
    public class AldaraGrass : MonoBehaviour
    {
        const float CELL = 4f, SP = 0.40f; const int PER = 1023;
        class Cell { public Matrix4x4[] m; public float y; public int tile; public float used; }
        readonly Dictionary<long, Cell> cells = new Dictionary<long, Cell>();
        readonly Dictionary<int, List<Matrix4x4>> byTile = new Dictionary<int, List<Matrix4x4>>();
        readonly Dictionary<int, MaterialPropertyBlock> blocks = new Dictionary<int, MaterialPropertyBlock>();
        readonly Dictionary<int, Texture> tileTex = new Dictionary<int, Texture>();
        readonly Matrix4x4[] buf = new Matrix4x4[PER];
        Mesh tuft; Material mat; static readonly int HeroId = Shader.PropertyToID("_Hero");
        int builtThisFrame;

        void Start()
        {
            var sh = Shader.Find("Aldara/Grass"); if (sh) mat = new Material(sh) { enableInstancing = true, name = "Grass" };
            tuft = MakeTuft();
        }
        static Mesh MakeTuft()
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>(); var rnd = new System.Random(7);
            System.Func<float> R = () => (float)rnd.NextDouble();
            for (int b = 0; b < 9; b++)
            {
                float a = R() * Mathf.PI * 2, r = R() * 0.13f, h = 0.20f + R() * 0.17f, w = 0.028f + R() * 0.014f;
                var root = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r * 0.6f);
                float face = R() * Mathf.PI; var side = new Vector3(Mathf.Cos(face), 0, Mathf.Sin(face)) * w;
                var lean = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (0.03f + R() * 0.06f);
                int i0 = v.Count;
                v.Add(root - side); v.Add(root + side);
                v.Add(root + lean * 0.5f + Vector3.up * h * 0.55f - side * 0.6f); v.Add(root + lean * 0.5f + Vector3.up * h * 0.55f + side * 0.6f);
                v.Add(root + lean + Vector3.up * h);
                float shade = 0.85f + R() * 0.3f;
                c.Add(new Color(shade, 0, 0, 0)); c.Add(new Color(shade, 0, 0, 0)); c.Add(new Color(shade, 0, 0, 0.55f)); c.Add(new Color(shade, 0, 0, 0.55f)); c.Add(new Color(shade, 0, 0, 1));
                t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0 + 1, i0 + 2, i0 + 3, i0 + 2, i0 + 4, i0 + 3 });
            }
            var m = new Mesh { name = "GrassTuft" }; m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
            var n = new Vector3[v.Count]; for (int i = 0; i < n.Length; i++) n[i] = Vector3.up; m.normals = n;
            m.bounds = new Bounds(new Vector3(0, 0.2f, 0), new Vector3(1.4f, 1.0f, 1.4f)); return m;
        }
        Texture TileTex(int ti, int tj)
        {
            int k = tj * 100 + ti; if (tileTex.TryGetValue(k, out var t)) return t;
            var go = GameObject.Find("World/Ground/Ground_" + ti + "_" + tj); var r = go ? go.GetComponent<MeshRenderer>() : null;
            t = r && r.sharedMaterial ? r.sharedMaterial.GetTexture("_BaseMap") : null; tileTex[k] = t; return t;
        }
        Cell Build(int cx, int cz)
        {
            var rnd = new System.Random(cx * 73856093 ^ cz * 19349663); var list = new List<Matrix4x4>(); float ysum = 0; int yn = 0;
            int n = Mathf.RoundToInt(CELL / SP);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float ux = cx * CELL + (i + 0.15f + (float)rnd.NextDouble() * 0.7f) * SP, uz = cz * CELL + (j + 0.15f + (float)rnd.NextDouble() * 0.7f) * SP;
                    float px = ux * AldaraWorld.PX, py = AldaraWorld.YTOP - uz * AldaraWorld.PX;
                    float yaw = (float)rnd.NextDouble() * 360, s = 0.75f + (float)rnd.NextDouble() * 0.6f;
                    if (AldaraWorld.LiquidAt(px, py) != 0 || AldaraWorld.LiquidAt(px + 22, py) != 0 || AldaraWorld.LiquidAt(px - 22, py) != 0 || AldaraWorld.LiquidAt(px, py + 22) != 0 || AldaraWorld.LiquidAt(px, py - 22) != 0) continue;
                    if (AldaraWorld.RoadDist(px, py) < 18 || AldaraWorld.BlockedAt(px, py) || AldaraWorld.InCity(px, py)) continue;
                    float gy = AldaraWorld.GroundY(px, py); ysum += gy; yn++;
                    list.Add(Matrix4x4.TRS(new Vector3(ux, gy - 0.02f, uz), Quaternion.Euler(0, yaw, 0), new Vector3(s, s * (0.85f + (float)rnd.NextDouble() * 0.3f), s)));
                }
            return new Cell { m = list.ToArray(), y = yn > 0 ? ysum / yn : AldaraWorld.GroundY(cx * CELL * AldaraWorld.PX, AldaraWorld.YTOP - cz * CELL * AldaraWorld.PX), tile = Mathf.FloorToInt(cx * CELL / 128f) + Mathf.FloorToInt(cz * CELL / 128f) * 100 };
        }
        void Update()
        {
            if (!Application.isPlaying || !mat || !tuft || !AldaraSave.Ready || !AldaraWorld.Loaded || AldaraWorld.Dun) return;
            if (!AldaraSettings.On("hq") || !AldaraSettings.On("grass")) return;
            var cam = Camera.main; var P = AldaraPlayer.I; if (!cam || !P) return;
            var hero = AldaraWorld.ToUnity(P.x, P.y); Shader.SetGlobalVector(HeroId, new Vector4(hero.x, hero.y, hero.z, 1));
            Vector3 f = AldaraCamera.I && AldaraCamera.I.target ? AldaraCamera.I.target.position : hero;
            float hh = cam.orthographicSize + 1.5f, hw = cam.orthographicSize * cam.aspect + 1.5f, sy = f.z + f.y;
            int x0 = Mathf.FloorToInt((f.x - hw) / CELL), x1 = Mathf.FloorToInt((f.x + hw) / CELL);
            int z0 = Mathf.FloorToInt((sy - hh - 34) / CELL), z1 = Mathf.FloorToInt((sy + hh + 6) / CELL);
            foreach (var l in byTile.Values) l.Clear(); builtThisFrame = 0; float now = Time.time;
            for (int cz = z0; cz <= z1; cz++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    if (cx < 0 || cz < 0) continue; long key = ((long)cx << 32) | (uint)cz;
                    if (!cells.TryGetValue(key, out var c)) { if (builtThisFrame > 24) continue; c = Build(cx, cz); cells[key] = c; builtThisFrame++; }
                    // on screen: screen height follows north plus ground height
                    float sc = (cz + 0.5f) * CELL + c.y; if (Mathf.Abs(sc - sy) > hh + CELL) continue;
                    c.used = now; if (c.m.Length == 0) continue;
                    if (!byTile.TryGetValue(c.tile, out var lst)) { lst = new List<Matrix4x4>(); byTile[c.tile] = lst; }
                    lst.AddRange(c.m);
                }
            foreach (var kv in byTile)
            {
                var lst = kv.Value; if (lst.Count == 0) continue; int ti = kv.Key % 100, tj = kv.Key / 100; var tex = TileTex(ti, tj); if (!tex) continue;
                if (!blocks.TryGetValue(kv.Key, out var mpb)) { mpb = new MaterialPropertyBlock(); mpb.SetTexture("_BaseMap", tex); mpb.SetVector("_TileO", new Vector4(ti * 128f, tj * 128f, 0, 0)); blocks[kv.Key] = mpb; }
                var rp = new RenderParams(mat) { matProps = mpb, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, worldBounds = new Bounds(f, new Vector3(hw * 2 + 8, 80, hh * 2 + 40)) };
                for (int s = 0; s < lst.Count; s += PER) { int n = Mathf.Min(PER, lst.Count - s); lst.CopyTo(s, buf, 0, n); Graphics.RenderMeshInstanced(rp, tuft, 0, buf, n); }
            }
            // forget cells long off screen
            if (cells.Count > 900) { var drop = new List<long>(); foreach (var kv in cells) if (now - kv.Value.used > 20) drop.Add(kv.Key); foreach (var k in drop) cells.Remove(k); }
        }
    }
}
