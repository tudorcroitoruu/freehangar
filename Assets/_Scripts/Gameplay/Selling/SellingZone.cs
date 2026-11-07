using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SellingZone : MonoBehaviour
{
    [SerializeField, Tooltip("Legacy fallback used only when Economy Settings is not assigned.")]
    private GrainElementSettings[] allGrainsSettings;
    [SerializeField] private SellingScreen sellingScreen;
    [SerializeField] private GrainEconomySettings economySettings;
    private BoxCollider area;
    private readonly List<GrainContainer> weighableContainers = new List<GrainContainer>();
    public bool HasVehicleOnScale
    {
        get
        {
            foreach (var hit in Overlaps()) if (IsVehicleCollider(hit)) return true;
            return false;
        }
    }
    public bool HasParkedVehicle => HasVehicleOnScale;
    public bool HasBlockingOccupants
    {
        get
        {
            foreach (var hit in Overlaps())
            {
                if (IsVehicleCollider(hit) || hit is CharacterController ||
                    hit.GetComponentInParent<PlayerController>() != null || hit.GetComponentInParent<PlayerInteractor>() != null) return true;
                var container = hit.GetComponentInParent<GrainContainer>();
                if (container != null)
                {
                    if (!IsUnloaded(container)) return true;
                    continue;
                }
                // Trailers and other movable objects must leave the scale as well.
                if (!hit.isTrigger && hit.attachedRigidbody != null) return true;
            }
            return false;
        }
    }

    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
    private void Awake()
    {
        area = GetComponent<BoxCollider>();
        if (GetComponent<WeighDeckSpring>() == null) gameObject.AddComponent<WeighDeckSpring>();
    }
    private void OnDisable()
    {
        if (sellingScreen != null && sellingScreen.Zone == this) sellingScreen.Close();
    }

    public bool Contains(PlayerInteractor player)
    {
        if (!isActiveAndEnabled || player == null) return false;
        if (area == null) area = GetComponent<BoxCollider>();
        if (area == null || !area.enabled) return false;
        var controller = player.GetComponent<CharacterController>();
        Vector3 point = controller != null ? controller.bounds.center : player.transform.position;
        Vector3 local = transform.InverseTransformPoint(point) - area.center;
        Vector3 half = area.size * .5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    public bool CanWeigh(PlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || interactor == null || interactor.HandsOccupied || Contains(interactor)) return false;
        var rider = interactor.GetComponent<PlayerBicycleInteraction>();
        if (rider != null && rider.IsRiding) return false;
        if (HasBlockingOccupants) return false;
        CollectContainers(weighableContainers);
        return weighableContainers.Count > 0;
    }

    public bool TryOpen(PlayerInteractor interactor)
    {
        if (!CanWeigh(interactor)) return false;
        if (sellingScreen == null) sellingScreen = FindFirstObjectByType<SellingScreen>(FindObjectsInactive.Include);
        return sellingScreen != null && sellingScreen.Open(this, interactor);
    }

    public bool TryGetSettings(GrainType grain, out GrainElementSettings settings)
    {
        if (economySettings != null)
            return economySettings.TryGetGrain(grain, out settings) && settings.grainPrice > 0;
        if (allGrainsSettings != null)
            foreach (var entry in allGrainsSettings)
                if (entry != null && entry.grainType == grain && float.IsFinite(entry.grainPrice) && entry.grainPrice > 0)
                { settings = entry; return true; }
        settings = null;
        return false;
    }

    public void CollectContainers(List<GrainContainer> containers)
    {
        containers.Clear();
        foreach (var hit in Overlaps())
            AddContainer(containers, hit.GetComponentInParent<GrainContainer>());
    }

    private void AddContainer(List<GrainContainer> containers, GrainContainer container)
    {
        if (container != null && container.isActiveAndEnabled && IsUnloaded(container) && !container.IsClosing &&
            float.IsFinite(container.Kilograms) && container.Kilograms > 0 &&
            TryGetSettings(container.AcceptedGrain, out _) && !containers.Contains(container))
            containers.Add(container);
    }

    private static bool IsVehicleCollider(Collider hit) =>
        hit.GetComponentInParent<BicycleController>() != null || hit.GetComponentInParent<WheelbarrowCargo>() != null;

    private static bool IsUnloaded(GrainContainer container)
    {
        if (container.IsCarried || container.GetComponentInParent<BicycleController>() != null ||
            container.GetComponentInParent<WheelbarrowCargo>() != null) return false;
        // Trailer slots can be siblings of the vehicle controller in the prefab.
        foreach (var vehicle in FindObjectsByType<BicycleController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (vehicle.HasCargo(container.transform)) return false;
        return true;
    }

    private Collider[] Overlaps()
    {
        if (area == null) area = GetComponent<BoxCollider>();
        if (!isActiveAndEnabled || area == null || !area.enabled) return System.Array.Empty<Collider>();
        Vector3 scale = transform.lossyScale;
        Vector3 half = Vector3.Scale(area.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        return Physics.OverlapBox(transform.TransformPoint(area.center), half, transform.rotation, ~0, QueryTriggerInteraction.Collide);
    }
}

/// <summary>Small visual suspension response; weighing and collision geometry stay authoritative.</summary>
[DisallowMultipleComponent]
public sealed class WeighDeckSpring : MonoBehaviour
{
    private readonly Collider[] occupants = new Collider[128];
    private readonly HashSet<Object> unique = new HashSet<Object>();
    private readonly List<Transform> moving = new List<Transform>();
    private readonly List<Vector3> rest = new List<Vector3>();
    private BoxCollider deck;
    private MeshRenderer originalRenderer;
    private GameObject visualDeck;
    private float displacement, velocity, load, pollAt;

    private void Start()
    {
        foreach (var node in transform.root.GetComponentsInChildren<Transform>(true))
            if (node.name == "Inset weighing deck") { deck = node.GetComponent<BoxCollider>(); break; }
        if (deck == null) { enabled = false; return; }
        originalRenderer = deck.GetComponent<MeshRenderer>();
        var filter = deck.GetComponent<MeshFilter>();
        if (originalRenderer == null || filter == null) { enabled = false; return; }
        visualDeck = new GameObject("Suspended deck visual");
        visualDeck.transform.SetParent(deck.transform, false);
        visualDeck.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        var renderer = visualDeck.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = originalRenderer.sharedMaterials;
        originalRenderer.enabled = false;
        moving.Add(visualDeck.transform);
        foreach (Transform node in deck.transform.parent)
            if (node.name == "Deck fastener" || node.name == "Deck instruction") moving.Add(node);
        foreach (var node in moving) rest.Add(node.localPosition);
    }

    private void FixedUpdate()
    {
        if (deck == null || WorldSaveRuntime.IsRestoring || WorldSaveRuntime.IsSeeking) return;
        if (Time.time >= pollAt)
        {
            pollAt = Time.time + .08f;
            var bounds = deck.bounds;
            Vector3 center = new Vector3(bounds.center.x, bounds.max.y + .6f, bounds.center.z);
            int count = Physics.OverlapBoxNonAlloc(center,
                new Vector3(bounds.extents.x, .65f, bounds.extents.z), occupants,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            unique.Clear();
            float nextLoad = 0f;
            for (int i = 0; i < count; i++)
            {
                var hit = occupants[i];
                var player = hit.GetComponentInParent<PlayerController>();
                var bag = hit.GetComponentInParent<GrainContainer>();
                // Count a dropped bag when it reaches the deck, not while held above it.
                if (player == null && bag != null &&
                    (bag.IsCarried || bag.WorldBounds.min.y > bounds.max.y + .12f ||
                     bag.GetComponentInParent<PlayerController>() != null)) continue;
                Object owner = player != null ? (Object)player : bag;
                if (owner == null || !unique.Add(owner)) continue;
                nextLoad += player != null ? 1f : .7f;
            }
            if (nextLoad > load) velocity -= Mathf.Min(.065f, (nextLoad - load) * .035f);
            load = nextLoad;
        }
        float dt = Time.fixedDeltaTime;
        float target = -Mathf.Min(.012f, load * .003f);
        // Substeps keep the damped spring stable at larger physics timesteps.
        int steps = Mathf.Max(1, Mathf.CeilToInt(dt / .01f));
        float step = dt / steps;
        for (int i = 0; i < steps; i++)
        {
            velocity += ((target - displacement) * 180f - velocity * 12f) * step;
            displacement = Mathf.Clamp(displacement + velocity * step, -.025f, .008f);
        }
        for (int i = 0; i < moving.Count; i++)
            if (moving[i] != null)
                moving[i].localPosition = rest[i] + moving[i].parent.InverseTransformVector(Vector3.up * displacement);
    }

    private void OnDisable()
    {
        for (int i = 0; i < moving.Count; i++) if (moving[i] != null) moving[i].localPosition = rest[i];
        displacement = velocity = load = 0f;
    }

    private void OnDestroy()
    {
        if (originalRenderer != null) originalRenderer.enabled = true;
        if (visualDeck != null) Destroy(visualDeck);
    }
}
