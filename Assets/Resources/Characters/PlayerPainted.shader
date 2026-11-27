Shader "PCShop/PlayerPainted"
{
 Properties
 {
  _BaseColor ("底色", Color) = (1,1,1,1)
  _Smoothness ("兼容服装参数", Float) = 0
  _Grain ("细颗粒", Float) = 0.009
  _SurfaceKind ("表面种类", Float) = 0
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Pass
  {
   Name "Painted"
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;
   half _Smoothness, _Grain, _SurfaceKind;
   CBUFFER_END
   struct A { float4 p:POSITION; float3 n:NORMAL; float2 layer:TEXCOORD1; float3 rest:TEXCOORD2; float3 restN:TEXCOORD3; };
   struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; half3 n:TEXCOORD1; float3 o:TEXCOORD2; half fog:TEXCOORD3; half2 layer:TEXCOORD4; half3 restN:TEXCOORD5; };
   V Vert(A a)
   {
    V v;v.w=TransformObjectToWorld(a.p.xyz);v.n=TransformObjectToWorldNormal(a.n);
    float pixelWorld=2*TransformWorldToHClip(v.w).w/max(abs(UNITY_MATRIX_P._m11)*_ScreenParams.y,1);
    float width=max(.0065,min(.018,pixelWorld*.7));
    v.w+=v.n*(a.layer.x*width);v.p=TransformWorldToHClip(v.w);
    v.layer=a.layer;v.o=a.rest;v.restN=a.restN;v.fog=ComputeFogFactor(v.p.z);return v;
   }
   half SoftStroke(float distance,float radius){return exp2(-distance*distance/max(radius*radius,.000001));}
   half GarmentPigment(float3 p,half3 n)
   {
    half front=smoothstep(.12h,.65h,n.z),amount=0;
    float y=p.y,x=abs(p.x);
    if(_SurfaceKind==2 && x>.15 && y<1.40)
    {
     float center=lerp(.238,.255,smoothstep(.82,.88,y));
     center=lerp(center,.224,smoothstep(.88,.955,y));
     center=lerp(center,.214,smoothstep(.955,1.035,y));
     center=lerp(center,.225,smoothstep(1.035,1.110,y));
     center=lerp(center,.193,smoothstep(1.110,1.200,y));
     center=lerp(center,.183,smoothstep(1.200,1.285,y));
     center=lerp(center,.165,smoothstep(1.285,1.355,y));
     amount=front*(SoftStroke(x-center-.018,.014)*.16-SoftStroke(x-center+.012,.018)*.09);
    }
    else if(_SurfaceKind==5)
    {
     // 翻领下方、门襟和袖内的大色块，取自衣物静止坐标，动作时不会滑动。
     half inner=SoftStroke(x-.047,.012)*smoothstep(.78,.9,y)*(1-smoothstep(1.12,1.20,y));
     half arm=SoftStroke(x-(.185+.11*(1.0-y)),.019)*smoothstep(.77,.90,y)*(1-smoothstep(1.08,1.17,y));
     amount=front*(inner*.17+arm*.16);
    }
    else if(_SurfaceKind==6)
    {
     amount=front*.08*SoftStroke(x-.165,.026)*smoothstep(.83,1.1,y);
     amount+=smoothstep(.20,.22,x)*SoftStroke(y-.705,.003)*.19;
    }
    else if(_SurfaceKind==7)
    {
     float edge=x/lerp(.175,.105,smoothstep(.82,1.03,y));
     amount=front*(pow(saturate(edge),4)*.15+SoftStroke(x-.074,.022)*(1-smoothstep(.71,.83,y))*.08);
    }
    else if(_SurfaceKind==8)
    {
     half tuck=SoftStroke(x-(.119-.36*(y-.82)),.009)*SoftStroke(y-.84,.063);
     half edge=SoftStroke(x-.129,.025)*smoothstep(.78,.94,y)*(1-smoothstep(1.05,1.15,y));
     amount=front*(tuck*.24+edge*.12);
    }
    else if(_SurfaceKind==9)
    {
     half fold=pow(saturate(.5+.5*cos(atan2(p.x,p.z)*10)),7);
     amount=fold*.21*smoothstep(.43,.50,y)*(1-smoothstep(.76,.80,y));
    }
    else if(_SurfaceKind==10)
    {
     amount=front*.12*SoftStroke(x-.044,.017)*(1-smoothstep(.67,.77,y));
    }
    return 1-amount;
   }
   half4 Frag(V v):SV_Target
   {
    half3 n=normalize(v.n),view=SafeNormalize(GetWorldSpaceViewDir(v.w));
    Light key=GetMainLight(TransformWorldToShadowCoord(v.w+n*.012));
    if(v.layer.x>.5h)
    {
     half light=saturate(max(key.color.r,max(key.color.g,key.color.b)));
     return half4(MixFog(half3(.0467h,.0232h,.0176h)*lerp(.35h,1.0h,light),v.fog),1);
    }
    half ndl=dot(n,key.direction);
    half band=smoothstep(.20h,.63h,ndl);
    half3 base=_BaseColor.rgb;
    if(_SurfaceKind==13)
    {
     // 小花纹沿衣服静止坐标随骨骼运动；保持大色块可读，远处自动淡出。
     float2 q=v.o.xy*26;float2 cell=floor(q);float2 flower=frac(q+float2(fmod(cell.y,2)*.5,0))-.5;
     half petal=1-smoothstep(.13,.20,length(flower)*(1+.24*cos(atan2(flower.y,flower.x)*5)));
     half leaf=1-smoothstep(.07,.12,length((flower-float2(.18,-.16))*float2(.7,1.7)));
     half fade=1-saturate(max(length(ddx(q)),length(ddy(q)))*1.8);
     base=lerp(base,base*half3(1.05,.60,.63),petal*fade*.55);
     base=lerp(base,base*half3(.55,.82,.55),leaf*fade*.45);
    }
    if(_SurfaceKind==14)
    {
     float2 q=frac(v.o.xy*float2(13,18))-.5;
     half diamond=1-smoothstep(.30,.34,abs(q.x)+abs(q.y));
     base*=lerp(1,.64,diamond*.65);
    }
    if(_SurfaceKind==11 && v.layer.y>.5h)base=half3(.0343h,.0296h,.0356h);
    if(v.layer.y>1.5h)base=half3(.0409h,.0296h,.0232h);
    bool skin=_SurfaceKind==1 || _SurfaceKind==12;
    half3 shadowTone=_SurfaceKind==12 ? half3(.98h,.95h,.92h) : skin ? half3(.86h,.78h,.73h) : half3(.62h,.62h,.69h);
    half3 pigment=lerp(base*shadowTone,base,band);
    pigment=lerp(pigment,base*1.10h,smoothstep(.55h,.85h,ndl)*.55h);
    half daylight=saturate(max(key.color.r,max(key.color.g,key.color.b)));
    half3 ambient=max(SampleSH(n),half3(.025h,.026h,.032h));
    half3 illumination=lerp(ambient*.75h,lerp(half3(1,1,1),min(key.color,1.15h),.28h),daylight);
    half3 color=pigment*illumination*lerp(.80h,1.0h,key.shadowAttenuation);
    color*=GarmentPigment(v.o,normalize(v.restN));
    half facingShape=saturate(dot(n,view));
    color*=1-smoothstep(.05h,.95h,1-facingShape)*(skin ? .07h : .12h);
    half broad=sin(v.o.x*27+sin(v.o.y*13))*sin(v.o.y*19+v.o.z*23);
    color*=1+broad*(skin ? .012h : .028h);
    if(_SurfaceKind==4)color*=lerp(.86h,1.06h,smoothstep(.018h,.11h,v.o.y));
    #if defined(_ADDITIONAL_LIGHTS)
    for(uint i=0u;i<min(GetAdditionalLightsCount(),4u);i++)
    {
     Light lamp=GetAdditionalLight(i,v.w);
     color+=base*lamp.color*min(lamp.distanceAttenuation,2.0h)*saturate(dot(n,lamp.direction)*.6h+.4h)*.40h;
    }
    #endif
    // 几何外轮廓另用背面壳；这里仅保留很淡的转折色，避免两道黑边。
    half facing=saturate(dot(n,view));
    half aa=max(fwidth(facing)*.8h,.018h);
    half inkCoverage=1-smoothstep(.17h-aa,.17h+aa,facing);
    half3 ink=min(color*.44h,half3(.024h,.015h,.011h)*max(daylight,.3h));
    color=lerp(color,ink,inkCoverage*.20h);
    float3 grain=v.o*180;
    half fade=1-saturate(max(length(ddx(grain)),length(ddy(grain)))*.4);
    color*=1+sin(grain.x)*sin(grain.y)*sin(grain.z)*_Grain*fade;
    return half4(MixFog(color,v.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
