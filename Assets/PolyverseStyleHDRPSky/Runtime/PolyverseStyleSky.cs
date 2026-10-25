using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Liviu.Environment.HDRP
{
    public enum PolyverseBackgroundMode
    {
        Colors = 0,
        Cubemap = 1,
        Combined = 2
    }

    [Serializable]
    public sealed class PolyverseBackgroundModeParameter : VolumeParameter<PolyverseBackgroundMode>
    {
        public PolyverseBackgroundModeParameter(
            PolyverseBackgroundMode value,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenu("Sky/Polyverse Style Sky")]
    [SupportedOnRenderPipeline(typeof(HDRenderPipelineAsset))]
    [SkyUniqueID(26072026)]
    public sealed class PolyverseStyleSky : SkySettings
    {
        private const bool DefaultOverrideState = true;

        [Tooltip("Chooses whether the background uses gradient colours, a cubemap, or both multiplied together.")]
        public PolyverseBackgroundModeParameter backgroundMode =
            new(PolyverseBackgroundMode.Colors, DefaultOverrideState);

        [ColorUsage(false, true)]
        public ColorParameter skyColor = new(new Color(0.498f, 0.745f, 1f), DefaultOverrideState);

        [ColorUsage(false, true)]
        public ColorParameter equatorColor = new(new Color(1f, 0.747f, 0f), DefaultOverrideState);

        [ColorUsage(false, true)]
        public ColorParameter groundColor = new(new Color(0.498f, 0.497f, 0f), DefaultOverrideState);

        [Tooltip("Vertical size of the equator band.")]
        public ClampedFloatParameter equatorHeight = new(0.5f, 0.001f, 1f, DefaultOverrideState);

        [Tooltip("Softness of the transition from the equator to the upper and lower sky.")]
        public ClampedFloatParameter equatorSmoothness = new(0.5f, 0.01f, 1f, DefaultOverrideState);

        public CubemapParameter backgroundCubemap = new(null, DefaultOverrideState);

        [Tooltip("Additional multiplier applied only to the background cubemap.")]
        public MinFloatParameter backgroundExposure = new(1f, 0f, DefaultOverrideState);

        [Tooltip("Renders only Sky Color. Use this to verify that HDRP is drawing this sky.")]
        public BoolParameter debugSolidColor = new(false, DefaultOverrideState);

        public BoolParameter enablePatternOverlay = new(false, DefaultOverrideState);
        public CubemapParameter patternCubemap = new(null, DefaultOverrideState);
        public ClampedFloatParameter patternContrast = new(0.5f, 0f, 1f, DefaultOverrideState);

        public BoolParameter enableStars = new(false, DefaultOverrideState);
        public CubemapParameter starsCubemap = new(null, DefaultOverrideState);
        public MinFloatParameter starsIntensity = new(2f, 0f, DefaultOverrideState);
        public ClampedFloatParameter starsVisibility = new(1f, 0f, 1f, DefaultOverrideState);

        [Tooltip("Keeps stars visually stable when HDRP Exposure uses automatic adaptation.")]
        public BoolParameter starsIgnoreExposure = new(true, DefaultOverrideState);

        public ClampedIntParameter starsLayers = new(2, 1, 3, DefaultOverrideState);

        [ColorUsage(false, true)]
        public ColorParameter starsColor = new(Color.white, DefaultOverrideState);

        public ClampedFloatParameter starsSize = new(0.5f, 0f, 0.99f, DefaultOverrideState);

        [Tooltip("Removes stars around the sun direction. Zero disables the mask.")]
        public ClampedFloatParameter starsSunMask = new(0f, 0f, 1f, DefaultOverrideState);

        [Tooltip("Hides stars close to the horizon. Zero disables the mask.")]
        public ClampedFloatParameter starsHeightMask = new(0f, 0f, 1f, DefaultOverrideState);

        [Tooltip("When enabled, stars render only above the horizon.")]
        public BoolParameter starsBottomMask = new(false, DefaultOverrideState);

        public BoolParameter enableStarsTwinkling = new(false, DefaultOverrideState);
        public ClampedFloatParameter twinklingContrast = new(1f, 0f, 2f, DefaultOverrideState);
        public FloatParameter twinklingSpeed = new(0.05f, DefaultOverrideState);

        public BoolParameter enableStarsRotation = new(false, DefaultOverrideState);
        public ClampedFloatParameter starsRotation = new(0f, 0f, 360f, DefaultOverrideState);
        public Vector3Parameter starsRotationAxis = new(Vector3.up, DefaultOverrideState);
        public FloatParameter starsRotationSpeed = new(0.5f, DefaultOverrideState);

        public BoolParameter enableSun = new(false, DefaultOverrideState);
        public Texture2DParameter sunTexture = new(null, DefaultOverrideState);

        [ColorUsage(false, true)]
        public ColorParameter sunColor = new(Color.white, DefaultOverrideState);

        public ClampedFloatParameter sunSize = new(0.5f, 0.1f, 1f, DefaultOverrideState);
        public MinFloatParameter sunIntensity = new(1f, 0f, DefaultOverrideState);

        [Tooltip("World-space direction from the observer toward the sun.")]
        public Vector3Parameter sunDirection = new(Vector3.forward, DefaultOverrideState);

        public BoolParameter enableMoon = new(false, DefaultOverrideState);
        public Texture2DParameter moonTexture = new(null, DefaultOverrideState);

        [ColorUsage(false, true)]
        public ColorParameter moonColor = new(Color.white, DefaultOverrideState);

        public ClampedFloatParameter moonSize = new(0.5f, 0.1f, 1f, DefaultOverrideState);
        public MinFloatParameter moonIntensity = new(1f, 0f, DefaultOverrideState);
        public ClampedFloatParameter moonPhase = new(0f, 0f, 1f, DefaultOverrideState);

        [Tooltip("Visibility and brightness of the moon's shadowed phase side. Zero hides it completely; one makes phases invisible.")]
        public ClampedFloatParameter moonDarkSideVisibility = new(0f, 0f, 1f, DefaultOverrideState);

        [Tooltip("Controls how strongly crescent and gibbous moon phases use a clean semicircle-style terminator. Lower values are straighter; higher values are rounder and more stylized.")]
        public ClampedFloatParameter moonPhaseRoundness = new(1.6f, 0.5f, 4f, DefaultOverrideState);

        [Tooltip("Softens the transition between the moon's lit and shadowed phase sides. Zero is a sharp line; 0.5 hides the line with a practical fade; one is very broad fading.")]
        public ClampedFloatParameter moonPhaseFade = new(0.5f, 0f, 1f, DefaultOverrideState);

        [Tooltip("World-space direction from the observer toward the moon.")]
        public Vector3Parameter moonDirection = new(Vector3.right, DefaultOverrideState);

        public BoolParameter enableClouds = new(false, DefaultOverrideState);
        public CubemapParameter cloudsCubemap = new(null, DefaultOverrideState);
        public ClampedFloatParameter cloudsOpacity = new(1f, 0f, 1f, DefaultOverrideState);
        public ClampedFloatParameter cloudsFade = new(1f, 0f, 1f, DefaultOverrideState);
        public ClampedFloatParameter cloudsHeight = new(0f, -0.5f, 0.5f, DefaultOverrideState);
        public Vector2Parameter cloudsOffset = new(Vector2.zero, DefaultOverrideState);

        [Tooltip("Used when Clouds Cubemap is empty. Zero clears the sky; one fills it.")]
        public ClampedFloatParameter cloudsCoverage = new(0.45f, 0f, 1f, DefaultOverrideState);
        [Tooltip("Horizontal layer density. Lower values create fewer, larger 3D cloud formations.")]
        public ClampedFloatParameter cloudsScale = new(3f, 0.2f, 16f, DefaultOverrideState);
        public ClampedFloatParameter cloudsSoftness = new(0.08f, 0.005f, 0.3f, DefaultOverrideState);
        [Tooltip("Variation between the proportions and lobes of individual cloud silhouettes.")]
        public ClampedFloatParameter cloudsDetail = new(0.65f, 0f, 1f, DefaultOverrideState);
        [Tooltip("Fades procedural clouds out near and below the horizon.")]
        public ClampedFloatParameter cloudsHorizonFade = new(0.1f, 0.01f, 0.5f, DefaultOverrideState);

        [Tooltip("Maximum elevation above the horizon in degrees. Zero hides clouds; small values leave the overhead sky clear; 90 allows clouds overhead.")]
        public ClampedFloatParameter cloudsMaxHeight = new(90f, 0f, 90f, DefaultOverrideState);

        [Tooltip("Height of the horizontal cloud layer above the observer, in metres.")]
        public MinFloatParameter cloudsLayerAltitude = new(1000f, 100f, DefaultOverrideState);
        [Tooltip("Horizontal distance in metres where clouds start fading and become invisible.")]
        public Vector2Parameter cloudsDistanceFade = new(new Vector2(3000f, 8000f), DefaultOverrideState);
        [Tooltip("Additional size multiplier at the far fade distance, on top of perspective shrinking.")]
        public ClampedFloatParameter cloudsDistantScale = new(0.35f, 0.1f, 1f, DefaultOverrideState);
        [Tooltip("Noise erosion during birth and death. Higher values split the cloud into fragments.")]
        public ClampedFloatParameter cloudsBreakup = new(0.8f, 0f, 1f, DefaultOverrideState);

        [Tooltip("Linear X/Z velocity in cloud-layer-height units per second (0.003 is 3 m/s at 1000 m altitude). The time controller owns drift when attached.")]
        public Vector2Parameter cloudsWindVelocity = new(new Vector2(0.003f, 0.001f), DefaultOverrideState);
        [Tooltip("Rate of independent cloud lifetimes and gentle lobe deformation. Zero freezes life and shape without stopping wind.")]
        public ClampedFloatParameter cloudsEvolutionSpeed = new(0.025f, 0f, 0.5f, DefaultOverrideState);
        [Tooltip("Strength of the slowly evolving distortion of the cloud outlines.")]
        public ClampedFloatParameter cloudsDeformation = new(0.35f, 0f, 1f, DefaultOverrideState);

        [ColorUsage(false, true)]
        public ColorParameter cloudsLightColor = new(Color.white, DefaultOverrideState);

        [ColorUsage(false, true)]
        public ColorParameter cloudsShadowColor =
            new(new Color(0.498f, 0.745f, 1f), DefaultOverrideState);

        [Tooltip("Uses HDRP's current directional sun orientation and colour. When disabled or unavailable, uses Sun Direction from this sky.")]
        public BoolParameter cloudsFollowSceneSun = new(true, DefaultOverrideState);
        [Tooltip("Strength of directional illumination on procedural cloud lobes.")]
        public ClampedFloatParameter cloudsDirectLight = new(0.9f, 0f, 1f, DefaultOverrideState);
        [Tooltip("Light filling the shadowed side. Lower values give stronger blue shadows.")]
        public ClampedFloatParameter cloudsAmbientFill = new(0.18f, 0f, 1f, DefaultOverrideState);
        [Tooltip("Softness of the stylized light/shadow transition across each cloud lobe.")]
        public ClampedFloatParameter cloudsLightSoftness = new(0.45f, 0.02f, 1f, DefaultOverrideState);
        [Tooltip("Soft bright edge when looking toward the sun through a procedural cloud.")]
        public ClampedFloatParameter cloudsSilverLining = new(0.18f, 0f, 1f, DefaultOverrideState);

        [Tooltip("Enables circular motion around Clouds Rotation Axis. Linear wind can run simultaneously; set wind to zero for rotation only.")]
        public BoolParameter enableCloudsRotation = new(false, DefaultOverrideState);
        public ClampedFloatParameter cloudsRotation = new(0f, 0f, 360f, DefaultOverrideState);
        public Vector3Parameter cloudsRotationAxis = new(Vector3.up, DefaultOverrideState);
        public FloatParameter cloudsRotationSpeed = new(0.5f, DefaultOverrideState);

        [Tooltip("This is only a visual horizon overlay. Use HDRP Fog for actual atmospheric fog in the scene.")]
        public BoolParameter enableHorizonFog = new(false, DefaultOverrideState);

        [ColorUsage(false, true)]
        public ColorParameter horizonFogColor = new(new Color(0.55f, 0.62f, 0.7f), DefaultOverrideState);

        public ClampedFloatParameter fogHeight = new(0.15f, 0f, 1f, DefaultOverrideState);
        public ClampedFloatParameter fogSmoothness = new(0.5f, 0.01f, 1f, DefaultOverrideState);
        public ClampedFloatParameter fogFill = new(0f, 0f, 1f, DefaultOverrideState);

        [Tooltip("Offsets the gradient and sampled cubemaps vertically before normalisation.")]
        public ClampedFloatParameter skyboxOffset = new(0f, -1f, 1f, DefaultOverrideState);

        [Tooltip("Axis used by the inherited SkySettings Rotation property.")]
        public Vector3Parameter skyboxRotationAxis = new(Vector3.up, DefaultOverrideState);

        public override Type GetSkyRendererType() => typeof(PolyverseStyleSkyRenderer);

        protected override void OnEnable()
        {
            base.OnEnable();
            EnableCustomOverrides();
        }

        private void EnableCustomOverrides()
        {
            backgroundMode.overrideState = true;
            skyColor.overrideState = true;
            equatorColor.overrideState = true;
            groundColor.overrideState = true;
            equatorHeight.overrideState = true;
            equatorSmoothness.overrideState = true;
            backgroundCubemap.overrideState = true;
            backgroundExposure.overrideState = true;
            debugSolidColor.overrideState = true;

            enablePatternOverlay.overrideState = true;
            patternCubemap.overrideState = true;
            patternContrast.overrideState = true;

            enableStars.overrideState = true;
            starsCubemap.overrideState = true;
            starsIntensity.overrideState = true;
            starsVisibility.overrideState = true;
            starsIgnoreExposure.overrideState = true;
            starsLayers.overrideState = true;
            starsColor.overrideState = true;
            starsSize.overrideState = true;
            starsSunMask.overrideState = true;
            starsHeightMask.overrideState = true;
            starsBottomMask.overrideState = true;
            enableStarsTwinkling.overrideState = true;
            twinklingContrast.overrideState = true;
            twinklingSpeed.overrideState = true;
            enableStarsRotation.overrideState = true;
            starsRotation.overrideState = true;
            starsRotationAxis.overrideState = true;
            starsRotationSpeed.overrideState = true;

            enableSun.overrideState = true;
            sunTexture.overrideState = true;
            sunColor.overrideState = true;
            sunSize.overrideState = true;
            sunIntensity.overrideState = true;
            sunDirection.overrideState = true;

            enableMoon.overrideState = true;
            moonTexture.overrideState = true;
            moonColor.overrideState = true;
            moonSize.overrideState = true;
            moonIntensity.overrideState = true;
            moonPhase.overrideState = true;
            moonDarkSideVisibility.overrideState = true;
            moonPhaseRoundness.overrideState = true;
            moonPhaseFade.overrideState = true;
            moonDirection.overrideState = true;

            enableClouds.overrideState = true;
            cloudsCubemap.overrideState = true;
            cloudsOpacity.overrideState = true;
            cloudsFade.overrideState = true;
            cloudsHeight.overrideState = true;
            cloudsOffset.overrideState = true;
            cloudsCoverage.overrideState = true;
            cloudsScale.overrideState = true;
            cloudsSoftness.overrideState = true;
            cloudsDetail.overrideState = true;
            cloudsHorizonFade.overrideState = true;
            cloudsMaxHeight.overrideState = true;
            cloudsLayerAltitude.overrideState = true;
            cloudsDistanceFade.overrideState = true;
            cloudsDistantScale.overrideState = true;
            cloudsBreakup.overrideState = true;
            cloudsWindVelocity.overrideState = true;
            cloudsEvolutionSpeed.overrideState = true;
            cloudsDeformation.overrideState = true;
            cloudsLightColor.overrideState = true;
            cloudsShadowColor.overrideState = true;
            cloudsFollowSceneSun.overrideState = true;
            cloudsDirectLight.overrideState = true;
            cloudsAmbientFill.overrideState = true;
            cloudsLightSoftness.overrideState = true;
            cloudsSilverLining.overrideState = true;
            enableCloudsRotation.overrideState = true;
            cloudsRotation.overrideState = true;
            cloudsRotationAxis.overrideState = true;
            cloudsRotationSpeed.overrideState = true;

            enableHorizonFog.overrideState = true;
            horizonFogColor.overrideState = true;
            fogHeight.overrideState = true;
            fogSmoothness.overrideState = true;
            fogFill.overrideState = true;

            skyboxOffset.overrideState = true;
            skyboxRotationAxis.overrideState = true;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();

            unchecked
            {
                hash = hash * 23 + backgroundMode.GetHashCode();
                hash = hash * 23 + skyColor.GetHashCode();
                hash = hash * 23 + equatorColor.GetHashCode();
                hash = hash * 23 + groundColor.GetHashCode();
                hash = hash * 23 + equatorHeight.GetHashCode();
                hash = hash * 23 + equatorSmoothness.GetHashCode();
                hash = hash * 23 + backgroundCubemap.GetHashCode();
                hash = hash * 23 + backgroundExposure.GetHashCode();
                hash = hash * 23 + debugSolidColor.GetHashCode();

                hash = hash * 23 + enablePatternOverlay.GetHashCode();
                hash = hash * 23 + patternCubemap.GetHashCode();
                hash = hash * 23 + patternContrast.GetHashCode();

                hash = hash * 23 + enableStars.GetHashCode();
                hash = hash * 23 + starsCubemap.GetHashCode();
                hash = hash * 23 + starsIntensity.GetHashCode();
                hash = hash * 23 + starsVisibility.GetHashCode();
                hash = hash * 23 + starsIgnoreExposure.GetHashCode();
                hash = hash * 23 + starsLayers.GetHashCode();
                hash = hash * 23 + starsColor.GetHashCode();
                hash = hash * 23 + starsSize.GetHashCode();
                hash = hash * 23 + starsSunMask.GetHashCode();
                hash = hash * 23 + starsHeightMask.GetHashCode();
                hash = hash * 23 + starsBottomMask.GetHashCode();
                hash = hash * 23 + enableStarsTwinkling.GetHashCode();
                hash = hash * 23 + twinklingContrast.GetHashCode();
                hash = hash * 23 + twinklingSpeed.GetHashCode();
                hash = hash * 23 + enableStarsRotation.GetHashCode();
                hash = hash * 23 + starsRotation.GetHashCode();
                hash = hash * 23 + starsRotationAxis.GetHashCode();
                hash = hash * 23 + starsRotationSpeed.GetHashCode();

                hash = hash * 23 + enableSun.GetHashCode();
                hash = hash * 23 + sunTexture.GetHashCode();
                hash = hash * 23 + sunColor.GetHashCode();
                hash = hash * 23 + sunSize.GetHashCode();
                hash = hash * 23 + sunIntensity.GetHashCode();
                hash = hash * 23 + sunDirection.GetHashCode();

                hash = hash * 23 + enableMoon.GetHashCode();
                hash = hash * 23 + moonTexture.GetHashCode();
                hash = hash * 23 + moonColor.GetHashCode();
                hash = hash * 23 + moonSize.GetHashCode();
                hash = hash * 23 + moonIntensity.GetHashCode();
                hash = hash * 23 + moonPhase.GetHashCode();
                hash = hash * 23 + moonDarkSideVisibility.GetHashCode();
                hash = hash * 23 + moonPhaseRoundness.GetHashCode();
                hash = hash * 23 + moonPhaseFade.GetHashCode();
                hash = hash * 23 + moonDirection.GetHashCode();

                hash = hash * 23 + enableClouds.GetHashCode();
                hash = hash * 23 + cloudsCubemap.GetHashCode();
                hash = hash * 23 + cloudsOpacity.GetHashCode();
                hash = hash * 23 + cloudsFade.GetHashCode();
                hash = hash * 23 + cloudsHeight.GetHashCode();
                hash = hash * 23 + cloudsOffset.GetHashCode();
                hash = hash * 23 + cloudsCoverage.GetHashCode();
                hash = hash * 23 + cloudsScale.GetHashCode();
                hash = hash * 23 + cloudsSoftness.GetHashCode();
                hash = hash * 23 + cloudsDetail.GetHashCode();
                hash = hash * 23 + cloudsHorizonFade.GetHashCode();
                hash = hash * 23 + cloudsMaxHeight.GetHashCode();
                hash = hash * 23 + cloudsLayerAltitude.GetHashCode();
                hash = hash * 23 + cloudsDistanceFade.GetHashCode();
                hash = hash * 23 + cloudsDistantScale.GetHashCode();
                hash = hash * 23 + cloudsBreakup.GetHashCode();
                hash = hash * 23 + cloudsWindVelocity.GetHashCode();
                hash = hash * 23 + cloudsEvolutionSpeed.GetHashCode();
                hash = hash * 23 + cloudsDeformation.GetHashCode();
                hash = hash * 23 + cloudsLightColor.GetHashCode();
                hash = hash * 23 + cloudsShadowColor.GetHashCode();
                hash = hash * 23 + cloudsFollowSceneSun.GetHashCode();
                hash = hash * 23 + cloudsDirectLight.GetHashCode();
                hash = hash * 23 + cloudsAmbientFill.GetHashCode();
                hash = hash * 23 + cloudsLightSoftness.GetHashCode();
                hash = hash * 23 + cloudsSilverLining.GetHashCode();
                hash = hash * 23 + enableCloudsRotation.GetHashCode();
                hash = hash * 23 + cloudsRotation.GetHashCode();
                hash = hash * 23 + cloudsRotationAxis.GetHashCode();
                hash = hash * 23 + cloudsRotationSpeed.GetHashCode();

                hash = hash * 23 + enableHorizonFog.GetHashCode();
                hash = hash * 23 + horizonFogColor.GetHashCode();
                hash = hash * 23 + fogHeight.GetHashCode();
                hash = hash * 23 + fogSmoothness.GetHashCode();
                hash = hash * 23 + fogFill.GetHashCode();

                hash = hash * 23 + skyboxOffset.GetHashCode();
                hash = hash * 23 + skyboxRotationAxis.GetHashCode();
            }

            return hash;
        }
    }
}
