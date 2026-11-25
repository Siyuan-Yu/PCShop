Shader "PCShop/Old town grass verge"
{
    Properties
    {
        [MainTexture] _BaseMap("Meadow pigment", 2D) = "white" {}
        _BaseColor("Grass tint", Color) = (.61,.65,.45,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half cover:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.world=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world); o.cover=v.color.a; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=half3(0,1,0);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 light=SampleSH(n)+sun.color*saturate(sun.direction.y)*sun.shadowAttenuation;
                half3 pigment=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.world.xz/5.5).rgb;
                half3 broad=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.world.zx/14.7).rgb;
                return half4(lerp(pigment,broad,.34h)*_BaseColor.rgb*light,i.cover);
            }
            ENDHLSL
        }
    }
}