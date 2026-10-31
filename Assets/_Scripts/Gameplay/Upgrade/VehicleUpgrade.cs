using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Saved cargo upgrades for vehicles using BicycleController.</summary>
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
    public VehicleUpgrade Replacement { get; private set; }
    public static event Action<VehicleUpgrade, VehicleUpgrade> VehicleReplaced;

    public string VehicleId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(vehicleId)) return vehicleId;
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
    public bool IsUnlocked => IsConfigured && settings.IsUnlocked(VehicleId, SaveSystem.Data);
    public VehicleUpgradeSettings.VehicleType NextVehicle => IsConfigured ? settings.Next(VehicleId) : null;
    public int Level => IsConfigured ? Mathf.Clamp(SaveSystem.Data?.GetVehicleUpgradeLevel(VehicleId) ?? 0, 0, Profile.MaxLevel) : 0;
    public float CapacityVolume => IsConfigured && Profile.upgradeCapacity ? Profile.At(Level).capacityCubicMeters : float.PositiveInfinity;
    public int SlotCount => IsConfigured ? Profile.At(Level).slots : int.MaxValue;
    public bool IsMaxed => IsConfigured && Level >= Profile.MaxLevel;
    public int Price => IsConfigured && !IsMaxed ? Profile.At(Level).price : 0;
    public bool UsesVoucher => Price > 0 && (SaveSystem.Data?.freeUpgradeVouchers ?? 0) > 0;

    private void Awake()
    {
        bicycle = GetComponent<BicycleController>();
        if (bicycle != null) originalSlots = bicycle.cargoSlots ?? new Transform[0];
    }
    private IEnumerator Start()
    {
        if (!SaveSystem.savesLoaded)
            yield return new WaitUntil(() => SaveSystem.savesLoaded);
        // Resolve the saved destination before instantiating, skipping intermediate prefabs.
        if (IsConfigured && IsUnlocked && IsMaxed && NextVehicle != null)
            TryAdvanceToUnlockedVehicle();
    }
    private void OnEnable()
    {
        SaveSystem.MoneyChanged += RefreshSlots;
        RefreshSlots(SaveSystem.Money);
    }
    private void OnDisable() => SaveSystem.MoneyChanged -= RefreshSlots;

    private void RefreshSlots(int _)
    {
        if (GetComponent<VehicleCargoRack>() != null) return;
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
            float volume = 0;
            foreach (var root in CargoRoots()) volume += MeasureVolume(root.gameObject);
            return volume;
        }
    }
    public int CargoCount => CargoRoots().Count;
    public bool HasCargo => CargoCount > 0;
    public bool CanLoadVolume(float volume, int slotCost = 1)
    {
        if (IsConfigured && !IsUnlocked) return false;
        if (GetComponent<VehicleCargoRack>() is VehicleCargoRack rack)
            // Rack bays are authored for one sack or two boxes. Closing a sack changes its
            // collider volume, but must not make it too large for an otherwise empty bay.
            return slotCost > 0 && rack.UsedSlots + slotCost <= (IsConfigured ? SlotCount : 2);
        return !IsConfigured || (float.IsFinite(volume) && volume > 0 &&
            CargoCount + slotCost <= SlotCount && OccupiedVolume + volume <= CapacityVolume + .0001f);
    }

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
        if (!isActiveAndEnabled || !IsConfigured || !IsUnlocked || HasCargo || IsMaxed || SaveSystem.Data == null) return false;
        if (bicycle != null && bicycle.IsRidden) return false;
        int level = SaveSystem.Data.GetVehicleUpgradeLevel(VehicleId);
        bool advancesVehicle = Level + 1 == Profile.MaxLevel && NextVehicle != null;
        VehicleUpgrade next = null;
        if (advancesVehicle && !TryCreateReplacement(out next)) return false;
        if (!SaveSystem.TryPurchaseVehicleUpgrade(VehicleId, Price, level, Profile.MaxLevel))
        {
            if (next != null) Destroy(next.gameObject);
            return false;
        }
        if (next != null) ActivateReplacement(next);
        return true;
    }

    public bool TryAdvanceToUnlockedVehicle()
    {
        if (Replacement != null) return true;
        if (!isActiveAndEnabled || !IsConfigured || !IsUnlocked || !IsMaxed || HasCargo ||
            NextVehicle == null || (bicycle != null && bicycle.IsRidden)) return false;
        if (!TryCreateReplacement(out var next)) return false;
        ActivateReplacement(next);
        return true;
    }

    private bool TryCreateReplacement(out VehicleUpgrade next)
    {
        next = null;
        var profile = IsMaxed ? settings.ResolveSavedVehicle(VehicleId, SaveSystem.Data) : NextVehicle;
        if (profile == Profile) return false;
        if (profile == null || !profile.IsConfigured) return false;
        var prefab = Resources.Load<GameObject>(profile.PrefabResourcePath);
        if (prefab == null || prefab.GetComponent<BicycleController>() == null)
        {
            Debug.LogWarning($"Cannot upgrade vehicle: Resources/{profile.PrefabResourcePath} must be a prefab with BicycleController on its root.", this);
            return false;
        }

        // Configure the save ID before the new vehicle's Awake/OnEnable can run.
        var staging = new GameObject("Vehicle upgrade staging") { hideFlags = HideFlags.DontSave };
        staging.SetActive(false);
        var instance = Instantiate(prefab, transform.position, transform.rotation, staging.transform);
        instance.SetActive(false);
        next = instance.GetComponent<VehicleUpgrade>();
        if (next == null) next = instance.AddComponent<VehicleUpgrade>();
        next.settings = settings;
        next.vehicleId = profile.id;
        next.enabled = true;
        if (GetComponent<VehicleUpgradeInteractable>() != null && instance.GetComponent<VehicleUpgradeInteractable>() == null)
            instance.AddComponent<VehicleUpgradeInteractable>();
        instance.transform.SetParent(transform.parent, true);
        Destroy(staging);
        return true;
    }

    private void ActivateReplacement(VehicleUpgrade next)
    {
        Replacement = next;
        next.gameObject.SetActive(true);
        foreach (var rider in FindObjectsByType<PlayerBicycleInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (bicycle != null && rider.bicycle == bicycle) rider.bicycle = next.GetComponent<BicycleController>();
        VehicleUpgradeInteractable.ReplaceTargets(this, next);
        VehicleReplaced?.Invoke(this, next);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
    public bool OpenMenu()
    {
        var manager = FindFirstObjectByType<UpgradeManager>(FindObjectsInactive.Include);
        return manager != null && manager.OpenVehicle(this);
    }
}
