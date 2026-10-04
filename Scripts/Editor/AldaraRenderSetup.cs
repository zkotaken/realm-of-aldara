using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// One-time setup of the render pipeline for the enhanced rendering: the camera depth texture (water shores, depth of
// field), colour grading in the game's own (gamma) range, soft cascaded shadows at a higher resolution, more local lights per object,
// and screen-space ambient occlusion as a renderer feature (switched on and off at runtime by AldaraGraphics).
public static class AldaraRenderSetup
{
    [MenuItem("Aldara/Setup Enhanced Rendering")]
    public static string Run()
    {
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Aldara/Settings/Aldara_URP.asset");
        var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Aldara/Settings/Aldara_Renderer.asset");
        if (!rp || !rd) return "pipeline assets not found";
        rp.supportsCameraDepthTexture = true; rp.supportsHDR = true; rp.colorGradingMode = ColorGradingMode.LowDynamicRange;   // the game is authored in gamma space: grade it as it is rp.colorGradingLutSize = 32;
        { var rso = new SerializedObject(rp); var ss = rso.FindProperty("m_SoftShadowsSupported"); if (ss != null) ss.boolValue = true; var sq = rso.FindProperty("m_SoftShadowQuality"); if (sq != null) sq.intValue = 3; rso.ApplyModifiedPropertiesWithoutUndo(); } rp.mainLightShadowmapResolution = 4096; rp.shadowCascadeCount = 2; rp.maxAdditionalLightsCount = 8;
        rd.copyDepthMode = CopyDepthMode.AfterOpaques;
        var ao = rd.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
        if (!ao)
        {
            ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>(); ao.name = "AldaraSSAO";
            AssetDatabase.AddObjectToAsset(ao, rd); rd.rendererFeatures.Add(ao);
            var m = typeof(ScriptableRendererData).GetMethod("ValidateRendererFeatures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); if (m != null) m.Invoke(rd, null);
        }
        // the settings object is private: set it through serialization
        var so = new SerializedObject(ao); var st = so.FindProperty("m_Settings");
        if (st != null)
        {
            void F(string n, float v) { var p = st.FindPropertyRelative(n); if (p != null) p.floatValue = v; }
            void I(string n, int v) { var p = st.FindPropertyRelative(n); if (p != null) p.intValue = v; }
            void B(string n, bool v) { var p = st.FindPropertyRelative(n); if (p != null) p.boolValue = v; }
            F("Intensity", 1.6f); F("Radius", 1.2f); F("DirectLightingStrength", 0.3f); F("Falloff", 1000f);   // the camera stands ~85 units off the ground
            I("Source", 0); I("NormalSamples", 1); B("Downsample", false); B("AfterOpaque", false); I("AOMethod", 0); I("Samples", 1); I("BlurQuality", 0);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        rd.SetDirty(); EditorUtility.SetDirty(rd); EditorUtility.SetDirty(rp); EditorUtility.SetDirty(ao); AssetDatabase.SaveAssets();
        return "ok features=" + string.Join(",", rd.rendererFeatures.Select(f => f ? f.GetType().Name : "null"));
    }
}
