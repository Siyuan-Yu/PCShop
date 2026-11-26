Shader "PCShop/Old Quarter Canopy"
{
    Properties
    {
        [MainTexture] _BaseMap("Hand-painted canopy",2D)="white"{}
        _PaintedCrown("Crown with authored light and shadow", Range(0,1)) = 0
        _PaintGrouping("Leaf colour masses", Range(0,1)) = 0
        _PetalLift("Sunlit petal pigment", Range(0,.12)) = 0
        _GroveContrast("Open-grown summer foliage", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _PaintGrouping, _PaintedCrown, _PetalLift, _GroveContrast;
            CBUFFER_END
            struct A { float4 pos:POSITION;float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float fog:TEXCOORD2; };
            V Vert(A a)
            {
                V v;
                float width=length(TransformObjectToWorldDir(float3(1,0,0),false));
                float height=length(TransformObjectToWorldDir(float3(0,1,0),false));
                float3 viewPivot=TransformWorldToView(TransformObjectToWorld(float3(0,0,0)));
                float3 viewPos=viewPivot+float3(a.pos.x*width,a.pos.y*height,0);
                v.pos=mul(UNITY_MATRIX_P,float4(viewPos,1));
                v.world=mul(UNITY_MATRIX_I_V,float4(viewPos,1)).xyz;
                v.uv=a.uv;v.fog=ComputeFogFactor(v.pos.z);return v;
            }
            half4 Frag(V v):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv);clip(c.a-.46);
                Light light=GetMainLight(TransformWorldToShadowCoord(v.world));
                half power=max(light.color.r,max(light.color.g,light.color.b));
                if (_PaintedCrown > .5)
                {
                    // Model the broad response of the crown as it turns against the sun.
                    // The atlas supplies leaf-by-leaf light, gaps, and warm and cool pigment.
                    float2 face = float2(frac(v.uv.x * 2), v.uv.y) * 2 - 1;
                    half3 dome = normalize(half3(face, sqrt(saturate(1-dot(face,face)))+.22));
                    half3 lightVS = TransformWorldToViewDir(light.direction, true);
                    half facing = saturate(dot(dome,lightVS)*.5h+.5h);
                    half3 illumination = lerp(half3(.88h,.93h,1.04h),half3(1.07h,1.035h,.97h),facing);
                    half3 sky = lerp(half3(.72h,.78h,.87h),half3(1,1,1),light.shadowAttenuation);
                    half3 hue = lerp(half3(1,1,1),light.color/max(power,.001h),.30h);
                    c.rgb *= illumination * sky * hue * clamp(.23h+power*.78h,.20h,1.13h);
                    if (_GroveContrast > 0)
                    {
                        half warm=smoothstep(.13h,.50h,Luminance(c.rgb));
                        half3 pigment=lerp(half3(.79h,.85h,.85h),half3(1.17h,1.08h,.92h),warm);
                        c.rgb*=lerp(half3(1,1,1),pigment,_GroveContrast);
                    }
                    if (_PetalLift > 0)
                    {
                        half petal = smoothstep(.035h,.16h,Luminance(c.rgb)) * smoothstep(.35h,.80h,power);
                        c.rgb = saturate(c.rgb + _PetalLift * petal * sqrt(saturate(c.rgb)));
                    }
                    return half4(MixFog(c.rgb,v.fog),1);
                }
                if (_PaintGrouping > 0)
                {
                    // The main masses are painted into these crowns. Only bind fine
                    // flecks into the nearby pigment; retain the artist's light and dark shapes.
                    float2 dx = ddx(v.uv), dy = ddy(v.uv);
                    half3 broad = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, v.uv, dx*1.85, dy*1.85).rgb;
                    c.rgb = lerp(c.rgb, broad, .16*_PaintGrouping);
                    half green = saturate((c.g-c.b)*10)*saturate((c.g-c.r+.055)*12);
                    half sunward = smoothstep(.10,.52,Luminance(c.rgb));
                    c.rgb *= lerp(half3(1,1,1),lerp(half3(.94,.94,.85),half3(1.17,.995,.76),sunward),green);
                }
                return half4(MixFog(c.rgb*clamp(.24+power*.77,.23,1.13)*lerp(.78,1,light.shadowAttenuation),v.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 vertex:POSITION;float2 uv:TEXCOORD0; };
            struct V { float4 position:SV_POSITION;float2 uv:TEXCOORD0; };
            V ShadowVert(A a)
            {
                V v;float2 scale=float2(length(TransformObjectToWorldDir(float3(1,0,0),false)),
                    length(TransformObjectToWorldDir(float3(0,1,0),false)));
                float3 pos=TransformWorldToView(TransformObjectToWorld(float3(0,0,0)));
                v.position=mul(UNITY_MATRIX_P,float4(pos+float3(a.vertex.xy*scale,0),1));
                v.uv=a.uv;return v;
            }
            half4 ShadowFrag(V v):SV_Target {clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv).a-.5);return 0;}
            ENDHLSL
        }
    }
}
