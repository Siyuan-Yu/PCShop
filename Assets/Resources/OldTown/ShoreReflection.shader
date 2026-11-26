Shader "PCShop/Painted shore reflection"
{
    Properties
    {
        _BaseMap("Reflected pigment",2D)="white"{}
        _Pigment("Pigment",Color)=(1,1,1,1)
        _Cutoff("Cutout",Range(0,1))=.36
        _LayeredCrown("Layered crown",Float)=0
        _ParkPond("Park pond ripple scattering",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            Stencil {Ref 1 ReadMask 1 Comp Equal Pass Keep}
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZTest Always ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CrownFacing.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Pigment;
                half _Cutoff,_LayeredCrown,_ParkPond;
            CBUFFER_END
            float _PCShopWorldVisualTime, _OldTownNightFactor;
            struct Attributes {float4 pos:POSITION;float4 tangent:TANGENT;float2 uv:TEXCOORD0;float2 offset:TEXCOORD2;};
            struct Varyings {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;};
            Varyings Vert(Attributes a)
            {
                Varyings o;o.world=TransformObjectToWorld(a.pos.xyz);
                if(_LayeredCrown>.5)o.world=CrownPosition(a.pos.xyz,a.tangent,a.offset,GetWorldSpaceNormalizeViewDir(o.world),-1);
                o.pos=TransformWorldToHClip(o.world);o.uv=a.uv;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float t=_PCShopWorldVisualTime;
                float waterline=dot(i.world.xz,float2(.38,.925))+.37*i.world.y;
                float wave=sin(waterline*4.8+.78*t+sin(i.world.x*1.7))*.62+
                    sin(waterline*9.1-.4*t+cos(i.world.z*1.3))*.38;
                float2 uv=i.uv+float2(wave*(.012+.012*_ParkPond),.002*sin(waterline*2.2+t*.31));
                half4 pigment=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
                clip(pigment.a-_Cutoff);
                half depth=saturate(-i.world.y*.046);
                half alpha=(.78-.33*depth)*(.77+.23*wave);
                half3 ink=lerp(pigment.rgb*_Pigment.rgb*.82,half3(.057,.104,.080),.33);
                if(_ParkPond>.5)
                {
                    half crest=smoothstep(.30,.80,wave);
                    alpha*=.95-.32*crest;
                    half3 leaf=pigment.rgb*_Pigment.rgb;
                    half tip=smoothstep(.13,.46,dot(leaf,half3(.25,.60,.15)));
                    ink=lerp(ink*half3(.54,.70,.50),half3(.105,.218,.068),tip*.47);
                }
                return half4(ink*(1.-.66*_OldTownNightFactor),alpha);
            }
            ENDHLSL
        }
    }
}
