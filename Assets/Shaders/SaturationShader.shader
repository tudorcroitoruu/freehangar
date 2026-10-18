Shader "UI/SpriteSaturation"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
        _Saturation("Saturation", Range(0, 1)) = 1

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        _MaskTex("MaskTexture", 2D) = "white" {}
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

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile __ UNITY_UI_ALPHACLIP

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
                half2 texcoord       : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                half4 mask           : TEXCOORD2;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Saturation;

            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            float3 ApplySaturation(float3 color, float saturation)
            {
                float luma = dot(color, float3(0.2126729, 0.7151522, 0.0721750));
                return lerp(luma.xxx, color, saturation);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;

                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = IN.texcoord;

                #ifdef UNITY_HALF_TEXEL_OFFSET
                OUT.vertex.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1) * OUT.vertex.w;
                #endif

                OUT.color = IN.color * _Color;

                float2 pixelSize = OUT.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(
                    _ClipRect,
                    -2e10,
                    2e10
                );

                OUT.mask = half4(
                    IN.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy))
                );

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 raw = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;

                half4 color = raw * IN.color;

                // Saturation only for RGB.
                color.rgb = ApplySaturation(color.rgb, _Saturation);

                // Optional mask texture.
                // If you want mask texture alpha to affect final alpha, uncomment this:
                // half4 maskCol = tex2D(_MaskTex, IN.texcoord);
                // color.a *= maskCol.a;

                half2 maskSoftness = saturate(
                    (_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw
                );

                color.a *= maskSoftness.x * maskSoftness.y;

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }

            ENDCG
        }
    }
}