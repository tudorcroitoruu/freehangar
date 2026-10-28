using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Stamina Settings")]
public sealed class StaminaUpgradeSettings : ScriptableObject
{
    public const string ResourcePath = "Upgrades/StaminaUpgradeSettings";
    [Min(.01f)] public float baseStamina = 100f;
    [Min(.01f)] public float staminaPerUpgrade = 25f;
    [Tooltip("One price per upgrade. Zero means Free.")]
    public int[] prices = { 100, 250, 500, 1000 };
    public int MaxUpgrades => prices?.Length ?? 0;
    public bool IsConfigured
    {
        get
        {
            if (MaxUpgrades == 0 || !float.IsFinite(baseStamina) || baseStamina <= 0f ||
                !float.IsFinite(staminaPerUpgrade) || staminaPerUpgrade <= 0f ||
                !float.IsFinite(baseStamina + staminaPerUpgrade * MaxUpgrades)) return false;
            foreach (int price in prices) if (price < 0) return false;
            return true;
        }
    }
    public int ClampLevel(int level) => Mathf.Clamp(level, 0, MaxUpgrades);
    public float Capacity(int level) => baseStamina + staminaPerUpgrade * ClampLevel(level);
    public int Price(int level) => IsConfigured && level >= 0 && level < MaxUpgrades ? prices[level] : 0;
}
