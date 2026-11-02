using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrainEconomySettings))]
public sealed class GrainEconomySettingsEditor : Editor
{
    private readonly bool[] cropDetails = new bool[GrainCargo.TypeCount];
    private bool shovelOpen, staminaOpen, vehicleOpen, stagesOpen;
    private Editor shovelEditor, staminaEditor, vehicleEditor;

    public override void OnInspectorGUI()
    {
        var economy = (GrainEconomySettings)target;
        EditorGUILayout.HelpBox("Starting cash is 0. ShopManager and SellingZone read prices directly from this asset. Upgrade costs come from the attached SOs. Forecasts update immediately when inputs change.", MessageType.Info);
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "hangars");
        DrawHangarInputs();
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(Application.isPlaying))
            if (GUILayout.Button("Read starting stock from the five open-scene piles"))
            {
                try { GrainEconomyReport.CaptureStock(economy); AssetDatabase.SaveAssetIfDirty(economy); serializedObject.Update(); }
                catch (Exception error) { Debug.LogException(error); }
            }
        EditorGUILayout.HelpBox("Physical stock is a snapshot. Read it again after changing scene piles. Pest percentages estimate exposure; they do not remove grain automatically. Actual feeding uses the bite amount and interval above.", MessageType.Info);
        if (economy.shovelSettings == null || economy.staminaSettings == null || economy.vehicleSettings == null)
            EditorGUILayout.HelpBox("Attach all three upgrade settings SOs to calculate the full cost.", MessageType.Warning);
        if ((economy.hangars?.Length ?? 0) != 5)
            EditorGUILayout.HelpBox("Capture the five hangars to calculate campaign revenue.", MessageType.Warning);
        foreach (var container in economy.containers ?? Array.Empty<GrainEconomySettings.ContainerPrice>())
            if (container == null || container.CapacityKg <= 0 || container.price < 0)
                EditorGUILayout.HelpBox("A container needs a valid GrainContainer prefab, positive capacity and a nonnegative price.", MessageType.Warning);
        foreach (var hangar in economy.hangars ?? Array.Empty<GrainEconomySettings.HangarStock>())
            if (hangar != null && !economy.TryGetContainer(hangar.containerProductId, out _))
                EditorGUILayout.HelpBox($"Hangar {hangar.hangarId} has no configured budget container.", MessageType.Warning);
        if (economy.hangars != null && economy.hangars.Any(h => h != null && h.ratLossPercent + h.birdLossPercent > 100))
            EditorGUILayout.HelpBox("Combined pest loss is capped at 100% of that hangar's collectible grain.", MessageType.Warning);
        for (int i = 0; i < GrainCargo.TypeCount; i++)
            if (!economy.TryGetGrain((GrainType)i, out _))
                EditorGUILayout.HelpBox("Missing or invalid selling price: " + (GrainType)i, MessageType.Warning);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Calculated totals (read only)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            ReadOnlyNumber("Starting cash", economy.StartingCash);
            ReadOnlyNumber("Gross grain sales $", economy.TotalGrossRevenue);
            ReadOnlyNumber("Sales after rats / birds $", economy.TotalExpectedRevenue);
            ReadOnlyNumber("All shovel upgrades $", economy.ShovelUpgradeCost);
            ReadOnlyNumber("All stamina upgrades $", economy.StaminaUpgradeCost);
            ReadOnlyNumber("All vehicle upgrades $", economy.VehicleUpgradeCost);
            ReadOnlyNumber("All upgrades combined $", economy.TotalUpgradeCost);
            ReadOnlyNumber("Container allowance $", economy.TotalContainerAllowance);
            ReadOnlyNumber("Expected cash left $", economy.ExpectedBalance);
        }
        if (economy.ExpectedBalance < 0) EditorGUILayout.HelpBox("Expected income does not cover all upgrades and containers.", MessageType.Warning);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grain and revenue per hangar (read only)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            foreach (var hangar in (economy.hangars ?? Array.Empty<GrainEconomySettings.HangarStock>())
                         .Where(h => h != null).OrderBy(h => h.hangarId))
            {
                double gross = 0, expected = 0;
                for (int i = 0; i < GrainCargo.TypeCount; i++)
                {
                    var row = economy.ForecastCrop((GrainType)i, hangar);
                    gross += row.grossRevenue;
                    expected += row.expectedRevenue;
                }
                EditorGUILayout.LabelField("Hangar " + hangar.hangarId, EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField("Collectible kg: stock / consume multiplier", EditorStyles.miniLabel);
                    double totalKg = 0;
                    for (int i = 0; i < GrainCargo.TypeCount; i++)
                    {
                        var row = economy.ForecastCrop((GrainType)i, hangar);
                        ReadOnlyNumber($"{(GrainType)i} kg", row.collectibleKg);
                        totalKg += row.collectibleKg;
                    }
                    ReadOnlyNumber("Total collectible kg", totalKg);
                    ReadOnlyNumber("Revenue before pests $", gross);
                    ReadOnlyNumber("Revenue after pests $", expected);
                }
            }
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Revenue per crop: expected / before pests", EditorStyles.boldLabel);
        for (int i = 0; i < GrainCargo.TypeCount; i++)
        {
            var row = economy.ForecastCrop((GrainType)i);
            cropDetails[i] = EditorGUILayout.Foldout(cropDetails[i], $"{(GrainType)i}: ${EconomyNumberFormat.Accounting(row.expectedRevenue)} / ${EconomyNumberFormat.Accounting(row.grossRevenue)}", true);
            if (!cropDetails[i]) continue;
            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(true))
            {
                ReadOnlyNumber("Physical kg", row.physicalKg);
                ReadOnlyNumber("Collectible kg", row.collectibleKg);
                ReadOnlyNumber("Estimated rats loss kg", row.ratLossKg);
                ReadOnlyNumber("Estimated birds loss kg", row.birdLossKg);
                ReadOnlyNumber("Saleable kg", row.SaleableKg);
                ReadOnlyNumber("Revenue before pests $", row.grossRevenue);
                ReadOnlyNumber("Revenue after pests $", row.expectedRevenue);
            }
        }
        stagesOpen = EditorGUILayout.Foldout(stagesOpen, "Affordability by hangar", true);
        if (stagesOpen) DrawStages(economy);
        if (GUILayout.Button("Write this economy's full report")) GrainEconomyReport.WriteAssetReport(economy);
        DrawUpgradeEditor("Edit attached shovel settings", economy.shovelSettings, ref shovelOpen, ref shovelEditor);
        DrawUpgradeEditor("Edit attached stamina settings", economy.staminaSettings, ref staminaOpen, ref staminaEditor);
        DrawUpgradeEditor("Edit attached vehicle settings", economy.vehicleSettings, ref vehicleOpen, ref vehicleEditor);
    }

    private static void DrawStages(GrainEconomySettings economy)
    {
        double balance = 0, stress = 0;
        long previous = 0;
        foreach (var hangar in (economy.hangars ?? Array.Empty<GrainEconomySettings.HangarStock>()).Where(h => h != null).OrderBy(h => h.hangarId))
        {
            double revenue = 0;
            for (int i = 0; i < GrainCargo.TypeCount; i++) revenue += economy.ForecastCrop((GrainType)i, hangar).expectedRevenue;
            long cumulative = economy.PlannedCumulativeUpgradeCost(hangar);
            double costs = economy.ContainerAllowance(hangar) + cumulative - previous;
            previous = cumulative;
            balance += revenue - costs;
            stress += economy.StressRevenue(hangar) - costs;
            EditorGUILayout.LabelField($"Hangar {hangar.hangarId}", $"${EconomyNumberFormat.Accounting(balance)} left; stress ${EconomyNumberFormat.Accounting(stress)}");
            if (balance < 0 || stress < 0) EditorGUILayout.HelpBox("This stage cannot fund its planned purchases in one of the scenarios.", MessageType.Warning);
        }
    }

    private static void ReadOnlyNumber(string label, double value) => EditorGUILayout.TextField(label, EconomyNumberFormat.Accounting(value));

    private void DrawHangarInputs()
    {
        var stocks = serializedObject.FindProperty("hangars");
        stocks.isExpanded = EditorGUILayout.Foldout(stocks.isExpanded, "Hangar stock and purchase plans", true);
        if (!stocks.isExpanded) return;
        for (int i = 0; i < stocks.arraySize; i++)
        {
            var stock = stocks.GetArrayElementAtIndex(i);
            stock.isExpanded = EditorGUILayout.Foldout(stock.isExpanded, "Hangar " + stock.FindPropertyRelative("hangarId").intValue, true);
            if (!stock.isExpanded) continue;
            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(stock.FindPropertyRelative("pileName"));
                EditorGUILayout.PropertyField(stock.FindPropertyRelative("ratLossPercent"));
                EditorGUILayout.PropertyField(stock.FindPropertyRelative("birdLossPercent"));
                EditorGUILayout.PropertyField(stock.FindPropertyRelative("containerProductId"));
                EditorGUILayout.PropertyField(stock.FindPropertyRelative("targetShovelLevel"));
                EditorGUILayout.PropertyField(stock.FindPropertyRelative("targetStaminaLevel"));
                EditorGUILayout.PropertyField(stock.FindPropertyRelative("targetVehicleId"));
                var mass = stock.FindPropertyRelative("physicalGrainKg");
                mass.isExpanded = EditorGUILayout.Foldout(mass.isExpanded, "Captured starting kg (read only)", true);
                if (mass.isExpanded)
                    using (new EditorGUI.DisabledScope(true))
                        for (int type = 0; type < mass.arraySize; type++)
                            ReadOnlyNumber(((GrainType)type).ToString(), mass.GetArrayElementAtIndex(type).doubleValue);
            }
        }
    }

    private static void DrawUpgradeEditor(string title, UnityEngine.Object source, ref bool open, ref Editor editor)
    {
        if (source == null) return;
        open = EditorGUILayout.Foldout(open, title, true);
        if (!open) return;
        CreateCachedEditor(source, null, ref editor);
        using (new EditorGUI.IndentLevelScope()) editor.OnInspectorGUI();
    }
    private void OnDisable()
    {
        DestroyImmediate(shovelEditor);
        DestroyImmediate(staminaEditor);
        DestroyImmediate(vehicleEditor);
    }
}
