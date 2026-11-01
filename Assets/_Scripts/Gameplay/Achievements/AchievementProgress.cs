using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameAchievementId
{
    GrainMountain=0, PackingProfessional=1, CleanSweep=2, MasterCleaner=3, KeyCollector=4,
    BuriedTreasure=5, CrowControl=6, RatPatrol=7, HeavyLifter=8, GrainTycoon=9
}
public readonly struct GameAchievementDefinition
{
    public readonly int index;
    public readonly string title,description;
    public string SteamApiName => "ACH_"+index;
    public GameAchievementDefinition(int index,string title,string description){this.index=index;this.title=title;this.description=description;}
}
[Serializable]
public sealed class AchievementProgress
{
    public double collectedKilograms;
    public long grainSaleCash;
    public int crowsScared,ratsScared;
    public List<string> closedBags=new List<string>();
    public List<string> forkliftDeliveries=new List<string>();
    public List<int> clearedHangars=new List<int>();
    public List<int> requiredHangars=new List<int>();
    public List<string> requiredVehicleKeys=new List<string>();
    public List<string> requiredChests=new List<string>();
    public List<int> unlocked=new List<int>();
    public void Normalize()
    {
        closedBags??=new List<string>();forkliftDeliveries??=new List<string>();clearedHangars??=new List<int>();
        requiredHangars??=new List<int>();requiredVehicleKeys??=new List<string>();requiredChests??=new List<string>();unlocked??=new List<int>();
    }
}
public static class AchievementRules
{
    public static readonly GameAchievementDefinition[] Definitions=
    {
        new GameAchievementDefinition(0,"Grain Mountain","Collect 5,000 kg of grain."),
        new GameAchievementDefinition(1,"Packing Professional","Fill and close 50 different bags."),
        new GameAchievementDefinition(2,"Clean Sweep","Fully clear 2 hangars."),
        new GameAchievementDefinition(3,"Master Cleaner","Fully clear half the hangars (rounded up)."),
        new GameAchievementDefinition(4,"Key Collector","Find half the vehicle keys (rounded up)."),
        new GameAchievementDefinition(5,"Buried Treasure","Open half the hidden chests (rounded up)."),
        new GameAchievementDefinition(6,"Crow Control","Scare away 25 crows with the broom."),
        new GameAchievementDefinition(7,"Rat Patrol","Scare away 50 rats with the broom."),
        new GameAchievementDefinition(8,"Heavy Lifter","Deliver 13 different full big bags to a weigh station using a forklift."),
        new GameAchievementDefinition(9,"Grain Tycoon","Earn $50,000 total from grain sales.")
    };
    public static AchievementProgress State(PlayerData data)
    {
        if(data==null)return null;
        data.achievementProgress??=new AchievementProgress();data.achievementProgress.Normalize();return data.achievementProgress;
    }
    public static double Target(AchievementProgress state,int index)
    {
        switch(index)
        {
            case 0:return 5000;case 1:return 50;case 2:return 2;case 3:return (state.requiredHangars.Count+1)/2;
            case 4:return (state.requiredVehicleKeys.Count+1)/2;case 5:return (state.requiredChests.Count+1)/2;
            case 6:return 25;case 7:return 50;case 8:return 13;case 9:return 50000;default:return 0;
        }
    }
    public static double Current(PlayerData data,int index)
    {
        var state=State(data);if(state==null)return 0;
        switch(index)
        {
            case 0:return state.collectedKilograms;case 1:return state.closedBags.Count;case 2:return state.clearedHangars.Count;
            case 3:int cleared=0;foreach(int id in state.requiredHangars)if(state.clearedHangars.Contains(id))cleared++;return cleared;
            case 4:return Matches(state.requiredVehicleKeys,data.foundSecrets);case 5:return Matches(state.requiredChests,data.foundSecrets);
            case 6:return state.crowsScared;case 7:return state.ratsScared;case 8:return state.forkliftDeliveries.Count;case 9:return state.grainSaleCash;default:return 0;
        }
    }
    private static int Matches(List<string> required,string[] found)
    {
        if(found==null)return 0;int count=0;
        foreach(string id in required)if(!string.IsNullOrEmpty(id) && Array.IndexOf(found,id)>=0)count++;
        return count;
    }
    public static bool TryUnlock(PlayerData data,int index)
    {
        if(index<0 || index>=Definitions.Length)return false;
        var state=State(data);if(state==null || state.unlocked.Contains(index))return false;
        double target=Target(state,index);if(target<=0 || Current(data,index)<target)return false;
        state.unlocked.Add(index);return true;
    }
    public static bool AddUnique(List<string> list,string id,int limit)
    {
        if(string.IsNullOrEmpty(id) || list.Count>=limit || list.Contains(id))return false;list.Add(id);return true;
    }
}
