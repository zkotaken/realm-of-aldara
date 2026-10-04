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
        Camera cam; bool loaded; float lastStep;
        static readonly float[] QZ_STEPS = { 0.8f, 0.9f, 1, 1.1f, 1.2f, 1.35f, 1.5f, 1.6f };
        /// camZoom: the next step in or out, kept between sessions, with a short note
        public void Zoom(int dir)
        {
            int i = System.Array.FindIndex(QZ_STEPS, z => Mathf.Abs(z - zoom) < 0.001f); if (i < 0) i = 2;
            int ni = Mathf.Clamp(i + dir, 0, QZ_STEPS.Length - 1); if (ni == i) { AldaraHudBar.Toast("Zoom " + Mathf.RoundToInt(zoom * 100) + "% (limit)"); return; }
            zoom = QZ_STEPS[ni]; PlayerPrefs.SetFloat("aldara/zoom", zoom); AldaraHudBar.Toast("Zoom " + Mathf.RoundToInt(zoom * 100) + "%");
        }
        public void ZoomReset() { zoom = 1; PlayerPrefs.SetFloat("aldara/zoom", 1); AldaraHudBar.Toast("Zoom 100%"); }

        void OnEnable() { I = this; cam = GetComponent<Camera>(); }
        void LateUpdate()
        {
            if (!cam) cam = GetComponent<Camera>();
            if (Application.isPlaying)
            {
                if (!loaded) { loaded = true; zoom = PlayerPrefs.GetFloat("aldara/zoom", 1); }
                var m = Mouse.current; bool overUi = AldaraHud.MouseOverUi() || (AldaraWindows.I && AldaraWindows.I.IsOpen("map"));
                if (m != null && !overUi && AldaraSave.Ready)
                {   // one step per notch (QZ_STEPS); a middle click goes back to 100%
                    float w = m.scroll.ReadValue().y; if (Mathf.Abs(w) > 0.01f && Time.unscaledTime - lastStep > 0.09f) { lastStep = Time.unscaledTime; Zoom(w > 0 ? 1 : -1); }
                    if (m.middleButton.wasPressedThisFrame) ZoomReset();
                }
                if (AldaraSave.Ready && !AldaraHud.Typing) { if (AldaraKeys.Pressed("zoomin")) Zoom(1); if (AldaraKeys.Pressed("zoomout")) Zoom(-1); }
            }
            Vector3 want = target ? target.position + new Vector3(0, 0, 18f / AldaraWorld.PX) : transform.position;
            if (Application.isPlaying && AldaraVfx.I) { var sh = AldaraVfx.ShakeOffset; want += new Vector3(sh.x, 0, -sh.y) / AldaraWorld.PX; }
            if (!hasFocus || !Application.isPlaying) { focus = want; hasFocus = true; }
            else focus = want;   // the browser keeps the camera locked on the hero
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
            return new Vector2((ux - AldaraWorld.OX) * AldaraWorld.PX, AldaraWorld.YTOP - uz * AldaraWorld.PX);
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
