using UnityEngine;

/// <summary>Finish, carry and drop the same filled bag; contents remain owned by GrainContainer.</summary>
[DefaultExecutionOrder(200), DisallowMultipleComponent]
public sealed class BagInteraction : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private ShovelTool shovel;
    [SerializeField] private Transform shovelPresentation;
    [SerializeField] private Transform carryAnchor;
    [SerializeField, Min(.2f)] private float carriedHeight = .75f;
    private GrainContainer target, carried;
    private Transform originalParent;
    private Vector3 originalScale, originalWorldScale, worldExtents, localCenter;
    private float carriedVolume;
    private Quaternion uprightRotation;
    private Rigidbody carriedBody;
    private Collider[] carriedColliders;
    private bool[] colliderStates;
    private bool shovelWasActive;
    private float blockedUntil;
    private readonly RaycastHit[] castHits = new RaycastHit[32];
    private readonly Collider[] overlaps = new Collider[32];
    public bool IsCarrying => carried != null;
    public GrainContainer CarriedBag => carried;
    public float CarriedVolume => carriedVolume;
    public InteractionPrompt Prompt => GameplayCanvas.Instance != null ? GameplayCanvas.Instance.interactionPrompt : null;
    public string PromptAction => carried != null ? "Drop" : target == null ? "" : target.IsClosing ? "Closing…" : target.IsFinished ? "Take" : "Finish";

    private void LateUpdate() => RestoreShovelIfNeeded();

    public bool TryGetPrompt(GrainContainer bag, out InteractionPromptData data)
    {
        data = default;
        if (!isActiveAndEnabled || bag == null || !bag.isActiveAndEnabled || bag.GetComponent<SackController>() == null ||
            bag.IsClosing || (bag.IsCarried && bag != carried) || (bag.IsFull && shovel != null && shovel.IsBusy)) return false;
        if (carried == null) target = bag;
        Bounds bounds = bag.WorldBounds;
        Vector3 position = bag == carried ? bounds.center : new Vector3(bounds.center.x, bounds.max.y + .12f, bounds.center.z);
        bool blocked = Time.time < blockedUntil;
        string action = bag == carried ? "Drop" : bag.IsFinished ? "Take" : "Finish";
        data = new InteractionPromptData(position, blocked ? "Make room to drop" : bag.IsFull ? action : "Fill",
            bag.IsFull && !blocked);
        data.ShowPrimaryButton = data.ShowProgress = !bag.IsFull;
        data.Progress = bag.CapacityKg > 0f ? Mathf.Clamp01(bag.Kilograms / bag.CapacityKg) : 0f;
        data.ProgressText = $"{bag.Kilograms:0.##} / {bag.CapacityKg:0.##} kg";
        return true;
    }

    public bool TryInteract()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0 || viewCamera == null || carryAnchor == null) return false;
        if (carried != null) return Drop();
        if (shovel == null || shovel.IsBusy) return false;
        target = shovel.FindBagForHandling(viewCamera, transform);
        return TryInteract(target);
    }

    public bool TryInteract(GrainContainer bag)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0 || viewCamera == null || carryAnchor == null ||
            carried != null || shovel == null || shovel.IsBusy || bag == null || !bag.CanHandle || bag.IsClosing) return false;
        target = bag;
        if (!target.IsFinished) return target.Finish();
        return Take(target);
    }

    private bool Take(GrainContainer bag)
    {
        if (!bag.CanHandle || !bag.IsFinished) return false;
        var sourceWheelbarrow = bag.GetComponentInParent<WheelbarrowCargo>();
        carried = bag;
        originalParent = bag.GetComponentInParent<BicycleController>() != null ||
            sourceWheelbarrow != null ? null : bag.transform.parent;
        originalScale = bag.transform.localScale;
        originalWorldScale = bag.transform.lossyScale;
        worldExtents = bag.WorldBounds.extents;
        var box = bag.GetComponent<BoxCollider>();
        localCenter = box != null ? box.center : Vector3.zero;
        carriedVolume = box != null ? Mathf.Abs(box.size.x * originalWorldScale.x *
            box.size.y * originalWorldScale.y * box.size.z * originalWorldScale.z) :
            bag.WorldBounds.size.x * bag.WorldBounds.size.y * bag.WorldBounds.size.z;
        uprightRotation = Quaternion.FromToRotation(bag.transform.InverseTransformDirection(Vector3.up), Vector3.up);
        carriedBody = bag.GetComponent<Rigidbody>();
        if (carriedBody == null) carriedBody = bag.gameObject.AddComponent<Rigidbody>();
        if (!carriedBody.isKinematic) { carriedBody.linearVelocity = Vector3.zero; carriedBody.angularVelocity = Vector3.zero; }
        carriedBody.isKinematic = true;
        carriedColliders = bag.GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[carriedColliders.Length];
        for (int i = 0; i < carriedColliders.Length; i++) { colliderStates[i] = carriedColliders[i].enabled; carriedColliders[i].enabled = false; }
        bag.SetCarried(true);
        shovel.CancelAction();
        shovelWasActive = shovelPresentation != null && shovelPresentation.gameObject.activeSelf;
        if (shovelPresentation != null) shovelPresentation.gameObject.SetActive(false);
        bag.transform.SetParent(carryAnchor, false);
        if (sourceWheelbarrow != null) sourceWheelbarrow.NotifyBagTaken(bag);
        float factor = Mathf.Min(1f, carriedHeight / Mathf.Max(.1f, worldExtents.y * 2f));
        Vector3 parentScale = carryAnchor.lossyScale;
        bag.transform.localScale = new Vector3(originalWorldScale.x / parentScale.x, originalWorldScale.y / parentScale.y, originalWorldScale.z / parentScale.z) * factor;
        bag.transform.localRotation = uprightRotation;
        bag.transform.localPosition = -(uprightRotation * Vector3.Scale(localCenter, bag.transform.localScale));
        target = null;
        return true;
    }

    public bool TryStow(Transform slot)
    {
        if (carried == null || slot == null) return false;
        var bag = carried;
        bag.transform.SetParent(slot, true);
        bag.transform.localScale = new Vector3(originalWorldScale.x / slot.lossyScale.x, originalWorldScale.y / slot.lossyScale.y, originalWorldScale.z / slot.lossyScale.z);
        bag.transform.rotation = Quaternion.AngleAxis(slot.eulerAngles.y, Vector3.up) * uprightRotation;
        bag.transform.position += slot.position - bag.WorldBounds.center;
        for (int i = 0; i < carriedColliders.Length; i++) if (carriedColliders[i] != null) carriedColliders[i].enabled = colliderStates[i];
        carriedBody.isKinematic = true;
        bag.SetCarried(false);
        carried = null; target = null;
        RestoreShovelIfNeeded();
        Physics.SyncTransforms();
        return true;
    }

    private bool Drop()
    {
        if (!FindDropPosition(out Vector3 center)) { blockedUntil = Time.time + 1f; return false; }
        Release(center);
        return true;
    }

    private void Release(Vector3 center)
    {
        var bag = carried;
        bag.transform.SetParent(originalParent, false);
        bag.transform.localScale = originalScale;
        bag.transform.rotation = Quaternion.AngleAxis(transform.eulerAngles.y, Vector3.up) * uprightRotation;
        bag.transform.position += center - bag.transform.TransformPoint(localCenter);
        for (int i = 0; i < carriedColliders.Length; i++)
            if (carriedColliders[i] != null) carriedColliders[i].enabled = colliderStates[i];
        bag.SetCarried(false);
        carriedBody.mass = Mathf.Max(1f, bag.Kilograms + 1f);
        carriedBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        carriedBody.useGravity = true;
        carriedBody.isKinematic = false;
        carriedBody.linearVelocity = Vector3.zero;
        carriedBody.angularVelocity = Vector3.zero;
        carried = null; target = null;
        RestoreShovelIfNeeded();
        Physics.SyncTransforms();
    }

    private bool FindDropPosition(out Vector3 center)
    {
        Vector3 forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .1f) forward = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        // A conservative upright box supports both imported sack sizes at any yaw.
        float radius = new Vector2(worldExtents.x, worldExtents.z).magnitude;
        Vector3 half = new Vector3(radius, worldExtents.y, radius);
        for (int option = 0; option < 3; option++)
        {
            Vector3 candidate = transform.position + forward * (1f + radius) + right * (option == 0 ? 0 : option == 1 ? .65f : -.65f);
            Vector3 origin = new Vector3(candidate.x, viewCamera.transform.position.y + .5f, candidate.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, castHits, 5f, ~0, QueryTriggerInteraction.Ignore);
            if (count == castHits.Length) continue;
            float nearest = float.PositiveInfinity; Vector3 ground = default; bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (Ignore(castHits[i].collider) || castHits[i].distance >= nearest || castHits[i].normal.y < .65f) continue;
                nearest = castHits[i].distance; ground = castHits[i].point; found = true;
            }
            if (!found) continue;
            center = ground + Vector3.up * (half.y + .12f);
            count = Physics.OverlapBoxNonAlloc(center, half, overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            bool clear = count < overlaps.Length;
            for (int i = 0; i < count && clear; i++) if (!Ignore(overlaps[i])) clear = false;
            if (!clear) continue;
            // Check the path too, so a clear spot behind a wall is not considered reachable.
            Vector3 start = viewCamera.transform.position;
            Vector3 delta = center - start;
            count = Physics.SphereCastNonAlloc(start, radius, delta.normalized, castHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            clear = count < castHits.Length;
            for (int i = 0; i < count && clear; i++) if (!Ignore(castHits[i].collider)) clear = false;
            if (clear) return true;
        }
        center = default;
        return false;
    }

    private bool Ignore(Collider collider) => collider == null || collider.transform.IsChildOf(transform) ||
        (carried != null && collider.transform.IsChildOf(carried.transform));

    private void RestoreShovelIfNeeded()
    {
        if (!shovelWasActive || carried != null) return;
        if (shovelPresentation != null) shovelPresentation.gameObject.SetActive(true);
        shovelWasActive = false;
    }

    private void OnDisable()
    {
        var prompt = Prompt;
        if (prompt != null) prompt.Hide();
        if (carried != null)
        {
            // Disabling the interaction must never leave the bag parented to a disappearing player.
            Vector3 center = carried.WorldBounds.center;
            if (FindDropPosition(out Vector3 safe)) center = safe;
            Release(center);
        }
        RestoreShovelIfNeeded();
    }
}
