using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using UnityEngine.Rendering;

/// <summary>Scene-owned cinematic. Runtime staging is removed and authored state restored on cancellation.</summary>
[DefaultExecutionOrder(-300)]
public sealed class RocketEndingCutscene : MonoBehaviour
{
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private GameObject rocketPrefab, fleetPrefab;
    [SerializeField] private Transform earth, departureReference;
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private Light sun;
    [SerializeField] private GameObject[] hideDuringCutscene;
    [Header("Seconds")]
    [SerializeField] private float cabinSeconds = 6.5f, pullbackSeconds = 6f, fleetSeconds = 8f, sunSeconds = 7f;
    [SerializeField] private float openingFadeSeconds = 1.6f;
    [Header("Flight")]
    [SerializeField, Range(1,16)] private int companionCount = 8;
    [SerializeField] private float rocketScale = .9f, flightSpeed = 4f;
    [SerializeField, Min(1)] private float companionFullSizeAltitude = 60f;
    public bool IsPlaying { get; private set; }
    public bool Completed { get; private set; }
    public string CurrentShot { get; private set; } = "Ready";
    public Camera CinematicCamera => view;
    public int FlyingRocketCount => ships.Count;
    private GameObject staging;
    private Camera view;
    private Transform hero;
    private Canvas overlay;
    private Image black;
    private Transform sunDisc;
    private Material sunMaterial;
    private VolumeProfile transitionProfile;
    private ChromaticAberration chromatic;
    private Renderer[] heroExhaust = new Renderer[0];
    private readonly List<Transform> ships = new List<Transform>();
    private readonly List<Vector3> shipStarts = new List<Vector3>();
    private readonly List<Vector3> shipDirections = new List<Vector3>();
    private readonly List<float> launchDelays = new List<float>(), shipSpeeds = new List<float>();
    private static readonly AnimationCurve LookPitch = LookCurve(3,-16,-16,-8,-8,-10,-10);
    private static readonly AnimationCurve LookYaw = LookCurve(0,-10,-10,15,15,5,5);
    private static AnimationCurve LookCurve(params float[] angles)
    {
        float[] times = {0,.25f,.34f,.70f,.80f,.96f,1};
        var curve = new AnimationCurve();
        // Equal-value pairs hold the glance briefly; zero tangents ease into each stop.
        for (int i=0;i<angles.Length;i++) curve.AddKey(new Keyframe(times[i],angles[i],0,0));
        return curve;
    }
    private readonly Dictionary<Behaviour,bool> behaviours = new Dictionary<Behaviour,bool>();
    private readonly Dictionary<GameObject,bool> hidden = new Dictionary<GameObject,bool>();
    private readonly Dictionary<Canvas,bool> canvases = new Dictionary<Canvas,bool>();
    private Vector3 direction, departure, finalCameraStart, finalCameraEnd;
    private Quaternion orientation, sunStartRotation;
    private float elapsed;
    private float earthRadius;
    private bool sunStarted;
    private CursorLockMode cursorMode;
    private bool cursorVisible;

    private void Start() { if (playOnStart) Play(); }
    public bool Play()
    {
        if (IsPlaying || Completed || rocketPrefab == null || fleetPrefab == null || earth == null || sourceCamera == null || sun == null) return false;
        IsPlaying = true; elapsed = 0; sunStarted = false;
        cursorMode = Cursor.lockState; cursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Suspend(camera);
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) Suspend(listener);
        foreach (var obj in hideDuringCutscene) if (obj != null) { hidden[obj] = obj.activeSelf; obj.SetActive(false); }
        staging = new GameObject("Ending runtime staging");
        var cameraObject = new GameObject("Ending cinematic camera"); cameraObject.transform.SetParent(staging.transform);
        view = cameraObject.AddComponent<Camera>(); view.CopyFrom(sourceCamera); view.enabled = true;
        view.nearClipPlane = .02f; view.farClipPlane = 3000; view.fieldOfView = 70;
        var hd = cameraObject.AddComponent<HDAdditionalCameraData>();
        var sourceHd = sourceCamera.GetComponent<HDAdditionalCameraData>(); if (sourceHd != null) sourceHd.CopyTo(hd);
        cameraObject.AddComponent<AudioListener>();
        var transition = cameraObject.AddComponent<Volume>(); transition.isGlobal = true; transition.priority = 100;
        transitionProfile = ScriptableObject.CreateInstance<VolumeProfile>(); transition.sharedProfile = transitionProfile;
        chromatic = transitionProfile.Add<ChromaticAberration>(true); chromatic.intensity.Override(0);
        direction = departureReference != null ? (departureReference.position - earth.position).normalized : Vector3.up;
        if (direction.sqrMagnitude < .1f) direction = Vector3.up;
        var forward = Vector3.ProjectOnPlane(-sun.transform.forward, direction).normalized;
        if (forward.sqrMagnitude < .1f) forward = Vector3.ProjectOnPlane(Vector3.forward, direction).normalized;
        orientation = Quaternion.LookRotation(forward, direction);
        float radius = 100;
        foreach (var r in earth.GetComponentsInChildren<Renderer>()) radius = Mathf.Max(radius, Mathf.Min(r.bounds.extents.x,r.bounds.extents.y,r.bounds.extents.z));
        earthRadius = radius;
        departure = earth.position + direction * (radius * 1.06f);
        hero = Spawn(rocketPrefab, "Player rocket", departure, true);
        var engine = hero.Find("Engine exhaust VFX");
        heroExhaust = engine != null ? engine.GetComponentsInChildren<Renderer>() : new Renderer[0];
        Vector3[] launchLayout = { new Vector3(-49,18,-25),new Vector3(62,45,9),new Vector3(-28,2,36),new Vector3(37,4,-21),new Vector3(-76,30,8),new Vector3(72,1,42),new Vector3(-17,12,-42),new Vector3(12,3,48) };
        float[] delays = {0,.2f,1.2f,2.5f,.4f,2,.8f,3.5f};
        for (int n = 0; n < companionCount; n++)
        {
            var layout = launchLayout[n % launchLayout.Length];
            bool alreadyInSpace = n % 8 == 0 || n % 8 == 1 || n % 8 == 4;
            if (alreadyInSpace) layout.x *= 1.6f;
            if (n >= launchLayout.Length) layout.x *= 1.4f;
            var radial = (direction * radius + orientation * new Vector3(layout.x,0,layout.z)).normalized;
            float startingAltitude = alreadyInSpace ? 45 + layout.y : -12;
            var ship = Spawn(fleetPrefab, "Companion rocket " + (n + 1), earth.position + radial * (radius + startingAltitude), false);
            ship.localScale = Vector3.one * (alreadyInSpace ? rocketScale : 0);
            shipDirections[n+1] = radial;
            // Keep the close exterior plan exclusively on the player's rocket and Earth.
            launchDelays[n+1] = alreadyInSpace ? 0 : cabinSeconds + pullbackSeconds + delays[n % delays.Length];
            shipSpeeds[n+1] = alreadyInSpace ? .45f + (n % 3)*.08f : .72f + (n * 7 % 9)*.09f;
            if (launchDelays[n+1] > 0) ship.gameObject.SetActive(false);
        }
        var fill = new GameObject("Cinematic soft fill"); fill.transform.SetParent(staging.transform);
        fill.transform.rotation = Quaternion.LookRotation(-(orientation * new Vector3(1, .1f, 1)).normalized, direction);
        var lamp = fill.AddComponent<Light>(); lamp.type = LightType.Directional;
        fill.AddComponent<HDAdditionalLightData>(); // Initialize HDRP defaults before setting physical intensity.
        lamp.color = new Color(.4f,.65f,1); lamp.intensity = 2; lamp.shadows = LightShadows.None;
        // HDRI skies have no physical sun disc; keep one at optical infinity along the authored light.
        var disc = GameObject.CreatePrimitive(PrimitiveType.Sphere); disc.name = "Ending sun disc";
        disc.transform.SetParent(staging.transform); disc.transform.localScale = Vector3.one * 30;
        Destroy(disc.GetComponent<Collider>()); sunDisc = disc.transform;
        sunMaterial = new Material(Shader.Find("HDRP/Unlit")); sunMaterial.name = "Ending sun glow";
        sunMaterial.SetColor("_UnlitColor",new Color(8,7.8f,7.3f));
        var discRenderer = disc.GetComponent<Renderer>(); discRenderer.sharedMaterial = sunMaterial;
        discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var fadeObject = new GameObject("Ending fade", typeof(Canvas)); fadeObject.transform.SetParent(staging.transform);
        overlay = fadeObject.GetComponent<Canvas>(); overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.sortingOrder = 32760;
        var panel = new GameObject("Black", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(fadeObject.transform,false);
        black = panel.GetComponent<Image>(); black.color = Color.black; black.raycastTarget = false;
        black.rectTransform.anchorMin = Vector2.zero; black.rectTransform.anchorMax = Vector2.one;
        black.rectTransform.offsetMin = black.rectTransform.offsetMax = Vector2.zero;
        HideHud(); Evaluate(0); HDCamera.GetOrCreate(view).Reset(); return true;
    }
    private Transform Spawn(GameObject prefab,string name,Vector3 position,bool interior)
    {
        var ship = Instantiate(prefab, position, orientation, staging.transform); ship.name = name; ship.transform.localScale = Vector3.one * rocketScale;
        foreach (var collider in ship.GetComponentsInChildren<Collider>()) collider.enabled = false;
        var stairs = ship.transform.Find("Entrance Stairs"); if (stairs != null) stairs.gameObject.SetActive(false);
        var hatch = ship.transform.Find("Entrance Door"); if (hatch != null) hatch.localRotation = Quaternion.identity;
        var exhaust = ship.transform.Find("Engine exhaust VFX"); if (exhaust != null) exhaust.localScale = Vector3.one * .95f;
        foreach (var particle in ship.GetComponentsInChildren<ParticleSystem>())
        {
            var main = particle.main;
            if (particle.name == "FireSpikes")
            {
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.4f,.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(13,23);
                main.startSize = new ParticleSystem.MinMaxCurve(.35f,.8f);
                var emission = particle.emission; emission.rateOverTime = 120;
            }
            if (particle.name == "SparkTrail") { main.startLifetime = .8f; main.startSpeed = 24; }
            if (particle.name == "FireSpikes" || particle.name == "SparkTrail" || particle.name == "SmokeTrail")
            {
                var noise = particle.noise; noise.enabled = true; noise.separateAxes = true;
                noise.strengthX = .7f; noise.strengthY = .6f; noise.strengthZ = .22f;
                noise.frequency = 1.8f; noise.scrollSpeed = 2.2f; noise.octaveCount = 2;
                noise.quality = ParticleSystemNoiseQuality.Medium;
            }
            var renderer = particle.GetComponent<ParticleSystemRenderer>();
            if (renderer.sharedMaterial.HasProperty("_Color") && particle.name != "SmokeTrail")
            {
                var tint = renderer.sharedMaterial.GetColor("_Color"); float alpha = tint.a; tint *= 2; tint.a = alpha;
                var properties = new MaterialPropertyBlock(); properties.SetColor("_Color",tint); renderer.SetPropertyBlock(properties);
            }
            particle.Play(true);
        }
        ships.Add(ship.transform); shipStarts.Add(position); shipDirections.Add(direction); launchDelays.Add(0); shipSpeeds.Add(1); return ship.transform;
    }
    private void Suspend(Behaviour b) { behaviours[b] = b.enabled; b.enabled = false; }
    private void HideHud()
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { if (canvas == overlay) continue; if (!canvases.ContainsKey(canvas)) canvases.Add(canvas,canvas.enabled); canvas.enabled = false; }
    }
    private void Update()
    {
        if (!IsPlaying || hero == null || view == null) return;
        elapsed += Time.deltaTime;
        for (int k = 0; k < ships.Count; k++) if (ships[k] != null)
        {
            float flightTime = elapsed - launchDelays[k];
            if (flightTime < 0) continue;
            if (!ships[k].gameObject.activeSelf) ships[k].gameObject.SetActive(true);
            float thrustTime = Mathf.Max(0, flightTime - (k == 0 ? cabinSeconds : 0));
            float distance = flightTime * flightSpeed + .22f * thrustTime * thrustTime;
            ships[k].position = shipStarts[k] + shipDirections[k] * distance * shipSpeeds[k]
                + orientation * new Vector3(Mathf.Sin(elapsed*.8f+k)*.16f,0,Mathf.Sin(elapsed*.6f+k)*.12f);
            if (k > 0)
            {
                // Grow with actual height above Earth, never while still below its surface.
                float altitude = Vector3.Distance(ships[k].position, earth.position) - earthRadius;
                float growth = Smooth(altitude / companionFullSizeAltitude);
                // Preserve monotonic growth despite the small flight sway.
                float targetScale = Mathf.Max(ships[k].localScale.x, rocketScale * growth);
                ships[k].localScale = Vector3.one * Mathf.MoveTowards(ships[k].localScale.x,targetScale,rocketScale * Time.deltaTime / 8f);
            }
            ships[k].rotation = Quaternion.FromToRotation(direction,shipDirections[k]) * orientation * Quaternion.Euler(Mathf.Sin(elapsed*1.1f+k)*.7f,0,Mathf.Sin(elapsed*.9f+k)*1.2f);
        }
        Evaluate(elapsed);
    }
    private void LateUpdate()
    {
        if (!IsPlaying) return;
        HideHud();
        if (sunDisc != null && view != null) sunDisc.position = view.transform.position - sun.transform.forward * 1400;
    }
    private static float Smooth(float t) => Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
    private void Evaluate(float time)
    {
        foreach (var exhaustRenderer in heroExhaust)
            if (exhaustRenderer != null) exhaustRenderer.enabled = time >= cabinSeconds + .1f;
        black.color = new Color(0,0,0,1-Smooth(time / Mathf.Max(.1f,openingFadeSeconds)));
        float pulse = Mathf.Clamp01((time-cabinSeconds+.22f)/.7f);
        chromatic.intensity.value = Mathf.Sin(pulse*Mathf.PI);
        if (time < cabinSeconds)
        {
            CurrentShot = "Inside: anxious look";
            float t = time / cabinSeconds;
            // Measured glances with short rests and only a subtle breathing movement.
            float yaw = LookYaw.Evaluate(t);
            var facing = Quaternion.Euler(LookPitch.Evaluate(t),180+yaw,-yaw*.035f);
            var breathing = new Vector3(0,Mathf.Sin(time*2.1f)*.004f,Mathf.Sin(time*2.1f)*.003f);
            view.transform.SetPositionAndRotation(hero.TransformPoint(new Vector3(0,2.75f,.65f)+breathing), orientation * facing);
            return;
        }
        if (time < cabinSeconds + pullbackSeconds)
        {
            CurrentShot = "Pullback: leaving Earth";
            float t = (time-cabinSeconds)/pullbackSeconds;
            // An optical cut avoids moving through the sealed hull or opening a hatch in flight.
            if (time < cabinSeconds + .1f) return;
            Vector3 local = Vector3.Lerp(new Vector3(7,17,15),new Vector3(8,18,17),Smooth(t));
            var position = hero.TransformPoint(local);
            var look = Quaternion.LookRotation(hero.TransformPoint(new Vector3(0,4.3f,0))-position,direction);
            var rotation = look * Quaternion.Euler(0,0,-35);
            float shakeTime = time - cabinSeconds - .1f;
            float shakePhase = Mathf.Clamp01(shakeTime / .75f);
            float shake = Mathf.Sin(shakePhase * Mathf.PI) * (1-shakePhase);
            position += rotation * new Vector3(Mathf.Sin(shakeTime*31)*.018f,Mathf.Sin(shakeTime*37)*.012f,0) * shake;
            rotation *= Quaternion.Euler(new Vector3(Mathf.Sin(shakeTime*29)*.4f,Mathf.Sin(shakeTime*34)*.25f,Mathf.Sin(shakeTime*23)*.35f) * shake);
            view.transform.SetPositionAndRotation(position,rotation);
            view.fieldOfView = Mathf.Lerp(50,53,Smooth(t)); return;
        }
        if (time < cabinSeconds + pullbackSeconds + fleetSeconds)
        {
            CurrentShot = "Wide: rocket fleet";
            float t = Smooth((time-cabinSeconds-pullbackSeconds)/fleetSeconds);
            var local = Vector3.Lerp(new Vector3(8,18,17),new Vector3(35,100,130),t);
            var position = hero.TransformPoint(local);
            view.transform.SetPositionAndRotation(position,Quaternion.LookRotation(hero.TransformPoint(new Vector3(0,4.3f,0))-position,direction) * Quaternion.Euler(0,0,Mathf.Lerp(-35,-12,t)));
            view.fieldOfView = Mathf.Lerp(53,62,t); return;
        }
        if (!sunStarted)
        {
            finalCameraStart = view.transform.position; sunStartRotation = view.transform.rotation;
            finalCameraEnd = finalCameraStart + orientation * new Vector3(8,12,25) * rocketScale; sunStarted = true;
        }
        CurrentShot = "Final: face the sun";
        float u = Mathf.Clamp01((time-cabinSeconds-pullbackSeconds-fleetSeconds)/sunSeconds);
        float decelerate = 1-Mathf.Pow(1-u,3);
        var sunLook = Quaternion.LookRotation(-sun.transform.forward,Vector3.up);
        view.transform.SetPositionAndRotation(Vector3.Lerp(finalCameraStart,finalCameraEnd,decelerate),Quaternion.Slerp(sunStartRotation,sunLook,Smooth(u)));
        if (u >= 1) { Completed = true; CurrentShot = "Complete: sun hold"; }
    }
    public void Cancel()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        if (staging != null) Destroy(staging);
        if (sunMaterial != null) Destroy(sunMaterial);
        if (transitionProfile != null) { foreach (var component in transitionProfile.components) Destroy(component); Destroy(transitionProfile); }
        foreach (var state in behaviours) if (state.Key != null) state.Key.enabled = state.Value;
        foreach (var state in hidden) if (state.Key != null) state.Key.SetActive(state.Value);
        foreach (var state in canvases) if (state.Key != null) state.Key.enabled = state.Value;
        behaviours.Clear(); hidden.Clear(); canvases.Clear(); ships.Clear(); shipStarts.Clear();
        shipDirections.Clear(); launchDelays.Clear(); shipSpeeds.Clear();
        Cursor.lockState = cursorMode; Cursor.visible = cursorVisible;
        CurrentShot = "Stopped";
    }
    private void OnDisable() => Cancel();
}
