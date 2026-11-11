// Shader-only, current-frame car-window reflections for HDRP 17.4 / Unity 6.
//
// This glass is rendered in HDRP's transparent Forward pass with alpha blending and
// no depth write. That is the first legal material stage where HDRP exposes both the
// opaque color pyramid and the hierarchical depth buffer without a feedback hazard.
// The reflection path reads only those two live camera buffers: it never samples a
// TextureCube, reflection probe, baked panorama or mesh UV for reflection placement.
// Accurate screen-space hits are completed with a deliberately soft projection of the
// visible scene, which keeps tunnel/road color across every window when a ray leaves
// the viewport while the alpha-blended glass still reveals the car interior.
//
// Required camera Frame Settings: Transparent Objects and Refraction.
// Screen-space detail is still limited to pixels present in the camera's opaque color
// and depth buffers. Off-screen or occluded geometry cannot be recovered by one camera.

Shader "Custom/HDRP/Realtime Screen-Space Window Reflection"
{
    Properties
    {
        [Header(Window Glass Surface)]
        [MainColor] _BaseColor("Glass Tint", Color) = (0.015, 0.035, 0.05, 1)
        [MainTexture] _BaseColorMap("Optional Tint Texture", 2D) = "white" {}

        [Enum(Front, 2, Back, 1, Both, 0)] _CullMode("Render Face", Float) = 2.0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.98
        _GlassOpacity("Glass Opacity", Range(0.02, 0.95)) = 0.18

        [Header(Player Local Rain)]
        [HideInInspector] _RainIntensity("Rain Intensity", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _RainWetness("Accumulated Rain Wetness", Range(0.0, 1.0)) = 0.0
        _RainWetSmoothness("Wet Glass Smoothness", Range(0.0, 1.0)) = 0.99
        _RainDropScale("Droplet Spatial Scale (Higher = Smaller)", Range(2.0, 100.0)) = 48.0
        _RainDropDensity("Droplet Density", Range(0.0, 1.5)) = 0.82
        _RainDropSpeed("Gravity Flow Speed", Range(0.0, 4.0)) = 0.82
        _RainDropNormalStrength("Droplet Normal Strength", Range(0.0, 1.0)) = 0.18
        [HideInInspector] _RainVehicleVelocityWS("Rain Vehicle Velocity WS", Vector) = (0, 0, 0, 0)
        [HideInInspector] _RainFlowOffsetWS("Rain Flow Offset WS", Vector) = (0, 0, 0, 0)

        // These toggle backing fields deliberately reuse otherwise-unused Standard
        // Lit fields so every pass keeps HDRP's SRP Batcher material-buffer layout.
        [Toggle(_MASKMAP)] _TransmissionMask("Use Mask Map (R: Metallic, G: AO, A: Smoothness)", Float) = 0.0
        [NoScaleOffset] _MaskMap("Mask Map", 2D) = "white" {}
        _MetallicRemapMin("Metallic Remap Min", Range(0.0, 1.0)) = 0.0
        _MetallicRemapMax("Metallic Remap Max", Range(0.0, 1.0)) = 1.0
        _AORemapMin("AO Remap Min", Range(0.0, 1.0)) = 0.0
        _AORemapMax("AO Remap Max", Range(0.0, 1.0)) = 1.0
        _SmoothnessRemapMin("Smoothness Remap Min", Range(0.0, 1.0)) = 0.0
        _SmoothnessRemapMax("Smoothness Remap Max", Range(0.0, 1.0)) = 1.0

        [Toggle(_NORMALMAP)] _IridescenceThickness("Use Normal Map", Float) = 0.0
        [Normal][NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Strength", Range(0.0, 2.0)) = 1.0

        [Toggle(_ALPHATEST_ON)] _AlphaCutoffEnable("Alpha Clipping", Float) = 0.0
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [HDR] _EmissiveColor("Emission Color", Color) = (0, 0, 0, 0)
        [Toggle(_EMISSIVE_COLOR_MAP)] _RayTracing("Use Emission Map", Float) = 0.0
        _EmissiveColorMap("Emission Map", 2D) = "white" {}
        _EmissiveExposureWeight("Emission Exposure Weight", Range(0.0, 1.0)) = 1.0

        [Header(Realtime Screen Space Reflection No Cubemap)]
        _SSRReflectionStrength("Reflection Strength", Range(0.0, 1.0)) = 0.85
        [HDR] _SSRReflectionTint("Reflection Tint", Color) = (1, 1, 1, 1)
        _SSRMaxRayDistance("Maximum Ray Distance", Range(0.25, 200.0)) = 120.0
        _SSRThickness("Relative Object Thickness", Range(0.001, 1.0)) = 0.01
        _SSRMaxIterations("Maximum Iterations", Range(8.0, 128.0)) = 32.0
        _SSRGroundMaxIterations("Downward Ray Iterations", Range(8.0, 128.0)) = 96.0
        _SSRGroundReflectionBias("Side Window Ground Bias", Range(0.0, 1.0)) = 0.1
        _SSRMaxDepthMip("Maximum Depth Mip", Range(0.0, 6.0)) = 6.0
        _SSRSelfIntersectionBias("Self-Intersection Bias", Range(0.0, 0.01)) = 0.001
        _SSRMinSmoothness("Minimum Smoothness", Range(0.0, 1.0)) = 0.45
        _SSRSmoothnessFadeWidth("Smoothness Fade Width", Range(0.001, 0.5)) = 0.2
        _SSRMinimumReflectance("Minimum Face-On Reflectance", Range(0.0, 0.5)) = 0.16
        _SSREdgeFadeWidth("Screen Edge Fade Width", Range(0.001, 0.25)) = 0.025
        _SSRDistanceFadeStart("Distance Fade Start", Range(0.0, 0.99)) = 0.85
        [Toggle] _SSRReflectSky("Use Current-Frame Visible Sky", Float) = 1.0
        _SSRScreenFillStrength("Visible Scene Fill On Miss", Range(0.0, 1.0)) = 0.65
        _SSRScreenFillCompression("Scene Fill Projection Compression", Range(0.05, 4.0)) = 0.75
        _SSRScreenFillLocality("Scene Fill Surface Locality", Range(0.0, 1.0)) = 0.2
        [Toggle] _SSRUseGeometryNormal("Ignore UV Normal Map For Reflections", Float) = 1.0

        // Fixed alpha-blended glass state. The shader must stay in the regular
        // transparent queue so the current color pyramid is available for sampling.
        [HideInInspector] _SurfaceType("__surfacetype", Float) = 1.0
        [HideInInspector] _BlendMode("__blendmode", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 10.0
        [HideInInspector] _DstBlend2("__dst2", Float) = 10.0
        [HideInInspector] _AlphaSrcBlend("__alphaSrc", Float) = 1.0
        [HideInInspector] _AlphaDstBlend("__alphaDst", Float) = 10.0
        [HideInInspector] _ZWrite("__zw", Float) = 0.0
        // Kept for compatibility with HDRP's Lit material property layout. All
        // passes use the public _CullMode control above.
        [HideInInspector] _CullModeForward("__cullmodeForward", Float) = 2.0
        [HideInInspector] _ZTestDepthEqualForOpaque("_ZTestDepthEqualForOpaque", Float) = 4.0
        [HideInInspector] _ZTestGBuffer("_ZTestGBuffer", Float) = 4.0
        [HideInInspector] _ZTestTransparent("_ZTestTransparent", Float) = 4.0
        [HideInInspector] _ZClip("_ZClip", Float) = 1.0
        [HideInInspector] _DoubleSidedConstants("_DoubleSidedConstants", Vector) = (1, 1, -1, 0)
        [HideInInspector] _EnableBlendModePreserveSpecularLighting("_EnableBlendModePreserveSpecularLighting", Float) = 1.0

        [HideInInspector] _StencilRef("_StencilRef", Int) = 0
        [HideInInspector] _StencilWriteMask("_StencilWriteMask", Int) = 6
        [HideInInspector] _StencilRefGBuffer("_StencilRefGBuffer", Int) = 2
        [HideInInspector] _StencilWriteMaskGBuffer("_StencilWriteMaskGBuffer", Int) = 11
        [HideInInspector] _StencilRefDepth("_StencilRefDepth", Int) = 0
        [HideInInspector] _StencilWriteMaskDepth("_StencilWriteMaskDepth", Int) = 8
        [HideInInspector] _StencilRefMV("_StencilRefMV", Int) = 32
        [HideInInspector] _StencilWriteMaskMV("_StencilWriteMaskMV", Int) = 40

        // HDRP Lit defaults required by LitData.hlsl. Features not listed in the
        // public UI above are deliberately fixed off to reduce variants and work.
        [HideInInspector] _MaterialID("_MaterialID", Int) = 1
        [HideInInspector] _SpecularOcclusionMode("_SpecularOcclusionMode", Int) = 1
        [HideInInspector] _EnergyConservingSpecularColor("_EnergyConservingSpecularColor", Float) = 1.0
        [HideInInspector] _CoatMask("_CoatMask", Float) = 0.0
        [HideInInspector] _SpecularColor("_SpecularColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _HeightAmplitude("_HeightAmplitude", Float) = 0.02
        [HideInInspector] _HeightCenter("_HeightCenter", Float) = 0.5
        [HideInInspector] _Thickness("_Thickness", Float) = 1.0
        [HideInInspector] _PPDMaxSamples("_PPDMaxSamples", Float) = 15.0
        [HideInInspector] _PPDMinSamples("_PPDMinSamples", Float) = 5.0
        [HideInInspector] _PPDLodThreshold("_PPDLodThreshold", Float) = 5.0
        [HideInInspector] _SpecularAAScreenSpaceVariance("_SpecularAAScreenSpaceVariance", Float) = 1.0
        [HideInInspector] _SpecularAAThreshold("_SpecularAAThreshold", Float) = 0.2
        [HideInInspector] _Anisotropy("_Anisotropy", Float) = 0.0
        [HideInInspector] _SubsurfaceMask("_SubsurfaceMask", Float) = 1.0
        [HideInInspector] _IridescenceMask("_IridescenceMask", Float) = 1.0
        [HideInInspector] _NormalMapOS("_NormalMapOS", 2D) = "white" {}
        [HideInInspector] _DetailMap("_DetailMap", 2D) = "linearGrey" {}
        [HideInInspector] _DetailAlbedoScale("_DetailAlbedoScale", Float) = 1.0
        [HideInInspector] _DetailNormalScale("_DetailNormalScale", Float) = 1.0
        [HideInInspector] _DetailSmoothnessScale("_DetailSmoothnessScale", Float) = 1.0
        [HideInInspector] _AlphaRemapMin("_AlphaRemapMin", Float) = 0.0
        [HideInInspector] _AlphaRemapMax("_AlphaRemapMax", Float) = 1.0
        [HideInInspector] _AlphaCutoffShadow("_AlphaCutoffShadow", Float) = 0.5
        [HideInInspector] _AlphaCutoffPrepass("_AlphaCutoffPrepass", Float) = 0.5
        [HideInInspector] _AlphaCutoffPostpass("_AlphaCutoffPostpass", Float) = 0.5
        [HideInInspector] _UseShadowThreshold("_UseShadowThreshold", Float) = 0.0
        [HideInInspector] _AlbedoAffectEmissive("_AlbedoAffectEmissive", Float) = 0.0
        [HideInInspector] _TransmittanceColor("_TransmittanceColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _Ior("_Ior", Float) = 1.5
        [HideInInspector] _ATDistance("_ATDistance", Float) = 1.0
        [HideInInspector] _InvPrimScale("_InvPrimScale", Vector) = (1, 1, 0, 0)
        [HideInInspector] _ThicknessRemap("_ThicknessRemap", Vector) = (0, 1, 0, 0)
        [HideInInspector] _IridescenceThicknessRemap("_IridescenceThicknessRemap", Vector) = (0, 1, 0, 0)
        [HideInInspector] _TexWorldScale("_TexWorldScale", Float) = 1.0
        [HideInInspector] _InvTilingScale("_InvTilingScale", Float) = 1.0
        [HideInInspector] _UVMappingMask("_UVMappingMask", Vector) = (1, 0, 0, 0)
        [HideInInspector] _UVDetailsMappingMask("_UVDetailsMappingMask", Vector) = (1, 0, 0, 0)
        [HideInInspector] _LinkDetailsWithBase("_LinkDetailsWithBase", Float) = 1.0
        [HideInInspector] _ObjectSpaceUVMapping("_ObjectSpaceUVMapping", Float) = 0.0
        [HideInInspector] _TexWorldScaleEmissive("_TexWorldScaleEmissive", Float) = 1.0
        [HideInInspector] _UVMappingMaskEmissive("_UVMappingMaskEmissive", Vector) = (1, 0, 0, 0)
        [HideInInspector] _ObjectSpaceUVMappingEmissive("_ObjectSpaceUVMappingEmissive", Float) = 0.0
        [HideInInspector] _EmissionColor("_EmissionColor", Color) = (1, 1, 1, 1)

        [HideInInspector] _MainTex("Albedo", 2D) = "white" {}
        [HideInInspector] _Color("Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_BaseColorMap); SAMPLER(sampler_BaseColorMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseColorMap_ST;
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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
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
                output.uv = input.uv * _BaseColorMap_ST.xy + _BaseColorMap_ST.zw;
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
                uint2 pixel = min((uint2)(uv * _ScaledScreenParams.xy), (uint2)_ScaledScreenParams.xy - 1);
                float depth = LoadSceneDepth(pixel);
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
                float4 surface = SAMPLE_TEXTURE2D(_BaseColorMap, sampler_BaseColorMap, input.uv) * _BaseColor;
                float3 missColor = surface.rgb;
                if (_SSRReflectionStrength <= 0) return float4(missColor, surface.a);
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 direction = reflect(-view, normal);
                float2 hitUV;
                if (!TraceMirror(input.positionWS + normal * max(0, _SSRSurfaceOffset), direction, hitUV)) return float4(missColor, surface.a);
                float3 reflected = SampleSceneColor(hitUV);
                return float4(lerp(missColor, reflected * _SSRReflectionTint.rgb, saturate(_SSRReflectionStrength)), surface.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
