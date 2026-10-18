using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class LevelRoadSplineBuilder : MonoBehaviour
{
    public enum BuildPlane
    {
        XY,
        XZ,
        YZ
    }

    public enum StartSide
    {
        Left,
        Right
    }

    public enum StartVertical
    {
        Top,
        Bottom
    }

    [SerializeField] private SplineContainer splineContainer;

    [Header("Auto Update")]
    [SerializeField] private bool autoRebuildOnValueChange = true;

    [Header("Plane")]
    [SerializeField] private BuildPlane buildPlane = BuildPlane.XY;

    [Header("Main Shape")]
    [SerializeField] private StartSide startSide = StartSide.Left;
    [SerializeField] private StartVertical startVertical = StartVertical.Top;

    [Tooltip("Distance between left and right vertical roads.")]
    [SerializeField] private float width = 5.2f;

    [Tooltip("Full vertical size of the whole road.")]
    [SerializeField] private float height = 12f;

    [Tooltip("Vertical straight distance from start to first corner.")]
    [SerializeField] private float topStraightLength = 4.2f;

    [Tooltip("Vertical straight distance from second corner to end.")]
    [SerializeField] private float bottomStraightLength = 4.2f;

    [Header("Middle Connector")]
    [Tooltip("Horizontal offset added to the middle segment. 0 = direct connection.")]
    [SerializeField] private float middleHorizontalOffset = 0f;

    [Header("Corner Rounding")]
    [SerializeField] private float cornerRadius = 0.8f;

    [Tooltip("More segments = smoother rounded corners.")]
    [SerializeField] private int cornerSegments = 12;

    [Header("Position")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    [Header("Direction")]
    [SerializeField] private bool reverseSplineDirection = false;

    private bool isBuilding;

    private void Reset()
    {
        splineContainer = GetComponent<SplineContainer>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!autoRebuildOnValueChange)
            return;

        if (isBuilding)
            return;

        EditorApplication.delayCall -= DelayedBuild;
        EditorApplication.delayCall += DelayedBuild;
    }

    private void OnDisable()
    {
        EditorApplication.delayCall -= DelayedBuild;
    }

    private void DelayedBuild()
    {
        if (this == null)
            return;

        if (!autoRebuildOnValueChange)
            return;

        BuildSpline();
    }
#endif

    [ContextMenu("Build Level Conveyor Spline")]
    public void BuildSpline()
    {
        if (isBuilding)
            return;

        isBuilding = true;

        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();

        if (splineContainer == null)
        {
            Debug.LogError("No SplineContainer found.", this);
            isBuilding = false;
            return;
        }

        width = Mathf.Max(1f, width);
        height = Mathf.Max(2f, height);
        topStraightLength = Mathf.Max(0.1f, topStraightLength);
        bottomStraightLength = Mathf.Max(0.1f, bottomStraightLength);
        cornerRadius = Mathf.Max(0.01f, cornerRadius);
        cornerSegments = Mathf.Clamp(cornerSegments, 3, 64);

        List<Vector2> path2D = BuildRoundedRoadPoints2D();

        if (reverseSplineDirection)
            path2D.Reverse();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RecordObject(splineContainer, "Build Conveyor Spline");
#endif
        Spline spline = new Spline();

        for (int i = 0; i < path2D.Count; i++)
        {
            Vector3 p = ToPlane(path2D[i].x, path2D[i].y);

            // zero tangents = no weird bezier overshoot
            BezierKnot knot = new BezierKnot(
                new float3(p.x, p.y, p.z),
                float3.zero,
                float3.zero,
                quaternion.identity
            );

            spline.Add(knot);
        }

        splineContainer.Spline = spline;

#if UNITY_EDITOR
        EditorUtility.SetDirty(splineContainer);
        EditorUtility.SetDirty(gameObject);
#endif

        isBuilding = false;
    }

    private List<Vector2> BuildRoundedRoadPoints2D()
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        bool startsLeft = startSide == StartSide.Left;
        bool startsTop = startVertical == StartVertical.Top;

        float startX = startsLeft ? -halfWidth : halfWidth;
        float endX = startsLeft ? halfWidth : -halfWidth;

        float startY = startsTop ? halfHeight : -halfHeight;
        float endY = startsTop ? -halfHeight : halfHeight;

        float verticalDir = startsTop ? -1f : 1f;

        Vector2 start = new Vector2(startX, startY);
        Vector2 firstCorner = new Vector2(startX, startY + verticalDir * topStraightLength);

        // middle segment target
        float middleX = endX + middleHorizontalOffset;
        Vector2 secondCorner = new Vector2(middleX, endY - verticalDir * bottomStraightLength);

        Vector2 end = new Vector2(endX, endY);

        List<Vector2> polyline = new List<Vector2>
        {
            start,
            firstCorner,
            secondCorner,
            end
        };

        return CreateRoundedPolyline(polyline, cornerRadius, cornerSegments);
    }

    private List<Vector2> CreateRoundedPolyline(List<Vector2> input, float radius, int segmentsPerCorner)
    {
        List<Vector2> result = new List<Vector2>();

        if (input == null || input.Count < 2)
            return result;

        result.Add(input[0]);

        for (int i = 1; i < input.Count - 1; i++)
        {
            Vector2 p0 = input[i - 1];
            Vector2 p1 = input[i];
            Vector2 p2 = input[i + 1];

            Vector2 dirIn = (p1 - p0).normalized;
            Vector2 dirOut = (p2 - p1).normalized;

            float lenIn = Vector2.Distance(p0, p1);
            float lenOut = Vector2.Distance(p1, p2);

            // if almost straight, no need to round
            float dot = Mathf.Clamp(Vector2.Dot(dirIn, dirOut), -1f, 1f);
            float turnAngle = Mathf.Acos(dot);

            if (turnAngle < 0.01f || Mathf.Abs(Mathf.PI - turnAngle) < 0.01f)
            {
                AddUnique(result, p1);
                continue;
            }

            float maxAllowed = Mathf.Min(lenIn, lenOut) * 0.5f;
            float usedRadius = Mathf.Min(radius, maxAllowed);

            float tangentDistance = usedRadius / Mathf.Tan(turnAngle * 0.5f);
            tangentDistance = Mathf.Min(tangentDistance, maxAllowed);

            Vector2 tangentStart = p1 - dirIn * tangentDistance;
            Vector2 tangentEnd = p1 + dirOut * tangentDistance;

            AddUnique(result, tangentStart);

            if (TryBuildArc(p1, dirIn, dirOut, tangentStart, tangentEnd, segmentsPerCorner, out List<Vector2> arcPoints))
            {
                for (int a = 0; a < arcPoints.Count; a++)
                    AddUnique(result, arcPoints[a]);
            }
            else
            {
                AddUnique(result, tangentEnd);
            }
        }

        result.Add(input[input.Count - 1]);
        return result;
    }

    private bool TryBuildArc(
        Vector2 corner,
        Vector2 dirIn,
        Vector2 dirOut,
        Vector2 tangentStart,
        Vector2 tangentEnd,
        int segments,
        out List<Vector2> arcPoints)
    {
        arcPoints = new List<Vector2>();

        float cross = dirIn.x * dirOut.y - dirIn.y * dirOut.x;

        if (Mathf.Abs(cross) < 0.00001f)
            return false;

        Vector2 normalIn = cross > 0f ? LeftNormal(dirIn) : RightNormal(dirIn);
        Vector2 normalOut = cross > 0f ? LeftNormal(dirOut) : RightNormal(dirOut);

        if (!LineLineIntersection(tangentStart, normalIn, tangentEnd, normalOut, out Vector2 center))
            return false;

        float radius = Vector2.Distance(center, tangentStart);
        if (radius < 0.0001f)
            return false;

        float startAngle = Mathf.Atan2(tangentStart.y - center.y, tangentStart.x - center.x);
        float endAngle = Mathf.Atan2(tangentEnd.y - center.y, tangentEnd.x - center.x);

        if (cross > 0f)
        {
            if (endAngle < startAngle)
                endAngle += Mathf.PI * 2f;
        }
        else
        {
            if (endAngle > startAngle)
                endAngle -= Mathf.PI * 2f;
        }

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(startAngle, endAngle, t);

            Vector2 p = new Vector2(
                center.x + Mathf.Cos(angle) * radius,
                center.y + Mathf.Sin(angle) * radius
            );

            arcPoints.Add(p);
        }

        return true;
    }

    private bool LineLineIntersection(Vector2 p1, Vector2 d1, Vector2 p2, Vector2 d2, out Vector2 intersection)
    {
        float cross = d1.x * d2.y - d1.y * d2.x;

        if (Mathf.Abs(cross) < 0.00001f)
        {
            intersection = Vector2.zero;
            return false;
        }

        Vector2 delta = p2 - p1;
        float t = (delta.x * d2.y - delta.y * d2.x) / cross;
        intersection = p1 + d1 * t;
        return true;
    }

    private Vector2 LeftNormal(Vector2 v)
    {
        return new Vector2(-v.y, v.x).normalized;
    }

    private Vector2 RightNormal(Vector2 v)
    {
        return new Vector2(v.y, -v.x).normalized;
    }

    private void AddUnique(List<Vector2> points, Vector2 point)
    {
        if (points.Count > 0)
        {
            if ((points[points.Count - 1] - point).sqrMagnitude < 0.000001f)
                return;
        }

        points.Add(point);
    }

    private Vector3 ToPlane(float horizontal, float vertical)
    {
        Vector3 p = buildPlane switch
        {
            BuildPlane.XY => new Vector3(horizontal, vertical, 0f),
            BuildPlane.XZ => new Vector3(horizontal, 0f, vertical),
            BuildPlane.YZ => new Vector3(0f, vertical, horizontal),
            _ => new Vector3(horizontal, 0f, vertical)
        };

        return localOffset + p;
    }
}

