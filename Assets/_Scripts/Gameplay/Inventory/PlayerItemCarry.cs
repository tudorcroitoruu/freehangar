using UnityEngine;

/// <summary>Carries an inventory prefab and places it back in the world with E.</summary>
[DisallowMultipleComponent]
public sealed class PlayerItemCarry : MonoBehaviour
{
    [SerializeField] private Transform carryAnchor;
    [SerializeField, Min(.1f)] private float carriedHeight = .75f;
    private PlayerController player;
    private PlayerInteractor interactor;
    private PlayerBicycleInteraction bicycle;
    private Camera view;
    private GameObject carried;
    private Vector3 worldScale, extents, localCenter;
    private Quaternion originalRotation;
    private Collider[] colliders;
    private bool[] colliderStates;
    private Rigidbody[] bodies;
    private bool[] kinematicStates;
    private GrainContainer[] containers;
    private bool instrumentWasActive;
    private float blockedUntil;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Collider[] overlaps = new Collider[32];

    public bool IsCarrying => carried != null;
    public GameObject CarriedObject => carried;
    public bool CanTake => isActiveAndEnabled && player != null && player.isActiveAndEnabled &&
        view != null && !IsCarrying && (player.Shovel == null || !player.Shovel.IsBusy) && (bicycle == null || !bicycle.IsRiding) &&
        (interactor == null || (interactor.PushedWheelbarrow == null &&
            (interactor.Bags == null || !interactor.Bags.IsCarrying)));

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        interactor = GetComponent<PlayerInteractor>();
        bicycle = GetComponent<PlayerBicycleInteraction>();
        view = player != null ? player.characterCamera : GetComponentInChildren<Camera>();
    }

    public bool TryTake(GameObject item)
    {
        if (!CanTake || item == null || Time.timeScale <= 0f ||
            (player.Shovel != null && player.Shovel.IsBusy)) return false;
        var carryable = item.GetComponent<CarryableItem>();
        if (carryable != null && !carryable.CanBeCarried) return false;
        var itemContainers = item.GetComponentsInChildren<GrainContainer>(true);
        foreach (var container in itemContainers)
            if (!container.CanCarry) return false;
        if (carryAnchor == null)
        {
            carryAnchor = new GameObject("Inventory carry anchor").transform;
            carryAnchor.SetParent(view.transform, false);
            carryAnchor.localPosition = new Vector3(.35f, -.35f, 1.05f);
        }
        Vector3 anchorScale = carryAnchor.lossyScale;
        if (Mathf.Abs(anchorScale.x * anchorScale.y * anchorScale.z) < .000001f) return false;
        bool restoreShovel = player.IsShovelEquipped;
        if (restoreShovel && !player.ToggleShovel()) return false;

        carried = item;
        instrumentWasActive = restoreShovel;
        worldScale = item.transform.lossyScale;
        originalRotation = item.transform.rotation;
        Bounds bounds = new Bounds(item.transform.position, Vector3.one * .5f);
        bool found = false;
        foreach (var renderer in item.GetComponentsInChildren<Renderer>())
        {
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        extents = bounds.extents;
        localCenter = item.transform.InverseTransformPoint(bounds.center);
        colliders = item.GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderStates[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }
        bodies = item.GetComponentsInChildren<Rigidbody>(true);
        kinematicStates = new bool[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            kinematicStates[i] = bodies[i].isKinematic;
            if (!bodies[i].isKinematic) { bodies[i].linearVelocity = Vector3.zero; bodies[i].angularVelocity = Vector3.zero; }
            bodies[i].isKinematic = true;
        }
        containers = itemContainers;
        foreach (var container in containers) container.SetCarried(true);
        if (player.Shovel != null) player.Shovel.CancelAction();
        if (player.HeldInstrument != null) player.HeldInstrument.gameObject.SetActive(false);
        item.transform.SetParent(carryAnchor, false);
        float scale = Mathf.Min(1f, carriedHeight / Mathf.Max(.1f, bounds.size.y));
        item.transform.localScale = new Vector3(worldScale.x / anchorScale.x, worldScale.y / anchorScale.y, worldScale.z / anchorScale.z) * scale;
        item.transform.localRotation = originalRotation;
        item.transform.localPosition = -(item.transform.localRotation * Vector3.Scale(localCenter, item.transform.localScale));
        return true;
    }

    public bool TryGetPrompt(out InteractionPromptData prompt)
    {
        prompt = default;
        if (!IsCarrying) return false;
        bool blocked = Time.time < blockedUntil;
        prompt = new InteractionPromptData(carried.transform.TransformPoint(localCenter),
            blocked ? "Make room to place" : "Place", !blocked);
        return true;
    }

    public bool TryPlace()
    {
        if (!isActiveAndEnabled || !IsCarrying || Time.timeScale <= 0f || view == null) return false;
        if (!FindPosition(out var center)) { blockedUntil = Time.time + 1f; return false; }
        Release(center);
        return true;
    }

    // Roll back a delivery that did not consume stock.
    public void CancelTake(GameObject item)
    {
        if (carried != item || !IsCarrying) return;
        Release(carried.transform.TransformPoint(localCenter));
    }

    private void Release(Vector3 center)
    {
        carried.transform.SetParent(null, true);
        carried.transform.localScale = worldScale;
        carried.transform.rotation = Quaternion.AngleAxis(transform.eulerAngles.y, Vector3.up) * originalRotation;
        carried.transform.position += center - carried.transform.TransformPoint(localCenter);
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
        foreach (var container in containers) if (container != null) container.SetCarried(false);
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != null) bodies[i].isKinematic = kinematicStates[i];
        carried = null;
        RestoreInstrument();
        Physics.SyncTransforms();
    }

    private bool FindPosition(out Vector3 center)
    {
        Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .1f) forward = transform.forward;
        float radius = new Vector2(extents.x, extents.z).magnitude;
        Vector3 half = new Vector3(radius, extents.y, radius);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        for (int option = 0; option < 3; option++)
        {
            Vector3 point = transform.position + forward * (1f + radius) + right * (option == 0 ? 0 : option == 1 ? .65f : -.65f);
            Vector3 origin = new Vector3(point.x, view.transform.position.y + .5f, point.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, 5f, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) continue;
            float nearest = float.PositiveInfinity;
            Vector3 ground = default;
            for (int i = 0; i < count; i++)
            {
                if (Ignore(hits[i].collider) || hits[i].normal.y < .65f || hits[i].distance >= nearest) continue;
                nearest = hits[i].distance;
                ground = hits[i].point;
            }
            if (float.IsPositiveInfinity(nearest)) continue;
            center = ground + Vector3.up * (half.y + .12f);
            count = Physics.OverlapBoxNonAlloc(center, half, overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            bool clear = count < overlaps.Length;
            for (int i = 0; i < count && clear; i++) if (!Ignore(overlaps[i])) clear = false;
            if (!clear) continue;
            Vector3 delta = center - view.transform.position;
            // A sphere based on width would intersect the floor when the item is short and wide.
            count = Physics.BoxCastNonAlloc(view.transform.position, half, delta.normalized, hits, Quaternion.identity,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            clear = count < hits.Length;
            for (int i = 0; i < count && clear; i++) if (!Ignore(hits[i].collider)) clear = false;
            if (clear) return true;
        }
        center = default;
        return false;
    }

    private bool Ignore(Collider collider) => collider == null || collider.transform.IsChildOf(transform) ||
        (carried != null && collider.transform.IsChildOf(carried.transform));

    private void RestoreInstrument()
    {
        if (IsCarrying || !instrumentWasActive) return;
        if (player == null || !player.isActiveAndEnabled || player.IsCarrying) return;
        if (player.IsShovelEquipped || player.ToggleShovel()) instrumentWasActive = false;
    }

    private void LateUpdate() => RestoreInstrument();
    private void OnDisable()
    {
        if (IsCarrying)
        {
            Vector3 center = carried.transform.TransformPoint(localCenter);
            if (view != null && FindPosition(out var safe)) center = safe;
            Release(center);
        }
        RestoreInstrument();
    }
}
