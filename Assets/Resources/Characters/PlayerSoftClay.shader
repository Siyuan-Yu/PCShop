Shader "PETSHOP/PlayerSoftClay"
{
 Properties
 {
  _BaseColor ("Color", Color) = (1,1,1,1)
  _Smoothness ("Surface smoothness", Range(0,1)) = .15
  _Grain ("Fabric grain", Range(0,.1)) = 0
  _SurfaceKind ("Surface: cloth, skin, hair, eye, leather", Float) = 0
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Pass
  {
   Name "SoftClay"
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;
   half _Smoothness;
   half _Grain;
   half _SurfaceKind;
   CBUFFER_END
   float4 _OldQuarterFigureZone;
   struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
   struct Varyings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; float3 positionOS:TEXCOORD2; };
   Varyings Vert(Attributes input)
   {
    Varyings output;
    output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS=TransformObjectToWorldNormal(input.normalOS);
    output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
    output.positionOS=input.positionOS.xyz;
    return output;
   }
   half3 SurfaceLight(half3 normal, half3 viewDir, Light light, half skin, half hair, half eye, half leather)
   {
    half nDotL=dot(normal,light.direction);
    half wrap=lerp(.18h,.48h,skin);
    half diffuse=saturate((nDotL+wrap)/(1.0h+wrap));
    half3 halfway=SafeNormalize(light.direction+viewDir);
    half nDotH=saturate(dot(normal,halfway));
    // Wide, quiet reflections for skin and hair; the eye has a small clear catchlight.
    half exponent=lerp(14.0h,100.0h,_Smoothness);
    exponent=lerp(exponent,28.0h,hair);
    exponent=lerp(exponent,150.0h,eye);
    half specStrength=.018h+skin*.065h+hair*.055h+eye*.24h+leather*.055h;
    half specular=pow(nDotH,exponent)*specStrength;
    half shadow=lerp(.58h,1.0h,light.shadowAttenuation);
    half attenuation=min(light.distanceAttenuation,2.0h)*shadow;
    half3 diffuseColor=_BaseColor.rgb*diffuse;
    // A restrained warm terminator keeps the miniature's skin soft without emissive skin.
    diffuseColor+=_BaseColor.rgb*half3(.045h,.012h,.004h)*skin*saturate(1.0h-abs(nDotL)*2.0h);
    half3 specColor=lerp(half3(1,1,1),half3(1,.84h,.68h),hair*.45h+leather*.25h);
    return (diffuseColor*.90h+specular*specColor)*light.color*attenuation;
   }
   half4 Frag(Varyings input):SV_Target
   {
    half3 normal=normalize(input.normalWS);
    half3 viewDir=SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
    half skin=1.0h-step(.5h,abs(_SurfaceKind-1.0h));
    half hair=1.0h-step(.5h,abs(_SurfaceKind-2.0h));
    half eye=1.0h-step(.5h,abs(_SurfaceKind-3.0h));
    half leather=1.0h-step(.5h,abs(_SurfaceKind-4.0h));
    half cloth=1.0h-step(.5h,_SurfaceKind);
    Light key=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
    half daylight=saturate(max(max(key.color.r,key.color.g),key.color.b));
    half3 ambient=max(SampleSH(normal),half3(.035h,.035h,.04h));
    half sky=saturate(normal.y*.5h+.5h);
    // The world light drives the form. This small fill only prevents unreadable silhouettes.
    half3 fillDir=normalize(viewDir+half3(-.35h,.65h,0));
    half fill=saturate(dot(normal,fillDir));
    half3 ambientLight=ambient*.70h+daylight*(half3(.09h,.10h,.13h)+sky*.07h+fill*.14h);
    half3 color=_BaseColor.rgb*ambientLight+SurfaceLight(normal,viewDir,key,skin,hair,eye,leather);
    #if defined(_ADDITIONAL_LIGHTS)
    uint lightCount=min(GetAdditionalLightsCount(),4u);
    for(uint i=0u;i<lightCount;i++)
    {
     Light lamp=GetAdditionalLight(i,input.positionWS);
     color+=SurfaceLight(normal,viewDir,lamp,skin,hair,eye,leather)*.45h;
    }
    #endif
    // Fine matte weave is object-space and fades with pixel footprint, so distant cloth is stable.
    float3 weave=input.positionOS*560.0;
    float pixelSpan=max(length(ddx(weave)),length(ddy(weave)));
    half fade=1.0h-saturate(pixelSpan*.28);
    half threads=sin(weave.x)*sin(weave.y)*sin(weave.z);
    color*=1.0h+_Grain*threads*fade;
    half grazing=pow(1.0h-saturate(dot(normal,viewDir)),3.0h);
    color+=_BaseColor.rgb*cloth*grazing*.035h*daylight;
    // Keep the street figures in the same two or three value masses as the scenery.
    // The zone is world-space so portrait cameras and characters elsewhere retain their lighting.
    if (_OldQuarterFigureZone.z > 0)
    {
     half influence = _OldQuarterFigureZone.z*(1-smoothstep(24,40,distance(input.positionWS.xz,_OldQuarterFigureZone.xy)));
     half ndl = dot(normal,key.direction);
     half form = .39h + .31h*smoothstep(-.25h,-.14h,ndl) + .25h*smoothstep(.38h,.51h,ndl);
     half3 dye = lerp(_BaseColor.rgb,Luminance(_BaseColor.rgb).xxx,.20h);
     dye *= lerp(half3(1,1,1),half3(.69h,.73h,.76h),cloth);
     dye *= 1-.29h*hair;
     half3 painted = dye*form*lerp(.59h,1.0h,key.shadowAttenuation)*min(key.color,half3(1.14h,1.14h,1.14h));
     // The dark-side contour is wider than the sunlit one. Pixel derivatives keep
     // a sleeve or the edge of a cheek legible at the street camera's scale.
     half facing = saturate(dot(normal,viewDir));
     half edgeWidth = clamp(fwidth(facing)*1.15h,.12h,.31h);
     half pressure = lerp(.47h,.26h,smoothstep(-.18h,.55h,ndl));
     half ink = (1-smoothstep(pressure-edgeWidth,pressure+edgeWidth,facing))*(1-.28h*skin);
     painted = lerp(painted,min(painted*.31h,half3(.029h,.023h,.019h)),ink);
     color = lerp(color,painted,influence*(.96h-.47h*eye));
    }
    return half4(color,1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
