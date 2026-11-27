// The world surface shader. One shader for concrete, asphalt, moss, rock, sand, steel, bark and the car body.
// No visible tiling: hex-tiled detail (VSCommon.hlsl) + world-space macro variation, grime, strata, and procedural facades.
Shader "VoidStunts/Surface"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _NormalMap ("Normal (linear)", 2D) = "bump" {}
        _MaskMap ("Mask R rough G AO B height (linear)", 2D) = "white" {}
        _NoiseMap ("Macro noise (linear)", 2D) = "gray" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Tiling ("Metres per repeat", Float) = 3
        _UVMode ("0 metres+hex · 1 mesh uv", Float) = 0
        _Hex ("Hex tiling", Float) = 1
        _HexContrast ("Hex contrast", Float) = 0.6
        _Macro ("Macro variation", Float) = 1
        _NormalStrength ("Normal strength", Float) = 1
        _Rough ("Roughness scale", Float) = 1
        _Metal ("Metallic", Float) = 0
        _Grime ("Grime", Float) = 0
        _Facade ("Procedural facade", Float) = 0
        _Strata ("Rock strata", Float) = 0
        _SlopeBlend ("Slope blend (terrain)", Float) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; float4 _BaseColor; float4 _EmissionColor;
            float _Tiling, _UVMode, _Hex, _HexContrast, _Macro, _NormalStrength, _Rough, _Metal, _Grime, _Facade, _Strata, _SlopeBlend, _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "VSCommon.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            float _VS_WindowGlow; float4 _VS_WindowTint;
            TEXTURE2D(_VS_Rock2); TEXTURE2D(_VS_Rock2N); TEXTURE2D(_VS_Rock2M);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float4 uv2 : TEXCOORD2; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 tangentWS : TEXCOORD2;
                float2 uv0 : TEXCOORD3; float2 uv1 : TEXCOORD4; float4 params : TEXCOORD5; float4 color : TEXCOORD6; float fog : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv0 = v.uv0; o.uv1 = v.uv1; o.params = v.uv2; o.color = v.color; o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // procedural window grid on a facade: u along the wall, v height (metres); params = bay, storey, seed, style
            void Facade(float2 fu, float4 prm, float2 fw, inout float3 albedo, inout float rough, inout float metal, inout float3 emission, inout float ao, float3 nWS)
            {
                if (abs(nWS.y) > 0.5 || prm.x <= 0) return;
                float bay = prm.x, storey = prm.y, seed = prm.z, style = prm.w;
                float2 cell = floor(float2(fu.x / bay, fu.y / storey));
                float2 f = float2(frac(fu.x / bay), frac(fu.y / storey));
                float h = VSHash12(cell + seed * 17.13), h2 = VSHash12(cell.yx + seed * 3.7 + 11.0);
                float win, frame;
                if (cell.y < 1)            // ground floor: tall shop glass with a fascia band
                {
                    win = VSBox(f, float2(0.05, 0.08), float2(0.95, 0.72), fw);
                    frame = VSBox(f, float2(0.03, 0.05), float2(0.97, 0.75), fw) - win;
                    float fascia = VSBox(f, float2(0.0, 0.8), float2(1.0, 0.95), fw);
                    albedo = lerp(albedo, albedo * 0.25, fascia);
                }
                else if (style < 0.5) { win = VSBox(f, float2(0.2, 0.3), float2(0.8, 0.86), fw); frame = VSBox(f, float2(0.17, 0.26), float2(0.83, 0.89), fw) - win; }
                else if (style < 1.5) { win = VSBox(f, float2(0.0, 0.34), float2(1.0, 0.84), fw); frame = VSBox(f, float2(0.0, 0.3), float2(1.0, 0.86), fw) - win; }
                else if (style < 2.5) { win = VSBox(f, float2(0.04, 0.06), float2(0.96, 0.94), fw); frame = VSBox(f, float2(0.0, 0.0), float2(1.0, 1.0), fw) - win; }
                else                  { win = VSBox(f, float2(0.42, 0.2), float2(0.58, 0.9), fw); frame = VSBox(f, float2(0.39, 0.16), float2(0.61, 0.93), fw) - win; }
                // glass: deep, reflective; blinds at a random height; a few warm lit rooms
                float blind = step(f.y, 0.86 - h2 * 0.5) * step(0.55, h2);
                float3 glass = lerp(float3(0.035, 0.045, 0.055), float3(0.42, 0.40, 0.36), blind * 0.7);
                float lit = step(h, cell.y < 1 ? 0.75 : 0.22);
                albedo = lerp(albedo, glass, win);
                albedo = lerp(albedo, albedo * 0.45, saturate(frame));
                rough = lerp(rough, lerp(0.06, 0.6, blind), win);
                metal = lerp(metal, 0.15, win);
                emission += win * lit * _VS_WindowTint.rgb * _VS_WindowGlow * (0.55 + 0.45 * h2) * (cell.y < 1 ? 1.6 : 1.0);
                // the wall darkens just under each sill (water and shadow)
                float sill = VSBox(f, float2(0.15, 0.05), float2(0.85, 0.28), fw) * step(0.5, cell.y);
                ao *= 1 - sill * 0.25 * (1 - win);
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 nWS = normalize(i.normalWS) * (IS_FRONT_VFACE(face, 1, -1));
                float2 uv = _UVMode > 0.5 ? i.uv0 : i.uv0 / _Tiling;
                float3 albedo, nTS, msk;
                if (_Hex > 0.5)
                {
                    VSHexSample s = VSHexTile(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap), TEXTURE2D_ARGS(_MaskMap, sampler_MaskMap), uv, _HexContrast);
                    albedo = s.albedo; nTS = s.normalTS; msk = s.mask;
                }
                else
                {
                    albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                    nTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv)); // platform-correct decode (BC5/DXT5nm on desktop, RGB on mobile)
                    msk = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv).rgb;
                }
                // terrain: steep faces become rock (sampled at a second scale), flats keep the base layer
                if (_SlopeBlend > 0.5)
                {
                    float steep = smoothstep(0.62, 0.78, 1 - nWS.y);
                    {
                        VSHexSample r = VSHexTile(TEXTURE2D_ARGS(_VS_Rock2, sampler_BaseMap), TEXTURE2D_ARGS(_VS_Rock2N, sampler_NormalMap), TEXTURE2D_ARGS(_VS_Rock2M, sampler_MaskMap), i.uv0 / (_Tiling * 2.3), _HexContrast);
                        albedo = lerp(albedo, r.albedo, steep); nTS = normalize(lerp(nTS, r.normalTS, steep)); msk = lerp(msk, r.mask, steep);
                    }
                }
                nTS.xy *= _NormalStrength;
                float3 wp = i.positionWS;
                // world-space macro variation: two noise scales break any residual repetition across hundreds of metres
                float4 n1 = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, wp.xz * (1.0 / 160.0) + wp.y * 0.002);
                float4 n2 = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, float2(wp.x + wp.z * 0.31, wp.y + wp.z * 0.67) * (1.0 / 37.0));
                float macro = (n1.r - 0.5) * 0.8 + (n2.g - 0.5) * 0.5 + (n1.b - 0.5) * 0.3;
                albedo *= 1 + macro * 0.16 * _Macro;
                albedo *= lerp(1, float3(1.03, 1.0, 0.96), (n1.g - 0.5) * 1.2 * _Macro);
                float rough = saturate(msk.r * _Rough * (1 + (n2.b - 0.5) * 0.35 * _Macro));
                float ao = msk.g * i.color.a;
                float metal = _Metal;
                albedo *= _BaseColor.rgb * i.color.rgb;
                // rock strata from world height, warped
                if (_Strata > 0.5)
                {
                    float y = wp.y + (n2.r - 0.5) * 6 + (n1.a - 0.5) * 14;
                    float band = frac(y / 9.0), band2 = frac(y / 2.7);
                    float3 s1 = float3(0.78, 0.55, 0.38), s2 = float3(0.62, 0.42, 0.3), s3 = float3(0.86, 0.72, 0.56);
                    float3 strata = lerp(lerp(s1, s2, smoothstep(0.3, 0.36, band)), s3, smoothstep(0.7, 0.74, band) * (1 - smoothstep(0.86, 0.9, band)));
                    strata *= 0.9 + 0.2 * smoothstep(0.4, 0.6, band2);
                    albedo *= strata * 1.5 * lerp(1, 0.8, step(0.5, nWS.y));
                }
                // grime: rain streaks down walls, darker near the ground
                if (_Grime > 0)
                {
                    float wall = 1 - abs(nWS.y);
                    float streak = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, float2((wp.x + wp.z) * 0.21, wp.y * 0.018)).a;
                    float base = 1 - smoothstep(0.0, 2.2, wp.y - (n1.r * 0.6));
                    float g = saturate((smoothstep(0.55, 0.85, streak) * 0.5 + base * 0.45) * wall * _Grime);
                    albedo *= 1 - g * 0.45; rough = lerp(rough, 0.95, g * 0.5); ao *= 1 - base * wall * 0.25;
                }
                float3 emission = _EmissionColor.rgb;
                float2 facW = fwidth(i.uv1 / max(i.params.xy, 0.01)) * 1.2; // taken before any per-pixel branch
                if (_Facade > 0.5) Facade(i.uv1, i.params, facW, albedo, rough, metal, emission, ao, nWS);

                float sgn = i.tangentWS.w;
                float3 t = normalize(i.tangentWS.xyz), b = sgn * cross(nWS, t);
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(nTS, half3x3(t, b, nWS)));

                InputData id = (InputData)0;
                id.positionWS = wp; id.normalWS = normalWS; id.viewDirectionWS = GetWorldSpaceNormalizeViewDir(wp);
                id.shadowCoord = TransformWorldToShadowCoord(wp);
                id.fogCoord = InitializeInputDataFog(float4(wp, 1), i.fog);
                id.bakedGI = SampleSH(normalWS);
                id.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                id.shadowMask = half4(1, 1, 1, 1);
                SurfaceData sd = (SurfaceData)0;
                sd.albedo = saturate(albedo); sd.metallic = metal; sd.smoothness = 1 - rough; sd.normalTS = nTS;
                sd.occlusion = ao; sd.emission = emission; sd.alpha = 1;
                half4 c = UniversalFragmentPBR(id, sd);
                c.rgb = MixFog(c.rgb, id.fogCoord);
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex sv
            #pragma fragment sf
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 sv(A v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.positionOS.xyz), n = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 L = normalize(_LightPosition - p);
                #else
                    float3 L = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p, n, L)));
            }
            half4 sf() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex dv
            #pragma fragment df
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 dv(A v) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(v); return TransformObjectToHClip(v.positionOS.xyz); }
            half df() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex nv
            #pragma fragment nf
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V nv(A v) { UNITY_SETUP_INSTANCE_ID(v); V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 nf(V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
