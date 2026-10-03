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
                for (int s = 0; s < b.m.Length; s += 1023)
                    Graphics.RenderMeshInstanced(rp, mesh, 0, b.m, Mathf.Min(1023, b.m.Length - s), s);
            }
        }
    }
}
