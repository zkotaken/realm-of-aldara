using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aldara
{
    // The browser's interface layout editor (hudEdit, HUD_MOVABLE): drag any piece of the interface where you want it.
    // The player frame, the target frame, the minimap with the zone and quests, the chat, the bars and hotbar, the dungeon
    // banner and the FPS counter can all move; the places are kept between sessions ('aldara/hud/layout').
    public static class AldaraHudLayout
    {
        static readonly string[][] MOVABLE = {
            new[] { "player", "Player frame" }, new[] { "target", "Target frame" }, new[] { "topRight", "Minimap and quests" }, new[] { "chat", "Chat" },
            new[] { "bottom", "Bars and hotbar" }, new[] { "dun", "Dungeon banner" }, new[] { "fps", "FPS counter" } };
        static string[] Parts(string k) { return k == "topRight" ? new[] { "minimap", "zone", "quest", "tip" } : k == "chat" ? new[] { "chat", "chatbox" } : new[] { k }; }
        static Dictionary<string, Vector2> off;
        static readonly Dictionary<VisualElement, Translate> baseT = new Dictionary<VisualElement, Translate>();
        static void Load()
        {
            if (off != null) return; off = new Dictionary<string, Vector2>();
            try { var j = JObject.Parse(PlayerPrefs.GetString("aldara/hud/layout", "{}")); foreach (var kv in j) off[kv.Key] = new Vector2((float)kv.Value[0], (float)kv.Value[1]); } catch { }
        }
        static void Save() { var j = new JObject(); foreach (var kv in off) j[kv.Key] = new JArray(Mathf.Round(kv.Value.x), Mathf.Round(kv.Value.y)); PlayerPrefs.SetString("aldara/hud/layout", j.ToString(Newtonsoft.Json.Formatting.None)); PlayerPrefs.Save(); }
        static IEnumerable<VisualElement> Elems(string k)
        {
            var H = AldaraHudUI.I; if (H == null) yield break;
            foreach (var p in Parts(k)) { var l = H.Piece(p); if (l == null) continue; foreach (var e in l) if (e != null) yield return e; }
        }
        /// hudApplyLayout: every piece's elements shifted by its offset
        public static void Apply()
        {
            Load(); if (AldaraHudUI.I == null) return;
            foreach (var m in MOVABLE)
            {
                Vector2 o; off.TryGetValue(m[0], out o);
                foreach (var e in Elems(m[0]))
                {
                    Translate b; if (!baseT.TryGetValue(e, out b)) { var t = e.style.translate; b = t.keyword == StyleKeyword.Undefined ? t.value : new Translate(0, 0); baseT[e] = b; }
                    if (o == Vector2.zero) e.style.translate = b.x.value == 0 && b.y.value == 0 && b.x.unit == LengthUnit.Pixel ? new StyleTranslate(StyleKeyword.Null) : new StyleTranslate(b);
                    else e.style.translate = new Translate(new Length(b.x.value + (b.x.unit == LengthUnit.Pixel ? o.x : 0), b.x.unit), new Length(b.y.value + (b.y.unit == LengthUnit.Pixel ? o.y : 0), b.y.unit));
                    if (b.x.unit != LengthUnit.Pixel && o.x != 0) e.style.marginLeft = o.x; else if (b.x.unit != LengthUnit.Pixel) e.style.marginLeft = StyleKeyword.Null;
                }
            }
        }
        public static void Reset() { Load(); off.Clear(); Save(); Apply(); AldaraHud.Banner("Layout restored"); }

        // ---- the editor ----
        public static bool Editing;
        static VisualElement layer, bar; static readonly Dictionary<string, VisualElement> boxes = new Dictionary<string, VisualElement>();
        static string drag; static Vector2 dragFrom, offFrom;
        public static void Edit(bool on)
        {
            var H = AldaraHudUI.I; if (H == null || H.Root == null) return; Load(); Editing = on;
            if (!on) { if (layer != null) layer.RemoveFromHierarchy(); layer = null; boxes.Clear(); Save(); return; }
            if (AldaraWindows.I) AldaraWindows.I.CloseAll();
            layer = new VisualElement { pickingMode = PickingMode.Ignore }; layer.style.position = Position.Absolute; layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0; H.Root.Add(layer);
            foreach (var m in MOVABLE)
            {
                string k = m[0]; var b = new VisualElement { pickingMode = PickingMode.Position }; b.style.position = Position.Absolute;
                b.style.borderLeftWidth = b.style.borderRightWidth = b.style.borderTopWidth = b.style.borderBottomWidth = 2; b.style.borderLeftColor = b.style.borderRightColor = b.style.borderTopColor = b.style.borderBottomColor = AldaraRules.Hex("#7fd8ff");
                b.style.backgroundColor = new Color(0.5f, 0.85f, 1, 0.06f);
                var l = new Label(m[1]) { pickingMode = PickingMode.Ignore }; l.style.position = Position.Absolute; l.style.left = 4; l.style.top = -20; l.style.fontSize = 12; l.style.color = AldaraRules.Hex("#bfeeff"); l.style.unityFontDefinition = FontDefinition.FromFont(AldaraUI.Font(true, true)); l.style.backgroundColor = new Color(0, 0, 0, 0.7f); l.style.paddingLeft = l.style.paddingRight = 6; b.Add(l);
                b.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; drag = k; dragFrom = e.position; Vector2 o; off.TryGetValue(k, out o); offFrom = o; b.CapturePointer(e.pointerId); e.StopPropagation(); });
                b.RegisterCallback<PointerMoveEvent>(e => { if (drag != k) return; var d = (Vector2)e.position - dragFrom; off[k] = offFrom + d; Apply(); });
                b.RegisterCallback<PointerUpEvent>(e => { if (drag != k) return; drag = null; b.ReleasePointer(e.pointerId); Save(); });
                layer.Add(b); boxes[k] = b;
            }
            bar = new VisualElement { pickingMode = PickingMode.Position }; bar.style.position = Position.Absolute; bar.style.left = Length.Percent(50); bar.style.top = 120; bar.style.translate = new Translate(Length.Percent(-50), 0);
            bar.style.backgroundColor = new Color(10 / 255f, 12 / 255f, 20 / 255f, 0.94f); bar.style.borderLeftWidth = bar.style.borderRightWidth = bar.style.borderTopWidth = bar.style.borderBottomWidth = 1; bar.style.borderLeftColor = bar.style.borderRightColor = bar.style.borderTopColor = bar.style.borderBottomColor = AldaraRules.Hex("#7fd8ff");
            bar.style.paddingLeft = bar.style.paddingRight = 18; bar.style.paddingTop = bar.style.paddingBottom = 12; bar.style.alignItems = Align.Center; bar.style.maxWidth = 520;
            var h = new Label("Layout editor"); h.style.fontSize = 16; h.style.color = AldaraRules.Hex("#ffe8a0"); h.style.unityFontDefinition = FontDefinition.FromFont(AldaraUI.Font(true, true)); bar.Add(h);
            var s2 = new Label("Drag any piece of the interface to where you want it. The hotbar, frames, chat, minimap and dungeon banner can all move."); s2.style.whiteSpace = WhiteSpace.Normal; s2.style.unityTextAlign = TextAnchor.MiddleCenter; s2.style.fontSize = 12; s2.style.color = AldaraRules.Hex("#d8d0bc"); s2.style.marginTop = 4; s2.style.unityFontDefinition = FontDefinition.FromFont(AldaraUI.Font(false, false)); bar.Add(s2);
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.marginTop = 8; bar.Add(row);
            var r1 = AldaraUI.B(row, "Reset positions", () => Reset(), "btn", 12); r1.style.marginRight = 8; AldaraUI.B(row, "Done", () => Edit(false), "btn_gold", 12);
            layer.Add(bar);
        }
        /// each frame while editing: the outline boxes follow their pieces
        public static void Tick()
        {
            if (!Editing || layer == null) return; var H = AldaraHudUI.I;
            var kb = UnityEngine.InputSystem.Keyboard.current; if (kb != null && kb.escapeKey.wasPressedThisFrame) { Edit(false); return; }
            foreach (var m in MOVABLE)
            {
                var b = boxes[m[0]]; Rect u = Rect.zero; bool any = false;
                foreach (var e in Elems(m[0])) { if (e.panel == null || e.resolvedStyle.display == DisplayStyle.None) continue; var w = e.worldBound; if (w.width <= 0 || w.height <= 0 || float.IsNaN(w.x)) continue; u = any ? Rect.MinMaxRect(Mathf.Min(u.xMin, w.xMin), Mathf.Min(u.yMin, w.yMin), Mathf.Max(u.xMax, w.xMax), Mathf.Max(u.yMax, w.yMax)) : w; any = true; }
                b.style.display = any ? DisplayStyle.Flex : DisplayStyle.None; if (!any) continue;
                var lo = layer.WorldToLocal(u.position); var hi = layer.WorldToLocal(u.max);
                b.style.left = lo.x - 4; b.style.top = lo.y - 4; b.style.width = hi.x - lo.x + 8; b.style.height = hi.y - lo.y + 8;
            }
        }
    }
}
