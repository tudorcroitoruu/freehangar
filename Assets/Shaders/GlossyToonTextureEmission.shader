Shader "Glossy Toon Texture Emission"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)

        [Header(Emission)]
        _EmissionMap("Emission Texture", 2D) = "black" {}
        [HDR] _EmissionColor("Emission Color", Color) = (1, 1, 1, 1)
        _EmissionStrength("Emission Strength", Range(0.0, 10.0)) = 1.0

        [Header(Global Look)]
        _GlobalBrightness("Overall Brightness", Range(0.25, 2.0)) = 1.0
        _LightColorInfluence("Light Color Influence", Range(0.0, 1.0)) = 0.35

        [Header(Toon Lighting)]
        _ShadowBrightness("Shadow Brightness", Range(0.0, 1.0)) = 0.46
        _MidBrightness("Midtone Brightness", Range(0.0, 1.25)) = 0.74
        _LitBrightness("Lit Brightness", Range(0.0, 1.5)) = 0.98
        _MidThreshold("Shadow To Mid Threshold", Range(-0.5, 1.0)) = 0.05
        _LitThreshold("Mid To Lit Threshold", Range(-0.5, 1.0)) = 0.62
        _BandSoftness("Band Softness", Range(0.001, 0.25)) = 0.015
        _RealtimeShadowStrength("Realtime Shadow Strength", Range(0.0, 1.0)) = 0.5

        [Header(Plastic Gloss)]
        _Glossiness("Glossiness", Range(0.0, 1.0)) = 0.52
        _GlossStrength("Highlight Strength", Range(0.0, 1.0)) = 0.32
        _HighlightSoftness("Highlight Softness", Range(0.01, 0.25)) = 0.1
        _EdgeSheenStrength("Edge Sheen Strength", Range(0.0, 0.25)) = 0.045
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

        // Every pass must use the exact same UnityPerMaterial layout. Reusing passes
        // from Glossy Toon Color gave Metal a different constant-buffer layout.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);
        SAMPLER(sampler_EmissionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _EmissionMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _EmissionStrength;
            half _GlobalBrightness;
            half _LightColorInfluence;
            half _ShadowBrightness;
            half _MidBrightness;
            half _LitBrightness;
            half _MidThreshold;
            half _LitThreshold;
            half _BandSoftness;
            half _RealtimeShadowStrength;
            half _Glossiness;
            half _GlossStrength;
            half _HighlightSoftness;
            half _EdgeSheenStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "GlossyToonForward"
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
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float2 baseUV : TEXCOORD4;
                float2 emissionUV : TEXCOORD5;
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
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.baseUV = TRANSFORM_TEX(input.uv, _BaseMap);
                output.emissionUV = TRANSFORM_TEX(input.uv, _EmissionMap);
                return output;
            }

            half ToonThreshold(half value, half threshold)
            {
                half edgeWidth = max(fwidth(value), _BandSoftness);
                return smoothstep(threshold - edgeWidth, threshold + edgeWidth, value);
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV) * _BaseColor;
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.emissionUV).rgb;
                emission *= _EmissionColor.rgb * _EmissionStrength;

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight(input.shadowCoord);

                half nDotL = dot(normalWS, mainLight.direction);
                half middleBand = ToonThreshold(nDotL, _MidThreshold);
                half brightBand = ToonThreshold(nDotL, max(_LitThreshold, _MidThreshold + 0.01h));

                half brightness = lerp(_ShadowBrightness, _MidBrightness, middleBand);
                brightness = lerp(brightness, _LitBrightness, brightBand);
                half attenuation = saturate(mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                brightness *= lerp(1.0h - _RealtimeShadowStrength, 1.0h, attenuation);

                half lightPeak = max(mainLight.color.r, max(mainLight.color.g, mainLight.color.b));
                half hasMainLight = step(0.001h, lightPeak);
                brightness = lerp(0.78h, brightness, hasMainLight);

                half3 normalizedLightTint = mainLight.color / max(lightPeak, 0.001h);
                normalizedLightTint = lerp(half3(1.0h, 1.0h, 1.0h), normalizedLightTint,
                    hasMainLight * _LightColorInfluence);
                half3 color = baseSample.rgb * brightness * normalizedLightTint;

                half3 halfDirectionWS = SafeNormalize(mainLight.direction + viewDirectionWS);
                half nDotH = saturate(dot(normalWS, halfDirectionWS));
                half specularPower = exp2(lerp(3.0h, 8.0h, _Glossiness));
                half specularShape = pow(nDotH, specularPower);
                half toonSpecular = smoothstep(0.18h - _HighlightSoftness,
                    0.18h + _HighlightSoftness, specularShape);
                toonSpecular *= _GlossStrength * attenuation * hasMainLight * step(-0.02h, nDotL);

                half nDotV = saturate(dot(normalWS, viewDirectionWS));
                half edgeSheen = pow(1.0h - nDotV, 4.0h) * _EdgeSheenStrength;
                half3 sheenColor = lerp(baseSample.rgb, half3(1.0h, 1.0h, 1.0h), 0.88h);
                color += sheenColor * (toonSpecular + edgeSheen);

                color = color * _GlobalBrightness + emission;
                color = MixFog(color, input.fogFactor);
                return half4(color, baseSample.a);
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

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
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
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    return half4(packedNormalWS, 0.0h);
                #else
                    return half4(normalWS, 0.0h);
                #endif
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
