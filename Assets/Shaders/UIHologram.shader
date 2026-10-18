Shader "UI/Color Sculpt/Hologram"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Hologram)]
        _EffectStrength ("Effect Strength", Range(0, 1)) = 1
        [HDR] _HologramColor ("Hologram Color", Color) = (1.15, 0.38, 0.85, 1)
        [HDR] _AccentColor ("Accent Color", Color) = (0.15, 1.2, 1.35, 1)
        _AccentStrength ("Accent Strength", Range(0, 1)) = 0.35

        [Header(Scanlines)]
        _ScanlineDensity ("Scanline Density", Range(10, 300)) = 140
        _ScanlineSpeed ("Scanline Speed", Range(-10, 10)) = 1.4
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.32

        [Header(Glitch)]
        _GlitchStrength ("Horizontal Distortion", Range(0, 0.08)) = 0.018
        _GlitchSpeed ("Glitch Speed", Range(0, 30)) = 12
        _GlitchBands ("Glitch Bands", Range(4, 100)) = 38
        _GlitchFrequency ("Glitch Frequency", Range(0, 1)) = 0.22
        _RGBSplit ("RGB Split", Range(0, 0.04)) = 0.008
        _Flicker ("Flicker", Range(0, 0.5)) = 0.08

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
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                half4 mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _HologramColor;
            fixed4 _AccentColor;
            float4 _ClipRect;
            half _EffectStrength;
            half _AccentStrength;
            half _ScanlineDensity;
            half _ScanlineSpeed;
            half _ScanlineIntensity;
            half _GlitchStrength;
            half _GlitchSpeed;
            half _GlitchBands;
            half _GlitchFrequency;
            half _RGBSplit;
            half _Flicker;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            float Hash11(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453);
            }

            fixed4 SampleSprite(float2 uv)
            {
                return tex2D(_MainTex, uv) + _TextureSampleAdd;
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;

                #ifdef UNITY_HALF_TEXEL_OFFSET
                output.vertex.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1) * output.vertex.w;
                #endif

                float2 pixelSize = output.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = half4(
                    input.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float effect = saturate(_EffectStrength);
                float time = _Time.y;
                float band = floor(input.texcoord.y * _GlitchBands);
                float glitchTick = floor(time * _GlitchSpeed);
                float bandNoise = Hash11(band + glitchTick * 17.0);
                float glitchGate = step(1.0 - _GlitchFrequency, bandNoise);
                float glitchDirection = Hash11(band * 3.7 + glitchTick * 29.0) * 2.0 - 1.0;
                float glitchOffset = glitchDirection * _GlitchStrength * glitchGate * effect;

                float2 centerUV = input.texcoord + float2(glitchOffset, 0.0);
                float splitAmount = (_RGBSplit + abs(glitchOffset) * 0.35) * effect;
                fixed4 centerSample = SampleSprite(centerUV);
                fixed4 redSample = SampleSprite(centerUV + float2(splitAmount, 0.0));
                fixed4 blueSample = SampleSprite(centerUV - float2(splitAmount, 0.0));

                half3 separatedColor = half3(redSample.r, centerSample.g, blueSample.b);
                half luminance = dot(separatedColor, half3(0.299h, 0.587h, 0.114h));

                half scanWave = 0.5h + 0.5h * sin(
                    (input.texcoord.y * _ScanlineDensity - time * _ScanlineSpeed) * 6.2831853);
                half scanBrightness = lerp(
                    1.0h - _ScanlineIntensity,
                    1.0h + _ScanlineIntensity * 0.3h,
                    scanWave);

                half3 hologramColor = separatedColor * _HologramColor.rgb;
                hologramColor += luminance * _AccentColor.rgb * _AccentStrength * scanWave;
                hologramColor += _AccentColor.rgb * glitchGate * luminance * 0.18h;

                half3 finalColor = lerp(separatedColor, hologramColor, effect);
                finalColor *= lerp(1.0h, scanBrightness, effect);

                half flickerNoise = Hash11(glitchTick * 7.0 + 3.0);
                half flicker = 1.0h - _Flicker * effect * flickerNoise;
                finalColor *= flicker;

                half ghostAlpha = max(centerSample.a, max(redSample.a, blueSample.a));
                half finalAlpha = lerp(centerSample.a, ghostAlpha, effect);
                fixed4 color = fixed4(finalColor, finalAlpha) * input.color;

                #ifdef UNITY_UI_CLIP_RECT
                half2 rectMask = saturate(
                    (_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                color.a *= rectMask.x * rectMask.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001h);
                #endif

                return color;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
