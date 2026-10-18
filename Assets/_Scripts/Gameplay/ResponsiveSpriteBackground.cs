using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class ResponsiveSpriteBackground : MonoBehaviour
{
    private const float MinimumAspect = 0.0001f;

    [Header("Reference Device")]
    [Tooltip("Portrait resolution used when the background transform was authored.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1284f, 2778f);

    [Header("References")]
    [Tooltip("Camera rendering this background. Main Camera is used when left empty.")]
    [SerializeField] private Camera targetCamera;

    [Header("iPhone 12 Reference Transform")]
    [Tooltip("Local position authored for the reference resolution.")]
    [SerializeField] private Vector3 referenceLocalPosition;
    [Tooltip("Local scale authored for the reference resolution.")]
    [SerializeField] private Vector3 referenceLocalScale = Vector3.one;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private float lastCameraAspect = -1f;

    private void Reset()
    {
        CaptureCurrentTransformAsReference();
        targetCamera = Camera.main;
    }

    private void Awake()
    {
        // The scene transform is the authoritative authored baseline by
        // default. Keeping an older serialized reference here can make the
        // background jump in depth as play mode starts and cover the level.
        CaptureCurrentTransformAsReference();
    }

    private void OnEnable()
    {
        ApplyFit(true);
    }

    private void Update()
    {
        ApplyFit(false);
    }

    [ContextMenu("Capture Current Transform As iPhone 12 Reference")]
    private void CaptureCurrentTransformAsReference()
    {
        referenceLocalPosition = transform.localPosition;
        referenceLocalScale = transform.localScale;
    }

    [ContextMenu("Apply Background Fit")]
    private void ApplyBackgroundFit()
    {
        ApplyFit(true);
    }

    private void ApplyFit(bool force)
    {
        if (!TryGetCamera(out Camera backgroundCamera))
            return;

        float cameraAspect = backgroundCamera.aspect;
        if (!force &&
            lastScreenWidth == Screen.width &&
            lastScreenHeight == Screen.height &&
            Mathf.Approximately(lastCameraAspect, cameraAspect))
        {
            return;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCameraAspect = cameraAspect;

        float referenceAspect = referenceResolution.y > 0f
            ? referenceResolution.x / referenceResolution.y
            : MinimumAspect;
        referenceAspect = Mathf.Max(MinimumAspect, referenceAspect);

        float aspectRatio = Mathf.Max(MinimumAspect, cameraAspect) / referenceAspect;

        // The reference sprite already covers the tall iPhone 12 viewport. Wider
        // screens need a uniform increase so the sides remain covered; narrower
        // screens can keep the reference scale and crop horizontally.
        float coverScale = Mathf.Max(1f, aspectRatio);
        transform.localScale = referenceLocalScale * coverScale;

        // Keep the authored horizontal screen-space anchor as the viewport widens.
        // This is usually unchanged for a centered background, but also supports
        // backgrounds intentionally authored off-center.
        Transform parent = transform.parent;
        Vector3 referenceWorldPosition = parent != null
            ? parent.TransformPoint(referenceLocalPosition)
            : referenceLocalPosition;
        Vector3 cameraSpacePosition = backgroundCamera.transform.InverseTransformPoint(referenceWorldPosition);
        cameraSpacePosition.x *= aspectRatio;
        Vector3 adjustedWorldPosition = backgroundCamera.transform.TransformPoint(cameraSpacePosition);
        transform.localPosition = parent != null
            ? parent.InverseTransformPoint(adjustedWorldPosition)
            : adjustedWorldPosition;
    }

    private bool TryGetCamera(out Camera backgroundCamera)
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        backgroundCamera = targetCamera;
        return backgroundCamera != null;
    }
}
