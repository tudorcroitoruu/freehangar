using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Liviu.Environment.HDRP
{
    public enum PolyverseSkyTimeMode
    {
        Day,
        Night,
        Full24Hours
    }

    [DisallowMultipleComponent]
    public sealed class PolyverseStyleSkyTimeController : MonoBehaviour
    {
        [Serializable]
        public sealed class TimeModeChangedEvent : UnityEvent<PolyverseSkyTimeMode>
        {
        }

        [Header("References")]
        public Volume skyVolume;
        public Camera targetCamera;
        public Light sunLight;
        public Light moonLight;

        [Header("Time")]
        [SerializeField]
        private PolyverseSkyTimeMode timeMode = PolyverseSkyTimeMode.Full24Hours;

        [Range(0f, 24f)]
        public float hour = 12f;

        public bool advanceTime = true;

        [Min(0.01f)]
        public float fullDayLengthMinutes = 24f;

        [Range(0f, 24f)]
        public float dayStartHour = 6f;

        [Range(0f, 24f)]
        public float dayEndHour = 18f;

        [Range(0f, 24f)]
        public float nightStartHour = 18f;

        [Range(0f, 24f)]
        public float nightEndHour = 6f;

        [Range(0f, 24f)]
        public float fixedDayHour = 12f;

        [Range(0f, 24f)]
        public float fixedNightHour = 0f;

        public float celestialAzimuth;
        public float horizonFadeStart = -0.08f;
        public float horizonFadeEnd = 0.12f;

        [Header("Sky Colors")]
        public bool driveSkyColors = true;
        public Color daySkyColor = new(0.498f, 0.745f, 1f, 1f);
        public Color dayEquatorColor = new(1f, 0.747f, 0f, 1f);
        public Color dayGroundColor = new(0.498f, 0.497f, 0f, 1f);
        public Color nightSkyColor = new(0.005f, 0.012f, 0.035f, 1f);
        public Color nightEquatorColor = new(0.03f, 0.04f, 0.08f, 1f);
        public Color nightGroundColor = new(0.005f, 0.006f, 0.01f, 1f);

        [Header("Sun And Moon")]
        public bool driveCelestialIntensity = true;
        public float sunSkyIntensity = 1f;
        public float moonSkyIntensity = 1f;
        public float sunLightIntensity = 100000f;
        public float moonLightIntensity = 0.2f;

        [Range(0f, 1f)]
        public float moonPhase = 0f;

        public bool advanceMoonPhase = true;

        [Min(0.01f)]
        public float lunarCycleDays = 29.53f;

        [Header("Clouds")]
        public Vector2 windDirection = Vector2.right;
        public float windSpeed = 0.01f;

        [Range(0f, 1f)]
        public float targetCloudFade = 1f;

        [Min(0f)]
        public float cloudFadeSpeed = 0.25f;

        [Header("Stars")]
        [Range(0f, 1f)]
        public float maxStarVisibility = 1f;

        public bool fadeStarsNearLocalLights = true;
        public Light[] localLights = Array.Empty<Light>();

        [Min(0.1f)]
        public float localLightIntensityForFullFade = 5f;

        [Range(0f, 1f)]
        public float localLightStarFadeStrength = 1f;

        [Range(0f, 1f)]
        public float minimumStarVisibilityNearLights;

        [Min(0.1f)]
        public float lightRefreshInterval = 2f;

        [Header("HDRP Volume")]
        public bool forceCustomSkyOnVolume = true;

        [Tooltip("Off by default to avoid expensive realtime environment cubemap refreshes while tuning in the editor.")]
        public bool forceRealtimeEnvironmentUpdates;

        [Min(0f)]
        public float environmentUpdatePeriod = 0f;

        public TimeModeChangedEvent onTimeModeChanged = new();

        private PolyverseStyleSky sky;
        private VisualEnvironment visualEnvironment;
        private Vector2 cloudOffset;
        private float currentCloudFade = 1f;
        private float localLightStarFade = 1f;
        private float nextLightRefreshTime;
        private Light[] cachedSceneLights = Array.Empty<Light>();

        public PolyverseSkyTimeMode TimeMode => timeMode;
        public float CurrentHour => hour;
        public string CurrentTimeModeLabel => timeMode == PolyverseSkyTimeMode.Full24Hours ? "24H" : timeMode.ToString();

        private void Reset()
        {
            skyVolume = FindFirstSkyVolume();
            targetCamera = Camera.main;
            sunLight = RenderSettings.sun != null ? RenderSettings.sun : FindFirstDirectionalLight();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshSceneLights();
            ApplyModeBounds();
            ApplySky(true);
        }

        private void Update()
        {
            ResolveReferences();

            if (sky == null)
                return;

            UpdateTime();
            UpdateClouds();
            UpdateMoonPhase();
            UpdateLocalLightStarFade();
            ApplySky(false);
        }

        public void SetTimeMode(PolyverseSkyTimeMode mode)
        {
            if (timeMode == mode)
                return;

            timeMode = mode;
            ApplyModeBounds();
            onTimeModeChanged.Invoke(timeMode);
        }

        public void SetTimeMode(int mode)
        {
            SetTimeMode((PolyverseSkyTimeMode)Mathf.Clamp(mode, 0, 2));
        }

        public void SetDayMode()
        {
            SetTimeMode(PolyverseSkyTimeMode.Day);
        }

        public void SetNightMode()
        {
            SetTimeMode(PolyverseSkyTimeMode.Night);
        }

        public void SetFull24HoursMode()
        {
            SetTimeMode(PolyverseSkyTimeMode.Full24Hours);
        }

        public void CycleTimeMode()
        {
            SetTimeMode((PolyverseSkyTimeMode)(((int)timeMode + 1) % 3));
        }

        public void SetMoonPhase(float phase)
        {
            moonPhase = Mathf.Repeat(phase, 1f);
        }

        public void SetCloudFade(float fade)
        {
            targetCloudFade = Mathf.Clamp01(fade);
        }

        private void ResolveReferences()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (skyVolume == null)
                skyVolume = FindFirstSkyVolume();

            if (sunLight == null)
                sunLight = RenderSettings.sun != null ? RenderSettings.sun : FindFirstDirectionalLight();

            if (skyVolume == null)
                return;

            VolumeProfile profile = skyVolume.profile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = $"{skyVolume.name} Runtime Profile";
                skyVolume.sharedProfile = profile;
            }

            if (!profile.TryGet(out sky))
                sky = profile.Add<PolyverseStyleSky>(true);

            if (!profile.TryGet(out visualEnvironment))
                visualEnvironment = profile.Add<VisualEnvironment>(true);

            if (forceCustomSkyOnVolume)
            {
                visualEnvironment.active = true;
                visualEnvironment.skyType.overrideState = true;
                visualEnvironment.skyType.value = SkySettings.GetUniqueID<PolyverseStyleSky>();
            }
        }

        private void UpdateTime()
        {
            if (!advanceTime)
            {
                ApplyModeBounds();
                return;
            }

            float deltaHours = Time.deltaTime * 24f / Mathf.Max(0.01f, fullDayLengthMinutes * 60f);

            switch (timeMode)
            {
                case PolyverseSkyTimeMode.Day:
                    hour = AdvanceInsideWindow(hour, dayStartHour, dayEndHour, deltaHours);
                    break;
                case PolyverseSkyTimeMode.Night:
                    hour = AdvanceInsideWindow(hour, nightStartHour, nightEndHour, deltaHours);
                    break;
                case PolyverseSkyTimeMode.Full24Hours:
                    hour = WrapHour(hour + deltaHours);
                    break;
            }
        }

        private void ApplyModeBounds()
        {
            switch (timeMode)
            {
                case PolyverseSkyTimeMode.Day:
                    if (!HourInWindow(hour, dayStartHour, dayEndHour))
                        hour = fixedDayHour;
                    break;
                case PolyverseSkyTimeMode.Night:
                    if (!HourInWindow(hour, nightStartHour, nightEndHour))
                        hour = fixedNightHour;
                    break;
                case PolyverseSkyTimeMode.Full24Hours:
                    hour = WrapHour(hour);
                    break;
            }
        }

        private void UpdateClouds()
        {
            Vector2 direction = windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized : Vector2.right;
            cloudOffset += direction * windSpeed * Time.deltaTime;
            currentCloudFade = Mathf.MoveTowards(currentCloudFade, targetCloudFade, cloudFadeSpeed * Time.deltaTime);
        }

        private void UpdateMoonPhase()
        {
            if (!advanceMoonPhase)
                return;

            float realSecondsPerGameDay = Mathf.Max(0.01f, fullDayLengthMinutes * 60f);
            moonPhase = Mathf.Repeat(moonPhase + Time.deltaTime / (realSecondsPerGameDay * lunarCycleDays), 1f);
        }

        private void UpdateLocalLightStarFade()
        {
            if (!fadeStarsNearLocalLights || targetCamera == null)
            {
                localLightStarFade = 1f;
                return;
            }

            Light[] lights = localLights != null && localLights.Length > 0
                ? localLights
                : cachedSceneLights;

            if ((localLights == null || localLights.Length == 0) && Time.unscaledTime >= nextLightRefreshTime)
                RefreshSceneLights();

            Vector3 cameraPosition = targetCamera.transform.position;
            float strongestInfluence = 0f;

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];

                if (light == null || !light.isActiveAndEnabled || light.type == LightType.Directional)
                    continue;

                float range = Mathf.Max(0.1f, light.range);
                float distance = Vector3.Distance(cameraPosition, light.transform.position);

                if (distance >= range)
                    continue;

                float rangeInfluence = 1f - distance / range;
                float intensityInfluence = Mathf.Clamp01(light.intensity / Mathf.Max(0.1f, localLightIntensityForFullFade));
                strongestInfluence = Mathf.Max(strongestInfluence, rangeInfluence * intensityInfluence);
            }

            float fadeAmount = Mathf.Clamp01(strongestInfluence) * localLightStarFadeStrength;
            localLightStarFade = Mathf.Lerp(1f, minimumStarVisibilityNearLights, fadeAmount);
        }

        private void RefreshSceneLights()
        {
#if UNITY_2023_1_OR_NEWER
            cachedSceneLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
#else
            cachedSceneLights = FindObjectsOfType<Light>();
#endif
            nextLightRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, lightRefreshInterval);
        }

        private void ApplySky(bool forceOverrideStates)
        {
            if (sky == null)
                return;

            Vector3 sunDirection = DirectionFromHour(hour, celestialAzimuth);
            Vector3 moonDirection = DirectionFromHour(WrapHour(hour + 12f), celestialAzimuth);
            float daylight = Mathf.InverseLerp(horizonFadeStart, horizonFadeEnd, sunDirection.y);
            daylight = Mathf.SmoothStep(0f, 1f, daylight);
            float moonlight = Mathf.InverseLerp(horizonFadeStart, horizonFadeEnd, moonDirection.y);
            moonlight = Mathf.SmoothStep(0f, 1f, moonlight) * (1f - daylight);
            float night = 1f - daylight;

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.LookRotation(-sunDirection, Vector3.up);
                sunLight.intensity = sunLightIntensity * daylight;
            }

            if (moonLight != null)
            {
                moonLight.transform.rotation = Quaternion.LookRotation(-moonDirection, Vector3.up);
                moonLight.intensity = moonLightIntensity * moonlight;
            }

            if (forceOverrideStates)
                SetRuntimeOverrideStates();

            if (forceRealtimeEnvironmentUpdates)
            {
                sky.updateMode.overrideState = true;
                sky.updateMode.value = EnvironmentUpdateMode.Realtime;
                sky.updatePeriod.overrideState = true;
                sky.updatePeriod.value = environmentUpdatePeriod;
            }
            else if (forceOverrideStates)
            {
                sky.updateMode.overrideState = true;
                sky.updateMode.value = EnvironmentUpdateMode.OnChanged;
            }

            sky.sunDirection.value = sunDirection;
            sky.moonDirection.value = moonDirection;
            sky.cloudsOffset.value = cloudOffset;
            // This controller already integrates wind into cloudsOffset. Avoid applying shader wind twice.
            sky.cloudsWindVelocity.Override(Vector2.zero);
            sky.cloudsFade.value = currentCloudFade;
            sky.moonPhase.value = moonPhase;
            sky.starsVisibility.value = night * maxStarVisibility * localLightStarFade;

            if (driveCelestialIntensity)
            {
                sky.sunIntensity.value = sunSkyIntensity * daylight;
                sky.moonIntensity.value = moonSkyIntensity * moonlight;
            }

            if (driveSkyColors)
            {
                sky.skyColor.value = Color.Lerp(nightSkyColor, daySkyColor, daylight);
                sky.equatorColor.value = Color.Lerp(nightEquatorColor, dayEquatorColor, daylight);
                sky.groundColor.value = Color.Lerp(nightGroundColor, dayGroundColor, daylight);
            }
        }

        private void SetRuntimeOverrideStates()
        {
            sky.sunDirection.overrideState = true;
            sky.moonDirection.overrideState = true;
            sky.cloudsOffset.overrideState = true;
            sky.cloudsFade.overrideState = true;
            sky.moonPhase.overrideState = true;
            sky.starsVisibility.overrideState = true;

            if (driveCelestialIntensity)
            {
                sky.sunIntensity.overrideState = true;
                sky.moonIntensity.overrideState = true;
            }

            if (driveSkyColors)
            {
                sky.skyColor.overrideState = true;
                sky.equatorColor.overrideState = true;
                sky.groundColor.overrideState = true;
            }
        }

        private static Vector3 DirectionFromHour(float hourValue, float yawDegrees)
        {
            float angle = WrapHour(hourValue) / 24f * 360f - 90f;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 localDirection = new(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
            return Quaternion.Euler(0f, yawDegrees, 0f) * localDirection;
        }

        private static float AdvanceInsideWindow(float currentHour, float startHour, float endHour, float deltaHours)
        {
            float length = Mathf.Repeat(endHour - startHour, 24f);

            if (length < 0.001f)
                length = 24f;

            float local = Mathf.Repeat(currentHour - startHour, length);
            return WrapHour(startHour + local + deltaHours);
        }

        private static bool HourInWindow(float value, float startHour, float endHour)
        {
            value = WrapHour(value);
            startHour = WrapHour(startHour);
            endHour = WrapHour(endHour);

            if (Mathf.Approximately(startHour, endHour))
                return true;

            if (startHour < endHour)
                return value >= startHour && value <= endHour;

            return value >= startHour || value <= endHour;
        }

        private static float WrapHour(float value)
        {
            return Mathf.Repeat(value, 24f);
        }

        private static Volume FindFirstSkyVolume()
        {
#if UNITY_2023_1_OR_NEWER
            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
#else
            Volume[] volumes = FindObjectsOfType<Volume>();
#endif
            for (int i = 0; i < volumes.Length; i++)
            {
                VolumeProfile profile = volumes[i].sharedProfile;

                if (profile != null && profile.TryGet<PolyverseStyleSky>(out _))
                    return volumes[i];
            }

#if UNITY_2023_1_OR_NEWER
            return FindAnyObjectByType<Volume>();
#else
            return FindObjectOfType<Volume>();
#endif
        }

        private static Light FindFirstDirectionalLight()
        {
#if UNITY_2023_1_OR_NEWER
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
#else
            Light[] lights = FindObjectsOfType<Light>();
#endif
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                    return lights[i];
            }

            return null;
        }
    }
}
