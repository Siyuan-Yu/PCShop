Shader "PCShop/Old Quarter Street Surfaces"
{
    Properties { [MainTexture] _BaseMap("Painted surface sheet",2D)="white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 position:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 position:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3; };
            V Vert(A a)
            {
                V v; VertexPositionInputs p=GetVertexPositionInputs(a.position.xyz);
                v.position=p.positionCS;v.world=p.positionWS;v.normal=TransformObjectToWorldNormal(a.normal);
                v.uv=a.uv;v.fog=ComputeFogFactor(v.position.z);return v;
            }
            half4 Frag(V v):SV_Target
            {
                // UVs in the second unit flag panes within a painted facade.
                float2 texcoord=float2(frac(v.uv.x),v.uv.y);
                half3 paint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,texcoord).rgb;
                Light sun=GetMainLight(TransformWorldToShadowCoord(v.world));
                half power=max(sun.color.r,max(sun.color.g,sun.color.b));
                half3 n=normalize(v.normal);
                half side=smoothstep(-.22h,.62h,dot(n,sun.direction));
                half3 fill=lerp(half3(.79h,.86h,.94h),half3(1.05h,1.03h,.99h),side);
                half3 light=lerp(half3(.76h,.81h,.87h),half3(1,1,1),sun.shadowAttenuation);
                half3 result=paint*fill*light*clamp(.19h+power*.85h,.18h,1.15h);
                // Small moving highlights belong to glass and iron, leaving limewash and canvas matte.
                int2 cell=int2(floor(texcoord.x*4),floor((1-texcoord.y)*8));
                int slot=cell.y*4+cell.x;
                half vertical=frac(texcoord.y*8);
                if(slot<=3 || (slot>=18 && slot<=20))
                    result*=1-.17h*smoothstep(.86h,.97h,vertical);
                if(slot>=8 && slot<=10)
                    result*=1-.25h*smoothstep(.51h,.96h,vertical);
                bool glazing=v.uv.x>=1 || slot==8 || slot==9 || slot==11 || slot==12;
                if (glazing || slot==28)
                {
                    half3 view=GetWorldSpaceNormalizeViewDir(v.world);
                    half spec=pow(saturate(dot(n,SafeNormalize(sun.direction+view))),glazing?27:43);
                    result+=min(power,1.5h)*sun.shadowAttenuation*spec*(glazing?.048h:.081h);
                    if(glazing)
                    {
                        half rim=pow(1-saturate(abs(dot(n,view))),3);
                        result+=half3(.007h,.015h,.024h)*rim*saturate(power);
                        half warm=saturate((paint.r-paint.b)*3)*saturate(paint.r*2);
                        result+=half3(.35h,.16h,.045h)*warm*(1-smoothstep(.30h,.89h,power));
                    }
                }
                return half4(MixFog(result,v.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
