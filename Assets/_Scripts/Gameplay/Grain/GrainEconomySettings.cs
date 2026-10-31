using System;
using UnityEngine;

/// <summary>Authoritative prices, upgrade references and campaign income forecasts.</summary>
[CreateAssetMenu(menuName = "Grain/Economy Settings")]
public sealed class GrainEconomySettings : ScriptableObject
{
    public const string ResourcePath = "Economy/GrainEconomySettings";
    private static GrainEconomySettings defaultSettings;
    public static GrainEconomySettings Default => defaultSettings != null ? defaultSettings : defaultSettings = Resources.Load<GrainEconomySettings>(ResourcePath);
    public int StartingCash => 0;

    [Min(1), Tooltip("Stockpile kg consumed per kg collected. 10 means 100 kg removed for 10 kg cargo. Set before starting a new game.")]
    public float grainConsumeMultiplier = 200f;

    [Header("Upgrade price sources")]
    public ShovelUpgradeSettings shovelSettings;
    public StaminaUpgradeSettings staminaSettings;
    public VehicleUpgradeSettings vehicleSettings;

    [Header("Selling prices and presentation")]
    public GrainElementSettings[] grains = Array.Empty<GrainElementSettings>();

    [Serializable]
    public sealed class ContainerPrice
    {
        public string productId;
        public GameObject prefab;
        [Min(0)] public int price;
        public float CapacityKg => prefab != null && prefab.TryGetComponent<GrainContainer>(out var container) ? container.CapacityKg : 0;
    }
    [Header("Container purchase prices (paid for every new container)")]
    public ContainerPrice[] containers = Array.Empty<ContainerPrice>();

    [Header("Actual pest feeding")]
    [Min(.1f)] public float pestBiteIntervalSeconds = 3f;
    [Min(0), Tooltip("Total kg per bite shared by the rats attacking one container.")]
    public float ratGroupKgPerBite = 1f;
    [Min(0), Tooltip("Kg per bite for a bird, or shared by the three birds in the introductory flock.")]
    public float birdGroupKgPerBite = 1f;

    [Serializable]
    public sealed class HangarStock
    {
        [Range(1, 5)] public int hangarId;
        public string pileName;
        [Tooltip("Physical starting kg by GrainType ID, captured from the scene. Re-read after changing pile shape, scale or crop layout.")]
        public double[] physicalGrainKg = new double[GrainCargo.TypeCount];
        [Range(0, 100), Tooltip("Expected share of this hangar's collectible stock lost to rats during play. Forecast only; runtime loss comes from bites.")]
        public float ratLossPercent;
        [Range(0, 100), Tooltip("Expected share lost to birds. Added to rat loss and capped at 100%. Forecast only.")]
        public float birdLossPercent;
        [Header("Purchases planned by the end of this hangar")]
        public string containerProductId;
        [Min(0)] public int targetShovelLevel;
        [Min(0)] public int targetStaminaLevel;
        public string targetVehicleId;
    }
    [Header("Five hangars: stock snapshot, pest loss assumptions and purchase route")]
    public HangarStock[] hangars = Array.Empty<HangarStock>();
    [Range(0, 100), Tooltip("Additional budget scenario using this total crop loss if it exceeds the estimated rat + bird loss.")]
    public float stressLossPercent = 20f;

    public float ConsumeMultiplier => float.IsFinite(grainConsumeMultiplier)
        ? Mathf.Clamp(grainConsumeMultiplier, 1f, 10000f) : 1f;

    public float BiteInterval => float.IsFinite(pestBiteIntervalSeconds) ? Mathf.Max(.1f, pestBiteIntervalSeconds) : 3f;
    public float RatBiteKg => (float)Positive(ratGroupKgPerBite);
    public float BirdBiteKg => (float)Positive(birdGroupKgPerBite);
    private static double Positive(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, value);
    private static double Fraction(float percent) => Math.Min(1, Positive(percent) / 100);

    public bool TryGetGrain(GrainType grain, out GrainElementSettings settings)
    {
        foreach (var entry in grains ?? Array.Empty<GrainElementSettings>())
            if (entry != null && entry.grainType == grain && float.IsFinite(entry.grainPrice) && entry.grainPrice >= 0)
            { settings = entry; return true; }
        settings = null;
        return false;
    }

    public bool TryGetContainer(string productId, out ContainerPrice settings)
    {
        foreach (var entry in containers ?? Array.Empty<ContainerPrice>())
            if (entry != null && !string.IsNullOrWhiteSpace(productId) && entry.productId == productId && entry.price >= 0)
            { settings = entry; return true; }
        settings = null;
        return false;
    }

    public struct CropForecast
    {
        public double physicalKg, collectibleKg, ratLossKg, birdLossKg, grossRevenue, expectedRevenue;
        public double SaleableKg => Math.Max(0, collectibleKg - ratLossKg - birdLossKg);
    }

    public CropForecast ForecastCrop(GrainType type, HangarStock onlyHangar = null)
    {
        var result = new CropForecast();
        int index = (int)type;
        if (index < 0 || index >= GrainCargo.TypeCount) return result;
        double price = TryGetGrain(type, out var grain) ? grain.grainPrice : 0;
        foreach (var hangar in hangars ?? Array.Empty<HangarStock>())
        {
            if (hangar == null || (onlyHangar != null && hangar != onlyHangar)) continue;
            double physical = hangar.physicalGrainKg != null && index < hangar.physicalGrainKg.Length ? Positive(hangar.physicalGrainKg[index]) : 0;
            double kg = physical / ConsumeMultiplier;
            double rats = kg * Fraction(hangar.ratLossPercent);
            double birds = Math.Min(kg - rats, kg * Fraction(hangar.birdLossPercent));
            result.physicalKg += physical;
            result.collectibleKg += kg;
            result.ratLossKg += rats;
            result.birdLossKg += birds;
            result.grossRevenue += kg * price;
            result.expectedRevenue += (kg - rats - birds) * price;
        }
        return result;
    }

    public double TotalGrossRevenue => SumForecast(false);
    public double TotalExpectedRevenue => SumForecast(true);
    private double SumForecast(bool expected)
    {
        double sum = 0;
        for (int i = 0; i < GrainCargo.TypeCount; i++) { var row = ForecastCrop((GrainType)i); sum += expected ? row.expectedRevenue : row.grossRevenue; }
        return sum;
    }

    public long ShovelUpgradeCost => ShovelCost(shovelSettings != null ? shovelSettings.MaxUpgrades : 0);
    public long StaminaUpgradeCost => StaminaCost(staminaSettings != null ? staminaSettings.MaxUpgrades : 0);
    public long VehicleUpgradeCost => VehicleCost(vehicleSettings?.vehicles?.Length - 1 ?? -1);
    public long TotalUpgradeCost => ShovelUpgradeCost + StaminaUpgradeCost + VehicleUpgradeCost;
    private long ShovelCost(int level)
    {
        long sum = 0;
        for (int i = 0; shovelSettings != null && i < Math.Min(level, shovelSettings.MaxUpgrades); i++) sum += Math.Max(0, shovelSettings.Price(i));
        return sum;
    }
    private long StaminaCost(int level)
    {
        long sum = 0;
        for (int i = 0; staminaSettings?.prices != null && i < Math.Min(level, staminaSettings.prices.Length); i++) sum += Math.Max(0, staminaSettings.prices[i]);
        return sum;
    }
    private long VehicleCost(int index)
    {
        long sum = 0;
        for (int i = 1; vehicleSettings?.vehicles != null && i <= Math.Min(index, vehicleSettings.vehicles.Length - 1); i++)
            sum += Math.Max(0, vehicleSettings.vehicles[i]?.price ?? 0);
        return sum;
    }

    public long PlannedCumulativeUpgradeCost(HangarStock hangar)
    {
        if (hangar == null) return 0;
        int vehicleIndex = Array.FindIndex(vehicleSettings?.vehicles ?? Array.Empty<VehicleUpgradeSettings.VehicleType>(), v => v != null && v.id == hangar.targetVehicleId);
        return ShovelCost(hangar.targetShovelLevel) + StaminaCost(hangar.targetStaminaLevel) + VehicleCost(vehicleIndex);
    }

    public double ContainerAllowance(HangarStock hangar)
    {
        if (hangar == null || !TryGetContainer(hangar.containerProductId, out var container) || container.CapacityKg <= 0) return 0;
        double cost = 0;
        for (int i = 0; i < GrainCargo.TypeCount; i++)
            cost += Math.Ceiling(ForecastCrop((GrainType)i, hangar).collectibleKg / container.CapacityKg) * container.price;
        return cost;
    }
    public double TotalContainerAllowance
    {
        get { double sum = 0; foreach (var hangar in hangars ?? Array.Empty<HangarStock>()) sum += ContainerAllowance(hangar); return sum; }
    }
    public double ExpectedBalance => StartingCash + TotalExpectedRevenue - TotalContainerAllowance - TotalUpgradeCost;
    public double StressRevenue(HangarStock hangar)
    {
        double gross = 0, expected = 0;
        for (int i = 0; i < GrainCargo.TypeCount; i++) { var row = ForecastCrop((GrainType)i, hangar); gross += row.grossRevenue; expected += row.expectedRevenue; }
        return Math.Min(expected, gross * (1 - Fraction(stressLossPercent)));
    }

    // Explicit local settings remain available to isolated Editor fixtures and legacy scenes.
    public static ShovelUpgradeSettings ResolveShovel(ShovelUpgradeSettings local) =>
        Application.isPlaying && Default != null && Default.shovelSettings != null ? Default.shovelSettings : local != null ? local : Default?.shovelSettings ?? Resources.Load<ShovelUpgradeSettings>(ShovelUpgradeSettings.ResourcePath);
    public static StaminaUpgradeSettings ResolveStamina(StaminaUpgradeSettings local) =>
        Application.isPlaying && Default != null && Default.staminaSettings != null ? Default.staminaSettings : local != null ? local : Default?.staminaSettings ?? Resources.Load<StaminaUpgradeSettings>(StaminaUpgradeSettings.ResourcePath);
    public static VehicleUpgradeSettings ResolveVehicle(VehicleUpgradeSettings local) =>
        Application.isPlaying && Default != null && Default.vehicleSettings != null ? Default.vehicleSettings : local != null ? local : Default?.vehicleSettings ?? Resources.Load<VehicleUpgradeSettings>(VehicleUpgradeSettings.ResourcePath);
}
