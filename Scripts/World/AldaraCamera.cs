using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldara
{
    // The browser game's view, reproduced exactly: the ground is seen straight from above (1 px of world = 1 px of
    // screen at zoom 1) and a point at height h is drawn h higher on screen. Nearer = further south, as in the
    // browser's depth buffer. Models are squashed in depth by TSP and in height by TCP (their 0.36 rad view pitch)
    // through AldaraView.Squash, so they stand on their footprints exactly as in the original.
    // Mouse wheel zooms like the browser (CAM_Z 0.8 .. 1.6). There is no camera turning.
    [ExecuteAlways]
    public class AldaraCamera : MonoBehaviour
    {
        public static AldaraCamera I;
        public Transform target;
        public float zoom = 1f;                     // the browser's CAM_Z
        public float follow = 12f;
        public float viewHeightPx = 760f;           // world px shown top to bottom at zoom 1
        Vector3 focus; bool hasFocus;
        Camera cam;

        void OnEnable() { I = this; cam = GetComponent<Camera>(); }
        void LateUpdate()
        {
            if (!cam) cam = GetComponent<Camera>();
            if (Application.isPlaying)
            {
                var m = Mouse.current;
                if (m != null) { float w = m.scroll.ReadValue().y; if (Mathf.Abs(w) > 0.01f) zoom = Mathf.Clamp(zoom * (w > 0 ? 1.1f : 1 / 1.1f), 0.8f, 1.6f); }
            }
            Vector3 want = target ? target.position + new Vector3(0, 0, 18f / AldaraWorld.PX) : transform.position;
            if (!hasFocus || !Application.isPlaying) { focus = want; hasFocus = true; }
            else focus = Vector3.Lerp(focus, want, 1 - Mathf.Exp(-follow * Time.deltaTime));
            Apply(cam, focus, zoom, viewHeightPx);
        }
        public static void Apply(Camera cam, Vector3 focus, float zoom, float viewHeightPx = 760f)
        {
            cam.orthographic = true;
            cam.orthographicSize = viewHeightPx / zoom / 2f / AldaraWorld.PX;
            cam.nearClipPlane = 1f; cam.farClipPlane = 1000f;
            // keep the transform above the focus so terrain detail and shadows centre on the hero
            cam.transform.SetPositionAndRotation(focus + new Vector3(0, 60, -60), Quaternion.Euler(45, 0, 0));
            float px = focus.x, pz = focus.z, gy = focus.y;
            var v = new Matrix4x4();
            v.SetRow(0, new Vector4(1, 0, 0, -px));
            v.SetRow(1, new Vector4(0, 1, 1, -pz - gy));          // screen up = north + height
            v.SetRow(2, new Vector4(0, 0.002f, -1, pz - 300f));   // depth: south is nearer, higher slightly nearer
            v.SetRow(3, new Vector4(0, 0, 0, 1));
            cam.worldToCameraMatrix = v;
        }
        /// Screen point -> browser world px on the ground at the focus height, for clicking.
        public Vector2 ScreenToWorldPx(Vector2 screen)
        {
            var vp = cam.ScreenToViewportPoint(screen);
            float h = cam.orthographicSize * 2, w = h * cam.aspect;
            float ux = focus.x + (vp.x - 0.5f) * w, uz = focus.z + (vp.y - 0.5f) * h;
            return new Vector2(ux * AldaraWorld.PX, AldaraWorld.YTOP - uz * AldaraWorld.PX);
        }
    }

    public static class AldaraView
    {
        public const float PITCH = 0.36f;
        public static readonly float TSP = Mathf.Sin(PITCH), TCP = Mathf.Cos(PITCH);
        /// The browser draws every model with depth x TSP and height x TCP. Put this scale on an anchor at the model's
        /// ground point and rotate a child: world = T * Squash * R, as in the browser.
        public static Vector3 Squash { get { return new Vector3(1, TCP, TSP); } }
    }
}
