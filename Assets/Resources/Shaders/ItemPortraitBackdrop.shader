Shader "PCShopSell/UI/ItemPortraitBackdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Item Atlas", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Vertex
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Interpolated
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _ClipRect;

            Interpolated vert(Vertex input)
            {
                Interpolated output;
                output.position = UnityObjectToClipPos(input.position);
                output.localPosition = input.position.xy;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(Interpolated input) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, input.uv);
                float3 rgb = sample.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = LinearToGammaSpace(rgb);
                #endif

                // The existing atlas sheets share a dark navy backdrop. Match its hue
                // and value together so lighter blues and neutral dark objects stay visible.
                float2 backdrop = rgb.b * float2(32.0 / 72.0, 49.0 / 72.0);
                float chroma = length(rgb.rg - backdrop);
                float hueMatch = 1 - smoothstep(0.018, 0.043, chroma);
                float valueMatch = smoothstep(0.12, 0.19, rgb.b) * (1 - smoothstep(0.30, 0.34, rgb.b));
                fixed4 color = sample * input.color;
                color.a *= 1 - hueMatch * valueMatch;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.localPosition, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
