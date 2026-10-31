using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Captures starting scene inventory; reports use the economy asset as their only price source.</summary>
public static class GrainEconomyReport
{
    [MenuItem("Tools/Grain/Write Economy Report")]
    public static void WriteReport() => WriteAssetReport(GrainEconomySettings.Default);

    public static void WriteAssetReport(GrainEconomySettings economy)
    {
        string report = BuildReport(economy);
        Directory.CreateDirectory("output/Economy");
        File.WriteAllText("output/Economy/current-balance.md", report);
        Debug.Log(report);
        EditorUtility.RevealInFinder("output/Economy/current-balance.md");
    }

    public static void CaptureStock(GrainEconomySettings economy)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Capture starting stock in Edit Mode.");
        if (economy == null) throw new ArgumentNullException(nameof(economy));
        var scene = SceneManager.GetActiveScene();
        var piles = UnityEngine.Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(p => { using (var so = new SerializedObject(p)) return p.gameObject.scene == scene && so.FindProperty("economySettings").objectReferenceValue == economy; }).ToArray();
        if (piles.Length != 5) throw new InvalidOperationException("Open the scene containing the five piles assigned to this economy.");
        var rows = new GrainEconomySettings.HangarStock[5];
        string[] containers = { "smallBag", "largeBag", "crate", "barrel", "bigBag" };
        int[] shovelTargets = { 4, 16, 20, 22, 24 }, staminaTargets = { 1, 2, 3, 4, 4 };
        string[] vehicles = { "motoroller", "lawnmower", "pickup", "pickupBig", "pickupLast" };
        foreach (var pile in piles)
        {
            using (var so = new SerializedObject(pile))
                if (so.FindProperty("previewClearedPercent").floatValue > 0) throw new InvalidOperationException("Reset cleared preview before capturing " + pile.name);
            GoalContext.Resolve(pile, out int id, out _);
            if (id == 0)
            {
                var match = Regex.Match(pile.name, @"^Hangar\s+(\d)");
                id = match.Success ? int.Parse(match.Groups[1].Value) : pile.name.StartsWith("Corn pile", StringComparison.Ordinal) ? 1 : 0;
            }
            if (id < 1 || id > 5 || rows[id - 1] != null) throw new InvalidOperationException("Missing or ambiguous hangar identity: " + pile.name);
            var old = Array.Find(economy.hangars ?? Array.Empty<GrainEconomySettings.HangarStock>(), h => h != null && h.hangarId == id);
            var row = new GrainEconomySettings.HangarStock
            {
                hangarId = id, pileName = pile.name,
                ratLossPercent = old != null ? old.ratLossPercent : id >= 3 ? 5 : 0,
                birdLossPercent = old != null ? old.birdLossPercent : id >= 4 ? 5 : 0,
                containerProductId = old != null ? old.containerProductId : containers[id - 1],
                targetShovelLevel = old != null ? old.targetShovelLevel : shovelTargets[id - 1],
                targetStaminaLevel = old != null ? old.targetStaminaLevel : staminaTargets[id - 1],
                targetVehicleId = old != null ? old.targetVehicleId : vehicles[id - 1]
            };
            pile.Initialize();
            for (int type = 0; type < GrainCargo.TypeCount; type++) row.physicalGrainKg[type] = pile.GrainKilograms((GrainType)type);
            if (Math.Abs(row.physicalGrainKg.Sum() - pile.Kilograms) > Math.Max(.01, pile.Kilograms * .0001))
                throw new InvalidOperationException("Capture requires the full spatial crop inventory: " + pile.name);
            rows[id - 1] = row;
        }
        Undo.RecordObject(economy, "Read initial grain inventory");
        economy.hangars = rows;
        EditorUtility.SetDirty(economy);
    }

    public static string BuildReport() => BuildReport(GrainEconomySettings.Default);

    public static string BuildReport(GrainEconomySettings economy)
    {
        if (economy == null) throw new InvalidOperationException("No economy asset selected.");
        var text = new StringBuilder("# Central grain economy forecast\n\n");
        text.AppendLine("Prices, upgrade references, loss estimates and starting-stock snapshots come from " + AssetDatabase.GetAssetPath(economy) + ". Re-capture stock after changing scene geometry/crop layout. Starting cash: $0.");
        text.AppendLine("\nPest percentages estimate exposure during play; actual losses come only from feeding bites. Rat and bird percentages are additive, capped at 100%. Totals/kg use comma thousands with no decimals; configured unit prices retain precision. Sales truncate to whole dollars per crop before summing; transaction rounding and optional consumables are not included in the forecast.");
        text.AppendLine("\n| Crop | Physical kg | Collectible kg | Rats kg | Birds kg | Saleable kg | $/kg | Gross $ | Expected $ |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        for (int i = 0; i < GrainCargo.TypeCount; i++)
        {
            var type = (GrainType)i;
            var row = economy.ForecastCrop(type);
            double price = economy.TryGetGrain(type, out var grain) ? grain.grainPrice : 0;
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1:N0} | {2:N0} | {3:N0} | {4:N0} | {5:N0} | {6:N2} | {7:N0} | {8:N0} |", type, row.physicalKg, row.collectibleKg, row.ratLossKg, row.birdLossKg, row.SaleableKg, price, row.grossRevenue, row.expectedRevenue));
        }
        text.AppendLine("\n| Hangar | Gross $ | Expected after pests $ | Containers $ | Upgrades $ | Expected balance $ | Stress balance $ |\n|---|---:|---:|---:|---:|---:|---:|");
        double balance = 0, stress = 0;
        long previousUpgrades = 0;
        foreach (var hangar in (economy.hangars ?? Array.Empty<GrainEconomySettings.HangarStock>()).Where(h => h != null).OrderBy(h => h.hangarId))
        {
            double gross = 0, expected = 0;
            for (int type = 0; type < GrainCargo.TypeCount; type++) { var row = economy.ForecastCrop((GrainType)type, hangar); gross += row.grossRevenue; expected += row.expectedRevenue; }
            long cumulativeUpgrades = economy.PlannedCumulativeUpgradeCost(hangar);
            long upgrades = cumulativeUpgrades - previousUpgrades;
            previousUpgrades = cumulativeUpgrades;
            double containers = economy.ContainerAllowance(hangar);
            balance += expected - containers - upgrades;
            stress += economy.StressRevenue(hangar) - containers - upgrades;
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1:N0} | {2:N0} | {3:N0} | {4:N0} | {5:N0} | {6:N0} |", hangar.hangarId, gross, expected, containers, upgrades, balance, stress));
            if (upgrades < 0 || balance < 0 || stress < 0) text.AppendLine("\n**Review this purchase route: decreasing upgrade targets or insufficient funds.**\n");
        }
        text.AppendLine(string.Format(CultureInfo.InvariantCulture, "\nGross: ${0:N0}. Expected after rats/birds: ${1:N0}. Shovel: ${2:N0}; stamina: ${3:N0}; vehicles: ${4:N0}; ALL upgrades: ${5:N0}. Containers: ${6:N0}. Final expected balance after ALL upgrades: ${7:N0}.", economy.TotalGrossRevenue, economy.TotalExpectedRevenue, economy.ShovelUpgradeCost, economy.StaminaUpgradeCost, economy.VehicleUpgradeCost, economy.TotalUpgradeCost, economy.TotalContainerAllowance, economy.ExpectedBalance));
        text.AppendLine(string.Format(CultureInfo.InvariantCulture, "\nStress total-loss scenario: {0:0.#}%. Actual feeding: rat group {1:0.###} kg/bite; bird {2:0.###} kg/bite (intro flock shares this amount); interval {3:0.###} seconds.", economy.stressLossPercent, economy.RatBiteKg, economy.BirdBiteKg, economy.BiteInterval));
        return text.ToString();
    }
}
