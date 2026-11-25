Shader "PCShop/Forest floor shade"
{
    Properties { _Squared("Foundation",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-12" "RenderType"="Transparent" }
        Pass
        {
            Blend DstColor Zero
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half _Squared;
            struct Input {float3 pos:POSITION;float2 uv:TEXCOORD0;};
            struct Output {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            Output Vert(Input v){Output o;o.pos=TransformObjectToHClip(v.pos);o.uv=v.uv;return o;}
            half4 Frag(Output i):SV_Target
            {
                float2 local=i.uv*2.-1.;
                half distance=_Squared>.5?max(abs(local.x),abs(local.y)):dot(local,local);
                half occlusion=1.-smoothstep(_Squared>.5?.63:.05,.99,distance);
                return half4(1.-occlusion*half3(.34,.32,.27),1);
            }
            ENDHLSL
        }
    }
}
