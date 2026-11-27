// Procedural sky: three-stop gradient, sun disc and halo, slow high cloud, horizon haze. Driven by the atmosphere presets.
Shader "VoidStunts/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.44, 0.5, 0.6, 1)
        _Mid ("Mid", Color) = (0.79, 0.67, 0.63, 1)
        _Hor ("Horizon", Color) = (0.94, 0.79, 0.66, 1)
        _SunColor ("Sun", Color) = (1, 0.85, 0.65, 1)
        _SunDir ("Sun direction", Vector) = (-0.6, 0.3, -0.7, 0)
        _Cloud ("Cloud amount", Float) = 0.5
        _Stars ("Stars", Float) = 0
        _NoiseMap ("Noise", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _Top, _Mid, _Hor, _SunColor, _SunDir; float _Cloud, _Stars; CBUFFER_END
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.dir = v.positionOS.xyz; return o; }
            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir); float y = d.y;
                float3 c = lerp(_Hor.rgb, _Mid.rgb, smoothstep(0.0, 0.2, y));
                c = lerp(c, _Top.rgb, smoothstep(0.18, 0.8, y));
                c = lerp(c, _Hor.rgb * 0.92, smoothstep(0.0, -0.25, y));
                float3 sd = normalize(_SunDir.xyz); float s = saturate(dot(d, sd));
                c += _SunColor.rgb * (pow(s, 6.0) * 0.18 + pow(s, 64.0) * 0.5 + smoothstep(0.9995, 0.99975, s) * 6.0);
                // high cloud: two scrolling noise layers projected on a dome
                if (y > 0.0)
                {
                    float2 uv = d.xz / (y + 0.12) * 0.35;
                    float n = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, uv + _Time.y * float2(0.002, 0.0007), 0).g * 0.65 + SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, uv * 2.7 - _Time.y * 0.0013, 0).b * 0.35;
                    float cl = smoothstep(0.55 - _Cloud * 0.25, 0.85, n) * smoothstep(0.0, 0.25, y) * _Cloud;
                    float3 cc = lerp(_Mid.rgb * 1.12, _SunColor.rgb, pow(s, 3.0) * 0.7) + 0.06;
                    c = lerp(c, cc, cl * 0.75);
                    float st = step(0.997, frac(sin(dot(floor(d.xz / (y + 0.2) * 260.0), float2(12.9898, 78.233))) * 43758.5453));
                    c += st * _Stars * smoothstep(0.1, 0.4, y);
                }
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
