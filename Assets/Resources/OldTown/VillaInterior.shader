Shader "PCShop/Villa interior"
{
    Properties { _BaseMap("Furnished room",2D)="white"{} _RoomView("Room arrangement",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half _RoomView;
            CBUFFER_END
            float _OldTownNightFactor;
            struct Attributes {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float fog:TEXCOORD1;};
            Varyings Vert(Attributes v)
            {
                Varyings o;o.pos=TransformObjectToHClip(v.vertex.xyz);
                o.uv=v.uv;o.fog=ComputeFogFactor(o.pos.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.uv;
                if(_RoomView>.5 && _RoomView<1.5)uv.x=.94-.72*uv.x;
                if(_RoomView>1.5)uv=uv*float2(.65,.90)+float2(.32,.06);
                half3 pigment=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                half night=saturate(_OldTownNightFactor);
                half luma=dot(pigment,half3(.26,.64,.10));
                // Outside the pool of lamplight the furnished recess falls back into shadow at night.
                float2 offset=(uv-float2(.33,.64))*float2(2.2,1.);
                half lamp=exp2(-5.*dot(offset,offset));
                half3 daylight=pigment*half3(1.94,1.84,1.63)*(.69+.68*lamp)+luma*half3(.06,.13,.16);
                half3 lamplight=(pigment*.70+luma*.30)*half3(2.86,1.69,.76)*(.26+1.42*lamp);
                half3 tint=_RoomView>1.5?half3(.71,.82,.89):_RoomView>.5?half3(.91,1.,.96):half3(1.,.96,.85);
                return half4(MixFog(lerp(daylight,lamplight,night)*tint,i.fog),1.);
            }
            ENDHLSL
        }
    }
}
