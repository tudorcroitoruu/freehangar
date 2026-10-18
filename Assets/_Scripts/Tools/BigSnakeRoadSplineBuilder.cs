using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class BigSnakeRoadSplineBuilder : MonoBehaviour
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

    public enum VerticalStart
    {
        Top,
        Bottom
    }

    [SerializeField] private SplineContainer splineContainer;

    [Header("Auto Update")]
    [SerializeField] private bool autoRebuildOnValueChange = true;

    [Header("Plane")]
    [SerializeField] private BuildPlane buildPlane = BuildPlane.XY;

    [Header("Snake Shape")]
    [SerializeField] private StartSide startSide = StartSide.Left;
    [SerializeField] private VerticalStart verticalStart = VerticalStart.Top;

    [Tooltip("Road width from left side to right side.")]
    [SerializeField] private float width = 6.2f;

    [Tooltip("Distance between horizontal road rows.")]
    [SerializeField] private float rowDistance = 2.3f;

    [Tooltip("How many horizontal road rows.")]
    [SerializeField] private int rows = 5;

    [Header("Start / End Extensions")]
    [SerializeField] private float startExtension = 0.8f;
    [SerializeField] private float endExtension = 0.8f;

    [Header("Rounded Corners")]
    [Tooltip("Corner radius. Usually rowDistance * 0.45 is good.")]
    [SerializeField] private float cornerRadius = 0.9f;

    [Tooltip("More segments = smoother corners.")]
    [SerializeField] private int cornerSegments = 16;

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

        BuildSnakeRoad();
    }
#endif

    [ContextMenu("Build Big Snake Conveyor")]
    public void BuildSnakeRoad()
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
        rowDistance = Mathf.Max(0.3f, rowDistance);
        rows = Mathf.Max(2, rows);
        cornerSegments = Mathf.Clamp(cornerSegments, 3, 64);
        cornerRadius = Mathf.Clamp(cornerRadius, 0.01f, Mathf.Min(width, rowDistance) * 0.45f);

        List<Vector2> rawPolyline = BuildRawSnakePolyline();
        List<Vector2> roundedPath = CreateRoundedPolyline(rawPolyline, cornerRadius, cornerSegments);

        if (reverseSplineDirection)
            roundedPath.Reverse();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RecordObject(splineContainer, "Build Conveyor Spline");
#endif
        Spline spline = new Spline();

        for (int i = 0; i < roundedPath.Count; i++)
        {
            Vector3 p = ToPlane(roundedPath[i].x, roundedPath[i].y);

            // Important: zero tangents avoid Bezier overshoot/inverted curves.
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

    private List<Vector2> BuildRawSnakePolyline()
    {
        List<Vector2> points = new List<Vector2>();

        float halfWidth = width * 0.5f;
        float firstY = (rows - 1) * rowDistance * 0.5f;

        bool startsLeft = startSide == StartSide.Left;
        bool startsTop = verticalStart == VerticalStart.Top;

        for (int row = 0; row < rows; row++)
        {
            float y = startsTop
                ? firstY - row * rowDistance
                : -firstY + row * rowDistance;

            bool leftToRight = startsLeft
                ? row % 2 == 0
                : row % 2 != 0;

            float startX = leftToRight ? -halfWidth : halfWidth;
            float endX = leftToRight ? halfWidth : -halfWidth;

            if (row == 0)
            {
                if (startExtension > 0f)
                {
                    float extensionDir = leftToRight ? -1f : 1f;
                    AddUnique(points, new Vector2(startX + extensionDir * startExtension, y));
                }

                AddUnique(points, new Vector2(startX, y));
            }

            AddUnique(points, new Vector2(endX, y));

            if (row < rows - 1)
            {
                float nextY = startsTop
                    ? firstY - (row + 1) * rowDistance
                    : -firstY + (row + 1) * rowDistance;

                // Vertical connector on the same side.
                AddUnique(points, new Vector2(endX, nextY));
            }
            else
            {
                if (endExtension > 0f)
                {
                    float extensionDir = leftToRight ? 1f : -1f;
                    AddUnique(points, new Vector2(endX + extensionDir * endExtension, y));
                }
            }
        }

        return points;
    }

    private List<Vector2> CreateRoundedPolyline(List<Vector2> input, float radius, int segmentsPerCorner)
    {
        List<Vector2> result = new List<Vector2>();

        if (input == null || input.Count < 2)
            return result;

        AddUnique(result, input[0]);

        for (int i = 1; i < input.Count - 1; i++)
        {
            Vector2 previous = input[i - 1];
            Vector2 corner = input[i];
            Vector2 next = input[i + 1];

            Vector2 dirIn = (corner - previous).normalized;
            Vector2 dirOut = (next - corner).normalized;

            float lenIn = Vector2.Distance(previous, corner);
            float lenOut = Vector2.Distance(corner, next);

            float dot = Mathf.Clamp(Vector2.Dot(dirIn, dirOut), -1f, 1f);
            float angle = Mathf.Acos(dot);

            // Straight line or 180 degree turn safety.
            if (angle < 0.001f || Mathf.Abs(Mathf.PI - angle) < 0.001f)
            {
                AddUnique(result, corner);
                continue;
            }

            float maxDistance = Mathf.Min(lenIn, lenOut) * 0.45f;
            float usedRadius = Mathf.Min(radius, maxDistance);

            float tangentDistance = usedRadius / Mathf.Tan(angle * 0.5f);
            tangentDistance = Mathf.Min(tangentDistance, maxDistance);

            Vector2 tangentStart = corner - dirIn * tangentDistance;
            Vector2 tangentEnd = corner + dirOut * tangentDistance;

            AddUnique(result, tangentStart);

            if (TryCreateCornerArc(
                    dirIn,
                    dirOut,
                    tangentStart,
                    tangentEnd,
                    segmentsPerCorner,
                    out List<Vector2> arcPoints))
            {
                for (int a = 0; a < arcPoints.Count; a++)
                    AddUnique(result, arcPoints[a]);
            }
            else
            {
                AddUnique(result, tangentEnd);
            }
        }

        AddUnique(result, input[input.Count - 1]);
        return result;
    }

    private bool TryCreateCornerArc(
        Vector2 dirIn,
        Vector2 dirOut,
        Vector2 tangentStart,
        Vector2 tangentEnd,
        int segments,
        out List<Vector2> arcPoints)
    {
        arcPoints = new List<Vector2>();

        float cross = Cross(dirIn, dirOut);

        if (Mathf.Abs(cross) < 0.00001f)
            return false;

        Vector2 normalIn;
        Vector2 normalOut;

        if (cross > 0f)
        {
            normalIn = LeftNormal(dirIn);
            normalOut = LeftNormal(dirOut);
        }
        else
        {
            normalIn = RightNormal(dirIn);
            normalOut = RightNormal(dirOut);
        }

        if (!LineIntersection(tangentStart, normalIn, tangentEnd, normalOut, out Vector2 center))
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
            float a = Mathf.Lerp(startAngle, endAngle, t);

            Vector2 point = new Vector2(
                center.x + Mathf.Cos(a) * radius,
                center.y + Mathf.Sin(a) * radius
            );

            arcPoints.Add(point);
        }

        return true;
    }

    private bool LineIntersection(Vector2 p1, Vector2 d1, Vector2 p2, Vector2 d2, out Vector2 intersection)
    {
        float cross = Cross(d1, d2);

        if (Mathf.Abs(cross) < 0.00001f)
        {
            intersection = Vector2.zero;
            return false;
        }

        Vector2 delta = p2 - p1;
        float t = Cross(delta, d2) / cross;

        intersection = p1 + d1 * t;
        return true;
    }

    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
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

