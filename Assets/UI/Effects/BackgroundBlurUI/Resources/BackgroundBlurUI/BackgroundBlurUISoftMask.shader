Shader "Hidden/BackgroundBlurUI/SoftAlphaMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mask Texture", 2D) = "white" {}
        _BlurTex ("Blur Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)]
        _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [HideInInspector]
        _CaptureScreenRect ("Capture Screen Rect", Vector) = (0, 0, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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

        Pass
        {
            Name "Soft Sprite Alpha Mask"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                fixed4 color : COLOR;
                float2 maskUV : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float4 localPosition : TEXCOORD2;
            };

            sampler2D _MainTex;
            sampler2D _BlurTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _CaptureScreenRect;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.localPosition = input.positionOS;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.color = input.color * _Color;
                output.maskUV = input.uv;
                output.screenPosition =
                    ComputeScreenPos(output.positionCS);
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed maskAlpha =
                    saturate((tex2D(_MainTex, input.maskUV) +
                    _TextureSampleAdd).a);

                // Continuous alpha avoids the binary stencil threshold.
                // This cheap smooth curve suppresses low-alpha compression
                // noise while preserving the sprite's antialiased edge.
                maskAlpha =
                    maskAlpha * maskAlpha * (3.0h - 2.0h * maskAlpha);

                float2 screenUV =
                    input.screenPosition.xy / input.screenPosition.w;
                float2 blurUV = saturate(
                    (screenUV - _CaptureScreenRect.xy) /
                    max(
                        _CaptureScreenRect.zw,
                        float2(0.000001, 0.000001)));
                fixed4 blur = tex2D(_BlurTex, blurUV);
                fixed4 color = fixed4(
                    blur.rgb * input.color.rgb,
                    blur.a * maskAlpha * input.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(
                    input.localPosition.xy,
                    _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001h);
                #endif

                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
