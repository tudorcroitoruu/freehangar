using UnityEngine;

/// <summary>Four-wheel and steering-wheel animation on the shared motor vehicle rig.</summary>
[RequireComponent(typeof(MotorollerController))]
public sealed class PickupController : MonoBehaviour
{
    public Transform frontRightSteering, frontRightWheel, rearRightWheel, steeringWheel;
    [Tooltip("Subtle road vibration. Set to zero to disable it.")]
    [Range(0, 1)] public float roadCameraIntensity = .65f;
    [Tooltip("Maximum camera roll around Z from road bumps, in degrees, before intensity scaling.")]
    [Range(0, .75f)] public float roadCameraRollDegrees = .45f;
    public Vector3 RoadCameraOffset { get; private set; }
    public Vector3 RoadCameraAngles { get; private set; }
    BicycleController movement;
    Quaternion steeringRest;
    Vector3 previousRoadPosition;
    float roadPhase, previousVerticalSpeed, bumpStrength;
    void Awake()
    {
        movement = GetComponent<BicycleController>();
        steeringRest = steeringWheel.localRotation;
        ResetRoadCamera();
    }
    public void ResetRoadCamera()
    {
        previousRoadPosition = transform.position;
        roadPhase = previousVerticalSpeed = bumpStrength = 0;
        RoadCameraOffset = RoadCameraAngles = Vector3.zero;
    }
    public void UpdateRoadCamera(float dt)
    {
        if (dt <= 0 || !float.IsFinite(dt)) return;
        var delta = transform.position - previousRoadPosition;
        previousRoadPosition = transform.position;
        if (!movement.IsRidden || roadCameraIntensity <= 0 || delta.magnitude > movement.maximumSpeed * dt * 2 + .5f)
        {
            ResetRoadCamera();
            return;
        }
        float distance = new Vector2(delta.x, delta.z).magnitude;
        float speedWeight = Mathf.Clamp01(distance / dt / Mathf.Max(1, movement.maximumSpeed));
        float verticalSpeed = delta.y / dt;
        float bump = Mathf.Clamp01(Mathf.Abs(verticalSpeed - previousVerticalSpeed) / dt / 6f);
        previousVerticalSpeed = verticalSpeed;
        float blend = 1 - Mathf.Exp(-dt / .1f);
        bumpStrength = Mathf.Lerp(bumpStrength, bump, blend);
        roadPhase = Mathf.Repeat(roadPhase + distance * 1.8f, 4096);
        // Distance-based, smoothed noise avoids random per-frame jitter. Ground-height
        // changes add a capped bump response; steady grades do not keep amplifying it.
        float vertical = Mathf.PerlinNoise(roadPhase, 13.7f) * 2 - 1;
        float lateral = Mathf.PerlinNoise(roadPhase, 47.1f) * 2 - 1;
        float amount = Mathf.Clamp01(roadCameraIntensity) * speedWeight * (.25f + .75f * bumpStrength);
        var offset = new Vector3(lateral * .002f, vertical * .006f, 0) * amount;
        float rocking = Mathf.PerlinNoise(roadPhase * .55f, 81.3f) * 2 - 1;
        float roll = rocking * Mathf.Clamp(roadCameraRollDegrees, 0, .75f) *
            Mathf.Clamp01(roadCameraIntensity) * speedWeight * (.08f + .92f * bumpStrength);
        var angles = new Vector3(vertical * .12f * amount, 0, roll);
        RoadCameraOffset = Vector3.Lerp(RoadCameraOffset, offset, blend);
        RoadCameraAngles = new Vector3(Mathf.Lerp(RoadCameraAngles.x, angles.x, blend), 0,
            Mathf.Lerp(RoadCameraAngles.z, angles.z, 1 - Mathf.Exp(-dt / .18f)));
    }
    void LateUpdate() => RefreshWheels();
    public void RefreshWheels()
    {
        frontRightSteering.localRotation = Quaternion.Euler(0, movement.CurrentSteeringAngle, 0);
        frontRightWheel.localRotation = movement.frontWheel.localRotation;
        rearRightWheel.localRotation = movement.rearWheel.localRotation;
        // These imported wheels lie in local YZ; local X follows the steering column.
        steeringWheel.localRotation = steeringRest * Quaternion.AngleAxis(-movement.CurrentSteeringAngle * 2.5f, Vector3.right);
    }
}
