Shader "PCShop/Old Quarter Garden Strokes"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Vertex { float4 pos:POSITION; half4 pigment:COLOR; };
            struct Stroke { float4 pos:SV_POSITION; float3 world:TEXCOORD0; half3 color:COLOR; half fog:TEXCOORD1; };
            Stroke Vert(Vertex a)
            {
                Stroke o;
                VertexPositionInputs p=GetVertexPositionInputs(a.pos.xyz);
                o.pos=p.positionCS; o.world=p.positionWS; o.color=SRGBToLinear(a.pigment.rgb);
                o.fog=ComputeFogFactor(o.pos.z); return o;
            }
            half4 Frag(Stroke i):SV_Target
            {
                Light day=GetMainLight(TransformWorldToShadowCoord(i.world));
                half power=max(day.color.r,max(day.color.g,day.color.b));
                half3 rgb=i.color*clamp(.18+.80*power,.18,1.17)*lerp(.62,1,day.shadowAttenuation);
                return half4(MixFog(rgb,i.fog),1);
            }
            ENDHLSL
        }
    }
}
