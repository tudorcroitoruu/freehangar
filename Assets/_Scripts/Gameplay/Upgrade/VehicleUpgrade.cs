using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared saved cargo upgrades for wheelbarrows and vehicles using BicycleController.</summary>
[DisallowMultipleComponent]
public sealed class VehicleUpgrade : MonoBehaviour
{
    [SerializeField] private VehicleUpgradeSettings settings;
    [Tooltip("Stable settings/save ID. Leave empty to detect the existing vehicle type. New vehicle types can use their own ID.")]
    [SerializeField] private string vehicleId;
    [Tooltip("Optional authored positions for extra cargo slots. Missing positions are generated beside the original rack.")]
    [SerializeField] private Transform[] additionalCargoSlots;
    private Transform[] originalSlots;
    private readonly List<Transform> generatedSlots = new List<Transform>();
    private BicycleController bicycle;
    private WheelbarrowCargo wheelbarrow;

    public string VehicleId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(vehicleId)) return vehicleId;
            var barrow = GetComponent<WheelbarrowCargo>();
            if (barrow != null) return barrow.UpgradeId;
            if (GetComponent<PickupController>() != null) return "pickup";
            if (GetComponent<TrikeController>() != null) return "trike";
            if (GetComponent<LawnmowerController>() != null) return "lawnmower";
            if (GetComponent<MotorollerController>() != null) return "motoroller";
            return "bicycle";
        }
    }
    public VehicleUpgradeSettings.VehicleType Profile
    {
        get
        {
            if (settings == null) settings = Resources.Load<VehicleUpgradeSettings>(VehicleUpgradeSettings.ResourcePath);
            return settings != null ? settings.Find(VehicleId) : null;
        }
    }
    public bool IsConfigured => Profile != null && Profile.IsConfigured;
    public int Level => IsConfigured ? Mathf.Clamp(SaveSystem.Data?.GetVehicleUpgradeLevel(VehicleId) ?? 0, 0, Profile.MaxLevel) : 0;
    public float CapacityVolume => IsConfigured ? Profile.At(Level).capacityCubicMeters : float.PositiveInfinity;
    public int SlotCount => IsConfigured ? Profile.At(Level).slots : int.MaxValue;
    public bool IsMaxed => IsConfigured && Level >= Profile.MaxLevel;
    public int Price => IsConfigured && !IsMaxed ? Profile.At(Level).price : 0;
    public bool UsesVoucher => Price > 0 && (SaveSystem.Data?.freeUpgradeVouchers ?? 0) > 0;

    private void Awake()
    {
        bicycle = GetComponent<BicycleController>();
        wheelbarrow = GetComponent<WheelbarrowCargo>();
        if (bicycle != null) originalSlots = bicycle.cargoSlots ?? new Transform[0];
    }
    private void OnEnable()
    {
        SaveSystem.MoneyChanged += RefreshSlots;
        RefreshSlots(SaveSystem.Money);
    }
    private void OnDisable() => SaveSystem.MoneyChanged -= RefreshSlots;

    private void RefreshSlots(int _)
    {
        if (bicycle == null || !IsConfigured) return;
        if (originalSlots == null) originalSlots = bicycle.cargoSlots ?? new Transform[0];
        var slots = new List<Transform>();
        foreach (var slot in originalSlots) if (slot != null && !slots.Contains(slot)) slots.Add(slot);
        if (additionalCargoSlots != null)
            foreach (var slot in additionalCargoSlots) if (slot != null && !slots.Contains(slot)) slots.Add(slot);
        int authoredCount = slots.Count;
        int needed = Profile.At(Profile.MaxLevel).slots;
        Transform origin = slots.Count > 0 ? slots[0] : transform;
        while (slots.Count < needed)
        {
            int index = slots.Count - authoredCount;
            if (index >= generatedSlots.Count)
            {
                var slot = new GameObject("Upgrade cargo slot " + (slots.Count + 1)).transform;
                slot.SetParent(origin != transform ? origin.parent : transform, false);
                Vector3 baseline = origin != transform ? origin.localPosition : Vector3.up * .6f;
                slot.localPosition = baseline + new Vector3((index % 2 == 0 ? 1 : -1) * .65f, 0, (index / 2 + 1) * .65f);
                slot.localRotation = origin != transform ? origin.localRotation : Quaternion.identity;
                generatedSlots.Add(slot);
            }
            slots.Add(generatedSlots[index]);
        }
        bicycle.cargoSlots = slots.ToArray();
    }

    private HashSet<Transform> CargoRoots()
    {
        var roots = new HashSet<Transform>();
        var rig = bicycle != null ? bicycle : GetComponent<BicycleController>();
        if (rig == null) return roots;
        if (rig.cargoSlots != null)
            foreach (var slot in rig.cargoSlots)
                if (slot != null) foreach (Transform child in slot) roots.Add(child);
        if (rig.exclusiveBagSlot != null)
            foreach (Transform child in rig.exclusiveBagSlot) roots.Add(child);
        return roots;
    }
    public float OccupiedVolume
    {
        get
        {
            var barrow = wheelbarrow != null ? wheelbarrow : GetComponent<WheelbarrowCargo>();
            if (barrow != null) return barrow.OccupiedVolume;
            float volume = 0;
            foreach (var root in CargoRoots()) volume += MeasureVolume(root.gameObject);
            return volume;
        }
    }
    public int CargoCount => GetComponent<WheelbarrowCargo>() is WheelbarrowCargo barrow ? barrow.BagCount : CargoRoots().Count;
    public bool CanLoadVolume(float volume) => !IsConfigured ||
        (float.IsFinite(volume) && volume > 0 && CargoCount < SlotCount && OccupiedVolume + volume <= CapacityVolume + .0001f);

    public static float MeasureVolume(GameObject item)
    {
        var box = item.GetComponent<BoxCollider>();
        if (box != null)
        {
            Vector3 size = Vector3.Scale(box.size, item.transform.lossyScale);
            return Mathf.Max(.001f, Mathf.Abs(size.x * size.y * size.z));
        }
        Bounds bounds = new Bounds();
        bool found = false;
        foreach (var renderer in item.GetComponentsInChildren<Renderer>())
        {
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return Mathf.Max(.001f, bounds.size.x * bounds.size.y * bounds.size.z);
    }

    public bool TryUpgrade()
    {
        if (!isActiveAndEnabled || !IsConfigured || IsMaxed || SaveSystem.Data == null) return false;
        int level = SaveSystem.Data.GetVehicleUpgradeLevel(VehicleId);
        bool purchased = SaveSystem.TryPurchaseVehicleUpgrade(VehicleId, Price, level, Profile.MaxLevel);
        if (purchased && GetComponent<WheelbarrowCargo>() != null)
            GoalChecker.ReportValue(GoalType.UpgradePurchased, 1, upgradeType: GoalUpgradeType.Wheelbarrow);
        return purchased;
    }
    public bool OpenMenu()
    {
        var manager = FindFirstObjectByType<UpgradeManager>(FindObjectsInactive.Include);
        return manager != null && manager.OpenVehicle(this);
    }
}
