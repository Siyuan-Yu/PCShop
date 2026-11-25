#ifndef PCSHOP_CROWN_FACING
#define PCSHOP_CROWN_FACING
// TEXCOORD2 stores signed physical offsets within each clump, not lightmap coordinates.
float3 CrownPosition(float3 posOS,float4 tangentOS,float2 offset,float3 toward,float verticalSign)
{
    float3 world=TransformObjectToWorld(posOS);
    float3 tangent=TransformObjectToWorldDir(tangentOS.xyz,false);
    float width=max(.0001,length(tangent));
    float height=length(TransformObjectToWorldDir(float3(0,1,0),false));
    float3 across=cross(float3(0,1,0),toward);
    across=dot(across,across)>.0001?normalize(across):float3(1,0,0);
    float3 lift=normalize(cross(toward,across))*verticalSign;
    return world+(across*width-tangent)*offset.x+
        (lift-float3(0,verticalSign,0))*offset.y*height*.87;
}
#endif
