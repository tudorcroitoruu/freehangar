using UnityEngine;
using Tool = PlayerCombatTools.Tool;

public static class CombatToolUpgrades
{
    public const int MaximumLevel = 3;
    public static int Price(int level) => level switch { 0 => 250, 1 => 500, 2 => 900, _ => 0 };

    public static bool Found(PlayerData data, Tool tool) => tool switch
    {
        Tool.Axe => BuriedFind.WasFound(data, PlayerCombatTools.AxeFindId) ||
            BuriedFind.WasFound(data, PlayerCombatTools.LegacyAxeFindId),
        Tool.Hammer => BuriedFind.WasFound(data, PlayerCombatTools.HammerFindId),
        _ => false
    };

    public static int Level(PlayerData data, Tool tool) => data == null ? 0 :
        Mathf.Clamp(tool == Tool.Axe ? data.axeUpgradeLevel : data.hammerUpgradeLevel, 0, MaximumLevel);

    public static int AxeDamage(int level) => level switch { 1 => 34, 2 => 42, 3 => 50, _ => 28 };
    public static int WallDamage(int level, int wallStrength) =>
        level == MaximumLevel ? wallStrength : Mathf.Min(Mathf.Max(1, wallStrength - 1), 1 + level);

    public static bool Purchase(PlayerData data, Tool tool, int expectedLevel)
    {
        if (!Found(data, tool) || expectedLevel != Level(data, tool) || expectedLevel < 0 ||
            expectedLevel >= MaximumLevel) return false;
        int price = Price(expectedLevel);
        if (data.freeUpgradeVouchers > 0) data.freeUpgradeVouchers--;
        else if (data.cash >= price) data.cash -= price;
        else return false;
        if (tool == Tool.Axe) data.axeUpgradeLevel = expectedLevel + 1;
        else data.hammerUpgradeLevel = expectedLevel + 1;
        return true;
    }
}
