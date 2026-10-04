using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aldara
{
    // Draws the browser's ~20k small props (grass tufts, bushes, flowers, rocks, mushrooms...) with GPU instancing,
    // only in the cells around the camera.
    [ExecuteAlways]
    public class AldaraPropField : MonoBehaviour
    {
        [System.Serializable] public class Batch { public int mesh; public int cell; public bool sway; public Matrix4x4[] m; }
        public Mesh[] meshes;
        public Material still, sway;
        public float cellSize = 48f, drawRadius = 60f;
        public List<Batch> batches = new List<Batch>();
        public int cellsX = 22;
        public bool occlude;
        static Bounds XB(Bounds b, Matrix4x4 M)
        {
            var c = M.MultiplyPoint3x4(b.center); var e = b.extents; var ax = M.GetColumn(0) * e.x; var ay = M.GetColumn(1) * e.y; var az = M.GetColumn(2) * e.z;
            var ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            return new Bounds(c, ext * 2);
        }
        RenderParams rpStill, rpSway; bool init;

        void Init()
        {
            rpStill = new RenderParams(still) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
            rpSway = new RenderParams(sway) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
            init = true;
        }
        void Update()
        {
            var cam = Camera.main; if (!cam || meshes == null) return;
            if (!still || !sway) return;
            if (!init || rpStill.material == null) Init();
            var c = cam.transform.position; var f = AldaraCamera.I ? AldaraCamera.I.target ? AldaraCamera.I.target.position : c : c;
            float r2 = drawRadius * drawRadius;
            foreach (var b in batches)
            {
                int cx = b.cell % cellsX, cz = b.cell / cellsX;
                float mx = (cx + 0.5f) * cellSize, mz = (cz + 0.5f) * cellSize;
                float dx = mx - f.x, dz = mz - f.z; if (dx * dx + dz * dz > r2) continue;
                if (b.m == null || b.m.Length == 0 || b.mesh >= meshes.Length) continue;
                var mesh = meshes[b.mesh]; if (!mesh) continue;
                var rp = b.sway ? rpSway : rpStill;
                var arr = b.m;
                // trees in front of the hero are drawn at 45% (the browser's hide test, see AldaraOccluders)
                if (occlude && Application.isPlaying && AldaraPlayer.I && !AldaraWorld.Dun && dx * dx + dz * dz < (cellSize * 1.6f) * (cellSize * 1.6f))
                {
                    List<Matrix4x4> keep = null; var P = AldaraPlayer.I; var mb = mesh.bounds;
                    for (int k = 0; k < arr.Length; k++)
                    {
                        var M = arr[k]; var pos = (Vector3)M.GetColumn(3); var opx = AldaraWorld.ToPx(pos); if (opx.y <= P.y || opx.y > P.y + 600 || Mathf.Abs(opx.x - P.x) > 600) { if (keep != null) keep.Add(M); continue; }
                        var wb = XB(mb, M); float L = (wb.min.x - AldaraWorld.OX) * AldaraWorld.PX, W = wb.size.x * AldaraWorld.PX;
                        float T = AldaraWorld.YTOP - wb.max.z * AldaraWorld.PX - (wb.max.y - pos.y) * AldaraWorld.PX, B = AldaraWorld.YTOP - wb.min.z * AldaraWorld.PX - (wb.min.y - pos.y) * AldaraWorld.PX;
                        if (AldaraOccluders.Hide(P.x, P.y, opx.x, opx.y, L, W, T, B - T))
                        {
                            if (keep == null) { keep = new List<Matrix4x4>(arr.Length); for (int q = 0; q < k; q++) keep.Add(arr[q]); }
                            var fm = AldaraOccluders.FadeOf(rp.material);
                            Graphics.RenderMesh(new RenderParams(AldaraOccluders.Prime) { shadowCastingMode = ShadowCastingMode.Off }, mesh, 0, M);
                            Graphics.RenderMesh(new RenderParams(fm) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true }, mesh, 0, M);
                        }
                        else if (keep != null) keep.Add(M);
                    }
                    if (keep != null) arr = keep.ToArray();
                }
                for (int s = 0; s < arr.Length; s += 1023)
                    Graphics.RenderMeshInstanced(rp, mesh, 0, arr, Mathf.Min(1023, arr.Length - s), s);
            }
        }
    }
}
