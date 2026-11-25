// Old-quarter pigment follows the surface as the camera rotates.
// World units set the brush size. The footprint of the pixel fades sub-pixel marks.
float BrushHash(float2 p)
{
    float3 q = frac(float3(p.xyx) * float3(.1031,.1030,.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

float BrushField(float2 pos)
{
    float2 cell = floor(pos), f = frac(pos);
    f = f * f * (3 - 2 * f);
    return lerp(lerp(BrushHash(cell), BrushHash(cell + float2(1,0)), f.x),
                lerp(BrushHash(cell + float2(0,1)), BrushHash(cell + 1), f.x), f.y);
}

float BrushTip(float2 pos, float width, float extent, float density)
{
    float2 cell = floor(pos);
    float seed = BrushHash(cell + 4.13);
    float2 p = frac(pos) - .5 - .24 * (float2(BrushHash(cell-1.7), BrushHash(cell+8.3)) - .5);
    p.x -= p.y * (seed - .30) * .57;
    float2 q = abs(p) / (float2(width,extent) * lerp(.74,1.07,seed));
    float contour = max(q.y, q.x + .67*q.y);
    float footprint = max(fwidth(pos.x)/width,fwidth(pos.y)/extent);
    float aa = clamp(footprint*.5,.07,.85);
    return (1-smoothstep(1-aa,1+aa,contour)) * step(1-density,seed)
         * (1-smoothstep(1.1,2.7,footprint));
}

half3 BrushSurface(half3 base, float3 world, float3 local, half3 normal, float kind)
{
    if (kind > 1.5 && kind < 2.5)
    {
        float2 uv = world.xz;
        float clump = BrushField(uv*.62 + 19) + .24 * BrushField(uv*2.2);
        float islands = smoothstep(.53,.83,clump);
        float blades = BrushTip(float2(uv.x+uv.y*.32,uv.y)*float2(8.7,5.6),.16,.41,.69);
        float bristles = BrushTip(float2(uv.x-uv.y*.15,uv.y)*float2(13,8.3)+3.27,.19,.39,.43);
        float deep = smoothstep(.55,.86, BrushField(uv*.33+3.3) + .14*clump);
        base *= lerp(half3(.99,1.015,.96), half3(.78,.86,.70), deep);
        base *= 1 - .20 * islands * blades;
        base += base * half3(.30,.22,.06) * bristles * islands;
    }
    else if (kind > 2.5 && kind < 3.5)
    {
        base *= half3(.79,.835,.88) * (1 + .035*(BrushField(world.xz*.9)-.5));
    }
    else if (kind > 3.5)
    {
        float2 p = world.xz;
        float scuff = BrushField(p*.91+7);
        float dry = BrushTip(p*float2(6,13),.27,.36,.38);
        base *= 1 + .10*(scuff-.50) + dry*.12*smoothstep(.48,.85,scuff);
    }
    else if (kind > .5)
    {
        float2 wall = abs(normal.x) > abs(normal.z) ? local.zy : local.xy;
        float vertical = 1 - smoothstep(.50,.78,abs(normal.y));
        float block = BrushField(wall*float2(1.9,.60));
        float streak = BrushTip(wall*float2(7,1.7),.18,.42,.72);
        float exposed = smoothstep(.09,.28,dot(base,half3(.25,.55,.20)));
        base *= 1 + vertical * exposed * (.08*(block-.5)-.20*streak*smoothstep(.38,.76,block));
        float roof = BrushField(local.xz*.95) - .5;
        base *= 1 + (1-vertical) * .12*roof;
    }
    return base;
}
