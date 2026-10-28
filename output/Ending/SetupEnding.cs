using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class SetupEnding {
 public static object Run() {
  if(EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Last Scene.unity") throw new System.Exception("Expected Last Scene in edit mode");
  const string root="Assets/Models/Rocket Modular/";
  var prefab=PrefabUtility.LoadPrefabContents(root+"Rocket Modular.prefab");
  var existing=prefab.transform.Find("Engine exhaust VFX"); if(existing!=null) Object.DestroyImmediate(existing.gameObject);
  var fx=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/Epic Toon FX/Prefabs/Combat/Missiles/Rocket/RocketMissileFire.prefab"));
  fx.name="Engine exhaust VFX"; fx.transform.SetParent(prefab.transform,false); fx.transform.localPosition=new Vector3(0,.25f,0); fx.transform.localRotation=Quaternion.Euler(-90,0,0); fx.transform.localScale=Vector3.one*.65f;
  foreach(var p in fx.GetComponentsInChildren<ParticleSystem>(true)) { var m=p.main; m.scalingMode=ParticleSystemScalingMode.Hierarchy; m.playOnAwake=true; }
  PrefabUtility.SaveAsPrefabAsset(prefab,root+"Rocket Modular.prefab");
  Object.DestroyImmediate(prefab.transform.Find("Interior Devices").gameObject);
  Object.DestroyImmediate(prefab.transform.Find("Entrance Stairs").gameObject);
  foreach(var c in prefab.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
  PrefabUtility.SaveAsPrefabAsset(prefab,root+"Rocket Fleet.prefab"); PrefabUtility.UnloadPrefabContents(prefab);
  var go=GameObject.Find("Ending Cutscene")??new GameObject("Ending Cutscene");
  var cscene=go.GetComponent<RocketEndingCutscene>()??go.AddComponent<RocketEndingCutscene>(); var so=new SerializedObject(cscene);
  so.FindProperty("rocketPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Rocket Modular.prefab");
  so.FindProperty("fleetPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Rocket Fleet.prefab");
  so.FindProperty("earth").objectReferenceValue=GameObject.Find("Earth (1)").transform;
  so.FindProperty("departureReference").objectReferenceValue=GameObject.Find("rocket").transform;
  so.FindProperty("sourceCamera").objectReferenceValue=GameObject.Find("Camera").GetComponent<Camera>();
  so.FindProperty("sun").objectReferenceValue=GameObject.Find("Directional Light").GetComponent<Light>();
  var arr=so.FindProperty("hideDuringCutscene"); arr.arraySize=3; string[] names={"rocket","Rocket preview temporary","Audio Game"}; for(int i=0;i<3;i++)arr.GetArrayElementAtIndex(i).objectReferenceValue=GameObject.Find(names[i]);
  so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(cscene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(go.scene);
  return "Ending scene saved; rocket exhaust and exterior fleet prefab assigned.";
 }
}
