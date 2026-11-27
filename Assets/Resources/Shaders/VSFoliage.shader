// Leaf cards and vines: alpha-tested, two-sided, wind-driven, with soft back-light translucency.
Shader "VoidStunts/Foliage"
{
    Properties
    {
        _BaseMap ("Leaves (RGBA)", 2D) = "white" {}
        _NormalMap ("Leaf normal", 2D) = "bump" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Float) = 0.45
        _Wind ("Wind strength", Float) = 1
        _Translucency ("Translucency", Float) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; float4 _BaseColor; float _Cutoff, _Wind, _Translucency;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        float3 WindOffset(float3 wp, float flex)
        {
            float t = _Time.y, ph = dot(wp.xz, float2(0.11, 0.07));
            float gust = 0.6 + 0.4 * sin(t * 0.37 + wp.x * 0.013);
            return float3(sin(t * 1.3 + ph) * 0.12 + sin(t * 3.1 + ph * 3.0) * 0.04, sin(t * 2.3 + ph * 2.0) * 0.03, cos(t * 1.1 + ph) * 0.1) * flex * _Wind * gust;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 tangentWS : TEXCOORD2; float2 uv : TEXCOORD3; float4 color : TEXCOORD4; float fog : TEXCOORD5; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                wp += WindOffset(wp, v.color.a);
                o.positionWS = wp; o.positionCS = TransformWorldToHClip(wp);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.normalWS = n.normalWS; o.tangentWS = float4(n.tangentWS, v.tangentOS.w); o.uv = v.uv; o.color = v.color; o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);
                float s = IS_FRONT_VFACE(face, 1, -1);
                float3 nWS = normalize(i.normalWS) * s, t = normalize(i.tangentWS.xyz), b = cross(nWS, t) * i.tangentWS.w;
                float3 nTS = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv).rgb * 2 - 1;
                float3 normalWS = normalize(TransformTangentToWorld(nTS, half3x3(t, b, nWS)));
                float3 albedo = tex.rgb * _BaseColor.rgb * i.color.rgb;
                InputData id = (InputData)0;
                id.positionWS = i.positionWS; id.normalWS = normalWS; id.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                id.shadowCoord = TransformWorldToShadowCoord(i.positionWS); id.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1), i.fog);
                id.bakedGI = SampleSH(normalWS); id.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS); id.shadowMask = 1;
                SurfaceData sd = (SurfaceData)0; sd.albedo = albedo; sd.smoothness = 0.32; sd.occlusion = lerp(0.55, 1, i.color.a); sd.alpha = 1; sd.normalTS = nTS;
                half4 c = UniversalFragmentPBR(id, sd);
                // light through the leaves when the sun is behind them
                Light L = GetMainLight(id.shadowCoord);
                float back = saturate(dot(-normalWS, L.direction)) * saturate(dot(id.viewDirectionWS, -L.direction) * 0.5 + 0.5);
                c.rgb += albedo * L.color * back * L.shadowAttenuation * _Translucency;
                c.rgb = MixFog(c.rgb, id.fogCoord);
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex sv
            #pragma fragment sf
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V sv(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v); V o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz); wp += WindOffset(wp, v.color.a);
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(wp, TransformObjectToWorldNormal(v.normalOS), _LightDirection)));
                o.uv = v.uv; return o;
            }
            half4 sf(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex dv
            #pragma fragment df
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V dv(A v) { UNITY_SETUP_INSTANCE_ID(v); V o; float3 wp = TransformObjectToWorld(v.positionOS.xyz) + WindOffset(TransformObjectToWorld(v.positionOS.xyz), v.color.a); o.positionCS = TransformWorldToHClip(wp); o.uv = v.uv; return o; }
            half df(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff); return 0; }
            ENDHLSL
        }
    }
}
