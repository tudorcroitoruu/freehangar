using NUnit.Framework;
using UnityEngine;

public class GameAchievementTests
{
    [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)]
    [TestCase(5)][TestCase(6)][TestCase(7)][TestCase(8)][TestCase(9)][TestCase(10)]
    public void EachRuleUnlocksOnlyAtItsThresholdAndOnlyOnce(int index)
    {
        var data=new PlayerData();var state=AchievementRules.State(data);
        state.requiredHangars.AddRange(new[]{1,2,3,4,5});state.requiredVehicleKeys.AddRange(new[]{"vehicle.key","v2","v3","v4","v5"});state.requiredChests.AddRange(new[]{"chest","c2","c3","c4","c5","c6"});
        Assert.IsFalse(AchievementRules.TryUnlock(data,index));
        switch(index)
        {
            case 0:state.collectedKilograms=4999.9;break;
            case 1:for(int i=0;i<49;i++)AchievementRules.AddUnique(state.closedBags,"bag"+i,100);break;
            case 2:state.clearedHangars.Add(1);break;
            case 3:state.clearedHangars.AddRange(new[]{1,2});break;
            case 4:data.foundSecrets=new[]{"vehicle.key","v2"};break;
            case 5:data.foundSecrets=new[]{"chest","c2"};break;
            case 6:state.crowsScared=24;break;
            case 7:state.ratsScared=49;break;
            case 8:for(int i=0;i<12;i++)AchievementRules.AddUnique(state.forkliftDeliveries,"delivery"+i,25);break;
            case 9:state.grainSaleCash=99999;break;
        }
        Assert.IsFalse(AchievementRules.TryUnlock(data,index));
        switch(index)
        {
            case 0:state.collectedKilograms=5000;break;case 1:AchievementRules.AddUnique(state.closedBags,"last",100);break;
            case 2:state.clearedHangars.Add(2);break;case 3:state.clearedHangars.Add(3);break;
            case 4:data.foundSecrets=new[]{"vehicle.key","v2","v3"};break;case 5:data.foundSecrets=new[]{"chest","c2","c3"};break;
            case 6:state.crowsScared=25;break;case 7:state.ratsScared=50;break;
            case 8:AchievementRules.AddUnique(state.forkliftDeliveries,"last",25);break;case 9:state.grainSaleCash=100000;break;
            case 10:state.electricalRoomPowered=true;break;
        }
        Assert.IsTrue(AchievementRules.TryUnlock(data,index));Assert.IsFalse(AchievementRules.TryUnlock(data,index));
        var restored=JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        Assert.IsTrue(AchievementRules.State(restored).unlocked.Contains(index));Assert.IsFalse(AchievementRules.TryUnlock(restored,index));
    }
    [Test] public void MissingCatalogCannotUnlockCollectionsOrAllHangars()
    {
        var data=new PlayerData();foreach(int index in new[]{3,4,5})Assert.IsFalse(AchievementRules.TryUnlock(data,index));
    }
    [Test] public void SameBagCannotBeFarmedForPackingOrDeliveries()
    {
        var state=new AchievementProgress();Assert.IsTrue(AchievementRules.AddUnique(state.closedBags,"one",100));
        Assert.IsFalse(AchievementRules.AddUnique(state.closedBags,"one",100));
        Assert.IsTrue(AchievementRules.AddUnique(state.forkliftDeliveries,"one",25));Assert.IsFalse(AchievementRules.AddUnique(state.forkliftDeliveries,"one",25));
        Assert.AreEqual(1,state.closedBags.Count);Assert.AreEqual(1,state.forkliftDeliveries.Count);
    }
    [Test] public void OldSavesMigrateWithoutInventingEarnedProgress()
    {
        var data=JsonUtility.FromJson<PlayerData>("{\"cash\":5000}");var state=AchievementRules.State(data);
        Assert.NotNull(state);Assert.IsEmpty(state.unlocked);Assert.AreEqual(0,AchievementRules.Current(data,9));
    }
    [Test] public void SteamIndexesAreStableAndTestAppIsNeverEligible()
    {
        var settings=ScriptableObject.CreateInstance<SteamAchievementSettings>();
        try
        {
            Assert.IsFalse(settings.HasRealAppId);settings.appId=480;Assert.IsFalse(settings.HasRealAppId);
            for(int i=0;i<11;i++){Assert.AreEqual(i,AchievementRules.Definitions[i].index);Assert.AreEqual("ACH_"+i,settings.ApiName(i));}
            Assert.IsNull(settings.ApiName(-1));Assert.IsNull(settings.ApiName(11));
        }
        finally{Object.DestroyImmediate(settings);}
    }
    [Test] public void DuplicateFoundSecretEntriesDoNotInflateCollectionProgress()
    {
        var data=new PlayerData();var state=AchievementRules.State(data);state.requiredVehicleKeys.AddRange(new[]{"a","b","c","d"});
        data.foundSecrets=new[]{"a","a","a"};Assert.AreEqual(1,AchievementRules.Current(data,4));Assert.IsFalse(AchievementRules.TryUnlock(data,4));
    }
    [TestCase(0,0)][TestCase(1,1)][TestCase(5,3)][TestCase(6,3)]
    public void CollectionTargetsUseHalfRoundedUp(int total,int target)
    {
        var state=new AchievementProgress();
        for(int i=0;i<total;i++){state.requiredHangars.Add(i+1);state.requiredVehicleKeys.Add("v"+i);state.requiredChests.Add("c"+i);}
        foreach(int index in new[]{3,4,5})Assert.AreEqual(target,AchievementRules.Target(state,index));
    }}
