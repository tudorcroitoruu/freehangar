Shader "UI/Image Margin Fade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TransparencyMaskTex ("Transparency Mask Texture", 2D) = "white" {}
        [HideInInspector] _HasTransparencyMaskTex ("Has Transparency Mask Texture", Float) = 0
        _TransparencyMaskScale ("Mask Scale", Range(0.1,10)) = 1
        _TransparencyMaskIntensity ("Mask Intensity", Range(0,1)) = 0
        [ToggleUI] _TransparencyMaskBlackTransparent ("Black Is Transparent", Float) = 1
        _MainOverlayTex ("Main Overlay Texture", 2D) = "white" {}
        [HideInInspector] _HasMainOverlayTex ("Has Main Overlay Texture", Float) = 0
        _MainOverlayColor ("Main Overlay Color", Color) = (1,1,1,1)
        _MainOverlayOpacity ("Main Overlay Opacity", Range(0,1)) = 0
        _MainOverlayScale ("Mask Scale", Range(0.1,10)) = 1
        [ToggleUI] _MainOverlayUseMaskScale ("Use Mask Scale", Float) = 1
        [ToggleUI] _MainOverlayBlackTransparent ("Black Is Transparent", Float) = 0
        [Enum(Multiply,0,Normal,1)] _MainOverlayBlend ("Main Overlay Blend", Float) = 0
        [ToggleUI] _UseGradient ("Use Gradient", Float) = 0
        _GradientStartColor ("Start Color", Color) = (1,1,1,1)
        _GradientEndColor ("End Color", Color) = (1,0.5,0,1)
        [Enum(Left To Right,0,Right To Left,1,Bottom To Top,2,Top To Bottom,3)] _GradientDirection ("Gradient Direction", Float) = 0
        _UISmoothness ("Smoothness", Range(0,1)) = 0
        _ShineColor ("Shine Color", Color) = (1,1,1,1)
        _ShineIntensity ("Shine Intensity", Range(0,2)) = 1
        _ShinePosition ("Shine Position", Range(0,1)) = 0.7
        _FadeColor ("Fade Color", Color) = (0,0,0,0)
        _Saturation ("Saturation", Range(0, 1)) = 1
        _RoundCorners ("Round Corners", Range(0, 1)) = 0
        [ToggleUI] _UseFill ("Use Fill", Float) = 0
        _FillAmount ("Fill Amount", Range(0,1)) = 1
        [Enum(Left To Right,0,Right To Left,1,Bottom To Top,2,Top To Bottom,3)] _FillDirection ("Fill Direction", Float) = 0
        [ToggleUI] _FillRoundCorners ("Round Fill Corners", Float) = 0
        [Toggle] _Outline ("Outline", Float) = 0
        _OutlineThickness ("Outline Thickness (UI Units)", Range(0, 32)) = 2
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineOverlayTex ("Outline Overlay Texture", 2D) = "white" {}
        [HideInInspector] _HasOutlineOverlayTex ("Has Outline Overlay Texture", Float) = 0
        _OutlineOverlayColor ("Outline Overlay Color", Color) = (1,1,1,1)
        _OutlineOverlayOpacity ("Outline Overlay Opacity", Range(0,1)) = 0
        _OutlineOverlayScale ("Mask Scale", Range(0.1,10)) = 1
        [ToggleUI] _OutlineOverlayUseMaskScale ("Use Mask Scale", Float) = 1
        [ToggleUI] _OutlineOverlayBlackTransparent ("Black Is Transparent", Float) = 0
        [Enum(Multiply,0,Normal,1)] _OutlineOverlayBlend ("Outline Overlay Blend", Float) = 0
        [ToggleUI] _UseGlow ("Use Glow", Float) = 0
        _GlowColor ("Glow Color", Color) = (1,0.7,0.2,1)
        _GlowSize ("Glow Size (Pixels)", Range(0,32)) = 8
        _GlowIntensity ("Glow Intensity", Range(0,5)) = 1
        _GlowSoftness ("Glow Softness", Range(0.01,1)) = 0.7
        _GlowPadding ("Glow Padding (Pixels)", Range(0,64)) = 12
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
            #pragma target 3.0

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
            sampler2D _TransparencyMaskTex;
            float _HasTransparencyMaskTex;
            float4 _TransparencyMaskTex_TexelSize;
            float _TransparencyMaskScale;
            float _TransparencyMaskIntensity;
            float _TransparencyMaskBlackTransparent;
            sampler2D _MainOverlayTex;
            float _HasMainOverlayTex;
            float4 _MainOverlayTex_ST;
            float4 _MainOverlayTex_TexelSize;
            float _MainOverlayScale;
            float _MainOverlayUseMaskScale;
            fixed4 _MainOverlayColor;
            float _MainOverlayOpacity;
            float _MainOverlayBlackTransparent;
            float _MainOverlayBlend;
            sampler2D _OutlineOverlayTex;
            float _HasOutlineOverlayTex;
            float4 _OutlineOverlayTex_ST;
            float4 _OutlineOverlayTex_TexelSize;
            float _OutlineOverlayScale;
            float _OutlineOverlayUseMaskScale;
            fixed4 _OutlineOverlayColor;
            float _OutlineOverlayOpacity;
            float _OutlineOverlayBlackTransparent;
            float _OutlineOverlayBlend;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            fixed4 _Color;
            float _UseGradient;
            fixed4 _GradientStartColor;
            fixed4 _GradientEndColor;
            float _GradientDirection;
            float _UISmoothness;
            fixed4 _ShineColor;
            float _ShineIntensity;
            float _ShinePosition;
            fixed4 _FadeColor;
            fixed4 _OutlineColor;
            float _UseGlow;
            float4 _GlowColor;
            float _GlowSize;
            float _GlowIntensity;
            float _GlowSoftness;
            float _GlowPadding;
            half _Outline;
            float _OutlineThickness;
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
            float _UseFill;
            float _FillAmount;
            float _FillDirection;
            float _FillRoundCorners;
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

            // Convert a screen-space derivative to a gradient in local UI units.
            // Inverting the position Jacobian removes camera zoom and Canvas scaling,
            // including when the canvas is rotated or viewed in perspective.
            float2 LocalUIGradient(float value, float2 localPosition)
            {
                float2 positionDx = ddx(localPosition);
                float2 positionDy = ddy(localPosition);
                float determinant = positionDx.x * positionDy.y - positionDx.y * positionDy.x;
                float safeDeterminant = (determinant < 0.0 ? -1.0 : 1.0) * max(abs(determinant), 1e-12);
                float valueDx = ddx(value);
                float valueDy = ddy(value);
                return float2(valueDx * positionDy.y - valueDy * positionDx.y,
                              valueDy * positionDx.x - valueDx * positionDy.x) / safeDeterminant;
            }

            float2 TransparencyMaskOffset(float2 cell)
            {
                return frac(sin(float2(dot(cell, float2(127.1, 311.7)),
                                       dot(cell, float2(269.5, 183.3)))) * 43758.5453) * 16.0;
            }

            float4 SampleTransparencyMaskTile(sampler2D maskTexture, float2 uv, float2 uvDx, float2 uvDy, float2 textureSize)
            {
                float2 phase = frac(uv * 0.5) * 2.0;
                float2 mirroredUV = 1.0 - abs(phase - 1.0);
                float2 direction = 1.0 - 2.0 * step(1.0, phase);
                float2 halfTexel = 0.5 / textureSize;
                mirroredUV = clamp(mirroredUV, halfTexel, 1.0 - halfTexel);
                return tex2Dgrad(maskTexture, mirroredUV, uvDx * direction, uvDy * direction);
            }

            float4 SampleTransparencyMask(sampler2D maskTexture, float2 uv, float2 textureSize)
            {
                // Neighboring cells share the same samples at their borders. Smooth
                // weights hide transitions while random offsets break mirrored repeats.
                // Gradients come from the continuous coordinates, never the cell hash.
                float2 cell = floor(uv);
                float2 blend = frac(uv);
                blend = blend * blend * (3.0 - 2.0 * blend);
                float2 uvDx = ddx(uv);
                float2 uvDy = ddy(uv);
                float4 a = SampleTransparencyMaskTile(maskTexture, uv + TransparencyMaskOffset(cell), uvDx, uvDy, textureSize);
                float4 b = SampleTransparencyMaskTile(maskTexture, uv + TransparencyMaskOffset(cell + float2(1, 0)), uvDx, uvDy, textureSize);
                float4 c = SampleTransparencyMaskTile(maskTexture, uv + TransparencyMaskOffset(cell + float2(0, 1)), uvDx, uvDy, textureSize);
                float4 d = SampleTransparencyMaskTile(maskTexture, uv + TransparencyMaskOffset(cell + float2(1, 1)), uvDx, uvDy, textureSize);
                return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
            }

            float CornerRadius(float2 uv)
            {
                float2 uvPerPixel = max(sqrt(ddx(uv) * ddx(uv) + ddy(uv) * ddy(uv)), float2(0.000001, 0.000001));
                float2 halfSize = 0.5 / uvPerPixel;
                return saturate(_RoundCorners) * min(halfSize.x, halfSize.y);
            }

            float RoundedCornersDistance(float2 uv, float radius)
            {
                // Convert the UV rectangle into screen-pixel units so a non-square
                // Image still has circular corner arcs instead of stretched ellipses.
                float2 uvDx = ddx(uv);
                float2 uvDy = ddy(uv);
                float2 uvPerPixel = max(sqrt(uvDx * uvDx + uvDy * uvDy), float2(0.000001, 0.000001));
                float2 halfSize = 0.5 / uvPerPixel;
                float2 position = (uv - 0.5) / uvPerPixel;
                float2 corner = abs(position) - (halfSize - radius);
                return length(max(corner, 0.0)) + min(max(corner.x, corner.y), 0.0) - radius;
            }

            half RoundedCornersMask(float2 uv)
            {
                if (_RoundCorners <= 0.0)
                    return 1.0;
                float distance = RoundedCornersDistance(uv, CornerRadius(uv));
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
                if (_Outline <= 0.5) noiseUV += _Time.y * _NoiseAnimationDirection.xy * _NoiseAnimationSpeed;
                #endif

                half noise = tex2D(_NoiseTex, noiseUV).r * 2.0 - 1.0;

                #if defined(_USE_SECONDARY_PAINTED_EDGE_NOISE)
                float2 secondaryUV = noisePosition * (_SecondaryNoiseScale * 0.01);
                secondaryUV = secondaryUV * _NoiseTex_ST.xy + _NoiseTex_ST.zw + _NoiseOffset.xy;

                #if defined(_ANIMATE_PAINTED_EDGE_NOISE)
                if (_Outline <= 0.5) secondaryUV += _Time.y * _SecondaryNoiseSpeed.xy;
                #endif

                half secondaryNoise = tex2D(_NoiseTex, secondaryUV).r * 2.0 - 1.0;
                noise = lerp(noise, secondaryNoise, saturate(_SecondaryNoiseBlend));
                #endif

                return noise;
                #else
                return 0.0;
                #endif
            }

            float NoisyRoundedCornersDistance(float2 uv, float2 noisePosition, float radius)
            {
                float distance = RoundedCornersDistance(uv, radius);
                #if defined(_USE_PAINTED_EDGE_NOISE)
                if (_RoundCorners > 0.0 && _NoiseStrength > 0.0)
                {
                    float2 uvPerPixel = max(sqrt(ddx(uv) * ddx(uv) + ddy(uv) * ddy(uv)), float2(0.000001, 0.000001));
                    float2 halfSize = 0.5 / uvPerPixel;
                    float2 position = (uv - 0.5) / uvPerPixel;
                    float2 corner = max(abs(position) - (halfSize - radius), 0.0);
                    float cornerLength = length(corner);
                    float2 normal = corner / max(cornerLength, 0.0001);
                    // Blend to zero where each arc joins a straight side.
                    float arcWeight = smoothstep(0.0, 0.2, min(normal.x, normal.y));
                    float2 localUnitsPerPixel = fwidth(noisePosition) / max(fwidth(uv), float2(0.000001, 0.000001)) * uvPerPixel;
                    // Project onto the original arc: noise stays constant along its
                    // inward normal, preserving a continuous tear without interior holes.
                    float2 contourPosition = noisePosition + sign(position) * normal * (radius - cornerLength) * localUnitsPerPixel;
                    if (_TornPaper > 0.5) contourPosition /= max(_TornPaperStretch, 0.1);
                    half noise = SamplePaintedNoise(contourPosition);
                    float roughness = saturate(_NoiseStrength) * (_TornPaper > 0.5 ? saturate(_TornPaperRoughness) : 1.0);
                    // Cut inward so the noisy arc remains inside the Image geometry.
                    distance += (0.5 + 0.5 * noise) * min(radius * 0.25, 16.0) * roughness * arcWeight;
                }
                #endif
                return distance;
            }

            half EdgePhase(half edgeIndex)
            {
                #if defined(_ANIMATE_PAINTED_EDGE_NOISE)
                return _Outline > 0.5 ? 0.0 : edgeIndex * saturate(_IndependentEdgeAnimation) * _EdgeVariation;
                #else
                return 0.0;
                #endif
            }

            half NoisePulse(half edgeIndex)
            {
                #if defined(_ANIMATE_PAINTED_EDGE_NOISE)
                half phase = EdgePhase(edgeIndex);
                return _Outline > 0.5 ? 0.0 : sin((_Time.y * _NoisePulseSpeed + phase) * 6.2831853) * _NoisePulseAmount;
                #else
                return 0.0;
                #endif
            }

            half FadeWave(half edgeIndex)
            {
                #if defined(_ANIMATE_PAINTED_EDGE_NOISE)
                half phase = EdgePhase(edgeIndex) * 1.618;
                return _Outline > 0.5 ? 0.0 : sin((_Time.y * _FadeWaveSpeed + phase) * 6.2831853) * _FadeWaveAmount;
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

            // Signed distance to the midpoint of the existing noisy fade transition.
            // Negative is inside the shape. No fade means the original UV boundary.
            float GlowEdgeDistance(float edgeDistance, half fadeSize, half softness, half noise, half edgeIndex)
            {
                float boundary = 0.0;
                if (fadeSize >= 0.0001)
                {
                    half fadeStart;
                    half fadeEnd;
                    #if defined(_USE_PAINTED_EDGE_NOISE)
                    if (_TornPaper > 0.5)
                    {
                        half roughness = saturate(_NoiseStrength) * saturate(_TornPaperRoughness);
                        half tornBoundary = fadeSize * (0.5 + 0.5 * noise * roughness * (1.0 + NoisePulse(edgeIndex)));
                        fadeEnd = clamp(tornBoundary + FadeWave(edgeIndex), 0.0001, max(fadeSize, 0.0001));
                        half transition = max(fadeSize * saturate(_TornPaperSoftness), fwidth(edgeDistance));
                        fadeStart = max(0.0, fadeEnd - max(transition, 0.0001));
                    }
                    else
                    #endif
                    {
                        half pulsedNoise = noise * (1.0 + NoisePulse(edgeIndex));
                        half widthMultiplier = max(1.0 + pulsedNoise * saturate(_NoiseStrength), 0.05);
                        fadeEnd = max(fadeSize * widthMultiplier + FadeWave(edgeIndex), 0.0001);
                        fadeStart = fadeEnd * (1.0 - max(saturate(softness), 0.0001));
                    }
                    boundary = (fadeStart + fadeEnd) * 0.5;
                }
                float distance = boundary - edgeDistance;
                return distance / max(length(float2(ddx(distance), ddy(distance))), 0.000001);
            }

            float4 frag(v2f IN) : SV_Target
            {
                bool useGlow = _UseGlow > 0.5 && _GlowIntensity > 0.0 && _GlowSize > 0.0;
                float2 shapeUV = IN.texcoord;
                if (useGlow)
                {
                    // Reserve transparent pixels INSIDE the existing Image geometry.
                    // Zero intensity leaves the original UVs and appearance untouched.
                    float2 uvPerPixel = sqrt(ddx(shapeUV) * ddx(shapeUV) + ddy(shapeUV) * ddy(shapeUV));
                    float2 padding = min(max(_GlowPadding, 0.0) * uvPerPixel, float2(0.49, 0.49));
                    shapeUV = (shapeUV - padding) / (1.0 - 2.0 * padding);
                }
                // Re-evaluate the existing contour on the filled rectangle. Derivatives
                // keep corner arcs circular instead of stretching them.
                bool roundedFill = _UseFill > 0.5 && _FillRoundCorners > 0.5 && _RoundCorners > 0.0
                    && _FillAmount > 0.0 && _FillAmount < 1.0;
                // Keep the full bar's radius even when the filled width/height becomes
                // smaller than its diameter. The end caps then intersect into a lens
                // instead of turning the remainder into a tall, narrow capsule.
                float cornerRadius = CornerRadius(shapeUV);
                float2 contourUV = shapeUV;
                if (roundedFill)
                {
                    float amount = max(saturate(_FillAmount), 0.0001);
                    if (_FillDirection < 1.5)
                        contourUV.x = (shapeUV.x - (_FillDirection < 0.5 ? 0.0 : 1.0 - amount)) / amount;
                    else
                        contourUV.y = (shapeUV.y - (_FillDirection < 2.5 ? 0.0 : 1.0 - amount)) / amount;
                }
                fixed4 color = (tex2D(_MainTex, useGlow ? saturate(shapeUV) : shapeUV) + _TextureSampleAdd) * IN.color;
                if (_UseGradient > 0.5)
                {
                    // Anchor the gradient to the full Image, so fill changes reveal
                    // more of the same gradient rather than stretching its colors.
                    float coordinate = _GradientDirection < 0.5 ? shapeUV.x
                        : _GradientDirection < 1.5 ? 1.0 - shapeUV.x
                        : _GradientDirection < 2.5 ? shapeUV.y : 1.0 - shapeUV.y;
                    color *= lerp(_GradientStartColor, _GradientEndColor, saturate(coordinate));
                }
                color.rgb = ApplySaturation(color.rgb, _Saturation);
                if (_UISmoothness > 0.0 && _ShineIntensity > 0.0)
                {
                    // A simulated surface reflection for unlit UI: higher smoothness
                    // produces a narrower, brighter highlight without HDR or scene lighting.
                    float smoothness = saturate(_UISmoothness);
                    float width = max(lerp(0.45, 0.035, smoothness), fwidth(shapeUV.y));
                    float distance = (shapeUV.y - _ShinePosition) / width;
                    float highlight = exp2(-4.0 * distance * distance);
                    float strength = saturate(highlight * smoothness * smoothness * _ShineIntensity * _ShineColor.a);
                    color.rgb = lerp(color.rgb, _ShineColor.rgb, strength);
                }

                // A color overlay stains the Source Image without cutting its alpha.
                // Full-image coordinates keep the pattern fixed while Fill reveals it.
                // All three masks share the same aspect-preserving scale convention.
                float2 maskLocalSize = 1.0 / max(float2(
                    length(LocalUIGradient(shapeUV.x, IN.noisePosition)),
                    length(LocalUIGradient(shapeUV.y, IN.noisePosition))), float2(0.000001, 0.000001));
                float2 imageAspect = maskLocalSize / min(maskLocalSize.x, maskLocalSize.y);
                // Define interior transparency on the full Image before evaluating
                // its margins. Fill and rounded contours do not stretch this pattern.
                float transparencyMask = 1.0;
                if (_HasTransparencyMaskTex > 0.5 && _TransparencyMaskIntensity > 0.0)
                {
                    // Larger scale means larger holes. Blended randomized samples
                    // avoid seams and obvious mirrored repetition in non-tileable masks.
                    // Measure the full shape in local UI units, not normalized UVs.
                    // Anchor scale to the shorter side and repeat along the longer one.
                    // Also preserve the source mask's own aspect ratio.
                    float2 maskTextureSize = max(_TransparencyMaskTex_TexelSize.zw, float2(1.0, 1.0));
                    float2 textureAspect = maskTextureSize / min(maskTextureSize.x, maskTextureSize.y);
                    float2 maskUV = (shapeUV - 0.5) * imageAspect / textureAspect
                        / max(_TransparencyMaskScale, 0.1) + 0.5;
                    float3 maskSample = SampleTransparencyMask(_TransparencyMaskTex, maskUV, maskTextureSize).rgb;
                    float maskValue = saturate(dot(maskSample, float3(0.2126, 0.7152, 0.0722)));
                    if (_TransparencyMaskBlackTransparent < 0.5)
                        maskValue = 1.0 - maskValue;
                    transparencyMask = lerp(1.0, maskValue, saturate(_TransparencyMaskIntensity));
                }
                if (_HasMainOverlayTex > 0.5 && _MainOverlayOpacity > 0.0)
                {
                    half4 overlay;
                    if (_MainOverlayUseMaskScale > 0.5)
                    {
                        float2 textureSize = max(_MainOverlayTex_TexelSize.zw, float2(1.0, 1.0));
                        float2 textureAspect = textureSize / min(textureSize.x, textureSize.y);
                        float2 maskUV = (shapeUV - 0.5) * imageAspect / textureAspect
                            / max(_MainOverlayScale, 0.1) + 0.5;
                        maskUV = maskUV * _MainOverlayTex_ST.xy + _MainOverlayTex_ST.zw;
                        overlay = SampleTransparencyMask(_MainOverlayTex, maskUV, textureSize);
                    }
                    else
                    {
                        float2 maskUV = (useGlow ? saturate(shapeUV) : shapeUV)
                            * _MainOverlayTex_ST.xy + _MainOverlayTex_ST.zw;
                        overlay = tex2D(_MainOverlayTex, maskUV);
                    }
                    // Derive transparency before tinting, so changing the overlay
                    // color does not change which parts of the texture are visible.
                    if (_MainOverlayBlackTransparent > 0.5)
                        overlay.a *= saturate(dot(overlay.rgb, half3(0.2126, 0.7152, 0.0722)));
                    overlay *= _MainOverlayColor;
                    half3 stainedColor = _MainOverlayBlend < 0.5 ? color.rgb * overlay.rgb : overlay.rgb;
                    color.rgb = lerp(color.rgb, stainedColor, saturate(_MainOverlayOpacity * overlay.a));
                }

                float2 uv = contourUV;
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
                float roundedDistance = NoisyRoundedCornersDistance(contourUV, IN.noisePosition, cornerRadius);
                float roundedAntialias = max(fwidth(roundedDistance), 0.0001);
                half roundedMask = saturate(0.5 - roundedDistance / roundedAntialias);
                if (_RoundCorners > 0.0) color.a *= roundedMask;

                if (_Outline > 0.5 && _OutlineThickness > 0.0)
                {
                    // Erode the same four noisy boundaries; reuse their tangent samples
                    // so the inner contour follows Torn Paper instead of a rectangle.
                    // Thickness follows the UI's scale rather than staying fixed in
                    // screen pixels. Keep derivatives for conversion and antialiasing.
                    float2 localUVGradientX = LocalUIGradient(contourUV.x, IN.noisePosition);
                    float2 localUVGradientY = LocalUIGradient(contourUV.y, IN.noisePosition);
                    float2 inset = _OutlineThickness * float2(
                        length(localUVGradientX), length(localUVGradientY));
                    half innerLeft = EdgeFade(uv.x - inset.x, _FadeLeftSize, _FadeLeftSoftness, leftNoise, 0.0);
                    half innerRight = EdgeFade(1.0 - uv.x - inset.x, _FadeRightSize, _FadeRightSoftness, rightNoise, 1.0);
                    half innerBottom = EdgeFade(uv.y - inset.y, _FadeBottomSize, _FadeBottomSoftness, bottomNoise, 2.0);
                    half innerTop = EdgeFade(1.0 - uv.y - inset.y, _FadeTopSize, _FadeTopSoftness, topNoise, 3.0);
                    half innerFade = max(max(innerLeft, innerRight), max(innerTop, innerBottom));
                    half outerShape = roundedMask;
                    // Convert thickness using the clean geometric normal. Differentiating
                    // the noisy distance amplifies texture/half-precision steps into spikes
                    // in the inner outline. Noise still defines both contour boundaries.
                    float2 uvDx = ddx(contourUV);
                    float2 uvDy = ddy(contourUV);
                    float2 uvPerPixel = max(sqrt(uvDx * uvDx + uvDy * uvDy), float2(0.000001, 0.000001));
                    float2 position = (contourUV - 0.5) / uvPerPixel;
                    float2 corner = abs(position) - (0.5 / uvPerPixel - cornerRadius);
                    float2 outsideCorner = max(corner, 0.0);
                    float outsideLength = length(outsideCorner);
                    float2 normal = outsideLength > 0.0001
                        ? outsideCorner / max(outsideLength, 0.0001)
                        : (corner.x > corner.y ? float2(1.0, 0.0) : float2(0.0, 1.0));
                    normal *= sign(position);
                    float2 localRoundedGradient = normal.x * localUVGradientX / uvPerPixel.x
                        + normal.y * localUVGradientY / uvPerPixel.y;
                    float roundedInset = _OutlineThickness * length(localRoundedGradient);
                    half innerShape = saturate(0.5 - (roundedDistance + roundedInset) / roundedAntialias);
                    half outlineMask = saturate((1.0 - edgeFade) * outerShape - (1.0 - innerFade) * innerShape);
                    half3 outlineColor = _OutlineColor.rgb;
                    if (_HasOutlineOverlayTex > 0.5 && _OutlineOverlayOpacity > 0.0)
                    {
                        half4 overlay;
                        if (_OutlineOverlayUseMaskScale > 0.5)
                        {
                            float2 textureSize = max(_OutlineOverlayTex_TexelSize.zw, float2(1.0, 1.0));
                            float2 textureAspect = textureSize / min(textureSize.x, textureSize.y);
                            float2 maskUV = (shapeUV - 0.5) * imageAspect / textureAspect
                                / max(_OutlineOverlayScale, 0.1) + 0.5;
                            maskUV = maskUV * _OutlineOverlayTex_ST.xy + _OutlineOverlayTex_ST.zw;
                            overlay = SampleTransparencyMask(_OutlineOverlayTex, maskUV, textureSize);
                        }
                        else
                        {
                            float2 maskUV = (useGlow ? saturate(shapeUV) : shapeUV)
                                * _OutlineOverlayTex_ST.xy + _OutlineOverlayTex_ST.zw;
                            overlay = tex2D(_OutlineOverlayTex, maskUV);
                        }
                        if (_OutlineOverlayBlackTransparent > 0.5)
                            overlay.a *= saturate(dot(overlay.rgb, half3(0.2126, 0.7152, 0.0722)));
                        overlay *= _OutlineOverlayColor;
                        half3 stainedColor = _OutlineOverlayBlend < 0.5 ? outlineColor * overlay.rgb : overlay.rgb;
                        outlineColor = lerp(outlineColor, stainedColor, saturate(_OutlineOverlayOpacity * overlay.a));
                    }
                    // Preserve sprite transparency and the UI tint/CanvasGroup opacity.
                    color.rgb = lerp(color.rgb, outlineColor, outlineMask * _OutlineColor.a);
                }

                float4 result = float4(color);

                if (useGlow)
                {
                    float4 edges = float4(
                        GlowEdgeDistance(uv.x, _FadeLeftSize, _FadeLeftSoftness, leftNoise, 0.0),
                        GlowEdgeDistance(1.0 - uv.x, _FadeRightSize, _FadeRightSoftness, rightNoise, 1.0),
                        GlowEdgeDistance(uv.y, _FadeBottomSize, _FadeBottomSoftness, bottomNoise, 2.0),
                        GlowEdgeDistance(1.0 - uv.y, _FadeTopSize, _FadeTopSoftness, topNoise, 3.0));
                    float2 edgeDistance = float2(max(edges.x, edges.y), max(edges.z, edges.w));
                    float distance = length(max(edgeDistance, 0.0)) + min(max(edgeDistance.x, edgeDistance.y), 0.0);
                    distance = max(distance, roundedDistance);
                    float softStart = _GlowSize * (1.0 - clamp(_GlowSoftness, 0.01, 1.0));
                    float glow = 1.0 - smoothstep(softStart, _GlowSize, max(distance, 0.0));

                    float insideImage = step(0.0, contourUV.x) * step(contourUV.x, 1.0)
                        * step(0.0, contourUV.y) * step(contourUV.y, 1.0);
                    result.a *= insideImage;
                    // Compose behind the original image/outline, then apply CanvasGroup
                    // opacity once. The sharp outline's original color stays on top.
                    float baseAlpha = IN.color.a > 0.0 ? saturate(result.a / IN.color.a) : 0.0;
                    float glowAlpha = saturate(glow * _GlowIntensity * _GlowColor.a);
                    float behindAlpha = glowAlpha * (1.0 - baseAlpha);
                    float combinedAlpha = baseAlpha + behindAlpha;
                    float3 glowColor = saturate(_GlowColor.rgb * _GlowIntensity);
                    result.rgb = (result.rgb * baseAlpha + glowColor * behindAlpha) / max(combinedAlpha, 0.000001);
                    result.a = combinedAlpha * IN.color.a;
                }

                // Multiply the complete composite so outline and glow cannot fill
                // the mask's holes. The existing margins multiply this opacity further.
                result.a *= transparencyMask;

                // Shader-driven fill also works on a Simple Image with no Source Image.
                // Apply to the complete result so the outline/glow cannot remain in empty space.
                if (_UseFill > 0.5 && _FillAmount < 1.0 && !roundedFill)
                {
                    float coordinate = _FillDirection < 0.5 ? shapeUV.x
                        : _FillDirection < 1.5 ? 1.0 - shapeUV.x
                        : _FillDirection < 2.5 ? shapeUV.y : 1.0 - shapeUV.y;
                    float fill = _FillAmount <= 0.0 ? 0.0
                        : saturate((_FillAmount - coordinate) / max(fwidth(coordinate), 0.000001) + 0.5);
                    result.a *= fill;
                }

                #ifdef UNITY_UI_CLIP_RECT
                half2 maskSoftness = saturate(
                    (_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw
                );

                result.a *= maskSoftness.x * maskSoftness.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }

            ENDCG
        }
    }

    CustomEditor "UIImageMarginFadeShaderGUI"
}
