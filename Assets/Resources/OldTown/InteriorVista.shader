Shader "PCShop/InteriorVista"
{
    Properties { [MainTexture] _BaseMap("街区景色", 2D)="white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float _OldTownNightFactor;
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A a) { V o; o.position=TransformObjectToHClip(a.vertex.xyz); o.uv=a.uv; return o; }
            half4 frag(V i):SV_Target
            {
                half3 scene=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                half3 twilight=scene*half3(.17,.23,.37)+half3(.012,.018,.031);
                return half4(lerp(scene,twilight,saturate(_OldTownNightFactor)),1);
            }
            ENDHLSL
        }
    }
}
