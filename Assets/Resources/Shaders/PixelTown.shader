Shader "PCShop/PixelTown"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _PixelGrid;
            half3 Sample(float2 uv){return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv).rgb;}
            half4 Frag(Varyings i):SV_Target
            {
                float2 grid=_PixelGrid.xy;
                float2 pixel=floor(i.texcoord*grid);
                float2 uv=(pixel+.5)/grid,step=1/grid;
                half3 c=Sample(uv);
                half3 right=Sample(uv+float2(step.x,0)),down=Sample(uv+float2(0,step.y));
                float edge=max(length(c-right),length(c-down));
                // A restrained blue ink edge, not a thick outline around every texture fleck.
                float ink=smoothstep(.15,.55,edge)*.36;
                c=lerp(c,half3(.035,.065,.14),ink);
                c=LinearToSRGB(max(c,0));
                float checker=fmod(pixel.x+pixel.y,2)*2-1;
                c=floor(saturate(c)*31+.5+checker*.015)/31;
                return half4(SRGBToLinear(c),1);
            }
            ENDHLSL
        }
    }
}
