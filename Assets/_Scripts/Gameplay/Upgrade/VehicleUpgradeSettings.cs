using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Vehicle Settings")]
public sealed class VehicleUpgradeSettings : ScriptableObject
{
    public const string ResourcePath = "Upgrades/VehicleUpgradeSettings";
    [Serializable]
    public sealed class Level
    {
        public Sprite image;
        [Min(0f)] public float capacityCubicMeters = .4f;
        [Min(1)] public int slots = 6;
        [Min(0), Tooltip("Cost to advance from this level. Zero means Free; ignored at maximum level.")]
        public int price = 250;
    }
    [Serializable]
    public sealed class VehicleType
    {
        public string id;
        public string displayName;
        [Tooltip("Prefab path relative to a Resources folder, without extension. Empty uses Vehicles/<id>. The prefab root must have BicycleController.")]
        public string prefabResourcePath;
        public string PrefabResourcePath => string.IsNullOrWhiteSpace(prefabResourcePath) ? "Vehicles/" + id : prefabResourcePath;
        [Tooltip("Enable capacity limits and capacity values in the upgrade screen. Disable for upgrades based only on cargo slots.")]
        public bool upgradeCapacity = true;
        [Tooltip("Starting stats followed by each miniupgrade. Buying the last miniupgrade unlocks the next vehicle in the vehicles list.")]
        public Level[] levels = Array.Empty<Level>();
        public int MaxLevel => Mathf.Max(0, (levels?.Length ?? 0) - 1);
        public bool IsConfigured
        {
            get
            {
                if (string.IsNullOrWhiteSpace(id) || levels == null || levels.Length == 0) return false;
                for (int i = 0; i < levels.Length; i++)
                {
                    var level = levels[i];
                    if (level == null || (upgradeCapacity && (!float.IsFinite(level.capacityCubicMeters) || level.capacityCubicMeters <= 0)) ||
                        level.slots <= 0 || level.price < 0) return false;
                    if (i > 0 && ((upgradeCapacity && level.capacityCubicMeters < levels[i - 1].capacityCubicMeters) || level.slots < levels[i - 1].slots)) return false;
                }
                return true;
            }
        }
        public Level At(int level) => levels[Mathf.Clamp(level, 0, MaxLevel)];
    }
    [Tooltip("Progression order. The first vehicle starts unlocked; completing its miniupgrades unlocks the next vehicle.")]
    public VehicleType[] vehicles = Array.Empty<VehicleType>();
    public VehicleType Find(string id) => Array.Find(vehicles ?? Array.Empty<VehicleType>(), vehicle => vehicle != null && vehicle.id == id);

    public VehicleType Next(string id)
    {
        int index = Array.FindIndex(vehicles ?? Array.Empty<VehicleType>(), vehicle => vehicle != null && vehicle.id == id);
        return index >= 0 && index + 1 < vehicles.Length ? vehicles[index + 1] : null;
    }

    public VehicleType ResolveSavedVehicle(string id, PlayerData data)
    {
        int index = Array.FindIndex(vehicles ?? Array.Empty<VehicleType>(), vehicle => vehicle != null && vehicle.id == id);
        if (index < 0) return null;
        while (data != null && index + 1 < vehicles.Length)
        {
            var current = vehicles[index];
            var next = vehicles[index + 1];
            if (!current.IsConfigured || current.MaxLevel == 0 || next == null || !next.IsConfigured ||
                data.GetVehicleUpgradeLevel(current.id) < current.MaxLevel) break;
            index++;
        }
        return vehicles[index];
    }

    public bool IsUnlocked(string id, PlayerData data)
    {
        int index = Array.FindIndex(vehicles ?? Array.Empty<VehicleType>(), vehicle => vehicle != null && vehicle.id == id);
        if (index < 0 || data == null) return false;
        for (int i = index; i >= 0; i--)
        {
            var vehicle = vehicles[i];
            if (vehicle == null || !vehicle.IsConfigured) return false;
            // Keep vehicles with upgrades from older saves available.
            if (data.GetVehicleUpgradeLevel(vehicle.id) > 0 || i == 0) return true;
            var previous = vehicles[i - 1];
            if (previous == null || !previous.IsConfigured || previous.MaxLevel == 0 ||
                data.GetVehicleUpgradeLevel(previous.id) < previous.MaxLevel) return false;
        }
        return false;
    }
}
