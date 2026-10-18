Shader "Glossy Toon Color"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 0.45, 0.08, 1)

        [Header(Surface Detail)]
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0.0, 2.0)) = 1.0

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

        [Header(Outline)]
        _OutlineWidth("Outline Width (Pixels)", Range(0.0, 12.0)) = 2.0
        _OutlineFallbackColor("Outline Fallback", Color) = (1.0, 0.42, 0.12, 1.0)
        [HideInInspector] _OutlineEnabled("Outline Enabled", Float) = 1.0
        [HideInInspector] _FlightDeform("Flight Deform", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _HitDissolve("Hit Dissolve", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _FlightDirectionOS("Flight Direction OS", Vector) = (0, 0, 1, 0)
        [HideInInspector] _FlightCenterOS("Flight Center OS", Vector) = (0, 0, 0, 0)
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
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BumpMap_ST;
            half4 _BaseColor;
            half _BumpScale;
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
            half _OutlineWidth;
            half4 _OutlineFallbackColor;
            half _OutlineEnabled;
        CBUFFER_END

        // Set once per level by GameManager. Alpha is also the "has sampled color" flag,
        // allowing the material fallback to remain useful in previews and isolated scenes.
        half4 _BrickOutlineColor;
        float _FlightDeform;
        float _HitDissolve;
        float4 _FlightDirectionOS;
        float4 _FlightCenterOS;

        float3 ApplyFlightDeformationOS(float3 positionOS)
        {
            float amount = saturate(_FlightDeform);
            float directionLengthSq = dot(_FlightDirectionOS.xyz, _FlightDirectionOS.xyz);
            float3 directionOS = directionLengthSq > 0.0001
                ? _FlightDirectionOS.xyz * rsqrt(directionLengthSq)
                : float3(0.0, 0.0, 1.0);
            float3 offset = positionOS - _FlightCenterOS.xyz;
            float along = dot(offset, directionOS);
            float3 parallelOffset = directionOS * along;
            float3 perpendicularOffset = offset - parallelOffset;

            // Loose pieces elongate in their travel direction and compress across
            // it. This is vertex-only: Rigidbody mass and colliders stay reliable.
            parallelOffset *= 1.0 + amount * 0.48;
            perpendicularOffset *= 1.0 - amount * 0.2;
            return _FlightCenterOS.xyz + parallelOffset + perpendicularOffset;
        }

        float HitDissolveNoise(float3 positionWS)
        {
            float3 cell = floor(positionWS * 5.0);
            return frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
        }
        ENDHLSL

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest LEqual

            // Every brick writes bit 6 in its regular forward pass. Rejecting those
            // pixels here turns all bricks into one combined level silhouette, so
            // shared/internal brick edges do not receive an outline.
            Stencil
            {
                Ref 64
                ReadMask 64
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing

            struct OutlineAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormalOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct OutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            OutlineVaryings OutlineVertex(OutlineAttributes input)
            {
                OutlineVaryings output = (OutlineVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 deformedPositionOS = ApplyFlightDeformationOS(input.positionOS.xyz);
                float3 positionWS = TransformObjectToWorld(deformedPositionOS);
                float hasSmoothNormal = step(0.0001, dot(input.smoothNormalOS, input.smoothNormalOS));
                float3 outlineNormalOS = normalize(lerp(
                    input.normalOS,
                    input.smoothNormalOS,
                    hasSmoothNormal));
                float3 normalWS = TransformObjectToWorldNormal(outlineNormalOS);
                output.positionCS = TransformWorldToHClip(positionWS);

                // Expand the inverted hull in clip space so the rim stays the same
                // thickness on screen as bricks move toward or away from the camera.
                float3 projectedNormal = mul((float3x3)UNITY_MATRIX_VP, normalWS);
                float normalLength = max(length(projectedNormal.xy), 0.0001);
                float2 outlineDirection = projectedNormal.xy / normalLength;
                float2 pixelSizeCS = 2.0 * output.positionCS.w / _ScreenParams.xy;
                output.positionCS.xy += outlineDirection * pixelSizeCS * _OutlineWidth;
                return output;
            }

            half4 OutlineFragment(OutlineVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // A loose/deformed brick disables this per renderer, without changing
                // the shared material used by the rest of the intact level.
                clip(_OutlineEnabled - 0.5h);

                half hasLevelColor = step(0.001h, _BrickOutlineColor.a);
                half3 outlineColor = lerp(
                    _OutlineFallbackColor.rgb,
                    _BrickOutlineColor.rgb,
                    hasLevelColor);
                return half4(outlineColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "GlossyToonForward"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual

            // Reserve one stencil bit as the union mask for the complete brick level.
            // The outline pass reads this shared mask instead of outlining each mesh.
            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp Always
                Pass Replace
            }

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
                float4 tangentOS : TANGENT;
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
                half4 tangentWS : TEXCOORD4;
                float2 normalUV : TEXCOORD5;
                float2 baseUV : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 deformedPositionOS = ApplyFlightDeformationOS(input.positionOS.xyz);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(deformedPositionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                half tangentSign = input.tangentOS.w * GetOddNegativeScale();

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.tangentWS = half4(
                    NormalizeNormalPerVertex(normalInputs.tangentWS),
                    tangentSign);
                output.normalUV = TRANSFORM_TEX(input.uv, _BumpMap);
                output.baseUV = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half3 GetMappedNormalWS(Varyings input)
            {
                half3 vertexNormalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 tangentWS = NormalizeNormalPerPixel(input.tangentWS.xyz);
                half3 bitangentWS = input.tangentWS.w * cross(vertexNormalWS, tangentWS);
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.normalUV),
                    _BumpScale);
                return NormalizeNormalPerPixel(TransformTangentToWorld(
                    normalTS,
                    half3x3(tangentWS, bitangentWS, vertexNormalWS)));
            }

            half ToonThreshold(half value, half threshold)
            {
                // Pixel derivatives soften only the edge of each otherwise-flat band.
                half edgeWidth = max(fwidth(value), _BandSoftness);
                return smoothstep(threshold - edgeWidth, threshold + edgeWidth, value);
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(HitDissolveNoise(input.positionWS) - saturate(_HitDissolve) * 1.01);

                half4 baseSample = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    input.baseUV) * _BaseColor;
                half3 normalWS = GetMappedNormalWS(input);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight(input.shadowCoord);

                half nDotL = dot(normalWS, mainLight.direction);
                half middleBand = ToonThreshold(nDotL, _MidThreshold);
                half brightBand = ToonThreshold(nDotL, max(_LitThreshold, _MidThreshold + 0.01h));

                half brightness = lerp(_ShadowBrightness, _MidBrightness, middleBand);
                brightness = lerp(brightness, _LitBrightness, brightBand);
                half attenuation = saturate(mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                brightness *= lerp(1.0h - _RealtimeShadowStrength, 1.0h, attenuation);

                // Keep the chosen color readable even if a scene has no main light.
                half lightPeak = max(mainLight.color.r, max(mainLight.color.g, mainLight.color.b));
                half hasMainLight = step(0.001h, lightPeak);
                brightness = lerp(0.78h, brightness, hasMainLight);

                half3 normalizedLightTint = mainLight.color / max(lightPeak, 0.001h);
                normalizedLightTint = lerp(
                    half3(1.0h, 1.0h, 1.0h),
                    normalizedLightTint,
                    hasMainLight * _LightColorInfluence);
                half3 color = baseSample.rgb * brightness * normalizedLightTint;

                // A compact, slightly hard highlight gives the surface toy-plastic gloss.
                half3 halfDirectionWS = SafeNormalize(mainLight.direction + viewDirectionWS);
                half nDotH = saturate(dot(normalWS, halfDirectionWS));
                half specularPower = exp2(lerp(3.0h, 8.0h, _Glossiness));
                half specularShape = pow(nDotH, specularPower);
                half toonSpecular = smoothstep(
                    0.18h - _HighlightSoftness,
                    0.18h + _HighlightSoftness,
                    specularShape);
                toonSpecular *= _GlossStrength * attenuation * hasMainLight * step(-0.02h, nDotL);

                // Very light Fresnel sheen rounds beveled edges without drawing an outline.
                half nDotV = saturate(dot(normalWS, viewDirectionWS));
                half edgeSheen = pow(1.0h - nDotV, 4.0h) * _EdgeSheenStrength;
                // Automatic flight deformation keeps its sheen based on the
                // authored base texture instead of washing stretched pieces white.
                half sheenWhiteness = lerp(0.88h, 0.12h, saturate(_FlightDeform));
                half3 sheenColor = lerp(
                    baseSample.rgb,
                    half3(1.0h, 1.0h, 1.0h),
                    sheenWhiteness);
                color += sheenColor * (toonSpecular + edgeSheen);

                color *= _GlobalBrightness;
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

                float3 deformedPositionOS = ApplyFlightDeformationOS(input.positionOS.xyz);
                float3 positionWS = TransformObjectToWorld(deformedPositionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
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
                output.positionCS = TransformObjectToHClip(
                    ApplyFlightDeformationOS(input.positionOS.xyz));
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
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half4 tangentWS : TEXCOORD1;
                float2 normalUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings DepthNormalsVertex(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(
                    ApplyFlightDeformationOS(input.positionOS.xyz));
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                half tangentSign = input.tangentOS.w * GetOddNegativeScale();
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.tangentWS = half4(
                    NormalizeNormalPerVertex(normalInputs.tangentWS),
                    tangentSign);
                output.normalUV = TRANSFORM_TEX(input.uv, _BumpMap);
                return output;
            }

            half4 DepthNormalsFragment(DepthNormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 vertexNormalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 tangentWS = NormalizeNormalPerPixel(input.tangentWS.xyz);
                half3 bitangentWS = input.tangentWS.w * cross(vertexNormalWS, tangentWS);
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.normalUV),
                    _BumpScale);
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(
                    normalTS,
                    half3x3(tangentWS, bitangentWS, vertexNormalWS)));
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
