using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Aldara
{
    // Distance fog in the swamps (Graphics, Atmosphere): in The Shadowfen a grey-green mist closes in beyond a little
    // way round the hero, drifting in banks and lying low over the water and reeds, darker and bluer by night. It fades
    // in and out across the region's border and is not drawn in towns or dungeons.
    public class AldaraSwampFog : MonoBehaviour
    {
        const int SWAMP = 2;   // AldaraWorld.ZoneNames: The Shadowfen
        Material mat; Mesh quad; float amt; float zoneT;
        void Update()
        {
            var cam = Camera.main; var P = AldaraPlayer.I;
            if (!cam || !P || !AldaraWorld.Loaded || !AldaraSave.Ready) return;
            float want = 0;
            if (!AldaraWorld.Dun && AldaraSettings.On("atmos") && AldaraWorld.ZoneName(P.x, P.y) != "Lorenmar" && AldaraWorld.ZoneName(P.x, P.y) != "Valcrest")
            {
                zoneT -= Time.deltaTime; if (zoneT <= 0) { zoneT = 0.25f; int z2; float e; int z = AldaraPost.ZoneCalc(P.x, P.y, out z2, out e); float s = Mathf.SmoothStep(0, 1, Mathf.Clamp01(e / 700f)); target = z == SWAMP ? 0.5f + 0.5f * s : (z2 == SWAMP ? 0.5f - 0.5f * s : 0); }
                want = target;
            }
            amt = Mathf.MoveTowards(amt, want, Time.deltaTime * 0.35f);
            if (amt <= 0.001f) return;
            if (!mat) { var sh = Resources.Load<Shader>("Shaders/AldaraSwampFog"); if (!sh) return; mat = new Material(sh) { renderQueue = 3290 }; }
            if (!quad) { quad = new Mesh { name = "FogQuad" }; quad.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) }; quad.triangles = new[] { 0, 2, 1, 0, 3, 2 }; quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f); }
            var cd = cam.GetUniversalAdditionalCameraData(); if (cd && !cd.requiresDepthTexture) cd.requiresDepthTexture = true;
            float night = AldaraPost.Night;
            var col = Color.Lerp(new Color(0.58f, 0.65f, 0.55f), new Color(0.15f, 0.19f, 0.21f), night);
            mat.SetColor("_Col", col); mat.SetFloat("_Amt", amt);
            var hero = AldaraWorld.ToUnity(P.x, P.y); hero.y = AldaraWorld.GroundY(P.x, P.y);
            mat.SetVector("_FogHero", hero);
            mat.SetVector("_FogR", new Vector4(4.5f, 18f, 0.78f, 0.14f));
            // a quad just in front of the camera that fills the view
            float hh = cam.orthographicSize * 1.1f, hw = hh * cam.aspect;
            var m = Matrix4x4.TRS(cam.transform.position + cam.transform.forward * (cam.nearClipPlane + 1f), cam.transform.rotation, new Vector3(hw, hh, 1));
            Graphics.DrawMesh(quad, m, mat, 0, cam);
        }
        float target;
    }
}
