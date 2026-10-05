Shader "Aldara/KnightFx"
{
    // The knight's enhanced effects (AldaraKnightFx): sword trails, shockwaves, glowing cracks, holy sigils, flares,
    // light shafts, a falling sword of light, a hex ward and flame auras, all added to the light.
    // The atlas is 4 x 4 cells of 256 px: alpha the shape and glow, red a white-hot core.
    // uv0: x, y in the cell (wrapped or clamped), z the cell, w how far the core whitens the colour.
    // Drawn twice: a solid body (alpha blended) under an added glow.
    // uv2.x: how far it is pulled toward the camera along its true view ray (_Pull), so only its depth changes.
    // uv1: x brightness, y fresnel (>0 bright rims, <0 bright centre), z scroll down the cell, w 1 to wrap.
    Properties { _MainTex("Atlas", 2D) = "white" {} [Enum(UnityEngine.Rendering.BlendMode)] _Src("Src", Float) = 1 [Enum(UnityEngine.Rendering.BlendMode)] _Dst("Dst", Float) = 1 _Body("Body alpha", Float) = 0.5 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Off Blend [_Src] [_Dst]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float _Src, _Body; float4 _Pull;
            struct A { float4 pos : POSITION; float3 n : NORMAL; float4 col : COLOR; float4 uv : TEXCOORD0; float4 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float4 uv : TEXCOORD0; float4 uv1 : TEXCOORD1; float3 n : TEXCOORD2; float3 wp : TEXCOORD3; };
            V vert(A i)
            {
                V o; o.wp = i.pos.xyz; float3 pw = i.pos.xyz + _Pull.xyz * i.uv2.x; o.pos = TransformWorldToHClip(pw); /* pulled toward the camera so grass and ground do not bury it */ o.col = i.col; o.uv = i.uv; o.uv1 = i.uv1; o.n = i.n; return o;
            }
            half4 frag(V i) : SV_Target
            {
                float cell = round(i.uv.z); float2 cxy = float2(fmod(cell, 4), floor(cell / 4));
                float2 lu = i.uv.xy + float2(0, i.uv1.z);
                float2 f = i.uv1.w > 0.5 ? frac(lu) : saturate(lu);
                const float pad = 2.0 / 256.0, sc = (1 - 2 * pad) / 4;
                float2 auv = cxy * 0.25 + pad * 0.25 + f * sc;
                float4 t = SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, auv, ddx(lu) * sc, ddy(lu) * sc);
                float a = t.a * i.col.a;
                if (abs(i.uv1.y) > 0.001)
                {
                    float3 v = GetWorldSpaceNormalizeViewDir(i.wp); float fr = 1 - abs(dot(normalize(i.n), v));
                    float rim = i.uv1.y > 0 ? 0.12 + 1.7 * fr * fr : pow(saturate(1 - fr), 1.6);
                    a *= lerp(1, rim, abs(i.uv1.y));
                }
                float3 c = lerp(i.col.rgb, float3(1, 1, 1), saturate(t.r * i.uv.w));
                if (_Src > 4.5) return half4(c, saturate(a * _Body * min(1.4, i.uv1.x)));   // the solid body under the glow, so it reads in daylight
                return half4(c * a * i.uv1.x, 0);
            }
            ENDHLSL
        }
    }
}
