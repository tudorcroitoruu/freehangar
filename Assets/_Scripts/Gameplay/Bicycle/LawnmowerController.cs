using UnityEngine;
using System.Collections.Generic;

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
    readonly List<Collider> trailerContacts = new List<Collider>();
    readonly List<Collider> mowerContacts = new List<Collider>();
    readonly List<(Collider cargo, Collider hull)> ignoredCargoContacts = new List<(Collider, Collider)>();

    // Stowed bags retain separate kinematic bodies for interaction. They must not
    // act as external obstacles to the dynamic trailer that carries them.
    public void RefreshTrailerCollisions()
    {
        if (trailerJoint == null) return;
        var body = trailerJoint.GetComponent<Rigidbody>();
        trailerJoint.GetComponentsInChildren(true, trailerContacts);
        GetComponentsInChildren(true, mowerContacts);
        for (int i = ignoredCargoContacts.Count - 1; i >= 0; i--)
        {
            var pair = ignoredCargoContacts[i];
            if (pair.cargo != null && pair.hull != null &&
                pair.cargo.transform.IsChildOf(trailerJoint.transform)) continue;
            if (pair.cargo != null && pair.hull != null) Physics.IgnoreCollision(pair.cargo, pair.hull, false);
            ignoredCargoContacts.RemoveAt(i);
        }
        foreach (var hull in trailerContacts)
        {
            if (hull.attachedRigidbody != body) continue;
            foreach (var mower in mowerContacts)
                if (mower.enabled && hull.enabled && !Physics.GetIgnoreCollision(mower, hull))
                    Physics.IgnoreCollision(mower, hull);
            foreach (var cargo in trailerContacts)
            {
                if (cargo.attachedRigidbody == body || !cargo.enabled || !hull.enabled ||
                    Physics.GetIgnoreCollision(cargo, hull)) continue;
                Physics.IgnoreCollision(cargo, hull);
                var pair = (cargo, hull);
                if (!ignoredCargoContacts.Contains(pair)) ignoredCargoContacts.Add(pair);
            }
        }
    }

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
        ReconnectTrailer();
    }

    // Teleports/restored saves must move the whole rig before the next physics step.
    // The jointed trailer and hitch proxy are siblings, not children of the mower.
    public void ReconnectTrailer()
    {
        if (hitchBody == null || hitchTarget == null || trailerJoint == null) return;
        var trailerBody = trailerJoint.GetComponent<Rigidbody>();
        if (trailerBody == null) return;
        // Inactive bodies can still report their old physics pose after assignment.
        // Use the requested pose for both the Transform and the anchor calculation.
        Vector3 hitchPosition = hitchTarget.position;
        Quaternion hitchRotation = hitchTarget.rotation;
        hitchBody.position = hitchPosition;
        hitchBody.rotation = hitchRotation;
        hitchBody.transform.SetPositionAndRotation(hitchPosition, hitchRotation);
        trailerJoint.connectedBody = hitchBody;
        Quaternion rotation = transform.rotation;
        Vector3 anchor = hitchPosition + hitchRotation *
            Vector3.Scale(trailerJoint.connectedAnchor, hitchBody.transform.lossyScale);
        Vector3 position = anchor - rotation *
            Vector3.Scale(trailerJoint.anchor, trailerJoint.transform.lossyScale);
        trailerBody.position = position;
        trailerBody.rotation = rotation;
        trailerBody.transform.SetPositionAndRotation(position, rotation);
        if (!trailerBody.isKinematic)
        {
            trailerBody.linearVelocity = Vector3.zero;
            trailerBody.angularVelocity = Vector3.zero;
            trailerBody.WakeUp();
        }
        previousPosition = transform.position;
        RefreshTrailerCollisions();
        UpdateTrailerParking();
    }
    void UpdateTrailerParking()
    {
        if (trailerJoint == null || movement == null) return;
        var body = trailerJoint.GetComponent<Rigidbody>();
        if (body == null) return;
        bool parked = !movement.IsRidden && !movement.IsCoasting;
        if (body.isKinematic == parked) return;
        if (parked)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        // A joint and wheel friction are not a parking brake. Lock the trailer
        // while unattended so cargo contacts and slopes cannot make it creep.
        body.isKinematic = parked;
        if (!parked) body.WakeUp();
    }
    void FixedUpdate() => UpdateHitch();
    public void UpdateHitch()
    {
        if (hitchBody == null || hitchTarget == null) return;
        RefreshTrailerCollisions();
        UpdateTrailerParking();
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
        // Compare both anchors at the same physics time. The render-frame hitch target
        // can lead the interpolated trailer by more than .25 m at normal driving speed.
        var trailerBody = trailerJoint.GetComponent<Rigidbody>();
        var connectedBody = trailerJoint.connectedBody;
        Vector3 trailerAnchor = trailerBody.position + trailerBody.rotation *
            Vector3.Scale(trailerJoint.anchor, trailerJoint.transform.lossyScale);
        Vector3 connectedAnchor = connectedBody != null
            ? connectedBody.position + connectedBody.rotation *
                Vector3.Scale(trailerJoint.connectedAnchor, connectedBody.transform.lossyScale)
            : trailerJoint.connectedAnchor;
        Vector3 gap = connectedAnchor - trailerAnchor;
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
