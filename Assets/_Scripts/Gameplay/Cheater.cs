using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Play Mode shortcuts. Hangar indices follow the game's numbering: 1 through 5.</summary>
[DisallowMultipleComponent]
public sealed class Cheater : MonoBehaviour
{
    private bool clearing;

    public async void ClearHangar(int hangarIndex)
    {
        if (clearing) return;
        if (!Application.isPlaying || !SaveSystem.savesLoaded || SaveSystem.Data == null || WorldSaveRuntime.IsRestoring ||
            hangarIndex < 1 || hangarIndex > 5)
        {
            Debug.LogWarning("ClearHangar requires a loaded game and a hangar index from 1 to 5.", this);
            return;
        }

        var piles = new List<GrainPile>();
        var quantities = new GrainCargo();
        var stock = new GrainCargo();
        foreach (var pile in FindObjectsByType<GrainPile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GoalContext.Resolve(pile, out int hangar, out _);
            if (hangar != hangarIndex) continue;
            piles.Add(pile);
            pile.CopyRemainingGrain(stock);
            for (int i = 0; i < GrainCargo.TypeCount; i++) quantities.Add((GrainType)i, stock[(GrainType)i]);
        }
        if (piles.Count == 0)
        {
            Debug.LogWarning($"No grain piles found for hangar {hangarIndex}.", this);
            return;
        }

        var cropValues = new List<double>();
        for (int i = 0; i < GrainCargo.TypeCount; i++)
        {
            var grain = (GrainType)i;
            if (quantities[grain] <= 0) continue;
            if (GrainEconomySettings.Default == null || !GrainEconomySettings.Default.TryGetGrain(grain, out var price))
            {
                Debug.LogWarning($"Missing selling price for {grain}.", this);
                return;
            }
            cropValues.Add(PlayerPerks.SellCash(EconomyNumberFormat.CropValue(quantities[grain], price.grainPrice), PlayerPerks.SellMultiplier));
        }
        int cash = EconomyNumberFormat.SaleTotal(cropValues);
        if (SaveSystem.Money > int.MaxValue - cash)
        {
            Debug.LogWarning("ClearHangar would exceed the cash limit.", this);
            return;
        }

        clearing = true;
        try
        {
            // Empty the piles before payment callbacks or goal completion can capture a save.
            foreach (var pile in piles) pile.ClearGrain();
            if (cash > 0)
            {
                SaveSystem.AddMoney(cash);
                GameAchievements.GrainSold(cash);
            }
            for (int i = 0; i < GrainCargo.TypeCount; i++)
                GoalChecker.ReportProgress(GoalType.GrainSold, quantities[(GrainType)i], hangarIndex, (GrainType)i);
            GoalChecker.ReportProgress(GoalType.MoneyEarned, cash, hangarIndex);
            SaveSystem.TryRecordHangarClearance(hangarIndex, 100f);
            GoalsManager.Instance?.CompleteHangarGrainGoals(hangarIndex);
            await SaveSystem.SaveGoalCompletionAsync();
        }
        catch (Exception error) { Debug.LogException(error, this); }
        finally { clearing = false; }
    }
}
