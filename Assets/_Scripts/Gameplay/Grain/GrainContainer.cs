using UnityEngine;

/// <summary>Shared capacity logic for sacks, boxes and barrels. One grain type for this prototype.</summary>
[DisallowMultipleComponent]
public sealed class GrainContainer : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float capacityKg = 50f;
    [SerializeField, Min(0f)] private float kilograms;
    [SerializeField] private SackController sack;
    [SerializeField] private bool fitFillCollider;
    [SerializeField] private Bounds emptyBounds;
    [SerializeField] private Bounds fullBounds;
    [SerializeField] private Bounds closedBounds;
    [SerializeField, Min(.1f)] private float closeDuration = .65f;
    private BoxCollider fillCollider;
    public float Kilograms => kilograms;
    public float CapacityKg => capacityKg;
    public bool IsOpen => sack == null || sack.Close < .05f;
    public bool IsFull => kilograms >= capacityKg - .001f;
    public bool IsFinished => sack != null && sack.Close >= .999f;
    public bool IsClosing { get; private set; }
    public bool IsCarried { get; private set; }
    public bool CanHandle => isActiveAndEnabled && sack != null && IsFull && !IsCarried;
    public Bounds WorldBounds
    {
        get
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return new Bounds(transform.position, Vector3.one * .5f);
            Bounds result = default;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = box.center + Vector3.Scale(box.size * .5f,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = transform.TransformPoint(corner);
                if (i == 0) result = new Bounds(point, Vector3.zero); else result.Encapsulate(point);
            }
            return result;
        }
    }
    public Vector3 PourPoint
    {
        get
        {
            var target = GetComponent<Collider>();
            return target != null ? new Vector3(target.bounds.center.x, target.bounds.max.y - .04f, target.bounds.center.z) : transform.position;
        }
    }

    private void Awake()
    {
        if (sack == null) sack = GetComponent<SackController>();
        fillCollider = GetComponent<BoxCollider>();
        kilograms = Mathf.Clamp(kilograms, 0, capacityKg);
        ApplyFill();
    }

    public float Add(float amountKg)
    {
        if (!isActiveAndEnabled || IsClosing || IsCarried || !IsOpen || !(amountKg > 0) || float.IsInfinity(amountKg)) return 0;
        float accepted = Mathf.Min(amountKg, Mathf.Max(0, capacityKg - kilograms));
        kilograms += accepted;
        ApplyFill();
        return accepted;
    }

    public bool Finish()
    {
        if (!isActiveAndEnabled || !CanHandle || IsFinished || IsClosing) return false;
        IsClosing = true;
        return true;
    }

    public void SetCarried(bool carried) => IsCarried = carried;

    private void Update()
    {
        if (!IsClosing || sack == null || Time.deltaTime <= 0) return;
        sack.Close = Mathf.MoveTowards(sack.Close, 1f, Time.deltaTime / closeDuration);
        ApplyFill();
        if (IsFinished) IsClosing = false;
    }

    private void ApplyFill()
    {
        if (sack != null) sack.Fill = kilograms / capacityKg;
        if (fitFillCollider && fillCollider != null)
        {
            float fill = kilograms / capacityKg;
            Bounds bounds = new Bounds(Vector3.Lerp(emptyBounds.center, fullBounds.center, fill),
                Vector3.Lerp(emptyBounds.size, fullBounds.size, fill));
            float closure = sack != null && closedBounds.size.sqrMagnitude > .0001f ? sack.Close : 0;
            fillCollider.center = Vector3.Lerp(bounds.center, closedBounds.center, closure);
            fillCollider.size = Vector3.Lerp(bounds.size, closedBounds.size, closure);
        }
    }
}
