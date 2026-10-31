using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SellingZone : MonoBehaviour
{
    [SerializeField] private GrainElementSettings[] allGrainsSettings;
    [SerializeField] private SellingScreen sellingScreen;
    private BoxCollider area;
    private readonly HashSet<BicycleController> parkedBicycles = new HashSet<BicycleController>();

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
        FindParkedBicycles();
        return parkedBicycles.Count > 0;
    }

    public bool TryOpen(PlayerInteractor interactor)
    {
        if (!CanWeigh(interactor)) return false;
        if (sellingScreen == null) sellingScreen = FindFirstObjectByType<SellingScreen>(FindObjectsInactive.Include);
        return sellingScreen != null && sellingScreen.Open(this, interactor);
    }

    public bool TryGetSettings(GrainType grain, out GrainElementSettings settings)
    {
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
        FindParkedBicycles();
        // Include all cargo on parked vehicles, including containers whose colliders are disabled.
        foreach (var bicycle in parkedBicycles)
            foreach (var container in bicycle.GetComponentsInChildren<GrainContainer>())
                AddContainer(containers, container);
        foreach (var hit in Overlaps())
            AddContainer(containers, hit.GetComponentInParent<GrainContainer>());
    }

    private void AddContainer(List<GrainContainer> containers, GrainContainer container)
    {
        if (container != null && container.isActiveAndEnabled && !container.IsCarried && !container.IsClosing &&
            float.IsFinite(container.Kilograms) && container.Kilograms > 0 &&
            TryGetSettings(container.AcceptedGrain, out _) && !containers.Contains(container))
            containers.Add(container);
    }

    private void FindParkedBicycles()
    {
        parkedBicycles.Clear();
        foreach (var hit in Overlaps())
        {
            var bicycle = hit.GetComponentInParent<BicycleController>();
            if (bicycle == null || !bicycle.isActiveAndEnabled || bicycle.IsRidden || bicycle.IsCoasting || Mathf.Abs(bicycle.Speed) > .01f) continue;
            var controller = bicycle.GetComponent<CharacterController>();
            Vector3 point = controller != null ? controller.bounds.center : bicycle.transform.position;
            Vector3 local = transform.InverseTransformPoint(point) - area.center;
            Vector3 half = area.size * .5f;
            if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z)
                parkedBicycles.Add(bicycle);
        }
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
