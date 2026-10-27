using UnityEngine;
using System.Collections.Generic;

/// <summary>Fits vehicle visuals to wheel support while keeping the movement capsule upright.</summary>
[DisallowMultipleComponent]
public sealed class VehicleGroundAlignment : MonoBehaviour
{
    public Transform Pose { get; private set; }
    readonly List<Vector3> axles = new List<Vector3>(4);
    readonly List<float> radii = new List<float>(4);
    readonly RaycastHit[] hits = new RaycastHit[32];
    readonly Vector3[] contacts = new Vector3[4];
    readonly bool[] supported = new bool[4];
    Transform ignoredTrailer;
    bool initialized;
    public void Initialize(BicycleController rig)
    {
        if (initialized) return;
        initialized = true;
        Pose = transform.Find("Ground aligned vehicle");
        if (Pose != null) { Pose.localPosition = Vector3.zero; Pose.localRotation = Quaternion.identity; }
        AddWheel(rig.frontWheel, rig.wheelRadius);
        AddWheel(rig.rearWheel, rig.wheelRadius);
        var pickup = GetComponent<PickupController>();
        var mower = GetComponent<LawnmowerController>();
        var trike = GetComponent<TrikeController>();
        if (pickup != null) { AddWheel(pickup.frontRightWheel, rig.wheelRadius); AddWheel(pickup.rearRightWheel, rig.wheelRadius); }
        else if (mower != null)
        {
            AddWheel(mower.frontRightWheel, rig.wheelRadius); AddWheel(mower.rearRightWheel, rig.wheelRadius);
            if (mower.trailerJoint != null) ignoredTrailer = mower.trailerJoint.transform;
        }
        else if (trike != null) AddWheel(trike.RearRightWheel, rig.wheelRadius);
        if (Pose == null)
        {
            var children = new List<Transform>();
            foreach (Transform child in transform) children.Add(child);
            Pose = new GameObject("Ground aligned vehicle").transform;
            Pose.SetParent(transform, false);
            foreach (var child in children) child.SetParent(Pose, true);
            // Parked body collision must follow the same pitch/roll as the visible
            // body. Only the movement capsule remains upright on the vehicle root.
            foreach (var original in GetComponents<BoxCollider>())
            {
                if (!original.enabled) continue;
                var body = new GameObject("Ground aligned body collider");
                body.layer = gameObject.layer;
                body.transform.SetParent(Pose, false);
                var collider = body.AddComponent<BoxCollider>();
                collider.center = original.center; collider.size = original.size;
                collider.sharedMaterial = original.sharedMaterial;
                collider.isTrigger = original.isTrigger;
                collider.contactOffset = original.contactOffset;
                collider.includeLayers = original.includeLayers; collider.excludeLayers = original.excludeLayers;
                collider.layerOverridePriority = original.layerOverridePriority;
                original.enabled = false;
            }
        }
        Align(1f);
    }
    void AddWheel(Transform wheel, float fallbackRadius)
    {
        axles.Add(transform.InverseTransformPoint(wheel.position));
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        foreach (var filter in wheel.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            var b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                float y = transform.InverseTransformPoint(filter.transform.TransformPoint(corner)).y;
                min = Mathf.Min(min, y); max = Mathf.Max(max, y);
            }
        }
        radii.Add(float.IsFinite(min) ? (max - min) * .5f : fallbackRadius);
    }
    public void Align(float dt)
    {
        if (Pose == null || dt <= 0) return;
        int count = 0; Vector3 mean = Vector3.zero, meanNormal = Vector3.zero;
        for (int i = 0; i < axles.Count; i++)
        {
            Vector3 wheel = Pose.TransformPoint(axles[i]);
            int n = Physics.RaycastNonAlloc(wheel + Vector3.up * 1.5f, Vector3.down, hits, 4f, ~0, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity; supported[i] = false;
            Vector3 selectedNormal = Vector3.up;
            for (int h = 0; h < n; h++)
            {
                var hit = hits[h];
                if (hit.transform.IsChildOf(transform) || (ignoredTrailer != null && hit.transform.IsChildOf(ignoredTrailer)) || hit.normal.y < .65f || hit.distance >= closest) continue;
                closest = hit.distance; contacts[i] = hit.point; supported[i] = true; selectedNormal = hit.normal;
            }
            if (!supported[i]) continue;
            meanNormal += selectedNormal;
            mean += contacts[i]; count++;
        }
        if (count < 2 || !supported[0] || !supported[1]) return;
        mean /= count;
        float xx = 0, zz = 0, xz = 0, xy = 0, zy = 0;
        for (int i = 0; i < axles.Count; i++) if (supported[i])
        {
            Vector3 d = contacts[i] - mean;
            xx += d.x * d.x; zz += d.z * d.z; xz += d.x * d.z; xy += d.x * d.y; zy += d.z * d.y;
        }
        float determinant = xx * zz - xz * xz;
        Vector3 normal;
        if (determinant > .00001f)
            normal = new Vector3(-(xy * zz - zy * xz) / determinant, 1, -(zy * xx - xy * xz) / determinant).normalized;
        else
        {
            Vector3 forward = (contacts[0] - contacts[1]).normalized;
            normal = Vector3.Cross(forward, Vector3.Cross(meanNormal.normalized, forward)).normalized;
        }
        if (normal.y < .65f) return;
        var target = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, normal), normal);
        float blend = 1 - Mathf.Exp(-dt / .08f);
        Pose.rotation = Quaternion.Slerp(Pose.rotation, target, blend);
        // Lift to the highest required support, preventing a tire from being buried
        // when the four contacts do not form a perfect plane.
        float correction = float.NegativeInfinity;
        for (int i = 0; i < axles.Count; i++) if (supported[i])
        {
            var wheel = Pose.TransformPoint(axles[i]);
            float groundY = mean.y - (normal.x * (wheel.x - mean.x) + normal.z * (wheel.z - mean.z)) / normal.y;
            float requiredY = Mathf.Max(groundY, contacts[i].y) + radii[i] * transform.lossyScale.y / normal.y;
            correction = Mathf.Max(correction, requiredY - wheel.y);
        }
        Pose.position += Vector3.up * (correction * blend);
    }
}
