using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class LoopBottleRoadSplineBuilder : MonoBehaviour
{
    public enum BuildPlane
    {
        XY,
        XZ,
        YZ
    }

    public enum StartPosition
    {
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

    [SerializeField] private SplineContainer splineContainer;

    [Header("Auto Update")]
    [SerializeField] private bool autoRebuildOnValueChange = true;

    [Header("Plane")]
    [SerializeField] private BuildPlane buildPlane = BuildPlane.XY;

    [Header("Main Size")]
    [SerializeField] private float width = 6.2f;
    [SerializeField] private float height = 6.8f;

    [Header("Corners")]
    [Tooltip("Main rectangle corner radius.")]
    [SerializeField] private float cornerRadius = 0.9f;

    [Tooltip("More segments = smoother rounded corners.")]
    [SerializeField] private int cornerSegments = 18;

    [Header("Side Shape")]
    [Tooltip("How much the side curves move inward near upper/lower-middle.")]
    [SerializeField] private float neckInset = 0.55f;

    [Tooltip("How much the side curves move outward around the center.")]
    [SerializeField] private float waistBulge = 0.20f;

    [Tooltip("Vertical offset for the side waist.")]
    [SerializeField] private float waistVerticalOffset = 0f;

    [Tooltip("More segments = smoother side curves.")]
    [SerializeField] private int sideSegments = 28;

    [Header("Top / Bottom Straights")]
    [SerializeField] private int topBottomSegments = 10;

    [Header("Loop")]
    [SerializeField] private bool closedLoop = true;
    [SerializeField] private StartPosition startPosition = StartPosition.BottomLeft;
    [SerializeField] private bool reverseDirection = false;

    [Header("Position")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;

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

        BuildLoopRoad();
    }
#endif

    [ContextMenu("Build Loop Bottle Conveyor")]
    public void BuildLoopRoad()
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
        height = Mathf.Max(1f, height);
        cornerRadius = Mathf.Clamp(cornerRadius, 0.05f, Mathf.Min(width, height) * 0.35f);

        neckInset = Mathf.Max(0f, neckInset);
        waistBulge = Mathf.Max(0f, waistBulge);

        cornerSegments = Mathf.Clamp(cornerSegments, 4, 64);
        sideSegments = Mathf.Clamp(sideSegments, 6, 128);
        topBottomSegments = Mathf.Clamp(topBottomSegments, 1, 64);

        List<Vector2> points2D = BuildBottleLoop2D();

        RotateStartPoint(points2D);

        if (reverseDirection)
            points2D.Reverse();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RecordObject(splineContainer, "Build Conveyor Spline");
#endif
        Spline spline = new Spline();

        for (int i = 0; i < points2D.Count; i++)
        {
            Vector3 p = ToPlane(points2D[i].x, points2D[i].y);

            // Keep zero tangents to avoid weird bezier overshoot.
            // Smoothness comes from dense generated points.
            BezierKnot knot = new BezierKnot(
                new float3(p.x, p.y, p.z),
                float3.zero,
                float3.zero,
                quaternion.identity
            );

            spline.Add(knot);
        }

        spline.Closed = closedLoop;
        splineContainer.Spline = spline;

#if UNITY_EDITOR
        EditorUtility.SetDirty(splineContainer);
        EditorUtility.SetDirty(gameObject);
#endif

        isBuilding = false;
    }

    private List<Vector2> BuildBottleLoop2D()
    {
        List<Vector2> points = new List<Vector2>();

        float halfW = width * 0.5f;
        float halfH = height * 0.5f;
        float r = cornerRadius;

        float leftX = -halfW;
        float rightX = halfW;
        float topY = halfH;
        float bottomY = -halfH;

        float topSideY = topY - r;
        float bottomSideY = bottomY + r;

        // Start at bottom-left straight and go clockwise.

        // Bottom horizontal
        AddLine(points,
            new Vector2(leftX + r, bottomY),
            new Vector2(rightX - r, bottomY),
            topBottomSegments);

        // Bottom-right rounded corner
        AddArc(points,
            new Vector2(rightX - r, bottomY + r),
            r,
            -90f,
            0f,
            cornerSegments);

        // Right side smooth S-curve
        AddBottleSide(points, true, rightX, bottomSideY, topSideY);

        // Top-right rounded corner
        AddArc(points,
            new Vector2(rightX - r, topY - r),
            r,
            0f,
            90f,
            cornerSegments);

        // Top horizontal
        AddLine(points,
            new Vector2(rightX - r, topY),
            new Vector2(leftX + r, topY),
            topBottomSegments);

        // Top-left rounded corner
        AddArc(points,
            new Vector2(leftX + r, topY - r),
            r,
            90f,
            180f,
            cornerSegments);

        // Left side smooth S-curve
        AddBottleSide(points, false, leftX, topSideY, bottomSideY);

        // Bottom-left rounded corner
        AddArc(points,
            new Vector2(leftX + r, bottomY + r),
            r,
            180f,
            270f,
            cornerSegments);

        return points;
    }

    private void AddBottleSide(List<Vector2> points, bool rightSide, float sideX, float fromY, float toY)
    {
        float direction = rightSide ? 1f : -1f;

        for (int i = 1; i <= sideSegments; i++)
        {
            float t = i / (float)sideSegments;

            float y = Mathf.Lerp(fromY, toY, t);

            // Smooth vertical distribution of the "waist"
            float centerBlend = Mathf.Sin(t * Mathf.PI);                 // 0 at ends, 1 at center
            float fourLobe = Mathf.Sin(t * Mathf.PI * 2f);              // creates inward / outward / inward
            float fourLobeSq = fourLobe * fourLobe;                     // smoother shape

            // Shift center a little if desired
            y += waistVerticalOffset * centerBlend;

            // Main side X offset
            // - neckInset makes the curve go inward around 1/4 and 3/4
            // + waistBulge makes a small outward belly near the middle
            float xOffset =
                (-neckInset * fourLobeSq) +
                (waistBulge * centerBlend * centerBlend);

            float x = sideX + direction * xOffset;

            AddUnique(points, new Vector2(x, y));
        }
    }

    private void AddLine(List<Vector2> points, Vector2 from, Vector2 to, int segments)
    {
        segments = Mathf.Max(1, segments);

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            AddUnique(points, Vector2.Lerp(from, to, t));
        }
    }

    private void AddArc(List<Vector2> points, Vector2 center, float radius, float fromAngle, float toAngle, int segments)
    {
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(fromAngle, toAngle, t) * Mathf.Deg2Rad;

            Vector2 p = new Vector2(
                center.x + Mathf.Cos(angle) * radius,
                center.y + Mathf.Sin(angle) * radius
            );

            AddUnique(points, p);
        }
    }

    private void RotateStartPoint(List<Vector2> points)
    {
        if (points == null || points.Count == 0)
            return;

        int bestIndex = 0;
        float bestScore = float.MaxValue;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 p = points[i];

            float score = startPosition switch
            {
                StartPosition.TopLeft => -p.y + p.x,
                StartPosition.TopRight => -p.y - p.x,
                StartPosition.BottomRight => p.y - p.x,
                StartPosition.BottomLeft => p.y + p.x,
                _ => p.y + p.x
            };

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        if (bestIndex <= 0)
            return;

        List<Vector2> rotated = new List<Vector2>(points.Count);

        for (int i = bestIndex; i < points.Count; i++)
            rotated.Add(points[i]);

        for (int i = 0; i < bestIndex; i++)
            rotated.Add(points[i]);

        points.Clear();
        points.AddRange(rotated);
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

