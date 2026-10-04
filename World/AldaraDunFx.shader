Shader "Aldara/DunFx"
{
    // The dungeon's 2D drawing, as the browser's canvas does it: vertex-coloured shapes (optionally textured), blended
    // normally, added ('lighter') or multiplied (the light map), on the floor or over everything.
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _Src("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst("Dst", Float) = 10
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZT("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_Src] [_Dst]
        ZWrite Off
        ZTest [_ZT]
        Cull Off
        Pass
        {
            Name "DunFx"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct A { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.col = i.col; o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.col; }
            ENDHLSL
        }
    }
}
