Shader "PCShop/LakeWater"
{
    Properties
    {
        _BaseColor("Deep pigment",Color)=(.30,.39,.40,1)
        _WaterMarks("Wind and broken ripple strokes",2D)="gray"{}
        _LakeShore("Use shoreline distances",Float)=0
        _Basin("Water body: lake, elliptical pond or tiled pool",Float)=0
        _ReflectMask("Lake reflection stencil",Float)=0
        _WindField("Wind patches",2D)="gray"{}
        _PoolLight("Underwater caustic pigment",2D)="gray"{}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-110" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Stencil {Ref [_ReflectMask] WriteMask 1 Comp Always Pass Replace}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            TEXTURE2D(_WaterMarks); SAMPLER(sampler_WaterMarks);
            TEXTURE2D(_WindField); SAMPLER(sampler_WindField);
            TEXTURE2D(_PoolLight); SAMPLER(sampler_PoolLight);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _LakeShore, _Basin;
            CBUFFER_END
            float _PCShopWorldVisualTime, _OldTownNightFactor, _CityRain, _DistrictDaylight;
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 shore:TEXCOORD1;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float fog:TEXCOORD1;float shore:TEXCOORD2;float2 uv:TEXCOORD3;};
            Varyings vert(Attributes a)
            {
                Varyings o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
                o.fog=ComputeFogFactor(o.positionCS.z);o.shore=a.shore.x;o.uv=a.uv;return o;
            }
            // Walk the reflected ray against the opaque scene. The view is usually orthographic,
            // so reconstruct actual world positions rather than using perspective-only linear depth.
            half4 ShoreReflection(float3 surface,float3 direction,float2 distortion)
            {
                float travel=1.0;
                [unroll] for(int k=0;k<11;k++)
                {
                    float3 probe=surface+direction*travel;
                    float4 projected=TransformWorldToHClip(probe);
                    float4 screen=ComputeScreenPos(projected);
                    float2 uv=screen.xy/screen.w+distortion;
                    if(any(uv<.002)||any(uv>.998)) break;
                    float depth=SampleSceneDepth(uv);
                    #if !UNITY_REVERSED_Z
                        depth=lerp(UNITY_NEAR_CLIP_VALUE,1.0,depth);
                    #endif
                    float3 scene=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                    float behind=TransformWorldToView(scene).z-TransformWorldToView(probe).z;
                    if(behind>=0 && behind<max(1.8,travel*.37) && scene.y>surface.y+.18)
                    {
                        float2 edge=min(uv,1.-uv);
                        half confidence=saturate(min(edge.x,edge.y)*19)*saturate((scene.y-surface.y)*2);
                        return half4(SampleSceneColor(uv),confidence);
                    }
                    travel=travel*1.45+1.0;
                }
                return 0;
            }
            float RippleSeed(float2 cell)
            {
                float3 p=frac(cell.xyx*float3(.1031,.1030,.0973));
                p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }

            // Reflected highlights occupy short wind patches. Each wavelet has a tapered end
            // and a different length, spacing and phase, so no stripe spans an entire pond.
            half SurfaceFlecks(float2 p,float time,float scale)
            {
                float2 grid=p*float2(1.12,5.8)*scale;
                grid.y+=.27*sin(grid.x*1.27+time*.24)+.22*sin(grid.x*.49-grid.y*.11);
                grid.x+=floor(grid.y)*.618;
                float pixelWidth=max(fwidth(grid.x),fwidth(grid.y));
                float2 id=floor(grid),cell=frac(grid);
                float seed=RippleSeed(id),center=.27+.45*seed;
                float width=.13+.29*RippleSeed(id+23.1);
                float span=abs(cell.x-.5)/width;
                float aa=clamp(fwidth(grid.y)*.56,.025,.26);
                float crest=(1.-smoothstep(.042,.042+aa,abs(cell.y-center)));
                float resolved=1.-smoothstep(.24,1.10,pixelWidth);
                return lerp(.019,crest*(1.-smoothstep(.45,1.,span))*smoothstep(.29,.81,seed),resolved);
            }

            // A bright reflected sky patch is carried by a handful of broad facets.
            // Each has a soft shoulder and a smaller silver core. At distant zoom the
            // area naturally dissolves into the broader water colour.
            half SkyCrests(float2 p,float time)
            {
                float2 grid=p*float2(1.14,2.82);
                float2 id=floor(grid);
                float seed=RippleSeed(id+19.2);
                float2 middle=.32+.36*float2(RippleSeed(id+7.8),RippleSeed(id+36.4));
                middle.y+=.047*sin(time*.65+seed*17.);
                float2 delta=(frac(grid)-middle)*float2(3.12,4.22);
                float radius=length(delta),aa=clamp(fwidth(radius),.055,.84);
                half shoulder=1.-smoothstep(.80-aa*.5,.80+aa*.5,radius);
                half core=1.-smoothstep(.38-aa*.5,.38+aa*.5,radius);
                half resolved=1.-smoothstep(.50,1.35,max(fwidth(grid.x),fwidth(grid.y)));
                return lerp(.018,(shoulder*.82+core*.71)*smoothstep(.38,.63,seed),resolved);
            }

            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.world.xz;float time=_PCShopWorldVisualTime;
                float2 wind=float2(dot(p,float2(.91,.415)),dot(p,float2(-.415,.91)));
                half2 gust=SAMPLE_TEXTURE2D(_WindField,sampler_WindField,p/67.+time*float2(.0008,.0003)).rg;
                float2 eddy=float2(gust.x-.5,gust.y-.5);
                half3 a=SAMPLE_TEXTURE2D(_WaterMarks,sampler_WaterMarks,wind/float2(9.2,4.1)+eddy*.18+float2(.0018,-.011)*time).rgb;
                half3 b=SAMPLE_TEXTURE2D(_WaterMarks,sampler_WaterMarks,(wind.yx+float2(wind.x*.13,wind.y*.11))/float2(6.4,11.3)+eddy*.07+float2(.006,.002)*time).rgb;
                float2 slope=float2(a.r-.5,b.g-.5)*(.19+_CityRain*.06);
                float3 n=normalize(float3(slope.x,1,slope.y));
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                float3 ray=reflect(-view,n);
                half4 reflection=ShoreReflection(i.world,ray,slope*.015);
                half shallows=(1-smoothstep(0.,12.,i.shore+eddy.y*2))*_LakeShore;
                half3 water=lerp(_BaseColor.rgb+_LakeShore*half3(.022,.016,.003),half3(.13,.19,.13),shallows*.78);
                half rippleBand=.5+.5*cos(wind.y*4.4+3.1*gust.g+2.6*a.g+.64*time);
                if(_Basin>.5)
                {
                    float2 local=abs(i.uv*2.-1.);
                    half edge=_Basin>1.5?max(local.x,local.y):length(local);
                    half shelf=smoothstep(.61,1.,edge+.05*(gust.r-.5));
                    half3 bottom=(_Basin>1.5 && _Basin<2.5)?half3(.16,.37,.33):half3(.22,.25,.13);
                    water=lerp(water,bottom*(.75+.45*b.g),shelf*.67);
                    if(_Basin>1.5 && _Basin<2.5)
                    {
                        float2 tile=abs(frac(p*1.75)-.5);
                        half joint=smoothstep(.455,.497,max(tile.x,tile.y));
                        water*=1.-joint*.13;
                        half3 caustic=SAMPLE_TEXTURE2D(_PoolLight,sampler_PoolLight,p/2.1+eddy*.12+slope*.12).rgb;
                        water=lerp(water,caustic*half3(.92,1.24,1.27),.72);
                    }
                }
                half sky=saturate(.20+gust.r*.06+.22*pow(1-saturate(dot(view,n)),2));
                half3 skyColor=lerp(half3(.24,.29,.30),half3(.32,.36,.35),gust.g);
                half3 color=lerp(water,skyColor,sky*.74);
                color*=lerp(half3(1,1,1),half3(.76,.89,.97),max(_LakeShore,step(.5,_Basin)*(1.-step(1.5,_Basin))));
                color*=.93+.095*gust.g+.09*(a.r-.5);
                half offshore=smoothstep(2.,21.,i.shore+eddy.x*3.5);
                half bankShade=(1.-offshore)*_LakeShore*(.23+.64*gust.g);
                color=lerp(color,color*half3(.55,.70,.59),bankShade);
                half longRipple=.5+.5*sin(wind.y*.88+6.8*gust.g+.3*time+sin(wind.x*.17));
                color+=_LakeShore*half3(.045,.039,.027)*pow(longRipple,7)*smoothstep(.3,.68,gust.r);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world),i.world,half4(1,1,1,1));
                half day=1-_OldTownNightFactor;
                half3 dusk=lerp(half3(.30,.39,.48),half3(1,.96,.87),day);
                half clearDay=_DistrictDaylight*(1.-_CityRain*.65);
                half3 clearWater=water*half3(.62,1.33,1.59)+half3(.012,.078,.125);
                color=lerp(color,clearWater,clearDay*.86);
                color*=dusk*lerp(.72,1.,sun.shadowAttenuation)*(1.-_CityRain*.14);
                color=lerp(color,reflection.rgb*half3(.84,.94,.86),reflection.a*(_Basin>1.5?.25:.65));
                half glint=smoothstep(.35,.81,a.b)*(.35+.65*b.r);
                half spec=pow(saturate(dot(n,normalize(view+sun.direction))),80);
                half fineWeight=1.-smoothstep(.6,1.4,fwidth(wind.y)*4.4);
                half brokenLight=pow(rippleBand,8)*smoothstep(.43,.76,a.b+b.r*.19)*fineWeight;
                if(_Basin>0)brokenLight*=.58;
                half brokenSky=smoothstep(.48,.75,gust.g+.14*sin(wind.x*.13+gust.r*6.));
                half silver=pow(saturate(.5+.5*sin(wind.y*1.27+gust.g*7.+time*.22)),9)*brokenSky;
                float2 towards=view.xz*rsqrt(max(dot(view.xz,view.xz),.0001));
                float2 side=float2(-towards.y,towards.x);
                float2 rippleSpace=float2(dot(p,side),dot(p,towards));
                half scatter=SurfaceFlecks(rippleSpace+eddy*.54,time,1.)
                    +.44*SurfaceFlecks(rippleSpace+float2(2.8,5.1),time,.49);
                half patch=.5+.5*sin(wind.x*.64+2.9*sin(wind.y*.23)+1.7*gust.r);
                half windBreak=smoothstep(.28,.84,.5+.5*sin(wind.y*.42+gust.r*3.9));
                half lightPatch=pow(patch,5)*(.20+.80*windBreak);
                color+=clearDay*(.016*glint+.033*brokenLight+.024*silver)*half3(.70,1.,1.15);
                color+=sun.color*(.004*glint+.013*brokenLight+.12*spec)*sun.shadowAttenuation;
                color+=day*sun.shadowAttenuation*(scatter*(half3(.016,.023,.023)+lightPatch*half3(.38,.28,.155))
                    +_LakeShore*silver*half3(.032,.027,.017));
                if(_Basin>.5 && _Basin<1.5)
                {
                    float2 pond=i.uv*2.-1.;
                    half bank=smoothstep(.55,.98,length(pond)+.05*(b.g-.5));
                    half canopy=bank*(.34+.66*smoothstep(-.35,.63,pond.y-.45*pond.x));
                    half3 teal=lerp(half3(.026,.194,.222),half3(.077,.287,.306),
                        smoothstep(-.39,.56,pond.x*.5-pond.y+gust.g*.34));
                    teal=lerp(teal,half3(.074,.175,.110)*(.80+.38*b.g),canopy);
                    color=lerp(color,teal*dusk*lerp(.62,1.,sun.shadowAttenuation),.79*(1.-reflection.a*.71));
                    // Tree shadows come from the park's real sun and foliage. Ripples
                    // break up their reflected colour, especially at the sheltered bank.
                    half inShade=1.-sun.shadowAttenuation;
                    half reflectedLeaf=reflection.a*smoothstep(.003,.068,
                        reflection.g-max(.91*reflection.r,1.04*reflection.b));
                    half leafInk=saturate(inShade*.78+reflectedLeaf*.70)*(.42+.58*bank);
                    half dappling=smoothstep(.30,.69,b.g*.54+a.r*.46);
                    color=lerp(color,color*half3(.56,.77,.64)+half3(.003,.010,.002)*day,
                        leafInk*(.56+.44*dappling));

                    // The broad patch breaks into short, screen-horizontal strokes at the regular play zoom.
                    // Coarse ripples survive minification; the finer layer dissolves into them as we zoom out.
                    float2 vertical=ddy(pond);
                    float2 across=float2(vertical.y,-vertical.x)*rsqrt(max(dot(vertical,vertical),.00000001));
                    half column=dot(pond,across)+.08*sin(pond.y*4.4+gust.r);
                    half openSky=exp2(-34.*column*column)
                        *(1.-smoothstep(.62,.96,length(pond)));
                    float2 mirror=rippleSpace+float2(eddy.y*.76,eddy.x*.29);
                    half coarse=SurfaceFlecks(mirror+float2(1.7,3.4),time,.61);
                    half fine=SurfaceFlecks(mirror+float2(4.4,1.8),time,1.12);
                    half wideFleck=SurfaceFlecks(mirror+float2(6.2,-1.9),time,.32);
                    half wave=.5+.5*sin(rippleSpace.y*2.7+.36*sin(rippleSpace.x*.87)+3.4*gust.g+time*.29);
                    half lane=pow(wave,5)*(.28+.72*smoothstep(.29,.73,patch));
                    // Smaller open-sky gaps interrupt the main highlight. The broad
                    // flecks remain visible when the pond occupies only a few dozen pixels.
                    half skyGaps=(1.-smoothstep(.65,.96,length(pond)))
                        *smoothstep(.60,.91,patch)*(.33+.67*windBreak);
                    half skyWindow=saturate(openSky+skyGaps*.52)*(1.-reflectedLeaf*.65);
                    color+=day*(1.-_CityRain*.74)*sun.shadowAttenuation*skyWindow
                        *((coarse*1.12+fine*.42+wideFleck*.84)*(.48+.52*wave)+.076*lane)*half3(.73,.88,.85);
                    // Only a fraction of the wavelets catch the brightest patch of open sky.
                    // Keep a small, separately antialiased core inside those short crests.
                    half shine=day*(1.-_CityRain*.84)*sun.shadowAttenuation*skyWindow;
                    color+=shine*SkyCrests(mirror,time)*half3(1.20,1.12,.93);
                }
                if (_Basin>2.5)
                {
                    float2 edgeUV=abs(i.uv*2.-1.);
                    half rim=smoothstep(.69,.99,max(edgeUV.x,edgeUV.y));
                    half windPool=smoothstep(.23,.78,patch*.58+gust.g*.42);
                    half3 blueGreen=lerp(half3(.019,.063,.070),half3(.086,.206,.196),windPool);
                    half shelf=smoothstep(.32,.75,a.r*.6+b.g*.4);
                    blueGreen=lerp(blueGreen,half3(.087,.140,.092)*(.76+.40*shelf),rim*(.45+.36*shelf));
                    half daylight=day*(1.-_CityRain*.58);
                    half reflectedTrees=reflection.a*smoothstep(.004,.074,reflection.g-max(.89*reflection.r,reflection.b));
                    color=lerp(color,blueGreen*dusk*lerp(.58,1.,sun.shadowAttenuation),
                        .84*(1.-.64*reflectedTrees));
                    half shade=saturate((1.-sun.shadowAttenuation)*.78+reflectedTrees*.90);
                    color*=lerp(half3(1.,1.,1.),half3(.52,.64,.56),shade*(.73+.27*shelf));
                    float2 basin=i.uv*2.-1.;
                    float2 vertical=ddy(basin);
                    float2 across=float2(vertical.y,-vertical.x)*rsqrt(max(dot(vertical,vertical),1.e-8));
                    half axis=dot(basin,across)-.13+.065*sin(basin.y*5.7+gust.r);
                    half glintPath=exp2(-27.*axis*axis)*(1.-smoothstep(.68,1.,max(edgeUV.x,edgeUV.y)));
                    half crest=SurfaceFlecks(rippleSpace+float2(2.9,4.1)+eddy*.40,time,.38);
                    half fineCrest=SurfaceFlecks(rippleSpace+float2(-3.9,7.2),time,.72);
                    half wavePeak=pow(.5+.5*sin(rippleSpace.y*2.4+gust.g*5.+.36*time),5);
                    color+=daylight*sun.shadowAttenuation*(1.-.76*reflectedTrees)
                        *((.025+.060*windPool)*fineCrest+glintPath*((1.02*crest+.48*fineCrest)*(.26+.74*wavePeak)+.038*wavePeak))
                        *half3(1.00,.89,.61);
                    color+=daylight*sun.shadowAttenuation*(1.-.82*reflectedTrees)*glintPath
                        *SkyCrests(rippleSpace+eddy*.46+float2(7.7,-3.1),time)*half3(1.27,1.08,.70);
                }
                // In a district overview individual ripples are subpixel. Broader wind lanes
                // carry the reflected sky between the tree-lined bank and open water.
                if(_LakeShore>.5 && clearDay>.001)
                {
                    half2 breeze=SAMPLE_TEXTURE2D(_WindField,sampler_WindField,wind/float2(144.,93.)+eddy*.09).rg;
                    half lane=smoothstep(.36,.82,breeze.r+.14*sin(wind.x*.031+breeze.g*5.2));
                    half ridge=pow(saturate(.5+.5*sin(dot(p,side)*.20+dot(p,towards)*1.35+breeze.g*8.+time*.33)),9);
                    half facing=saturate(dot(reflect(-sun.direction,n),view));
                    color+=clearDay*offshore*sun.shadowAttenuation*lane*(.018+ridge*(.042+.12*facing))*half3(.68,.91,1.);
                }
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
