if(UnityEngine.Application.isPlaying)throw new System.Exception("Stop Play mode before changing authored progression.");
var economy=GrainEconomySettings.Default;
UnityEditor.Undo.RecordObject(economy.shovelSettings,"Lower starter shovel prices");
economy.shovelSettings.shovels[0].prices=new[]{10,15,20,25};
UnityEditor.EditorUtility.SetDirty(economy.shovelSettings);UnityEditor.AssetDatabase.SaveAssetIfDirty(economy.shovelSettings);
UnityEditor.Undo.RecordObject(economy.vehicleSettings,"Lower Motoroller price");
economy.vehicleSettings.Find("motoroller").price=25;
UnityEditor.EditorUtility.SetDirty(economy.vehicleSettings);UnityEditor.AssetDatabase.SaveAssetIfDirty(economy.vehicleSettings);
var manager=UnityEngine.Object.FindFirstObjectByType<GoalsManager>(UnityEngine.FindObjectsInactive.Include);
var data=new UnityEditor.SerializedObject(manager);
var goals=data.FindProperty("allGoalSettings");
int sale=-1,upgrade=-1;
for(int i=0;i<goals.arraySize;i++){
var goal=goals.GetArrayElementAtIndex(i);string id=goal.FindPropertyRelative("goalId").stringValue;
if(id=="tutorial_load_200kg_corn"){goal.FindPropertyRelative("goalAmount").floatValue=10;goal.FindPropertyRelative("goalDescription").stringValue="Load your first 10 kg sack onto the bicycle";}
if(id=="tutorial_load_sack")goal.FindPropertyRelative("goalDescription").stringValue="Load the grain sack onto the bicycle";
if(id=="tutorial_sell_corn"){sale=i;goal.FindPropertyRelative("hangarId").intValue=0;goal.FindPropertyRelative("filterByGrainType").boolValue=false;}
if(id=="tutorial_upgrade_shovel")upgrade=i;
}
if(sale<0)throw new System.Exception("First sale tutorial missing.");
if(upgrade<0){goals.InsertArrayElementAtIndex(sale+1);upgrade=sale+1;}
var added=goals.GetArrayElementAtIndex(upgrade);
added.FindPropertyRelative("goalId").stringValue="tutorial_upgrade_shovel";
added.FindPropertyRelative("goalDescription").stringValue="Visit the shop and upgrade your shovel for $10";
added.FindPropertyRelative("stage").intValue=0;
added.FindPropertyRelative("hangarId").intValue=0;
added.FindPropertyRelative("goalType").intValue=13;
added.FindPropertyRelative("upgradeType").intValue=6;
added.FindPropertyRelative("filterByGrainType").boolValue=false;
added.FindPropertyRelative("progressMode").intValue=1;
added.FindPropertyRelative("goalAmount").floatValue=1;
added.FindPropertyRelative("rewardAmount").intValue=0;
data.ApplyModifiedProperties();
UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(manager.gameObject.scene);
return "Saved first shovel prices 10/15/20/25; Motoroller 25; one-bag tutorial followed by shovel upgrade.";
