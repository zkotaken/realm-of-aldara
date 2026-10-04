using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aldara.EditorTools
{
    // Before every build: renderer features the game switches at runtime (ambient occlusion) must be saved switched
    // on, or the build strips their shaders and the player draws a black screen when the setting turns them on.
    public class AldaraBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset; if (rp == null) return;
            foreach (var rd in rp.rendererDataList)
            {
                if (rd == null) continue;
                foreach (var f in rd.rendererFeatures)
                    if (f is ScreenSpaceAmbientOcclusion && !f.isActive) { f.SetActive(true); EditorUtility.SetDirty(f); EditorUtility.SetDirty(rd); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
