using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Vehicle Settings")]
public sealed class VehicleUpgradeSettings : ScriptableObject
{
    public const string ResourcePath = "Upgrades/VehicleUpgradeSettings";

    [Tooltip("World-space effect played at the new vehicle's center after a successful upgrade purchase.")]
    public GameObject upgradeEffect;

    [Serializable]
    public sealed class CargoPose
    {
        public string containerId;
        [Min(0)] public int slotIndex;
        public bool applyToAllSlots = true;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
        public Vector3 EffectiveScale => scale == Vector3.zero ? Vector3.one : new Vector3(Mathf.Max(.01f,scale.x),Mathf.Max(.01f,scale.y),Mathf.Max(.01f,scale.z));
    }

    [Serializable]
    public sealed class VehicleType
    {
        public string id;
        public string displayName;
        public CargoPose[] cargoPoses = Array.Empty<CargoPose>();
        public CargoPose FindCargoPoseExact(string containerId, int slotIndex) => Array.Find(cargoPoses ?? Array.Empty<CargoPose>(), p => p != null && p.containerId == containerId && p.slotIndex == slotIndex);
        public bool UsesFirstSlotForAll(string containerId) => FindCargoPoseExact(containerId, 0)?.applyToAllSlots ?? true;
        public CargoPose FindCargoPose(string containerId, int slotIndex) => FindCargoPoseExact(containerId, UsesFirstSlotForAll(containerId) ? 0 : slotIndex);
        [Min(1), Tooltip("Highest container transport level this vehicle accepts.")] public int maximumCargoLevel = 2;
        public Sprite image;
        [Tooltip("Presentation only. Actual cargo slots come from the vehicle prefab.")]
        [Min(0)] public int slots = 2;
        [Tooltip("Presentation only, in m/s. Actual movement speed comes from the vehicle prefab.")]
        [Min(0f)] public float movementSpeed = 7f;
        [Tooltip("Price to unlock this vehicle from the preceding vehicle.")]
        [Min(0)] public int price = 250;
        public bool requiresKey;
        [Tooltip("Key icon shown when this vehicle is the next upgrade and requires a key.")]
        public Sprite keySprite;
        [Tooltip("Collected key ID. Empty uses vehicle.key.<id>. Keys are not consumed.")]
        public string keyId;
        public string RequiredKeyId => string.IsNullOrWhiteSpace(keyId) ? "vehicle.key." + id : keyId;
        [Tooltip("Prefab path relative to Resources, without extension. Empty uses Vehicles/<id>.")]
        public string prefabResourcePath;
        public string PrefabResourcePath => string.IsNullOrWhiteSpace(prefabResourcePath) ? "Vehicles/" + id : prefabResourcePath;
        // Only used to recognize completed miniupgrades in saves from the previous progression.
        [HideInInspector] public int legacyUpgradeCount = 2;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(id) && slots >= 0 &&
            float.IsFinite(movementSpeed) && movementSpeed >= 0f && price >= 0;
        public bool HasRequiredKey(PlayerData data) => !requiresKey ||
            (data?.foundSecrets != null && Array.IndexOf(data.foundSecrets, RequiredKeyId) >= 0);
    }

    [Tooltip("Direct upgrade order. Each purchase unlocks and spawns the next vehicle.")]
    public VehicleType[] vehicles = Array.Empty<VehicleType>();
    public VehicleType Find(string id) => Array.Find(vehicles ?? Array.Empty<VehicleType>(), vehicle => vehicle != null && vehicle.id == id);
    public VehicleType Next(string id)
    {
        int index = Array.FindIndex(vehicles ?? Array.Empty<VehicleType>(), vehicle => vehicle != null && vehicle.id == id);
        return index >= 0 && index + 1 < vehicles.Length ? vehicles[index + 1] : null;
    }
    public bool IsUnlocked(string id, PlayerData data)
    {
        int index = Array.FindIndex(vehicles ?? Array.Empty<VehicleType>(), vehicle => vehicle != null && vehicle.id == id);
        if (index < 0 || data == null || !vehicles[index].IsConfigured) return false;
        if (index == 0 || (data.unlockedVehicles?.Contains(id) ?? false) || data.GetVehicleUpgradeLevel(id) > 0) return true;
        var previous = vehicles[index - 1];
        return previous != null && previous.IsConfigured && previous.legacyUpgradeCount > 0 &&
            data.GetVehicleUpgradeLevel(previous.id) >= previous.legacyUpgradeCount;
    }
    public VehicleType ResolveSavedVehicle(string id, PlayerData data)
    {
        int index = Array.FindIndex(vehicles ?? Array.Empty<VehicleType>(), vehicle => vehicle != null && vehicle.id == id);
        if (index < 0) return null;
        var result = vehicles[index];
        for (int i = index + 1; data != null && i < vehicles.Length; i++)
            if (vehicles[i] != null && IsUnlocked(vehicles[i].id, data)) result = vehicles[i];
        return result;
    }
    public bool TryPurchase(PlayerData data, string currentId)
    {
        var next = Next(currentId);
        if (data == null || !IsUnlocked(currentId, data) || next == null || !next.IsConfigured ||
            IsUnlocked(next.id, data) || !next.HasRequiredKey(data)) return false;
        bool voucher = next.price > 0 && data.freeUpgradeVouchers > 0;
        if (!voucher && data.cash < next.price) return false;
        data.unlockedVehicles ??= new System.Collections.Generic.List<string>();
        data.unlockedVehicles.Add(next.id);
        if (voucher) data.freeUpgradeVouchers--;
        else data.cash -= next.price;
        return true;
    }
}
