using UnityEngine;

[CreateAssetMenu(menuName = "Gameplay/Dog upgrades")]
public sealed class DogUpgradeSettings : ScriptableObject
{
    [Tooltip("Costs to reach levels 2, 3, 4 and 5.")]
    public int[] prices = { 500, 1000, 2000, 4000 };
    public int Price(int currentLevel) => prices != null && currentLevel >= 1 && currentLevel <= prices.Length
        ? Mathf.Max(0, prices[currentLevel - 1]) : 0;
    public static string Description(int level) => level <= 1 ? "Stays at home" : level == 2 ? "Hits a chicken every 8 minutes" :
        level == 3 ? "Hits a chicken every 4 minutes" : level == 4 ? "Brings a chicken leg home every 8 minutes" :
        "Brings a chicken leg home every 4 minutes";
}
