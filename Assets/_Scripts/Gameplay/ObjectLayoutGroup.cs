using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ObjectLayoutGroup : MonoBehaviour
{
    public enum LayoutType
    {
        Grid,
        LineX,
        LineY,
        LineZ,
        Circle
    }

    public enum LayoutAnchor
    {
        UpperLeft,
        UpperCenter,
        UpperRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        LowerLeft,
        LowerCenter,
        LowerRight
    }

    [Header("Layout")]
    [SerializeField] private LayoutType layoutType = LayoutType.Grid;
    [SerializeField] private LayoutAnchor startPoint = LayoutAnchor.UpperLeft;
    [SerializeField] private bool autoRefresh = true;
    [Tooltip("Places active children in reverse hierarchy order without changing their sibling indices.")]
    [SerializeField] private bool reverseArrangement;
    [Tooltip("Keeps each child's saved position on axes the selected layout does not control. For example, Line Y preserves each child's X and Z offsets.")]
    [SerializeField] private bool preserveCrossAxisOffsets = true;

    [Header("Grid")]
    [Min(1)]
    [SerializeField] private int columns = 5;
    [SerializeField] private Vector2 gridSpacing = Vector2.one;

    [Header("Padding")]
    [SerializeField] private float paddingTop;
    [SerializeField] private float paddingBottom;
    [SerializeField] private float paddingLeft;
    [SerializeField] private float paddingRight;

    [Header("Line")]
    [SerializeField] private float linearSpacing = 1f;

    [Header("Circle")]
    [Min(0f)]
    [SerializeField] private float radius = 5f;

    [Header("Content Width")]
    [Tooltip("Maximum width occupied by SpriteRenderer content and horizontal padding. Set to 0 for unlimited width. Grid and Line X reduce their horizontal spacing when needed.")]
    [Min(0f)]
    [SerializeField] private float contentWidth;
    [SerializeField] private Color contentWidthGizmoColor = new Color(0f, 0.8f, 1f, 1f);

    [Header("Runtime Animation")]
    [SerializeField, Min(0f)] private float runtimeMoveDuration = 0.18f;
    [SerializeField, Range(0f, 0.35f)] private float runtimeScalePulse = 0.12f;

    private readonly List<Transform> layoutChildren = new List<Transform>();
    private readonly List<Vector3> layoutPositions = new List<Vector3>();
    private readonly List<Vector3> layoutStartPositions = new List<Vector3>();
    private readonly List<Vector3> layoutTargetPositions = new List<Vector3>();
    private readonly List<Vector3> layoutStartScales = new List<Vector3>();
    private readonly List<bool> layoutMovingChildren = new List<bool>();
    private readonly List<bool> layoutControlsChildScale = new List<bool>();
    private readonly List<float> childMinimumX = new List<float>();
    private readonly List<float> childMaximumX = new List<float>();
    private readonly Dictionary<Transform, Vector3> preservedCrossAxisOffsets = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> lastAppliedChildPositions = new Dictionary<Transform, Vector3>();
    private int childrenSignature;
    private bool isApplyingLayout;
    private bool layoutAnimationActive;
    private float layoutAnimationElapsed;

    public bool ReverseArrangement => reverseArrangement;

    private void OnEnable()
    {
        RefreshLayoutImmediate();
    }

    private void OnValidate()
    {
        columns = Mathf.Max(1, columns);
        radius = Mathf.Max(0f, radius);
        contentWidth = Mathf.Max(0f, contentWidth);
        runtimeMoveDuration = Mathf.Max(0f, runtimeMoveDuration);
        runtimeScalePulse = Mathf.Clamp(runtimeScalePulse, 0f, 0.35f);

        if (autoRefresh && isActiveAndEnabled)
        {
            RefreshLayout();
        }
    }

    private void OnTransformChildrenChanged()
    {
        if (autoRefresh)
        {
            RefreshLayout();
        }
    }

    private void OnDidApplyAnimationProperties()
    {
        if (autoRefresh)
        {
            RefreshLayout();
        }
    }

    private void Update()
    {
        if (Application.isPlaying && layoutAnimationActive)
        {
            UpdateLayoutAnimation();
            childrenSignature = CalculateChildrenSignature();
            return;
        }

        if (autoRefresh && childrenSignature != CalculateChildrenSignature())
        {
            RefreshLayout();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (contentWidth <= 0f)
        {
            return;
        }

        GetAnchorFactors(out float horizontalFactor, out _);

        float left = -contentWidth * horizontalFactor;
        float right = contentWidth * (1f - horizontalFactor);
        float endCapHalfSize = Mathf.Max(0.1f, contentWidth * 0.025f);
        Vector3 endCapDirection = layoutType == LayoutType.LineY ? Vector3.up : Vector3.forward;

        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = contentWidthGizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 leftPoint = new Vector3(left, 0f, 0f);
        Vector3 rightPoint = new Vector3(right, 0f, 0f);
        Vector3 endCap = endCapDirection * endCapHalfSize;

        Gizmos.DrawLine(leftPoint, rightPoint);
        Gizmos.DrawLine(leftPoint - endCap, leftPoint + endCap);
        Gizmos.DrawLine(rightPoint - endCap, rightPoint + endCap);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    [ContextMenu("Refresh Layout")]
    public void RefreshLayout()
    {
        RefreshLayoutInternal(Application.isPlaying && runtimeMoveDuration > 0f);
    }

    public void RefreshLayoutImmediate()
    {
        RefreshLayoutInternal(false);
    }

    private void RefreshLayoutInternal(bool animate)
    {
        if (isApplyingLayout)
        {
            return;
        }

        isApplyingLayout = true;

        try
        {
            if (layoutAnimationActive)
                RestoreAnimatedScales();
            CollectActiveChildren();
            CaptureExternalChildOffsets();
            CapturePreservedCrossAxisOffsets();
            MeasureChildContent();
            BuildLayoutPositions();
            ApplyAnchorOffset();
            ApplyPreservedCrossAxisOffsets();
            ApplyChildOffsets();

            if (animate && layoutChildren.Count > 0)
            {
                BeginLayoutAnimation();
            }
            else
            {
                layoutAnimationActive = false;
                for (int i = 0; i < layoutChildren.Count; i++)
                {
                    layoutChildren[i].localPosition = layoutPositions[i];
                    RecordAppliedChildPosition(i);
                }
            }

            childrenSignature = CalculateChildrenSignature();
        }
        finally
        {
            isApplyingLayout = false;
        }
    }

    private void BeginLayoutAnimation()
    {
        layoutStartPositions.Clear();
        layoutTargetPositions.Clear();
        layoutStartScales.Clear();
        layoutMovingChildren.Clear();
        layoutControlsChildScale.Clear();
        bool hasMovement = false;
        for (int index = 0; index < layoutChildren.Count; index++)
        {
            Vector3 start = layoutChildren[index].localPosition;
            Vector3 target = layoutPositions[index];
            bool isMoving = (start - target).sqrMagnitude > 0.000001f;
            layoutStartPositions.Add(start);
            layoutTargetPositions.Add(target);
            layoutStartScales.Add(layoutChildren[index].localScale);
            layoutMovingChildren.Add(isMoving);
            // ItemColor owns its scale through its idle/click presentation.
            // Nested layout groups also own their children's presentation, so
            // an outer layout must never pulse a complete column at once.
            layoutControlsChildScale.Add(
                layoutChildren[index].GetComponent<ObjectLayoutGroup>() == null);
            hasMovement |= isMoving;
        }

        layoutAnimationElapsed = 0f;
        layoutAnimationActive = hasMovement;
    }

    private void UpdateLayoutAnimation()
    {
        for (int index = 0; index < layoutChildren.Count; index++)
        {
            if (layoutChildren[index] == null ||
                !layoutChildren[index].gameObject.activeSelf)
            {
                RestoreAnimatedScales();
                layoutAnimationActive = false;
                RefreshLayout();
                return;
            }
        }

        layoutAnimationElapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(
            layoutAnimationElapsed / Mathf.Max(0.0001f, runtimeMoveDuration));
        float eased = progress * progress * (3f - 2f * progress);
        float scalePulse = Mathf.Sin(progress * Mathf.PI * 2f) *
                           runtimeScalePulse;
        for (int index = 0; index < layoutChildren.Count; index++)
        {
            layoutChildren[index].localPosition = Vector3.LerpUnclamped(
                layoutStartPositions[index],
                layoutTargetPositions[index],
                eased);
            RecordAppliedChildPosition(index);
            if (layoutControlsChildScale[index])
            {
                layoutChildren[index].localScale = layoutMovingChildren[index]
                    ? layoutStartScales[index] * (1f + scalePulse)
                    : layoutStartScales[index];
            }
        }

        if (progress < 1f)
            return;

        for (int index = 0; index < layoutChildren.Count; index++)
        {
            layoutChildren[index].localPosition = layoutTargetPositions[index];
            RecordAppliedChildPosition(index);
        }
        RestoreAnimatedScales();
        layoutAnimationActive = false;
    }

    private void RestoreAnimatedScales()
    {
        int count = Mathf.Min(
            layoutChildren.Count,
            Mathf.Min(layoutStartScales.Count, layoutControlsChildScale.Count));
        for (int index = 0; index < count; index++)
        {
            if (layoutChildren[index] != null && layoutControlsChildScale[index])
                layoutChildren[index].localScale = layoutStartScales[index];
        }
    }

    private void CollectActiveChildren()
    {
        layoutChildren.Clear();

        for (int layoutIndex = 0; layoutIndex < transform.childCount; layoutIndex++)
        {
            int i = reverseArrangement
                ? transform.childCount - 1 - layoutIndex
                : layoutIndex;
            Transform child = transform.GetChild(i);

            if (child.gameObject.activeSelf)
            {
                layoutChildren.Add(child);
            }
        }
    }

    private void BuildLayoutPositions()
    {
        layoutPositions.Clear();

        int childCount = layoutChildren.Count;
        int safeColumns = Mathf.Max(1, columns);
        float gridHorizontalSpacing = layoutType == LayoutType.Grid
            ? GetFittedHorizontalSpacing(gridSpacing.x, safeColumns)
            : gridSpacing.x;
        float lineHorizontalSpacing = layoutType == LayoutType.LineX
            ? GetFittedHorizontalSpacing(linearSpacing, childCount)
            : linearSpacing;

        for (int i = 0; i < childCount; i++)
        {
            Vector3 position;

            switch (layoutType)
            {
                case LayoutType.Grid:
                    int row = i / safeColumns;
                    int column = i % safeColumns;
                    position = new Vector3(column * gridHorizontalSpacing, 0f, row * gridSpacing.y);
                    break;

                case LayoutType.LineX:
                    position = new Vector3(i * lineHorizontalSpacing, 0f, 0f);
                    break;

                case LayoutType.LineY:
                    position = new Vector3(0f, i * linearSpacing, 0f);
                    break;

                case LayoutType.LineZ:
                    position = new Vector3(0f, 0f, i * linearSpacing);
                    break;

                case LayoutType.Circle:
                    float angle = childCount > 0 ? i * Mathf.PI * 2f / childCount : 0f;
                    position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    break;

                default:
                    position = Vector3.zero;
                    break;
            }

            layoutPositions.Add(position);
        }
    }

    private void ApplyAnchorOffset()
    {
        if (layoutPositions.Count == 0)
        {
            return;
        }

        Vector3 minimum = layoutPositions[0];
        Vector3 maximum = layoutPositions[0];
        minimum.x += childMinimumX[0];
        maximum.x += childMaximumX[0];

        for (int i = 1; i < layoutPositions.Count; i++)
        {
            Vector3 childMinimum = layoutPositions[i];
            Vector3 childMaximum = layoutPositions[i];
            childMinimum.x += childMinimumX[i];
            childMaximum.x += childMaximumX[i];
            minimum = Vector3.Min(minimum, childMinimum);
            maximum = Vector3.Max(maximum, childMaximum);
        }

        GetAnchorFactors(out float horizontalFactor, out float verticalFactor);

        float verticalMinimum;
        float verticalMaximum;

        if (layoutType == LayoutType.LineY)
        {
            verticalMinimum = minimum.y;
            verticalMaximum = maximum.y;
        }
        else
        {
            verticalMinimum = minimum.z;
            verticalMaximum = maximum.z;
        }

        minimum.x -= paddingLeft;
        maximum.x += paddingRight;
        verticalMinimum -= paddingBottom;
        verticalMaximum += paddingTop;

        Vector3 anchorPosition = new Vector3(
            Mathf.Lerp(minimum.x, maximum.x, horizontalFactor),
            layoutType == LayoutType.LineY
                ? Mathf.Lerp(verticalMinimum, verticalMaximum, verticalFactor)
                : 0f,
            layoutType == LayoutType.LineY
                ? 0f
                : Mathf.Lerp(verticalMinimum, verticalMaximum, verticalFactor));

        for (int i = 0; i < layoutPositions.Count; i++)
        {
            layoutPositions[i] -= anchorPosition;
        }
    }

    private void CaptureExternalChildOffsets()
    {
        for (int i = 0; i < layoutChildren.Count; i++)
        {
            ObjectLayoutOffset offset = layoutChildren[i].GetComponent<ObjectLayoutOffset>();
            if (offset != null && offset.isActiveAndEnabled)
                offset.CaptureExternalPositionChange();
        }
    }

    private void CapturePreservedCrossAxisOffsets()
    {
        if (!preserveCrossAxisOffsets)
            return;

        for (int i = 0; i < layoutChildren.Count; i++)
        {
            Transform child = layoutChildren[i];
            ObjectLayoutOffset explicitOffset = child.GetComponent<ObjectLayoutOffset>();
            if (explicitOffset != null && explicitOffset.isActiveAndEnabled)
                continue;

            if (!preservedCrossAxisOffsets.TryGetValue(child, out Vector3 preserved))
            {
                preservedCrossAxisOffsets[child] = ExtractCrossAxisPosition(child.localPosition);
                continue;
            }

            if (lastAppliedChildPositions.TryGetValue(child, out Vector3 lastApplied))
                preservedCrossAxisOffsets[child] = preserved +
                    ExtractCrossAxisPosition(child.localPosition - lastApplied);
        }
    }

    private void ApplyPreservedCrossAxisOffsets()
    {
        if (!preserveCrossAxisOffsets)
            return;

        for (int i = 0; i < layoutChildren.Count; i++)
        {
            Transform child = layoutChildren[i];
            ObjectLayoutOffset explicitOffset = child.GetComponent<ObjectLayoutOffset>();
            if ((explicitOffset == null || !explicitOffset.isActiveAndEnabled) &&
                preservedCrossAxisOffsets.TryGetValue(child, out Vector3 preserved))
            {
                layoutPositions[i] += preserved;
            }
        }
    }

    private Vector3 ExtractCrossAxisPosition(Vector3 position)
    {
        switch (layoutType)
        {
            case LayoutType.LineX:
                return new Vector3(0f, position.y, position.z);
            case LayoutType.LineY:
                return new Vector3(position.x, 0f, position.z);
            case LayoutType.LineZ:
                return new Vector3(position.x, position.y, 0f);
            default:
                return new Vector3(0f, position.y, 0f);
        }
    }

    private void RecordAppliedChildPosition(int index)
    {
        lastAppliedChildPositions[layoutChildren[index]] = layoutChildren[index].localPosition;
        ObjectLayoutOffset offset = layoutChildren[index].GetComponent<ObjectLayoutOffset>();
        if (offset != null && offset.isActiveAndEnabled)
            offset.RecordAppliedPosition(layoutChildren[index].localPosition);
    }

    private void ApplyChildOffsets()
    {
        for (int i = 0; i < layoutChildren.Count; i++)
        {
            ObjectLayoutOffset offset = layoutChildren[i].GetComponent<ObjectLayoutOffset>();
            if (offset != null && offset.isActiveAndEnabled)
                layoutPositions[i] += offset.PositionOffset;
        }
    }

    private void MeasureChildContent()
    {
        childMinimumX.Clear();
        childMaximumX.Clear();

        for (int i = 0; i < layoutChildren.Count; i++)
        {
            Transform child = layoutChildren[i];
            // A nested layout owns its internal bounds. Treat it as a fixed
            // anchor here so removing one of its cards cannot recenter the
            // parent layout and move every sibling column.
            if (child.GetComponent<ObjectLayoutGroup>() != null)
            {
                childMinimumX.Add(0f);
                childMaximumX.Add(0f);
                continue;
            }

            SpriteRenderer directRenderer = child.GetComponent<SpriteRenderer>();
            SpriteRenderer[] nestedRenderers = directRenderer == null
                ? child.GetComponentsInChildren<SpriteRenderer>(false)
                : null;
            int rendererCount = directRenderer != null ? 1 : nestedRenderers.Length;
            float minimum = 0f;
            float maximum = 0f;
            bool foundRenderer = false;

            for (int rendererIndex = 0; rendererIndex < rendererCount; rendererIndex++)
            {
                SpriteRenderer spriteRenderer = directRenderer != null
                    ? directRenderer
                    : nestedRenderers[rendererIndex];

                if (!spriteRenderer.enabled || spriteRenderer.sprite == null)
                {
                    continue;
                }

                Bounds bounds = spriteRenderer.localBounds;

                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 rendererPoint = new Vector3(
                                x == 0 ? bounds.min.x : bounds.max.x,
                                y == 0 ? bounds.min.y : bounds.max.y,
                                z == 0 ? bounds.min.z : bounds.max.z);
                            Vector3 worldPoint = spriteRenderer.transform.TransformPoint(rendererPoint);
                            float localX = transform.InverseTransformPoint(worldPoint).x - child.localPosition.x;

                            if (!foundRenderer)
                            {
                                minimum = localX;
                                maximum = localX;
                                foundRenderer = true;
                            }
                            else
                            {
                                minimum = Mathf.Min(minimum, localX);
                                maximum = Mathf.Max(maximum, localX);
                            }
                        }
                    }
                }
            }

            childMinimumX.Add(minimum);
            childMaximumX.Add(maximum);
        }
    }

    private float GetFittedHorizontalSpacing(float preferredSpacing, int itemsPerRow)
    {
        if (contentWidth <= 0f || layoutChildren.Count < 2 ||
            (layoutType != LayoutType.Grid && layoutType != LayoutType.LineX))
        {
            return preferredSpacing;
        }

        if (CalculateContentWidth(preferredSpacing, itemsPerRow) +
            paddingLeft + paddingRight <= contentWidth)
        {
            return preferredSpacing;
        }

        float direction = preferredSpacing < 0f ? -1f : 1f;
        float lowerMagnitude = 0f;
        float upperMagnitude = Mathf.Abs(preferredSpacing);

        for (int i = 0; i < 16; i++)
        {
            float middleMagnitude = (lowerMagnitude + upperMagnitude) * 0.5f;

            if (CalculateContentWidth(middleMagnitude * direction, itemsPerRow) +
                paddingLeft + paddingRight <= contentWidth)
            {
                lowerMagnitude = middleMagnitude;
            }
            else
            {
                upperMagnitude = middleMagnitude;
            }
        }

        return lowerMagnitude * direction;
    }

    private float CalculateContentWidth(float spacing, int itemsPerRow)
    {
        float minimum = float.PositiveInfinity;
        float maximum = float.NegativeInfinity;
        int safeItemsPerRow = Mathf.Max(1, itemsPerRow);

        for (int i = 0; i < layoutChildren.Count; i++)
        {
            int horizontalIndex = layoutType == LayoutType.Grid ? i % safeItemsPerRow : i;
            float center = horizontalIndex * spacing;
            minimum = Mathf.Min(minimum, center + childMinimumX[i]);
            maximum = Mathf.Max(maximum, center + childMaximumX[i]);
        }

        return maximum - minimum;
    }

    private void GetAnchorFactors(out float horizontal, out float vertical)
    {
        switch (startPoint)
        {
            case LayoutAnchor.UpperLeft:
                horizontal = 0f;
                vertical = 1f;
                break;
            case LayoutAnchor.UpperCenter:
                horizontal = 0.5f;
                vertical = 1f;
                break;
            case LayoutAnchor.UpperRight:
                horizontal = 1f;
                vertical = 1f;
                break;
            case LayoutAnchor.MiddleLeft:
                horizontal = 0f;
                vertical = 0.5f;
                break;
            case LayoutAnchor.MiddleCenter:
                horizontal = 0.5f;
                vertical = 0.5f;
                break;
            case LayoutAnchor.MiddleRight:
                horizontal = 1f;
                vertical = 0.5f;
                break;
            case LayoutAnchor.LowerLeft:
                horizontal = 0f;
                vertical = 0f;
                break;
            case LayoutAnchor.LowerCenter:
                horizontal = 0.5f;
                vertical = 0f;
                break;
            case LayoutAnchor.LowerRight:
                horizontal = 1f;
                vertical = 0f;
                break;
            default:
                horizontal = 0.5f;
                vertical = 0.5f;
                break;
        }
    }

    private int CalculateChildrenSignature()
    {
        unchecked
        {
            int signature = 17;
            signature = signature * 31 + transform.childCount;
            signature = signature * 31 + reverseArrangement.GetHashCode();

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                signature = signature * 31 + child.GetInstanceID();
                signature = signature * 31 + child.gameObject.activeSelf.GetHashCode();
                ObjectLayoutOffset offset = child.GetComponent<ObjectLayoutOffset>();
                signature = signature * 31 + (offset != null && offset.isActiveAndEnabled
                    ? offset.PositionOffset.GetHashCode()
                    : 0);
                if (offset != null && offset.isActiveAndEnabled)
                    signature = signature * 31 + child.localPosition.GetHashCode();
                else if (preserveCrossAxisOffsets)
                    signature = signature * 31 + ExtractCrossAxisPosition(child.localPosition).GetHashCode();
                // ItemColor owns its breathing/click pose. Including that pose
                // here makes auto-layout recenter it every frame and defeats
                // the bottom-anchored breathing compensation.

                // Descendant changes belong to the nested layout. Including
                // its renderers here makes the parent Columns group refresh
                // whenever a single color card is consumed.
                if (child.GetComponent<ObjectLayoutGroup>() != null)
                    continue;

                SpriteRenderer directRenderer = child.GetComponent<SpriteRenderer>();
                SpriteRenderer[] nestedRenderers = directRenderer == null
                    ? child.GetComponentsInChildren<SpriteRenderer>(true)
                    : null;
                int rendererCount = directRenderer != null ? 1 : nestedRenderers.Length;
                signature = signature * 31 + rendererCount;

                for (int rendererIndex = 0; rendererIndex < rendererCount; rendererIndex++)
                {
                    SpriteRenderer spriteRenderer = directRenderer != null
                        ? directRenderer
                        : nestedRenderers[rendererIndex];
                    signature = signature * 31 + spriteRenderer.GetInstanceID();
                    signature = signature * 31 + spriteRenderer.enabled.GetHashCode();
                    signature = signature * 31 + spriteRenderer.gameObject.activeInHierarchy.GetHashCode();
                    signature = signature * 31 + (spriteRenderer.sprite != null
                        ? spriteRenderer.sprite.GetInstanceID()
                        : 0);
                    // Runtime fill animation changes a tiled SpriteRenderer's bounds.
                    // It must not trigger a new layout pass: measuring that pass while
                    // the vacuum head is dragged would shift the complete vacuum row.
                }
            }

            return signature;
        }
    }
}
