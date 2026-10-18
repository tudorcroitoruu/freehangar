using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SnakeSplineBuilder : MonoBehaviour
{
    public enum SnakePlane
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

    public enum VerticalStartPosition
    {
        Upper,
        Lower
    }

    [SerializeField] private SplineContainer splineContainer;

    [Header("Auto Update")]
    [SerializeField] private bool autoRebuildOnValueChange = true;

    [Header("Plane")]
    [SerializeField] private SnakePlane snakePlane = SnakePlane.XY;

    [Header("Shape")]
    [SerializeField] private float width = 7f;
    [SerializeField] private float rowDistance = 1.5f;
    [SerializeField] private int rows = 3;
    [SerializeField] private StartSide startSide = StartSide.Left;
    [SerializeField] private VerticalStartPosition verticalStartPosition = VerticalStartPosition.Upper;
    [SerializeField] private bool reverseDirection = false;

    [Header("Round Corners")]
    [Tooltip("More segments = smoother round corners. Use 12-24.")]
    [SerializeField] private int turnSegments = 16;

    [Tooltip("0 = perfect half row distance. Lower value makes smaller turn.")]
    [Range(0.2f, 1f)]
    [SerializeField] private float turnRadiusMultiplier = 1f;

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

        EditorApplication.delayCall -= DelayedAutoBuild;
        EditorApplication.delayCall += DelayedAutoBuild;
    }

    private void OnDisable()
    {
        EditorApplication.delayCall -= DelayedAutoBuild;
    }

    private void DelayedAutoBuild()
    {
        if (this == null)
            return;

        if (!autoRebuildOnValueChange)
            return;

        BuildSnakeSpline();
    }
#endif

    [ContextMenu("Build Snake Spline")]
    public void BuildSnakeSpline()
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

        width = Mathf.Max(0.5f, width);
        rowDistance = Mathf.Max(0.2f, rowDistance);
        rows = Mathf.Max(2, rows);
        turnSegments = Mathf.Clamp(turnSegments, 4, 64);
        turnRadiusMultiplier = Mathf.Clamp01(turnRadiusMultiplier);

        List<Vector2> points2D = BuildSnakePoints2D();

        if (reverseDirection)
            points2D.Reverse();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RecordObject(splineContainer, "Build Conveyor Spline");
#endif
        Spline spline = new Spline();

        for (int i = 0; i < points2D.Count; i++)
        {
            Vector3 p = ToSelectedPlane(points2D[i].x, points2D[i].y);

            // IMPORTANT:
            // Zero tangents prevent Unity from overshooting and making diagonal/crossed roads.
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

    private List<Vector2> BuildSnakePoints2D()
    {
        List<Vector2> points = new List<Vector2>();

        float halfWidth = width * 0.5f;

        float radius = rowDistance * 0.5f * turnRadiusMultiplier;
        radius = Mathf.Max(0.05f, radius);

        // Need enough width so the round U-turn does not collapse.
        halfWidth = Mathf.Max(halfWidth, radius + 0.2f);

        float topY = (rows - 1) * rowDistance * 0.5f;

        bool startUpper = verticalStartPosition == VerticalStartPosition.Upper;
        bool startLeft = startSide == StartSide.Left;

        for (int rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            float currentY = startUpper
                ? topY - rowIndex * rowDistance
                : -topY + rowIndex * rowDistance;

            bool leftToRight = startLeft
                ? rowIndex % 2 == 0
                : rowIndex % 2 != 0;

            float startX = leftToRight ? -halfWidth : halfWidth;
            float endX = leftToRight ? halfWidth : -halfWidth;

            if (rowIndex == 0)
            {
                AddPoint(points, new Vector2(startX, currentY));
            }

            if (rowIndex == rows - 1)
            {
                AddPoint(points, new Vector2(endX, currentY));
                continue;
            }

            float nextY = startUpper
                ? topY - (rowIndex + 1) * rowDistance
                : -topY + (rowIndex + 1) * rowDistance;

            float turnCenterY = (currentY + nextY) * 0.5f;

            if (leftToRight)
            {
                // Straight part before right rounded turn
                float tangentX = halfWidth - radius;
                AddPoint(points, new Vector2(tangentX, currentY));

                // Right semicircle
                AddRightTurn(points, tangentX, turnCenterY, radius, currentY > nextY);
            }
            else
            {
                // Straight part before left rounded turn
                float tangentX = -halfWidth + radius;
                AddPoint(points, new Vector2(tangentX, currentY));

                // Left semicircle
                AddLeftTurn(points, tangentX, turnCenterY, radius, currentY > nextY);
            }
        }

        return points;
    }

    private void AddRightTurn(List<Vector2> points, float centerX, float centerY, float radius, bool goingDown)
    {
        float fromAngle = goingDown ? 90f : -90f;
        float toAngle = goingDown ? -90f : 90f;

        for (int i = 1; i <= turnSegments; i++)
        {
            float t = i / (float)turnSegments;
            float angle = Mathf.Lerp(fromAngle, toAngle, t) * Mathf.Deg2Rad;

            float x = centerX + Mathf.Cos(angle) * radius;
            float y = centerY + Mathf.Sin(angle) * radius;

            AddPoint(points, new Vector2(x, y));
        }
    }

    private void AddLeftTurn(List<Vector2> points, float centerX, float centerY, float radius, bool goingDown)
    {
        float fromAngle = goingDown ? 90f : -90f;
        float toAngle = goingDown ? 270f : -270f;

        for (int i = 1; i <= turnSegments; i++)
        {
            float t = i / (float)turnSegments;
            float angle = Mathf.Lerp(fromAngle, toAngle, t) * Mathf.Deg2Rad;

            float x = centerX + Mathf.Cos(angle) * radius;
            float y = centerY + Mathf.Sin(angle) * radius;

            AddPoint(points, new Vector2(x, y));
        }
    }

    private void AddPoint(List<Vector2> points, Vector2 point)
    {
        if (points.Count > 0)
        {
            float sqrDistance = (points[points.Count - 1] - point).sqrMagnitude;
            if (sqrDistance < 0.000001f)
                return;
        }

        points.Add(point);
    }

    private Vector3 ToSelectedPlane(float horizontal, float vertical)
    {
        Vector3 p = snakePlane switch
        {
            SnakePlane.XY => new Vector3(horizontal, vertical, 0f),
            SnakePlane.XZ => new Vector3(horizontal, 0f, vertical),
            SnakePlane.YZ => new Vector3(0f, vertical, horizontal),
            _ => new Vector3(horizontal, 0f, vertical)
        };

        return localOffset + p;
    }
}

