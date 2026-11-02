var manager=UnityEngine.Object.FindFirstObjectByType<GoalsManager>(UnityEngine.FindObjectsInactive.Include);
var data=new UnityEditor.SerializedObject(manager);var list=data.FindProperty("allGoalSettings");
for(int i=0;i<list.arraySize;i++){var p=list.GetArrayElementAtIndex(i);
if(!p.FindPropertyRelative("goalId").stringValue.StartsWith("tutorial_"))continue;
p.FindPropertyRelative("hangarId").intValue=0;p.FindPropertyRelative("filterByGrainType").boolValue=false;
}
data.ApplyModifiedProperties();UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(manager.gameObject.scene);
return "Tutorial accepts starter sack actions regardless of current parent/source metadata.";
