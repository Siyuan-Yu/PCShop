Shader "PCShop/PixelFoliage"
{
    Properties { _BaseColor("Leaf palette",Color)=(.3,.6,.2,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1; };
            V vert(A a){V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normalOS);return o;}
            float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
            half4 frag(V i):SV_Target
            {
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                float light=saturate(dot(normalize(i.normal),sun.direction)) * lerp(.48,1,sun.shadowAttenuation);
                float band=floor(light*3.99)/3;
                float patch=hash(floor(i.world*3.2));
                float cluster=hash(floor(i.world*.9));
                half3 leaf=_BaseColor.rgb * lerp(.67,1.25,band);
                leaf*=lerp(.85,1.16,step(.57,patch)*.7+cluster*.3);
                leaf=lerp(leaf,leaf*half3(1.38,1.14,.65),step(.82,patch)*band*.32);
                return half4(leaf*(.46+sun.color*.54),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
