Shader "PCShop/Layered town canopy"
{
    Properties { [MainTexture] _BaseMap("Leaf and bark pigment",2D)="white"{} _Cutoff("Leaf edge",Range(0,1))=.32
        _LeafTone("District foliage",Vector)=(1,1,1,1) _PaintedBough("Painted leaf fans",Float)=0 _ForestPigment("Woodland pigment",Float)=0 _SummerPetals("Flowering tree palette",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "DisableBatching"="True" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "CrownFacing.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            half4 _LeafTone;
            half _Cutoff,_PaintedBough,_ForestPigment,_SummerPetals;
        CBUFFER_END
        half _OldTownNightFactor;
        struct Input {float4 pos:POSITION;half3 n:NORMAL;float4 tangent:TANGENT;float2 uv:TEXCOORD0;float2 offset:TEXCOORD2;half4 color:COLOR;};
        struct Vary {float4 clip:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;half3 n:TEXCOORD2;half shade:TEXCOORD3;float fog:TEXCOORD4;half paleBloom:TEXCOORD5;};
        Vary LitVert(Input v)
        {
            Vary o;float3 view=GetWorldSpaceNormalizeViewDir(TransformObjectToWorld(v.pos.xyz));
            o.world=CrownPosition(v.pos.xyz,v.tangent,v.offset,view,1);
            o.clip=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(v.n);
            if(dot(v.offset,v.offset)>.0001)
            {
                float3 right=cross(float3(0,1,0),view);
                right*=rsqrt(max(dot(right,right),.001));
                // A rounded light field across each leafy mass gives the sun a real lit and shaded flank.
                float3 relief=right*sign(v.offset.x)*.72+view*.39+float3(0,.80+sign(v.offset.y)*.35,0);
                o.n=normalize(relief);
            }
            o.uv=v.uv;o.shade=v.color.r;o.paleBloom=v.color.g;o.fog=ComputeFogFactor(o.clip.z);return o;
        }
        half4 Albedo(Vary i){half4 p=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(p.a-_Cutoff);return p;}
        ENDHLSL
        Pass
        {
            Name "CanopyColor" Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex LitVert
            #pragma fragment ColorPass
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            half _DistrictDaylight;
            half4 ColorPass(Vary i):SV_Target
            {
                half3 raw=Albedo(i).rgb;
                half3 paint=raw*half3(1.02,.91,1.03);
                paint=lerp(paint,raw*half3(.98,1.06,.91),_DistrictDaylight);
                paint*=lerp(half3(1,1,1),_LeafTone.rgb,step(.097,i.uv.y));
                if(_SummerPetals>.5 && i.uv.x<.5 && i.uv.y>.103 && i.uv.y<.49)
                {
                    half petals=smoothstep(.008,.085,raw.r-raw.g)*smoothstep(-.009,.05,raw.b-raw.g);
                    half light=Luminance(raw);
                    half3 ivory=half3(1.08,1.0,.92)*(light*1.20+sqrt(light)*.21);
                    half tip=smoothstep(.17,.69,light);
                    half3 pink=paint*lerp(half3(.78,.57,.74),half3(1.,.89,.95),tip);
                    half3 cultivar=lerp(pink,ivory,smoothstep(.1,.9,i.paleBloom));
                    paint=lerp(paint,cultivar,petals*.96);
                }
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world),i.world,half4(1,1,1,1));
                half diffuse=.22+.78*saturate(dot(normalize(i.n),sun.direction)*.85+.18);
                half3 sky=lerp(half3(.43,.50,.54),half3(.13,.18,.24),_OldTownNightFactor);
                half3 lit=paint*(sky*i.shade+sun.color*diffuse*lerp(.12,1.,sun.shadowAttenuation))*i.shade;
                if (_PaintedBough>.5 && i.uv.y>.097)
                {
                    // Leave the leaf tips in sunlight while grouping the middle tones into deeper fans.
                    half value=Luminance(lit);
                    half body=smoothstep(.032,.12,value)*(1-smoothstep(.32,.57,value));
                    half tips=smoothstep(.36,.63,value);
                    half day=saturate(1-_OldTownNightFactor);
                    half lower=1.-smoothstep(.17,.85,frac((i.uv.y-.103)*2.));
                    half leafSun=smoothstep(.18,.61,Luminance(raw));
                    lit*=lerp(half3(1,1,1),(1.-.13*body)*(1.-.14*lower)*half3(.94+.16*tips,1.03,.92-.14*tips),day);
                    lit+=day*sun.shadowAttenuation*leafSun*leafSun*half3(.18,.14,.035);
                }
                // Distant foliage still needs distinct underleaf, mids and sunlit tips. Protect bark and blossom.
                if(_ForestPigment>.5 && i.uv.y>.097)
                {
                    half tips=smoothstep(.18,.61,Luminance(lit));
                    half green=smoothstep(-.012,.068,raw.g-raw.r*.94);
                    half highSlope=smoothstep(36.,148.,i.world.y);
                    half3 mids=lerp(half3(.48,.56,1.09),half3(1.04,1.06,1.24),highSlope);
                    half3 forest=lit*lerp(mids,half3(1.04,1.08,1.18),tips)
                        +lerp(half3(0,.001,.010),half3(.013,.016,.023),highSlope)*(1.-tips);
                    lit=lerp(lit,forest,_DistrictDaylight*green);
                }
                return half4(MixFog(lit,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ColorMask 0 ZWrite On
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment Mask
            float3 _LightDirection;
            Vary ShadowVert(Input v)
            {
                Vary o=(Vary)0;o.uv=v.uv;
                float3 world=CrownPosition(v.pos.xyz,v.tangent,v.offset,normalize(_LightDirection),1);
                o.clip=TransformWorldToHClip(ApplyShadowBias(world,TransformObjectToWorldNormal(v.n),_LightDirection));
                #if UNITY_REVERSED_Z
                    o.clip.z=min(o.clip.z,o.clip.w*UNITY_NEAR_CLIP_VALUE);
                #else
                    o.clip.z=max(o.clip.z,o.clip.w*UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 Mask(Vary i):SV_Target{Albedo(i);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask 0 ZWrite On
            HLSLPROGRAM
            #pragma vertex LitVert
            #pragma fragment Depth
            half4 Depth(Vary i):SV_Target{Albedo(i);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags {"LightMode"="DepthNormalsOnly"} ZWrite On
            HLSLPROGRAM
            #pragma vertex LitVert
            #pragma fragment Normals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 Normals(Vary i):SV_Target
            {
                Albedo(i);half3 normal=normalize(i.n);
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(PackNormalOctQuadEncode(normal)*.5+.5),0);
                #else
                    return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
    }
}
