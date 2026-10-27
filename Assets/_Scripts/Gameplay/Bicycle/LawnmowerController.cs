using UnityEngine;

/// <summary>Four-wheel visuals and a physics hitch for the mower's shared driving rig.</summary>
[RequireComponent(typeof(MotorollerController))]
public sealed class LawnmowerController : MonoBehaviour
{
    public Transform frontRightSteering, frontRightWheel, rearRightWheel, steeringWheel;
    public Rigidbody hitchBody;
    public Transform hitchTarget;
    public ConfigurableJoint trailerJoint;
    public WheelCollider[] trailerWheels;
    public Transform[] trailerWheelVisuals;
    BicycleController movement;
    Quaternion steeringRest;
    Vector3 previousPosition;
    float rearAngle;
    readonly RaycastHit[] trailerObstacles = new RaycastHit[24];

    void Awake()
    {
        movement = GetComponent<BicycleController>();
        steeringRest = steeringWheel.localRotation;
        previousPosition = transform.position;
        // The joint connects to a kinematic hitch proxy, so explicitly exclude the
        // mower's colliders from its own drawbar/trailer contacts as well.
        var mowerColliders = GetComponentsInChildren<Collider>();
        var trailerColliders = trailerJoint.GetComponentsInChildren<Collider>();
        foreach (var mowerCollider in mowerColliders)
            foreach (var trailerCollider in trailerColliders)
                Physics.IgnoreCollision(mowerCollider, trailerCollider);
    }
    void FixedUpdate() => UpdateHitch();
    public void UpdateHitch()
    {
        if (hitchBody == null || hitchTarget == null) return;
        hitchBody.MovePosition(hitchTarget.position);
        hitchBody.MoveRotation(hitchTarget.rotation);
    }
    void LateUpdate()
    {
        float distance = Vector3.Dot(transform.position - previousPosition, transform.forward);
        previousPosition = transform.position;
        rearAngle += distance / .305f * Mathf.Rad2Deg;
        frontRightSteering.localRotation = Quaternion.Euler(0, movement.CurrentSteeringAngle, 0);
        frontRightWheel.localRotation = movement.frontWheel.localRotation;
        movement.rearWheel.localRotation = rearRightWheel.localRotation = Quaternion.Euler(rearAngle, 0, 0);
        steeringWheel.localRotation = steeringRest * Quaternion.Euler(0, movement.CurrentSteeringAngle * 2.5f, 0);
        for (int i = 0; i < trailerWheels.Length; i++)
        {
            trailerWheels[i].GetWorldPose(out var position, out var rotation);
            trailerWheelVisuals[i].SetPositionAndRotation(position, rotation);
        }
    }
    public Vector3 ConstrainTowMotion(Vector3 motion)
    {
        // A trapped trailer must stop the kinematic tow vehicle instead of stretching the joint.
        if (trailerJoint == null || hitchTarget == null) return motion;
        Vector3 gap = hitchTarget.position - trailerJoint.transform.TransformPoint(trailerJoint.anchor);
        gap.y = 0;
        if (gap.magnitude > .25f && Vector3.Dot(motion, gap) > 0) return Vector3.zero;
        float distance = motion.magnitude;
        if (distance < .00001f) return motion;
        var trailer = trailerJoint.transform;
        int count = Physics.BoxCastNonAlloc(trailer.TransformPoint(new Vector3(0, .95f, -2.9f)),
            new Vector3(1.23f, .34f, 1.62f), motion / distance, trailerObstacles,
            trailer.rotation, distance + .08f, ~0, QueryTriggerInteraction.Ignore);
        if (count == trailerObstacles.Length) return Vector3.zero;
        float allowed = distance;
        for (int i = 0; i < count; i++)
        {
            var hit = trailerObstacles[i];
            if (hit.transform.IsChildOf(trailer) || hit.transform.IsChildOf(transform) ||
                hit.normal.y > .65f || (hit.collider is TerrainCollider && hit.distance <= 0)) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0, hit.distance - .08f));
        }
        return motion * (allowed / distance);
    }
}
