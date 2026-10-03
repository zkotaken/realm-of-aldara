Shader "Aldara/Water"
{
    Properties { _Glint("Glint", Float) = 0.6 _Speed("Speed", Float) = 0.4 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float _Glint, _Speed;
            struct A { float4 pos : POSITION; float4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float3 wp : TEXCOORD0; };
            V vert(A i){ V o; float3 wp = TransformObjectToWorld(i.pos.xyz);
                float lava = step(0.9, i.col.r) * step(i.col.b, 0.3);
                wp.y += sin(wp.x * 0.7 + _Time.y * 1.3) * 0.04 + cos(wp.z * 0.6 + _Time.y) * 0.04;
                o.wp = wp; o.pos = TransformWorldToHClip(wp); o.col = i.col; return o; }
            float h21(float2 p){ return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vn(float2 p){ float2 i = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(h21(i), h21(i+float2(1,0)), f.x), lerp(h21(i+float2(0,1)), h21(i+float2(1,1)), f.x), f.y); }
            half4 frag(V i) : SV_Target {
                float t = _Time.y * _Speed;
                float n = vn(i.wp.xz * 0.9 + float2(t, t*0.7)) * 0.6 + vn(i.wp.xz * 2.3 - float2(t*1.3, t)) * 0.4;
                float lava = step(0.9, i.col.r) * step(i.col.b, 0.3);
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float fres = pow(1 - saturate(V.y), 3);
                Light L = GetMainLight();
                float3 c = i.col.rgb * (0.75 + 0.35 * n) * lerp(0.6 + 0.5 * L.color, 1, lava);
                float gl = smoothstep(0.78, 0.95, n) * _Glint * (1 - lava);
                c += gl * L.color + fres * 0.25 * (1 - lava);
                c += lava * (n * n) * float3(1.2, 0.5, 0.1);
                return half4(c, saturate(i.col.a + fres * 0.2 + gl * 0.3));
            }
            ENDHLSL
        }
    }
}