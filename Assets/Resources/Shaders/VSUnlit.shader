// Light sources that ARE light: LED strips, neon, lanterns, dot-matrix signs, the screen. HDR colour drives bloom.
Shader "VoidStunts/Emissive"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 3
        _FarDim ("Dim with distance", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST; float4 _BaseColor; float _Intensity, _FarDim; CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : TEXCOORD1; float fog : TEXCOORD2; float dist : TEXCOORD3; };
            V vert(A v) { UNITY_SETUP_INSTANCE_ID(v); V o; VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz); o.positionCS = p.positionCS; o.uv = TRANSFORM_TEX(v.uv, _BaseMap); o.color = v.color; o.fog = ComputeFogFactor(p.positionCS.z); o.dist = length(p.positionVS); return o; }
            half4 frag(V i) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb * i.color.rgb * _Intensity;
                c *= lerp(1.0, 0.35, smoothstep(40.0, 400.0, i.dist) * _FarDim); // thin far lights must not sparkle
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex dv
            #pragma fragment df
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 dv(A v) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(v); return TransformObjectToHClip(v.positionOS.xyz); }
            half df() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
