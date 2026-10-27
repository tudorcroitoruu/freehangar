using UnityEngine;

/// <summary>Motor drive settings and body clearance on the shared upright vehicle rig.</summary>
[RequireComponent(typeof(BicycleController)), DisallowMultipleComponent]
public sealed class MotorollerController : MonoBehaviour
{
    [SerializeField, Min(1)] private float maximumSpeed = 12f;
    [SerializeField, Min(.1f)] private float acceleration = 4f;
    [SerializeField, Min(.1f)] private float brakingDeceleration = 10f;
    [SerializeField, Min(.05f)] private float wheelRadius = .2471f;
    [SerializeField, Min(.2f)] private float wheelbase = 1.29826f;
    [SerializeField] private string vehicleName = "motoroller";
    [SerializeField] private Vector3 collisionCenter = new Vector3(0,.65f,0);
    [SerializeField] private Vector3 collisionHalfExtents = new Vector3(.29f,.43f,.96f);
    public string VehicleName => vehicleName;
    private BicycleController movement;
    private LawnmowerController lawnmower;
    private VehicleGroundAlignment groundAlignment;
    private readonly RaycastHit[] obstacles = new RaycastHit[24];
    public BicycleController Movement => movement != null ? movement : movement = GetComponent<BicycleController>();
    public float Speed => Movement.Speed;
    public bool IsRidden => Movement.IsRidden;
    private void Awake() { Configure(); lawnmower = GetComponent<LawnmowerController>(); }
    private void OnValidate() => Configure();
    public void Configure()
    {
        var rig = Movement;
        rig.motorDriven = true;
        rig.maximumSpeed = maximumSpeed;
        rig.acceleration = acceleration;
        rig.brakingDeceleration = brakingDeceleration;
        rig.wheelRadius = wheelRadius;
        rig.wheelbase = wheelbase;
        rig.boostSpeedMultiplier = 1;
    }
    public void Drive(float throttle, float steering, bool brake, float dt)
    {
        // Engine power never consumes pedaling stamina. Reverse is capped by the shared motor.
        Movement.Drive(throttle, steering, brake, dt);
    }
    public Vector3 ConstrainMotion(Vector3 motion)
    {
        if (lawnmower != null) motion = lawnmower.ConstrainTowMotion(motion);
        float distance = motion.magnitude;
        if (distance < .00001f) return motion;
        // A vertical CharacterController alone misses the scooter's long nose and tail.
        Vector3 scale = transform.lossyScale;
        Vector3 extents = Vector3.Scale(collisionHalfExtents, scale);
        if (groundAlignment == null) groundAlignment = GetComponent<VehicleGroundAlignment>();
        Transform bodyPose = groundAlignment != null && groundAlignment.Pose != null ? groundAlignment.Pose : transform;
        int count = Physics.BoxCastNonAlloc(bodyPose.TransformPoint(collisionCenter), extents,
            motion / distance, obstacles, bodyPose.rotation, distance + .025f, ~0, QueryTriggerInteraction.Ignore);
        float allowed = distance;
        if (count == obstacles.Length) return Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            var hit = obstacles[i];
            if (hit.transform.IsChildOf(transform) || hit.normal.y > .65f) continue;
            if (lawnmower != null && lawnmower.trailerJoint != null &&
                hit.transform.IsChildOf(lawnmower.trailerJoint.transform)) continue;
            // The upright body box can already overlap rising terrain at its nose.
            // Such sweeps report distance zero and a synthetic normal opposite travel,
            // not a wall normal. Let the CharacterController resolve ground and slopes.
            if (hit.collider is TerrainCollider && hit.distance <= 0f) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0,hit.distance-.025f));
        }
        return motion * (allowed / distance);
    }
}
