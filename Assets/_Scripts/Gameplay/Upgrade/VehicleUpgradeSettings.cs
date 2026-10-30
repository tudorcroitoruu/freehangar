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
        [Tooltip("Enable capacity limits and capacity values in the upgrade screen. Disable for upgrades based only on cargo slots.")]
        public bool upgradeCapacity = true;
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
    public VehicleType[] vehicles = Array.Empty<VehicleType>();
    public VehicleType Find(string id) => Array.Find(vehicles ?? Array.Empty<VehicleType>(), vehicle => vehicle != null && vehicle.id == id);
}
