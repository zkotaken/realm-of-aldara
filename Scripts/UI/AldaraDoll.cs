using UnityEngine;
using UnityEngine.UIElements;

namespace Aldara
{
    // The inventory's big preview of your character (the browser's drawDoll): the hero model rendered by its own camera
    // into a texture, turned with the arrow buttons, by dragging or with the wheel, or left spinning.
    public static class AldaraDoll
    {
        public static float a = 0.4f; public static bool spin;
        static GameObject rig, model; static Camera cam; static RenderTexture rt; static string builtCls;
        static VisualElement view; static float dragX = float.NaN, dragA;
        static readonly Vector3 HOME = new Vector3(-4000, 0, -4000);

        public static void Turn(int d) { spin = false; a += d * Mathf.PI / 4; }
        public static void Show(bool on) { if (rig) rig.SetActive(on); }
        static void Build()
        {
            var H = AldaraHero.I; if (!H) return;
            if (!rig)
            {
                rig = new GameObject("DollRig"); rig.transform.position = HOME; Object.DontDestroyOnLoad(rig);
                var cg = new GameObject("DollCam"); cg.transform.SetParent(rig.transform, false); cam = cg.AddComponent<Camera>();
                cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); cam.nearClipPlane = 0.1f; cam.farClipPlane = 50;
                rt = new RenderTexture(260, 600, 24, RenderTextureFormat.ARGB32) { name = "Doll", antiAliasing = 4 }; cam.targetTexture = rt;
            }
            if (builtCls != H.cls || !model)
            {
                if (model) Object.Destroy(model);
                var pf = Resources.Load<GameObject>("Heroes/Hero_" + H.cls); if (!pf) return;
                model = Object.Instantiate(pf, rig.transform, false); model.name = "DollHero"; model.transform.localPosition = Vector3.zero; builtCls = H.cls;
                var an = model.GetComponent<AldaraCharacterAnimator>(); if (an) an.ApplyRest();
            }
            Fit();
        }
        static void Fit()
        {
            var rs = model.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
            model.transform.localRotation = Quaternion.identity;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float feet = b.min.y - HOME.y, h = b.size.y, w = Mathf.Max(b.size.x, b.size.z);
            float aspect = 260f / 600f, size = Mathf.Max(h / 1.64f, w / 2f / aspect * 1.08f);
            cam.orthographicSize = size;
            cam.transform.localPosition = new Vector3(0, feet + 0.74f * size + 0.04f + 12 * Mathf.Tan(4 * Mathf.Deg2Rad), -12); cam.transform.localRotation = Quaternion.Euler(4, 0, 0);
        }
        public static void Attach(VisualElement parent)
        {
            Build(); if (!rt) return;
            view = new VisualElement { name = "doll" }; view.style.width = 130; view.style.height = 300; parent.Add(view);
            var shadow = new Ellipse(); shadow.style.position = Position.Absolute; shadow.style.left = 0; shadow.style.right = 0; shadow.style.top = 0; shadow.style.bottom = 0; shadow.pickingMode = PickingMode.Ignore; view.Add(shadow);
            var img = new VisualElement { pickingMode = PickingMode.Ignore }; img.style.position = Position.Absolute; img.style.left = 0; img.style.right = 0; img.style.top = 0; img.style.bottom = 0;
            img.style.backgroundImage = Background.FromRenderTexture(rt); view.Add(img);
            view.RegisterCallback<PointerDownEvent>(e => { dragX = e.position.x; dragA = a; spin = false; view.CapturePointer(e.pointerId); });
            view.RegisterCallback<PointerMoveEvent>(e => { if (!float.IsNaN(dragX)) a = dragA + (e.position.x - dragX) * 0.025f; });
            view.RegisterCallback<PointerUpEvent>(e => { dragX = float.NaN; view.ReleasePointer(e.pointerId); });
            view.RegisterCallback<WheelEvent>(e => { a += Mathf.Sign(e.delta.y) * 0.2f; e.StopPropagation(); });
        }
        public static void Tick()
        {
            if (!rig || !model) return;
            var H = AldaraHero.I; if (H && builtCls != H.cls) Build();
            if (spin) a += Mathf.Min(0.1f, Time.unscaledDeltaTime) * 1.2f;
            float facing = Mathf.PI / 2 + a;   // the browser draws the doll at angle PI/2 + a
            model.transform.localRotation = Quaternion.Euler(0, 90 + facing * Mathf.Rad2Deg, 0);
        }
        class Ellipse : VisualElement
        {   // the soft shadow under the doll's feet (#0006)
            public Ellipse() { generateVisualContent += Draw; }
            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect; if (r.width <= 0) return; var p = ctx.painter2D; p.fillColor = new Color(0, 0, 0, 0.4f);
                var c = new Vector2(r.width / 2, r.height * 0.87f); float rx = r.width * 0.3f, ry = rx * 0.28f;
                p.BeginPath(); for (int i = 0; i <= 32; i++) { float t = i / 32f * Mathf.PI * 2; var q = c + new Vector2(Mathf.Cos(t) * rx, Mathf.Sin(t) * ry); if (i == 0) p.MoveTo(q); else p.LineTo(q); } p.ClosePath(); p.Fill();
            }
        }
    }
}
