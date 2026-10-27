using UnityEngine;

/// <summary>Arcade bicycle movement with collision, slope support and separately animated parts.</summary>
[RequireComponent(typeof(CharacterController)), DisallowMultipleComponent]
public sealed class BicycleController : MonoBehaviour
{
    public Transform steering, frontWheel, rearWheel, crank, leftPedal, rightPedal, riderView;
    public Transform[] cargoSlots;
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
        if (motorDriven) return;
        crankRest = crank.localRotation;
        leftOffset = leftPedal.position - crank.position; rightOffset = rightPedal.position - crank.position;
        leftOffset = transform.InverseTransformVector(leftOffset); rightOffset = transform.InverseTransformVector(rightOffset);
        leftRest = leftPedal.localRotation; rightRest = rightPedal.localRotation;
    }
    void Awake() { Initialize(); LoadRidingCamera(); }
    Collider[] cargoColliders;
    bool[] cargoColliderStates;
    public void SetRidden(bool value)
    {
        Initialize(); if (IsRidden == value) return;
        IsRidden = value; Speed = 0; fallSpeed = 0; steeringVelocity = 0; PedalingCameraPitch = 0;
        if(value)
        {
            cargoColliders = GetComponentsInChildren<Collider>(); cargoColliderStates = new bool[cargoColliders.Length];
            for(int i=0;i<cargoColliders.Length;i++){cargoColliderStates[i]=cargoColliders[i].enabled;if(cargoColliders[i]!=motor)cargoColliders[i].enabled=false;}
        }
        else if(cargoColliders!=null) for(int i=0;i<cargoColliders.Length;i++)if(cargoColliders[i]!=null && cargoColliders[i]!=motor)cargoColliders[i].enabled=cargoColliderStates[i];
    }
    public void Drive(float throttle, float turn, bool brake, float dt, bool boost = false)
    {
        if (!IsRidden || dt <= 0 || !float.IsFinite(dt)) return;
        float target = Mathf.Clamp(throttle, -.3f, 1f) * maximumSpeed * (boost && throttle > 0 && !brake ? Mathf.Max(1, boostSpeedMultiplier) : 1);
        Speed = Mathf.MoveTowards(Speed, brake ? 0 : target, (brake ? brakingDeceleration : Mathf.Abs(throttle) < .01f ? 1.8f : acceleration) * dt);
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
        steering.localRotation = steerRest * Quaternion.Euler(0, steer, 0);
        float travelled = Vector3.Dot(transform.position-before, transform.forward);
        wheelAngle += travelled / Mathf.Max(.05f,wheelRadius * transform.lossyScale.y) * Mathf.Rad2Deg;
        // The front wheel is carried by the shared headset pivot; only spin it at its axle.
        frontWheel.localRotation = frontRest * Quaternion.Euler(wheelAngle,0,0);
        rearWheel.localRotation = rearRest * Quaternion.Euler(wheelAngle,0,0);
        if (motorDriven) { PedalingCameraPitch = 0; return; }
        if (Mathf.Abs(throttle) > .01f && !brake) crankAngle += Speed * 100 * dt;
        // Use the actual crank phase, so the rider motion follows the animated feet.
        bool pedaling = Mathf.Abs(throttle) > .01f && !brake && Mathf.Abs(Speed) > .1f;
        float pitchTarget = pedaling ? Mathf.Sin(crankAngle * Mathf.Deg2Rad) * pedalingPitchAmplitude *
            Mathf.Clamp01(Mathf.Abs(Speed) / Mathf.Max(.1f, maximumSpeed)) : 0f;
        PedalingCameraPitch = Mathf.Lerp(PedalingCameraPitch, pitchTarget,
            1f - Mathf.Exp(-dt / Mathf.Max(.01f, pedalingPitchSmoothTime)));
        Quaternion rotation = Quaternion.Euler(crankAngle,0,0);
        crank.localRotation = crankRest * rotation;
        leftPedal.position = crank.position + transform.TransformVector(rotation * leftOffset);
        rightPedal.position = crank.position + transform.TransformVector(rotation * rightOffset);
        leftPedal.localRotation = leftRest; rightPedal.localRotation = rightRest;
    }
    public Transform FreeCargoSlot()
    {
        foreach (var slot in cargoSlots) if (slot != null && slot.childCount == 0) return slot;
        return null;
    }
    public bool HasCargo(Transform item)
    {
        foreach(var slot in cargoSlots) if(item.IsChildOf(slot)) return true;
        return false;
    }
}
