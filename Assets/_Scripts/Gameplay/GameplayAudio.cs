using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>Gameplay sound clips and short-lived/looping effects shared by the game scenes.</summary>
[DisallowMultipleComponent]
public sealed class GameplayAudio : MonoBehaviour
{
    public static GameplayAudio Instance { get; private set; }

    [SerializeField] private AudioMixerGroup effectsGroup;
    [Header("Airplane ambience")]
    [SerializeField] private AudioClip airplane;
    [SerializeField] private Vector2 airplaneIntervalSeconds = new Vector2(240f, 420f);
    [SerializeField] private Vector2 airplanePitchRange = new Vector2(.95f, 1.05f);
    [SerializeField] private Vector2 airplaneVolumeRange = new Vector2(.65f, .8f);
    private AudioSource airplaneSource;
    private Coroutine airplanePlayback;
    [Header("Animals")]
    [SerializeField] private AudioClip[] crowHits;
    [SerializeField] private AudioClip crowFlyAway, crowsIdle, ratHit, ratIdle;
    [Header("Controller")]
    [SerializeField] private AudioClip[] broomHits;
    [SerializeField] private AudioClip[] axeWoodHits;
    [SerializeField] private AudioClip hammerMetal, hammerWood, pickUp, drop;
    [SerializeField] private AudioClip hitAir;
    [SerializeField] private AudioClip switchInstruments;
    [SerializeField] private AudioClip closeBag;
    [SerializeField] private AudioClip drinkingBottle;
    [SerializeField] private AudioClip eat;
    public float BottleDrinkDuration => drinkingBottle != null ? drinkingBottle.length : 1.9f;
    public AudioSource StartBottleDrink()
    {
        if (drinkingBottle == null || WorldSaveRuntime.IsSeeking || WorldSaveRuntime.IsRestoring) return null;
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = effectsGroup;
        source.clip = drinkingBottle;
        source.volume = .9f;
        source.Play();
        return source;
    }
    [Header("Last scene")]
    [SerializeField] private AudioClip openDoor, openRocketDoor, closeDoor, rocket, switchElectrical, lightsOn;
    [Header("Other")]
    [SerializeField] private AudioClip paperGet, perkFound;
    [SerializeField] private AudioClip pressButton, cashIn, upgradeFailed, upgradeSuccess;
    [Header("Grain tools")]
    [SerializeField] private AudioClip[] shovelGets, fillGrain;
    [SerializeField] private AudioClip vacuum;
    [Header("Vehicles")]
    [SerializeField] private AudioClip resetVehicle;
    [SerializeField] private AudioClip bicycleMove, bicycleScratch, motorollerAndTrike, pickupMotor, wheelbarrow;
    [SerializeField] private AudioClip[] vehicleHits;

    private AudioSource effects;
    private readonly Dictionary<Transform, AudioSource> loops = new Dictionary<Transform, AudioSource>();

    private void Awake()
    {
        Instance = this;
        effects = gameObject.AddComponent<AudioSource>();
        effects.playOnAwake = false;
        effects.spatialBlend = 0f;
        effects.outputAudioMixerGroup = effectsGroup;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnEnable()
    {
        if (airplane == null) return;
        if (airplaneSource == null)
        {
            airplaneSource = gameObject.AddComponent<AudioSource>();
            airplaneSource.playOnAwake = false;
            airplaneSource.loop = false;
            airplaneSource.spatialBlend = 0f;
            airplaneSource.outputAudioMixerGroup = effectsGroup;
        }
        airplaneSource.clip = airplane;
        airplanePlayback = StartCoroutine(PlayAirplaneAmbience());
    }

    private IEnumerator PlayAirplaneAmbience()
    {
        while (true)
        {
            float minimum = Mathf.Max(1f, airplaneIntervalSeconds.x);
            float remaining = Random.Range(minimum, Mathf.Max(minimum, airplaneIntervalSeconds.y));
            while (remaining > 0f)
            {
                if (SceneManager.GetActiveScene().name == "Gameplay" && !LoadingScreen.isLoadingOn &&
                    !WorldSaveRuntime.IsSeeking && !WorldSaveRuntime.IsRestoring)
                    remaining -= Time.deltaTime;
                else if (airplaneSource.isPlaying) airplaneSource.Stop();
                yield return null;
            }
            if (SceneManager.GetActiveScene().name != "Gameplay" || LoadingScreen.isLoadingOn ||
                WorldSaveRuntime.IsSeeking || WorldSaveRuntime.IsRestoring || airplaneSource.isPlaying) continue;
            airplaneSource.pitch = Mathf.Clamp(Random.Range(airplanePitchRange.x, airplanePitchRange.y), .1f, 3f);
            airplaneSource.volume = Mathf.Clamp01(Random.Range(airplaneVolumeRange.x, airplaneVolumeRange.y));
            airplaneSource.Play();
        }
    }

    private void OnDisable()
    {
        if (airplanePlayback != null) StopCoroutine(airplanePlayback);
        airplanePlayback = null;
        if (airplaneSource != null) airplaneSource.Stop();
    }

    private void Play(AudioClip clip, float volume = 1f)
    {
        if (WorldSaveRuntime.IsSeeking || WorldSaveRuntime.IsRestoring) return;
        if (clip != null && effects != null) effects.PlayOneShot(clip, volume);
    }

    private void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float maxSeconds = float.PositiveInfinity)
    {
        if (WorldSaveRuntime.IsSeeking || WorldSaveRuntime.IsRestoring) return;
        if (clip == null) return;
        var go = new GameObject("Gameplay sound: " + clip.name);
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = .8f;
        source.minDistance = 2f;
        source.maxDistance = 24f;
        source.outputAudioMixerGroup = effectsGroup;
        if (clip == ratHit) source.pitch = Random.Range(.85f, 1.15f);
        source.PlayOneShot(clip, volume);
        Destroy(go, Mathf.Min(clip.length / source.pitch, maxSeconds) + .2f);
    }

    private static AudioClip Choose(AudioClip[] clips) => clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;

    public void PestIdle(bool crow, Vector3 point) => PlayAt(crow ? crowsIdle : ratIdle, point, crow ? .35f : .28f);
    public void PestHit(bool crow, Vector3 point) => PlayAt(crow ? Choose(crowHits) : ratHit, point, .8f);
    public void CrowFlyAway(Vector3 point) => PlayAt(crowFlyAway, point, .65f / 1.5f);
    public void BroomHit() => Play(Choose(broomHits), .75f);
    public void SwitchInstruments() => Play(switchInstruments, .8f);
    public void ToolSwing() => Play(hitAir, .75f);
    public void CloseContainer() => Play(closeBag, .8f);
    public void Eat() => Play(eat);
    public void PressWeighButton() => Play(pressButton, .8f);
    // Cash_in is mastered very quietly (peak 0.066); compensate without clipping.
    public const float CashInVolume = 8f;
    public void CashIn() => Play(cashIn, CashInVolume);
    public void UpgradeResult(bool success)
    {
        Play(success ? upgradeSuccess : upgradeFailed, .8f);
    }
    public void BarrierHit(bool metal) => Play(metal ? hammerMetal : (Choose(axeWoodHits) ?? hammerWood), .8f);
    public void PickUp() => Play(pickUp, .7f);
    public void Drop() => Play(drop, .7f);
    public void OpenElectricalDoor() => Play(openDoor, .8f);
    public void OpenRocketDoor() => Play(openRocketDoor, .85f);
    public void CloseRocketDoor() => Play(closeDoor, .8f);
    public void SwitchElectrical() { Play(switchElectrical, .85f); Play(lightsOn, .7f); }
    public void PaperGet() => Play(paperGet, .8f);
    public void PerkFound() => Play(perkFound, .8f);
    public void ShovelGet() => Play(Choose(shovelGets), .75f);
    public void FillGrain() => Play(Choose(fillGrain), .65f);
    public void VacuumGrainIntake() => Play(Choose(fillGrain), .3f);
    public void VehicleHit(Vector3 point) => PlayAt(Choose(vehicleHits), point, .85f);
    public void VehicleResetComplete() => Play(resetVehicle, .8f);
    public void BicycleScratch(Vector3 point) => PlayAt(bicycleScratch, point, .25f, 1f);

    public void StartRocket() => RocketLaunchAudio.Begin(rocket, effectsGroup);

    private void SetLoop(Transform owner, AudioClip clip, bool moving, float pitch, float volume, float blend = .8f, bool engine = false)
    {
        if (owner == null || clip == null) return;
        if (!loops.TryGetValue(owner, out var source) || source == null)
        {
            if (!moving) return;
            source = owner.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = blend;
            source.minDistance = 2f;
            source.maxDistance = 30f;
            source.outputAudioMixerGroup = effectsGroup;
            source.pitch = pitch;
            source.volume = engine ? 0f : volume;
            if (engine) source.dopplerLevel = 0f;
            loops[owner] = source;
        }
        if (!moving) { if (source.isPlaying) source.Stop(); return; }
        if (source.clip != clip) { source.Stop(); source.clip = clip; }
        float response = engine ? 1f - Mathf.Exp(-3f * Time.deltaTime) : 1f;
        source.pitch = Mathf.Lerp(source.pitch, pitch, response);
        source.volume = Mathf.Lerp(source.volume, volume, response);
        if (!source.isPlaying) source.Play();
    }

    public void VacuumMoving(Transform tool, bool moving, bool lastVacuum) =>
        SetLoop(tool, vacuum, moving, lastVacuum ? 1.18f : .92f, .83f, 0f);
    public void WheelbarrowMoving(Transform barrow, bool moving) =>
        SetLoop(barrow, wheelbarrow, moving, 1f, .45f);

    public void VehicleMoving(BicycleController vehicle, bool moving)
    {
        bool motor = vehicle.motorDriven || vehicle.GetComponent<MotorollerController>() != null;
        bool pickup = vehicle.GetComponent<PickupController>() != null;
        AudioClip clip = pickup ? pickupMotor : motor ? motorollerAndTrike : bicycleMove;
        float pitch = pickup ? vehicle.name.Contains("Last") ? 1.1f : vehicle.name.Contains("Bigger") ? 1f : .9f :
            motor ? vehicle.GetComponent<TrikeController>() != null ? .88f : 1.08f : 1f;
        if(vehicle.TryGetComponent<ForkliftController>(out var forklift)) pitch *= forklift.EnginePitchMultiplier;
        if (motor || pickup)
        {
            // Wheel travel can be tiny for one frame at high frame rates or over bumps.
            // Keep the engine running at idle instead of replaying its startup on each frame.
            bool running = vehicle.isActiveAndEnabled && (vehicle.IsRidden || vehicle.IsCoasting) &&
                (!vehicle.TryGetComponent<VehicleReset>(out var reset) || !reset.IsResetting);
            float rpm = Mathf.Sqrt(Mathf.Clamp01(Mathf.Abs(vehicle.Speed) / Mathf.Max(1f, vehicle.maximumSpeed)));
            SetLoop(vehicle.transform, clip, running, pitch * Mathf.Lerp(.65f, 1.25f, rpm),
                Mathf.Lerp(.22f, .55f, rpm), .8f, true);
        }
        else SetLoop(vehicle.transform, clip, moving, pitch, .45f);
    }
}

/// <summary>Keeps the launch rumble audible across the loading transition until the ending finishes.</summary>
public sealed class RocketLaunchAudio : MonoBehaviour
{
    private static RocketLaunchAudio active;
    private AudioSource source;
    private RocketEndingCutscene ending;
    private bool enteredEnding;

    public static void Begin(AudioClip clip, AudioMixerGroup group)
    {
        if (clip == null || active != null) return;
        var go = new GameObject("Rocket launch audio");
        DontDestroyOnLoad(go);
        active = go.AddComponent<RocketLaunchAudio>();
        active.source = go.AddComponent<AudioSource>();
        active.source.playOnAwake = false;
        active.source.spatialBlend = 0f;
        active.source.outputAudioMixerGroup = group;
        active.source.clip = clip;
        active.source.loop = true;
        active.source.volume = .7f;
        active.source.Play();
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name == "Last Scene")
        {
            enteredEnding = true;
            if (ending == null) ending = FindFirstObjectByType<RocketEndingCutscene>();
            if (ending != null && ending.Completed) Destroy(gameObject);
        }
        else if (enteredEnding) Destroy(gameObject);
    }

    private void OnDestroy() { if (active == this) active = null; }
}
