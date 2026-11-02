Shader "PCShop/LakeWater"
{
    Properties { _BaseColor("Lake blue",Color)=(0.12,0.48,0.65,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float fog:TEXCOORD1; };
            Varyings vert(Attributes a)
            {
                Varyings o; o.world=TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.world.xz;float time=_Time.y;
                float wave=sin(p.x*.31+sin(p.y*.09)*2+time*.5)*cos(p.y*.47+cos(p.x*.07)*2-time*.3);
                float3 n=normalize(float3(wave*.025,1,cos(p.x*.23-p.y*.41+wave+time*.3)*.018));
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                float fresnel=pow(1-saturate(dot(view,n)),3);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                float spec=pow(saturate(dot(n,normalize(sun.direction+view))),130);
                float ripple=pow(saturate(wave),18)*.018;
                float depth=saturate(1-length(float2((p.x+25)/360,(p.y-90)/300)));
                half3 lake=lerp(_BaseColor.rgb,half3(.055,.32,.49),depth*.7);
                half3 color=lerp(lake,half3(.52,.77,.82),fresnel*.65);
                color*=.38+.62*sun.color*sun.shadowAttenuation;
                color+=(spec*.4+ripple)*sun.color;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
