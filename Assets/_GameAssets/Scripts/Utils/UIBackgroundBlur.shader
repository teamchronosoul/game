Shader "UI/BackgroundBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _BlurSize ("Blur Size", Range(0, 6)) = 1.5
        _Opacity ("Opacity", Range(0, 1)) = 1

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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

        GrabPass
        {
            "_UIBlurGrabTex"
        }

        Pass
        {
            Name "UIBlur"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 grabPos       : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            sampler2D _UIBlurGrabTex;
            float4 _UIBlurGrabTex_TexelSize;

            float4 _ClipRect;
            float _BlurSize;
            float _Opacity;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.grabPos = ComputeGrabScreenPos(OUT.vertex);
                return OUT;
            }

            fixed4 SampleBlur(float4 grabPos)
            {
                float2 uv = grabPos.xy / grabPos.w;
                float2 texel = _UIBlurGrabTex_TexelSize.xy * _BlurSize;

                fixed4 col = 0;

                col += tex2D(_UIBlurGrabTex, uv + texel * float2(-2,  0));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2(-1, -1));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2(-1,  0));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2(-1,  1));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2( 0, -2));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2( 0, -1));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2( 0,  0));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2( 0,  1));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2( 0,  2));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2( 1, -1));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2( 1,  0));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2( 1,  1));
                col += tex2D(_UIBlurGrabTex, uv + texel * float2( 2,  0));

                col /= 13.0;
                return col;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 blurred = SampleBlur(IN.grabPos);

                fixed4 sprite = tex2D(_MainTex, IN.texcoord) * IN.color;

                fixed4 col;
                col.rgb = blurred.rgb * sprite.rgb;
                col.a = sprite.a * _Opacity;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}