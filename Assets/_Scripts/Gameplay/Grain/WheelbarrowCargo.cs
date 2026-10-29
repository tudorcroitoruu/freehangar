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
    private readonly RaycastHit[] groundHits = new RaycastHit[32];
    private readonly List<(Collider player, Collider barrow)> ignoredCollisions = new List<(Collider, Collider)>();
    private Bounds bodyBounds;
    private float handleDistance;
    private Transform[] wheels;
    private PlayerInteractor pusher;
    private float occupiedVolume;
    private PlayerController pushingPlayer;
    private bool instrumentWasActive, shovelWasEnabled;
    private readonly List<GameObject> hiddenTools = new List<GameObject>();

    public int UpgradeLevel => Upgrades != null && Upgrades.IsConfigured ? Upgrades.Level :
        SaveSystem.Data != null && SaveSystem.Data.wheelbarrowUpgradeLevels != null &&
        upgradeIndex < SaveSystem.Data.wheelbarrowUpgradeLevels.Length ?
        SaveSystem.Data.wheelbarrowUpgradeLevels[upgradeIndex] : 0;
    public float CapacityVolume => Upgrades != null && Upgrades.IsConfigured ? Upgrades.CapacityVolume : cargoVolumeCubicMeters + .4f * UpgradeLevel;
    public float OccupiedVolume => occupiedVolume;
    public int BagCount => bags.Count;
    public string UpgradeId => "wheelbarrow." + upgradeIndex;
    private VehicleUpgrade Upgrades => GetComponent<VehicleUpgrade>();
    public int CargoSlotCount => Upgrades != null && Upgrades.IsConfigured ? Upgrades.SlotCount :
        Mathf.Max(1, Mathf.FloorToInt(traySize.z / .25f) * 2);

    private void Awake()
    {
        if (GetComponent<VehicleUpgrade>() == null) gameObject.AddComponent<VehicleUpgrade>();
        RebuildCargo();
        var found = new List<Transform>(2);
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child != transform && (child.name.EndsWith("_Wheel", System.StringComparison.Ordinal) ||
                child.name.Contains("_Wheel_")) &&
                child.GetComponent<MeshFilter>() != null) found.Add(child);
        wheels = found.ToArray();
        var bodyCollider = GetComponent<MeshCollider>();
        bodyBounds = bodyCollider != null && bodyCollider.sharedMesh != null ? bodyCollider.sharedMesh.bounds :
            new Bounds(trayCenter, traySize + new Vector3(.15f,.2f,.3f));
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
        return occupiedVolume + volume <= CapacityVolume + .0001f && bags.Count < CargoSlotCount;
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
            action = "Release wheelbarrow";
        }
        else if (interactor.Bags != null && interactor.Bags.IsCarrying)
        {
            available = CanLoad(interactor.Bags);
            action = available ? "Load bag" : "Wheelbarrow full";
        }
        else action = interactor.Items != null && interactor.Items.IsCarrying ? "Put down item and push" : "Push wheelbarrow";
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
        if (interactor.Items != null && interactor.Items.IsCarrying && !interactor.Items.TryPlace()) return false;
        pusher = interactor;
        var controller = interactor.GetComponent<CharacterController>();
        float playerRadius = controller != null ? controller.radius * Mathf.Abs(interactor.transform.lossyScale.x) : .3f;
        handleDistance = Mathf.Max(pushDistance, -bodyBounds.min.z * Mathf.Abs(transform.lossyScale.z) + playerRadius + .12f);
        foreach (var playerCollider in interactor.GetComponentsInChildren<Collider>(true))
            foreach (var barrowCollider in GetComponentsInChildren<Collider>(true))
            {
                if (playerCollider == barrowCollider || Physics.GetIgnoreCollision(playerCollider,barrowCollider)) continue;
                Physics.IgnoreCollision(playerCollider,barrowCollider,true);
                ignoredCollisions.Add((playerCollider,barrowCollider));
            }
        // Attach to the handles once; subsequent movement is committed as a single pair.
        Vector3 previousPosition = interactor.transform.position;
        Vector3 handle = transform.position - transform.forward * handleDistance;
        handle.y = previousPosition.y;
        if (controller != null) controller.Move(handle - previousPosition);
        else interactor.transform.position = handle;
        if (Vector3.ProjectOnPlane(interactor.transform.position - handle, Vector3.up).sqrMagnitude > .0025f)
        {
            interactor.transform.position = previousPosition;
            StopPushing();
            return false;
        }
        interactor.transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        interactor.PushedWheelbarrow = this;
        pushingPlayer = interactor.GetComponent<PlayerController>();
        if (pushingPlayer != null)
        {
            instrumentWasActive = pushingPlayer.HeldInstrument != null && pushingPlayer.HeldInstrument.gameObject.activeSelf;
            shovelWasEnabled = pushingPlayer.Shovel != null && pushingPlayer.Shovel.enabled;
            if (pushingPlayer.Shovel != null) { pushingPlayer.Shovel.CancelAction(); pushingPlayer.Shovel.enabled = false; }
            if (pushingPlayer.HeldInstrument != null) pushingPlayer.HeldInstrument.gameObject.SetActive(false);
            pushingPlayer.ResetLook();
        }
        var tools = interactor.GetComponent<PlayerCombatTools>();
        if (tools != null) tools.HideForPushing(hiddenTools);
        return true;
    }

    public bool TryUpgrade(PlayerInteractor interactor)
    {
        return pusher == interactor && interactor != null && Upgrades != null && Upgrades.OpenMenu();
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
    }

    // The player and barrow advance together in short collision-checked steps, including turns.
    public void Move(Vector2 input, float speed, float verticalSpeed, float dt)
    {
        if (pusher == null || dt <= 0) return;
        var controller = pusher.GetComponent<CharacterController>();
        if (controller == null || !controller.enabled) return;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(input.y * speed * dt) / .06f,
            Mathf.Abs(input.x * 80f * dt) / 3f)));
        float step = dt / steps;
        for (int n = 0; n < steps; n++)
        {
            Quaternion rotation = Quaternion.Euler(0, transform.eulerAngles.y + input.x * 80f * step, 0);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 target = transform.position + forward * (input.y * speed * step);
            if (!CanMoveTo(ref target, rotation)) break;
            Vector3 previous = pusher.transform.position;
            Vector3 handle = target - forward * handleDistance;
            handle.y = previous.y;
            controller.Move(handle - previous + Vector3.up * (verticalSpeed * step));
            // Sliding against a wall must stop the barrow too, rather than breaking the handle offset.
            if (Vector3.ProjectOnPlane(pusher.transform.position - handle, Vector3.up).sqrMagnitude > .0004f)
            {
                pusher.transform.position = previous;
                Physics.SyncTransforms();
                break;
            }
            Vector3 oldPosition = transform.position;
            transform.SetPositionAndRotation(target, rotation);
            pusher.transform.rotation = rotation;
            float roll = Vector3.Dot(target - oldPosition, forward) / (.29f * Mathf.Abs(transform.lossyScale.y)) * Mathf.Rad2Deg;
            foreach (Transform wheel in wheels) if (wheel != null) wheel.Rotate(Vector3.right, roll, Space.Self);
            Physics.SyncTransforms();
        }
    }

    private bool CanMoveTo(ref Vector3 target, Quaternion rotation)
    {
        int groundCount = Physics.RaycastNonAlloc(target + Vector3.up * 2f, Vector3.down, groundHits, 5f, ~0, QueryTriggerInteraction.Ignore);
        if (groundCount == groundHits.Length) return false;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < groundCount; i++)
        {
            var hit = groundHits[i];
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(pusher.transform) ||
                hit.normal.y <= .65f || hit.distance >= nearest) continue;
            nearest = hit.distance; target.y = hit.point.y + .02f;
        }
        if (float.IsPositiveInfinity(nearest) || Mathf.Abs(target.y - transform.position.y) > .25f) return false;
        Vector3 scale = transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 center = target + rotation * Vector3.Scale(bodyBounds.center, scale);
        Vector3 extents = Vector3.Scale(bodyBounds.extents, scale);
        extents.y = Mathf.Max(.01f, extents.y - .03f);
        int count = Physics.OverlapBoxNonAlloc(center, extents, blockers, rotation, ~0, QueryTriggerInteraction.Ignore);
        if (count == blockers.Length) return false;
        for (int i = 0; i < count; i++)
            if (!blockers[i].transform.IsChildOf(transform) && !blockers[i].transform.IsChildOf(pusher.transform)) return false;
        return true;
    }

    private void StopPushing()
    {
        foreach (var pair in ignoredCollisions)
            if (pair.player != null && pair.barrow != null) Physics.IgnoreCollision(pair.player,pair.barrow,false);
        ignoredCollisions.Clear();
        if (pusher != null && pusher.PushedWheelbarrow == this) pusher.PushedWheelbarrow = null;
        if (pushingPlayer != null)
        {
            if (pushingPlayer.HeldInstrument != null) pushingPlayer.HeldInstrument.gameObject.SetActive(instrumentWasActive);
            if (pushingPlayer.Shovel != null) pushingPlayer.Shovel.enabled = shovelWasEnabled;
            pushingPlayer.ResetLook();
        }
        foreach (var tool in hiddenTools) if (tool != null) tool.SetActive(true);
        hiddenTools.Clear();
        pushingPlayer = null;
        pusher = null;
    }

    private void OnDisable() => StopPushing();
}
