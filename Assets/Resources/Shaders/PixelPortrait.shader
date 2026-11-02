Shader "PCShop/PixelPortrait"
{
    Properties
    {
        [PerRendererData] _MainTex("Portrait",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 world:TEXCOORD1; };
            sampler2D _MainTex;float4 _MainTex_TexelSize;fixed4 _Color;float4 _ClipRect;
            v2f vert(appdata a){v2f o;o.world=a.vertex;o.vertex=UnityObjectToClipPos(a.vertex);o.uv=a.uv;o.color=a.color*_Color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float2 grid=float2(144*_MainTex_TexelSize.z/_MainTex_TexelSize.w,144);
                float2 uv=(floor(i.uv*grid)+.5)/grid;
                fixed4 c=tex2D(_MainTex,uv);
                c.rgb=floor(c.rgb*23+.5)/23;
                c*=i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
