using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Shovel Settings")]
public sealed class ShovelUpgradeSettings : ScriptableObject
{
    public const int UpgradesPerShovel = 4;
    public const string ResourcePath = "Upgrades/ShovelUpgradeSettings";

    [Serializable]
    public sealed class ShovelType
    {
        public string name = "Shovel";
        public Sprite image;
        [Min(.01f)] public float capacityKg = 6.5f;
        [Min(.01f)] public float weightKg = 1f;
        [Min(0f)] public float capacityPerUpgrade = 1f;
        [Min(0f)] public float weightReductionPerUpgrade = .05f;
        [Tooltip("Four purchase prices. The fourth purchase advances to the next shovel's starting stats, or completes the last shovel.")]
        public int[] prices = { 100, 200, 300, 400 };
    }

    public ShovelType[] shovels = Array.Empty<ShovelType>();
    public int MaxUpgrades => (shovels?.Length ?? 0) * UpgradesPerShovel;
    public bool IsConfigured
    {
        get
        {
            if (MaxUpgrades == 0) return false;
            foreach (var shovel in shovels)
                if (shovel == null || !float.IsFinite(shovel.capacityKg) || shovel.capacityKg <= 0f ||
                    !float.IsFinite(shovel.weightKg) || shovel.weightKg <= 0f ||
                    !float.IsFinite(shovel.capacityPerUpgrade) || shovel.capacityPerUpgrade < 0f ||
                    !float.IsFinite(shovel.weightReductionPerUpgrade) || shovel.weightReductionPerUpgrade < 0f ||
                    shovel.prices == null || shovel.prices.Length != UpgradesPerShovel) return false;
            foreach (var shovel in shovels)
                foreach (int price in shovel.prices) if (price < 0) return false;
            return true;
        }
    }

    public int ClampLevel(int totalUpgrades) => Mathf.Clamp(totalUpgrades, 0, MaxUpgrades);
    public int TypeIndex(int totalUpgrades) => Mathf.Min(ClampLevel(totalUpgrades) / UpgradesPerShovel, Mathf.Max(0, (shovels?.Length ?? 0) - 1));
    public int FilledDots(int totalUpgrades) => MaxUpgrades > 0 && totalUpgrades >= MaxUpgrades
        ? UpgradesPerShovel : ClampLevel(totalUpgrades) % UpgradesPerShovel;
    public float Capacity(int totalUpgrades)
    {
        var shovel = shovels[TypeIndex(totalUpgrades)];
        return shovel.capacityKg + shovel.capacityPerUpgrade * FilledDots(totalUpgrades);
    }
    public float Weight(int totalUpgrades)
    {
        var shovel = shovels[TypeIndex(totalUpgrades)];
        return Mathf.Max(.01f, shovel.weightKg - shovel.weightReductionPerUpgrade * FilledDots(totalUpgrades));
    }
    public int Price(int totalUpgrades) => !IsConfigured || totalUpgrades < 0 || totalUpgrades >= MaxUpgrades
        ? 0 : shovels[TypeIndex(totalUpgrades)].prices[FilledDots(totalUpgrades)];
}
