using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Aldara
{
    // The picture's frame on the screen:
    // - aspect ratio (Graphics, Display): Auto fills the window; a fixed ratio letterboxes or pillarboxes the world and the
    //   interface into the largest box of that shape, with black bars around it;
    // - interface scaling: the HUD was laid out at 1920 x 1080, so the whole interface scales to fit that frame inside the
    //   picture (the smaller of width / 1920 and height / 1080), times the Interface "Whole interface" size. On narrow screens
    //   (4:3, 5:4) nothing overlaps or runs off the edge, and on wide ones it keeps the height, like the browser;
    // - world zoom (Graphics, World zoom): the browser's VIEW_S. Auto shows 760 world pixels top to bottom (or the whole
    //   picture on a short window); a percentage shows the picture's height divided by it.
    public class AldaraDisplay : MonoBehaviour
    {
        public static AldaraDisplay I;
        /// the picture's rectangle in screen pixels (origin bottom left, like Camera.pixelRect)
        public static Rect View = new Rect(0, 0, 1920, 1080);
        /// panel units per screen pixel of the interface
        public static float UiScale = 1;
        const float RW = 1920, RH = 1080;

        PanelSettings ps; PanelScaleMode oMode; float oScale; PanelScreenMatchMode oMatch; Vector2Int oRef;
        Camera bars; readonly List<UIDocument> docs = new List<UIDocument>(); float docT; Rect lastRoot;

        void Awake()
        {
            I = this; ps = Resources.Load<PanelSettings>("UI/AldaraPanel");
            if (ps) { oMode = ps.scaleMode; oScale = ps.scale; oMatch = ps.screenMatchMode; oRef = ps.referenceResolution; }
        }
        void OnDestroy()
        {   // the panel asset is shared with the editor: hand it back as it was
            if (ps) { ps.scaleMode = oMode; ps.scale = oScale; ps.screenMatchMode = oMatch; ps.referenceResolution = oRef; }
            if (bars) Destroy(bars.gameObject);
        }
        public static float AspectOf(string a)
        {
            switch (a)
            {
                case "16:9": return 16f / 9f; case "16:10": return 16f / 10f; case "21:9": return 64f / 27f; case "32:9": return 32f / 9f;
                case "4:3": return 4f / 3f; case "5:4": return 5f / 4f; case "3:2": return 3f / 2f; default: return 0;
            }
        }
        void LateUpdate()
        {
            float W = Screen.width, H = Screen.height; if (W < 8 || H < 8) return;
            var at = AldaraSettings.Get("aspect"); float a = AspectOf(at != null ? (string)at : "auto");
            Rect r = new Rect(0, 0, W, H);
            if (a > 0)
            {
                if (W / H > a + 0.002f) { float w = Mathf.Round(H * a); r = new Rect(Mathf.Floor((W - w) / 2), 0, w, H); }
                else if (W / H < a - 0.002f) { float h = Mathf.Round(W / a); r = new Rect(0, Mathf.Floor((H - h) / 2), W, h); }
            }
            View = r; bool boxed = r.width < W - 0.5f || r.height < H - 0.5f;

            // the world camera into the box, black bars around it
            var cam = Camera.main;
            if (cam)
            {
                var nr = new Rect(r.x / W, r.y / H, r.width / W, r.height / H); if (cam.rect != nr) cam.rect = nr;
                // world zoom: the browser's VIEW_S on the picture's height in logical pixels
                var cc = cam.GetComponent<AldaraCamera>();
                if (cc)
                {
                    float dpr = Screen.dpi > 0 ? Mathf.Max(1, Screen.dpi / 96f) : 1;
                    var vs = AldaraSettings.Get("viewScale"); string m = vs != null ? (string)vs : "auto";
                    float vh = m == "auto" ? Mathf.Min(760, r.height) : r.height / dpr / Mathf.Max(0.5f, AldaraSettings.F("viewScale", 1));
                    cc.viewHeightPx = Mathf.Clamp(vh, 240, 4000);
                }
            }
            if (boxed && !bars) MakeBars();
            if (bars && bars.enabled != boxed) bars.enabled = boxed;

            // the interface: fit the 1920 x 1080 layout into the box
            if (ps)
            {
                float s = Mathf.Min(r.width / RW, r.height / RH) * Mathf.Clamp(AldaraSettings.F("uiAll", 1), 0.5f, 2f);
                s = Mathf.Max(0.2f, s); UiScale = s;
                if (ps.scaleMode != PanelScaleMode.ConstantPixelSize) ps.scaleMode = PanelScaleMode.ConstantPixelSize;
                if (Mathf.Abs(ps.scale - s) > 0.0001f) ps.scale = s;
                // every document's root covers the box (panel units: screen pixels / scale, top left origin)
                var want = new Rect(r.x / s, (H - r.yMax) / s, r.width / s, r.height / s);
                docT -= Time.unscaledDeltaTime;
                if (docT <= 0 || want != lastRoot)
                {
                    docT = 0.5f; lastRoot = want; docs.Clear(); docs.AddRange(FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
                    foreach (var d in docs)
                    {
                        if (!d || d.panelSettings != ps) continue; var e = d.rootVisualElement; if (e == null) continue;
                        e.style.position = Position.Absolute; e.style.left = want.x; e.style.top = want.y; e.style.width = want.width; e.style.height = want.height;
                    }
                }
            }
        }
        void MakeBars()
        {
            var go = new GameObject("Letterbox"); go.hideFlags = HideFlags.DontSave; DontDestroyOnLoad(go);
            bars = go.AddComponent<Camera>(); bars.depth = -100; bars.cullingMask = 0; bars.clearFlags = CameraClearFlags.SolidColor; bars.backgroundColor = Color.black;
            bars.orthographic = true; bars.useOcclusionCulling = false; bars.allowHDR = false; bars.allowMSAA = false;
            var d = go.AddComponent<UniversalAdditionalCameraData>(); d.renderPostProcessing = false; d.renderShadows = false; d.requiresDepthTexture = false; d.requiresColorTexture = false;
        }
        /// a screen point (pixels, bottom left origin) to the interface's panel units inside the box (top left origin)
        public static Vector2 ScreenToUi(Vector2 sp)
        {
            return new Vector2((sp.x - View.x) / UiScale, (View.yMax - sp.y) / UiScale);
        }
    }
}
