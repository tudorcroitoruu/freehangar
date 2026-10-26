using UnityEngine;

/// <summary>Arcade bicycle movement with collision, slope support and separately animated parts.</summary>
[RequireComponent(typeof(CharacterController)), DisallowMultipleComponent]
public sealed class BicycleController : MonoBehaviour
{
    public Transform steering, frontWheel, rearWheel, crank, leftPedal, rightPedal, riderView;
    public Transform[] cargoSlots;
    [Min(1)] public float maximumSpeed = 7f;
    [Min(0)] public float acceleration = 2.5f;
    [Min(1)] public float boostSpeedMultiplier = 1.5f;
    [Header("Steering")]
    [Range(.02f, .5f)] public float steeringSmoothTime = .14f;
    [Range(5f, 45f)] public float maximumSteeringAngle = 30f;
    [Header("Riding camera (relative to rider view)")]
    [SerializeField] private Vector3 ridingCameraOffset;
    [SerializeField, Range(-35, 35)] private float ridingCameraPitch;
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
    float fallSpeed, wheelAngle, crankAngle;
    Quaternion steerRest, frontRest, rearRest, crankRest;
    Vector3 leftOffset, rightOffset;
    Quaternion leftRest, rightRest;
    public void Initialize()
    {
        if (motor != null) return;
        motor = GetComponent<CharacterController>();
        steerRest = steering.localRotation; frontRest = frontWheel.localRotation;
        rearRest = rearWheel.localRotation; crankRest = crank.localRotation;
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
        IsRidden = value; Speed = 0; fallSpeed = 0; steeringVelocity = 0;
        if(value)
        {
            cargoColliders = GetComponentsInChildren<Collider>(); cargoColliderStates = new bool[cargoColliders.Length];
            for(int i=0;i<cargoColliders.Length;i++){cargoColliderStates[i]=cargoColliders[i].enabled;if(cargoColliders[i]!=motor)cargoColliders[i].enabled=false;}
        }
        else if(cargoColliders!=null) for(int i=0;i<cargoColliders.Length;i++)if(cargoColliders[i]!=null && cargoColliders[i]!=motor)cargoColliders[i].enabled=cargoColliderStates[i];
    }
    public void Drive(float throttle, float turn, bool brake, float dt, bool boost = false)
    {
        if (!IsRidden || dt <= 0) return;
        float target = Mathf.Clamp(throttle, -.3f, 1f) * maximumSpeed * (boost && throttle > 0 && !brake ? Mathf.Max(1, boostSpeedMultiplier) : 1);
        Speed = Mathf.MoveTowards(Speed, brake ? 0 : target, (brake ? 12f : Mathf.Abs(throttle) < .01f ? 1.8f : acceleration) * dt);
        float targetSteering = Mathf.Clamp(turn, -1, 1) * maximumSteeringAngle;
        currentSteering = Mathf.SmoothDampAngle(currentSteering, targetSteering, ref steeringVelocity,
            Mathf.Max(.02f, steeringSmoothTime), Mathf.Infinity, dt);
        float steer = currentSteering;
        transform.Rotate(0, Mathf.Tan(steer * Mathf.Deg2Rad) * Speed / 1.117f * Mathf.Rad2Deg * dt, 0);
        if (motor.isGrounded && fallSpeed < 0) fallSpeed = -2;
        fallSpeed -= 9.81f * dt;
        Vector3 before = transform.position;
        CollisionFlags hit = motor.Move((transform.forward * Speed + Vector3.up * fallSpeed) * dt);
        if ((hit & CollisionFlags.Sides) != 0 && Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude < Mathf.Abs(Speed)*dt*.3f) Speed = 0;
        if ((hit & CollisionFlags.Above) != 0) fallSpeed = 0;
        steering.localRotation = steerRest * Quaternion.Euler(0, steer, 0);
        wheelAngle += Speed / .34f * Mathf.Rad2Deg * dt;
        frontWheel.localRotation = Quaternion.Euler(0,steer,0) * frontRest * Quaternion.Euler(wheelAngle,0,0);
        rearWheel.localRotation = rearRest * Quaternion.Euler(wheelAngle,0,0);
        if (Mathf.Abs(throttle) > .01f && !brake) crankAngle += Speed * 100 * dt;
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
