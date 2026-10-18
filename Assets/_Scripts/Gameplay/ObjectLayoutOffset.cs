using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Layout/Object Layout Offset")]
public sealed class ObjectLayoutOffset : MonoBehaviour
{
    [Tooltip("Local position added after the parent ObjectLayoutGroup calculates this child's layout position. Moving the child Transform directly updates this value automatically.")]
    [SerializeField] private Vector3 positionOffset;
    private Vector3 lastAppliedLocalPosition;
    private bool hasAppliedPosition;

    public Vector3 PositionOffset
    {
        get => positionOffset;
        set
        {
            if (positionOffset == value)
                return;

            positionOffset = value;
            RefreshParentLayout();
        }
    }

    private void OnEnable() => RefreshParentLayout();

    private void OnDisable() => RefreshParentLayout();

    private void OnValidate() => RefreshParentLayout();

    private void OnTransformParentChanged() => RefreshParentLayout();

    internal void CaptureExternalPositionChange()
    {
        if (!hasAppliedPosition)
            return;

        Vector3 change = transform.localPosition - lastAppliedLocalPosition;
        if (change.sqrMagnitude <= 0.000001f)
            return;

        positionOffset += change;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    internal void RecordAppliedPosition(Vector3 localPosition)
    {
        lastAppliedLocalPosition = localPosition;
        hasAppliedPosition = true;
    }

    [ContextMenu("Reset Position Offset")]
    private void ResetPositionOffset()
    {
        PositionOffset = Vector3.zero;
    }

    private void RefreshParentLayout()
    {
        if (transform.parent != null &&
            transform.parent.TryGetComponent(out ObjectLayoutGroup layoutGroup))
        {
            layoutGroup.RefreshLayout();
        }
    }
}
