// The VS-02 rocket plume, ported from the frozen v0.8 car: shock diamonds in a hot core, a thin coloured sheath.
Shader "VoidStunts/Plume"
{
    Properties
    {
        _Core ("Core", Vector) = (3, 3, 3, 0)
        _Sheath ("Sheath", Vector) = (1, 1, 1, 0)
        _On ("On", Float) = 0
        _Rad ("Radius", Float) = 0.2
        _Len ("Length", Float) = 2
        _N ("Diamonds", Float) = 0
        _I ("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _Core, _Sheath; float _On, _Rad, _Len, _N, _I; CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float t : TEXCOORD0; float f : TEXCOORD1; };
            V vert(A v)
            {
                V o; float t = v.positionOS.y + 0.5;
                float pinch = _N > 0 ? 1 - 0.22 * pow(abs(sin(t * _N * PI)), 1.5) : 1;
                float r = _Rad * pinch * lerp(1, 0.25, pow(t, 1.3)) * (0.9 + 0.1 * _On);
                float3 p = float3(v.positionOS.x / 0.5 * r, v.positionOS.z / 0.5 * r, -t * _Len * (0.35 + 0.65 * _On));
                float3 vp = TransformWorldToView(TransformObjectToWorld(p));
                float3 nv = normalize(mul((float3x3)UNITY_MATRIX_V, TransformObjectToWorldDir(float3(v.positionOS.x, v.positionOS.z, 0))));
                o.f = abs(dot(nv, normalize(-vp))); o.t = t;
                o.positionCS = TransformObjectToHClip(p);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float fall = pow(saturate(1 - i.t), 1.5);
                float dia = _N > 0 ? 0.45 + 0.55 * pow(abs(cos(i.t * _N * PI)), 7) : 1;
                float fl = 0.9 + 0.1 * sin(_Time.y * 71 + i.t * 23) * sin(_Time.y * 37);
                float edge = pow(saturate(i.f), 1.6);
                float3 c = lerp(_Sheath.rgb, _Core.rgb, edge * (1 - smoothstep(0, 0.75, i.t)));
                float a = saturate(fall * edge * dia * fl * _On * _I);
                return half4(max(c * a, 0), 0);
            }
            ENDHLSL
        }
    }
}
