using System.Collections.Generic;
using UnityEngine;

/// <summary>Physical bag cargo and on-foot pushing for the three farm wheelbarrows.</summary>
[DisallowMultipleComponent]
public sealed class WheelbarrowCargo : MonoBehaviour, IInteractable
{
    [SerializeField, Min(.01f)] private float cargoVolumeCubicMeters = .4f;
    [SerializeField, Range(0, 2)] private int upgradeIndex;
    [SerializeField, Min(1)] private int upgradePrice = 250;
    [SerializeField] private Vector3 trayCenter = new Vector3(0f, .46f, -.72f);
    [SerializeField] private Vector3 traySize = new Vector3(.7f, .35f, .85f);
    [SerializeField, Min(.5f)] private float pushDistance = 1.4f;
    private readonly List<GrainContainer> bags = new List<GrainContainer>(6);
    private readonly List<float> bagVolumes = new List<float>(6);
    private readonly List<Transform> slots = new List<Transform>(6);
    private readonly Collider[] blockers = new Collider[32];
    private Transform[] wheels;
    private PlayerInteractor pusher;
    private float occupiedVolume;

    public int UpgradeLevel => SaveSystem.Data != null && SaveSystem.Data.wheelbarrowUpgradeLevels != null &&
        upgradeIndex < SaveSystem.Data.wheelbarrowUpgradeLevels.Length ?
        SaveSystem.Data.wheelbarrowUpgradeLevels[upgradeIndex] : 0;
    public float CapacityVolume => cargoVolumeCubicMeters + .4f * UpgradeLevel;
    public float OccupiedVolume => occupiedVolume;
    public int BagCount => bags.Count;

    private void Awake()
    {
        RebuildCargo();
        var found = new List<Transform>(2);
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child != transform && child.name.Contains("_Wheel") &&
                child.GetComponent<MeshFilter>() != null) found.Add(child);
        wheels = found.ToArray();
    }

    private void RebuildCargo()
    {
        bags.Clear(); bagVolumes.Clear(); slots.Clear(); occupiedVolume = 0f;
        foreach (GrainContainer bag in GetComponentsInChildren<GrainContainer>(true))
        {
            if (bag.transform == transform) continue;
            bags.Add(bag);
            float volume = Volume(bag);
            bagVolumes.Add(volume);
            slots.Add(bag.transform.parent);
            occupiedVolume += volume;
        }
    }

    private static float Volume(Vector3 size) => Mathf.Max(.001f, size.x * size.y * size.z);
    private static float Volume(GrainContainer bag)
    {
        var box = bag.GetComponent<BoxCollider>();
        if (box == null) return Volume(bag.WorldBounds.size);
        Vector3 scale = bag.transform.lossyScale;
        return Mathf.Max(.001f, Mathf.Abs(box.size.x * scale.x * box.size.y * scale.y * box.size.z * scale.z));
    }

    public void NotifyBagTaken(GrainContainer bag)
    {
        int index = bags.IndexOf(bag);
        if (index < 0) return;
        occupiedVolume = Mathf.Max(0f, occupiedVolume - bagVolumes[index]);
        if (slots[index] != null && slots[index].name.StartsWith("Bag slot ") && slots[index].childCount == 0)
            Destroy(slots[index].gameObject);
        bags.RemoveAt(index); bagVolumes.RemoveAt(index); slots.RemoveAt(index);
    }

    public bool CanLoad(BagInteraction carrier)
    {
        if (carrier == null || !carrier.IsCarrying || carrier.CarriedBag == null || pusher != null) return false;
        float volume = carrier.CarriedVolume;
        return occupiedVolume + volume <= CapacityVolume + .0001f &&
            bags.Count < Mathf.Max(1, Mathf.FloorToInt(traySize.z / .25f) * 2);
    }

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        if (!isActiveAndEnabled || interactor == null) return false;
        if (pusher != null && pusher != interactor) return false;
        string action;
        bool available = true;
        if (pusher == interactor)
        {
            string price = SaveSystem.Data != null && SaveSystem.Data.freeUpgradeVouchers > 0 ? "FREE" : "$" + upgradePrice;
            action = UpgradeLevel > 0 ? "Release wheelbarrow" : "Release wheelbarrow  |  U: upgrade " + price;
        }
        else if (interactor.Bags != null && interactor.Bags.IsCarrying)
        {
            available = CanLoad(interactor.Bags);
            action = available ? "Load bag" : "Wheelbarrow full";
        }
        else action = "Push wheelbarrow";
        prompt = new InteractionPromptData(transform.TransformPoint(trayCenter + Vector3.up * .35f),
            action + $"  ({occupiedVolume:0.00}/{CapacityVolume:0.00} m³)", available);
        return true;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || interactor == null || Time.timeScale <= 0f) return false;
        if (pusher == interactor) { StopPushing(); return true; }
        if (pusher != null) return false;
        if (interactor.Bags != null && interactor.Bags.IsCarrying)
        {
            if (!CanLoad(interactor.Bags)) return false;
            GrainContainer bag = interactor.Bags.CarriedBag;
            float volume = interactor.Bags.CarriedVolume;
            GameObject anchor = new GameObject("Bag slot " + (bags.Count + 1));
            anchor.transform.SetParent(transform, false);
            int index = bags.Count;
            anchor.transform.localPosition = trayCenter + new Vector3(0f, index * .32f,
                (index % 2 == 0 ? -.06f : .06f));
            if (!interactor.Bags.TryStow(anchor.transform)) { Destroy(anchor); return false; }
            bags.Add(bag); bagVolumes.Add(volume); slots.Add(anchor.transform); occupiedVolume += volume;
            return true;
        }
        if (interactor.GetComponent<PlayerBicycleInteraction>() is PlayerBicycleInteraction vehicle && vehicle.IsRiding)
            return false;
        pusher = interactor;
        interactor.PushedWheelbarrow = this;
        return true;
    }

    public bool TryUpgrade(PlayerInteractor interactor)
    {
        if (pusher != interactor || UpgradeLevel > 0 || SaveSystem.Data == null) return false;
        bool purchased = SaveSystem.TryPurchaseUpgrade(upgradePrice, data =>
        {
            if (data.wheelbarrowUpgradeLevels == null || data.wheelbarrowUpgradeLevels.Length < 3)
                System.Array.Resize(ref data.wheelbarrowUpgradeLevels, 3);
            if (data.wheelbarrowUpgradeLevels[upgradeIndex] > 0) return false;
            data.wheelbarrowUpgradeLevels[upgradeIndex] = 1;
            return true;
        }, out _);
        if (purchased) GoalChecker.ReportValue(GoalType.UpgradePurchased, 1,
            upgradeType: GoalUpgradeType.Wheelbarrow);
        return purchased;
    }

    private void LateUpdate()
    {
        for (int i = bags.Count - 1; i >= 0; i--)
        {
            if (bags[i] != null && bags[i].transform.IsChildOf(transform)) continue;
            NotifyBagTaken(bags[i]);
        }
        if (pusher == null) return;
        if (!pusher.isActiveAndEnabled || !pusher.gameObject.activeInHierarchy ||
            Vector3.Distance(pusher.transform.position, transform.position) > 3.5f)
        { StopPushing(); return; }
        Vector3 forward = Vector3.ProjectOnPlane(pusher.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .01f) return;
        Vector3 target = pusher.transform.position + forward * pushDistance;
        Vector3 origin = target + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore) &&
            !hit.collider.transform.IsChildOf(transform) && hit.normal.y > .65f)
            target.y = hit.point.y + .02f;
        else target.y = transform.position.y;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 center = target + rotation * new Vector3(0f, .4f, -.7f);
        int count = Physics.OverlapBoxNonAlloc(center, new Vector3(.4f, .35f, .8f), blockers,
            rotation, ~0, QueryTriggerInteraction.Ignore);
        if (count == blockers.Length) return;
        for (int i = 0; i < count; i++)
            if (!blockers[i].transform.IsChildOf(transform) && !blockers[i].transform.IsChildOf(pusher.transform)) return;
        Vector3 previous = transform.position;
        transform.SetPositionAndRotation(Vector3.MoveTowards(transform.position, target, 3.5f * Time.deltaTime),
            Quaternion.RotateTowards(transform.rotation, rotation, 150f * Time.deltaTime));
        float roll = Vector3.Distance(previous, transform.position) / (.29f * transform.lossyScale.y) * Mathf.Rad2Deg;
        foreach (Transform wheel in wheels) if (wheel != null) wheel.Rotate(Vector3.right, roll, Space.Self);
    }

    private void StopPushing()
    {
        if (pusher != null && pusher.PushedWheelbarrow == this) pusher.PushedWheelbarrow = null;
        pusher = null;
    }

    private void OnDisable() => StopPushing();
}
