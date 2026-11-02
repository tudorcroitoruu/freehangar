var settings=GrainEconomySettings.Default;
var report=new System.Text.StringBuilder();
for(int i=0;i<4;i++)if(settings.shovelSettings.Price(i)!=10+5*i)throw new System.Exception("Wrong shovel price");
var motor=settings.vehicleSettings.Find("motoroller");
if(motor.price!=25 || motor.requiresKey)throw new System.Exception("Motoroller is not available for $25");
settings.TryGetGrain(GrainType.Corn,out var corn);
int sale=EconomyNumberFormat.SaleTotal(new System.Collections.Generic.List<double>{EconomyNumberFormat.CropValue(10,corn.grainPrice)});
var profile=new PlayerData{cash=sale};
if(!profile.TryUpgradeShovel(settings.shovelSettings.Price(0),0,settings.shovelSettings.MaxUpgrades))throw new System.Exception("First sack cannot fund shovel");
profile.cash+=sale*2;
if(!settings.vehicleSettings.TryPurchase(profile,"bicycle"))throw new System.Exception("Three sacks cannot fund Motoroller after shovel");
report.AppendLine("PASS 10 kg corn sale=$"+sale+"; first sale funds shovel; three sales fund Motoroller after buying shovel.");
var source=UnityEngine.Object.FindFirstObjectByType<GoalsManager>(UnityEngine.FindObjectsInactive.Include);
var field=typeof(GoalsManager).GetField("allGoalSettings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
var goals=(GoalSettings[])field.GetValue(source);
var go=new UnityEngine.GameObject("Tutorial validation");go.SetActive(false);
var manager=go.AddComponent<GoalsManager>();field.SetValue(manager,goals);
var oldData=SaveSystem.Data;bool oldLoaded=SaveSystem.savesLoaded;
try{
SaveSystem.savesLoaded=false;SaveSystem.Data=new PlayerData();manager.Initialize(SaveSystem.Data);
foreach(var type in new[]{GoalType.SackPickedUp,GoalType.SackPlaced,GoalType.ShovelEquipped,GoalType.SackFilled,GoalType.SackClosed,GoalType.SackLoaded,GoalType.GrainLoaded,GoalType.BuyerReached,GoalType.GrainSold}){
bool value=type==GoalType.ShovelEquipped||type==GoalType.SackFilled||type==GoalType.BuyerReached;
manager.ProcessAction(new GoalAction(type,type==GoalType.SackFilled||type==GoalType.GrainLoaded?10:1,0,null,GoalUpgradeType.None,value,null));
}
if(manager.CurrentGoal.goalId!="tutorial_upgrade_shovel")throw new System.Exception("Tutorial stuck at "+manager.CurrentGoal.goalId);
manager.ProcessAction(new GoalAction(GoalType.UpgradePurchased,1,0,null,GoalUpgradeType.Shovel,true,null));
if(manager.CurrentGoal.goalId=="tutorial_upgrade_shovel")throw new System.Exception("Upgrade did not finish tutorial goal");
report.AppendLine("PASS authored tutorial advances from one bag to shovel upgrade, then completes on purchase.");
var prior=new PlayerData{shovelUpgradeLevel=1};
prior.goals=new GoalSettingsSave[9];
for(int i=0;i<9;i++)prior.goals[i]=new GoalSettingsSave{goalId=goals[i].goalId,completed=true,rewardClaimed=true};
SaveSystem.Data=prior;manager.Initialize(prior);
if(manager.CurrentGoal.goalId=="tutorial_upgrade_shovel")throw new System.Exception("Existing shovel upgrade was not recognized");
report.AppendLine("PASS existing saves with a purchased shovel skip the new upgrade objective.");
}finally{UnityEngine.Object.DestroyImmediate(go);SaveSystem.Data=oldData;SaveSystem.savesLoaded=oldLoaded;}
System.IO.File.WriteAllText("output/EarlyUpgrades/validation.txt",report.ToString());return report.ToString();
