using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldara
{
    // The hero, simulated in the browser's pixel space (x east, y south) with the browser's rules:
    // WASD moves in world directions (W = north), on foot 72% of base speed, +12% on roads, 62% in water, 45% in lava,
    // and the capsule slides along mountain sides (port of capsuleSlide / slideMove / freeSpotNear).
    public class AldaraPlayer : MonoBehaviour
    {
        public static AldaraPlayer I;
        [Header("Browser state (px)")]
        public float x = 3600, y = 7190;
        public float facing = Mathf.PI / 2;   // radians, browser convention (atan2(dy, dx), 0 = east, PI/2 = south)
        public float speed = 220;             // player.speed
        public bool wings;
        public int liq;
        [Header("Visual")]
        public Transform model;               // child that holds the character (faces +Z)
        public float turnRate = 14f;
        public AldaraCharacterAnimator anim;

        public bool Moving { get; private set; }
        public bool MovingBack { get; private set; }
        float shownFacing;
        Vector2 lastSlide; float lastSlideT = -1;

        const float CAP_R = 7; const int CAP_N = 16;
        static Vector2[] CAP_DIRS;

        void Awake()
        {
            I = this; AldaraWorld.Load();
            if (CAP_DIRS == null) { CAP_DIRS = new Vector2[CAP_N]; for (int i = 0; i < CAP_N; i++) CAP_DIRS[i] = new Vector2(Mathf.Cos(i / (float)CAP_N * Mathf.PI * 2), Mathf.Sin(i / (float)CAP_N * Mathf.PI * 2)); }
            if (AldaraWorld.BlockedAt(x, y)) { var f = FreeSpotNear(x, y); x = f.x; y = f.y; }
            shownFacing = facing;
            transform.localScale = AldaraView.Squash;   // drawn like the browser: depth x TSP, height x TCP
            if (!anim) anim = GetComponentInChildren<AldaraCharacterAnimator>();
        }

        float PSpeed()
        {
            float road = AldaraWorld.RoadDist(x, y) < 30 ? 1.12f : 1f;
            float liqK = liq == 2 ? 0.45f : liq != 0 ? 0.62f : 1f;
            float slow = AldaraHero.I && AldaraHero.I.slowT > 0 ? 0.65f : 1f;
            return road * speed * (wings ? 1.15f : 0.72f) * liqK * slow;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (!AldaraSave.Ready) { transform.position = AldaraWorld.ToUnity(x, y + 18); return; }
            float lx = x, ly = y;
            var kb = Keyboard.current; var hero = AldaraHero.I;
            float dx = 0, dy = 0;
            if (kb != null && (hero == null || hero.alive))
            {
                if (AldaraKeys.Held("up") || kb.upArrowKey.isPressed) dy -= 1;
                if (AldaraKeys.Held("down") || kb.downArrowKey.isPressed) dy += 1;
                if (AldaraKeys.Held("left") || kb.leftArrowKey.isPressed) dx -= 1;
                if (AldaraKeys.Held("right") || kb.rightArrowKey.isPressed) dx += 1;
            }
            if (dx != 0 || dy != 0)
            {
                float len = Mathf.Sqrt(dx * dx + dy * dy); dx /= len; dy /= len;
                float sp = PSpeed();
                x = Mathf.Clamp(x + dx * sp * dt, 20, AldaraWorld.WORLD_W - 20);
                y = Mathf.Clamp(y + dy * sp * dt, 20, AldaraWorld.WORLD_H - 20);
                facing = Mathf.Atan2(dy, dx);
                if (hero) hero.target = null;
            }
            else if (hero && hero.Steer(dt, out float ang))
            {
                float sp = PSpeed();
                x = Mathf.Clamp(x + Mathf.Cos(ang) * sp * dt, 20, AldaraWorld.WORLD_W - 20);
                y = Mathf.Clamp(y + Mathf.Sin(ang) * sp * dt, 20, AldaraWorld.WORLD_H - 20);
                facing = ang;
            }
            CapsuleSlide(lx, ly);
            if (AldaraWorld.BlockedAt(x, y)) { var f = FreeSpotNear(x, y); x = f.x; y = f.y; }
            liq = wings ? 0 : AldaraWorld.LiquidAt(x, y);

            float mv = Mathf.Sqrt((x - lx) * (x - lx) + (y - ly) * (y - ly));
            Moving = mv > 0.05f;
            if (Moving) { float ma = Mathf.Atan2(y - ly, x - lx); MovingBack = Mathf.Cos(ma - facing) < -0.35f; }

            // place in Unity: feet on the ground, a little lower in water (wading)
            var p = AldaraWorld.ToUnity(x, y + 18);   // the browser draws the hero's feet 18 px below its point
            if (liq != 0) p.y = Mathf.Max(p.y, AldaraWorld.WATER_Y) - (liq == 3 ? 0.55f : 0.4f);
            transform.position = p;
            shownFacing = Mathf.LerpAngle(shownFacing * Mathf.Rad2Deg, facing * Mathf.Rad2Deg, 1 - Mathf.Exp(-turnRate * dt)) * Mathf.Deg2Rad;
            // browser facing 0 = east (+x), PI/2 = south (Unity -z). Unity yaw: 0 = +z, 90 = +x
            if (model) model.localRotation = Quaternion.Euler(0, 90 + shownFacing * Mathf.Rad2Deg, 0);
            // with wings you fly: no running gait (the browser's _movingNow is false while hovering)
            if (anim) anim.SetMoving(Moving && !wings, MovingBack, mv / Mathf.Max(dt, 1e-4f));
        }

        // ---- collision: port of the browser's capsule slide ----
        static bool Blocked(float x, float y) { return AldaraWorld.BlockedAt(x, y); }
        Vector2? CapNormal(float x, float y, float R)
        {
            float nx = 0, ny = 0;
            foreach (var d in CAP_DIRS) if (Blocked(x + d.x * R, y + d.y * R)) { nx -= d.x; ny -= d.y; }
            float l = Mathf.Sqrt(nx * nx + ny * ny); return l > 1e-6f ? new Vector2(nx / l, ny / l) : (Vector2?)null;
        }
        void SlideMove(float ox, float oy)
        {
            if (!Blocked(x, y) || Blocked(ox, oy)) return;
            float nx = x, ny = y;
            if (!Blocked(nx, oy)) y = oy;
            else if (!Blocked(ox, ny)) x = ox;
            else { x = ox; y = oy; }
        }
        void CapsuleSlide(float ox, float oy)
        {
            float dx = x - ox, dy = y - oy, L = Mathf.Sqrt(dx * dx + dy * dy); if (L < 1e-4f) return;
            if (!Blocked(x, y)) return;
            if (Blocked(ox, oy)) { SlideMove(ox, oy); return; }
            var n = CapNormal(x, y, CAP_R + 8) ?? CapNormal(ox, oy, CAP_R + 12);
            if (n.HasValue)
            {
                var N = n.Value; float dot = dx * N.x + dy * N.y;
                if (dot < 0)
                {
                    float tx = dx - dot * N.x, ty = dy - dot * N.y, tl = Mathf.Sqrt(tx * tx + ty * ty);
                    if (tl > 1e-4f)
                    {
                        float ux = tx / tl, uy = ty / tl, sp = Mathf.Max(tl, L * 0.55f);
                        foreach (var lean in new[] { 0f, 0.25f, 0.5f, 0.8f })
                        {
                            float vx = ux + N.x * lean, vy = uy + N.y * lean, vl = Mathf.Sqrt(vx * vx + vy * vy); vx = vx / vl * sp; vy = vy / vl * sp;
                            if (!Blocked(ox + vx, oy + vy))
                            {
                                float now = Time.time;
                                if (lastSlideT >= 0 && now - lastSlideT < 0.25f && lastSlide.x * vx + lastSlide.y * vy < 0) { x = ox; y = oy; return; }
                                lastSlide = new Vector2(vx, vy); lastSlideT = now;
                                x = ox + vx; y = oy + vy; return;
                            }
                        }
                    }
                }
            }
            SlideMove(ox, oy);
        }
        public static Vector2 FreeSpotNear(float x, float y)
        {
            for (float r = 24; r < 3000; r += 24)
                for (float a = 0; a < 6.28f; a += 12 / r + 0.05f)
                {
                    float px = x + Mathf.Cos(a) * r, py = y + Mathf.Sin(a) * r;
                    if (!AldaraWorld.BlockedAt(px, py) && AldaraWorld.LiquidAt(px, py) == 0) return new Vector2(px, py);
                }
            return AldaraWorld.TOWN_SPAWN;
        }
    }
}
