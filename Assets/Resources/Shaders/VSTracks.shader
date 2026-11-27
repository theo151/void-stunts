// Tyre tracks: tread ribbons printed by every wheel, fading over ~45 s. Ported from v0.9 (the part you loved).
// Vertex data: uv.x across the tread (0..1), uv.y metres along; uv2 = (birth time, strength); colour unused.
Shader "VoidStunts/Tracks"
{
    Properties { _Fade ("Fade start, end (s)", Vector) = (28, 45, 0, 0) _Ink ("Ink", Color) = (0.055, 0.055, 0.06, 1) }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+50" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Offset -2, -6
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _Fade; float4 _Ink; CBUFFER_END
            float _VS_Now;
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float a : TEXCOORD1; float fog : TEXCOORD2; };
            V vert(A v)
            {
                V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv;
                float age = _VS_Now - v.uv2.x; o.a = v.uv2.y * (1 - smoothstep(_Fade.x, _Fade.y, age)) * step(0, age) * step(0.001, v.uv2.y);
                o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 frag(V i) : SV_Target
            {
                float x = abs(i.uv.x - 0.5) * 2;
                float edge = 1 - smoothstep(0.72, 1, x);
                float blocks = 0.6 + 0.4 * step(0.42, frac(i.uv.y / 0.15 + step(0.5, i.uv.x) * 0.5));
                float groove = 1 - 0.55 * (1 - smoothstep(0.03, 0.07, abs(x - 0.42)));
                float a = i.a * edge * blocks * groove * 0.68;
                clip(a - 0.004);
                return half4(MixFog(_Ink.rgb, i.fog), a);
            }
            ENDHLSL
        }
    }
}
