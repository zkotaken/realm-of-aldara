using System.Collections.Generic;
using UnityEngine;

namespace Aldara
{
    // The browser's seeded random generator (mulberry32), bit for bit, so camps roll the same monsters and levels.
    public class AldaraRng
    {
        uint s;
        public AldaraRng(int seed) { s = unchecked((uint)seed); }
        public float Next()
        {
            unchecked
            {
                s = s + 0x6D2B79F5u;
                uint t = s;
                t = (uint)((int)(t ^ (t >> 15)) * (int)(1u | s));
                t = (t + (uint)((int)(t ^ (t >> 7)) * (int)(61u | t))) ^ t;
                return (float)(((t ^ (t >> 14))) / 4294967296.0);
            }
        }
    }

    [System.Serializable] public class MoveDef
    {
        public string id, pose, shape, move, look, el, hz; public float[] hits;
        public float wind, act, rec, reach, arc, rad, fwd, wid, len, dmg, dist, spd, min, max, cd, w, slow, kb, buff, n, spread, T, H, delay, tick, turn, lift, @lock, gap, sf, shake, pool, tele, cast, chan, mag, back; public int i;
        public bool Has(float v) { return v >= 0; }
    }
    [System.Serializable] public class MonDef
    {
        public string name, id, color, kind; public int boss, tier, lv0, lv1, winged, fly, flyer, floater, kite;
        public float r, hpBase, atk, xp, gold, spd, unitW, top; public MoveDef[] moves;
    }
    [System.Serializable] public class CampDef { public float x, y, r; public int z; public string type; }
    [System.Serializable] public class LairDef { public float x, y, r; public string name; }
    [System.Serializable] public class MonsterBook
    {
        public MonDef[] monsters; public CampDef[] camps; public LairDef[] lairs; public float baseSpeed, speedMult, bossSpeedMult; public float[] aggro;
        Dictionary<string, MonDef> byName;
        public MonDef Get(string n) { if (byName == null) { byName = new Dictionary<string, MonDef>(); foreach (var m in monsters) byName[m.name] = m; } MonDef d; return byName.TryGetValue(n, out d) ? d : null; }
        static MonsterBook book;
        public static MonsterBook Load() { if (book == null) book = JsonUtility.FromJson<MonsterBook>(Resources.Load<TextAsset>("monsters").text); return book; }
    }

    // ---- the browser's level and damage curves ----
    public static class AldaraRules
    {
        static readonly int[,] XPK = { {1,14},{3,24},{8,60},{12,90},{15,180},{20,260},{25,300},{30,500},{35,560},{40,780},{45,850},{50,1150},{55,1300},{60,1400},{65,1480},{70,1560},{75,1650},{80,2150},{85,2400},{90,3200},{95,3500},{100,5000},{120,6500} };
        public static float XpPerKillAt(int L)
        {
            if (L <= XPK[0, 0]) return XPK[0, 1];
            for (int i = 1; i < XPK.GetLength(0); i++) { int a = XPK[i - 1, 0], va = XPK[i - 1, 1], b = XPK[i, 0], vb = XPK[i, 1]; if (L <= b) return va + (vb - va) * (L - a) / (float)(b - a); }
            return XPK[XPK.GetLength(0) - 1, 1] * (1 + (L - 120) * 0.02f);
        }
        public static int XpNeedFor(int L) { return Mathf.Max(80, Mathf.RoundToInt((6 + L * 2.2f) * XpPerKillAt(L))); }
        public static float LvOut(int g) { return g > 0 ? 1f / (1 + 0.075f * g + 0.0035f * g * g) : Mathf.Min(1.25f, 1 - 0.015f * g); }
        public static float LvIn(int g) { return g > 0 ? 1 + 0.1f * g + 0.005f * g * g : Mathf.Max(0.6f, 1 + 0.025f * g); }
        public static float LvMiss(int g) { return g > 5 ? Mathf.Min(0.6f, (g - 5) * 0.035f) : 0; }
        public static float AngD(float a, float b) { return Mathf.Atan2(Mathf.Sin(a - b), Mathf.Cos(a - b)); }
        public static Color Hex(string h) { Color c; return ColorUtility.TryParseHtmlString(h, out c) ? c : Color.white; }
    }
}
