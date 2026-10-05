Shader "Aldara/VfxPart"
{
    // The enhanced effects' particles (AldaraVfxPlus): soft glowing sparks, embers, motes and shards added to the
    // light (bright enough to bloom), and smoke and dust laid over it. uv.xy the sprite, uv.z brightness, uv.w softness.
    Properties { _MainTex("Sprite", 2D) = "white" {} [Enum(UnityEngine.Rendering.BlendMode)] _Src("Src", Float) = 1 [Enum(UnityEngine.Rendering.BlendMode)] _Dst("Dst", Float) = 1 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Off Blend [_Src] [_Dst]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float _Src;
            struct A { float4 pos : POSITION; float4 col : COLOR; float4 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float4 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformWorldToHClip(i.pos.xyz); o.col = i.col; o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
                float a = lerp(t.a, t.r, saturate(i.uv.w)) * i.col.a;
                if (_Src > 4.5) return half4(i.col.rgb, a);                 // alpha blended (smoke, dust)
                return half4(i.col.rgb * i.uv.z * a, 0);                     // added light (sparks, embers, motes)
            }
            ENDHLSL
        }
    }
}
