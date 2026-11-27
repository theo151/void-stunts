// Smoked and clear glass: lit, transparent, strong fresnel reflection. Car canopy, glass ramps, the screen tower skin.
Shader "VoidStunts/Glass"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (0.9, 0.93, 0.95, 0.2)
        _Smooth ("Smoothness", Float) = 0.95
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _BaseColor; float _Smooth; CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float fog : TEXCOORD2; };
            V vert(A v) { UNITY_SETUP_INSTANCE_ID(v); V o; VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz); o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = TransformObjectToWorldNormal(v.normalOS); o.fog = ComputeFogFactor(p.positionCS.z); return o; }
            half4 frag(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1, -1), vd = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float fres = pow(1 - saturate(dot(n, vd)), 4);
                InputData id = (InputData)0; id.positionWS = i.positionWS; id.normalWS = n; id.viewDirectionWS = vd; id.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                id.bakedGI = SampleSH(n); id.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS); id.shadowMask = 1;
                SurfaceData sd = (SurfaceData)0; sd.albedo = _BaseColor.rgb * 0.2; sd.smoothness = _Smooth; sd.metallic = 0; sd.occlusion = 1; sd.alpha = 1;
                half4 c = UniversalFragmentPBR(id, sd);
                float a = saturate(_BaseColor.a + fres * 0.7);
                c.rgb = MixFog(c.rgb, InitializeInputDataFog(float4(i.positionWS, 1), i.fog));
                return half4(c.rgb * a + _BaseColor.rgb * _BaseColor.a * 0.05, a);
            }
            ENDHLSL
        }
    }
}
