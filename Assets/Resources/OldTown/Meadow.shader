Shader "PCShop/Painted meadow"
{
    Properties
    {
        _BaseMap("Meadow",2D)="white"{}
        _Lawn("Summer turf",2D)="white"{}
        _Soil("Broken earth",2D)="white"{}
        _Variation("Patch distribution",2D)="gray"{}
        _LandUse("District soil and grove weights",2D)="gray"{}
        _SoilPigment("Regional soil pigment balance",2D)="gray"{}
        _LandBounds("World origin and inverse extent",Vector)=(0,0,1,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Lawn); SAMPLER(sampler_Lawn);
            TEXTURE2D(_Soil); SAMPLER(sampler_Soil);
            TEXTURE2D(_Variation); SAMPLER(sampler_Variation);
            TEXTURE2D(_LandUse); SAMPLER(sampler_LandUse);
            TEXTURE2D(_SoilPigment); SAMPLER(sampler_SoilPigment);
            float4 _LandBounds;
            half _DistrictLandscape;
            struct Attributes {float4 positionOS:POSITION;half3 normalOS:NORMAL;};
            struct Varyings {float4 pos:SV_POSITION;float3 world:TEXCOORD0;half3 n:TEXCOORD1;float fog:TEXCOORD2;};
            Varyings Vert(Attributes v)
            {
                Varyings o; o.world=TransformObjectToWorld(v.positionOS.xyz);o.pos=TransformWorldToHClip(o.world);
                o.n=TransformObjectToWorldNormal(v.normalOS);o.fog=ComputeFogFactor(o.pos.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.world.xz;
                half3 patch=SAMPLE_TEXTURE2D(_Variation,sampler_Variation,p/88.).rgb;
                float2 rotated=float2(.8*p.x+.6*p.y,-.6*p.x+.8*p.y);
                half3 fine=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p/5.2+patch.rg*.12).rgb;
                half3 leaf=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,rotated/13.7).rgb;
                half3 soil=SAMPLE_TEXTURE2D(_Soil,sampler_Soil,rotated/5.8).rgb;
                half dry=smoothstep(.49,.72,patch.r*.60+patch.g*.40+(.1-leaf.g)*.5);
                half3 pigment=lerp(fine,leaf,.24)*lerp(half3(.86,.94,.90),half3(1.17,1.09,.87),saturate(patch.g*1.35));
                half value=dot(pigment,half3(.30,.54,.16));
                pigment=lerp(value.xxx,pigment,.64)*half3(1.13,1.00,.96);
                pigment=lerp(pigment,soil*.92,dry*.57);
                float2 fieldUV=(p-_LandBounds.xy)*_LandBounds.zw;
                half4 field=SAMPLE_TEXTURE2D(_LandUse,sampler_LandUse,saturate(fieldUV));
                half coverage=field.a*all(fieldUV>=0.)*all(fieldUV<=1.);
                half3 warm=lerp(half3(.89,1.03,.96),half3(1.20,1.05,.96),field.g);
                pigment*=lerp(half3(1,1,1),warm*lerp(.74,1.65,field.r),coverage);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world),i.world,half4(1,1,1,1));
                half3 balance=SAMPLE_TEXTURE2D(_SoilPigment,sampler_SoilPigment,saturate(fieldUV)).rgb;
                pigment*=exp2((balance-.5)*1.8*coverage);
                // Managed turf keeps the brushwork of the original ground plate. Light and dark
                // blades shift together, while exposed earth keeps its own hue.
                if (_DistrictLandscape>.001)
                {
                    half3 nearGrass=SAMPLE_TEXTURE2D(_Lawn,sampler_Lawn,p/6.25+patch.rg*.11).rgb;
                    half3 broadGrass=SAMPLE_TEXTURE2D(_Lawn,sampler_Lawn,rotated/15.7).rgb;
                    half3 turf=lerp(nearGrass,broadGrass,.26)*half3(.91,.84,1.12);
                    half painterly=SAMPLE_TEXTURE2D(_Variation,sampler_Variation,rotated/4.8+patch.rg*.29).g;
                    half mow=smoothstep(.19,.69,patch.g*.72+painterly*.28);
                    turf*=lerp(half3(.59,.66,.74),half3(1.35,1.18,.99),mow);
                    pigment=lerp(pigment,lerp(turf,soil*1.12,dry*.13),_DistrictLandscape*.80);
                }
                half3 normal=normalize(i.n);
                AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.pos));
                half3 light=SampleSH(normal)*ao.indirectAmbientOcclusion+
                    sun.color*saturate(dot(normal,sun.direction))*lerp(.16,1.,sun.shadowAttenuation)*ao.directAmbientOcclusion;
                return half4(MixFog(pigment*light,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
