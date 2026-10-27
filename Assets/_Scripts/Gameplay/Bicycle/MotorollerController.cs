using UnityEngine;

/// <summary>Motor drive settings on the shared upright two-wheel movement rig.</summary>
[RequireComponent(typeof(BicycleController)), DisallowMultipleComponent]
public sealed class MotorollerController : MonoBehaviour
{
    [SerializeField, Min(1)] private float maximumSpeed = 12f;
    [SerializeField, Min(.1f)] private float acceleration = 4f;
    [SerializeField, Min(.1f)] private float brakingDeceleration = 10f;
    [SerializeField, Min(.05f)] private float wheelRadius = .2471f;
    [SerializeField, Min(.2f)] private float wheelbase = 1.29826f;
    private BicycleController movement;
    private readonly RaycastHit[] obstacles = new RaycastHit[24];
    public BicycleController Movement => movement != null ? movement : movement = GetComponent<BicycleController>();
    public float Speed => Movement.Speed;
    public bool IsRidden => Movement.IsRidden;
    private void Awake() => Configure();
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
        float distance = motion.magnitude;
        if (distance < .00001f) return motion;
        // A vertical CharacterController alone misses the scooter's long nose and tail.
        Vector3 scale = transform.lossyScale;
        Vector3 extents = Vector3.Scale(new Vector3(.29f,.43f,.96f), scale);
        int count = Physics.BoxCastNonAlloc(transform.TransformPoint(new Vector3(0,.65f,0)), extents,
            motion / distance, obstacles, transform.rotation, distance + .025f, ~0, QueryTriggerInteraction.Ignore);
        float allowed = distance;
        if (count == obstacles.Length) return Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            var hit = obstacles[i];
            if (hit.transform.IsChildOf(transform) || hit.normal.y > .65f) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0,hit.distance-.025f));
        }
        return motion * (allowed / distance);
    }
}
