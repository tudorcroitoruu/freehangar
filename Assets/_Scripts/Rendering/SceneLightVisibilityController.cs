using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;

/// <summary>Owns view/distance culling for realtime local lights in this scene during play.</summary>
[DisallowMultipleComponent]
public sealed class SceneLightVisibilityController : MonoBehaviour
{
    [SerializeField, Tooltip("Player/world camera. When empty, falls back to Camera.main.")]
    private Camera viewCamera;
    [SerializeField, Range(0f, 35f), Tooltip("Extra degrees beyond each edge of the camera view.")]
    private float viewPaddingDegrees = 10f;
    [SerializeField, Range(0f, 15f), Tooltip("Extra exit margin to prevent flicker at view edges.")]
    private float viewHysteresisDegrees = 5f;
    [SerializeField, Min(0f), Tooltip("Shadows are allowed within this distance of the camera, in metres.")]
    private float shadowDistance = 18f;
    [SerializeField, Min(0f), Tooltip("Shadows already enabled remain on this much farther away.")]
    private float shadowDistanceHysteresis = 2f;
    [SerializeField, Range(0f, .5f), Tooltip("Seconds between view updates. Zero updates every LateUpdate.")]
    private float updateInterval = .05f;
    [SerializeField, Min(.5f), Tooltip("Seconds between scans for spawned/removed lights.")]
    private float discoveryInterval = 2f;
    [SerializeField, Tooltip("Optional lights to leave entirely under their existing control.")]
    private Light[] excludedLights = new Light[0];

    private sealed class LightState
    {
        public Light Light;
        public bool AuthoredEnabled, LastEnabled, Visible, NearForShadows, Seen;
        public LightShadows AuthoredShadows, LastShadows;
    }

    private readonly List<LightState> lights = new List<LightState>();
    private readonly Dictionary<Light, LightState> lookup = new Dictionary<Light, LightState>();
    private static readonly ProfilerMarker EvaluateMarker = new ProfilerMarker("SceneLightVisibility.Evaluate");
    private Camera fallbackCamera;
    private float nextUpdate, nextDiscovery, nextCameraLookup;
    private bool ownsScene;

    public Camera ViewCamera
    {
        get => viewCamera;
        set { viewCamera = value; fallbackCamera = null; nextCameraLookup = 0; EvaluateNow(); }
    }
    public int ManagedLightCount => lights.Count;
    public int EnabledLightCount { get; private set; }
    public int ShadowedLightCount { get; private set; }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        foreach (var other in Object.FindObjectsByType<SceneLightVisibilityController>(FindObjectsSortMode.None))
        {
            if (other != this && other.ownsScene && other.gameObject.scene == gameObject.scene)
            {
                Debug.LogWarning("This scene already has an active light visibility controller.", this);
                enabled = false;
                return;
            }
        }
        ownsScene = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        nextCameraLookup = nextUpdate = nextDiscovery = 0;
        RefreshLights();
        EvaluateNow();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ownsScene = false;
        for (int i = 0; i < lights.Count; i++) Restore(lights[i]);
        lights.Clear();
        lookup.Clear();
        EnabledLightCount = ShadowedLightCount = 0;
    }

    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        if (now >= nextDiscovery) RefreshLights();
        if (now < nextUpdate) return;
        nextUpdate = now + updateInterval;
        EvaluateNow();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshLights();

    private bool ShouldManage(Light light)
    {
        if (!light || light.gameObject.scene != gameObject.scene ||
            light.type == LightType.Directional || light.lightmapBakeType != LightmapBakeType.Realtime)
            return false;
        for (int i = 0; i < excludedLights.Length; i++)
            if (excludedLights[i] == light) return false;
        return true;
    }

    [ContextMenu("Rescan scene lights (Play Mode)")]
    public void RefreshLights()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        for (int i = 0; i < lights.Count; i++) lights[i].Seen = false;
        // Discovery allocates only on this slow interval, never in the view-update loop.
        var found = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            Light light = found[i];
            if (!ShouldManage(light)) continue;
            if (!lookup.TryGetValue(light, out LightState state))
            {
                state = new LightState
                {
                    Light = light, AuthoredEnabled = light.enabled, LastEnabled = light.enabled,
                    AuthoredShadows = light.shadows, LastShadows = light.shadows
                };
                lights.Add(state);
                lookup.Add(light, state);
            }
            state.Seen = true;
        }
        for (int i = lights.Count - 1; i >= 0; i--)
        {
            LightState state = lights[i];
            if (state.Seen) continue;
            Restore(state);
            lookup.Remove(state.Light);
            lights.RemoveAt(i);
        }
        nextDiscovery = Time.unscaledTime + discoveryInterval;
    }

    private Camera ResolveCamera()
    {
        if (viewCamera) return viewCamera;
        if (Time.unscaledTime >= nextCameraLookup)
        {
            fallbackCamera = Camera.main;
            nextCameraLookup = Time.unscaledTime + 1f;
        }
        return fallbackCamera;
    }

    /// <summary>Immediate update, also useful after moving a camera or changing light state.</summary>
    public void EvaluateNow()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        using (EvaluateMarker.Auto())
        {
            Camera camera = ResolveCamera();
            bool hasCamera = camera && camera.isActiveAndEnabled;
            Vector3 position = hasCamera ? camera.transform.position : Vector3.zero;
            Quaternion inverseRotation = hasCamera ? Quaternion.Inverse(camera.transform.rotation) : Quaternion.identity;
            float halfVertical = hasCamera ? camera.fieldOfView * .5f : 0;
            float halfHorizontal = hasCamera
                ? Mathf.Atan(Mathf.Tan(halfVertical * Mathf.Deg2Rad) * camera.aspect) * Mathf.Rad2Deg : 0;
            float enterX = Mathf.Tan(Mathf.Min(89f, halfHorizontal + viewPaddingDegrees) * Mathf.Deg2Rad);
            float enterY = Mathf.Tan(Mathf.Min(89f, halfVertical + viewPaddingDegrees) * Mathf.Deg2Rad);
            float exitX = Mathf.Tan(Mathf.Min(89f, halfHorizontal + viewPaddingDegrees + viewHysteresisDegrees) * Mathf.Deg2Rad);
            float exitY = Mathf.Tan(Mathf.Min(89f, halfVertical + viewPaddingDegrees + viewHysteresisDegrees) * Mathf.Deg2Rad);
            EnabledLightCount = ShadowedLightCount = 0;
            for (int i = 0; i < lights.Count; i++)
            {
                LightState state = lights[i];
                Light light = state.Light;
                if (!light) continue;
                CaptureExternalChanges(state);
                if (!ShouldManage(light) || !hasCamera || !light.gameObject.activeInHierarchy || !state.AuthoredEnabled)
                {
                    Apply(state, state.AuthoredEnabled, state.AuthoredShadows);
                    state.Visible = state.NearForShadows = false;
                }
                else
                {
                    Vector3 offset = light.transform.position - position;
                    Vector3 local = inverseRotation * offset;
                    float limitX = state.Visible ? exitX : enterX;
                    float limitY = state.Visible ? exitY : enterY;
                    if (camera.orthographic)
                    {
                        float margin = Mathf.Max(0, local.z) * Mathf.Tan(viewPaddingDegrees * Mathf.Deg2Rad);
                        if (state.Visible) margin += .25f;
                        state.Visible = local.z >= 0 &&
                            Mathf.Abs(local.x) <= camera.orthographicSize * camera.aspect + margin &&
                            Mathf.Abs(local.y) <= camera.orthographicSize + margin;
                    }
                    else
                    {
                        state.Visible = offset.sqrMagnitude < .000001f ||
                            (local.z > 0 && Mathf.Abs(local.x) <= local.z * limitX && Mathf.Abs(local.y) <= local.z * limitY);
                    }
                    float distanceLimit = shadowDistance + (state.NearForShadows ? shadowDistanceHysteresis : 0);
                    state.NearForShadows = offset.sqrMagnitude <= distanceLimit * distanceLimit;
                    Apply(state, state.Visible,
                        state.Visible && state.NearForShadows ? state.AuthoredShadows : LightShadows.None);
                }
                if (!light.isActiveAndEnabled) continue;
                EnabledLightCount++;
                if (light.shadows != LightShadows.None) ShadowedLightCount++;
            }
        }
    }

    /// <summary>Use this for gameplay switches, including lights currently culled by this tool.</summary>
    public void SetLightEnabled(Light light, bool enabled)
    {
        if (!light) return;
        if (!isActiveAndEnabled || !Application.isPlaying || !ShouldManage(light))
        { light.enabled = enabled; return; }
        if (!lookup.TryGetValue(light, out LightState state))
        {
            light.enabled = enabled;
            RefreshLights();
            lookup.TryGetValue(light, out state);
        }
        if (state != null) state.AuthoredEnabled = enabled;
        EvaluateNow();
    }

    private static void CaptureExternalChanges(LightState state)
    {
        // Respect direct changes when distinguishable from the controller's own last write.
        if (state.Light.enabled != state.LastEnabled) state.AuthoredEnabled = state.Light.enabled;
        if (state.Light.shadows != state.LastShadows) state.AuthoredShadows = state.Light.shadows;
    }

    private static void Apply(LightState state, bool enabled, LightShadows shadows)
    {
        if (state.Light.enabled != enabled) state.Light.enabled = enabled;
        // HDRP's EnableShadows API writes this same legacy Light property. Preserve Hard/Soft exactly.
        if (state.Light.shadows != shadows) state.Light.shadows = shadows;
        state.LastEnabled = enabled;
        state.LastShadows = shadows;
    }

    private static void Restore(LightState state)
    {
        if (!state.Light) return;
        CaptureExternalChanges(state);
        Apply(state, state.AuthoredEnabled, state.AuthoredShadows);
    }

    private void OnValidate()
    {
        shadowDistance = Mathf.Max(0, shadowDistance);
        shadowDistanceHysteresis = Mathf.Max(0, shadowDistanceHysteresis);
        discoveryInterval = Mathf.Max(.5f, discoveryInterval);
        updateInterval = Mathf.Max(0, updateInterval);
    }
}
