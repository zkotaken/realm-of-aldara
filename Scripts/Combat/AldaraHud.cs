using UnityEngine;

namespace Aldara
{
    // A first HUD: health and mana, experience, level and gold, the target frame, floating combat text, banners
    // and health bars over hurt monsters. (The full browser HUD is ported later.)
    public class AldaraHud : MonoBehaviour
    {
        static string banner; static float bannerT;
        static Rect[] uiRects = new Rect[0];
        public static void Banner(string s) { banner = s; bannerT = 1.4f; AldaraHudUI.ShowBanner(s); }
        /// a text box has the keyboard: no game or menu key does anything. It stays set for the frame the box lets go,
        /// so the Esc or Enter that closed it does not also open the Game Menu or the chat again
        public static bool Typing { get { return typing || Time.frameCount - typingOff <= 1 || AldaraUI.TextFocused(); } set { if (typing && !value) typingOff = Time.frameCount; typing = value; } }
        static bool typing; static int typingOff = -10;
        static Rect blockR; static int blockF = -10;
        public static void Block(Rect r) { blockR = r; blockF = Time.frameCount; }
        public static bool MouseOverUi()
        {
            var m = UnityEngine.InputSystem.Mouse.current; if (m == null) return false; var p = m.position.ReadValue(); if (AldaraUI.PointerOverUi(p)) return true; p.y = Screen.height - p.y;
            if (Time.frameCount - blockF < 3 && blockR.Contains(p)) return true;
            foreach (var r in uiRects) if (r.Contains(p)) return true; return false;
        }
        GUIStyle txt, big, small; Texture2D white;
        void Init()
        {
            if (white) return; white = Texture2D.whiteTexture;
            txt = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; txt.normal.textColor = Color.white;
            big = new GUIStyle(txt) { fontSize = 30 }; small = new GUIStyle(txt) { fontSize = 12, fontStyle = FontStyle.Normal };
        }
        void Bar(Rect r, float f, Color c, string label)
        {
            GUI.color = new Color(0, 0, 0, 0.65f); GUI.DrawTexture(r, white);
            GUI.color = c; GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(f), r.height - 4), white);
            GUI.color = Color.white; if (label != null) Shadow(r, label, small);
        }
        void Shadow(Rect r, string s, GUIStyle st)
        {
            var c = st.normal.textColor; st.normal.textColor = new Color(0, 0, 0, 0.8f); GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), s, st); st.normal.textColor = c; GUI.Label(r, s, st);
        }
        void Update() { if (bannerT > 0) bannerT -= Time.deltaTime; }
        void OnGUI()
        {
            Init(); var H = AldaraHero.I; var P = AldaraPlayer.I; var cam = Camera.main; if (!H || !P || !cam || !AldaraSave.Ready) return;
            float W = Screen.width, Hh = Screen.height;
            bool ui = AldaraHudUI.I != null;
            if (ui)
            {
            // floating combat text
            if (!AldaraQuestWorld.I) foreach (var f in AldaraFx.texts)
            {
                var sp = cam.WorldToScreenPoint(AldaraWorld.ToUnityFlat(f.x, f.y) + Vector3.up * AldaraWorld.GroundY(f.x, f.y));
                var st = new GUIStyle(txt); var c = f.col; c.a = Mathf.Clamp01(f.life * 1.6f); st.normal.textColor = c;
                Shadow(new Rect(sp.x - 80, Hh - sp.y - 10, 160, 20), f.text, st);
            }
                uiRects = new Rect[0]; return;
            }
            // player frame (top left)
            var pf = new Rect(16, 16, 300, 74);
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.75f); GUI.DrawTexture(pf, white); GUI.color = Color.white;
            var nm = new GUIStyle(txt) { alignment = TextAnchor.MiddleLeft, fontSize = 14 };
            Shadow(new Rect(28, 20, 280, 20), H.heroName + "  -  Lv " + H.lvl + " " + char.ToUpper(H.cls[0]) + H.cls.Substring(1), nm);
            Bar(new Rect(26, 42, 280, 18), H.hp / H.maxHp, new Color(0.78f, 0.12f, 0.12f), Mathf.CeilToInt(Mathf.Max(0, H.hp)) + " / " + H.maxHp);
            Bar(new Rect(26, 62, 280, 14), H.mana / H.maxMana, new Color(0.18f, 0.35f, 0.85f), Mathf.FloorToInt(H.mana) + " / " + H.maxMana);
            var gs = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft };
            Shadow(new Rect(20, 94, 300, 18), "ATK " + H.atk + "   SPD " + Mathf.RoundToInt(H.speed) + "   Gold " + H.gold + "   Kills " + H.kills, gs);
            // experience (bottom)
            Bar(new Rect(W * 0.25f, Hh - 22, W * 0.5f, 12), H.xp / H.xpNeed, new Color(0.55f, 0.38f, 0.9f), H.xp + " / " + H.xpNeed + " XP");
            // zone (top right)
            Shadow(new Rect(W - 260, 16, 240, 24), AldaraWorld.ZoneName(P.x, P.y), new GUIStyle(txt) { fontSize = 17 });
            // target frame (top centre)
            var t = H.target; Rect tf = new Rect(0, 0, 0, 0);
            if (t != null && !t.dead)
            {
                tf = new Rect(W / 2 - 170, 16, 340, 52);
                GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.75f); GUI.DrawTexture(tf, white); GUI.color = Color.white;
                int g = t.lvl - H.lvl; var lc = g >= 5 ? new Color(1, 0.3f, 0.3f) : g >= 3 ? new Color(1, 0.6f, 0.2f) : g >= -2 ? new Color(1, 0.9f, 0.4f) : g >= -8 ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
                var ts = new GUIStyle(txt) { fontSize = 14 }; ts.normal.textColor = t.boss ? new Color(1, 0.75f, 0.3f) : Color.white;
                Shadow(new Rect(tf.x, tf.y + 4, tf.width, 18), (t.boss ? "Boss: " : "") + t.name, ts);
                var ls = new GUIStyle(small) { alignment = TextAnchor.MiddleRight }; ls.normal.textColor = lc; GUI.Label(new Rect(tf.x, tf.y + 4, tf.width - 10, 18), "Lv " + t.lvl, ls);
                Bar(new Rect(tf.x + 10, tf.y + 26, tf.width - 20, 18), t.hp / t.maxHp, new Color(0.75f, 0.15f, 0.1f), Mathf.CeilToInt(t.hp) + " / " + t.maxHp);
            }
            // skill bar (keys 1-9, 0)
            var S = AldaraSkills.I; Rect bar = new Rect(0, 0, 0, 0);
            if (S)
            {
                float sz = 54, gap = 6, bw2 = 10 * sz + 9 * gap; bar = new Rect(W / 2 - bw2 / 2, Hh - 44 - sz, bw2, sz);
                var ks = new GUIStyle(small) { alignment = TextAnchor.UpperLeft, fontSize = 11 }; var ns = new GUIStyle(small) { fontSize = 10, wordWrap = true };
                for (int i = 0; i < 10; i++)
                {
                    var r = new Rect(bar.x + i * (sz + gap), bar.y, sz, sz);
                    GUI.color = new Color(0.06f, 0.06f, 0.1f, 0.85f); GUI.DrawTexture(r, white); GUI.color = Color.white;
                    if (i < S.equipped.Count)
                    {
                        var sk = S.Get(S.equipped[i]); bool ok = sk != null && H.mana >= sk.cost;
                        GUI.color = ok ? new Color(0.35f, 0.3f, 0.18f, 0.9f) : new Color(0.2f, 0.2f, 0.28f, 0.9f); GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, sz - 4, sz - 4), white); GUI.color = Color.white;
                        Shadow(new Rect(r.x + 2, r.y + 8, sz - 4, sz - 10), sk != null ? sk.name : "", ns);
                        float cd = sk != null ? S.Cd(sk.id) : 0; if (cd > 0 && sk.cd > 0) { GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(new Rect(r.x, r.y + sz * (1 - cd / sk.cd), sz, sz * cd / sk.cd), white); GUI.color = Color.white; Shadow(r, Mathf.CeilToInt(cd).ToString(), txt); }
                        if (S.queued != null && sk != null && S.queued == sk.id) { GUI.color = new Color(1, 0.85f, 0.3f, 0.5f); GUI.DrawTexture(new Rect(r.x, r.y + sz - 4, sz, 4), white); GUI.color = Color.white; }
                    }
                    GUI.Label(new Rect(r.x + 4, r.y + 2, 20, 14), ((i + 1) % 10).ToString(), ks);
                }
            }
            uiRects = new[] { pf, tf, bar };
            // health bars over hurt or targeted monsters
            foreach (var m in AldaraMonsters.I.all)
            {
                if (m.dead || !m.view || !m.view.activeSelf) continue; if (!(m.hp < m.maxHp || m == t)) continue;
                var sp = cam.WorldToScreenPoint(AldaraWorld.ToUnity(m.x, m.y) + Vector3.up * (m.r * 2.4f + m.lift) / AldaraWorld.PX);
                if (sp.z < 0) continue; float bw = Mathf.Clamp(m.r * 2.4f, 34, 110);
                Bar(new Rect(sp.x - bw / 2, Hh - sp.y - 6, bw, 7), m.hp / m.maxHp, m == t ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.75f, 0.15f, 0.1f), null);
            }
            // floating combat text
            foreach (var f in AldaraFx.texts)
            {
                var sp = cam.WorldToScreenPoint(AldaraWorld.ToUnityFlat(f.x, f.y) + Vector3.up * AldaraWorld.GroundY(f.x, f.y));
                var st = new GUIStyle(txt); var c = f.col; c.a = Mathf.Clamp01(f.life * 1.6f); st.normal.textColor = c;
                Shadow(new Rect(sp.x - 80, Hh - sp.y - 10, 160, 20), f.text, st);
            }
            if (bannerT > 0) { var b = new GUIStyle(big); b.normal.textColor = new Color(1, 0.9f, 0.55f, Mathf.Clamp01(bannerT * 2)); Shadow(new Rect(0, Hh * 0.22f, W, 40), banner, b); }
            if (!H.alive) { var b = new GUIStyle(big); b.normal.textColor = new Color(1, 0.4f, 0.4f); Shadow(new Rect(0, Hh * 0.4f, W, 40), "You have fallen...", b); }
        }
    }
}
