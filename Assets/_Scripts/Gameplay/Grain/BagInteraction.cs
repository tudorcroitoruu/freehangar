using UnityEngine;

/// <summary>Finish sacks and carry portable grain containers at any fill level.</summary>
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
    private bool IsShovelEquipped => GetComponent<PlayerController>() is PlayerController player ?
        player.IsShovelEquipped : shovel != null && shovel.isActiveAndEnabled;
    public string PromptAction => carried != null ? "Drop" : target == null ? "" : target.IsClosing ? "Closing…" :
        IsShovelEquipped && target.CanHandle && !target.IsFinished ? "Finish" :
        target.CanCarry ? "Take" : target.IsFull ? "Full" : "Fill";

    private void LateUpdate() => RestoreShovelIfNeeded();

    public bool TryGetPrompt(GrainContainer bag, out InteractionPromptData data)
    {
        data = default;
        if (!isActiveAndEnabled || bag == null || !bag.isActiveAndEnabled ||
            bag.IsClosing || (bag.IsCarried && bag != carried) || (shovel != null && shovel.IsBusy)) return false;
        if (carried == null) target = bag;
        Bounds bounds = bag.WorldBounds;
        Vector3 position = bag == carried ? bounds.center : new Vector3(bounds.center.x, bounds.max.y + .12f, bounds.center.z);
        bool blocked = Time.time < blockedUntil;
        bool shovelEquipped = IsShovelEquipped;
        bool canFinish = shovelEquipped && bag.CanHandle && !bag.IsFinished;
        bool canCarry = bag.CanCarry || bag == carried;
        bool canPour = shovelEquipped && shovel != null && bag != carried && shovel.CanPourInto(bag);
        string action = canPour ? "Fill" : bag == carried ? "Drop" : canFinish ? "Finish" : canCarry ? "Take" :
            bag.IsFull ? "Full" : "Fill";
        data = new InteractionPromptData(position, blocked ? "Make room to drop" : action,
            !canPour && (canFinish || canCarry) && !blocked);
        data.ShowProgress = !bag.IsFull;
        data.ShowPrimaryButton = canPour;
        var interactor = GetComponent<PlayerInteractor>();
        data.RequiresFreeHands = bag != carried && bag.CanCarry && !canFinish && !data.ShowPrimaryButton &&
            interactor != null && interactor.HandsOccupied;
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
            carried != null || (shovel != null && shovel.IsBusy) || bag == null || bag.IsClosing ||
            (GetComponent<PlayerItemCarry>() is PlayerItemCarry items && items.IsCarrying)) return false;
        target = bag;
        if (IsShovelEquipped && target.CanHandle && !target.IsFinished) return target.Finish();
        if (!target.CanCarry) return false;
        if (IsShovelEquipped)
        {
            var player = GetComponent<PlayerController>();
            if (player == null || !player.ToggleShovel()) return false;
        }
        return Take(target);
    }

    private bool Take(GrainContainer bag)
    {
        if (!bag.CanCarry || IsShovelEquipped) return false;
        var sourceWheelbarrow = bag.GetComponentInParent<WheelbarrowCargo>();
        carried = bag;
        originalParent = bag.GetComponentInParent<BicycleController>() != null ||
            sourceWheelbarrow != null ? null : bag.transform.parent;
        originalScale = bag.transform.localScale;
        originalWorldScale = bag.transform.lossyScale;
        worldExtents = bag.WorldBounds.extents;
        var box = bag.GetComponent<BoxCollider>();
        localCenter = box != null ? box.center : bag.transform.InverseTransformPoint(bag.WorldBounds.center);
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
        if (shovel != null) shovel.CancelAction();
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

    // Roll back inventory delivery without leaving a destroyed object in the carrier.
    public void CancelTake(GrainContainer bag)
    {
        if (carried == null || carried != bag) return;
        Release(carried.WorldBounds.center);
    }

    public bool TryStow(Transform slot)
    {
        if (carried == null || slot == null) return false;
        var upgrades = slot.GetComponentInParent<VehicleUpgrade>();
        var rack = slot.GetComponentInParent<VehicleCargoRack>();
        if (upgrades != null && !upgrades.CanLoadVolume(carriedVolume,rack != null ? 2 : 1)) return false;
        var bag = carried;
        bag.transform.SetParent(slot, true);
        bag.transform.localScale = new Vector3(originalWorldScale.x / slot.lossyScale.x, originalWorldScale.y / slot.lossyScale.y, originalWorldScale.z / slot.lossyScale.z);
        bag.transform.rotation = Quaternion.AngleAxis(slot.eulerAngles.y, Vector3.up) * uprightRotation;
        bag.transform.position += slot.position - bag.WorldBounds.center;
        if (rack != null) VehicleCargoRack.Seat(bag.transform,slot);
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
            // Preserve the bag's height: a width-sized sphere clips the floor for flat/empty bags.
            count = Physics.BoxCastNonAlloc(start, half, delta.normalized, castHits, Quaternion.identity,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
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
