var report=new System.Text.StringBuilder();
foreach(var manager in UnityEngine.Object.FindObjectsByType<GoalsManager>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)){
report.AppendLine(manager.name+" "+manager.gameObject.scene.path);
var list=new UnityEditor.SerializedObject(manager).FindProperty("allGoalSettings");
for(int i=0;i<list.arraySize;i++){var p=list.GetArrayElementAtIndex(i);report.AppendLine(i+" "+p.FindPropertyRelative("goalId").stringValue+" | "+p.FindPropertyRelative("goalDescription").stringValue+" | type="+p.FindPropertyRelative("goalType").intValue+" target="+p.FindPropertyRelative("goalAmount").floatValue+" upgrade="+p.FindPropertyRelative("upgradeType").intValue+" reward="+p.FindPropertyRelative("rewardAmount").intValue);}
}
var settings=GrainEconomySettings.Default;
report.AppendLine("Motoroller "+settings.vehicleSettings.Find("motoroller").price+" key="+settings.vehicleSettings.Find("motoroller").requiresKey);
foreach(var c in UnityEngine.Object.FindObjectsByType<GrainContainer>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None))if(c.CargoTypeId=="FreeBag")report.AppendLine(c.name+" kg="+c.CapacityKg);
System.IO.File.WriteAllText("output/EarlyUpgrades/before.txt",report.ToString());return report.ToString();
