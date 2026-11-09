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
    private GameObject spawnedAssembly;
    private GameObject AssemblyRoot => spawnedAssembly != null ? spawnedAssembly : gameObject;
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
            settings = GrainEconomySettings.ResolveVehicle(settings);
            return settings != null ? settings.Find(VehicleId) : null;
        }
    }
    public bool IsConfigured => Profile != null && Profile.IsConfigured;
    public bool IsUnlocked => IsConfigured && settings.IsUnlocked(VehicleId, SaveSystem.Data);
    public VehicleUpgradeSettings.VehicleType NextVehicle => IsConfigured ? settings.Next(VehicleId) : null;
    public int SlotCount
    {
        get
        {
            if (bicycle == null) bicycle = GetComponent<BicycleController>();
            if (bicycle != null && bicycle.TryGetComponent<PickupCargoLayout>(out var pickupLayout)) return pickupLayout.LargeCapacity;
            return bicycle != null && bicycle.TryGetComponent<VehicleCargoRack>(out var rack)
                ? rack.SlotLimit : bicycle != null ? Mathf.Max(bicycle.cargoSlots?.Length ?? 0, bicycle.exclusiveBagSlot != null ? 1 : 0) : 0;
        }
    }
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
    public bool AcceptsContainer(GrainContainer container) => container != null && container.TransportLevel <= (Profile?.maximumCargoLevel ?? 2);

    public bool CanLoadCargo(int slotCost = 1)
    {
        if (slotCost <= 0 || (IsConfigured && !IsUnlocked)) return false;
        if (GetComponent<PickupCargoLayout>() is PickupCargoLayout layout) return layout.FreeSlotByUnits(slotCost) != null;
        if (GetComponent<VehicleCargoRack>() is VehicleCargoRack rack)
            return rack.UsedSlots + slotCost <= rack.SlotLimit;
        return bicycle != null && CargoCount + slotCost <= SlotCount;
    }

    public bool TryUpgrade()
    {
        if (!isActiveAndEnabled || SaveSystem.UpgradePurchaseLocked || Replacement != null || !IsConfigured || !IsUnlocked || !HasRequiredKey || HasCargo || IsMaxed || SaveSystem.Data == null) return false;
        if (bicycle != null && bicycle.IsRidden) return false;
        if (!TryCreateReplacement(out var next)) return false;
        if (!SaveSystem.TryPurchaseVehicle(settings, VehicleId))
        {
            if (next != null) ReleaseObject(next.AssemblyRoot);
            return false;
        }
        if (next != null)
        {
            ActivateReplacement(next);
            next.PlayUpgradeEffect();
        }
        return true;
    }

    private void PlayUpgradeEffect()
    {
        if (settings == null || settings.upgradeEffect == null) return;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (var renderer in AssemblyRoot.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        var effect = Instantiate(settings.upgradeEffect, bounds.center, settings.upgradeEffect.transform.rotation);
        // The authored cylinder has a radius of about 1.2 metres. Enclose larger vehicles too.
        float scale = Mathf.Max(1f, Mathf.Max(bounds.extents.x, bounds.extents.z) / 1.2f);
        effect.transform.localScale *= scale;
        float lifetime = 0f;
        foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.loop = false;
            lifetime = Mathf.Max(lifetime, (main.startDelay.constantMax + main.duration + main.startLifetime.constantMax)
                / Mathf.Max(.01f, main.simulationSpeed));
        }
        Destroy(effect, Mathf.Max(.1f, lifetime));
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
        if (prefab == null || prefab.GetComponentInChildren<BicycleController>(true) == null)
        {
            Debug.LogWarning($"Cannot upgrade vehicle: Resources/{profile.PrefabResourcePath} must be a prefab containing BicycleController.", this);
            return false;
        }

        // Configure the save ID before the new vehicle's Awake/OnEnable can run.
        var staging = new GameObject("Vehicle upgrade staging") { hideFlags = HideFlags.DontSave };
        staging.SetActive(false);
        var instance = Instantiate(prefab, transform.position, transform.rotation, staging.transform);
        instance.SetActive(false);
        var rideable = instance.GetComponentInChildren<BicycleController>(true);
        next = rideable.GetComponent<VehicleUpgrade>();
        if (next == null) next = rideable.gameObject.AddComponent<VehicleUpgrade>();
        next.settings = settings;
        next.vehicleId = profile.id;
        next.spawnedAssembly = instance;
        next.enabled = true;
        if (VehicleId == "motoroller" && HangarVehicleAccess.BlocksPlacement(
                new Bounds(transform.position, Vector3.zero), bicycle, true))
        {
            // Renderer local bounds remain valid while the replacement is staged inactive.
            // Include the whole assembly so a trailer or cargo bed also clears the doorway.
            Bounds footprint = new Bounds(instance.transform.position, Vector3.zero);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                Bounds local = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                    footprint.Encapsulate(renderer.transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            }
            if (!VehicleReset.TryFindPlacement(footprint, instance.transform.position, instance.transform.position,
                    AssemblyRoot.transform, rideable, true, out var outside))
            {
                ReleaseObject(staging);
                next = null;
                return false;
            }
            instance.transform.position = outside;
        }
        if (GetComponent<VehicleUpgradeInteractable>() != null && rideable.GetComponent<VehicleUpgradeInteractable>() == null)
            rideable.gameObject.AddComponent<VehicleUpgradeInteractable>();
        instance.transform.SetParent(AssemblyRoot.transform.parent, true);
        ReleaseObject(staging);
        return true;
    }

    private void ActivateReplacement(VehicleUpgrade next)
    {
        WorldSaveIdentity.Transfer(gameObject, next.gameObject);
        Replacement = next;
        next.AssemblyRoot.SetActive(true);
        foreach (var rider in FindObjectsByType<PlayerBicycleInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (bicycle != null && rider.bicycle == bicycle) rider.bicycle = next.GetComponent<BicycleController>();
        VehicleUpgradeInteractable.ReplaceTargets(this, next);
        VehicleReplaced?.Invoke(this, next);
        AssemblyRoot.SetActive(false);
        ReleaseObject(AssemblyRoot);
    }
    private static void ReleaseObject(GameObject target)
    {
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }
    public bool OpenMenu()
    {
        var manager = FindFirstObjectByType<UpgradeManager>(FindObjectsInactive.Include);
        return manager != null && manager.OpenVehicle(this);
    }
}
