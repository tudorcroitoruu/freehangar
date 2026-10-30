using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>Flies from the nearest route point without changing the airplane's editor position.</summary>
[DisallowMultipleComponent]
public sealed class PlaneController : MonoBehaviour
{
    [SerializeField] private SplineContainer flightPath;
    [SerializeField, Min(0f)] private float speed = 16f;
    [SerializeField] private bool playOnAwake = true;
    [SerializeField] private SplineComponent.AlignAxis forwardAxis = SplineComponent.AlignAxis.XAxis;
    [SerializeField] private SplineComponent.AlignAxis upAxis = SplineComponent.AlignAxis.YAxis;

    private NativeSpline worldPath;
    private bool hasPath;
    private float normalizedDistance;
    private float length;
    private Quaternion axisCorrection;

    private void Awake()
    {
        if (flightPath == null || flightPath.Splines.Count == 0)
            return;

        var path = new SplinePath<Spline>(flightPath.Splines);
        if (path.GetLength() <= Mathf.Epsilon)
            return;

        worldPath = new NativeSpline(path, (float4x4)flightPath.transform.localToWorldMatrix,
            Unity.Collections.Allocator.Persistent);
        hasPath = true;
        length = worldPath.GetLength();
        if (length <= Mathf.Epsilon)
            return;

        // Search in 3D so crossings at different heights have separate starting points.
        SplineUtility.GetNearestPoint(worldPath, (float3)transform.position,
            out _, out normalizedDistance, 64, 5);
        axisCorrection = Quaternion.Inverse(Quaternion.LookRotation(Axis(forwardAxis), Axis(upAxis)));
    }

    private void Start()
    {
        if (playOnAwake)
            ApplyPose();
    }

    private void Update()
    {
        if (!playOnAwake || !hasPath || length <= Mathf.Epsilon)
            return;

        normalizedDistance = Mathf.Repeat(normalizedDistance + speed * Time.deltaTime / length, 1f);
        ApplyPose();
    }

    private void ApplyPose()
    {
        if (!hasPath || length <= Mathf.Epsilon)
            return;

        SplineUtility.Evaluate(worldPath, normalizedDistance, out var position, out var tangent, out var up);
        if (math.lengthsq(tangent) > 0.000001f && math.lengthsq(up) > 0.000001f)
            transform.SetPositionAndRotation(position,
                Quaternion.LookRotation((Vector3)tangent, (Vector3)up) * axisCorrection);
    }

    private void OnDestroy()
    {
        if (hasPath)
            worldPath.Dispose();
    }

    private static Vector3 Axis(SplineComponent.AlignAxis axis)
    {
        switch (axis)
        {
            case SplineComponent.AlignAxis.XAxis: return Vector3.right;
            case SplineComponent.AlignAxis.YAxis: return Vector3.up;
            case SplineComponent.AlignAxis.NegativeXAxis: return Vector3.left;
            case SplineComponent.AlignAxis.NegativeYAxis: return Vector3.down;
            case SplineComponent.AlignAxis.NegativeZAxis: return Vector3.back;
            default: return Vector3.forward;
        }
    }
}
