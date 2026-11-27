// Shock rings on landing and ring pass-through: a flat annulus, colour × alpha, optional additive.
Shader "VoidStunts/Pulse"
{
    Properties { _BaseColor ("Colour", Color) = (1,1,1,1) [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Dst blend", Float) = 10 }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+5" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha [_Dst] ZWrite Off Cull Off Offset -2, -6
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _BaseColor; float _Dst; CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(V i) : SV_Target { return half4(_BaseColor.rgb, _BaseColor.a); }
            ENDHLSL
        }
    }
}
