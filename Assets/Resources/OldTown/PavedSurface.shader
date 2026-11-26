Shader "PCShop/Painted street paving"
{
    Properties
    {
        [MainTexture] _BaseMap("Paving or asphalt",2D)="white"{}
        [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
        _Variation("Weathering",2D)="gray"{}
        _Asphalt("Asphalt",Float)=0
        _PatinaStrength("Dust variation",Range(0,1))=1
        _Smoothness("Roughness input",Range(0,1))=.13
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Variation);SAMPLER(sampler_Variation);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;half4 _BaseColor;half _Asphalt;
            CBUFFER_END
            half _OldTownNightFactor,_CityRain;
            struct Input {float4 pos:POSITION;half3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct Output {float4 pos:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float fog:TEXCOORD3;};
            Output Vert(Input v)
            {
                Output o;o.world=TransformObjectToWorld(v.pos.xyz);o.pos=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normal);
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.fog=ComputeFogFactor(o.pos.z);return o;
            }
            half _DistrictDaylight, _PatinaStrength;
            half4 Frag(Output i):SV_Target
            {
                half3 pigment=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                half2 patina=SAMPLE_TEXTURE2D(_Variation,sampler_Variation,i.world.xz/14.6).rg;
                half dust=lerp(.22,smoothstep(.24,.77,patina.x*.70+patina.y*.30),_PatinaStrength);
                pigment*=lerp(half3(.84,.84,.90),half3(1.08,1.00,.93),dust);
                pigment*=lerp(half3(.79,.79,.78),half3(1.37,1.35,1.29),_Asphalt);
                pigment+=_Asphalt*dust*half3(.037,.023,.017);
                half gray=dot(pigment,half3(.27,.59,.14));
                pigment=lerp(pigment,gray*half3(.85,.96,1.06),_Asphalt*_DistrictDaylight*.65);
                half3 n=normalize(i.normal);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world),i.world,half4(1,1,1,1));
                AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.pos));
                half3 sky=SampleSH(n)*half3(.99,1.04,1.14)*ao.indirectAmbientOcclusion;
                half3 direct=sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation*ao.directAmbientOcclusion;
                half3 lamps=0;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                    if(_OldTownNightFactor>.001)
                    {
                        uint count=GetAdditionalLightsCount();
                        for(uint j=0;j<count;j++)
                        {
                            Light light=GetAdditionalLight(j,i.world);
                            lamps+=light.color*light.distanceAttenuation*saturate(dot(n,light.direction));
                        }
                    }
                #endif
                half wet=_CityRain*(.45+.4*dust);
                half sheen=pow(saturate(dot(n,normalize(sun.direction+GetWorldSpaceNormalizeViewDir(i.world)))),22);
                half3 color=pigment*(sky+direct+lamps)*(1.-.25*wet)+wet*sheen*sun.color*.10*sun.shadowAttenuation;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
