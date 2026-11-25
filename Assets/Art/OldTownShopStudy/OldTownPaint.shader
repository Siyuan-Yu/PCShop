Shader "PCShop/Old Town Painted Study"
{
    Properties
    {
        [MainTexture] _BaseMap("Painted albedo", 2D) = "white" {}
        _PaperFill("Diffuse fill", Range(0,1)) = 0.52
        [HideInInspector] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Painted diffuse"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _PaperFill;
            CBUFFER_END
            struct Vertex { float4 positionOS: POSITION; half3 normalOS: NORMAL; float2 uv: TEXCOORD0; };
            struct Varyings { float4 positionCS: SV_POSITION; float3 positionWS: TEXCOORD0; half3 normalWS: TEXCOORD1; float2 uv: TEXCOORD2; };
            Varyings Vert(Vertex v)
            {
                Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=TRANSFORM_TEX(v.uv,_BaseMap); return o;
            }
            half4 Frag(Varyings i): SV_Target
            {
                half3 n=normalize(i.normalWS);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half directional=smoothstep(-0.24h,0.84h,dot(n,sun.direction));
                half sky=saturate(n.y*.5h+.5h);
                half3 ambient=half3(.96h,.98h,1.03h)*(_PaperFill-.085h+.095h*sky);
                half3 diffuse=(1.09h-_PaperFill)*sun.color*directional*lerp(.08h,1.h,sun.shadowAttenuation);
                half rim=1.h-saturate(dot(n,GetWorldSpaceNormalizeViewDir(i.positionWS)));
                half edge=1.h-.14h*smoothstep(.53h,.99h,rim);
                return half4(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*(ambient+diffuse)*edge,1.h);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
