using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class RectTransformOverlay : MonoBehaviour
{
    [SerializeField] private List<GameObject> targets = new();
    [SerializeField] private Vector2 padding;
    [SerializeField] private bool includeInactive;
    [SerializeField] private bool updateEveryFrame = true;
    [SerializeField] private Camera worldCamera;

    private readonly Vector3[] worldCorners = new Vector3[4];
    private RectTransform rectTransform;

    private void OnEnable()
    {
        CacheRectTransform();
        ResizeToTargets();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || updateEveryFrame)
            ResizeToTargets();
    }

    private void OnValidate()
    {
        padding.x = Mathf.Max(0f, padding.x);
        padding.y = Mathf.Max(0f, padding.y);

        CacheRectTransform();
        ResizeToTargets();
    }

    public void SetTargets(List<GameObject> targets)
    {
        this.targets = targets;
        ResizeToTargets();
    }

    [ContextMenu("Resize To Targets")]
    public void ResizeToTargets()
    {
        CacheRectTransform();

        RectTransform parent = rectTransform.parent as RectTransform;
        if (parent == null)
            return;

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.rootCanvas.worldCamera
            : null;

        bool hasBounds = false;
        Vector2 minimum = Vector2.zero;
        Vector2 maximum = Vector2.zero;

        foreach (GameObject target in targets)
        {
            if (target == null || target == gameObject)
                continue;

            if (!includeInactive && !target.activeInHierarchy)
                continue;

            if (target.transform is RectTransform targetRect)
            {
                Canvas targetCanvas = targetRect.GetComponentInParent<Canvas>();
                Camera targetCamera = targetCanvas != null && targetCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? targetCanvas.rootCanvas.worldCamera
                    : null;

                targetRect.GetWorldCorners(worldCorners);
                for (int i = 0; i < worldCorners.Length; i++)
                {
                    Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, worldCorners[i]);
                    AddScreenPoint(screenPoint, parent, canvasCamera, ref hasBounds, ref minimum, ref maximum);
                }

                continue;
            }

            Camera camera = worldCamera != null ? worldCamera : Camera.main;
            if (camera == null)
                continue;

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(includeInactive);
            if (renderers.Length > 0)
            {
                foreach (Renderer targetRenderer in renderers)
                {
                    AddWorldBounds(targetRenderer.bounds, camera, parent, canvasCamera,
                        ref hasBounds, ref minimum, ref maximum);
                }

                continue;
            }

            Collider[] colliders = target.GetComponentsInChildren<Collider>(includeInactive);
            foreach (Collider targetCollider in colliders)
            {
                AddWorldBounds(targetCollider.bounds, camera, parent, canvasCamera,
                    ref hasBounds, ref minimum, ref maximum);
            }

            Collider2D[] colliders2D = target.GetComponentsInChildren<Collider2D>(includeInactive);
            foreach (Collider2D targetCollider in colliders2D)
            {
                AddWorldBounds(targetCollider.bounds, camera, parent, canvasCamera,
                    ref hasBounds, ref minimum, ref maximum);
            }
        }

        if (!hasBounds)
            return;

        minimum -= padding;
        maximum += padding;

        Rect parentRect = parent.rect;
        Vector2 anchorMinimum = parentRect.min + Vector2.Scale(parentRect.size, rectTransform.anchorMin);
        Vector2 anchorMaximum = parentRect.min + Vector2.Scale(parentRect.size, rectTransform.anchorMax);

        rectTransform.offsetMin = minimum - anchorMinimum;
        rectTransform.offsetMax = maximum - anchorMaximum;
    }

    public void SetTarget(GameObject target)
    {
        targets.Clear();

        if (target != null)
            targets.Add(target);

        ResizeToTargets();
    }

    public void ClearTargets()
        => targets.Clear();

    private static void AddWorldBounds(
        Bounds bounds,
        Camera camera,
        RectTransform parent,
        Camera canvasCamera,
        ref bool hasBounds,
        ref Vector2 minimum,
        ref Vector2 maximum)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Vector3 worldPoint = new(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z);
                    Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);

                    if (screenPoint.z > 0f)
                    {
                        AddScreenPoint(screenPoint, parent, canvasCamera,
                            ref hasBounds, ref minimum, ref maximum);
                    }
                }
            }
        }
    }

    private static void AddScreenPoint(
        Vector2 screenPoint,
        RectTransform parent,
        Camera canvasCamera,
        ref bool hasBounds,
        ref Vector2 minimum,
        ref Vector2 maximum)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, canvasCamera, out Vector2 localPoint))
            return;

        if (!hasBounds)
        {
            minimum = localPoint;
            maximum = localPoint;
            hasBounds = true;
            return;
        }

        minimum = Vector2.Min(minimum, localPoint);
        maximum = Vector2.Max(maximum, localPoint);
    }

    private void CacheRectTransform()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
    }
}
