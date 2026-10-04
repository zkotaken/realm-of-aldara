using UnityEngine;

namespace Aldara
{
    // The browser game's world lives in a 2D pixel space: x to the east, y to the south, 32500 x 25500 px.
    // Unity: 1 unit = 32 px. X = x/32, Z = (YTOP - y)/32 so north is +Z, Y = height/32 * HS (heights exaggerated
    // because the browser's oblique camera makes a 240 px mountain look far taller than 7.5 m).
    // The 16 px grids exported from the browser build (height, liquid, zone, blocked, road distance) are loaded
    // here so gameplay (collision, zones, swimming) matches the original exactly.
    public static class AldaraWorld
    {
        public const float PX = 32f;          // pixels per Unity unit
        public const float HS = 1f;   // the browser draws a point at height h exactly h px higher         // height exaggeration
        public const float YTOP = 28672f;     // browser y that maps to Unity z = 0 (7 tiles of 4096 px)
        public const int WORLD_W = 32500, WORLD_H = 25500;
        public const int CELL = 16;           // grid cell in px
        public const float TILE_PX = 4096f;   // one terrain tile in px (128 units)
        public const int TILES_X = 8, TILES_Y = 7;
        public const float BASE_Y = -3f;      // terrain origin height (water is carved below 0)
        public const float SPAN_Y = 32f;      // terrain height range
        public const float WATER_Y = -0.3f;

        public static readonly string[] ZoneNames = {
            "Green Fields","Ashen Highlands","The Shadowfen","Frostpeak Tundra","Emberwaste","The Void Reach",
            "Elysian Expanse","The Brigand Marches","Gloomwood","The Crownlands","The Bloodmoor" };
        public static readonly Vector2 CENTER = new Vector2(3600, 7000), TOWN_SPAWN = new Vector2(3600, 7190);
        public static readonly Vector3 KINGDOM = new Vector3(25500, 18500, 3950); // x, y, radius
        /// kdInCity: inside Valcrest's sixteen-sided outer wall
        public static bool InCity(float x, float y)
        {
            const float RO = 2750; float AO = RO * Mathf.Cos(Mathf.PI / 16), dx = x - KINGDOM.x, dy = y - KINGDOM.y, r = Mathf.Sqrt(dx * dx + dy * dy);
            float s = Mathf.PI * 2 / 16, a = Mathf.Atan2(dy, dx), k = Mathf.Round(a / s); return r < RO + 30 && r * Mathf.Cos(a - k * s) < AO + 30;
        }

        public static int W, H;
        static byte[] liquid, zone, blocked, road;
        static float[] height;
        public static bool Loaded { get { return height != null; } }

        /// in a dungeon (Dun): the map's own pixel space, flat, shown DOX units west of the world
        public static bool Dun; public const float DOX = -2400f;
        public static float OX { get { return Dun ? DOX : 0; } }
        public static Vector3 ToUnity(float x, float y) { return new Vector3(x / PX + OX, GroundY(x, y), (YTOP - y) / PX); }
        public static Vector3 ToUnityFlat(float x, float y) { return new Vector3(x / PX + OX, 0, (YTOP - y) / PX); }
        public static Vector2 ToPx(Vector3 p) { return new Vector2((p.x - OX) * PX, YTOP - p.z * PX); }

        public static void Load()
        {
            if (Loaded) return;
            var g = Resources.Load<TextAsset>("World/grid");
            var meta = JsonUtility.FromJson<GridMeta>(g.text); W = meta.W; H = meta.H;
            liquid = Resources.Load<TextAsset>("World/liquid").bytes;
            zone = Resources.Load<TextAsset>("World/zone").bytes;
            blocked = Resources.Load<TextAsset>("World/blocked").bytes;
            road = Resources.Load<TextAsset>("World/road").bytes;
            var hb = Resources.Load<TextAsset>("World/height").bytes;
            height = new float[W * H]; System.Buffer.BlockCopy(hb, 0, height, 0, hb.Length);
        }
        [System.Serializable] class GridMeta { public int W, H, cell; }

        static int Idx(float x, float y)
        {
            int i = Mathf.FloorToInt(x / CELL + 0.5f), j = Mathf.FloorToInt(y / CELL + 0.5f);
            if (i < 0 || j < 0 || i >= W || j >= H) return -1; return j * W + i;
        }
        public static float HeightPx(float x, float y)
        {
            if (!Loaded || Dun) return 0;
            float fx = x / CELL, fy = y / CELL; int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fy);
            float u = fx - i, v = fy - j;
            float a = Hc(i, j), b = Hc(i + 1, j), c = Hc(i, j + 1), d = Hc(i + 1, j + 1);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }
        static float Hc(int i, int j) { if (i < 0 || j < 0 || i >= W || j >= H) return 0; return height[j * W + i]; }
        public static float GroundY(float x, float y) { return HeightPx(x, y) / PX * HS; }
        /// 0 land, 1 lake water, 2 lava, 3 sea
        public static int LiquidAt(float x, float y) { if (Dun) return 0; int k = Idx(x, y); return k < 0 ? 3 : liquid[k]; }
        public static int ZoneAt(float x, float y) { int k = Idx(x, y); return k < 0 ? 0 : zone[k]; }
        public static bool BlockedAt(float x, float y)
        {
            if (Dun) return AldaraDungeon.Active && AldaraDungeon.Blocked(x, y);
            if (x < 20 || y < 20 || x > WORLD_W - 20 || y > WORLD_H - 20) return true;
            int k = Idx(x, y); return k < 0 || blocked[k] != 0;
        }
        public static float RoadDist(float x, float y) { if (Dun) return 255; int k = Idx(x, y); return k < 0 ? 255 : road[k]; }
        public static string ZoneName(float x, float y)
        {
            if (Dun && AldaraDungeon.def != null) return AldaraDungeon.def.name;
            if (Vector2.Distance(new Vector2(x, y), CENTER) < 1020) return "Lorenmar";
            if (Vector2.Distance(new Vector2(x, y), new Vector2(KINGDOM.x, KINGDOM.y)) < 2760) return "Valcrest";
            return ZoneNames[ZoneAt(x, y)];
        }
    }
}
