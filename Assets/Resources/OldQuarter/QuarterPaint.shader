Shader "PCShop/Old Quarter Paint"
{
    Properties
    {
        [MainTexture] _BaseMap("Paint", 2D) = "white" {}
        _WindowMap("Lit windows",2D)="black" {}
        [NoScaleOffset] _Elevations("Painted elevations / window light",2D)="white" {}
        [NoScaleOffset] _WearMap("Entrance wear",2D)="black" {}
        _TurfStrokes("Garden pigment detail / coverage",2D)="gray" {}
        [NoScaleOffset] _MeadowMap("Residential meadow pigment / coverage",2D)="black" {}
        _LineTint("Road paint", Color) = (1,1,1,1)
        [Enum(Off,0,Building,1,Grass,2,Asphalt,3,Paving,4,RoadPaint,5)] _BrushSurface("Brush material", Float) = 0
        _Patch("Feather outer lawn", Float) = 0
        [Toggle] _RouteGrass("Daylight lawn along the old street",Float)=0
        _Ink("Relief", Range(0,1)) = 0.8
        _PaintGrouping("Painted edge and value grouping", Range(0,1)) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
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
            TEXTURE2D(_WindowMap); SAMPLER(sampler_WindowMap);
            TEXTURE2D(_Elevations); SAMPLER(sampler_Elevations);
            TEXTURE2D(_WearMap); SAMPLER(sampler_WearMap);
            TEXTURE2D(_MeadowMap); SAMPLER(sampler_MeadowMap);
            TEXTURE2D(_TurfStrokes); SAMPLER(sampler_TurfStrokes);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _TurfStrokes_ST;
                half4 _LineTint;
                float _Patch, _Ink, _Cull, _BrushSurface, _PaintGrouping, _RouteGrass;
            CBUFFER_END
            #include "QuarterBrush.hlsl"
            struct Attr { float4 position:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float4 frontage:TEXCOORD2; };
            struct Var { float4 position:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3; float3 local:TEXCOORD4; half3 objectNormal:TEXCOORD5; float4 frontage:TEXCOORD6; };
            Var Vert(Attr a)
            {
                Var v; VertexPositionInputs p=GetVertexPositionInputs(a.position.xyz);
                v.position=p.positionCS; v.world=p.positionWS; v.normal=TransformObjectToWorldNormal(a.normal);
                v.frontage=a.frontage; v.local=a.position.xyz; v.objectNormal=a.normal; v.uv=a.uv; v.fog=ComputeFogFactor(p.positionCS.z); return v;
            }
            // The atlas uses a padded 8x8 grid. Accent only the contact edge on projecting
            // trim and canvas. Outlining every mesh face would draw the roof slab seams.
            half TrimInk(float2 uv, half3 normal, float3 local)
            {
                float2 atlas = uv*8;
                int2 cell = int2(floor(atlas));
                int tile = cell.x + 8*(7-cell.y);
                float2 face = frac(atlas);
                half vertical = 1-smoothstep(.36,.63,abs(normal.y));
                float bottom = max(face.y-.018,0);
                half weight = 0;
                if (tile == 55 || tile == 57 || tile == 31) // Window and roof trims
                    weight = vertical*(tile == 57 ? .94 : .86);
                else if (tile == 27 || tile == 28) // Canvas has a separate outer lip
                {
                    bottom = lerp(max(.982-face.y,0), bottom, vertical);
                    weight = .72;
                }
                float footprint = max(fwidth(atlas.y),.0001);
                half stroke = 1-smoothstep(.14,1.38,bottom/footprint);
                if (tile == 55 || tile == 57)
                    stroke = max(stroke, .50*(1-smoothstep(.06,.89,face.y)));
                if ((tile == 55 || tile == 54) && normal.y > .85)
                {
                    // Cement caps and hoods have drawn borders on the upper face. Leave
                    // some of the highlight visible across the width of a very thin ledge.
                    float2 across = min(face-.018,.982-face)/max(fwidth(atlas),.0001);
                    float narrow = max(fwidth(atlas.x),fwidth(atlas.y));
                    weight = .93*(1-smoothstep(.24,.90,narrow));
                    stroke = 1-smoothstep(.15,1.27,min(across.x,across.y));
                }
                float2 run = float2(dot(local.xz,float2(.83,.71)), local.y);
                half pressure = .72 + .28*BrushField(run*float2(2.7,1.9));
                return weight*stroke*pressure;
            }
            half4 Frag(Var v):SV_Target
            {
                Light main = GetMainLight();
                half power = max(main.color.r,max(main.color.g,main.color.b));
                half grouping = _PaintGrouping*lerp(.22,1,smoothstep(.45,.98,power));
                if (_Patch > .5)
                {
                    float2 border=min(v.uv,1-v.uv);
                    float edge=saturate(min(border.x,border.y)*10);
                    float2 grain=floor(v.world.xz*15);
                    clip(edge-frac(52.9829189*frac(dot(grain,float2(.06711056,.00583715)))));
                }
                float2 surfaceUV=TRANSFORM_TEX(v.uv,_BaseMap);
                if (_RouteGrass>.5)
                    surfaceUV=v.world.xz/8.5 + .08*float2(BrushField(v.world.xz*.12),BrushField(v.world.xz*.09+15));
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,surfaceUV).rgb;
                if (_BrushSurface==2)
                {
                    float2 rotated=float2(-surfaceUV.y,surfaceUV.x)+float2(.28,.63);
                    half3 other=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,rotated).rgb;
                    half turn = smoothstep(.38,.66,BrushField(v.world.xz*.19));
                    albedo=lerp(albedo,other,_RouteGrass>.5 ? .40h*turn : turn);
                    if (_RouteGrass>.5)
                    {
                        half3 pigment = clamp(albedo / half3(.275h,.242h,.058h),.68h,1.44h);
                        half fresh = smoothstep(.22h,.78h,BrushField(v.world.xz*.057)+.16h*(BrushField(v.world.xz*.19)-.5h));
                        half3 wash = lerp(half3(.253h,.227h,.069h),half3(.248h,.266h,.087h),fresh);
                        half4 strokes = half4(.5h,.5h,.5h,0);
                        if (_Patch > .5)
                        {
                            float2 garden = v.frontage.xy;
                            half4 meadow = SAMPLE_TEXTURE2D(_MeadowMap,sampler_MeadowMap,saturate(garden));
                            float2 border = min(garden,1-garden);
                            half coverage = saturate(min(border.x,border.y)*36);
                            wash = lerp(wash,meadow.rgb,coverage*meadow.a*.90h);
                            half4 localStrokes = SAMPLE_TEXTURE2D(_TurfStrokes,sampler_TurfStrokes,TRANSFORM_TEX(garden,_TurfStrokes));
                            strokes = lerp(strokes,localStrokes,coverage);
                        }
                        half3 tileRelief = pow(pigment,lerp(1.72h,.77h,strokes.a));
                        albedo = wash * tileRelief * exp2((strokes.rgb-.5h)*2.28h);
                        // Damp growth reads greener; the drier blades keep a warm ochre tip.
                        half3 leaf = albedo * half3(.75h,1.045h,1.16h);
                        leaf.b += .037h * Luminance(albedo);
                        albedo = lerp(albedo,leaf,.62h + .34h*fresh);
                    }
                }
                half lamp=0;
                if (_BrushSurface==1 && v.uv.x>1.5)
                {
                    half4 wall=SAMPLE_TEXTURE2D(_Elevations,sampler_Elevations,v.uv-float2(2,0));
                    albedo=wall.rgb;
                    lamp=wall.a;
                    if (grouping > 0)
                    {
                        half3 pigment = BrushSurface(albedo,v.world,v.local,normalize(v.objectNormal),1);
                        albedo = lerp(albedo,pigment,grouping);
                        // Pick up existing drawn edges on their dark side, across one output pixel.
                        float2 uv = v.uv-float2(2,0);
                        float2 radius = clamp(fwidth(uv)*.92, .00028, .0024);
                        half3 right = SAMPLE_TEXTURE2D(_Elevations,sampler_Elevations,uv+float2(radius.x,0)).rgb;
                        half3 above = SAMPLE_TEXTURE2D(_Elevations,sampler_Elevations,uv+float2(0,radius.y)).rgb;
                        half contrast = max(Luminance(right),Luminance(above)) - Luminance(albedo);
                        half ink = smoothstep(.026,.125,contrast);
                        float wallU = frac(uv.x*2);
                        float cornerPx = max(min(wallU,1-wallU)-.003,0) / max(2*fwidth(uv.x),.0001);
                        ink = max(ink,.86*(1-smoothstep(.22,1.55,cornerPx)));
                        albedo *= lerp(half3(1,1,1),half3(.44,.42,.39),ink*grouping);
                    }
                }
                else
                {
                    half3 brush=BrushSurface(albedo,v.world,v.local,normalize(v.objectNormal),_BrushSurface==5 ? 3 : _BrushSurface);
                    albedo=lerp(albedo,brush,_RouteGrass>.5?.32:1);
                    if (_BrushSurface==1)
                    {
                        lamp=SAMPLE_TEXTURE2D(_WindowMap,sampler_WindowMap,v.uv).r;
                        if (grouping > 0)
                            albedo *= lerp(1,.29,TrimInk(v.uv,normalize(v.objectNormal),v.local)*grouping);
                    }
                }
                if (_BrushSurface==4)
                {
                    float4 delta=abs(v.frontage-.5);
                    float2 uv = max(delta.x,delta.y)<max(delta.z,delta.w) ? v.frontage.xy : v.frontage.zw;
                    half2 wear=SAMPLE_TEXTURE2D(_WearMap,sampler_WearMap,saturate(uv)).rg;
                    albedo*=lerp(half3(1,1,1),half3(1.36,1.23,1.09),wear.r);
                    albedo*=lerp(half3(1,1,1),half3(.55,.62,.57),wear.g);
                }
                if (_BrushSurface==5)
                {
                    float2 p=v.world.xz;
                    half abrasion=BrushField(p*3.1)*.64+BrushField(p*16)*.36;
                    albedo=lerp(albedo,_LineTint.rgb,.26+.70*smoothstep(.24,.57,abrasion));
                }
                float3 receiver=v.world;
                if (_BrushSurface==2)
                    receiver.xz+=(float2(BrushField(receiver.xz*4.6),BrushField(receiver.xz*4.6+5))-.5)*.095;
                Light sun=GetMainLight(TransformWorldToShadowCoord(receiver));
                half facing=dot(normalize(v.normal),sun.direction);
                half plane=smoothstep(-.10,.60,facing);
                half lit=lerp(.66,1.0,plane*_Ink+plane*(1-_Ink)*.25);
                half castFloor = (_BrushSurface==3 || _BrushSurface==5) ? .79 : .62;
                half cast=lerp(castFloor,1.0,sun.shadowAttenuation);
                half3 tint=lerp(half3(.86,.88,.96),sun.color/max(.001,power),.30);
                half3 result=albedo*lit*cast*tint*clamp(.20+power*.70,.18,1.12);
                if (_BrushSurface==3) result*=half3(1.34,1.26,1.22);
                if (_BrushSurface==1)
                    result *= lerp(half3(1,1,1), half3(1.33,1.24,1.13), grouping);
                if (_BrushSurface==1 && grouping>0)
                {
                    half shade = (1-plane)*(1-smoothstep(.4,.8,abs(v.normal.y)));
                    result *= lerp(half3(1,1,1),half3(.89,.87,.88),shade*grouping);
                    if (v.uv.x <= 1)
                    {
                        int2 cell = int2(floor(v.uv*8));
                        int tile = cell.x+8*(7-cell.y);
                        if (tile == 40) // Bark pigment follows the circumference and forks of the tree.
                        {
                            half wrap = smoothstep(-.68h,.72h,facing);
                            half3 barkLight = lerp(half3(.63h,.68h,.74h),half3(1.10h,1.04h,.97h),wrap);
                            result *= lerp(half3(1,1,1),barkLight,grouping);
                        }
                        if (tile == 33) // Galvanized metal: cool body and a warmer lip toward the sun.
                        {
                            half turn = smoothstep(-.14,.15,facing)*.55+smoothstep(.43,.61,facing)*.45;
                            half3 metal = lerp(half3(.44,.54,.63),half3(1.30,1.27,1.20),turn);
                            half dry = BrushField(float2(v.local.x+.47*v.local.z,v.local.y)*5.3);
                            result *= lerp(half3(1,1,1),metal*(.92+.16*dry),grouping);
                        }
                        if ((tile == 43 || tile == 50) && v.normal.y > .88)
                        {
                            // The roof mesh is built in bays. Draw the felt joints directly on each bay
                            // so their spacing and width survive static batching and camera rotation.
                            float2 sheet = frac(v.uv * 8) * (tile == 43 ? 3.0 : 2.0);
                            float2 seam = abs(frac(sheet+.5)-.5);
                            float2 edge = smoothstep(float2(.015,.015),.019+fwidth(sheet)*.82,seam);
                            half joint = 1-min(edge.x,edge.y);
                            result *= 1-joint*(tile == 43 ? .17h : .21h)*grouping;
                        }
                        if (tile == 36 || tile == 43 || tile == 50) // Flat masonry, clay tile and roofing felt.
                        {
                            half occlusion = smoothstep(.08,.55,1-sun.shadowAttenuation);
                            half3 roofShade = tile == 36 ? half3(1.34,1.32,1.30) : half3(.76,.74,.72);
                            result *= lerp(half3(1,1,1),roofShade,occlusion*grouping);
                        }
                    }
                }
                if (_BrushSurface==2)
                {
                    // Grass in the shadow still sees the sky. Suppress warm sunlight while keeping
                    // reflected blue in the dark blades. Fade the daylight palette as the sun sets.
                    half daylight = smoothstep(.42h,.95h,power);
                    half3 shade=_RouteGrass>.5 ? lerp(half3(.65,.75,.83),half3(.41,.56,1.16),daylight) : half3(.42,.66,.92);
                    result *= lerp(half3(1,1,1),shade,saturate((1-sun.shadowAttenuation)*1.38));
                    // Leave calm ground around scattered grass marks. Keep the cast shadow as one mass.
                    if (grouping > 0)
                    {
                        half3 quiet = SRGBToLinear(_RouteGrass>.5 ? half3(.51,.56,.31) : half3(.49,.47,.235));
                        half open = smoothstep(.28,.73,BrushField(v.world.xz*.40));
                        half coverage = (.32 + .44*open)*lerp(.48,1,sun.shadowAttenuation)*grouping*(_RouteGrass>.5?.12:1);
                        half3 flatten = lerp(albedo,quiet,coverage);
                        result *= flatten/max(albedo,half3(.012,.012,.012));
                        if (_RouteGrass < .5)
                        {
                            float2 ground = v.world.xz;
                            half patch = smoothstep(.72,.90,BrushField(ground*1.1)+.20*BrushField(ground*3.1));
                            half tips = BrushTip(ground*float2(4.1,5.6),.25,.41,.62);
                            result *= 1 + patch*tips*.29h*grouping;
                        }
                    }
                }
                result+=half3(1.35,.64,.18)*lamp*saturate((.95-power)*1.8);
                return half4(MixFog(result,v.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
