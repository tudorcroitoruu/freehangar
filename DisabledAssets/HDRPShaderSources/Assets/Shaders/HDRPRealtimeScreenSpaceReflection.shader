// HDRP 17: sharp current-frame reflections for mirror surfaces.
// Requires camera Transparent Objects and Refraction (opaque color pyramid).
// Rays leaving the screen or hitting hidden geometry cannot produce a reflection.
Shader "Custom/HDRP/Realtime Screen-Space Reflection"
{
    Properties
    {
        [MainColor] _BaseColor("Miss Color", Color) = (0.025, 0.025, 0.025, 1)
        _SSRReflectionTint("Mirror Tint", Color) = (1, 1, 1, 1)
        _SSRReflectionStrength("Reflection Strength", Range(0, 1)) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 1
        _SSRMaxRayDistance("Reflection Distance", Range(1, 150)) = 60
        _SSRRayStartDistance("Ray Start Distance (Metres)", Range(0.001, 2)) = 0.005
        _SSRSurfaceOffset("Surface Offset (Metres)", Range(0, 0.1)) = 0.002
        _SSRNearTraceDistance("Close Detail Distance (Metres)", Range(0.05, 5)) = 0.5
        _SSRThickness("Hit Thickness (Metres)", Range(0.01, 1)) = 0.15
        [IntRange] _SSRMaxIterations("Ray Steps", Range(16, 128)) = 64
    }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Opaque" "Queue"="Transparent" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariablesFunctions.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SSRReflectionTint;
                float _SSRReflectionStrength;
                float _Smoothness;
                float _SSRMaxRayDistance;
                float _SSRThickness;
                float _SSRRayStartDistance;
                float _SSRSurfaceOffset;
                float _SSRNearTraceDistance;
                float _SSRMaxIterations;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            // Compare ray depth with the opaque camera surface in view-space metres.
            bool DepthDifference(float3 pointWS, out float2 uv, out float difference)
            {
                uv = 0; difference = -1;
                float4 clip = TransformWorldToHClip(pointWS);
                if (clip.w <= 0) return false;
                uv = ComputeNormalizedDeviceCoordinatesWithZ(pointWS, UNITY_MATRIX_VP).xy;
                if (any(uv <= 0) || any(uv >= 1)) return false;
                uint2 pixel = min((uint2)(uv * _ScreenSize.xy), (uint2)_ScreenSize.xy - 1);
                float depth = LoadCameraDepth(pixel);
                // HDRP uses reversed Z; the far clear value denotes sky.
                if (depth == UNITY_RAW_FAR_CLIP_VALUE) return true;
                float sceneDepth = LinearEyeDepth(depth, _ZBufferParams);
                float rayDepth = -TransformWorldToView(pointWS).z;
                difference = rayDepth - sceneDepth;
                return true;
            }

            bool TraceMirror(float3 origin, float3 direction, out float2 hitUV)
            {
                hitUV = 0;
                float startDistance = max(0.001, _SSRRayStartDistance);
                float previousDistance = startDistance;
                int steps = clamp((int)_SSRMaxIterations, 16, 128);
                float maxDistance = max(1, _SSRMaxRayDistance);
                // Stop before crossing the camera near plane instead of stepping past
                // it and discarding the entire ray when the camera is close.
                float3 originVS = TransformWorldToView(origin);
                float3 directionVS = mul((float3x3)UNITY_MATRIX_V, direction);
                if (directionVS.z > 0.00001)
                    maxDistance = min(maxDistance, (-_ProjectionParams.y - originVS.z) / directionVS.z * 0.99);
                if (maxDistance <= startDistance) return false;
                float nearDistance = min(startDistance + max(0.05, _SSRNearTraceDistance), maxDistance);
                [loop] for (int i = 1; i <= steps; i++)
                {
                    // Half the samples cover nearby objects; the rest reach distant geometry.
                    float fraction = (float)i / steps;
                    float nearFraction = saturate(fraction * 2);
                    float farFraction = saturate((fraction - 0.5) * 2);
                    float distance = fraction <= 0.5
                        ? lerp(startDistance, nearDistance, nearFraction * nearFraction)
                        : lerp(nearDistance, maxDistance, farFraction * farFraction);
                    float2 uv; float difference;
                    if (!DepthDifference(origin + direction * distance, uv, difference)) return false;
                    if (difference >= 0)
                    {
                        float low = previousDistance, high = distance;
                        [unroll] for (int j = 0; j < 6; j++)
                        {
                            float middle = (low + high) * 0.5;
                            float2 refinedUV; float refinedDifference;
                            if (!DepthDifference(origin + direction * middle, refinedUV, refinedDifference)) return false;
                            if (refinedDifference >= 0) high = middle; else low = middle;
                        }
                        if (!DepthDifference(origin + direction * high, hitUV, difference)) return false;
                        // Reject crossing an unrelated foreground silhouette.
                        if (difference >= 0 && difference <= max(0.01, _SSRThickness)) return true;
                    }
                    previousDistance = distance;
                }
                return false;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 missColor = _BaseColor.rgb * GetCurrentExposureMultiplier();
                if (_ColorPyramidLodCount < 1 || _SSRReflectionStrength <= 0) return float4(missColor, 1);
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 direction = reflect(-view, normal);
                float2 hitUV;
                if (!TraceMirror(input.positionWS + normal * max(0, _SSRSurfaceOffset), direction, hitUV)) return float4(missColor, 1);
                float mip = (1 - saturate(_Smoothness)) * max(0, _ColorPyramidLodCount - 1);
                float2 uv = hitUV * _RTHandleScaleHistory.xy;
                float2 texel = _ColorPyramidUvScaleAndLimitCurrentFrame.xy - _ColorPyramidUvScaleAndLimitCurrentFrame.zw;
                uv = clamp(uv, 0, max(0, _ColorPyramidUvScaleAndLimitCurrentFrame.xy - texel * exp2(1 + mip)));
                float3 reflected = SAMPLE_TEXTURE2D_X_LOD(_ColorPyramidTexture, s_trilinear_clamp_sampler, uv, mip).rgb;
                // No Fresnel dimming or projected-color fill: a mirror stays reflective face-on.
                return float4(lerp(missColor, reflected * _SSRReflectionTint.rgb, saturate(_SSRReflectionStrength)), 1);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/HDRP/FallbackError"
}
