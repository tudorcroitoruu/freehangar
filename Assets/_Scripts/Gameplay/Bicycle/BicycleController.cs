using UnityEngine;

/// <summary>Arcade bicycle movement with collision, slope support and separately animated parts.</summary>
[RequireComponent(typeof(CharacterController)), DisallowMultipleComponent]
public sealed class BicycleController : MonoBehaviour, IInteractable
{
    public Transform steering, frontWheel, rearWheel, crank, leftPedal, rightPedal, riderView;
    public Transform[] cargoSlots;
    [Tooltip("Optional single bag position sharing the entire box rack.")]
    public Transform exclusiveBagSlot;
    public string cargoLocationName = "rear rack";
    [Min(.5f)] public float dismountSideDistance = 1.1f;
    [Min(.5f)] public float dismountLongitudinalDistance = 1.5f;
    [Min(1)] public float maximumSpeed = 7f;
    [Min(0)] public float acceleration = 2.5f;
    [Min(.05f)] public float wheelRadius = .34f;
    [Min(.2f)] public float wheelbase = 1.117f;
    [Min(.1f)] public float brakingDeceleration = 12f;
    [Tooltip("Disable crank and pedaling camera motion for motor-driven vehicles.")]
    public bool motorDriven;
    [Tooltip("Maximum time a motor vehicle coasts after the driver exits. Zero stops immediately.")]
    [Min(0)] public float exitCoastDuration = .8f;
    [Min(1)] public float boostSpeedMultiplier = 1.5f;
    [Header("Steering")]
    [Range(.02f, .5f)] public float steeringSmoothTime = .14f;
    [Range(5f, 45f)] public float maximumSteeringAngle = 30f;
    [Header("Riding camera (relative to rider view)")]
    [SerializeField] private Vector3 ridingCameraOffset;
    [SerializeField, Range(-35, 35)] private float ridingCameraPitch;
    [Header("Pedaling camera motion")]
    [Tooltip("Subtle pitch in degrees, synchronized with the crank. Set to zero to disable.")]
    [SerializeField, Range(0f, 2f)] private float pedalingPitchAmplitude = .3f;
    [SerializeField, Min(.01f)] private float pedalingPitchSmoothTime = .09f;
    public float PedalingCameraPitch { get; private set; }
    public Vector3 RidingCameraOffset => ridingCameraOffset;
    public float RidingCameraPitch => ridingCameraPitch;
    public float CurrentSteeringAngle => currentSteering;
    float currentSteering, steeringVelocity;
    string CameraSettingsKey => "Bicycle.View." + gameObject.scene.name + "." + gameObject.name;
    [System.Serializable] sealed class ViewSettings { public Vector3 offset; public float pitch; }
    public void SetRidingCamera(Vector3 offset, float pitch)
    {
        ridingCameraOffset = new Vector3(Mathf.Clamp(offset.x, -.5f, .5f), Mathf.Clamp(offset.y, -.5f, .7f), Mathf.Clamp(offset.z, -.6f, .6f));
        ridingCameraPitch = Mathf.Clamp(pitch, -35, 35);
    }
    public void SaveRidingCamera()
    {
        PlayerPrefs.SetString(CameraSettingsKey, JsonUtility.ToJson(new ViewSettings { offset = ridingCameraOffset, pitch = ridingCameraPitch }));
        PlayerPrefs.Save();
    }
    public void ClearSavedRidingCamera() { PlayerPrefs.DeleteKey(CameraSettingsKey); PlayerPrefs.Save(); }
    void LoadRidingCamera()
    {
        if (!PlayerPrefs.HasKey(CameraSettingsKey)) return;
        try { var settings = JsonUtility.FromJson<ViewSettings>(PlayerPrefs.GetString(CameraSettingsKey)); if (settings != null) SetRidingCamera(settings.offset, settings.pitch); }
        catch (System.ArgumentException) { /* Ignore a malformed local preference and retain the authored view. */ }
    }
    public bool IsRidden { get; private set; }
    public float Speed { get; private set; }
    CharacterController motor;
    MotorollerController motoroller;
    VehicleGroundAlignment groundAlignment;
    float fallSpeed, wheelAngle, crankAngle;
    Quaternion steerRest, frontRest, rearRest, crankRest;
    Vector3 leftOffset, rightOffset;
    Quaternion leftRest, rightRest;
    public void Initialize()
    {
        if (motor != null) return;
        motor = GetComponent<CharacterController>();
        motoroller = GetComponent<MotorollerController>();
        steerRest = steering.localRotation; frontRest = frontWheel.localRotation;
        rearRest = rearWheel.localRotation;
        if (!motorDriven)
        {
        crankRest = crank.localRotation;
        leftOffset = leftPedal.position - crank.position; rightOffset = rightPedal.position - crank.position;
        leftOffset = transform.InverseTransformVector(leftOffset); rightOffset = transform.InverseTransformVector(rightOffset);
        leftRest = leftPedal.localRotation; rightRest = rightPedal.localRotation;
        }
        groundAlignment = GetComponent<VehicleGroundAlignment>() ?? gameObject.AddComponent<VehicleGroundAlignment>();
        groundAlignment.Initialize(this);
    }
    void Awake()
    {
        Initialize(); LoadRidingCamera();
        if (GetComponent<VehicleUpgrade>() == null) gameObject.AddComponent<VehicleUpgrade>();
    }
    Collider[] cargoColliders;
    bool[] cargoColliderStates;
    bool coasting;
    float coastDeceleration;
    bool pedalingInput;
    public bool IsCoasting => coasting;
    void Update()
    {
        TickUnoccupied(Time.deltaTime);
        if (!IsRidden && !coasting && groundAlignment != null) groundAlignment.Align(Time.deltaTime);
    }
    public void TickUnoccupied(float dt)
    {
        if (!coasting || IsRidden || dt <= 0 || !float.IsFinite(dt)) return;
        Speed = Mathf.MoveTowards(Speed, 0, coastDeceleration * dt);
        StepMotion(0, dt);
        if (Mathf.Abs(Speed) < .01f) StopCoasting();
    }
    void RestoreCargoColliders()
    {
        if (cargoColliders == null) return;
        for (int i = 0; i < cargoColliders.Length; i++)
            if (cargoColliders[i] != null && cargoColliders[i] != motor)
                cargoColliders[i].enabled = cargoColliderStates[i];
        cargoColliders = null;
        cargoColliderStates = null;
    }
    void StopCoasting() { coasting = false; Speed = 0; RestoreCargoColliders(); }
    void OnDisable() { if (coasting) StopCoasting(); }
    public void SetRidden(bool value)
    {
        Initialize(); if (IsRidden == value) return;
        bool wasCoasting = coasting;
        IsRidden = value; steeringVelocity = 0; PedalingCameraPitch = 0;
        if(value)
        {
            coasting = false;
            // Re-entering a rolling vehicle keeps its momentum and original collider states.
            if (wasCoasting) return;
            Speed = 0; fallSpeed = 0;
            cargoColliders = GetComponentsInChildren<Collider>(); cargoColliderStates = new bool[cargoColliders.Length];
            for(int i=0;i<cargoColliders.Length;i++){cargoColliderStates[i]=cargoColliders[i].enabled;if(cargoColliders[i]!=motor)cargoColliders[i].enabled=false;}
        }
        else if (motorDriven && exitCoastDuration > 0 && Mathf.Abs(Speed) > .1f)
        {
            coasting = true;
            coastDeceleration = Mathf.Max(brakingDeceleration, Mathf.Abs(Speed) / exitCoastDuration);
        }
        else StopCoasting();
    }
    public void Drive(float throttle, float turn, bool brake, float dt, bool boost = false)
    {
        if (!IsRidden || dt <= 0 || !float.IsFinite(dt)) return;
        pedalingInput = Mathf.Abs(throttle) > .01f && !brake;
        float target = Mathf.Clamp(throttle, -.3f, 1f) * maximumSpeed * (boost && throttle > 0 && !brake ? Mathf.Max(1, boostSpeedMultiplier) : 1);
        Speed = Mathf.MoveTowards(Speed, brake ? 0 : target, (brake ? brakingDeceleration : Mathf.Abs(throttle) < .01f ? 1.8f : acceleration) * dt);
        StepMotion(turn, dt);
    }
    void StepMotion(float turn, float dt)
    {
        float targetSteering = Mathf.Clamp(turn, -1, 1) * maximumSteeringAngle;
        currentSteering = Mathf.SmoothDampAngle(currentSteering, targetSteering, ref steeringVelocity,
            Mathf.Max(.02f, steeringSmoothTime), Mathf.Infinity, dt);
        float steer = currentSteering;
        transform.Rotate(0, Mathf.Tan(steer * Mathf.Deg2Rad) * Speed / Mathf.Max(.2f,wheelbase * transform.lossyScale.z) * Mathf.Rad2Deg * dt, 0);
        if (motor.isGrounded && fallSpeed < 0) fallSpeed = -2;
        fallSpeed -= 9.81f * dt;
        Vector3 before = transform.position;
        Vector3 motion = transform.forward * Speed * dt;
        if (motoroller != null) motion = motoroller.ConstrainMotion(motion);
        CollisionFlags hit = motor.Move(motion + Vector3.up * fallSpeed * dt);
        if (motoroller != null && motion.sqrMagnitude < .000001f && Mathf.Abs(Speed) > .1f) Speed = 0;
        if ((hit & CollisionFlags.Sides) != 0 && Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude < Mathf.Abs(Speed)*dt*.3f) Speed = 0;
        if ((hit & CollisionFlags.Above) != 0) fallSpeed = 0;
        groundAlignment.Align(dt);
        steering.localRotation = steerRest * Quaternion.Euler(0, steer, 0);
        float travelled = Vector3.Dot(transform.position-before, transform.forward);
        wheelAngle += travelled / Mathf.Max(.05f,wheelRadius * transform.lossyScale.y) * Mathf.Rad2Deg;
        // The front wheel is carried by the shared headset pivot; only spin it at its axle.
        frontWheel.localRotation = frontRest * Quaternion.Euler(wheelAngle,0,0);
        rearWheel.localRotation = rearRest * Quaternion.Euler(wheelAngle,0,0);
        if (motorDriven) { PedalingCameraPitch = 0; return; }
        if (pedalingInput) crankAngle += Speed * 100 * dt;
        // Use the actual crank phase, so the rider motion follows the animated feet.
        bool pedaling = pedalingInput && Mathf.Abs(Speed) > .1f;
        float pitchTarget = pedaling ? Mathf.Sin(crankAngle * Mathf.Deg2Rad) * pedalingPitchAmplitude *
            Mathf.Clamp01(Mathf.Abs(Speed) / Mathf.Max(.1f, maximumSpeed)) : 0f;
        PedalingCameraPitch = Mathf.Lerp(PedalingCameraPitch, pitchTarget,
            1f - Mathf.Exp(-dt / Mathf.Max(.01f, pedalingPitchSmoothTime)));
        Quaternion rotation = Quaternion.Euler(crankAngle,0,0);
        crank.localRotation = crankRest * rotation;
        leftPedal.position = crank.position + groundAlignment.Pose.TransformVector(rotation * leftOffset);
        rightPedal.position = crank.position + groundAlignment.Pose.TransformVector(rotation * rightOffset);
        leftPedal.localRotation = leftRest; rightPedal.localRotation = rightRest;
    }
    public Transform FreeCargoSlot()
    {
        var upgrades = GetComponent<VehicleUpgrade>();
        bool configured = upgrades != null && upgrades.IsConfigured;
        if (configured && upgrades.CargoCount >= upgrades.SlotCount) return null;
        if (!configured && exclusiveBagSlot != null && exclusiveBagSlot.childCount > 0) return null;
        if (cargoSlots == null) return null;
        int limit = configured ? Mathf.Min(cargoSlots.Length, upgrades.SlotCount) : cargoSlots.Length;
        for (int i = 0; i < limit; i++)
            if (cargoSlots[i] != null && cargoSlots[i].childCount == 0) return cargoSlots[i];
        return null;
    }
    public Transform FreeBagCargoSlot()
    {
        var upgrades = GetComponent<VehicleUpgrade>();
        if (upgrades != null && upgrades.IsConfigured)
        {
            if (upgrades.CargoCount >= upgrades.SlotCount) return null;
            if (upgrades.CargoCount == 0 && exclusiveBagSlot != null && exclusiveBagSlot.childCount == 0) return exclusiveBagSlot;
            return FreeCargoSlot();
        }
        if (exclusiveBagSlot == null) return FreeCargoSlot();
        if (exclusiveBagSlot.childCount > 0) return null;
        foreach (var slot in cargoSlots) if (slot != null && slot.childCount > 0) return null;
        return exclusiveBagSlot;
    }
    public bool HasCargo(Transform item)
    {
        if (exclusiveBagSlot != null && item.IsChildOf(exclusiveBagSlot)) return true;
        if (cargoSlots != null) foreach(var slot in cargoSlots) if(slot != null && item.IsChildOf(slot)) return true;
        return false;
    }

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        if (!isActiveAndEnabled || IsRidden || interactor == null) return false;
        var rider = interactor.GetComponent<PlayerBicycleInteraction>();
        var actor = interactor.GetComponent<PlayerController>();
        if (rider == null || rider.IsRiding || actor == null || actor.characterCamera == null ||
            Vector3.Distance(actor.transform.position, transform.position) > rider.interactionDistance) return false;
        if (interactor.Bags != null && interactor.Bags.IsCarrying)
        {
            var upgrades = GetComponent<VehicleUpgrade>();
            bool available = FreeBagCargoSlot() != null && (upgrades == null || upgrades.CanLoadVolume(interactor.Bags.CarriedVolume));
            prompt = new InteractionPromptData(transform.position + Vector3.up, available ? "Load bag" : "Vehicle full", available);
            return true;
        }
        if (actor.IsCarrying) return false;
        var engine = GetComponent<MotorollerController>();
        prompt = new InteractionPromptData(transform.position + Vector3.up, engine != null ? "Drive " + engine.VehicleName : "Ride bicycle");
        return true;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!TryGetPrompt(interactor, out var prompt) || !prompt.ShowKey) return false;
        var rider = interactor.GetComponent<PlayerBicycleInteraction>();
        rider.bicycle = this;
        return interactor.Bags != null && interactor.Bags.IsCarrying ? rider.TryLoadBag() : rider.TryMount();
    }
}
