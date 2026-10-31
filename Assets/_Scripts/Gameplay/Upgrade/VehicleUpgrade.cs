using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Saved vehicle unlocks and prefab replacement. Driving and cargo stats stay authored in the prefab.</summary>
[DisallowMultipleComponent]
public sealed class VehicleUpgrade : MonoBehaviour
{
    [SerializeField] private VehicleUpgradeSettings settings;
    [Tooltip("Stable settings/save ID. Leave empty to detect the existing vehicle type. New vehicle types can use their own ID.")]
    [SerializeField] private string vehicleId;
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
    public int SlotCount => bicycle != null && bicycle.TryGetComponent<VehicleCargoRack>(out var rack)
        ? rack.SlotLimit : bicycle != null ? Mathf.Max(bicycle.cargoSlots?.Length ?? 0, bicycle.exclusiveBagSlot != null ? 1 : 0) : 0;
    public bool IsMaxed => IsConfigured && NextVehicle == null;
    public int Price => NextVehicle?.price ?? 0;
    public bool HasRequiredKey => NextVehicle == null || NextVehicle.HasRequiredKey(SaveSystem.Data);
    public bool UsesVoucher => Price > 0 && (SaveSystem.Data?.freeUpgradeVouchers ?? 0) > 0;

    private void Awake()
    {
        bicycle = GetComponent<BicycleController>();
    }
    private IEnumerator Start()
    {
        if (!SaveSystem.savesLoaded)
            yield return new WaitUntil(() => SaveSystem.savesLoaded);
        // Resolve the saved destination before instantiating, skipping intermediate prefabs.
        if (IsConfigured && IsUnlocked && settings.ResolveSavedVehicle(VehicleId, SaveSystem.Data) != Profile)
            TryAdvanceToUnlockedVehicle();
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
    public int CargoCount => CargoRoots().Count;
    public bool HasCargo => CargoCount > 0;
    public bool CanLoadCargo(int slotCost = 1)
    {
        if (slotCost <= 0 || (IsConfigured && !IsUnlocked)) return false;
        if (GetComponent<VehicleCargoRack>() is VehicleCargoRack rack)
            return rack.UsedSlots + slotCost <= rack.SlotLimit;
        return bicycle != null && CargoCount + slotCost <= SlotCount;
    }

    public bool TryUpgrade()
    {
        if (!isActiveAndEnabled || !IsConfigured || !IsUnlocked || !HasRequiredKey || HasCargo || IsMaxed || SaveSystem.Data == null) return false;
        if (bicycle != null && bicycle.IsRidden) return false;
        if (!TryCreateReplacement(out var next)) return false;
        if (!SaveSystem.TryPurchaseVehicle(settings, VehicleId))
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
        if (!isActiveAndEnabled || !IsConfigured || !IsUnlocked || HasCargo ||
            NextVehicle == null || (bicycle != null && bicycle.IsRidden)) return false;
        if (settings.ResolveSavedVehicle(VehicleId, SaveSystem.Data) == Profile) return false;
        if (!TryCreateReplacement(out var next)) return false;
        ActivateReplacement(next);
        return true;
    }

    private bool TryCreateReplacement(out VehicleUpgrade next)
    {
        next = null;
        var saved = settings.ResolveSavedVehicle(VehicleId, SaveSystem.Data);
        var profile = saved != Profile ? saved : NextVehicle;
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
