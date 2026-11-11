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

    HLSLINCLUDE

    #define SUPPORT_GLOBAL_MIP_BIAS
    #define HAVE_VERTEX_MODIFICATION
    #define SUPPORT_BLENDMODE_PRESERVE_SPECULAR_LIGHTING
    #define PREFER_HALF 0
    // Back-facing glass needs its geometric normal oriented toward the viewer so
    // direct lighting and the custom screen-space reflection remain valid.
    #define _DOUBLESIDED_ON

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
    // Append the shader-specific controls to HDRP Lit's UnityPerMaterial buffer.
    // This keeps the material SRP-Batcher compatible without overloading unrelated
    // Lit properties (important when an existing HDRP Lit material changes shader).
    #undef CBUFFER_END
    #define CBUFFER_END \
        float4 _SSRReflectionTint; \
        float _GlassOpacity; \
        float _RainIntensity; \
        float _RainWetness; \
        float _RainWetSmoothness; \
        float _RainDropScale; \
        float _RainDropDensity; \
        float _RainDropSpeed; \
        float _RainDropNormalStrength; \
        float4 _RainVehicleVelocityWS; \
        float4 _RainFlowOffsetWS; \
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
    };
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitProperties.hlsl"
    #undef CBUFFER_END
    #define CBUFFER_END };

    StructuredBuffer<int2> _DepthPyramidMipLevelOffsets;

    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "HDLitShader"
            "Queue" = "Transparent+1"
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
            Cull [_CullMode]

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
            Name "Forward"
            Tags { "LightMode" = "Forward" }

            Stencil
            {
                WriteMask [_StencilWriteMask]
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            // HDRP alpha mode uses premultiplied diffuse while preserve-specular
            // keeps the live tunnel reflection visible on low-opacity glass.
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest [_ZTestTransparent]
            ZWrite Off
            Cull [_CullMode]

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
            #include "VehicleRainDroplets.hlsl"

            static const float kRayTraceEpsilon = 0.000244140625; // 2^-12
            static const float kSkyDepthEpsilon = 0.000000238418579; // 2^-22
            // Set by component-free runtime state. RainManager is optional; Scene and
            // material previews still avoid an unavailable current-frame pyramid.
            float _UnderRainRuntimeActive;

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
            // hidden-geometry reconstruction. It keeps ceiling/walls toward upper
            // glass and road color toward lower/downward-facing window pixels without
            // a cubemap, probe or UV lookup.
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

                // Keep the windshield/rear glass reflection physical, but give
                // near-vertical side windows a small downward bias so their lower
                // area can pick up the road instead of only hidden rear geometry.
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

                // Do not consume HDRP's reflection hierarchy in Edit/Scene view.
                // The current-frame color pyramid may exist there as a black target.
                if (_UnderRainRuntimeActive < 0.5 ||
                    _ColorPyramidLodCount < 1.0)
                {
                    // Glass still needs a visible non-screen-space contribution in
                    // Scene view. Never return an empty hierarchy result for this
                    // opaque-composited transparent pass.
                    float safeSpecular = max(
                        Luminance(preLightData.specularFGD),
                        0.04);
                    lighting.specularReflected =
                        float3(
                            safeSpecular,
                            safeSpecular,
                            safeSpecular) * 0.08;
                    reflectionHierarchyWeight = 1.0;
                    return lighting;
                }

                // Use the geometry normal by default so broken UVs/tangents cannot
                // distort reflection placement. Direct Lit shading remains free to
                // use the normal map when the material enables it.
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
                float screenReflectionWeight;
                float3 reflectedRadiance = EvaluateRealtimeReflection(
                    posInput.positionWS,
                    posInput.positionSS,
                    reflectionNormalWS,
                    viewWS,
                    reflectedDirectionWS,
                    preLightData.iblPerceptualRoughness,
                    screenReflectionWeight);

                // Use HDRP's preintegrated specular response so roughness, metallic
                // Fresnel and specular occlusion remain consistent with Lit shading.
                float3 specularResponse = max(
                    preLightData.specularFGD,
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

                float activeRain = saturate(_RainIntensity);
                float rainWetness = saturate(max(
                    _RainWetness,
                    activeRain * 0.12));
                float dropletMask = 0.0;
                if (rainWetness > 0.0001)
                {
                    surfaceData.perceptualSmoothness = lerp(
                        surfaceData.perceptualSmoothness,
                        max(
                            surfaceData.perceptualSmoothness,
                            saturate(_RainWetSmoothness)),
                        rainWetness * 0.78);
                }

                if (activeRain > 0.0001 &&
                    _RainDropNormalStrength > 0.0001)
                {
                    float3 rainNormalOffsetWS;
                    EvaluateVehicleRainDrops(
                        TransformWorldToObject(posInput.positionWS),
                        normalize(TransformWorldToObjectNormal(
                            surfaceData.geomNormalWS)),
                        surfaceData.normalWS,
                        activeRain,
                        rainNormalOffsetWS,
                        dropletMask);
                    surfaceData.normalWS = normalize(
                        surfaceData.normalWS
                        - rainNormalOffsetWS
                        * saturate(_RainDropNormalStrength));
                }

                // Keep transmission independent of the mesh UVs and optional tint
                // texture. HDRP alpha blending reveals the opaque interior while the
                // custom specular reflection remains visible above it.
                builtinData.opacity = saturate(_GlassOpacity);
            }

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForward.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }
    }

    FallBack "Hidden/HDRP/FallbackError"
}
