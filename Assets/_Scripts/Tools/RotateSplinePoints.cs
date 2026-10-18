using UnityEngine;
using UnityEngine.Splines;

[ExecuteAlways]
public class RotateSplinePoints : MonoBehaviour
{
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private float angleDegrees = 90f;
    [SerializeField] private Vector3 localPivot = Vector3.zero;
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;

    [ContextMenu("Rotate Spline Points")]
    public void Rotate()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();

        if (splineContainer == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RecordObject(splineContainer, "Rotate Conveyor Spline");
#endif
        Vector3 axis = rotationAxis.sqrMagnitude > 0f ? rotationAxis.normalized : Vector3.forward;
        Quaternion rotation = Quaternion.AngleAxis(angleDegrees, axis);

        foreach (Spline spline in splineContainer.Splines)
        {
            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];

                Vector3 pos = knot.Position;
                pos = localPivot + rotation * (pos - localPivot);

                knot.Position = pos;
                knot.Rotation = rotation * knot.Rotation;

                spline[i] = knot;
            }
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(splineContainer);
#endif
    }
}

