Shader "PCShop/Old Quarter Footmark"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float3 position:POSITION; };
            struct V { float4 clipPos:SV_POSITION; float2 local:TEXCOORD0; };
            V Vert(A a)
            {
                V v;
                v.clipPos = TransformObjectToHClip(a.position);
                v.local = a.position.xz*2;
                return v;
            }
            half4 Frag(V v):SV_Target
            {
                float r = length(v.local);
                float aa = max(fwidth(r),.025);
                half contact = (1-smoothstep(.12,.87,r))*.48;
                half rim = (1-smoothstep(.018,.018+aa,abs(r-.79)))*
                           smoothstep(-.16,.42,v.local.y+.30*v.local.x)*.24;
                half alpha = contact+rim;
                half3 color = lerp(half3(.027,.022,.018),half3(.24,.17,.082),rim/max(alpha,.001));
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
