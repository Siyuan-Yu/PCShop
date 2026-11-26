Shader "PCShop/RoomJunction"
{
    Properties { _Tint("交接处颜色", Color)=(.15,.11,.08,.25) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 position:SV_POSITION; half2 uv:TEXCOORD0; };
            V vert(A a){ V o; o.position=TransformObjectToHClip(a.vertex.xyz); o.uv=a.uv; return o; }
            half4 frag(V i):SV_Target
            {
                half f=saturate(i.uv.y);
                return half4(_Tint.rgb,_Tint.a*f*f*f);
            }
            ENDHLSL
        }
    }
}
