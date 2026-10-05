using System.Collections;
using UnityEngine;

namespace Aldara
{
    // A release check for the Windows build, used only when the game is started with
    //   -aldaraSmoke <folder> [-aldaraSlot <n>]
    // It waits for the title screen, loads the save slot (default 9, a test copy), plays for a while, saves three
    // screenshots and a short report into the folder, and quits. Players never pass these arguments.
    public class AldaraSmoke : MonoBehaviour
    {
        string dir; int slot = 9; Vector2 field = new Vector2(4400, 5600);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); string d = null; int s = 9;
            for (int i = 0; i < a.Length - 1; i++) { if (a[i] == "-aldaraSmoke") d = a[i + 1]; if (a[i] == "-aldaraSlot") int.TryParse(a[i + 1], out s); }
            if (string.IsNullOrEmpty(d)) return;
            var go = new GameObject("AldaraSmoke"); DontDestroyOnLoad(go); var k = go.AddComponent<AldaraSmoke>(); k.dir = d; k.slot = s;
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-aldaraAt") { var q = a[i + 1].Split(','); float fx, fy; if (q.Length == 2 && float.TryParse(q[0], out fx) && float.TryParse(q[1], out fy)) k.field = new Vector2(fx, fy); }
        }
        void Report(string s) { try { System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "smoke.txt"), System.DateTime.Now.ToString("HH:mm:ss") + " " + s + "\n"); } catch { } }
        IEnumerator Start()
        {
            System.IO.Directory.CreateDirectory(dir); Report("started " + Application.version + " " + Screen.width + "x" + Screen.height + " " + SystemInfo.graphicsDeviceName + " " + SystemInfo.graphicsDeviceType);
            Application.logMessageReceived += (m, st, t) => { if (t == LogType.Error || t == LogType.Exception) Report(t + ": " + m + " | " + (st ?? "").Split('\n')[0]); };
            yield return new WaitForSeconds(6);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "smoke_title.png"));
            yield return new WaitForSeconds(1);
            try { AldaraSave.Load(slot); Report("loaded slot " + slot + " " + (AldaraHero.I ? AldaraHero.I.heroName + " lv " + AldaraHero.I.lvl : "no hero")); } catch (System.Exception e) { Report("load failed: " + e.Message); }
            yield return new WaitForSeconds(12);
            float t = 0; int n = 0; float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < 5) { n++; t += Time.unscaledDeltaTime; yield return null; }
            Report("fps " + (n / 5f).ToString("0.0") + " grass tufts " + AldaraGrass.Drawn + " " + AldaraGrass.Why + " ground detail " + Shader.GetGlobalFloat("_DetailOn"));
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "smoke_town.png"));
            yield return new WaitForSeconds(1);
            if (AldaraPlayer.I) { AldaraPlayer.I.x = field.x; AldaraPlayer.I.y = field.y; }
            yield return new WaitForSeconds(8);
            Report("field grass tufts " + AldaraGrass.Drawn + " " + AldaraGrass.Why);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "smoke_field.png"));
            yield return new WaitForSeconds(2);
            Report("done"); Application.Quit();
        }
    }
}
