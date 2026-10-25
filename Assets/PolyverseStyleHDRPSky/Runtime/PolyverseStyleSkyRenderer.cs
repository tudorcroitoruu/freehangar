using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Liviu.Environment.HDRP
{
    internal sealed class PolyverseStyleSkyRenderer : SkyRenderer
    {
        private const string ShaderName = "Hidden/HDRP/Sky/PolyverseStyleSky";

        private Material material;
        private readonly MaterialPropertyBlock properties = new();
        private int bakingPass = -1;
        private int fullscreenPass = -1;
        private bool loggedFirstRender;
        private bool loggedPassError;
        private Cubemap fallbackCubemap;

        private static readonly ProfilingSampler ProfilingSampler =
            new("Polyverse Style Sky");

        private static readonly int PixelCoordToViewDirWS =
            Shader.PropertyToID("_PixelCoordToViewDirWS");

        private static readonly int SkyIntensity = Shader.PropertyToID("_SkyIntensity");
        private static readonly int SkyTime = Shader.PropertyToID("_SkyTime");
        private static readonly int DebugSolidColor = Shader.PropertyToID("_DebugSolidColor");
        private static readonly int BackgroundMode = Shader.PropertyToID("_BackgroundMode");
        private static readonly int SkyColor = Shader.PropertyToID("_SkyColor");
        private static readonly int EquatorColor = Shader.PropertyToID("_EquatorColor");
        private static readonly int GroundColor = Shader.PropertyToID("_GroundColor");
        private static readonly int EquatorHeight = Shader.PropertyToID("_EquatorHeight");
        private static readonly int EquatorSmoothness = Shader.PropertyToID("_EquatorSmoothness");
        private static readonly int BackgroundCubemap = Shader.PropertyToID("_BackgroundCubemap");
        private static readonly int HasBackgroundCubemap = Shader.PropertyToID("_HasBackgroundCubemap");
        private static readonly int BackgroundExposure = Shader.PropertyToID("_BackgroundExposure");

        private static readonly int EnablePatternOverlay = Shader.PropertyToID("_EnablePatternOverlay");
        private static readonly int PatternCubemap = Shader.PropertyToID("_PatternCubemap");
        private static readonly int HasPatternCubemap = Shader.PropertyToID("_HasPatternCubemap");
        private static readonly int PatternContrast = Shader.PropertyToID("_PatternContrast");

        private static readonly int EnableStars = Shader.PropertyToID("_EnableStars");
        private static readonly int StarsCubemap = Shader.PropertyToID("_StarsCubemap");
        private static readonly int HasStarsCubemap = Shader.PropertyToID("_HasStarsCubemap");
        private static readonly int StarsIntensity = Shader.PropertyToID("_StarsIntensity");
        private static readonly int StarsVisibility = Shader.PropertyToID("_StarsVisibility");
        private static readonly int StarsIgnoreExposure = Shader.PropertyToID("_StarsIgnoreExposure");
        private static readonly int StarsLayers = Shader.PropertyToID("_StarsLayers");
        private static readonly int StarsColor = Shader.PropertyToID("_StarsColor");
        private static readonly int StarsSize = Shader.PropertyToID("_StarsSize");
        private static readonly int StarsSunMask = Shader.PropertyToID("_StarsSunMask");
        private static readonly int StarsHeightMask = Shader.PropertyToID("_StarsHeightMask");
        private static readonly int StarsBottomMask = Shader.PropertyToID("_StarsBottomMask");
        private static readonly int EnableStarsTwinkling = Shader.PropertyToID("_EnableStarsTwinkling");
        private static readonly int TwinklingContrast = Shader.PropertyToID("_TwinklingContrast");
        private static readonly int TwinklingSpeed = Shader.PropertyToID("_TwinklingSpeed");
        private static readonly int EnableStarsRotation = Shader.PropertyToID("_EnableStarsRotation");
        private static readonly int StarsRotation = Shader.PropertyToID("_StarsRotation");
        private static readonly int StarsRotationAxis = Shader.PropertyToID("_StarsRotationAxis");
        private static readonly int StarsRotationSpeed = Shader.PropertyToID("_StarsRotationSpeed");

        private static readonly int EnableSun = Shader.PropertyToID("_EnableSun");
        private static readonly int SunTexture = Shader.PropertyToID("_SunTexture");
        private static readonly int HasSunTexture = Shader.PropertyToID("_HasSunTexture");
        private static readonly int SunColor = Shader.PropertyToID("_SunColor");
        private static readonly int SunSize = Shader.PropertyToID("_SunSize");
        private static readonly int SunIntensity = Shader.PropertyToID("_SunIntensity");
        private static readonly int SunDirection = Shader.PropertyToID("_SunDirection");

        private static readonly int EnableMoon = Shader.PropertyToID("_EnableMoon");
        private static readonly int MoonTexture = Shader.PropertyToID("_MoonTexture");
        private static readonly int HasMoonTexture = Shader.PropertyToID("_HasMoonTexture");
        private static readonly int MoonColor = Shader.PropertyToID("_MoonColor");
        private static readonly int MoonSize = Shader.PropertyToID("_MoonSize");
        private static readonly int MoonIntensity = Shader.PropertyToID("_MoonIntensity");
        private static readonly int MoonPhase = Shader.PropertyToID("_MoonPhase");
        private static readonly int MoonDarkSideVisibility = Shader.PropertyToID("_MoonDarkSideVisibility");
        private static readonly int MoonPhaseRoundness = Shader.PropertyToID("_MoonPhaseRoundness");
        private static readonly int MoonPhaseFade = Shader.PropertyToID("_MoonPhaseFade");
        private static readonly int MoonDirection = Shader.PropertyToID("_MoonDirection");

        private static readonly int EnableClouds = Shader.PropertyToID("_EnableClouds");
        private static readonly int CloudsCubemap = Shader.PropertyToID("_CloudsCubemap");
        private static readonly int HasCloudsCubemap = Shader.PropertyToID("_HasCloudsCubemap");
        private static readonly int CloudsOpacity = Shader.PropertyToID("_CloudsOpacity");
        private static readonly int CloudsFade = Shader.PropertyToID("_CloudsFade");
        private static readonly int CloudsHeight = Shader.PropertyToID("_CloudsHeight");
        private static readonly int CloudsOffset = Shader.PropertyToID("_CloudsOffset");
        private static readonly int CloudsShape = Shader.PropertyToID("_CloudsShape");
        private static readonly int CloudsAnimation = Shader.PropertyToID("_CloudsAnimation");
        private static readonly int CloudsHorizonFade = Shader.PropertyToID("_CloudsHorizonFade");
        private static readonly int CloudsMaxHeight = Shader.PropertyToID("_CloudsMaxHeight");
        private static readonly int CloudsLayer = Shader.PropertyToID("_CloudsLayer");
        private static readonly int CloudsBreakup = Shader.PropertyToID("_CloudsBreakup");
        private static readonly int CloudsLightColor = Shader.PropertyToID("_CloudsLightColor");
        private static readonly int CloudsShadowColor = Shader.PropertyToID("_CloudsShadowColor");
        private static readonly int EnableCloudsRotation = Shader.PropertyToID("_EnableCloudsRotation");
        private static readonly int CloudsRotation = Shader.PropertyToID("_CloudsRotation");
        private static readonly int CloudsRotationAxis = Shader.PropertyToID("_CloudsRotationAxis");
        private static readonly int CloudsRotationSpeed = Shader.PropertyToID("_CloudsRotationSpeed");

        private static readonly int EnableHorizonFog = Shader.PropertyToID("_EnableHorizonFog");
        private static readonly int HorizonFogColor = Shader.PropertyToID("_HorizonFogColor");
        private static readonly int FogHeight = Shader.PropertyToID("_FogHeight");
        private static readonly int FogSmoothness = Shader.PropertyToID("_FogSmoothness");
        private static readonly int FogFill = Shader.PropertyToID("_FogFill");

        private static readonly int SkyboxOffset = Shader.PropertyToID("_SkyboxOffset");
        private static readonly int SkyboxRotation = Shader.PropertyToID("_SkyboxRotation");
        private static readonly int SkyboxRotationAxis = Shader.PropertyToID("_SkyboxRotationAxis");

        public override void Build()
        {
            Shader shader = Shader.Find(ShaderName);

            if (shader == null)
            {
                Debug.LogError(
                    $"HDRP sky shader '{ShaderName}' was not found. " +
                    "Keep PolyverseStyleSky.shader in the project and add it to Always Included Shaders if shader stripping removes it.");
                return;
            }

            material = CoreUtils.CreateEngineMaterial(shader);
            fallbackCubemap = CreateFallbackCubemap(Color.black);
            SetFallbackTextures();

            bakingPass = material.FindPass("FragBaking");
            fullscreenPass = material.FindPass("FragRender");
            loggedFirstRender = false;
            loggedPassError = false;
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(fallbackCubemap);
            material = null;
            fallbackCubemap = null;
        }

        public override void RenderSky(
            BuiltinSkyParameters builtinParams,
            bool renderForCubemap,
            bool renderSunDisk)
        {
            if (material == null)
                return;

            if (builtinParams.skySettings is not PolyverseStyleSky sky)
                return;

            int pass = renderForCubemap ? bakingPass : fullscreenPass;
            if (pass < 0)
            {
                if (!loggedPassError)
                {
                    loggedPassError = true;
                    Debug.LogError(
                        $"Polyverse Style Sky could not find shader pass {(renderForCubemap ? "FragBaking" : "FragRender")} " +
                        $"on shader '{material.shader.name}'.");
                }

                return;
            }

            float animatedTime = builtinParams.hdCamera.time;
            bool hasBackgroundCubemap = sky.backgroundCubemap.value != null;
            bool hasPatternCubemap = sky.patternCubemap.value != null;
            bool hasStarsCubemap = sky.starsCubemap.value != null;
            bool hasSunTexture = sky.sunTexture.value != null;
            bool hasMoonTexture = sky.moonTexture.value != null;
            bool hasCloudsCubemap = sky.cloudsCubemap.value != null;

            properties.Clear();
            properties.SetMatrix(PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);
            properties.SetFloat(SkyIntensity, GetSkyIntensity(sky, builtinParams.debugSettings));
            properties.SetFloat(SkyTime, animatedTime);
            properties.SetFloat(DebugSolidColor, sky.debugSolidColor.value ? 1f : 0f);

            properties.SetInt(BackgroundMode, (int)sky.backgroundMode.value);
            properties.SetColor(SkyColor, sky.skyColor.value);
            properties.SetColor(EquatorColor, sky.equatorColor.value);
            properties.SetColor(GroundColor, sky.groundColor.value);
            properties.SetFloat(EquatorHeight, sky.equatorHeight.value);
            properties.SetFloat(EquatorSmoothness, sky.equatorSmoothness.value);
            properties.SetFloat(HasBackgroundCubemap, hasBackgroundCubemap ? 1f : 0f);
            SetTextureIfAvailable(BackgroundCubemap, sky.backgroundCubemap.value);
            properties.SetFloat(BackgroundExposure, sky.backgroundExposure.value);

            properties.SetInt(EnablePatternOverlay, sky.enablePatternOverlay.value ? 1 : 0);
            properties.SetFloat(HasPatternCubemap, hasPatternCubemap ? 1f : 0f);
            SetTextureIfAvailable(PatternCubemap, sky.patternCubemap.value);
            properties.SetFloat(PatternContrast, sky.patternContrast.value);

            properties.SetInt(EnableStars, sky.enableStars.value ? 1 : 0);
            properties.SetFloat(HasStarsCubemap, hasStarsCubemap ? 1f : 0f);
            SetTextureIfAvailable(StarsCubemap, sky.starsCubemap.value);
            properties.SetFloat(StarsIntensity, sky.starsIntensity.value);
            properties.SetFloat(StarsVisibility, sky.starsVisibility.value);
            properties.SetFloat(StarsIgnoreExposure, sky.starsIgnoreExposure.value ? 1f : 0f);
            properties.SetInt(StarsLayers, sky.starsLayers.value);
            properties.SetColor(StarsColor, sky.starsColor.value);
            properties.SetFloat(StarsSize, sky.starsSize.value);
            properties.SetFloat(StarsSunMask, sky.starsSunMask.value);
            properties.SetFloat(StarsHeightMask, sky.starsHeightMask.value);
            properties.SetInt(StarsBottomMask, sky.starsBottomMask.value ? 1 : 0);
            properties.SetInt(EnableStarsTwinkling, sky.enableStarsTwinkling.value ? 1 : 0);
            properties.SetFloat(TwinklingContrast, sky.twinklingContrast.value);
            properties.SetFloat(TwinklingSpeed, sky.twinklingSpeed.value);
            properties.SetInt(EnableStarsRotation, sky.enableStarsRotation.value ? 1 : 0);
            properties.SetFloat(StarsRotation, sky.starsRotation.value);
            properties.SetVector(StarsRotationAxis, SafeDirection(sky.starsRotationAxis.value, Vector3.up));
            properties.SetFloat(StarsRotationSpeed, sky.starsRotationSpeed.value);

            properties.SetInt(EnableSun, sky.enableSun.value && renderSunDisk ? 1 : 0);
            properties.SetFloat(HasSunTexture, hasSunTexture ? 1f : 0f);
            SetTextureIfAvailable(SunTexture, sky.sunTexture.value);
            properties.SetColor(SunColor, sky.sunColor.value);
            properties.SetFloat(SunSize, sky.sunSize.value);
            properties.SetFloat(SunIntensity, sky.sunIntensity.value);
            properties.SetVector(SunDirection, SafeDirection(sky.sunDirection.value, Vector3.forward));

            properties.SetInt(EnableMoon, sky.enableMoon.value ? 1 : 0);
            properties.SetFloat(HasMoonTexture, hasMoonTexture ? 1f : 0f);
            SetTextureIfAvailable(MoonTexture, sky.moonTexture.value);
            properties.SetColor(MoonColor, sky.moonColor.value);
            properties.SetFloat(MoonSize, sky.moonSize.value);
            properties.SetFloat(MoonIntensity, sky.moonIntensity.value);
            properties.SetFloat(MoonPhase, sky.moonPhase.value);
            properties.SetFloat(MoonDarkSideVisibility, sky.moonDarkSideVisibility.value);
            properties.SetFloat(MoonPhaseRoundness, sky.moonPhaseRoundness.value);
            properties.SetFloat(MoonPhaseFade, sky.moonPhaseFade.value);
            properties.SetVector(MoonDirection, SafeDirection(sky.moonDirection.value, Vector3.right));

            properties.SetInt(EnableClouds, sky.enableClouds.value ? 1 : 0);
            properties.SetFloat(HasCloudsCubemap, hasCloudsCubemap ? 1f : 0f);
            SetTextureIfAvailable(CloudsCubemap, sky.cloudsCubemap.value);
            properties.SetFloat(CloudsOpacity, sky.cloudsOpacity.value);
            properties.SetFloat(CloudsFade, sky.cloudsFade.value);
            properties.SetFloat(CloudsHeight, sky.cloudsHeight.value);
            properties.SetVector(CloudsOffset, sky.cloudsOffset.value);
            properties.SetVector(CloudsShape, new Vector4(sky.cloudsCoverage.value, sky.cloudsScale.value,
                sky.cloudsSoftness.value, sky.cloudsDetail.value));
            properties.SetVector(CloudsAnimation, new Vector4(sky.cloudsWindVelocity.value.x,
                sky.cloudsWindVelocity.value.y, sky.cloudsEvolutionSpeed.value, sky.cloudsDeformation.value));
            properties.SetFloat(CloudsHorizonFade, sky.cloudsHorizonFade.value);
            float fadeStart = Mathf.Max(0f, sky.cloudsDistanceFade.value.x);
            float fadeEnd = Mathf.Max(fadeStart + 100f, sky.cloudsDistanceFade.value.y);
            properties.SetFloat(CloudsMaxHeight, sky.cloudsMaxHeight.value);
            properties.SetVector(CloudsLayer, new Vector4(sky.cloudsLayerAltitude.value,
                fadeStart, fadeEnd, sky.cloudsDistantScale.value));
            properties.SetFloat(CloudsBreakup, sky.cloudsBreakup.value);
            properties.SetColor(CloudsLightColor, sky.cloudsLightColor.value);
            properties.SetColor(CloudsShadowColor, sky.cloudsShadowColor.value);
            properties.SetInt(EnableCloudsRotation, sky.enableCloudsRotation.value ? 1 : 0);
            properties.SetFloat(CloudsRotation, sky.cloudsRotation.value);
            properties.SetVector(CloudsRotationAxis, SafeDirection(sky.cloudsRotationAxis.value, Vector3.up));
            properties.SetFloat(CloudsRotationSpeed, sky.cloudsRotationSpeed.value);

            properties.SetInt(EnableHorizonFog, sky.enableHorizonFog.value ? 1 : 0);
            properties.SetColor(HorizonFogColor, sky.horizonFogColor.value);
            properties.SetFloat(FogHeight, sky.fogHeight.value);
            properties.SetFloat(FogSmoothness, sky.fogSmoothness.value);
            properties.SetFloat(FogFill, sky.fogFill.value);

            properties.SetFloat(SkyboxOffset, sky.skyboxOffset.value);
            properties.SetFloat(SkyboxRotation, sky.rotation.value);
            properties.SetVector(SkyboxRotationAxis, SafeDirection(sky.skyboxRotationAxis.value, Vector3.up));

            if (!loggedFirstRender && !renderForCubemap)
            {
                loggedFirstRender = true;
                Debug.Log(
                    "[PolyverseStyleSkyRenderer] Rendering Polyverse Style Sky. " +
                    $"shader='{material.shader.name}', pass={pass}, debugSolidColor={sky.debugSolidColor.value}, " +
                    $"backgroundMode={sky.backgroundMode.value}, hasBackgroundCubemap={hasBackgroundCubemap}, " +
                    $"enableStars={sky.enableStars.value}, starsVisibility={sky.starsVisibility.value}, " +
                    $"starsIgnoreExposure={sky.starsIgnoreExposure.value}, " +
                    $"enableClouds={sky.enableClouds.value}, hasCloudsCubemap={hasCloudsCubemap}");
            }

            using (new ProfilingScope(builtinParams.commandBuffer, ProfilingSampler))
            {
                CoreUtils.DrawFullScreen(
                    builtinParams.commandBuffer,
                    material,
                    properties,
                    pass);
            }
        }

        private static Vector3 SafeDirection(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 0.000001f ? value.normalized : fallback;
        }

        private void SetTextureIfAvailable(int propertyId, Texture texture)
        {
            if (texture != null)
                properties.SetTexture(propertyId, texture);
        }

        private void SetFallbackTextures()
        {
            if (material == null)
                return;

            if (fallbackCubemap != null)
            {
                material.SetTexture(BackgroundCubemap, fallbackCubemap);
                material.SetTexture(PatternCubemap, fallbackCubemap);
                material.SetTexture(StarsCubemap, fallbackCubemap);
                material.SetTexture(CloudsCubemap, fallbackCubemap);
            }

            material.SetTexture(SunTexture, Texture2D.blackTexture);
            material.SetTexture(MoonTexture, Texture2D.blackTexture);
        }

        private static Cubemap CreateFallbackCubemap(Color color)
        {
            Cubemap cubemap = new(1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = { color };
            cubemap.SetPixels(pixels, CubemapFace.PositiveX);
            cubemap.SetPixels(pixels, CubemapFace.NegativeX);
            cubemap.SetPixels(pixels, CubemapFace.PositiveY);
            cubemap.SetPixels(pixels, CubemapFace.NegativeY);
            cubemap.SetPixels(pixels, CubemapFace.PositiveZ);
            cubemap.SetPixels(pixels, CubemapFace.NegativeZ);
            cubemap.Apply(false, true);

            return cubemap;
        }
    }
}
