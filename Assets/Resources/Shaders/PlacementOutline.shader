Shader "PCShopSell/PlacementOutline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline colour", Color) = (0.18, 1.65, 0.42, 1)
        _OutlineWidth ("Width in pixels", Float) = 1.8
        [HideInInspector] _Cull ("Cull", Float) = 1
        [HideInInspector] _ZTest ("Depth test", Float) = 4
        [HideInInspector] _ColorMask ("Colour mask", Float) = 15
        [HideInInspector] _StencilRef ("Stencil reference", Float) = 1
        [HideInInspector] _StencilComp ("Stencil comparison", Float) = 6
        [HideInInspector] _StencilOp ("Stencil operation", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Name "PlacementEdge"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull [_Cull]
            ZTest [_ZTest]
            ZWrite Off
            ColorMask [_ColorMask]
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref [_StencilRef]
                ReadMask 1
                WriteMask 1
                Comp [_StencilComp]
                Pass [_StencilOp]
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 direction = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy * _ScaledScreenParams.xy;
                direction /= max(length(direction), 0.001);
                output.positionCS.xy += direction * (_OutlineWidth * 2.0 / _ScaledScreenParams.xy) * output.positionCS.w;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
