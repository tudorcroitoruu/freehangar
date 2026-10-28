Shader "UI/Image Margin Fade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FadeColor ("Fade Color", Color) = (0,0,0,0)
        _Saturation ("Saturation", Range(0, 1)) = 1
        _RoundCorners ("Round Corners", Range(0, 1)) = 0
        _FadeLeftSize ("Left Fade Size", Range(0, 1)) = 0.1
        _FadeLeftSoftness ("Left Softness", Range(0, 1)) = 1
        _FadeRightSize ("Right Fade Size", Range(0, 1)) = 0.1
        _FadeRightSoftness ("Right Softness", Range(0, 1)) = 1
        _FadeTopSize ("Top Fade Size", Range(0, 1)) = 0.1
        _FadeTopSoftness ("Top Softness", Range(0, 1)) = 1
        _FadeBottomSize ("Bottom Fade Size", Range(0, 1)) = 0.1
        _FadeBottomSoftness ("Bottom Softness", Range(0, 1)) = 1

        [Toggle(_USE_PAINTED_EDGE_NOISE)] _UseNoise ("Use Noise", Float) = 1
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Range(0, 20)) = 3.5
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.08
        [Toggle] _TornPaper ("Torn Paper (No Interior Holes)", Float) = 0
        _TornPaperRoughness ("Torn Paper Roughness", Range(0, 1)) = 0.75
        _TornPaperStretch ("Torn Paper Stretch", Range(0.1, 10)) = 1
        _TornPaperSoftness ("Torn Paper Softness", Range(0, 1)) = 0.02
        _NoiseOffset ("Noise Offset", Vector) = (0, 0, 0, 0)
        [Toggle(_ANIMATE_PAINTED_EDGE_NOISE)] _AnimateNoise ("Animate Noise", Float) = 1
        _NoiseAnimationDirection ("Animation Direction", Vector) = (1, 0.35, 0, 0)
        _NoiseAnimationSpeed ("Animation Speed", Float) = 0.012
        _NoisePulseAmount ("Noise Pulse Amount", Range(0, 1)) = 0.08
        _NoisePulseSpeed ("Noise Pulse Speed", Float) = 0.45
        _FadeWaveAmount ("Fade Wave Amount", Range(0, 0.05)) = 0.004
        _FadeWaveSpeed ("Fade Wave Speed", Float) = 0.3
        [Toggle] _IndependentEdgeAnimation ("Independent Edge Animation", Float) = 1
        _EdgeVariation ("Edge Variation", Range(0, 1)) = 0.4
        [Toggle(_USE_SECONDARY_PAINTED_EDGE_NOISE)] _UseSecondaryNoise ("Secondary Noise", Float) = 1
        _SecondaryNoiseScale ("Secondary Scale", Range(0, 20)) = 7
        _SecondaryNoiseSpeed ("Secondary Speed", Vector) = (-0.005, 0.009, 0, 0)
        _SecondaryNoiseBlend ("Secondary Blend", Range(0, 1)) = 0.25

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

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local _USE_PAINTED_EDGE_NOISE
            #pragma shader_feature_local _ANIMATE_PAINTED_EDGE_NOISE
            #pragma shader_feature_local _USE_SECONDARY_PAINTED_EDGE_NOISE

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
                float2 noisePosition : TEXCOORD3;
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            fixed4 _Color;
            fixed4 _FadeColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _NoiseOffset;
            float4 _NoiseAnimationDirection;
            float4 _SecondaryNoiseSpeed;

            half _FadeLeftSize;
            half _FadeLeftSoftness;
            half _FadeRightSize;
            half _FadeRightSoftness;
            half _FadeTopSize;
            half _FadeTopSoftness;
            half _FadeBottomSize;
            half _FadeBottomSoftness;
            half _Saturation;
            half _RoundCorners;
            half _NoiseScale;
            half _NoiseStrength;
            half _TornPaper;
            half _TornPaperRoughness;
            float _TornPaperStretch;
            half _TornPaperSoftness;
            half _NoiseAnimationSpeed;
            half _NoisePulseAmount;
            half _NoisePulseSpeed;
            half _FadeWaveAmount;
            half _FadeWaveSpeed;
            half _IndependentEdgeAnimation;
            half _EdgeVariation;
            half _SecondaryNoiseScale;
            half _SecondaryNoiseBlend;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            v2f vert(appdata_t IN)
            {
                v2f OUT;

                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = IN.texcoord;
                OUT.noisePosition = IN.vertex.xy;

                #ifdef UNITY_HALF_TEXEL_OFFSET
                OUT.vertex.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1) * OUT.vertex.w;
                #endif

                OUT.color = IN.color * _Color;

                float2 pixelSize = OUT.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);

                OUT.mask = half4(
                    IN.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy))
                );

                return OUT;
            }

            half3 ApplySaturation(half3 rgb, half saturation)
            {
                half luma = dot(rgb, half3(0.2126, 0.7152, 0.0722));
                return lerp(luma.xxx, rgb, saturate(saturation));
            }

            half RoundedCornersMask(float2 uv)
            {
                if (_RoundCorners <= 0.0)
                    return 1.0;

                // Convert the UV rectangle into screen-pixel units so a non-square
                // Image still has circular corner arcs instead of stretched ellipses.
                float2 uvDx = ddx(uv);
                float2 uvDy = ddy(uv);
                float2 uvPerPixel = max(sqrt(uvDx * uvDx + uvDy * uvDy), float2(0.000001, 0.000001));
                float2 halfSize = 0.5 / uvPerPixel;
                float radius = saturate(_RoundCorners) * min(halfSize.x, halfSize.y);
                float2 position = (uv - 0.5) / uvPerPixel;
                float2 corner = abs(position) - (halfSize - radius);
                float distance = length(max(corner, 0.0)) + min(max(corner.x, corner.y), 0.0) - radius;
                float antialias = max(fwidth(distance), 0.0001);
                return saturate(0.5 - distance / antialias);
            }

            half SamplePaintedNoise(float2 noisePosition)
            {
                #if defined(_USE_PAINTED_EDGE_NOISE)
                // Local UI position keeps noise scale more consistent on non-square Images than raw UVs.
                float2 noiseUV = noisePosition * (_NoiseScale * 0.01);
                noiseUV = noiseUV * _NoiseTex_ST.xy + _NoiseTex_ST.zw + _NoiseOffset.xy;

                #if defined(_ANIMATE_PAINTED_EDGE_NOISE)
                noiseUV += _Time.y * _NoiseAnimationDirection.xy * _NoiseAnimationSpeed;
                #endif

                half noise = tex2D(_NoiseTex, noiseUV).r * 2.0 - 1.0;

                #if defined(_USE_SECONDARY_PAINTED_EDGE_NOISE)
                float2 secondaryUV = noisePosition * (_SecondaryNoiseScale * 0.01);
                secondaryUV = secondaryUV * _NoiseTex_ST.xy + _NoiseTex_ST.zw + _NoiseOffset.xy;

                #if defined(_ANIMATE_PAINTED_EDGE_NOISE)
                secondaryUV += _Time.y * _SecondaryNoiseSpeed.xy;
                #endif

                half secondaryNoise = tex2D(_NoiseTex, secondaryUV).r * 2.0 - 1.0;
                noise = lerp(noise, secondaryNoise, saturate(_SecondaryNoiseBlend));
                #endif

                return noise;
                #else
                return 0.0;
                #endif
            }

            half EdgePhase(half edgeIndex)
            {
                #if defined(_ANIMATE_PAINTED_EDGE_NOISE)
                return edgeIndex * saturate(_IndependentEdgeAnimation) * _EdgeVariation;
                #else
                return 0.0;
                #endif
            }

            half NoisePulse(half edgeIndex)
            {
                #if defined(_ANIMATE_PAINTED_EDGE_NOISE)
                half phase = EdgePhase(edgeIndex);
                return sin((_Time.y * _NoisePulseSpeed + phase) * 6.2831853) * _NoisePulseAmount;
                #else
                return 0.0;
                #endif
            }

            half FadeWave(half edgeIndex)
            {
                #if defined(_ANIMATE_PAINTED_EDGE_NOISE)
                half phase = EdgePhase(edgeIndex) * 1.618;
                return sin((_Time.y * _FadeWaveSpeed + phase) * 6.2831853) * _FadeWaveAmount;
                #else
                return 0.0;
                #endif
            }

            half EdgeFade(half edgeDistance, half fadeSize, half softness, half noise, half edgeIndex)
            {
                fadeSize = max(fadeSize, 0.0);
                half hasFade = step(0.0001, fadeSize);

                #if defined(_USE_PAINTED_EDGE_NOISE)
                if (_TornPaper > 0.5)
                {
                    // Noise is constant along the inward normal of each edge, so each
                    // scanline has a single boundary rather than isolated interior holes.
                    half roughness = saturate(_NoiseStrength) * saturate(_TornPaperRoughness);
                    half boundary = fadeSize * (0.5 + 0.5 * noise * roughness * (1.0 + NoisePulse(edgeIndex)));
                    // Keep every animated cut inside its configured edge band.
                    half fadeEnd = clamp(boundary + FadeWave(edgeIndex), 0.0001, max(fadeSize, 0.0001));
                    half transition = max(fadeSize * saturate(_TornPaperSoftness), fwidth(edgeDistance));
                    half fadeStart = max(0.0, fadeEnd - max(transition, 0.0001));
                    return (1.0 - smoothstep(fadeStart, fadeEnd, edgeDistance)) * hasFade;
                }
                #endif

                // Fade size is the total UV-space edge band affected by this side.
                // Setting a side's fade size to 0 disables that side.
                // Noise changes the fade width per pixel, distorting only the inner fade boundary.
                half pulsedNoise = noise * (1.0 + NoisePulse(edgeIndex));
                half noiseWidthMultiplier = max(1.0 + pulsedNoise * saturate(_NoiseStrength), 0.05);
                half effectiveFadeSize = fadeSize * noiseWidthMultiplier + FadeWave(edgeIndex);

                // Softness controls how much of that band is used for the smoothstep ramp:
                // 1 = smooth fade across the whole band, 0 = nearly hard transition at the inner edge.
                softness = max(saturate(softness), 0.0001);
                half fadeEnd = max(effectiveFadeSize, 0.0001);
                half fadeStart = fadeEnd * (1.0 - softness);

                // smoothstep returns 0 at the outer edge and 1 once the pixel reaches the unchanged center.
                // Inverting it gives a mask that is strongest at the border and disappears inward.
                half edgeMask = 1.0 - smoothstep(fadeStart, fadeEnd, edgeDistance);

                return edgeMask * hasFade;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                color.rgb = ApplySaturation(color.rgb, _Saturation);

                half2 uv = IN.texcoord;
                half edgeNoise = SamplePaintedNoise(IN.noisePosition);
                half leftNoise = edgeNoise;
                half rightNoise = edgeNoise;
                half bottomNoise = edgeNoise;
                half topNoise = edgeNoise;

                #if defined(_USE_PAINTED_EDGE_NOISE)
                if (_TornPaper > 0.5)
                {
                    // Project onto the edge tangent, with distinct slices per side.
                    // Do not sample the inward coordinate: that would reintroduce holes.
                    // Stretch both noise layers along the contour without changing cut depth.
                    float2 stretchedPosition = IN.noisePosition / max(_TornPaperStretch, 0.1);
                    leftNoise = SamplePaintedNoise(float2(17.0, stretchedPosition.y));
                    rightNoise = SamplePaintedNoise(float2(137.0, stretchedPosition.y));
                    bottomNoise = SamplePaintedNoise(float2(stretchedPosition.x, 271.0));
                    topNoise = SamplePaintedNoise(float2(stretchedPosition.x, 389.0));
                }
                #endif

                // Distances are measured in sprite UV space:
                // left = uv.x, right = 1 - uv.x, bottom = uv.y, top = 1 - uv.y.
                half leftFade = EdgeFade(uv.x, _FadeLeftSize, _FadeLeftSoftness, leftNoise, 0.0);
                half rightFade = EdgeFade(1.0 - uv.x, _FadeRightSize, _FadeRightSoftness, rightNoise, 1.0);
                half bottomFade = EdgeFade(uv.y, _FadeBottomSize, _FadeBottomSoftness, bottomNoise, 2.0);
                half topFade = EdgeFade(1.0 - uv.y, _FadeTopSize, _FadeTopSoftness, topNoise, 3.0);

                // Use the strongest enabled edge so corners fade cleanly without over-darkening.
                half edgeFade = max(max(leftFade, rightFade), max(topFade, bottomFade));

                // _FadeColor.a chooses the fade target opacity:
                // alpha 0 fades margins to transparent, alpha 1 keeps the sprite alpha and tints to _FadeColor.
                fixed4 fadeTarget = fixed4(_FadeColor.rgb, color.a * _FadeColor.a);
                color = lerp(color, fadeTarget, edgeFade);
                color.a *= RoundedCornersMask(IN.texcoord);

                #ifdef UNITY_UI_CLIP_RECT
                half2 maskSoftness = saturate(
                    (_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw
                );

                color.a *= maskSoftness.x * maskSoftness.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }

            ENDCG
        }
    }

    CustomEditor "UIImageMarginFadeShaderGUI"
}
