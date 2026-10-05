Shader "Aldara/SwampFog"
{
    // The Shadowfen's mist (AldaraSwampFog): thin around the hero, thickening with distance into a grey-green wall,
    // heaviest low over the ground so trees and roofs rise out of it, drifting in slow banks. Reads the scene's depth
    // to put every pixel back on the ground, so the fog lies in the world rather than on the screen.
    Properties { _Col("Colour", Color) = (0.6, 0.66, 0.56, 1) _Amt("Amount", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../../Materials/AldaraNoise.hlsl"
            float4 _Col; float _Amt; float4 _FogHero; float4 _FogR;   // hero xyz; near radius, far radius, most, ground haze
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); return o; }
            half4 frag(V i) : SV_Target
            {
                float2 uv = i.pos.xy / _ScaledScreenParams.xy;
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool sky = raw <= 1e-6;
                #else
                    bool sky = raw >= 1 - 1e-6; raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
                #endif
                float3 wp = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float2 d2 = wp.xz - _FogHero.xz; float dist = sky ? 1e3 : length(d2);
                float h = sky ? 0 : max(0, wp.y - _FogHero.y);
                float t = _Time.y;
                // drifting banks: two layers moving different ways
                float n1 = AFbm3(float3(wp.xz * 0.11 + float2(t * 0.05, t * 0.02), t * 0.03), 4);
                float n2 = AFbm3(float3(wp.xz * 0.3 - float2(t * 0.08, -t * 0.05), t * 0.06 + 3.7), 3);
                float bank = saturate(0.75 + n1 * 1.6 + n2 * 0.6);
                float far = smoothstep(_FogR.x, _FogR.y, dist);
                float low = exp(-h * 0.55);                    // lies low: the tops of tall things stand out of it
                float f = far * _FogR.z * lerp(0.55, 1, low) * lerp(0.7, 1.15, bank);
                f += _FogR.w * low * saturate(bank - 0.35) * (1 - far * 0.5); // thin wisps over the ground, even close by
                f = saturate(f) * _Amt;
                return half4(_Col.rgb * (0.92 + 0.12 * n2), f);
            }
            ENDHLSL
        }
    }
}
