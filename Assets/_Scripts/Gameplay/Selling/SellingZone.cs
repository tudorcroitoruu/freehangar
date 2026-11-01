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
    private void Awake() => area = GetComponent<BoxCollider>();
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
        var box = container.GetComponentInParent<BicycleCargoBox>();
        if (box != null && box.IsLoaded) return false;
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
