Shader "PCShop/Villa glass"
{
    Properties { _GuardRail("Clear balustrade",Float)=0 _Sheltered("Under a canopy",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _GuardRail, _Sheltered;
            CBUFFER_END
            float _OldTownNightFactor, _CityRain;
            struct Attributes {float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct Varyings {float4 pos:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float fog:TEXCOORD3;};
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.world=TransformObjectToWorld(v.position.xyz);
                o.pos=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normal);
                o.uv=v.uv;o.fog=ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                half3 normal=normalize(i.normal);
                if(dot(normal,view)<0)normal=-normal;
                half grazing=pow(1.-saturate(dot(view,normal)),2.);
                half3 ray=reflect(-view,normal);
                half3 sky=SampleSH(ray);
                half upper=smoothstep(.12,.97,i.uv.y);
                half3 reflected=lerp(half3(.055,.163,.139),half3(.23,.44,.49),upper);
                half sunward=saturate(dot(normal,GetMainLight().direction)*.5+.5);
                reflected=reflected*(.77+.27*sunward)+.17*max(half3(0,0,0),sky);
                half alpha=.13+.18*upper+.31*grazing;
                // The overhanging slab blocks the bright upper sky at these windows.
                reflected=lerp(reflected,reflected*half3(.42,.68,.69),_Sheltered);
                alpha*=1.-.47*_Sheltered;
                alpha=lerp(alpha,.024+.033*upper+.048*grazing,_GuardRail);
                half night=saturate(_OldTownNightFactor);
                reflected=lerp(reflected,half3(.033,.074,.106),night*.90);
                return half4(MixFog(reflected,i.fog),saturate(alpha*(1.-.52*night)+(.045-.029*_GuardRail)*_CityRain));
            }
            ENDHLSL
        }
    }
}
