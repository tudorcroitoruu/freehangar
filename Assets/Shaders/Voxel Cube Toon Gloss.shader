Shader "Color Sculpt/Voxel Cube Toon Gloss"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Color("Legacy Color", Color) = (1, 1, 1, 1)

        [Header(Toon Lighting)]
        _GlobalBrightness("Overall Brightness", Range(0.5, 1.5)) = 1.1
        _LightColorInfluence("Light Color Influence", Range(0, 1)) = 0.14
        _ShadowBrightness("Shadow Brightness", Range(0, 1)) = 0.69
        _MidBrightness("Midtone Brightness", Range(0, 1.25)) = 0.94
        _LitBrightness("Lit Brightness", Range(0, 1.5)) = 1.16
        _MidThreshold("Shadow To Mid", Range(-0.5, 1)) = -0.02
        _LitThreshold("Mid To Lit", Range(-0.5, 1)) = 0.5
        _BandSoftness("Band Softness", Range(0.001, 0.25)) = 0.13
        _RealtimeShadowStrength("Realtime Shadow Strength", Range(0, 1)) = 0.27

        [Header(Cozy Color)]
        [HDR] _WarmLightTint("Warm Light Tint", Color) = (1.15, 0.97, 0.77, 1)
        _SoftShadowTint("Soft Shadow Tint", Color) = (0.82, 0.68, 0.66, 1)
        _CozyTintStrength("Cozy Tint Strength", Range(0, 0.5)) = 0.27
        _AmbientFill("Warm Ambient Fill", Range(0, 0.4)) = 0.16

        [Header(Palette Finish)]
        _PaletteSaturation("Palette Saturation", Range(0.5, 1.5)) = 1.13
        _PaletteLift("Palette Soft Lift", Range(0.5, 1.25)) = 0.8
        _BevelShadowStrength("Rounded Edge Depth", Range(0, 0.35)) = 0.025
        _BevelLightStrength("Rounded Edge Light", Range(0, 0.25)) = 0.035
        _CreviceAOStrength("Crevice AO Strength", Range(0, 0.5)) = 0.16
        _CreviceAOPower("Crevice AO Tightness", Range(0.5, 4)) = 1.35

        [Header(Glossy Toy Finish)]
        _Glossiness("Glossiness", Range(0, 1)) = 0.36
        _GlossStrength("Highlight Strength", Range(0, 1)) = 0.17
        _HighlightSoftness("Highlight Softness", Range(0.01, 0.25)) = 0.19
        _EdgeSheenStrength("Edge Sheen Strength", Range(0, 0.25)) = 0.012
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _GlobalBrightness;
            half _LightColorInfluence;
            half _ShadowBrightness;
            half _MidBrightness;
            half _LitBrightness;
            half _MidThreshold;
            half _LitThreshold;
            half _BandSoftness;
            half _RealtimeShadowStrength;
            half4 _WarmLightTint;
            half4 _SoftShadowTint;
            half _CozyTintStrength;
            half _AmbientFill;
            half _PaletteSaturation;
            half _PaletteLift;
            half _BevelShadowStrength;
            half _BevelLightStrength;
            half _CreviceAOStrength;
            half _CreviceAOPower;
            half _Glossiness;
            half _GlossStrength;
            half _HighlightSoftness;
            half _EdgeSheenStrength;
        CBUFFER_END

        UNITY_INSTANCING_BUFFER_START(VoxelCubePerInstance)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
        UNITY_INSTANCING_BUFFER_END(VoxelCubePerInstance)
        ENDHLSL

        Pass
        {
            Name "VoxelToonForward"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(
                    TransformObjectToWorldNormal(input.normalOS));
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half ToonThreshold(half value, half threshold)
            {
                half edgeWidth = max(fwidth(value), _BandSoftness);
                return smoothstep(
                    threshold - edgeWidth,
                    threshold + edgeWidth,
                    value);
            }

            half GetToonBrightness(half nDotL)
            {
                half middleBand = ToonThreshold(nDotL, _MidThreshold);
                half brightBand = ToonThreshold(
                    nDotL,
                    max(_LitThreshold, _MidThreshold + 0.01h));
                half brightness = lerp(
                    _ShadowBrightness,
                    _MidBrightness,
                    middleBand);
                return lerp(brightness, _LitBrightness, brightBand);
            }

            half3 GetGloss(
                half3 normalWS,
                half3 viewDirectionWS,
                Light light,
                half nDotL)
            {
                half attenuation = saturate(
                    light.distanceAttenuation * light.shadowAttenuation);
                half3 halfDirectionWS = SafeNormalize(
                    light.direction + viewDirectionWS);
                half nDotH = saturate(dot(normalWS, halfDirectionWS));
                half specularPower = exp2(lerp(3.0h, 8.0h, _Glossiness));
                half specularShape = pow(nDotH, specularPower);
                half toonSpecular = smoothstep(
                    0.18h - _HighlightSoftness,
                    0.18h + _HighlightSoftness,
                    specularShape);
                toonSpecular *= _GlossStrength * attenuation * step(-0.02h, nDotL);
                half lightPeak = max(
                    light.color.r,
                    max(light.color.g, light.color.b));
                half3 normalizedLightColor = light.color / max(lightPeak, 1.0h);
                return normalizedLightColor * toonSpecular;
            }

            half3 GradePaletteColor(half3 sourceColor)
            {
                half luminance = dot(
                    sourceColor,
                    half3(0.2126h, 0.7152h, 0.0722h));
                half3 saturatedColor = lerp(
                    half3(luminance, luminance, luminance),
                    sourceColor,
                    _PaletteSaturation);
                return pow(saturate(saturatedColor), _PaletteLift);
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(
                    GetWorldSpaceViewDir(input.positionWS));
                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(
                    VoxelCubePerInstance,
                    _BaseColor);
                baseColor.rgb = GradePaletteColor(baseColor.rgb);
                Light mainLight = GetMainLight(input.shadowCoord);
                half nDotL = dot(normalWS, mainLight.direction);
                half attenuation = saturate(
                    mainLight.distanceAttenuation * mainLight.shadowAttenuation);

                half brightness = GetToonBrightness(nDotL);
                brightness *= lerp(
                    1.0h - _RealtimeShadowStrength,
                    1.0h,
                    attenuation);

                half lightPeak = max(
                    mainLight.color.r,
                    max(mainLight.color.g, mainLight.color.b));
                half hasMainLight = step(0.001h, lightPeak);
                brightness = lerp(_MidBrightness, brightness, hasMainLight);
                half3 lightTint = mainLight.color / max(lightPeak, 0.001h);
                lightTint = lerp(
                    half3(1.0h, 1.0h, 1.0h),
                    lightTint,
                    hasMainLight * _LightColorInfluence);

                half lightFacing = saturate(nDotL * 0.5h + 0.5h);
                half3 cozyTint = lerp(
                    _SoftShadowTint.rgb,
                    _WarmLightTint.rgb,
                    lightFacing);
                lightTint *= lerp(
                    half3(1.0h, 1.0h, 1.0h),
                    cozyTint,
                    _CozyTintStrength);

                half3 color = baseColor.rgb * brightness * lightTint;
                half upwardFacing = saturate(normalWS.y * 0.5h + 0.5h);
                half3 ambientTint = lerp(
                    _SoftShadowTint.rgb,
                    _WarmLightTint.rgb,
                    upwardFacing);
                color += baseColor.rgb * ambientTint * _AmbientFill;
                color += GetGloss(
                    normalWS,
                    viewDirectionWS,
                    mainLight,
                    nDotL) * hasMainLight;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u;
                         lightIndex < additionalLightCount;
                         ++lightIndex)
                    {
                        Light light = GetAdditionalLight(lightIndex, input.positionWS);
                        half additionalNdotL = dot(normalWS, light.direction);
                        half additionalAttenuation = saturate(
                            light.distanceAttenuation * light.shadowAttenuation);
                        half wrappedDiffuse = saturate(additionalNdotL * 0.5h + 0.5h);
                        color += baseColor.rgb * light.color *
                            wrappedDiffuse * additionalAttenuation * 0.2h;
                        color += GetGloss(
                            normalWS,
                            viewDirectionWS,
                            light,
                            additionalNdotL) * 0.5h;
                    }
                #endif

                half nDotV = saturate(dot(normalWS, viewDirectionWS));
                half edgeSheen = pow(1.0h - nDotV, 4.0h) * _EdgeSheenStrength;
                color += lerp(
                    baseColor.rgb,
                    half3(1.0h, 1.0h, 1.0h),
                    0.85h) * edgeSheen;

                // The rounded cube mesh has axis-aligned face normals and
                // blended normals around its chamfer. Use that distinction to
                // give every cube a soft toy-like bevel even when it is merged
                // into the editor preview mesh.
                half dominantNormal = max(
                    abs(normalWS.x),
                    max(abs(normalWS.y), abs(normalWS.z)));
                half bevelAmount = saturate((1.0h - dominantNormal) * 3.4h);
                half bevelFacing = saturate(nDotL * 0.5h + 0.5h);
                half creviceAO = pow(bevelAmount, _CreviceAOPower) *
                                  _CreviceAOStrength;
                color *= 1.0h - creviceAO;
                color *= 1.0h - bevelAmount * _BevelShadowStrength *
                    (1.0h - bevelFacing);
                color += baseColor.rgb * bevelAmount * _BevelLightStrength *
                    bevelFacing * bevelFacing;

                color *= _GlobalBrightness;
                color = MixFog(color, input.fogFactor);
                return half4(color, baseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0.0h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0.0h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings DepthNormalsVertex(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = NormalizeNormalPerVertex(
                    TransformObjectToWorldNormal(input.normalOS));
                return output;
            }

            half4 DepthNormalsFragment(DepthNormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    return half4(PackFloat2To888(remappedOctNormalWS), 0.0h);
                #else
                    return half4(normalWS, 0.0h);
                #endif
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
