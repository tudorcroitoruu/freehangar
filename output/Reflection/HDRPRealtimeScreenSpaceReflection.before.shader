// Shader-only, current-frame screen-space reflections for HDRP 17.4 / Unity 6.
//
// This material is intentionally rendered in HDRP's transparent Forward pass even
// though it writes depth and looks opaque. That is the first legal material stage
// where HDRP exposes both the opaque color pyramid and the hierarchical depth buffer.
// The reflection path reads only those two live camera buffers: it never samples a
// TextureCube, reflection probe, baked panorama or mesh UV for reflection placement.
// Accurate screen-space hits are completed with a deliberately soft projection of the
// visible scene, which keeps tunnel/road color across body pixels whose ray leaves the
// viewport without reverting to a six-face environment.
//
// Required camera Frame Settings: Transparent Objects and Refraction.
// Screen-space detail is still limited to pixels present in the camera's opaque color
// and depth buffers. Off-screen or occluded geometry cannot be recovered by one camera.
// Automotive body layering is opt-in because this shader is also used by glass,
// interior, wheel, and trim materials. AutomotivePaintDamage supplies per-vehicle
// wheel placement and recent scrape segments through material property blocks.

Shader "Custom/HDRP/Realtime Screen-Space Reflection"
{
    Properties
    {
        [Header(Standard Surface)]
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseColorMap("Base Texture", 2D) = "white" {}

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.15
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.88

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

        // Kept separate from HDRP's surface maps so generated AO can use a
        // non-overlapping UV1 atlas while source color/normal/mask maps keep UV0.
        [NoScaleOffset] _BakedAOMap("Baked AO (UV1, Grayscale)", 2D) = "white" {}
        _BakedAOStrength("Baked AO Strength", Range(0.0, 1.0)) = 1.0
        [Normal][NoScaleOffset] _BakedNormalMap("Baked Bent Normal (UV1)", 2D) = "bump" {}
        _BakedNormalStrength("Baked Normal Strength", Range(0.0, 1.0)) = 0.0

        [Toggle(_NORMALMAP)] _IridescenceThickness("Use Normal Map", Float) = 0.0
        [Normal][NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Strength", Range(0.0, 2.0)) = 1.0

        [Header(Automotive Body Paint)]
        [Toggle] _CarBodyEffectsEnable("Enable Body Paint, Dirt And Scratches", Float) = 0.0
        _CoatMask("Clear Coat", Range(0.0, 1.0)) = 0.0
        [NoScaleOffset] _CoatMaskMap("Clear Coat Mask", 2D) = "white" {}
        _PaintColorDepth("Grazing Color Depth", Range(0.0, 1.0)) = 0.18
        [HDR] _PaintFlakeTint("Metal Flake Tint", Color) = (0.55, 0.62, 0.75, 1)
        _PaintFlakeStrength("Metal Flake Strength", Range(0.0, 1.0)) = 0.12
        _PaintFlakeScale("Metal Flake Scale", Range(4.0, 1250.0)) = 90.0

        [Header(Player Local Rain)]
        _RainIntensity("Rain Intensity (Runtime Per Player)", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _RainWetness("Accumulated Rain Wetness", Range(0.0, 1.0)) = 0.0
        _RainWetSmoothness("Wet Paint Smoothness", Range(0.0, 1.0)) = 0.97
        _RainDarkening("Wet Paint Darkening", Range(0.0, 0.35)) = 0.08
        _RainCoatStrength("Wet Clear Coat", Range(0.0, 1.0)) = 0.82
        _RainDropScale("Droplet Spatial Scale (Higher = Smaller)", Range(2.0, 100.0)) = 42.0
        _RainDropDensity("Droplet Density", Range(0.0, 1.5)) = 0.62
        _RainDropSpeed("Droplet Flow Speed", Range(0.0, 4.0)) = 0.72
        _RainDropNormalStrength("Droplet Normal Strength", Range(0.0, 1.0)) = 0.24
        [HideInInspector] _RainVehicleVelocityWS("Rain Vehicle Velocity WS", Vector) = (0, 0, 0, 0)
        [HideInInspector] _RainFlowOffsetWS("Rain Flow Offset WS", Vector) = (0, 0, 0, 0)

        [Header(Lower Body And Wheel Dirt)]
        _DirtAmount("Dirt Amount", Range(0.0, 1.0)) = 0.0
        _DirtColor("Dirt Color", Color) = (0.09, 0.06, 0.035, 1)
        [NoScaleOffset] _DirtMaskMap("Dirt Placement Mask (UV0)", 2D) = "white" {}
        _DirtHeight("Lower Body Coverage", Range(0.0, 1.0)) = 0.26
        _DirtFade("Lower Body Fade", Range(0.01, 1.0)) = 0.4
        _DirtColorFade("Dirt To Body Color Fade", Range(0.0, 1.0)) = 1.0
        _DirtScale("Dirt Breakup Scale", Range(0.25, 540.0)) = 5.0
        _DirtWheelStrength("Wheel Splash Strength", Range(0.0, 3.0)) = 1.35
        _DirtCavityStrength("Cavity Dirt Strength", Range(0.0, 2.0)) = 0.45
        _DirtSmoothness("Dirt Smoothness", Range(0.0, 1.0)) = 0.2

        [Header(Scratch Damage)]
        [NoScaleOffset] _ScratchMaskMap("Authored Scratch Mask (UV0)", 2D) = "black" {}
        _ScratchMaskStrength("Authored Scratch Strength", Range(0.0, 2.0)) = 1.0
        _ScratchColor("Exposed Primer / Metal", Color) = (0.18, 0.19, 0.2, 1)
        _ScratchMetallic("Scratch Metallic", Range(0.0, 1.0)) = 0.72
        _ScratchSmoothness("Scratch Smoothness", Range(0.0, 1.0)) = 0.24
        _ScratchWidthScale("Runtime Scratch Width", Range(0.25, 4.0)) = 1.0

        // Fallback placement is used in Edit Mode. AutomotivePaintDamage replaces
        // it per renderer at runtime so dirt follows the actual body and wheels.
        [HideInInspector] _CarBoundsMin("Car Bounds Min", Vector) = (-1.05, 0.0, -2.25, 0)
        [HideInInspector] _CarBoundsMax("Car Bounds Max", Vector) = (1.05, 1.25, 2.25, 0)
        [HideInInspector] _WheelData0("Wheel 0", Vector) = (-0.85, 0.33, 1.35, 0.35)
        [HideInInspector] _WheelData1("Wheel 1", Vector) = (0.85, 0.33, 1.35, 0.35)
        [HideInInspector] _WheelData2("Wheel 2", Vector) = (-0.85, 0.33, -1.25, 0.35)
        [HideInInspector] _WheelData3("Wheel 3", Vector) = (0.85, 0.33, -1.25, 0.35)
        [HideInInspector] _CarPaintRuntimeData("Runtime Placement Data", Float) = 0.0
        [HideInInspector] _DynamicScratchCount("Runtime Scratch Count", Float) = 0.0
        [HideInInspector] _ScratchStart0("Scratch Start 0", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchStart1("Scratch Start 1", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchStart2("Scratch Start 2", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchStart3("Scratch Start 3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchStart4("Scratch Start 4", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchStart5("Scratch Start 5", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchStart6("Scratch Start 6", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchStart7("Scratch Start 7", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchEnd0("Scratch End 0", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchEnd1("Scratch End 1", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchEnd2("Scratch End 2", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchEnd3("Scratch End 3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchEnd4("Scratch End 4", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchEnd5("Scratch End 5", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchEnd6("Scratch End 6", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScratchEnd7("Scratch End 7", Vector) = (0, 0, 0, 0)

        [Toggle(_ALPHATEST_ON)] _AlphaCutoffEnable("Alpha Clipping", Float) = 0.0
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [HDR] _EmissiveColor("Emission Color", Color) = (0, 0, 0, 0)
        [Toggle(_EMISSIVE_COLOR_MAP)] _RayTracing("Use Emission Map", Float) = 0.0
        _EmissiveColorMap("Emission Map", 2D) = "white" {}
        _EmissiveExposureWeight("Emission Exposure Weight", Range(0.0, 1.0)) = 1.0

        [Header(Realtime Screen Space Reflection No Cubemap)]
        _SSRReflectionStrength("Reflection Strength", Range(0.0, 1.0)) = 1.0
        [HDR] _SSRReflectionTint("Reflection Tint", Color) = (1, 1, 1, 1)
        _SSRMaxRayDistance("Maximum Ray Distance", Range(0.25, 200.0)) = 120.0
        _SSRThickness("Relative Object Thickness", Range(0.001, 1.0)) = 0.01
        _SSRMaxIterations("Maximum Iterations", Range(8.0, 128.0)) = 32.0
        _SSRGroundMaxIterations("Downward Ray Iterations", Range(8.0, 128.0)) = 96.0
        _SSRGroundReflectionBias("Side Panel Ground Bias", Range(0.0, 1.0)) = 0.4
        _SSRMaxDepthMip("Maximum Depth Mip", Range(0.0, 6.0)) = 6.0
        _SSRSelfIntersectionBias("Self-Intersection Bias", Range(0.0, 0.01)) = 0.001
        _SSRMinSmoothness("Minimum Smoothness", Range(0.0, 1.0)) = 0.45
        _SSRSmoothnessFadeWidth("Smoothness Fade Width", Range(0.001, 0.5)) = 0.2
        _SSRMinimumReflectance("Minimum Face-On Reflectance", Range(0.0, 0.5)) = 0.2
        _SSREdgeFadeWidth("Screen Edge Fade Width", Range(0.001, 0.25)) = 0.025
        _SSRDistanceFadeStart("Distance Fade Start", Range(0.0, 0.99)) = 0.85
        [Toggle] _SSRReflectSky("Use Current-Frame Visible Sky", Float) = 1.0
        _SSRScreenFillStrength("Visible Scene Fill On Miss", Range(0.0, 1.0)) = 0.65
        _SSRScreenFillCompression("Scene Fill Projection Compression", Range(0.05, 4.0)) = 0.75
        _SSRScreenFillLocality("Scene Fill Surface Locality", Range(0.0, 1.0)) = 0.2
        [Toggle] _SSRUseGeometryNormal("Ignore UV Normal Map For Reflections", Float) = 1.0

        // Fixed opaque-looking transparent state. The shader must stay in the regular
        // transparent queue so the current color pyramid is available for sampling.
        [HideInInspector] _SurfaceType("__surfacetype", Float) = 1.0
        [HideInInspector] _BlendMode("__blendmode", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _DstBlend2("__dst2", Float) = 0.0
        [HideInInspector] _AlphaSrcBlend("__alphaSrc", Float) = 1.0
        [HideInInspector] _AlphaDstBlend("__alphaDst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _CullMode("__cullmode", Float) = 2.0
        [HideInInspector] _CullModeForward("__cullmodeForward", Float) = 2.0
        [HideInInspector] _ZTestDepthEqualForOpaque("_ZTestDepthEqualForOpaque", Float) = 4.0
        [HideInInspector] _ZTestGBuffer("_ZTestGBuffer", Float) = 4.0
        [HideInInspector] _ZTestTransparent("_ZTestTransparent", Float) = 4.0
        [HideInInspector] _ZClip("_ZClip", Float) = 1.0
        [HideInInspector] _DoubleSidedConstants("_DoubleSidedConstants", Vector) = (1, 1, -1, 0)
        [HideInInspector] _EnableBlendModePreserveSpecularLighting("_EnableBlendModePreserveSpecularLighting", Float) = 1.0

        [HideInInspector] _StencilRef("_StencilRef", Int) = 0
        [HideInInspector] _StencilWriteMask("_StencilWriteMask", Int) = 3
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

    HLSLINCLUDE

    #define SUPPORT_GLOBAL_MIP_BIAS
    #define HAVE_VERTEX_MODIFICATION
    #define SUPPORT_BLENDMODE_PRESERVE_SPECULAR_LIGHTING
    #define PREFER_HALF 0

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
    // Append the shader-specific controls to HDRP Lit's UnityPerMaterial buffer.
    // This keeps the material SRP-Batcher compatible without overloading unrelated
    // Lit properties (important when an existing HDRP Lit material changes shader).
    #undef CBUFFER_END
    #define CBUFFER_END \
        float4 _SSRReflectionTint; \
        float _SSRReflectionStrength; \
        float _SSRMaxRayDistance; \
        float _SSRThickness; \
        float _SSRMaxIterations; \
        float _SSRGroundMaxIterations; \
        float _SSRGroundReflectionBias; \
        float _SSRMaxDepthMip; \
        float _SSRSelfIntersectionBias; \
        float _SSRMinSmoothness; \
        float _SSRSmoothnessFadeWidth; \
        float _SSRMinimumReflectance; \
        float _SSREdgeFadeWidth; \
        float _SSRDistanceFadeStart; \
        float _SSRReflectSky; \
        float _SSRScreenFillStrength; \
        float _SSRScreenFillCompression; \
        float _SSRScreenFillLocality; \
        float _SSRUseGeometryNormal; \
        float4 _PaintFlakeTint; \
        float4 _DirtColor; \
        float4 _ScratchColor; \
        float4 _CarBoundsMin; \
        float4 _CarBoundsMax; \
        float4 _WheelData0; \
        float4 _WheelData1; \
        float4 _WheelData2; \
        float4 _WheelData3; \
        float4x4 _ObjectToCar; \
        float4 _ScratchStart0; \
        float4 _ScratchStart1; \
        float4 _ScratchStart2; \
        float4 _ScratchStart3; \
        float4 _ScratchStart4; \
        float4 _ScratchStart5; \
        float4 _ScratchStart6; \
        float4 _ScratchStart7; \
        float4 _ScratchEnd0; \
        float4 _ScratchEnd1; \
        float4 _ScratchEnd2; \
        float4 _ScratchEnd3; \
        float4 _ScratchEnd4; \
        float4 _ScratchEnd5; \
        float4 _ScratchEnd6; \
        float4 _ScratchEnd7; \
        float _CarBodyEffectsEnable; \
        float _PaintColorDepth; \
        float _PaintFlakeStrength; \
        float _PaintFlakeScale; \
        float _RainIntensity; \
        float _RainWetness; \
        float _RainWetSmoothness; \
        float _RainDarkening; \
        float _RainCoatStrength; \
        float _RainDropScale; \
        float _RainDropDensity; \
        float _RainDropSpeed; \
        float _RainDropNormalStrength; \
        float4 _RainVehicleVelocityWS; \
        float4 _RainFlowOffsetWS; \
        float _DirtAmount; \
        float _DirtHeight; \
        float _DirtFade; \
        float _DirtColorFade; \
        float _DirtScale; \
        float _DirtWheelStrength; \
        float _DirtCavityStrength; \
        float _DirtSmoothness; \
        float _ScratchMaskStrength; \
        float _ScratchMetallic; \
        float _ScratchSmoothness; \
        float _ScratchWidthScale; \
        float _CarPaintRuntimeData; \
        float _DynamicScratchCount; \
        float _BakedAOStrength; \
        float _BakedNormalStrength; \
    };
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitProperties.hlsl"
    #undef CBUFFER_END
    #define CBUFFER_END };

    TEXTURE2D(_BakedAOMap);
    SAMPLER(sampler_BakedAOMap);
    TEXTURE2D(_BakedNormalMap);
    SAMPLER(sampler_BakedNormalMap);
    TEXTURE2D(_DirtMaskMap);
    SAMPLER(sampler_DirtMaskMap);
    TEXTURE2D(_ScratchMaskMap);
    SAMPLER(sampler_ScratchMaskMap);

    StructuredBuffer<int2> _DepthPyramidMipLevelOffsets;

    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "HDLitShader"
            "Queue" = "Transparent"
        }

        // These small local versions of HDRP Lit's utility passes intentionally use
        // the same extended UnityPerMaterial buffer as Forward. UsePass would import
        // Lit's shorter buffer and disable the SRP Batcher for the whole SubShader.
        Pass
        {
            Name "ScenePickingPass"
            Tags { "LightMode" = "Picking" }
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local _ALPHATEST_ON

            #define _NORMALMAP_TANGENT_SPACE
            #define SHADERPASS SHADERPASS_DEPTH_ONLY
            #define SCENEPICKINGPASS
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/PickingSpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/ShaderPass/LitDepthPass.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma editor_sync_compilation
            ENDHLSL
        }

        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode" = "SceneSelectionPass" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local _ALPHATEST_ON

            #define _NORMALMAP_TANGENT_SPACE
            #define SHADERPASS SHADERPASS_DEPTH_ONLY
            #define SCENESELECTIONPASS
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/PickingSpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/ShaderPass/LitDepthPass.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma editor_sync_compilation
            ENDHLSL
        }

        Pass
        {
            Name "META"
            Tags { "LightMode" = "META" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature EDITOR_VISUALIZATION
            #pragma shader_feature_local_fragment _MASKMAP
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSIVE_COLOR_MAP
            #pragma shader_feature_local _ALPHATEST_ON

            #define _NORMALMAP_TANGENT_SPACE
            #define SHADERPASS SHADERPASS_LIGHT_TRANSPORT
            #define SCENEPICKINGPASS
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/PickingSpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/ShaderPass/LitSharePass.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassLightTransport.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull [_CullMode]
            ZClip [_ZClip]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local _ALPHATEST_ON

            #define _NORMALMAP_TANGENT_SPACE
            #define SHADERPASS SHADERPASS_SHADOWS
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/ShaderPass/LitDepthPass.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "Forward" }

            Stencil
            {
                WriteMask [_StencilWriteMask]
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            // Opaque composition, but deliberately scheduled as transparent so the
            // color pyramid can be read without a render-target feedback hazard.
            Blend One Zero
            ZTest [_ZTestTransparent]
            ZWrite On
            Cull [_CullModeForward]

            HLSLPROGRAM

            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
            #pragma multi_compile_fragment SCREEN_SPACE_SHADOWS_OFF SCREEN_SPACE_SHADOWS_ON
            #pragma multi_compile_fragment DECALS_OFF DECALS_3RT DECALS_4RT
            #pragma multi_compile_fragment _ DECAL_SURFACE_GRADIENT

            #pragma multi_compile_fragment PUNCTUAL_SHADOW_LOW PUNCTUAL_SHADOW_MEDIUM PUNCTUAL_SHADOW_HIGH
            #pragma multi_compile_fragment DIRECTIONAL_SHADOW_LOW DIRECTIONAL_SHADOW_MEDIUM DIRECTIONAL_SHADOW_HIGH
            #pragma multi_compile_fragment AREA_SHADOW_MEDIUM AREA_SHADOW_HIGH
            #pragma multi_compile_fragment USE_FPTL_LIGHTLIST USE_CLUSTERED_LIGHTLIST

            #pragma shader_feature_local_fragment _MASKMAP
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSIVE_COLOR_MAP
            #pragma shader_feature_local _ALPHATEST_ON
            // Force transparent shader logic independently of the material's old
            // _SurfaceType value. Render scheduling still comes from the Queue tag.
            #define _SURFACE_TYPE_TRANSPARENT
            #define _ENABLE_FOG_ON_TRANSPARENT
            #define _NORMALMAP_TANGENT_SPACE
            #define SHADERPASS SHADERPASS_FORWARD

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl"

            #ifdef DEBUG_DISPLAY
                #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl"
            #endif

            #define HAS_LIGHTLOOP
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoopDef.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl"

            static const float kRayTraceEpsilon = 0.000244140625; // 2^-12
            static const float kSkyDepthEpsilon = 0.000000238418579; // 2^-22
            // Set by component-free runtime state. RainManager is optional; Scene and
            // material previews still avoid an unavailable current-frame pyramid.
            float _UnderRainRuntimeActive;

            float CarPaintHash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float CarPaintValueNoise(float3 position)
            {
                float3 cell = floor(position);
                float3 blend = frac(position);
                blend = blend * blend * (3.0 - 2.0 * blend);

                float n000 = CarPaintHash31(cell + float3(0.0, 0.0, 0.0));
                float n100 = CarPaintHash31(cell + float3(1.0, 0.0, 0.0));
                float n010 = CarPaintHash31(cell + float3(0.0, 1.0, 0.0));
                float n110 = CarPaintHash31(cell + float3(1.0, 1.0, 0.0));
                float n001 = CarPaintHash31(cell + float3(0.0, 0.0, 1.0));
                float n101 = CarPaintHash31(cell + float3(1.0, 0.0, 1.0));
                float n011 = CarPaintHash31(cell + float3(0.0, 1.0, 1.0));
                float n111 = CarPaintHash31(cell + float3(1.0, 1.0, 1.0));

                float n00 = lerp(n000, n100, blend.x);
                float n10 = lerp(n010, n110, blend.x);
                float n01 = lerp(n001, n101, blend.x);
                float n11 = lerp(n011, n111, blend.x);
                return lerp(
                    lerp(n00, n10, blend.y),
                    lerp(n01, n11, blend.y),
                    blend.z);
            }

            float CarPaintFbm(float3 position)
            {
                float noise = CarPaintValueNoise(position) * 0.5714;
                noise += CarPaintValueNoise(position * 2.03 + 13.17) * 0.2857;
                noise += CarPaintValueNoise(position * 4.11 + 37.91) * 0.1429;
                return saturate(noise);
            }

            float CarPaintWheelDirt(float3 carPosition, float4 wheelData)
            {
                float radius = max(wheelData.w, 0.0001);
                if (wheelData.w <= 0.0)
                    return 0.0;

                float archDistance = length(
                    (carPosition.yz - wheelData.yz) / radius);
                float archMask = 1.0 - smoothstep(0.65, 2.25, archDistance);
                float sideDistance = abs(carPosition.x - wheelData.x);
                float sideMask = 1.0 - smoothstep(
                    radius * 0.3,
                    radius * 1.6,
                    sideDistance);
                return saturate(archMask * sideMask);
            }

            float CarPaintScratchCapsule(
                float3 carPosition,
                float4 startData,
                float4 endData,
                float seed)
            {
                if (startData.w <= 0.0 || endData.w <= 0.0)
                    return 0.0;

                float3 segment = endData.xyz - startData.xyz;
                float segmentLengthSquared = max(dot(segment, segment), 1e-8);
                float along = saturate(
                    dot(carPosition - startData.xyz, segment)
                    / segmentLengthSquared);
                float distanceToScratch = length(
                    carPosition - (startData.xyz + segment * along));
                float width = max(
                    startData.w * max(_ScratchWidthScale, 0.25),
                    0.0005);
                float antialiasWidth = max(fwidth(distanceToScratch), width * 0.12);
                float scratch = 1.0 - smoothstep(
                    width - antialiasWidth,
                    width + antialiasWidth,
                    distanceToScratch);

                // Small stable width variation prevents a perfectly synthetic line
                // while keeping every stored capsule continuous.
                float breakup = lerp(
                    0.78,
                    1.0,
                    CarPaintValueNoise(float3(along * 29.0, seed, seed * 0.37)));
                return saturate(scratch * breakup * endData.w);
            }

            float CarPaintDynamicScratchMask(float3 carPosition)
            {
                int scratchCount = (int)clamp(
                    round(_DynamicScratchCount),
                    0.0,
                    8.0);
                float scratch = 0.0;

                if (scratchCount > 0)
                    scratch = max(scratch, CarPaintScratchCapsule(
                        carPosition, _ScratchStart0, _ScratchEnd0, 1.0));
                if (scratchCount > 1)
                    scratch = max(scratch, CarPaintScratchCapsule(
                        carPosition, _ScratchStart1, _ScratchEnd1, 2.0));
                if (scratchCount > 2)
                    scratch = max(scratch, CarPaintScratchCapsule(
                        carPosition, _ScratchStart2, _ScratchEnd2, 3.0));
                if (scratchCount > 3)
                    scratch = max(scratch, CarPaintScratchCapsule(
                        carPosition, _ScratchStart3, _ScratchEnd3, 4.0));
                if (scratchCount > 4)
                    scratch = max(scratch, CarPaintScratchCapsule(
                        carPosition, _ScratchStart4, _ScratchEnd4, 5.0));
                if (scratchCount > 5)
                    scratch = max(scratch, CarPaintScratchCapsule(
                        carPosition, _ScratchStart5, _ScratchEnd5, 6.0));
                if (scratchCount > 6)
                    scratch = max(scratch, CarPaintScratchCapsule(
                        carPosition, _ScratchStart6, _ScratchEnd6, 7.0));
                if (scratchCount > 7)
                    scratch = max(scratch, CarPaintScratchCapsule(
                        carPosition, _ScratchStart7, _ScratchEnd7, 8.0));

                return saturate(scratch);
            }

            #include "VehicleRainDroplets.hlsl"

            void ApplyAutomotiveBodySurface(
                FragInputs input,
                PositionInputs posInput,
                float3 viewWS,
                float bakedAO,
                inout SurfaceData surfaceData)
            {
                float coatMask = 0.0;
                if (_CoatMask > 0.0001)
                {
                    coatMask = saturate(_CoatMask) * SAMPLE_TEXTURE2D(
                        _CoatMaskMap,
                        sampler_CoatMaskMap,
                        input.texCoord0.xy).r;
                }

                if (_CarBodyEffectsEnable >= 0.5)
                {
                    float3 objectPosition = TransformWorldToObject(posInput.positionWS);
                    float3 carPosition = objectPosition;
                    if (_CarPaintRuntimeData >= 0.5)
                    {
                        carPosition = mul(
                            _ObjectToCar,
                            float4(objectPosition, 1.0)).xyz;
                    }

                    float3 objectNormal = TransformWorldToObjectNormal(
                        surfaceData.geomNormalWS);
                    float3 carNormal = objectNormal;
                    if (_CarPaintRuntimeData >= 0.5)
                    {
                        carNormal = mul(
                            (float3x3)_ObjectToCar,
                            objectNormal);
                    }
                    carNormal = normalize(carNormal);

                    float grazing = 1.0 - saturate(dot(
                        normalize(surfaceData.normalWS),
                        normalize(viewWS)));
                    float colorDepth = saturate(_PaintColorDepth)
                                     * grazing * grazing;
                    surfaceData.baseColor *= lerp(1.0, 0.72, colorDepth);

                    float flakeNoise = CarPaintValueNoise(
                        carPosition * max(_PaintFlakeScale, 4.0) + 19.37);
                    float flakeAntialias = max(fwidth(flakeNoise), 0.015);
                    float flakeMask = smoothstep(
                        0.76 - flakeAntialias,
                        0.91 + flakeAntialias,
                        flakeNoise);
                    flakeMask *= saturate(_PaintFlakeStrength);
                    float3 flakeColor = max(
                        surfaceData.baseColor,
                        _PaintFlakeTint.rgb);
                    surfaceData.baseColor = lerp(
                        surfaceData.baseColor,
                        flakeColor,
                        flakeMask);
                    surfaceData.perceptualSmoothness = saturate(
                        surfaceData.perceptualSmoothness
                        + flakeMask * 0.06);

                    float3 boundsSize = max(
                        _CarBoundsMax.xyz - _CarBoundsMin.xyz,
                        0.001);
                    float height01 = saturate(
                        (carPosition.y - _CarBoundsMin.y)
                        / boundsSize.y);
                    float lowerBody = 1.0 - smoothstep(
                        saturate(_DirtHeight),
                        saturate(_DirtHeight + max(_DirtFade, 0.01)),
                        height01);
                    float sideFacing = 1.0 - saturate(abs(carNormal.y));
                    float wheelDirt = max(
                        max(
                            CarPaintWheelDirt(carPosition, _WheelData0),
                            CarPaintWheelDirt(carPosition, _WheelData1)),
                        max(
                            CarPaintWheelDirt(carPosition, _WheelData2),
                            CarPaintWheelDirt(carPosition, _WheelData3)));
                    float cavity = 1.0 - saturate(min(
                        bakedAO,
                        surfaceData.ambientOcclusion));
                    float placement = lowerBody * lerp(0.5, 1.0, sideFacing);
                    placement += wheelDirt * max(_DirtWheelStrength, 0.0);
                    placement += cavity
                               * max(_DirtCavityStrength, 0.0)
                               * lowerBody;

                    float dirtNoise = CarPaintFbm(
                        carPosition
                        * max(_DirtScale, 0.25)
                        * float3(0.83, 1.27, 0.91));
                    float dirtSpeckle = smoothstep(
                        0.7,
                        0.92,
                        CarPaintValueNoise(
                            carPosition * max(_DirtScale, 0.25) * 3.7
                            + 51.2));
                    float dirtBreakup = saturate(
                        0.38 + dirtNoise * 0.72 + dirtSpeckle * 0.28);
                    float dirtTexture = SAMPLE_TEXTURE2D(
                        _DirtMaskMap,
                        sampler_DirtMaskMap,
                        input.texCoord0.xy).r;
                    float dirtCoverage = saturate(
                        placement
                        * dirtBreakup
                        * dirtTexture);
                    float softDirtCoverage = dirtCoverage
                                           * dirtCoverage
                                           * (3.0 - 2.0 * dirtCoverage);
                    dirtCoverage = lerp(
                        dirtCoverage,
                        softDirtCoverage,
                        saturate(_DirtColorFade));
                    float dirtMask = saturate(
                        _DirtAmount * dirtCoverage);

                    surfaceData.baseColor = lerp(
                        surfaceData.baseColor,
                        _DirtColor.rgb,
                        dirtMask);
                    surfaceData.metallic = lerp(
                        surfaceData.metallic,
                        0.0,
                        dirtMask);
                    surfaceData.perceptualSmoothness = lerp(
                        surfaceData.perceptualSmoothness,
                        saturate(_DirtSmoothness),
                        dirtMask);
                    surfaceData.ambientOcclusion *= lerp(
                        1.0,
                        0.72,
                        dirtMask);
                    surfaceData.specularOcclusion *= lerp(
                        1.0,
                        0.65,
                        dirtMask);

                    float authoredScratch = SAMPLE_TEXTURE2D(
                        _ScratchMaskMap,
                        sampler_ScratchMaskMap,
                        input.texCoord0.xy).r
                        * max(_ScratchMaskStrength, 0.0);
                    float scratchMask = saturate(
                        authoredScratch
                        + CarPaintDynamicScratchMask(carPosition));
                    float scratchCore = smoothstep(0.35, 0.92, scratchMask);
                    float3 exposedColor = lerp(
                        _ScratchColor.rgb * 0.62,
                        _ScratchColor.rgb * 1.18,
                        scratchCore);
                    surfaceData.baseColor = lerp(
                        surfaceData.baseColor,
                        exposedColor,
                        scratchMask);
                    surfaceData.metallic = lerp(
                        surfaceData.metallic,
                        saturate(_ScratchMetallic),
                        scratchMask);
                    surfaceData.perceptualSmoothness = lerp(
                        surfaceData.perceptualSmoothness,
                        saturate(_ScratchSmoothness),
                        scratchMask);
                    surfaceData.ambientOcclusion *= lerp(
                        1.0,
                        0.76,
                        scratchMask);

                    coatMask *= (1.0 - dirtMask * 0.92)
                              * (1.0 - scratchMask);

                    // Rain is the outermost paint layer: it wets dirt and scratches
                    // alike, restores a glossy water surface, and adds textureless
                    // moving beads without mutating shared car materials.
                    float activeRain = saturate(_RainIntensity);
                    float rainWetness = saturate(max(
                        _RainWetness,
                        activeRain * 0.12));
                    if (rainWetness > 0.0001)
                    {
                        float skyFacing = lerp(
                            0.55,
                            1.0,
                            saturate(surfaceData.geomNormalWS.y));
                        float wetCoverage = rainWetness * skyFacing;
                        surfaceData.baseColor *= 1.0
                                               - saturate(_RainDarkening)
                                               * wetCoverage;
                        surfaceData.perceptualSmoothness = lerp(
                            surfaceData.perceptualSmoothness,
                            max(
                                surfaceData.perceptualSmoothness,
                                saturate(_RainWetSmoothness)),
                            wetCoverage);
                        coatMask = max(
                            coatMask,
                            wetCoverage * saturate(_RainCoatStrength));
                    }

                    if (activeRain > 0.0001 &&
                        _RainDropNormalStrength > 0.0001)
                    {
                        float3 rainNormalOffsetWS;
                        float dropletMask;
                        EvaluateVehicleRainDrops(
                            objectPosition,
                            objectNormal,
                            surfaceData.normalWS,
                            activeRain,
                            rainNormalOffsetWS,
                            dropletMask);
                        surfaceData.normalWS = normalize(
                            surfaceData.normalWS
                            - rainNormalOffsetWS
                            * saturate(_RainDropNormalStrength));
                        surfaceData.perceptualSmoothness = saturate(
                            surfaceData.perceptualSmoothness
                            + dropletMask * activeRain * 0.025);
                    }
                }
                else
                {
                    // Glass and other non-paint vehicle materials use this shader too.
                    // They do not opt into dirt/scratches, but still need the same
                    // fine water layer when RainManager binds the vehicle.
                    float activeRain = saturate(_RainIntensity);
                    float rainWetness = saturate(max(
                        _RainWetness,
                        activeRain * 0.12));
                    float3 objectPosition =
                        TransformWorldToObject(posInput.positionWS);
                    float3 objectNormal = normalize(
                        TransformWorldToObjectNormal(
                            surfaceData.geomNormalWS));

                    if (rainWetness > 0.0001)
                    {
                        float skyFacing = lerp(
                            0.62,
                            1.0,
                            saturate(surfaceData.geomNormalWS.y));
                        float wetCoverage = rainWetness * skyFacing;
                        surfaceData.perceptualSmoothness = lerp(
                            surfaceData.perceptualSmoothness,
                            max(
                                surfaceData.perceptualSmoothness,
                                saturate(_RainWetSmoothness)),
                            wetCoverage * 0.72);
                        coatMask = max(
                            coatMask,
                            wetCoverage
                            * saturate(_RainCoatStrength)
                            * 0.72);
                    }

                    if (activeRain > 0.0001 &&
                        _RainDropNormalStrength > 0.0001)
                    {
                        float3 rainNormalOffsetWS;
                        float dropletMask;
                        EvaluateVehicleRainDrops(
                            objectPosition,
                            objectNormal,
                            surfaceData.normalWS,
                            activeRain,
                            rainNormalOffsetWS,
                            dropletMask);
                        surfaceData.normalWS = normalize(
                            surfaceData.normalWS
                            - rainNormalOffsetWS
                            * saturate(_RainDropNormalStrength)
                            * 0.78);
                        surfaceData.perceptualSmoothness = saturate(
                            surfaceData.perceptualSmoothness
                            + dropletMask * activeRain * 0.018);
                    }
                }

                if (coatMask > 0.0001)
                {
                    surfaceData.materialFeatures =
                        surfaceData.materialFeatures
                        | MATERIALFEATUREFLAGS_LIT_CLEAR_COAT;
                    surfaceData.coatMask = saturate(coatMask);
                }
                else
                {
                    surfaceData.coatMask = 0.0;
                }
            }

            bool TraceHierarchicalReflection(
                float3 positionWS,
                float3 rayDirectionWS,
                float2 positionSS,
                float deviceDepth,
                out float2 hitUV,
                out float hitDeviceDepth,
                out bool cleanMiss)
            {
                hitUV = 0.0;
                hitDeviceDepth = UNITY_RAW_FAR_CLIP_VALUE;
                cleanMiss = false;

                float2 pixelCenter = floor(positionSS) + 0.5;
                float3 rayOrigin = float3(pixelCenter, deviceDepth);

                float maxDistance = max(_SSRMaxRayDistance, 0.25);

                // A long user ray may cross the camera plane when it points toward
                // the eye. Clip that endpoint to the near plane before projecting it;
                // rays pointing away are clipped against the far plane below.
                float3 positionVS = TransformWorldToView(positionWS);
                float3 rayDirectionVS = TransformWorldToViewDir(rayDirectionWS);
                if (rayDirectionVS.z > 0.000001)
                {
                    float distanceToNear = (-_ProjectionParams.y - positionVS.z)
                                         / rayDirectionVS.z;
                    maxDistance = min(maxDistance, max(distanceToNear * 0.999, 0.0));
                }
                if (maxDistance <= 0.0001)
                    return false;

                float3 rayEndWS = positionWS + rayDirectionWS * maxDistance;
                float3 rayEndNDC = ComputeNormalizedDeviceCoordinatesWithZ(rayEndWS, UNITY_MATRIX_VP);
                float3 rayEndSS = float3(rayEndNDC.xy * _ScreenSize.xy, rayEndNDC.z);
                float3 rayDir = rayEndSS - rayOrigin;

                if (max(abs(rayDir.x), abs(rayDir.y)) < 0.001)
                {
                    cleanMiss = true;
                    return false;
                }

                float3 rcpRayDir = rcp(rayDir);
                int2 rayStep = int2(rcpRayDir.x >= 0.0 ? 1 : 0, rcpRayDir.y >= 0.0 ? 1 : 0);
                float3 raySign = float3(
                    rcpRayDir.x >= 0.0 ? 1.0 : -1.0,
                    rcpRayDir.y >= 0.0 ? 1.0 : -1.0,
                    rcpRayDir.z >= 0.0 ? 1.0 : -1.0);

                // Rays toward the eye cannot use coarse maximum-depth cells safely;
                // keep those rays at mip zero, matching HDRP's native traversal.
                bool rayTowardsEye = rcpRayDir.z >= 0.0;

                const float halfTexel = 0.5;

                float3 frustumBounds;
                frustumBounds.x = rcpRayDir.x >= 0.0 ? _ScreenSize.x - halfTexel : halfTexel;
                frustumBounds.y = rcpRayDir.y >= 0.0 ? _ScreenSize.y - halfTexel : halfTexel;
                // Geometry tracing stops just before reverse-Z far depth. Sky is
                // resolved in O(1) after a miss instead of marching to the far plane.
                frustumBounds.z = rcpRayDir.z >= 0.0 ? 1.0 : kSkyDepthEpsilon;

                float3 frustumDistance = frustumBounds * rcpRayDir - rayOrigin * rcpRayDir;
                float tMax = min(1.0, Min3(frustumDistance.x, frustumDistance.y, frustumDistance.z));
                if (tMax <= 0.0)
                {
                    cleanMiss = true;
                    return false;
                }

                // Begin at the next pixel boundary to avoid immediately hitting self.
                float2 firstBoundary = abs(halfTexel * rcpRayDir.xy);
                float t = min(firstBoundary.x, firstBoundary.y);

                float thickness = max(_SSRThickness, 0.0001);
                float thicknessScale = rcp(1.0 + thickness);
                float nearPlane = _ProjectionParams.y;
                float farPlane = _ProjectionParams.z;
                float thicknessBias = -nearPlane / max(farPlane - nearPlane, 0.0001)
                                    * (thickness * thicknessScale);

                int availableMipLevel = (int)floor(log2(max(max(_ScreenSize.x, _ScreenSize.y), 1.0)));
                int maxMipLevel = min(
                    (int)clamp(round(_SSRMaxDepthMip), 0.0, 6.0),
                    availableMipLevel);
                int maxIterations = (int)clamp(round(_SSRMaxIterations), 8.0, 128.0);
                // Only rays heading down toward the camera need the longer linear
                // traversal. Away-facing rays keep the cheaper logarithmic Hi-Z path.
                if (rayTowardsEye && rayDirectionWS.y < -0.05)
                {
                    int groundMaxIterations = (int)clamp(
                        round(_SSRGroundMaxIterations),
                        8.0,
                        128.0);
                    maxIterations = max(maxIterations, groundMaxIterations);
                }
                int mipLevel = 0;
                int iteration = 0;
                bool hit = false;
                bool miss = false;
                bool belowMipZero = false;
                float3 rayPos = rayOrigin;

                [loop]
                while (!hit && !miss && t <= tMax && iteration < maxIterations)
                {
                    rayPos = rayOrigin + t * rayDir;

                    // Pick the cell on the forward side when the ray lies on an edge.
                    float2 signedEdgeDistance = round(rayPos.xy) - rayPos.xy;
                    float2 saturatedEdgeDistance = clamp(
                        raySign.xy * signedEdgeDistance + kRayTraceEpsilon,
                        0.0,
                        kRayTraceEpsilon);
                    rayPos.xy += raySign.xy * saturatedEdgeDistance;

                    int2 mipCoord = (int2)rayPos.xy >> mipLevel;
                    int2 mipOffset = _DepthPyramidMipLevelOffsets[mipLevel];

                    float4 bounds;
                    bounds.xy = (mipCoord + rayStep) << mipLevel;
                    bounds.z = LOAD_TEXTURE2D_X(_CameraDepthTexture, mipOffset + mipCoord).r;
                    bounds.w = bounds.z * thicknessScale + thicknessBias;

                    float4 distanceToBounds = bounds * rcpRayDir.xyzz
                                            - rayOrigin.xyzz * rcpRayDir.xyzz;
                    float distanceToWall = min(distanceToBounds.x, distanceToBounds.y);
                    float distanceToFloor = distanceToBounds.z;

                    bool belowFloor = rayPos.z < bounds.z;
                    bool aboveBase = rayPos.z >= bounds.w;
                    bool insideFloor = belowFloor && aboveBase;
                    bool hitFloor = t <= distanceToFloor && distanceToFloor <= distanceToWall;

                    // A ray is a miss only after it crossed completely through the
                    // previous mip-zero surface slab. Keep grazing hits valid.
                    miss = belowMipZero && insideFloor;
                    hit = mipLevel == 0 && (hitFloor || insideFloor);
                    belowMipZero = mipLevel == 0 && belowFloor;

                    t = hitFloor
                        ? distanceToFloor
                        : ((mipLevel != 0 && belowFloor) ? t : distanceToWall);
                    rayPos.z = bounds.z;

                    mipLevel += (hitFloor || belowFloor || rayTowardsEye) ? -1 : 1;
                    mipLevel = clamp(mipLevel, 0, maxMipLevel);
                    iteration++;
                }

                bool crossedSurface = miss;
                bool reachedSkyDepth = !crossedSurface && rayPos.z <= kSkyDepthEpsilon;
                bool exhaustedTraceRange = !hit && !crossedSurface && t > tMax;
                bool exhaustedIterationBudget = !hit
                                             && !crossedSurface
                                             && iteration >= maxIterations
                                             && t <= tMax;

                if (reachedSkyDepth)
                    miss = true;

                hit = hit && !miss;
                if (!hit)
                {
                    cleanMiss = !exhaustedIterationBudget
                             && !crossedSurface
                             && (reachedSkyDepth || exhaustedTraceRange);
                    return false;
                }

                hitUV = (floor(rayPos.xy) + 0.5) * _ScreenSize.zw;
                hitDeviceDepth = rayPos.z;
                return all(hitUV > 0.0) && all(hitUV < 1.0);
            }

            // Resolve a visible sky pixel with one depth lookup after a geometry miss.
            // This preserves current-frame clouds/sky without extending every Hi-Z ray
            // from the material range all the way to the camera far plane.
            bool ResolveVisibleSkyUV(
                float3 positionWS,
                float3 rayDirectionWS,
                out float2 skyUV)
            {
                skyUV = 0.0;

                float3 positionVS = TransformWorldToView(positionWS);
                float3 directionVS = TransformWorldToViewDir(rayDirectionWS);
                if (directionVS.z >= -0.000001)
                    return false;

                float distanceToFar = (-_ProjectionParams.z * 0.999 - positionVS.z)
                                    / directionVS.z;
                if (distanceToFar <= 0.0)
                    return false;

                float3 farPositionWS = positionWS + rayDirectionWS * distanceToFar;
                float3 farNDC = ComputeNormalizedDeviceCoordinatesWithZ(
                    farPositionWS,
                    UNITY_MATRIX_VP);
                skyUV = farNDC.xy;

                float2 halfTexel = 0.5 * _ScreenSize.zw;
                if (any(skyUV <= halfTexel) || any(skyUV >= 1.0 - halfTexel))
                    return false;

                int2 skyPixel = min(
                    (int2)(skyUV * _ScreenSize.xy),
                    (int2)_ScreenSize.xy - 1);
                float skyDepth = LOAD_TEXTURE2D_X(_CameraDepthTexture, skyPixel).r;
                return skyDepth <= kSkyDepthEpsilon;
            }

            float3 SampleCurrentReflectionColor(float2 hitUV, float perceptualRoughness)
            {
                uint maxColorMip = (uint)max(_ColorPyramidLodCount - 1.0, 0.0);
                float colorMip = PerceptualRoughnessToMipmapLevel(
                    perceptualRoughness,
                    maxColorMip);

                float2 samplingUV = hitUV * _RTHandleScaleHistory.xy;
                float2 pyramidTexelSpan = _ColorPyramidUvScaleAndLimitCurrentFrame.xy
                                        - _ColorPyramidUvScaleAndLimitCurrentFrame.zw;
                float2 mipMargin = pyramidTexelSpan * exp2(2.0 + ceil(abs(colorMip)));
                float2 samplingLimit = max(
                    _ColorPyramidUvScaleAndLimitCurrentFrame.xy - mipMargin,
                    0.0);
                samplingUV = clamp(samplingUV, 0.0, samplingLimit);

                float3 reflectedColor = SAMPLE_TEXTURE2D_X_LOD(
                    _ColorPyramidTexture,
                    s_trilinear_clamp_sampler,
                    samplingUV,
                    colorMip).rgb;
                return max(reflectedColor * GetInverseCurrentExposureMultiplier(), 0.0);
            }

            // When a physical ray cannot be resolved from the depth buffer, fold its
            // view direction into the visible camera hemisphere and sample the same
            // live opaque scene. This is an artistic screen-space completion, not
            // hidden-geometry reconstruction. It keeps ceiling/walls toward the upper
            // body and road color toward lower/downward-facing body pixels without a
            // cubemap, probe or UV lookup.
            float2 ResolveVisibleSceneFillUV(
                float2 positionSS,
                float3 reflectedDirectionWS)
            {
                float3 directionVS = normalize(
                    TransformWorldToViewDir(reflectedDirectionWS));

                // A single camera has no rear hemisphere. Fold it forward, then
                // compress the perspective projection so useful tunnel pixels remain
                // inside the viewport instead of collapsing onto its border.
                directionVS.z = -(
                    max(abs(directionVS.z), 0.05)
                    + max(_SSRScreenFillCompression, 0.05));
                float2 directionUV = ComputeNormalizedDeviceCoordinates(
                    directionVS,
                    UNITY_MATRIX_P);

                float edgeInset = max(_SSREdgeFadeWidth, 0.001);
                float2 inset = max(_ScreenSize.zw, edgeInset.xx);
                directionUV = clamp(directionUV, inset, 1.0 - inset);

                // Mixing in the pixel behind the late-rendered car prevents a ray
                // miss from becoming a uniform edge smear. That pixel contains the
                // live tunnel/road because the car is absent from the opaque pyramid.
                float2 surfaceUV = clamp(
                    (floor(positionSS) + 0.5) * _ScreenSize.zw,
                    inset,
                    1.0 - inset);
                float2 fillUV = lerp(
                    directionUV,
                    surfaceUV,
                    saturate(_SSRScreenFillLocality));

                return clamp(fillUV, inset, 1.0 - inset);
            }

            float3 EvaluateRealtimeReflection(
                float3 positionWS,
                float2 positionSS,
                float3 normalWS,
                float3 viewWS,
                float3 reflectedDirectionWS,
                float perceptualRoughness,
                out float hierarchyWeight)
            {
                hierarchyWeight = 0.0;

                if (_SSRReflectionStrength <= 0.0001 || _ColorPyramidLodCount < 1.0)
                    return 0.0;

                float smoothness = 1.0 - perceptualRoughness;
                float smoothnessWeight = saturate(
                    (smoothness - _SSRMinSmoothness)
                    / max(_SSRSmoothnessFadeWidth, 0.001));
                smoothnessWeight = smoothnessWeight * smoothnessWeight
                                 * (3.0 - 2.0 * smoothnessWeight);
                if (smoothnessWeight <= 0.0001)
                    return 0.0;

                float NdotV = saturate(dot(normalWS, viewWS));
                if (NdotV <= 0.0001)
                    return 0.0;

                // Pull the origin toward the camera along the same view ray. Dividing
                // by NdotV keeps grazing surfaces separated without making the bias
                // grow with camera distance.
                float3 biasedPositionWS = positionWS
                                        + viewWS * (_SSRSelfIntersectionBias / max(NdotV, 0.05));
                float biasedDeviceDepth = ComputeNormalizedDeviceCoordinatesWithZ(
                    biasedPositionWS,
                    UNITY_MATRIX_VP).z;

                // Keep roof reflections physical, but give near-vertical painted
                // panels a small downward bias so lower bodywork can pick up the road
                // instead of only reflecting content hidden behind the camera.
                float sideFacing = 1.0 - smoothstep(
                    0.15,
                    0.65,
                    saturate(normalWS.y));
                float groundBias = saturate(_SSRGroundReflectionBias) * sideFacing;
                float groundTargetY = min(reflectedDirectionWS.y, -0.25);
                reflectedDirectionWS.y = lerp(
                    reflectedDirectionWS.y,
                    groundTargetY,
                    groundBias);
                reflectedDirectionWS = normalize(reflectedDirectionWS);

                float2 hitUV;
                float hitDeviceDepth;
                bool cleanGeometryMiss;
                bool geometryHit = TraceHierarchicalReflection(
                    biasedPositionWS,
                    reflectedDirectionWS,
                    positionSS,
                    biasedDeviceDepth,
                    hitUV,
                    hitDeviceDepth,
                    cleanGeometryMiss);

                bool skyHit = false;
                if (!geometryHit && cleanGeometryMiss && _SSRReflectSky >= 0.5)
                {
                    skyHit = ResolveVisibleSkyUV(
                        biasedPositionWS,
                        reflectedDirectionWS,
                        hitUV);
                }

                bool resolvedScreenHit = geometryHit || skyHit;
                float resolvedWeight = 0.0;
                float distanceWeight = 1.0;
                if (resolvedScreenHit)
                {
                    float2 edgeDistance = min(hitUV, 1.0 - hitUV);
                    float edgeWeight = saturate(min(edgeDistance.x, edgeDistance.y)
                                      / max(_SSREdgeFadeWidth, 0.001));
                    edgeWeight = edgeWeight * edgeWeight
                               * (3.0 - 2.0 * edgeWeight);

                    if (geometryHit)
                    {
                        // Reconstruct the hit in world space. The traversal parameter
                        // is projective, not a linear fraction of the ray in metres.
                        float3 hitPositionWS = ComputeWorldSpacePosition(
                            hitUV,
                            hitDeviceDepth,
                            UNITY_MATRIX_I_VP);
                        float hitDistance01 = saturate(
                            distance(hitPositionWS, biasedPositionWS)
                            / max(_SSRMaxRayDistance, 0.25));
                        float distanceFadeStart = min(
                            _SSRDistanceFadeStart,
                            0.99);
                        distanceWeight = 1.0 - smoothstep(
                            distanceFadeStart,
                            1.0,
                            hitDistance01);
                    }

                    resolvedWeight = edgeWeight * distanceWeight;
                }

                float baseReflectionWeight = smoothnessWeight
                                           * saturate(_SSRReflectionStrength);
                float hitWeight = baseReflectionWeight * resolvedWeight;
                float fillWeight = baseReflectionWeight
                                 * (1.0 - resolvedWeight)
                                 * saturate(_SSRScreenFillStrength);

                float3 reflectedRadiance = 0.0;
                if (hitWeight > 0.0001)
                {
                    reflectedRadiance += SampleCurrentReflectionColor(
                        hitUV,
                        perceptualRoughness) * hitWeight;
                }

                if (fillWeight > 0.0001)
                {
                    float2 fillUV = ResolveVisibleSceneFillUV(
                        positionSS,
                        reflectedDirectionWS);
                    reflectedRadiance += SampleCurrentReflectionColor(
                        fillUV,
                        perceptualRoughness) * fillWeight;
                }

                hierarchyWeight = saturate(hitWeight + fillWeight);
                return reflectedRadiance * _SSRReflectionTint.rgb;
            }

            // Inject only the live camera reflection and fully consume HDRP's
            // reflection hierarchy. A weight of one deliberately prevents reflection
            // probes and the six-face sky texture from being sampled afterward.
            IndirectLighting EvaluateCustomScreenSpaceReflection(
                PositionInputs posInput,
                inout PreLightData preLightData,
                BSDFData bsdfData,
                inout float reflectionHierarchyWeight)
            {
                IndirectLighting lighting;
                ZERO_INITIALIZE(IndirectLighting, lighting);

                // In Edit/Scene view the HDRP color pyramid can be allocated but
                // contain no rendered color. Let HDRP continue to probes/sky instead
                // of consuming the hierarchy with a black custom reflection.
                if (_UnderRainRuntimeActive < 0.5 ||
                    _ColorPyramidLodCount < 1.0)
                {
                    // This shader is metallic and rendered in the transparent
                    // queue, so an empty SSR callback can resolve as black in the
                    // Scene view. Supply a restrained material-derived ambient
                    // specular term and consume this stage without sampling screen
                    // textures that do not exist outside Play Mode.
                    float safeSpecular = max(
                        Luminance(preLightData.specularFGD),
                        0.04);
                    lighting.specularReflected =
                        float3(
                            safeSpecular,
                            safeSpecular,
                            safeSpecular) * 0.12;
                    reflectionHierarchyWeight = 1.0;
                    return lighting;
                }

                // Use the geometry normal by default so broken UVs/tangents cannot
                // distort reflection placement. Direct Lit shading remains free to
                // use the normal map when the material enables it. When geometry
                // normals are selected, evaluate only the procedural water normal
                // here so rain never reintroduces a problematic base normal map.
                float useGeometryNormal = step(0.5, _SSRUseGeometryNormal);
                float3 reflectionNormalWS = normalize(lerp(
                    bsdfData.normalWS,
                    bsdfData.geomNormalWS,
                    useGeometryNormal));
                float activeRain = saturate(_RainIntensity);
                if (useGeometryNormal > 0.5 &&
                    activeRain > 0.0001 &&
                    _RainDropNormalStrength > 0.0001)
                {
                    float3 rainNormalOffsetWS;
                    float unusedDropletMask;
                    EvaluateVehicleRainDrops(
                        TransformWorldToObject(posInput.positionWS),
                        normalize(TransformWorldToObjectNormal(
                            bsdfData.geomNormalWS)),
                        bsdfData.geomNormalWS,
                        activeRain,
                        rainNormalOffsetWS,
                        unusedDropletMask);
                    reflectionNormalWS = normalize(
                        reflectionNormalWS
                        - rainNormalOffsetWS
                        * saturate(_RainDropNormalStrength));
                }
                float3 viewWS = GetWorldSpaceNormalizeViewDir(posInput.positionWS);
                float3 reflectedDirectionWS = reflect(
                    -viewWS,
                    reflectionNormalWS);
                bool hasClearCoat =
                    HasFlag(
                        bsdfData.materialFeatures,
                        MATERIALFEATUREFLAGS_LIT_CLEAR_COAT)
                    && bsdfData.coatMask > 0.0001;
                float reflectionRoughness = preLightData.iblPerceptualRoughness;
                if (hasClearCoat)
                {
                    reflectionRoughness = lerp(
                        reflectionRoughness,
                        CLEAR_COAT_PERCEPTUAL_ROUGHNESS,
                        saturate(bsdfData.coatMask));
                }

                float screenReflectionWeight;
                float3 reflectedRadiance = EvaluateRealtimeReflection(
                    posInput.positionWS,
                    posInput.positionSS,
                    reflectionNormalWS,
                    viewWS,
                    reflectedDirectionWS,
                    reflectionRoughness,
                    screenReflectionWeight);

                // Use HDRP's preintegrated specular response so roughness, metallic
                // Fresnel and specular occlusion remain consistent with Lit shading.
                // A coated pixel uses HDRP's dedicated top-lobe response; otherwise
                // the clear coat would appear in direct lights but disappear from
                // this shader's custom current-frame reflection.
                float3 specularResponse;
                if (hasClearCoat)
                {
                    float coatLightOnBase = 1.0 - saturate(
                        (bsdfData.perceptualRoughness
                         - CLEAR_COAT_PERCEPTUAL_ROUGHNESS)
                        / 0.2);
                    specularResponse =
                        coatLightOnBase
                        * preLightData.specularFGD
                        * (1.0 - preLightData.coatIblF)
                        + preLightData.coatIblF;
                    preLightData.coatReflectionWeight = 1.0;
                }
                else
                {
                    specularResponse = preLightData.specularFGD;
                }
                specularResponse = max(
                    specularResponse,
                    saturate(_SSRMinimumReflectance).xxx);
                lighting.specularReflected = reflectedRadiance * specularResponse;

                // Suppress the native environment path even on a trace miss. The
                // screen fill above is the only fallback and uses no six-face data.
                reflectionHierarchyWeight = 1.0;

                return lighting;
            }

            #define EvaluateBSDF_ScreenSpaceReflection EvaluateCustomScreenSpaceReflection
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoop.hlsl"
            #undef EvaluateBSDF_ScreenSpaceReflection
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/ShaderPass/LitSharePass.hlsl"

            // Reuse HDRP's complete Lit surface evaluation. The reflection callback
            // above contributes scene-linear indirect specular during the light loop.
            #define GetSurfaceAndBuiltinData GetHDRPLitSurfaceAndBuiltinData
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl"
            #undef GetSurfaceAndBuiltinData

            void GetSurfaceAndBuiltinData(
                FragInputs input,
                float3 V,
                inout PositionInputs posInput,
                out SurfaceData surfaceData,
                out BuiltinData builtinData RAY_TRACING_OPTIONAL_PARAMETERS)
            {
                GetHDRPLitSurfaceAndBuiltinData(
                    input,
                    V,
                    posInput,
                    surfaceData,
                    builtinData RAY_TRACING_OPTIONAL_PARAMETERS);

                float bakedAO = SAMPLE_TEXTURE2D(
                    _BakedAOMap,
                    sampler_BakedAOMap,
                    input.texCoord1.xy).r;
                bakedAO = lerp(1.0, bakedAO, saturate(_BakedAOStrength));

                float3 bakedNormalTS = UnpackNormal(SAMPLE_TEXTURE2D(
                    _BakedNormalMap,
                    sampler_BakedNormalMap,
                    input.texCoord1.xy));
                bakedNormalTS = normalize(float3(
                    bakedNormalTS.xy,
                    max(1e-4, bakedNormalTS.z)));
                float3 bakedNormalWS = normalize(
                    TransformTangentToWorld(bakedNormalTS, input.tangentToWorld));
                surfaceData.normalWS = normalize(lerp(
                    surfaceData.normalWS,
                    bakedNormalWS,
                    saturate(_BakedNormalStrength)));

                ApplyAutomotiveBodySurface(
                    input,
                    posInput,
                    V,
                    bakedAO,
                    surfaceData);

                // Keep cavity shading visible without letting a fully occluded texel
                // turn the directly lit paint completely black. Indirect diffuse and
                // specular occlusion below still receive the full baked value.
                surfaceData.baseColor *= lerp(1.0, bakedAO, 0.75);
                surfaceData.ambientOcclusion = saturate(
                    surfaceData.ambientOcclusion * bakedAO);
                surfaceData.specularOcclusion = saturate(
                    surfaceData.specularOcclusion * bakedAO);

                builtinData.opacity = 1.0;
            }

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForward.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }
    }

    FallBack "Hidden/HDRP/FallbackError"
}
