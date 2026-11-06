Shader "UI/Sprite Merge Blend"
{
    Properties
    {
        [PerRendererData] _MainTex ("Current Sprite", 2D) = "white" {}
        [NoScaleOffset] _PreviousTex ("Previous Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Blend ("Blend", Range(0,1)) = 1
        _CurrentUvRect ("Current UV Rect", Vector) = (0,0,1,1)
        _PreviousUvRect ("Previous UV Rect", Vector) = (0,0,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,0)
        _OutlineThickness ("Outline Thickness", Range(0,8)) = 2
        _OutlineSweep ("Outline Sweep", Range(0,1)) = 1
        _OutlineSweepSoftness ("Outline Sweep Softness", Range(0.001,0.25)) = 0.04

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

        Pass
        {
            Name "SpriteMergeBlend"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _PreviousTex;
            float4 _MainTex_TexelSize;
            float4 _PreviousTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            fixed4 _TextureSampleAdd;
            float _Blend;
            float _OutlineThickness;
            float _OutlineSweep;
            float _OutlineSweepSoftness;
            float4 _CurrentUvRect;
            float4 _PreviousUvRect;
            float4 _ClipRect;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            float IsInsideRect(float2 uv, float4 rect)
            {
                float2 minimum = step(rect.xy, uv);
                float2 maximum = step(uv, rect.xy + rect.zw);
                return minimum.x * minimum.y * maximum.x * maximum.y;
            }

            fixed SampleMergedAlpha(
                float2 currentUv,
                float2 previousUv,
                float2 direction)
            {
                float2 currentSampleUv = currentUv +
                    direction * _MainTex_TexelSize.xy * _OutlineThickness;
                float2 previousSampleUv = previousUv +
                    direction * _PreviousTex_TexelSize.xy * _OutlineThickness;
                fixed currentAlpha = tex2D(
                    _MainTex,
                    currentSampleUv).a *
                    IsInsideRect(currentSampleUv, _CurrentUvRect);
                fixed previousAlpha = tex2D(
                    _PreviousTex,
                    previousSampleUv).a *
                    IsInsideRect(previousSampleUv, _PreviousUvRect);
                return lerp(
                    previousAlpha,
                    currentAlpha,
                    saturate(_Blend));
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 currentSize = max(_CurrentUvRect.zw, float2(0.00001, 0.00001));
                float2 localUv = saturate((input.uv - _CurrentUvRect.xy) / currentSize);
                float2 previousUv = _PreviousUvRect.xy + localUv * _PreviousUvRect.zw;

                fixed4 previousColor = tex2D(_PreviousTex, previousUv);
                fixed4 currentColor = tex2D(_MainTex, input.uv) + _TextureSampleAdd;
                fixed4 outputColor = lerp(previousColor, currentColor, saturate(_Blend));
                outputColor *= input.color;

                fixed neighborAlpha = 0.0;
                neighborAlpha = max(neighborAlpha, SampleMergedAlpha(
                    input.uv, previousUv, float2(-1.0, 0.0)));
                neighborAlpha = max(neighborAlpha, SampleMergedAlpha(
                    input.uv, previousUv, float2(1.0, 0.0)));
                neighborAlpha = max(neighborAlpha, SampleMergedAlpha(
                    input.uv, previousUv, float2(0.0, -1.0)));
                neighborAlpha = max(neighborAlpha, SampleMergedAlpha(
                    input.uv, previousUv, float2(0.0, 1.0)));
                neighborAlpha = max(neighborAlpha, SampleMergedAlpha(
                    input.uv, previousUv, float2(-0.707, -0.707)));
                neighborAlpha = max(neighborAlpha, SampleMergedAlpha(
                    input.uv, previousUv, float2(0.707, -0.707)));
                neighborAlpha = max(neighborAlpha, SampleMergedAlpha(
                    input.uv, previousUv, float2(-0.707, 0.707)));
                neighborAlpha = max(neighborAlpha, SampleMergedAlpha(
                    input.uv, previousUv, float2(0.707, 0.707)));

                fixed outlineMask = saturate(
                    neighborAlpha - outputColor.a) *
                    saturate(_OutlineColor.a);
                float outlineAngle = frac(
                    atan2(
                        localUv.x - 0.5,
                        localUv.y - 0.5) /
                    6.2831853 + 1.0);
                float outlineReveal = 1.0 - smoothstep(
                    max(0.0, _OutlineSweep - _OutlineSweepSoftness),
                    max(0.0001, _OutlineSweep),
                    outlineAngle);
                outlineReveal = lerp(
                    outlineReveal,
                    1.0,
                    step(0.9999, _OutlineSweep));
                outlineMask *= outlineReveal;
                outputColor.rgb = lerp(
                    outputColor.rgb,
                    _OutlineColor.rgb,
                    outlineMask);
                outputColor.a = max(outputColor.a, outlineMask);

                #ifdef UNITY_UI_CLIP_RECT
                outputColor.a *= UnityGet2DClipping(
                    input.worldPosition.xy,
                    _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outputColor.a - 0.001);
                #endif

                return outputColor;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
