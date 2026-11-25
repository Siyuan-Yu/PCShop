Shader "PCShop/Old Town Street Paint"
{
    Properties
    {
        [MainTexture] _BaseMap("Painted albedo", 2D) = "white" {}
        _Pigment("Wash tint", Color) = (1,1,1,1)
        _PaperFill("Diffuse fill", Range(0,1)) = 0.52
        _Cutoff("Leaf mask", Range(0,1)) = 0.0
        [HideInInspector] _BaseColor("Shadow alpha", Color) = (1,1,1,1)
        [HideInInspector] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _Pigment;
            half _PaperFill;
            half _Cutoff;
        CBUFFER_END
        struct DepthInput
        {
            float4 positionOS : POSITION;
            half3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct DepthVaryings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
        half4 MaskedDepth(DepthVaryings input):SV_Target
        {
            clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a-_Cutoff);
            return input.positionCS.z;
        }
        ENDHLSL
        Pass
        {
            Name "Painted diffuse"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            half _OldTownNightFactor;
            struct Vertex { UNITY_VERTEX_INPUT_INSTANCE_ID half4 color: COLOR; float4 positionOS: POSITION; half3 normalOS: NORMAL; float2 uv: TEXCOORD0; };
            struct Varyings { float4 positionCS: SV_POSITION; half ao: TEXCOORD3; float3 positionWS: TEXCOORD0; half3 normalWS: TEXCOORD1; float2 uv: TEXCOORD2; };
            Varyings Vert(Vertex v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.ao=v.color.r; o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=TRANSFORM_TEX(v.uv,_BaseMap); return o;
            }
            half4 Frag(Varyings i): SV_Target
            {
                half4 paint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                clip(paint.a-_Cutoff);
                half3 n=normalize(i.normalWS);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half directional=smoothstep(-0.24h,0.84h,dot(n,sun.direction));
                if (_Cutoff>0) directional=.61h+.22h*directional;
                half sky=saturate(n.y*.5h+.5h);
                half3 ambient=half3(.96h,.98h,1.03h)*(_PaperFill-.085h+.095h*sky);
                half3 diffuse=(1.09h-_PaperFill)*sun.color*directional*lerp(.08h,1.h,sun.shadowAttenuation);
                half rim=1.h-saturate(dot(n,GetWorldSpaceNormalizeViewDir(i.positionWS)));
                half edge=1.h-.14h*smoothstep(.53h,.99h,rim);
                half3 lamps=0;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                if (_OldTownNightFactor > .001h)
                {
                    uint count=GetAdditionalLightsCount();
                    for (uint j=0;j<count;j++)
                    {
                        Light lamp=GetAdditionalLight(j,i.positionWS);
                        lamps+=lamp.color*lamp.distanceAttenuation*saturate(dot(n,lamp.direction))*.55h;
                    }
                }
                #endif
                AmbientOcclusionFactor contact=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                ambient*=lerp(1.h,contact.indirectAmbientOcclusion,.72h);
                diffuse*=contact.directAmbientOcclusion;
                return half4(paint.rgb*_Pigment.rgb*(ambient*lerp(1.h,.29h,_OldTownNightFactor)*i.ao+lamps*_OldTownNightFactor+diffuse*lerp(.72h,1.h,i.ao))*edge,1.h);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZWrite On
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex ShadowVertex
            #pragma fragment MaskedDepth
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            DepthVaryings ShadowVertex(DepthInput input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                DepthVaryings o;
                float3 world=TransformObjectToWorld(input.positionOS.xyz);
                half3 normal=TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir=normalize(_LightPosition-world);
                #else
                    float3 lightDir=_LightDirection;
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(world,normal,lightDir));
                #if UNITY_REVERSED_Z
                    o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                o.uv=TRANSFORM_TEX(input.uv,_BaseMap);
                return o;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex DepthVertex
            #pragma fragment MaskedDepth
            DepthVaryings DepthVertex(DepthInput input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                DepthVaryings o;
                o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.uv=TRANSFORM_TEX(input.uv,_BaseMap);
                return o;
            }
            ENDHLSL
        }
    }
}
